using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DeliveryDriver.Quest
{
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

        [Header("Audio")]
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float musicVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

        [Header("Gameplay")]
        [SerializeField] private QuestDifficultyPreference questDifficultyPreference = QuestDifficultyPreference.MatchPlayerLevel;

        [Header("UI")]
        [Range(0.75f, 1.5f)] [SerializeField] private float uiScale = 1f;
        [SerializeField] private Transform[] uiScaleRoots;

#if ENABLE_INPUT_SYSTEM
        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
#endif

        public float MasterVolume => masterVolume;
        public float MusicVolume => musicVolume;
        public float SfxVolume => sfxVolume;
        public float UiScale => uiScale;
        public QuestDifficultyPreference DifficultyPreference => questDifficultyPreference;

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
        }

        public void SaveSettings()
        {
            PlayerPrefs.SetFloat(MasterVolumeKey, masterVolume);
            PlayerPrefs.SetFloat(MusicVolumeKey, musicVolume);
            PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume);
            PlayerPrefs.SetFloat(UiScaleKey, uiScale);
            PlayerPrefs.SetInt(QuestDifficultyKey, (int)questDifficultyPreference);
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
        }
    }
}
