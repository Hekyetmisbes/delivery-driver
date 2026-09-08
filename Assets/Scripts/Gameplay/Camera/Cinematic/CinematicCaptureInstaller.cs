using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryDriver.Cinematic
{
    /// <summary>
    /// Game sahnesi açıldığında sinematik kamera, yönetmen ve kayıt HUD sistemini
    /// sahneye dokunmadan otomatik olarak başlatan bootstrap bileşeni.
    /// </summary>
    [DefaultExecutionOrder(-400)]
    public static class CinematicCaptureInstaller
    {
        private const string GameSceneName = "Game";
        private static bool sceneHookRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            if (sceneHookRegistered) return;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            sceneHookRegistered = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureCinematicSystemForActiveScene()
        {
            TryEnsureCinematicSystem(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryEnsureCinematicSystem(scene);
        }

        private static void TryEnsureCinematicSystem(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            if (!scene.name.Equals(GameSceneName, StringComparison.OrdinalIgnoreCase)) return;

            if (UnityEngine.Object.FindFirstObjectByType<CinematicCameraController>() != null) return;

            GameObject root = new GameObject("[CinematicCaptureSystem]");
            root.AddComponent<CinematicCameraController>();
            root.AddComponent<CinematicDirector>();
            root.AddComponent<CinematicCaptureHUD>();

            Debug.Log("[CinematicCaptureInstaller] Sinematik Çekim & Yönetmen Sistemi sahneye otomatik olarak kuruldu.");
        }
    }
}
