using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class GazeActivatable : MonoBehaviour
{
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
    private Renderer objectRenderer;
    private bool isHovering = false;
    private Coroutine delayCoroutine = null;

    // === Trial 3 state ===
    //private bool hasBeenActivated = false;  // Whether the object has been triggered
    private bool isVisible = false;  

    void Start()
    {
        objectRenderer = GetComponent<Renderer>();
        
        // Get the active scene's build index
        int sceneIndex = SceneManager.GetActiveScene().buildIndex;
        // Use the scene index directly as the condition number
        conditionNumber = sceneIndex;

        // Set initial visibility based on condition
        if (objectRenderer != null)
        {
            if (conditionNumber == 0 || conditionNumber == 1) // Tutorial scene and Condition 1
            {
                objectRenderer.enabled = true; // Show object all the time
                isVisible = true;
            }
            else
            {
                objectRenderer.enabled = false; // Hide initially for conditions 2 and 3
                isVisible = false;
            }
        }
    }


    void Update()
    {
        if (GazeManager.Instance == null) return;

        Vector3 origin = GazeManager.Instance.gazeOrigin;
        Vector3 direction = GazeManager.Instance.gazeDirection;

        // Perform gaze raycast
        if (Physics.Raycast(origin, direction, out RaycastHit hit, maxGazeDistance, gazeLayerMask))
        {
            if (hit.transform == transform)
            {
                if (!isHovering)
                {
                    isHovering = true;

                    switch (conditionNumber)
                    {
                        // 24.07.2025 Start
                        case 0: // tutorial scene
                            // Object is already visible all the time, no action needed
                            break;
                        // 24.07.2025 End
                        case 1: // condition 1
                            // Object is already visible all the time, no action needed
                            break;

                        case 2: // condition 2
                            // Start the delay coroutine to show the object
                            if (delayCoroutine == null) 
                                delayCoroutine = StartCoroutine(ShowObjectWithDelay(delay2));
                            break;

                        case 3: // condition 3
                            // Start the delay coroutine to show the object 05.08.2025
                            if (delayCoroutine == null) 
                                delayCoroutine = StartCoroutine(ShowObjectWithDelay(delay3));
                            break;
                    }
                }
                return;
            }
        }

        // Gaze exit logic
        if (isHovering)
        {
            isHovering = false;

            if (delayCoroutine != null)
            {
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
                    HideObjectImmediate(); // Hide immediately when gaze is removed
                    break;

                case 3: // condition 3
                    HideObjectImmediate(); // Hide immediately when gaze is removed
                    break;
            }
        }
    }

    // === Utility Methods ===

    void ShowObjectImmediate()
    {
        if (objectRenderer != null)
        {
            objectRenderer.enabled = true;
            isVisible = true;
        }
    }

    void HideObjectImmediate()
    {
        if (objectRenderer != null)
        {
            objectRenderer.enabled = false;
            isVisible = false;
        }
    }

    IEnumerator ShowObjectWithDelay(float delay)
    {
        // wait for a specified delay before showing the object
        yield return new WaitForSeconds(delay);
        ShowObjectImmediate();
    }

    // condition 3: Automatically hide object after duration
    //IEnumerator DeactivateAfterDuration()
    //{
        //yield return new WaitForSeconds(visibilityDuration);
      //  HideObjectImmediate();
    //}
}