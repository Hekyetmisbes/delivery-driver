using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryDriver.Cinematic
{
    /// <summary>
    /// Oynanış sahnelerinde sinematik kamera, yönetmen ve kayıt HUD sistemini
    /// sahneye dokunmadan otomatik olarak başlatan ve garanti altına alan bootstrap bileşeni.
    /// </summary>
    public static class CinematicCaptureInstaller
    {
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
            // Sadece MainMenu sahnesinde çalıştırma, diğer tüm sahnelerde (Game, GameScene vb.) aktif et
            if (scene.name.Equals("MainMenu", StringComparison.OrdinalIgnoreCase)) return;

            EnsureCinematicSystem();
        }

        /// <summary>
        /// Sahneye [CinematicCaptureSystem] nesnesini ekler veya zaten varsa referansını döner.
        /// Editor window veya Play mode esnasında her an çağrılabilir.
        /// </summary>
        public static GameObject EnsureCinematicSystem()
        {
            CinematicCameraController existing = UnityEngine.Object.FindFirstObjectByType<CinematicCameraController>();
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject root = new GameObject("[CinematicCaptureSystem]");
            UnityEngine.Object.DontDestroyOnLoad(root);

            root.AddComponent<CinematicCameraController>();
            root.AddComponent<CinematicDirector>();
            root.AddComponent<CinematicCaptureHUD>();

            Debug.Log("<color=#33FF88><b>[CinematicCaptureInstaller]</b></color> Sinematik Çekim & Yönetmen Sistemi sahneye başarıyla kuruldu.");
            return root;
        }
    }
}
