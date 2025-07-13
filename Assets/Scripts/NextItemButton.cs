using UnityEngine;
using System.Collections;

/// <summary>
/// Handles the next item button functionality in the game.
/// Triggers the LoadNextItem function in GameManager.
/// Same script as ContinueButton but triggers a different function in GameManager.
/// </summary>
public class NextItemButton : MonoBehaviour
{
    public GameManager gameManager;
    private void Start()
    {
    }
    public void OnPress()
    {
        // load the next item in the game aka go to the questions
        gameManager.LoadNextItem();
    }
}
