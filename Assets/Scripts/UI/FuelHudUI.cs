using DeliveryDriver.Company;
using DeliveryDriver.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// HUD element that displays fuel level, low fuel warning, "Press E" prompt,
/// and the out-of-fuel return home popup.
/// </summary>
public class FuelHudUI : MonoBehaviour
{
    private const string GameSceneName = "Game";
    private const string FuelHudRootName = "FuelHudRoot";
    private const string FuelWarningLabelName = "FuelWarningLabel";

    [SerializeField] private Vector2 anchoredPosition = new Vector2(-40f, 270f);
    [SerializeField] private Vector2 panelSize = new Vector2(220f, 70f);
    [SerializeField] private Color fuelFullColor = new Color(0.15f, 0.85f, 0.35f, 1f);
    [SerializeField] private Color fuelLowColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color fuelEmptyColor = new Color(1f, 0.25f, 0.2f, 1f);
    [SerializeField] private Color warningBackgroundColor = new Color(0.78f, 0.55f, 0.08f, 0.92f);
    [SerializeField] private Color warningTextColor = new Color(1f, 0.97f, 0.92f, 1f);
    [SerializeField, Min(1f)] private float lowFuelLitersThreshold = 25f;
    [SerializeField, Min(0.1f)] private float lowFuelWarningBlinkSpeed = 6f;

    private RectTransform hudRoot;
    private Image fuelBarFill;
    private TextMeshProUGUI fuelPercentText;
    private TextMeshProUGUI fuelLabelText;

    private CanvasGroup warningCanvasGroup;
    private TextMeshProUGUI warningText;

    private RectTransform promptRoot;
    private TextMeshProUGUI promptText;
    private CanvasGroup promptCanvasGroup;

