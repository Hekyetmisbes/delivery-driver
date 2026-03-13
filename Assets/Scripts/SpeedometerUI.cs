using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeliveryDriver.Quest;
using DeliveryDriver.UI;

public class SpeedometerUI : MonoBehaviour
{
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
    [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.6f);
    [SerializeField] private Color lowSpeedColor = new Color(0f, 0.78f, 1f, 1f);
    [SerializeField] private Color highSpeedColor = new Color(1f, 0.06f, 0.06f, 1f);
    [SerializeField] private Color inactiveTickColor = new Color(1f, 1f, 1f, 0.34f);

    private RectTransform rootRect;
    private Image panelImage;
    private TextMeshProUGUI speedValueText;
    private TextMeshProUGUI unitText;
    private TextMeshProUGUI gearText;
    private Rigidbody playerRigidbody;
    private Image[] tickImages;
    private int frameCounter;
    private int lastDisplayedSpeed = -1;
    private string lastGearDisplay = "";
    private Vector2 lastScreenSize = Vector2.negativeInfinity;

    public void Initialize(Rigidbody rb)
    {
        playerRigidbody = rb;
        SyncUnitPreferenceFromSettings();
        EnsureUI();
    }

    private void OnEnable()
    {
        GameSettings.OnSpeedUnitChanged += HandleSpeedUnitChanged;
        SyncUnitPreferenceFromSettings();
    }

    private void OnDisable()
    {
        GameSettings.OnSpeedUnitChanged -= HandleSpeedUnitChanged;
    }

    private void Update()
    {
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
        panelImage.color = panelColor;
        panelImage.raycastTarget = false;
        panelImage.sprite = fallback;
        panelImage.type = Image.Type.Simple;

        BuildGaugeTicks(panelObject.transform, fallback);
        BuildSpeedTexts(panelObject.transform);
        ApplyResponsiveLayout();
    }

