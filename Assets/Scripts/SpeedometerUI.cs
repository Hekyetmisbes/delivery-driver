using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Standalone speedometer HUD component.
/// Extracted from DeliveryManager for separation of concerns.
/// Uses TMP zero-allocation SetText API and only updates when integer km/h changes.
/// </summary>
public class SpeedometerUI : MonoBehaviour
{
    private enum SpeedometerTier
    {
        Eco,
        Cruise,
        Fast,
        Max
    }

    [Header("Speedometer UI")]
    [SerializeField] private bool showSpeedometer = true;
    [SerializeField] private string speedometerLabel = "Hiz";
    [SerializeField] private Vector2 speedometerAnchoredPosition = new Vector2(-28f, 24f);
    [SerializeField] private int speedometerFontSize = 34;
    [SerializeField] private Color speedometerColor = Color.white;
    [SerializeField] private Vector2 speedometerPanelSize = new Vector2(320f, 116f);
    [SerializeField] private Color speedometerPanelBaseColor = new Color(0.07f, 0.08f, 0.1f, 0.72f);
    [SerializeField] private Color speedometerPanelEcoColor = new Color(0.08f, 0.2f, 0.12f, 0.78f);
    [SerializeField] private Color speedometerPanelCruiseColor = new Color(0.08f, 0.13f, 0.2f, 0.78f);
    [SerializeField] private Color speedometerPanelFastColor = new Color(0.24f, 0.18f, 0.06f, 0.8f);
    [SerializeField] private Color speedometerPanelMaxColor = new Color(0.3f, 0.08f, 0.08f, 0.84f);
    [SerializeField] private Color speedometerIconEcoColor = new Color(0.35f, 0.9f, 0.45f, 1f);
    [SerializeField] private Color speedometerIconCruiseColor = new Color(0.35f, 0.7f, 1f, 1f);
    [SerializeField] private Color speedometerIconFastColor = new Color(1f, 0.78f, 0.3f, 1f);
    [SerializeField] private Color speedometerIconMaxColor = new Color(1f, 0.32f, 0.32f, 1f);
    [SerializeField] private float cruiseThresholdKmh = 20f;
    [SerializeField] private float fastThresholdKmh = 60f;
    [SerializeField] private float maxThresholdKmh = 100f;
    [SerializeField] private Sprite speedIconEcoSprite;
    [SerializeField] private Sprite speedIconCruiseSprite;
    [SerializeField] private Sprite speedIconFastSprite;
    [SerializeField] private Sprite speedIconMaxSprite;
    [SerializeField] private bool useKenneySpeedometerSkin = true;
    [SerializeField] private Sprite speedometerPanelSprite;
    [SerializeField] private Sprite speedometerBarTrackSprite;
    [SerializeField] private Sprite speedometerBarFillSprite;
    [SerializeField] private Sprite speedometerDefaultIconSprite;
    [SerializeField] private int updateEveryNFrames = 3;

    private RectTransform panelRect;
    private Image panelImage;
    private Image iconImage;
    private Image barFillImage;
    private RectTransform barFillRect;
    private TextMeshProUGUI speedText;
    private Rigidbody playerRigidbody;
    private int frameCounter;
    private int lastDisplayedKmh = -1;

    // Pre-built format template for TMP SetText zero-allocation overload.
    // Uses TMP's {0} placeholder which accepts float args.
    private string tmProTemplate;

    public void Initialize(Rigidbody rb)
    {
        playerRigidbody = rb;
        EnsureUI();
    }

    private void Update()
    {
        frameCounter++;
        if (frameCounter % updateEveryNFrames == 0)
        {
            UpdateUI();
        }
    }

