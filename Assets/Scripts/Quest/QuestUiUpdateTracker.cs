using System;
using UnityEngine;

namespace DeliveryDriver.Quest
{
    /// <summary>Tracks presentation changes without changing quest lifecycle state.</summary>
    public sealed class QuestUiUpdateTracker
    {
        private int lastTimeRemainingSeconds = -1;
        private int lastDeliveryIndex = -1;
        private bool lastHasPickedUpCargo;
        private float lastCargoHealth = -1f;
        private string lastQuestId = string.Empty;
        private bool dirty = true;

        public void MarkDirty() => dirty = true;

        public bool TryCaptureChanged(QuestData quest)
        {
            if (quest == null)
                return false;

            bool changed = dirty ||
                !string.Equals(quest.QuestID, lastQuestId, StringComparison.Ordinal) ||
                Mathf.CeilToInt(quest.TimeRemaining) != lastTimeRemainingSeconds ||
                quest.CurrentDeliveryIndex != lastDeliveryIndex ||
                quest.HasPickedUpCargo != lastHasPickedUpCargo ||
                Mathf.Abs(CargoHealth(quest) - lastCargoHealth) > 0.1f;

            if (changed)
                Capture(quest);
            return changed;
        }

        public void Capture(QuestData quest)
        {
            if (quest == null)
                return;

            lastQuestId = quest.QuestID ?? string.Empty;
            lastTimeRemainingSeconds = Mathf.CeilToInt(quest.TimeRemaining);
            lastDeliveryIndex = quest.CurrentDeliveryIndex;
            lastHasPickedUpCargo = quest.HasPickedUpCargo;
            lastCargoHealth = CargoHealth(quest);
            dirty = false;
        }

        private static float CargoHealth(QuestData quest) =>
            quest.Cargo != null && quest.Cargo.IsFragile ? quest.Cargo.CargoHealth : -1f;
    }
}
