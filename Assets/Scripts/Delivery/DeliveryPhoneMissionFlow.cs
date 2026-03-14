using System;
using UnityEngine;

internal static class DeliveryPhoneMissionFlow
{
    public static PhoneMissionUI EnsurePhoneMissionUI(
        GameObject host,
        PhoneMissionUI existing,
        bool requirePhoneMissionAccept,
        Action onAccept,
        Action onReject)
    {
        if (!requirePhoneMissionAccept)
        {
            return existing;
        }

        PhoneMissionUI phoneMissionUi = existing;
        if (phoneMissionUi == null)
        {
            phoneMissionUi = UnityEngine.Object.FindFirstObjectByType<PhoneMissionUI>();
        }

        if (phoneMissionUi == null && host != null)
        {
            phoneMissionUi = host.GetComponent<PhoneMissionUI>();
        }

        if (phoneMissionUi == null && host != null)
        {
            phoneMissionUi = host.AddComponent<PhoneMissionUI>();
        }

        phoneMissionUi?.BindCallbacks(onAccept, onReject);
        return phoneMissionUi;
    }

    public static bool TryShowOffer(
        PhoneMissionUI phoneMissionUi,
        bool requirePhoneMissionAccept,
        bool hasPendingPhoneOffer,
        bool isDeliveryActive,
        DeliveryBox currentBox,
        bool isFinishingDeliveryLifecycle,
        DeliveryMissionType missionType,
        float rewardMultiplier,
        bool rushHourBonus,
        bool nightBonus,
        bool rainRiskBonus,
        int multiStopMinStops,
        int multiStopMaxStops)
    {
        if (!requirePhoneMissionAccept || hasPendingPhoneOffer || isDeliveryActive || currentBox != null || isFinishingDeliveryLifecycle)
        {
            return false;
        }

        if (phoneMissionUi == null)
        {
            Debug.LogError("[DeliveryManager] PhoneMissionUI not found. Mission offer cannot be shown.");
            return false;
        }

        phoneMissionUi.ShowOffer(
            DeliveryMissionRules.GetMissionLabel(missionType),
            $"Yeni gorev teklifi\n{DeliveryMissionRules.BuildMissionOfferBody(missionType, rewardMultiplier, rushHourBonus, nightBonus, rainRiskBonus, multiStopMinStops, multiStopMaxStops)}\nKabul edersen gorev olusacak.",
            DeliveryMissionRules.BuildMissionRewardPreview(missionType, rewardMultiplier));
        return true;
    }

    public static void HideOffer(PhoneMissionUI phoneMissionUi)
    {
        phoneMissionUi?.HideOffer();
    }
}
