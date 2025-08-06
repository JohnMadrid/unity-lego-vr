using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System;
using System.IO;
using System.Collections.Generic;
using TMPro; 

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
    public int trialNumber; // Current trial number
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
        
        // Start tracking managers with the correct participant code
        StartTrackingManagers();
        // 30.07.2025 end
        StartCoroutine(ValidateAndLoadItem());
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
            eyeTrackingManager.StartLoggingManually();
        }
        
        var viveTrackerManager = FindObjectOfType<ViveTrackerManager>();
        if (viveTrackerManager != null && viveTrackerManager.trackingEnabled)
        {
            viveTrackerManager.StartLoggingManually();
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
            // Step 1: Reset the fixation/button validation UI
            startValidator.ResetValidator();

            // Step 2: Wait until player completes validation
            yield return new WaitUntil(() => startValidator.IsValidated);

            // Step 3: spawn the new one in model (clearing took place in Showquestion function question phase 1)
            Instantiate(modelPrefabs[currentItemIndex], modelSpawnPoint);
            // 01.07.2025 begin

            // Step 4: Spawn resource bricks like models in step 3
            foreach (Transform child in resourceBrickSpawnPoint)
                Destroy(child.gameObject);

            Instantiate(resourceBrickPrefabs[currentResourceBrickIndex], resourceBrickSpawnPoint);
            // 01.07.2025 end

            // 30.07.2025 begin

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
            StartCoroutine(BreakBeforeNextScene());
        } 
        // 08.07.2025 end
    }

    /// <summary>
    /// Waits for a break interval before loading the next level.
    /// </summary>
    IEnumerator BreakBeforeNextScene()
    {
        Debug.Log($"Break time! Waiting for {breakDuration} seconds.");
        yield return new WaitForSeconds(breakDuration);

        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;

        if (currentSceneIndex + 1 < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(currentSceneIndex + 1);
        else
            Debug.Log("All levels complete!");
    }
}
