using UnityEngine;
using TMPro;
using System;
using System.IO;
using System.Linq;

public class ParticipantInputManager : MonoBehaviour
{
    public TMP_InputField inputField;
    public GameObject inputCanvas;
    public GameObject tutorialGameManager;
    public GameObject trackingManager;
    public GameObject randomModelManager; // Reference to RandomModelManager
    public GameObject resumeButton;       // Reference to ResumeButton in the UI
    public GameObject startExperimentButton; // Reference to the Start Experiment button in the UI

    // Stores the chosen condition (1 = Condition1Constant, 2 = Condition2Delay, 3 = Condition3Once).
    // This is set by UI controls in the TutorialVideo scene before starting the experiment.
    private int selectedCondition = 1;

    // Path where ModelOrder CSVs are written (must match RandomModelManager.logPath).
    private readonly string modelOrderPath = @"D:\LegoVR\unity-lego-vr\Other_than_in_project_files\Model_Order_Data";

    private void Start()
    {
        InitializeResumeUI();

        // By default, hide the Start Experiment button until a condition is chosen manually.
        if (startExperimentButton != null)
        {
            startExperimentButton.SetActive(false);
        }
    }

    /// <summary>
    /// Called by UI (e.g., buttons or dropdown) to select which condition scene to run after the tutorial.
    /// </summary>
    public void SetCondition(int conditionNumber)
    {
        selectedCondition = Mathf.Clamp(conditionNumber, 1, 3);

        PlayerPrefs.SetInt("SelectedCondition", selectedCondition);
        PlayerPrefs.Save();

        Debug.Log($"ParticipantInputManager: Selected condition set to {selectedCondition}");

        // Once a condition has been selected manually, show the Start Experiment button.
        if (startExperimentButton != null)
        {
            startExperimentButton.SetActive(true);
        }
    }

    public void StartExperiment()
    {
        string code = inputField.text;

        if (!string.IsNullOrEmpty(code))
        {
            BeginExperimentWithCode(code);
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

        // Brick relation tracking (snap events between bricks/board)
        var bricksRelationTracker = FindObjectOfType<BricksRelationTracker>();
        if (bricksRelationTracker != null && bricksRelationTracker.trackingEnabled)
        {
            bricksRelationTracker.StartLoggingManually();
        }

    }

    /// <summary>
    /// Initializes the Resume button based on the latest ModelOrder CSV.
    /// Shows the button only if the latest participant has at least one incomplete entry.
    /// </summary>
    private void InitializeResumeUI()
    {
        if (resumeButton == null)
        {
            return;
        }

        // Default: hide resume button until we know we can resume.
        resumeButton.SetActive(false);

        try
        {
            if (!Directory.Exists(modelOrderPath))
            {
                return;
            }

            DirectoryInfo dirInfo = new DirectoryInfo(modelOrderPath);
            FileInfo latestCsv = dirInfo
                .GetFiles("*_ModelOrder_*.csv")
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault();

            if (latestCsv == null)
            {
                return;
            }

            // Check if this latest CSV has any incomplete entries.
            bool hasIncomplete = false;
            string[] lines = File.ReadAllLines(latestCsv.FullName);

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                string[] parts = lines[i].Split(',');
                if (parts.Length < 6) continue;

                string completedFlag = parts[5].Trim();
                if (completedFlag.Equals("False", StringComparison.OrdinalIgnoreCase))
                {
                    hasIncomplete = true;
                    break;
                }
            }

            if (hasIncomplete)
            {
                // Show resume button and pre-fill the input field and condition
                // with the first incomplete participant from the latest CSV.
                resumeButton.SetActive(true);

                string participantCode;
                int conditionNumber;
                if (TryGetResumeTarget(out participantCode, out conditionNumber))
                {
                    inputField.text = participantCode;
                    selectedCondition = Mathf.Clamp(conditionNumber, 1, 3);

                    PlayerPrefs.SetString("ParticipantCode", participantCode);
                    PlayerPrefs.SetInt("SelectedCondition", selectedCondition);
                    PlayerPrefs.Save();
                }
            }
            else
            {
                resumeButton.SetActive(false);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"ParticipantInputManager: Failed to initialize Resume UI. {ex.Message}");
            // In case of error, keep resume button hidden.
            resumeButton.SetActive(false);
        }
    }

