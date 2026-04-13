using DeliveryDriver.Quest;
using DeliveryDriver.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuRuntimeUI : MonoBehaviour
{
    private const float SettingsLabelColumnWidth = 260f;
    private const float SettingsRowHeight = 54f;

    private enum MenuPanelState
    {
        Main,
        Settings,
        Credits
    }

    [Header("Scenes")]
    [SerializeField] private string gameSceneName = "Game";

    [Header("Background")]
    [SerializeField] private string backgroundResourcesPath = "UI/MainMenu/mainmenu_bg";
    [SerializeField] private string backgroundAssetPath = "Assets/Images/MainMenuImage.png";

    [Header("Credits")]
    [SerializeField] private string developerName = "hekye";

    [Header("Main Panel Layout")]
    [SerializeField] private Vector2 mainPanelSize = new Vector2(640f, 430f);
    [SerializeField] private Vector2 mainPanelOffset = new Vector2(0f, -150f);

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

    private CanvasGroup mainPanelCanvasGroup;
    private CanvasGroup settingsPanelCanvasGroup;
    private CanvasGroup creditsPanelCanvasGroup;

    private Slider masterVolumeSlider;
    private Slider musicVolumeSlider;
    private Slider sfxVolumeSlider;
    private TMP_Dropdown qualityDropdown;
    private TMP_Dropdown resolutionDropdown;
    private Toggle fullScreenToggle;
    private TMP_Dropdown fpsDropdown;
    private TMP_Dropdown speedUnitDropdown;
    private TMP_Dropdown languageDropdown;
    private TMP_Dropdown colorBlindDropdown;
    private Slider textScaleSlider;
    private Toggle highContrastToggle;

    private bool suppressCallbacks;
    private int[] qualityTierToProjectQuality = { 0, 1, 2 };
    private Resolution[] availableResolutions;
    private MenuPanelState currentPanelState = MenuPanelState.Main;

    private void Awake()
    {
        if (!IsMainMenuScene(SceneManager.GetActiveScene().name))
        {
            Destroy(gameObject);
            return;
        }

        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        LocalizationTable.OnLocaleChanged += HandleLocaleChanged;
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        LocalizationTable.OnLocaleChanged -= HandleLocaleChanged;
        CleanupUi();
    }

    private void CleanupUi(bool destroyCanvas = true)
    {
        if (overlayObject != null) Destroy(overlayObject);
        if (backgroundObject != null) Destroy(backgroundObject);
        if (destroyCanvas && createdCanvasObject != null) Destroy(createdCanvasObject);
        ResetUiReferences(destroyCanvas);
    }

    private void Start()
    {
        LocalizationTable.EnsureLoaded();
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
        settingsPanel = CreatePanel(overlay.transform, "SettingsPanel", new Vector2(980f, 980f));
        creditsPanel = CreatePanel(overlay.transform, "CreditsPanel", new Vector2(900f, 720f));

        mainPanelCanvasGroup = mainPanel.AddComponent<CanvasGroup>();
        settingsPanelCanvasGroup = settingsPanel.AddComponent<CanvasGroup>();
        creditsPanelCanvasGroup = creditsPanel.AddComponent<CanvasGroup>();

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

        Button playButton = CreateMenuButton(parent, LocalizationTable.Get("play"), new Color(0.12f, 0.58f, 0.24f, 0.95f));
        Button settingsButton = CreateMenuButton(parent, LocalizationTable.Get("settings"), new Color(0.13f, 0.43f, 0.72f, 0.95f));
        Button creditsButton = CreateMenuButton(parent, LocalizationTable.Get("credits"), new Color(0.64f, 0.45f, 0.1f, 0.95f));
        Button quitButton = CreateMenuButton(parent, LocalizationTable.Get("quit"), new Color(0.66f, 0.2f, 0.2f, 0.95f));

        UIButtonEnhancer.EnhanceButton(playButton);
        UIButtonEnhancer.EnhanceButton(settingsButton);
        UIButtonEnhancer.EnhanceButton(creditsButton);
        UIButtonEnhancer.EnhanceButton(quitButton);

        playButton.onClick.AddListener(LoadGameScene);
        settingsButton.onClick.AddListener(() =>
        {
            RefreshSettingsControls();
            ShowSettingsPanel();
        });
        creditsButton.onClick.AddListener(ShowCreditsPanel);
        quitButton.onClick.AddListener(() =>
        {
            ConfirmationDialog.Show(
                LocalizationTable.Get("confirm_quit_title"),
                LocalizationTable.Get("confirm_quit"),
                QuitGame);
        });
    }

    private void BuildSettingsPanel(Transform parent)
    {
        VerticalLayoutGroup layout = parent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(30, 30, 24, 24);
        layout.spacing = 14f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        CreateTitle(parent, LocalizationTable.Get("settings_title"));

        Transform contentRoot = CreateScrollableSettingsContent(parent);

        Transform audioSection = CreateSectionContainer(contentRoot, "AudioSection", 220f);
        CreateSectionLabel(audioSection, LocalizationTable.Get("audio"));

        masterVolumeSlider = CreateLabeledSlider(audioSection, LocalizationTable.Get("master_volume"));
        masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);

        musicVolumeSlider = CreateLabeledSlider(audioSection, LocalizationTable.Get("music_volume"));
        musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);

        sfxVolumeSlider = CreateLabeledSlider(audioSection, LocalizationTable.Get("sfx_volume"));
        sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);

        Transform graphicsSection = CreateSectionContainer(contentRoot, "GraphicsSection", 430f);
        CreateSectionLabel(graphicsSection, LocalizationTable.Get("graphics"));

        ConfigureQualityTierMapping();
        qualityDropdown = CreateLabeledDropdown(graphicsSection, LocalizationTable.Get("quality"),
            new[] { LocalizationTable.Get("quality_low"), LocalizationTable.Get("quality_medium"), LocalizationTable.Get("quality_high") });
        qualityDropdown.onValueChanged.AddListener(OnQualityChanged);

        BuildResolutionDropdown(graphicsSection);

        fullScreenToggle = CreateLabeledToggle(graphicsSection, LocalizationTable.Get("fullscreen"));
        fullScreenToggle.onValueChanged.AddListener(OnFullscreenChanged);

        fpsDropdown = CreateLabeledDropdown(graphicsSection, LocalizationTable.Get("fps_limit"),
            new[] { "30", "60", "120", LocalizationTable.Get("fps_unlimited") });
        fpsDropdown.onValueChanged.AddListener(OnFpsChanged);

        speedUnitDropdown = CreateLabeledDropdown(graphicsSection, LocalizationTable.Get("speed_unit"),
            new[] { "KMH", "MPH" });
        speedUnitDropdown.onValueChanged.AddListener(OnSpeedUnitChanged);

        languageDropdown = CreateLabeledDropdown(graphicsSection, LocalizationTable.Get("language"),
            new[]
            {
                LocalizationTable.GetLocaleDisplayName(LocalizationTable.TurkishLocale),
                LocalizationTable.GetLocaleDisplayName(LocalizationTable.EnglishLocale)
            });
        languageDropdown.onValueChanged.AddListener(OnLanguageChanged);

        Transform accessibilitySection = CreateSectionContainer(contentRoot, "AccessibilitySection", 250f);
        CreateSectionLabel(accessibilitySection, LocalizationTable.Get("accessibility"));

        colorBlindDropdown = CreateLabeledDropdown(accessibilitySection, LocalizationTable.Get("color_blind_mode"),
            new[]
            {
                LocalizationTable.Get("color_blind_none"),
                LocalizationTable.Get("color_blind_protanopia"),
                LocalizationTable.Get("color_blind_deuteranopia"),
                LocalizationTable.Get("color_blind_tritanopia")
            });
        colorBlindDropdown.onValueChanged.AddListener(OnColorBlindModeChanged);

        textScaleSlider = CreateLabeledSlider(accessibilitySection, LocalizationTable.Get("text_scale"));
        textScaleSlider.minValue = 0.8f;
        textScaleSlider.maxValue = 1.5f;
        textScaleSlider.onValueChanged.AddListener(OnTextScaleChanged);

        highContrastToggle = CreateLabeledToggle(accessibilitySection, LocalizationTable.Get("high_contrast"));
        highContrastToggle.onValueChanged.AddListener(OnHighContrastChanged);

        Button backButton = CreateMenuButton(parent, LocalizationTable.Get("back"), UIThemeConstants.ButtonBlue);
        UIButtonEnhancer.EnhanceButton(backButton);
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

        CreateTitle(parent, LocalizationTable.Get("credits"));

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

        Button backButton = CreateMenuButton(parent, LocalizationTable.Get("back"), UIThemeConstants.ButtonBlue);
        UIButtonEnhancer.EnhanceButton(backButton);
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

    private void OnResolutionChanged(int index)
    {
        if (suppressCallbacks || GameSettings.Instance == null) return;
        if (availableResolutions == null || index < 0 || index >= availableResolutions.Length) return;

        Resolution resolution = availableResolutions[index];
        bool isFullScreen = fullScreenToggle != null && fullScreenToggle.isOn;
        Screen.SetResolution(resolution.width, resolution.height, isFullScreen);
        GameSettings.Instance.SetResolutionIndex(index);
        GameSettings.Instance.SetResolutionSize(resolution.width, resolution.height);
        GameSettings.Instance.SetFullScreen(isFullScreen);
        GameSettings.Instance.SaveSettings();
    }

    private void OnSpeedUnitChanged(int index)
    {
        if (suppressCallbacks || GameSettings.Instance == null) return;
        GameSettings.Instance.SetSpeedUnitPreference(index == 1
            ? SpeedUnitPreference.Mph
            : SpeedUnitPreference.Kmh);
        GameSettings.Instance.ApplySettings();
        GameSettings.Instance.SaveSettings();
    }

    private void OnColorBlindModeChanged(int mode)
    {
        if (suppressCallbacks || GameSettings.Instance == null) return;
        GameSettings.Instance.SetColorBlindMode(mode);
        GameSettings.Instance.SaveSettings();
    }

    private void OnTextScaleChanged(float value)
    {
        if (suppressCallbacks || GameSettings.Instance == null) return;
        GameSettings.Instance.SetTextScaleMultiplier(value);
        GameSettings.Instance.SaveSettings();
    }

    private void OnHighContrastChanged(bool value)
    {
        if (suppressCallbacks || GameSettings.Instance == null) return;
        GameSettings.Instance.SetHighContrastMode(value);
        GameSettings.Instance.SaveSettings();
    }

    private void OnLanguageChanged(int index)
    {
        if (suppressCallbacks)
        {
            return;
        }

        LocalizationTable.SetLocale(LocalizationTable.GetLocaleByIndex(index));
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

        if (resolutionDropdown != null && availableResolutions != null && availableResolutions.Length > 0)
        {
            int safeIndex = Mathf.Clamp(GameSettings.Instance.ResolutionIndex, 0, availableResolutions.Length - 1);
            resolutionDropdown.value = safeIndex;
        }

        if (fullScreenToggle != null)
        {
            fullScreenToggle.isOn = GameSettings.Instance.FullScreen;
        }

        if (fpsDropdown != null)
        {
            SetFpsDropdownValue();
        }

        if (speedUnitDropdown != null)
        {
            speedUnitDropdown.value = GameSettings.Instance.SpeedUnitPreference == SpeedUnitPreference.Mph ? 1 : 0;
        }

        if (languageDropdown != null)
        {
            languageDropdown.value = LocalizationTable.GetLocaleIndex(GameSettings.Instance.Language);
        }

        if (colorBlindDropdown != null)
        {
            colorBlindDropdown.value = Mathf.Clamp(GameSettings.Instance.ColorBlindMode, 0, 3);
        }

        if (textScaleSlider != null)
        {
            textScaleSlider.value = Mathf.Clamp(GameSettings.Instance.TextScaleMultiplier, 0.8f, 1.5f);
        }

        if (highContrastToggle != null)
        {
            highContrastToggle.isOn = GameSettings.Instance.HighContrastMode;
        }

        suppressCallbacks = false;
    }

    private void SetFpsDropdownValue()
    {
        if (fpsDropdown == null || GameSettings.Instance == null)
        {
            return;
        }

        int fps = GameSettings.Instance.TargetFps;
        fpsDropdown.value = fps switch
        {
            30 => 0,
            60 => 1,
            120 => 2,
            _ => 3
        };
    }

    private void BuildResolutionDropdown(Transform parent)
    {
        Resolution[] allResolutions = Screen.resolutions;
        var uniqueResolutions = new System.Collections.Generic.List<Resolution>();
        var options = new System.Collections.Generic.List<string>();
        int currentIndex = 0;
        int currentWidth = Screen.currentResolution.width;
        int currentHeight = Screen.currentResolution.height;

        for (int i = 0; i < allResolutions.Length; i++)
        {
            Resolution res = allResolutions[i];
            bool duplicate = false;
            for (int j = 0; j < uniqueResolutions.Count; j++)
            {
                if (uniqueResolutions[j].width == res.width && uniqueResolutions[j].height == res.height)
                {
                    duplicate = true;
                    break;
                }
            }

            if (duplicate) continue;

            if (res.width == currentWidth && res.height == currentHeight)
            {
                currentIndex = uniqueResolutions.Count;
            }

            uniqueResolutions.Add(res);
            options.Add($"{res.width} x {res.height}");
        }

        availableResolutions = uniqueResolutions.ToArray();
        resolutionDropdown = CreateLabeledDropdown(parent, LocalizationTable.Get("resolution"), options.ToArray());
        if (resolutionDropdown != null)
        {
            resolutionDropdown.value = currentIndex;
            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        }
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
        currentPanelState = MenuPanelState.Main;
        AnimateHidePanel(settingsPanel, settingsPanelCanvasGroup);
        AnimateHidePanel(creditsPanel, creditsPanelCanvasGroup);
        AnimateShowPanel(mainPanel, mainPanelCanvasGroup);
    }

    private void ShowSettingsPanel()
    {
        currentPanelState = MenuPanelState.Settings;
        AnimateHidePanel(mainPanel, mainPanelCanvasGroup);
        AnimateHidePanel(creditsPanel, creditsPanelCanvasGroup);
        AnimateShowPanel(settingsPanel, settingsPanelCanvasGroup);
    }

    private void ShowCreditsPanel()
    {
        currentPanelState = MenuPanelState.Credits;
        AnimateHidePanel(mainPanel, mainPanelCanvasGroup);
        AnimateHidePanel(settingsPanel, settingsPanelCanvasGroup);
        AnimateShowPanel(creditsPanel, creditsPanelCanvasGroup);
    }

    private void AnimateShowPanel(GameObject panel, CanvasGroup group)
    {
        if (panel == null) return;
        panel.SetActive(true);
        if (group != null)
        {
            group.alpha = 0f;
            UIAnimationHelper.FadeIn(this, group, UIThemeConstants.PanelFadeDuration);
            UIAnimationHelper.ScaleIn(this, panel.GetComponent<RectTransform>(), UIThemeConstants.PanelScaleDuration);
        }
    }

    private void AnimateHidePanel(GameObject panel, CanvasGroup group)
    {
        if (panel == null || !panel.activeSelf) return;
        if (group != null)
        {
            UIAnimationHelper.FadeOut(this, group, UIThemeConstants.PanelFadeDuration * 0.5f, () =>
            {
                panel.SetActive(false);
            });
        }
        else
        {
            panel.SetActive(false);
        }
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
        layout.preferredHeight = 58f;

        TextMeshProUGUI text = titleObject.GetComponent<TextMeshProUGUI>();
        text.text = title;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.fontSize = 38f;
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
        GameObject row = new GameObject($"{label}Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);

        LayoutElement rowLayout = row.GetComponent<LayoutElement>();
        rowLayout.preferredHeight = SettingsRowHeight;
        rowLayout.minHeight = SettingsRowHeight;

        HorizontalLayoutGroup h = row.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 14f;
        h.childControlHeight = true;
        h.childControlWidth = true;
        h.childForceExpandHeight = false;
        h.childForceExpandWidth = true;
        h.childAlignment = TextAnchor.MiddleLeft;

        GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        labelObj.transform.SetParent(row.transform, false);
        LayoutElement labelLayout = labelObj.GetComponent<LayoutElement>();
        labelLayout.preferredWidth = SettingsLabelColumnWidth;
        labelLayout.minWidth = SettingsLabelColumnWidth;
        labelLayout.flexibleWidth = 0f;
        TextMeshProUGUI labelText = labelObj.GetComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.fontSize = 20f;
        labelText.color = Color.white;
        labelText.alignment = TextAlignmentOptions.MidlineLeft;
        if (TMP_Settings.defaultFontAsset != null)
        {
            labelText.font = TMP_Settings.defaultFontAsset;
        }

        GameObject sliderObj = new GameObject("Slider", typeof(RectTransform), typeof(Slider), typeof(Image));
        sliderObj.transform.SetParent(row.transform, false);
        LayoutElement sliderLayout = sliderObj.AddComponent<LayoutElement>();
        sliderLayout.minHeight = 26f;
        sliderLayout.preferredHeight = 26f;
        sliderLayout.flexibleWidth = 1f;
        sliderObj.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);

        Slider slider = sliderObj.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0f);
        fillAreaRect.anchorMax = new Vector2(1f, 1f);
        fillAreaRect.offsetMin = new Vector2(8f, 8f);
        fillAreaRect.offsetMax = new Vector2(-8f, -8f);

        GameObject track = new GameObject("Track", typeof(RectTransform), typeof(Image));
        track.transform.SetParent(fillArea.transform, false);
        RectTransform trackRect = track.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0f, 0.5f);
        trackRect.anchorMax = new Vector2(1f, 0.5f);
        trackRect.pivot = new Vector2(0.5f, 0.5f);
        trackRect.sizeDelta = new Vector2(0f, 6f);
        Image trackImage = track.GetComponent<Image>();
        trackImage.color = new Color(0.24f, 0.29f, 0.37f, 1f);

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        Image fillImage = fill.GetComponent<Image>();
        fillImage.color = new Color(0.21f, 0.56f, 0.93f, 1f);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0.5f);
        fillRect.anchorMax = new Vector2(1f, 0.5f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.sizeDelta = new Vector2(0f, 6f);

        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderObj.transform, false);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = new Vector2(0f, 0f);
        handleAreaRect.anchorMax = new Vector2(1f, 1f);
        handleAreaRect.offsetMin = new Vector2(8f, 0f);
        handleAreaRect.offsetMax = new Vector2(-8f, 0f);

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        Image handleImage = handle.GetComponent<Image>();
        handleImage.color = new Color(0.95f, 0.97f, 1f, 1f);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(12f, 20f);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;

        return slider;
    }

    private TMP_Dropdown CreateLabeledDropdown(Transform parent, string label, string[] options)
    {
        GameObject row = new GameObject($"{label}Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);

        LayoutElement rowLayout = row.GetComponent<LayoutElement>();
        rowLayout.preferredHeight = SettingsRowHeight;
        rowLayout.minHeight = SettingsRowHeight;

        HorizontalLayoutGroup h = row.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 14f;
        h.childControlHeight = true;
        h.childControlWidth = true;
        h.childForceExpandHeight = false;
        h.childForceExpandWidth = true;
        h.childAlignment = TextAnchor.MiddleLeft;

        GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        labelObj.transform.SetParent(row.transform, false);
        LayoutElement labelLayout = labelObj.GetComponent<LayoutElement>();
        labelLayout.preferredWidth = SettingsLabelColumnWidth;
        labelLayout.minWidth = SettingsLabelColumnWidth;
        labelLayout.flexibleWidth = 0f;
        TextMeshProUGUI labelText = labelObj.GetComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.fontSize = 20f;
        labelText.color = Color.white;
        labelText.alignment = TextAlignmentOptions.MidlineLeft;
        if (TMP_Settings.defaultFontAsset != null)
        {
            labelText.font = TMP_Settings.defaultFontAsset;
        }

        GameObject ddObj = new GameObject("Dropdown", typeof(RectTransform), typeof(TMP_Dropdown), typeof(Image), typeof(LayoutElement));
        ddObj.transform.SetParent(row.transform, false);
        LayoutElement dropdownLayout = ddObj.GetComponent<LayoutElement>();
        dropdownLayout.minHeight = 44f;
        dropdownLayout.preferredHeight = 44f;
        dropdownLayout.flexibleWidth = 1f;
        Image dropdownImage = ddObj.GetComponent<Image>();
        dropdownImage.color = new Color(0.18f, 0.22f, 0.28f, 1f);

        GameObject captionObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        captionObj.transform.SetParent(ddObj.transform, false);
        RectTransform capRect = captionObj.GetComponent<RectTransform>();
        capRect.anchorMin = Vector2.zero;
        capRect.anchorMax = Vector2.one;
        capRect.offsetMin = new Vector2(12f, 6f);
        capRect.offsetMax = new Vector2(-32f, -6f);
        TextMeshProUGUI captionText = captionObj.GetComponent<TextMeshProUGUI>();
        captionText.fontSize = 18f;
        captionText.color = Color.white;
        captionText.alignment = TextAlignmentOptions.MidlineLeft;
        if (TMP_Settings.defaultFontAsset != null)
        {
            captionText.font = TMP_Settings.defaultFontAsset;
        }

        GameObject templateObj = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        templateObj.transform.SetParent(ddObj.transform, false);
        RectTransform templateRect = templateObj.GetComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0f, 1f);
        templateRect.anchorMax = new Vector2(1f, 1f);
        templateRect.pivot = new Vector2(0.5f, 0f);
        templateRect.anchoredPosition = new Vector2(0f, 2f);
        templateRect.sizeDelta = new Vector2(0f, 164f);
        Image templateImage = templateObj.GetComponent<Image>();
        templateImage.color = new Color(0.12f, 0.16f, 0.22f, 0.98f);
        templateObj.SetActive(false);

        GameObject viewportObj = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
        viewportObj.transform.SetParent(templateObj.transform, false);
        RectTransform viewportRect = viewportObj.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewportObj.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.001f);
        viewportObj.GetComponent<Mask>().showMaskGraphic = false;

        GameObject contentObj = new GameObject("Content", typeof(RectTransform));
        contentObj.transform.SetParent(viewportObj.transform, false);
        RectTransform contentRect = contentObj.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 36f);

        GameObject itemObj = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
        itemObj.transform.SetParent(contentObj.transform, false);
        RectTransform itemRect = itemObj.GetComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(0f, 0.5f);
        itemRect.anchorMax = new Vector2(1f, 0.5f);
        itemRect.sizeDelta = new Vector2(0f, 34f);

        GameObject itemBgObj = new GameObject("Item Background", typeof(RectTransform), typeof(Image));
        itemBgObj.transform.SetParent(itemObj.transform, false);
        RectTransform itemBgRect = itemBgObj.GetComponent<RectTransform>();
        itemBgRect.anchorMin = Vector2.zero;
        itemBgRect.anchorMax = Vector2.one;
        itemBgRect.offsetMin = Vector2.zero;
        itemBgRect.offsetMax = Vector2.zero;
        Image itemBgImage = itemBgObj.GetComponent<Image>();
        itemBgImage.color = new Color(0.20f, 0.25f, 0.34f, 1f);

        GameObject itemLabelObj = new GameObject("Item Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        itemLabelObj.transform.SetParent(itemObj.transform, false);
        RectTransform itemLabelRect = itemLabelObj.GetComponent<RectTransform>();
        itemLabelRect.anchorMin = Vector2.zero;
        itemLabelRect.anchorMax = Vector2.one;
        itemLabelRect.offsetMin = new Vector2(10f, 2f);
        itemLabelRect.offsetMax = new Vector2(-10f, -2f);
        TextMeshProUGUI itemLabelText = itemLabelObj.GetComponent<TextMeshProUGUI>();
        itemLabelText.fontSize = 17f;
        itemLabelText.color = Color.white;
        itemLabelText.alignment = TextAlignmentOptions.MidlineLeft;
        if (TMP_Settings.defaultFontAsset != null)
        {
            itemLabelText.font = TMP_Settings.defaultFontAsset;
        }

        Toggle itemToggle = itemObj.GetComponent<Toggle>();
        itemToggle.targetGraphic = itemBgImage;
        
        ColorBlock cb = itemToggle.colors;
        cb.normalColor = new Color(0.20f, 0.25f, 0.34f, 1f);
        cb.highlightedColor = new Color(0.30f, 0.35f, 0.44f, 1f);
        cb.pressedColor = new Color(0.15f, 0.20f, 0.29f, 1f);
        cb.selectedColor = new Color(0.25f, 0.30f, 0.39f, 1f);
        cb.colorMultiplier = 1f;
        itemToggle.colors = cb;
        
        itemToggle.isOn = true;

        ScrollRect scrollRect = templateObj.GetComponent<ScrollRect>();
        scrollRect.content = contentRect;
        scrollRect.viewport = viewportRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        TMP_Dropdown dropdown = ddObj.GetComponent<TMP_Dropdown>();
        dropdown.targetGraphic = dropdownImage;

        ColorBlock dropdownColors = dropdown.colors;
        dropdownColors.normalColor = new Color(0.22f, 0.26f, 0.32f, 1f);
        dropdownColors.highlightedColor = new Color(0.32f, 0.36f, 0.42f, 1f);
        dropdownColors.pressedColor = new Color(0.12f, 0.16f, 0.22f, 1f);
        dropdownColors.selectedColor = new Color(0.27f, 0.31f, 0.37f, 1f);
        dropdown.colors = dropdownColors;

        dropdown.template = templateRect;
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
        rowLayout.preferredHeight = 44f;
        rowLayout.minHeight = 44f;

        HorizontalLayoutGroup h = row.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 10f;
        h.childControlHeight = true;
        h.childControlWidth = true;
        h.childForceExpandHeight = false;
        h.childForceExpandWidth = false;

        GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        labelObj.transform.SetParent(row.transform, false);
        LayoutElement labelLayout = labelObj.GetComponent<LayoutElement>();
        labelLayout.preferredWidth = SettingsLabelColumnWidth;
        labelLayout.minWidth = SettingsLabelColumnWidth;
        labelLayout.flexibleWidth = 1f;
        TextMeshProUGUI labelText = labelObj.GetComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.fontSize = 20f;
        labelText.color = Color.white;
        labelText.alignment = TextAlignmentOptions.MidlineLeft;
        if (TMP_Settings.defaultFontAsset != null)
        {
            labelText.font = TMP_Settings.defaultFontAsset;
        }

        GameObject toggleObj = new GameObject("Toggle", typeof(RectTransform), typeof(Toggle), typeof(Image), typeof(LayoutElement));
        toggleObj.transform.SetParent(row.transform, false);
        LayoutElement toggleLayout = toggleObj.GetComponent<LayoutElement>();
        toggleLayout.minWidth = 34f;
        toggleLayout.preferredWidth = 34f;
        toggleLayout.minHeight = 34f;
        toggleLayout.preferredHeight = 34f;
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
        return CreateSectionContainer(parent, objectName, 160f);
    }

    private Transform CreateSectionContainer(Transform parent, string objectName, float preferredHeight)
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
        sectionLayout.padding = new RectOffset(18, 18, 14, 14);
        sectionLayout.spacing = 12f;
        sectionLayout.childControlWidth = true;
        sectionLayout.childControlHeight = true;
        sectionLayout.childForceExpandWidth = true;
        sectionLayout.childForceExpandHeight = false;

        LayoutElement sectionSize = sectionObject.GetComponent<LayoutElement>();
        sectionSize.minHeight = preferredHeight;
        sectionSize.preferredHeight = preferredHeight;

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

    private Transform CreateScrollableSettingsContent(Transform parent)
    {
        GameObject scrollRoot = new GameObject("SettingsScrollRoot", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(ScrollRect), typeof(RectMask2D));
        scrollRoot.transform.SetParent(parent, false);

        LayoutElement layoutElement = scrollRoot.GetComponent<LayoutElement>();
        layoutElement.flexibleHeight = 1f;
        layoutElement.minHeight = 300f;

        Image background = scrollRoot.GetComponent<Image>();
        background.color = new Color(1f, 1f, 1f, 0.02f);

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(scrollRoot.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 14f;
        contentLayout.padding = new RectOffset(0, 0, 0, 4);
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        ScrollRect scrollRect = scrollRoot.GetComponent<ScrollRect>();
        scrollRect.viewport = scrollRoot.GetComponent<RectTransform>();
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;

        return content.transform;
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
            LocalizationTable.Get("credits_game_by") + ": " + developerName + "\n\n" +
            LocalizationTable.Get("credits_used_assets") + ":\n" +
            "- Main Menu Artwork (Assets/Images/MainMenuImage.png)\n" +
            "- SimplePoly City - Low Poly Assets\n" +
            "- Nebula - Free low poly car pack\n" +
            "- EasyRoads3D\n" +
            "- Cardboard Box (Rigged)\n" +
            "- Kenney UI Pack\n" +
            "- HQP Studios Low Poly 3D Icons - Pack Lite\n" +
            "- Keypad\n" +
            "- TextMesh Pro\n\n" +
            LocalizationTable.Get("credits_thanks");
    }

    private void HandleLocaleChanged()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        MenuPanelState activePanel = currentPanelState;
        CleanupUi(false);
        BuildUi();

        switch (activePanel)
        {
            case MenuPanelState.Settings:
                ShowSettingsPanel();
                break;
            case MenuPanelState.Credits:
                ShowCreditsPanel();
                break;
            default:
                ShowMainPanel();
                break;
        }

        RefreshSettingsControls();
    }

    private void ResetUiReferences(bool clearCanvasReference = true)
    {
        mainPanel = null;
        settingsPanel = null;
        creditsPanel = null;
        if (clearCanvasReference)
        {
            createdCanvasObject = null;
        }
        backgroundObject = null;
        overlayObject = null;
        mainPanelCanvasGroup = null;
        settingsPanelCanvasGroup = null;
        creditsPanelCanvasGroup = null;
        masterVolumeSlider = null;
        musicVolumeSlider = null;
        sfxVolumeSlider = null;
        qualityDropdown = null;
        resolutionDropdown = null;
        fullScreenToggle = null;
        fpsDropdown = null;
        speedUnitDropdown = null;
        languageDropdown = null;
        colorBlindDropdown = null;
        textScaleSlider = null;
        highContrastToggle = null;
        availableResolutions = null;
    }

    private static Canvas EnsureCanvas(out bool wasCreated)
    {
        // Check if a MainMenuCanvas already exists (e.g. from a previous run)
        GameObject existing = GameObject.Find("MainMenuCanvas");
        if (existing != null)
        {
            Canvas existingCanvas = existing.GetComponent<Canvas>();
            if (existingCanvas != null)
            {
                wasCreated = false;
                return existingCanvas;
            }
        }

        // Always create our own canvas so it doesn't conflict with GlobalUICanvas
        // or other DontDestroyOnLoad canvases that may overlay this one.
        wasCreated = true;
        GameObject canvasObject = new GameObject("MainMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static void EnsureEventSystem()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            eventSystem = FindFirstObjectByType<EventSystem>();
        }

        if (eventSystem == null)
        {
            eventSystem = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
        }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        StandaloneInputModule standalone = eventSystem.GetComponent<StandaloneInputModule>();
        if (standalone != null)
        {
            Object.Destroy(standalone);
        }
#else
        if (eventSystem.GetComponent<StandaloneInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<StandaloneInputModule>();
        }
#endif

        if (!eventSystem.gameObject.activeSelf)
        {
            eventSystem.gameObject.SetActive(true);
        }
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
            SceneTransitionManager.TransitionToScene(sceneToLoad);
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
