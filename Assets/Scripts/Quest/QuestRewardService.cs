using System;
using UnityEngine;

namespace DeliveryDriver.Quest
{
    internal readonly struct QuestRewardConfig
    {
        public QuestRewardConfig(
            QuestSystemSettings questSystemSettings,
            float payoutAverageSpeedMetersPerSecond,
            float minTimePressureMultiplier,
            float maxTimePressureMultiplier,
            float fragileCargoDifficultyBonus,
            float heavyCargoWeightStartKg,
            float heavyCargoWeightMaxBonus,
            float mediumRiskMultiplier,
            float hardRiskMultiplier,
            float expertRiskMultiplier,
            float expressRiskBonus,
            float fragileRiskBonus,
            float multiStopRiskBonus,
            int collisionPenaltyBase,
            int collisionPenaltyStep,
            int npcCollisionPenaltyBase,
            int npcCollisionPenaltyStep,
            int hardBrakePenaltyBase,
            int hardBrakePenaltyStep,
            float delayPenaltyStartsAtRemainingRatio,
            float delayPenaltyMaxRatio,
            float cargoDamagePenaltyPerPercent,
            int timeExpiredFailurePenalty,
            int cargoDestroyedFailurePenalty,
            bool enableDriftBonus,
            int driftPointsPerRewardStep,
            int driftRewardStepAmount,
            int maxDriftBonusReward,
            float streakMultiplierIncrement,
            float maxStreakMultiplier)
        {
            QuestSystemSettings = questSystemSettings;
            PayoutAverageSpeedMetersPerSecond = payoutAverageSpeedMetersPerSecond;
            MinTimePressureMultiplier = minTimePressureMultiplier;
            MaxTimePressureMultiplier = maxTimePressureMultiplier;
            FragileCargoDifficultyBonus = fragileCargoDifficultyBonus;
            HeavyCargoWeightStartKg = heavyCargoWeightStartKg;
            HeavyCargoWeightMaxBonus = heavyCargoWeightMaxBonus;
            MediumRiskMultiplier = mediumRiskMultiplier;
            HardRiskMultiplier = hardRiskMultiplier;
            ExpertRiskMultiplier = expertRiskMultiplier;
            ExpressRiskBonus = expressRiskBonus;
            FragileRiskBonus = fragileRiskBonus;
            MultiStopRiskBonus = multiStopRiskBonus;
            CollisionPenaltyBase = collisionPenaltyBase;
            CollisionPenaltyStep = collisionPenaltyStep;
            NpcCollisionPenaltyBase = npcCollisionPenaltyBase;
            NpcCollisionPenaltyStep = npcCollisionPenaltyStep;
            HardBrakePenaltyBase = hardBrakePenaltyBase;
            HardBrakePenaltyStep = hardBrakePenaltyStep;
            DelayPenaltyStartsAtRemainingRatio = delayPenaltyStartsAtRemainingRatio;
            DelayPenaltyMaxRatio = delayPenaltyMaxRatio;
            CargoDamagePenaltyPerPercent = cargoDamagePenaltyPerPercent;
            TimeExpiredFailurePenalty = timeExpiredFailurePenalty;
            CargoDestroyedFailurePenalty = cargoDestroyedFailurePenalty;
            EnableDriftBonus = enableDriftBonus;
            DriftPointsPerRewardStep = driftPointsPerRewardStep;
            DriftRewardStepAmount = driftRewardStepAmount;
            MaxDriftBonusReward = maxDriftBonusReward;
            StreakMultiplierIncrement = streakMultiplierIncrement;
            MaxStreakMultiplier = maxStreakMultiplier;
        }

        public QuestSystemSettings QuestSystemSettings { get; }
        public float PayoutAverageSpeedMetersPerSecond { get; }
        public float MinTimePressureMultiplier { get; }
        public float MaxTimePressureMultiplier { get; }
        public float FragileCargoDifficultyBonus { get; }
        public float HeavyCargoWeightStartKg { get; }
        public float HeavyCargoWeightMaxBonus { get; }
        public float MediumRiskMultiplier { get; }
        public float HardRiskMultiplier { get; }
        public float ExpertRiskMultiplier { get; }
        public float ExpressRiskBonus { get; }
        public float FragileRiskBonus { get; }
        public float MultiStopRiskBonus { get; }
        public int CollisionPenaltyBase { get; }
        public int CollisionPenaltyStep { get; }
        public int NpcCollisionPenaltyBase { get; }
        public int NpcCollisionPenaltyStep { get; }
        public int HardBrakePenaltyBase { get; }
        public int HardBrakePenaltyStep { get; }
        public float DelayPenaltyStartsAtRemainingRatio { get; }
        public float DelayPenaltyMaxRatio { get; }
        public float CargoDamagePenaltyPerPercent { get; }
        public int TimeExpiredFailurePenalty { get; }
        public int CargoDestroyedFailurePenalty { get; }
        public bool EnableDriftBonus { get; }
        public int DriftPointsPerRewardStep { get; }
        public int DriftRewardStepAmount { get; }
        public int MaxDriftBonusReward { get; }
        public float StreakMultiplierIncrement { get; }
        public float MaxStreakMultiplier { get; }
    }

