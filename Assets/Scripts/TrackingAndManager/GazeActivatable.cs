using UnityEngine;
using System.Collections;

public class GazeActivatable : MonoBehaviour
{
    [Header("Gaze Settings")]
    public float maxGazeDistance = 10f;
    public LayerMask gazeLayerMask;

    [Header("Trial 2 Delay Settings")]
    public float minDelay = 0.7f;
    public float maxDelay = 3f;

    [Header("Trial 3 Visibility Duration")]
    public float visibilityDuration = 10f; // New: How long the object stays visible in Trial 3

    private int trialNumber;
    private Renderer objectRenderer;
    private bool isHovering = false;
    private Coroutine delayCoroutine = null;

    // === Trial 3 state ===
    private bool hasBeenActivated = false;  // Whether the object has been triggered
    private bool isVisible = false;         // Whether the object is currently visible

    void Start()
    {
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer != null)
            objectRenderer.enabled = false;

        // Try to get trial number from GameManager, fallback to TutorialGameManager
        GameManager gm = FindObjectOfType<GameManager>();
        if (gm != null)
        {
            trialNumber = gm.trialNumber;
        }
        else
        {
            // Try to find TutorialGameManager instead
            TutorialGameManager tgm = FindObjectOfType<TutorialGameManager>();
            if (tgm != null)
            {
                trialNumber = tgm.trialNumber;
                Debug.LogWarning("Using TutorialGameManager as fallback.");
            }
            else
            {
                Debug.LogWarning("Neither GameManager nor TutorialGameManager found. Defaulting to Trial 1.");
                trialNumber = 1;
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

                    switch (trialNumber)
                    {
                        // 24.07.2025 Start
                        case 0:
                            ShowObjectImmediate();
                            break;
                        // 24.07.2025 End
                        case 1:
                            ShowObjectImmediate();
                            break;

                        case 2:
                            delayCoroutine = StartCoroutine(ShowObjectWithDelay());
                            break;

                        case 3:
                            if (!hasBeenActivated)
                            {
                                hasBeenActivated = true; // Ensure one-time activation
                                ShowObjectImmediate();
                                StartCoroutine(DeactivateAfterDuration()); // 🆕 Start timed disappearance
                            }
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

            switch (trialNumber)
            {
                // 24.07.2025 Start
                case 0:
                // 24.07.2025 End
                case 1:
                case 2:
                    HideObjectImmediate();
                    break;

                case 3:
                    // No longer respond to gaze exit in Trial 3
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

    IEnumerator ShowObjectWithDelay()
    {
        float delay = Random.Range(minDelay, maxDelay);
        yield return new WaitForSeconds(delay);
        ShowObjectImmediate();
    }

    // 🆕 Trial 3: Automatically hide object after duration
    IEnumerator DeactivateAfterDuration()
    {
        yield return new WaitForSeconds(visibilityDuration);
        HideObjectImmediate();
    }
}
