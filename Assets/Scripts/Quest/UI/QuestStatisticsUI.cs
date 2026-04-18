using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using DeliveryDriver.UI;

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
        [SerializeField] private bool showCharts = true;

        [Header("Charts")]
        [SerializeField] private SimpleBarChart cargoChart;
        [SerializeField] private SimpleBarChart dailyChart;
        [SerializeField] private int maxCargoEntries = 5;
        [SerializeField] private int maxDailyEntries = 7;

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
                statsText.text = LocalizationTable.Get("quest_stats_unavailable");
                return;
            }

            float successRate = manager.GetSuccessRatePercentage();
            float averageTime = manager.GetAverageDeliveryTimeSeconds();
            float fastestTime = manager.GetFastestDeliveryTimeSeconds();

            statsText.text = string.Join("\n", new[]
            {
                LocalizationTable.Format("stats_total_deliveries", manager.TotalQuestsCompleted),
                LocalizationTable.Format("stats_success_rate", successRate),
                LocalizationTable.Format("stats_total_money_earned", manager.TotalMoneyEarned),
                LocalizationTable.Format("stats_distance_traveled", manager.GetFormattedDistanceTraveled()),
                LocalizationTable.Format("stats_average_delivery_time", manager.FormatDuration(averageTime)),
                LocalizationTable.Format("stats_fastest_delivery", manager.FormatDuration(fastestTime)),
                LocalizationTable.Format("stats_perfect_deliveries", manager.SRanksAchieved),
                LocalizationTable.Format("stats_favorite_cargo", manager.GetFavoriteCargoType())
            });

            UpdateCharts(manager);
        }

        private void UpdateCharts(PlayerProgressionManager manager)
        {
            if (!showCharts || manager == null)
            {
                return;
            }

            if (cargoChart != null)
            {
                List<SimpleBarChart.ChartPoint> cargoPoints = manager.CargoTypeStats
                    .OrderByDescending(stat => stat.Count)
                    .Take(maxCargoEntries)
                    .Select(stat => new SimpleBarChart.ChartPoint(ShortenLabel(stat.CargoName, 10), stat.Count))
                    .ToList();

                cargoChart.Render(cargoPoints);
            }

            if (dailyChart != null)
            {
                List<PlayerProgressionManager.DailyStat> ordered = manager.DailyStats
                    .OrderBy(stat => stat.Date)
                    .ToList();

                int skip = Mathf.Max(0, ordered.Count - maxDailyEntries);
                List<SimpleBarChart.ChartPoint> dailyPoints = ordered
                    .Skip(skip)
                    .Select(stat => new SimpleBarChart.ChartPoint(FormatDailyLabel(stat.Date), stat.QuestsCompleted))
                    .ToList();

                dailyChart.Render(dailyPoints);
            }
        }

        private static string ShortenLabel(string label, int maxLength)
        {
            if (string.IsNullOrEmpty(label) || label.Length <= maxLength)
            {
                return label;
            }

            if (maxLength <= 3)
            {
                return label.Substring(0, maxLength);
            }

            return label.Substring(0, maxLength - 3) + "...";
        }

        private static string FormatDailyLabel(string dateText)
        {
            if (DateTime.TryParse(dateText, out DateTime parsed))
            {
                return parsed.ToString("MM/dd");
            }

            if (string.IsNullOrEmpty(dateText) || dateText.Length <= 5)
            {
                return dateText;
            }

            return dateText.Substring(dateText.Length - 5);
        }
    }
}
