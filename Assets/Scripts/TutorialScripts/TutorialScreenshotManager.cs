using UnityEngine;
using System.Collections;
using System.IO;
using System;

/// <summary>
/// Manages screenshot capture from multiple camera angles and saves them with detailed filenames.
/// Triggered externally (e.g. via button press) and continues the experiment flow after capture.
/// </summary>
public class TutorialScreenshotManager : MonoBehaviour
{
    // Cameras positioned around the model for different angles
    public Camera frontCamera;
    public Camera backCamera;
    public Camera leftCamera;
    public Camera rightCamera;
    public Camera topCamera;

    // Directory where screenshots will be saved
    private string screenshotPath = @"D:\LegoVR\unity-lego-vr\Other_than_in_project_files\Screenshot_Data";

    // 07.08.2025 Start: Fixed screenshot resolution and reusable buffers ~J
    // Use a fixed output resolution independent of Game view size to control
    // file size and remove dependency on screen resolution.
    [SerializeField] private int targetWidth = 1600;   // width in pixels
    [SerializeField] private int targetHeight = 900;   // height in pixels

    // Shared render targets/textures reused across all five captures to
    // minimize allocations and GC spikes.
    private RenderTexture sharedRenderTexture;
    private Texture2D sharedTexture;
    private Rect captureRect;

    /// <summary>
    /// Ensures the reusable buffers exist and match the configured resolution.
    /// </summary>
    private void EnsureBuffers()
    {
        // Create or recreate the shared RenderTexture if dimensions changed
        if (sharedRenderTexture == null || sharedRenderTexture.width != targetWidth || sharedRenderTexture.height != targetHeight)
        {
            ReleaseBuffers();
            sharedRenderTexture = new RenderTexture(targetWidth, targetHeight, 24);
            sharedRenderTexture.Create();
        }

        // Create or recreate the shared Texture2D if dimensions changed
        if (sharedTexture == null || sharedTexture.width != targetWidth || sharedTexture.height != targetHeight)
        {
            sharedTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
        }

        // Precompute the capture rectangle
        captureRect = new Rect(0, 0, targetWidth, targetHeight);
    }

    /// <summary>
    /// Releases and destroys reusable GPU/CPU buffers.
    /// </summary>
    private void ReleaseBuffers()
    {
        if (sharedRenderTexture != null)
        {
            sharedRenderTexture.Release();
            Destroy(sharedRenderTexture);
            sharedRenderTexture = null;
        }

        if (sharedTexture != null)
        {
            Destroy(sharedTexture);
            sharedTexture = null;
        }
    }

    private void OnDisable()
    {
        ReleaseBuffers();
    }

    private void OnDestroy()
    {
        ReleaseBuffers();
    }
    // 07.08.2025 End ~J

    /// <summary>
    /// Captures screenshots from all five cameras after a short delay,
    /// then continues to the next item via GameManager.
    /// </summary>
    /// <param name="tutorialGameManager">Reference to the GameManager for accessing experiment data and flow control</param>
    public IEnumerator CaptureScreenshotsAndContinue(TutorialGameManager tutorialGameManager)
    {

        // Fetch metadata from GameManager
        string participantCode = tutorialGameManager.participantCode;
        string conditionName = $"Condition{tutorialGameManager.trialNumber}"; // 30.07.2025 begin changed Trial to condition
        int modelIndex = tutorialGameManager.GetCurrentItemIndex();
        string modelName = tutorialGameManager.modelPrefabs[modelIndex].name;

        // Define cameras and their corresponding labels
        Camera[] cameras = { frontCamera, backCamera, leftCamera, rightCamera, topCamera };
        string[] positions = { "Front", "Back", "Left", "Right", "Top" };

        // 07.08.2025 Start: Ensure buffers and capture one camera per frame to reduce stutter ~J
        // Prepare the shared buffers once before starting the multi-camera capture.
        EnsureBuffers();

        // Capture from each camera sequentially with a yield between shots to
        // distribute work across frames and avoid blocking a single frame.
        for (int i = 0; i < cameras.Length; i++)
        {
            yield return StartCoroutine(CaptureFromCamera(cameras[i], positions[i], participantCode, conditionName, modelIndex, modelName));
            // Let the engine breathe one extra frame between shots (disk IO/GC)
            yield return null;
        }
        // 07.08.2025 End ~J

        // Continue experiment flow
        tutorialGameManager.LoadNextItem();
    }

    /// <summary>
    /// Captures a screenshot from a single camera and saves it with a detailed filename.
    /// </summary>
    private IEnumerator CaptureFromCamera(Camera cam, string position, string participantCode, string conditionName, int modelIndex, string modelName)
    {
        // 07.08.2025 Start: Per-frame capture using reusable buffers ~J
        // Step 1: Wait until the current frame finishes rendering to avoid partial frames
        yield return new WaitForEndOfFrame();

        // Step 2: Ensure the reusable buffers exist
        EnsureBuffers();

        // Step 3: Temporarily set camera to render into the shared RenderTexture
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = cam.targetTexture;

        // Optionally enforce a stable aspect matching the capture resolution
        float originalAspect = cam.aspect;
        float targetAspect = (float)targetWidth / targetHeight;
        cam.aspect = targetAspect;

        cam.targetTexture = sharedRenderTexture;
        cam.Render();

        // Step 4: Read pixels from GPU into the shared Texture2D
        RenderTexture.active = sharedRenderTexture;
        sharedTexture.ReadPixels(captureRect, 0, 0, false);
        sharedTexture.Apply(false, false);

        // Step 5: Restore camera and RT state
        cam.targetTexture = previousTarget;
        RenderTexture.active = previousActive;
        cam.aspect = originalAspect;

        // Step 6: Build filename and write PNG to disk
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
        string filename = $"{participantCode}_{conditionName}_Model{modelIndex}_{modelName}_{position}_{timestamp}.png";
        string fullPath = Path.Combine(screenshotPath, filename);

        File.WriteAllBytes(fullPath, sharedTexture.EncodeToPNG());
        Debug.Log($"Screenshot saved: {fullPath}");
        // 07.08.2025 End ~J
    }
}
