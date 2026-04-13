using System;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DeliveryDriver.Company
{
    [DefaultExecutionOrder(-425)]
    public class GameSceneCompanyPageInstaller : MonoBehaviour
    {
        private const string GameSceneName = "Game";
        private const string VehicleCatalogResourcePath = "Company/VehiclePrefabCatalog";
        private const string VehicleResourcesRoot = "Company/Vehicles";
        private const string VanPrefabAssetPath = "Assets/Prefabs/Vehicle/Minivan.prefab";
        private const string TruckPrefabAssetPath = "Assets/Prefabs/Vehicle/LorryCargo.prefab";
        private const string VanPrefabResourcePath = VehicleResourcesRoot + "/Minivan";
        private const string TruckPrefabResourcePath = VehicleResourcesRoot + "/LorryCargo";
        private static bool sceneHookRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            if (sceneHookRegistered)
            {
                return;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            sceneHookRegistered = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstallerForActiveScene()
        {
            TryEnsureInstaller(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryEnsureInstaller(scene);
        }

        private static void TryEnsureInstaller(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            if (!scene.name.Equals(GameSceneName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (UnityEngine.Object.FindFirstObjectByType<GameSceneCompanyPageInstaller>() != null)
            {
                return;
            }

            GameObject installerObject = new GameObject("GameSceneCompanyPageInstaller");
            installerObject.AddComponent<GameSceneCompanyPageInstaller>();
        }

        private void Awake()
        {
            if (!SceneManager.GetActiveScene().name.Equals(GameSceneName, StringComparison.OrdinalIgnoreCase))
            {
                Destroy(gameObject);
                return;
            }

            if (FindAnyObjectByType<PlayerVehicleManager>() == null)
            {
                GameObject vehicleManagerObject = new GameObject("PlayerVehicleManager");
                vehicleManagerObject.AddComponent<PlayerVehicleManager>();
            }

            PlayerVehicleManager vehicleManager = FindAnyObjectByType<PlayerVehicleManager>();
            if (vehicleManager != null)
            {
                vehicleManager.SetVehiclePrefabs(
                    LoadVehiclePrefab(VanPrefabAssetPath, "Van"),
                    LoadVehiclePrefab(TruckPrefabAssetPath, "Truck"));
            }

            if (FindAnyObjectByType<CompanyPageUI>() == null)
            {
                GameObject pageObject = new GameObject("CompanyPageUI");
                pageObject.AddComponent<CompanyPageUI>();
            }

            Destroy(gameObject);
        }

        private static GameObject LoadVehiclePrefab(string assetPath, string label)
        {
            string resourcePath = string.Equals(label, "Truck", StringComparison.OrdinalIgnoreCase)
                ? TruckPrefabResourcePath
                : VanPrefabResourcePath;

            GameObject directResourcePrefab = Resources.Load<GameObject>(resourcePath);
            if (directResourcePrefab != null)
            {
                return directResourcePrefab;
            }

            VehiclePrefabCatalog catalog = Resources.Load<VehiclePrefabCatalog>(VehicleCatalogResourcePath);
            if (catalog != null && catalog.TryGetPrefab(label, out GameObject catalogPrefab))
            {
                return catalogPrefab;
            }

#if UNITY_EDITOR
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                Debug.LogError($"[GameSceneCompanyPageInstaller] {label} prefab load failed. No runtime prefab was found at Resources/{resourcePath}, no valid catalog entry was found at Resources/{VehicleCatalogResourcePath}, and editor asset lookup also failed at '{assetPath}'.");
            }

            return prefab;
#else
            Debug.LogError($"[GameSceneCompanyPageInstaller] {label} prefab reference missing. Expected a runtime prefab at Resources/{resourcePath} or a runtime-loadable catalog at Resources/{VehicleCatalogResourcePath}.");
            return null;
#endif
        }
    }
}
