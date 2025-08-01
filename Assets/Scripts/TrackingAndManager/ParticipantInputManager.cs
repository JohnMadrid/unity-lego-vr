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
            Debug.Log($"ParticipantInputManager: Saved participant code to PlayerPrefs: '{code}'");
            tutorialGameManager.GetComponent<TutorialGameManager>().participantCode = code; // Store participant code in the game manager

            // Activate managers
            trackingManager.SetActive(true);
            tutorialGameManager.SetActive(true);
            randomModelManager.SetActive(true); // Activate RandomModelManager

            // Start tracking managers with the correct participant code
            StartTrackingManagers();

            // Hide UI
            inputCanvas.SetActive(false);
        }
        else
        {
            Debug.LogWarning("Please enter a participant code.");
        }
    }

    /// <summary>
    /// Starts all tracking managers with the correct participant code.
    /// </summary>
    private void StartTrackingManagers()
    {
        // Get the participant code that was just saved
        string participantCode = PlayerPrefs.GetString("ParticipantCode", "Unknown");
        Debug.Log($"ParticipantInputManager: Starting tracking managers with participant code: '{participantCode}'");
        
        // Find and start all tracking managers
        var eyeTrackingManager = FindObjectOfType<EyeTrackingManager>();
        if (eyeTrackingManager != null && eyeTrackingManager.trackingEnabled)
        {
            eyeTrackingManager.StartLoggingManually();
        }
        
        var viveTrackerManager = FindObjectOfType<ViveTrackerManager>();
        if (viveTrackerManager != null && viveTrackerManager.trackingEnabled)
        {
            viveTrackerManager.StartLoggingManually();
        }
        
        var controllerTrackingManager = FindObjectOfType<IndexControllerLogger>();
        if (controllerTrackingManager != null && controllerTrackingManager.trackingEnabled)
        {
            controllerTrackingManager.StartLoggingManually();
        }
    }
}