    private void EnsureUI()
    {
        if (!showSpeedometer || speedText != null)
        {
            return;
        }

        ResolveSpeedometerSkinSprites();
        Canvas targetCanvas = FindBestHudCanvas();
        if (targetCanvas == null)
        {
            GameObject canvasObject = new GameObject("GameplayHUDCanvas");
            targetCanvas = canvasObject.AddComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        Sprite fallback = DeliveryUiSpriteHelper.GetFallbackSprite();

        GameObject panelObject = new GameObject("SpeedometerPanel");
        panelObject.transform.SetParent(targetCanvas.transform, false);
        panelRect = panelObject.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = speedometerAnchoredPosition;
        panelRect.sizeDelta = speedometerPanelSize;

        panelImage = panelObject.AddComponent<Image>();
        panelImage.color = useKenneySpeedometerSkin ? new Color(1f, 1f, 1f, 0.98f) : speedometerPanelBaseColor;
        panelImage.raycastTarget = false;
        panelImage.sprite = speedometerPanelSprite != null ? speedometerPanelSprite : fallback;
        panelImage.type = speedometerPanelSprite != null ? Image.Type.Sliced : Image.Type.Simple;

        GameObject iconObject = new GameObject("SpeedometerIcon");
        iconObject.transform.SetParent(panelObject.transform, false);
        RectTransform iconRect = iconObject.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(14f, 10f);
        iconRect.sizeDelta = new Vector2(40f, 40f);

        iconImage = iconObject.AddComponent<Image>();
        iconImage.raycastTarget = false;
        iconImage.sprite = speedIconEcoSprite != null
            ? speedIconEcoSprite
            : (speedometerDefaultIconSprite != null ? speedometerDefaultIconSprite : fallback);
        iconImage.color = speedometerIconEcoColor;
        iconImage.preserveAspect = true;

        GameObject speedTextObject = new GameObject("SpeedometerText");
        speedTextObject.transform.SetParent(panelObject.transform, false);
        RectTransform textRect = speedTextObject.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = new Vector2(60f, 28f);
        textRect.offsetMax = new Vector2(-14f, -8f);

        speedText = speedTextObject.AddComponent<TextMeshProUGUI>();
        speedText.fontSize = Mathf.Max(14, speedometerFontSize);
        speedText.color = speedometerColor;
        speedText.alignment = TextAlignmentOptions.BottomRight;
        speedText.enableAutoSizing = false;

        // Build TMP template once — {0} is the zero-alloc float placeholder
        int bigSize = Mathf.Max(18, speedometerFontSize + 4);
        tmProTemplate = $"{speedometerLabel}\n<size={bigSize}>{{0}}</size> <size=20>km/h</size>";
        speedText.SetText(tmProTemplate, 0f);

        GameObject barTrackObject = new GameObject("SpeedometerBarTrack");
        barTrackObject.transform.SetParent(panelObject.transform, false);
        RectTransform barTrackRect = barTrackObject.AddComponent<RectTransform>();
        barTrackRect.anchorMin = new Vector2(0f, 0f);
        barTrackRect.anchorMax = new Vector2(1f, 0f);
        barTrackRect.pivot = new Vector2(0.5f, 0f);
        barTrackRect.offsetMin = new Vector2(16f, 10f);
        barTrackRect.offsetMax = new Vector2(-16f, 24f);

        Image barTrackImage = barTrackObject.AddComponent<Image>();
        barTrackImage.raycastTarget = false;
        barTrackImage.color = new Color(1f, 1f, 1f, 0.78f);
        barTrackImage.sprite = speedometerBarTrackSprite != null ? speedometerBarTrackSprite : fallback;
        barTrackImage.type = speedometerBarTrackSprite != null ? Image.Type.Sliced : Image.Type.Simple;

        GameObject barFillObject = new GameObject("SpeedometerBarFill");
        barFillObject.transform.SetParent(barTrackObject.transform, false);
        barFillRect = barFillObject.AddComponent<RectTransform>();
        barFillRect.anchorMin = new Vector2(0f, 0f);
        barFillRect.anchorMax = new Vector2(0f, 1f);
        barFillRect.pivot = new Vector2(0f, 0.5f);
        barFillRect.offsetMin = new Vector2(3f, 3f);
        barFillRect.offsetMax = new Vector2(0f, -3f);
        barFillRect.sizeDelta = new Vector2(0f, 0f);

        barFillImage = barFillObject.AddComponent<Image>();
        barFillImage.raycastTarget = false;
        barFillImage.color = speedometerIconEcoColor;
        barFillImage.sprite = speedometerBarFillSprite != null ? speedometerBarFillSprite : fallback;
        barFillImage.type = speedometerBarFillSprite != null ? Image.Type.Sliced : Image.Type.Simple;
    }

    private void UpdateUI()
    {
        if (!showSpeedometer)
        {
            if (panelRect != null)
            {
                panelRect.gameObject.SetActive(false);
            }
            return;
        }

        if (speedText == null || panelRect == null)
        {
            EnsureUI();
        }

        if (speedText == null || panelRect == null)
        {
            return;
        }

        float speedKmh = playerRigidbody != null
            ? playerRigidbody.linearVelocity.magnitude * 3.6f
            : 0f;

        SpeedometerTier tier = EvaluateTier(speedKmh);
        ApplyTierVisual(tier);
        UpdateProgress(speedKmh);

        panelRect.gameObject.SetActive(true);

        // Zero-allocation: only update text when integer value changes
        int currentKmh = Mathf.RoundToInt(speedKmh);
        if (currentKmh != lastDisplayedKmh)
        {
            lastDisplayedKmh = currentKmh;
            speedText.SetText(tmProTemplate, (float)currentKmh);
        }
    }

    private SpeedometerTier EvaluateTier(float speedKmh)
    {
        if (speedKmh >= maxThresholdKmh) return SpeedometerTier.Max;
        if (speedKmh >= fastThresholdKmh) return SpeedometerTier.Fast;
        if (speedKmh >= cruiseThresholdKmh) return SpeedometerTier.Cruise;
        return SpeedometerTier.Eco;
    }

    private void ApplyTierVisual(SpeedometerTier tier)
    {
        if (panelImage == null || iconImage == null)
        {
            return;
        }

        Sprite fallback = DeliveryUiSpriteHelper.GetFallbackSprite();
        Sprite selectedSprite = fallback;
        switch (tier)
        {
            case SpeedometerTier.Max:
                panelImage.color = useKenneySpeedometerSkin ? new Color(1f, 0.86f, 0.86f, 0.98f) : speedometerPanelMaxColor;
                iconImage.color = speedometerIconMaxColor;
                selectedSprite = speedIconMaxSprite != null ? speedIconMaxSprite : fallback;
                break;
            case SpeedometerTier.Fast:
                panelImage.color = useKenneySpeedometerSkin ? new Color(1f, 0.95f, 0.84f, 0.98f) : speedometerPanelFastColor;
                iconImage.color = speedometerIconFastColor;
                selectedSprite = speedIconFastSprite != null ? speedIconFastSprite : fallback;
                break;
            case SpeedometerTier.Cruise:
                panelImage.color = useKenneySpeedometerSkin ? new Color(0.88f, 0.94f, 1f, 0.98f) : speedometerPanelCruiseColor;
                iconImage.color = speedometerIconCruiseColor;
                selectedSprite = speedIconCruiseSprite != null ? speedIconCruiseSprite : fallback;
                break;
            default:
                panelImage.color = useKenneySpeedometerSkin ? new Color(0.9f, 1f, 0.9f, 0.98f) : speedometerPanelEcoColor;
                iconImage.color = speedometerIconEcoColor;
                selectedSprite = speedIconEcoSprite != null ? speedIconEcoSprite : fallback;
                break;
        }

        iconImage.sprite = selectedSprite;
        iconImage.type = Image.Type.Simple;
        iconImage.fillAmount = 1f;
        if (barFillImage != null)
        {
            barFillImage.color = iconImage.color;
        }
    }

    private void UpdateProgress(float speedKmh)
    {
        if (barFillRect == null)
        {
            return;
        }

        RectTransform trackRect = barFillRect.parent as RectTransform;
        if (trackRect == null)
        {
            return;
        }

        float maxVisualSpeed = Mathf.Max(10f, maxThresholdKmh * 1.25f);
        float normalized = Mathf.Clamp01(speedKmh / maxVisualSpeed);
        float availableWidth = Mathf.Max(0f, trackRect.rect.width - 6f);
        barFillRect.sizeDelta = new Vector2(availableWidth * normalized, 0f);
    }

    private void ResolveSpeedometerSkinSprites()
    {
        if (!useKenneySpeedometerSkin)
        {
            return;
        }

        speedometerPanelSprite ??= RuntimeUiSkinLoader.LoadSprite(
            "UI/Kenney/panel_bg",
            "Assets/kenney_ui-pack/PNG/Grey/Double/button_rectangle_depth_flat.png");

        speedometerBarTrackSprite ??= RuntimeUiSkinLoader.LoadSprite(
            "UI/Kenney/speed_track",
            "Assets/kenney_ui-pack/PNG/Grey/Default/slide_horizontal_grey_section_wide.png");

        speedometerBarFillSprite ??= RuntimeUiSkinLoader.LoadSprite(
            "UI/Kenney/speed_fill",
            "Assets/kenney_ui-pack/PNG/Blue/Default/slide_horizontal_color_section_wide.png");

        speedometerDefaultIconSprite ??= RuntimeUiSkinLoader.LoadSprite(
            "UI/Kenney/speed_icon",
            "Assets/kenney_ui-pack/PNG/Blue/Default/icon_circle.png");
    }

    private Canvas FindBestHudCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null && canvases[i].isActiveAndEnabled && canvases[i].renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return canvases[i];
            }
        }

        return FindFirstObjectByType<Canvas>();
    }
}
