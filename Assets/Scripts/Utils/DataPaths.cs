using System.IO;
using UnityEngine;

/// <summary>
/// Centralized resolver for runtime data folders.
///
/// Resolves a portable "data/" root next to the built executable
/// (or the project root in the Unity Editor) and ensures each
/// subfolder exists on first access.
///
/// Resolution rules:
/// - Standalone build: Application.dataPath == "&lt;exeFolder&gt;/&lt;exeName&gt;_Data",
///   so its parent is the folder containing the .exe.
/// - Editor: Application.dataPath == "&lt;projectRoot&gt;/Assets",
///   so its parent is the project root.
/// </summary>
public static class DataPaths
{
    private const string RootFolderName = "data";

    /// <summary>
    /// Absolute path to the root "data/" folder. Created on first access.
    /// </summary>
    public static string Root
    {
        get
        {
            string baseDir = Path.GetDirectoryName(Application.dataPath);
            string root = Path.Combine(baseDir, RootFolderName);
            Directory.CreateDirectory(root);
            return root;
        }
    }

    public static string BTData         => EnsureSub("BT_Data");
    public static string ETData         => EnsureSub("ET_Data");
    public static string BRData         => EnsureSub("BR_Data");
    public static string QData          => EnsureSub("Q_Data");
    public static string ModelOrderData => EnsureSub("Model_Order_Data");
    public static string ScreenshotData => EnsureSub("Screenshot_Data");

    private static string EnsureSub(string name)
    {
        string p = Path.Combine(Root, name);
        Directory.CreateDirectory(p);
        return p;
    }
}
