using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryDriver.Quest.UI
{
    public class ActiveQuestUI : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] private TextMeshProUGUI objectiveText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI distanceText;

        [Header("Cargo Health")]
        [SerializeField] private Image cargoHealthFill;
        [SerializeField] private GameObject cargoHealthPanel;

        private QuestData currentQuest;

        public void UpdateQuestDisplay(QuestData quest)
        {
            currentQuest = quest;

            if (currentQuest == null)
            {
                ClearDisplay();
                return;
            }

            if (objectiveText != null)
            {
                objectiveText.text = BuildObjectiveText(currentQuest);
            }

            UpdateTimer(currentQuest.TimeRemaining, currentQuest.TimeLimit);

            if (currentQuest.Cargo != null && currentQuest.Cargo.IsFragile)
            {
                UpdateCargoHealth(currentQuest.Cargo.CargoHealth);
                if (cargoHealthPanel != null)
                {
                    cargoHealthPanel.SetActive(true);
                }
            }
            else
            {
                if (cargoHealthPanel != null)
                {
                    cargoHealthPanel.SetActive(false);
                }
            }
        }

        public void UpdateTimer(float timeRemaining, float timeLimit)
        {
            if (timerText == null)
            {
                return;
            }

            timerText.text = FormatTime(timeRemaining);
            timerText.color = GetTimerColor(timeRemaining, timeLimit);
        }

        public void UpdateDistance(float distanceMeters)
        {
            if (distanceText == null)
            {
                return;
            }

            distanceText.text = FormatDistance(distanceMeters);
        }

        public void UpdateCargoHealth(float health)
        {
            if (cargoHealthFill == null)
            {
                return;
            }

            float normalized = Mathf.Clamp01(health / 100f);
            cargoHealthFill.fillAmount = normalized;
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

            if (cargoHealthPanel != null)
            {
                cargoHealthPanel.SetActive(false);
            }
        }

        private static string BuildObjectiveText(QuestData quest)
        {
            if (quest == null)
            {
                return string.Empty;
            }

            if (!quest.HasPickedUpCargo)
            {
                string pickupName = quest.PickupLocation != null ? quest.PickupLocation.LocationName : "pickup";
                return $"Go to {pickupName}\nPick up cargo";
            }

            QuestLocation delivery = GetCurrentDeliveryLocation(quest);
            string deliveryName = delivery != null ? delivery.LocationName : "delivery";
            int totalStops = quest.DeliveryLocations != null ? quest.DeliveryLocations.Count : 0;
            int currentStop = Mathf.Clamp(quest.CurrentDeliveryIndex + 1, 1, Mathf.Max(1, totalStops));

            if (totalStops > 1)
            {
                return $"Deliver cargo ({currentStop}/{totalStops})\n{deliveryName}";
            }

            return $"Deliver to {deliveryName}";
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

        private static string FormatTime(float timeSeconds)
        {
            int minutes = Mathf.Max(0, Mathf.FloorToInt(timeSeconds / 60f));
            int seconds = Mathf.Max(0, Mathf.FloorToInt(timeSeconds % 60f));
            return $"{minutes:00}:{seconds:00}";
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
    }
}
