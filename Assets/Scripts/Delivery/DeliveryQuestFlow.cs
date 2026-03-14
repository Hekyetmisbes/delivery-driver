using System;
using System.Collections.Generic;
using DeliveryDriver.Quest;
using UnityEngine;

internal static class DeliveryQuestFlow
{
    public static QuestData CreateDeliveryQuest(
        Vector3 pickupPos,
        DeliveryMissionType missionType,
        float rewardMultiplier,
        string conditionSummary,
        CargoLibrary cargoLibrary,
        GameObject pickupIndicatorPrefab,
        GameObject deliveryIndicatorPrefab,
        float deliveryRadius,
        bool showDebugInfo)
    {
        if (QuestManager.Instance == null)
        {
            Debug.LogWarning("[DeliveryManager] QuestManager not found! Quest will not be created.");
            return null;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        QuestManager.Instance.SetDebugInfiniteTime(false);
#endif

        QuestType questType = DeliveryMissionRules.ToQuestType(missionType);
        QuestDifficulty difficulty = missionType switch
        {
            DeliveryMissionType.MultiStop => QuestDifficulty.Medium,
            DeliveryMissionType.Fragile => QuestDifficulty.Medium,
            DeliveryMissionType.Timed => QuestDifficulty.Medium,
            _ => QuestDifficulty.Easy
        };

        float baseTimeLimit = missionType switch
        {
            DeliveryMissionType.Timed => 180f,
            DeliveryMissionType.Fragile => 260f,
            DeliveryMissionType.MultiStop => 420f,
            _ => 300f
        };

        DeliveryMissionRules.GetMissionRewardValues(missionType, rewardMultiplier, out int baseReward, out int bonusReward);
        string missionLabel = DeliveryMissionRules.GetMissionLabel(missionType);
        string conditionLine = string.IsNullOrEmpty(conditionSummary) ? string.Empty : conditionSummary;

        QuestData quest = new QuestData
        {
            QuestID = Guid.NewGuid().ToString(),
            QuestName = missionLabel,
            QuestDescription = $"Pick up package at {FormatCoordinates(pickupPos)}{conditionLine}",
            QuestType = questType,
            Difficulty = difficulty,
            Status = QuestStatus.NotStarted,
            TimeLimit = baseTimeLimit,
            TimeRemaining = baseTimeLimit,
            BaseReward = baseReward,
            BonusReward = bonusReward,
            PickupLocation = new QuestLocation(pickupPos, $"Pickup: {FormatCoordinates(pickupPos)}", deliveryRadius),
            DeliveryLocations = new List<QuestLocation>()
        };

        if (quest.PickupLocation != null)
        {
            quest.PickupLocation.VisualMarker = pickupIndicatorPrefab != null
                ? pickupIndicatorPrefab
                : deliveryIndicatorPrefab;
        }

        if (cargoLibrary != null)
        {
            CargoData randomCargo = cargoLibrary.GetRandomCargo();
            if (randomCargo != null)
            {
                quest.Cargo = randomCargo;
            }
        }

        if (quest.Cargo == null)
        {
            quest.Cargo = new CargoData("Package", 50f, false, "Delivery package");
        }

        if (missionType == DeliveryMissionType.Fragile)
        {
            quest.Cargo.IsFragile = true;
            quest.Cargo.CargoHealth = 100f;
        }

        QuestManager.Instance.AddAvailableQuest(quest);
        QuestManager.Instance.StartQuest(quest);

        if (showDebugInfo)
        {
            Debug.Log($"[DeliveryManager] Created delivery quest: {quest.QuestName}");
        }

        return quest;
    }

    public static void UpdateQuestWithDelivery(
        QuestData quest,
        List<Vector3> deliveryStops,
        List<string> deliveryNeighborhoods,
        Func<Vector3, string> resolveNeighborhoodName,
        GameObject deliveryIndicatorPrefab,
        float deliveryRadius,
        bool showDebugInfo,
        string firstObjectiveDescription)
    {
        if (quest == null || deliveryStops == null || deliveryStops.Count == 0)
        {
            return;
        }

        quest.DeliveryLocations.Clear();
        float questTriggerRadius = Mathf.Max(2f, deliveryRadius * 0.65f);
        for (int i = 0; i < deliveryStops.Count; i++)
        {
            Vector3 stop = deliveryStops[i];
            string neighborhood = (deliveryNeighborhoods != null && i < deliveryNeighborhoods.Count)
                ? deliveryNeighborhoods[i]
                : resolveNeighborhoodName(stop);
            QuestLocation deliveryLocation = new QuestLocation(
                stop,
                $"Delivery {i + 1}: {FormatCoordinates(stop)} ({neighborhood})",
                questTriggerRadius);
            deliveryLocation.VisualMarker = deliveryIndicatorPrefab;
            quest.DeliveryLocations.Add(deliveryLocation);
        }

        quest.QuestDescription = firstObjectiveDescription;
        quest.Status = QuestStatus.Active;
        quest.HasPickedUpCargo = true;
        quest.CurrentDeliveryIndex = 0;
        QuestManager.Instance?.OnQuestUpdated?.Invoke(quest);

        if (showDebugInfo)
        {
            Debug.Log($"[DeliveryManager] Updated quest with {deliveryStops.Count} delivery stop(s).");
        }
    }

    public static void CompleteDeliveryQuest(QuestData quest)
    {
        if (quest == null || QuestManager.Instance == null)
        {
            return;
        }

        if (quest.Status == QuestStatus.Active)
        {
            quest.Status = QuestStatus.Completed;
            QuestManager.Instance.CompleteQuest(quest);
        }
    }

    public static string BuildDeliveryObjectiveDescription(
        int currentStopIndex,
        int totalStops,
        Vector3 currentDeliveryPoint,
        string currentDeliveryNeighborhoodName,
        string conditionSummary)
    {
        int safeTotalStops = Mathf.Max(1, totalStops);
        int shownIndex = Mathf.Clamp(currentStopIndex + 1, 1, safeTotalStops);
        string target = FormatCoordinates(currentDeliveryPoint);
        string neighborhood = string.IsNullOrWhiteSpace(currentDeliveryNeighborhoodName) ? "Bilinmiyor" : currentDeliveryNeighborhoodName;
        return $"Deliver package to stop {shownIndex}/{safeTotalStops} at {target} ({neighborhood}){conditionSummary}";
    }

    private static string FormatCoordinates(Vector3 position)
    {
        return $"({position.x:F0}, {position.z:F0})";
    }

}
