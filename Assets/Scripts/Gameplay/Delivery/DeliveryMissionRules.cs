using UnityEngine;
using DeliveryDriver.Quest;
using DeliveryDriver.UI;
using TrafficSystem;

internal enum DeliveryMissionType
{
    Standard,
    Timed,
    Fragile,
    MultiStop
}

internal readonly struct DeliveryMissionConditionEvaluation
{
    public DeliveryMissionConditionEvaluation(float rewardMultiplier, bool rushHourBonus, bool nightBonus, bool rainRiskBonus)
    {
        RewardMultiplier = Mathf.Max(1f, rewardMultiplier);
        RushHourBonus = rushHourBonus;
        NightBonus = nightBonus;
        RainRiskBonus = rainRiskBonus;
    }

    public float RewardMultiplier { get; }
    public bool RushHourBonus { get; }
    public bool NightBonus { get; }
    public bool RainRiskBonus { get; }
}

internal static class DeliveryMissionRules
{
    public static DeliveryMissionType PickMissionType(int standardMissionWeight, int timedMissionWeight, int fragileMissionWeight, int multiStopMissionWeight)
    {
        int standard = Mathf.Max(0, standardMissionWeight);
        int timed = Mathf.Max(0, timedMissionWeight);
        int fragile = Mathf.Max(0, fragileMissionWeight);
        int multiStop = Mathf.Max(0, multiStopMissionWeight);

        if (CourierRatingManager.Instance != null)
        {
            float rating = CourierRatingManager.Instance.CurrentRating;
            if (rating >= 4.5f)
            {
                // Yüksek puanlı kuryelere yüksek gelirli kargolar (kırılabilir, acil, çoklu) daha sık gelir
                fragile = Mathf.RoundToInt(fragile * 1.6f);
                timed = Mathf.RoundToInt(timed * 1.6f);
                multiStop = Mathf.RoundToInt(multiStop * 1.5f);
            }
            else if (rating < 3.2f)
            {
                // Düşük puanlı kuryeler ucuz standart kargolara yönlendirilir
                standard = Mathf.RoundToInt(standard * 2.5f);
                fragile = Mathf.RoundToInt(fragile * 0.3f);
                timed = Mathf.RoundToInt(timed * 0.3f);
            }
        }

        int totalWeight = standard + timed + fragile + multiStop;
        if (totalWeight <= 0)
        {
            return DeliveryMissionType.Standard;
        }

        int roll = Random.Range(0, totalWeight);
        if (roll < standard) return DeliveryMissionType.Standard;
        roll -= standard;
        if (roll < timed) return DeliveryMissionType.Timed;
        roll -= timed;
        if (roll < fragile) return DeliveryMissionType.Fragile;
        return DeliveryMissionType.MultiStop;
    }

    public static DeliveryMissionConditionEvaluation EvaluateMissionConditions(
        float rushHourRewardMultiplier,
        float nightRewardMultiplier,
        float rainyRiskRewardMultiplier)
    {
        bool rushHourBonus = IsRushHour();
        bool nightBonus = IsNightTime();
        bool rainRiskBonus = WeatherManager.Instance != null &&
                             WeatherManager.Instance.GetCurrentWeather() == WeatherCondition.Rain;

        float rewardMultiplier = 1f;
        if (rushHourBonus)
        {
            rewardMultiplier *= Mathf.Max(1f, rushHourRewardMultiplier);
        }

        if (nightBonus)
        {
            rewardMultiplier *= Mathf.Max(1f, nightRewardMultiplier);
        }

        if (rainRiskBonus)
        {
            rewardMultiplier *= Mathf.Max(1f, rainyRiskRewardMultiplier);
        }

        return new DeliveryMissionConditionEvaluation(rewardMultiplier, rushHourBonus, nightBonus, rainRiskBonus);
    }

    public static string BuildMissionOfferBody(
        DeliveryMissionType missionType,
        float rewardMultiplier,
        bool rushHourBonus,
        bool nightBonus,
        bool rainRiskBonus,
        int multiStopMinStops,
        int multiStopMaxStops)
    {
        string missionLine = missionType switch
        {
            DeliveryMissionType.Timed => LocalizationTable.Get("mission_line_timed"),
            DeliveryMissionType.Fragile => LocalizationTable.Get("mission_line_fragile"),
            DeliveryMissionType.MultiStop => LocalizationTable.Format(
                "mission_line_multistop",
                Mathf.Max(2, multiStopMinStops),
                Mathf.Max(multiStopMinStops, multiStopMaxStops)),
            _ => LocalizationTable.Get("mission_line_standard")
        };

        string conditionSummary = BuildMissionConditionSummary(rewardMultiplier, rushHourBonus, nightBonus, rainRiskBonus);
        return string.IsNullOrEmpty(conditionSummary) ? missionLine : $"{missionLine}\n{conditionSummary}";
    }

