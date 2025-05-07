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

    void Start()
    {
        Debug.Log("Initializing Eye Tracking...");

        // Request gaze calibration at startup
        if (VarjoEyeTracking.RequestGazeCalibration()) // Using Varjo Plugin’s EyeTrackingExample
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
            List<VarjoEyeTracking.GazeData> gazeDataList = new List<VarjoEyeTracking.GazeData>();
            List<VarjoEyeTracking.EyeMeasurements> eyeMeasurementsList = new List<VarjoEyeTracking.EyeMeasurements>();
            int dataCount = VarjoEyeTracking.GetGazeList(out gazeDataList, out eyeMeasurementsList);

            if (dataCount > 0)
            {
                foreach (var gazeData in gazeDataList)
                {
                    var eyeMeasurements = eyeMeasurementsList.Find(m => m.frameNumber == gazeData.frameNumber);
                    if (gazeData.status != VarjoEyeTracking.GazeStatus.Invalid)
                    {
                        string gazeEntry = $"{gazeData.captureTime},{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()},{Time.time},{gazeData.focusDistance},{gazeData.frameNumber},{gazeData.focusStability},{gazeData.status}," +
                                           $"{gazeData.gaze.forward.x},{gazeData.gaze.forward.y},{gazeData.gaze.forward.z}," +
                                           $"{gazeData.gaze.origin.x},{gazeData.gaze.origin.y},{gazeData.gaze.origin.z}," +
                                           $"{gazeData.left.forward.x},{gazeData.left.forward.y},{gazeData.left.forward.z}," +
                                           $"{gazeData.left.origin.x},{gazeData.left.origin.y},{gazeData.left.origin.z}," +
                                           $"{gazeData.leftStatus}," +
                                           $"{eyeMeasurements.leftPupilDiameterInMM},{eyeMeasurements.leftIrisDiameterInMM},{eyeMeasurements.leftPupilIrisDiameterRatio},{eyeMeasurements.leftEyeOpenness}," +
                                           $"{gazeData.right.forward.x},{gazeData.right.forward.y},{gazeData.right.forward.z}," +
                                           $"{gazeData.right.origin.x},{gazeData.right.origin.y},{gazeData.right.origin.z}," +
                                           $"{gazeData.rightStatus}," +
                                           $"{eyeMeasurements.rightPupilDiameterInMM},{eyeMeasurements.rightIrisDiameterInMM},{eyeMeasurements.rightPupilIrisDiameterRatio},{eyeMeasurements.rightEyeOpenness}," +
                                           $"{eyeMeasurements.interPupillaryDistanceInMM}";

                        writer.WriteLine(gazeEntry);
                        writer.Flush(); // Ensure data is written immediately
                    }
                }
            }
        }
    }

    void StartLogging()
    {
        string logPath = @"D:\LegoVR\unity-lego-vr\Other_than_in_project_files\ET_Data";  // <-- Updated path
        Directory.CreateDirectory(logPath);

        DateTime now = DateTime.Now;
        string fileName = $"{now:yyyy-MM-dd-HH-mm}.csv";
        filePath = Path.Combine(logPath, fileName);

        writer = new StreamWriter(filePath);
        writer.WriteLine("raw_timestamp,relative_to_unix_epoch_timestamp,relative_to_video_first_frame_timestamp,focus_distance,frame_number,stability,status," +
                         "gaze_forward_x,gaze_forward_y,gaze_forward_z," +
                         "gaze_origin_x,gaze_origin_y,gaze_origin_z," +
                         "left_forward_x,left_forward_y,left_forward_z," +
                         "left_origin_x,left_origin_y,left_origin_z,left_status," +
                         "left_pupil_diameter,left_iris_diameter,left_pupil_iris_ratio,left_eye_openness," +
                         "right_forward_x,right_forward_y,right_forward_z," +
                         "right_origin_x,right_origin_y,right_origin_z,right_status," +
                         "right_pupil_diameter,right_iris_diameter,right_pupil_iris_ratio,right_eye_openness," +
                         "inter_pupillary_distance");

        logging = true;
        Debug.Log($"Logging started: {filePath}");
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
        Debug.Log($"Logging ended. Data saved at {filePath}");
    }

    void OnApplicationQuit()
    {
        StopLogging();
    }
}

