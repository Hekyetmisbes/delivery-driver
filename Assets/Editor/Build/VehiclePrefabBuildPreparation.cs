using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class VehiclePrefabBuildPreparation : IPreprocessBuildWithReport
{
    private const string ResourcesCompanyFolder = "Assets/Resources/Company";
    private const string ResourcesVehiclesFolder = ResourcesCompanyFolder + "/Vehicles";

    private static readonly (string source, string destination)[] VehicleCopies =
    {
        ("Assets/Prefabs/Vehicle/Minivan.prefab", ResourcesVehiclesFolder + "/Minivan.prefab"),
        ("Assets/Prefabs/Vehicle/LorryCargo.prefab", ResourcesVehiclesFolder + "/LorryCargo.prefab")
    };

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        EnsureFolder(ResourcesCompanyFolder);
        EnsureFolder(ResourcesVehiclesFolder);

        bool copiedAny = false;
        foreach ((string source, string destination) in VehicleCopies)
        {
            if (!File.Exists(source))
            {
                Debug.LogError($"[VehiclePrefabBuildPreparation] Source prefab is missing: {source}");
                continue;
            }

            if (!File.Exists(destination))
            {
                if (!AssetDatabase.CopyAsset(source, destination))
                {
                    Debug.LogError($"[VehiclePrefabBuildPreparation] Failed to copy '{source}' to '{destination}'.");
                    continue;
                }
            }
            else
            {
                File.Copy(source, destination, true);
                AssetDatabase.ImportAsset(destination);
            }

            copiedAny = true;
            Debug.Log($"[VehiclePrefabBuildPreparation] Copied '{source}' to '{destination}' for runtime vehicle loading.");
        }

        if (copiedAny)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
        string name = Path.GetFileName(folderPath);
        if (!string.IsNullOrWhiteSpace(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, name);
    }
}
