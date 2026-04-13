using UnityEngine;

/// <summary>
/// Hook for the experimenter UI interrupt button with Yes/No confirmation flow.
/// </summary>
public class ExperimentInterruptButton : MonoBehaviour
{
    [Tooltip("Optional. If unset, the script will auto-find the first GameManager in the scene.")]
    public GameManager gameManager;

    [Header("Interrupt Confirmation UI")]
    [Tooltip("Initial interrupt button object (hidden while confirmation is shown).")]
    public GameObject interruptButtonObject;

    [Tooltip("Optional text object associated with the interrupt button.")]
    public GameObject interruptButtonTextObject;

    [Tooltip("Confirmation text object shown after pressing interrupt.")]
    public GameObject confirmationTextObject;

    [Tooltip("Yes button object shown after pressing interrupt.")]
    public GameObject confirmYesButtonObject;

    [Tooltip("No button object shown after pressing interrupt.")]
    public GameObject confirmNoButtonObject;

    private void Awake()
    {
        // Initial state: interrupt button visible, confirmation hidden.
        SetInterruptVisible(true);
        SetConfirmationVisible(false);
    }

    // Assign this method to the Interrupt button OnClick event.
    public void OnInterruptClicked()
    {
        // Step 1: Ask for confirmation instead of terminating immediately.
        SetInterruptVisible(false);
        SetConfirmationVisible(true);
    }

    // Assign this method to the Yes button OnClick event.
    public void OnConfirmYes()
    {
        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
        }

        if (gameManager == null)
        {
            Debug.LogWarning("ExperimentInterruptButton: GameManager not found in scene.");
            return;
        }

        gameManager.InterruptAndCleanupCurrentCondition();
    }

    // Assign this method to the No button OnClick event.
    public void OnConfirmNo()
    {
        // Cancel interruption and restore default UI state.
        SetConfirmationVisible(false);
        SetInterruptVisible(true);
    }

    private void SetConfirmationVisible(bool visible)
    {
        if (confirmationTextObject != null) confirmationTextObject.SetActive(visible);
        if (confirmYesButtonObject != null) confirmYesButtonObject.SetActive(visible);
        if (confirmNoButtonObject != null) confirmNoButtonObject.SetActive(visible);
    }

    private void SetInterruptVisible(bool visible)
    {
        if (interruptButtonObject != null) interruptButtonObject.SetActive(visible);
        if (interruptButtonTextObject != null) interruptButtonTextObject.SetActive(visible);
    }
}

