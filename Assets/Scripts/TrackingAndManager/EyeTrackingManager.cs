using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Varjo.XR;

/// <summary>
/// EyeTrackingManager handles eye tracking data collection and logging for Varjo Aero headset.
/// This script manages gaze data, eye measurements, and CSV file logging for experimental data collection.
/// 
/// Features:
/// - Automatic calibration with quality checking and unlimited retry logic
/// - Continuous calibration attempts until "Medium" or "High" quality is achieved
/// - Comprehensive debug logging for calibration process (controlled by enableDebugMode)
/// - CSV tracking of calibration status, attempts, and quality metrics
/// - Manual calibration restart capability
/// - Progress monitoring for extended calibration sessions
/// - Robust error handling and user feedback
/// </summary>
public class EyeTrackingManager : MonoBehaviour
{
    // File writing and logging control
    private StreamWriter writer;
    private bool logging = false;
    private string filePath;

    // Camera reference for HMD position tracking
    [SerializeField]
    private Camera xrCamera; // Assign XR headset camera in inspector
    
    // Main tracking control - can be toggled in inspector
    public bool trackingEnabled; // Can be toggled in inspector; default = false
    
    // Debug control - enables detailed gaze data structure logging
    [SerializeField]
    private bool enableDebugMode = false; // Checkbox in inspector to enable debug output
    
    // Debug control for gaze hit detection
    [SerializeField]
    private bool enableGazeHitDebug = false; // Checkbox in inspector to enable gaze hit debugging

    // 30.07.2025 begin
    private bool isBuildingModel = false;
    private string modelName = "";
    // 08.08.2025 begin remove unused field 'hitObjName' (CS0414)
    // Step: Remove the unused field to resolve the compiler warning; CSV logging uses local 'hitObjectName'.
    // 08.08.2025 end
    // 30.07.2025 end

    // 30.07.2025 begin
    private string participantCode; // Default value, will be set in Start()
    // 30.07.2025 end

     // Cached references
     private GameManager gameManager;
 
    // Calibration tracking variables
    private enum CalibrationState { Idle, Requesting, WaitingForUser, CheckingQuality, Succeeded }
    private CalibrationState calibrationState = CalibrationState.Idle;
    private int calibrationAttempts = 0;
    private int totalCalibrationAttempts = 0; // Total across all sessions
    private bool calibrationCompleted = false;
    private string leftEyeCalibrationQuality = "Unknown";
    private string rightEyeCalibrationQuality = "Unknown";
    private float calibrationStartTime = 0f;
    private float calibrationEndTime = 0f;
    
    // Calibration quality thresholds
    private readonly string[] acceptableQualities = { "Medium", "High" };
    private const float calibrationRetryDelay = 3.0f; // Delay between calibration attempts

    /// <summary>
    /// Initializes eye tracking system, calibrates the headset, and starts logging if enabled.
    /// </summary>
    void Start()
    {
        Debug.Log("Initializing Eye Tracking...");

         // Cache GameManager reference for condition and trial access
         gameManager = FindObjectOfType<GameManager>();
 
        // Debug gaze data structure only if debug mode is enabled
        if (enableDebugMode)
        {
            DebugGazeDataStructure();
        }

        // Start the calibration process
        StartCalibrationProcess();

        // Don't start logging immediately - wait for participant code to be set
        // StartLogging() will be called manually when participant code is ready
    }

