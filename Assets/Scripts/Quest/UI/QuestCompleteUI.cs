using System;
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

        private Action continueAction;

        private void Awake()
        {
            if (continueButton != null)
            {
                continueButton.onClick.AddListener(HandleContinueClicked);
            }
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

            if (completedPanel != null)
            {
                completedPanel.SetActive(true);
            }

            if (failedPanel != null)
            {
                failedPanel.SetActive(false);
            }

            if (resultText != null)
            {
                resultText.text = "DELIVERY COMPLETE";
            }

            if (questNameText != null)
            {
                questNameText.text = quest != null ? quest.QuestName : "Delivery Complete";
            }

            if (statsText != null)
            {
                statsText.text = BuildStatsText(quest, null, 0, breakdown);
            }

            if (rewardText != null)
            {
                rewardText.text = $"+ ${reward}";
            }

            if (successSound != null)
            {
                successSound.Play();
            }
        }

        public void ShowFailedScreen(QuestData quest, string reason, int penaltyAmount = 0, QuestManager.RewardPenaltyBreakdown? breakdown = null)
        {
            SetVisible(true);

            if (completedPanel != null)
            {
                completedPanel.SetActive(false);
            }

            if (failedPanel != null)
            {
                failedPanel.SetActive(true);
            }

            if (resultText != null)
            {
                resultText.text = "DELIVERY FAILED";
            }

            if (questNameText != null)
            {
                questNameText.text = quest != null ? quest.QuestName : "Delivery Failed";
            }

            if (statsText != null)
            {
                statsText.text = BuildStatsText(quest, reason, penaltyAmount, breakdown);
            }

            if (rewardText != null)
            {
                rewardText.text = penaltyAmount > 0 ? $"- ${penaltyAmount}" : "$0";
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
            if (rootPanel != null)
            {
                rootPanel.SetActive(visible);
                return;
            }

            gameObject.SetActive(visible);
        }

        private static string BuildStatsText(QuestData quest, string failureReason, int penaltyAmount = 0, QuestManager.RewardPenaltyBreakdown? breakdown = null)
        {
            if (quest == null)
            {
                return string.IsNullOrWhiteSpace(failureReason) ? string.Empty : $"Reason: {failureReason}";
            }

            float timeTaken = Mathf.Max(0f, quest.TimeLimit - quest.TimeRemaining);
            string timeLine = $"Time: {FormatTime(timeTaken)} / {FormatTime(quest.TimeLimit)}";

            string stopsLine = string.Empty;
            if (quest.DeliveryLocations != null && quest.DeliveryLocations.Count > 1)
            {
                int totalStops = quest.DeliveryLocations.Count;
                int completedStops = Mathf.Clamp(quest.CurrentDeliveryIndex, 0, totalStops);
                stopsLine = $"Stops: {completedStops}/{totalStops}";
            }

            string cargoLine = string.Empty;
            if (quest.Cargo != null && quest.Cargo.IsFragile)
            {
                cargoLine = $"Cargo condition: {quest.Cargo.CargoHealth:0}%";
            }

            string reasonLine = string.IsNullOrWhiteSpace(failureReason) ? string.Empty : $"Reason: {failureReason}";
            string penaltyLine = penaltyAmount > 0 ? $"Penalty: -${penaltyAmount}" : string.Empty;
            string collisionLine = quest.CollisionCount > 0 ? $"Collisions: {quest.CollisionCount} (NPC: {quest.NpcCollisionCount})" : "Collisions: 0";
            string hardBrakeLine = quest.HardBrakeCount > 0 ? $"Hard brakes: {quest.HardBrakeCount}" : "Hard brakes: 0";
            string driftLine = quest.DriftScorePoints > 0 ? $"Drift score: {quest.DriftScorePoints}" : "Drift score: 0";

            string breakdownLine = string.Empty;
            if (breakdown.HasValue)
            {
                QuestManager.RewardPenaltyBreakdown b = breakdown.Value;
                string driftBonusText = b.DriftBonus > 0 ? $" | Drift bonus: +${b.DriftBonus}" : string.Empty;
                breakdownLine = $"Reward: ${b.FinalReward} | Penalties: -${b.TotalPenalty}{driftBonusText}";
            }

            string[] lines = { timeLine, stopsLine, cargoLine, collisionLine, hardBrakeLine, driftLine, breakdownLine, reasonLine, penaltyLine };
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
    }
}
