using UnityEngine;
using DeliveryDriver.Vehicle;
using Unity.Cinemachine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("Takip edilecek arac")]
    public Transform target;

    [Header("Offset Settings")]
    [Tooltip("Kameranin araca gore konumu (X, Y, Z). Z negatif olmali ki arkada dursun.")]
    [SerializeField] private Vector3 offset = new Vector3(0, 4f, -8f);
    [SerializeField] private Vector3 lookAtTargetOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Smooth Settings")]
    [Tooltip("Kamera takip yumusakligi (Dusuk = daha siki, Yuksek = daha gevek)")]
    [SerializeField] private float translateSmoothTime = 0.2f;
    [Tooltip("Yuksek hizda takip gecikmesini azaltmak icin minimum smooth time")]
    [SerializeField] private float minTranslateSmoothTime = 0.08f;
    [Tooltip("Bu hizdan sonra kamera takipte sikilasir (km/h)")]
    [SerializeField] private float tightenFollowStartSpeedKmh = 70f;
    [Tooltip("Bu hizda minimum smooth time degerine ulasir (km/h)")]
    [SerializeField] private float tightenFollowFullSpeedKmh = 170f;
    [Tooltip("Donus yumusakligi (Dusuk = daha gecikmeli, Yuksek = daha hizli)")]
    [SerializeField] private float rotationSmoothSpeed = 1.5f;
    [Tooltip("Kamera rotasyonu tamamen arabayi takip etsin mi?")]
    [SerializeField] private bool followRotation = false;

    [Header("Dinamik Mesafe")]
    [Tooltip("İleri giderken kameranın geri çekildiği maksimum ekstra mesafe (m)")]
    [SerializeField] private float forwardZoomOutExtra = 2.5f;
    [Tooltip("Bu hızda (km/h) maksimum geri çekilme sağlanır")]
    [SerializeField] private float forwardZoomOutSpeedKmh = 80f;
    [Tooltip("Mesafe değişim yumuşaklığı (düşük = daha yavaş uzaklaşma)")]
    [SerializeField] private float dynamicZoomLerpSpeed = 1.2f;

    [Header("Speed Feel")]
    [Tooltip("Arac hizina gore kamera FOV degissin mi")]
    [SerializeField] private bool enableSpeedFov = true;
    [SerializeField] private float baseFov = 60f;
    [SerializeField] private float maxSpeedFov = 78f;
    [SerializeField] private float fovMaxSpeedKmh = 190f;
    [SerializeField] private float fovLerpSpeed = 6f;

    [Header("Geri Görüş Kamerası (HUD)")]
    [Tooltip("Araç geri giderken ekranın üstünde geri görüş kamerası göster")]
    [SerializeField] private bool enableReverseCamera = true;
    [Tooltip("Araç arkasındaki kamera konumu (lokal). Z negatif = aracın arkası.")]
    [SerializeField] private Vector3 reverseCamOffset = new Vector3(0f, 1.1f, -2.0f);
    [Tooltip("Kamera açısı. Y=180 geriye bakar, X pozitif = aşağı eğimli.")]
    [SerializeField] private Vector3 reverseCamEuler = new Vector3(12f, 180f, 0f);
    [SerializeField] private float reverseCamFov = 95f;
    [Tooltip("Viewport sol kenarı (0-1)")]
    [SerializeField] private float reverseCamVpX = 0.2f;
    [Tooltip("Viewport alt kenarı (0=alt, 1=üst). 0.74 → ekranın üst kısmı.")]
    [SerializeField] private float reverseCamVpY = 0.74f;
    [SerializeField] private float reverseCamVpW = 0.60f;
    [SerializeField] private float reverseCamVpH = 0.24f;
    [Tooltip("Bu hızın (m/s) altında araç durağan sayılır; geri tuşu kamerayı açar.")]
    [SerializeField] private float reverseCamStationaryThreshold = 1.0f;

    [Header("Geri Görüş Kamerası Stili")]
    [Tooltip("Çerçeve rengi")]
    [SerializeField] private Color reverseCamBorderColor = new Color(0.1f, 0.9f, 1f, 0.85f);
    [Tooltip("Çerçeve kalınlığı (piksel)")]
    [SerializeField] private float reverseCamBorderWidth = 2.5f;
    [Tooltip("Arka plan karartma çerçevesi kalınlığı (piksel)")]
    [SerializeField] private float reverseCamFramePadding = 6f;
    [Tooltip("Fade animasyon hızı")]
    [SerializeField] private float reverseCamFadeSpeed = 6f;
    [Tooltip("Üst gradient yüksekliği (piksel)")]
    [SerializeField] private float reverseCamGradientHeight = 28f;

    // Runtime variables
    private Vector3 currentVelocity;
    private Rigidbody targetRb;
    private CarController carController;
    private Camera mainCamera;
    private Camera reverseCamHUD;
    private bool reverseCamShowing;
    private float reverseCamFadeAlpha;
    private Texture2D reverseCamWhiteTex;
    private GUIStyle reverseCamLabelStyle;
    private float currentZoomOffset;
    private VehicleCameraAnchors vehicleCameraAnchors;
    private CinemachineBrain cinemachineBrain;
    private CameraFollowGameplayRig gameplayRig;
    private CameraFollowExternalControllerCoordinator externalControllerCoordinator;

    void Awake()
    {
        mainCamera = GetComponent<Camera>();
        cinemachineBrain = GetComponent<CinemachineBrain>();
        gameplayRig = new CameraFollowGameplayRig(mainCamera, baseFov);
        externalControllerCoordinator = new CameraFollowExternalControllerCoordinator();

        gameplayRig.ResolveRig();
        EnsureExternalCameraControllers();
    }

    void Start()
    {
        if (target != null)
        {
            SetTarget(target);
            return;
        }

        TryResolveTarget();

        if (target == null)
        {
            Debug.LogWarning("[CameraFollow] Target (Arac) bulunamadi!");
        }
    }

    public void SetTarget(Transform newTarget)
    {
        VehicleCameraBinding binding = VehicleCameraTargetResolver.Resolve(newTarget);
        target = binding.Target;
        targetRb = binding.Rigidbody;
        carController = binding.CarController;
        vehicleCameraAnchors = binding.CameraAnchors;

        gameplayRig.ResolveRig();
        EnsureExternalCameraControllers();

        if (target == null)
        {
            gameplayRig.ClearTarget(cinemachineBrain);
            externalControllerCoordinator.ClearTargets();
            return;
        }

        gameplayRig.ResetRuntimeFov(gameplayRig.ResolveCurrentLensFov(baseFov));

        BindGameplayRig();
        externalControllerCoordinator.ApplyTarget(mainCamera, target);
        gameplayRig.Update(target, targetRb, carController, BuildGameplaySettings(), 0f);

        Debug.Log($"[CameraFollow] Target updated: target={target.name}, targetRb={targetRb != null}, carController={carController != null}, cinemachine={gameplayRig.HasGameplayCamera}");
    }

    void LateUpdate()
    {
        if (target == null)
        {
            TryResolveTarget();
            return;
        }

        gameplayRig.Update(target, targetRb, carController, BuildGameplaySettings(), Time.deltaTime);
    }

    void TryResolveTarget()
    {
        Transform resolvedTarget = VehicleCameraTargetResolver.ResolveDefaultTarget();
        if (resolvedTarget != null && resolvedTarget != target)
        {
            SetTarget(resolvedTarget);
        }
    }

    void EnsureExternalCameraControllers()
    {
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

    void BindGameplayRig()
    {
        gameplayRig.BindTarget(target, cinemachineBrain);
    }

    CameraFollowGameplayRigSettings BuildGameplaySettings()
    {
        return new CameraFollowGameplayRigSettings(
            offset,
            lookAtTargetOffset,
            enableSpeedFov,
            baseFov,
            maxSpeedFov,
            fovMaxSpeedKmh,
            fovLerpSpeed,
            forwardZoomOutExtra,
            forwardZoomOutSpeedKmh,
            dynamicZoomLerpSpeed);
    }

    void HandleCameraMovement(float deltaTime)
    {
        // --- Geri/fren tespiti ve dinamik Z offset ---
        bool reverseInput = carController != null && carController.IsReverseInputActive;
        bool isReversing = false;
        if (targetRb != null)
        {
            float localVelZ = target.InverseTransformDirection(targetRb.linearVelocity).z;
            // Geri tuşuna basılıyorsa VEYA araç gerçekten geri gidiyorsa → kamera sabit
            isReversing = reverseInput || localVelZ < -0.3f;

            if (isReversing)
            {
                currentVelocity = Vector3.zero;
                currentZoomOffset = Mathf.Lerp(currentZoomOffset, 0f, 10f * deltaTime);
            }
            else
            {
                // İleri giderken: hıza göre kamerayı yavaşça geri çek
                float forwardKmh = Mathf.Max(0f, localVelZ * 3.6f);
                float zoomT = Mathf.Clamp01(forwardKmh / Mathf.Max(1f, forwardZoomOutSpeedKmh));
                float targetZoom = -zoomT * forwardZoomOutExtra;
                currentZoomOffset = Mathf.Lerp(currentZoomOffset, targetZoom, dynamicZoomLerpSpeed * deltaTime);
            }
        }

        Vector3 targetOffset = new Vector3(offset.x, offset.y, offset.z + currentZoomOffset);

        // --- Pozisyon hesaplama ---
        Vector3 desiredPosition;
        if (followRotation)
        {
            desiredPosition = target.TransformPoint(targetOffset);
        }
        else
        {
            Quaternion yawRotation = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
            desiredPosition = target.position + (yawRotation * targetOffset);
        }

        // --- Pozisyonu uygula ---
        if (isReversing)
        {
            // Geri/fren: SmoothDamp ataleti sıfırla, pozisyona anında snap yap
            currentVelocity = Vector3.zero;
            transform.position = desiredPosition;
        }
        else
        {
            float smoothTime = translateSmoothTime;
            if (targetRb != null)
            {
                float speedKmh = targetRb.linearVelocity.magnitude * 3.6f;
                float tightenT = Mathf.InverseLerp(tightenFollowStartSpeedKmh, tightenFollowFullSpeedKmh, speedKmh);
                smoothTime = Mathf.Lerp(translateSmoothTime, minTranslateSmoothTime, tightenT);
            }

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref currentVelocity,
                Mathf.Max(0.01f, smoothTime),
                Mathf.Infinity,
                Mathf.Max(0.0001f, deltaTime));
        }

        // 6. Rotasyon ayari — her zaman arabanin biraz ustune bak
        Vector3 lookAtTarget = target.position + Vector3.up * 1.5f;
        Vector3 direction = lookAtTarget - transform.position;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);

            float currentRotSpeed = rotationSmoothSpeed;

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, currentRotSpeed * deltaTime);
        }
    }
    void OnGUI()
    {
        if (!enableReverseCamera || reverseCamFadeAlpha < 0.01f) return;

        EnsureReverseCamGUIResources();

        // Viewport → ekran pikseli
        float sw = Screen.width;
        float sh = Screen.height;
        float rx = reverseCamVpX * sw;
        float ry = (1f - reverseCamVpY - reverseCamVpH) * sh; // GUI: sol üst orijin
        float rw = reverseCamVpW * sw;
        float rh = reverseCamVpH * sh;

        Color prevColor = GUI.color;
        float a = reverseCamFadeAlpha;

        // 1) Dış karartma çerçevesi (padding)
        float pad = reverseCamFramePadding;
        GUI.color = new Color(0f, 0f, 0f, 0.7f * a);
        // Üst
        GUI.DrawTexture(new Rect(rx - pad, ry - pad, rw + pad * 2f, pad), reverseCamWhiteTex);
        // Alt
        GUI.DrawTexture(new Rect(rx - pad, ry + rh, rw + pad * 2f, pad), reverseCamWhiteTex);
        // Sol
        GUI.DrawTexture(new Rect(rx - pad, ry, pad, rh), reverseCamWhiteTex);
        // Sağ
        GUI.DrawTexture(new Rect(rx + rw, ry, pad, rh), reverseCamWhiteTex);

        // 2) Parlak kenarlık çizgileri
        float bw = reverseCamBorderWidth;
        Color borderCol = reverseCamBorderColor;
        borderCol.a *= a;
        GUI.color = borderCol;
        // Üst
        GUI.DrawTexture(new Rect(rx - bw, ry - bw, rw + bw * 2f, bw), reverseCamWhiteTex);
        // Alt
        GUI.DrawTexture(new Rect(rx - bw, ry + rh, rw + bw * 2f, bw), reverseCamWhiteTex);
        // Sol
        GUI.DrawTexture(new Rect(rx - bw, ry, bw, rh), reverseCamWhiteTex);
        // Sağ
        GUI.DrawTexture(new Rect(rx + rw, ry, bw, rh), reverseCamWhiteTex);

        // 3) Üst gradient overlay (karartma)
        float gradH = Mathf.Min(reverseCamGradientHeight, rh * 0.4f);
        for (int i = 0; i < 8; i++)
        {
            float t = i / 8f;
            float lineH = gradH / 8f;
            GUI.color = new Color(0f, 0f, 0f, (1f - t) * 0.45f * a);
            GUI.DrawTexture(new Rect(rx, ry + t * gradH, rw, lineH), reverseCamWhiteTex);
        }

        // 4) "REAR VIEW" etiketi
        GUI.color = new Color(1f, 1f, 1f, 0.9f * a);
        reverseCamLabelStyle.fontSize = Mathf.Max(10, (int)(sh * 0.014f));
        Rect labelRect = new Rect(rx, ry + 2f, rw, reverseCamLabelStyle.fontSize + 6f);
        GUI.Label(labelRect, "REAR VIEW", reverseCamLabelStyle);

        // 5) Köşe aksan çizgileri (küçük L şekilleri)
        float cornerLen = Mathf.Min(rw, rh) * 0.08f;
        float cornerW = bw * 1.5f;
        Color cornerCol = reverseCamBorderColor;
        cornerCol.a = Mathf.Min(1f, cornerCol.a * 1.4f) * a;
        GUI.color = cornerCol;
        // Sol üst
        GUI.DrawTexture(new Rect(rx - bw, ry - bw, cornerLen, cornerW), reverseCamWhiteTex);
        GUI.DrawTexture(new Rect(rx - bw, ry - bw, cornerW, cornerLen), reverseCamWhiteTex);
        // Sağ üst
        GUI.DrawTexture(new Rect(rx + rw - cornerLen + bw, ry - bw, cornerLen, cornerW), reverseCamWhiteTex);
        GUI.DrawTexture(new Rect(rx + rw, ry - bw, cornerW, cornerLen), reverseCamWhiteTex);
        // Sol alt
        GUI.DrawTexture(new Rect(rx - bw, ry + rh, cornerLen, cornerW), reverseCamWhiteTex);
        GUI.DrawTexture(new Rect(rx - bw, ry + rh - cornerLen + bw, cornerW, cornerLen), reverseCamWhiteTex);
        // Sağ alt
        GUI.DrawTexture(new Rect(rx + rw - cornerLen + bw, ry + rh, cornerLen, cornerW), reverseCamWhiteTex);
        GUI.DrawTexture(new Rect(rx + rw, ry + rh - cornerLen + bw, cornerW, cornerLen), reverseCamWhiteTex);

        GUI.color = prevColor;
    }

    void EnsureReverseCamGUIResources()
    {
        if (reverseCamWhiteTex == null)
        {
            reverseCamWhiteTex = new Texture2D(1, 1);
            reverseCamWhiteTex.SetPixel(0, 0, Color.white);
            reverseCamWhiteTex.Apply();
        }

        if (reverseCamLabelStyle == null)
        {
            reverseCamLabelStyle = new GUIStyle(GUI.skin.label);
            reverseCamLabelStyle.alignment = TextAnchor.UpperCenter;
            reverseCamLabelStyle.fontStyle = FontStyle.Bold;
            reverseCamLabelStyle.normal.textColor = new Color(0.85f, 0.95f, 1f, 1f);
        }
    }

    void OnDestroy()
    {
        if (reverseCamHUD != null) Destroy(reverseCamHUD.gameObject);
        if (reverseCamWhiteTex != null) Destroy(reverseCamWhiteTex);
    }

    // ── Geri Görüş Kamerası ──────────────────────────────────────────────────

    void SetupReverseCameraHUD()
    {
        if (!enableReverseCamera) return;

        var go = new GameObject("_ReverseCameraHUD");
        reverseCamHUD = go.AddComponent<Camera>();
        reverseCamHUD.fieldOfView = reverseCamFov;
        reverseCamHUD.nearClipPlane = 0.15f;
        reverseCamHUD.farClipPlane = 300f;
        reverseCamHUD.depth = 2f;   // Main cam (0) üzerinde, overlay kamera katmaninda
        reverseCamHUD.rect = new Rect(reverseCamVpX, reverseCamVpY, reverseCamVpW, reverseCamVpH);
        reverseCamHUD.cullingMask = mainCamera != null ? mainCamera.cullingMask : ~0;
        reverseCamHUD.backgroundColor = mainCamera != null ? mainCamera.backgroundColor : Color.black;
        reverseCamHUD.clearFlags = ResolveSecondaryCameraClearFlags();
        reverseCamHUD.allowHDR = mainCamera != null && mainCamera.allowHDR;
        reverseCamHUD.allowMSAA = mainCamera != null && mainCamera.allowMSAA;

        Skybox mainSkybox = mainCamera != null ? mainCamera.GetComponent<Skybox>() : null;
        Material skyboxMaterial = mainSkybox != null && mainSkybox.material != null
            ? mainSkybox.material
            : RenderSettings.skybox;
        if (skyboxMaterial != null)
        {
            Skybox reverseSkybox = go.AddComponent<Skybox>();
            reverseSkybox.material = skyboxMaterial;
        }

        reverseCamHUD.enabled = false;
    }

    CameraClearFlags ResolveSecondaryCameraClearFlags()
    {
        if (mainCamera != null)
        {
            return mainCamera.clearFlags;
        }

        return RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
    }

    void UpdateReverseCameraHUD()
    {
        if (!enableReverseCamera || reverseCamHUD == null) return;

        bool shouldShow = IsCarReversing();

        // Smooth fade
        float targetAlpha = shouldShow ? 1f : 0f;
        reverseCamFadeAlpha = Mathf.MoveTowards(reverseCamFadeAlpha, targetAlpha,
            reverseCamFadeSpeed * Time.deltaTime);

        bool camActive = reverseCamFadeAlpha > 0.01f;
        if (camActive != reverseCamShowing)
        {
            reverseCamShowing = camActive;
            reverseCamHUD.enabled = reverseCamShowing;
        }

        if (!reverseCamShowing) return;

        Transform reverseAnchor = vehicleCameraAnchors != null ? vehicleCameraAnchors.ReverseCameraAnchor : null;
        Quaternion yaw = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
        reverseCamHUD.transform.position = reverseAnchor != null
            ? reverseAnchor.position
            : target.position + yaw * reverseCamOffset;
        reverseCamHUD.transform.rotation = yaw * Quaternion.Euler(reverseCamEuler);
    }

    bool IsCarReversing()
    {
        if (targetRb == null)
            return carController != null && carController.IsReverseInputActive;

        float localZ = target.InverseTransformDirection(targetRb.linearVelocity).z;

        // Araç gerçekten geri gidiyorsa
        if (localZ < -0.1f) return true;

        // Araç durağan haldeyken geri tuşuna basılıyorsa
        if (targetRb.linearVelocity.magnitude < reverseCamStationaryThreshold
            && carController != null
            && carController.IsReverseInputActive)
        {
            return true;
        }

        return false;
    }
}

