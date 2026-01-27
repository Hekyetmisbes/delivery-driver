using TMPro;
using UnityEngine;

namespace DeliveryDriver.Quest.UI
{
    /// <summary>
    /// Displays quest statistics in a UI panel.
    /// </summary>
    public class QuestStatisticsUI : MonoBehaviour
    {
        [SerializeField] private GameObject statsPanel;
        [SerializeField] private TextMeshProUGUI statsText;
        [SerializeField] private bool refreshOnEnable = true;

        private void OnEnable()
        {
            if (refreshOnEnable)
            {
                RefreshStats();
            }
        }

        /// <summary>
        /// Shows or hides the statistics panel.
        /// </summary>
        /// <param name="visible">True to show, false to hide.</param>
        public void SetVisible(bool visible)
        {
            if (statsPanel != null)
            {
                statsPanel.SetActive(visible);
            }
            else
            {
                gameObject.SetActive(visible);
            }

            if (visible)
            {
                RefreshStats();
            }
        }

        /// <summary>
        /// Refreshes the displayed statistics text.
        /// </summary>
        public void RefreshStats()
        {
            if (statsText == null)
            {
                return;
            }

            PlayerProgressionManager manager = PlayerProgressionManager.Instance;
            if (manager == null)
            {
                statsText.text = "Statistics unavailable.";
                return;
            }

            float successRate = manager.GetSuccessRatePercentage();
            float averageTime = manager.GetAverageDeliveryTimeSeconds();
            float fastestTime = manager.GetFastestDeliveryTimeSeconds();

            statsText.text = string.Join("\n", new[]
            {
                $"Total Deliveries: {manager.TotalQuestsCompleted}",
                $"Success Rate: {successRate:F1}%",
                $"Total Money Earned: ${manager.TotalMoneyEarned}",
                $"Distance Traveled: {manager.GetFormattedDistanceTraveled()}",
                $"Average Delivery Time: {manager.FormatDuration(averageTime)}",
                $"Fastest Delivery: {manager.FormatDuration(fastestTime)}",
                $"Perfect Deliveries (S): {manager.SRanksAchieved}",
                $"Favorite Cargo: {manager.GetFavoriteCargoType()}"
            });
        }
    }
}
