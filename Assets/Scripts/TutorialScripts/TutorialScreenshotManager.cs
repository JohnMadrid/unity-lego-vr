using UnityEngine;
using System.Collections;
using System.IO;
using System;

/// <summary>
/// Manages screenshot capture from three camera angles (0°, 120°, 240°)
/// and saves them with detailed filenames.
/// Triggered externally (e.g. via button press) and continues the experiment flow after capture.
/// </summary>
public class TutorialScreenshotManager : MonoBehaviour
{
    // Cameras positioned around the model with rotation
    public Camera camFront;     
    public Camera camRight;   
    public Camera camLeft;   

    // Directory where screenshots will be saved
    private string screenshotPath = @"D:\LegoVR\unity-lego-vr\Other_than_in_project_files\Screenshot_Data";

    // Fixed screenshot resolution and reusable buffers
    [SerializeField] private int targetWidth = 1600;
    [SerializeField] private int targetHeight = 900;

    private RenderTexture sharedRenderTexture;
    private Texture2D sharedTexture;
    private Rect captureRect;

    private void EnsureBuffers()
    {
        if (sharedRenderTexture == null ||
            sharedRenderTexture.width != targetWidth ||
            sharedRenderTexture.height != targetHeight)
        {
            ReleaseBuffers();
            sharedRenderTexture = new RenderTexture(targetWidth, targetHeight, 24);
            sharedRenderTexture.Create();
        }

        if (sharedTexture == null ||
            sharedTexture.width != targetWidth ||
            sharedTexture.height != targetHeight)
        {
            sharedTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
        }

        captureRect = new Rect(0, 0, targetWidth, targetHeight);
    }

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

    private void OnDisable() => ReleaseBuffers();
    private void OnDestroy() => ReleaseBuffers();

    /// <summary>
    /// Captures screenshots from all three cameras and continues the tutorial flow.
    /// </summary>
    public IEnumerator CaptureScreenshotsAndContinue(TutorialGameManager tutorialGameManager)
    {
        string participantCode = tutorialGameManager.participantCode;
        string conditionName = $"Condition{tutorialGameManager.trialNumber}";
        int modelIndex = tutorialGameManager.GetCurrentItemIndex();
        string modelName = tutorialGameManager.modelPrefabs[modelIndex].name;

        // Updated camera list (3 cameras)
        Camera[] cameras = { camFront, camRight, camLeft };
        string[] positions = { "Front", "Right", "Left" };

        EnsureBuffers();

        for (int i = 0; i < cameras.Length; i++)
        {
            yield return StartCoroutine(
                CaptureFromCamera(cameras[i], positions[i], participantCode, conditionName, modelIndex, modelName)
            );

            // Allow one frame between captures
            yield return null;
        }

        tutorialGameManager.LoadNextItem();
    }

    /// <summary>
    /// Captures a screenshot from a single camera and saves it.
    /// </summary>
    private IEnumerator CaptureFromCamera(Camera cam, string position,
        string participantCode, string conditionName, int modelIndex, string modelName)
    {
        yield return new WaitForEndOfFrame();

        EnsureBuffers();

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = cam.targetTexture;

        float originalAspect = cam.aspect;
        float targetAspect = (float)targetWidth / targetHeight;
        cam.aspect = targetAspect;

        cam.targetTexture = sharedRenderTexture;
        cam.Render();

        RenderTexture.active = sharedRenderTexture;
        sharedTexture.ReadPixels(captureRect, 0, 0, false);
        sharedTexture.Apply(false, false);

        cam.targetTexture = previousTarget;
        RenderTexture.active = previousActive;
        cam.aspect = originalAspect;

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
        string filename = $"{participantCode}_{conditionName}_Model{modelIndex}_{modelName}_{position}_{timestamp}.png";
        string fullPath = Path.Combine(screenshotPath, filename);

        File.WriteAllBytes(fullPath, sharedTexture.EncodeToPNG());
        Debug.Log($"Screenshot saved: {fullPath}");
    }
}
