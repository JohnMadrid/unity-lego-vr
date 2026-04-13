using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deletes files permanently.
/// </summary>
public static class RecycleBinDeleteUtility
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    public static void DeleteFilesToRecycleBin(IEnumerable<string> filePaths)
    {
        if (filePaths == null) return;

        foreach (var path in filePaths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            DeleteFileToRecycleBin(path);
        }
    }

    public static void DeleteFileToRecycleBin(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        try
        {
            if (System.IO.File.Exists(filePath))
            {
                // Permanent delete on Windows.
                System.IO.File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"RecycleBinDeleteUtility: Permanent delete failed for '{filePath}'. {ex.Message}");
        }
    }
#else
    // Fallback for non-Windows: do a hard delete.
    public static void DeleteFilesToRecycleBin(IEnumerable<string> filePaths)
    {
        if (filePaths == null) return;
        foreach (var path in filePaths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            DeleteFileToRecycleBin(path);
        }
    }

    public static void DeleteFileToRecycleBin(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        try
        {
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"RecycleBinDeleteUtility: Hard delete failed for '{filePath}'. {ex.Message}");
        }
    }
#endif
}

