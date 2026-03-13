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
        private const string VanPrefabAssetPath = "Assets/Prefabs/Vehicle/Minivan.prefab";
        private const string TruckPrefabAssetPath = "Assets/Prefabs/Vehicle/LorryCargo.prefab";
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
#if UNITY_EDITOR
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                Debug.LogError($"[GameSceneCompanyPageInstaller] {label} prefab load failed at path '{assetPath}'.");
            }

            return prefab;
#else
            Debug.LogError($"[GameSceneCompanyPageInstaller] {label} prefab reference missing. Asset path loading is only available in the Unity Editor for '{assetPath}'.");
            return null;
#endif
        }
    }
}
