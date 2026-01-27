using UnityEngine;

namespace DeliveryDriver.Quest
{
    /// <summary>
    /// Centralized tuning configuration for quest generation and rewards.
    /// </summary>
    [CreateAssetMenu(fileName = "QuestSystemSettings", menuName = "Quest System/Quest System Settings", order = 2)]
    public class QuestSystemSettings : ScriptableObject
    {
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogging = true;

        [Header("Standard Delivery Distances (m)")]
        [SerializeField] private Vector2 easyDistanceRange = new Vector2(1000f, 2000f);
        [SerializeField] private Vector2 mediumDistanceRange = new Vector2(2000f, 4000f);
        [SerializeField] private Vector2 hardDistanceRange = new Vector2(4000f, 6000f);
        [SerializeField] private Vector2 expertDistanceRange = new Vector2(6000f, 10000f);

        [Header("Time Multipliers")]
        [SerializeField] private float easyTimeMultiplier = 2.0f;
        [SerializeField] private float mediumTimeMultiplier = 1.5f;
        [SerializeField] private float hardTimeMultiplier = 1.2f;
        [SerializeField] private float expertTimeMultiplier = 1.0f;
        [SerializeField] private float minimumTimeLimit = 60f;

        [Header("Reward Scaling")]
        [SerializeField] private float baseRewardPerMeter = 0.1f;
        [SerializeField] private float bonusRewardMultiplier = 0.5f;
        [SerializeField] private int mediumDifficultyBonus = 100;
        [SerializeField] private int hardDifficultyBonus = 250;
        [SerializeField] private int expertDifficultyBonus = 500;

        [Header("Express Delivery")]
        [SerializeField] private float expressTimeScale = 0.6f;
        [SerializeField] private float expressRewardMultiplier = 2.0f;

        [Header("Fragile Delivery")]
        [SerializeField] private float fragileEasyTimeMultiplier = 2.5f;
        [SerializeField] private float fragileMediumTimeMultiplier = 1.8f;

        [Header("Time Trial")]
        [SerializeField] private float timeTrialTimeScale = 0.5f;
        [SerializeField] private float timeTrialRewardMultiplier = 1.5f;

        [Header("Multi-Stop")]
        [SerializeField] private float multiStopTimeScale = 1.5f;
        [SerializeField] private float multiStopRewardScale = 1.8f;
        [SerializeField] private float minimumMultiStopTimeLimit = 120f;

        public bool EnableDebugLogging => enableDebugLogging;
        public float MinimumTimeLimit => minimumTimeLimit;
        public float MinimumMultiStopTimeLimit => minimumMultiStopTimeLimit;
        public float BaseRewardPerMeter => baseRewardPerMeter;
        public float BonusRewardMultiplier => bonusRewardMultiplier;
        public float ExpressTimeScale => expressTimeScale;
        public float ExpressRewardMultiplier => expressRewardMultiplier;
        public float FragileEasyTimeMultiplier => fragileEasyTimeMultiplier;
        public float FragileMediumTimeMultiplier => fragileMediumTimeMultiplier;
        public float TimeTrialTimeScale => timeTrialTimeScale;
        public float TimeTrialRewardMultiplier => timeTrialRewardMultiplier;
        public float MultiStopTimeScale => multiStopTimeScale;
        public float MultiStopRewardScale => multiStopRewardScale;

        public Vector2 GetDistanceRange(QuestDifficulty difficulty)
        {
            return difficulty switch
            {
                QuestDifficulty.Medium => mediumDistanceRange,
                QuestDifficulty.Hard => hardDistanceRange,
                QuestDifficulty.Expert => expertDistanceRange,
                _ => easyDistanceRange
            };
        }

        public float GetTimeMultiplier(QuestDifficulty difficulty)
        {
            return difficulty switch
            {
                QuestDifficulty.Medium => mediumTimeMultiplier,
                QuestDifficulty.Hard => hardTimeMultiplier,
                QuestDifficulty.Expert => expertTimeMultiplier,
                _ => easyTimeMultiplier
            };
        }

        public int GetDifficultyBonus(QuestDifficulty difficulty)
        {
            return difficulty switch
            {
                QuestDifficulty.Medium => mediumDifficultyBonus,
                QuestDifficulty.Hard => hardDifficultyBonus,
                QuestDifficulty.Expert => expertDifficultyBonus,
                _ => 0
            };
        }
    }
}
