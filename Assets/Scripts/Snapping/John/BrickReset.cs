using UnityEngine;
using System.Collections;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Script that makes bricks return to resource table f accidentally dropped on floor by participant.

public class BrickReset : MonoBehaviour
{
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    private BrickBehavior _brickBehavior;

    public float resetDelay = 0.2f;
    public float resetDuration = 0.5f;

    private bool _isResetting = false;
    
    // Debug settings
    [Header("Debug Settings")]
    public bool enableDebugLogging = false;

    void Start()
    {
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
        _brickBehavior = GetComponent<BrickBehavior>();
        
        bool isBoard = gameObject.CompareTag("Board");
        if (_brickBehavior != null && _isResetting)
        {
            _brickBehavior.LogDebug("[BrickReset] DEBUG: Initialized at position " + _initialPosition, true); // ExtensiveDebug
            _brickBehavior.LogDebug("[BrickReset] DEBUG: IsBoard: " + isBoard + ", Tag: " + gameObject.tag, true); // ExtensiveDebug
            Rigidbody rb = GetComponent<Rigidbody>();
            Collider col = GetComponent<Collider>();
            _brickBehavior.LogDebug("[BrickReset] DEBUG: Rigidbody: " + (rb != null) + ", Collider: " + (col != null) + ", BrickBehavior: " + (_brickBehavior != null), true); // ExtensiveDebug
            if (rb != null)
                _brickBehavior.LogDebug("[BrickReset] DEBUG: Rigidbody - isKinematic: " + rb.isKinematic + ", useGravity: " + rb.useGravity, true); // ExtensiveDebug
            if (col != null)
                _brickBehavior.LogDebug("[BrickReset] DEBUG: Collider - isTrigger: " + col.isTrigger + ", enabled: " + col.enabled, true); // ExtensiveDebug
        }
        else if (enableDebugLogging && _isResetting)
        {
            Debug.Log($"[BrickReset] {gameObject.name}: Initialized at position {_initialPosition}");
            Debug.Log($"[BrickReset] {gameObject.name}: IsBoard: {isBoard}, Tag: {gameObject.tag}");
            Rigidbody rb = GetComponent<Rigidbody>();
            Collider col = GetComponent<Collider>();
            Debug.Log($"[BrickReset] {gameObject.name}: Rigidbody: {rb != null}, Collider: {col != null}, BrickBehavior: {_brickBehavior != null}");
            if (rb != null)
                Debug.Log($"[BrickReset] {gameObject.name}: Rigidbody - isKinematic: {rb.isKinematic}, useGravity: {rb.useGravity}");
            if (col != null)
                Debug.Log($"[BrickReset] {gameObject.name}: Collider - isTrigger: {col.isTrigger}, enabled: {col.enabled}");
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_brickBehavior != null && _isResetting)
        {
            _brickBehavior.LogDebug($"[BrickReset] {gameObject.name}: Collision detected with {collision.gameObject.name} (Tag: {collision.gameObject.tag})", false); // NormalDebug
            _brickBehavior.LogDebug($"[BrickReset] {gameObject.name}: Current state - isResetting: {_isResetting}", false); // NormalDebug
        }
        else if (enableDebugLogging && _isResetting)
        {
            Debug.Log($"[BrickReset] {gameObject.name}: Collision detected with {collision.gameObject.name} (Tag: {collision.gameObject.tag})");
            Debug.Log($"[BrickReset] {gameObject.name}: Current state - isResetting: {_isResetting}");
        }
        
        if (collision.gameObject.CompareTag("Floor") && !_isResetting)
        {
            if (_brickBehavior != null)
                _brickBehavior.LogDebug($"[BrickReset] {gameObject.name}: Starting reset routine - brick hit floor", false); // LiteDebug
            else if (enableDebugLogging)
            Debug.Log($"[BrickReset] {gameObject.name}: Starting reset routine - brick hit floor");
            InitiateGroupReset();
        }
        else if (_isResetting)
        {
            if (_brickBehavior != null)
                _brickBehavior.LogDebug($"[BrickReset] {gameObject.name}: Ignoring collision - already resetting", false); // NormalDebug
            else if (enableDebugLogging)
                Debug.Log($"[BrickReset] {gameObject.name}: Ignoring collision - already resetting");
        }
        else if (!collision.gameObject.CompareTag("Floor"))
        {
            if (_brickBehavior != null)
                _brickBehavior.LogDebug($"[BrickReset] {gameObject.name}: Ignoring collision - not with floor (tag: {collision.gameObject.tag})", false); // NormalDebug
            else if (enableDebugLogging)
                Debug.Log($"[BrickReset] {gameObject.name}: Ignoring collision - not with floor (tag: {collision.gameObject.tag})");
        }
    }
    
    // Prevent floor objects from grabbing this brick
    void OnTriggerEnter(Collider other)
    {
        if (_brickBehavior != null && _isResetting)
        {
            _brickBehavior.LogDebug($"[BrickReset] {gameObject.name}: Trigger detected with {other.gameObject.name} (Tag: {other.gameObject.tag})", false); // NormalDebug
        }
        else if (enableDebugLogging && _isResetting)
        {
            Debug.Log($"[BrickReset] {gameObject.name}: Trigger detected with {other.gameObject.name} (Tag: {other.gameObject.tag})");
        }
        
        if (other.CompareTag("Floor"))
        {
            var floorGrabInteractable = other.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (floorGrabInteractable != null)
            {
                floorGrabInteractable.enabled = false;
                if (_brickBehavior != null && _isResetting)
                    _brickBehavior.LogDebug($"[BrickReset] {gameObject.name}: Disabled XR Grab Interactable on floor object {other.name}", false); // NormalDebug
                else if (enableDebugLogging && _isResetting)
                    Debug.Log($"[BrickReset] {gameObject.name}: Disabled XR Grab Interactable on floor object {other.name}");
                }
            else if (_brickBehavior != null && _isResetting)
            {
                _brickBehavior.LogDebug($"[BrickReset] {gameObject.name}: Floor object {other.name} has no XR Grab Interactable", false); // NormalDebug
            }
            else if (enableDebugLogging && _isResetting)
            {
                Debug.Log($"[BrickReset] {gameObject.name}: Floor object {other.name} has no XR Grab Interactable");
            }
        }
    }

