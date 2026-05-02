using DeliveryDriver.Quest;
using DeliveryDriver.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Popup UI for refueling at the fuel station.
/// Shows fuel gauge, cost, and refuel options.
/// </summary>
public class FuelStationUI : MonoBehaviour
{
    private static FuelStationUI activeUI;

    public static bool IsShowing => activeUI != null;

    private FuelSystem fuelSystem;
    private CanvasGroup canvasGroup;
    private TextMeshProUGUI fuelInfoText;
    private TextMeshProUGUI costText;
    private Slider fuelSlider;
    private Button fullRefuelButton;
    private Button halfRefuelButton;
    private Button closeButton;
    private bool isClosing;

    public static void Show(FuelSystem system)
    {
        if (activeUI != null || system == null) return;

        // Don't show if tank is full
        if (system.FuelNormalized >= 0.99f)
        {
            NotificationQueue.Enqueue(
                LocalizationTable.Get("fuel_tank_full_title"),
                LocalizationTable.Get("fuel_tank_full"),
                2f,
                NotificationPriority.Normal);
            return;
        }

        GameObject root = new GameObject(
            "FuelStationUI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup));

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 29000;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        FuelStationUI ui = root.AddComponent<FuelStationUI>();
        activeUI = ui;
        ui.fuelSystem = system;
        ui.canvasGroup = root.GetComponent<CanvasGroup>();
        ui.BuildUI(root.transform);

        ui.canvasGroup.alpha = 0f;
        ui.canvasGroup.interactable = true;
        ui.canvasGroup.blocksRaycasts = true;
        UIAnimationHelper.FadeIn(ui, ui.canvasGroup, 0.2f);

        Time.timeScale = 0f;
    }

    private void BuildUI(Transform parent)
    {
        Sprite panelSprite = RuntimeUiSkinLoader.LoadSprite(
            "UI/Kenney/panel_bg",
            "Assets/kenney_ui-pack/PNG/Grey/Double/button_rectangle_depth_flat.png");
        Sprite buttonSprite = RuntimeUiSkinLoader.LoadSprite(
            "UI/Kenney/button_bg",
            "Assets/kenney_ui-pack/PNG/Grey/Default/button_rectangle_depth_flat.png");

        // Dark overlay
        GameObject overlay = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(parent, false);
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        Button overlayBtn = overlay.AddComponent<Button>();
        overlayBtn.transition = Selectable.Transition.None;
        overlayBtn.onClick.AddListener(Close);

        // Main panel
        GameObject panel = new GameObject("FuelPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(parent, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(520f, 380f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.04f, 0.08f, 0.14f, 0.97f);
        if (panelSprite != null)
        {
            panelImage.sprite = panelSprite;
            panelImage.type = Image.Type.Sliced;
        }

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 24, 24);
        layout.spacing = 12f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        // Fuel icon + title
        CreateText(panel.transform, "Title", "[F] " + LocalizationTable.Get("fuel_station_title"),
            30, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, 42f);

        // Fuel info text
        GameObject infoObj = CreateTextElement(panel.transform, "FuelInfo", "", 22,
            FontStyles.Normal, TextAlignmentOptions.Center, UIThemeConstants.TextSecondary, 32f);
        fuelInfoText = infoObj.GetComponent<TextMeshProUGUI>();

        // Fuel slider
        BuildFuelSlider(panel.transform);

        // Cost text
        GameObject costObj = CreateTextElement(panel.transform, "CostInfo", "", 20,
            FontStyles.Normal, TextAlignmentOptions.Center, UIThemeConstants.MoneyText, 28f);
        costText = costObj.GetComponent<TextMeshProUGUI>();

        // Button row
        GameObject buttonRow = new GameObject("ButtonRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        buttonRow.transform.SetParent(panel.transform, false);
        HorizontalLayoutGroup btnLayout = buttonRow.GetComponent<HorizontalLayoutGroup>();
        btnLayout.spacing = 12f;
        btnLayout.childControlWidth = true;
        btnLayout.childControlHeight = true;
        btnLayout.childForceExpandWidth = true;
        btnLayout.childForceExpandHeight = false;
        buttonRow.GetComponent<LayoutElement>().minHeight = 54f;

        // Close button
        closeButton = CreateButton(buttonRow.transform, LocalizationTable.Get("cancel"),
            UIThemeConstants.ButtonNeutral, buttonSprite);
        closeButton.onClick.AddListener(Close);
        UIButtonEnhancer.EnhanceButton(closeButton);

        // Half refuel button
        halfRefuelButton = CreateButton(buttonRow.transform, LocalizationTable.Get("fuel_half_tank"),
            UIThemeConstants.ButtonBlue, buttonSprite);
        halfRefuelButton.onClick.AddListener(OnHalfRefuelClicked);
        UIButtonEnhancer.EnhanceButton(halfRefuelButton);

        // Full refuel button
        fullRefuelButton = CreateButton(buttonRow.transform, LocalizationTable.Get("fuel_full_tank"),
            UIThemeConstants.ButtonGreen, buttonSprite);
        fullRefuelButton.onClick.AddListener(OnFullRefuelClicked);
        UIButtonEnhancer.EnhanceButton(fullRefuelButton);

        UIAnimationHelper.ScaleIn(this, panelRect, 0.25f);

        UpdateDisplay();
    }

    private void BuildFuelSlider(Transform parent)
    {
        GameObject sliderObj = new GameObject("FuelSlider", typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
        sliderObj.transform.SetParent(parent, false);
        sliderObj.GetComponent<LayoutElement>().minHeight = 30f;

        Slider slider = sliderObj.GetComponent<Slider>();
        slider.interactable = false;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = fuelSystem != null ? fuelSystem.FuelNormalized : 0f;

        // Background
        GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgObj.transform.SetParent(sliderObj.transform, false);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.35f);
        bgRect.anchorMax = new Vector2(1f, 0.65f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        bgObj.GetComponent<Image>().color = new Color(0.15f, 0.18f, 0.25f, 1f);

        // Fill area
        GameObject fillArea = new GameObject("FillArea", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.35f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.65f);
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = Vector2.zero;

        // Fill
        GameObject fillObj = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObj.transform.SetParent(fillArea.transform, false);
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Image fillImage = fillObj.GetComponent<Image>();
        float fuel = fuelSystem != null ? fuelSystem.FuelNormalized : 0.5f;
        fillImage.color = fuel > 0.3f
            ? new Color(0.15f, 0.85f, 0.35f, 1f)
            : new Color(1f, 0.35f, 0.2f, 1f);

        slider.fillRect = fillRect;
        fuelSlider = slider;
    }

    private void UpdateDisplay()
    {
        if (fuelSystem == null) return;

        float fuel = fuelSystem.CurrentFuel;
        float max = fuelSystem.MaxFuel;
        float norm = fuelSystem.FuelNormalized;

        PlayerProgressionManager progression = PlayerProgressionManager.Instance;
        int balance = progression != null ? progression.CurrentMoney : 0;

        if (fuelInfoText != null)
        {
            fuelInfoText.text = LocalizationTable.Format("fuel_info", fuel.ToString("F1"), max.ToString("F0"), (norm * 100f).ToString("F0"));
        }

        if (fuelSlider != null)
        {
            fuelSlider.value = norm;
        }

        int fullCost = fuelSystem.CalculateFullRefuelCost();
        int halfLiters = Mathf.CeilToInt((max - fuel) * 0.5f);
        int halfCost = fuelSystem.CalculateRefuelCost(halfLiters);

        if (costText != null)
        {
            costText.text = LocalizationTable.Format("fuel_cost_info",
                fullCost.ToString(),
                balance.ToString());
        }

        // Update button interactability
        if (fullRefuelButton != null)
        {
            fullRefuelButton.interactable = fullCost <= balance && norm < 0.99f;
        }

        if (halfRefuelButton != null)
        {
            halfRefuelButton.interactable = halfCost <= balance && norm < 0.99f;
        }
    }

    private void OnFullRefuelClicked()
    {
        if (fuelSystem == null || isClosing) return;
        fuelSystem.RefuelToFull();
        UpdateDisplay();

        if (fuelSystem.FuelNormalized >= 0.99f)
        {
            Close();
        }
    }

    private void OnHalfRefuelClicked()
    {
        if (fuelSystem == null || isClosing) return;
        float halfLiters = (fuelSystem.MaxFuel - fuelSystem.CurrentFuel) * 0.5f;
        fuelSystem.Refuel(halfLiters);
        UpdateDisplay();
    }

    private void Close()
    {
        if (isClosing) return;
        isClosing = true;

        Time.timeScale = 1f;

        if (activeUI == this)
        {
            activeUI = null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            UIAnimationHelper.FadeOut(this, canvasGroup, 0.15f, () => Destroy(gameObject));
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (isClosing) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            Close();
        }
    }

    private void OnDestroy()
    {
        if (activeUI == this)
        {
            activeUI = null;
        }
        // Ensure timeScale is restored
        if (Time.timeScale < 0.01f)
        {
            Time.timeScale = 1f;
        }
    }

    private void CreateText(Transform parent, string name, string content, float fontSize,
        FontStyles style, TextAlignmentOptions alignment, Color color, float minHeight)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<LayoutElement>().minHeight = minHeight;

        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        if (TMP_Settings.defaultFontAsset != null)
        {
            text.font = TMP_Settings.defaultFontAsset;
        }
    }

    private GameObject CreateTextElement(Transform parent, string name, string content, float fontSize,
        FontStyles style, TextAlignmentOptions alignment, Color color, float minHeight)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<LayoutElement>().minHeight = minHeight;

        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        if (TMP_Settings.defaultFontAsset != null)
        {
            text.font = TMP_Settings.defaultFontAsset;
        }

        return obj;
    }

    private Button CreateButton(Transform parent, string label, Color color, Sprite bgSprite)
    {
        GameObject btnObj = new GameObject($"{label}Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        btnObj.transform.SetParent(parent, false);
        btnObj.GetComponent<LayoutElement>().minHeight = 48f;

        Image btnImage = btnObj.GetComponent<Image>();
        btnImage.color = color;
        if (bgSprite != null)
        {
            btnImage.sprite = bgSprite;
            btnImage.type = Image.Type.Sliced;
        }

        GameObject textObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 20f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null)
        {
            text.font = TMP_Settings.defaultFontAsset;
        }

        return btnObj.GetComponent<Button>();
    }
}
