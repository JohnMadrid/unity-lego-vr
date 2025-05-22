using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class BrickSnapXR : MonoBehaviour
{
    public float snapRadius = 0.2f; // Snapping range
    private Transform snapTarget; // Stores closest snap point
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable; // XR Interactable component

    void Start()
    {
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

        // Listen for grab and release events
        grabInteractable.selectEntered.AddListener(OnGrab);
        grabInteractable.selectExited.AddListener(OnRelease);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("SnapPoint")) 
        {
            // Find the closest snap point
            if (snapTarget == null || Vector3.Distance(transform.position, other.transform.position) < Vector3.Distance(transform.position, snapTarget.position))
            {
                snapTarget = other.transform;
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("SnapPoint") && other.transform == snapTarget)
        {
            snapTarget = null; // Reset if moving away
        }
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        // When grabbed, prevent snapping
        snapTarget = null;
    }

    void OnRelease(SelectExitEventArgs args)
    {
        // When released, snap to position if a valid snapTarget exists
        if (snapTarget != null)
        {
            transform.position = snapTarget.position;
            transform.rotation = snapTarget.rotation;
        }
    }
}