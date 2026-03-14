using System;
using System.Collections;
using UnityEngine;

namespace DeliveryDriver.Quest
{
    internal sealed class QuestAudioPresentationService
    {
        private readonly AudioSource questSfxSource;
        private readonly AudioSource musicSource;
        private readonly AudioClip timeWarningClip;
        private readonly AudioClip levelUpClip;
        private readonly AudioClip explorationMusicClip;
        private readonly AudioClip deliveryMusicClip;
        private readonly float timeWarningThreshold;
        private readonly float musicCrossfadeDuration;

        private bool timeWarningPlayed;
        private bool isCrossfading;

        public QuestAudioPresentationService(
            AudioSource questSfxSource,
            AudioSource musicSource,
            AudioClip timeWarningClip,
            AudioClip levelUpClip,
            AudioClip explorationMusicClip,
            AudioClip deliveryMusicClip,
            float timeWarningThreshold,
            float musicCrossfadeDuration)
        {
            this.questSfxSource = questSfxSource;
            this.musicSource = musicSource;
            this.timeWarningClip = timeWarningClip;
            this.levelUpClip = levelUpClip;
            this.explorationMusicClip = explorationMusicClip;
            this.deliveryMusicClip = deliveryMusicClip;
            this.timeWarningThreshold = timeWarningThreshold;
            this.musicCrossfadeDuration = musicCrossfadeDuration;
        }

        public void InitializeExplorationMusic()
        {
            if (musicSource == null || explorationMusicClip == null)
            {
                return;
            }

            musicSource.clip = explorationMusicClip;
            musicSource.loop = true;
            musicSource.Play();
        }

        public void PlayQuestClip(AudioClip clip)
        {
            if (questSfxSource == null || clip == null)
            {
                return;
            }

            questSfxSource.PlayOneShot(clip);
        }

        public void PlayTimeWarning()
        {
            if (timeWarningClip == null)
            {
                return;
            }

            PlayQuestClip(timeWarningClip);
            Debug.Log("[QuestManager] Time warning! Less than 30 seconds remaining!");
        }

        public void ResetTimeWarning()
        {
            timeWarningPlayed = false;
        }

        public void TryPlayTimeWarning(float timeRemaining)
        {
            if (timeWarningPlayed || timeRemaining >= timeWarningThreshold)
            {
                return;
            }

            PlayTimeWarning();
            timeWarningPlayed = true;
        }

        public void SwitchToDeliveryMusic(Func<IEnumerator, Coroutine> startCoroutine)
        {
            TryStartCrossfade(deliveryMusicClip, startCoroutine);
        }

        public void SwitchToExplorationMusic(Func<IEnumerator, Coroutine> startCoroutine)
        {
            TryStartCrossfade(explorationMusicClip, startCoroutine);
        }

        public void PlayLevelUpSound()
        {
            PlayQuestClip(levelUpClip);
        }

        public void SetMusicVolume(float volume)
        {
            if (musicSource != null)
            {
                musicSource.volume = Mathf.Clamp01(volume);
            }
        }

        public void SetSfxVolume(float volume)
        {
            if (questSfxSource != null)
            {
                questSfxSource.volume = Mathf.Clamp01(volume);
            }
        }

        private void TryStartCrossfade(AudioClip newClip, Func<IEnumerator, Coroutine> startCoroutine)
        {
            if (musicSource == null || newClip == null || startCoroutine == null)
            {
                return;
            }

            if (musicSource.clip == newClip)
            {
                return;
            }

            startCoroutine(PerformCrossfade(newClip));
        }

        private IEnumerator PerformCrossfade(AudioClip newClip)
        {
            if (isCrossfading || musicSource == null || newClip == null)
            {
                yield break;
            }

            if (musicCrossfadeDuration <= 0f)
            {
                musicSource.clip = newClip;
                musicSource.loop = true;
                musicSource.Play();
                yield break;
            }

            isCrossfading = true;
            float startVolume = musicSource.volume;

            float elapsed = 0f;
            float halfDuration = musicCrossfadeDuration / 2f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / halfDuration);
                yield return null;
            }

            musicSource.clip = newClip;
            musicSource.loop = true;
            musicSource.Play();

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(0f, startVolume, elapsed / halfDuration);
                yield return null;
            }

            musicSource.volume = startVolume;
            isCrossfading = false;
        }
    }
}
