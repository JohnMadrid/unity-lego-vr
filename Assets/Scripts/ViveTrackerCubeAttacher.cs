using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class ViveTrackerCubeAttacher : MonoBehaviour
{
    [Header("Assign the tracker cubes here")]
    public GameObject cubeWaist;
    public GameObject cubeRightFoot;
    public GameObject cubeLeftFoot;

    // The display names you see in InputDevice.name
    private const string WaistTrackerName = "SteamVR Tracker (Waist)";
    private const string RightFootTrackerName = "SteamVR Tracker (Right Foot)";
    private const string LeftFootTrackerName = "SteamVR Tracker (Left Foot)";

    private Dictionary<string, GameObject> trackerGos;

    // To avoid allocation every frame
    private List<InputDevice> inputDevices = new List<InputDevice>();

    void Awake()
    {
        trackerGos = new Dictionary<string, GameObject>
        {
            { WaistTrackerName, cubeWaist },
            { RightFootTrackerName, cubeRightFoot },
            { LeftFootTrackerName, cubeLeftFoot }
        };
    }

    void Update()
    {
        InputDevices.GetDevices(inputDevices);
        foreach (var device in inputDevices)
        {
            // Only look for trackers and not controllers by name, or expand characteristic checks if needed
            if (trackerGos.ContainsKey(device.name))
            {
                Vector3 position;
                Quaternion rotation;
                // These might fail if something disconnects; default to zero/identity
                device.TryGetFeatureValue(CommonUsages.devicePosition, out position);
                device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation);

                // Assign to the correct cube
                var go = trackerGos[device.name];
                if (go != null)
                {
                    go.transform.position = position;
                    go.transform.rotation = rotation;
                }
            }
        }
    }
}