    void Update()
    {
        // --- Calibration State Machine ---
        // This machine will only run until it reaches the 'Succeeded' state.
        if (calibrationState != CalibrationState.Succeeded)
        {
            switch (calibrationState)
            {
                case CalibrationState.Requesting:
                    // Attempt to request calibration from the Varjo system.
                    Debug.Log($"[CALIBRATION] Attempt {calibrationAttempts + 1}: Requesting calibration.");
                    if (VarjoEyeTracking.RequestGazeCalibration())
                    {
                        Debug.Log("[CALIBRATION] Request successful. Waiting for user to finish on-screen prompts.");
                        calibrationState = CalibrationState.WaitingForUser;
                    }
                    else
                    {
                        Debug.LogError("[CALIBRATION] Request failed. Retrying after delay.");
                        StartCoroutine(RetryCalibration("Request failed"));
                    }
                    break;

                case CalibrationState.WaitingForUser:
                    // Monitor the gaze status. While the user is calibrating, the status is 'Invalid'.
                    // Once they finish, it will switch to 'Valid'.
                    var gaze = VarjoEyeTracking.GetGaze();
                    if (gaze.status == VarjoEyeTracking.GazeStatus.Valid)
                    {
                        Debug.Log("[CALIBRATION] User finished on-screen prompts (Gaze is Valid). Checking quality...");
                        calibrationState = CalibrationState.CheckingQuality;
                    }
                    break;

                case CalibrationState.CheckingQuality:
                    // Check the calibration quality once the user is done.
                    var quality = VarjoEyeTracking.GetGazeCalibrationQuality();
                    leftEyeCalibrationQuality = quality.left.ToString();
                    rightEyeCalibrationQuality = quality.right.ToString();
                    Debug.Log($"[CALIBRATION] Quality Check - Left: {leftEyeCalibrationQuality}, Right: {rightEyeCalibrationQuality}");

                    if (System.Array.Exists(acceptableQualities, q => q == leftEyeCalibrationQuality) && System.Array.Exists(acceptableQualities, q => q == rightEyeCalibrationQuality))
                    {
                        Debug.Log("[CALIBRATION] Success! Quality is acceptable. Calibration is now complete for this scene.");
                        calibrationState = CalibrationState.Succeeded;
                        calibrationCompleted = true; // Set final flag
                    }
                    else
                    {
                        Debug.LogError("[CALIBRATION] Quality not acceptable. Retrying after delay.");
                        StartCoroutine(RetryCalibration("Poor quality"));
                    }
                    break;
            }
        }

        // --- Data Logging ---
        if (logging)
        {
            LogGazeData();
        }
    }

    /// <summary>
    /// Starts the calibration process.
    /// </summary>
    private void StartCalibrationProcess()
    {
        calibrationAttempts = 0;
        totalCalibrationAttempts++;
        calibrationCompleted = false;
        calibrationState = CalibrationState.Requesting;
        calibrationStartTime = Time.time;
    }

    /// <summary>
    /// Retries the calibration process after a delay.
    /// </summary>
    private System.Collections.IEnumerator RetryCalibration(string reason)
    {
        calibrationState = CalibrationState.Idle; // Pause state machine
        calibrationAttempts++;
        Debug.Log($"[CALIBRATION] Retrying due to: {reason}. Waiting {calibrationRetryDelay}s.");
        yield return new WaitForSeconds(calibrationRetryDelay);
        calibrationState = CalibrationState.Requesting; // Restart the process
    }
    
