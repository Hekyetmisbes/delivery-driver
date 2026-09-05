using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using DeliveryDriver.Vehicle;
using DeliveryDriver.Company;

/// <summary>
/// Modern HUD Geri Görüş Kamerası (Reverse Camera HUD).
/// Araç geri vitese geçtiğinde veya geri giderken ekranın üst-orta kısmında (dikiz aynası hizasında)
/// şık bir çerçeve, park kılavuz çizgileri (guidelines), vites rozeti ve canlı kamera görüntüsüyle açılır.
/// URP ve tüm platformlarla %100 uyumlu RenderTexture + UI RawImage mimarisi kullanır.
/// Geri viteste değilken kamera otomatik devre dışı kalarak 0 draw-call / 0 GPU yükü üretir.
/// </summary>
public class ReverseCameraHUD : MonoBehaviour
{
    private const string GameSceneName = "Game";
    private const string AltGameSceneName = "GameScene";

    [Header("--- HEDEF ARAÇ ---")]
    [Tooltip("Takip edilecek araç. Boşsa otomatik bulunur.")]
    [SerializeField] private Transform carTarget;

    [Header("--- KAMERA POZİSYONU (LOKAL) ---")]
    [Tooltip("Aracın arkasındaki kamera konumu. Z negatif = arkası.")]
    [SerializeField] private Vector3 cameraLocalOffset = new Vector3(0f, 0.75f, -1.8f);
    [Tooltip("Kamera açısı. Y=180 geriye bakar, X pozitif = aşağı eğimli.")]
    [SerializeField] private Vector3 cameraLocalEuler = new Vector3(12f, 180f, 0f);
    [SerializeField] private float fieldOfView = 65f;

    [Header("--- HUD PANEL AYARLARI ---")]
    [SerializeField] private Vector2 panelSize = new Vector2(380f, 190f);
    [SerializeField] private Vector2 panelAnchoredPosition = new Vector2(0f, -18f);
    [SerializeField] private float fadeSpeed = 8f;

    [Header("--- TETİKLEYİCİLER ---")]
    [SerializeField] private float reverseVelocityThreshold = -0.12f;
    [SerializeField] private float stationarySpeedThreshold = 1.0f;

    [Header("--- GÖRSEL STİL ---")]
    [SerializeField] private Color borderColor = new Color(0.12f, 0.85f, 1f, 0.9f);
    [SerializeField] private Color frameBackgroundColor = new Color(0.05f, 0.07f, 0.1f, 0.95f);
    [SerializeField] private Color headerColor = new Color(0.03f, 0.04f, 0.06f, 0.98f);
    [SerializeField] private bool showParkingGuidelines = true;

    // Runtime components
    private Camera gameplayCamera;
    private Camera reverseCam;
    private RenderTexture renderTexture;
    private CarController carController;
    private Rigidbody carRb;
    private VehicleCameraAnchors vehicleCameraAnchors;
    private Vector3 cachedFallbackOffset;

    // UI elements
    private RectTransform panelRoot;
    private CanvasGroup panelCanvasGroup;
    private RawImage cameraDisplayImage;
    private TextMeshProUGUI headerTitleText;
    private TextMeshProUGUI gearBadgeText;
    private float currentAlpha;
    private bool isReverseActive;

    private void Awake()
    {
        InitializeRenderTexture();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        PlayerVehicleManager.ActiveVehicleChanged += OnVehicleChanged;

        ResolveTarget();
        EnsureUI();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        PlayerVehicleManager.ActiveVehicleChanged -= OnVehicleChanged;

        if (reverseCam != null)
        {
            reverseCam.enabled = false;
        }
    }

    private void Start()
    {
        if (carTarget == null)
        {
            ResolveTarget();
        }

        EnsureUI();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveTarget();
        EnsureUI();
    }

    private void OnVehicleChanged(CarController controller)
    {
        if (controller != null)
        {
            SetTarget(controller.transform);
        }
        else
        {
            ResolveTarget();
        }
    }

