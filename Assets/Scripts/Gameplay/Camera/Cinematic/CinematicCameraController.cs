using System;
using System.Collections.Generic;
using DeliveryDriver.Vehicle;
using UnityEngine;

namespace DeliveryDriver.Cinematic
{
    /// <summary>
    /// Hem 16:9 Trailer hem de 9:16 Instagram Reels formatları için
    /// profesyonel sinematik kamera açılarını ve geçişlerini yöneten kontrolcü.
    /// </summary>
    public class CinematicCameraController : MonoBehaviour
    {
        public static CinematicCameraController Instance { get; private set; }

        [Header("Hedef ve Referanslar")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform vehicleTarget;
        [SerializeField] private CameraFollow gameplayCameraFollow;

        [Header("Sinematik Yapılandırma")]
        [SerializeField] private bool isCinematicActive = false;
        [SerializeField] private CinematicCameraAngle currentAngle = CinematicCameraAngle.GameplayChase;
        [SerializeField] private CinematicVideoFormat currentFormat = CinematicVideoFormat.Trailer_16_9;
        [SerializeField] private float fovTransitionSpeed = 6f;

        [Header("Açı Önayarları (Presets)")]
        [SerializeField] private List<CameraPresetSettings> presets = new List<CameraPresetSettings>();

        [Header("Ağır Çekim (Slow-Motion)")]
        [SerializeField] private float slowMotionScale = 0.25f;
        private bool isSlowMotionActive = false;

        // Runtime geçiş değişkenleri
        private Vector3 currentVelocity;
        private Vector3 roadsideWorldPosition;
        private bool isRoadsideInitialized = false;
        private float targetFov;
        private float currentFov;
        private Rigidbody targetRb;
        private CarController carController;

        public bool IsCinematicActive => isCinematicActive;
        public CinematicCameraAngle CurrentAngle => currentAngle;
        public CinematicVideoFormat CurrentFormat => currentFormat;
        public bool IsSlowMotionActive => isSlowMotionActive;
        public Camera TargetCamera => targetCamera;
        public Transform VehicleTarget => vehicleTarget;

        public event Action<CinematicCameraAngle> OnAngleChanged;
        public event Action<CinematicVideoFormat> OnFormatChanged;
        public event Action<bool> OnCinematicModeToggled;
        public event Action<bool> OnSlowMotionToggled;

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

            InitializePresets();
            ResolveReferences();
        }

        private void Start()
        {
            ResolveReferences();
            if (targetCamera != null)
            {
                currentFov = targetCamera.fieldOfView;
                targetFov = currentFov;
            }
        }

        private void InitializePresets()
        {
            if (presets != null && presets.Count >= 7) return;

            presets = new List<CameraPresetSettings>();
            for (int i = 0; i <= (int)CinematicCameraAngle.RoadsideFlyby; i++)
            {
                presets.Add(CameraPresetSettings.CreateDefault((CinematicCameraAngle)i));
            }
        }

