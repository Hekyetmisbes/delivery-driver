using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using DeliveryDriver.Quest;
using DeliveryDriver.UI;
using DeliveryDriver.Company;
using DeliveryDriver.Vehicle;
using DeliveryDriver.UIKit;

/// <summary>
/// Modern araç hız göstergesi (Speedometer HUD).
/// Ekranın sağ alt köşesinde dairesel kavisli gösterge çentikleri (arc gauge ticks),
/// büyük dijital hız değeri, KM/H veya MPH birim göstergesi ve vites göstergesi (P, R, N, 1-6) sunar.
/// Araç değişimlerinde ve sahne geçişlerinde otomatik olarak oyuncu aracını bulur ve bağlanır.
/// </summary>
public class SpeedometerUI : MonoBehaviour
{
    private const string GameSceneName = "Game";
    private const string AltGameSceneName = "GameScene";

    [Header("Visibility")]
    [SerializeField] private bool showSpeedometer = true;

    [Header("Layout")]
    [SerializeField] private Vector2 anchoredPosition = new Vector2(-40f, 36f);
    [SerializeField] private Vector2 panelSize = new Vector2(280f, 220f);
    [SerializeField] private Vector2 minimumPanelSize = new Vector2(252f, 198f);
    [SerializeField] private Vector2 maximumPanelSize = new Vector2(304f, 236f);
    [SerializeField] private float responsiveReferenceShortSide = 1080f;
    [SerializeField] private float gaugeRadius = 88f;
    [SerializeField] private int tickCount = 28;
    [SerializeField] private float gaugeStartAngle = 220f;
    [SerializeField] private float gaugeSweepAngle = 280f;

    [Header("Display")]
    [SerializeField] private bool displayMph = false;
    [SerializeField] private bool useGameSettingsUnit = true;
    [SerializeField] private int speedFontSize = 114;
    [SerializeField] private int unitFontSize = 38;
    [SerializeField] private float maxGaugeSpeedMph = 160f;
    [SerializeField] private float maxGaugeSpeedKmh = 260f;
    [SerializeField] private float redThresholdKmh = 100f;
    [SerializeField] private float redThresholdMph = 62f;
    [SerializeField] private int updateEveryNFrames = 2;

    [Header("Colors")]
    [SerializeField] private Color panelColor = new Color(0.04f, 0.06f, 0.1f, 0.72f);
    [SerializeField] private Color lowSpeedColor = new Color(0f, 0.82f, 1f, 1f);
    [SerializeField] private Color highSpeedColor = new Color(1f, 0.15f, 0.15f, 1f);
    [SerializeField] private Color inactiveTickColor = new Color(1f, 1f, 1f, 0.28f);

    private RectTransform rootRect;
    private Image panelImage;
    private TextMeshProUGUI speedValueText;
    private TextMeshProUGUI unitText;
    private TextMeshProUGUI gearText;
    private Rigidbody playerRigidbody;
    private CarController activeCarController;
    private Image[] tickImages;
    private bool hasPackDial;
    private int frameCounter;
    private int lastDisplayedSpeed = -1;
    private string lastGearDisplay = "";
    private Vector2 lastScreenSize = Vector2.negativeInfinity;

    public void Initialize(Rigidbody rb)
    {
        playerRigidbody = rb;
        if (playerRigidbody != null)
        {
            activeCarController = playerRigidbody.GetComponent<CarController>();
        }

        SyncUnitPreferenceFromSettings();
        EnsureUI();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        PlayerVehicleManager.ActiveVehicleChanged += OnVehicleChanged;
        GameSettings.OnSpeedUnitChanged += HandleSpeedUnitChanged;

        SyncUnitPreferenceFromSettings();
        ResolvePlayerRigidbody();
        EnsureUI();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        PlayerVehicleManager.ActiveVehicleChanged -= OnVehicleChanged;
        GameSettings.OnSpeedUnitChanged -= HandleSpeedUnitChanged;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolvePlayerRigidbody();
        SyncUnitPreferenceFromSettings();
        EnsureUI();
    }