    private FuelSystem fuelSystem;
    private CarController activeVehicle;
    private bool subscribedToFuelEvents;
    private bool outOfFuelPopupShown;
    private float lastFuelNormalized = -1f;
    private bool lastNearStation;
    private bool lowFuelBlinkActive;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        PlayerVehicleManager.ActiveVehicleChanged += OnVehicleChanged;
        RefreshBindings();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        PlayerVehicleManager.ActiveVehicleChanged -= OnVehicleChanged;
        UnsubscribeFuelEvents();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshBindings();
    }

    private void OnVehicleChanged(CarController controller)
    {
        RefreshBindings();
    }

    private void Update()
    {
        if (!IsGameSceneActive()) return;

        ResolveFuelSystem();
        if (fuelSystem == null)
        {
            SetHudVisible(false);
            return;
        }

        EnsureHud();
        UpdateFuelBar();
        UpdatePrompt();
        UpdateLowFuelWarning();
    }

    private void RefreshBindings()
    {
        ResolveFuelSystem();
        EnsureHud();
        EnsureWarningOverlay();
    }

    private void ResolveFuelSystem()
    {
        FuelSystem newFuelSystem = FuelSystem.Instance;

        if (newFuelSystem == null)
        {
            // Try to find one on the active vehicle
            PlayerVehicleManager vehicleManager = PlayerVehicleManager.Instance;
            if (vehicleManager != null && vehicleManager.ActiveVehicleController != null)
            {
                newFuelSystem = vehicleManager.ActiveVehicleController.GetComponent<FuelSystem>();
            }

            if (newFuelSystem == null)
            {
                CarController car = FindFirstObjectByType<CarController>();
                if (car != null)
                {
                    newFuelSystem = car.GetComponent<FuelSystem>();
                }
            }
        }

        if (newFuelSystem != fuelSystem)
        {
            UnsubscribeFuelEvents();
            fuelSystem = newFuelSystem;
            SubscribeFuelEvents();
            outOfFuelPopupShown = false;
        }
    }

    private void SubscribeFuelEvents()
    {
        if (fuelSystem == null || subscribedToFuelEvents) return;
        fuelSystem.OnLowFuelWarning += HandleLowFuelWarning;
        fuelSystem.OnFuelEmpty += HandleFuelEmpty;
        fuelSystem.OnFuelRefilled += HandleFuelRefilled;
        fuelSystem.OnNearFuelStation += HandleNearFuelStation;
        subscribedToFuelEvents = true;
    }

    private void UnsubscribeFuelEvents()
    {
        if (fuelSystem == null || !subscribedToFuelEvents) return;
        fuelSystem.OnLowFuelWarning -= HandleLowFuelWarning;
        fuelSystem.OnFuelEmpty -= HandleFuelEmpty;
        fuelSystem.OnFuelRefilled -= HandleFuelRefilled;
        fuelSystem.OnNearFuelStation -= HandleNearFuelStation;
        subscribedToFuelEvents = false;
    }

    private void HandleLowFuelWarning()
    {
        ShowWarning(LocalizationTable.Get("fuel_low_warning"));
    }

    private void HandleFuelEmpty()
    {
        ShowReturnHomePopup();
    }

    private void HandleFuelRefilled()
    {
        SetWarningVisible(false);
        lowFuelBlinkActive = false;
        outOfFuelPopupShown = false;
    }

    private void HandleNearFuelStation(bool entered)
    {
        // Prompt visibility is handled in UpdatePrompt
    }

    private void ShowReturnHomePopup()
    {
        if (outOfFuelPopupShown) return;
        outOfFuelPopupShown = true;

        if (fuelSystem == null) return;

        int cost = fuelSystem.CalculateReturnHomeCost();
        string title = LocalizationTable.Get("fuel_empty_title");
        string message = cost > 0
            ? LocalizationTable.Format("fuel_empty_return_home", cost.ToString())
            : LocalizationTable.Get("fuel_empty_return_home_free");

        ConfirmationDialog.Show(
            title,
            message,
            () =>
            {
                fuelSystem.ReturnHome();
                outOfFuelPopupShown = false;
            },
            () =>
            {
                // Player dismissed - show again after a short delay
                outOfFuelPopupShown = false;
            });
    }

    private void ShowWarning(string text)
    {
        EnsureWarningOverlay();
        if (warningText != null)
        {
            warningText.text = text;
        }
        SetWarningVisible(true);
    }

    private void SetWarningVisible(bool visible)
    {
        if (warningCanvasGroup == null) return;
        warningCanvasGroup.alpha = visible ? 1f : 0f;
    }

    private void SetHudVisible(bool visible)
    {
        if (hudRoot != null) hudRoot.gameObject.SetActive(visible);
    }

    private void EnsureHud()
    {
        if (hudRoot != null) return;

        Canvas targetCanvas = GetOrCreateHudCanvas();
        if (targetCanvas == null) return;

        // Panel
        GameObject panelObj = new GameObject(FuelHudRootName, typeof(RectTransform), typeof(Image));
        panelObj.transform.SetParent(targetCanvas.transform, false);

        hudRoot = panelObj.GetComponent<RectTransform>();
        hudRoot.anchorMin = new Vector2(1f, 0f);
        hudRoot.anchorMax = new Vector2(1f, 0f);
        hudRoot.pivot = new Vector2(1f, 0f);
        hudRoot.anchoredPosition = anchoredPosition;
        hudRoot.sizeDelta = panelSize;

        Image panelImage = panelObj.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.55f);
        panelImage.raycastTarget = false;

        // Fuel icon + label
        GameObject labelObj = new GameObject("FuelLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(panelObj.transform, false);
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.55f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(10f, 0f);
        labelRect.offsetMax = new Vector2(-10f, -4f);

        fuelLabelText = labelObj.GetComponent<TextMeshProUGUI>();
        fuelLabelText.text = "[F] " + LocalizationTable.Get("fuel_label");
        fuelLabelText.fontSize = 18f;
        fuelLabelText.fontStyle = FontStyles.Bold;
        fuelLabelText.alignment = TextAlignmentOptions.Left;
        fuelLabelText.color = Color.white;
        fuelLabelText.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null) fuelLabelText.font = TMP_Settings.defaultFontAsset;

        // Fuel % text
        GameObject percentObj = new GameObject("FuelPercent", typeof(RectTransform), typeof(TextMeshProUGUI));
        percentObj.transform.SetParent(panelObj.transform, false);
        RectTransform percentRect = percentObj.GetComponent<RectTransform>();
        percentRect.anchorMin = new Vector2(0.65f, 0.55f);
        percentRect.anchorMax = new Vector2(1f, 1f);
        percentRect.offsetMin = new Vector2(0f, 0f);
        percentRect.offsetMax = new Vector2(-10f, -4f);

        fuelPercentText = percentObj.GetComponent<TextMeshProUGUI>();
        fuelPercentText.text = "100%";
        fuelPercentText.fontSize = 18f;
        fuelPercentText.fontStyle = FontStyles.Bold;
        fuelPercentText.alignment = TextAlignmentOptions.Right;
        fuelPercentText.color = fuelFullColor;
        fuelPercentText.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null) fuelPercentText.font = TMP_Settings.defaultFontAsset;

        // Fuel bar background
        GameObject barBg = new GameObject("FuelBarBg", typeof(RectTransform), typeof(Image));
        barBg.transform.SetParent(panelObj.transform, false);
        RectTransform barBgRect = barBg.GetComponent<RectTransform>();
        barBgRect.anchorMin = new Vector2(0f, 0.1f);
        barBgRect.anchorMax = new Vector2(1f, 0.48f);
        barBgRect.offsetMin = new Vector2(10f, 0f);
        barBgRect.offsetMax = new Vector2(-10f, 0f);
        barBg.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.2f, 1f);
        barBg.GetComponent<Image>().raycastTarget = false;

        // Fuel bar fill
        GameObject barFill = new GameObject("FuelBarFill", typeof(RectTransform), typeof(Image));
        barFill.transform.SetParent(barBg.transform, false);
        RectTransform barFillRect = barFill.GetComponent<RectTransform>();
        barFillRect.anchorMin = Vector2.zero;
        barFillRect.anchorMax = Vector2.one;
        barFillRect.offsetMin = Vector2.zero;
        barFillRect.offsetMax = Vector2.zero;
        barFillRect.pivot = new Vector2(0f, 0.5f);

        fuelBarFill = barFill.GetComponent<Image>();
        fuelBarFill.color = fuelFullColor;
        fuelBarFill.raycastTarget = false;

        // Prompt (Press E)
        BuildPrompt(targetCanvas.transform);
    }

    private void BuildPrompt(Transform canvasRoot)
    {
        GameObject promptObj = new GameObject("FuelStationPrompt", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        promptObj.transform.SetParent(canvasRoot, false);

        promptRoot = promptObj.GetComponent<RectTransform>();
        promptRoot.anchorMin = new Vector2(0.5f, 0.25f);
        promptRoot.anchorMax = new Vector2(0.5f, 0.25f);
        promptRoot.pivot = new Vector2(0.5f, 0.5f);
        promptRoot.sizeDelta = new Vector2(320f, 60f);

        Image promptBg = promptObj.GetComponent<Image>();
        promptBg.color = new Color(0.06f, 0.1f, 0.16f, 0.88f);
        promptBg.raycastTarget = false;

        promptCanvasGroup = promptObj.GetComponent<CanvasGroup>();
        promptCanvasGroup.alpha = 0f;
        promptCanvasGroup.interactable = false;
        promptCanvasGroup.blocksRaycasts = false;

        GameObject textObj = new GameObject("PromptText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(promptObj.transform, false);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 4f);
        textRect.offsetMax = new Vector2(-12f, -4f);

        promptText = textObj.GetComponent<TextMeshProUGUI>();
        promptText.text = LocalizationTable.Get("fuel_press_e");
        promptText.fontSize = 24f;
        promptText.fontStyle = FontStyles.Bold;
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.color = new Color(0.3f, 0.95f, 0.45f, 1f);
        promptText.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null) promptText.font = TMP_Settings.defaultFontAsset;

        promptObj.SetActive(true);
    }

    private void UpdateFuelBar()
    {
        if (fuelSystem == null || fuelBarFill == null) return;

        float norm = fuelSystem.FuelNormalized;
        if (Mathf.Approximately(norm, lastFuelNormalized)) return;
        lastFuelNormalized = norm;

        // Update bar scale
        Vector3 scale = fuelBarFill.rectTransform.localScale;
        scale.x = norm;
        fuelBarFill.rectTransform.localScale = scale;

        // Update color
        Color barColor;
        if (norm <= 0.01f)
            barColor = fuelEmptyColor;
        else if (norm <= 0.15f)
            barColor = fuelLowColor;
        else
            barColor = Color.Lerp(fuelLowColor, fuelFullColor, Mathf.InverseLerp(0.15f, 0.5f, norm));

        fuelBarFill.color = barColor;

        // Percent text
        if (fuelPercentText != null)
        {
            int pct = Mathf.RoundToInt(norm * 100f);
            fuelPercentText.text = $"{pct}%";
            fuelPercentText.color = barColor;
        }

        // Show/hide HUD
        SetHudVisible(true);

        // If fuel is empty and popup not shown yet
        if (fuelSystem.IsOutOfFuel && !outOfFuelPopupShown)
        {
            ShowReturnHomePopup();
        }
    }

    private void UpdatePrompt()
    {
        if (fuelSystem == null || promptCanvasGroup == null) return;

        bool nearStation = fuelSystem.IsNearFuelStation;
        bool showPrompt = nearStation && !fuelSystem.IsOutOfFuel && !FuelStationUI.IsShowing;

        if (showPrompt != lastNearStation)
        {
            lastNearStation = showPrompt;
            promptCanvasGroup.alpha = showPrompt ? 1f : 0f;
        }
    }

    private void UpdateLowFuelWarning()
    {
        if (fuelSystem == null)
        {
            if (lowFuelBlinkActive)
            {
                lowFuelBlinkActive = false;
                SetWarningVisible(false);
            }
            return;
        }

        EnsureWarningOverlay();
        if (warningCanvasGroup == null)
        {
            return;
        }

        bool shouldBlinkWarning = !fuelSystem.IsOutOfFuel && fuelSystem.CurrentFuel < lowFuelLitersThreshold;
        if (!shouldBlinkWarning)
        {
            if (lowFuelBlinkActive)
            {
                lowFuelBlinkActive = false;
                SetWarningVisible(false);
            }
            return;
        }

        lowFuelBlinkActive = true;
        if (warningText != null)
        {
            warningText.text = $"{LocalizationTable.Get("fuel_low_warning")} ({fuelSystem.CurrentFuel:F1}L)";
        }

        float blink = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * lowFuelWarningBlinkSpeed));
        warningCanvasGroup.alpha = blink;
    }

    private void EnsureWarningOverlay()
    {
        if (warningCanvasGroup != null && warningCanvasGroup.gameObject != null && warningText != null)
            return;

        Canvas canvas = GlobalUiCoordinator.PrimaryCanvas;
        if (canvas == null) return;

        Transform existing = canvas.transform.Find(FuelWarningLabelName);
        if (existing != null)
        {
            warningCanvasGroup = existing.GetComponent<CanvasGroup>();
            warningText = existing.GetComponentInChildren<TextMeshProUGUI>(true);
            return;
        }

        GameObject warningObj = new GameObject(FuelWarningLabelName, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        warningObj.transform.SetParent(canvas.transform, false);

        RectTransform warningRect = warningObj.GetComponent<RectTransform>();
        warningRect.anchorMin = new Vector2(0.5f, 1f);
        warningRect.anchorMax = new Vector2(0.5f, 1f);
        warningRect.pivot = new Vector2(0.5f, 1f);
        warningRect.anchoredPosition = new Vector2(0f, -92f); // Below the border warning
        warningRect.sizeDelta = new Vector2(460f, 52f);

        Image warningBg = warningObj.GetComponent<Image>();
        warningBg.color = warningBackgroundColor;
        warningBg.raycastTarget = false;

        warningCanvasGroup = warningObj.GetComponent<CanvasGroup>();
        warningCanvasGroup.alpha = 0f;
        warningCanvasGroup.interactable = false;
        warningCanvasGroup.blocksRaycasts = false;

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(warningObj.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 8f);
        textRect.offsetMax = new Vector2(-16f, -8f);

        warningText = textObject.GetComponent<TextMeshProUGUI>();
        warningText.text = LocalizationTable.Get("fuel_low_warning");
        warningText.fontSize = 24f;
        warningText.fontStyle = FontStyles.Bold;
        warningText.alignment = TextAlignmentOptions.Center;
        warningText.color = warningTextColor;
        warningText.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null) warningText.font = TMP_Settings.defaultFontAsset;

        warningObj.SetActive(true);
        SetWarningVisible(false);
    }

    private Canvas GetOrCreateHudCanvas()
    {
        GameObject existing = GameObject.Find("GameplayHUDCanvas");
        if (existing != null)
        {
            Canvas c = existing.GetComponent<Canvas>();
            if (c != null) return c;
        }

        // Create one
        GameObject canvasObj = new GameObject("GameplayHUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 220;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static bool IsGameSceneActive()
    {
        return SceneManager.GetActiveScene().name.Equals(GameSceneName, System.StringComparison.OrdinalIgnoreCase);
    }
}
