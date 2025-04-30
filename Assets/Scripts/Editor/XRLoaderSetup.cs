using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEditor.XR.Management;

using System.Collections.Generic;
using System.Linq; // Ensure LINQ is included

[InitializeOnLoad]
public static class XRLoaderSetup
{
    static XRLoaderSetup()
    {
        SetupXRLoaders();
    }

    private static void SetupXRLoaders()
    {
        var targetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
        var generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(targetGroup);
        if (generalSettings == null)
        {
            Debug.LogWarning("XR General Settings are null, cannot modify loader list.");
            return;
        }

        var settingsManager = generalSettings.Manager;
        if (settingsManager == null)
        {
            Debug.LogWarning("XR Manager is null, cannot modify loader list.");
            return;
        }

        var currentLoaders = new List<XRLoader>(settingsManager.activeLoaders);

        // Find the VarjoXR and OpenXR loaders
        var varjoLoader = currentLoaders.FirstOrDefault(loader => loader.GetType().Name.Contains("Varjo"));
        var openXRLoader = currentLoaders.FirstOrDefault(loader => loader.GetType().Name.Contains("OpenXR"));

        if (varjoLoader == null || openXRLoader == null)
        {
            Debug.LogWarning("Could not find Varjo or OpenXR loader. Ensure the plugins are installed correctly.");
            return;
        }

        // Ensure VarjoXR loader is first
        currentLoaders.Remove(varjoLoader);
        currentLoaders.Insert(0, varjoLoader);

        if (!settingsManager.TrySetLoaders(currentLoaders))
        {
            Debug.LogError("Failed to set the reordered loader list!");
        }
        else
        {
            Debug.Log("XR loader order updated successfully.");
        }
    }
}