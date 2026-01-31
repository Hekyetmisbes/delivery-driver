using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DeliveryDriver.Quest
{
    /// <summary>
    /// Displays achievement unlock notifications
    /// </summary>
    public class AchievementNotificationUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject notificationPanel;
        [SerializeField] private TextMeshProUGUI achievementNameText;
        [SerializeField] private TextMeshProUGUI achievementDescriptionText;
        [SerializeField] private TextMeshProUGUI rewardText;
        [SerializeField] private Image achievementIcon;

        [Header("Animation")]
        [SerializeField] private Animator notificationAnimator;
        [SerializeField] private float displayDuration = 3f;

        [Header("Effects")]
        [SerializeField] private ParticleSystem unlockParticles;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip unlockSound;

        private void Start()
        {
            // Subscribe to achievement unlock event
            if (PlayerProgressionManager.Instance != null)
            {
                PlayerProgressionManager.Instance.OnAchievementUnlocked.AddListener(ShowAchievementNotification);
            }

            // Hide notification panel initially
            if (notificationPanel != null)
            {
                notificationPanel.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (PlayerProgressionManager.Instance != null)
            {
                PlayerProgressionManager.Instance.OnAchievementUnlocked.RemoveListener(ShowAchievementNotification);
            }
        }

        /// <summary>
        /// Shows achievement unlock notification
        /// </summary>
        /// <param name="achievement">The unlocked achievement</param>
        public void ShowAchievementNotification(Achievement achievement)
        {
            if (achievement == null || notificationPanel == null)
            {
                return;
            }

            // Update UI elements
            if (achievementNameText != null)
            {
                achievementNameText.text = achievement.Name;
            }

            if (achievementDescriptionText != null)
            {
                achievementDescriptionText.text = achievement.Description;
            }

            if (rewardText != null)
            {
                string rewardString = "";
                if (achievement.RewardMoney > 0)
                {
                    rewardString += $"+${achievement.RewardMoney}";
                }
                if (achievement.RewardXP > 0)
                {
                    if (rewardString.Length > 0)
                    {
                        rewardString += "  ";
                    }
                    rewardString += $"+{achievement.RewardXP} XP";
                }
                rewardText.text = rewardString;
            }

            if (achievementIcon != null && achievement.Icon != null)
            {
                achievementIcon.sprite = achievement.Icon;
                achievementIcon.enabled = true;
            }
            else if (achievementIcon != null)
            {
                achievementIcon.enabled = false;
            }

            // Show panel
            notificationPanel.SetActive(true);

            // Play animation
            if (notificationAnimator != null)
            {
                notificationAnimator.SetTrigger("Show");
            }

            // Play particle effect
            if (unlockParticles != null)
            {
                unlockParticles.Play();
            }

            // Play sound
            if (audioSource != null && unlockSound != null)
            {
                audioSource.PlayOneShot(unlockSound);
            }

            // Auto-hide after duration
            StartCoroutine(HideAfterDelay());
        }

        /// <summary>
        /// Hides the notification after a delay
        /// </summary>
        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(displayDuration);
            HideNotification();
        }

        /// <summary>
        /// Hides the achievement notification
        /// </summary>
        public void HideNotification()
        {
            if (notificationAnimator != null)
            {
                notificationAnimator.SetTrigger("Hide");
            }

            // Delay deactivation to allow animation to play
            StartCoroutine(DeactivateAfterAnimation());
        }

        /// <summary>
        /// Deactivates the panel after animation completes
        /// </summary>
        private IEnumerator DeactivateAfterAnimation()
        {
            yield return new WaitForSeconds(0.5f); // Wait for hide animation

            if (notificationPanel != null)
            {
                notificationPanel.SetActive(false);
            }
        }
    }
}