        private void ResolveReferences()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null)
                {
                    targetCamera = FindFirstObjectByType<Camera>();
                }
            }

            if (gameplayCameraFollow == null && targetCamera != null)
            {
                gameplayCameraFollow = targetCamera.GetComponent<CameraFollow>();
                if (gameplayCameraFollow == null)
                {
                    gameplayCameraFollow = FindFirstObjectByType<CameraFollow>();
                }
            }

            if (vehicleTarget == null)
            {
                if (gameplayCameraFollow != null && gameplayCameraFollow.target != null)
                {
                    SetVehicleTarget(gameplayCameraFollow.target);
                }
                else
                {
                    Transform resolved = VehicleCameraTargetResolver.ResolveDefaultTarget();
                    if (resolved != null)
                    {
                        SetVehicleTarget(resolved);
                    }
                }
            }
        }

        public void SetVehicleTarget(Transform newTarget)
        {
            vehicleTarget = newTarget;
            if (vehicleTarget != null)
            {
                VehicleCameraBinding binding = VehicleCameraTargetResolver.Resolve(newTarget);
                targetRb = binding.Rigidbody;
                carController = binding.CarController;
            }
            else
            {
                targetRb = null;
                carController = null;
            }
        }

        /// <summary>
        /// Sinematik kamera modunu açar veya standart oynanış kamerasına geri döndürür.
        /// </summary>
        public void ToggleCinematicMode()
        {
            SetCinematicMode(!isCinematicActive);
        }

        public void SetCinematicMode(bool active)
        {
            isCinematicActive = active;

            if (gameplayCameraFollow != null)
            {
                gameplayCameraFollow.enabled = !active;
                if (!active)
                {
                    gameplayCameraFollow.WarpToTarget();
                }
            }

            if (active)
            {
                ApplyAngle(currentAngle, instantCut: true);
            }

            OnCinematicModeToggled?.Invoke(active);
        }

        /// <summary>
        /// Belirtilen kamera açısına geçiş yapar.
        /// </summary>
        public void SetAngle(CinematicCameraAngle angle, bool instantCut = false)
        {
            if (!isCinematicActive)
            {
                SetCinematicMode(true);
            }

            currentAngle = angle;
            ApplyAngle(angle, instantCut);
            OnAngleChanged?.Invoke(angle);
        }

        /// <summary>
        /// 16:9 (Trailer) ve 9:16 (Reels) video en-boy formatı arasında geçiş yapar.
        /// </summary>
        public void ToggleVideoFormat()
        {
            SetVideoFormat(currentFormat == CinematicVideoFormat.Trailer_16_9
                ? CinematicVideoFormat.Reels_9_16
                : CinematicVideoFormat.Trailer_16_9);
        }

        public void SetVideoFormat(CinematicVideoFormat format)
        {
            currentFormat = format;
            UpdateTargetFov();
            OnFormatChanged?.Invoke(format);
        }

        /// <summary>
        /// Drift, viraj ve kritik anlar için sinematik ağır çekim modunu açar/kapatır.
        /// </summary>
        public void ToggleSlowMotion()
        {
            SetSlowMotion(!isSlowMotionActive);
        }

        public void SetSlowMotion(bool active)
        {
            isSlowMotionActive = active;
            Time.timeScale = active ? slowMotionScale : 1.0f;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
            OnSlowMotionToggled?.Invoke(active);
        }

        private void ApplyAngle(CinematicCameraAngle angle, bool instantCut)
        {
            int index = (int)angle;
            if (index < 0 || index >= presets.Count) return;

            UpdateTargetFov();

            if (angle == CinematicCameraAngle.RoadsideFlyby)
            {
                ResetRoadsideFlybyPosition();
            }

            if (instantCut && targetCamera != null && vehicleTarget != null)
            {
                CameraPresetSettings preset = presets[index];
                Vector3 desiredPos = CalculateDesiredPosition(preset);
                targetCamera.transform.position = desiredPos;

                Vector3 lookTarget = CalculateLookAtTarget(preset);
                targetCamera.transform.LookAt(lookTarget);
                currentFov = targetFov;
                targetCamera.fieldOfView = targetFov;
            }
        }

        private void UpdateTargetFov()
        {
            int index = (int)currentAngle;
            if (index >= 0 && index < presets.Count)
            {
                targetFov = currentFormat == CinematicVideoFormat.Reels_9_16
                    ? presets[index].fov9x16
                    : presets[index].fov16x9;
            }
        }

        private void ResetRoadsideFlybyPosition()
        {
            if (vehicleTarget == null) return;

            Vector3 vehicleVelocity = targetRb != null ? targetRb.linearVelocity : vehicleTarget.forward * 10f;
            float speed = vehicleVelocity.magnitude;
            Vector3 forwardDir = speed > 1f ? vehicleVelocity.normalized : vehicleTarget.forward;
            Vector3 rightDir = Vector3.Cross(Vector3.up, forwardDir).normalized;

            // Aracın 22-30m ilerisinde, yolun 4.5m sağında ve 1.5m yükseklikte bir nokta seç
            float leadDistance = Mathf.Clamp(speed * 1.5f, 18f, 35f);
            roadsideWorldPosition = vehicleTarget.position + (forwardDir * leadDistance) + (rightDir * 4.5f) + Vector3.up * 1.4f;
            isRoadsideInitialized = true;
        }

        private void LateUpdate()
        {
            if (!isCinematicActive) return;

            if (vehicleTarget == null)
            {
                ResolveReferences();
                if (vehicleTarget == null) return;
            }

            if (targetCamera == null) return;

            int index = (int)currentAngle;
            if (index < 0 || index >= presets.Count) return;

            CameraPresetSettings preset = presets[index];
            float dt = Time.unscaledDeltaTime;

            // 1. FOV Yumuşak Geçiş
            currentFov = Mathf.Lerp(currentFov, targetFov, dt * fovTransitionSpeed);
            targetCamera.fieldOfView = currentFov;

            // 2. Pozisyon Güncelleme
            Vector3 desiredPos = CalculateDesiredPosition(preset);
            if (preset.isFixedWorldPosition)
            {
                // Roadside Flyby: Araç kameranın çok uzağına geçerse (> 25m) yeni bir ön pozisyona atla
                Vector3 toVehicle = vehicleTarget.position - roadsideWorldPosition;
                float forwardDot = Vector3.Dot(toVehicle, targetCamera.transform.forward);
                if (toVehicle.sqrMagnitude > 40f * 40f || (forwardDot < -15f && toVehicle.sqrMagnitude > 20f * 20f))
                {
                    ResetRoadsideFlybyPosition();
                    desiredPos = roadsideWorldPosition;
                }
                targetCamera.transform.position = roadsideWorldPosition;
            }
            else
            {
                float smoothTime = Mathf.Max(0.01f, preset.positionSmoothTime);
                targetCamera.transform.position = Vector3.SmoothDamp(
                    targetCamera.transform.position,
                    desiredPos,
                    ref currentVelocity,
                    smoothTime,
                    Mathf.Infinity,
                    dt);
            }

            // 3. Rotasyon / LookAt Güncelleme
            Vector3 lookTarget = CalculateLookAtTarget(preset);
            Vector3 lookDir = lookTarget - targetCamera.transform.position;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
                float rotSpeed = preset.rotationSmoothTime > 0.001f ? (1f / preset.rotationSmoothTime) : 50f;
                targetCamera.transform.rotation = Quaternion.Slerp(
                    targetCamera.transform.rotation,
                    targetRot,
                    dt * rotSpeed);
            }
        }

        private Vector3 CalculateDesiredPosition(CameraPresetSettings preset)
        {
            if (preset.isFixedWorldPosition)
            {
                if (!isRoadsideInitialized) ResetRoadsideFlybyPosition();
                return roadsideWorldPosition;
            }

            Vector3 offset = preset.localOffset;

            // Reels (9:16) formatında aracı dikey kadrajın hafif alt-orta kısmına almak için
            // Y ve Z mesafesinde dinamik ayarlama yap
            if (currentFormat == CinematicVideoFormat.Reels_9_16)
            {
                offset.y += 0.25f;
                if (offset.z < 0) offset.z -= 0.6f; // Biraz daha geniş kadraj
            }

            return vehicleTarget.position + (vehicleTarget.rotation * offset);
        }

        private Vector3 CalculateLookAtTarget(CameraPresetSettings preset)
        {
            Vector3 lookOffset = preset.localLookAtOffset;
            if (currentFormat == CinematicVideoFormat.Reels_9_16)
            {
                lookOffset.y += 0.2f; // Dikey ekranda odağı hafif yukarı taşı
            }
            return vehicleTarget.position + (vehicleTarget.rotation * lookOffset);
        }
    }
}
