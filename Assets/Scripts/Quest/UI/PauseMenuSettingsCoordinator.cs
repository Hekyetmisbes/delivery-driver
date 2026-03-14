using System.Collections.Generic;
using DeliveryDriver.UI;
using UnityEngine;

namespace DeliveryDriver.Quest.UI
{
    internal sealed class PauseMenuSettingsCoordinator
    {
        private readonly PauseMenuRuntimeView view;
        private readonly List<string> qualityTierOptions = new List<string> { "Dusuk", "Orta", "Yuksek" };
        private int[] qualityTierToProjectQuality = { 0, 1, 2 };
        private bool suppressSliderCallbacks;
        private bool suppressGraphicsCallbacks;

        public PauseMenuSettingsCoordinator(PauseMenuRuntimeView view)
        {
            this.view = view;
            ConfigureQualityTierMapping();
        }

        public void BindCallbacks()
        {
            view.MasterVolumeSlider?.onValueChanged.AddListener(OnMasterVolumeChanged);
            view.MusicVolumeSlider?.onValueChanged.AddListener(OnMusicVolumeChanged);
            view.SfxVolumeSlider?.onValueChanged.AddListener(OnSfxVolumeChanged);
            view.QualityDropdown?.onValueChanged.AddListener(OnQualityChanged);
            view.ResolutionDropdown?.onValueChanged.AddListener(OnResolutionChanged);
            view.FullScreenToggle?.onValueChanged.AddListener(OnFullScreenChanged);
            view.FpsDropdown?.onValueChanged.AddListener(OnFpsChanged);
            view.SpeedUnitDropdown?.onValueChanged.AddListener(OnSpeedUnitChanged);
            view.ColorBlindDropdown?.onValueChanged.AddListener(OnColorBlindModeChanged);
            view.TextScaleSlider?.onValueChanged.AddListener(OnTextScaleChanged);
            view.HighContrastToggle?.onValueChanged.AddListener(OnHighContrastChanged);
        }

        public void RefreshAll()
        {
            RefreshAudioControls();
            RefreshGraphicsControls();
            RefreshAccessibilityControls();
        }

        public void InitializeControlValues()
        {
            if (view.QualityDropdown != null)
            {
                qualityTierOptions[0] = LocalizationTable.Get("quality_low");
                qualityTierOptions[1] = LocalizationTable.Get("quality_medium");
                qualityTierOptions[2] = LocalizationTable.Get("quality_high");
                view.QualityDropdown.ClearOptions();
                view.QualityDropdown.AddOptions(qualityTierOptions);
            }

            if (view.ColorBlindDropdown != null)
            {
                view.ColorBlindDropdown.ClearOptions();
                view.ColorBlindDropdown.AddOptions(new List<string>
                {
                    LocalizationTable.Get("color_blind_none"),
                    LocalizationTable.Get("color_blind_protanopia"),
                    LocalizationTable.Get("color_blind_deuteranopia"),
                    LocalizationTable.Get("color_blind_tritanopia")
                });
            }

            if (view.FpsDropdown != null)
            {
                view.FpsDropdown.ClearOptions();
                view.FpsDropdown.AddOptions(new List<string> { "30", "60", "120", LocalizationTable.Get("fps_unlimited") });
            }

            RefreshAll();
        }

        private void RefreshAudioControls()
        {
            GameSettings settings = GameSettings.Instance;
            if (settings == null)
            {
                return;
            }

            suppressSliderCallbacks = true;
            if (view.MasterVolumeSlider != null)
            {
                view.MasterVolumeSlider.value = settings.MasterVolume;
            }

            if (view.MusicVolumeSlider != null)
            {
                view.MusicVolumeSlider.value = settings.MusicVolume;
            }

            if (view.SfxVolumeSlider != null)
            {
                view.SfxVolumeSlider.value = settings.SfxVolume;
            }
            suppressSliderCallbacks = false;
        }

        private void RefreshGraphicsControls()
        {
            GameSettings settings = GameSettings.Instance;
            if (settings == null)
            {
                return;
            }

            suppressGraphicsCallbacks = true;

            if (view.QualityDropdown != null)
            {
                int qualityLevel = settings.QualityLevel >= 0 ? settings.QualityLevel : QualitySettings.GetQualityLevel();
                view.QualityDropdown.value = ConvertProjectQualityToTierIndex(qualityLevel);
            }

            if (view.ResolutionDropdown != null && settings.ResolutionIndex >= 0 && settings.ResolutionIndex < view.AvailableResolutions.Length)
            {
                view.ResolutionDropdown.value = settings.ResolutionIndex;
            }

            if (view.FullScreenToggle != null)
            {
                view.FullScreenToggle.isOn = settings.FullScreen;
            }

            if (view.FpsDropdown != null)
            {
                SetFpsDropdownValue();
            }

            if (view.SpeedUnitDropdown != null)
            {
                view.SpeedUnitDropdown.value = settings.SpeedUnitPreference == SpeedUnitPreference.Mph ? 1 : 0;
            }

            suppressGraphicsCallbacks = false;
        }

