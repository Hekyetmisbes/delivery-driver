using System.Collections.Generic;
using DeliveryDriver.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem.UI;
#endif

namespace DeliveryDriver.Quest.UI
{
    internal struct PauseMenuRuntimeViewConfig
    {
        public Vector2 PauseMenuSize { get; set; }
        public Sprite PanelBackgroundSprite { get; set; }
        public Sprite ButtonBackgroundSprite { get; set; }
        public Sprite SliderBackgroundSprite { get; set; }
        public Sprite SliderFillSprite { get; set; }
        public Sprite SliderHandleSprite { get; set; }
        public Sprite DropdownBackgroundSprite { get; set; }
        public Sprite ToggleBackgroundSprite { get; set; }
        public Sprite ToggleCheckmarkSprite { get; set; }
        public string ResumeButtonLabel { get; set; }
        public string QuitButtonLabel { get; set; }
    }

    internal sealed class PauseMenuRuntimeViewBuilder
    {
        private readonly PauseMenuRuntimeViewConfig config;

        public PauseMenuRuntimeViewBuilder(PauseMenuRuntimeViewConfig config)
        {
            this.config = config;
        }

        public PauseMenuRuntimeView Build()
        {
            EnsureEventSystem();
            Canvas rootCanvas = ResolveRootCanvas();

            GameObject panelObject = new GameObject("PausePanel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(rootCanvas.transform, false);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, 12f);
            panelRect.sizeDelta = new Vector2(Mathf.Max(config.PauseMenuSize.x, 820f), Mathf.Max(config.PauseMenuSize.y, 860f));

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.05f, 0.08f, 0.12f, 0.98f);
            if (config.PanelBackgroundSprite != null)
            {
                panelImage.sprite = config.PanelBackgroundSprite;
                panelImage.type = Image.Type.Sliced;
            }

            CanvasGroup panelCanvasGroup = panelObject.AddComponent<CanvasGroup>();
            Canvas pauseOverrideCanvas = panelObject.AddComponent<Canvas>();
            pauseOverrideCanvas.overrideSorting = true;
            pauseOverrideCanvas.sortingOrder = 9990;
            panelObject.AddComponent<GraphicRaycaster>();

            VerticalLayoutGroup layout = panelObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 28, 24);
            layout.spacing = 16f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            return BuildSections(panelObject, panelCanvasGroup);
        }

        private PauseMenuRuntimeView BuildSections(GameObject panelObject, CanvasGroup panelCanvasGroup)
        {
            CreateHeader(panelObject.transform, LocalizationTable.Get("paused_title"));
            Transform audioSection = CreateSectionContainer(panelObject.transform, "SesBolumu");
            CreateSectionLabel(audioSection, LocalizationTable.Get("audio"));
            Slider masterVolumeSlider = CreateLabeledSlider(audioSection, LocalizationTable.Get("master_volume"));
            Slider musicVolumeSlider = CreateLabeledSlider(audioSection, LocalizationTable.Get("music_volume"));
            Slider sfxVolumeSlider = CreateLabeledSlider(audioSection, LocalizationTable.Get("sfx_volume"));

            Transform graphicsSection = CreateSectionContainer(panelObject.transform, "GrafikBolumu");
            CreateSectionLabel(graphicsSection, LocalizationTable.Get("graphics"));
            TMP_Dropdown qualityDropdown = CreateLabeledDropdown(
                graphicsSection,
                LocalizationTable.Get("quality"),
                new List<string>
                {
                    LocalizationTable.Get("quality_low"),
                    LocalizationTable.Get("quality_medium"),
                    LocalizationTable.Get("quality_high")
                });

            ResolutionBuildResult resolutionBuild = BuildResolutionDropdown(graphicsSection);
            Toggle fullScreenToggle = CreateLabeledToggle(graphicsSection, LocalizationTable.Get("fullscreen"));
            TMP_Dropdown fpsDropdown = CreateLabeledDropdown(
                graphicsSection,
                LocalizationTable.Get("fps_limit"),
                new List<string> { "30", "60", "120", LocalizationTable.Get("fps_unlimited") });
            TMP_Dropdown speedUnitDropdown = CreateLabeledDropdown(
                graphicsSection,
                LocalizationTable.Get("speed_unit"),
                new List<string> { "KMH", "MPH" });

            Transform accessibilitySection = CreateSectionContainer(panelObject.transform, "ErisilebilirlikBolumu");
            CreateSectionLabel(accessibilitySection, LocalizationTable.Get("accessibility"));
            TMP_Dropdown colorBlindDropdown = CreateLabeledDropdown(
                accessibilitySection,
                LocalizationTable.Get("color_blind_mode"),
                new List<string>
                {
                    LocalizationTable.Get("color_blind_none"),
                    LocalizationTable.Get("color_blind_protanopia"),
                    LocalizationTable.Get("color_blind_deuteranopia"),
                    LocalizationTable.Get("color_blind_tritanopia")
                });
            Slider textScaleSlider = CreateLabeledSlider(accessibilitySection, LocalizationTable.Get("text_scale"));
            textScaleSlider.minValue = 0.8f;
            textScaleSlider.maxValue = 1.5f;
            Toggle highContrastToggle = CreateLabeledToggle(accessibilitySection, LocalizationTable.Get("high_contrast"));

            return BuildFooter(
                panelObject,
                panelCanvasGroup,
                masterVolumeSlider,
                musicVolumeSlider,
                sfxVolumeSlider,
                qualityDropdown,
                resolutionBuild,
                fullScreenToggle,
                fpsDropdown,
                speedUnitDropdown,
                colorBlindDropdown,
                textScaleSlider,
                highContrastToggle);
        }

