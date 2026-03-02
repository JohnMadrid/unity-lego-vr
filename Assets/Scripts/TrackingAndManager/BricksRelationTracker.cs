using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Logs snapping relationships between moving bricks and their targets (board or other bricks).
///
/// The logger writes CSV rows with the following columns:
/// raw_timestamp (Unix ms),
/// relative_timestamp (Time.time),
/// condition_number,
/// trial_number,
/// snapped_brick_name,
/// target_object_name,
/// snapped_studs  (semicolon-separated stud IDs on the moving brick),
/// target_studs   (semicolon-separated stud IDs on the target),
/// snap_event_type ("snap","unsnap","resnap"),
/// snapped_pos_x, snapped_pos_y, snapped_pos_z,
/// snapped_rot_x, snapped_rot_y, snapped_rot_z, snapped_rot_w,
/// target_pos_x,  target_pos_y,  target_pos_z,
/// target_rot_x,  target_rot_y,  target_rot_z,  target_rot_w.
///
/// Logging is event-driven: no work is done in Update().
/// </summary>
public class BricksRelationTracker : MonoBehaviour
{
    private StreamWriter writer;
    private bool logging = false;
    private string filePath;

    [SerializeField] public bool trackingEnabled = true;

    private GameManager gameManager;
    private string participantCode = "Unknown";

    private void Start()
    {
        gameManager = FindObjectOfType<GameManager>();
    }

    /// <summary>
    /// Public API used by other systems to record a snap-like event.
    /// This overload defaults the snapEventType to "snap".
    /// </summary>
    public void RecordSnapEvent(BrickBehavior snappedBrick,
                                BrickBehavior targetBrickOrBoard,
                                List<Stud> snappedStuds,
                                List<Stud> targetStuds)
    {
        RecordSnapEvent(snappedBrick, targetBrickOrBoard, snappedStuds, targetStuds, "snap");
    }

    /// <summary>
    /// Extended overload that lets callers specify the snap_event_type
    /// (e.g., "snap", "unsnap", "resnap").
    /// </summary>
    public void RecordSnapEvent(BrickBehavior snappedBrick,
                                BrickBehavior targetBrickOrBoard,
                                List<Stud> snappedStuds,
                                List<Stud> targetStuds,
                                string snapEventType)
    {
        if (!trackingEnabled)
            return;

        // Ensure logging is initialized lazily.
        if (!logging)
        {
            StartLogging();
            if (!logging)
                return; // If StartLogging failed for some reason.
        }

        // Refresh GameManager reference if needed.
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        long rawTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        float relativeTimestamp = Time.time;

        int conditionNumber = (gameManager != null) ? gameManager.trialNumber : 0;
        int trialNumber = (gameManager != null) ? Mathf.Clamp(gameManager.GetCurrentItemIndex(), 0, 6) : 0;

        string snappedBrickName = SafeName(snappedBrick != null ? snappedBrick.gameObject.name : "UnknownSnappedBrick");

        // Determine target object name and transform even if there is no Brick component (e.g., baseboard).
        Transform targetTransform = null;
        string targetObjectName;

        if (targetBrickOrBoard != null)
        {
            targetTransform = targetBrickOrBoard.transform;
            targetObjectName = SafeName(targetBrickOrBoard.gameObject.name);
        }
        else
        {
            // Fallback: infer target from target studs (e.g., board studs without Brick component).
            if (targetStuds != null && targetStuds.Count > 0 && targetStuds[0] != null)
            {
                Transform t = targetStuds[0].transform;
                if (t != null)
                {
                    // Use the root object name (e.g., "Lego_Board_10x10").
                    Transform root = t.root != null ? t.root : t;
                    targetTransform = root;
                    targetObjectName = SafeName(root.gameObject.name);
                }
                else
                {
                    targetObjectName = "UnknownTarget";
                }
            }
            else
            {
                targetObjectName = "UnknownTarget";
            }
        }

        // Fallback for targetTransform if still null but brick exists.
        if (targetTransform == null && targetBrickOrBoard != null)
            targetTransform = targetBrickOrBoard.transform;

        // Collect stud ID lists (semicolon-separated, safe for CSV).
        string snappedStudList = FormatStudList(snappedStuds);
        string targetStudList = FormatStudList(targetStuds);

        string safeEventType = SafeField(snapEventType ?? "snap");

        // Positions and rotations in world space, defaulting to zero if missing.
        Vector3 snappedPos = Vector3.zero;
        Quaternion snappedRot = Quaternion.identity;
        if (snappedBrick != null && snappedBrick.transform != null)
        {
            snappedPos = snappedBrick.transform.position;
            snappedRot = snappedBrick.transform.rotation;
        }

        Vector3 targetPos = Vector3.zero;
        Quaternion targetRot = Quaternion.identity;
        if (targetTransform != null)
        {
            targetPos = targetTransform.position;
            targetRot = targetTransform.rotation;
        }

        // Resolve current model name at the time of the event.
        string modelName = SafeField(GetCurrentModelName());

        // Build CSV row.
        string row =
            $"{rawTimestamp}," +
            $"{relativeTimestamp}," +
            $"{conditionNumber}," +
            $"{trialNumber}," +
            $"{snappedBrickName}," +
            $"{targetObjectName}," +
            $"{snappedStudList}," +
            $"{targetStudList}," +
            $"{safeEventType}," +
            $"{snappedPos.x},{snappedPos.y},{snappedPos.z}," +
            $"{snappedRot.x},{snappedRot.y},{snappedRot.z},{snappedRot.w}," +
            $"{targetPos.x},{targetPos.y},{targetPos.z}," +
            $"{targetRot.x},{targetRot.y},{targetRot.z},{targetRot.w}," +
            $"{modelName}";

        writer.WriteLine(row);
        writer.Flush();
    }

