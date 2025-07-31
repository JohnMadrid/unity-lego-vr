using UnityEngine;
using System.Collections;
using System.IO;
using System;

/// <summary>
/// Manages screenshot capture from multiple camera angles and saves them with detailed filenames.
/// Triggered externally (e.g. via button press) and continues the experiment flow after capture.
/// </summary>
public class ScreenshotManager : MonoBehaviour
{
    // Cameras positioned around the model for different angles
    public Camera frontCamera;
    public Camera backCamera;
    public Camera leftCamera;
    public Camera rightCamera;
    public Camera topCamera;

    // Directory where screenshots will be saved
    private string screenshotPath = @"D:\LegoVR\unity-lego-vr\Other_than_in_project_files\Screenshot_Data";

    /// <summary>
    /// Captures screenshots from all five cameras after a short delay,
    /// then continues to the next item via GameManager.
    /// </summary>
    /// <param name="gameManager">Reference to the GameManager for accessing experiment data and flow control</param>
    public IEnumerator CaptureScreenshotsAndContinue(GameManager gameManager)
    {

        // Fetch metadata from GameManager
        string participantCode = gameManager.participantCode;
        string conditionName = $"Condition{gameManager.trialNumber}"; // 30.07.2025 changed Trial to Condition
        int modelIndex = gameManager.GetCurrentItemIndex();
        string modelName = gameManager.modelPrefabs[modelIndex].name;

        // Define cameras and their corresponding labels
        Camera[] cameras = { frontCamera, backCamera, leftCamera, rightCamera, topCamera };
        string[] positions = { "Front", "Back", "Left", "Right", "Top" };

        // Capture from each camera sequentially
        for (int i = 0; i < cameras.Length; i++)
        {
            yield return StartCoroutine(CaptureFromCamera(cameras[i], positions[i], participantCode, conditionName, modelIndex, modelName));
        }

        // Continue experiment flow
        gameManager.LoadNextItem();
    }

    /// <summary>
    /// Captures a screenshot from a single camera and saves it with a detailed filename.
    /// </summary>
    private IEnumerator CaptureFromCamera(Camera cam, string position, string participantCode, string conditionName, int modelIndex, string modelName)
    {
        // Wait until the frame is fully rendered
        yield return new WaitForEndOfFrame();

        // Create a temporary render texture for capturing
        RenderTexture rt = new RenderTexture(Screen.width, Screen.height, 24);
        cam.targetTexture = rt;

        // Create a texture to store the screenshot
        Texture2D screenshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);

        // Render the camera view and read pixels into the texture
        cam.Render();
        RenderTexture.active = rt;
        screenshot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        screenshot.Apply();

        // Clean up render texture
        cam.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);

        // Generate timestamp and filename
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
        string filename = $"{participantCode}_{conditionName}_Model{modelIndex}_{modelName}_{position}_{timestamp}.png";
        string fullPath = Path.Combine(screenshotPath, filename);

        // Save the screenshot to disk
        File.WriteAllBytes(fullPath, screenshot.EncodeToPNG());
        Debug.Log($"Screenshot saved: {fullPath}");
    }
}
