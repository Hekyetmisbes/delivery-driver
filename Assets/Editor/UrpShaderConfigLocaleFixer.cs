using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeliveryDriver.EditorTools
{
    public static class UrpShaderConfigLocaleFixer
    {
        private const string SessionKey = "DeliveryDriver.EditorTools.UrpShaderConfigLocaleFixer.Ran";
        private const string MenuPath = "Tools/Rendering/Fix Generated Shader Includes";

        [InitializeOnLoadMethod]
        private static void QueueFix()
        {
            if (Application.isBatchMode || SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            SessionState.SetBool(SessionKey, true);
            EditorApplication.delayCall += RunIfSafe;
        }

        [MenuItem(MenuPath)]
        public static void FixNow()
        {
            RunFix(logWhenUnchanged: true);
        }

        private static void RunIfSafe()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += RunIfSafe;
                return;
            }

            RunFix(logWhenUnchanged: false);
        }

        private static void RunFix(bool logWhenUnchanged)
        {
            string projectRoot = Directory.GetCurrentDirectory();
            List<string> targetFiles = EnumerateTargetFiles(projectRoot).ToList();
            int changedCount = 0;

            foreach (string fullPath in targetFiles)
            {
                if (!TryNormalizeFile(fullPath))
                {
                    continue;
                }

                changedCount++;
            }

            if (changedCount > 0)
            {
                Debug.Log($"[URP Fix] Normalized locale-sensitive identifiers in {changedCount} generated shader include file(s).");
                AssetDatabase.Refresh();
                return;
            }

            if (logWhenUnchanged)
            {
                Debug.Log("[URP Fix] Generated shader includes did not need locale normalization.");
            }
        }

        private static IEnumerable<string> EnumerateTargetFiles(string projectRoot)
        {
            string packageCacheRoot = Path.Combine(projectRoot, "Library", "PackageCache");
            if (!Directory.Exists(packageCacheRoot))
            {
                yield break;
            }

            foreach (string filePath in Directory.EnumerateFiles(packageCacheRoot, "*.cs.hlsl", SearchOption.AllDirectories))
            {
                yield return filePath;
            }
        }

        private static bool TryNormalizeFile(string fullPath)
        {
            if (!File.Exists(fullPath))
            {
                return false;
            }

            string original = File.ReadAllText(fullPath, Encoding.UTF8);
            string normalized = NormalizeIdentifiers(original);
            if (string.Equals(original, normalized, StringComparison.Ordinal))
            {
                return false;
            }

            File.WriteAllText(fullPath, normalized, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Debug.Log($"[URP Fix] Normalized locale-sensitive identifiers in '{fullPath}'.");
            return true;
        }

        private static string NormalizeIdentifiers(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return value
                .Replace('\u0130', 'I')
                .Replace('\u0131', 'i')
                .Replace("Ä°", "I", StringComparison.Ordinal)
                .Replace("Ä±", "i", StringComparison.Ordinal)
                .Replace("Ş", "S", StringComparison.Ordinal)
                .Replace("ş", "s", StringComparison.Ordinal)
                .Replace("Ğ", "G", StringComparison.Ordinal)
                .Replace("ğ", "g", StringComparison.Ordinal)
                .Replace("Ü", "U", StringComparison.Ordinal)
                .Replace("ü", "u", StringComparison.Ordinal)
                .Replace("Ö", "O", StringComparison.Ordinal)
                .Replace("ö", "o", StringComparison.Ordinal)
                .Replace("Ç", "C", StringComparison.Ordinal)
                .Replace("ç", "c", StringComparison.Ordinal);
        }
    }
}
