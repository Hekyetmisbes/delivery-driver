using System.Text;
using DeliveryDriver.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DeliveryDriver.Quest;

namespace DeliveryDriver.Quest.UI
{
    public class ActiveQuestUI : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] private TextMeshProUGUI objectiveText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI distanceText;
        [SerializeField] private TextMeshProUGUI feedbackText;

        [Header("Cargo Health")]
        [SerializeField] private Image cargoHealthFill;
        [SerializeField] private GameObject cargoHealthPanel;

        [Header("Route Estimate")]
        [SerializeField] private float estimatedAverageSpeedMetersPerSec = 11f;
        [SerializeField] private float distanceDisplayMultiplier = 10f;

        [Header("Driving Feedback")]
        [SerializeField] private float feedbackDuration = 1.3f;

        [Header("Layout")]
        [SerializeField] private Vector2 minimumPanelSize = new Vector2(520f, 250f);
        [SerializeField] private int minimumObjectiveFontSize = 26;
        [SerializeField] private int minimumTimerFontSize = 24;
        [SerializeField] private int minimumDistanceFontSize = 22;

        [Header("Feedback Animation")]
        [SerializeField] private float feedbackFadeInDuration = 0.2f;
        [SerializeField] private float feedbackFadeOutDuration = 0.35f;

        private QuestData currentQuest;
        private string objectiveBaseText = string.Empty;
        private string lastObjectiveText = string.Empty;
        private int lastTimerSeconds = -1;
        private Color lastTimerColor = Color.clear;
        private string lastDistanceText = string.Empty;
        private string lastCompactLine = string.Empty;
        private float lastCargoHealth = -1f;
        private bool lastCargoPanelActive = false;
        private string currentFeedbackText = string.Empty;
        private Color currentFeedbackColor = Color.white;
        private float feedbackExpireAt;
        private bool feedbackVisible;
        private bool feedbackFadingOut;
        private readonly StringBuilder objectiveBuilder = new StringBuilder(64);
        private QuestManager questManager;
        private Transform playerTransform;
        private bool hasLoggedRuntimeUpdater;
        private bool timerPulsing;
        private RectTransform timerRect;
        private CanvasGroup feedbackCanvasGroup;
        private QuestLocation cachedObjective;
        private int frameCounter;
        private const int UpdateEveryNFrames = 2;
        private float lastExternalDistance;
        private float lastExternalTimeRemaining;
        private float lastExternalTimeLimit;

        private void Awake()
        {
            AutoBindTextReferences();
            EnsureReadableLayout();
            questManager = QuestManager.Instance;
        }

        private void Update()
        {
            if (currentQuest == null)
            {
                return;
            }

            frameCounter++;
            bool fullUpdate = frameCounter % UpdateEveryNFrames == 0;

            if (playerTransform == null)
            {
                ResolvePlayerTransform();
            }

            float distance = 0f;
            if (playerTransform != null)
            {
                if (fullUpdate || cachedObjective == null)
                {
                    cachedObjective = GetCurrentObjective(currentQuest);
                }
                if (cachedObjective != null)
                {
                    distance = Vector3.Distance(playerTransform.position, cachedObjective.Position);
                }
            }

            UpdateCompactTimerLine(currentQuest.TimeRemaining, currentQuest.TimeLimit, distance);
            TryExpireFeedback();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!hasLoggedRuntimeUpdater)
            {
                hasLoggedRuntimeUpdater = true;
                Debug.Log($"[ActiveQuestUI] Runtime update active. Player='{playerTransform.name}', Distance={distance:F1}, Time={currentQuest.TimeRemaining:F1}");
            }
