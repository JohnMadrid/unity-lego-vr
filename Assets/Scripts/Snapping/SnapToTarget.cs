using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(Rigidbody), typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class SnapToTarget : MonoBehaviour
{
    public float snapRange = 0.1f;
    public Transform snapTop;
    public Transform snapBottom;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
    private Rigidbody rb;

    void Awake()
    {
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
    }

    void OnEnable()
    {
        grabInteractable.selectExited.AddListener(OnRelease);
    }

    void OnDisable()
    {
        grabInteractable.selectExited.RemoveListener(OnRelease);
    }

    void OnRelease(SelectExitEventArgs args)
    {
        // Make sure it's not parented to any interactor or object
        transform.SetParent(null);

        SnapToNearestCube();
    }

    void SnapToNearestCube()
    {
        GameObject[] allCubes = GameObject.FindGameObjectsWithTag("Snappable");

        foreach (GameObject other in allCubes)
        {
            if (other == gameObject) continue;

            SnapToTarget otherSnap = other.GetComponent<SnapToTarget>();
            if (otherSnap == null) continue;

            float dist = Vector3.Distance(otherSnap.snapTop.position, snapBottom.position);
            if (dist < snapRange)
            {
                // Align position
                Vector3 offset = snapBottom.position - transform.position;
                Vector3 targetPosition = otherSnap.snapTop.position - offset;

                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.MovePosition(targetPosition);
                transform.rotation = other.transform.rotation;

                // Add a FixedJoint to connect both objects
                FixedJoint joint = gameObject.AddComponent<FixedJoint>();
                joint.connectedBody = other.GetComponent<Rigidbody>();

                // Optional: tune joint for stability
                joint.breakForce = Mathf.Infinity;
                joint.breakTorque = Mathf.Infinity;

                break;
            }
        }
    }
}
