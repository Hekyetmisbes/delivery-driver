#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace DeliveryDriver.Quest
{
    /// <summary>
    /// Lightweight balance/testing helper for quest generation in development builds.
    /// </summary>
    public class QuestBalanceTester : MonoBehaviour
    {
        [SerializeField] private int samplesPerDifficulty = 10;
        [SerializeField] private bool includeSpecialQuestTypes = true;

        [ContextMenu("Run Quest Balance Report")]
        private void RunQuestBalanceReport()
        {
            QuestManager manager = QuestManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[QuestBalanceTester] QuestManager not found.");
                return;
            }

            RunDifficultyReport(manager, QuestDifficulty.Easy);
            RunDifficultyReport(manager, QuestDifficulty.Medium);
            RunDifficultyReport(manager, QuestDifficulty.Hard);
            RunDifficultyReport(manager, QuestDifficulty.Expert);

            if (includeSpecialQuestTypes)
            {
                RunSpecialQuestReport(manager);
            }
        }

        [ContextMenu("Run Quest Edge Case Checks")]
        private void RunQuestEdgeCaseChecks()
        {
            QuestManager manager = QuestManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[QuestBalanceTester] QuestManager not found.");
                return;
            }

            QuestData quest = manager.CurrentQuest;
            if (quest == null)
            {
                Debug.LogWarning("[QuestBalanceTester] No active quest to validate.");
                return;
            }

            ValidateLocation("Pickup", quest.PickupLocation);

            if (quest.DeliveryLocations != null)
            {
                for (int i = 0; i < quest.DeliveryLocations.Count; i++)
                {
                    ValidateLocation($"Delivery {i + 1}", quest.DeliveryLocations[i]);
                }
            }

            Debug.Log($"[QuestBalanceTester] Edge case check complete for '{quest.QuestName}'. Status: {quest.Status}");
        }

        private void RunDifficultyReport(QuestManager manager, QuestDifficulty difficulty)
        {
            float totalDistance = 0f;
            float totalTime = 0f;
            int totalReward = 0;
            int totalBonus = 0;
            int samples = 0;

            for (int i = 0; i < samplesPerDifficulty; i++)
            {
                QuestData quest = manager.GenerateQuestByDifficulty(difficulty);
                if (quest == null)
                {
                    continue;
                }

                totalDistance += quest.GetOptimalRouteDistance();
                totalTime += quest.TimeLimit;
                totalReward += quest.BaseReward;
                totalBonus += quest.BonusReward;
                samples++;
            }

            if (samples == 0)
            {
                Debug.LogWarning($"[QuestBalanceTester] No quests generated for {difficulty}.");
                return;
            }

            float avgDistance = totalDistance / samples;
            float avgTime = totalTime / samples;
            int avgReward = Mathf.RoundToInt((float)totalReward / samples);
            int avgBonus = Mathf.RoundToInt((float)totalBonus / samples);

            Debug.Log($"[QuestBalanceTester] {difficulty}: avg distance {avgDistance:F0}m, avg time {avgTime:F0}s, avg reward ${avgReward}, avg bonus ${avgBonus}.");
        }

        private void RunSpecialQuestReport(QuestManager manager)
        {
            QuestData express = manager.GenerateExpressDelivery();
            QuestData fragile = manager.GenerateFragileDelivery();
            QuestData timeTrial = manager.GenerateTimeTrial();

            LogSpecialQuest(express, "Express");
            LogSpecialQuest(fragile, "Fragile");
            LogSpecialQuest(timeTrial, "Time Trial");
        }

        private void LogSpecialQuest(QuestData quest, string label)
        {
            if (quest == null)
            {
                Debug.LogWarning($"[QuestBalanceTester] {label} quest generation failed.");
                return;
            }

            Debug.Log($"[QuestBalanceTester] {label}: {quest.Difficulty}, {quest.TimeLimit:F0}s, reward ${quest.BaseReward}, bonus ${quest.BonusReward}.");
        }

        private void ValidateLocation(string label, QuestLocation location)
        {
            if (location == null)
            {
                Debug.LogWarning($"[QuestBalanceTester] Missing {label} location.");
                return;
            }

            if (!Physics.Raycast(location.Position + Vector3.up * 50f, Vector3.down, out _, 100f))
            {
                Debug.LogWarning($"[QuestBalanceTester] {label} location has no ground hit: {location.LocationName}");
                return;
            }

            Debug.Log($"[QuestBalanceTester] {label} location OK: {location.LocationName}");
        }
    }
}
#endif