    private void OnVehicleChanged(CarController controller)
    {
        if (controller != null)
        {
            activeCarController = controller;
            playerRigidbody = controller.GetComponent<Rigidbody>();
        }
        else
        {
            ResolvePlayerRigidbody();
        }
    }

    public void ResolvePlayerRigidbody()
    {
        // 1. PlayerVehicleManager
        if (PlayerVehicleManager.Instance != null && PlayerVehicleManager.Instance.ActiveVehicleController != null)
        {
            activeCarController = PlayerVehicleManager.Instance.ActiveVehicleController;
            playerRigidbody = activeCarController.GetComponent<Rigidbody>();
            if (playerRigidbody != null) return;
        }

        // 2. VehicleCameraTargetResolver
        Transform target = VehicleCameraTargetResolver.ResolveDefaultTarget();
        if (target != null)
        {
            VehicleCameraBinding binding = VehicleCameraTargetResolver.Resolve(target);
            if (binding.Rigidbody != null)
            {
                playerRigidbody = binding.Rigidbody;
                activeCarController = binding.CarController;
                return;
            }
        }

        // 3. Find in scene
        CarController sceneCar = FindFirstObjectByType<CarController>();
        if (sceneCar != null)
        {
            activeCarController = sceneCar;
            playerRigidbody = sceneCar.GetComponent<Rigidbody>();
        }
    }

    private void Update()
    {
        if (!IsGameSceneActive())
        {
            if (rootRect != null && rootRect.gameObject.activeSelf)
            {
                rootRect.gameObject.SetActive(false);
            }
            return;
        }

        if (playerRigidbody == null)
        {
            ResolvePlayerRigidbody();
        }

        int safeFrameInterval = Mathf.Max(1, updateEveryNFrames);
        frameCounter++;
        if (frameCounter % safeFrameInterval == 0)
        {
            UpdateUI();
        }
    }

