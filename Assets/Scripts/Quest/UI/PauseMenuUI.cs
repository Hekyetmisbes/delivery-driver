using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DeliveryDriver.Quest;
using DeliveryDriver.UI;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DeliveryDriver.Quest.UI
{
    public class PauseMenuUI : MonoBehaviour
    {
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private SettingsMenuUI settingsMenu;
        [SerializeField] private QuestStatisticsUI statisticsMenu;
        [SerializeField] private bool pauseTimeScale = true;
        [SerializeField] private bool showStatsOnPause = true;
        [SerializeField] private bool buildKenneyPauseMenuAtRuntime = true;
        [SerializeField] private bool useKenneySkin = true;
        [SerializeField] private Vector2 pauseMenuSize = new Vector2(760f, 840f);
        [SerializeField] private string resumeButtonLabel = "Devam Et";
        [SerializeField] private string quitButtonLabel = "Oyundan Cik";
        [SerializeField] private string resumeSceneName = "";
        [SerializeField] private string quitSceneName = "";
        [SerializeField] private bool enablePauseToggleInput = true;
        [SerializeField] private bool startPaused;

        [Header("Kenney Skin")]
        [SerializeField] private Sprite panelBackgroundSprite;
        [SerializeField] private Sprite buttonBackgroundSprite;
        [SerializeField] private Sprite sliderBackgroundSprite;
        [SerializeField] private Sprite sliderFillSprite;
        [SerializeField] private Sprite sliderHandleSprite;
        [SerializeField] private Sprite dropdownBackgroundSprite;
        [SerializeField] private Sprite toggleBackgroundSprite;
        [SerializeField] private Sprite toggleCheckmarkSprite;

        private Slider masterVolumeSlider;
        private Slider musicVolumeSlider;
        private Slider sfxVolumeSlider;
        private TMP_Dropdown qualityDropdown;
        private TMP_Dropdown resolutionDropdown;
        private Toggle fullScreenToggle;
        private TMP_Dropdown fpsDropdown;
        private TMP_Dropdown speedUnitDropdown;
        private TMP_Dropdown colorBlindDropdown;
        private Slider textScaleSlider;
        private Toggle highContrastToggle;
        private Resolution[] availableResolutions;
        private readonly System.Collections.Generic.List<string> qualityTierOptions =
            new System.Collections.Generic.List<string> { "Dusuk", "Orta", "Yuksek" };
        private int[] qualityTierToProjectQuality = { 0, 1, 2 };
        private bool suppressSliderCallbacks;
        private bool suppressGraphicsCallbacks;
        private bool runtimePausePanelBuilt;
        private CanvasGroup pausePanelCanvasGroup;

        private bool isPaused;

        private void Start()
        {
            EnsurePauseMenu();
            SetPaused(startPaused);
        }

        private void Update()
        {
            HandlePauseInput();
        }

        private void HandlePauseInput()
        {
            if (!enablePauseToggleInput)
            {
                return;
            }

#if ENABLE_INPUT_SYSTEM
            bool escPressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#if ENABLE_LEGACY_INPUT_MANAGER
            escPressed = escPressed || Input.GetKeyDown(KeyCode.Escape);
#endif
            if (escPressed)
            {
                TogglePause();
            }
#else
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TogglePause();
            }
#endif
        }

        public void TogglePause()
        {
            SetPaused(!isPaused);
        }

        public void SetPaused(bool paused)
        {
            isPaused = paused;
            RefreshAudioSliders();
            RefreshGraphicsControls();

            if (pausePanel != null)
            {
                if (paused)
                {
                    pausePanel.SetActive(true);
                    if (pausePanelCanvasGroup != null)
                    {
                        pausePanelCanvasGroup.alpha = 0f;
                        UIAnimationHelper.FadeIn(this, pausePanelCanvasGroup, UIThemeConstants.PanelFadeDuration);
                        UIAnimationHelper.ScaleIn(this, pausePanel.GetComponent<RectTransform>(), UIThemeConstants.PanelScaleDuration);
                    }
                }
                else
                {
                    if (pausePanelCanvasGroup != null)
                    {
                        UIAnimationHelper.FadeOut(this, pausePanelCanvasGroup, UIThemeConstants.PanelFadeDuration * 0.5f, () =>
                        {
                            pausePanel.SetActive(false);
                        });
                    }
                    else
                    {
                        pausePanel.SetActive(false);
                    }
                }
            }

            if (settingsMenu != null && !runtimePausePanelBuilt)
            {
                settingsMenu.SetOpen(paused);
            }

            if (statisticsMenu != null && showStatsOnPause)
            {
                statisticsMenu.SetVisible(paused);
            }

            if (pauseTimeScale)
            {
                Time.timeScale = paused ? 0f : 1f;
            }

            if (QuestManager.Instance != null && QuestManager.Instance.CurrentQuest != null)
            {
                if (paused)
                {
                    QuestManager.Instance.CurrentQuest.PauseQuest();
                }
                else
                {
                    QuestManager.Instance.CurrentQuest.ResumeQuest();
                }
            }
        }

        private void EnsurePauseMenu()
        {
            if (!buildKenneyPauseMenuAtRuntime || runtimePausePanelBuilt)
            {
                return;
            }

            ResolveSkinSprites();
            EnsureEventSystem();

            if (pausePanel == null)
            {
                pausePanel = BuildPausePanel();
            }

            runtimePausePanelBuilt = pausePanel != null;
            RefreshAudioSliders();
        }

        private GameObject BuildPausePanel()
        {
            Canvas rootCanvas = FindFirstObjectByType<Canvas>();
            if (rootCanvas == null)
            {
                GameObject canvasObject = new GameObject("PauseMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                rootCanvas = canvasObject.GetComponent<Canvas>();
                rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            GameObject panelObject = new GameObject("PausePanel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(rootCanvas.transform, false);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, 12f);
            panelRect.sizeDelta = new Vector2(Mathf.Max(pauseMenuSize.x, 820f), Mathf.Max(pauseMenuSize.y, 860f));

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.05f, 0.08f, 0.12f, 0.98f);
            if (panelBackgroundSprite != null)
            {
                panelImage.sprite = panelBackgroundSprite;
                panelImage.type = Image.Type.Sliced;
            }

            pausePanelCanvasGroup = panelObject.AddComponent<CanvasGroup>();

            VerticalLayoutGroup layout = panelObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 28, 24);
            layout.spacing = 16f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            CreateHeader(panelObject.transform, LocalizationTable.Get("paused_title"));
            Transform audioSection = CreateSectionContainer(panelObject.transform, "SesBolumu");
            CreateSectionLabel(audioSection, LocalizationTable.Get("audio"));
            masterVolumeSlider = CreateLabeledSlider(audioSection, LocalizationTable.Get("master_volume"));
            musicVolumeSlider = CreateLabeledSlider(audioSection, LocalizationTable.Get("music_volume"));
            sfxVolumeSlider = CreateLabeledSlider(audioSection, LocalizationTable.Get("sfx_volume"));

            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            }

            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            }

            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            }

            Transform graphicsSection = CreateSectionContainer(panelObject.transform, "GrafikBolumu");
            CreateSectionLabel(graphicsSection, LocalizationTable.Get("graphics"));
            ConfigureQualityTierMapping();

            qualityTierOptions[0] = LocalizationTable.Get("quality_low");
            qualityTierOptions[1] = LocalizationTable.Get("quality_medium");
            qualityTierOptions[2] = LocalizationTable.Get("quality_high");
            qualityDropdown = CreateLabeledDropdown(graphicsSection, LocalizationTable.Get("quality"), qualityTierOptions);
            qualityDropdown.value = ConvertProjectQualityToTierIndex(QualitySettings.GetQualityLevel());
            qualityDropdown.onValueChanged.AddListener(OnQualityChanged);

            BuildResolutionDropdown(graphicsSection);

            fullScreenToggle = CreateLabeledToggle(graphicsSection, LocalizationTable.Get("fullscreen"));
            fullScreenToggle.isOn = Screen.fullScreen;
            fullScreenToggle.onValueChanged.AddListener(OnFullScreenChanged);

            fpsDropdown = CreateLabeledDropdown(graphicsSection, LocalizationTable.Get("fps_limit"),
                new System.Collections.Generic.List<string> { "30", "60", "120", LocalizationTable.Get("fps_unlimited") });
            SetFpsDropdownValue();
            fpsDropdown.onValueChanged.AddListener(OnFpsChanged);

            speedUnitDropdown = CreateLabeledDropdown(graphicsSection, LocalizationTable.Get("speed_unit"),
                new System.Collections.Generic.List<string> { "KMH", "MPH" });
            speedUnitDropdown.onValueChanged.AddListener(OnSpeedUnitChanged);

            // Accessibility section
            Transform accessibilitySection = CreateSectionContainer(panelObject.transform, "ErisilebilirlikBolumu");
            CreateSectionLabel(accessibilitySection, LocalizationTable.Get("accessibility"));

            colorBlindDropdown = CreateLabeledDropdown(accessibilitySection, LocalizationTable.Get("color_blind_mode"),
                new System.Collections.Generic.List<string>
                {
                    LocalizationTable.Get("color_blind_none"),
                    LocalizationTable.Get("color_blind_protanopia"),
                    LocalizationTable.Get("color_blind_deuteranopia"),
                    LocalizationTable.Get("color_blind_tritanopia")
                });
            if (GameSettings.Instance != null) colorBlindDropdown.value = GameSettings.Instance.ColorBlindMode;
            colorBlindDropdown.onValueChanged.AddListener(OnColorBlindModeChanged);

            textScaleSlider = CreateLabeledSlider(accessibilitySection, LocalizationTable.Get("text_scale"));
            textScaleSlider.minValue = 0.8f;
            textScaleSlider.maxValue = 1.5f;
            if (GameSettings.Instance != null) textScaleSlider.value = GameSettings.Instance.TextScaleMultiplier;
            textScaleSlider.onValueChanged.AddListener(OnTextScaleChanged);

            highContrastToggle = CreateLabeledToggle(accessibilitySection, LocalizationTable.Get("high_contrast"));
            if (GameSettings.Instance != null) highContrastToggle.isOn = GameSettings.Instance.HighContrastMode;
            highContrastToggle.onValueChanged.AddListener(OnHighContrastChanged);

            GameObject footerSpacer = new GameObject("FooterSpacer", typeof(RectTransform), typeof(LayoutElement));
            footerSpacer.transform.SetParent(panelObject.transform, false);
            LayoutElement footerSpacerLayout = footerSpacer.GetComponent<LayoutElement>();
            footerSpacerLayout.minHeight = 10f;

            GameObject buttonRow = new GameObject("ButtonRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            buttonRow.transform.SetParent(panelObject.transform, false);
            HorizontalLayoutGroup buttonLayout = buttonRow.GetComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 10f;
            buttonLayout.childControlWidth = true;
            buttonLayout.childControlHeight = true;
            buttonLayout.childForceExpandWidth = true;
            buttonLayout.childForceExpandHeight = false;

            LayoutElement buttonRowLayout = buttonRow.GetComponent<LayoutElement>();
            buttonRowLayout.minHeight = 58f;

            Button resumeButton = CreateButton(buttonRow.transform, "ResumeButton", GetResumeButtonLabel(), UIThemeConstants.ButtonGreen);
            Button quitButton = CreateButton(buttonRow.transform, "QuitButton", GetQuitButtonLabel(), UIThemeConstants.ButtonRed);

            UIButtonEnhancer.EnhanceButton(resumeButton);
            UIButtonEnhancer.EnhanceButton(quitButton);

            if (resumeButton != null)
            {
                resumeButton.onClick.AddListener(OnResumeButtonClicked);
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(() =>
                {
                    ConfirmationDialog.Show(
                        LocalizationTable.Get("confirm_quit_title"),
                        LocalizationTable.Get("confirm_quit"),
                        OnQuitButtonClicked);
                });
            }

            return panelObject;
        }

        private Transform CreateSectionContainer(Transform parent, string objectName)
        {
            GameObject sectionObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            sectionObject.transform.SetParent(parent, false);

            Image sectionImage = sectionObject.GetComponent<Image>();
            sectionImage.color = new Color(0.08f, 0.14f, 0.22f, 0.9f);
            if (panelBackgroundSprite != null)
            {
                sectionImage.sprite = panelBackgroundSprite;
                sectionImage.type = Image.Type.Sliced;
            }

            VerticalLayoutGroup sectionLayout = sectionObject.GetComponent<VerticalLayoutGroup>();
            sectionLayout.padding = new RectOffset(20, 20, 16, 16);
            sectionLayout.spacing = 10f;
            sectionLayout.childControlWidth = true;
            sectionLayout.childControlHeight = true;
            sectionLayout.childForceExpandWidth = true;
            sectionLayout.childForceExpandHeight = false;

            LayoutElement sectionSize = sectionObject.GetComponent<LayoutElement>();
            sectionSize.minHeight = 0f;

            return sectionObject.transform;
        }

        private void CreateSectionLabel(Transform parent, string label)
        {
            GameObject headerObject = new GameObject($"{label}Header", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            headerObject.transform.SetParent(parent, false);

            LayoutElement layout = headerObject.GetComponent<LayoutElement>();
            layout.minHeight = 28f;

            TextMeshProUGUI text = headerObject.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 20f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Left;
            text.color = new Color(0.78f, 0.87f, 0.98f, 0.95f);
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }
        }

        private void CreateHeader(Transform parent, string label)
        {
            GameObject headerObject = new GameObject("Header", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            headerObject.transform.SetParent(parent, false);
            LayoutElement layout = headerObject.GetComponent<LayoutElement>();
            layout.minHeight = 54f;

            TextMeshProUGUI text = headerObject.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 36f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.95f, 0.98f, 1f, 1f);
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }
        }

        private Slider CreateLabeledSlider(Transform parent, string label)
        {
            GameObject rowObject = new GameObject($"{label}Row", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            rowObject.transform.SetParent(parent, false);

            VerticalLayoutGroup rowLayout = rowObject.GetComponent<VerticalLayoutGroup>();
            rowLayout.spacing = 6f;
            rowLayout.childControlHeight = true;
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childForceExpandWidth = true;

            LayoutElement rowElement = rowObject.GetComponent<LayoutElement>();
            rowElement.minHeight = 68f;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(rowObject.transform, false);
            TextMeshProUGUI labelText = labelObject.GetComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 20f;
            labelText.fontStyle = FontStyles.Bold;
            labelText.color = new Color(0.9f, 0.95f, 1f, 1f);
            labelText.alignment = TextAlignmentOptions.Left;
            if (TMP_Settings.defaultFontAsset != null)
            {
                labelText.font = TMP_Settings.defaultFontAsset;
            }

            Slider slider = CreateSlider(rowObject.transform, $"{label}Slider");
            return slider;
        }

        private Slider CreateSlider(Transform parent, string objectName)
        {
            GameObject sliderObject = new GameObject(objectName, typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
            sliderObject.transform.SetParent(parent, false);
            RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0f, 0.5f);
            sliderRect.anchorMax = new Vector2(1f, 0.5f);
            sliderRect.pivot = new Vector2(0.5f, 0.5f);
            sliderRect.sizeDelta = new Vector2(0f, 26f);
            LayoutElement layout = sliderObject.GetComponent<LayoutElement>();
            layout.minHeight = 26f;

            GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(sliderObject.transform, false);
            RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0.25f);
            backgroundRect.anchorMax = new Vector2(1f, 0.75f);
            backgroundRect.offsetMin = new Vector2(0f, 0f);
            backgroundRect.offsetMax = new Vector2(0f, 0f);
            Image backgroundImage = backgroundObject.GetComponent<Image>();
            backgroundImage.color = new Color(0.22f, 0.26f, 0.32f, 1f);
            if (sliderBackgroundSprite != null)
            {
                backgroundImage.sprite = sliderBackgroundSprite;
                backgroundImage.type = Image.Type.Sliced;
            }

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObject.transform, false);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0f);
            fillAreaRect.anchorMax = new Vector2(1f, 1f);
            fillAreaRect.offsetMin = new Vector2(16f, 7f);
            fillAreaRect.offsetMax = new Vector2(-16f, -7f);

            GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(fillArea.transform, false);
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image fillImage = fillObject.GetComponent<Image>();
            fillImage.color = new Color(0.16f, 0.52f, 0.88f, 1f);
            fillImage.preserveAspect = false;
            if (sliderFillSprite != null)
            {
                fillImage.sprite = sliderFillSprite;
                fillImage.type = Image.Type.Sliced;
            }

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderObject.transform, false);
            RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = new Vector2(0f, 0f);
            handleAreaRect.anchorMax = new Vector2(1f, 1f);
            handleAreaRect.offsetMin = new Vector2(16f, 0f);
            handleAreaRect.offsetMax = new Vector2(-16f, 0f);

            GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleObject.transform.SetParent(handleArea.transform, false);
            RectTransform handleRect = handleObject.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(28f, 28f);
            Image handleImage = handleObject.GetComponent<Image>();
            handleImage.color = Color.white;
            handleImage.preserveAspect = true;
            if (sliderHandleSprite != null)
            {
                handleImage.sprite = sliderHandleSprite;
                handleImage.type = Image.Type.Simple;
            }

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.targetGraphic = handleImage;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private Button CreateButton(Transform parent, string objectName, string label, Color color)
        {
            GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
            layout.minHeight = 52f;

            Image image = buttonObject.GetComponent<Image>();
            image.color = color;
            if (buttonBackgroundSprite != null)
            {
                image.sprite = buttonBackgroundSprite;
                image.type = Image.Type.Sliced;
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
            text.fontSize = 22f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            return button;
        }

        private void RefreshAudioSliders()
        {
            GameSettings settings = GameSettings.Instance;
            if (settings == null)
            {
                return;
            }

            suppressSliderCallbacks = true;
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.value = settings.MasterVolume;
            }

            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.value = settings.MusicVolume;
            }

            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.value = settings.SfxVolume;
            }

            suppressSliderCallbacks = false;
        }

        private void OnMasterVolumeChanged(float value)
        {
            if (suppressSliderCallbacks || GameSettings.Instance == null)
            {
                return;
            }

            GameSettings.Instance.SetMasterVolume(value);
            GameSettings.Instance.ApplySettings();
            GameSettings.Instance.SaveSettings();
        }

        private void OnMusicVolumeChanged(float value)
        {
            if (suppressSliderCallbacks || GameSettings.Instance == null)
            {
                return;
            }

            GameSettings.Instance.SetMusicVolume(value);
            GameSettings.Instance.ApplySettings();
            GameSettings.Instance.SaveSettings();
        }

        private void OnSfxVolumeChanged(float value)
        {
            if (suppressSliderCallbacks || GameSettings.Instance == null)
            {
                return;
            }

            GameSettings.Instance.SetSfxVolume(value);
            GameSettings.Instance.ApplySettings();
            GameSettings.Instance.SaveSettings();
        }

        private void RefreshGraphicsControls()
        {
            GameSettings settings = GameSettings.Instance;
            if (settings == null)
            {
                return;
            }

            suppressGraphicsCallbacks = true;

            if (qualityDropdown != null)
            {
                int qualityLevel = settings.QualityLevel >= 0
                    ? settings.QualityLevel
                    : QualitySettings.GetQualityLevel();
                qualityDropdown.value = ConvertProjectQualityToTierIndex(qualityLevel);
            }

            if (resolutionDropdown != null && settings.ResolutionIndex >= 0)
            {
                resolutionDropdown.value = settings.ResolutionIndex;
            }

            if (fullScreenToggle != null)
            {
                fullScreenToggle.isOn = settings.FullScreen;
            }

            if (fpsDropdown != null)
            {
                SetFpsDropdownValue();
            }

            if (speedUnitDropdown != null)
            {
                speedUnitDropdown.value = settings.SpeedUnitPreference == SpeedUnitPreference.Mph ? 1 : 0;
            }

            suppressGraphicsCallbacks = false;
        }

        private void SetFpsDropdownValue()
        {
            if (fpsDropdown == null) return;
            int fps = GameSettings.Instance != null
                ? GameSettings.Instance.TargetFps
                : Application.targetFrameRate;
            switch (fps)
            {
                case 30: fpsDropdown.value = 0; break;
                case 60: fpsDropdown.value = 1; break;
                case 120: fpsDropdown.value = 2; break;
                default: fpsDropdown.value = 3; break;
            }
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
            resolutionDropdown = CreateLabeledDropdown(parent, "Cozunurluk", options);
            resolutionDropdown.value = currentIndex;
            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        }

        private void OnQualityChanged(int index)
        {
            if (suppressGraphicsCallbacks || GameSettings.Instance == null) return;
            int projectQualityIndex = ConvertTierIndexToProjectQuality(index);
            GameSettings.Instance.SetQualityLevel(projectQualityIndex);
            GameSettings.Instance.ApplySettings();
            GameSettings.Instance.SaveSettings();
        }

        private void OnResolutionChanged(int index)
        {
            if (suppressGraphicsCallbacks || GameSettings.Instance == null) return;
            if (index >= 0 && index < availableResolutions.Length)
            {
                Resolution res = availableResolutions[index];
                bool isFullScreen = fullScreenToggle != null && fullScreenToggle.isOn;
                Screen.SetResolution(res.width, res.height, isFullScreen);
                GameSettings.Instance.SetResolutionIndex(index);
                GameSettings.Instance.SetResolutionSize(res.width, res.height);
                GameSettings.Instance.SetFullScreen(isFullScreen);
                GameSettings.Instance.SaveSettings();
            }
        }

        private void OnFullScreenChanged(bool value)
        {
            if (suppressGraphicsCallbacks || GameSettings.Instance == null) return;
            GameSettings.Instance.SetFullScreen(value);
            GameSettings.Instance.ApplySettings();
            GameSettings.Instance.SaveSettings();
        }

        private void OnFpsChanged(int index)
        {
            if (suppressGraphicsCallbacks || GameSettings.Instance == null) return;
            int[] fpsValues = { 30, 60, 120, -1 };
            GameSettings.Instance.SetTargetFps(fpsValues[index]);
            GameSettings.Instance.ApplySettings();
            GameSettings.Instance.SaveSettings();
        }

        private void OnSpeedUnitChanged(int index)
        {
            if (suppressGraphicsCallbacks || GameSettings.Instance == null) return;
            GameSettings.Instance.SetSpeedUnitPreference(index == 1
                ? SpeedUnitPreference.Mph
                : SpeedUnitPreference.Kmh);
            GameSettings.Instance.ApplySettings();
            GameSettings.Instance.SaveSettings();
        }

        private void OnColorBlindModeChanged(int mode)
        {
            if (GameSettings.Instance == null) return;
            GameSettings.Instance.SetColorBlindMode(mode);
            GameSettings.Instance.SaveSettings();
        }

        private void OnTextScaleChanged(float value)
        {
            if (GameSettings.Instance == null) return;
            GameSettings.Instance.SetTextScaleMultiplier(value);
            GameSettings.Instance.SaveSettings();
        }

        private void OnHighContrastChanged(bool value)
        {
            if (GameSettings.Instance == null) return;
            GameSettings.Instance.SetHighContrastMode(value);
            GameSettings.Instance.SaveSettings();
        }

        private TMP_Dropdown CreateLabeledDropdown(Transform parent, string label, System.Collections.Generic.List<string> options)
        {
            GameObject rowObject = new GameObject($"{label}Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rowObject.transform.SetParent(parent, false);

            HorizontalLayoutGroup rowLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 14f;
            rowLayout.childControlHeight = true;
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            LayoutElement rowElement = rowObject.GetComponent<LayoutElement>();
            rowElement.minHeight = 46f;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            labelObject.transform.SetParent(rowObject.transform, false);
            LayoutElement labelLayout = labelObject.GetComponent<LayoutElement>();
            labelLayout.preferredWidth = 230f;
            labelLayout.flexibleWidth = 0f;

            TextMeshProUGUI labelText = labelObject.GetComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 20f;
            labelText.fontStyle = FontStyles.Bold;
            labelText.color = new Color(0.9f, 0.95f, 1f, 1f);
            labelText.alignment = TextAlignmentOptions.Left;
            if (TMP_Settings.defaultFontAsset != null)
            {
                labelText.font = TMP_Settings.defaultFontAsset;
            }

            GameObject dropdownObject = new GameObject($"{label}Dropdown", typeof(RectTransform), typeof(TMP_Dropdown), typeof(Image), typeof(LayoutElement));
            dropdownObject.transform.SetParent(rowObject.transform, false);

            LayoutElement dropdownLayout = dropdownObject.GetComponent<LayoutElement>();
            dropdownLayout.minHeight = 44f;
            dropdownLayout.preferredWidth = 300f;
            dropdownLayout.flexibleWidth = 1f;

            Image dropdownImage = dropdownObject.GetComponent<Image>();
            dropdownImage.color = new Color(0.22f, 0.26f, 0.32f, 1f);
            if (dropdownBackgroundSprite != null)
            {
                dropdownImage.sprite = dropdownBackgroundSprite;
                dropdownImage.type = Image.Type.Sliced;
            }
            else if (buttonBackgroundSprite != null)
            {
                dropdownImage.sprite = buttonBackgroundSprite;
                dropdownImage.type = Image.Type.Sliced;
            }

            // Caption text
            GameObject captionObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            captionObject.transform.SetParent(dropdownObject.transform, false);
            RectTransform captionRect = captionObject.GetComponent<RectTransform>();
            captionRect.anchorMin = Vector2.zero;
            captionRect.anchorMax = Vector2.one;
            captionRect.offsetMin = new Vector2(14f, 4f);
            captionRect.offsetMax = new Vector2(-40f, -4f);
            TextMeshProUGUI captionText = captionObject.GetComponent<TextMeshProUGUI>();
            captionText.fontSize = 17f;
            captionText.color = Color.white;
            captionText.alignment = TextAlignmentOptions.Left;
            if (TMP_Settings.defaultFontAsset != null)
            {
                captionText.font = TMP_Settings.defaultFontAsset;
            }

            // Arrow indicator
            GameObject arrowObject = new GameObject("Arrow", typeof(RectTransform), typeof(TextMeshProUGUI));
            arrowObject.transform.SetParent(dropdownObject.transform, false);
            RectTransform arrowRect = arrowObject.GetComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(1f, 0f);
            arrowRect.anchorMax = new Vector2(1f, 1f);
            arrowRect.pivot = new Vector2(1f, 0.5f);
            arrowRect.sizeDelta = new Vector2(32f, 0f);
            arrowRect.anchoredPosition = new Vector2(-8f, 0f);
            TextMeshProUGUI arrowText = arrowObject.GetComponent<TextMeshProUGUI>();
            arrowText.text = "\u25BC";
            arrowText.fontSize = 13f;
            arrowText.color = Color.white;
            arrowText.alignment = TextAlignmentOptions.Center;
            if (TMP_Settings.defaultFontAsset != null)
            {
                arrowText.font = TMP_Settings.defaultFontAsset;
            }

            // Template
            GameObject templateObject = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            templateObject.transform.SetParent(dropdownObject.transform, false);
            RectTransform templateRect = templateObject.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 1f);
            templateRect.anchorMax = new Vector2(1f, 1f);
            templateRect.pivot = new Vector2(0.5f, 0f);
            templateRect.anchoredPosition = new Vector2(0f, 2f);
            templateRect.sizeDelta = new Vector2(0f, 150f);
            Image templateImage = templateObject.GetComponent<Image>();
            templateImage.color = new Color(0.15f, 0.18f, 0.22f, 0.98f);
            if (dropdownBackgroundSprite != null)
            {
                templateImage.sprite = dropdownBackgroundSprite;
                templateImage.type = Image.Type.Sliced;
            }
            else if (panelBackgroundSprite != null)
            {
                templateImage.sprite = panelBackgroundSprite;
                templateImage.type = Image.Type.Sliced;
            }

            // Viewport
            GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
            viewportObject.transform.SetParent(templateObject.transform, false);
            RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            Image viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = Color.white;
            Mask viewportMask = viewportObject.GetComponent<Mask>();
            viewportMask.showMaskGraphic = false;

            // Content
            GameObject contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewportObject.transform, false);
            RectTransform contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 28f);

            // Item
            GameObject itemObject = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            itemObject.transform.SetParent(contentObject.transform, false);
            RectTransform itemRect = itemObject.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 0.5f);
            itemRect.anchorMax = new Vector2(1f, 0.5f);
            itemRect.sizeDelta = new Vector2(0f, 28f);

            // Item background
            GameObject itemBgObject = new GameObject("Item Background", typeof(RectTransform), typeof(Image));
            itemBgObject.transform.SetParent(itemObject.transform, false);
            RectTransform itemBgRect = itemBgObject.GetComponent<RectTransform>();
            itemBgRect.anchorMin = Vector2.zero;
            itemBgRect.anchorMax = Vector2.one;
            itemBgRect.offsetMin = Vector2.zero;
            itemBgRect.offsetMax = Vector2.zero;
            Image itemBgImage = itemBgObject.GetComponent<Image>();
            itemBgImage.color = new Color(0.25f, 0.35f, 0.5f, 0.5f);

            // Item label
            GameObject itemLabelObject = new GameObject("Item Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            itemLabelObject.transform.SetParent(itemObject.transform, false);
            RectTransform itemLabelRect = itemLabelObject.GetComponent<RectTransform>();
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(10f, 1f);
            itemLabelRect.offsetMax = new Vector2(-10f, -1f);
            TextMeshProUGUI itemLabelText = itemLabelObject.GetComponent<TextMeshProUGUI>();
            itemLabelText.fontSize = 16f;
            itemLabelText.color = Color.white;
            itemLabelText.alignment = TextAlignmentOptions.Left;
            if (TMP_Settings.defaultFontAsset != null)
            {
                itemLabelText.font = TMP_Settings.defaultFontAsset;
            }

            // Wire up toggle
            Toggle itemToggle = itemObject.GetComponent<Toggle>();
            itemToggle.targetGraphic = itemBgImage;
            itemToggle.isOn = true;

            // Wire up scroll rect
            ScrollRect scrollRect = templateObject.GetComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            templateObject.SetActive(false);

            // Wire up dropdown
            TMP_Dropdown dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
            dropdown.targetGraphic = dropdownImage;
            dropdown.template = templateRect;
            dropdown.captionText = captionText;
            dropdown.itemText = itemLabelText;
            dropdown.ClearOptions();
            dropdown.AddOptions(options);

            return dropdown;
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
            if (qualityTierToProjectQuality == null || qualityTierToProjectQuality.Length < 3)
            {
                ConfigureQualityTierMapping();
            }

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

        private int ConvertTierIndexToProjectQuality(int tierIndex)
        {
            if (qualityTierToProjectQuality == null || qualityTierToProjectQuality.Length < 3)
            {
                ConfigureQualityTierMapping();
            }

            int safeTier = Mathf.Clamp(tierIndex, 0, qualityTierToProjectQuality.Length - 1);
            return qualityTierToProjectQuality[safeTier];
        }

        private Toggle CreateLabeledToggle(Transform parent, string label)
        {
            GameObject rowObject = new GameObject($"{label}Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rowObject.transform.SetParent(parent, false);

            HorizontalLayoutGroup rowLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 14f;
            rowLayout.childControlHeight = true;
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            LayoutElement rowElement = rowObject.GetComponent<LayoutElement>();
            rowElement.minHeight = 46f;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            labelObject.transform.SetParent(rowObject.transform, false);
            LayoutElement labelLayout = labelObject.GetComponent<LayoutElement>();
            labelLayout.preferredWidth = 230f;
            labelLayout.flexibleWidth = 1f;
            TextMeshProUGUI labelText = labelObject.GetComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 20f;
            labelText.fontStyle = FontStyles.Bold;
            labelText.color = new Color(0.9f, 0.95f, 1f, 1f);
            labelText.alignment = TextAlignmentOptions.Left;
            if (TMP_Settings.defaultFontAsset != null)
            {
                labelText.font = TMP_Settings.defaultFontAsset;
            }

            GameObject toggleObject = new GameObject($"{label}Toggle", typeof(RectTransform), typeof(Toggle), typeof(LayoutElement));
            toggleObject.transform.SetParent(rowObject.transform, false);
            LayoutElement toggleLayout = toggleObject.GetComponent<LayoutElement>();
            toggleLayout.minWidth = 42f;
            toggleLayout.minHeight = 42f;
            toggleLayout.preferredWidth = 42f;
            toggleLayout.flexibleWidth = 0f;

            // Background
            GameObject bgObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgObject.transform.SetParent(toggleObject.transform, false);
            RectTransform bgRect = bgObject.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.5f, 0.5f);
            bgRect.anchorMax = new Vector2(0.5f, 0.5f);
            bgRect.sizeDelta = new Vector2(36f, 36f);
            Image bgImage = bgObject.GetComponent<Image>();
            bgImage.color = new Color(0.22f, 0.26f, 0.32f, 1f);
            if (toggleBackgroundSprite != null)
            {
                bgImage.sprite = toggleBackgroundSprite;
                bgImage.type = Image.Type.Sliced;
            }
            else if (buttonBackgroundSprite != null)
            {
                bgImage.sprite = buttonBackgroundSprite;
                bgImage.type = Image.Type.Sliced;
            }

            // Checkmark (Image-based to avoid TMP glyph fallback warnings)
            GameObject checkObject = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkObject.transform.SetParent(bgObject.transform, false);
            RectTransform checkRect = checkObject.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkRect.pivot = new Vector2(0.5f, 0.5f);
            checkRect.sizeDelta = new Vector2(20f, 20f);

            Image checkImage = checkObject.GetComponent<Image>();
            checkImage.color = new Color(0.16f, 0.52f, 0.88f, 1f);
            if (toggleCheckmarkSprite != null)
            {
                checkImage.sprite = toggleCheckmarkSprite;
                checkImage.preserveAspect = true;
            }
            else
            {
                checkRect.sizeDelta = new Vector2(14f, 14f);
            }

            Toggle toggle = toggleObject.GetComponent<Toggle>();
            toggle.targetGraphic = bgImage;
            toggle.graphic = checkImage;
            toggle.isOn = false;

            return toggle;
        }

        public void ConfigureForSettingsScene(string mainMenuSceneName = "MainMenu")
        {
            pauseTimeScale = false;
            showStatsOnPause = false;
            enablePauseToggleInput = false;
            startPaused = true;
            resumeButtonLabel = "Ana Menu";
            resumeSceneName = mainMenuSceneName;
            quitButtonLabel = "Oyundan Cik";
            quitSceneName = string.Empty;
        }

        private string GetResumeButtonLabel()
        {
            return string.IsNullOrWhiteSpace(resumeButtonLabel)
                ? LocalizationTable.Get("resume")
                : resumeButtonLabel;
        }

        private string GetQuitButtonLabel()
        {
            return string.IsNullOrWhiteSpace(quitButtonLabel)
                ? LocalizationTable.Get("quit_game")
                : quitButtonLabel;
        }

        private void OnResumeButtonClicked()
        {
            if (!string.IsNullOrWhiteSpace(resumeSceneName))
            {
                Time.timeScale = 1f;
                SceneTransitionManager.TransitionToScene(resumeSceneName);
                return;
            }

            SetPaused(false);
        }

        private void OnQuitButtonClicked()
        {
            if (!string.IsNullOrWhiteSpace(quitSceneName))
            {
                Time.timeScale = 1f;
                SceneTransitionManager.TransitionToScene(quitSceneName);
                return;
            }

            QuitGame();
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void EnsureEventSystem()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                eventSystem = FindFirstObjectByType<EventSystem>();
            }

            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
                eventSystemObject.hideFlags = HideFlags.DontSave;
                eventSystem = eventSystemObject.GetComponent<EventSystem>();
            }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if (eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            StandaloneInputModule standalone = eventSystem.GetComponent<StandaloneInputModule>();
            if (standalone != null)
            {
                Destroy(standalone);
            }
#else
            if (eventSystem.GetComponent<StandaloneInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            }
#endif
        }

        private void ResolveSkinSprites()
        {
            if (!useKenneySkin)
            {
                return;
            }

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
            toggleCheckmarkSprite ??= RuntimeUiSkinLoader.LoadSprite(
                "UI/Kenney/icon_accept",
                "Assets/Resources/UI/FreeButtonSet/checkmark_64.png");
        }
    }
}
