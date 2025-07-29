using UnityEngine;
using System.Collections;

/// <summary>
/// Handles the next item button functionality in the game.
/// Triggers the Screenshotmanager to make photos of the built model and which later callsLoadNextItem function in GameManager.
/// Same script as ContinueButton but triggers a different function in GameManager.
/// </summary>

public class NextItemButton : MonoBehaviour
{
    public GameManager gameManager;
    public ScreenshotManager screenshotManager;

    public bool pressed = false;

    public void OnPress()
    {
        pressed = true;
        StartCoroutine(CaptureAndReset());
    }

    private IEnumerator CaptureAndReset()
    {
        yield return StartCoroutine(screenshotManager.CaptureScreenshotsAndContinue(gameManager));

        // Delay before resetting to allow EyeTrackingManager to detect it
        yield return new WaitForEndOfFrame();
        pressed = false;
    }

}
