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


   void Start()
   {
       Debug.Log("Initializing Body Tracking...");


       // Check if tracking enabled (can be changed in inspector) and then start logging
       if (trackingEnabled)
       {
           StartLogging();
       }
   }


    void Update()
    {
        if (!logging) return;

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

        writer.WriteLine(row);
        writer.Flush();
    }


   void StartLogging()
   {
       string logPath = @"D:\LegoVR\unity-lego-vr\Other_than_in_project_files\BT_Data"; // Same path as EyeTrackingManager
       Directory.CreateDirectory(logPath);


       DateTime now = DateTime.Now;
       string fileName = $"BT_Data_{now:yyyy-MM-dd-HH-mm}.csv";
       filePath = Path.Combine(logPath, fileName);


       writer = new StreamWriter(filePath);
       writer.WriteLine("raw_timestamp,relative_to_unix_epoch_timestamp," +
           "RightFoot_pos_x,RightFoot_pos_y,RightFoot_pos_z,RightFoot_rot_x,RightFoot_rot_y,RightFoot_rot_z,RightFoot_rot_w," +
           "LeftFoot_pos_x,LeftFoot_pos_y,LeftFoot_pos_z,LeftFoot_rot_x,LeftFoot_rot_y,LeftFoot_rot_z,LeftFoot_rot_w," +
           "Waist_pos_x,Waist_pos_y,Waist_pos_z,Waist_rot_x,Waist_rot_y,Waist_rot_z,Waist_rot_w," +
           "LeftHand_pos_x,LeftHand_pos_y,LeftHand_pos_z,LeftHand_rot_x,LeftHand_rot_y,LeftHand_rot_z,LeftHand_rot_w," +
           "RightHand_pos_x,RightHand_pos_y,RightHand_pos_z,RightHand_rot_x,RightHand_rot_y,RightHand_rot_z,RightHand_rot_w");


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

