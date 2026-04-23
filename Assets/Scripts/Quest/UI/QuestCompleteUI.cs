using System;
using System.Text;
using DeliveryDriver.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryDriver.Quest.UI
{
    public class QuestCompleteUI : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private GameObject completedPanel;
        [SerializeField] private GameObject failedPanel;

        [Header("Text")]
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private TextMeshProUGUI questNameText;
        [SerializeField] private TextMeshProUGUI statsText;
        [SerializeField] private TextMeshProUGUI rewardText;

        [Header("Actions")]
        [SerializeField] private Button continueButton;

        [Header("Audio")]
        [SerializeField] private AudioSource successSound;
        [SerializeField] private AudioSource failureSound;

        [Header("Layout")]
        [SerializeField] private Vector2 minimumResultPanelSize = new Vector2(860f, 560f);
        [SerializeField] private int minimumResultFontSize = 48;
        [SerializeField] private int minimumTitleFontSize = 30;
        [SerializeField] private int minimumBodyFontSize = 24;

        private Action continueAction;
        private CanvasGroup rootCanvasGroup;
        private RectTransform animatedPanelRect;
        private bool continueButtonBound;

        private void Awake()
        {
            EnsureReadableLayout();
            BindContinueButton();
        }

        private void Start()
        {
            EnsureReadableLayout();
            BindContinueButton();
        }

        private void OnDestroy()
        {
            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(HandleContinueClicked);
            }
        }

        public void SetContinueAction(Action action)
        {
            continueAction = action;
        }

        public void ShowCompleteScreen(QuestData quest, int reward, QuestManager.RewardPenaltyBreakdown? breakdown = null)
        {
            SetVisible(true);
            ApplyVisualState(true);

            if (resultText != null)
            {
                resultText.text = LocalizationTable.Get("delivery_complete");
            }

            if (questNameText != null)
            {
                questNameText.text = quest != null ? quest.QuestName : LocalizationTable.Get("quest_complete_default_name");
            }

            if (statsText != null)
            {
                statsText.text = BuildStatsText(quest, null, 0, breakdown);
            }

            if (rewardText != null)
            {
                rewardText.text = BuildRewardText(LocalizationTable.Get("reward"), reward, true);
            }

            if (successSound != null)
            {
                successSound.Play();
            }
        }

        public void ShowFailedScreen(QuestData quest, string reason, int penaltyAmount = 0, QuestManager.RewardPenaltyBreakdown? breakdown = null)
        {
            SetVisible(true);
            ApplyVisualState(false);

            if (resultText != null)
            {
                resultText.text = LocalizationTable.Get("delivery_failed");
            }

            if (questNameText != null)
            {
                questNameText.text = quest != null ? quest.QuestName : LocalizationTable.Get("quest_failed_default_name");
            }

            if (statsText != null)
            {
                statsText.text = BuildStatsText(quest, reason, penaltyAmount, breakdown);
            }

            if (rewardText != null)
            {
                rewardText.text = BuildRewardText(LocalizationTable.Get("penalty"), penaltyAmount, false);
            }

            if (failureSound != null)
            {
                failureSound.Play();
            }
        }

        public void Hide()
        {
            SetVisible(false);
        }

        private void HandleContinueClicked()
        {
            Hide();
            continueAction?.Invoke();
        }

        private void SetVisible(bool visible)
        {
            GameObject target = rootPanel != null ? rootPanel : gameObject;
            if (visible)
            {
                target.SetActive(true);
                EnsureCanvasGroup(target);
                if (rootCanvasGroup != null)
                {
                    rootCanvasGroup.alpha = 0f;
                    UIAnimationHelper.FadeIn(this, rootCanvasGroup, UIThemeConstants.PanelFadeDuration);

                    ResolveAnimatedPanelRect(target.transform);
                    if (animatedPanelRect != null)
                    {
                        UIAnimationHelper.ScaleIn(this, animatedPanelRect, UIThemeConstants.PanelScaleDuration);
                    }
                }
            }
            else
            {
                if (rootCanvasGroup != null)
                {
                    UIAnimationHelper.FadeOut(this, rootCanvasGroup, UIThemeConstants.PanelFadeDuration * 0.5f, () =>
                    {
                        target.SetActive(false);
                    });
                }
                else
                {
                    target.SetActive(false);
                }
            }
        }

        private void ApplyVisualState(bool isSuccess)
        {
            if (completedPanel != null)
            {
                completedPanel.SetActive(isSuccess);
            }

            if (failedPanel != null)
            {
                failedPanel.SetActive(!isSuccess);
            }

            Color accentColor = isSuccess ? UIThemeConstants.Positive : UIThemeConstants.Negative;
            Color rewardColor = isSuccess ? UIThemeConstants.RewardText : UIThemeConstants.Negative;
            Color buttonColor = isSuccess ? UIThemeConstants.ButtonGreen : UIThemeConstants.ButtonBlue;
            Color rewardCardColor = isSuccess
                ? new Color(0.12f, 0.24f, 0.18f, 0.97f)
                : new Color(0.27f, 0.11f, 0.12f, 0.97f);

            if (resultText != null)
            {
                resultText.color = accentColor;
            }

            if (rewardText != null)
            {
                rewardText.color = rewardColor;
            }

            Transform resultPanel = ResolveAnimatedPanelRect(rootPanel != null ? rootPanel.transform : transform);
            if (resultPanel == null)
            {
                return;
            }

            Transform accentBar = resultPanel.Find("AccentBar");
            if (accentBar != null && accentBar.TryGetComponent(out Image accentImage))
            {
                accentImage.color = accentColor;
            }

            Transform rewardCard = resultPanel.Find("Content/RewardCard");
            if (rewardCard != null && rewardCard.TryGetComponent(out Image rewardCardImage))
            {
                rewardCardImage.color = rewardCardColor;
            }

            if (continueButton != null && continueButton.TryGetComponent(out Image buttonImage))
            {
                buttonImage.color = buttonColor;
                ColorBlock colors = continueButton.colors;
                colors.normalColor = buttonColor;
                colors.highlightedColor = Color.Lerp(buttonColor, Color.white, 0.1f);
                colors.pressedColor = Color.Lerp(buttonColor, Color.black, 0.15f);
                colors.selectedColor = colors.highlightedColor;
                colors.disabledColor = new Color(buttonColor.r, buttonColor.g, buttonColor.b, 0.45f);
                continueButton.colors = colors;
            }
        }

        private void EnsureCanvasGroup(GameObject target)
        {
            if (rootCanvasGroup != null) return;
            rootCanvasGroup = target.GetComponent<CanvasGroup>();
            if (rootCanvasGroup == null)
            {
                rootCanvasGroup = target.AddComponent<CanvasGroup>();
            }
        }

        private void BindContinueButton()
        {
            if (continueButtonBound || continueButton == null)
            {
                return;
            }

            continueButton.onClick.RemoveListener(HandleContinueClicked);
            continueButton.onClick.AddListener(HandleContinueClicked);
            continueButtonBound = true;
        }

        private Transform ResolveAnimatedPanelRect(Transform rootTransform)
        {
            if (animatedPanelRect != null)
            {
                return animatedPanelRect;
            }

            if (rootTransform == null)
            {
                return null;
            }

            Transform resultPanel = rootTransform.Find("ResultPanel");
            if (resultPanel == null && rootPanel != null && rootPanel.transform != rootTransform)
            {
                resultPanel = rootPanel.transform.Find("ResultPanel");
            }

            animatedPanelRect = resultPanel as RectTransform;
            return animatedPanelRect;
        }

        private static string BuildStatsText(QuestData quest, string failureReason, int penaltyAmount = 0, QuestManager.RewardPenaltyBreakdown? breakdown = null)
        {
            if (quest == null)
            {
                return string.IsNullOrWhiteSpace(failureReason)
                    ? string.Empty
                    : BuildMetricLine(GetDisplayLabel("reason"), failureReason, UIThemeConstants.Negative);
            }

            float timeTaken = Mathf.Max(0f, quest.TimeLimit - quest.TimeRemaining);
            float timeRatio = quest.TimeLimit > 0.01f ? timeTaken / quest.TimeLimit : 0f;
            string timeLine = BuildMetricLine(
                GetDisplayLabel("time"),
                $"{FormatTime(timeTaken)} / {FormatTime(quest.TimeLimit)}",
                timeRatio >= 0.9f ? UIThemeConstants.TimerDanger : timeRatio >= 0.7f ? UIThemeConstants.TimerWarning : UIThemeConstants.TextPrimary);

            string stopsLine = string.Empty;
            if (quest.DeliveryLocations != null && quest.DeliveryLocations.Count > 1)
            {
                int totalStops = quest.DeliveryLocations.Count;
                int completedStops = Mathf.Clamp(quest.CurrentDeliveryIndex, 0, totalStops);
                stopsLine = BuildMetricLine(GetDisplayLabel("stops"), $"{completedStops}/{totalStops}", UIThemeConstants.TextPrimary);
            }

            string cargoLine = string.Empty;
            if (quest.Cargo != null && quest.Cargo.IsFragile)
            {
                Color cargoColor = quest.Cargo.CargoHealth >= 90f
                    ? UIThemeConstants.Positive
                    : quest.Cargo.CargoHealth >= 65f ? UIThemeConstants.Warning : UIThemeConstants.Negative;
                cargoLine = BuildMetricLine(GetDisplayLabel("cargo_condition"), $"{quest.Cargo.CargoHealth:0}%", cargoColor);
            }

            string reasonLine = string.IsNullOrWhiteSpace(failureReason)
                ? string.Empty
                : BuildMetricLine(GetDisplayLabel("reason"), failureReason, UIThemeConstants.Negative);
            string penaltyLine = penaltyAmount > 0
                ? BuildMetricLine(GetDisplayLabel("penalty"), $"-${penaltyAmount}", UIThemeConstants.Negative)
                : string.Empty;
            string collisionLine = BuildMetricLine(
                GetDisplayLabel("collisions"),
                $"{quest.CollisionCount} (NPC: {quest.NpcCollisionCount})",
                quest.CollisionCount == 0 ? UIThemeConstants.Positive : quest.CollisionCount <= 2 ? UIThemeConstants.Warning : UIThemeConstants.Negative);
            string hardBrakeLine = BuildMetricLine(
                GetDisplayLabel("hard_brakes"),
                quest.HardBrakeCount.ToString(),
                quest.HardBrakeCount == 0 ? UIThemeConstants.Positive : UIThemeConstants.Warning);
            string driftLine = BuildMetricLine(
                GetDisplayLabel("drift_score"),
                quest.DriftScorePoints.ToString(),
                quest.DriftScorePoints > 0 ? UIThemeConstants.Info : UIThemeConstants.TextPrimary);

            string[] lines = { timeLine, stopsLine, cargoLine, collisionLine, hardBrakeLine, driftLine, reasonLine, penaltyLine };
            return string.Join("\n", Array.FindAll(lines, line => !string.IsNullOrWhiteSpace(line)));
        }

        private static string FormatTime(float timeSeconds)
        {
            if (timeSeconds <= 0f)
            {
                return "00:00";
            }

            int minutes = Mathf.FloorToInt(timeSeconds / 60f);
            int seconds = Mathf.FloorToInt(timeSeconds % 60f);
            return $"{minutes:00}:{seconds:00}";
        }

        private static string BuildMetricLine(string label, string value, Color valueColor)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(96);
            builder.Append("<size=18><color=#")
                .Append(ColorUtility.ToHtmlStringRGB(UIThemeConstants.TextSubheader))
                .Append("><b>")
                .Append(label)
                .Append("</b></color></size>  ")
                .Append("<size=22><color=#")
                .Append(ColorUtility.ToHtmlStringRGB(valueColor))
                .Append("><b>")
                .Append(value)
                .Append("</b></color></size>");

            return builder.ToString();
        }

        private static string GetDisplayLabel(string key)
        {
            string label = LocalizationTable.Get(key);
            if (!string.IsNullOrWhiteSpace(label) && !string.Equals(label, key, StringComparison.Ordinal))
            {
                return label;
            }

            string[] parts = key.Split('_');
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i]))
                {
                    continue;
                }

                string part = parts[i].ToLowerInvariant();
                parts[i] = char.ToUpperInvariant(part[0]) + part.Substring(1);
            }

            return string.Join(" ", parts);
        }

        private static string BuildRewardText(string caption, int amount, bool positive)
        {
            string colorHex = ColorUtility.ToHtmlStringRGB(positive ? UIThemeConstants.RewardText : UIThemeConstants.Negative);
            string amountText = positive ? $"+ ${amount}" : amount > 0 ? $"- ${amount}" : "$0";
            return $"<size=18><color=#{ColorUtility.ToHtmlStringRGB(UIThemeConstants.TextSubheader)}><b>{caption}</b></color></size>\n<size=40><color=#{colorHex}><b>{amountText}</b></color></size>";
        }

        private void EnsureReadableLayout()
        {
            Transform resultPanel = ResolveAnimatedPanelRect(rootPanel != null ? rootPanel.transform : transform);
            RectTransform resultRect = resultPanel as RectTransform;
            if (resultRect != null)
            {
                Vector2 size = resultRect.sizeDelta;
                resultRect.sizeDelta = new Vector2(
                    Mathf.Max(size.x, minimumResultPanelSize.x),
                    Mathf.Max(size.y, minimumResultPanelSize.y));

                if (resultPanel.TryGetComponent(out Image panelImage))
                {
                    panelImage.color = UIThemeConstants.PanelBackground;
                }
            }

            GameObject target = rootPanel != null ? rootPanel : gameObject;
            if (target.TryGetComponent(out Image overlayImage))
            {
                overlayImage.color = UIThemeConstants.OverlayBackground;
            }

            if (resultPanel != null)
            {
                Transform statsCard = resultPanel.Find("Content/StatsCard");
                if (statsCard != null && statsCard.TryGetComponent(out Image statsImage))
                {
                    statsImage.color = UIThemeConstants.SectionBackground;
                }
            }

            ApplyMinimumTextStyle(resultText, minimumResultFontSize);
            ApplyMinimumTextStyle(questNameText, minimumTitleFontSize);
            ApplyMinimumTextStyle(statsText, minimumBodyFontSize);
            ApplyMinimumTextStyle(rewardText, minimumTitleFontSize);

            if (resultText != null)
            {
                resultText.alignment = TextAlignmentOptions.Center;
                resultText.enableAutoSizing = true;
                resultText.fontSizeMin = minimumTitleFontSize;
                resultText.fontSizeMax = minimumResultFontSize + 12;
                resultText.characterSpacing = 3f;
            }

            if (questNameText != null)
            {
                questNameText.alignment = TextAlignmentOptions.Center;
                questNameText.enableAutoSizing = true;
                questNameText.fontSizeMin = minimumBodyFontSize;
                questNameText.fontSizeMax = minimumTitleFontSize + 6;
                questNameText.color = UIThemeConstants.TextHeader;
            }

            if (statsText != null)
            {
                statsText.alignment = TextAlignmentOptions.TopLeft;
                statsText.textWrappingMode = TextWrappingModes.Normal;
                statsText.enableAutoSizing = true;
                statsText.fontSizeMin = 18f;
                statsText.fontSizeMax = minimumBodyFontSize;
                statsText.overflowMode = TextOverflowModes.Ellipsis;
                statsText.lineSpacing = 6f;
                statsText.color = UIThemeConstants.TextSecondary;
            }

            if (rewardText != null)
            {
                rewardText.alignment = TextAlignmentOptions.Center;
                rewardText.enableAutoSizing = true;
                rewardText.fontSizeMin = minimumBodyFontSize;
                rewardText.fontSizeMax = minimumTitleFontSize + 10;
                rewardText.lineSpacing = 8f;
            }

            if (continueButton != null)
            {
                RectTransform buttonRect = continueButton.transform as RectTransform;
                if (buttonRect != null)
                {
                    buttonRect.sizeDelta = new Vector2(Mathf.Max(buttonRect.sizeDelta.x, 260f), Mathf.Max(buttonRect.sizeDelta.y, 64f));
                }
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