    /// <summary>
    /// Logs all available gaze data points to the CSV file.
    /// </summary>
    private void LogGazeData()
    {
        List<VarjoEyeTracking.GazeData> gazeDataList;
        List<VarjoEyeTracking.EyeMeasurements> eyeMeasurementsList;
        int dataCount = VarjoEyeTracking.GetGazeList(out gazeDataList, out eyeMeasurementsList);

        if (dataCount == 0) return;

        Transform modelSpawnPoint = null;
        var tutorialGM = GameObject.Find("TutorialGameManager")?.GetComponent<TutorialGameManager>();
        if (tutorialGM != null && tutorialGM.modelSpawnPoint != null)
        {
            modelSpawnPoint = tutorialGM.modelSpawnPoint;
        }
        else
        {
            var gameGM = GameObject.Find("GameManager")?.GetComponent<GameManager>();
            if (gameGM != null)
            {
                modelSpawnPoint = gameGM.modelSpawnPoint;
            }
        }
        string currentModelName = (modelSpawnPoint != null && modelSpawnPoint.childCount > 0)
            ? modelSpawnPoint.GetChild(0).gameObject.name.Replace("(Clone)", "").Trim()
            : "None";

        foreach (var gazeData in gazeDataList)
        {
            if (gazeData.status == VarjoEyeTracking.GazeStatus.Invalid) continue;

            var eyeMeasurements = eyeMeasurementsList.Find(m => m.frameNumber == gazeData.frameNumber);

            Vector3 hmdPosition = xrCamera.transform.position;
            Quaternion hmdRotation = xrCamera.transform.rotation;
            // Name derived from ray hit (stud -> parent brick normalization handled below)
            string hitObjectName = "None";
            // Compute condition and trial numbers
            int conditionNumber = (gameManager != null) ? gameManager.trialNumber : 0; // if gameManager not found (are in Tutorial Scene) therefore, 0. Because Tutorial Scene has Tutorialmanager
            int trialNumber = (gameManager != null) ? Mathf.Clamp(gameManager.GetCurrentItemIndex(), 0, 6) : 0; // see above
            // Try to get detailed hit info
            Transform hitTransform;
            GazeActivatable activatable;
            bool hasHit = TryGetGazeHit(gazeData, out hitTransform, out activatable);
            int modelVisibilityState = (hasHit && activatable != null && activatable.IsVisible) ? 1 : 0;
            Vector3 objPos = hasHit ? hitTransform.position : Vector3.zero;
            Quaternion objRot = hasHit ? hitTransform.rotation : Quaternion.identity;
            hitObjectName = GetLoggedObjectName(hitTransform, activatable);

            string csvEntry =
                $"{gazeData.captureTime},{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()},{Time.time},{gazeData.focusDistance},{gazeData.frameNumber},{gazeData.focusStability},{gazeData.status}," +
                $"{gazeData.gaze.forward.x},{gazeData.gaze.forward.y},{gazeData.gaze.forward.z}," +
                $"{gazeData.gaze.origin.x},{gazeData.gaze.origin.y},{gazeData.gaze.origin.z}," +
                $"{gazeData.left.forward.x},{gazeData.left.forward.y},{gazeData.left.forward.z}," +
                $"{gazeData.left.origin.x},{gazeData.left.origin.y},{gazeData.left.origin.z},{gazeData.leftStatus}," +
                $"{eyeMeasurements.leftPupilDiameterInMM},{eyeMeasurements.leftIrisDiameterInMM},{eyeMeasurements.leftPupilIrisDiameterRatio},{eyeMeasurements.leftEyeOpenness}," +
                $"{gazeData.right.forward.x},{gazeData.right.forward.y},{gazeData.right.forward.z}," +
                $"{gazeData.right.origin.x},{gazeData.right.origin.y},{gazeData.right.origin.z},{gazeData.rightStatus}," +
                $"{eyeMeasurements.rightPupilDiameterInMM},{eyeMeasurements.rightIrisDiameterInMM},{eyeMeasurements.rightPupilIrisDiameterRatio},{eyeMeasurements.rightEyeOpenness}," +
                $"{eyeMeasurements.interPupillaryDistanceInMM}," +
                $"{hmdPosition.x},{hmdPosition.y},{hmdPosition.z}," +
                $"{hmdRotation.x},{hmdRotation.y},{hmdRotation.z},{hmdRotation.w}," +
                $"{currentModelName},{isBuildingModel},{hitObjectName}," +
                 $"{calibrationState},{calibrationAttempts},{leftEyeCalibrationQuality},{rightEyeCalibrationQuality}," +
                 $"{conditionNumber},{trialNumber},{modelVisibilityState}," +
                 $"{objPos.x},{objPos.y},{objPos.z}," +
                 $"{objRot.x},{objRot.y},{objRot.z},{objRot.w}";

            writer.WriteLine(csvEntry);
        }
        writer.Flush();
    }
    
