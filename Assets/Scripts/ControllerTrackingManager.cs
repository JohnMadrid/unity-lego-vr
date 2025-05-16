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

    void StartLogging()
    {
        string logPath = @"D:\LegoVR\unity-lego-vr\Other_than_in_project_files\CT_Data";
        Directory.CreateDirectory(logPath);

        DateTime now = DateTime.Now;
        string fileName = $"controller_log_{now:yyyy-MM-dd-HH-mm}.csv";
        filePath = Path.Combine(logPath, fileName);

        writer = new StreamWriter(filePath);
        writer.WriteLine("time,hand,pos_x,pos_y,pos_z,rot_x,rot_y,rot_z,rot_w," +
                         "vel_x,vel_y,vel_z,ang_vel_x,ang_vel_y,ang_vel_z," +
                         "trigger_pressed,grip_pressed,primary_button_pressed," +
                         "joystick_x,joystick_y," +
                         "thumb,index,middle,ring,pinky");

        logging = true;
        Debug.Log($"Controller logging started: {filePath}");
    }

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

        float thumb = GetFeature(device, "ThumbCapacitiveTouch", CommonUsages.thumbTouch);
        float index = GetFeature(device, "IndexFinger", new InputFeatureUsage<float>("indexFinger"));
        float middle = GetFeature(device, "MiddleFinger", new InputFeatureUsage<float>("middleFinger"));
        float ring = GetFeature(device, "RingFinger", new InputFeatureUsage<float>("ringFinger"));
        float pinky = GetFeature(device, "PinkyFinger", new InputFeatureUsage<float>("pinkyFinger"));

        string line = string.Format("{0:F4},{1},{2:F4},{3:F4},{4:F4},{5:F4},{6:F4},{7:F4},{8:F4}," +
                                    "{9:F4},{10:F4},{11:F4},{12:F4},{13:F4},{14:F4}," +
                                    "{15},{16},{17},{18:F4},{19:F4}," +
                                    "{20:F4},{21:F4},{22:F4},{23:F4},{24:F4}",
            Time.time, hand,
            position.x, position.y, position.z,
            rotation.x, rotation.y, rotation.z, rotation.w,
            velocity.x, velocity.y, velocity.z,
            angularVelocity.x, angularVelocity.y, angularVelocity.z,
            triggerPressed, gripPressed, primaryPressed,
            joystick.x, joystick.y,
            thumb, index, middle, ring, pinky
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
