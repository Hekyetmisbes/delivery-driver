#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;

namespace DeliveryDriver.EditorTools
{
    /// <summary>
    /// Forces Unity to regenerate external project files via the active code editor integration.
    /// Falls back to Unity's internal SyncVS pipeline when the current editor integration is a no-op.
    /// </summary>
    public static class ProjectFileSyncUtility
    {
        private const string SyncRequestFileName = "SyncExternalProjectFiles.request";

        [InitializeOnLoadMethod]
        private static void SyncIfRequestedOnLoad()
        {
            EditorApplication.delayCall += TryConsumeSyncRequest;
        }

        [MenuItem("Tools/Maintenance/Sync External Project Files")]
        public static void SyncProjectFilesMenu()
        {
            SyncProjectFilesCore();
            Debug.Log("[ProjectFileSyncUtility] External project files synced.");
        }

        public static void SyncProjectFilesBatch()
        {
            try
            {
                SyncProjectFilesCore();
                Debug.Log("[ProjectFileSyncUtility] External project files synced.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProjectFileSyncUtility] Failed to sync project files: {ex}");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }

        private static void SyncProjectFilesCore()
        {
            AssetDatabase.Refresh();

            Dictionary<string, DateTime> beforeTimestamps = CaptureProjectFileTimestamps();

            bool synced = TrySyncViaCurrentCodeEditor();
            AssetDatabase.Refresh();

            if (!ProjectFilesChanged(beforeTimestamps))
            {
                synced |= TrySyncViaUnityReflection();
                AssetDatabase.Refresh();
            }

            if (!synced)
            {
                throw new InvalidOperationException("No Unity project file sync mechanism succeeded.");
            }
        }

        private static bool TrySyncViaCurrentCodeEditor()
        {
            IExternalCodeEditor externalCodeEditor = CodeEditor.Editor.CurrentCodeEditor;
            if (externalCodeEditor == null)
            {
                Debug.LogWarning("[ProjectFileSyncUtility] No external code editor integration is currently registered.");
                return false;
            }

            externalCodeEditor.SyncAll();
            return true;
        }

        private static bool TrySyncViaUnityReflection()
        {
            Type syncVsType = Type.GetType("UnityEditor.SyncVS,UnityEditor");
            if (syncVsType == null)
            {
                Debug.LogWarning("[ProjectFileSyncUtility] UnityEditor.SyncVS type was not found.");
                return false;
            }

            string[] candidateMethodNames =
            {
                "SyncSolution",
                "SynchronizeSolution",
                "Sync",
                "GenerateAndWriteSolutionAndProjects"
            };

            foreach (string methodName in candidateMethodNames)
            {
                MethodInfo method = syncVsType.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (method == null || method.GetParameters().Length != 0)
                {
                    continue;
                }

                method.Invoke(null, null);
                Debug.Log($"[ProjectFileSyncUtility] Synced project files via UnityEditor.SyncVS.{methodName}().");
                return true;
            }

            Debug.LogWarning("[ProjectFileSyncUtility] UnityEditor.SyncVS exists but no supported sync method was found.");
            return false;
        }

        private static Dictionary<string, DateTime> CaptureProjectFileTimestamps()
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string[] projectFiles =
            {
                Path.Combine(projectRoot, "Assembly-CSharp.csproj"),
                Path.Combine(projectRoot, "Assembly-CSharp-Editor.csproj"),
                Path.Combine(projectRoot, "Delivery Driver.slnx"),
                Path.Combine(projectRoot, "Delivery Driver.sln")
            };

            Dictionary<string, DateTime> timestamps = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in projectFiles)
            {
                if (File.Exists(path))
                {
                    timestamps[path] = File.GetLastWriteTimeUtc(path);
                }
            }

            return timestamps;
        }

        private static bool ProjectFilesChanged(Dictionary<string, DateTime> beforeTimestamps)
        {
            if (beforeTimestamps == null || beforeTimestamps.Count == 0)
            {
                return false;
            }

            return beforeTimestamps.Any(pair =>
            {
                if (!File.Exists(pair.Key))
                {
                    return true;
                }

                return File.GetLastWriteTimeUtc(pair.Key) != pair.Value;
            });
        }

        private static void TryConsumeSyncRequest()
        {
            string requestPath = Path.Combine(Directory.GetCurrentDirectory(), "Temp", SyncRequestFileName);
            if (!File.Exists(requestPath))
            {
                return;
            }

            File.Delete(requestPath);

            try
            {
                SyncProjectFilesCore();
                Debug.Log("[ProjectFileSyncUtility] Consumed sync request and regenerated project files.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProjectFileSyncUtility] Failed while consuming sync request: {ex}");
            }
        }
    }
}
#endif
