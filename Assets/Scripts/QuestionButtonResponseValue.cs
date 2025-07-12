using UnityEngine;

/// <summary>
/// Handles numeric response from physical Likert buttons (values 1–7).
/// Each button sends an string to GameManager depending on which question is active.
/// </summary>
public class QuestionButtonResponseValue : MonoBehaviour
{
    public GameManager gameManager;
    [Range(1, 7)]
    public int response; // 1–7 for Likert scale

    public void OnPress()
    {
        string stringResponse = response.ToString(); // Store the response value
        gameManager.OnQuestionResponse(stringResponse); // Convert int to string for GameManager
    }
}