    private void EnsureUI()
    {
        if (!showSpeedometer || rootRect != null)
        {
            return;
        }

        Canvas targetCanvas = GetOrCreateHudCanvas();
        if (targetCanvas == null) return;

        Sprite fallback = DeliveryUiSpriteHelper.GetFallbackSprite();

        GameObject panelObject = new GameObject("SpeedometerPanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(targetCanvas.transform, false);

        rootRect = panelObject.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(1f, 0f);
        rootRect.anchorMax = new Vector2(1f, 0f);
        rootRect.pivot = new Vector2(1f, 0f);
        rootRect.anchoredPosition = anchoredPosition;
        rootRect.sizeDelta = panelSize;

        panelImage = panelObject.GetComponent<Image>();
        Sprite packDial = DDUIVisuals.LoadComponent("speedometer_dial");
        hasPackDial = packDial != null;
        panelImage.color = hasPackDial ? Color.white : panelColor;
        panelImage.raycastTarget = false;
        panelImage.sprite = hasPackDial ? packDial : fallback;
        panelImage.type = Image.Type.Simple;
        panelImage.preserveAspect = hasPackDial;

        BuildGaugeTicks(panelObject.transform, fallback);
        BuildSpeedTexts(panelObject.transform);
        ApplyResponsiveLayout();
    }

    private void UpdateUI()
    {
        if (!showSpeedometer || !IsGameSceneActive())
        {
            if (rootRect != null && rootRect.gameObject.activeSelf)
            {
                rootRect.gameObject.SetActive(false);
            }
            return;
        }

        if (speedValueText == null || rootRect == null)
        {
            EnsureUI();
        }

        if (speedValueText == null || rootRect == null)
        {
            return;
        }

        ApplyResponsiveLayout();

        float speedMps = playerRigidbody != null ? playerRigidbody.linearVelocity.magnitude : 0f;
        float speedKmh = speedMps * 3.6f;
        float speedMph = speedMps * 2.2369363f;
        float shownSpeed = displayMph ? speedMph : speedKmh;
        float maxShownSpeed = displayMph ? Mathf.Max(10f, maxGaugeSpeedMph) : Mathf.Max(10f, maxGaugeSpeedKmh);
        float normalized = Mathf.Clamp01(shownSpeed / maxShownSpeed);
        Color activeColor = EvaluateSpeedColor(normalized, shownSpeed);

        if (!rootRect.gameObject.activeSelf)
        {
            rootRect.gameObject.SetActive(true);
        }

        UpdateTicks(normalized, activeColor);
        if (unitText != null) unitText.color = activeColor;
        if (speedValueText != null) speedValueText.color = activeColor;

        int roundedSpeed = Mathf.RoundToInt(shownSpeed);
        if (roundedSpeed != lastDisplayedSpeed)
        {
            lastDisplayedSpeed = roundedSpeed;
            if (speedValueText != null)
            {
                speedValueText.SetText("{0:00}", roundedSpeed);
            }
        }

        UpdateGearDisplay(speedMps, speedKmh);
    }

    private void BuildSpeedTexts(Transform parent)
    {
        GameObject speedTextObject = new GameObject("SpeedValue", typeof(RectTransform), typeof(TextMeshProUGUI));
        speedTextObject.transform.SetParent(parent, false);
        RectTransform speedRect = speedTextObject.GetComponent<RectTransform>();
        speedRect.anchorMin = new Vector2(0.5f, 0.44f);
        speedRect.anchorMax = new Vector2(0.5f, 0.44f);
        speedRect.pivot = new Vector2(0.5f, 0.5f);
        speedRect.sizeDelta = new Vector2(210f, 120f);

        speedValueText = speedTextObject.GetComponent<TextMeshProUGUI>();
        speedValueText.alignment = TextAlignmentOptions.Center;
        speedValueText.fontSize = hasPackDial ? 84f : Mathf.Max(48, speedFontSize);
        speedValueText.fontStyle = FontStyles.Bold;
        speedValueText.textWrappingMode = TextWrappingModes.NoWrap;
        speedValueText.outlineWidth = 0.2f;
        speedValueText.outlineColor = new Color(0f, 0f, 0f, 0.95f);
        speedValueText.text = "00";

        GameObject unitTextObject = new GameObject("SpeedUnit", typeof(RectTransform), typeof(TextMeshProUGUI));
        unitTextObject.transform.SetParent(parent, false);
        RectTransform unitRect = unitTextObject.GetComponent<RectTransform>();
        unitRect.anchorMin = new Vector2(0.72f, 0.14f);
        unitRect.anchorMax = new Vector2(0.72f, 0.14f);
        unitRect.pivot = new Vector2(0.5f, 0.5f);
        unitRect.sizeDelta = new Vector2(90f, 46f);

        unitText = unitTextObject.GetComponent<TextMeshProUGUI>();
        unitText.alignment = TextAlignmentOptions.Center;
        unitText.fontSize = hasPackDial ? 22f : Mathf.Max(16, unitFontSize);
        unitText.fontStyle = FontStyles.Bold;
        unitText.textWrappingMode = TextWrappingModes.NoWrap;
        unitText.outlineWidth = 0.16f;
        unitText.outlineColor = new Color(0f, 0f, 0f, 0.95f);
        unitText.text = displayMph ? "MPH" : "KM/H";

        // Gear text
        GameObject gearTextObject = new GameObject("GearIndicator", typeof(RectTransform), typeof(TextMeshProUGUI));
        gearTextObject.transform.SetParent(parent, false);
        RectTransform gearRect = gearTextObject.GetComponent<RectTransform>();
        gearRect.anchorMin = new Vector2(0.28f, 0.14f);
        gearRect.anchorMax = new Vector2(0.28f, 0.14f);
        gearRect.pivot = new Vector2(0.5f, 0.5f);
        gearRect.sizeDelta = new Vector2(60f, 40f);

        gearText = gearTextObject.GetComponent<TextMeshProUGUI>();
        gearText.alignment = TextAlignmentOptions.Center;
        gearText.fontSize = 28f;
        gearText.fontStyle = FontStyles.Bold;
        gearText.textWrappingMode = TextWrappingModes.NoWrap;
        gearText.outlineWidth = 0.16f;
        gearText.outlineColor = new Color(0f, 0f, 0f, 0.95f);
        gearText.text = "P";
        gearText.color = Color.white;
    }

    private void UpdateGearDisplay(float speedMps, float speedKmh)
    {
        if (gearText == null) return;

        string gear;
        bool isReverse = false;

        if (activeCarController != null && activeCarController.IsReverseInputActive)
        {
            isReverse = true;
        }
        else if (playerRigidbody != null)
        {
            float forwardDot = Vector3.Dot(playerRigidbody.linearVelocity, playerRigidbody.transform.forward);
            isReverse = forwardDot < -0.3f && speedMps > 0.3f;
        }

        if (isReverse)
        {
            gear = "R";
        }
        else if (speedKmh < 1.5f)
        {
            gear = (activeCarController != null && Mathf.Abs(playerRigidbody != null ? playerRigidbody.linearVelocity.magnitude : 0f) < 0.2f) ? "P" : "N";
        }
        else if (speedKmh < 30f)
        {
            gear = "1";
        }
        else if (speedKmh < 60f)
        {
            gear = "2";
        }
        else if (speedKmh < 100f)
        {
            gear = "3";
        }
        else if (speedKmh < 140f)
        {
            gear = "4";
        }
        else if (speedKmh < 190f)
        {
            gear = "5";
        }
        else
        {
            gear = "6";
        }

        if (!string.Equals(gear, lastGearDisplay))
        {
            lastGearDisplay = gear;
            gearText.text = gear;
            gearText.color = isReverse ? new Color(1f, 0.25f, 0.2f, 1f) : Color.white;
        }
    }

    private void BuildGaugeTicks(Transform parent, Sprite fallbackSprite)
    {
        int safeTickCount = Mathf.Max(10, tickCount);
        tickImages = new Image[safeTickCount];

        GameObject ticksRootObject = new GameObject("GaugeTicks", typeof(RectTransform));
        ticksRootObject.transform.SetParent(parent, false);
        RectTransform ticksRoot = ticksRootObject.GetComponent<RectTransform>();
        ticksRoot.anchorMin = new Vector2(0.5f, 0.5f);
        ticksRoot.anchorMax = new Vector2(0.5f, 0.5f);
        ticksRoot.pivot = new Vector2(0.5f, 0.5f);
        ticksRoot.sizeDelta = Vector2.zero;

        float safeRadius = Mathf.Max(40f, gaugeRadius);
        float angleStep = gaugeSweepAngle / (safeTickCount - 1);
        for (int i = 0; i < safeTickCount; i++)
        {
            float angleDeg = gaugeStartAngle - (angleStep * i);
            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

            bool isMajorTick = i % 4 == 0;
            float tickLength = isMajorTick ? 17f : 9f;
            float tickWidth = isMajorTick ? 3.2f : 2.2f;
            Vector2 tickPosition = direction * safeRadius;

            GameObject tickObject = new GameObject("Tick_" + i, typeof(RectTransform), typeof(Image));
            tickObject.transform.SetParent(ticksRoot, false);

            RectTransform tickRect = tickObject.GetComponent<RectTransform>();
            tickRect.anchorMin = new Vector2(0.5f, 0.5f);
            tickRect.anchorMax = new Vector2(0.5f, 0.5f);
            tickRect.pivot = new Vector2(0.5f, 0.5f);
            tickRect.anchoredPosition = tickPosition;
            tickRect.sizeDelta = new Vector2(tickWidth, tickLength);
            tickRect.localRotation = Quaternion.Euler(0f, 0f, angleDeg - 90f);

            Image tickImg = tickObject.GetComponent<Image>();
            tickImg.color = hasPackDial ? Color.clear : inactiveTickColor;
            tickImg.raycastTarget = false;
            tickImg.sprite = fallbackSprite;
            tickImg.type = Image.Type.Simple;

            tickImages[i] = tickImg;
        }
    }

    private void UpdateTicks(float normalizedSpeed, Color activeColor)
    {
        if (tickImages == null || tickImages.Length == 0) return;

        int activeCount = Mathf.RoundToInt(normalizedSpeed * tickImages.Length);
        for (int i = 0; i < tickImages.Length; i++)
        {
            Image tick = tickImages[i];
            if (tick == null) continue;

            if (i < activeCount)
            {
                tick.color = activeColor;
            }
            else
            {
                tick.color = hasPackDial ? Color.clear : inactiveTickColor;
            }
        }
    }

    private Color EvaluateSpeedColor(float normalized, float shownSpeed)
    {
        float threshold = displayMph ? redThresholdMph : redThresholdKmh;
        if (shownSpeed >= threshold)
        {
            Color forcedRed = Color.Lerp(highSpeedColor, new Color(1f, 0.1f, 0.1f, 1f), (shownSpeed - threshold) / 30f);
            forcedRed.a = 1f;
            return forcedRed;
        }

        float thresholdT = Mathf.Clamp01(shownSpeed / threshold);
        Color visible = Color.Lerp(DDUITheme.Primary, DDUITheme.Warning, thresholdT);
        visible.a = 1f;
        return visible;
    }

    private void HandleSpeedUnitChanged(SpeedUnitPreference preference)
    {
        if (!useGameSettingsUnit)
        {
            return;
        }

        bool shouldDisplayMph = preference == SpeedUnitPreference.Mph;
        if (displayMph == shouldDisplayMph)
        {
            return;
        }

        displayMph = shouldDisplayMph;
        lastDisplayedSpeed = -1;
        if (unitText != null)
        {
            unitText.text = displayMph ? "MPH" : "KM/H";
        }

        UpdateUI();
    }

    private void SyncUnitPreferenceFromSettings()
    {
        if (!useGameSettingsUnit)
        {
            return;
        }

        GameSettings settings = GameSettings.Instance;
        if (settings == null)
        {
            return;
        }

        HandleSpeedUnitChanged(settings.SpeedUnitPreference);
    }

    private Canvas GetOrCreateHudCanvas()
    {
        // 1. Primary Canvas from GlobalUiCoordinator
        Canvas primary = GlobalUiCoordinator.PrimaryCanvas;
        if (primary != null)
        {
            return primary;
        }

        // 2. GameplayHUDCanvas in scene
        GameObject existing = GameObject.Find("GameplayHUDCanvas");
        if (existing != null)
        {
            Canvas existingCanvas = existing.GetComponent<Canvas>();
            if (existingCanvas != null)
            {
                EnsureHudCanvasSettings(existingCanvas.gameObject, existingCanvas);
                return existingCanvas;
            }
        }

        // 3. Fallback create
        GameObject canvasObject = new GameObject("GameplayHUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        EnsureHudCanvasSettings(canvasObject, canvas);
        return canvas;
    }

    private static void EnsureHudCanvasSettings(GameObject canvasObject, Canvas canvas)
    {
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 220;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvasObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform rect = canvasObject.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }

    private void ApplyResponsiveLayout()
    {
        if (rootRect == null)
        {
            return;
        }

        Vector2 screenSize = new Vector2(Screen.width, Screen.height);
        if (screenSize == lastScreenSize)
        {
            return;
        }

        lastScreenSize = screenSize;
        float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / Mathf.Max(1f, responsiveReferenceShortSide), 0.92f, 1.06f);
        Vector2 size = panelSize * scale;
        size.x = Mathf.Clamp(size.x, minimumPanelSize.x, maximumPanelSize.x);
        size.y = Mathf.Clamp(size.y, minimumPanelSize.y, maximumPanelSize.y);
        rootRect.sizeDelta = size;
        rootRect.anchoredPosition = anchoredPosition;
    }

    private static bool IsGameSceneActive()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return sceneName.Equals(GameSceneName, System.StringComparison.OrdinalIgnoreCase) ||
               sceneName.Equals(AltGameSceneName, System.StringComparison.OrdinalIgnoreCase);
    }
}