    /// <summary>
    /// Logs all available gaze data points to the CSV file.
    /// </summary>
    private void DebugGazeDataStructure()
    {
        Debug.Log("=== GAZE DATA STRUCTURE DEBUG ===");
        
        // Get a sample of gaze data to analyze structure
        List<VarjoEyeTracking.GazeData> gazeDataList = new List<VarjoEyeTracking.GazeData>();
        List<VarjoEyeTracking.EyeMeasurements> eyeMeasurementsList = new List<VarjoEyeTracking.EyeMeasurements>();
        
        // Retrieve current gaze data from Varjo system
        int dataCount = VarjoEyeTracking.GetGazeList(out gazeDataList, out eyeMeasurementsList);
        
        // Log the count of available data objects
        Debug.Log($"Number of objects in gazeDataList: {gazeDataList.Count}");
        Debug.Log($"Number of objects in eyeMeasurementsList: {eyeMeasurementsList.Count}");
        
        // Analyze GazeData structure if data is available
        if (gazeDataList.Count > 0)
        {
            var sampleGazeData = gazeDataList[0];
            var type = sampleGazeData.GetType();
            
            Debug.Log($"GazeData object type: {type.Name}");
            Debug.Log("Available fields and properties in GazeData:");
            
            // Get all public fields and their types
            var fields = type.GetFields();
            foreach (var field in fields)
            {
                Debug.Log($"  Field: {field.Name} (Type: {field.FieldType.Name})");
            }
            
            // Get all public properties and their types
            var properties = type.GetProperties();
            foreach (var prop in properties)
            {
                if (prop.CanRead)
                {
                    Debug.Log($"  Property: {prop.Name} (Type: {prop.PropertyType.Name})");
                }
            }
        }
        
        // Analyze EyeMeasurements structure if data is available
        if (eyeMeasurementsList.Count > 0)
        {
            var sampleEyeMeasurements = eyeMeasurementsList[0];
            var type = sampleEyeMeasurements.GetType();
            
            Debug.Log($"EyeMeasurements object type: {type.Name}");
            Debug.Log("Available fields and properties in EyeMeasurements:");
            
            // Get all public fields and their types
            var fields = type.GetFields();
            foreach (var field in fields)
            {
                Debug.Log($"  Field: {field.Name} (Type: {field.FieldType.Name})");
            }
            
            // Get all public properties and their types
            var properties = type.GetProperties();
            foreach (var prop in properties)
            {
                if (prop.CanRead)
                {
                    Debug.Log($"  Property: {prop.Name} (Type: {prop.PropertyType.Name})");
                }
            }
        }
        
        Debug.Log("=== END GAZE DATA STRUCTURE DEBUG ===");
    }

    // 29.07.2025 start
    /// <summary>
    /// Initializes CSV logging system and creates the output file.
    /// Sets up file path, headers, and enables logging mode.
    /// </summary>
    void StartLogging()
    {
        // Get the participant code from the TutorialGameManager or GameManager
        var tutorialGM = GameObject.Find("TutorialGameManager")?.GetComponent<TutorialGameManager>();
        var gameGM = GameObject.Find("GameManager")?.GetComponent<GameManager>();
        
        if (tutorialGM != null)
        {
            participantCode = tutorialGM.participantCode;
            Debug.Log($"EyeTrackingManager: Retrieved participant code from TutorialGameManager: '{participantCode}'");
        }
        else if (gameGM != null)
        {
            participantCode = gameGM.participantCode;
            Debug.Log($"EyeTrackingManager: Retrieved participant code from GameManager: '{participantCode}'");
        }
        else
        {
            participantCode = "Unknown";
            Debug.Log($"EyeTrackingManager: No GameManager found, using default: '{participantCode}'");
        }
        
        Debug.Log($"EyeTrackingManager: Final participant code: '{participantCode}'");
        
        // Define log directory path
        string logPath = @"D:\LegoVR\unity-lego-vr\Other_than_in_project_files\ET_Data";
        Directory.CreateDirectory(logPath);

        DateTime now = DateTime.Now;

        // Option 1: Same file per day (append mode)

        // Construct the filename using the participant code
        // 30.07.2025 begin add participant code to filename
        string fileName = $"{participantCode}_ET_Data_{now:yyyy-MM-dd}.csv";
        // 30.07.2025 end
        filePath = Path.Combine(logPath, fileName);
        bool fileExists = File.Exists(filePath);

        Debug.Log($"EyeTrackingManager: Creating file: {filePath}");

        // 30.07.2025 begin
        // Open file in append mode to allow multiple sessions per day
        FileStream stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        writer = new StreamWriter(stream);
        // 30.07.2025 end

        // Write CSV headers only if file is new
        if (!fileExists)
        {
            writer.WriteLine("gaze_capture_time,raw_timestamp,relative_to_unix_epoch_timestamp,focus_distance,frame_number,stability,status," +
                            "gaze_forward_x,gaze_forward_y,gaze_forward_z," +
                            "gaze_origin_x,gaze_origin_y,gaze_origin_z," +
                            "left_forward_x,left_forward_y,left_forward_z," +
                            "left_origin_x,left_origin_y,left_origin_z,left_status," +
                            "left_pupil_diameter,left_iris_diameter,left_pupil_iris_ratio,left_eye_openness," +
                            "right_forward_x,right_forward_y,right_forward_z," +
                            "right_origin_x,right_origin_y,right_origin_z,right_status," +
                            "right_pupil_diameter,right_iris_diameter,right_pupil_iris_ratio,right_eye_openness," +
                            "inter_pupillary_distance," +
                            "hmd_position_x,hmd_position_y,hmd_position_z," +
                            "hmd_rotation_x,hmd_rotation_y,hmd_rotation_z,hmd_rotation_w," +
                            "model_name,is_building_model,hit_obj_name," +
                             "calibration_state,calibration_attempts,left_eye_calibration_quality,right_eye_calibration_quality," +
                             "condition_number,trial_number,model_visibility_state," +
                             "object_position_x,object_position_y,object_position_z," +
                             "object_rotation_x,object_rotation_y,object_rotation_z,object_rotation_w");
        }

        logging = true;
        Debug.Log($"Logging started: {filePath}");
    }
    // 29.07.2025 end