        private void RefreshAccessibilityControls()
        {
            GameSettings settings = GameSettings.Instance;
            if (settings == null)
            {
                return;
            }

            if (view.ColorBlindDropdown != null)
            {
                view.ColorBlindDropdown.value = settings.ColorBlindMode;
            }

            if (view.TextScaleSlider != null)
            {
                view.TextScaleSlider.value = settings.TextScaleMultiplier;
            }

            if (view.HighContrastToggle != null)
            {
                view.HighContrastToggle.isOn = settings.HighContrastMode;
            }
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

        private void OnQualityChanged(int index)
        {
            if (suppressGraphicsCallbacks || GameSettings.Instance == null)
            {
                return;
            }

            GameSettings.Instance.SetQualityLevel(ConvertTierIndexToProjectQuality(index));
            GameSettings.Instance.ApplySettings();
            GameSettings.Instance.SaveSettings();
        }

        private void OnResolutionChanged(int index)
        {
            if (suppressGraphicsCallbacks || GameSettings.Instance == null)
            {
                return;
            }

            if (index < 0 || index >= view.AvailableResolutions.Length)
            {
                return;
            }

            Resolution resolution = view.AvailableResolutions[index];
            bool isFullScreen = view.FullScreenToggle != null && view.FullScreenToggle.isOn;
            Screen.SetResolution(resolution.width, resolution.height, isFullScreen);
            GameSettings.Instance.SetResolutionIndex(index);
            GameSettings.Instance.SetResolutionSize(resolution.width, resolution.height);
            GameSettings.Instance.SetFullScreen(isFullScreen);
            GameSettings.Instance.SaveSettings();
        }

        private void OnFullScreenChanged(bool value)
        {
            if (suppressGraphicsCallbacks || GameSettings.Instance == null)
            {
                return;
            }

            GameSettings.Instance.SetFullScreen(value);
            GameSettings.Instance.ApplySettings();
            GameSettings.Instance.SaveSettings();
        }

        private void OnFpsChanged(int index)
        {
            if (suppressGraphicsCallbacks || GameSettings.Instance == null)
            {
                return;
            }

            int[] fpsValues = { 30, 60, 120, -1 };
            GameSettings.Instance.SetTargetFps(fpsValues[Mathf.Clamp(index, 0, fpsValues.Length - 1)]);
            GameSettings.Instance.ApplySettings();
            GameSettings.Instance.SaveSettings();
        }

        private void OnSpeedUnitChanged(int index)
        {
            if (suppressGraphicsCallbacks || GameSettings.Instance == null)
            {
                return;
            }

            GameSettings.Instance.SetSpeedUnitPreference(index == 1 ? SpeedUnitPreference.Mph : SpeedUnitPreference.Kmh);
            GameSettings.Instance.ApplySettings();
            GameSettings.Instance.SaveSettings();
        }

        private static void OnColorBlindModeChanged(int mode)
        {
            if (GameSettings.Instance == null)
            {
                return;
            }

            GameSettings.Instance.SetColorBlindMode(mode);
            GameSettings.Instance.SaveSettings();
        }

        private static void OnTextScaleChanged(float value)
        {
            if (GameSettings.Instance == null)
            {
                return;
            }

            GameSettings.Instance.SetTextScaleMultiplier(value);
            GameSettings.Instance.SaveSettings();
        }

        private static void OnHighContrastChanged(bool value)
        {
            if (GameSettings.Instance == null)
            {
                return;
            }

            GameSettings.Instance.SetHighContrastMode(value);
            GameSettings.Instance.SaveSettings();
        }

        private void SetFpsDropdownValue()
        {
            if (view.FpsDropdown == null)
            {
                return;
            }

            int fps = GameSettings.Instance != null ? GameSettings.Instance.TargetFps : Application.targetFrameRate;
            switch (fps)
            {
                case 30:
                    view.FpsDropdown.value = 0;
                    break;
                case 60:
                    view.FpsDropdown.value = 1;
                    break;
                case 120:
                    view.FpsDropdown.value = 2;
                    break;
                default:
                    view.FpsDropdown.value = 3;
                    break;
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

        private int ConvertTierIndexToProjectQuality(int tierIndex)
        {
            int safeTier = Mathf.Clamp(tierIndex, 0, qualityTierToProjectQuality.Length - 1);
            return qualityTierToProjectQuality[safeTier];
        }
    }
}
