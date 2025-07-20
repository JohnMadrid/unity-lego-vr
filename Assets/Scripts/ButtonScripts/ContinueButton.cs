using UnityEngine;
using System.Collections;

/// <summary>
/// Handles the continue button functionality in the game.
/// same script as NextItemButton but triggers different function in gamemanager
/// </summary>
public class ContinueButton : MonoBehaviour
{
    public GameManager gameManager;
    private void Start()
    {
    }
    public void OnPress()
    {
        // Unified transition function to show the second question
        gameManager.ShowQuestion();
    }
}