    private void InitializeRenderTexture()
    {
        if (renderTexture == null)
        {
            renderTexture = new RenderTexture(480, 240, 16, RenderTextureFormat.ARGB32)
            {
                name = "_ReverseCameraTexture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            renderTexture.Create();
        }
    }

    private void EnsureCamera()
    {
        if (reverseCam != null) return;

        InitializeRenderTexture();

        GameObject camObj = new GameObject("_ReverseCameraHUD");
        reverseCam = camObj.AddComponent<Camera>();
        reverseCam.targetTexture = renderTexture;
        reverseCam.nearClipPlane = 0.15f;
        reverseCam.farClipPlane = 250f;
        reverseCam.fieldOfView = fieldOfView;
        reverseCam.clearFlags = CameraClearFlags.Skybox;
        reverseCam.backgroundColor = Color.black;
        reverseCam.cullingMask = ~0;
        reverseCam.enabled = false;

        Camera source = gameplayCamera != null ? gameplayCamera : Camera.main;
        if (source != null)
        {
            reverseCam.cullingMask = source.cullingMask;
            reverseCam.clearFlags = source.clearFlags;
            reverseCam.backgroundColor = source.backgroundColor;
        }
    }

    public void ResolveTarget()
    {
        // 1. PlayerVehicleManager
        if (PlayerVehicleManager.Instance != null && PlayerVehicleManager.Instance.ActiveVehicleController != null)
        {
            SetTarget(PlayerVehicleManager.Instance.ActiveVehicleController.transform);
            return;
        }

        // 2. VehicleCameraTargetResolver
        Transform defaultTarget = VehicleCameraTargetResolver.ResolveDefaultTarget();
        if (defaultTarget != null)
        {
            SetTarget(defaultTarget);
            return;
        }

        // 3. Scene CarController
        CarController sceneCar = FindFirstObjectByType<CarController>();
        if (sceneCar != null)
        {
            SetTarget(sceneCar.transform);
        }
    }

    public void SetTarget(Transform newTarget)
    {
        VehicleCameraBinding binding = VehicleCameraTargetResolver.Resolve(newTarget);
        carTarget = binding.Target;
        carController = binding.CarController;
        carRb = binding.Rigidbody;
        vehicleCameraAnchors = binding.CameraAnchors;
        cachedFallbackOffset = ResolveFallbackOffset();

        if (carTarget == null && reverseCam != null)
        {
            reverseCam.enabled = false;
        }
    }

    public void SetGameplayCamera(Camera sourceCamera)
    {
        gameplayCamera = sourceCamera;
        if (reverseCam != null && sourceCamera != null)
        {
            reverseCam.cullingMask = sourceCamera.cullingMask;
            reverseCam.clearFlags = sourceCamera.clearFlags;
            reverseCam.backgroundColor = sourceCamera.backgroundColor;
        }
    }

    public void Configure(
        Vector3 localOffset,
        Vector3 localEuler,
        float cameraFov,
        Rect viewport,
        float reverseThreshold,
        float stationaryThresholdValue,
        Color frameColor,
        float frameLineWidth,
        float frameInset,
        float fadeSpeedValue,
        float gradientHeightValue)
    {
        cameraLocalOffset = localOffset;
        cameraLocalEuler = localEuler;
        fieldOfView = cameraFov;
        reverseVelocityThreshold = reverseThreshold;
        stationarySpeedThreshold = stationaryThresholdValue;
        borderColor = frameColor;
        fadeSpeed = fadeSpeedValue;
        cachedFallbackOffset = ResolveFallbackOffset();

        if (reverseCam != null)
        {
            reverseCam.fieldOfView = fieldOfView;
        }
    }

    private void EnsureUI()
    {
        if (panelRoot != null || !IsGameSceneActive()) return;

        Canvas targetCanvas = GetOrCreateHudCanvas();
        if (targetCanvas == null) return;

        Sprite fallback = DeliveryUiSpriteHelper.GetFallbackSprite();

        // 1. Root Panel
        GameObject panelObj = new GameObject("ReverseCameraHUDPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        panelObj.transform.SetParent(targetCanvas.transform, false);

        panelRoot = panelObj.GetComponent<RectTransform>();
        panelRoot.anchorMin = new Vector2(0.5f, 1f);
        panelRoot.anchorMax = new Vector2(0.5f, 1f);
        panelRoot.pivot = new Vector2(0.5f, 1f);
        panelRoot.anchoredPosition = panelAnchoredPosition;
        panelRoot.sizeDelta = panelSize;

        panelCanvasGroup = panelObj.GetComponent<CanvasGroup>();
        panelCanvasGroup.alpha = 0f;
        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = false;

        Image panelBorderImg = panelObj.GetComponent<Image>();
        panelBorderImg.color = borderColor;
        panelBorderImg.sprite = fallback;
        panelBorderImg.type = Image.Type.Simple;

        // 2. Inner Frame Background
        GameObject innerBgObj = new GameObject("InnerFrame", typeof(RectTransform), typeof(Image));
        innerBgObj.transform.SetParent(panelObj.transform, false);
        RectTransform innerRect = innerBgObj.GetComponent<RectTransform>();
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(3f, 3f);
        innerRect.offsetMax = new Vector2(-3f, -3f);

        Image innerImg = innerBgObj.GetComponent<Image>();
        innerImg.color = frameBackgroundColor;
        innerImg.sprite = fallback;
        innerImg.type = Image.Type.Simple;

        // 3. Top Header Bar
        GameObject headerObj = new GameObject("HeaderBar", typeof(RectTransform), typeof(Image));
        headerObj.transform.SetParent(innerBgObj.transform, false);
        RectTransform headerRect = headerObj.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = Vector2.zero;
        headerRect.sizeDelta = new Vector2(0f, 26f);

        Image headerImg = headerObj.GetComponent<Image>();
        headerImg.color = headerColor;
        headerImg.sprite = fallback;

        // Gear Badge [R]
        GameObject badgeObj = new GameObject("GearBadge", typeof(RectTransform), typeof(Image));
        badgeObj.transform.SetParent(headerObj.transform, false);
        RectTransform badgeRect = badgeObj.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0f, 0.5f);
        badgeRect.anchorMax = new Vector2(0f, 0.5f);
        badgeRect.pivot = new Vector2(0f, 0.5f);
        badgeRect.anchoredPosition = new Vector2(8f, 0f);
        badgeRect.sizeDelta = new Vector2(22f, 18f);

        Image badgeImg = badgeObj.GetComponent<Image>();
        badgeImg.color = new Color(0.9f, 0.15f, 0.15f, 1f);
        badgeImg.sprite = fallback;

        GameObject badgeTextObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        badgeTextObj.transform.SetParent(badgeObj.transform, false);
        RectTransform btRect = badgeTextObj.GetComponent<RectTransform>();
        btRect.anchorMin = Vector2.zero;
        btRect.anchorMax = Vector2.one;
        btRect.offsetMin = Vector2.zero;
        btRect.offsetMax = Vector2.zero;

        gearBadgeText = badgeTextObj.GetComponent<TextMeshProUGUI>();
        gearBadgeText.text = "R";
        gearBadgeText.fontSize = 14f;
        gearBadgeText.fontStyle = FontStyles.Bold;
        gearBadgeText.alignment = TextAlignmentOptions.Center;
        gearBadgeText.color = Color.white;

        // Header Title Text
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(headerObj.transform, false);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(36f, 0f);
        titleRect.offsetMax = new Vector2(-60f, 0f);

        headerTitleText = titleObj.GetComponent<TextMeshProUGUI>();
        headerTitleText.text = "REAR VIEW / GERİ GÖRÜŞ";
        headerTitleText.fontSize = 12f;
        headerTitleText.fontStyle = FontStyles.Bold;
        headerTitleText.alignment = TextAlignmentOptions.Left;
        headerTitleText.color = new Color(0.85f, 0.95f, 1f, 0.95f);

        // LIVE Indicator
        GameObject liveObj = new GameObject("LiveIndicator", typeof(RectTransform), typeof(TextMeshProUGUI));
        liveObj.transform.SetParent(headerObj.transform, false);
        RectTransform liveRect = liveObj.GetComponent<RectTransform>();
        liveRect.anchorMin = new Vector2(1f, 0.5f);
        liveRect.anchorMax = new Vector2(1f, 0.5f);
        liveRect.pivot = new Vector2(1f, 0.5f);
        liveRect.anchoredPosition = new Vector2(-8f, 0f);
        liveRect.sizeDelta = new Vector2(60f, 20f);

        TextMeshProUGUI liveText = liveObj.GetComponent<TextMeshProUGUI>();
        liveText.text = "● LIVE";
        liveText.fontSize = 11f;
        liveText.fontStyle = FontStyles.Bold;
        liveText.alignment = TextAlignmentOptions.Right;
        liveText.color = new Color(0.2f, 1f, 0.4f, 0.9f);

        // 4. RawImage Camera Screen
        GameObject screenObj = new GameObject("CameraScreen", typeof(RectTransform), typeof(RawImage));
        screenObj.transform.SetParent(innerBgObj.transform, false);
        RectTransform screenRect = screenObj.GetComponent<RectTransform>();
        screenRect.anchorMin = Vector2.zero;
        screenRect.anchorMax = Vector2.one;
        screenRect.offsetMin = new Vector2(0f, 0f);
        screenRect.offsetMax = new Vector2(0f, -26f); // Below header

        cameraDisplayImage = screenObj.GetComponent<RawImage>();
        cameraDisplayImage.texture = renderTexture;
        cameraDisplayImage.color = Color.white;

        // 5. Parking Guidelines Overlay
        if (showParkingGuidelines)
        {
            BuildParkingGuidelines(screenObj.transform, fallback);
        }

        panelObj.SetActive(false);
    }

    private void BuildParkingGuidelines(Transform parent, Sprite fallback)
    {
        GameObject guideRoot = new GameObject("ParkingGuidelines", typeof(RectTransform));
        guideRoot.transform.SetParent(parent, false);
        RectTransform grRect = guideRoot.GetComponent<RectTransform>();
        grRect.anchorMin = Vector2.zero;
        grRect.anchorMax = Vector2.one;
        grRect.offsetMin = Vector2.zero;
        grRect.offsetMax = Vector2.zero;

        // Kırmızı Dur Çizgisi (Alt / En Yakın)
        CreateGuideLine(guideRoot.transform, fallback, new Vector2(0.22f, 0.12f), new Vector2(0.78f, 0.12f), new Color(1f, 0.15f, 0.15f, 0.85f), 2.5f);
        // Sarı Uyarı Çizgisi (Orta)
        CreateGuideLine(guideRoot.transform, fallback, new Vector2(0.26f, 0.28f), new Vector2(0.74f, 0.28f), new Color(1f, 0.85f, 0.1f, 0.75f), 2f);
        // Yeşil Güvenli Çizgi (Üst / Uzak)
        CreateGuideLine(guideRoot.transform, fallback, new Vector2(0.30f, 0.46f), new Vector2(0.70f, 0.46f), new Color(0.2f, 1f, 0.35f, 0.7f), 1.8f);

        // Sol Kılavuz Hattı (Perspektif)
        CreateGuideLine(guideRoot.transform, fallback, new Vector2(0.18f, 0.05f), new Vector2(0.32f, 0.52f), new Color(0.15f, 0.9f, 1f, 0.65f), 2f);
        // Sağ Kılavuz Hattı (Perspektif)
        CreateGuideLine(guideRoot.transform, fallback, new Vector2(0.82f, 0.05f), new Vector2(0.68f, 0.52f), new Color(0.15f, 0.9f, 1f, 0.65f), 2f);
    }

    private static void CreateGuideLine(Transform parent, Sprite fallback, Vector2 startRatio, Vector2 endRatio, Color color, float thickness)
    {
        GameObject lineObj = new GameObject("GuideLine", typeof(RectTransform), typeof(Image));
        lineObj.transform.SetParent(parent, false);

        RectTransform rect = lineObj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);

        // Parent boyutu varsayımı (368x150)
        Vector2 start = new Vector2(startRatio.x * 368f, startRatio.y * 150f);
        Vector2 end = new Vector2(endRatio.x * 368f, endRatio.y * 150f);
        Vector2 mid = (start + end) * 0.5f;
        Vector2 diff = end - start;
        float length = diff.magnitude;
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

        rect.anchoredPosition = mid;
        rect.sizeDelta = new Vector2(length, thickness);
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);

