using UnityEngine; using System.Collections;

/// Handles the next item button functionality in the game. 
/// Triggers the Screenshotmanager to make photos of the built model and which later calls LoadNextItem function in TutorialGameManager. 
/// Same script as NextItemButton but for tutorial. ///
public class TutorialNextItemButton : MonoBehaviour {
    public TutorialGameManager tutorialGameManager;
    public TutorialScreenshotManager tutorialScreenshotManager;

    // 30.07.2025 begin
    // Reference to EyeTrackingManager to mark model end
    public EyeTrackingManager eyeTrackingManager;
    public ViveTrackerManager viveTrackerManager;
    // 30.07.2025 end

    public void OnPress()
    {
        Debug.Log("TutorialNextItemButton: OnPress() called.");

        // 30.07.2025 begin
        // Trigger logging of model build end
        if (eyeTrackingManager != null)
        {
            Debug.Log("TutorialNextItemButton: Calling eyeTrackingManager.RecordModelBuildEnd().");
            eyeTrackingManager.RecordModelBuildEnd();
        }
        else
        {
            Debug.LogWarning("TutorialNextItemButton: eyeTrackingManager is not assigned.");
        }

        if (viveTrackerManager != null)
        {
            Debug.Log("TutorialNextItemButton: Calling viveTrackerManager.RecordModelBuildEnd().");
            viveTrackerManager.RecordModelBuildEnd();
        }
        else
        {
            Debug.LogWarning("TutorialNextItemButton: viveTrackerManager is not assigned.");
        }
        // 30.07.2025 end

        Debug.Log("TutorialNextItemButton: Starting CaptureAndReset coroutine.");
        StartCoroutine(CaptureAndReset());
    }

    private IEnumerator CaptureAndReset()
    {
        Debug.Log("TutorialNextItemButton: CaptureAndReset coroutine started.");
        if (tutorialScreenshotManager != null)
        {
            Debug.Log("TutorialNextItemButton: Starting tutorialScreenshotManager.CaptureScreenshotsAndContinue coroutine.");
            yield return StartCoroutine(tutorialScreenshotManager.CaptureScreenshotsAndContinue(tutorialGameManager));
            Debug.Log("TutorialNextItemButton: tutorialScreenshotManager.CaptureScreenshotsAndContinue coroutine finished.");
        }
        else
        {
            Debug.LogError("TutorialNextItemButton: tutorialScreenshotManager is not assigned. Cannot proceed.");
        }
        Debug.Log("TutorialNextItemButton: CaptureAndReset coroutine finished.");
    }
}