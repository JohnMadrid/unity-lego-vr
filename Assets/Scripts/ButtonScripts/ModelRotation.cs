using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Rotates a target model (model_plate) by a fixed step whenever
/// the attached XR button (with XRSimpleInteractable) is pressed.
///
/// Attach this script to the RotationButton GameObject.
/// In the Inspector, assign the model_plate Transform, choose
/// the rotation axis, and set the rotation step in degrees.
/// </summary>
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable))]
public class ModelRotation : MonoBehaviour
{
    /// <summary>
    /// Fired every time the model_plate is rotated by one discrete step.
    /// Used by logging systems (e.g., ViveTrackerManager) to track cumulative rotation.
    /// </summary>
    public static event Action ModelRotated;

    [Header("Target To Rotate")]
    [Tooltip("The Transform of the model_plate (or any object) that should rotate when the button is pressed.")]
    public Transform modelPlate;

    [Header("Rotation Settings")]
    [Tooltip("Axis around which the model will rotate. For example, (0, 1, 0) for world Y axis.")]
    public Vector3 rotationAxis = Vector3.up;

    [Tooltip("Rotation step in degrees applied each time the button is pressed.")]
    public float rotationStepDegrees = 30f;

    /// <summary>
    /// Cumulative rotation in degrees for the currently active model.
    /// Resets when a new model appears or a full 360° rotation is completed.
    /// </summary>
    public static float CurrentRotationDegrees { get; private set; }

    // Cached reference to the XRSimpleInteractable on the same GameObject.
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable _interactable;

    /// <summary>
    /// Resets the cumulative rotation tracking back to 0°.
    /// Intended to be called when a new model appears or logging is reset.
    /// </summary>
    public static void ResetRotationTracking()
    {
        CurrentRotationDegrees = 0f;
    }

    private void Awake()
    {
        // Get the XRSimpleInteractable component required by this script.
        _interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
    }

    private void OnEnable()
    {
        // Subscribe to the selectEntered event, which is fired when
        // the user "presses" this XR button (e.g., with a controller).
        if (_interactable != null)
        {
            _interactable.selectEntered.AddListener(OnButtonSelected);
        }
    }

    private void OnDisable()
    {
        // Always unsubscribe in OnDisable to avoid memory leaks
        // or multiple subscriptions when enabling/disabling.
        if (_interactable != null)
        {
            _interactable.selectEntered.RemoveListener(OnButtonSelected);
        }
    }

    /// <summary>
    /// Called by XRSimpleInteractable when the button is selected/pressed.
    /// </summary>
    /// <param name="args">XR select event data (not used here, but required by signature).</param>
    private void OnButtonSelected(SelectEnterEventArgs args)
    {
        RotateModel();
    }

    /// <summary>
    /// Rotates the target model by rotationStepDegrees around rotationAxis.
    /// </summary>
    private void RotateModel()
    {
        if (modelPlate == null)
        {
            Debug.LogWarning("[ModelRotation] modelPlate reference is not set.", this);
            return;
        }

        // Ensure we have a valid axis; fall back to world up if zero.
        Vector3 axis = rotationAxis.sqrMagnitude > 0f ? rotationAxis.normalized : Vector3.up;

        // Apply an instant rotation step in world space.
        modelPlate.Rotate(axis * rotationStepDegrees, Space.World);

        // Update cumulative rotation tracking for the current model.
        CurrentRotationDegrees += rotationStepDegrees;
        if (CurrentRotationDegrees >= 360f)
        {
            CurrentRotationDegrees = 0f;
        }

        // Notify listeners (e.g., ViveTrackerManager) that a rotation step occurred.
        ModelRotated?.Invoke();
    }
}