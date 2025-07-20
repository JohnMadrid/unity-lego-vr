using UnityEngine;
using System.Collections;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Script that makes bricks return to resource table f accidentally dropped on floor by participant.

public class BrickReset : MonoBehaviour
{
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;

    public float resetDelay = 0.2f;
    public float resetDuration = 1000f;

    private bool _isResetting = false;
    
    // Debug settings
    [Header("Debug Settings")]
    public bool enableDebugLogging = false;

    void Start()
    {
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
        
        // Check if this is a board or brick
        bool isBoard = gameObject.CompareTag("Board");
        if (enableDebugLogging)
        {
            Debug.Log($"[BrickReset] {gameObject.name}: Initialized at position {_initialPosition}");
            Debug.Log($"[BrickReset] {gameObject.name}: IsBoard: {isBoard}, Tag: {gameObject.tag}");
            
            // Check for required components
            Rigidbody rb = GetComponent<Rigidbody>();
            Collider col = GetComponent<Collider>();
            var brickBehavior = GetComponent<BrickBehavior>();
            
            Debug.Log($"[BrickReset] {gameObject.name}: Rigidbody: {rb != null}, Collider: {col != null}, BrickBehavior: {brickBehavior != null}");
            
            if (rb != null)
            {
                Debug.Log($"[BrickReset] {gameObject.name}: Rigidbody - isKinematic: {rb.isKinematic}, useGravity: {rb.useGravity}");
            }
            
            if (col != null)
            {
                Debug.Log($"[BrickReset] {gameObject.name}: Collider - isTrigger: {col.isTrigger}, enabled: {col.enabled}");
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[BrickReset] {gameObject.name}: Collision detected with {collision.gameObject.name} (Tag: {collision.gameObject.tag})");
            Debug.Log($"[BrickReset] {gameObject.name}: Current state - isResetting: {_isResetting}");
        }
        
        if (collision.gameObject.CompareTag("Floor") && !_isResetting)
        {
            //if (enableDebugLogging)
            //{
            Debug.Log($"[BrickReset] {gameObject.name}: Starting reset routine - brick hit floor");
            //}
            StartCoroutine(ResetRoutine());
        }
        else if (_isResetting)
        {
            if (enableDebugLogging)
            {
                Debug.Log($"[BrickReset] {gameObject.name}: Ignoring collision - already resetting");
            }
        }
        else if (!collision.gameObject.CompareTag("Floor"))
        {
            if (enableDebugLogging)
            {
                Debug.Log($"[BrickReset] {gameObject.name}: Ignoring collision - not with floor (tag: {collision.gameObject.tag})");
            }
        }
    }
    
    // Prevent floor objects from grabbing this brick
    void OnTriggerEnter(Collider other)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[BrickReset] {gameObject.name}: Trigger detected with {other.gameObject.name} (Tag: {other.gameObject.tag})");
        }
        
        if (other.CompareTag("Floor"))
        {
            // If the floor object has XR grab interactable, disable it
            var floorGrabInteractable = other.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (floorGrabInteractable != null)
            {
                floorGrabInteractable.enabled = false;
                if (enableDebugLogging)
                {
                    Debug.Log($"[BrickReset] {gameObject.name}: Disabled XR Grab Interactable on floor object {other.name}");
                }
            }
            else if (enableDebugLogging)
            {
                Debug.Log($"[BrickReset] {gameObject.name}: Floor object {other.name} has no XR Grab Interactable");
            }
        }
    }

    private IEnumerator ResetRoutine()
    {
        //if (enableDebugLogging)
        //{
        Debug.Log($"[BrickReset] {gameObject.name}: Reset routine started");
        Debug.Log($"[BrickReset] {gameObject.name}: Current position: {transform.position}, Target: {_initialPosition}");
        //}
        
        _isResetting = true;

        yield return new WaitForSeconds(resetDelay);
        
        if (enableDebugLogging)
        {
            Debug.Log($"[BrickReset] {gameObject.name}: Reset delay completed, starting movement");
        }

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float elapsed = 0f;

        if (enableDebugLogging)
        {
            Debug.Log($"[BrickReset] {gameObject.name}: Moving from {startPos} to {_initialPosition}");
        }

        while (elapsed < resetDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / resetDuration);
            transform.position = Vector3.Lerp(startPos, _initialPosition, t);
            transform.rotation = Quaternion.Slerp(startRot, _initialRotation, t);
            
            // Log progress every 10 frames
            if (enableDebugLogging && Mathf.FloorToInt(elapsed * 60) % 10 == 0)
            {
                Debug.Log($"[BrickReset] {gameObject.name}: Reset progress - {t:P0} complete, position: {transform.position}");
            }
            
            yield return null;
        }

        // ensure exact position/rotation after interpolation
        transform.position = _initialPosition;
        transform.rotation = _initialRotation;

        //if (enableDebugLogging)
        //{
        Debug.Log($"[BrickReset] {gameObject.name}: Reset complete - position: {transform.position}");
        //}

        // stop velocities to prevent drifting
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            if (enableDebugLogging)
            {
                Debug.Log($"[BrickReset] {gameObject.name}: Rigidbody velocities reset to zero");
            }
        }
        else if (enableDebugLogging)
        {
            Debug.LogWarning($"[BrickReset] {gameObject.name}: No Rigidbody found on brick");
        }

        _isResetting = false;
        
        //if (enableDebugLogging)
        //{
        Debug.Log($"[BrickReset] {gameObject.name}: Reset routine finished");
        //}
    }
}