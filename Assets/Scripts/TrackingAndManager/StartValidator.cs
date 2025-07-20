using UnityEngine;
using TMPro;

/// <summary>
/// Validates that the player is looking directly at the fixation cross (via collider)
/// and holding both VR buttons continuously for a required time before proceeding.
/// </summary>
public class StartValidator : MonoBehaviour
{
    [Header("XR Rig References")]
    [Tooltip("The camera that follows the VR headset (usually the Main Camera)")]
    public Transform playerCamera;

    [Header("Fixation Settings")]
    [Tooltip("Collider attached to the fixation cross (must be a trigger or visible)")]
    public Collider fixationCollider;

    [Tooltip("Max distance the gaze ray can check (in meters)")]
    public float gazeRayMaxDistance = 10f;

    [Header("Buttons & Hold Logic")]
    public ButtonHoldSensor leftButton;
    public ButtonHoldSensor rightButton;

    [Header("UI Elements")]
    public GameObject fixationCross;
    public GameObject instructionMessage;
    public GameObject startMessage;
    // 27.06.2025 start
    public GameObject ValidationButtonLeft;
    public GameObject ValidationButtonRight;
    public GameObject NextLevelButton;
    // 27.06.2025 end

    // 03.07.2025 begin
    public GameObject fixationPanel;
    // 03.07.2025 end

    // 10.07.2025 begin
    public GameObject continuePanel;
    // 10.07.2025 end

    [Header("Timing Settings")]
    [Tooltip("Required gaze + button hold time (in seconds)")]
    public float requiredHoldTime = 1f;

    private float timer = 0f;
    private bool validationComplete = false;

    public bool IsValidated => validationComplete;

    private bool prevLooking = false;
    private bool prevLeftHeld = false;
    private bool prevRightHeld = false;

    void Update()
    {
        if (validationComplete) return;

        // Cast a ray from the player’s camera forward
        Ray gazeRay = new Ray(playerCamera.position, playerCamera.forward);
        bool isLooking = false;

        if (Physics.Raycast(gazeRay, out RaycastHit hitInfo, gazeRayMaxDistance))
        {
            if (hitInfo.collider == fixationCollider)
            {
                isLooking = true;
            }
        }

        // Check button hold states
        bool leftHeld = leftButton.IsHeld;
        bool rightHeld = rightButton.IsHeld;
        bool isHolding = leftHeld && rightHeld;

        // 🔎 Logging transitions only
        if (leftHeld && !prevLeftHeld) Debug.Log("Left button pressed.");
        if (!leftHeld && prevLeftHeld) Debug.Log("Left button released.");

        if (rightHeld && !prevRightHeld) Debug.Log("Right button pressed.");
        if (!rightHeld && prevRightHeld) Debug.Log("Right button released.");

        if (isLooking && !prevLooking) Debug.Log("Fixation collider fixated.");
        if (!isLooking && prevLooking) Debug.Log("Player looked away from fixation.");

        prevLeftHeld = leftHeld;
        prevRightHeld = rightHeld;
        prevLooking = isLooking;

        // If both conditions met, start accumulating time
        if (isLooking && isHolding)
        {
            instructionMessage?.SetActive(false);
            timer += Time.deltaTime;

            Debug.Log($"Validating... held for {timer:F2} / {requiredHoldTime} seconds.");

            if (timer >= requiredHoldTime)
                ConfirmStartPosition();
        }
        else
        {
            if (timer > 0f)
                Debug.Log("Validation reset — lost gaze or button hold.");
            timer = 0f;
            instructionMessage?.SetActive(true);
        }
    }

    void ConfirmStartPosition()
    {
        validationComplete = true;

        fixationCross?.SetActive(false);
        startMessage?.SetActive(true);
        // 27.06.2025 start
        ValidationButtonLeft?.SetActive(false);
        ValidationButtonRight?.SetActive(false);
        NextLevelButton?.SetActive(true);
        // 27.06.2025 end

        // 10.07.2025 start
        // enable next text and disable finish text
        continuePanel.transform.Find("NextText").gameObject.SetActive(true);
        continuePanel.transform.Find("FinishText").gameObject.SetActive(false);
        // 10.07.2025
        
        leftButton.SetColor(Color.green);
        rightButton.SetColor(Color.green);

        Debug.Log("Start position validated — player may proceed.");
    }

    public void ResetValidator()
    {
        validationComplete = false;
        timer = 0f;

        fixationCross?.SetActive(true);
        instructionMessage?.SetActive(true);
        startMessage?.SetActive(false);
        // 27.06.2025 start
        ValidationButtonLeft?.SetActive(true);
        ValidationButtonRight?.SetActive(true);
        NextLevelButton?.SetActive(false);
        // 27.06.2025 end

        // 03.07.2025 begin
        fixationPanel?.SetActive(true); // Enable fixation panel
        // 03.07.2025 end

        leftButton.SetColor(leftButton.restingColor);
        rightButton.SetColor(rightButton.restingColor);

        prevLooking = false;
        prevLeftHeld = false;
        prevRightHeld = false;

        Debug.Log("Validator reset — awaiting gaze and button hold.");
    }
}
