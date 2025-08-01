using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Varjo.XR;

/// <summary>
/// EyeTrackingManager handles eye tracking data collection and logging for Varjo Aero headset.
/// This script manages gaze data, eye measurements, and CSV file logging for experimental data collection.
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
    private string hitObjName = ""; // Track the name of the object being gazed at
    // 30.07.2025 end

    // 30.07.2025 begin
    private string participantCode; // Default value, will be set in Start()
    // 30.07.2025 end

    /// <summary>
    /// Initializes eye tracking system, calibrates the headset, and starts logging if enabled.
    /// </summary>
    void Start()
    {
        Debug.Log("Initializing Eye Tracking...");

        // Request eye tracking calibration from Varjo system
        if (VarjoEyeTracking.RequestGazeCalibration())
        {
            Debug.Log($"Eye tracking calibrated.");
        }
        else
        {
            Debug.LogError("Calibration failed.");
        }

        // Debug gaze data structure only if debug mode is enabled
        if (enableDebugMode)
        {
            DebugGazeDataStructure();
        }

        // Don't start logging immediately - wait for participant code to be set
        // StartLogging() will be called manually when participant code is ready
    }

    /// <summary>
    /// Main update loop that collects and logs eye tracking data every frame.
    /// Processes gaze data and eye measurements from Varjo headset.
    /// </summary>
    void Update()
    {
        if (logging)
        {
            // Initialize lists to store gaze data and eye measurements
            List<VarjoEyeTracking.GazeData> gazeDataList = new List<VarjoEyeTracking.GazeData>();
            List<VarjoEyeTracking.EyeMeasurements> eyeMeasurementsList = new List<VarjoEyeTracking.EyeMeasurements>();

            // Get current gaze data from Varjo system
            int dataCount = VarjoEyeTracking.GetGazeList(out gazeDataList, out eyeMeasurementsList);

            // 30.07.2025 begin
            // Update modelName every frame based on current item at modelSpawnPoint
            Transform modelSpawnPoint = null;
            
            // Try to get modelSpawnPoint from TutorialGameManager first (for tutorial phase)
            var tutorialGM = GameObject.Find("TutorialGameManager")?.GetComponent<TutorialGameManager>();
            if (tutorialGM != null && tutorialGM.modelSpawnPoint != null)
            {
                modelSpawnPoint = tutorialGM.modelSpawnPoint;
            }
            // If not found in TutorialGameManager, try GameManager (for main experiment phase)
            else
            {
                var gameGM = GameObject.Find("GameManager")?.GetComponent<GameManager>();
                if (gameGM != null)
                {
                    modelSpawnPoint = gameGM.modelSpawnPoint;
                }
            }
            
            if (modelSpawnPoint != null && modelSpawnPoint.childCount > 0)
            {
                modelName = modelSpawnPoint.GetChild(0).gameObject.name.Replace("(Clone)", "").Trim();
            }
            else
            {
                modelName = "None";
            }
            
            // Update modelNumber: set to -1 only when no model is available AND not building
            // Otherwise, keep the current model number (which gets incremented in RecordModelBuildEnd)
            if (modelName == "None" && !isBuildingModel)
            {
                // modelNumber = -1; // This line is removed
            }
            // Note: modelNumber is incremented in RecordModelBuildEnd() method
            // 30.07.2025 end

            // Process gaze data if available
            if (dataCount > 0)
            {
                // bool printedGazeFields = false;
                foreach (var gazeData in gazeDataList)
                {
                    // Find corresponding eye measurements for this frame
                    var eyeMeasurements = eyeMeasurementsList.Find(m => m.frameNumber == gazeData.frameNumber);

                    // Only log valid gaze data
                    if (gazeData.status != VarjoEyeTracking.GazeStatus.Invalid)
                    {
                        // Get current HMD position and rotation
                        Vector3 hmdPosition = xrCamera.transform.position;
                        Quaternion hmdRotation = xrCamera.transform.rotation;
                        
                        // Detect what object the gaze is hitting using raycasting
                        hitObjName = DetectGazeHitObject(gazeData);

                        // Construct comprehensive CSV entry with all gaze and eye measurement data
                        string gazeEntry =
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
                            // 30.07.2025 begin
                            $"{modelName},{isBuildingModel},{hitObjName}";
                            // 30.07.2025 end
                        
                        // Write to CSV file and flush to ensure data is saved
                        writer.WriteLine(gazeEntry);
                        writer.Flush();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Debug method to analyze and print gaze data structure information.
    /// Only runs when enableDebugMode is true in the inspector.
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
                            // 30.07.2025 begin
                            "model_name,is_building_model,hit_obj_name");
                            // 30.07.2025 end
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

    // 30.07.2025 begin
    public void RecordModelBuildStart()
    {
        isBuildingModel = true;
        // Ensure model number is not -1 when building starts
        // if (modelNumber == -1) // This line is removed
        // {
        //     modelNumber = 1; // Start with model 1 if it was -1
        // }
        Debug.Log($"Model '{modelName}'");
    }

    public void RecordModelBuildEnd()
    {
        isBuildingModel = false;
        Debug.Log($"Model '{modelName}'");
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
}