using UnityEngine;
using UnityEngine.UI;
using DeliveryDriver.UI;

namespace DeliveryDriver.Quest.UI
{
    internal sealed class PauseMenuRuntimeView
    {
        public GameObject Panel { get; set; }
        public CanvasGroup PanelCanvasGroup { get; set; }
        public Slider MasterVolumeSlider { get; set; }
        public Slider MusicVolumeSlider { get; set; }
        public Slider SfxVolumeSlider { get; set; }
        public RuntimeOptionSelector QualityDropdown { get; set; }
        public RuntimeOptionSelector ResolutionDropdown { get; set; }
        public Toggle FullScreenToggle { get; set; }
        public RuntimeOptionSelector FpsDropdown { get; set; }
        public RuntimeOptionSelector SpeedUnitDropdown { get; set; }
        public RuntimeOptionSelector LanguageDropdown { get; set; }
        public RuntimeOptionSelector ColorBlindDropdown { get; set; }
        public Slider TextScaleSlider { get; set; }
        public Toggle HighContrastToggle { get; set; }
        public Button ResumeButton { get; set; }
        public Button QuitButton { get; set; }
        public Resolution[] AvailableResolutions { get; set; }
    }
}
