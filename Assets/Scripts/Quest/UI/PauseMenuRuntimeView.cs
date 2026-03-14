using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryDriver.Quest.UI
{
    internal sealed class PauseMenuRuntimeView
    {
        public GameObject Panel { get; set; }
        public CanvasGroup PanelCanvasGroup { get; set; }
        public Slider MasterVolumeSlider { get; set; }
        public Slider MusicVolumeSlider { get; set; }
        public Slider SfxVolumeSlider { get; set; }
        public TMP_Dropdown QualityDropdown { get; set; }
        public TMP_Dropdown ResolutionDropdown { get; set; }
        public Toggle FullScreenToggle { get; set; }
        public TMP_Dropdown FpsDropdown { get; set; }
        public TMP_Dropdown SpeedUnitDropdown { get; set; }
        public TMP_Dropdown ColorBlindDropdown { get; set; }
        public Slider TextScaleSlider { get; set; }
        public Toggle HighContrastToggle { get; set; }
        public Button ResumeButton { get; set; }
        public Button QuitButton { get; set; }
        public Resolution[] AvailableResolutions { get; set; }
    }
}
