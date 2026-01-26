using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DeliveryDriver.Quest
{
    /// <summary>
    /// Manages the UI display for player progression (money, level, XP bar)
    /// </summary>
    public class ProgressionUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI moneyText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private Image xpFillBar;

        [Header("Animations")]
        [SerializeField] private Animator moneyAnimator;
        [SerializeField] private float xpBarAnimationSpeed = 1f;

        [Header("Level Up Popup")]
        [SerializeField] private GameObject levelUpPopup;
        [SerializeField] private TextMeshProUGUI levelUpText;
        [SerializeField] private ParticleSystem levelUpParticles;
        [SerializeField] private AudioSource levelUpAudioSource;
        [SerializeField] private AudioClip levelUpSound;
        [SerializeField] private float levelUpPopupDuration = 2f;

        [Header("Money Animation")]
        [SerializeField] private Color moneyGainColor = Color.green;
        [SerializeField] private float moneyFlashDuration = 0.3f;

        private Color originalMoneyColor;
        private Coroutine xpBarAnimationCoroutine;

        private void Start()
        {
            // Subscribe to PlayerProgressionManager events
            if (PlayerProgressionManager.Instance != null)
            {
                PlayerProgressionManager.Instance.OnMoneyChanged.AddListener(OnMoneyChanged);
                PlayerProgressionManager.Instance.OnLevelUp.AddListener(OnLevelUp);
                PlayerProgressionManager.Instance.OnXPGained.AddListener(OnXPGained);

                // Initialize display with current values
                UpdateMoneyDisplay(PlayerProgressionManager.Instance.CurrentMoney);
                UpdateLevelDisplay(PlayerProgressionManager.Instance.CurrentLevel);
                UpdateXPBar(PlayerProgressionManager.Instance.GetLevelProgressPercentage(), false);
            }
            else
            {
                Debug.LogWarning("[ProgressionUI] PlayerProgressionManager instance not found!");
            }

            // Store original money text color
            if (moneyText != null)
            {
                originalMoneyColor = moneyText.color;
            }

            // Hide level up popup initially
            if (levelUpPopup != null)
            {
                levelUpPopup.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from events to prevent memory leaks
            if (PlayerProgressionManager.Instance != null)
            {
                PlayerProgressionManager.Instance.OnMoneyChanged.RemoveListener(OnMoneyChanged);
                PlayerProgressionManager.Instance.OnLevelUp.RemoveListener(OnLevelUp);
                PlayerProgressionManager.Instance.OnXPGained.RemoveListener(OnXPGained);
            }
        }

        /// <summary>
        /// Called when player's money changes
        /// </summary>
        /// <param name="newAmount">New money amount</param>
        private void OnMoneyChanged(int newAmount)
        {
            UpdateMoneyDisplay(newAmount);
            PlayMoneyAnimation();
        }

        /// <summary>
        /// Called when player levels up
        /// </summary>
        /// <param name="newLevel">New level</param>
        private void OnLevelUp(int newLevel)
        {
            UpdateLevelDisplay(newLevel);
            ShowLevelUpPopup(newLevel);
        }

        /// <summary>
        /// Called when player gains XP
        /// </summary>
        /// <param name="xpGained">Amount of XP gained</param>
        private void OnXPGained(int xpGained)
        {
            if (PlayerProgressionManager.Instance != null)
            {
                float targetProgress = PlayerProgressionManager.Instance.GetLevelProgressPercentage();
                UpdateXPBar(targetProgress, true);
            }
        }

        /// <summary>
        /// Updates the money display text
        /// </summary>
        /// <param name="amount">Money amount to display</param>
        private void UpdateMoneyDisplay(int amount)
        {
            if (moneyText != null)
            {
                moneyText.text = $"${amount:N0}";
            }
        }

        /// <summary>
        /// Updates the level display text
        /// </summary>
        /// <param name="level">Level to display</param>
        private void UpdateLevelDisplay(int level)
        {
            if (levelText != null)
            {
                levelText.text = $"Level {level}";
            }
        }

        /// <summary>
        /// Updates the XP bar fill amount
        /// </summary>
        /// <param name="progress">Progress percentage (0-1)</param>
        /// <param name="animate">Whether to animate the transition</param>
        private void UpdateXPBar(float progress, bool animate)
        {
            if (xpFillBar == null)
            {
                return;
            }

            progress = Mathf.Clamp01(progress);

            if (animate)
            {
                // Stop any existing animation
                if (xpBarAnimationCoroutine != null)
                {
                    StopCoroutine(xpBarAnimationCoroutine);
                }

                xpBarAnimationCoroutine = StartCoroutine(AnimateXPBar(progress));
            }
            else
            {
                xpFillBar.fillAmount = progress;
            }
        }

        /// <summary>
        /// Animates the XP bar to the target fill amount
        /// </summary>
        /// <param name="targetFillAmount">Target fill amount (0-1)</param>
        private IEnumerator AnimateXPBar(float targetFillAmount)
        {
            float currentFillAmount = xpFillBar.fillAmount;
            float elapsedTime = 0f;
            float duration = Mathf.Abs(targetFillAmount - currentFillAmount) / xpBarAnimationSpeed;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;
                xpFillBar.fillAmount = Mathf.Lerp(currentFillAmount, targetFillAmount, t);
                yield return null;
            }

            xpFillBar.fillAmount = targetFillAmount;
        }

        /// <summary>
        /// Plays the money gain animation using Animator if available, or color flash fallback
        /// </summary>
        private void PlayMoneyAnimation()
        {
            if (moneyAnimator != null)
            {
                moneyAnimator.SetTrigger("MoneyGained");
            }
            else if (moneyText != null)
            {
                // Fallback: Flash the text color
                StartCoroutine(FlashMoneyColor());
            }
        }

        /// <summary>
        /// Flashes the money text color to indicate gain
        /// </summary>
        private IEnumerator FlashMoneyColor()
        {
            if (moneyText == null)
            {
                yield break;
            }

            moneyText.color = moneyGainColor;
            yield return new WaitForSeconds(moneyFlashDuration);
            moneyText.color = originalMoneyColor;
        }

        /// <summary>
        /// Shows the level up popup with animations and effects
        /// </summary>
        /// <param name="newLevel">The new level reached</param>
        private void ShowLevelUpPopup(int newLevel)
        {
            if (levelUpPopup != null)
            {
                levelUpPopup.SetActive(true);

                if (levelUpText != null)
                {
                    levelUpText.text = $"LEVEL UP!\nLevel {newLevel}";
                }

                // Play particle effect
                if (levelUpParticles != null)
                {
                    levelUpParticles.Play();
                }

                // Play sound effect
                if (levelUpAudioSource != null && levelUpSound != null)
                {
                    levelUpAudioSource.PlayOneShot(levelUpSound);
                }

                // Auto-hide popup after duration
                StartCoroutine(HideLevelUpPopupAfterDelay());
            }
        }

        /// <summary>
        /// Hides the level up popup after a delay
        /// </summary>
        private IEnumerator HideLevelUpPopupAfterDelay()
        {
            yield return new WaitForSeconds(levelUpPopupDuration);

            if (levelUpPopup != null)
            {
                levelUpPopup.SetActive(false);
            }
        }

        /// <summary>
        /// Manually closes the level up popup (for button click)
        /// </summary>
        public void CloseLevelUpPopup()
        {
            if (levelUpPopup != null)
            {
                levelUpPopup.SetActive(false);
            }
        }

        /// <summary>
        /// Updates all displays with current progression data
        /// </summary>
        public void RefreshDisplay()
        {
            if (PlayerProgressionManager.Instance != null)
            {
                UpdateMoneyDisplay(PlayerProgressionManager.Instance.CurrentMoney);
                UpdateLevelDisplay(PlayerProgressionManager.Instance.CurrentLevel);
                UpdateXPBar(PlayerProgressionManager.Instance.GetLevelProgressPercentage(), false);
            }
        }
    }
}
