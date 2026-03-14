using UnityEngine;
using DeliveryDriver.Quest;
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
            DeliveryMissionType.Timed => "Sureli teslimat. Hedefe hizli ulasman gerekiyor.",
            DeliveryMissionType.Fragile => "Kirilgan kargo. Carpmalardan kacinarak tasimalisin.",
            DeliveryMissionType.MultiStop => $"Cok durakli rota. Genelde {Mathf.Max(2, multiStopMinStops)}-{Mathf.Max(multiStopMinStops, multiStopMaxStops)} teslim noktasi olur.",
            _ => "Standart paket teslimati."
        };

        string conditionSummary = BuildMissionConditionSummary(rewardMultiplier, rushHourBonus, nightBonus, rainRiskBonus);
        return string.IsNullOrEmpty(conditionSummary) ? missionLine : $"{missionLine}\n{conditionSummary}";
    }

    public static string BuildMissionRewardPreview(DeliveryMissionType missionType, float rewardMultiplier)
    {
        GetMissionRewardValues(missionType, rewardMultiplier, out int baseReward, out int bonusReward);
        return $"Odul: ${baseReward} (+Bonus ${bonusReward})";
    }

    public static string BuildMissionConditionSummary(float rewardMultiplier, bool rushHourBonus, bool nightBonus, bool rainRiskBonus)
    {
        System.Collections.Generic.List<string> tags = new System.Collections.Generic.List<string>();
        if (rushHourBonus) tags.Add("Rush Hour");
        if (nightBonus) tags.Add("Night");
        if (rainRiskBonus) tags.Add("Rain Risk");

        return tags.Count == 0
            ? string.Empty
            : $"{string.Join(", ", tags)} (x{Mathf.Max(1f, rewardMultiplier):F2} reward)";
    }

    public static string GetMissionLabel(DeliveryMissionType missionType)
    {
        return missionType switch
        {
            DeliveryMissionType.Timed => "Timed Run",
            DeliveryMissionType.Fragile => "Fragile Cargo",
            DeliveryMissionType.MultiStop => "Multi-Stop Route",
            _ => "Package Delivery"
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

        float safeMultiplier = Mathf.Max(1f, rewardMultiplier);
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
