using UnityEngine;
using UnityEngine.XR;

// This script monitors the state of the VR headset and pauses the game when the headset is removed.
// It resumes the game when the headset is worn again.
public class HeadsetMonitor : MonoBehaviour
{
    private bool isPaused = false;

    void Update()
    {
        if (!XRSettings.isDeviceActive && !isPaused)
        {
            isPaused = true;
            PauseGame();
        }
        else if (XRSettings.isDeviceActive && isPaused)
        {
            isPaused = false;
            ResumeGame();
        }
    }

    void PauseGame()
    {
        Time.timeScale = 0;  // Freeze the game
        Debug.Log("Headset removed! Game is paused.");
    }

    void ResumeGame()
    {
        Time.timeScale = 1;  // Resume the game
        Debug.Log("Headset worn again! Game continues.");
    }
}