#endif
        }

        public void UpdateQuestDisplay(QuestData quest)
        {
            currentQuest = quest;

            if (currentQuest == null)
            {
                ClearDisplay();
                return;
            }

            string objective = BuildObjectiveText(currentQuest);
            if (!string.Equals(objectiveBaseText, objective, System.StringComparison.Ordinal))
            {
                objectiveBaseText = objective;
                RefreshObjectiveText();
            }

            UpdateCompactTimerLine(currentQuest.TimeRemaining, currentQuest.TimeLimit, 0f);

            if (currentQuest.Cargo != null && currentQuest.Cargo.IsFragile)
            {
                UpdateCargoHealth(currentQuest.Cargo.CargoHealth);
                if (cargoHealthPanel != null)
                {
                    if (!lastCargoPanelActive)
                    {
                        cargoHealthPanel.SetActive(true);
                        lastCargoPanelActive = true;
                    }
                }
            }
            else
            {
                if (cargoHealthPanel != null)
                {
                    if (lastCargoPanelActive)
                    {
                        cargoHealthPanel.SetActive(false);
                        lastCargoPanelActive = false;
                    }
                }
            }
        }

        public void UpdateTimer(float timeRemaining, float timeLimit)
        {
            lastExternalTimeRemaining = timeRemaining;
            lastExternalTimeLimit = timeLimit;
            UpdateCompactTimerLine(timeRemaining, timeLimit, lastExternalDistance);
        }

        public void UpdateDistance(float distanceMeters)
        {
            lastExternalDistance = distanceMeters;
            UpdateCompactTimerLine(lastExternalTimeRemaining, lastExternalTimeLimit, distanceMeters);
        }

        public void UpdateCompactTimerLine(float timeRemaining, float timeLimit, float distanceMeters)
        {
            if (timerText == null)
            {
                return;
            }

            int seconds = Mathf.CeilToInt(timeRemaining);
            string distStr = FormatDistanceCompact(distanceMeters, estimatedAverageSpeedMetersPerSec, distanceDisplayMultiplier);
            string compactLine = $"{FormatTime(seconds)}  |  {distStr}";

            if (!string.Equals(lastCompactLine, compactLine, System.StringComparison.Ordinal))
            {
                lastCompactLine = compactLine;
                lastTimerSeconds = seconds;
                timerText.text = compactLine;
            }

            Color color = GetTimerColor(timeRemaining, timeLimit);
            if (color != lastTimerColor)
            {
                timerText.color = color;
                lastTimerColor = color;
            }

            // Pulse animation when timer is critical (<20% remaining)
            bool shouldPulse = timeLimit > 0f && timeRemaining < timeLimit * 0.2f && timeRemaining > 0f;
            if (shouldPulse && !timerPulsing)
            {
                timerPulsing = true;
                if (timerRect == null)
                {
                    timerRect = timerText.GetComponent<RectTransform>();
                }
                if (timerRect != null)
                {
                    UIAnimationHelper.PulseScale(this, timerRect, 1.12f, UIThemeConstants.PulseDuration);
                }
            }
            else if (!shouldPulse)
            {
                timerPulsing = false;
            }

            // Also update separate distanceText if it exists (backwards compat)
            if (distanceText != null)
            {
                distanceText.gameObject.SetActive(false);
            }
        }

        public void ShowDrivingFeedback(string message, int scoreDelta)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            string scoreText = scoreDelta == 0 ? string.Empty : $" ({(scoreDelta > 0 ? "+" : "")}${scoreDelta})";
            currentFeedbackText = $"{message}{scoreText}";
            currentFeedbackColor = scoreDelta < 0 ? new Color(1f, 0.45f, 0.35f) : new Color(0.55f, 1f, 0.55f);
            feedbackExpireAt = Time.time + Mathf.Max(0.25f, feedbackDuration);
            feedbackVisible = true;
            feedbackFadingOut = false;
            RefreshObjectiveText();
            UpdateFeedbackTextComponent();

            // Fade in feedback text
            EnsureFeedbackCanvasGroup();
            if (feedbackCanvasGroup != null)
            {
                UIAnimationHelper.FadeIn(this, feedbackCanvasGroup, feedbackFadeInDuration);
            }
        }

        public void UpdateCargoHealth(float health)
        {
            if (cargoHealthFill == null)
            {
                return;
            }

            float normalized = Mathf.Clamp01(health / 100f);
            if (Mathf.Abs(normalized - lastCargoHealth) > 0.001f)
            {
                cargoHealthFill.fillAmount = normalized;
                lastCargoHealth = normalized;
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        private void ClearDisplay()
        {
            if (objectiveText != null)
            {
                objectiveText.text = string.Empty;
            }

            if (timerText != null)
            {
                timerText.text = "00:00";
                timerText.color = Color.white;
            }

            if (distanceText != null)
            {
                distanceText.text = "--";
            }

            if (feedbackText != null)
            {
                feedbackText.text = string.Empty;
            }

            if (cargoHealthPanel != null)
            {
                cargoHealthPanel.SetActive(false);
            }

            lastObjectiveText = string.Empty;
            objectiveBaseText = string.Empty;
            lastTimerSeconds = -1;
            lastTimerColor = Color.clear;
            lastDistanceText = string.Empty;
            lastCargoHealth = -1f;
            lastCargoPanelActive = false;
            currentFeedbackText = string.Empty;
            feedbackVisible = false;
        }

        private string BuildObjectiveText(QuestData quest)
        {
            if (quest == null)
            {
                return string.Empty;
            }

            if (!quest.HasPickedUpCargo)
            {
                string pickupName = quest.PickupLocation != null ? quest.PickupLocation.LocationName : LocalizationTable.Get("unknown");
                return LocalizationTable.Format("activequest_go_to_pickup", pickupName);
            }

            QuestLocation delivery = GetCurrentDeliveryLocation(quest);
            string deliveryName = delivery != null ? delivery.LocationName : "delivery";
            int totalStops = quest.DeliveryLocations != null ? quest.DeliveryLocations.Count : 0;
            int currentStop = Mathf.Clamp(quest.CurrentDeliveryIndex + 1, 1, Mathf.Max(1, totalStops));

            if (totalStops > 1)
            {
                return LocalizationTable.Format("activequest_deliver_progress", currentStop, totalStops, deliveryName);
            }

            return LocalizationTable.Format("activequest_deliver_to", deliveryName);
        }

        private static QuestLocation GetCurrentDeliveryLocation(QuestData quest)
        {
            if (quest?.DeliveryLocations == null || quest.DeliveryLocations.Count == 0)
            {
                return null;
            }

            int index = Mathf.Clamp(quest.CurrentDeliveryIndex, 0, quest.DeliveryLocations.Count - 1);
            return quest.DeliveryLocations[index];
        }

        private static QuestLocation GetCurrentObjective(QuestData quest)
        {
            if (quest == null)
            {
                return null;
            }

            if (!quest.HasPickedUpCargo)
            {
                return quest.PickupLocation;
            }

            return GetCurrentDeliveryLocation(quest);
        }

        private static string FormatTime(int timeSeconds)
        {
            int minutes = Mathf.Max(0, Mathf.FloorToInt(timeSeconds / 60f));
            int seconds = Mathf.Max(0, timeSeconds % 60);
            return $"{minutes:00}:{seconds:00}";
        }

        private static string FormatDistanceCompact(float distanceMeters, float estimatedSpeed, float displayMultiplier = 1f)
        {
            if (distanceMeters <= 0f)
            {
                return "-- m";
            }

            float d = distanceMeters * displayMultiplier;
            string distStr = d >= 1000f ? $"{d / 1000f:F1}km" : $"{Mathf.RoundToInt(d)} m";

            if (estimatedSpeed > 0.1f)
            {
                int etaSeconds = Mathf.CeilToInt(distanceMeters / estimatedSpeed);
                return $"{distStr} (~{FormatTime(etaSeconds)})";
            }

            return distStr;
        }

        private static Color GetTimerColor(float timeRemaining, float timeLimit)
        {
            if (timeLimit <= 0f)
            {
                return Color.white;
            }

            if (timeRemaining > timeLimit * 0.5f)
            {
                return Color.green;
            }

            if (timeRemaining > timeLimit * 0.25f)
            {
                return Color.yellow;
            }

            return Color.red;
        }

        private void AutoBindTextReferences()
        {
            if (objectiveText == null)
            {
                objectiveText = FindTextByName("Objective");
                if (objectiveText == null)
                {
                    objectiveText = FindTextByName("QuestName");
                }
            }

            if (timerText == null)
            {
                timerText = FindTextByName("Timer");
            }

            if (distanceText == null)
            {
                distanceText = FindTextByName("Distance");
            }

            if (feedbackText == null)
            {
                feedbackText = FindTextByName("Feedback");
            }
        }

        private TextMeshProUGUI FindTextByName(string objectName)
        {
            Transform target = transform.Find(objectName);
            if (target == null)
            {
                return null;
            }

            return target.GetComponent<TextMeshProUGUI>();
        }

        private void ResolvePlayerTransform()
        {
            CarController car = FindAnyObjectByType<CarController>();
            if (car != null)
            {
                playerTransform = car.transform;
                return;
            }

            if (questManager == null)
            {
                questManager = QuestManager.Instance;
            }

            if (questManager != null && questManager.PlayerTransform != null)
            {
                playerTransform = questManager.PlayerTransform;
                return;
            }

            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
            {
                playerTransform = taggedPlayer.transform;
            }
        }

        private void RefreshObjectiveText()
        {
            if (objectiveText == null)
            {
                return;
            }

            string composed = objectiveBaseText;
            if (feedbackVisible && !string.IsNullOrWhiteSpace(currentFeedbackText))
            {
                composed = $"{objectiveBaseText}\n<color=#{ColorUtility.ToHtmlStringRGB(currentFeedbackColor)}>{currentFeedbackText}</color>";
            }

            if (!string.Equals(lastObjectiveText, composed, System.StringComparison.Ordinal))
            {
                lastObjectiveText = composed;
                objectiveText.text = composed;
            }
        }

        private void TryExpireFeedback()
        {
            if (!feedbackVisible || Time.time < feedbackExpireAt)
            {
                return;
            }

            if (!feedbackFadingOut)
            {
                feedbackFadingOut = true;
                EnsureFeedbackCanvasGroup();
                if (feedbackCanvasGroup != null)
                {
                    UIAnimationHelper.FadeOut(this, feedbackCanvasGroup, feedbackFadeOutDuration, () =>
                    {
                        feedbackVisible = false;
                        feedbackFadingOut = false;
                        currentFeedbackText = string.Empty;
                        UpdateFeedbackTextComponent();
                        RefreshObjectiveText();
                        if (feedbackCanvasGroup != null)
                        {
                            feedbackCanvasGroup.alpha = 1f;
                        }
                    });
                }
                else
                {
                    feedbackVisible = false;
                    feedbackFadingOut = false;
                    currentFeedbackText = string.Empty;
                    UpdateFeedbackTextComponent();
                    RefreshObjectiveText();
                }
            }
        }

        private void UpdateFeedbackTextComponent()
        {
            if (feedbackText == null)
            {
                return;
            }

            if (!feedbackVisible || string.IsNullOrWhiteSpace(currentFeedbackText))
            {
                feedbackText.text = string.Empty;
                return;
            }

            feedbackText.text = currentFeedbackText;
            feedbackText.color = currentFeedbackColor;
        }

        private void EnsureReadableLayout()
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null && parentCanvas.name == "Quest UI Canvas")
            {
                CanvasScaler scaler = parentCanvas.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920f, 1080f);
                    scaler.matchWidthOrHeight = 0.5f;
                }

                RectTransform canvasRect = parentCanvas.transform as RectTransform;
                if (canvasRect != null)
                {
                    canvasRect.localScale = Vector3.one;
                }
            }

            RectTransform panelRect = transform as RectTransform;
            if (panelRect != null)
            {
                Vector2 currentSize = panelRect.sizeDelta;
                float width = currentSize.x <= 0f ? minimumPanelSize.x : currentSize.x;
                float height = currentSize.y <= 0f ? minimumPanelSize.y : currentSize.y;
                if (width < minimumPanelSize.x || height < minimumPanelSize.y)
                {
                    panelRect.sizeDelta = new Vector2(
                        Mathf.Max(width, minimumPanelSize.x),
                        Mathf.Max(height, minimumPanelSize.y));
                }
            }

            ApplyMinimumTextStyle(objectiveText, minimumObjectiveFontSize);
            ApplyMinimumTextStyle(distanceText, minimumDistanceFontSize);
            ApplyMinimumTextStyle(timerText, minimumTimerFontSize);
            ApplyMinimumTextStyle(feedbackText, minimumDistanceFontSize);
        }

        private void EnsureFeedbackCanvasGroup()
        {
            if (feedbackCanvasGroup != null || feedbackText == null)
            {
                return;
            }

            feedbackCanvasGroup = feedbackText.GetComponent<CanvasGroup>();
            if (feedbackCanvasGroup == null)
            {
                feedbackCanvasGroup = feedbackText.gameObject.AddComponent<CanvasGroup>();
            }
        }

        private static void ApplyMinimumTextStyle(TextMeshProUGUI text, int minimumFontSize)
        {
            if (text == null)
            {
                return;
            }

            if (text.fontSize < minimumFontSize)
            {
                text.fontSize = minimumFontSize;
            }

            text.textWrappingMode = TextWrappingModes.Normal;
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }
        }
    }
}
