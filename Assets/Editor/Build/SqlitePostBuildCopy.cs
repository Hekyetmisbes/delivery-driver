using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

public static class SqlitePostBuildCopy
{
    private const string SourceRelativePath = "Assets/Plugins/x86_64/sqlite3.dll";

    [PostProcessBuild(1000)]
    private static void EnsureSqliteNativePlugin(BuildTarget target, string builtProjectPath)
    {
        if (target != BuildTarget.StandaloneWindows64)
        {
            return;
        }

        string sourcePath = Path.GetFullPath(SourceRelativePath);
        if (!File.Exists(sourcePath))
        {
            Debug.LogWarning($"[SqlitePostBuildCopy] sqlite3 source DLL not found at '{sourcePath}'.");
            return;
        }

        string buildDirectory = Path.GetDirectoryName(builtProjectPath);
        string executableName = Path.GetFileNameWithoutExtension(builtProjectPath);
        if (string.IsNullOrWhiteSpace(buildDirectory) || string.IsNullOrWhiteSpace(executableName))
        {
            Debug.LogWarning($"[SqlitePostBuildCopy] Could not resolve build output paths for '{builtProjectPath}'.");
            return;
        }

        string pluginsDirectory = Path.Combine(buildDirectory, $"{executableName}_Data", "Plugins", "x86_64");
        Directory.CreateDirectory(pluginsDirectory);

        string rootDestinationPath = Path.Combine(buildDirectory, "sqlite3.dll");
        File.Copy(sourcePath, rootDestinationPath, true);

        string pluginsDestinationPath = Path.Combine(pluginsDirectory, "sqlite3.dll");
        File.Copy(sourcePath, pluginsDestinationPath, true);

        Debug.Log($"[SqlitePostBuildCopy] Copied sqlite3.dll to '{rootDestinationPath}' and '{pluginsDestinationPath}'.");
    }
}
