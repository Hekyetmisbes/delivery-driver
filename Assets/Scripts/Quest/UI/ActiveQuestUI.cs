using System.Text;
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
        private string lastObjectiveText = string.Empty;
        private int lastTimerSeconds = -1;
        private Color lastTimerColor = Color.clear;
        private string lastDistanceText = string.Empty;
        private float lastCargoHealth = -1f;
        private bool lastCargoPanelActive = false;
        private readonly StringBuilder objectiveBuilder = new StringBuilder(64);

        private void Awake()
        {
            AutoBindTextReferences();
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
            if (objectiveText != null && !string.Equals(lastObjectiveText, objective, System.StringComparison.Ordinal))
            {
                lastObjectiveText = objective;
                objectiveText.text = objective;
            }

            UpdateTimer(currentQuest.TimeRemaining, currentQuest.TimeLimit);

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
            if (timerText == null)
            {
                return;
            }

            int seconds = Mathf.CeilToInt(timeRemaining);
            if (seconds != lastTimerSeconds)
            {
                timerText.text = FormatTime(seconds);
                lastTimerSeconds = seconds;
            }

            Color color = GetTimerColor(timeRemaining, timeLimit);
            if (color != lastTimerColor)
            {
                timerText.color = color;
                lastTimerColor = color;
            }
        }

        public void UpdateDistance(float distanceMeters)
        {
            if (distanceText == null)
            {
                AutoBindTextReferences();
            }

            if (distanceText == null)
            {
                return;
            }

            string formatted = FormatDistance(distanceMeters);
            if (!string.Equals(lastDistanceText, formatted, System.StringComparison.Ordinal))
            {
                lastDistanceText = formatted;
                distanceText.text = formatted;
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

            if (cargoHealthPanel != null)
            {
                cargoHealthPanel.SetActive(false);
            }

            lastObjectiveText = string.Empty;
            lastTimerSeconds = -1;
            lastTimerColor = Color.clear;
            lastDistanceText = string.Empty;
            lastCargoHealth = -1f;
            lastCargoPanelActive = false;
        }

        private string BuildObjectiveText(QuestData quest)
        {
            if (quest == null)
            {
                return string.Empty;
            }

            if (!quest.HasPickedUpCargo)
            {
                string pickupName = quest.PickupLocation != null ? quest.PickupLocation.LocationName : "pickup";
                objectiveBuilder.Clear();
                objectiveBuilder.Append("Go to ").Append(pickupName).Append('\n').Append("Pick up cargo");
                return objectiveBuilder.ToString();
            }

            QuestLocation delivery = GetCurrentDeliveryLocation(quest);
            string deliveryName = delivery != null ? delivery.LocationName : "delivery";
            int totalStops = quest.DeliveryLocations != null ? quest.DeliveryLocations.Count : 0;
            int currentStop = Mathf.Clamp(quest.CurrentDeliveryIndex + 1, 1, Mathf.Max(1, totalStops));

            if (totalStops > 1)
            {
                objectiveBuilder.Clear();
                objectiveBuilder.Append("Deliver cargo (")
                    .Append(currentStop)
                    .Append('/')
                    .Append(totalStops)
                    .Append(")\n")
                    .Append(deliveryName);
                return objectiveBuilder.ToString();
            }

            objectiveBuilder.Clear();
            objectiveBuilder.Append("Deliver to ").Append(deliveryName);
            return objectiveBuilder.ToString();
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

        private static string FormatTime(int timeSeconds)
        {
            int minutes = Mathf.Max(0, Mathf.FloorToInt(timeSeconds / 60f));
            int seconds = Mathf.Max(0, timeSeconds % 60);
            return $"{minutes:00}:{seconds:00}";
        }

        private static string FormatDistance(float distanceMeters)
        {
            if (distanceMeters <= 0f)
            {
                return "Mesafe: -- m";
            }

            return $"Mesafe: {Mathf.RoundToInt(distanceMeters)} m";
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
    }
}
