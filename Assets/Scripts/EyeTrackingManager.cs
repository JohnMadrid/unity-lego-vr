using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Varjo.XR; 

public class EyeTrackingManager : MonoBehaviour
{
    private StreamWriter writer;
    private bool logging = false;
    private string filePath;
    private List<string> gazeDataLog = new List<string>();

    void Start()
    {
        Debug.Log("Initializing Eye Tracking...");

        // Request gaze calibration at startup
        if (EyeTrackingExample.RequestGazeCalibration()) // EyeTracingExample is file in VarjoUnityXR Plugin for ET under Runtime > EyeTracking
        {
            Debug.Log("Eye tracking calibrated.");
        }
        else
        {
            Debug.LogError("Calibration failed.");
        }

        StartLogging();
    }

    void Update()
    {
        if (logging)
        {
            EyeTrackingExample.GazeData gazeData = EyeTrackingExample.GetGaze();
            if (gazeData.status != EyeTrackingExample.GazeStatus.Invalid)
            {
                string gazeEntry = $"{Time.time},{gazeData.gaze.origin},{gazeData.gaze.forward},{gazeData.focusDistance},{gazeData.focusStability}";
                gazeDataLog.Add(gazeEntry);
            }
        }
    }

    void StartLogging()
    {
        
        string logPath = @"D:\LegoVR\unity-lego-vr\ET_Data";  // where ET data is saved
        Directory.CreateDirectory(logPath);

        DateTime now = DateTime.Now;
        string fileName = $"{now:yyyy-MM-dd-HH-mm}.csv";
        filePath = Path.Combine(logPath, fileName);

        writer = new StreamWriter(filePath);
        writer.WriteLine("Time,GazeOrigin,GazeForward,FocusDistance,FocusStability");

        logging = true;
        Debug.Log($"Logging started: {filePath}");
    }

    void StopLogging()
    {
        if (!logging) return;

        if (writer != null)
        {
            foreach (var entry in gazeDataLog)
            {
                writer.WriteLine(entry);
            }

            writer.Flush();
            writer.Close();
            writer = null;
        }

        logging = false;
        Debug.Log($"Logging ended. Data saved at {filePath}");
    }

    void OnApplicationQuit()
    {
        StopLogging();
    }
}
