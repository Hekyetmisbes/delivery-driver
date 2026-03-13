using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DeliveryDriver.Quest.UI
{
    public class SettingsMenuUI : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject saveLoadPanel;

        [Header("Audio")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;

        [Header("Gameplay")]
        [SerializeField] private TMP_Dropdown questDifficultyDropdown;
        [SerializeField] private TMP_Dropdown speedUnitDropdown;

        [Header("UI")]
        [SerializeField] private Slider uiScaleSlider;

        [Header("Buttons")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button applyButton;
        [SerializeField] private Button resetButton;

#if ENABLE_INPUT_SYSTEM
        [Header("Input Rebinding")]
        [SerializeField] private InputActionReference rebindAction;
        [SerializeField] private TMP_Text rebindStatusText;
#endif

        [Header("Behavior")]
        [SerializeField] private bool applyOnChange = true;
        [SerializeField] private bool saveOnChange = true;

        private GameSettings settings;
        private bool suppressCallbacks;

        private void Awake()
        {
            settings = GameSettings.Instance;
        }

        private void Start()
        {
            BindControls();
            RefreshUI();
        }

        private void OnDestroy()
        {
            UnbindControls();
        }

        public void SetOpen(bool isOpen)
        {
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(isOpen);
            }

            if (saveLoadPanel != null)
            {
                saveLoadPanel.SetActive(isOpen);
            }

            if (isOpen)
            {
                RefreshUI();
            }
        }

        private void BindControls()
        {
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

            if (uiScaleSlider != null)
            {
                uiScaleSlider.onValueChanged.AddListener(OnUiScaleChanged);
            }

            if (questDifficultyDropdown != null)
            {
                questDifficultyDropdown.onValueChanged.AddListener(OnDifficultyChanged);
            }

            if (speedUnitDropdown != null)
            {
                speedUnitDropdown.onValueChanged.AddListener(OnSpeedUnitChanged);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(HandleClose);
            }

            if (applyButton != null)
            {
                applyButton.onClick.AddListener(ApplySettings);
            }

            if (resetButton != null)
            {
                resetButton.onClick.AddListener(ResetToDefaults);
            }
        }

        private void UnbindControls()
        {
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
            }

            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
            }

            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
            }

            if (uiScaleSlider != null)
            {
                uiScaleSlider.onValueChanged.RemoveListener(OnUiScaleChanged);
            }

            if (questDifficultyDropdown != null)
            {
                questDifficultyDropdown.onValueChanged.RemoveListener(OnDifficultyChanged);
            }

            if (speedUnitDropdown != null)
            {
                speedUnitDropdown.onValueChanged.RemoveListener(OnSpeedUnitChanged);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HandleClose);
            }

            if (applyButton != null)
            {
                applyButton.onClick.RemoveListener(ApplySettings);
            }

            if (resetButton != null)
            {
                resetButton.onClick.RemoveListener(ResetToDefaults);
            }
        }

        private void RefreshUI()
        {
            if (settings == null)
            {
                settings = GameSettings.Instance;
            }

            if (settings == null)
            {
                return;
            }

            suppressCallbacks = true;

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

            if (uiScaleSlider != null)
            {
                uiScaleSlider.value = settings.UiScale;
            }

            if (questDifficultyDropdown != null)
            {
                EnsureDifficultyOptions();
                questDifficultyDropdown.value = (int)settings.DifficultyPreference;
            }

            if (speedUnitDropdown != null)
            {
                EnsureSpeedUnitOptions();
                speedUnitDropdown.value = (int)settings.SpeedUnitPreference;
            }

            suppressCallbacks = false;
        }

        private void EnsureDifficultyOptions()
        {
            if (questDifficultyDropdown == null || questDifficultyDropdown.options.Count > 0)
            {
                return;
            }

            questDifficultyDropdown.options.Add(new TMP_Dropdown.OptionData("Match Player Level"));
            questDifficultyDropdown.options.Add(new TMP_Dropdown.OptionData("Easy"));
            questDifficultyDropdown.options.Add(new TMP_Dropdown.OptionData("Medium"));
            questDifficultyDropdown.options.Add(new TMP_Dropdown.OptionData("Hard"));
            questDifficultyDropdown.options.Add(new TMP_Dropdown.OptionData("Expert"));
        }

        private void EnsureSpeedUnitOptions()
        {
            if (speedUnitDropdown == null || speedUnitDropdown.options.Count > 0)
            {
                return;
            }

            speedUnitDropdown.options.Add(new TMP_Dropdown.OptionData("KMH"));
            speedUnitDropdown.options.Add(new TMP_Dropdown.OptionData("MPH"));
        }

        private void OnMasterVolumeChanged(float value)
        {
            if (suppressCallbacks || settings == null)
            {
                return;
            }

            settings.SetMasterVolume(value);
            ApplyIfNeeded();
        }

        private void OnMusicVolumeChanged(float value)
        {
            if (suppressCallbacks || settings == null)
            {
                return;
            }

            settings.SetMusicVolume(value);
            ApplyIfNeeded();
        }

        private void OnSfxVolumeChanged(float value)
        {
            if (suppressCallbacks || settings == null)
            {
                return;
            }

            settings.SetSfxVolume(value);
            ApplyIfNeeded();
        }

        private void OnUiScaleChanged(float value)
        {
            if (suppressCallbacks || settings == null)
            {
                return;
            }

            settings.SetUiScale(value);
            ApplyIfNeeded();
        }

        private void OnDifficultyChanged(int value)
        {
            if (suppressCallbacks || settings == null)
            {
                return;
            }

            settings.SetQuestDifficultyPreference((QuestDifficultyPreference)value);
            ApplyIfNeeded();
        }

        private void OnSpeedUnitChanged(int value)
        {
            if (suppressCallbacks || settings == null)
            {
                return;
            }

            settings.SetSpeedUnitPreference(value == (int)SpeedUnitPreference.Mph
                ? SpeedUnitPreference.Mph
                : SpeedUnitPreference.Kmh);
            ApplyIfNeeded();
        }

        private void ApplyIfNeeded()
        {
            if (applyOnChange)
            {
                settings.ApplySettings();
            }

            if (saveOnChange)
            {
                settings.SaveSettings();
            }
        }

        private void ApplySettings()
        {
            if (settings == null)
            {
                return;
            }

            settings.ApplySettings();
            settings.SaveSettings();
        }

        private void ResetToDefaults()
        {
            if (settings == null)
            {
                return;
            }

            settings.SetMasterVolume(1f);
            settings.SetMusicVolume(1f);
            settings.SetSfxVolume(1f);
            settings.SetUiScale(1f);
            settings.SetQuestDifficultyPreference(QuestDifficultyPreference.MatchPlayerLevel);
            settings.SetSpeedUnitPreference(SpeedUnitPreference.Kmh);
            ApplySettings();
            RefreshUI();
        }

        private void HandleClose()
        {
            SetOpen(false);
        }

#if ENABLE_INPUT_SYSTEM
        public void StartRebind()
        {
            if (rebindAction == null || rebindAction.action == null)
            {
                return;
            }

            rebindStatusText?.SetText("Press a key...");
            rebindAction.action.Disable();
            rebindAction.action.PerformInteractiveRebinding()
                .OnComplete(operation =>
                {
                    operation.Dispose();
                    rebindAction.action.Enable();
                    rebindStatusText?.SetText("Rebind complete");
                })
                .Start();
        }
#endif
    }
}
