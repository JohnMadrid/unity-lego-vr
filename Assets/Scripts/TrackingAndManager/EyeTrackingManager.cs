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

    [SerializeField]
    private Camera xrCamera; // Assign XR headset camera in inspector
    public bool trackingEnabled; // Can be toggled in inspector; default = false

    void Start()
    {
        Debug.Log("Initializing Eye Tracking...");

        if (VarjoEyeTracking.RequestGazeCalibration())
        {
            Debug.Log($"Eye tracking calibrated.");
        }
        else
        {
            Debug.LogError("Calibration failed.");
        }

        if (trackingEnabled)
        {
            StartLogging();
        }
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
                // bool printedGazeFields = false;
                foreach (var gazeData in gazeDataList)
                {
                    /*
                    if (!printedGazeFields)
                    {
                        var type = gazeData.GetType();
                        var fieldNames = new List<string>();
                        foreach (var field in type.GetFields())
                        {
                            fieldNames.Add(field.Name);
                        }
                        foreach (var prop in type.GetProperties())
                        {
                            if (prop.CanRead)
                                fieldNames.Add(prop.Name);
                        }
                        Debug.Log(string.Join(",", fieldNames));
                        printedGazeFields = true;
                    }*/
                    var eyeMeasurements = eyeMeasurementsList.Find(m => m.frameNumber == gazeData.frameNumber);

                    if (gazeData.status != VarjoEyeTracking.GazeStatus.Invalid)
                    {
                        Vector3 hmdPosition = xrCamera.transform.position;
                        Quaternion hmdRotation = xrCamera.transform.rotation;

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
                            $"{hmdRotation.x},{hmdRotation.y},{hmdRotation.z},{hmdRotation.w}";

                        writer.WriteLine(gazeEntry);
                        writer.Flush();

                    }
                }
            }
        }
    }

    void StartLogging()
    {
        string logPath = @"D:\LegoVR\unity-lego-vr\Other_than_in_project_files\ET_Data";
        Directory.CreateDirectory(logPath);

        DateTime now = DateTime.Now;
        string fileName = $"ET_Data_{now:yyyy-MM-dd}.csv";
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
                        "inter_pupillary_distance," +
                        "hmd_position_x,hmd_position_y,hmd_position_z," +
                        "hmd_rotation_x,hmd_rotation_y,hmd_rotation_z,hmd_rotation_w");


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