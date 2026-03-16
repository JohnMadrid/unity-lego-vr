using UnityEngine;
using LSL;

/// <summary>
/// Central LSL outlet manager for LegoVR.
/// Attach this as an extra component to your existing TrackingManager object.
/// </summary>
public class LslOutletManager : MonoBehaviour
{
    public static LslOutletManager Instance { get; private set; }

    [Header("LSL Toggles")]
    [Tooltip("Enable/disable eye-tracking numeric LSL stream.")]
    public bool enableEtStream = true;

    [Tooltip("Enable/disable body-tracking numeric LSL stream.")]
    public bool enableBtStream = true;

    [Tooltip("Enable/disable string marker LSL stream.")]
    public bool enableMarkers = true;

    [Header("Optional stream names (advanced)")]
    public string etStreamName = "LegoVR_EyeTracking";
    public string btStreamName = "LegoVR_BodyTracking";
    public string markerStreamName = "LegoVR_Markers";

    // --- Session metadata (used as LSL stream description fields) ---
    string participantCode = "Unknown";
    int conditionNumber = 0;
    string sceneName = "";

    // --- LSL outlets ---
    StreamOutlet etOutlet;
    StreamOutlet btOutlet;
    StreamOutlet markerOutlet;

    void Awake()
    {
        // Simple singleton: first instance wins, later ones just log a warning.
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("LslOutletManager: Multiple instances detected. Using the first one.");
            return;
        }

        Instance = this;

        // IMPORTANT:
        // We do NOT call DontDestroyOnLoad here because your TrackingManager
        // might already control its own lifetime across scenes.
        // If you want this object to persist across scenes, add DontDestroyOnLoad
        // on the TrackingManager GameObject or here explicitly.
    }

    /// <summary>
    /// Update participant/condition/scene information for new sessions or scenes.
    /// Call this from ParticipantInputManager/GameManager when those values are known.
    /// </summary>
    public void SetSessionInfo(string participant, int condition, string scene)
    {
        participantCode = string.IsNullOrEmpty(participant) ? "Unknown" : participant;
        conditionNumber = condition;
        sceneName = scene ?? "";
    }

    // =========================
    //   EYE TRACKING OUTLET
    // =========================

    /// <summary>
    /// Ensure the ET outlet exists. Safe to call multiple times.
    /// channelCount must match the length of the float[] you push.
    /// </summary>
    public void EnsureEtOutlet(int channelCount)
    {
        if (!enableEtStream) return;
        if (etOutlet != null) return;
        if (channelCount <= 0)
        {
            Debug.LogWarning("LslOutletManager.EnsureEtOutlet: channelCount must be > 0.");
            return;
        }

        var info = new StreamInfo(
            etStreamName,
            "EyeTracking",
            channelCount,
            0, // irregular timing; Varjo provides its own timestamps
            channel_format_t.cf_float32,
            "lego_et"
        );

        // Attach metadata
        var desc = info.desc();
        desc.append_child_value("participant", participantCode);
        desc.append_child_value("condition", conditionNumber.ToString());
        desc.append_child_value("scene", sceneName);
        desc.append_child_value("software", Application.productName);
        desc.append_child_value("version", Application.version);

        etOutlet = new StreamOutlet(info);
        Debug.Log($"LslOutletManager: Created ET outlet '{etStreamName}' with {channelCount} channels.");
    }

    /// <summary>
    /// Push a single ET sample (float array) to the ET LSL stream.
    /// Make sure channel count matches what you used in EnsureEtOutlet.
    /// </summary>
    public void PushEtSample(float[] sample)
    {
        if (!enableEtStream) return;
        if (etOutlet == null)
        {
            Debug.LogWarning("LslOutletManager.PushEtSample: etOutlet is null. Call EnsureEtOutlet() first.");
            return;
        }
        if (sample == null)
        {
            Debug.LogWarning("LslOutletManager.PushEtSample: sample is null.");
            return;
        }

        etOutlet.push_sample(sample);
    }

    // =========================
    //   BODY TRACKING OUTLET
    // =========================

    /// <summary>
    /// Ensure the BT outlet exists. Safe to call multiple times.
    /// channelCount must match the length of the float[] you push.
    /// </summary>
    public void EnsureBtOutlet(int channelCount, double nominalSamplingRate = 90.0)
    {
        if (!enableBtStream) return;
        if (btOutlet != null) return;
        if (channelCount <= 0)
        {
            Debug.LogWarning("LslOutletManager.EnsureBtOutlet: channelCount must be > 0.");
            return;
        }

        var info = new StreamInfo(
            btStreamName,
            "BodyTracking",
            channelCount,
            nominalSamplingRate, // typically Application.targetFrameRate (e.g., 90 Hz)
            channel_format_t.cf_float32,
            "lego_bt"
        );

        var desc = info.desc();
        desc.append_child_value("participant", participantCode);
        desc.append_child_value("condition", conditionNumber.ToString());
        desc.append_child_value("scene", sceneName);
        desc.append_child_value("software", Application.productName);
        desc.append_child_value("version", Application.version);

        btOutlet = new StreamOutlet(info);
        Debug.Log($"LslOutletManager: Created BT outlet '{btStreamName}' with {channelCount} channels at {nominalSamplingRate} Hz.");
    }

    /// <summary>
    /// Push a single BT sample (float array) to the BT LSL stream.
    /// </summary>
    public void PushBtSample(float[] sample)
    {
        if (!enableBtStream) return;
        if (btOutlet == null)
        {
            Debug.LogWarning("LslOutletManager.PushBtSample: btOutlet is null. Call EnsureBtOutlet() first.");
            return;
        }
        if (sample == null)
        {
            Debug.LogWarning("LslOutletManager.PushBtSample: sample is null.");
            return;
        }

        btOutlet.push_sample(sample);
    }

    // =========================
    //        MARKERS
    // =========================

    /// <summary>
    /// Ensure the marker outlet exists. Safe to call multiple times.
    /// </summary>
    public void EnsureMarkerOutlet()
    {
        if (!enableMarkers) return;
        if (markerOutlet != null) return;

        var info = new StreamInfo(
            markerStreamName,
            "Markers",
            1,      // single string channel
            0,      // irregular
            channel_format_t.cf_string,
            "lego_markers"
        );

        var desc = info.desc();
        desc.append_child_value("participant", participantCode);
        desc.append_child_value("condition", conditionNumber.ToString());
        desc.append_child_value("scene", sceneName);
        desc.append_child_value("software", Application.productName);
        desc.append_child_value("version", Application.version);

        markerOutlet = new StreamOutlet(info);
        Debug.Log($"LslOutletManager: Created marker outlet '{markerStreamName}'.");
    }

    /// <summary>
    /// Push a string marker sample like
    /// \"TRIAL_START;P001;cond=1;item=0;model=TM\".
    /// </summary>
    public void PushMarker(string message)
    {
        if (!enableMarkers) return;
        if (string.IsNullOrEmpty(message)) return;

        if (markerOutlet == null)
        {
            EnsureMarkerOutlet();
            if (markerOutlet == null)
            {
                Debug.LogWarning("LslOutletManager.PushMarker: markerOutlet is still null after EnsureMarkerOutlet().");
                return;
            }
        }

        markerOutlet.push_sample(new[] { message });
    }
}