using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using System.Linq; // 07.08.2025: Added for filtering components.

public class GazeActivatable : MonoBehaviour
{
        // 07.08.2025 Start
    [Header("Debug Settings")]
    [Tooltip("Enable to see detailed log messages from this script in the console.")]
    public bool enableDebugMode = false;
    // 07.08.2025 End

    [Header("Gaze Settings")]
    public float maxGazeDistance = 10f;
    public LayerMask gazeLayerMask;
    
    [Header("Trial 2 Delay Settings")]
    public float delay2 = 0.7f;
    public float delay3 = 2f; // New: Delay for Condition 3
    // 05.08.2025 commented out
    //[Header("Trial 3 Visibility Duration")]
    //public float visibilityDuration = 10f; // New: How long the object stays visible in Trial 3

    public int conditionNumber;
    // 07.08.2025 Start
    // Step 1: Store an array of Renderers to hold all the brick meshes.
    private Renderer[] objectRenderers;
    // 07.08.2025 End

    private bool isHovering = false;
    private Coroutine delayCoroutine = null;

    // === Trial 3 state ===
    //private bool hasBeenActivated = false;  // Whether the object has been triggered
    public bool isVisible = false;  
    public bool IsVisible => isVisible;

    // 07.08.2025 Start
    void Start()
    {
        // Step 2: Find the 'Primitive_Cylinder' GameObject which contains the bricks.
        // We start the search from this object's transform.
        Transform brickContainer = transform.Find("Model_plate/Primitive_Cylinder");

        if (brickContainer != null)
        {
            // Step 3: Gather all Renderer components from the children of the brick container.
            // This collects the renderers for every individual brick.
            // 07.08.2025 Start: Added 'Where' clause to exclude the parent's (Primitive_Cylinder) own renderer.
            objectRenderers = brickContainer.GetComponentsInChildren<Renderer>()
                .Where(r => r.gameObject != brickContainer.gameObject).ToArray();
            // 07.08.2025 End
            if (enableDebugMode) Debug.Log($"GazeActivatable: Found brick container. Found {objectRenderers.Length} renderers (excluding container).", gameObject);
        }
        else
        {
            if (enableDebugMode) Debug.LogError("GazeActivatable: Could not find 'Model_plate/Primitive_Cylinder' child object.", gameObject);
        }

        if (objectRenderers == null || objectRenderers.Length == 0)
        {
            if (enableDebugMode) Debug.LogError("GazeActivatable: No renderers found on the children of the brick container.", gameObject);
            return; // Stop the script if there's nothing to control.
        }

        // Get the active scene's build index to determine the condition.
        int sceneIndex = SceneManager.GetActiveScene().buildIndex;
        conditionNumber = sceneIndex;
        if (enableDebugMode) Debug.Log($"GazeActivatable: Initialized in Condition {conditionNumber}.", gameObject);

        // Set initial visibility for all bricks to true for all conditions.
        // The gaze exit logic in Update() will handle hiding them for Conditions 2 and 3.
        ShowObjectImmediate();
    }
    // 07.08.2025 End


