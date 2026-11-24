using UnityEngine;

namespace DeliveryDriver.Quest
{
    /// <summary>Shared generation formulas; retains each quest type's original rounding and defaults.</summary>
    public static class QuestGenerationRules
    {
        public static Vector2 StandardDistanceRange(QuestDifficulty difficulty) => difficulty switch
        {
            QuestDifficulty.Medium => new Vector2(2000f, 4000f),
            QuestDifficulty.Hard => new Vector2(4000f, 6000f),
            QuestDifficulty.Expert => new Vector2(6000f, 10000f),
            _ => new Vector2(1000f, 2000f)
        };

        public static void Apply(QuestData quest, float distance, QuestSystemSettings settings)
        {
            QuestDifficulty difficulty = quest.Difficulty;
            QuestType type = quest.QuestType;
            bool timeTrial = type == QuestType.TimeTrial;
            float timeMultiplier = settings != null ? settings.GetTimeMultiplier(difficulty) :
                difficulty == QuestDifficulty.Easy ? 2f : difficulty == QuestDifficulty.Medium ? 1.5f :
                difficulty == QuestDifficulty.Hard ? 1.2f : 1f;
            int difficultyBonus = settings != null ? settings.GetDifficultyBonus(difficulty) :
                difficulty == QuestDifficulty.Medium ? 100 : difficulty == QuestDifficulty.Hard ? 250 :
                difficulty == QuestDifficulty.Expert ? 500 : 0;
            float minimumTime = settings != null ? settings.MinimumTimeLimit : 60f;
            float rewardPerMeter = settings != null ? settings.BaseRewardPerMeter : timeTrial ? 0.15f : 0.1f;
            float bonusMultiplier = settings != null ? settings.BonusRewardMultiplier : timeTrial ? 1f : 0.5f;
            float rawReward = distance * rewardPerMeter + difficultyBonus;
            float time = distance / 11f * timeMultiplier;
            int reward = Mathf.RoundToInt(rawReward);
            int xp = 50 * ((int)difficulty + 1);

            switch (type)
            {
                case QuestType.MultiStopDelivery:
                    int count = quest.DeliveryLocations.Count;
                    minimumTime = settings != null ? settings.MinimumMultiStopTimeLimit : 120f;
                    time = time * count * (settings != null ? settings.MultiStopTimeScale : 1.5f);
                    reward = Mathf.RoundToInt(reward * count * (settings != null ? settings.MultiStopRewardScale : 1.8f));
                    xp *= count;
                    break;
                case QuestType.ExpressDelivery:
                    time *= settings != null ? settings.ExpressTimeScale : 0.6f;
                    reward = Mathf.RoundToInt(reward * (settings != null ? settings.ExpressRewardMultiplier : 2f));
                    break;
                case QuestType.FragileDelivery:
                    minimumTime = Mathf.Max(90f, minimumTime);
                    timeMultiplier = difficulty == QuestDifficulty.Easy
                        ? (settings != null ? settings.FragileEasyTimeMultiplier : 2.5f)
                        : (settings != null ? settings.FragileMediumTimeMultiplier : 1.8f);
                    time = distance / 11f * timeMultiplier;
                    break;
                case QuestType.TimeTrial:
                    time *= settings != null ? settings.TimeTrialTimeScale : 0.5f;
                    // Unlike express/multi-stop, time trial scales BEFORE rounding.
                    reward = Mathf.RoundToInt(rawReward * (settings != null ? settings.TimeTrialRewardMultiplier : 1f));
                    break;
            }

            quest.TimeRemaining = quest.TimeLimit = Mathf.Max(minimumTime, time);
            quest.BaseReward = reward;
            quest.BonusReward = Mathf.RoundToInt(reward * bonusMultiplier);
            quest.BonusTimeThreshold = 0.5f;
            quest.RequiredLevel = difficulty == QuestDifficulty.Easy ? 1 :
                difficulty == QuestDifficulty.Medium ? 5 : difficulty == QuestDifficulty.Hard ? 15 : 30;
            quest.IsRepeatable = true;
            quest.XPReward = xp;
        }
    }
}
