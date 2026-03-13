using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using DeliveryDriver.UI;

namespace DeliveryDriver.Quest.UI
{
    /// <summary>
    /// Controls the tutorial UI overlay and display
    /// </summary>
    public class TutorialUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject tutorialPanel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private TextMeshProUGUI stepCounterText;

        [Header("Highlight")]
        [SerializeField] private Image highlightOverlay;
        [SerializeField] private RectTransform highlightWindow;
        [SerializeField] private GameObject arrowPointer;

        [Header("Animation")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.2f;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Colors")]
        [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 0.7f);

        [Header("Arrow Bounce")]
        [SerializeField] private float arrowBounceAmount = 10f;
        [SerializeField] private float arrowBounceSpeed = 3f;

        private TutorialStep currentStep;
        private bool isVisible = false;
        private Vector3 arrowBasePosition;
        private bool arrowBouncing;

        private void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            SetupButtons();
            Hide();
        }

        private void Update()
        {
            if (arrowBouncing && arrowPointer != null)
            {
                RectTransform arrowRect = arrowPointer.GetComponent<RectTransform>();
                if (arrowRect != null)
                {
                    float bounce = Mathf.Sin(Time.unscaledTime * arrowBounceSpeed * Mathf.PI) * arrowBounceAmount;
                    arrowRect.anchoredPosition = (Vector2)arrowBasePosition + new Vector2(0f, bounce);
                }
            }
        }

        private void SetupButtons()
        {
            if (nextButton != null)
            {
                nextButton.onClick.AddListener(OnNextButtonClicked);
            }

            if (skipButton != null)
            {
                skipButton.onClick.AddListener(OnSkipButtonClicked);
            }
        }

        public void Show()
        {
            if (isVisible)
            {
                return;
            }

            isVisible = true;

            if (tutorialPanel != null)
            {
                tutorialPanel.SetActive(true);
            }

            StartCoroutine(FadeIn());
        }

        public void Hide()
        {
            if (!isVisible)
            {
                return;
            }

            isVisible = false;
            StartCoroutine(FadeOut());
        }

        public void DisplayStep(TutorialStep step)
        {
            if (step == null)
            {
                return;
            }

            currentStep = step;

            // Update title
            if (titleText != null)
            {
                titleText.text = string.IsNullOrEmpty(step.title) ? "Tutorial" : step.title;
            }

            // Update message
            if (messageText != null)
            {
                messageText.text = step.message;
            }

            // Update step counter with localized format
            if (stepCounterText != null && TutorialManager.Instance != null)
            {
                int current = TutorialManager.Instance.CurrentStepIndex + 1;
                int total = TutorialManager.Instance.TotalSteps;
                string template = LocalizationTable.Get("step_of");
                stepCounterText.text = string.Format(template, current, total);
            }

            // Update buttons visibility with localized text
            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(step.triggerType == TutorialTriggerType.ManualAdvance);

                TextMeshProUGUI buttonText = nextButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.text = LocalizationTable.Get("next");
                }
            }

            if (skipButton != null)
            {
                skipButton.gameObject.SetActive(step.canSkip);
                TextMeshProUGUI skipText = skipButton.GetComponentInChildren<TextMeshProUGUI>();
                if (skipText != null)
                {
                    skipText.text = LocalizationTable.Get("skip");
                }
            }

            // Update highlight
            UpdateHighlight(step);

            // Show waiting indicator or keyboard hint
            if (step.triggerType != TutorialTriggerType.ManualAdvance)
            {
                ShowWaitingIndicator(step);
            }
            else
            {
                // Show keyboard shortcut hint
                if (messageText != null)
                {
                    string hint = LocalizationTable.Get("continue_space");
                    messageText.text = step.message + $"\n\n<size=80%><i>{hint}</i></size>";
                }
            }
        }

        private void UpdateHighlight(TutorialStep step)
        {
            if (step.highlightTarget != null && highlightWindow != null)
            {
                // Show highlight around target UI element
                highlightWindow.gameObject.SetActive(true);
                highlightWindow.position = step.highlightTarget.position;
                highlightWindow.sizeDelta = step.highlightTarget.sizeDelta;

                // Show arrow pointer with bounce
                if (arrowPointer != null)
                {
                    arrowPointer.SetActive(true);
                    RectTransform arrowRect = arrowPointer.GetComponent<RectTransform>();
                    if (arrowRect != null)
                    {
                        arrowRect.position = step.highlightTarget.position;
                        arrowBasePosition = arrowRect.anchoredPosition;
                        float angle = Mathf.Atan2(step.arrowDirection.y, step.arrowDirection.x) * Mathf.Rad2Deg;
                        arrowRect.rotation = Quaternion.Euler(0f, 0f, angle);
                        arrowBouncing = true;
                    }
                }
            }
            else
            {
                // Hide highlight
                if (highlightWindow != null)
                {
                    highlightWindow.gameObject.SetActive(false);
                }

                if (arrowPointer != null)
                {
                    arrowPointer.SetActive(false);
                    arrowBouncing = false;
                }
            }

            // Update overlay color
            if (highlightOverlay != null)
            {
                highlightOverlay.color = overlayColor;
            }
        }

        private void ShowWaitingIndicator(TutorialStep step)
        {
            // Show indicator text based on trigger type
            string waitingText = "";

            switch (step.triggerType)
            {
                case TutorialTriggerType.KeyPress:
                    waitingText = $"\n\n<i>Press {step.triggerKey} to continue...</i>";
                    break;
                case TutorialTriggerType.QuestOpened:
                    waitingText = "\n\n<i>Waiting for you to open the quest menu...</i>";
                    break;
                case TutorialTriggerType.QuestAccepted:
                    waitingText = "\n\n<i>Waiting for you to accept a quest...</i>";
                    break;
                case TutorialTriggerType.CargoPickedUp:
                    waitingText = "\n\n<i>Waiting for you to pick up the cargo...</i>";
                    break;
                case TutorialTriggerType.CargoDelivered:
                    waitingText = "\n\n<i>Waiting for you to deliver the cargo...</i>";
                    break;
                case TutorialTriggerType.QuestCompleted:
                    waitingText = "\n\n<i>Waiting for quest completion...</i>";
                    break;
            }

            if (messageText != null && !string.IsNullOrEmpty(waitingText))
            {
                messageText.text = step.message + waitingText;
            }
        }

        private void HideWaitingIndicator()
        {
            // Nothing specific to hide, waiting text is part of message
        }

        private IEnumerator FadeIn()
        {
            float elapsed = 0f;

            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Clamp01(elapsed / fadeInDuration);

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = alpha;
                }

                yield return null;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }

        private IEnumerator FadeOut()
        {
            float elapsed = 0f;
            float startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeOutDuration);

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = alpha;
                }

                yield return null;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            if (tutorialPanel != null)
            {
                tutorialPanel.SetActive(false);
            }
        }

        private void OnNextButtonClicked()
        {
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.AdvanceToNextStep();
            }
        }

        private void OnSkipButtonClicked()
        {
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.SkipTutorial();
            }
        }

        public void SetInteractable(bool interactable)
        {
            if (canvasGroup != null)
            {
                canvasGroup.interactable = interactable;
                canvasGroup.blocksRaycasts = interactable;
            }
        }
    }
}