    void Update()
    {
        if (GazeManager.Instance == null) return;

        Vector3 origin = GazeManager.Instance.gazeOrigin;
        Vector3 direction = GazeManager.Instance.gazeDirection;

        // Perform gaze raycast
        if (Physics.Raycast(origin, direction, out RaycastHit hit, maxGazeDistance, gazeLayerMask))
        {
            if (enableDebugMode) Debug.Log($"GazeActivatable: Raycast hit '{hit.collider.name}'.", gameObject);
            
            // 07.08.2025 Start
            // Step 5 (Logic): Check if the gazed object (or its parent) has this script component.
            // This robustly identifies the correct model, even if the collider is on a child object like 'Model_plate'.
            if (hit.collider.GetComponentInParent<GazeActivatable>() == this)
            {
            // 07.08.2025 End
                if (!isHovering)
                {
                    if (enableDebugMode) Debug.Log("GazeActivatable: Gaze ENTERED.", gameObject);
                    isHovering = true;

                    switch (conditionNumber)
                    {
                        // 24.07.2025 Start
                        case 0: // tutorial scene
                            if (enableDebugMode) Debug.Log("GazeActivatable: Condition 0 - no action.", gameObject);
                            break;
                        // 24.07.2025 End
                        case 1: // condition 1
                            if (enableDebugMode) Debug.Log("GazeActivatable: Condition 1 - no action.", gameObject);
                            break;

                        case 2: // condition 2
                            // 07.08.2025 Start
                            // If the bricks are currently invisible, start the process to show them.
                            if (!isVisible && delayCoroutine == null)
                            {
                                if (enableDebugMode) Debug.Log($"GazeActivatable: Condition 2 - Starting Show coroutine with {delay2}s delay.", gameObject);
                                delayCoroutine = StartCoroutine(ShowObjectWithDelay(delay2));
                            }
                            // 07.08.2025 End
                            break;

                        case 3: // condition 3
                            // 07.08.2025 Start
                            // If the bricks are currently invisible, start the process to show them.
                            if (!isVisible && delayCoroutine == null)
                            {
                                if (enableDebugMode) Debug.Log($"GazeActivatable: Condition 3 - Starting Show coroutine with {delay3}s delay.", gameObject);
                                delayCoroutine = StartCoroutine(ShowObjectWithDelay(delay3));
                            }
                            // 07.08.2025 End
                            break;
                    }
                }
                return;
            }
        }

        // Gaze exit logic
        if (isHovering)
        {
            if (enableDebugMode) Debug.Log("GazeActivatable: Gaze EXITED.", gameObject);
            isHovering = false;

            if (delayCoroutine != null)
            {
                if (enableDebugMode) Debug.Log("GazeActivatable: Gaze exited while delay was active. Stopping coroutine.", gameObject);
                StopCoroutine(delayCoroutine);
                delayCoroutine = null;
            }

            switch (conditionNumber)
            {
                // 24.07.2025 Start
                case 0: // tutorial scene
                    // Object should stay visible all the time, no hiding
                    break;
                // 24.07.2025 End
                case 1: // condition 1
                    // Object should stay visible all the time, no hiding
                    break;
                    
                case 2: // condition 2
                    if (enableDebugMode) Debug.Log("GazeActivatable: Condition 2 - Hiding object immediately on gaze exit.", gameObject);
                    HideObjectImmediate(); // Hide immediately when gaze is removed
                    break;

                case 3: // condition 3
                    if (enableDebugMode) Debug.Log("GazeActivatable: Condition 3 - Hiding object immediately on gaze exit.", gameObject);
                    HideObjectImmediate(); // Hide immediately when gaze is removed
                    break;
            }
        }
    }

    // === Utility Methods ===

    // 07.08.2025 Start
    // Step 4: Control all bricks together. This function now loops through the array.
    void ShowObjectImmediate()
    {
        if (objectRenderers != null && objectRenderers.Length > 0)
        {
            if (enableDebugMode) Debug.Log($"GazeActivatable: SHOWING {objectRenderers.Length} renderers.", gameObject);
            foreach (var renderer in objectRenderers)
            {
                renderer.enabled = true;
            }
            isVisible = true;
        }
    }

    // Step 4: Control all bricks together. This function now loops through the array.
    void HideObjectImmediate()
    {
        if (objectRenderers != null && objectRenderers.Length > 0)
        {
            if (enableDebugMode) Debug.Log($"GazeActivatable: HIDING {objectRenderers.Length} renderers.", gameObject);
            foreach (var renderer in objectRenderers)
            {
                renderer.enabled = false;
            }
            isVisible = false;
        }
    }
    // 07.08.2025 End

    IEnumerator ShowObjectWithDelay(float delay)
    {
        // wait for a specified delay before showing the object
        if (enableDebugMode) Debug.Log($"GazeActivatable: Waiting for {delay} seconds...", gameObject);
        yield return new WaitForSeconds(delay);

        if (enableDebugMode) Debug.Log("GazeActivatable: Delay finished. Calling ShowObjectImmediate.", gameObject);
        ShowObjectImmediate();
        
        // 07.08.2025 Start
        delayCoroutine = null; // Reset coroutine tracker after it has finished
        // 07.08.2025 End
    }

    // condition 3: Automatically hide object after duration
    //IEnumerator DeactivateAfterDuration()
    //{
        //yield return new WaitForSeconds(visibilityDuration);
      //  HideObjectImmediate();
    //}
}