    public static string BuildMissionRewardPreview(DeliveryMissionType missionType, float rewardMultiplier)
    {
        GetMissionRewardValues(missionType, rewardMultiplier, out int baseReward, out int bonusReward);
        return LocalizationTable.Format("mission_reward_preview", baseReward, bonusReward);
    }

    public static string BuildMissionConditionSummary(float rewardMultiplier, bool rushHourBonus, bool nightBonus, bool rainRiskBonus)
    {
        System.Collections.Generic.List<string> tags = new System.Collections.Generic.List<string>();
        if (rushHourBonus) tags.Add(LocalizationTable.Get("mission_condition_rush_hour"));
        if (nightBonus) tags.Add(LocalizationTable.Get("mission_condition_night"));
        if (rainRiskBonus) tags.Add(LocalizationTable.Get("mission_condition_rain_risk"));

        if (CourierRatingManager.Instance != null)
        {
            float rating = CourierRatingManager.Instance.CurrentRating;
            if (rating >= 4.8f)
                tags.Add($"{LocalizationTable.Get("rating_tier_vip")} ({LocalizationTable.Format("rating_bonus_positive", 35)})");
            else if (rating >= 4.4f)
                tags.Add($"{LocalizationTable.Get("rating_tier_senior")} ({LocalizationTable.Format("rating_bonus_positive", 20)})");
            else if (rating < 3.0f)
                tags.Add($"{LocalizationTable.Get("rating_tier_risk")} ({LocalizationTable.Format("rating_bonus_negative", 20)})");
        }

        return tags.Count == 0
            ? string.Empty
            : LocalizationTable.Format("mission_condition_summary", string.Join(", ", tags), Mathf.Max(1f, rewardMultiplier));
    }

    public static string GetMissionLabel(DeliveryMissionType missionType)
    {
        return missionType switch
        {
            DeliveryMissionType.Timed => LocalizationTable.Get("mission_label_timed"),
            DeliveryMissionType.Fragile => LocalizationTable.Get("mission_label_fragile"),
            DeliveryMissionType.MultiStop => LocalizationTable.Get("mission_label_multistop"),
            _ => LocalizationTable.Get("mission_label_standard")
        };
    }

    public static void GetMissionRewardValues(DeliveryMissionType missionType, float rewardMultiplier, out int baseReward, out int bonusReward)
    {
        int baseRaw = missionType switch
        {
            DeliveryMissionType.Timed => 150,
            DeliveryMissionType.Fragile => 175,
            DeliveryMissionType.MultiStop => 220,
            _ => 100
        };

        int bonusRaw = missionType switch
        {
            DeliveryMissionType.Timed => 95,
            DeliveryMissionType.Fragile => 110,
            DeliveryMissionType.MultiStop => 140,
            _ => 50
        };

        float ratingMult = CourierRatingManager.Instance != null
            ? CourierRatingManager.Instance.GetRatingRewardMultiplier()
            : 1f;

        float safeMultiplier = Mathf.Max(0.5f, rewardMultiplier * ratingMult);
        baseReward = Mathf.RoundToInt(baseRaw * safeMultiplier);
        bonusReward = Mathf.RoundToInt(bonusRaw * safeMultiplier);
    }

    public static QuestType ToQuestType(DeliveryMissionType missionType)
    {
        return missionType switch
        {
            DeliveryMissionType.Timed => QuestType.ExpressDelivery,
            DeliveryMissionType.Fragile => QuestType.FragileDelivery,
            DeliveryMissionType.MultiStop => QuestType.MultiStopDelivery,
            _ => QuestType.StandardDelivery
        };
    }

    private static bool IsRushHour()
    {
        int hour = System.DateTime.Now.Hour;
        return (hour >= 7 && hour <= 9) || (hour >= 17 && hour <= 19);
    }

    private static bool IsNightTime()
    {
        int hour = System.DateTime.Now.Hour;
        return hour >= 22 || hour <= 5;
    }
}
