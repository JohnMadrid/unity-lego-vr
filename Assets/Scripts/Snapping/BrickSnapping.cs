using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class BrickSnapping : MonoBehaviour
{
    [Tooltip("How far from the snap point we still allow snapping (in metres). "
           + "Set to 0 if you size the trigger colliders exactly as you want.")]
    public float snapRadius = 0.0f;

    private Transform snapTarget;               // Current candidate snap point
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
    private bool isGrabbed;

    void Awake()
    {
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        grabInteractable.selectEntered.AddListener(OnGrab);
        grabInteractable.selectExited.AddListener(OnRelease);
    }

    /* ---------- Trigger callbacks ---------- */

    void OnTriggerEnter(Collider other)
    {
        // Are we colliding with ANY snap point?
        if (!other.CompareTag("SnapPointTop") && !other.CompareTag("SnapPointBottom"))
            return;

        // If we’re holding the brick right now, ignore (user is still moving it)
        if (isGrabbed)
            return;

        // First candidate or closer one?
        if (snapTarget == null ||
            (snapRadius > 0 &&
             Vector3.SqrMagnitude(transform.position - other.transform.position)
             < snapRadius * snapRadius))
        {
            snapTarget = other.transform;
            // Optional:   Debug.Log($"Snap target set to {snapTarget.name}");
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.transform == snapTarget)
            snapTarget = null;
    }

    /* ---------- Grab callbacks ---------- */

    void OnGrab(SelectEnterEventArgs _)  => isGrabbed = true;

    void OnRelease(SelectExitEventArgs _)
    {
        isGrabbed = false;

        if (snapTarget != null)
        {
            // Move the brick root so its own top/bottom aligns exactly with the snap point
            transform.SetPositionAndRotation(snapTarget.position, snapTarget.rotation);

            // Optionally: parent it so it stays put if the snapped-to brick moves
            // transform.SetParent(snapTarget);

            snapTarget = null;
        }
    }
}
