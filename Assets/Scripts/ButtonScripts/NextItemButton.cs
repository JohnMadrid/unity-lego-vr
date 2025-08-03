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
    // 30.07.2025 end

    public void OnPress()
    {
        Debug.Log("NextItemButton: OnPress() called.");

        // 30.07.2025 begin
        // Trigger logging of model build end
        if (eyeTrackingManager != null)
        {
            Debug.Log("NextItemButton: Calling eyeTrackingManager.RecordModelBuildEnd().");
            eyeTrackingManager.RecordModelBuildEnd();
        }
        else
        {
            Debug.LogWarning("NextItemButton: eyeTrackingManager is not assigned.");
        }

        if (viveTrackerManager != null)
        {
            Debug.Log("NextItemButton: Calling viveTrackerManager.RecordModelBuildEnd().");
            viveTrackerManager.RecordModelBuildEnd();
        }
        else
        {
            Debug.LogWarning("NextItemButton: viveTrackerManager is not assigned.");
        }
        // 30.07.2025 end

        Debug.Log("NextItemButton: Starting CaptureAndReset coroutine.");
        StartCoroutine(CaptureAndReset());
    }

    private IEnumerator CaptureAndReset()
    {
        Debug.Log("NextItemButton: CaptureAndReset coroutine started.");
        if (screenshotManager != null)
        {
            Debug.Log("NextItemButton: Starting screenshotManager.CaptureScreenshotsAndContinue coroutine.");
            yield return StartCoroutine(screenshotManager.CaptureScreenshotsAndContinue(gameManager));
            Debug.Log("NextItemButton: screenshotManager.CaptureScreenshotsAndContinue coroutine finished.");
        }
        else
        {
            Debug.LogError("NextItemButton: screenshotManager is not assigned. Cannot proceed.");
        }
        Debug.Log("NextItemButton: CaptureAndReset coroutine finished.");
    }
}
