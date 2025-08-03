using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;


public class ViveTrackerManager : MonoBehaviour
{
    private StreamWriter writer;
    private bool logging = false;
    private string filePath;
    [SerializeField] public bool trackingEnabled; //Variable with field in inspector to enable tracking; default = false


    // 30.07.2025 begin
    private long modelStartTime = -1;
    private long modelEndTime = -1;
    private bool isBuildingModel = false;
    private string modelName = "";
    // 30.07.2025 end

    
    // 30.07.2025 begin
    private string participantCode; // Default value, will be set in Start()
    // 30.07.2025 end 
    void Start()
    {
        Debug.Log("Initializing Body Tracking...");

        // Don't start logging immediately - wait for participant code to be set
        // StartLogging() will be called manually when participant code is ready
    }


    void Update()
    {
        if (!logging) return;

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
        // if (modelName == "None" && !isBuildingModel)
        // {
        //     modelNumber = -1;
        // }
        // Note: modelNumber is incremented in RecordModelBuildEnd() method
        // 30.07.2025 end

        long rawTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        float relativeTimestamp = Time.time;

        Dictionary<string, string> deviceData = new Dictionary<string, string>();

        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevices(devices);

        foreach (var device in devices)
        {
            if (!device.isValid) continue;

            Vector3 position;
            Quaternion rotation;

            device.TryGetFeatureValue(CommonUsages.devicePosition, out position);
            device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation);

            string key = device.name.Replace(",", "").Replace(" ", "_"); // Clean for CSV
            string data = $"{position.x},{position.y},{position.z},{rotation.x},{rotation.y},{rotation.z},{rotation.w}";
            deviceData[key] = data;
        }

        // Build row
        string row = $"{rawTimestamp},{relativeTimestamp}";

        // Define expected devices for consistent column order
        string[] expectedDevices = {
            "SteamVR_Tracker_(Right_Foot)",
            "SteamVR_Tracker_(Left_Foot)",
            "SteamVR_Tracker_(Waist)",
            "SteamVR_Controller_(Index_Controller)_(Left_Hand)",
            "SteamVR_Controller_(Index_Controller)_(Right_Hand)"
        };

        foreach (string deviceKey in expectedDevices)
        {
            if (deviceData.ContainsKey(deviceKey))
                row += $",{deviceData[deviceKey]}";
            else
                row += ",,,,,,,";
        }
        
        // Add model_name column at the end
        row += $",{modelName}";

        writer.WriteLine(row);
        writer.Flush();
    }

    /// <summary>
    /// Public method to manually start logging. Can be called from other scripts.
    /// </summary>
    public void StartLoggingManually()
    {
        if (!logging && trackingEnabled)
        {
            StartLogging();
        }
    }

    // 29.07.2025 start
    void StartLogging()
    {
        string logPath = @"D:\LegoVR\unity-lego-vr\Other_than_in_project_files\BT_Data";
        Directory.CreateDirectory(logPath);

        DateTime now = DateTime.Now;

        // Get the participant code from the TutorialGameManager or GameManager
        var tutorialGM = GameObject.Find("TutorialGameManager")?.GetComponent<TutorialGameManager>();
        var gameGM = GameObject.Find("GameManager")?.GetComponent<GameManager>();
        
        if (tutorialGM != null)
        {
            participantCode = tutorialGM.participantCode;
            Debug.Log($"ViveTrackerManager: Retrieved participant code from TutorialGameManager: '{participantCode}'");
        }
        else if (gameGM != null)
        {
            participantCode = gameGM.participantCode;
            Debug.Log($"ViveTrackerManager: Retrieved participant code from GameManager: '{participantCode}'");
        }
        else
        {
            participantCode = "Unknown";
            Debug.Log($"ViveTrackerManager: No GameManager found, using default: '{participantCode}'");
        }
        
        Debug.Log($"ViveTrackerManager: Final participant code: '{participantCode}'");

        // Construct the filename using the participant code
        // 30.07.2025 begin add participant code to filename
        string fileName = $"{participantCode}_BT_Data_{now:yyyy-MM-dd}.csv";
        // 30.07.2025 end

        filePath = Path.Combine(logPath, fileName);

        bool fileExists = File.Exists(filePath);
        // 30.07.2025 begin
        // Open file in append mode
        FileStream stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        writer = new StreamWriter(stream);
        // 30.07.2025 end
        if (!fileExists)
        {
            // Write header only if file is new
            writer.WriteLine("raw_timestamp,relative_to_unix_epoch_timestamp," +
                "RightFoot_pos_x,RightFoot_pos_y,RightFoot_pos_z,RightFoot_rot_x,RightFoot_rot_y,RightFoot_rot_z,RightFoot_rot_w," +
                "LeftFoot_pos_x,LeftFoot_pos_y,LeftFoot_pos_z,LeftFoot_rot_x,LeftFoot_rot_y,LeftFoot_rot_z,LeftFoot_rot_w," +
                "Waist_pos_x,Waist_pos_y,Waist_pos_z,Waist_rot_x,Waist_rot_y,Waist_rot_z,Waist_rot_w," +
                "LeftHand_pos_x,LeftHand_pos_y,LeftHand_pos_z,LeftHand_rot_x,LeftHand_rot_y,LeftHand_rot_z,LeftHand_rot_w," +
                "RightHand_pos_x,RightHand_pos_y,RightHand_pos_z,RightHand_rot_x,RightHand_rot_y,RightHand_rot_z,RightHand_rot_w," +
                // 30.07.2025 begin
                "model_name");
                // 30.07.2025 end
        }

        logging = true;
        Debug.Log($"Body logging started: {filePath}");
    }
    // 29.07.2025 end

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
        Debug.Log($"Body logging ended. Data saved at {filePath}");
    }


    void OnApplicationQuit()
    {
        StopLogging();
    }
   
    // 30.07.2025 begin
    public void RecordModelBuildStart()
    {
        isBuildingModel = true;
        // Ensure model number is not -1 when building starts
        // if (modelNumber == -1)
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
    // 30.07.2025 end
}

