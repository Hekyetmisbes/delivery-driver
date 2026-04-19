using UnityEngine;
using UnityEngine.UI;
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
        private const string MainMenuSceneName = "MainMenu";

        [SerializeField] private GameObject pausePanel;
        [SerializeField] private SettingsMenuUI settingsMenu;
        [SerializeField] private QuestStatisticsUI statisticsMenu;
        [SerializeField] private bool pauseTimeScale = true;
        [SerializeField] private bool showStatsOnPause = true;
        [SerializeField] private bool buildKenneyPauseMenuAtRuntime = true;
        [SerializeField] private bool useKenneySkin = true;
        [SerializeField] private Vector2 pauseMenuSize = new Vector2(760f, 840f);
        [SerializeField] private string resumeButtonLabel = "";
        [SerializeField] private string quitButtonLabel = "";
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

        private bool runtimePausePanelBuilt;
        private CanvasGroup pausePanelCanvasGroup;
        private PauseMenuRuntimeView runtimeView;
        private PauseMenuSettingsCoordinator settingsCoordinator;

        private bool isPaused;

        private void Awake()
        {
            LocalizationTable.OnLocaleChanged += HandleLocaleChanged;
        }

        private void Start()
        {
            EnsurePauseMenu();
            SetPaused(startPaused);
        }

        private void OnDestroy()
        {
            LocalizationTable.OnLocaleChanged -= HandleLocaleChanged;
        }

        private void Update()
        {
            // Rebuild pause panel if it was destroyed (e.g. scene change destroyed its parent canvas)
            if (runtimePausePanelBuilt && pausePanel == null)
            {
                runtimePausePanelBuilt = false;
                EnsurePauseMenu();
            }

            HandlePauseInput();
        }

        private void HandlePauseInput()
        {
            if (!enablePauseToggleInput || IsMainMenuSceneActive())
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

        private static bool IsMainMenuSceneActive()
        {
            return SceneManager.GetActiveScene().name.Equals(MainMenuSceneName, System.StringComparison.OrdinalIgnoreCase);
        }

        public void TogglePause()
        {
            SetPaused(!isPaused);
        }

        public void SetPaused(bool paused)
        {
            isPaused = paused;
            settingsCoordinator?.RefreshAll();

            if (pausePanel != null)
            {
                if (paused)
                {
                    pausePanel.SetActive(true);
                    if (pausePanelCanvasGroup != null)
                    {
                        pausePanelCanvasGroup.alpha = 0f;
                        pausePanelCanvasGroup.interactable = true;
                        pausePanelCanvasGroup.blocksRaycasts = true;
                        UIAnimationHelper.FadeIn(this, pausePanelCanvasGroup, UIThemeConstants.PanelFadeDuration);
                        UIAnimationHelper.ScaleIn(this, pausePanel.GetComponent<RectTransform>(), UIThemeConstants.PanelScaleDuration);
                    }
                }
                else
                {
                    if (pausePanelCanvasGroup != null)
                    {
                        pausePanelCanvasGroup.interactable = false;
                        pausePanelCanvasGroup.blocksRaycasts = false;
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

            if (paused)
            {
                EnsureRuntimeControlsInteractable();
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

            if (pausePanel == null)
            {
                BuildRuntimePausePanel();
            }

            runtimePausePanelBuilt = pausePanel != null;
            settingsCoordinator?.InitializeControlValues();
        }

        private void BuildRuntimePausePanel()
        {
            PauseMenuRuntimeViewBuilder builder = new PauseMenuRuntimeViewBuilder(new PauseMenuRuntimeViewConfig
            {
                PauseMenuSize = pauseMenuSize,
                PanelBackgroundSprite = panelBackgroundSprite,
                ButtonBackgroundSprite = buttonBackgroundSprite,
                SliderBackgroundSprite = sliderBackgroundSprite,
                SliderFillSprite = sliderFillSprite,
                SliderHandleSprite = sliderHandleSprite,
                DropdownBackgroundSprite = dropdownBackgroundSprite,
                ToggleBackgroundSprite = toggleBackgroundSprite,
                ToggleCheckmarkSprite = toggleCheckmarkSprite,
                ResumeButtonLabel = GetResumeButtonLabel(),
                QuitButtonLabel = GetQuitButtonLabel()
            });

            runtimeView = builder.Build();
            pausePanel = runtimeView.Panel;
            pausePanelCanvasGroup = runtimeView.PanelCanvasGroup;
            settingsCoordinator = new PauseMenuSettingsCoordinator(runtimeView);
            settingsCoordinator.BindCallbacks();
            EnsureRuntimeControlsInteractable();

            if (runtimeView.ResumeButton != null)
            {
                runtimeView.ResumeButton.onClick.AddListener(OnResumeButtonClicked);
            }

            if (runtimeView.QuitButton != null)
            {
                runtimeView.QuitButton.onClick.AddListener(() =>
                {
                    if (ConfirmationDialog.IsShowing)
                    {
                        return;
                    }

                    ConfirmationDialog.Show(
                        LocalizationTable.Get("confirm_quit_title"),
                        LocalizationTable.Get("confirm_quit"),
                        OnQuitButtonClicked);
                });
            }
        }



        public void ConfigureForSettingsScene(string mainMenuSceneName = "MainMenu")
        {
            pauseTimeScale = false;
            showStatsOnPause = false;
            enablePauseToggleInput = false;
            startPaused = true;
            resumeButtonLabel = string.Empty;
            resumeSceneName = mainMenuSceneName;
            quitButtonLabel = string.Empty;
            quitSceneName = string.Empty;
        }

        private string GetResumeButtonLabel()
        {
            string localizationKey = string.IsNullOrWhiteSpace(resumeSceneName) ? "resume" : "quit_to_menu";
            return ResolveButtonLabel(resumeButtonLabel, localizationKey);
        }

        private string GetQuitButtonLabel()
        {
            return ResolveButtonLabel(quitButtonLabel, "quit_game");
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

        private void HandleLocaleChanged()
        {
            if (!buildKenneyPauseMenuAtRuntime || !runtimePausePanelBuilt)
            {
                return;
            }

            bool wasPaused = isPaused;
            DestroyRuntimePausePanel();
            EnsurePauseMenu();
            SetPaused(wasPaused);
        }

        private void DestroyRuntimePausePanel()
        {
            if (pausePanel != null)
            {
                Destroy(pausePanel);
            }

            pausePanel = null;
            pausePanelCanvasGroup = null;
            runtimeView = null;
            settingsCoordinator = null;
            runtimePausePanelBuilt = false;
        }

        private static string ResolveButtonLabel(string configuredLabel, string localizationKey)
        {
            if (ShouldUseLocalizedLabel(configuredLabel, localizationKey))
            {
                return LocalizationTable.Get(localizationKey);
            }

            return configuredLabel;
        }

        private static bool ShouldUseLocalizedLabel(string configuredLabel, string localizationKey)
        {
            if (string.IsNullOrWhiteSpace(configuredLabel))
            {
                return true;
            }

            switch (localizationKey)
            {
                case "resume":
                    return configuredLabel == "Devam Et" || configuredLabel == "Resume";
                case "quit_to_menu":
                    return configuredLabel == "Ana Menüye Dön" || configuredLabel == "Ana Menuye Don" || configuredLabel == "Return to Menu";
                case "quit_game":
                    return configuredLabel == "Oyundan Çık" || configuredLabel == "Oyundan Cik" || configuredLabel == "Quit Game";
                default:
                    return false;
            }
        }

        private void EnsureRuntimeControlsInteractable()
        {
            if (runtimeView == null)
            {
                return;
            }

            if (runtimeView.MasterVolumeSlider != null) runtimeView.MasterVolumeSlider.interactable = true;
            if (runtimeView.MusicVolumeSlider != null) runtimeView.MusicVolumeSlider.interactable = true;
            if (runtimeView.SfxVolumeSlider != null) runtimeView.SfxVolumeSlider.interactable = true;
            if (runtimeView.QualityDropdown != null) runtimeView.QualityDropdown.interactable = true;
            if (runtimeView.ResolutionDropdown != null) runtimeView.ResolutionDropdown.interactable = true;
            if (runtimeView.FullScreenToggle != null) runtimeView.FullScreenToggle.interactable = true;
            if (runtimeView.FpsDropdown != null) runtimeView.FpsDropdown.interactable = true;
            if (runtimeView.SpeedUnitDropdown != null) runtimeView.SpeedUnitDropdown.interactable = true;
            if (runtimeView.LanguageDropdown != null) runtimeView.LanguageDropdown.interactable = true;
            if (runtimeView.ColorBlindDropdown != null) runtimeView.ColorBlindDropdown.interactable = true;
            if (runtimeView.TextScaleSlider != null) runtimeView.TextScaleSlider.interactable = true;
            if (runtimeView.HighContrastToggle != null) runtimeView.HighContrastToggle.interactable = true;
            if (runtimeView.ResumeButton != null) runtimeView.ResumeButton.interactable = true;
            if (runtimeView.QuitButton != null) runtimeView.QuitButton.interactable = true;
        }
    }
}
