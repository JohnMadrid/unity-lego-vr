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

    void Start()
    {
        Debug.Log("Initializing Body Tracking...");

        // Start logging body tracking data
        StartLogging();
    }

    void Update()
    {
        if (logging)
        {
            List<InputDevice> devices = new List<InputDevice>();
            InputDevices.GetDevices(devices);

            foreach (var device in devices)
            {
                if (device.characteristics.HasFlag(InputDeviceCharacteristics.Controller))
                {
                    Vector3 position;
                    Quaternion rotation;
                    
                    device.TryGetFeatureValue(CommonUsages.devicePosition, out position);
                    device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation);

                    string logEntry = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()},{Time.time},{device.name}," +
                                      $"{position.x},{position.y},{position.z}," +
                                      $"{rotation.x},{rotation.y},{rotation.z},{rotation.w}";

                    writer.WriteLine(logEntry);
                    writer.Flush(); // Ensure data is written immediately
                }
            }
        }
    }

    void StartLogging()
    {
        string logPath = @"D:\LegoVR\unity-lego-vr\Other_than_in_project_files\BT_Data"; // Same path as EyeTrackingManager
        Directory.CreateDirectory(logPath);

        DateTime now = DateTime.Now;
        string fileName = $"BT_Data_{now:yyyy-MM-dd-HH-mm}.csv";
        filePath = Path.Combine(logPath, fileName);

        writer = new StreamWriter(filePath);
        writer.WriteLine("raw_timestamp,relative_to_unix_epoch_timestamp,device_name," +
                         "position_x,position_y,position_z,rotation_x,rotation_y,rotation_z,rotation_w");

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
        Debug.Log($"Body logging ended. Data saved at {filePath}");
    }

    void OnApplicationQuit()
    {
        StopLogging();
    }
}
