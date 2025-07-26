using UnityEngine;
using Varjo.XR;

public class GazeManager : MonoBehaviour
{
    public static GazeManager Instance;

    [Header("References")]
    public Camera xrCamera;

    /* // Optional: Visualize gaze target in the world
    public GameObject gazeTarget;               // A small visible sphere
    public float gazeRadius = 0.01f;
    public float floatingGazeTargetDistance = 5f;
    public float targetOffset = 0.2f;
    public bool scaleWithDistance = true;

    [Header("Debug")]
    public bool debugDrawRay = true; */

    public Vector3 gazeDirection { get; private set; }
    public Vector3 gazeOrigin { get; private set; }

    private RaycastHit hit;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (!VarjoEyeTracking.IsGazeAllowed() || !VarjoEyeTracking.IsGazeCalibrated()) return;

        var gaze = VarjoEyeTracking.GetGaze();
        if (gaze.status == VarjoEyeTracking.GazeStatus.Invalid) return;

        // Get gaze origin/direction from headset space → world space
        gazeOrigin = xrCamera.transform.TransformPoint(gaze.gaze.origin);
        gazeDirection = xrCamera.transform.TransformDirection(gaze.gaze.forward);

        /* 
        // Visualize with a raycast
        RaycastHit hit;
        if (Physics.SphereCast(gazeOrigin, gazeRadius, gazeDirection, out hit, 20f))
        {
            gazeTarget.transform.position = hit.point - gazeDirection * targetOffset;
            gazeTarget.transform.LookAt(gazeOrigin, Vector3.up);
            gazeTarget.transform.localScale = Vector3.one * hit.distance;

            if (!gazeTarget.activeSelf)
                gazeTarget.SetActive(true);
        }
        else
        {
            // Fallback
            gazeTarget.transform.position = gazeOrigin + gazeDirection * floatingGazeTargetDistance;
            gazeTarget.transform.LookAt(gazeOrigin, Vector3.up);
            gazeTarget.transform.localScale = Vector3.one * floatingGazeTargetDistance;

            if (!gazeTarget.activeSelf)
                gazeTarget.SetActive(true);
        } 

        Debug.DrawRay(gazeOrigin, gazeDirection * 5f, Color.green);*/
    }

}
