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
    
    void Start()
    {
        TryInitializeControllers();
        
        // Check if tracking enabled (can be changed in inspector) and then start logging
        if (trackingEnabled)
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

        // Option 1: Append to a single file per day
        string fileName = $"CT_Data_{now:yyyy-MM-dd}.csv";
        
        // Option 2: Uncomment this for a new file every session
        // string fileName = $"CT_Data_{now:yyyy-MM-dd_HH-mm-ss}.csv";

        filePath = Path.Combine(logPath, fileName);
        bool fileExists = File.Exists(filePath);

        writer = new StreamWriter(filePath, true); // true = append mode

        if (!fileExists)
        {
            writer.WriteLine("raw_timestamp,relative_to_unix_epoch_timestamp,hand," +
                            "pos_x,pos_y,pos_z,rot_x,rot_y,rot_z,rot_w," +
                            "vel_x,vel_y,vel_z,ang_vel_x,ang_vel_y,ang_vel_z," +
                            "trigger_pressed,grip_pressed,primary_button_pressed," +
                            "joystick_x,joystick_y");
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
                                    "{16},{17},{18},{19:F4},{20:F4}",
            rawTimestamp, relativeTimestamp,
            hand,
            position.x, position.y, position.z,
            rotation.x, rotation.y, rotation.z, rotation.w,
            velocity.x, velocity.y, velocity.z,
            angularVelocity.x, angularVelocity.y, angularVelocity.z,
            triggerPressed, gripPressed, primaryPressed,
            joystick.x, joystick.y
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
}