    /// <summary>
    /// Stops the logging process and closes the CSV file.
    /// Ensures all data is flushed to disk before closing.
    /// </summary>
    void StopLogging()
    {
        if (!logging) return;

        if (writer != null)
        {
            writer.Flush();
            writer.Close();
            writer = null;
        }

        logging = false;
        Debug.Log($"Logging ended. Data saved at {filePath}");
    }

    /// <summary>
    /// Cleanup method called when application quits.
    /// Ensures logging is properly stopped and files are closed.
    /// </summary>
    void OnApplicationQuit()
    {
        StopLogging();
    }

    /// <summary>
    /// Public method to manually start logging. Can be called from other scripts.
    /// </summary>
    public void StartLoggingManually()
    {
        if (!logging)
        {
            StartLogging();
        }
    }

    // 08.08.2025 begin
    // Expose a public method to stop logging on demand during finalization.
    public void StopLoggingManually()
    {
        // Step: Route to internal StopLogging to flush and close the file synchronously.
        StopLogging();
    }
    // 08.08.2025 end

    // 30.07.2025 begin
    public void RecordModelBuildStart()
    {
        isBuildingModel = true;
        // Ensure model number is not -1 when building starts
        // if (modelNumber == -1) // This line is removed
        // {
        //     modelNumber = 1; // Start with model 1 if it was -1
        // }
        Debug.Log($"Model '{modelName}' build started at {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
    }

    public void RecordModelBuildEnd()
    {
        isBuildingModel = false;
        Debug.Log($"Model '{modelName}' build ended at {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
        // modelNumber++; // This line is removed
    }
    
    /// <summary>
    /// Detects what object the gaze is hitting using raycasting from the gaze origin.
    /// Returns the name of the hit object, or "None" if no object is hit.
    /// </summary>
    /// <param name="gazeData">The current gaze data containing origin and direction</param>
    /// <returns>The name of the object being gazed at, or "None" if no hit</returns>
    private string DetectGazeHitObject(VarjoEyeTracking.GazeData gazeData)
    {
        // Transform gaze origin from headset-local coordinates to world coordinates
        // The gaze origin is relative to the HMD, so we need to transform it using the HMD's position and rotation
        Vector3 rayOrigin = xrCamera.transform.position + xrCamera.transform.rotation * gazeData.gaze.origin;
        Vector3 rayDirection = xrCamera.transform.rotation * gazeData.gaze.forward;
        
        // Maximum raycast distance (adjust as needed for your scene)
        // Reduced from 100f to 10f for better precision with small LEGO bricks
        float maxDistance = 10f;
        
        // Debug: Print raycast parameters (only if debug is enabled)
        if (enableGazeHitDebug)
        {
            Debug.Log($"Gaze Raycast - Origin: {rayOrigin}, Direction: {rayDirection}, MaxDistance: {maxDistance}");
            Debug.Log($"HMD Position: {xrCamera.transform.position}, HMD Rotation: {xrCamera.transform.rotation.eulerAngles}");
            Debug.Log($"Raw Gaze Origin: {gazeData.gaze.origin}, Raw Gaze Direction: {gazeData.gaze.forward}");
        }
        
        // Perform the raycast
        RaycastHit hit;
        if (Physics.Raycast(rayOrigin, rayDirection, out hit, maxDistance))
        {
            // Debug: Print hit information (only if debug is enabled)
            if (enableGazeHitDebug)
            {
                Debug.Log($"Gaze Hit Object: '{hit.collider.gameObject.name}' at distance {hit.distance}");
            }
            
            // Return the name of the hit object
            return hit.collider.gameObject.name;
        }
        else
        {
            // Debug: Print when no hit occurs (only if debug is enabled)
            if (enableGazeHitDebug)
            {
                Debug.Log("Gaze Raycast: No object hit");
            }
        }
        
        // If no object is hit, return "None"
        return "None";
    }
    // 30.07.2025 end

     /// <summary>
     /// Raycasts from the gaze to get the hit Transform and associated GazeActivatable, if any.
     /// </summary>
     private bool TryGetGazeHit(VarjoEyeTracking.GazeData gazeData, out Transform hitTransform, out GazeActivatable activatable)
     {
         hitTransform = null;
         activatable = null;
 
         Vector3 rayOrigin = xrCamera.transform.position + xrCamera.transform.rotation * gazeData.gaze.origin;
         Vector3 rayDirection = xrCamera.transform.rotation * gazeData.gaze.forward;
         float maxDistance = 10f;
 
         if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, maxDistance))
         {
             hitTransform = hit.collider.transform;
             activatable = hitTransform.GetComponentInParent<GazeActivatable>();
             return true;
         }
         return false;
     }
     // 30.07.2025 add hit info method end