    private void UpdateUI()
    {
        if (!showSpeedometer)
        {
            if (rootRect != null)
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

        rootRect.gameObject.SetActive(true);
        UpdateTicks(normalized, activeColor);
        unitText.color = activeColor;
        speedValueText.color = activeColor;

        int roundedSpeed = Mathf.RoundToInt(shownSpeed);
        if (roundedSpeed != lastDisplayedSpeed)
        {
            lastDisplayedSpeed = roundedSpeed;
            speedValueText.SetText("{0:00}", roundedSpeed);
        }

        UpdateGearDisplay(speedMps, speedKmh);
    }

    private void BuildSpeedTexts(Transform parent)
    {
        GameObject speedTextObject = new GameObject("SpeedValue", typeof(RectTransform), typeof(TextMeshProUGUI));
        speedTextObject.transform.SetParent(parent, false);
        RectTransform speedRect = speedTextObject.GetComponent<RectTransform>();
        speedRect.anchorMin = new Vector2(0.5f, 0.42f);
        speedRect.anchorMax = new Vector2(0.5f, 0.42f);
        speedRect.pivot = new Vector2(0.5f, 0.5f);
        speedRect.sizeDelta = new Vector2(210f, 120f);

        speedValueText = speedTextObject.GetComponent<TextMeshProUGUI>();
        speedValueText.alignment = TextAlignmentOptions.Center;
        speedValueText.fontSize = Mathf.Max(48, speedFontSize);
        speedValueText.fontStyle = FontStyles.Bold;
        speedValueText.textWrappingMode = TextWrappingModes.NoWrap;
        speedValueText.outlineWidth = 0.2f;
        speedValueText.outlineColor = new Color(0f, 0f, 0f, 0.95f);
        speedValueText.text = "00";

        GameObject unitTextObject = new GameObject("SpeedUnit", typeof(RectTransform), typeof(TextMeshProUGUI));
        unitTextObject.transform.SetParent(parent, false);
        RectTransform unitRect = unitTextObject.GetComponent<RectTransform>();
        unitRect.anchorMin = new Vector2(0.72f, 0.12f);
        unitRect.anchorMax = new Vector2(0.72f, 0.12f);
        unitRect.pivot = new Vector2(0.5f, 0.5f);
        unitRect.sizeDelta = new Vector2(90f, 46f);

        unitText = unitTextObject.GetComponent<TextMeshProUGUI>();
        unitText.alignment = TextAlignmentOptions.Center;
        unitText.fontSize = Mathf.Max(16, unitFontSize);
        unitText.fontStyle = FontStyles.Bold;
        unitText.textWrappingMode = TextWrappingModes.NoWrap;
        unitText.outlineWidth = 0.16f;
        unitText.outlineColor = new Color(0f, 0f, 0f, 0.95f);
        unitText.text = displayMph ? "MPH" : "KM/H";

        // Gear text
        GameObject gearTextObject = new GameObject("GearIndicator", typeof(RectTransform), typeof(TextMeshProUGUI));
        gearTextObject.transform.SetParent(parent, false);
        RectTransform gearRect = gearTextObject.GetComponent<RectTransform>();
        gearRect.anchorMin = new Vector2(0.28f, 0.12f);
        gearRect.anchorMax = new Vector2(0.28f, 0.12f);
        gearRect.pivot = new Vector2(0.5f, 0.5f);
        gearRect.sizeDelta = new Vector2(50f, 36f);

        gearText = gearTextObject.GetComponent<TextMeshProUGUI>();
        gearText.alignment = TextAlignmentOptions.Center;
        gearText.fontSize = 28f;
        gearText.fontStyle = FontStyles.Bold;
        gearText.textWrappingMode = TextWrappingModes.NoWrap;
        gearText.outlineWidth = 0.16f;
        gearText.outlineColor = new Color(0f, 0f, 0f, 0.95f);
        gearText.text = "N";
        gearText.color = Color.white;
    }

    private void UpdateGearDisplay(float speedMps, float speedKmh)
    {
        if (gearText == null) return;

        string gear;
        bool isReverse = false;

        if (playerRigidbody != null)
        {
            float forwardDot = Vector3.Dot(playerRigidbody.linearVelocity, playerRigidbody.transform.forward);
            isReverse = forwardDot < -0.5f && speedMps > 0.5f;
        }

        if (isReverse)
        {
            gear = "R";
        }
        else if (speedKmh < 3f)
        {
            gear = "N";
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
            gearText.color = isReverse ? UIThemeConstants.Negative : Color.white;
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

            Image tickImage = tickObject.GetComponent<Image>();
            tickImage.sprite = fallbackSprite;
            tickImage.raycastTarget = false;
            tickImage.color = inactiveTickColor;

            tickImages[i] = tickImage;
        }
    }

    private void UpdateTicks(float normalizedSpeed, Color activeColor)
    {
        if (tickImages == null || tickImages.Length == 0)
        {
            return;
        }

        int activeTickCount = Mathf.RoundToInt(normalizedSpeed * tickImages.Length);
        for (int i = 0; i < tickImages.Length; i++)
        {
            if (tickImages[i] == null)
            {
                continue;
            }

            tickImages[i].color = i < activeTickCount ? activeColor : inactiveTickColor;
        }
    }

    private Color EvaluateSpeedColor(float normalizedSpeed, float shownSpeed)
    {
        float safeT = Mathf.Clamp01(normalizedSpeed);
        float threshold = displayMph ? Mathf.Max(1f, redThresholdMph) : Mathf.Max(1f, redThresholdKmh);

        if (shownSpeed >= threshold)
        {
            Color forcedRed = highSpeedColor;
            forcedRed.a = 1f;
            return forcedRed;
        }

        float thresholdT = Mathf.Clamp01(shownSpeed / threshold);
        float hue = Mathf.Lerp(0.56f, 0.04f, thresholdT);
        float sat = Mathf.Lerp(0.96f, 0.9f, safeT);
        float val = Mathf.Lerp(0.98f, 0.94f, safeT);
        Color visible = Color.HSVToRGB(hue, sat, val);
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
}
