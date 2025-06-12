using UnityEngine;
using System.Collections;

public class BrickReset : MonoBehaviour
{
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;

    public float resetDelay = 0.2f;
    public float resetDuration = 0.5f;

    private bool _isResetting = false;

    void Start()
    {
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Floor") && !_isResetting)
        {
            StartCoroutine(ResetRoutine());
        }
    }

    private IEnumerator ResetRoutine()
    {
        _isResetting = true;

        yield return new WaitForSeconds(resetDelay);

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float elapsed = 0f;

        while (elapsed < resetDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / resetDuration);
            transform.position = Vector3.Lerp(startPos, _initialPosition, t);
            transform.rotation = Quaternion.Slerp(startRot, _initialRotation, t);
            yield return null;
        }

        // ensure exact position/rotation after interpolation
        transform.position = _initialPosition;
        transform.rotation = _initialRotation;

        // stop velocities to prevent drifting
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        _isResetting = false;
    }
}