     // === Hit object name normalization helpers ===
     private static bool NameContains(string value, string term)
     {
         return !string.IsNullOrEmpty(value) &&
                value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
     }
 
     private static Transform FindAncestorWithNameContaining(Transform start, string term, int maxDepth = 10)
     {
         int depth = 0;
         Transform current = start;
         while (current != null && depth++ < maxDepth)
         {
             if (NameContains(current.name, term)) return current;
             current = current.parent;
         }
         return null;
     }
 
     private static Transform FindFirstNonStudAncestor(Transform start, int maxDepth = 10)
     {
         int depth = 0;
         Transform current = start;
         while (current != null && depth++ < maxDepth)
         {
             if (!NameContains(current.name, "stud")) return current;
             current = current.parent;
         }
         return null;
     }
 
     private string GetLoggedObjectName(Transform hitTransform, GazeActivatable activatable)
     {
         if (hitTransform == null) return "None";
 
         string rawName = hitTransform.gameObject.name;
         if (NameContains(rawName, "stud"))
         {
             // Prefer a parent with 'brick' in the name
             Transform brickAncestor = FindAncestorWithNameContaining(hitTransform, "brick");
             if (brickAncestor != null) return brickAncestor.name;
 
             // Otherwise, fallback to first non-stud ancestor
             Transform nonStud = FindFirstNonStudAncestor(hitTransform);
             if (nonStud != null) return nonStud.name;
 
             // Last resort: use the model root (GazeActivatable) name if available
             if (activatable != null) return activatable.gameObject.name;
         }
 
         // Not a stud: keep original
         return rawName;
     }
     // === End helpers ===
}