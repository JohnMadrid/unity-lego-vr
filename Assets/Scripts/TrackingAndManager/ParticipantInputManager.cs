using UnityEngine;
using TMPro;

public class ParticipantInputManager : MonoBehaviour
{
    public TMP_InputField inputField;
    public GameObject inputCanvas;
    public GameObject tutorialGameManager;
    public GameObject trackingManager;
    public GameObject randomModelManager; // Reference to RandomModelManager

    public void StartExperiment()
    {
        string code = inputField.text;

        if (!string.IsNullOrEmpty(code))
        {
            PlayerPrefs.SetString("ParticipantCode", code);
            PlayerPrefs.Save();
            tutorialGameManager.GetComponent<TutorialGameManager>().participantCode = code; // Store participant code in the game manager

            // Activate managers
            trackingManager.SetActive(true);
            tutorialGameManager.SetActive(true);
            randomModelManager.SetActive(true); // Activate RandomModelManager

            // Hide UI
            inputCanvas.SetActive(false);
        }
        else
        {
            Debug.LogWarning("Participant code cannot be empty.");
        }
    }
}