        private PauseMenuRuntimeView BuildFooter(
            GameObject panelObject,
            CanvasGroup panelCanvasGroup,
            Slider masterVolumeSlider,
            Slider musicVolumeSlider,
            Slider sfxVolumeSlider,
            TMP_Dropdown qualityDropdown,
            ResolutionBuildResult resolutionBuild,
            Toggle fullScreenToggle,
            TMP_Dropdown fpsDropdown,
            TMP_Dropdown speedUnitDropdown,
            TMP_Dropdown colorBlindDropdown,
            Slider textScaleSlider,
            Toggle highContrastToggle)
        {
            GameObject footerSpacer = new GameObject("FooterSpacer", typeof(RectTransform), typeof(LayoutElement));
            footerSpacer.transform.SetParent(panelObject.transform, false);
            footerSpacer.GetComponent<LayoutElement>().minHeight = 10f;

            GameObject buttonRow = new GameObject("ButtonRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            buttonRow.transform.SetParent(panelObject.transform, false);
            HorizontalLayoutGroup buttonLayout = buttonRow.GetComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 10f;
            buttonLayout.childControlWidth = true;
            buttonLayout.childControlHeight = true;
            buttonLayout.childForceExpandWidth = true;
            buttonLayout.childForceExpandHeight = false;
            buttonRow.GetComponent<LayoutElement>().minHeight = 58f;

            Button resumeButton = CreateButton(buttonRow.transform, "ResumeButton", config.ResumeButtonLabel, UIThemeConstants.ButtonGreen);
            Button quitButton = CreateButton(buttonRow.transform, "QuitButton", config.QuitButtonLabel, UIThemeConstants.ButtonRed);
            UIButtonEnhancer.EnhanceButton(resumeButton);
            UIButtonEnhancer.EnhanceButton(quitButton);

            return new PauseMenuRuntimeView
            {
                Panel = panelObject,
                PanelCanvasGroup = panelCanvasGroup,
                MasterVolumeSlider = masterVolumeSlider,
                MusicVolumeSlider = musicVolumeSlider,
                SfxVolumeSlider = sfxVolumeSlider,
                QualityDropdown = qualityDropdown,
                ResolutionDropdown = resolutionBuild.Dropdown,
                FullScreenToggle = fullScreenToggle,
                FpsDropdown = fpsDropdown,
                SpeedUnitDropdown = speedUnitDropdown,
                ColorBlindDropdown = colorBlindDropdown,
                TextScaleSlider = textScaleSlider,
                HighContrastToggle = highContrastToggle,
                ResumeButton = resumeButton,
                QuitButton = quitButton,
                AvailableResolutions = resolutionBuild.AvailableResolutions
            };
        }

        private static Canvas ResolveRootCanvas()
        {
            Canvas rootCanvas = GlobalUiCoordinator.PrimaryCanvas;
            if (rootCanvas == null)
            {
                rootCanvas = Object.FindFirstObjectByType<Canvas>();
            }

            if (rootCanvas != null)
            {
                return rootCanvas;
            }

            GameObject canvasObject = new GameObject("PauseMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            rootCanvas = canvasObject.GetComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return rootCanvas;
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = EventSystem.current ?? Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
                if (Application.isPlaying)
                {
                    Object.DontDestroyOnLoad(eventSystemObject);
                }
                else
                {
                    eventSystemObject.hideFlags = HideFlags.DontSave;
                }

                eventSystem = eventSystemObject.GetComponent<EventSystem>();
            }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
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
        }

        private Transform CreateSectionContainer(Transform parent, string objectName)
        {
            GameObject sectionObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            sectionObject.transform.SetParent(parent, false);

            Image sectionImage = sectionObject.GetComponent<Image>();
            sectionImage.color = new Color(0.08f, 0.14f, 0.22f, 0.9f);
            if (config.PanelBackgroundSprite != null)
            {
                sectionImage.sprite = config.PanelBackgroundSprite;
                sectionImage.type = Image.Type.Sliced;
            }

            VerticalLayoutGroup sectionLayout = sectionObject.GetComponent<VerticalLayoutGroup>();
            sectionLayout.padding = new RectOffset(20, 20, 16, 16);
            sectionLayout.spacing = 10f;
            sectionLayout.childControlWidth = true;
            sectionLayout.childControlHeight = true;
            sectionLayout.childForceExpandWidth = true;
            sectionLayout.childForceExpandHeight = false;
            sectionObject.GetComponent<LayoutElement>().minHeight = 0f;
            return sectionObject.transform;
        }

        private static void CreateSectionLabel(Transform parent, string label)
        {
            GameObject headerObject = new GameObject($"{label}Header", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            headerObject.transform.SetParent(parent, false);
            headerObject.GetComponent<LayoutElement>().minHeight = 28f;

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

        private static void CreateHeader(Transform parent, string label)
        {
            GameObject headerObject = new GameObject("Header", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            headerObject.transform.SetParent(parent, false);
            headerObject.GetComponent<LayoutElement>().minHeight = 54f;

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
            rowObject.GetComponent<LayoutElement>().minHeight = 68f;

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

            return CreateSlider(rowObject.transform, $"{label}Slider");
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
            sliderObject.GetComponent<LayoutElement>().minHeight = 26f;

            GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(sliderObject.transform, false);
            RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0.25f);
            backgroundRect.anchorMax = new Vector2(1f, 0.75f);
            Image backgroundImage = backgroundObject.GetComponent<Image>();
            backgroundImage.color = new Color(0.22f, 0.26f, 0.32f, 1f);
            if (config.SliderBackgroundSprite != null)
            {
                backgroundImage.sprite = config.SliderBackgroundSprite;
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
            Image fillImage = fillObject.GetComponent<Image>();
            fillImage.color = new Color(0.16f, 0.52f, 0.88f, 1f);
            if (config.SliderFillSprite != null)
            {
                fillImage.sprite = config.SliderFillSprite;
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
            if (config.SliderHandleSprite != null)
            {
                handleImage.sprite = config.SliderHandleSprite;
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
            buttonObject.GetComponent<LayoutElement>().minHeight = 52f;

            Image image = buttonObject.GetComponent<Image>();
            image.color = color;
            if (config.ButtonBackgroundSprite != null)
            {
                image.sprite = config.ButtonBackgroundSprite;
                image.type = Image.Type.Sliced;
            }

            Button button = buttonObject.GetComponent<Button>();
            GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(buttonObject.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;

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

        private ResolutionBuildResult BuildResolutionDropdown(Transform parent)
        {
            Resolution[] allResolutions = Screen.resolutions;
            List<Resolution> uniqueResolutions = new List<Resolution>();
            List<string> options = new List<string>();
            int currentIndex = 0;
            int currentWidth = Screen.currentResolution.width;
            int currentHeight = Screen.currentResolution.height;

            for (int i = 0; i < allResolutions.Length; i++)
            {
                Resolution resolution = allResolutions[i];
                bool duplicate = false;
                for (int j = 0; j < uniqueResolutions.Count; j++)
                {
                    if (uniqueResolutions[j].width == resolution.width && uniqueResolutions[j].height == resolution.height)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (duplicate)
                {
                    continue;
                }

                if (resolution.width == currentWidth && resolution.height == currentHeight)
                {
                    currentIndex = uniqueResolutions.Count;
                }

                uniqueResolutions.Add(resolution);
                options.Add($"{resolution.width} x {resolution.height}");
            }

            TMP_Dropdown dropdown = CreateLabeledDropdown(parent, "Cozunurluk", options);
            dropdown.value = currentIndex;
            return new ResolutionBuildResult(dropdown, uniqueResolutions.ToArray());
        }

        private TMP_Dropdown CreateLabeledDropdown(Transform parent, string label, List<string> options)
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
            rowObject.GetComponent<LayoutElement>().minHeight = 46f;

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
            if (config.DropdownBackgroundSprite != null)
            {
                dropdownImage.sprite = config.DropdownBackgroundSprite;
                dropdownImage.type = Image.Type.Sliced;
            }
            else if (config.ButtonBackgroundSprite != null)
            {
                dropdownImage.sprite = config.ButtonBackgroundSprite;
                dropdownImage.type = Image.Type.Sliced;
            }

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

            return CreateDropdownTemplate(dropdownObject, captionText, options);
        }

        private TMP_Dropdown CreateDropdownTemplate(GameObject dropdownObject, TextMeshProUGUI captionText, List<string> options)
        {
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
            if (config.DropdownBackgroundSprite != null)
            {
                templateImage.sprite = config.DropdownBackgroundSprite;
                templateImage.type = Image.Type.Sliced;
            }
            else if (config.PanelBackgroundSprite != null)
            {
                templateImage.sprite = config.PanelBackgroundSprite;
                templateImage.type = Image.Type.Sliced;
            }

            GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
            viewportObject.transform.SetParent(templateObject.transform, false);
            RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportObject.GetComponent<Image>().color = Color.white;
            viewportObject.GetComponent<Mask>().showMaskGraphic = false;

            GameObject contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewportObject.transform, false);
            RectTransform contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 28f);

            GameObject itemObject = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            itemObject.transform.SetParent(contentObject.transform, false);
            RectTransform itemRect = itemObject.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 0.5f);
            itemRect.anchorMax = new Vector2(1f, 0.5f);
            itemRect.sizeDelta = new Vector2(0f, 28f);

            GameObject itemBgObject = new GameObject("Item Background", typeof(RectTransform), typeof(Image));
            itemBgObject.transform.SetParent(itemObject.transform, false);
            RectTransform itemBgRect = itemBgObject.GetComponent<RectTransform>();
            itemBgRect.anchorMin = Vector2.zero;
            itemBgRect.anchorMax = Vector2.one;
            Image itemBgImage = itemBgObject.GetComponent<Image>();
            itemBgImage.color = new Color(0.25f, 0.35f, 0.5f, 0.5f);

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

            Toggle itemToggle = itemObject.GetComponent<Toggle>();
            itemToggle.targetGraphic = itemBgImage;
            itemToggle.isOn = true;

            ScrollRect scrollRect = templateObject.GetComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            templateObject.SetActive(false);

            TMP_Dropdown dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
            dropdown.targetGraphic = dropdownObject.GetComponent<Image>();
            dropdown.template = templateRect;
            dropdown.captionText = captionText;
            dropdown.itemText = itemLabelText;
            dropdown.ClearOptions();
            dropdown.AddOptions(options);
            return dropdown;
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
            rowObject.GetComponent<LayoutElement>().minHeight = 46f;

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

            GameObject bgObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgObject.transform.SetParent(toggleObject.transform, false);
            RectTransform bgRect = bgObject.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.5f, 0.5f);
            bgRect.anchorMax = new Vector2(0.5f, 0.5f);
            bgRect.sizeDelta = new Vector2(36f, 36f);
            Image bgImage = bgObject.GetComponent<Image>();
            bgImage.color = new Color(0.22f, 0.26f, 0.32f, 1f);
            if (config.ToggleBackgroundSprite != null)
            {
                bgImage.sprite = config.ToggleBackgroundSprite;
                bgImage.type = Image.Type.Sliced;
            }
            else if (config.ButtonBackgroundSprite != null)
            {
                bgImage.sprite = config.ButtonBackgroundSprite;
                bgImage.type = Image.Type.Sliced;
            }

            GameObject checkObject = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkObject.transform.SetParent(bgObject.transform, false);
            RectTransform checkRect = checkObject.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkRect.pivot = new Vector2(0.5f, 0.5f);
            checkRect.sizeDelta = new Vector2(20f, 20f);

            Image checkImage = checkObject.GetComponent<Image>();
            checkImage.color = new Color(0.16f, 0.52f, 0.88f, 1f);
            if (config.ToggleCheckmarkSprite != null)
            {
                checkImage.sprite = config.ToggleCheckmarkSprite;
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

        private readonly struct ResolutionBuildResult
        {
            public ResolutionBuildResult(TMP_Dropdown dropdown, Resolution[] availableResolutions)
            {
                Dropdown = dropdown;
                AvailableResolutions = availableResolutions;
            }

            public TMP_Dropdown Dropdown { get; }
            public Resolution[] AvailableResolutions { get; }
        }
    }
}