    internal static class QuestRewardService
    {
        public static QuestManager.RewardPenaltyBreakdown GetQuestRewardPreview(
            QuestData quest,
            int consecutiveSuccesses,
            QuestRewardConfig config)
        {
            return CalculateRewardPenaltyBreakdown(
                quest,
                CalculateStreakMultiplier(consecutiveSuccesses + 1, config),
                false,
                string.Empty,
                config);
        }

        public static QuestManager.RewardPenaltyBreakdown CalculateRewardPenaltyBreakdown(
            QuestData quest,
            float appliedStreakMultiplier,
            bool useRuntimePenalties,
            string lastFailureReason,
            QuestRewardConfig config)
        {
            QuestManager.RewardPenaltyBreakdown breakdown = new QuestManager.RewardPenaltyBreakdown
            {
                TimePressureMultiplier = 1f,
                CargoDifficultyMultiplier = 1f,
                NeighborhoodRiskMultiplier = 1f,
                StreakMultiplier = Mathf.Max(1f, appliedStreakMultiplier)
            };

            if (quest == null)
            {
                return breakdown;
            }

            float routeDistance = GetQuestRouteDistance(quest);
            float rewardPerMeter = config.QuestSystemSettings != null ? config.QuestSystemSettings.BaseRewardPerMeter : 0.1f;
            breakdown.DistanceReward = Mathf.RoundToInt(routeDistance * rewardPerMeter);

            breakdown.TimePressureMultiplier = CalculateTimePressureMultiplier(routeDistance, quest.TimeLimit, config);
            breakdown.CargoDifficultyMultiplier = CalculateCargoDifficultyMultiplier(quest.Cargo, config);
            breakdown.NeighborhoodRiskMultiplier = CalculateNeighborhoodRiskMultiplier(quest, config);

            int staticDifficultyBonus = config.QuestSystemSettings != null ? config.QuestSystemSettings.GetDifficultyBonus(quest.Difficulty) : 0;
            float grossFloat = (breakdown.DistanceReward + staticDifficultyBonus) *
                               breakdown.TimePressureMultiplier *
                               breakdown.CargoDifficultyMultiplier *
                               breakdown.NeighborhoodRiskMultiplier;
            breakdown.GrossReward = Mathf.Max(0, Mathf.RoundToInt(grossFloat));

            breakdown.SpeedBonus = CalculateSpeedBonus(quest, useRuntimePenalties);
            breakdown.DriftBonus = CalculateDriftBonus(quest, useRuntimePenalties, config);
            breakdown.CollisionPenalty = CalculateCollisionPenalty(quest, useRuntimePenalties, config);
            breakdown.HardBrakePenalty = CalculateHardBrakePenalty(quest, useRuntimePenalties, config);
            breakdown.DelayPenalty = CalculateDelayPenalty(quest, breakdown.GrossReward, useRuntimePenalties, config);
            breakdown.CargoDamagePenalty = CalculateCargoDamagePenalty(quest, useRuntimePenalties, config);
            breakdown.TotalPenalty = breakdown.CollisionPenalty + breakdown.HardBrakePenalty + breakdown.DelayPenalty + breakdown.CargoDamagePenalty;

            int grossWithSpeed = breakdown.GrossReward + breakdown.SpeedBonus + breakdown.DriftBonus;
            int streakAdjusted = Mathf.RoundToInt(grossWithSpeed * breakdown.StreakMultiplier);
            breakdown.FinalReward = Mathf.Max(0, streakAdjusted - breakdown.TotalPenalty);

            breakdown.FailurePenalty = CalculateFailurePenalty(quest, lastFailureReason, config);
            return breakdown;
        }