    /// <summary>
    /// Manually start logging (mirrors pattern used in other tracking managers).
    /// </summary>
    public void StartLoggingManually()
    {
        if (!logging && trackingEnabled)
            StartLogging();
    }

    /// <summary>
    /// Manually stop logging and close the CSV.
    /// </summary>
    public void StopLoggingManually()
    {
        StopLogging();
    }

    private void StartLogging()
    {
        try
        {
            string logPath = @"D:\LegoVR\unity-lego-vr\Other_than_in_project_files\BR_Data";
            Directory.CreateDirectory(logPath);

            DateTime now = DateTime.Now;

            // Determine participant code in the same way as ViveTrackerManager.
            var tutorialGM = GameObject.Find("TutorialGameManager")?.GetComponent<TutorialGameManager>();
            var gameGM = GameObject.Find("GameManager")?.GetComponent<GameManager>();

            if (tutorialGM != null)
                participantCode = tutorialGM.participantCode;
            else if (gameGM != null)
                participantCode = gameGM.participantCode;
            else
                participantCode = "Unknown";

            string fileName = $"{participantCode}_BR_Data_{now:yyyy-MM-dd}.csv";
            filePath = Path.Combine(logPath, fileName);

            bool fileExists = File.Exists(filePath);

            FileStream stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            writer = new StreamWriter(stream);

            if (!fileExists)
            {
                writer.WriteLine(
                    "raw_timestamp,relative_timestamp," +
                    "condition_number,trial_number," +
                    "snapped_brick_name,target_object_name," +
                    "snapped_studs,target_studs," +
                    "snap_event_type," +
                    "snapped_pos_x,snapped_pos_y,snapped_pos_z," +
                    "snapped_rot_x,snapped_rot_y,snapped_rot_z,snapped_rot_w," +
                    "target_pos_x,target_pos_y,target_pos_z," +
                    "target_rot_x,target_rot_y,target_rot_z,target_rot_w," +
                    "model_name"
                );
                writer.Flush();
            }

            logging = true;
            Debug.Log($"BricksRelationTracker logging started: {filePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"BricksRelationTracker failed to start logging: {ex}");
            logging = false;
        }
    }

    private void StopLogging()
    {
        if (!logging)
            return;

        try
        {
            if (writer != null)
            {
                writer.Flush();
                writer.Close();
                writer = null;
            }

            logging = false;
            Debug.Log($"BricksRelationTracker logging ended. Data saved at {filePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"BricksRelationTracker failed to stop logging cleanly: {ex}");
        }
    }

    private void OnApplicationQuit()
    {
        StopLogging();
    }

    /// <summary>
    /// Resolve the current model name based on the active modelSpawnPoint,
    /// mirroring the logic used in ViveTrackerManager.
    /// </summary>
    private string GetCurrentModelName()
    {
        Transform modelSpawnPoint = null;

        var tutorialGM = GameObject.Find("TutorialGameManager")?.GetComponent<TutorialGameManager>();
        if (tutorialGM != null && tutorialGM.modelSpawnPoint != null)
        {
            modelSpawnPoint = tutorialGM.modelSpawnPoint;
        }
        else
        {
            var gameGM = GameObject.Find("GameManager")?.GetComponent<GameManager>();
            if (gameGM != null)
                modelSpawnPoint = gameGM.modelSpawnPoint;
        }

        if (modelSpawnPoint != null && modelSpawnPoint.childCount > 0)
        {
            string rawName = modelSpawnPoint.GetChild(0).gameObject.name;
            return rawName.Replace("(Clone)", "").Trim();
        }

        return "None";
    }

    /// <summary>
    /// Convert a list of studs to a semicolon-separated list of stud identifiers.
    /// Uses the stud GameObject name as a stable identifier.
    /// Handles null lists and missing entries gracefully.
    /// </summary>
    private string FormatStudList(List<Stud> studs)
    {
        if (studs == null || studs.Count == 0)
            return string.Empty;

        List<string> ids = new List<string>(studs.Count);
        for (int i = 0; i < studs.Count; i++)
        {
            var stud = studs[i];
            if (stud == null)
                continue;

            string id = stud.gameObject != null ? stud.gameObject.name : "UnnamedStud";
            ids.Add(SafeField(id));
        }

        return string.Join(";", ids);
    }

    /// <summary>
    /// Sanitize a name for safe CSV usage (no commas, trimmed).
    /// </summary>
    private string SafeName(string value)
    {
        return SafeField(value);
    }

    /// <summary>
    /// Generic CSV-safe field sanitizer (removes commas and trims whitespace).
    /// </summary>
    private string SafeField(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace(",", "_").Trim();
    }
}
