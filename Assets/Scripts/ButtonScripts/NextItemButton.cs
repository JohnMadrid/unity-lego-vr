using UnityEngine;
using System.Collections;

/// Handles the next item button functionality in the game.
/// Triggers the ScreenshotManager to make photos of the built model and which later calls LoadNextItem function in GameManager.
/// Same script as ContinueButton but triggers a different function in GameManager.
public class NextItemButton : MonoBehaviour
{
    public GameManager gameManager;
    public ScreenshotManager screenshotManager;

    // 30.07.2025 begin
    // Reference to TrackingManagers to mark model end
    public EyeTrackingManager eyeTrackingManager;
    public ViveTrackerManager viveTrackerManager;
    public IndexControllerLogger indexControllerLogger;
    // 30.07.2025 end

    public void OnPress()
    {
        // 30.07.2025 begin
        // Trigger logging of model build end
        eyeTrackingManager?.RecordModelBuildEnd();
        viveTrackerManager?.RecordModelBuildEnd();
        indexControllerLogger?.RecordModelBuildEnd();
        // 30.07.2025 end

        StartCoroutine(CaptureAndReset());
    }

    private IEnumerator CaptureAndReset()
    {
        yield return StartCoroutine(screenshotManager.CaptureScreenshotsAndContinue(gameManager));
    }
}
