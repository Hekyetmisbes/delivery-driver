using UnityEngine;

namespace DeliveryDriver.UI
{
    public class UIAudioFeedback : MonoBehaviour
    {
        private static UIAudioFeedback instance;
        private AudioSource audioSource;

        private AudioClip clickClip;
        private AudioClip hoverClip;
        private AudioClip switchClip;
        private AudioClip errorClip;

        public static UIAudioFeedback Instance
        {
            get
            {
                if (instance == null)
                {
                    EnsureInstance();
                }
                return instance;
            }
        }

        private static void EnsureInstance()
        {
            if (instance != null) return;

            instance = FindFirstObjectByType<UIAudioFeedback>();
            if (instance != null) return;

            GameObject go = new GameObject("UIAudioFeedback");
            instance = go.AddComponent<UIAudioFeedback>();
            DontDestroyOnLoad(go);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInit()
        {
            EnsureInstance();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSounds();
        }

        private AudioSource GetAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
            return audioSource;
        }

        private void LoadSounds()
        {
            clickClip = LoadSoundClip("click-a");
            hoverClip = LoadSoundClip("tap-a");
            switchClip = LoadSoundClip("switch-a");
            errorClip = LoadSoundClip("click-b");
        }

        private static AudioClip LoadSoundClip(string name)
        {
            AudioClip clip = Resources.Load<AudioClip>($"UI/Sounds/{name}");
            if (clip != null) return clip;

#if UNITY_EDITOR
            clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/kenney_ui-pack/Sounds/{name}.ogg");
#endif
            return clip;
        }

        public void PlayClick()
        {
            PlayClip(clickClip);
        }

        public void PlayHover()
        {
            PlayClip(hoverClip, 0.5f);
        }

        public void PlaySwitch()
        {
            PlayClip(switchClip);
        }

        public void PlayError()
        {
            PlayClip(errorClip);
        }

        private void PlayClip(AudioClip clip, float volumeMultiplier = 1f)
        {
            if (clip == null) return;

            float volume = volumeMultiplier;
            if (Quest.GameSettings.Instance != null)
            {
                volume *= Quest.GameSettings.Instance.SfxVolume * Quest.GameSettings.Instance.MasterVolume;
            }

            GetAudioSource().PlayOneShot(clip, volume);
        }
    }
}
