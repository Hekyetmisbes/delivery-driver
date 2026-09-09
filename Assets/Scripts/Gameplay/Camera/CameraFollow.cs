using UnityEngine;
using DeliveryDriver.Vehicle;
using Unity.Cinemachine;

/// <summary>
/// Araç takip kamerası (Dedicated 3rd-person Vehicle Chase Camera).
/// Cinemachine gecikmeleri ve açı bozulmalarını önlemek için LateUpdate içinde
/// doğrudan transform kontrolü yapar.
/// 
/// Güvenlik Önlemleri:
/// 1. Geri giderken kamera açısı (yaw) kilitlenir; araç arkasında kalır ve dönmez ("geri giderken hareket etmesin kamera").
/// 2. Geri giderken mesafeyi anında korur; araç kameranın içine/tavanına giremez.
/// 3. Yatay min mesafe (5.2m) ve min yükseklik (2.0m) sert sınırlarla (clamp) korunur.
/// 4. Pitch açısı (3° - 24°) sınırlandırılarak kameranın tavana dik bakması kesinlikle engellenir.
/// 5. CinemachineBrain ve CinemachineCamera runtime'da devre dışı bırakılır.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("--- HEDEF AYARLARI ---")]
    [Tooltip("Takip edilecek araç")]
    public Transform target;

    [Header("--- KONUM VE MESAFE ---")]
    [Tooltip("Kameranın araca göre varsayılan konumu (X=0, Y=yükseklik, Z=negatif mesafe)")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 3.4f, -7.0f);
    [Tooltip("Kameranın odaklanacağı hedef yükseklik (aracın merkezinden yukarı)")]
    [SerializeField] private float lookAtHeight = 1.3f;
    [Tooltip("Kameranın araca yatayda yaklaşabileceği MUTLAK minimum mesafe (tavana yapışmayı önler)")]
    [SerializeField] private float minHorizontalDistance = 5.2f;
    [Tooltip("Kameranın araçtan yüksekliğinin MUTLAK minimum sınırı")]
    [SerializeField] private float minHeightAboveTarget = 2.0f;

    [Header("--- AÇI VE PITCH SINIRLARI ---")]
    [Tooltip("Minimum aşağı eğim açısı (derece).")]
    [SerializeField] private float minPitchAngle = 3f;
    [Tooltip("Maksimum aşağı eğim açısı (derece). Kameranın tavana dik bakmasını önlemek için sınırlıdır.")]
    [SerializeField] private float maxPitchAngle = 24f;

    [Header("--- TAKİP YUMUŞAKLIĞI ---")]
    [Tooltip("Kamera yatay dönüş (yaw) yumuşaklık süresi")]
    [SerializeField] private float yawSmoothTime = 0.16f;
    [Tooltip("Kamera pozisyon takip yumuşaklığı")]
    [SerializeField] private float translateSmoothTime = 0.14f;
    [Tooltip("Yüksek hızda pozisyon takip yumuşaklığı")]
    [SerializeField] private float minTranslateSmoothTime = 0.06f;

    [Header("--- DİNAMİK HIZ MESAFESİ ---")]
    [Tooltip("Yüksek hızda kameranın hafifçe geriye çekilme miktarı (m)")]
    [SerializeField] private float forwardZoomOutExtra = 0.8f;
    [Tooltip("Maksimum geri çekilmenin sağlandığı hız (km/h)")]
    [SerializeField] private float forwardZoomOutSpeedKmh = 140f;

    [Header("--- DİNAMİK FOV ---")]
    [Tooltip("Araç hızına göre FOV değişimi")]
    [SerializeField] private bool enableSpeedFov = true;
    [SerializeField] private float baseFov = 60f;
    [SerializeField] private float maxSpeedFov = 68f;
    [SerializeField] private float fovMaxSpeedKmh = 150f;
    [SerializeField] private float fovLerpSpeed = 4f;

    [Header("--- GERİ GÖRÜŞ KAMERASI (HUD) ---")]
    [SerializeField] private bool enableReverseCamera = true;
    [SerializeField] private Vector3 reverseCamOffset = new Vector3(0f, 0.8f, -1.4f);
    [SerializeField] private Vector3 reverseCamEuler = new Vector3(8f, 180f, 0f);
    [SerializeField] private float reverseCamFov = 72f;
    [SerializeField] private float reverseCamVpX = 0.32f;
    [SerializeField] private float reverseCamVpY = 0.79f;
    [SerializeField] private float reverseCamVpW = 0.36f;
    [SerializeField] private float reverseCamVpH = 0.18f;
    [SerializeField] private float reverseCamStationaryThreshold = 1.0f;

    [Header("--- GERİ GÖRÜŞ KAMERASI STİLİ ---")]
    [SerializeField] private Color reverseCamBorderColor = new Color(0.1f, 0.9f, 1f, 0.85f);
    [SerializeField] private float reverseCamBorderWidth = 2.5f;
    [SerializeField] private float reverseCamFramePadding = 6f;
    [SerializeField] private float reverseCamFadeSpeed = 6f;
    [SerializeField] private float reverseCamGradientHeight = 28f;

    // Runtime state
    public static bool IsCinematicOverridden { get; set; }
    private Camera mainCamera;
    private Rigidbody targetRb;
    private CarController carController;
    private float currentYaw;
    private float yawVelocity;
    private Vector3 positionVelocity;
    private float currentFov;
    private CameraFollowExternalControllerCoordinator externalControllerCoordinator;
    private bool isInitialized;

    private void Awake()
    {
        InitializeCamera();
    }

    private void Start()
    {
        if (target != null)
        {
            SetTarget(target);
        }
        else
        {
            TryResolveTarget();
        }

        WarpToTarget();
    }

    private void InitializeCamera()
    {
        if (isInitialized) return;

        mainCamera = GetComponent<Camera>();
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        currentFov = baseFov;
        if (mainCamera != null)
        {
            mainCamera.fieldOfView = baseFov;
        }

        // Cinemachine çakışmalarını devre dışı bırak:
        // Kamera kontrolü doğrudan bu scriptin LateUpdate metodunda gerçekleşir.
        var brain = GetComponent<CinemachineBrain>();
        if (brain != null)
        {
            brain.WorldUpOverride = null;
            brain.enabled = false;
        }

        var cmCameras = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        for (int i = 0; i < cmCameras.Length; i++)
        {
            if (cmCameras[i] != null)
            {
                cmCameras[i].enabled = false;
            }
        }

        externalControllerCoordinator = new CameraFollowExternalControllerCoordinator();
        EnsureExternalControllers();

        isInitialized = true;
    }

    public void SetTarget(Transform newTarget)
    {
        InitializeCamera();

        VehicleCameraBinding binding = VehicleCameraTargetResolver.Resolve(newTarget);
        target = binding.Target;
        targetRb = binding.Rigidbody;
        carController = binding.CarController;

        if (targetRb != null)
        {
            targetRb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        if (target != null)
        {
            currentYaw = target.eulerAngles.y;
            yawVelocity = 0f;
            positionVelocity = Vector3.zero;
        }

        EnsureExternalControllers();
        if (externalControllerCoordinator != null)
        {
            if (target != null)
                externalControllerCoordinator.ApplyTarget(mainCamera, target);
            else
                externalControllerCoordinator.ClearTargets();
        }
    }

    /// <summary>
    /// Kamerayı anında hedefin arkasındaki ideal konuma yerleştirir (spawn/teleport durumları).
    /// </summary>
    public void WarpToTarget()
    {
        if (target == null) return;

        currentYaw = target.eulerAngles.y;
        yawVelocity = 0f;
        positionVelocity = Vector3.zero;

        Quaternion yawRot = Quaternion.Euler(0f, currentYaw, 0f);
        Vector3 targetPos = target.position;
        Vector3 desiredPos = targetPos + (yawRot * offset);

        transform.position = desiredPos;

        Vector3 lookTarget = targetPos + Vector3.up * lookAtHeight;
        Vector3 lookDir = lookTarget - transform.position;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
            float pitch = targetRot.eulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
            pitch = Mathf.Clamp(pitch, minPitchAngle, maxPitchAngle);
            transform.rotation = Quaternion.Euler(pitch, targetRot.eulerAngles.y, 0f);
        }
    }

    private void TryResolveTarget()
    {
        Transform resolved = VehicleCameraTargetResolver.ResolveDefaultTarget();
        if (resolved != null && resolved != target)
        {
            SetTarget(resolved);
        }
    }

    private void LateUpdate()
    {
        if (IsCinematicOverridden) return;

        if (target == null)
        {
            TryResolveTarget();
            if (target == null) return;
        }

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0.00001f) return;

        Vector3 targetPos = target.position;

        // Araç hız ve yön verileri
        Vector3 velocity = targetRb != null ? targetRb.linearVelocity : Vector3.zero;
        float speedKmh = velocity.magnitude * 3.6f;
        float localVelZ = target.InverseTransformDirection(velocity).z;

        bool reverseInput = carController != null && carController.IsReverseInputActive;
        bool isMovingReverse = localVelZ < -0.25f;
        bool isReversing = reverseInput || isMovingReverse;

        // 1. YAW (Yatay Dönüş Açısı)
        // Kural: "Geri giderken hareket etmesin kamera"
        // Geri giderken kameranın araç önüne dönmesini veya saçmalamasını önlemek için yaw açısı kilitlenir.
        if (!isReversing)
        {
            float targetYaw = target.eulerAngles.y;
            currentYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref yawVelocity, yawSmoothTime, Mathf.Infinity, deltaTime);
        }
        else
        {
            yawVelocity = 0f; // Geri viteste yaw sabit kalır
        }

        // 2. MESAFE VE HIZ ZOOM
        float forwardSpeedKmh = Mathf.Max(0f, localVelZ * 3.6f);
        float zoomOut = isReversing ? 0f : (Mathf.Clamp01(forwardSpeedKmh / Mathf.Max(1f, forwardZoomOutSpeedKmh)) * forwardZoomOutExtra);
        float targetDistance = Mathf.Abs(offset.z) + zoomOut;
        float targetHeight = offset.y;

        Quaternion yawRotation = Quaternion.Euler(0f, currentYaw, 0f);
        Vector3 desiredPosition = targetPos + (yawRotation * new Vector3(offset.x, targetHeight, -targetDistance));

        // 3. POZİSYON TAKİBİ
        if (isReversing)
        {
            // Geri giderken araba kameraya çarpmasın diye kamera araçla tam senkronize geri çekilir
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref positionVelocity, 0.02f, Mathf.Infinity, deltaTime);
        }
        else
        {
            float smoothT = Mathf.InverseLerp(40f, 130f, speedKmh);
            float currentSmoothTime = Mathf.Lerp(translateSmoothTime, minTranslateSmoothTime, smoothT);
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref positionVelocity, currentSmoothTime, Mathf.Infinity, deltaTime);
        }

        // 4. KESİN GÜVENLİK SINIRLARI (TAVANA YAPIŞMAYI VE İÇİNE GİRMEYİ MATEMATİKSEL OLARAK ENGELLER)
        // A) Minimum Yükseklik Sınırı:
        if (transform.position.y < targetPos.y + minHeightAboveTarget)
        {
            Vector3 p = transform.position;
            p.y = targetPos.y + minHeightAboveTarget;
            transform.position = p;
        }

        // B) Minimum Yatay Mesafe Sınırı:
        Vector3 diff = transform.position - targetPos;
        Vector2 horizDiff = new Vector2(diff.x, diff.z);
        if (horizDiff.magnitude < minHorizontalDistance)
        {
            Vector3 backDir3D = -(yawRotation * Vector3.forward);
            Vector2 horizDir = horizDiff.sqrMagnitude > 0.001f
                ? horizDiff.normalized
                : new Vector2(backDir3D.x, backDir3D.z).normalized;

            Vector3 p = transform.position;
            p.x = Mathf.MoveTowards(p.x, targetPos.x + horizDir.x * minHorizontalDistance, deltaTime * 20f);
            p.z = Mathf.MoveTowards(p.z, targetPos.z + horizDir.y * minHorizontalDistance, deltaTime * 20f);
            transform.position = p;
        }

        // 5. ROTASYON (LOOK AT & PITCH CLAMP)
        // Kameranın arabanın tavanına dik bakması (yere bakması) maxPitchAngle ile engellenir!
        Vector3 lookTarget = targetPos + Vector3.up * lookAtHeight;
        Vector3 lookDir = lookTarget - transform.position;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
            float pitch = targetRot.eulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
            pitch = Mathf.Clamp(pitch, minPitchAngle, maxPitchAngle);
            Quaternion desiredRot = Quaternion.Euler(pitch, targetRot.eulerAngles.y, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, deltaTime * 25f);
        }

        // 6. SPEED FOV
        if (enableSpeedFov && mainCamera != null)
        {
            float targetFov = baseFov;
            if (speedKmh > 10f)
            {
                float t = Mathf.Clamp01(speedKmh / fovMaxSpeedKmh);
                targetFov = Mathf.Lerp(baseFov, maxSpeedFov, t);
            }
            currentFov = Mathf.Lerp(currentFov, targetFov, fovLerpSpeed * deltaTime);
            mainCamera.fieldOfView = currentFov;
        }
    }

    private void EnsureExternalControllers()
    {
        if (externalControllerCoordinator == null)
        {
            externalControllerCoordinator = new CameraFollowExternalControllerCoordinator();
        }

        externalControllerCoordinator.EnsureControllers(
            mainCamera,
            gameObject,
            target,
            new CameraFollowExternalControllerSettings(
                enableReverseCamera,
                reverseCamOffset,
                reverseCamEuler,
                reverseCamFov,
                new Rect(reverseCamVpX, reverseCamVpY, reverseCamVpW, reverseCamVpH),
                reverseCamStationaryThreshold,
                reverseCamBorderColor,
                reverseCamBorderWidth,
                reverseCamFramePadding,
                reverseCamFadeSpeed,
                reverseCamGradientHeight));
    }
}