    /// <summary>
    /// Called by the ResumeButton. Uses the latest ModelOrder CSV to
    /// auto-fill ParticipantCode and SelectedCondition and then starts
    /// the experiment just like for a new participant.
    /// </summary>
    public void ResumeLastParticipant()
    {
        string participantCode;
        int conditionNumber;

        if (!TryGetResumeTarget(out participantCode, out conditionNumber))
        {
            Debug.LogWarning("ParticipantInputManager: No resumable participant found in latest ModelOrder CSV.");
            return;
        }

        // Auto-fill UI and PlayerPrefs as if user had entered this manually.
        inputField.text = participantCode;
        selectedCondition = Mathf.Clamp(conditionNumber, 1, 3);

        PlayerPrefs.SetString("ParticipantCode", participantCode);
        PlayerPrefs.SetInt("SelectedCondition", selectedCondition);
        PlayerPrefs.Save();

        Debug.Log($"ParticipantInputManager: Resuming participant '{participantCode}' at condition {selectedCondition}.");

        // Update LSL session info + SESSION_RESUME marker (if LSL is present)
        var lsl = LslOutletManager.Instance;
        if (lsl != null)
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            lsl.SetSessionInfo(participantCode, selectedCondition, sceneName);
            lsl.PushMarker($"SESSION_RESUME;{participantCode};cond={selectedCondition};scene={sceneName}");
        }

        // Start experiment flow exactly as for a new participant.
        BeginExperimentWithCode(participantCode);
    }

    /// <summary>
    /// Shared path for starting the experiment once a participant code
    /// (and SelectedCondition) are already determined.
    /// </summary>
    /// <param name="code">Participant code to use for this run.</param>
    private void BeginExperimentWithCode(string code)
    {
        PlayerPrefs.SetString("ParticipantCode", code);
        PlayerPrefs.Save();
        Debug.Log($"ParticipantInputManager: Starting experiment for participant code: '{code}'");

        // Update LSL session info + SESSION_START marker (if LSL is present)
        var lsl = LslOutletManager.Instance;
        if (lsl != null)
        {
            int cond = PlayerPrefs.GetInt("SelectedCondition", 1);
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            lsl.SetSessionInfo(code, cond, sceneName);
            lsl.PushMarker($"SESSION_START;{code};cond={cond};scene={sceneName}");
        }

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

    /// <summary>
    /// Reads the latest ModelOrder CSV and finds the first entry
    /// with Completed == False, returning its participant code and
    /// condition number.
    /// </summary>
    private bool TryGetResumeTarget(out string participantCode, out int conditionNumber)
    {
        participantCode = null;
        conditionNumber = 1;

        try
        {
            if (!Directory.Exists(modelOrderPath))
            {
                return false;
            }

            DirectoryInfo dirInfo = new DirectoryInfo(modelOrderPath);
            FileInfo latestCsv = dirInfo
                .GetFiles("*_ModelOrder_*.csv")
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault();

            if (latestCsv == null)
            {
                return false;
            }

            string[] lines = File.ReadAllLines(latestCsv.FullName);
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                string[] parts = lines[i].Split(',');
                if (parts.Length < 6) continue;

                string completedFlag = parts[5].Trim();
                if (completedFlag.Equals("False", StringComparison.OrdinalIgnoreCase))
                {
                    participantCode = parts[0].Trim();
                    int.TryParse(parts[1].Trim(), out conditionNumber);
                    return !string.IsNullOrEmpty(participantCode);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"ParticipantInputManager: Failed to determine resume target. {ex.Message}");
        }

        return false;
    }
}