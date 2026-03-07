using DeliveryDriver.Quest;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuRuntimeUI : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string gameSceneName = "Game";

    [Header("Background")]
    [SerializeField] private string backgroundResourcesPath = "UI/MainMenu/mainmenu_bg";
    [SerializeField] private string backgroundAssetPath = "Assets/Images/MainMenuImage.png";

    [Header("Credits")]
    [SerializeField] private string developerName = "hekye";

    [Header("Main Panel Layout")]
    [SerializeField] private Vector2 mainPanelSize = new Vector2(560f, 380f);
    [SerializeField] private Vector2 mainPanelOffset = new Vector2(0f, -110f);

    [Header("Kenney Skin")]
    [SerializeField] private Sprite panelBackgroundSprite;
    [SerializeField] private Sprite buttonBackgroundSprite;
    [SerializeField] private Sprite sliderBackgroundSprite;
    [SerializeField] private Sprite sliderFillSprite;
    [SerializeField] private Sprite sliderHandleSprite;
    [SerializeField] private Sprite dropdownBackgroundSprite;
    [SerializeField] private Sprite toggleBackgroundSprite;

    private GameObject mainPanel;
    private GameObject settingsPanel;
    private GameObject creditsPanel;
    private GameObject createdCanvasObject;
    private GameObject backgroundObject;
    private GameObject overlayObject;

    private Slider masterVolumeSlider;
    private Slider musicVolumeSlider;
    private Slider sfxVolumeSlider;
    private TMP_Dropdown qualityDropdown;
    private Toggle fullScreenToggle;
    private TMP_Dropdown fpsDropdown;

    private bool suppressCallbacks;
    private int[] qualityTierToProjectQuality = { 0, 1, 2 };

    private void Awake()
    {
        if (!IsMainMenuScene(SceneManager.GetActiveScene().name))
        {
            Destroy(gameObject);
            return;
        }

        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        CleanupUi();
    }

    private void CleanupUi()
    {
        if (overlayObject != null) Destroy(overlayObject);
        if (backgroundObject != null) Destroy(backgroundObject);
        if (createdCanvasObject != null) Destroy(createdCanvasObject);
    }

    private void Start()
    {
        BuildUi();
        ShowMainPanel();
        RefreshSettingsControls();
    }

    private void BuildUi()
    {
        Canvas canvas = EnsureCanvas(out bool wasCreated);
        if (wasCreated) createdCanvasObject = canvas.gameObject;
        EnsureEventSystem();
        ResolveSkinSprites();

        CreateBackground(canvas.transform);

        GameObject overlay = new GameObject("MainMenuOverlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(canvas.transform, false);
        overlayObject = overlay;
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);

        mainPanel = CreatePanel(overlay.transform, "MainPanel", mainPanelSize);
        mainPanel.GetComponent<RectTransform>().anchoredPosition = mainPanelOffset;
        settingsPanel = CreatePanel(overlay.transform, "SettingsPanel", new Vector2(760f, 720f));
        creditsPanel = CreatePanel(overlay.transform, "CreditsPanel", new Vector2(900f, 720f));

        BuildMainPanel(mainPanel.transform);
        BuildSettingsPanel(settingsPanel.transform);
        BuildCreditsPanel(creditsPanel.transform);
    }

    private void BuildMainPanel(Transform parent)
    {
        VerticalLayoutGroup layout = parent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 28, 28);
        layout.spacing = 14f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        Button playButton = CreateMenuButton(parent, "Play", new Color(0.12f, 0.58f, 0.24f, 0.95f));
        Button settingsButton = CreateMenuButton(parent, "Settings", new Color(0.13f, 0.43f, 0.72f, 0.95f));
        Button creditsButton = CreateMenuButton(parent, "Credits", new Color(0.64f, 0.45f, 0.1f, 0.95f));
        Button quitButton = CreateMenuButton(parent, "Quit", new Color(0.66f, 0.2f, 0.2f, 0.95f));

        playButton.onClick.AddListener(LoadGameScene);
        settingsButton.onClick.AddListener(() =>
        {
            RefreshSettingsControls();
            ShowSettingsPanel();
        });
        creditsButton.onClick.AddListener(ShowCreditsPanel);
        quitButton.onClick.AddListener(QuitGame);
    }

    private void BuildSettingsPanel(Transform parent)
    {
        VerticalLayoutGroup layout = parent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 24, 24);
        layout.spacing = 12f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        CreateTitle(parent, "SETTINGS");

        Transform audioSection = CreateSectionContainer(parent, "AudioSection");
        CreateSectionLabel(audioSection, "SES");

        masterVolumeSlider = CreateLabeledSlider(audioSection, "Master Volume");
        masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);

        musicVolumeSlider = CreateLabeledSlider(audioSection, "Music Volume");
        musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);

        sfxVolumeSlider = CreateLabeledSlider(audioSection, "SFX Volume");
        sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);

        Transform graphicsSection = CreateSectionContainer(parent, "GraphicsSection");
        CreateSectionLabel(graphicsSection, "GRAFIK");

        ConfigureQualityTierMapping();
        qualityDropdown = CreateLabeledDropdown(graphicsSection, "Quality", new[] { "Dusuk", "Orta", "Yuksek" });
        qualityDropdown.onValueChanged.AddListener(OnQualityChanged);

        fullScreenToggle = CreateLabeledToggle(graphicsSection, "Fullscreen");
        fullScreenToggle.onValueChanged.AddListener(OnFullscreenChanged);

        fpsDropdown = CreateLabeledDropdown(graphicsSection, "FPS", new[] { "30", "60", "120", "Sinirsiz" });
        fpsDropdown.onValueChanged.AddListener(OnFpsChanged);

        Button backButton = CreateMenuButton(parent, "Back", new Color(0.15f, 0.45f, 0.75f, 0.95f));
        backButton.onClick.AddListener(ShowMainPanel);
    }

    private void BuildCreditsPanel(Transform parent)
    {
        VerticalLayoutGroup layout = parent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 24, 24);
        layout.spacing = 14f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        CreateTitle(parent, "CREDITS");

        GameObject textObject = new GameObject("CreditsText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);
        LayoutElement textLayout = textObject.GetComponent<LayoutElement>();
        textLayout.preferredHeight = 470f;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.TopLeft;
        text.fontSize = 27f;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.text = BuildCreditsText();
        if (TMP_Settings.defaultFontAsset != null)
        {
            text.font = TMP_Settings.defaultFontAsset;
        }

        Button backButton = CreateMenuButton(parent, "Back", new Color(0.15f, 0.45f, 0.75f, 0.95f));
        backButton.onClick.AddListener(ShowMainPanel);
    }

    private void OnMasterVolumeChanged(float value)
    {
        if (suppressCallbacks || GameSettings.Instance == null) return;
        GameSettings.Instance.SetMasterVolume(value);
        GameSettings.Instance.ApplySettings();
        GameSettings.Instance.SaveSettings();
    }

    private void OnMusicVolumeChanged(float value)
    {
        if (suppressCallbacks || GameSettings.Instance == null) return;
        GameSettings.Instance.SetMusicVolume(value);
        GameSettings.Instance.ApplySettings();
        GameSettings.Instance.SaveSettings();
    }

    private void OnSfxVolumeChanged(float value)
    {
        if (suppressCallbacks || GameSettings.Instance == null) return;
        GameSettings.Instance.SetSfxVolume(value);
        GameSettings.Instance.ApplySettings();
        GameSettings.Instance.SaveSettings();
    }

    private void OnQualityChanged(int tier)
    {
        if (suppressCallbacks || GameSettings.Instance == null) return;
        int index = Mathf.Clamp(tier, 0, qualityTierToProjectQuality.Length - 1);
        GameSettings.Instance.SetQualityLevel(qualityTierToProjectQuality[index]);
        GameSettings.Instance.ApplySettings();
        GameSettings.Instance.SaveSettings();
    }

    private void OnFullscreenChanged(bool value)
    {
        if (suppressCallbacks || GameSettings.Instance == null) return;
        GameSettings.Instance.SetFullScreen(value);
        GameSettings.Instance.ApplySettings();
        GameSettings.Instance.SaveSettings();
    }

    private void OnFpsChanged(int index)
    {
        if (suppressCallbacks || GameSettings.Instance == null) return;
        int[] fpsValues = { 30, 60, 120, -1 };
        GameSettings.Instance.SetTargetFps(fpsValues[Mathf.Clamp(index, 0, fpsValues.Length - 1)]);
        GameSettings.Instance.ApplySettings();
        GameSettings.Instance.SaveSettings();
    }

    private void RefreshSettingsControls()
    {
        if (GameSettings.Instance == null)
        {
            return;
        }

        suppressCallbacks = true;

        if (masterVolumeSlider != null) masterVolumeSlider.value = GameSettings.Instance.MasterVolume;
        if (musicVolumeSlider != null) musicVolumeSlider.value = GameSettings.Instance.MusicVolume;
        if (sfxVolumeSlider != null) sfxVolumeSlider.value = GameSettings.Instance.SfxVolume;

        if (qualityDropdown != null)
        {
            int currentQuality = GameSettings.Instance.QualityLevel >= 0
                ? GameSettings.Instance.QualityLevel
                : QualitySettings.GetQualityLevel();
            qualityDropdown.value = ConvertProjectQualityToTierIndex(currentQuality);
        }

        if (fullScreenToggle != null)
        {
            fullScreenToggle.isOn = GameSettings.Instance.FullScreen;
        }

        if (fpsDropdown != null)
        {
            int fps = GameSettings.Instance.TargetFps;
            fpsDropdown.value = fps switch
            {
                30 => 0,
                60 => 1,
                120 => 2,
                _ => 3
            };
        }

        suppressCallbacks = false;
    }

    private void ConfigureQualityTierMapping()
    {
        int qualityCount = QualitySettings.names.Length;
        if (qualityCount <= 0)
        {
            qualityTierToProjectQuality = new[] { 0, 0, 0 };
            return;
        }

        int lowIndex = 0;
        int mediumIndex = Mathf.Clamp(qualityCount / 2, 0, qualityCount - 1);
        int highIndex = qualityCount - 1;
        qualityTierToProjectQuality = new[] { lowIndex, mediumIndex, highIndex };
    }

    private int ConvertProjectQualityToTierIndex(int qualityIndex)
    {
        int bestTier = 0;
        int bestDistance = int.MaxValue;
        for (int i = 0; i < qualityTierToProjectQuality.Length; i++)
        {
            int distance = Mathf.Abs(qualityTierToProjectQuality[i] - qualityIndex);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTier = i;
            }
        }

        return bestTier;
    }

    private void ShowMainPanel()
    {
        mainPanel.SetActive(true);
        settingsPanel.SetActive(false);
        creditsPanel.SetActive(false);
    }

    private void ShowSettingsPanel()
    {
        mainPanel.SetActive(false);
        settingsPanel.SetActive(true);
        creditsPanel.SetActive(false);
    }

    private void ShowCreditsPanel()
    {
        mainPanel.SetActive(false);
        settingsPanel.SetActive(false);
        creditsPanel.SetActive(true);
    }

    private GameObject CreatePanel(Transform parent, string name, Vector2 size)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;

        Image image = panel.GetComponent<Image>();
        image.color = new Color(0.05f, 0.08f, 0.12f, 0.94f);
        if (panelBackgroundSprite != null)
        {
            image.sprite = panelBackgroundSprite;
            image.type = Image.Type.Sliced;
        }

        panel.SetActive(false);
        return panel;
    }

    private void CreateTitle(Transform parent, string title)
    {
        GameObject titleObject = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        titleObject.transform.SetParent(parent, false);

        LayoutElement layout = titleObject.GetComponent<LayoutElement>();
        layout.preferredHeight = 72f;

        TextMeshProUGUI text = titleObject.GetComponent<TextMeshProUGUI>();
        text.text = title;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.fontSize = 52f;
        text.fontStyle = FontStyles.Bold;
        if (TMP_Settings.defaultFontAsset != null)
        {
            text.font = TMP_Settings.defaultFontAsset;
        }
    }

    private Button CreateMenuButton(Transform parent, string label, Color color)
    {
        GameObject buttonObject = new GameObject($"{label}Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 64f;

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = color;
        if (buttonBackgroundSprite != null)
        {
            buttonImage.sprite = buttonBackgroundSprite;
            buttonImage.type = Image.Type.Sliced;
        }

        Button button = buttonObject.GetComponent<Button>();

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.fontSize = 34f;
        text.fontStyle = FontStyles.Bold;
        if (TMP_Settings.defaultFontAsset != null)
        {
            text.font = TMP_Settings.defaultFontAsset;
        }

        return button;
    }

    private Slider CreateLabeledSlider(Transform parent, string label)
    {
        GameObject row = new GameObject($"{label}Row", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);

        LayoutElement rowLayout = row.GetComponent<LayoutElement>();
        rowLayout.preferredHeight = 74f;

        VerticalLayoutGroup v = row.GetComponent<VerticalLayoutGroup>();
        v.spacing = 4f;
        v.childControlHeight = true;
        v.childControlWidth = true;
        v.childForceExpandHeight = false;
        v.childForceExpandWidth = true;

        GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(row.transform, false);
        TextMeshProUGUI labelText = labelObj.GetComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.fontSize = 22f;
        labelText.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null)
        {
            labelText.font = TMP_Settings.defaultFontAsset;
        }

        GameObject sliderObj = new GameObject("Slider", typeof(RectTransform), typeof(Slider), typeof(Image));
        sliderObj.transform.SetParent(row.transform, false);
        sliderObj.GetComponent<Image>().color = new Color(0.18f, 0.22f, 0.28f, 1f);
        if (sliderBackgroundSprite != null)
        {
            Image sliderImage = sliderObj.GetComponent<Image>();
            sliderImage.sprite = sliderBackgroundSprite;
            sliderImage.type = Image.Type.Sliced;
        }

        Slider slider = sliderObj.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0f);
        fillAreaRect.anchorMax = new Vector2(1f, 1f);
        fillAreaRect.offsetMin = new Vector2(10f, 6f);
        fillAreaRect.offsetMax = new Vector2(-10f, -6f);

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        fill.GetComponent<Image>().color = new Color(0.2f, 0.62f, 0.95f, 1f);
        if (sliderFillSprite != null)
        {
            Image fillImage = fill.GetComponent<Image>();
            fillImage.sprite = sliderFillSprite;
            fillImage.type = Image.Type.Sliced;
        }
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderObj.transform, false);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = new Vector2(0f, 0f);
        handleAreaRect.anchorMax = new Vector2(1f, 1f);
        handleAreaRect.offsetMin = new Vector2(10f, 0f);
        handleAreaRect.offsetMax = new Vector2(-10f, 0f);

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        handle.GetComponent<Image>().color = Color.white;
        if (sliderHandleSprite != null)
        {
            Image handleImage = handle.GetComponent<Image>();
            handleImage.sprite = sliderHandleSprite;
            handleImage.type = Image.Type.Simple;
            handleImage.preserveAspect = true;
        }
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20f, 28f);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;

        return slider;
    }

    private TMP_Dropdown CreateLabeledDropdown(Transform parent, string label, string[] options)
    {
        GameObject row = new GameObject($"{label}Row", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);

        LayoutElement rowLayout = row.GetComponent<LayoutElement>();
        rowLayout.preferredHeight = 64f;

        VerticalLayoutGroup v = row.GetComponent<VerticalLayoutGroup>();
        v.spacing = 4f;
        v.childControlHeight = true;
        v.childControlWidth = true;
        v.childForceExpandHeight = false;
        v.childForceExpandWidth = true;

        GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(row.transform, false);
        TextMeshProUGUI labelText = labelObj.GetComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.fontSize = 22f;
        labelText.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null)
        {
            labelText.font = TMP_Settings.defaultFontAsset;
        }

        GameObject ddObj = new GameObject("Dropdown", typeof(RectTransform), typeof(TMP_Dropdown), typeof(Image));
        ddObj.transform.SetParent(row.transform, false);
        ddObj.GetComponent<Image>().color = new Color(0.18f, 0.22f, 0.28f, 1f);
        if (dropdownBackgroundSprite != null)
        {
            Image dropdownImage = ddObj.GetComponent<Image>();
            dropdownImage.sprite = dropdownBackgroundSprite;
            dropdownImage.type = Image.Type.Sliced;
        }

        GameObject captionObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        captionObj.transform.SetParent(ddObj.transform, false);
        RectTransform capRect = captionObj.GetComponent<RectTransform>();
        capRect.anchorMin = Vector2.zero;
        capRect.anchorMax = Vector2.one;
        capRect.offsetMin = new Vector2(8f, 0f);
        capRect.offsetMax = new Vector2(-30f, 0f);
        TextMeshProUGUI captionText = captionObj.GetComponent<TextMeshProUGUI>();
        captionText.fontSize = 18f;
        captionText.color = Color.white;
        captionText.alignment = TextAlignmentOptions.Left;
        if (TMP_Settings.defaultFontAsset != null)
        {
            captionText.font = TMP_Settings.defaultFontAsset;
        }

        GameObject templateObj = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        templateObj.transform.SetParent(ddObj.transform, false);
        templateObj.SetActive(false);

        GameObject viewportObj = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
        viewportObj.transform.SetParent(templateObj.transform, false);
        viewportObj.GetComponent<Image>().color = Color.white;
        viewportObj.GetComponent<Mask>().showMaskGraphic = false;

        GameObject contentObj = new GameObject("Content", typeof(RectTransform));
        contentObj.transform.SetParent(viewportObj.transform, false);

        GameObject itemObj = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
        itemObj.transform.SetParent(contentObj.transform, false);

        GameObject itemBgObj = new GameObject("Item Background", typeof(RectTransform), typeof(Image));
        itemBgObj.transform.SetParent(itemObj.transform, false);

        GameObject itemLabelObj = new GameObject("Item Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        itemLabelObj.transform.SetParent(itemObj.transform, false);
        TextMeshProUGUI itemLabelText = itemLabelObj.GetComponent<TextMeshProUGUI>();
        itemLabelText.fontSize = 18f;
        itemLabelText.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null)
        {
            itemLabelText.font = TMP_Settings.defaultFontAsset;
        }

        TMP_Dropdown dropdown = ddObj.GetComponent<TMP_Dropdown>();
        dropdown.template = templateObj.GetComponent<RectTransform>();
        dropdown.captionText = captionText;
        dropdown.itemText = itemLabelText;
        dropdown.ClearOptions();
        dropdown.AddOptions(new System.Collections.Generic.List<string>(options));

        return dropdown;
    }

    private Toggle CreateLabeledToggle(Transform parent, string label)
    {
        GameObject row = new GameObject($"{label}Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);

        LayoutElement rowLayout = row.GetComponent<LayoutElement>();
        rowLayout.preferredHeight = 38f;

        HorizontalLayoutGroup h = row.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 10f;
        h.childControlHeight = true;
        h.childControlWidth = true;
        h.childForceExpandHeight = false;
        h.childForceExpandWidth = false;

        GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        labelObj.transform.SetParent(row.transform, false);
        labelObj.GetComponent<LayoutElement>().preferredWidth = 230f;
        TextMeshProUGUI labelText = labelObj.GetComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.fontSize = 22f;
        labelText.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null)
        {
            labelText.font = TMP_Settings.defaultFontAsset;
        }

        GameObject toggleObj = new GameObject("Toggle", typeof(RectTransform), typeof(Toggle), typeof(Image));
        toggleObj.transform.SetParent(row.transform, false);
        Image bg = toggleObj.GetComponent<Image>();
        bg.color = new Color(0.18f, 0.22f, 0.28f, 1f);
        if (toggleBackgroundSprite != null)
        {
            bg.sprite = toggleBackgroundSprite;
            bg.type = Image.Type.Sliced;
        }

        GameObject checkObj = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        checkObj.transform.SetParent(toggleObj.transform, false);
        Image check = checkObj.GetComponent<Image>();
        check.color = new Color(0.2f, 0.62f, 0.95f, 1f);

        RectTransform checkRect = checkObj.GetComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0.15f, 0.15f);
        checkRect.anchorMax = new Vector2(0.85f, 0.85f);
        checkRect.offsetMin = Vector2.zero;
        checkRect.offsetMax = Vector2.zero;

        Toggle toggle = toggleObj.GetComponent<Toggle>();
        toggle.targetGraphic = bg;
        toggle.graphic = check;

        return toggle;
    }

    private Transform CreateSectionContainer(Transform parent, string objectName)
    {
        GameObject sectionObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        sectionObject.transform.SetParent(parent, false);

        Image sectionImage = sectionObject.GetComponent<Image>();
        sectionImage.color = new Color(0.09f, 0.13f, 0.19f, 0.92f);
        if (panelBackgroundSprite != null)
        {
            sectionImage.sprite = panelBackgroundSprite;
            sectionImage.type = Image.Type.Sliced;
        }

        VerticalLayoutGroup sectionLayout = sectionObject.GetComponent<VerticalLayoutGroup>();
        sectionLayout.padding = new RectOffset(16, 16, 14, 14);
        sectionLayout.spacing = 8f;
        sectionLayout.childControlWidth = true;
        sectionLayout.childControlHeight = true;
        sectionLayout.childForceExpandWidth = true;
        sectionLayout.childForceExpandHeight = false;

        LayoutElement sectionSize = sectionObject.GetComponent<LayoutElement>();
        sectionSize.minHeight = 120f;

        return sectionObject.transform;
    }

    private void CreateSectionLabel(Transform parent, string label)
    {
        GameObject headerObject = new GameObject($"{label}Header", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        headerObject.transform.SetParent(parent, false);

        LayoutElement layout = headerObject.GetComponent<LayoutElement>();
        layout.minHeight = 34f;

        TextMeshProUGUI text = headerObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 24f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Left;
        text.color = new Color(0.88f, 0.94f, 1f, 1f);
        if (TMP_Settings.defaultFontAsset != null)
        {
            text.font = TMP_Settings.defaultFontAsset;
        }
    }

    private void CreateBackground(Transform parent)
    {
        backgroundObject = new GameObject("MainMenuBackground", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(parent, false);

        RectTransform rect = backgroundObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = backgroundObject.GetComponent<Image>();
        image.color = Color.white;
        image.sprite = RuntimeUiSkinLoader.LoadSprite(backgroundResourcesPath, backgroundAssetPath);
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
    }

    private string BuildCreditsText()
    {
        return
            "Game by: " + developerName + "\n\n" +
            "Used Assets:\n" +
            "- Main Menu Artwork (Assets/Images/MainMenuImage.png)\n" +
            "- SimplePoly City - Low Poly Assets\n" +
            "- Nebula - Free low poly car pack\n" +
            "- EasyRoads3D\n" +
            "- Cardboard Box (Rigged)\n" +
            "- Kenney UI Pack\n" +
            "- HQP Studios Low Poly 3D Icons - Pack Lite\n" +
            "- Keypad\n" +
            "- TextMesh Pro\n\n" +
            "Thanks for playing Delivery Driver.";
    }

    private static Canvas EnsureCanvas(out bool wasCreated)
    {
        Canvas existingCanvas = FindFirstObjectByType<Canvas>();
        if (existingCanvas != null)
        {
            wasCreated = false;
            return existingCanvas;
        }

        wasCreated = true;
        GameObject canvasObject = new GameObject("MainMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void LoadGameScene()
    {
        string sceneToLoad = ResolvePlayableSceneName();
        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.Log($"[MainMenuRuntimeUI] Loading scene: {sceneToLoad}");
            SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);
        }
        else
        {
            Debug.LogError("[MainMenuRuntimeUI] No playable game scene found in Build Settings.");
        }
    }

    private string ResolvePlayableSceneName()
    {
        if (!string.IsNullOrWhiteSpace(gameSceneName) && IsSceneInBuildByName(gameSceneName))
        {
            return gameSceneName;
        }

        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!name.Equals("MainMenu", System.StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        return string.Empty;
    }

    private static bool IsSceneInBuildByName(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name.Equals(sceneName, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void HandleActiveSceneChanged(Scene previous, Scene next)
    {
        if (!IsMainMenuScene(next.name))
        {
            Destroy(gameObject);
        }
    }

    private static bool IsMainMenuScene(string sceneName)
    {
        return sceneName.Equals("MainMenu", System.StringComparison.OrdinalIgnoreCase);
    }

    private void ResolveSkinSprites()
    {
        panelBackgroundSprite ??= RuntimeUiSkinLoader.LoadSprite(
            "UI/Kenney/panel_bg",
            "Assets/kenney_ui-pack/PNG/Grey/Double/button_rectangle_depth_flat.png");

        buttonBackgroundSprite ??= RuntimeUiSkinLoader.LoadSprite(
            "UI/Kenney/button_bg",
            "Assets/kenney_ui-pack/PNG/Grey/Default/button_rectangle_depth_flat.png");

        sliderBackgroundSprite ??= RuntimeUiSkinLoader.LoadSprite(
            "UI/Kenney/slider_bg",
            "Assets/kenney_ui-pack/PNG/Grey/Default/slide_horizontal_grey_section_wide.png");

        sliderFillSprite ??= RuntimeUiSkinLoader.LoadSprite(
            "UI/Kenney/slider_fill",
            "Assets/kenney_ui-pack/PNG/Blue/Default/slide_horizontal_color_section_wide.png");

        sliderHandleSprite ??= RuntimeUiSkinLoader.LoadSprite(
            "UI/Kenney/slider_handle",
            "Assets/kenney_ui-pack/PNG/Grey/Default/slide_hangle.png");

        dropdownBackgroundSprite ??= buttonBackgroundSprite;
        toggleBackgroundSprite ??= buttonBackgroundSprite;
    }
}
