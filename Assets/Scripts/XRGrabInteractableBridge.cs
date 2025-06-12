using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

//[RequireComponent(typeof(XRGrabInteractable))]
public class XRGrabInteractableBridge : MonoBehaviour
{
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable _grabInteractable; // XR Interactable component
    private BrickSnapController _snapController;

    void Awake()
    {
        _grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        _snapController = GetComponent<BrickSnapController>();

        _grabInteractable.selectEntered.AddListener(OnSelectEntered);
        _grabInteractable.selectExited.AddListener(OnSelectExited);
    }

    private void OnDestroy()
    {
        _grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
        _grabInteractable.selectExited.RemoveListener(OnSelectExited);
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        _snapController.OnSelectEntered(args);
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        _snapController.OnSelectExited(args);
    }
}