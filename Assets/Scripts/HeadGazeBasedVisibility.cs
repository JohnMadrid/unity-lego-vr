using UnityEngine;

/// <summary>
/// Makes the model visible only when the participant is looking directly at it with the HEAD Gaze.
/// Attached this script to the model GameObject if want head gaze detection
/// </summary>
public class GazeBasedVisibility : MonoBehaviour
{
    [Header("Player Camera")]
    [Tooltip("The camera representing the player's head or VR view.")]
    public Transform playerCamera;

    [Header("Visibility Settings")]
    [Tooltip("Angle (in degrees) within which the object is considered 'looked at'.")]
    [Range(0f, 90f)]
    public float viewThresholdAngle = 5f;

    [Tooltip("Maximum distance at which the object can be seen.")]
    public float maxViewDistance = 10f;

    private Renderer objectRenderer;

    void Start()
    {
        // Cache the Renderer component for performance
        objectRenderer = GetComponent<Renderer>();

        if (playerCamera == null)
        {
            Debug.LogWarning("Player Camera not assigned to GazeBasedVisibility.");
        }
    }

    void Update()
    {
        if (playerCamera == null) return;

        // Vector from the camera to the object
        // This assumes the script is attached to the object that should be visible based on gaze
        Vector3 directionToObject = transform.position - playerCamera.position;

        // Angle between the camera's forward direction and the object
        float angle = Vector3.Angle(playerCamera.forward, directionToObject);

        // Distance from the camera to the object
        float distance = directionToObject.magnitude;

        // Check if the object is within the view cone and distance
        bool isInView = angle < viewThresholdAngle && distance <= maxViewDistance;

        // Enable or disable the object's renderer based on visibility
        objectRenderer.enabled = isInView;
    }
}
