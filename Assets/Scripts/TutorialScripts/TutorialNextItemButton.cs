using UnityEngine;
using System.Collections;

/// <summary>
/// Handles the next item button functionality in the game.
/// Triggers the Screenshotmanager to make photos of the built model and which later callsLoadNextItem function in GameManager.
/// Same script as ContinueButton but triggers a different function in GameManager.
/// </summary>

public class TutorialNextItemButton : MonoBehaviour
{
    public TutorialGameManager tutorialGameManager;
    public TutorialScreenshotManager tutorialScreenshotManager;

    public void OnPress()
    {
        StartCoroutine(tutorialScreenshotManager.CaptureScreenshotsAndContinue(tutorialGameManager));
    }
}
