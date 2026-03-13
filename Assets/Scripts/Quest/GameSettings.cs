using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DeliveryDriver.Quest
{
    public enum SpeedUnitPreference
    {
        Kmh = 0,
        Mph = 1
    }

    public enum QuestDifficultyPreference
    {
        MatchPlayerLevel = 0,
        Easy = 1,
        Medium = 2,
        Hard = 3,
        Expert = 4
    }

    public class GameSettings : MonoBehaviour
    {
        public static GameSettings Instance { get; private set; }

        private const string MasterVolumeKey = "MasterVolume";
        private const string MusicVolumeKey = "MusicVolume";
        private const string SfxVolumeKey = "SfxVolume";
        private const string UiScaleKey = "UIScale";
        private const string QuestDifficultyKey = "QuestDifficultyPreference";
        private const string QualityLevelKey = "QualityLevel";
        private const string ResolutionIndexKey = "ResolutionIndex";
        private const string ResolutionWidthKey = "ResolutionWidth";
        private const string ResolutionHeightKey = "ResolutionHeight";
        private const string FullScreenKey = "FullScreen";
        private const string TargetFpsKey = "TargetFPS";
        private const string SpeedUnitKey = "SpeedUnitPreference";
        private const string LanguageKey = "Language";
        private const string ColorBlindModeKey = "ColorBlindMode";
        private const string TextScaleMultiplierKey = "TextScaleMultiplier";
        private const string HighContrastModeKey = "HighContrastMode";
        private const string MinimapZoomKey = "MinimapZoom";

        [Header("Audio")]
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float musicVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

        [Header("Gameplay")]
        [SerializeField] private QuestDifficultyPreference questDifficultyPreference = QuestDifficultyPreference.MatchPlayerLevel;
        [SerializeField] private SpeedUnitPreference speedUnitPreference = SpeedUnitPreference.Kmh;

        [Header("Graphics")]
        [SerializeField] private int qualityLevel = -1;
        [SerializeField] private int resolutionIndex = -1;
        [SerializeField] private int resolutionWidth;
        [SerializeField] private int resolutionHeight;
        [SerializeField] private bool fullScreen = true;
        [SerializeField] private int targetFps = -1;

        [Header("UI")]
        [Range(0.75f, 1.5f)] [SerializeField] private float uiScale = 1f;
        [SerializeField] private Transform[] uiScaleRoots;

        [Header("Localization")]
        [SerializeField] private string language = "tr";

        [Header("Accessibility")]
        [SerializeField] private int colorBlindMode = 0;
        [Range(0.8f, 1.5f)] [SerializeField] private float textScaleMultiplier = 1f;
        [SerializeField] private bool highContrastMode = false;

        [Header("Minimap")]
        [SerializeField] private float minimapZoom = 250f;

#if ENABLE_INPUT_SYSTEM
        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
#endif

        public float MasterVolume => masterVolume;
        public float MusicVolume => musicVolume;
        public float SfxVolume => sfxVolume;
        public float UiScale => uiScale;
        public QuestDifficultyPreference DifficultyPreference => questDifficultyPreference;
        public int QualityLevel => qualityLevel;
        public int ResolutionIndex => resolutionIndex;
        public bool FullScreen => fullScreen;
        public int TargetFps => targetFps;
        public SpeedUnitPreference SpeedUnitPreference => speedUnitPreference;
        public string Language => language;
        public int ColorBlindMode => colorBlindMode;
        public float TextScaleMultiplier => textScaleMultiplier;
        public bool HighContrastMode => highContrastMode;
        public float MinimapZoom => minimapZoom;

        public static event System.Action<SpeedUnitPreference> OnSpeedUnitChanged;
        public static event System.Action OnLanguageChanged;
        public static event System.Action OnAccessibilityChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureInstance()
        {
            if (Instance != null)
            {
                return;
            }

            GameObject settingsObject = new GameObject("GameSettings");
            settingsObject.AddComponent<GameSettings>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings();
            ApplySettings();
        }

        public void SetMasterVolume(float value)
        {
            masterVolume = Mathf.Clamp01(value);
        }

        public void SetMusicVolume(float value)
        {
            musicVolume = Mathf.Clamp01(value);
        }

        public void SetSfxVolume(float value)
        {
            sfxVolume = Mathf.Clamp01(value);
        }

        public void SetUiScale(float value)
        {
            uiScale = Mathf.Clamp(value, 0.75f, 1.5f);
        }

        public void SetQuestDifficultyPreference(QuestDifficultyPreference preference)
        {
            questDifficultyPreference = preference;
        }

        public void SetSpeedUnitPreference(SpeedUnitPreference preference)
        {
            SpeedUnitPreference clamped = preference == SpeedUnitPreference.Mph
                ? SpeedUnitPreference.Mph
                : SpeedUnitPreference.Kmh;
            if (speedUnitPreference == clamped)
            {
                return;
            }

            speedUnitPreference = clamped;
            OnSpeedUnitChanged?.Invoke(speedUnitPreference);
        }

        public void SetQualityLevel(int level)
        {
            qualityLevel = level;
        }

        public void SetResolutionIndex(int index)
        {
            resolutionIndex = index;
        }

        public void SetResolutionSize(int width, int height)
        {
            resolutionWidth = width;
            resolutionHeight = height;
        }

        public void SetFullScreen(bool value)
        {
            fullScreen = value;
        }

        public void SetTargetFps(int fps)
        {
            targetFps = fps;
        }

        public void SetLanguage(string lang)
        {
            if (string.Equals(language, lang, System.StringComparison.OrdinalIgnoreCase)) return;
            language = lang;
            OnLanguageChanged?.Invoke();
        }

        public void SetColorBlindMode(int mode)
        {
            colorBlindMode = Mathf.Clamp(mode, 0, 3);
            OnAccessibilityChanged?.Invoke();
        }

        public void SetTextScaleMultiplier(float scale)
        {
            textScaleMultiplier = Mathf.Clamp(scale, 0.8f, 1.5f);
            OnAccessibilityChanged?.Invoke();
        }

        public void SetHighContrastMode(bool value)
        {
            highContrastMode = value;
            OnAccessibilityChanged?.Invoke();
        }

        public void SetMinimapZoom(float zoom)
        {
            minimapZoom = Mathf.Clamp(zoom, 100f, 500f);
        }

        public QuestDifficulty ResolveQuestDifficulty(int playerLevel)
        {
            if (questDifficultyPreference != QuestDifficultyPreference.MatchPlayerLevel)
            {
                int offset = Mathf.Clamp((int)questDifficultyPreference - 1, 0, (int)QuestDifficulty.Expert);
                return (QuestDifficulty)offset;
            }

            QuestDifficulty difficulty = QuestDifficulty.Easy;
            if (playerLevel >= 5) difficulty = QuestDifficulty.Medium;
            if (playerLevel >= 15) difficulty = QuestDifficulty.Hard;
            if (playerLevel >= 30) difficulty = QuestDifficulty.Expert;
            return difficulty;
        }

        public void ApplySettings()
        {
            AudioListener.volume = masterVolume;

            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.SetMusicVolume(musicVolume);
                QuestManager.Instance.SetSFXVolume(sfxVolume);
            }

            // Graphics settings
            if (qualityLevel >= 0)
            {
                if (QualityLevelManager.Instance != null)
                {
                    QualityLevelManager.Instance.SetQualityLevel(qualityLevel);
                }
                else
                {
                    QualitySettings.SetQualityLevel(qualityLevel, true);
                }
            }

            if (resolutionWidth > 0 && resolutionHeight > 0)
            {
                Screen.SetResolution(resolutionWidth, resolutionHeight, fullScreen);
            }
            else
            {
                Screen.fullScreen = fullScreen;
            }

            Application.targetFrameRate = targetFps;

            if (uiScaleRoots != null)
            {
                Vector3 scale = new Vector3(uiScale, uiScale, uiScale);
                foreach (Transform root in uiScaleRoots)
                {
                    if (root != null)
                    {
                        root.localScale = scale;
                    }
                }
            }

            OnSpeedUnitChanged?.Invoke(speedUnitPreference);
        }

        public void SaveSettings()
        {
            PlayerPrefs.SetFloat(MasterVolumeKey, masterVolume);
            PlayerPrefs.SetFloat(MusicVolumeKey, musicVolume);
            PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume);
            PlayerPrefs.SetFloat(UiScaleKey, uiScale);
            PlayerPrefs.SetInt(QuestDifficultyKey, (int)questDifficultyPreference);
            PlayerPrefs.SetInt(QualityLevelKey, qualityLevel);
            PlayerPrefs.SetInt(ResolutionIndexKey, resolutionIndex);
            PlayerPrefs.SetInt(ResolutionWidthKey, resolutionWidth);
            PlayerPrefs.SetInt(ResolutionHeightKey, resolutionHeight);
            PlayerPrefs.SetInt(FullScreenKey, fullScreen ? 1 : 0);
            PlayerPrefs.SetInt(TargetFpsKey, targetFps);
            PlayerPrefs.SetInt(SpeedUnitKey, (int)speedUnitPreference);
            PlayerPrefs.SetString(LanguageKey, language);
            PlayerPrefs.SetInt(ColorBlindModeKey, colorBlindMode);
            PlayerPrefs.SetFloat(TextScaleMultiplierKey, textScaleMultiplier);
            PlayerPrefs.SetInt(HighContrastModeKey, highContrastMode ? 1 : 0);
            PlayerPrefs.SetFloat(MinimapZoomKey, minimapZoom);
            PlayerPrefs.Save();
        }

        public void LoadSettings()
        {
            if (PlayerPrefs.HasKey(MasterVolumeKey))
            {
                masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, masterVolume);
            }

            if (PlayerPrefs.HasKey(MusicVolumeKey))
            {
                musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, musicVolume);
            }

            if (PlayerPrefs.HasKey(SfxVolumeKey))
            {
                sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, sfxVolume);
            }

            if (PlayerPrefs.HasKey(UiScaleKey))
            {
                uiScale = PlayerPrefs.GetFloat(UiScaleKey, uiScale);
            }

            if (PlayerPrefs.HasKey(QuestDifficultyKey))
            {
                questDifficultyPreference = (QuestDifficultyPreference)PlayerPrefs.GetInt(QuestDifficultyKey);
            }

            if (PlayerPrefs.HasKey(QualityLevelKey))
            {
                qualityLevel = PlayerPrefs.GetInt(QualityLevelKey, qualityLevel);
            }

            if (PlayerPrefs.HasKey(ResolutionIndexKey))
            {
                resolutionIndex = PlayerPrefs.GetInt(ResolutionIndexKey, resolutionIndex);
            }

            if (PlayerPrefs.HasKey(ResolutionWidthKey))
            {
                resolutionWidth = PlayerPrefs.GetInt(ResolutionWidthKey, 0);
            }

            if (PlayerPrefs.HasKey(ResolutionHeightKey))
            {
                resolutionHeight = PlayerPrefs.GetInt(ResolutionHeightKey, 0);
            }

            if (PlayerPrefs.HasKey(FullScreenKey))
            {
                fullScreen = PlayerPrefs.GetInt(FullScreenKey, 1) == 1;
            }

            if (PlayerPrefs.HasKey(TargetFpsKey))
            {
                targetFps = PlayerPrefs.GetInt(TargetFpsKey, targetFps);
            }

            if (PlayerPrefs.HasKey(SpeedUnitKey))
            {
                int savedUnit = PlayerPrefs.GetInt(SpeedUnitKey, (int)SpeedUnitPreference.Kmh);
                speedUnitPreference = savedUnit == (int)SpeedUnitPreference.Mph
                    ? SpeedUnitPreference.Mph
                    : SpeedUnitPreference.Kmh;
            }

            if (PlayerPrefs.HasKey(LanguageKey))
            {
                language = PlayerPrefs.GetString(LanguageKey, language);
            }

            if (PlayerPrefs.HasKey(ColorBlindModeKey))
            {
                colorBlindMode = PlayerPrefs.GetInt(ColorBlindModeKey, colorBlindMode);
            }

            if (PlayerPrefs.HasKey(TextScaleMultiplierKey))
            {
                textScaleMultiplier = PlayerPrefs.GetFloat(TextScaleMultiplierKey, textScaleMultiplier);
            }

            if (PlayerPrefs.HasKey(HighContrastModeKey))
            {
                highContrastMode = PlayerPrefs.GetInt(HighContrastModeKey, 0) == 1;
            }

            if (PlayerPrefs.HasKey(MinimapZoomKey))
            {
                minimapZoom = PlayerPrefs.GetFloat(MinimapZoomKey, minimapZoom);
            }
        }
    }
}
