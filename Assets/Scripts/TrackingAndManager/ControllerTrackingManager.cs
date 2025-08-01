using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class IndexControllerLogger : MonoBehaviour
{
    private InputDevice leftController;
    private InputDevice rightController;

    private StreamWriter writer;
    private string filePath;
    private bool logging = false;
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
        TryInitializeControllers();

        // Don't start logging immediately - wait for participant code to be set
        // StartLogging() will be called manually when participant code is ready
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

    void Update()
    {
        if (!leftController.isValid || !rightController.isValid)
            TryInitializeControllers();

        if (logging)
        {
            LogControllerData(leftController, XRNode.LeftHand, "Left");
            LogControllerData(rightController, XRNode.RightHand, "Right");
        }

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
    }

    void TryInitializeControllers()
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, devices);
        if (devices.Count > 0) leftController = devices[0];

        devices.Clear();
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, devices);
        if (devices.Count > 0) rightController = devices[0];
    }
    // 29.07.2025 start
    void StartLogging()
    {
        string logPath = @"D:\LegoVR\unity-lego-vr\Other_than_in_project_files\CT_Data";
        Directory.CreateDirectory(logPath);

        DateTime now = DateTime.Now;

        // Get the participant code from the TutorialGameManager or GameManager
        var tutorialGM = GameObject.Find("TutorialGameManager")?.GetComponent<TutorialGameManager>();
        var gameGM = GameObject.Find("GameManager")?.GetComponent<GameManager>();
        
        if (tutorialGM != null)
        {
            participantCode = tutorialGM.participantCode;
            Debug.Log($"ControllerTrackingManager: Retrieved participant code from TutorialGameManager: '{participantCode}'");
        }
        else if (gameGM != null)
        {
            participantCode = gameGM.participantCode;
            Debug.Log($"ControllerTrackingManager: Retrieved participant code from GameManager: '{participantCode}'");
        }
        else
        {
            participantCode = "Unknown";
            Debug.Log($"ControllerTrackingManager: No GameManager found, using default: '{participantCode}'");
        }
        
        Debug.Log($"ControllerTrackingManager: Final participant code: '{participantCode}'");

        // Construct the filename using the participant code
        // 30.07.2025 begin add participant code to filename
        string fileName = $"{participantCode}_CT_Data_{now:yyyy-MM-dd}.csv";
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
            writer.WriteLine("raw_timestamp,relative_to_unix_epoch_timestamp,hand," +
                            "pos_x,pos_y,pos_z,rot_x,rot_y,rot_z,rot_w," +
                            "vel_x,vel_y,vel_z,ang_vel_x,ang_vel_y,ang_vel_z," +
                            "trigger_pressed,grip_pressed,primary_button_pressed," +
                            "joystick_x,joystick_y," +
                            // 30.07.2025 begin
                            "model_name,is_building_model,model_start_time,model_end_time");
            // 30.07.2025 end
        }

        logging = true;
        Debug.Log($"Controller logging started: {filePath}");
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
        Debug.Log($"Controller logging ended. File saved at: {filePath}");
    }

    void LogControllerData(InputDevice device, XRNode node, string hand)
    {
        if (!device.isValid || writer == null) return;

        Vector3 position = InputTracking.GetLocalPosition(node);
        Quaternion rotation = InputTracking.GetLocalRotation(node);

        device.TryGetFeatureValue(CommonUsages.deviceVelocity, out Vector3 velocity);
        device.TryGetFeatureValue(CommonUsages.deviceAngularVelocity, out Vector3 angularVelocity);

        device.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerPressed);
        device.TryGetFeatureValue(CommonUsages.gripButton, out bool gripPressed);
        device.TryGetFeatureValue(CommonUsages.primaryButton, out bool primaryPressed);
        device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 joystick);

        long rawTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        float relativeTimestamp = Time.time;


        string line = string.Format("{0},{1},{2},{3},{4:F4},{5:F4},{6:F4},{7:F4},{8:F4},{9:F4}," +
                                    "{10:F4},{11:F4},{12:F4},{13:F4},{14:F4},{15:F4}," +
                                    "{16},{17},{18},{19:F4},{20:F4}," +
                                    // 30.07.2025 begin
                                    "{21},{22},{23},{24}",
            // 30.07.2025 end
            rawTimestamp, relativeTimestamp,
            hand,
            position.x, position.y, position.z,
            rotation.x, rotation.y, rotation.z, rotation.w,
            velocity.x, velocity.y, velocity.z,
            angularVelocity.x, angularVelocity.y, angularVelocity.z,
            triggerPressed, gripPressed, primaryPressed,
            joystick.x, joystick.y,
            // 30.07.2025 begin
            modelName, isBuildingModel, modelStartTime, modelEndTime
        // 30.07.2025 end
        );

        writer.WriteLine(line);
        writer.Flush();
    }

    float GetFeature(InputDevice device, string label, InputFeatureUsage<float> usage)
    {
        return device.TryGetFeatureValue(usage, out float value) ? value : -1f;
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
