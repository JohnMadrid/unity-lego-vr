using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System;
using System.IO;
using System.Collections.Generic;
using TMPro; 
using UnityEngine.UI;
using System.Linq;

/// <summary>
/// This script manages the trial levels in a VR experiment.
/// It ensures that player validation occurs before showing each item,
/// and introduces break periods between levels.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("Model Management")]
    public GameObject[] modelPrefabs;        // List of trial objects (Models) to present
    public Transform modelSpawnPoint;            // Where to spawn each item
    // public int modelsPerLevel = 6;

    // 01.07.2025 begin
    [Header("Resource Brick Management")]
    public GameObject[] resourceBrickPrefabs; // List of resource brick objects to present
    public Transform resourceBrickSpawnPoint; // Where to spawn each resource brick
    // public int resourceBricksPerLevel = 6;   // Number of resource bricks per level
    // 01.07.2025 end

    [Header("Break Settings")]
    [Tooltip("Time in seconds to wait between levels (e.g. 900 = 15 minutes)")]
    [Range(0f, 1800f)]
    public float breakDuration = 900f;

    [Header("Start Validation")]
    public StartValidator startValidator;   // Reference to your StartValidator script
    public GameObject fixationPanel;


    // 30.06.2025 begin
    // 03.07.2025 begin deletion
    //[Header("Complexity Question UI")]
    //public GameObject questionPanel;

    //Header("Complexity Physical Buttons")]
    //public GameObject easyButton;
    //public GameObject mediumButton;
    //public GameObject hardButton;

    // 03.07.2025 end

    public string participantCode; // 30.07.2025 Participant code to be set from input field

    // 05.08.2025 begin
    public bool iQuestionsTracking = true; // Flag to enable/disable I-Questions tracking
    // 05.08.2025 end
    public int trialNumber; // Current condition number
    private string csvPathQ1; // change this
    private string csvPathQ2; // change this
    private string csvPathQ3; // change this

    // 30.06.2025 end
    // 03.07.2025 begin
    private string questionLogPath = @"D:\LegoVR\unity-lego-vr\Other_than_in_project_files\Q_Data"; // target directory
    // 03.07.2025 end

    private int currentItemIndex = 0;
    // 01.07.2025 begin
    private int currentResourceBrickIndex = 0;
    // 01.07.2025 end

    // 03.07.2025 begin
    [Header("Likert Question System")]
    public GameObject questionCanvas;
    public GameObject questionPanel;
    public GameObject[] likertButtons;
    public GameObject button1Canvas;
    public GameObject button4Canvas;
    public GameObject button7Canvas;
    public TMP_Text mentalDemandText;
    public TMP_Text successText;

    // 10.07.2025 begin
    public TMP_Text complexityText; // Text to display complexity question
    // 10.07.2025 end

    public GameObject nextItemButton; // same position as continue button but loads next item
    public GameObject continueButton; // same position as next item button but continues to next question
    public GameObject continuePanel; // implements text for nextitembutton and continue button (display "Next" btw questions and "Finish" between item and questions)
    // 03.07.2025 end
    // 05.08.2025 begin
    public GameObject modelBuildingPlate; // plate where the model is built on on work desk
    // 05.08.2025 end

    // 10.07.2025 begin
    public GameObject instructionCanvas; // anvas holding text to press next between questions and indicating that condition is over
    // 10.07.2025 end

    private int questionPhase = 1;

    public global::System.String QuestionLogPath { get => questionLogPath; set => questionLogPath = value; }

    // 30.06.2025 end

    // 08.08.2025 begin
    [Header("Finalization UI")]
    [Tooltip("Message shown when the experiment is fully completed.")]
    public TMP_Text finalMessageText; // Should read: "Experiment completed. Thanks for your participation!"

    [Tooltip("Countdown text shown right before quitting the application.")]
    public TMP_Text finalCountdownText; // Shows: "Quitting in 1..."

    [Header("Finalization Timing (seconds)")]
    [Tooltip("How long the final message is displayed before the countdown starts.")]
    public float finalMessageDisplaySeconds = 3f;

    [Tooltip("Length of the final countdown before quitting.")]
    public float finalCountdownSeconds = 1f;
    // 08.08.2025 end

    // 13.03.2026 begin
    [Header("Condition Selection UI")]
    [Tooltip("Canvas or panel that holds the experimenter condition selection UI (3 buttons + instruction text).")]
    public GameObject conditionSelectionCanvas;

    [Tooltip("Button for Condition 1 (e.g., Condition1Constant).")]
    public Button condition1Button;
    [Tooltip("Button for Condition 2 (e.g., Condition2Delay).")]
    public Button condition2Button;
    [Tooltip("Button for Condition 3 (e.g., Condition3Once).")]
    public Button condition3Button;

    [Tooltip("Image component used to color the Condition 1 button.")]
    public Image condition1Image;
    [Tooltip("Image component used to color the Condition 2 button.")]
    public Image condition2Image;
    [Tooltip("Image component used to color the Condition 3 button.")]
    public Image condition3Image;

    [Tooltip("Instruction text shown to the experimenter when the experiment is paused between conditions.")]
    public TMP_Text conditionSelectionInstructionText;

    // Default button color, cached at runtime.
    private Color defaultButtonColor = Color.white;

    // Flag to ensure finalization is only started once.
    private bool experimentFinalizationStarted = false;

    // Flag to prevent interrupt logic from running multiple times.
    private bool interruptStarted = false;

    public bool IsInterrupting => interruptStarted;

    // Path where ModelOrder CSVs are written (must match RandomModelManager / ParticipantInputManager).
    private readonly string modelOrderPath = @"D:\LegoVR\unity-lego-vr\Other_than_in_project_files\Model_Order_Data";
    // 13.03.2026 end

    private void Start()
    {
        // Set target frame rate to 90 FPS for optimal VR performance
        Application.targetFrameRate = 90;
        Debug.Log("GameManager: Set target frame rate to 90 FPS for VR optimization");

        // 16.07.2025 begin
        RandomModelManager.Instance.AssignPrefabsToGameManager(this);
        // 16.07.2025 end
        // 30.07.2025 begin
        // Initialize participant code from PlayerPrefs or set a default value
        string playerPrefsValue = PlayerPrefs.GetString("ParticipantCode", "NOT_FOUND");
        Debug.Log($"GameManager: PlayerPrefs value for ParticipantCode: '{playerPrefsValue}'");
        
        participantCode = PlayerPrefs.GetString("ParticipantCode", "P001");
        Debug.Log($"GameManager: Retrieved participant code from PlayerPrefs: '{participantCode}'");
        
        // Inform LSL about condition/scene and emit CONDITION_START marker if LSL is present.
        var lsl = LslOutletManager.Instance;
        if (lsl != null)
        {
            string sceneName = SceneManager.GetActiveScene().name;
            lsl.SetSessionInfo(participantCode, trialNumber, sceneName);
            lsl.PushMarker($"CONDITION_START;{participantCode};cond={trialNumber};scene={sceneName}");
        }

        // Start tracking managers with the correct participant code
        StartTrackingManagers();
        // 30.07.2025 end
        StartCoroutine(ValidateAndLoadItem());

        // 13.03.2026 begin
        // Cache the default button color from one of the condition images if available.
        if (condition1Image != null)
        {
            defaultButtonColor = condition1Image.color;
        }
        // Hide condition selection UI on scene start (will be shown only between conditions).
        if (conditionSelectionCanvas != null)
        {
            conditionSelectionCanvas.SetActive(false);
        }
        // 13.03.2026 end
    }

    /// <summary>
    /// Starts all tracking managers with the correct participant code.
    /// </summary>
    private void StartTrackingManagers()
    {
        Debug.Log($"GameManager: Starting tracking managers with participant code: '{participantCode}'");
        
        // Find and start all tracking managers
        var eyeTrackingManager = FindObjectOfType<EyeTrackingManager>();
        if (eyeTrackingManager != null && eyeTrackingManager.trackingEnabled)
        {
            eyeTrackingManager.StartLoggingManually(trialNumber);
        }
        
        var viveTrackerManager = FindObjectOfType<ViveTrackerManager>();
        if (viveTrackerManager != null && viveTrackerManager.trackingEnabled)
        {
            viveTrackerManager.StartLoggingManually(trialNumber);
        }

        // Brick relation tracking (snap events between bricks/board)
        var bricksRelationTracker = FindObjectOfType<BricksRelationTracker>();
        if (bricksRelationTracker != null && bricksRelationTracker.trackingEnabled)
        {
            bricksRelationTracker.StartLoggingManually(trialNumber);
        }
    }

    // function is triggered by the NextItembutton in the scene. it is initially enabled
    public void LoadNextItem()
    {

        currentItemIndex++;
        // 01.07.2025 begin
        currentResourceBrickIndex++;
        // 01.07.2025 end

        // if we have still items in the condition 
        /// 08.07.2025 to be able to load the last item and the last questions needs to run through process once more than tiem in list aka <= instead of < from before
        /// since making the break for the next scene is in couroutine in ValidateAndLoad next item, have only ifstatement here
        if (currentItemIndex <= modelPrefabs.Length) 
        {
            // 30.06.2025 begin
            // Before loading the next item Show question panel
            ShowQuestion();  // <- Don't load item yet
            // 30.06.2025 end
        }

        //else
        //{
        //    StartCoroutine(BreakBeforeNextScene());
        //}
        // 08.07.2025 end
    }

    /// <summary>
    /// Shows the complexity question UI. 30.06.2025
    /// This method is called after each item is completed
    /// to gather feedback on the complexity of the item.
    /// </summary>
    /*
    void ShowComplexityQuestion()
    {
        // first disable start + instruction message, validation and next level buttons if not already done
        startValidator.startMessage.SetActive(false);
        startValidator.ValidationButtonLeft.SetActive(false);
        startValidator.ValidationButtonRight.SetActive(false);  
        startValidator.NextLevelButton.SetActive(false);
        startValidator.fixationCross.SetActive(false);  

        // then show the question panel
        questionPanel.SetActive(true);
        questionText.text = "How complex was the item you just built?";

        easyButton.SetActive(true);
        mediumButton.SetActive(true);
        hardButton.SetActive(true);

    }
    */

    public void ShowQuestion()
    {
        // first disable start + instruction message, validation and next level and continue buttons if not already done
        // now next level function and validation cannot be triggered because objects are not in scene
        startValidator.startMessage.SetActive(false);
        startValidator.ValidationButtonLeft.SetActive(false);
        startValidator.ValidationButtonRight.SetActive(false);
        startValidator.fixationCross.SetActive(false);
        fixationPanel.SetActive(false); // Disable fixation panel
        nextItemButton.SetActive(false); // Disable next item button
        // 05.08.2025 begin
        modelBuildingPlate.SetActive(false); // Disable model building plate
        // 05.08.2025 end

        // 10.07.2025 begin
        // disable the texts on the continue/next button since it is not visible anyway
        continuePanel.transform.Find("NextText").gameObject.SetActive(false);
        continuePanel.transform.Find("FinishText").gameObject.SetActive(false);

        // disable instruction texts between questions and finishing condition
        instructionCanvas.transform.Find("PressNextText").gameObject.SetActive(false);
        instructionCanvas.transform.Find("ConditionFinishText").gameObject.SetActive(false);

        // nextCanvas.SetActive(false); // Disable next canvas to hide continue button
        // 10.07.2025 end

        // then show the question panel canvas. continue button false by default
        questionCanvas.SetActive(true);
        questionPanel.SetActive(true);

        // if question one is to be answered 
        if (questionPhase == 1)
        {
            //questionText.text = "How mentally demanding was the item you just built?";

            // Before clearing, mark the just-completed model as completed in the model-order CSV.
            // At this point, currentItemIndex has already been incremented for the next item,
            // so the completed model corresponds to currentItemIndex - 1.
            int completedOrderIndex = Mathf.Clamp(currentItemIndex - 1, 0, modelPrefabs.Length - 1);
            if (RandomModelManager.Instance != null)
            {
                RandomModelManager.Instance.MarkModelCompleted(participantCode, trialNumber, completedOrderIndex);
            }

            // LSL markers: model-building phase end and question-answering phase start for the completed trial.
            var lsl = LslOutletManager.Instance;
            if (lsl != null)
            {
                string completedModelName =
                    (completedOrderIndex >= 0 && completedOrderIndex < modelPrefabs.Length && modelPrefabs[completedOrderIndex] != null)
                        ? modelPrefabs[completedOrderIndex].name
                        : "UnknownModel";
                // Model building just ended for this item.
                lsl.PushMarker($"TRIAL_PHASE;model_end;{participantCode};cond={trialNumber};item={completedOrderIndex};model={completedModelName}");
                // Question answering just began.
                lsl.PushMarker($"TRIAL_PHASE;question_start;{participantCode};cond={trialNumber};item={completedOrderIndex};model={completedModelName}");
            }

            //clear the current model and resource brick spawn points
            foreach (Transform child in modelSpawnPoint)
                Destroy(child.gameObject);
            foreach (Transform child in resourceBrickSpawnPoint)
                Destroy(child.gameObject);

            // show all text for question 1, but not the ones for question 2 or 3
            mentalDemandText.gameObject.SetActive(true);
            successText.gameObject.SetActive(false);
            button1Canvas.transform.Find("Question1").gameObject.SetActive(true);
            button4Canvas.transform.Find("Question1").gameObject.SetActive(true);
            button7Canvas.transform.Find("Question1").gameObject.SetActive(true);
            button1Canvas.transform.Find("Question2").gameObject.SetActive(false);
            button4Canvas.transform.Find("Question2").gameObject.SetActive(false);
            button7Canvas.transform.Find("Question2").gameObject.SetActive(false);

            // 10.07.2025 begin
            // hide the question 3 text as it is not used in this version + set complexity text false
            complexityText.gameObject.SetActive(false); // Hide complexity question text
            button1Canvas.transform.Find("Question3").gameObject.SetActive(false);
            button4Canvas.transform.Find("Question3").gameObject.SetActive(false);
            button7Canvas.transform.Find("Question3").gameObject.SetActive(false);
            // 10.07.2025 end

            nextItemButton.SetActive(false); // Disable next item button
            continueButton.SetActive(false); // Disable continue button to prevent multiple responses

            // 10.07.2025 begin
            // disable the texts on the continue/next button since it is not visible anyway
            continuePanel.transform.Find("NextText").gameObject.SetActive(false);
            continuePanel.transform.Find("FinishText").gameObject.SetActive(false);

            // disable instruction texts between questions and finishing condition
            instructionCanvas.transform.Find("PressNextText").gameObject.SetActive(false);
            instructionCanvas.transform.Find("ConditionFinishText").gameObject.SetActive(false);
            // nextCanvas.SetActive(false); // Disable next canvas to hide continue/next button text
            // 10.07.2025 end

            // show all likert buttons for question 1
            foreach (var btn in likertButtons)
                btn.SetActive(true);
            // now only the buttons for the response can be pressed -> on press then in OnquestionResponse(int response) is triggered (look in questionbuttonresponsevalue script)

            // the next level button and continue button are disabled
        }
        else if (questionPhase == 2) // if the second question is to be answered
        {
            //questionText.text = "How successful were you in building the item?";
            // show all text for question 2, but not the ones for question 1
            // hide the text for question 1
            mentalDemandText.gameObject.SetActive(false);
            successText.gameObject.SetActive(true);
            button1Canvas.transform.Find("Question2").gameObject.SetActive(true);
            button4Canvas.transform.Find("Question2").gameObject.SetActive(true);
            button7Canvas.transform.Find("Question2").gameObject.SetActive(true);
            button1Canvas.transform.Find("Question1").gameObject.SetActive(false);
            button4Canvas.transform.Find("Question1").gameObject.SetActive(false);
            button7Canvas.transform.Find("Question1").gameObject.SetActive(false);

            // 10.07.2025 begin
            // hide the question 3 text as it is not used in this version + set complexity text false
            complexityText.gameObject.SetActive(false); // Hide complexity question text
            button1Canvas.transform.Find("Question3").gameObject.SetActive(false);
            button4Canvas.transform.Find("Question3").gameObject.SetActive(false);
            button7Canvas.transform.Find("Question3").gameObject.SetActive(false);
            // 10.07.2025 end

            nextItemButton.SetActive(false); // Disable next item button
            continueButton.SetActive(false); // Disable continue button to prevent multiple responses
            // 10.07.2025 begin
            // disable the texts on the continue/next button since it is not visible anyway
            continuePanel.transform.Find("NextText").gameObject.SetActive(false);
            continuePanel.transform.Find("FinishText").gameObject.SetActive(false);
            // disable instruction texts between questions and finishing condition
            instructionCanvas.transform.Find("PressNextText").gameObject.SetActive(false);
            instructionCanvas.transform.Find("ConditionFinishText").gameObject.SetActive(false);
            // nextCanvas.SetActive(false); // Disable next canvas to hide continue button text
            // 10.07.2025 end



            // show all likert buttons for question 2
            foreach (var btn in likertButtons)
                btn.SetActive(true);
            // now only the buttons for the response can be pressed -> on press then in OnquestionResponse(int response) is triggered (look in questionbuttonresponsevalue script)
            // the next level button and continue button are disabled
        } // 10.07.2025 begin
        else if (questionPhase == 3) // if the third question is to be answered
        {
            //questionText.text = "How complex was the item you just built?";

            // show all text for question 3, but not the ones for question 1 or 2
            mentalDemandText.gameObject.SetActive(false);
            successText.gameObject.SetActive(false);
            button1Canvas.transform.Find("Question3").gameObject.SetActive(true);
            button4Canvas.transform.Find("Question3").gameObject.SetActive(true);
            button7Canvas.transform.Find("Question3").gameObject.SetActive(true);
            button1Canvas.transform.Find("Question1").gameObject.SetActive(false);
            button4Canvas.transform.Find("Question1").gameObject.SetActive(false);
            button7Canvas.transform.Find("Question1").gameObject.SetActive(false);
            button1Canvas.transform.Find("Question2").gameObject.SetActive(false);
            button4Canvas.transform.Find("Question2").gameObject.SetActive(false);
            button7Canvas.transform.Find("Question2").gameObject.SetActive(false);

            // 10.07.2025 begin
            // show complexity question text
            complexityText.gameObject.SetActive(true); // Show complexity question text
            // 10.07.2025 end

            nextItemButton.SetActive(false); // Disable next item button
            continueButton.SetActive(false); // Disable continue button to prevent multiple responses
            // 10.07.2025 begin
            // disable the texts on the continue/next button since it is not visible anyway
            continuePanel.transform.Find("NextText").gameObject.SetActive(false);
            continuePanel.transform.Find("FinishText").gameObject.SetActive(false);
            // disable instruction texts between questions and finishing condition
            instructionCanvas.transform.Find("PressNextText").gameObject.SetActive(false);
            instructionCanvas.transform.Find("ConditionFinishText").gameObject.SetActive(false);
            // nextCanvas.SetActive(false); // Disable next canvas to hide continue/next button text
            // 10.07.2025 end

            // show all likert buttons for question 3
            foreach (var btn in likertButtons)
                btn.SetActive(true);
        } // 10.07.2025 end
    }

    /// <summary>
    /// Saves the complexity response to a CSV file. 30.06.2025
    /// This method is called when the player selects a complexity level.
    /// </summary>
    /// 
    /*
    public void OnComplexityResponse(string response)
    {

        // Initialize CSV if not done yet
        // Save response to CSV
        SaveResponseToCSV(response);

        // disable the question panel and buttons
        easyButton.gameObject.SetActive(false);
        mediumButton.gameObject.SetActive(false);
        hardButton.gameObject.SetActive(false);
        questionPanel.SetActive(false);
        Debug.Log($"Complexity response received: {response}");

        // Now begin the validation for the next item
        startValidator.ResetValidator();  // Shows buttons and fixation cross

        // Wait for validation to complete before loading the item
        StartCoroutine(ValidateAndLoadItem());
    }
    */
    public void OnQuestionResponse(string response)
    {
        // this function is triggered if a button press on the 7 response buttons has been triggered
        // response is the value of the button pressed (1-7)
        // function will be called twice as continue button will be pressed after first question to trigger the second question due to increment in first if part here
        // because this function is ran through twice and after question 1 continue button is enabled, first disabe the continue button and then enable it again after the second question is answered
        continueButton.SetActive(false); // Disable continue button to prevent multiple responses
        // 10.07.2025 begin
        // disable the texts on the continue/next button since it is not visible anyway
        continuePanel.transform.Find("NextText").gameObject.SetActive(false);
        continuePanel.transform.Find("FinishText").gameObject.SetActive(false);
        // disable instruction texts between questions and finishing condition
        instructionCanvas.transform.Find("PressNextText").gameObject.SetActive(false);
        instructionCanvas.transform.Find("ConditionFinishText").gameObject.SetActive(false);
        // nextCanvas.SetActive(false); // Disable next canvas to hide continue button
        // 10.07.2025 end

        if (questionPhase == 1)
        {

            // Save the response to a variable
            SaveResponseToCSV(response);
            Debug.Log($"Mental Load response received: {response}");

            // Hide the question panel and buttons but enable the continue button
            questionCanvas.SetActive(false);
            questionPanel.SetActive(false);

            mentalDemandText.gameObject.SetActive(false);
            button1Canvas.transform.Find("Question1").gameObject.SetActive(false);
            button4Canvas.transform.Find("Question1").gameObject.SetActive(false);
            button7Canvas.transform.Find("Question1").gameObject.SetActive(false);
            nextItemButton.SetActive(false); // Disable next item button
            // 10.07.2025 begin
            // disable the texts on the continue/next button since it is not visible anyway
            continuePanel.transform.Find("NextText").gameObject.SetActive(false);
            continuePanel.transform.Find("FinishText").gameObject.SetActive(false);
            // disable instruction texts between questions and finishing condition
            instructionCanvas.transform.Find("PressNextText").gameObject.SetActive(false);
            instructionCanvas.transform.Find("ConditionFinishText").gameObject.SetActive(false);
            // nextCanvas.SetActive(false); // Disable next canvas to hide continue button
            // 10.07.2025 end



            foreach (var btn in likertButtons)
                btn.SetActive(false);
            continueButton.SetActive(true); // Enable continue button to proceed to the next question

            // 10.07.2025 begin
            // disable the texts for finish and enable text to press next to go to next question
            continuePanel.transform.Find("NextText").gameObject.SetActive(true);
            continuePanel.transform.Find("FinishText").gameObject.SetActive(false);
            // enable  instruction texts between questions and not finishing condition
            instructionCanvas.transform.Find("PressNextText").gameObject.SetActive(true);
            instructionCanvas.transform.Find("ConditionFinishText").gameObject.SetActive(false);
            // nextCanvas.SetActive(true); // Enable next canvas to show the continue button

            // 10.07.2025 end


            // now only this button can be pressed trigeguring the OnQuestionResponse(int response) function again but with questionPhase == 2

            // Increment the question phase to show the success question next time
            questionPhase = 2;

        }
        else if (questionPhase == 2)
        {
            // Save the response to a variable
            SaveResponseToCSV(response);
            Debug.Log($"Success response received: {response}");

            // Hide the question panel and buttons but enable the next item button
            questionCanvas.SetActive(false);
            questionPanel.SetActive(false);
            successText.gameObject.SetActive(false);
            nextItemButton.SetActive(false); // Enable next item button to proceed to the next item
            continueButton.SetActive(false); // Disable continue button to prevent multiple SuccessResponses

            // 10.07.2025 begin
            // disable the texts for finish and next to go to next question
            continuePanel.transform.Find("NextText").gameObject.SetActive(false);
            continuePanel.transform.Find("FinishText").gameObject.SetActive(false);
            // disable instruction texts between questions and finishing condition
            instructionCanvas.transform.Find("PressNextText").gameObject.SetActive(false);
            instructionCanvas.transform.Find("ConditionFinishText").gameObject.SetActive(false);
            // nextCanvas.SetActive(false); // disable next canvas to show the continue button

            // 10.07.2025 end
            button1Canvas.transform.Find("Question2").gameObject.SetActive(false);
            button4Canvas.transform.Find("Question2").gameObject.SetActive(false);
            button7Canvas.transform.Find("Question2").gameObject.SetActive(false);

            
            foreach (var btn in likertButtons)
                btn.SetActive(false);
            continueButton.SetActive(true); // Enable continue button to proceed to the next question

            // 10.07.2025 begin
            // disable the texts for finish and enable text to press next to go to next question
            continuePanel.transform.Find("NextText").gameObject.SetActive(true);
            continuePanel.transform.Find("FinishText").gameObject.SetActive(false);
            // enable  instruction texts between questions and not finishing condition
            instructionCanvas.transform.Find("PressNextText").gameObject.SetActive(true);
            instructionCanvas.transform.Find("ConditionFinishText").gameObject.SetActive(false);
            // nextCanvas.SetActive(true); // Enable next canvas to show the continue button

            // 10.07.2025 end

            // now only this button can be pressed trigeguring the OnQuestionResponse(int response) function again but with questionPhase == 3

            // 10.07.2025 begin
            // Increment the question phase to show the success question next time
            questionPhase = 3;
            // 10.07.2025 end
        }
        else if (questionPhase == 3) // if the third question is to be answered
        {
            // Save the response to a variable
            SaveResponseToCSV(response);
            Debug.Log($"Complexity response received: {response}");

            // Hide the question panel and buttons but enable the continue button
            questionCanvas.SetActive(false);
            questionPanel.SetActive(false);
            complexityText.gameObject.SetActive(false);
            nextItemButton.SetActive(false); // Disable next item button
            continueButton.SetActive(false); // Disable continue button to prevent multiple responses

            // 10.07.2025 begin
            // disable the texts for finish and next to go to next question
            continuePanel.transform.Find("NextText").gameObject.SetActive(false);
            continuePanel.transform.Find("FinishText").gameObject.SetActive(false);
            // disable instruction texts between questions and finishing condition
            instructionCanvas.transform.Find("PressNextText").gameObject.SetActive(false);
            instructionCanvas.transform.Find("ConditionFinishText").gameObject.SetActive(false);
            // nextCanvas.SetActive(false); // disable next canvas to show the continue button

            // 10.07.2025 end

            button1Canvas.transform.Find("Question3").gameObject.SetActive(false);
            button4Canvas.transform.Find("Question3").gameObject.SetActive(false);
            button7Canvas.transform.Find("Question3").gameObject.SetActive(false);

            foreach (var btn in likertButtons)
                btn.SetActive(false);

            // Reset the question phase for the next item
            questionPhase = 1; // Reset to the first question for the next item

            // LSL markers: question-answering phase end and trial end after last question answered.
            var lsl = LslOutletManager.Instance;
            if (lsl != null)
            {
                int completedOrderIndex = Mathf.Clamp(currentItemIndex - 1, 0, modelPrefabs.Length - 1);
                string completedModelName =
                    (completedOrderIndex >= 0 && completedOrderIndex < modelPrefabs.Length && modelPrefabs[completedOrderIndex] != null)
                        ? modelPrefabs[completedOrderIndex].name
                        : "UnknownModel";

                // Question answering ended for this trial.
                lsl.PushMarker($"TRIAL_PHASE;question_end;{participantCode};cond={trialNumber};item={completedOrderIndex};model={completedModelName}");
                // Full trial ended.
                lsl.PushMarker($"TRIAL_END;{participantCode};cond={trialNumber};item={completedOrderIndex};model={completedModelName}");
            }

            // after question 3 is answered start validation for the next item
            StartCoroutine(ValidateAndLoadItem());
        }
    }
    /// <summary>
    /// Saves the complexity response to a CSV file.    30.06.2025
    /// This method is called when the player selects a complexity level.
    /// is triggered by the OnComplexityResponse(string response) function
    /// and the OnQuestionResponse(int response) function.
    /// </summary>

    // 03.07.2025 begin
    void SaveResponseToCSV(string response)
    { //05.08.2025 begin
        if (!iQuestionsTracking) // If tracking is disabled
        // 05.08.2025 end
            return;

        Directory.CreateDirectory(questionLogPath); // Ensure directory exists

        if (questionPhase == 1)
        {
            if (string.IsNullOrEmpty(csvPathQ1))
            {
                DateTime now = DateTime.Now; // 30.07.2025 begin changed Trial to condition
                csvPathQ1 = Path.Combine(questionLogPath, $"{participantCode}_MentalLoadResponses_Condition{trialNumber}_{now:yyyy-MM-dd}.csv");
            }

            bool fileExists = File.Exists(csvPathQ1);
            using (var writer = new StreamWriter(csvPathQ1, append: true))
            {
                if (!fileExists) // 30.07.2025 begin changed Trial to condition
                    writer.WriteLine("ParticipantCode,ConditionNumber,ItemNumber,Response");

                writer.WriteLine($"{participantCode},{trialNumber},{currentItemIndex},{response}");
            }
            // 30.07.2025 begin changed Trial to condition
            Debug.Log($"Mental Load response saved: {participantCode}, Condition: {trialNumber}, Item: {currentItemIndex}, Response: {response}");
        }
        else if (questionPhase == 2)
        {
            if (string.IsNullOrEmpty(csvPathQ2))
            {
                DateTime now = DateTime.Now; // 30.07.2025 begin changed Trial to condition
                csvPathQ2 = Path.Combine(questionLogPath, $"{participantCode}_SuccessResponses_Condition{trialNumber}_{now:yyyy-MM-dd}.csv");
            }

            bool fileExists = File.Exists(csvPathQ2);
            using (var writer = new StreamWriter(csvPathQ2, append: true))
            {
                if (!fileExists) // 30.07.2025 begin changed Trial to condition
                    writer.WriteLine("ParticipantCode,ConditionNumber,ItemNumber,Response");

                writer.WriteLine($"{participantCode},{trialNumber},{currentItemIndex},{response}");
            }
            // 30.07.2025 begin changed Trial to condition
            Debug.Log($"Success response saved: {participantCode}, Condition: {trialNumber}, Item: {currentItemIndex}, Response: {response}");
        } // 10.07.2025 begin
        else if (questionPhase == 3) // if the third question is to be answered
        {
            if (string.IsNullOrEmpty(csvPathQ3))
            {
                DateTime now = DateTime.Now; // 30.07.2025 begin changed Trial to condition
                csvPathQ3 = Path.Combine(questionLogPath, $"{participantCode}_ComplexityResponses_Condition{trialNumber}_{now:yyyy-MM-dd}.csv");
            }

            bool fileExists = File.Exists(csvPathQ3);
            using (var writer = new StreamWriter(csvPathQ3, append: true))
            {
                if (!fileExists) // 30.07.2025 begin changed Trial to condition
                    writer.WriteLine("ParticipantCode,ConditionNumber,ItemNumber,Response");

                writer.WriteLine($"{participantCode},{trialNumber},{currentItemIndex},{response}");
            }
            // 30.07.2025 begin changed Trial to condition
            Debug.Log($"Complexity response saved: {participantCode}, Condition: {trialNumber}, Item: {currentItemIndex}, Response: {response}");

        }  // 10.07.2025 end
    }

    // 03.07.2025 end

    // 15.07.2025 begin exposing currentitemindex for screenshotmanager
    public int GetCurrentItemIndex()
    {
        return currentItemIndex;
    }

    // 15.07.2025 end
    
    /// <summary>
    /// Handles validation, then loads current item once validation succeeds.
    /// </summary>
    IEnumerator ValidateAndLoadItem()
    {
        // 08.07.2025 begin
        // to load last item in lis AND the questions, ned to run thorugh process in activated in LoadNextItem() once more than items in Item list
        // -> need here to check whether need to start validation (currentItemIndex < modelPrefabs.Length) or if need ti break before next scene (currentItemIndex = modelPrefabs.Length)
        if (currentItemIndex < modelPrefabs.Length)
        {
            // New LSL markers: trial + validation phase start.
            var lsl = LslOutletManager.Instance;
            if (lsl != null)
            {
                string modelName = modelPrefabs[currentItemIndex] != null
                    ? modelPrefabs[currentItemIndex].name
                    : "UnknownModel";
                lsl.PushMarker($"TRIAL_START;{participantCode};cond={trialNumber};item={currentItemIndex};model={modelName}");
                lsl.PushMarker($"TRIAL_PHASE;validation_start;{participantCode};cond={trialNumber};item={currentItemIndex}");
            }

            // Step 1: Reset the fixation/button validation UI
            startValidator.ResetValidator();

            // Step 2: Wait until player completes validation
            yield return new WaitUntil(() => startValidator.IsValidated);

            // LSL marker: validation phase end (immediately before model building begins).
            if (lsl != null)
            {
                lsl.PushMarker($"TRIAL_PHASE;validation_end;{participantCode};cond={trialNumber};item={currentItemIndex}");
            }

            // Step 3: spawn the new one in model (clearing took place in Showquestion function question phase 1)
            Instantiate(modelPrefabs[currentItemIndex], modelSpawnPoint);
            // 01.07.2025 begin

            // Step 4: Spawn resource bricks like models in step 3
            foreach (Transform child in resourceBrickSpawnPoint)
                Destroy(child.gameObject);

            Instantiate(resourceBrickPrefabs[currentResourceBrickIndex], resourceBrickSpawnPoint);
            // 01.07.2025 end

            // 30.07.2025 begin

            // LSL marker: model-building phase start.
            if (lsl != null)
            {
                string modelName = modelPrefabs[currentItemIndex] != null
                    ? modelPrefabs[currentItemIndex].name
                    : "UnknownModel";
                lsl.PushMarker($"TRIAL_PHASE;model_start;{participantCode};cond={trialNumber};item={currentItemIndex};model={modelName}");
            }

            // Log start of model building through TrackingManagers on TrackingManager
            EyeTrackingManager etTracker = GameObject.Find("TrackingManager")?.GetComponent<EyeTrackingManager>();
            ViveTrackerManager btTracker = GameObject.Find("TrackingManager")?.GetComponent<ViveTrackerManager>();

            if (etTracker != null)
            {
                etTracker.RecordModelBuildStart();
            }
            if (btTracker != null)
            {
                btTracker.RecordModelBuildStart();
            }
            // 30.07.2025 end


            // 03.07.2025 begin
            // Step 5: enable next level button for the next ruthrough
            nextItemButton.SetActive(true);


            // 10.07.2025 begin
            // disable the texts for next and enable text to press finish to go from model to first question
            continuePanel.transform.Find("NextText").gameObject.SetActive(false);
            continuePanel.transform.Find("FinishText").gameObject.SetActive(true);
            // nextCanvas.SetActive(true); // enable next canvas to hide continue button

            // 10.07.2025 end

            // 03.07.2025 end
            
            // 05.08.2025 begin
            modelBuildingPlate.SetActive(true); // Enable model building plate for building
            // 05.08.2025 end
        }
        else if (currentItemIndex == modelPrefabs.Length)
        {
            // 10.07.2025 begin
            // disable instruction texts between questions and enable finishing condition
            instructionCanvas.transform.Find("PressNextText").gameObject.SetActive(false);
            instructionCanvas.transform.Find("ConditionFinishText").gameObject.SetActive(true);
            // 10.07.2025 end

            // 13.03.2026 begin
            // Instead of automatically loading the next scene, pause the experiment
            // and show the experimenter condition selection UI that uses the ModelOrder CSV
            // to determine which conditions are completed.
            ShowExperimenterConditionSelection();
            // 13.03.2026 end
        } 
        // 08.07.2025 end
    }

    /// <summary>
    /// Waits for a break interval before loading the next level.
    /// </summary>
    IEnumerator BreakBeforeNextScene()
    {
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        bool hasNextScene = (currentSceneIndex + 1 < SceneManager.sceneCountInBuildSettings);

        if (hasNextScene)
        {
            // Keep existing break behavior for intermediate scenes
            Debug.Log($"Break time! Waiting for {breakDuration} seconds before next scene.");
            yield return new WaitForSeconds(breakDuration);

            // LSL marker: condition end before loading the next condition scene.
            SendConditionEndMarker();

            SceneManager.LoadScene(currentSceneIndex + 1);
        }
        else
        {
            // 08.08.2025 begin
            // Final scene reached: run finalization flow (no 15-minute break).
            Debug.Log("All levels complete! Initiating finalization and graceful quit.");
            // LSL marker: final condition end before experiment finalization.
            SendConditionEndMarker();
            yield return StartCoroutine(FinalizeAndQuit());
            // 08.08.2025 end
        }
    }

    // 08.08.2025 begin
    // Finalization flow to cleanly stop logging and quit application
    private IEnumerator FinalizeAndQuit()
    {
        // LSL marker: full experiment finished (all conditions complete).
        var lsl = LslOutletManager.Instance;
        if (lsl != null)
        {
            string sceneName = SceneManager.GetActiveScene().name;
            lsl.PushMarker($"EXPERIMENT_END;{participantCode};cond={trialNumber};scene={sceneName}");
        }

        // Step 1: Show final message so participant can read it
        if (finalMessageText != null)
        {
            finalMessageText.text = "Experiment completed. Thanks for your participation!";
            finalMessageText.gameObject.SetActive(true);
        }

        // Hide guidance texts related to continuing/finishing
        if (instructionCanvas != null)
        {
            var pressNext = instructionCanvas.transform.Find("PressNextText");
            if (pressNext != null) pressNext.gameObject.SetActive(false);
            var finishText = instructionCanvas.transform.Find("ConditionFinishText");
            if (finishText != null) finishText.gameObject.SetActive(true);
        }

        // Allow time to read the message
        yield return new WaitForSeconds(Mathf.Max(0f, finalMessageDisplaySeconds));

        // Step 2: Show short countdown before quitting
        int countdown = Mathf.Max(1, Mathf.RoundToInt(finalCountdownSeconds));
        for (int i = countdown; i >= 1; i--)
        {
            if (finalCountdownText != null)
            {
                finalCountdownText.text = $"Quitting in {i}...";
                finalCountdownText.gameObject.SetActive(true);
            }
            yield return new WaitForSeconds(1f);
        }

        // Step 3: Stop ET/BT logging explicitly (flush and close files)
        var eyeTrackingManager = FindObjectOfType<EyeTrackingManager>();
        if (eyeTrackingManager != null)
        {
            eyeTrackingManager.StopLoggingManually();
        }
        var viveTrackerManager = FindObjectOfType<ViveTrackerManager>();
        if (viveTrackerManager != null)
        {
            viveTrackerManager.StopLoggingManually();
        }

        // Ensure any PlayerPrefs are saved
        PlayerPrefs.Save();

        // Give one frame to ensure IO flushes completed on main thread
        yield return new WaitForEndOfFrame();

        // Step 4: Quit application
        Application.Quit();

#if UNITY_EDITOR
        // If running in the editor, stop play mode
        UnityEditor.EditorApplication.isPlaying = false;
#endif
        yield break;
    }
    // 08.08.2025 end

    // 25.03.2026 begin
    /// <summary>
    /// Immediately stops the current run, deletes ONLY the current participant's files
    /// for the current condition, moves them to the Windows recycle bin, and terminates.
    /// </summary>
    public void InterruptAndCleanupCurrentCondition()
    {
        if (interruptStarted) return;
        interruptStarted = true;

        // Prevent normal finalization logic from racing.
        experimentFinalizationStarted = true;

        // The experimenter condition selection UI sets timeScale=0.
        // Ensure our interrupt coroutine runs reliably.
        Time.timeScale = 1f;

        StartCoroutine(InterruptAndCleanupCurrentConditionRoutine());
    }

    private IEnumerator InterruptAndCleanupCurrentConditionRoutine()
    {
        // Step 1: Stop logging so files are not locked when deleting.
        var eyeTrackingManager = FindObjectOfType<EyeTrackingManager>();
        if (eyeTrackingManager != null) eyeTrackingManager.StopLoggingManually();

        var viveTrackerManager = FindObjectOfType<ViveTrackerManager>();
        if (viveTrackerManager != null) viveTrackerManager.StopLoggingManually();

        var bricksRelationTracker = FindObjectOfType<BricksRelationTracker>();
        if (bricksRelationTracker != null) bricksRelationTracker.StopLoggingManually();

        // Step 2: Wait one frame for IO flush.
        yield return new WaitForEndOfFrame();

        PlayerPrefs.Save();

        // Step 3: Delete current-condition files for this participant only.
        TryDeleteCurrentConditionFilesToRecycleBin();

        // Step 4: Terminate immediately.
        yield return new WaitForEndOfFrame();
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
        yield break;
    }

    private void TryDeleteCurrentConditionFilesToRecycleBin()
    {
        // Only delete for real conditions (1..3).
        int conditionNumber = trialNumber;
        if (conditionNumber < 1 || conditionNumber > 3) return;

        string code = participantCode;
        if (string.IsNullOrWhiteSpace(code)) return;

        // IMPORTANT: Do not touch ModelOrder CSVs (and do not delete other participants).
        // Filenames in your project are already structured as:
        //   {participantCode}_<TYPE>_Condition{conditionNumber}_<date>.csv
        //   {participantCode}_Condition{conditionNumber}_Model*.png
        string brDataPath = @"D:\LegoVR\unity-lego-vr\Other_than_in_project_files\BR_Data";
        string btDataPath = @"D:\LegoVR\unity-lego-vr\Other_than_in_project_files\BT_Data";
        string etDataPath = @"D:\LegoVR\unity-lego-vr\Other_than_in_project_files\ET_Data";
        string screenshotPath = @"D:\LegoVR\unity-lego-vr\Other_than_in_project_files\Screenshot_Data";

        var filesToDelete = new List<string>();

        filesToDelete.AddRange(SafeGetFiles(brDataPath, $"{code}_BR_Data_Condition{conditionNumber}_*.csv"));
        filesToDelete.AddRange(SafeGetFiles(btDataPath, $"{code}_BT_Data_Condition{conditionNumber}_*.csv"));
        filesToDelete.AddRange(SafeGetFiles(etDataPath, $"{code}_ET_Data_Condition{conditionNumber}_*.csv"));

        // Question responses are saved into Q_Data with condition-tagged filenames.
        filesToDelete.AddRange(SafeGetFiles(questionLogPath, $"{code}_MentalLoadResponses_Condition{conditionNumber}_*.csv"));
        filesToDelete.AddRange(SafeGetFiles(questionLogPath, $"{code}_SuccessResponses_Condition{conditionNumber}_*.csv"));
        filesToDelete.AddRange(SafeGetFiles(questionLogPath, $"{code}_ComplexityResponses_Condition{conditionNumber}_*.csv"));

        // Screenshots: {participantCode}_Condition{N}_Model..._{Front/Left/Right}_<timestamp>.png
        filesToDelete.AddRange(SafeGetFiles(screenshotPath, $"{code}_Condition{conditionNumber}_Model*.png"));

        RecycleBinDeleteUtility.DeleteFilesToRecycleBin(filesToDelete);
    }

    private static IEnumerable<string> SafeGetFiles(string dir, string pattern)
    {
        if (string.IsNullOrWhiteSpace(dir)) yield break;
        if (!Directory.Exists(dir)) yield break;

        string[] files = null;
        try
        {
            files = Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly);
        }
        catch
        {
            yield break;
        }

        if (files == null) yield break;
        foreach (var f in files)
        {
            if (!string.IsNullOrWhiteSpace(f)) yield return f;
        }
    }

    // 25.03.2026 end

    // 13.03.2026 begin
    /// <summary>
    /// Sends a CONDITION_END marker via LSL for the current scene/condition.
    /// </summary>
    private void SendConditionEndMarker()
    {
        var lsl = LslOutletManager.Instance;
        if (lsl != null)
        {
            string sceneName = SceneManager.GetActiveScene().name;
            lsl.PushMarker($"CONDITION_END;{participantCode};cond={trialNumber};scene={sceneName}");
        }
    }

    /// <summary>
    /// Reads the latest ModelOrder CSV and determines, for the current participant,
    /// whether each of the three conditions (1,2,3) is fully completed.
    /// A condition is considered completed if there is at least one row for it
    /// and there is no row with Completed == False.
    /// </summary>
    private bool TryGetConditionCompletionFromModelOrder(
        string participant,
        out bool cond1Complete,
        out bool cond2Complete,
        out bool cond3Complete)
    {
        cond1Complete = cond2Complete = cond3Complete = false;

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

            bool[] hasAnyRow = new bool[4];        // 0 unused, 1..3
            bool[] hasIncomplete = new bool[4];    // 0 unused, 1..3

            string[] lines = File.ReadAllLines(latestCsv.FullName);
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                string[] parts = lines[i].Split(',');
                if (parts.Length < 6) continue;

                string csvParticipant = parts[0].Trim();
                if (!string.Equals(csvParticipant, participant, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!int.TryParse(parts[1].Trim(), out int conditionNumber))
                {
                    continue;
                }
                if (conditionNumber < 1 || conditionNumber > 3)
                {
                    continue;
                }

                string completedFlag = parts[5].Trim();
                bool isCompleted = completedFlag.Equals("True", StringComparison.OrdinalIgnoreCase);

                hasAnyRow[conditionNumber] = true;
                if (!isCompleted)
                {
                    hasIncomplete[conditionNumber] = true;
                }
            }

            cond1Complete = hasAnyRow[1] && !hasIncomplete[1];
            cond2Complete = hasAnyRow[2] && !hasIncomplete[2];
            cond3Complete = hasAnyRow[3] && !hasIncomplete[3];

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"GameManager: Failed to read ModelOrder CSV for condition completion. {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Configures the experimenter condition selection UI based on the ModelOrder CSV
    /// for the current participant: completed conditions are dark grey and not interactable,
    /// incomplete conditions are normal and clickable. Also sets the instruction text.
    /// </summary>
    private void ConfigureConditionSelectionUI()
    {
        if (conditionSelectionCanvas == null ||
            conditionSelectionInstructionText == null ||
            condition1Button == null || condition2Button == null || condition3Button == null)
        {
            Debug.LogWarning("GameManager: Condition selection UI is not fully wired. Falling back to automatic scene progression.");
            StartCoroutine(BreakBeforeNextScene());
            return;
        }

        bool cond1Complete, cond2Complete, cond3Complete;
        bool success = TryGetConditionCompletionFromModelOrder(participantCode, out cond1Complete, out cond2Complete, out cond3Complete);

        // Build list of completed conditions for instruction text.
        List<int> completedList = new List<int>();
        if (cond1Complete) completedList.Add(1);
        if (cond2Complete) completedList.Add(2);
        if (cond3Complete) completedList.Add(3);

        string completedPart;
        if (completedList.Count > 0)
        {
            completedPart = $"Condition {string.Join(",", completedList)} completed.";
        }
        else
        {
            completedPart = "No conditions completed yet.";
        }

        bool allCompleted = cond1Complete && cond2Complete && cond3Complete;

        if (allCompleted)
        {
            conditionSelectionInstructionText.text =
                "Experiment paused. Conditions 1,2 and 3 completed. Grey conditions are completed and cannot be selected. You may now end the experiment.";

            // When all conditions are completed, immediately begin the finalization flow
            // (short final message + countdown + quit), regardless of which condition scene
            // we are currently in. Resume time so FinalizeAndQuit's WaitForSeconds work.
            if (!experimentFinalizationStarted)
            {
                experimentFinalizationStarted = true;
                Time.timeScale = 1f;
                StartCoroutine(FinalizeAndQuit());
            }
        }
        else
        {
            conditionSelectionInstructionText.text =
                $"Experiment paused. Grey conditions are completed and cannot be selected. {completedPart} Please select a remaining condition to continue the experiment.";
        }

        // Helper to set button state and color.
        void SetButtonState(Button button, Image image, bool isCompleted)
        {
            if (button == null) return;

            button.interactable = !isCompleted;

            if (image != null)
            {
                image.color = isCompleted ? new Color(0.3f, 0.3f, 0.3f) : defaultButtonColor;
            }
        }

        SetButtonState(condition1Button, condition1Image, cond1Complete);
        SetButtonState(condition2Button, condition2Image, cond2Complete);
        SetButtonState(condition3Button, condition3Image, cond3Complete);
    }

    /// <summary>
    /// Shows the experimenter condition selection canvas instead of automatically
    /// loading the next scene when a condition has finished.
    /// </summary>
    private void ShowExperimenterConditionSelection()
    {
        if (conditionSelectionCanvas == null)
        {
            Debug.LogWarning("GameManager: conditionSelectionCanvas is not assigned. Falling back to automatic scene progression.");
            StartCoroutine(BreakBeforeNextScene());
            return;
        }

        // Pause game time while the experimenter chooses the next condition.
        Time.timeScale = 0f;

        conditionSelectionCanvas.SetActive(true);
        ConfigureConditionSelectionUI();
    }

    /// <summary>
    /// Experimenter clicked on Condition 1 in the selection UI.
    /// Loads the Condition1 scene if it is not yet completed.
    /// </summary>
    public void OnSelectCondition1()
    {
        HandleConditionSelection(1, "Condition1Constant");
    }

    /// <summary>
    /// Experimenter clicked on Condition 2 in the selection UI.
    /// Loads the Condition2 scene if it is not yet completed.
    /// </summary>
    public void OnSelectCondition2()
    {
        HandleConditionSelection(2, "Condition2Delay");
    }

    /// <summary>
    /// Experimenter clicked on Condition 3 in the selection UI.
    /// Loads the Condition3 scene if it is not yet completed.
    /// </summary>
    public void OnSelectCondition3()
    {
        HandleConditionSelection(3, "Condition3Once");
    }

    /// <summary>
    /// Shared logic for selecting a condition from the experimenter UI.
    /// Verifies via the ModelOrder CSV that the target condition is not already
    /// completed, then sends a CONDITION_END marker and loads the target scene.
    /// </summary>
    private void HandleConditionSelection(int conditionNumber, string sceneName)
    {
        bool cond1Complete, cond2Complete, cond3Complete;
        bool success = TryGetConditionCompletionFromModelOrder(participantCode, out cond1Complete, out cond2Complete, out cond3Complete);

        bool isCompleted = conditionNumber switch
        {
            1 => cond1Complete,
            2 => cond2Complete,
            3 => cond3Complete,
            _ => false
        };

        if (isCompleted)
        {
            Debug.LogWarning($"GameManager: Condition {conditionNumber} is already completed. Selection ignored.");
            return;
        }

        // Optional: store selected condition in PlayerPrefs for logging / LSL session info.
        PlayerPrefs.SetInt("SelectedCondition", conditionNumber);
        PlayerPrefs.Save();

        // Send CONDITION_END marker for the current condition before switching scenes.
        SendConditionEndMarker();

        // Resume game time before loading the next condition scene.
        Time.timeScale = 1f;

        // Hide the selection UI before loading the next scene.
        if (conditionSelectionCanvas != null)
        {
            conditionSelectionCanvas.SetActive(false);
        }

        Debug.Log($"GameManager: Loading condition {conditionNumber} via experimenter selection. Scene: {sceneName}");
        SceneManager.LoadScene(sceneName);
    }
    // 13.03.2026 end
}
