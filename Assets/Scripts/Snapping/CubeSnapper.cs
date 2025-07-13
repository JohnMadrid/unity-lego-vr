using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(Rigidbody), typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class CubeSnapper : MonoBehaviour
{
    [Header("Snap Settings")]
    public Transform topSnapPoint;
    public Transform bottomSnapPoint;
    public float snapDistance = 0.05f;
    public float disconnectDistance = 0.15f;

    private FixedJoint topJoint;
    private FixedJoint bottomJoint;

    private CubeSnapper topConnected;
    private CubeSnapper bottomConnected;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
    private bool isGrabbed;

    private List<System.Action> jointCleanupQueue = new List<System.Action>();

    void Start()
    {
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        grabInteractable.selectEntered.AddListener(_ => isGrabbed = true);
        grabInteractable.selectExited.AddListener(_ => isGrabbed = false);

        // Optional: Improve physics fidelity
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    void OnCollisionStay(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Cube")) return;

        CubeSnapper other = collision.gameObject.GetComponent<CubeSnapper>();
        if (other == null) return;

        TrySnapWith(other);
    }

    void TrySnapWith(CubeSnapper other)
    {
        if (topJoint != null && bottomJoint != null) return;

        float verticalOffset = transform.position.y - other.transform.position.y;

        if (Mathf.Abs(verticalOffset) < 0.01f) return;

        // Connect this.bottom to other.top
        if (verticalOffset > 0 && bottomJoint == null && other.topJoint == null)
        {
            float dist = Vector3.Distance(bottomSnapPoint.position, other.topSnapPoint.position);
            if (dist < snapDistance && (!isGrabbed || !other.isGrabbed))
            {
                AlignTo(
                    other.topSnapPoint.position - (bottomSnapPoint.position - transform.position),
                    other.transform.rotation
                );
                CreateJointTo(other, isBottom: true);
            }
        }
        // Connect this.top to other.bottom
        else if (verticalOffset < 0 && topJoint == null && other.bottomJoint == null)
        {
            float dist = Vector3.Distance(topSnapPoint.position, other.bottomSnapPoint.position);
            if (dist < snapDistance && (!isGrabbed || !other.isGrabbed))
            {
                AlignTo(
                    other.bottomSnapPoint.position - (topSnapPoint.position - transform.position),
                    other.transform.rotation
                );
                CreateJointTo(other, isBottom: false);
            }
        }
    }

    void AlignTo(Vector3 targetPosition, Quaternion targetRotation)
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // First snap rotation
        rb.MoveRotation(targetRotation);

        // Then snap position
        rb.MovePosition(targetPosition);
    }

    void CreateJointTo(CubeSnapper other, bool isBottom)
    {
        FixedJoint joint = gameObject.AddComponent<FixedJoint>();
        joint.connectedBody = other.GetComponent<Rigidbody>();
        joint.autoConfigureConnectedAnchor = false;

        if (isBottom)
        {
            joint.anchor = transform.InverseTransformPoint(bottomSnapPoint.position);
            joint.connectedAnchor = other.transform.InverseTransformPoint(other.topSnapPoint.position);

            bottomJoint = joint;
            bottomConnected = other;

            other.topJoint = joint;
            other.topConnected = this;
        }
        else
        {
            joint.anchor = transform.InverseTransformPoint(topSnapPoint.position);
            joint.connectedAnchor = other.transform.InverseTransformPoint(other.bottomSnapPoint.position);

            topJoint = joint;
            topConnected = other;

            other.bottomJoint = joint;
            other.bottomConnected = this;
        }
    }

    void FixedUpdate()
    {
        CheckForManualBreak();
    }

    void LateUpdate()
    {
        foreach (var action in jointCleanupQueue)
            action.Invoke();
        jointCleanupQueue.Clear();
    }

    void CheckForManualBreak()
    {
        if (topJoint != null && topConnected != null && isGrabbed && topConnected.isGrabbed)
        {
            float dist = Vector3.Distance(topSnapPoint.position, topConnected.bottomSnapPoint.position);
            if (dist > disconnectDistance)
            {
                QueueBreakTopJoint();
                topConnected.QueueBreakBottomJoint();
            }
        }

        if (bottomJoint != null && bottomConnected != null && isGrabbed && bottomConnected.isGrabbed)
        {
            float dist = Vector3.Distance(bottomSnapPoint.position, bottomConnected.topSnapPoint.position);
            if (dist > disconnectDistance)
            {
                QueueBreakBottomJoint();
                bottomConnected.QueueBreakTopJoint();
            }
        }
    }

    void QueueBreakTopJoint()
    {
        if (topJoint != null)
        {
            var jointToDestroy = topJoint;
            jointCleanupQueue.Add(() => Destroy(jointToDestroy));
            topJoint = null;
            topConnected = null;
        }
    }

    void QueueBreakBottomJoint()
    {
        if (bottomJoint != null)
        {
            var jointToDestroy = bottomJoint;
            jointCleanupQueue.Add(() => Destroy(jointToDestroy));
            bottomJoint = null;
            bottomConnected = null;
        }
    }
}