        public static float CalculateStreakMultiplier(int successCountAfterCompletion, QuestRewardConfig config)
        {
            int bonusCount = Mathf.Max(0, successCountAfterCompletion - 1);
            return Mathf.Min(1.0f + (bonusCount * config.StreakMultiplierIncrement), config.MaxStreakMultiplier);
        }

        public static int CalculateFailurePenalty(QuestData quest, string reason, QuestRewardConfig config)
        {
            if (quest == null)
            {
                return 0;
            }

            int failurePenalty = 0;
            if (!string.IsNullOrWhiteSpace(reason))
            {
                if (reason.IndexOf("Time", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    failurePenalty += config.TimeExpiredFailurePenalty;
                }
                else if (reason.IndexOf("Cargo", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    failurePenalty += config.CargoDestroyedFailurePenalty;
                }
            }

            failurePenalty += CalculateCollisionPenalty(quest, true, config);
            failurePenalty += CalculateCargoDamagePenalty(quest, true, config);
            return Mathf.Max(0, failurePenalty);
        }

        public static void TryApplyFailurePenalty(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            if (PlayerProgressionManager.Instance != null)
            {
                PlayerProgressionManager.Instance.SpendMoney(amount);
                return;
            }

            Type progressionType = Type.GetType("PlayerProgressionManager");
            if (progressionType == null)
            {
                return;
            }

            UnityEngine.Object manager = UnityEngine.Object.FindAnyObjectByType(progressionType);
            if (manager == null)
            {
                return;
            }

            foreach (string methodName in new[] { "SpendMoney", "RemoveCurrency", "DeductMoney" })
            {
                var method = progressionType.GetMethod(methodName, new[] { typeof(int) });
                if (method != null)
                {
                    method.Invoke(manager, new object[] { amount });
                    return;
                }
            }
        }

        public static void TryAwardRewards(QuestData quest, int reward)
        {
            if (quest == null)
            {
                return;
            }

            if (PlayerProgressionManager.Instance != null)
            {
                int fuelRebate = DriverProgressionSystem.Instance != null
                    ? DriverProgressionSystem.Instance.CalculateFuelEfficiencyRebate(quest)
                    : 0;
                int totalMoneyAward = reward + Mathf.Max(0, fuelRebate);

                PlayerProgressionManager.Instance.AwardMoney(totalMoneyAward);
                PlayerProgressionManager.Instance.AwardXP(quest.XPReward);
                PlayerProgressionManager.Instance.IncrementQuestsCompleted();
                PlayerProgressionManager.Instance.AddDistanceTraveled(quest.TotalDistanceTraveled);
                return;
            }

            Type progressionType = Type.GetType("PlayerProgressionManager");
            if (progressionType == null)
            {
                Debug.Log($"[QuestManager] Reward granted: {reward} currency, {quest.XPReward} XP.");
                return;
            }

            UnityEngine.Object manager = UnityEngine.Object.FindAnyObjectByType(progressionType);
            if (manager == null)
            {
                Debug.Log($"[QuestManager] Reward granted: {reward} currency, {quest.XPReward} XP.");
                return;
            }

            bool invoked = false;
            foreach (string methodName in new[] { "AddCurrency", "AddMoney", "AddCash" })
            {
                var method = progressionType.GetMethod(methodName, new[] { typeof(int) });
                if (method != null)
                {
                    method.Invoke(manager, new object[] { reward });
                    invoked = true;
                    break;
                }
            }

            foreach (string methodName in new[] { "AddXP", "AddExperience" })
            {
                var method = progressionType.GetMethod(methodName, new[] { typeof(int) });
                if (method != null)
                {
                    method.Invoke(manager, new object[] { quest.XPReward });
                    invoked = true;
                    break;
                }
            }

            if (!invoked)
            {
                Debug.Log($"[QuestManager] Reward granted: {reward} currency, {quest.XPReward} XP.");
            }
        }

        private static float GetQuestRouteDistance(QuestData quest)
        {
            if (quest == null)
            {
                return 0f;
            }

            float routeDistance = quest.GetOptimalRouteDistance();
            if (routeDistance > 0f)
            {
                return routeDistance;
            }

            if (quest.PickupLocation == null || quest.DeliveryLocations == null || quest.DeliveryLocations.Count == 0)
            {
                return 0f;
            }

            float totalDistance = 0f;
            Vector3 start = quest.PickupLocation.Position;
            for (int i = 0; i < quest.DeliveryLocations.Count; i++)
            {
                QuestLocation stop = quest.DeliveryLocations[i];
                if (stop == null)
                {
                    continue;
                }

                totalDistance += Vector3.Distance(start, stop.Position);
                start = stop.Position;
            }

            return totalDistance;
        }

        private static float CalculateTimePressureMultiplier(float routeDistance, float timeLimit, QuestRewardConfig config)
        {
            if (routeDistance <= 0f || timeLimit <= 0f)
            {
                return 1f;
            }

            float baselineTime = routeDistance / Mathf.Max(1f, config.PayoutAverageSpeedMetersPerSecond);
            float pressureRatio = baselineTime / Mathf.Max(1f, timeLimit);
            float normalized = Mathf.InverseLerp(0.45f, 1.15f, pressureRatio);
            return Mathf.Lerp(config.MinTimePressureMultiplier, config.MaxTimePressureMultiplier, Mathf.Clamp01(normalized));
        }

        private static float CalculateCargoDifficultyMultiplier(CargoData cargo, QuestRewardConfig config)
        {
            if (cargo == null)
            {
                return 1f;
            }

            float multiplier = 1f;
            if (cargo.IsFragile)
            {
                multiplier += config.FragileCargoDifficultyBonus;
            }

            if (cargo.Weight > config.HeavyCargoWeightStartKg)
            {
                float extraWeight = cargo.Weight - config.HeavyCargoWeightStartKg;
                float weightFactor = Mathf.Clamp01(extraWeight / Mathf.Max(1f, config.HeavyCargoWeightStartKg));
                multiplier += config.HeavyCargoWeightMaxBonus * weightFactor;
            }

            return Mathf.Clamp(multiplier, 1f, 1.8f);
        }

        private static float CalculateNeighborhoodRiskMultiplier(QuestData quest, QuestRewardConfig config)
        {
            if (quest == null)
            {
                return 1f;
            }

            float multiplier = quest.Difficulty switch
            {
                QuestDifficulty.Medium => config.MediumRiskMultiplier,
                QuestDifficulty.Hard => config.HardRiskMultiplier,
                QuestDifficulty.Expert => config.ExpertRiskMultiplier,
                _ => 1f
            };

            multiplier += quest.QuestType switch
            {
                QuestType.ExpressDelivery => config.ExpressRiskBonus,
                QuestType.FragileDelivery => config.FragileRiskBonus,
                QuestType.MultiStopDelivery => config.MultiStopRiskBonus,
                _ => 0f
            };

            return Mathf.Clamp(multiplier, 1f, 2f);
        }

        private static int CalculateSpeedBonus(QuestData quest, bool useRuntimeState)
        {
            if (quest == null)
            {
                return 0;
            }

            float completionPercent = 0.55f;
            if (useRuntimeState && quest.TimeLimit > 0f)
            {
                completionPercent = Mathf.Clamp01(quest.TimeRemaining / quest.TimeLimit);
            }

            if (completionPercent < quest.BonusTimeThreshold)
            {
                return 0;
            }

            float bonusMultiplier = completionPercent >= 0.75f ? 1.5f : 1.0f;
            int configuredBonus = quest.BonusReward > 0 ? quest.BonusReward : Mathf.RoundToInt(quest.BaseReward * 0.5f);
            return Mathf.RoundToInt(configuredBonus * bonusMultiplier);
        }

        public static int CalculateDriftBonus(QuestData quest, bool useRuntimeState, QuestRewardConfig config)
        {
            if (!config.EnableDriftBonus || quest == null || !useRuntimeState)
            {
                return 0;
            }

            int driftPoints = Mathf.Max(0, quest.DriftScorePoints);
            if (driftPoints <= 0)
            {
                return 0;
            }

            int stepPoints = Mathf.Max(1, config.DriftPointsPerRewardStep);
            int steps = driftPoints / stepPoints;
            int bonus = steps * Mathf.Max(0, config.DriftRewardStepAmount);
            return Mathf.Clamp(bonus, 0, Mathf.Max(0, config.MaxDriftBonusReward));
        }

        public static int CalculateCollisionPenalty(QuestData quest, bool useRuntimeState, QuestRewardConfig config)
        {
            int totalCollisions = 0;
            int npcCollisions = 0;

            if (quest != null && useRuntimeState)
            {
                totalCollisions = Mathf.Max(0, quest.CollisionCount);
                npcCollisions = Mathf.Clamp(quest.NpcCollisionCount, 0, totalCollisions);
            }

            int nonNpcCollisions = Mathf.Max(0, totalCollisions - npcCollisions);
            int penalty = SumProgressivePenalty(nonNpcCollisions, config.CollisionPenaltyBase, config.CollisionPenaltyStep);
            penalty += SumProgressivePenalty(npcCollisions, config.NpcCollisionPenaltyBase, config.NpcCollisionPenaltyStep);
            return penalty;
        }

        public static int CalculateHardBrakePenalty(QuestData quest, bool useRuntimeState, QuestRewardConfig config)
        {
            int hardBrakes = 0;
            if (quest != null && useRuntimeState)
            {
                hardBrakes = Mathf.Max(0, quest.HardBrakeCount);
            }

            int penalty = SumProgressivePenalty(hardBrakes, config.HardBrakePenaltyBase, config.HardBrakePenaltyStep);
            if (DriverProgressionSystem.Instance != null)
            {
                penalty = Mathf.RoundToInt(penalty * DriverProgressionSystem.Instance.GetRoutePenaltyMultiplier());
            }

            return penalty;
        }

        private static int CalculateDelayPenalty(QuestData quest, int grossReward, bool useRuntimeState, QuestRewardConfig config)
        {
            if (quest == null)
            {
                return 0;
            }

            int maxDelayPenalty = Mathf.RoundToInt(grossReward * Mathf.Clamp01(config.DelayPenaltyMaxRatio));
            if (!useRuntimeState)
            {
                return maxDelayPenalty;
            }

            if (quest.TimeLimit <= 0f)
            {
                return 0;
            }

            float remainingRatio = Mathf.Clamp01(quest.TimeRemaining / quest.TimeLimit);
            if (remainingRatio >= config.DelayPenaltyStartsAtRemainingRatio)
            {
                return 0;
            }

            float missingRatio = (config.DelayPenaltyStartsAtRemainingRatio - remainingRatio) / Mathf.Max(0.01f, config.DelayPenaltyStartsAtRemainingRatio);
            int penalty = Mathf.RoundToInt(maxDelayPenalty * Mathf.Clamp01(missingRatio));
            if (DriverProgressionSystem.Instance != null)
            {
                penalty = Mathf.RoundToInt(penalty * DriverProgressionSystem.Instance.GetRoutePenaltyMultiplier());
            }

            return penalty;
        }

        private static int CalculateCargoDamagePenalty(QuestData quest, bool useRuntimeState, QuestRewardConfig config)
        {
            if (quest?.Cargo == null || !quest.Cargo.IsFragile)
            {
                return 0;
            }

            if (!useRuntimeState)
            {
                return Mathf.RoundToInt(100f * config.CargoDamagePenaltyPerPercent);
            }

            float damagedPercent = Mathf.Clamp(100f - quest.Cargo.CargoHealth, 0f, 100f);
            return Mathf.RoundToInt(damagedPercent * config.CargoDamagePenaltyPerPercent);
        }

        private static int SumProgressivePenalty(int count, int basePenalty, int stepPenalty)
        {
            int total = 0;
            for (int i = 0; i < count; i++)
            {
                total += basePenalty + (i * stepPenalty);
            }

            return total;
        }
    }
}
