using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public class ParticipantInputManager : MonoBehaviour
{
    public TMP_InputField inputField;
    public GameObject inputCanvas;
    public GameObject tutorialGameManager;
    public GameObject trackingManager;
    public GameObject randomModelManager; // Reference to RandomModelManager
    public GameObject resumeButton;       // Reference to ResumeButton in the UI
    public GameObject startExperimentButton; // Reference to the Start Experiment button in the UI

    [Header("Condition Selection Buttons (Start Screen)")]
    public Button condition1Button;
    public Button condition2Button;
    public Button condition3Button;
    public Image condition1Image;
    public Image condition2Image;
    public Image condition3Image;

    // Stores the chosen condition (1 = Condition1Constant, 2 = Condition2Delay, 3 = Condition3Once).
    // This is set by UI controls in the TutorialVideo scene before starting the experiment.
    private int selectedCondition = 1;

    // Path where ModelOrder CSVs are written (must match RandomModelManager.logPath).
    private readonly string modelOrderPath = @"D:\LegoVR\unity-lego-vr\Other_than_in_project_files\Model_Order_Data";

    // Participant code that can be resumed (null if nothing to resume).
    private string resumableParticipantCode = null;
    private int resumableConditionNumber = -1;
    private Color defaultButtonColor = Color.white;

    private void Start()
    {
        if (condition1Image != null)
        {
            defaultButtonColor = condition1Image.color;
        }

        InitializeResumeUI();

        // By default, hide the Start Experiment button until a condition is chosen manually.
        if (startExperimentButton != null)
        {
            startExperimentButton.SetActive(false);
        }

        if (inputField != null)
        {
            inputField.onValueChanged.AddListener(OnParticipantInputChanged);
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
        int conditionNumber = 0; // Tutorial scene has no real condition
        var eyeTrackingManager = FindObjectOfType<EyeTrackingManager>();
        if (eyeTrackingManager != null && eyeTrackingManager.trackingEnabled)
        {
            eyeTrackingManager.StartLoggingManually(conditionNumber);
        }
        
        var viveTrackerManager = FindObjectOfType<ViveTrackerManager>();
        if (viveTrackerManager != null && viveTrackerManager.trackingEnabled)
        {
            viveTrackerManager.StartLoggingManually(conditionNumber);
        }

        // Brick relation tracking (snap events between bricks/board)
        var bricksRelationTracker = FindObjectOfType<BricksRelationTracker>();
        if (bricksRelationTracker != null && bricksRelationTracker.trackingEnabled)
        {
            bricksRelationTracker.StartLoggingManually(conditionNumber);
        }

    }

    /// <summary>
    /// Initializes the Resume button based on the latest ModelOrder CSV.
    /// Shows the button only if the latest participant has at least one incomplete entry.
    /// </summary>
    private void InitializeResumeUI()
    {
        resumableParticipantCode = null;
        resumableConditionNumber = -1;

        if (resumeButton == null)
        {
            ConfigureConditionButtonsForResume(false);
            return;
        }

        // Default: hide resume button until we know we can resume.
        resumeButton.SetActive(false);

        try
        {
            if (!Directory.Exists(modelOrderPath))
            {
                ConfigureConditionButtonsForResume(false);
                return;
            }

            DirectoryInfo dirInfo = new DirectoryInfo(modelOrderPath);
            FileInfo latestCsv = dirInfo
                .GetFiles("*_ModelOrder_*.csv")
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault();

            if (latestCsv == null)
            {
                ConfigureConditionButtonsForResume(false);
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
                string participantCode;
                int conditionNumber;
                if (TryGetResumeTarget(out participantCode, out conditionNumber))
                {
                    resumableParticipantCode = participantCode;
                    resumableConditionNumber = conditionNumber;

                    inputField.text = participantCode;
                    selectedCondition = Mathf.Clamp(conditionNumber, 1, 3);

                    PlayerPrefs.SetString("ParticipantCode", participantCode);
                    PlayerPrefs.SetInt("SelectedCondition", selectedCondition);
                    PlayerPrefs.Save();

                    resumeButton.SetActive(true);
                    ConfigureConditionButtonsForResume(true);
                }
                else
                {
                    ConfigureConditionButtonsForResume(false);
                }
            }
            else
            {
                resumeButton.SetActive(false);
                ConfigureConditionButtonsForResume(false);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"ParticipantInputManager: Failed to initialize Resume UI. {ex.Message}");
            resumeButton.SetActive(false);
            ConfigureConditionButtonsForResume(false);
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
    /// Reacts to every change of the participant input field.
    /// Toggles between resume mode (name matches resumable participant)
    /// and free selection mode (any other name).
    /// </summary>
    private void OnParticipantInputChanged(string text)
    {
        bool isResumable = resumableParticipantCode != null
            && string.Equals(text.Trim(), resumableParticipantCode.Trim(), StringComparison.OrdinalIgnoreCase);

        if (isResumable)
        {
            if (resumeButton != null) resumeButton.SetActive(true);
            if (startExperimentButton != null) startExperimentButton.SetActive(false);
            ConfigureConditionButtonsForResume(true);
        }
        else
        {
            if (resumeButton != null) resumeButton.SetActive(false);
            if (startExperimentButton != null) startExperimentButton.SetActive(false);
            ConfigureConditionButtonsForResume(false);
        }
    }

    /// <summary>
    /// Configures condition buttons for resume mode or free selection mode.
    /// In resume mode: all buttons are non-interactable, completed conditions are grey.
    /// In free mode: all buttons are interactable with default color.
    /// </summary>
    private void ConfigureConditionButtonsForResume(bool isResumeMode)
    {
        if (isResumeMode && resumableParticipantCode != null)
        {
            bool c1Done, c2Done, c3Done;
            TryGetConditionCompletion(resumableParticipantCode, out c1Done, out c2Done, out c3Done);

            SetConditionButtonState(condition1Button, condition1Image, completed: c1Done, clickable: false);
            SetConditionButtonState(condition2Button, condition2Image, completed: c2Done, clickable: false);
            SetConditionButtonState(condition3Button, condition3Image, completed: c3Done, clickable: false);
        }
        else
        {
            SetConditionButtonState(condition1Button, condition1Image, completed: false, clickable: true);
            SetConditionButtonState(condition2Button, condition2Image, completed: false, clickable: true);
            SetConditionButtonState(condition3Button, condition3Image, completed: false, clickable: true);
        }
    }

    private void SetConditionButtonState(Button button, Image image, bool completed, bool clickable)
    {
        if (button != null)
        {
            button.interactable = clickable;
        }
        if (image != null)
        {
            image.color = completed ? new Color(0.3f, 0.3f, 0.3f) : defaultButtonColor;
        }
    }

    /// <summary>
    /// Reads the latest ModelOrder CSV and determines which of the three
    /// conditions (1,2,3) are fully completed for the given participant.
    /// A condition is completed when it has at least one row and no row
    /// with Completed == False.
    /// </summary>
    private bool TryGetConditionCompletion(string participant,
        out bool cond1Complete, out bool cond2Complete, out bool cond3Complete)
    {
        cond1Complete = cond2Complete = cond3Complete = false;

        try
        {
            if (!Directory.Exists(modelOrderPath)) return false;

            DirectoryInfo dirInfo = new DirectoryInfo(modelOrderPath);
            FileInfo latestCsv = dirInfo
                .GetFiles("*_ModelOrder_*.csv")
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault();

            if (latestCsv == null) return false;

            bool[] hasAnyRow = new bool[4];
            bool[] hasIncomplete = new bool[4];

            string[] lines = File.ReadAllLines(latestCsv.FullName);
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                string[] parts = lines[i].Split(',');
                if (parts.Length < 6) continue;

                string csvParticipant = parts[0].Trim();
                if (!string.Equals(csvParticipant, participant, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!int.TryParse(parts[1].Trim(), out int condNum)) continue;
                if (condNum < 1 || condNum > 3) continue;

                string completedFlag = parts[5].Trim();
                bool isCompleted = completedFlag.Equals("True", StringComparison.OrdinalIgnoreCase);

                hasAnyRow[condNum] = true;
                if (!isCompleted) hasIncomplete[condNum] = true;
            }

            cond1Complete = hasAnyRow[1] && !hasIncomplete[1];
            cond2Complete = hasAnyRow[2] && !hasIncomplete[2];
            cond3Complete = hasAnyRow[3] && !hasIncomplete[3];
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"ParticipantInputManager: Failed to read condition completion. {ex.Message}");
            return false;
        }
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