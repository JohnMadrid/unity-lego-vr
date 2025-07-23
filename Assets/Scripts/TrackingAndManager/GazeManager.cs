using UnityEngine;
using Varjo.XR;
using System;

public class GazeManager : MonoBehaviour
{
    public static GazeManager Instance;
    public Vector3 gazeDirection { get; private set; }
    public Vector3 gazeOrigin { get; private set; }

    public Transform gazeVisualizer;
    public Camera xrCamera;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (!VarjoEyeTracking.IsGazeAllowed() || !VarjoEyeTracking.IsGazeCalibrated()) return;

        var gaze = VarjoEyeTracking.GetGaze();
        if (gaze.status != VarjoEyeTracking.GazeStatus.Invalid)
        {
            gazeOrigin = xrCamera.transform.TransformPoint(gaze.gaze.origin);
            gazeDirection = xrCamera.transform.TransformDirection(gaze.gaze.forward);

            if (gazeVisualizer)
            {
                gazeVisualizer.position = gazeOrigin + gazeDirection * 1f;
            }
            Debug.DrawRay(gazeOrigin, gazeDirection * 5, Color.red);

        }
    }
}
