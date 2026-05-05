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

    [SerializeField] public bool trackingEnabled;

    [Header("LSL")]
    [SerializeField] private bool lslEnabled = true; // Toggle BT LSL streaming on/off

    private GameManager gameManager;

    // --- Foot area settings ---
    [Header("Foot Area Settings")]
    [SerializeField] private Transform areaColliderRoot; // Assign the AreaCollider GameObject here
    [SerializeField] private float areaCheckRadius = 0.05f;

    // --- Logged area names ---
    // Default starting area for both feet is Work
    private string leftFootArea = "Work";
    private string rightFootArea = "Work";

    // Name â†’ area mapping for AreaCollider children
    private Dictionary<string, string> areaByColliderName;

    // Cached model info
    private long modelStartTime = -1;
    private long modelEndTime = -1;
    private bool isBuildingModel = false;
    private string modelName = "";

    // Cumulative rotation (in degrees) for the currently active model,
    // mirrored from ModelRotation.CurrentRotationDegrees for logging.
    public float model_rot_deg { get; private set; } = 0f;

    private string participantCode;
    private int conditionNumberForFile = 0;

    void Start()
    {
        Debug.Log("Initializing Body Tracking...");
        gameManager = FindObjectOfType<GameManager>();

        // Initialize mapping from collider names under AreaCollider to logical area labels
        areaByColliderName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "DeathZoneR", "Middle" },
            { "DeathZoneM", "Middle" },
            { "DeathZoneW", "Middle" },
            { "WorkBack", "Work" },
            { "WorkLeft", "Work" },
            { "WorkRight", "Work" },
            { "ResourceBack", "Resource" },
            { "ResourceFront", "Resource" },
            { "ModelBack", "Model" },
            { "ModelFront", "Model" }
        };
    }

    void Update()
    {
        if (!logging) return;

        // Track if the active model changed this frame, so we can reset rotation tracking.
        string previousModelName = modelName;

        // --- Determine current model name ---
        Transform modelSpawnPoint = null;

        var tutorialGM = GameObject.Find("TutorialGameManager")?.GetComponent<TutorialGameManager>();
        if (tutorialGM != null && tutorialGM.modelSpawnPoint != null)
            modelSpawnPoint = tutorialGM.modelSpawnPoint;
        else
        {
            var gameGM = GameObject.Find("GameManager")?.GetComponent<GameManager>();
            if (gameGM != null)
                modelSpawnPoint = gameGM.modelSpawnPoint;
        }

        if (modelSpawnPoint != null && modelSpawnPoint.childCount > 0)
            modelName = modelSpawnPoint.GetChild(0).gameObject.name.Replace("(Clone)", "").Trim();
        else
            modelName = "None";

        // If a new model appeared (or the current one disappeared), reset rotation tracking.
        if (!string.Equals(modelName, previousModelName, StringComparison.Ordinal))
        {
            model_rot_deg = 0f;
            ModelRotation.ResetRotationTracking();
        }

        // Mirror the current rotation amount from ModelRotation for logging and inspection.
        model_rot_deg = ModelRotation.CurrentRotationDegrees;

        long rawTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        float relativeTimestamp = Time.time;

        int conditionNumber = (gameManager != null) ? gameManager.trialNumber : 0;
        int trialNumber = (gameManager != null) ? Mathf.Clamp(gameManager.GetCurrentItemIndex(), 0, 6) : 0;

        Dictionary<string, string> deviceData = new Dictionary<string, string>();

        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevices(devices);

        Vector3 leftFootPos = Vector3.zero;
        Vector3 rightFootPos = Vector3.zero;
        bool hasLeftFoot = false;
        bool hasRightFoot = false;

        foreach (var device in devices)
        {
            if (!device.isValid) continue;

            Vector3 position;
            Quaternion rotation;

            device.TryGetFeatureValue(CommonUsages.devicePosition, out position);
            device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation);

            string key = device.name.Replace(",", "").Replace(" ", "_");
            string data = $"{position.x},{position.y},{position.z},{rotation.x},{rotation.y},{rotation.z},{rotation.w}";
            deviceData[key] = data;

            // --- Detect foot tracker positions for area classification ---
            if (key.Contains("Right_Foot"))
            {
                rightFootPos = position;
                hasRightFoot = true;
            }

            if (key.Contains("Left_Foot"))
            {
                leftFootPos = position;
                hasLeftFoot = true;
            }
        }

        // --- Determine / update foot areas based on AreaCollider children ---
        if (hasLeftFoot)
            leftFootArea = UpdateFootArea(leftFootPos, leftFootArea);

        if (hasRightFoot)
            rightFootArea = UpdateFootArea(rightFootPos, rightFootArea);

        // --- Build CSV row ---
        string row = $"{rawTimestamp},{relativeTimestamp}";

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

        // --- Controller trigger/grip + grabbed object ---
        InputDevice leftController = default;
        InputDevice rightController = default;
        bool hasLeftController = false;
        bool hasRightController = false;

        {
            var nodeDevices = new List<InputDevice>();
            InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, nodeDevices);
            if (nodeDevices.Count > 0 && nodeDevices[0].isValid)
            {
                leftController = nodeDevices[0];
                hasLeftController = true;
            }
        }

        {
            var nodeDevices = new List<InputDevice>();
            InputDevices.GetDevicesAtXRNode(XRNode.RightHand, nodeDevices);
            if (nodeDevices.Count > 0 && nodeDevices[0].isValid)
            {
                rightController = nodeDevices[0];
                hasRightController = true;
            }
        }

        Vector3 lGrabPos = Vector3.zero;
        Quaternion lGrabRot = Quaternion.identity;
        Vector3 rGrabPos = Vector3.zero;
        Quaternion rGrabRot = Quaternion.identity;

        if (hasLeftController)
        {
            leftController.TryGetFeatureValue(CommonUsages.triggerButton, out bool lTrig);
            leftController.TryGetFeatureValue(CommonUsages.gripButton, out bool lGrip);

            string lGrabbed = "None";
            if (lGrip && leftController.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 lPos))
            {
                Transform lt = GetNearestColliderTransform(lPos, 0.075f);
                if (lt != null)
                {
                    lGrabbed = NormalizeGrabbedName(lt);
                    lGrabPos = lt.position;
                    lGrabRot = lt.rotation;
                }
            }

            row += $",{lTrig},{lGrip},{lGrabbed}";
        }
        else row += ",,,";

        if (hasRightController)
        {
            rightController.TryGetFeatureValue(CommonUsages.triggerButton, out bool rTrig);
            rightController.TryGetFeatureValue(CommonUsages.gripButton, out bool rGrip);

            string rGrabbed = "None";
            if (rGrip && rightController.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 rPos))
            {
                Transform rt = GetNearestColliderTransform(rPos, 0.075f);
                if (rt != null)
                {
                    rGrabbed = NormalizeGrabbedName(rt);
                    rGrabPos = rt.position;
                    rGrabRot = rt.rotation;
                }
            }

            row += $",{rTrig},{rGrip},{rGrabbed}";
        }
        else row += ",,,";

        // --- Append condition/trial, grabbed poses, model name, and NEW foot areas ---
        row +=
            $",{conditionNumber},{trialNumber}," +
            $"{lGrabPos.x},{lGrabPos.y},{lGrabPos.z}," +
            $"{lGrabRot.x},{lGrabRot.y},{lGrabRot.z},{lGrabRot.w}," +
            $"{rGrabPos.x},{rGrabPos.y},{rGrabPos.z}," +
            $"{rGrabRot.x},{rGrabRot.y},{rGrabRot.z},{rGrabRot.w}," +
            $"{modelName}," +
            $"{model_rot_deg}," +
            $"{leftFootArea},{rightFootArea}";

        writer.WriteLine(row);
        writer.Flush();

        // --- LSL numeric streaming (subset of body tracking data) ---
        if (lslEnabled && LslOutletManager.Instance != null)
        {
            float[] sample =
            {
                rawTimestamp,
                relativeTimestamp,

                leftFootPos.x, leftFootPos.y, leftFootPos.z,
                rightFootPos.x, rightFootPos.y, rightFootPos.z,

                conditionNumber,
                trialNumber,

                model_rot_deg,

                lGrabPos.x, lGrabPos.y, lGrabPos.z,
                lGrabRot.x, lGrabRot.y, lGrabRot.z, lGrabRot.w,

                rGrabPos.x, rGrabPos.y, rGrabPos.z,
                rGrabRot.x, rGrabRot.y, rGrabRot.z, rGrabRot.w
            };

            var lsl = LslOutletManager.Instance;
            // Use current Application.targetFrameRate as nominal BT sampling rate (e.g., 90 Hz).
            lsl.EnsureBtOutlet(sample.Length, Application.targetFrameRate);
            lsl.PushBtSample(sample);
        }
    }

    // --- Determine / update which area a foot is in based on AreaCollider children ---
    private string UpdateFootArea(Vector3 footPos, string currentArea)
    {
        string newArea = GetAreaFromColliders(footPos);
        if (!string.IsNullOrEmpty(newArea))
            return newArea;

        // No new area detected this frame â€“ preserve previous area (persistence requirement)
        return currentArea;
    }

    private string GetAreaFromColliders(Vector3 footPos)
    {
        if (areaColliderRoot == null || areaByColliderName == null || areaByColliderName.Count == 0)
            return null;

        // Guard against uninitialized or invalid positions
        if (float.IsNaN(footPos.x) || float.IsNaN(footPos.y) || float.IsNaN(footPos.z))
            return null;

        Collider[] hits = Physics.OverlapSphere(footPos, areaCheckRadius);
        if (hits == null || hits.Length == 0)
            return null;

        foreach (var hit in hits)
        {
            if (hit == null) continue;

            Transform t = hit.transform;
            if (!t.IsChildOf(areaColliderRoot)) continue; // Ignore anything not under AreaCollider

            string colliderName = t.gameObject.name;
            if (areaByColliderName.TryGetValue(colliderName, out string area))
                return area;
        }

        return null;
    }

    // --- Logging control ---
    public void StartLoggingManually(int conditionNumber)
    {
        this.conditionNumberForFile = conditionNumber;
        if (!logging && trackingEnabled)
            StartLogging();
    }

    public void StopLoggingManually()
    {
        StopLogging();
    }

    void StartLogging()
    {
        string logPath = DataPaths.BTData;

        DateTime now = DateTime.Now;

        var tutorialGM = GameObject.Find("TutorialGameManager")?.GetComponent<TutorialGameManager>();
        var gameGM = GameObject.Find("GameManager")?.GetComponent<GameManager>();

        if (tutorialGM != null)
            participantCode = tutorialGM.participantCode;
        else if (gameGM != null)
            participantCode = gameGM.participantCode;
        else
            participantCode = "Unknown";

        string fileName = $"{participantCode}_BT_Data_Condition{conditionNumberForFile}_{now:yyyy-MM-dd}.csv";
        filePath = Path.Combine(logPath, fileName);

        bool fileExists = File.Exists(filePath);

        FileStream stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        writer = new StreamWriter(stream);

        if (!fileExists)
        {
            writer.WriteLine(
                "raw_timestamp,relative_to_unix_epoch_timestamp," +
                "RightFoot_pos_x,RightFoot_pos_y,RightFoot_pos_z,RightFoot_rot_x,RightFoot_rot_y,RightFoot_rot_z,RightFoot_rot_w," +
                "LeftFoot_pos_x,LeftFoot_pos_y,LeftFoot_pos_z,LeftFoot_rot_x,LeftFoot_rot_y,LeftFoot_rot_z,LeftFoot_rot_w," +
                "Waist_pos_x,Waist_pos_y,Waist_pos_z,Waist_rot_x,Waist_rot_y,Waist_rot_z,Waist_rot_w," +
                "LeftHand_pos_x,LeftHand_pos_y,LeftHand_pos_z,LeftHand_rot_x,LeftHand_rot_y,LeftHand_rot_z,LeftHand_rot_w," +
                "RightHand_pos_x,RightHand_pos_y,RightHand_pos_z,RightHand_rot_x,RightHand_rot_y,RightHand_rot_z,RightHand_rot_w," +
                "LeftHand_trigger_pressed,LeftHand_grip_pressed,LeftHand_grabbed_name," +
                "RightHand_trigger_pressed,RightHand_grip_pressed,RightHand_grabbed_name," +
                "condition_number,trial_number," +
                "LeftGrab_obj_pos_x,LeftGrab_obj_pos_y,LeftGrab_obj_pos_z," +
                "LeftGrab_obj_rot_x,LeftGrab_obj_rot_y,LeftGrab_obj_rot_z,LeftGrab_obj_rot_w," +
                "RightGrab_obj_pos_x,RightGrab_obj_pos_y,RightGrab_obj_pos_z," +
                "RightGrab_obj_rot_x,RightGrab_obj_rot_y,RightGrab_obj_rot_z,RightGrab_obj_rot_w," +
                "model_name,model_rot_deg," +
                "LeftFootArea,RightFootArea"
            );
        }

        logging = true;
        Debug.Log($"Body logging started: {filePath}");

        // LSL marker: BT logging started.
        if (lslEnabled && LslOutletManager.Instance != null)
        {
            LslOutletManager.Instance.PushMarker($"BT_LOG_START;{participantCode}");
        }
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

        // LSL marker: BT logging stopped.
        if (lslEnabled && LslOutletManager.Instance != null)
        {
            LslOutletManager.Instance.PushMarker($"BT_LOG_STOP;{participantCode}");
        }
    }

    void OnDestroy()
    {
        StopLogging();
    }

    void OnApplicationQuit()
    {
        StopLogging();
    }

    // --- Grabbed object helpers ---
    private Transform GetNearestColliderTransform(Vector3 position, float radius)
    {
        Collider[] hits = Physics.OverlapSphere(position, radius);
        if (hits == null || hits.Length == 0)
            return null;

        float bestDistSq = float.MaxValue;
        Collider best = null;

        foreach (var hit in hits)
        {
            float dSq = (hit.ClosestPoint(position) - position).sqrMagnitude;
            if (dSq < bestDistSq)
            {
                bestDistSq = dSq;
                best = hit;
            }
        }

        return best != null ? best.transform : null;
    }

    private static bool NameContains(string value, string term)
    {
        return !string.IsNullOrEmpty(value) &&
               value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static Transform FindAncestorWithNameContaining(Transform start, string term, int maxDepth = 10)
    {
        int depth = 0;
        Transform current = start;
        while (current != null && depth++ < maxDepth)
        {
            if (NameContains(current.name, term)) return current;
            current = current.parent;
        }
        return null;
    }

    private static Transform FindFirstNonStudAncestor(Transform start, int maxDepth = 10)
    {
        int depth = 0;
        Transform current = start;
        while (current != null && depth++ < maxDepth)
        {
            if (!NameContains(current.name, "stud")) return current;
            current = current.parent;
        }
        return null;
    }

    private static string NormalizeGrabbedName(Transform t)
    {
        if (t == null) return "None";
        string rawName = t.name;

        if (NameContains(rawName, "stud"))
        {
            Transform brickAncestor = FindAncestorWithNameContaining(t, "brick");
            if (brickAncestor != null) return brickAncestor.name;

            Transform nonStud = FindFirstNonStudAncestor(t);
            if (nonStud != null) return nonStud.name;

            return t.root != null ? t.root.name : rawName;
        }

        return rawName;
    }

    // Model build tracking
    public void RecordModelBuildStart()
    {
        isBuildingModel = true;
        Debug.Log($"Model '{modelName}' build started at {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
    }

    public void RecordModelBuildEnd()
    {
        isBuildingModel = false;
        Debug.Log($"Model '{modelName}' build ended at {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
    }
}