    public void InitiateGroupReset()
    {
        if (_isResetting)
        {
            if (_brickBehavior != null && _isResetting)
                _brickBehavior.LogDebug($"[BrickReset] {gameObject.name}: Group reset already in progress, ignoring request.", false); // NormalDebug
            else if (enableDebugLogging && _isResetting)
                Debug.Log($"[BrickReset] {gameObject.name}: Group reset already in progress, ignoring request.");
            return;
        }

        if (_brickBehavior == null)
        {
            if (_isResetting)
            {
                if (_brickBehavior != null)
                    _brickBehavior.LogWarning($"[BrickReset] {gameObject.name}: Cannot initiate group reset, BrickBehavior is null.", false); // LiteDebug
                else if (enableDebugLogging)
                Debug.LogWarning($"[BrickReset] {gameObject.name}: Cannot initiate group reset, BrickBehavior is null.");
            }
            StartCoroutine(ResetRoutine(new System.Collections.Generic.List<BrickReset> { this }));
            return;
        }
        
        var groupMembers = new System.Collections.Generic.List<BrickBehavior>();
        BrickGroupUtils.FindAllConnectedInGroup(_brickBehavior, groupMembers, name);
        
        // NEW: If group has more than 2 bricks, do not reset
        if (groupMembers.Count > 2)
        {
            if (_brickBehavior != null)
                _brickBehavior.LogDebug($"[BrickReset] {gameObject.name}: Group has more than 2 bricks ({groupMembers.Count}), not resetting.", false);
            else if (enableDebugLogging)
                Debug.Log($"[BrickReset] {gameObject.name}: Group has more than 2 bricks ({groupMembers.Count}), not resetting.");
            return;
        }

        var resetTargets = new System.Collections.Generic.List<BrickReset>();
        foreach (var member in groupMembers)
        {
            var resetter = member.GetComponent<BrickReset>();
            if (resetter != null)
            {
                resetTargets.Add(resetter);
            }
        }

        StartCoroutine(ResetRoutine(resetTargets));
    }

    private IEnumerator ResetRoutine(System.Collections.Generic.List<BrickReset> bricksToReset)
    {
        // Set resetting flag for all bricks in the group
        foreach (var brick in bricksToReset)
        {
            brick._isResetting = true;
        }

        if (_brickBehavior != null && _isResetting)
            _brickBehavior.LogDebug($"[BrickReset] {gameObject.name}: Reset routine started for group of {bricksToReset.Count} bricks.", false); // LiteDebug
        else if (enableDebugLogging && _isResetting)
            Debug.Log($"[BrickReset] {gameObject.name}: Reset routine started for group of {bricksToReset.Count} bricks.");

        yield return new WaitForSeconds(resetDelay);
        
        if (_brickBehavior != null && _isResetting)
            _brickBehavior.LogDebug($"[BrickReset] {gameObject.name}: Reset delay completed, starting movement for group.", false); // LiteDebug
        else if (enableDebugLogging && _isResetting)
            Debug.Log($"[BrickReset] {gameObject.name}: Reset delay completed, starting movement for group.");

        // --- NEW: Calculate target rotation for the group to be upright ---
        var initiator = this;
        // Find the rotation required to make the initiator's "up" vector point to the world's "up"
        Quaternion deltaRotation = Quaternion.FromToRotation(initiator._initialRotation * Vector3.up, Vector3.up);

        // --- NEW: Add the requested final rotation ---
        Quaternion finalAdjustmentRotation = Quaternion.Euler(90, 0, 0);

        // Store initial positions and target rotations for all bricks in the group
        var startPositions = new System.Collections.Generic.Dictionary<BrickReset, Vector3>();
        var startRotations = new System.Collections.Generic.Dictionary<BrickReset, Quaternion>();
        var targetRotations = new System.Collections.Generic.Dictionary<BrickReset, Quaternion>();

        foreach (var brick in bricksToReset)
        {
            startPositions[brick] = brick.transform.position;
            startRotations[brick] = brick.transform.rotation;
            // Apply the upright delta and the final adjustment to each brick's initial rotation
            targetRotations[brick] = finalAdjustmentRotation * deltaRotation * brick._initialRotation;
        }

        float elapsed = 0f;
        while (elapsed < resetDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / resetDuration);

            foreach (var brick in bricksToReset)
            {
                brick.transform.position = Vector3.Lerp(startPositions[brick], brick._initialPosition, t);
                brick.transform.rotation = Quaternion.Slerp(startRotations[brick], targetRotations[brick], t);
            }
            
            yield return null;
        }

        // Ensure exact final positions and rotations
        foreach (var brick in bricksToReset)
        {
            brick.transform.position = brick._initialPosition;
            brick.transform.rotation = targetRotations[brick];

            Rigidbody rb = brick.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
        
        // Reset the flag for all bricks
        foreach (var brick in bricksToReset)
        {
            brick._isResetting = false;
        }

        if (_brickBehavior != null && _isResetting)
            _brickBehavior.LogDebug($"[BrickReset] {gameObject.name}: Group reset routine finished.", false); // LiteDebug
        else if (enableDebugLogging && _isResetting)
            Debug.Log($"[BrickReset] {gameObject.name}: Group reset routine finished.");
    }
}