using System;

namespace DeliveryDriver.Quest
{
    /// <summary>Separates periodic quest persistence from UI countdown updates.</summary>
    public sealed class QuestSnapshotPolicy
    {
        private string questId;
        private bool pickedUp;
        private int stopIndex;
        private int stopCount;
        private float lastWriteTime;
        private bool hasSnapshot;

        public bool ShouldWrite(string id, bool hasPickedUp, int index, int count, float now, float interval)
        {
            return !hasSnapshot || !string.Equals(questId, id, StringComparison.Ordinal) ||
                   pickedUp != hasPickedUp || stopIndex != index || stopCount != count ||
                   now - lastWriteTime >= interval;
        }

        public void Record(string id, bool hasPickedUp, int index, int count, float now)
        {
            questId = id;
            pickedUp = hasPickedUp;
            stopIndex = index;
            stopCount = count;
            lastWriteTime = now;
            hasSnapshot = true;
        }

        public void Reset() => hasSnapshot = false;
    }
}
