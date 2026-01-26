using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryDriver.Quest.UI
{
    public class QuestEntryUI : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] private TextMeshProUGUI questNameText;
        [SerializeField] private TextMeshProUGUI distanceText;
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private TextMeshProUGUI rewardText;

        [Header("Visuals")]
        [SerializeField] private Image difficultyBar;
        [SerializeField] private Image typeIcon;

        [Header("Actions")]
        [SerializeField] private Button acceptButton;

        [Header("Type Icons")]
        [SerializeField] private Sprite standardIcon;
        [SerializeField] private Sprite expressIcon;
        [SerializeField] private Sprite fragileIcon;
        [SerializeField] private Sprite multiStopIcon;
        [SerializeField] private Sprite timeTrialIcon;

        private QuestData questData;

        public void Initialize(QuestData quest)
        {
            questData = quest;

            if (questData == null)
            {
                return;
            }

            if (questNameText != null)
            {
                questNameText.text = questData.QuestName;
            }

            if (distanceText != null)
            {
                distanceText.text = FormatDistance(GetQuestDistance(questData));
            }

            if (timeText != null)
            {
                timeText.text = FormatTime(questData.TimeLimit);
            }

            if (rewardText != null)
            {
                rewardText.text = $"${questData.BaseReward}";
            }

            if (difficultyBar != null)
            {
                difficultyBar.color = GetDifficultyColor(questData.Difficulty);
            }

            if (typeIcon != null)
            {
                typeIcon.sprite = GetTypeIcon(questData.QuestType);
                typeIcon.enabled = typeIcon.sprite != null;
            }

            if (acceptButton != null)
            {
                acceptButton.onClick.RemoveAllListeners();
                acceptButton.onClick.AddListener(HandleAcceptClicked);
            }
        }

        private void HandleAcceptClicked()
        {
            if (questData == null)
            {
                return;
            }

            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.AcceptQuest(questData.QuestID);
            }
        }

        private float GetQuestDistance(QuestData quest)
        {
            if (quest == null)
            {
                return 0f;
            }

            QuestLocation pickup = quest.PickupLocation;
            QuestLocation delivery = null;

            if (quest.DeliveryLocations != null && quest.DeliveryLocations.Count > 0)
            {
                delivery = quest.DeliveryLocations[0];
            }

            if (pickup == null || delivery == null)
            {
                return 0f;
            }

            return Vector3.Distance(pickup.Position, delivery.Position);
        }

        private static string FormatDistance(float distanceMeters)
        {
            if (distanceMeters <= 0f)
            {
                return "--";
            }

            if (distanceMeters >= 1000f)
            {
                float km = distanceMeters / 1000f;
                return $"{km:0.0} km";
            }

            return $"{distanceMeters:0} m";
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

        private static Color GetDifficultyColor(QuestDifficulty difficulty)
        {
            return difficulty switch
            {
                QuestDifficulty.Easy => new Color32(0x4C, 0xAF, 0x50, 0xFF),
                QuestDifficulty.Medium => new Color32(0xFF, 0xC1, 0x07, 0xFF),
                QuestDifficulty.Hard => new Color32(0xFF, 0x98, 0x00, 0xFF),
                QuestDifficulty.Expert => new Color32(0xF4, 0x43, 0x36, 0xFF),
                _ => Color.white
            };
        }

        private Sprite GetTypeIcon(QuestType questType)
        {
            return questType switch
            {
                QuestType.StandardDelivery => standardIcon,
                QuestType.ExpressDelivery => expressIcon,
                QuestType.FragileDelivery => fragileIcon,
                QuestType.MultiStopDelivery => multiStopIcon,
                QuestType.TimeTrial => timeTrialIcon,
                _ => null
            };
        }
    }
}
