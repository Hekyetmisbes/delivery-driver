using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DeliveryDriver.Quest;
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
        [SerializeField] private Vector2 pauseMenuSize = new Vector2(560f, 440f);
        [SerializeField] private string resumeButtonLabel = "Devam Et";
        [SerializeField] private string quitButtonLabel = "Oyundan Cik";

        [Header("Kenney Skin")]
        [SerializeField] private Sprite panelBackgroundSprite;
        [SerializeField] private Sprite buttonBackgroundSprite;
        [SerializeField] private Sprite sliderBackgroundSprite;
        [SerializeField] private Sprite sliderFillSprite;
        [SerializeField] private Sprite sliderHandleSprite;

        private Slider masterVolumeSlider;
        private Slider musicVolumeSlider;
        private Slider sfxVolumeSlider;
        private bool suppressSliderCallbacks;
        private bool runtimePausePanelBuilt;

        private bool isPaused;

        private void Start()
        {
            EnsurePauseMenu();
            SetPaused(false);
        }

        private void Update()
        {
            HandlePauseInput();
        }

        private void HandlePauseInput()
        {
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

            if (pausePanel != null)
            {
                pausePanel.SetActive(paused);
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
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = pauseMenuSize;

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.05f, 0.08f, 0.12f, 0.98f);
            if (panelBackgroundSprite != null)
            {
                panelImage.sprite = panelBackgroundSprite;
                panelImage.type = Image.Type.Sliced;
            }

            VerticalLayoutGroup layout = panelObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.spacing = 12f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            CreateHeader(panelObject.transform, "OYUN DURAKLATILDI");
            masterVolumeSlider = CreateLabeledSlider(panelObject.transform, "Ana Ses");
            musicVolumeSlider = CreateLabeledSlider(panelObject.transform, "Muzik");
            sfxVolumeSlider = CreateLabeledSlider(panelObject.transform, "Efekt");

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

            Button resumeButton = CreateButton(buttonRow.transform, "ResumeButton", resumeButtonLabel, new Color(0.16f, 0.62f, 0.3f, 1f));
            Button quitButton = CreateButton(buttonRow.transform, "QuitButton", quitButtonLabel, new Color(0.67f, 0.22f, 0.2f, 1f));
            if (resumeButton != null)
            {
                resumeButton.onClick.AddListener(() => SetPaused(false));
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(QuitGame);
            }

            return panelObject;
        }

        private void CreateHeader(Transform parent, string label)
        {
            GameObject headerObject = new GameObject("Header", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            headerObject.transform.SetParent(parent, false);
            LayoutElement layout = headerObject.GetComponent<LayoutElement>();
            layout.minHeight = 48f;

            TextMeshProUGUI text = headerObject.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 32f;
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
            rowLayout.spacing = 4f;
            rowLayout.childControlHeight = true;
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childForceExpandWidth = true;

            LayoutElement rowElement = rowObject.GetComponent<LayoutElement>();
            rowElement.minHeight = 74f;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(rowObject.transform, false);
            TextMeshProUGUI labelText = labelObject.GetComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 22f;
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
            sliderRect.sizeDelta = new Vector2(0f, 30f);
            LayoutElement layout = sliderObject.GetComponent<LayoutElement>();
            layout.minHeight = 30f;

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
            fillAreaRect.offsetMin = new Vector2(16f, 8f);
            fillAreaRect.offsetMax = new Vector2(-16f, -8f);

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
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystemObject.hideFlags = HideFlags.DontSave;
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
        }
    }
}
