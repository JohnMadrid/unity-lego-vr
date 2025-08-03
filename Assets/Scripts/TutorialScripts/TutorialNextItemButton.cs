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
        // 30.07.2025 begin
        // Trigger logging of model build end
        eyeTrackingManager?.RecordModelBuildEnd();
        viveTrackerManager?.RecordModelBuildEnd();
        // 30.07.2025 end

        StartCoroutine(CaptureAndReset());
    }

    private IEnumerator CaptureAndReset()
    {
        yield return StartCoroutine(tutorialScreenshotManager.CaptureScreenshotsAndContinue(tutorialGameManager));
    }
}