        Image img = lineObj.GetComponent<Image>();
        img.color = color;
        img.sprite = fallback;
        img.raycastTarget = false;
    }

    private void LateUpdate()
    {
        if (!IsGameSceneActive())
        {
            if (panelRoot != null && panelRoot.gameObject.activeSelf)
            {
                panelRoot.gameObject.SetActive(false);
            }
            if (reverseCam != null)
            {
                reverseCam.enabled = false;
            }
            return;
        }

        if (carTarget == null)
        {
            ResolveTarget();
            if (carTarget == null) return;
        }

        EnsureCamera();
        EnsureUI();

        bool reversing = CheckIsReversing();
        isReverseActive = reversing;

        float targetAlpha = reversing ? 1f : 0f;
        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);

        bool shouldBeVisible = currentAlpha > 0.005f;

        if (panelRoot != null)
        {
            if (shouldBeVisible != panelRoot.gameObject.activeSelf)
            {
                panelRoot.gameObject.SetActive(shouldBeVisible);
            }

            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = currentAlpha;
            }

            // Hafif yukarı-aşağı kayma animasyonu (-10'dan -18'e)
            float yPos = Mathf.Lerp(-10f, panelAnchoredPosition.y, currentAlpha);
            panelRoot.anchoredPosition = new Vector2(panelAnchoredPosition.x, yPos);
        }

        if (reverseCam != null)
        {
            reverseCam.enabled = shouldBeVisible;

            if (shouldBeVisible)
            {
                UpdateCameraPosition();
            }
        }
    }

    private bool CheckIsReversing()
    {
        if (carRb == null)
        {
            return carController != null && carController.IsReverseInputActive;
        }

        float localZ = carTarget.InverseTransformDirection(carRb.linearVelocity).z;
        if (localZ < reverseVelocityThreshold) return true;

        if (carRb.linearVelocity.magnitude < stationarySpeedThreshold
            && carController != null
            && carController.IsReverseInputActive)
        {
            return true;
        }

        return false;
    }

    private void UpdateCameraPosition()
    {
        if (carTarget == null || reverseCam == null) return;

        Transform reverseAnchor = vehicleCameraAnchors != null ? vehicleCameraAnchors.ReverseCameraAnchor : null;
        Quaternion yaw = Quaternion.Euler(0f, carTarget.eulerAngles.y, 0f);

        if (reverseAnchor != null)
        {
            reverseCam.transform.position = reverseAnchor.position;
            reverseCam.transform.rotation = yaw * Quaternion.Euler(cameraLocalEuler);
        }
        else
        {
            reverseCam.transform.position = carTarget.TransformPoint(cachedFallbackOffset);
            reverseCam.transform.rotation = yaw * Quaternion.Euler(cameraLocalEuler);
        }
    }

    private Vector3 ResolveFallbackOffset()
    {
        if (carTarget == null) return cameraLocalOffset;

        Renderer[] renderers = carTarget.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || r is ParticleSystemRenderer || r is TrailRenderer) continue;
            if (r.transform.name.Contains("Wheel")) continue;

            Bounds b = r.bounds;
            Vector3 localCenter = carTarget.InverseTransformPoint(b.center);
            Vector3 localExtents = carTarget.InverseTransformVector(b.extents);
            localExtents = new Vector3(Mathf.Abs(localExtents.x), Mathf.Abs(localExtents.y), Mathf.Abs(localExtents.z));

            Vector3 bMin = localCenter - localExtents;
            Vector3 bMax = localCenter + localExtents;

            if (!hasBounds)
            {
                min = bMin;
                max = bMax;
                hasBounds = true;
            }
            else
            {
                min = Vector3.Min(min, bMin);
                max = Vector3.Max(max, bMax);
            }
        }

        if (!hasBounds) return cameraLocalOffset;

        // Aracın arka tamponunun hemen gerisi ve tavan/cam hizası
        return new Vector3(0f, max.y * 0.72f, min.z - 0.25f);
    }

    private Canvas GetOrCreateHudCanvas()
    {
        Canvas primary = GlobalUiCoordinator.PrimaryCanvas;
        if (primary != null) return primary;

        GameObject existing = GameObject.Find("GameplayHUDCanvas");
        if (existing != null)
        {
            Canvas existingCanvas = existing.GetComponent<Canvas>();
            if (existingCanvas != null) return existingCanvas;
        }

        GameObject canvasObject = new GameObject("GameplayHUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 220;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private void OnDestroy()
    {
        if (reverseCam != null)
        {
            Destroy(reverseCam.gameObject);
        }

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }

    private static bool IsGameSceneActive()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return sceneName.Equals(GameSceneName, System.StringComparison.OrdinalIgnoreCase) ||
               sceneName.Equals(AltGameSceneName, System.StringComparison.OrdinalIgnoreCase);
    }
}
