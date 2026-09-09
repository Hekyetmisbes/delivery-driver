using System;
using System.Collections.Generic;
using DeliveryDriver.Vehicle;
using UnityEngine;

namespace DeliveryDriver.Cinematic
{
    /// <summary>
    /// Hem 16:9 Trailer hem de 9:16 Instagram Reels formatları için
    /// profesyonel sinematik kamera açılarını ve geçişlerini yöneten kontrolcü.
    /// LateUpdate içinde en son çalışarak kamera transformunu tam yetkiyle yönetir.
    /// </summary>
    [DefaultExecutionOrder(100)]
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
        private float chaseYaw;
        private float chaseYawVelocity;
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
            presets = new List<CameraPresetSettings>();
            for (int i = 0; i <= (int)CinematicCameraAngle.RoadsideFlyby; i++)
            {
                presets.Add(CameraPresetSettings.CreateDefault((CinematicCameraAngle)i));
            }
        }

        public void ResolveReferences()
        {
            if (gameplayCameraFollow == null)
            {
                gameplayCameraFollow = FindFirstObjectByType<CameraFollow>();
            }

            if (targetCamera == null)
            {
                if (gameplayCameraFollow != null)
                {
                    targetCamera = gameplayCameraFollow.GetComponent<Camera>();
                }

                if (targetCamera == null)
                {
                    targetCamera = Camera.main ?? FindFirstObjectByType<Camera>();
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
            else if (gameplayCameraFollow != null && gameplayCameraFollow.target != null && vehicleTarget != gameplayCameraFollow.target)
            {
                SetVehicleTarget(gameplayCameraFollow.target);
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
                chaseYaw = vehicleTarget.eulerAngles.y;
                chaseYawVelocity = 0f;

                // Titreme önleme: Aracın Rigidbody'sinde fizik interpolasyonunu aç.
                // Rigidbody.interpolation = Interpolate olmadan LateUpdate her render karesinde (60-144 FPS)
                // fizik adımları (50Hz) arasında araç transformu durağan kalır ve kamera mikro-titreme yapar!
                if (targetRb != null)
                {
                    targetRb.interpolation = RigidbodyInterpolation.Interpolate;
                }
            }
            else
            {
                targetRb = null;
                carController = null;
            }
        }

        private List<Canvas> suppressedCanvases = new List<Canvas>();

        /// <summary>
        /// Sinematik modda veya video kaydı esnasında oyun içi HUD, hız göstergesi,
        /// geri görüş kamerası ve diğer Canvas öğelerini gizler / geri getirir.
        /// </summary>
        public void SuppressGameplayUI(bool suppress)
        {
            // 1. Geri Görüş Kamerasını tamamen devre dışı bırak / aç
            ReverseCameraHUD.IsSuppressed = suppress;
            if (suppress && ReverseCameraHUD.Instance != null && ReverseCameraHUD.Instance.ReverseCamera != null)
            {
                ReverseCameraHUD.Instance.ReverseCamera.enabled = false;
            }

            // 2. Sahnedeki UGUI Canvas bileşenlerini (Hız göstergesi, minimap, görevler, telefon vb.) gizle/göster
            if (suppress)
            {
                suppressedCanvases.Clear();
                Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                for (int i = 0; i < allCanvases.Length; i++)
                {
                    Canvas c = allCanvases[i];
                    if (c != null && c.enabled && c.gameObject.activeInHierarchy)
                    {
                        string cName = c.gameObject.name;
                        if (cName.IndexOf("Pause", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            cName.IndexOf("Confirmation", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            continue;
                        }

                        c.enabled = false;
                        suppressedCanvases.Add(c);
                    }
                }
            }
            else
            {
                for (int i = 0; i < suppressedCanvases.Count; i++)
                {
                    if (suppressedCanvases[i] != null)
                    {
                        suppressedCanvases[i].enabled = true;
                    }
                }
                suppressedCanvases.Clear();
            }
        }

        private void OnDisable()
        {
            if (isCinematicActive)
            {
                SuppressGameplayUI(false);
            }
        }

        private void OnDestroy()
        {
            if (isCinematicActive)
            {
                SuppressGameplayUI(false);
            }
            if (Instance == this)
            {
                Instance = null;
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
            CameraFollow.IsCinematicOverridden = active;

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
                if (targetRb != null)
                {
                    targetRb.interpolation = RigidbodyInterpolation.Interpolate;
                }
                currentVelocity = Vector3.zero;
                ApplyAngle(currentAngle, instantCut: true);
            }

            SuppressGameplayUI(active);
            OnCinematicModeToggled?.Invoke(active);
        }

        /// <summary>
        /// Belirtilen kamera açısına geçiş yapar.
        /// </summary>
        public void SetAngle(CinematicCameraAngle angle, bool instantCut = false)
        {
            ResolveReferences();

            if (!isCinematicActive)
            {
                SetCinematicMode(true);
            }

            currentAngle = angle;
            currentVelocity = Vector3.zero;
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
                if (preset.isChassisMounted)
                {
                    Vector3 offset = preset.localOffset;
                    if (currentFormat == CinematicVideoFormat.Reels_9_16)
                    {
                        offset.y += 0.15f;
                    }
                    targetCamera.transform.position = vehicleTarget.TransformPoint(offset);
                    targetCamera.transform.rotation = vehicleTarget.rotation * Quaternion.Euler(preset.localRotationEuler);
                }
                else if (angle == CinematicCameraAngle.GameplayChase)
                {
                    Vector3 offset = preset.localOffset;
                    if (currentFormat == CinematicVideoFormat.Reels_9_16)
                    {
                        offset.y += 0.35f;
                        offset.z -= 0.6f;
                    }
                    chaseYaw = vehicleTarget.eulerAngles.y;
                    chaseYawVelocity = 0f;
                    Vector3 desiredPos = vehicleTarget.position + (Quaternion.Euler(0f, chaseYaw, 0f) * offset);
                    targetCamera.transform.position = desiredPos;

                    Vector3 lookTarget = vehicleTarget.position + Vector3.up * (currentFormat == CinematicVideoFormat.Reels_9_16 ? 1.4f : 1.1f);
                    targetCamera.transform.rotation = Quaternion.LookRotation(lookTarget - desiredPos, Vector3.up);
                }
                else
                {
                    Vector3 desiredPos = CalculateDesiredPosition(preset);
                    targetCamera.transform.position = desiredPos;

                    Vector3 lookTarget = CalculateLookAtTarget(preset);
                    Vector3 lookDir = lookTarget - desiredPos;
                    if (lookDir.sqrMagnitude > 0.001f)
                    {
                        targetCamera.transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
                    }
                }

                currentFov = targetFov;
                targetCamera.fieldOfView = targetFov;
                currentVelocity = Vector3.zero;
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

            // Aracın 20-32m ilerisinde, yolun 4.5m sağında ve 1.4m yükseklikte bir nokta seç
            float leadDistance = Mathf.Clamp(speed * 1.5f, 18f, 32f);
            roadsideWorldPosition = vehicleTarget.position + (forwardDir * leadDistance) + (rightDir * 4.2f) + Vector3.up * 1.3f;
            isRoadsideInitialized = true;
        }

        private void LateUpdate()
        {
            if (!isCinematicActive) return;

            // CameraFollow'un kesin olarak devre dışı kaldığından emin ol
            CameraFollow.IsCinematicOverridden = true;
            if (gameplayCameraFollow != null && gameplayCameraFollow.enabled)
            {
                gameplayCameraFollow.enabled = false;
            }

            // Geri görüş kamerasını sinematik mod boyunca kesin olarak bastır
            ReverseCameraHUD.IsSuppressed = true;

            if (vehicleTarget == null || targetCamera == null)
            {
                ResolveReferences();
                if (vehicleTarget == null || targetCamera == null) return;
            }

            int index = (int)currentAngle;
            if (index < 0 || index >= presets.Count) return;

            CameraPresetSettings preset = presets[index];
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

            // 1. FOV Yumuşak Geçiş
            currentFov = Mathf.Lerp(currentFov, targetFov, dt * fovTransitionSpeed);
            targetCamera.fieldOfView = currentFov;

            // 2. Pozisyon ve Rotasyon Güncelleme
            if (preset.isChassisMounted)
            {
                // ARACA KİLİTLİ (GoPro / Onboard / Tampon / Egzoz / Tekerlek)
                // Kesinlikle SmoothDamp ve gecikme uygulanmaz; aracın gövdesine milimetrik kilitlenir!
                Vector3 offset = preset.localOffset;
                if (currentFormat == CinematicVideoFormat.Reels_9_16)
                {
                    offset.y += 0.15f;
                }

                targetCamera.transform.position = vehicleTarget.TransformPoint(offset);
                targetCamera.transform.rotation = vehicleTarget.rotation * Quaternion.Euler(preset.localRotationEuler);
                currentVelocity = Vector3.zero;
            }
            else if (preset.isFixedWorldPosition)
            {
                // YOL KENARI SABİT GEÇİŞ KAMERASI (Roadside Flyby)
                Vector3 toVehicle = vehicleTarget.position - roadsideWorldPosition;
                float forwardDot = Vector3.Dot(toVehicle, targetCamera.transform.forward);
                if (toVehicle.sqrMagnitude > 40f * 40f || (forwardDot < -10f && toVehicle.sqrMagnitude > 15f * 15f))
                {
                    ResetRoadsideFlybyPosition();
                }

                targetCamera.transform.position = roadsideWorldPosition;
                currentVelocity = Vector3.zero;

                Vector3 lookTarget = vehicleTarget.position + (vehicleTarget.rotation * preset.localLookAtOffset);
                Vector3 lookDir = lookTarget - targetCamera.transform.position;
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    targetCamera.transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
                }
            }
            else if (currentAngle == CinematicCameraAngle.GameplayChase)
            {
                // SİNEMATİK TAKİP KAMERASI (GameplayChase)
                // Süspansiyon sarsıntılarını (pitch) ve viraj yatmasını (roll) tamamen izole et!
                // Kamera sadece yatay yönelimi (Yaw) ve araç pozisyonunu pürüzsüzce takip eder.
                float targetYaw = vehicleTarget.eulerAngles.y;
                chaseYaw = Mathf.SmoothDampAngle(chaseYaw, targetYaw, ref chaseYawVelocity, 0.08f, Mathf.Infinity, dt);

                Vector3 offset = preset.localOffset;
                if (currentFormat == CinematicVideoFormat.Reels_9_16)
                {
                    offset.y += 0.35f;
                    offset.z -= 0.6f;
                }

                Quaternion yawRot = Quaternion.Euler(0f, chaseYaw, 0f);
                Vector3 desiredPos = vehicleTarget.position + (yawRot * offset);

                float smoothTime = Mathf.Max(0.04f, preset.positionSmoothTime);
                targetCamera.transform.position = Vector3.SmoothDamp(
                    targetCamera.transform.position,
                    desiredPos,
                    ref currentVelocity,
                    smoothTime,
                    Mathf.Infinity,
                    dt);

                // LookAt hedefi: Aracın merkezinin 1.1m yukarısı (süspansiyon pitch'inden etkilenmez!)
                Vector3 lookTarget = vehicleTarget.position + Vector3.up * 1.1f;
                if (currentFormat == CinematicVideoFormat.Reels_9_16)
                {
                    lookTarget += Vector3.up * 0.30f;
                }

                Vector3 lookDir = lookTarget - targetCamera.transform.position;
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
                    targetCamera.transform.rotation = Quaternion.Slerp(
                        targetCamera.transform.rotation,
                        targetRot,
                        dt * 20f);
                }
            }
            else
            {
                // SERBEST / HAVADAN TAKİP (HelicopterDrone vb.)
                Vector3 desiredPos = CalculateDesiredPosition(preset);
                float smoothTime = Mathf.Max(0.02f, preset.positionSmoothTime);

                targetCamera.transform.position = Vector3.SmoothDamp(
                    targetCamera.transform.position,
                    desiredPos,
                    ref currentVelocity,
                    smoothTime,
                    Mathf.Infinity,
                    dt);

                if (preset.lookAtTarget)
                {
                    Vector3 lookTarget = CalculateLookAtTarget(preset);
                    Vector3 lookDir = lookTarget - targetCamera.transform.position;
                    if (lookDir.sqrMagnitude > 0.001f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
                        float rotSpeed = preset.rotationSmoothTime > 0.001f ? (1f / preset.rotationSmoothTime) : 25f;
                        targetCamera.transform.rotation = Quaternion.Slerp(
                            targetCamera.transform.rotation,
                            targetRot,
                            dt * rotSpeed);
                    }
                }
                else
                {
                    targetCamera.transform.rotation = Quaternion.Slerp(
                        targetCamera.transform.rotation,
                        vehicleTarget.rotation * Quaternion.Euler(preset.localRotationEuler),
                        dt * 15f);
                }
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

            // Polis Helikopteri: Aracın ani savrulma ve drift açılarıyla sarsılmadan, hareket yönünü gökyüzünden takip eder
            if (currentAngle == CinematicCameraAngle.HelicopterDrone)
            {
                Vector3 vel = targetRb != null ? targetRb.linearVelocity : vehicleTarget.forward;
                Vector3 forwardDir = vel.magnitude > 2f ? vel.normalized : vehicleTarget.forward;
                Vector3 rightDir = Vector3.Cross(Vector3.up, forwardDir).normalized;

                Vector3 heliOffset = (Vector3.up * offset.y) + (rightDir * offset.x) + (forwardDir * offset.z);
                return vehicleTarget.position + heliOffset;
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
