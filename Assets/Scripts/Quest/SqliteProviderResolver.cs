using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Mono.Data.Sqlite;
using UnityEngine;

namespace DeliveryDriver.Quest
{
    internal static class SqliteProviderResolver
    {
        private static bool nativeDependencyInitialized;
        private static bool nativeDependencyReady;
        private static string nativeDependencyDiagnostics = "SQLite native dependency has not been probed yet.";

        [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_libversion();

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ProbeManagedProvider()
        {
            EnsureNativeDependencyAvailable();

            // Touch the managed provider early so the player build keeps the assembly.
            _ = ResolveConnectionType();
            PreserveMethods();
        }

        public static Type ResolveConnectionType()
        {
            EnsureNativeDependencyAvailable();
            return typeof(SqliteConnection);
        }

        public static string GetNativeDependencyDiagnostics()
        {
            return nativeDependencyDiagnostics;
        }

        public static string TryGetNativeVersion()
        {
            try
            {
                EnsureNativeDependencyAvailable();
                IntPtr versionPtr = sqlite3_libversion();
                return versionPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(versionPtr) : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SqliteProviderResolver] Native sqlite3 probe failed: {ex.GetType().Name}: {ex.Message}. {nativeDependencyDiagnostics}");
                return null;
            }
        }

        private static bool EnsureNativeDependencyAvailable()
        {
            if (nativeDependencyInitialized)
            {
                return nativeDependencyReady;
            }

            nativeDependencyInitialized = true;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            List<string> attemptedPaths = new List<string>();
            string pluginDirectory = GetPreferredPluginDirectory();
            if (!string.IsNullOrWhiteSpace(pluginDirectory) && Directory.Exists(pluginDirectory))
            {
                bool searchPathConfigured = SetDllDirectory(pluginDirectory);
                int setDllDirectoryError = searchPathConfigured ? 0 : Marshal.GetLastWin32Error();
                Debug.Log(searchPathConfigured
                    ? $"[SqliteProviderResolver] Added SQLite plugin directory to the DLL search path: {pluginDirectory}"
                    : $"[SqliteProviderResolver] Failed to add SQLite plugin directory to the DLL search path: {pluginDirectory} (Win32={setDllDirectoryError})");
            }

            foreach (string candidatePath in EnumerateNativeLibraryCandidates())
            {
                attemptedPaths.Add(candidatePath);
                if (!File.Exists(candidatePath))
                {
                    continue;
                }

                IntPtr handle = LoadLibrary(candidatePath);
                if (handle != IntPtr.Zero)
                {
                    nativeDependencyReady = true;
                    nativeDependencyDiagnostics = $"Loaded sqlite3.dll from '{candidatePath}'.";
                    Debug.Log($"[SqliteProviderResolver] {nativeDependencyDiagnostics}");
                    return true;
                }

                int loadError = Marshal.GetLastWin32Error();
                Debug.LogWarning($"[SqliteProviderResolver] LoadLibrary failed for '{candidatePath}' (Win32={loadError}).");
            }

            nativeDependencyReady = false;
            nativeDependencyDiagnostics = attemptedPaths.Count > 0
                ? $"sqlite3.dll could not be loaded. Searched: {string.Join(" | ", attemptedPaths)}"
                : "sqlite3.dll could not be loaded because no candidate paths were resolved.";
            Debug.LogWarning($"[SqliteProviderResolver] {nativeDependencyDiagnostics}");
            return false;
#else
            nativeDependencyReady = true;
            nativeDependencyDiagnostics = "Native sqlite3 probing is only customized for Windows in this project.";
            return true;
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private static IEnumerable<string> EnumerateNativeLibraryCandidates()
        {
            string executableDirectory = GetExecutableDirectory();
            string dataPath = Application.dataPath;

            if (!string.IsNullOrWhiteSpace(executableDirectory))
            {
                yield return Path.Combine(executableDirectory, "sqlite3.dll");
            }

            if (!string.IsNullOrWhiteSpace(dataPath))
            {
                yield return Path.Combine(dataPath, "Plugins", "x86_64", "sqlite3.dll");
                yield return Path.Combine(dataPath, "Plugins", "sqlite3.dll");
            }
        }

        private static string GetPreferredPluginDirectory()
        {
            string dataPath = Application.dataPath;
            if (string.IsNullOrWhiteSpace(dataPath))
            {
                return null;
            }

            string x64Directory = Path.Combine(dataPath, "Plugins", "x86_64");
            if (Directory.Exists(x64Directory))
            {
                return x64Directory;
            }

            string pluginsDirectory = Path.Combine(dataPath, "Plugins");
            return Directory.Exists(pluginsDirectory) ? pluginsDirectory : null;
        }

        private static string GetExecutableDirectory()
        {
            string dataPath = Application.dataPath;
            if (string.IsNullOrWhiteSpace(dataPath))
            {
                return null;
            }

            DirectoryInfo dataDirectory = Directory.GetParent(dataPath);
            return dataDirectory != null ? dataDirectory.FullName : null;
        }
#endif
        
        private static void PreserveMethods()
        {
            // Prevent IL2CPP/Mono linker from stripping methods accessed via reflection in QuestDatabaseService/Bootstrap
            if (Application.isPlaying && Time.time > 999999f)
            {
                using (var conn = new SqliteConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "";
                        var p = cmd.CreateParameter();
                        p.ParameterName = "";
                        p.Value = null;
                        cmd.Parameters.Add(p);
                        cmd.ExecuteNonQuery();
                        cmd.ExecuteScalar();
                        using (var reader = cmd.ExecuteReader())
                        {
                            reader.Read();
                            reader.GetName(0);
                            reader.GetValue(0);
                            _ = reader.FieldCount;
                        }
                    }
                    conn.Close();
                }
            }
        }
    }
}
