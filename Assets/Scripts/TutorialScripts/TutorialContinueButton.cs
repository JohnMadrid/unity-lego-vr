using UnityEngine;
using System.Collections;

/// <summary>
/// Handles the continue button functionality in the game.
/// same script as NextItemButton but triggers different function in gamemanager
/// </summary>
public class TutorialContinueButton : MonoBehaviour
{
    public TutorialGameManager tutorialGameManager;

    public void OnPress()
    {
        // Unified transition function to show the second question
        tutorialGameManager.ShowQuestion();
    }
}
