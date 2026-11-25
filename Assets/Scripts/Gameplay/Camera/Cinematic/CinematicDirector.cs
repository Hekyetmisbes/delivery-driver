using System;
using System.Collections.Generic;
using DeliveryDriver.Vehicle;
using UnityEngine;

namespace DeliveryDriver.Cinematic
{
    /// <summary>
    /// Otomatik Yönetmen Modu (Automated Cinematic Director).
    /// Aracı sürerken belirli aralıklarla (3-5 sn) kamera açılarını dinamik,
    /// ritmik ve profesyonel bir klip/fragman kurgusu şeklinde otomatik olarak değiştirir.
    /// </summary>
    public class CinematicDirector : MonoBehaviour
    {
        public static CinematicDirector Instance { get; private set; }

        [Header("Yönetmen Ayarları")]
        [SerializeField] private bool isDirectorActive = false;
        [Tooltip("Minimum sahne süresi (saniye)")]
        [SerializeField] private float minCutInterval = 3.0f;
        [Tooltip("Maksimum sahne süresi (saniye)")]
        [SerializeField] private float maxCutInterval = 5.0f;
        [Tooltip("Kamera geçişlerinde anlık kesme (Jump-cut) kullanılsın mı?")]
        [SerializeField] private bool useInstantCuts = true;

        [Header("Açı Havuzu")]
        [SerializeField] private List<CinematicCameraAngle> anglePool = new List<CinematicCameraAngle>
        {
            CinematicCameraAngle.HoodBumper,
            CinematicCameraAngle.WheelDrift,
            CinematicCameraAngle.DoorMirrorProfile,
            CinematicCameraAngle.LowRearExhaust,
            CinematicCameraAngle.HelicopterDrone,
            CinematicCameraAngle.RoadsideFlyby,
            CinematicCameraAngle.GameplayChase
        };

        private CinematicCameraController cameraController;
        private float cutTimer = 0f;
        private float currentInterval = 4.0f;
        private CinematicCameraAngle lastAngle = CinematicCameraAngle.GameplayChase;
        private Rigidbody targetRb;

        public bool IsDirectorActive => isDirectorActive;
        public float TimeUntilNextCut => Mathf.Max(0f, currentInterval - cutTimer);
        public float CurrentInterval => currentInterval;

        public event Action<bool> OnDirectorToggled;
        public event Action<CinematicCameraAngle> OnDirectorCut;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            cameraController = CinematicCameraController.Instance;
            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<CinematicCameraController>();
            }
            ResetInterval();
        }

        public void ToggleDirector()
        {
            SetDirector(!isDirectorActive);
        }

        public void SetDirector(bool active)
        {
            isDirectorActive = active;
            cutTimer = 0f;
            ResetInterval();

            if (active)
            {
                if (cameraController == null)
                {
                    cameraController = CinematicCameraController.Instance;
                }

                if (cameraController != null && !cameraController.IsCinematicActive)
                {
                    cameraController.SetCinematicMode(true);
                }

                // İlk kesimi hemen yap
                PerformDirectorCut();
            }

            OnDirectorToggled?.Invoke(active);
        }

        private void ResetInterval()
        {
            currentInterval = UnityEngine.Random.Range(minCutInterval, maxCutInterval);
        }

        private void Update()
        {
            if (!isDirectorActive) return;

            if (cameraController == null)
            {
                cameraController = CinematicCameraController.Instance;
                if (cameraController == null) return;
            }

            // Unscaled delta time kullanıyoruz (ağır çekimde yönetmen donmasın)
            cutTimer += Time.unscaledDeltaTime;
            if (cutTimer >= currentInterval)
            {
                cutTimer = 0f;
                ResetInterval();
                PerformDirectorCut();
            }
        }

        /// <summary>
        /// Aracın hızına ve durumuna göre en sinematik açıyı seçer ve kesim yapar.
        /// </summary>
        public void PerformDirectorCut()
        {
            if (cameraController == null) return;

            CinematicCameraAngle chosenAngle = SelectNextAngle();
            lastAngle = chosenAngle;

            cameraController.SetAngle(chosenAngle, instantCut: useInstantCuts);
            OnDirectorCut?.Invoke(chosenAngle);
        }

        private CinematicCameraAngle SelectNextAngle()
        {
            if (anglePool == null || anglePool.Count == 0)
            {
                return CinematicCameraAngle.GameplayChase;
            }

            // Aracın hızını ölç
            float speedKmh = 0f;
            if (cameraController.VehicleTarget != null)
            {
                if (targetRb == null)
                {
                    targetRb = cameraController.VehicleTarget.GetComponentInParent<Rigidbody>();
                }
                if (targetRb != null)
                {
                    speedKmh = targetRb.linearVelocity.magnitude * 3.6f;
                }
            }

            // Aday listesi oluştur (son seçilen açıyı ele)
            List<CinematicCameraAngle> candidates = new List<CinematicCameraAngle>();
            for (int i = 0; i < anglePool.Count; i++)
            {
                if (anglePool[i] != lastAngle)
                {
                    candidates.Add(anglePool[i]);
                }
            }

            if (candidates.Count == 0)
            {
                return CinematicCameraAngle.GameplayChase;
            }

            // Araç hızlıysa (> 50 km/h) dinamik açılara (kaput, jant, egzoz, roadside) ağırlık ver
            if (speedKmh > 50f)
            {
                List<CinematicCameraAngle> highSpeedAngles = new List<CinematicCameraAngle>
                {
                    CinematicCameraAngle.HoodBumper,
                    CinematicCameraAngle.WheelDrift,
                    CinematicCameraAngle.LowRearExhaust,
                    CinematicCameraAngle.RoadsideFlyby
                };

                for (int i = 0; i < highSpeedAngles.Count; i++)
                {
                    if (candidates.Contains(highSpeedAngles[i]))
                    {
                        // Ağırlık artırmak için ikişer defa ekle
                        candidates.Add(highSpeedAngles[i]);
                    }
                }
            }

            int randomIndex = UnityEngine.Random.Range(0, candidates.Count);
            return candidates[randomIndex];
        }
    }
}
