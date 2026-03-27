using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DeliveryDriver.EditorTools
{
    public static class UrpBuildSceneRepairUtility
    {
        private const string MenuPath = "Tools/Rendering/Repair Build Scenes For URP";
        private const string AutoRepairSessionKey = "DeliveryDriver.EditorTools.UrpBuildSceneRepairUtility.AutoRepairCompleted";
        private const MaterialUpgrader.UpgradeFlags UpgradeFlags =
            MaterialUpgrader.UpgradeFlags.LogMessageWhenNoUpgraderFound;

        [InitializeOnLoadMethod]
        private static void QueueAutomaticRepair()
        {
            if (Application.isBatchMode || SessionState.GetBool(AutoRepairSessionKey, false))
            {
                return;
            }

            SessionState.SetBool(AutoRepairSessionKey, true);
            EditorApplication.delayCall += RunAutomaticRepairIfSafe;
        }

        [MenuItem(MenuPath)]
        public static void RepairBuildScenesForUrp()
        {
            RunRepair(interactive: true);
        }

        public static void RepairBuildScenesForUrpBatchMode()
        {
            RunRepair(interactive: false);
        }

        public static void WriteBuildSceneUrpReportBatchMode()
        {
            string reportPath = Path.Combine(Directory.GetCurrentDirectory(), "urp-build-scene-report.txt");
            File.WriteAllText(reportPath, BuildLegacyMaterialReport());
            Debug.Log($"[URP Repair] Wrote build-scene URP report to '{reportPath}'.");
        }

        private static void RunAutomaticRepairIfSafe()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += RunAutomaticRepairIfSafe;
                return;
            }

            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isDirty)
            {
                Debug.LogWarning("[URP Repair] Automatic build-scene repair skipped because the active scene has unsaved changes. Use Tools/Rendering/Repair Build Scenes For URP when convenient.");
                return;
            }

            RunRepair(interactive: false);

            string reportPath = Path.Combine(Directory.GetCurrentDirectory(), "urp-build-scene-report.txt");
            File.WriteAllText(reportPath, BuildLegacyMaterialReport());
            Debug.Log($"[URP Repair] Wrote build-scene URP report to '{reportPath}'.");
        }

        private static void RunRepair(bool interactive)
        {
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset pipelineAsset)
            {
                Fail("URP repair skipped because no active Universal Render Pipeline asset is assigned.", interactive);
                return;
            }

            List<MaterialUpgrader> upgraders = MaterialUpgrader.FetchAllUpgradersForPipeline(typeof(UniversalRenderPipelineAsset));
            if (upgraders.Count == 0)
            {
                Fail("URP repair skipped because Unity did not expose any material upgraders for Universal Render Pipeline.", interactive);
                return;
            }

            List<string> buildScenePaths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (buildScenePaths.Count == 0)
            {
                Fail("URP repair skipped because there are no enabled build scenes.", interactive);
                return;
            }

            var processedMaterials = new HashSet<int>();
            var materialAssetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var prefabPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string scenePath in buildScenePaths)
            {
                foreach (string dependencyPath in AssetDatabase.GetDependencies(scenePath, true))
                {
                    if (dependencyPath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                    {
                        materialAssetPaths.Add(dependencyPath);
                    }
                    else if (dependencyPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    {
                        prefabPaths.Add(dependencyPath);
                    }
                }
            }

            int upgradedMaterialAssets = 0;
            int upgradedPrefabMaterials = 0;
            int upgradedSceneMaterials = 0;

            foreach (string materialPath in materialAssetPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (TryUpgradeMaterial(material, upgraders, processedMaterials))
                {
                    EditorUtility.SetDirty(material);
                    upgradedMaterialAssets++;
                }
            }

            foreach (string prefabPath in prefabPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                GameObject prefabRoot = null;

                try
                {
                    prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                    if (prefabRoot == null)
                    {
                        continue;
                    }

                    int before = upgradedPrefabMaterials;
                    upgradedPrefabMaterials += UpgradeMaterialsInHierarchy(prefabRoot, upgraders, processedMaterials);
                    if (upgradedPrefabMaterials > before)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[URP Repair] Failed to inspect prefab '{prefabPath}': {ex.Message}");
                }
                finally
                {
                    if (prefabRoot != null)
                    {
                        PrefabUtility.UnloadPrefabContents(prefabRoot);
                    }
                }
            }

            string originalScenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;

            try
            {
                foreach (string scenePath in buildScenePaths)
                {
                    var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    int before = upgradedSceneMaterials;

                    upgradedSceneMaterials += UpgradeMaterialsInLoadedScenes(upgraders, processedMaterials);

                    Material skybox = RenderSettings.skybox;
                    if (TryUpgradeMaterial(skybox, upgraders, processedMaterials))
                    {
                        upgradedSceneMaterials++;
                    }

                    if (upgradedSceneMaterials > before)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                }
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(originalScenePath) && File.Exists(originalScenePath))
                {
                    EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[URP Repair] Active pipeline: {pipelineAsset.name}. " +
                $"Upgraded {upgradedMaterialAssets} material assets, {upgradedPrefabMaterials} prefab-scoped materials, " +
                $"{upgradedSceneMaterials} scene-scoped materials across {buildScenePaths.Count} build scenes.");
        }

        private static int UpgradeMaterialsInLoadedScenes(
            List<MaterialUpgrader> upgraders,
            HashSet<int> processedMaterials)
        {
            int upgradedCount = 0;

            foreach (Renderer renderer in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (!renderer.gameObject.scene.IsValid())
                {
                    continue;
                }

                upgradedCount += UpgradeRendererMaterials(renderer, upgraders, processedMaterials);
            }

            foreach (Terrain terrain in Resources.FindObjectsOfTypeAll<Terrain>())
            {
                if (!terrain.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (TryUpgradeMaterial(terrain.materialTemplate, upgraders, processedMaterials))
                {
                    EditorUtility.SetDirty(terrain);
                    upgradedCount++;
                }
            }

            return upgradedCount;
        }

        private static int UpgradeMaterialsInHierarchy(
            GameObject root,
            List<MaterialUpgrader> upgraders,
            HashSet<int> processedMaterials)
        {
            int upgradedCount = 0;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                upgradedCount += UpgradeRendererMaterials(renderer, upgraders, processedMaterials);
            }

            foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
            {
                if (TryUpgradeMaterial(terrain.materialTemplate, upgraders, processedMaterials))
                {
                    EditorUtility.SetDirty(terrain);
                    upgradedCount++;
                }
            }

            return upgradedCount;
        }

        private static int UpgradeRendererMaterials(
            Renderer renderer,
            List<MaterialUpgrader> upgraders,
            HashSet<int> processedMaterials)
        {
            int upgradedCount = 0;
            Material[] materials = renderer.sharedMaterials;

            for (int i = 0; i < materials.Length; i++)
            {
                if (TryUpgradeMaterial(materials[i], upgraders, processedMaterials))
                {
                    upgradedCount++;
                }
            }

            if (upgradedCount > 0)
            {
                EditorUtility.SetDirty(renderer);
            }

            return upgradedCount;
        }

        private static bool TryUpgradeMaterial(
            Material material,
            List<MaterialUpgrader> upgraders,
            HashSet<int> processedMaterials)
        {
            if (material == null || material.shader == null)
            {
                return false;
            }

            int instanceId = material.GetInstanceID();
            if (!processedMaterials.Add(instanceId))
            {
                return false;
            }

            string originalShader = material.shader.name;
            string message = string.Empty;

            bool upgraded = MaterialUpgrader.Upgrade(material, upgraders, UpgradeFlags, ref message);
            if (!upgraded)
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    Debug.Log($"[URP Repair] {message}");
                }

                return false;
            }

            if (material.shader != null && !string.Equals(originalShader, material.shader.name, StringComparison.Ordinal))
            {
                EditorUtility.SetDirty(material);
            }

            return true;
        }

        private static void Fail(string message, bool interactive)
        {
            if (interactive && !Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("URP Repair", message, "OK");
            }

            Debug.LogError($"[URP Repair] {message}");
        }

        private static string BuildLegacyMaterialReport()
        {
            List<MaterialUpgrader> upgraders = MaterialUpgrader.FetchAllUpgradersForPipeline(typeof(UniversalRenderPipelineAsset));
            var legacyShaderNames = new HashSet<string>(
                upgraders
                    .Select(upgrader => upgrader.OldShaderPath)
                    .Where(path => !string.IsNullOrWhiteSpace(path)),
                StringComparer.Ordinal);

            List<string> buildScenePaths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var findings = new List<string>();
            var processedMaterials = new HashSet<int>();
            var prefabPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var materialAssetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string scenePath in buildScenePaths)
            {
                foreach (string dependencyPath in AssetDatabase.GetDependencies(scenePath, true))
                {
                    if (dependencyPath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                    {
                        materialAssetPaths.Add(dependencyPath);
                    }
                    else if (dependencyPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    {
                        prefabPaths.Add(dependencyPath);
                    }
                }
            }

            foreach (string materialPath in materialAssetPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                AddLegacyFinding(findings, processedMaterials, legacyShaderNames, material, $"asset:{materialPath}");
            }

            foreach (string prefabPath in prefabPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                GameObject prefabRoot = null;

                try
                {
                    prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                    if (prefabRoot == null)
                    {
                        continue;
                    }

                    foreach (Renderer renderer in prefabRoot.GetComponentsInChildren<Renderer>(true))
                    {
                        foreach (Material material in renderer.sharedMaterials)
                        {
                            AddLegacyFinding(findings, processedMaterials, legacyShaderNames, material, $"prefab:{prefabPath}");
                        }
                    }

                    foreach (Terrain terrain in prefabRoot.GetComponentsInChildren<Terrain>(true))
                    {
                        AddLegacyFinding(findings, processedMaterials, legacyShaderNames, terrain.materialTemplate, $"prefab:{prefabPath}");
                    }
                }
                finally
                {
                    if (prefabRoot != null)
                    {
                        PrefabUtility.UnloadPrefabContents(prefabRoot);
                    }
                }
            }

            string originalScenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;

            try
            {
                foreach (string scenePath in buildScenePaths)
                {
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                    foreach (Renderer renderer in Resources.FindObjectsOfTypeAll<Renderer>())
                    {
                        if (!renderer.gameObject.scene.IsValid())
                        {
                            continue;
                        }

                        foreach (Material material in renderer.sharedMaterials)
                        {
                            AddLegacyFinding(findings, processedMaterials, legacyShaderNames, material, $"scene:{scenePath}");
                        }
                    }

                    foreach (Terrain terrain in Resources.FindObjectsOfTypeAll<Terrain>())
                    {
                        if (!terrain.gameObject.scene.IsValid())
                        {
                            continue;
                        }

                        AddLegacyFinding(findings, processedMaterials, legacyShaderNames, terrain.materialTemplate, $"scene:{scenePath}");
                    }

                    AddLegacyFinding(findings, processedMaterials, legacyShaderNames, RenderSettings.skybox, $"scene:{scenePath}:RenderSettings");
                }
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(originalScenePath) && File.Exists(originalScenePath))
                {
                    EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
                }
            }

            findings.Sort(StringComparer.Ordinal);

            if (findings.Count == 0)
            {
                return "No remaining build-scene materials matched Unity's Built-in -> URP upgrader list.";
            }

            return
                $"Remaining upgradable materials: {findings.Count}{Environment.NewLine}" +
                string.Join(Environment.NewLine, findings);
        }

        private static void AddLegacyFinding(
            List<string> findings,
            HashSet<int> processedMaterials,
            HashSet<string> legacyShaderNames,
            Material material,
            string context)
        {
            if (material == null || material.shader == null)
            {
                return;
            }

            if (!processedMaterials.Add(material.GetInstanceID()))
            {
                return;
            }

            if (!legacyShaderNames.Contains(material.shader.name))
            {
                return;
            }

            findings.Add($"{context} => {material.name} [{material.shader.name}]");
        }
    }
}
