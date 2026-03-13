using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDriver.Quest
{
    internal readonly struct QuestSaveRestoreState
    {
        public QuestSaveRestoreState(
            List<QuestData> activeQuests,
            List<QuestData> availableQuests,
            List<QuestData> completedQuests,
            QuestData currentQuest)
        {
            ActiveQuests = activeQuests;
            AvailableQuests = availableQuests;
            CompletedQuests = completedQuests;
            CurrentQuest = currentQuest;
        }

        public List<QuestData> ActiveQuests { get; }
        public List<QuestData> AvailableQuests { get; }
        public List<QuestData> CompletedQuests { get; }
        public QuestData CurrentQuest { get; }
    }

    internal static class QuestSaveRestoreService
    {
        public static QuestSaveData BuildSaveData(
            IReadOnlyList<QuestData> activeQuests,
            IReadOnlyList<QuestData> availableQuests,
            IReadOnlyList<QuestData> completedQuests,
            QuestData currentQuest)
        {
            return new QuestSaveData
            {
                ActiveQuests = ConvertQuestList(activeQuests),
                AvailableQuests = ConvertQuestList(availableQuests),
                CompletedQuests = ConvertQuestList(completedQuests),
                CurrentQuestID = currentQuest?.QuestID
            };
        }

        public static bool TryRestoreSaveData(
            QuestSaveData data,
            out QuestSaveRestoreState state)
        {
            state = default;
            if (data == null)
            {
                Debug.LogWarning("[QuestManager] LoadSaveData called with null data.");
                return false;
            }

            List<QuestData> activeQuests = ConvertQuestRecords(data.ActiveQuests);
            List<QuestData> availableQuests = ConvertQuestRecords(data.AvailableQuests);
            List<QuestData> completedQuests = ConvertQuestRecords(data.CompletedQuests);

            QuestData currentQuest = null;
            if (!string.IsNullOrWhiteSpace(data.CurrentQuestID))
            {
                currentQuest = activeQuests.Find(q => q.QuestID == data.CurrentQuestID);
            }

            state = new QuestSaveRestoreState(activeQuests, availableQuests, completedQuests, currentQuest);
            return true;
        }

        public static void RestoreLoadedQuestState(
            QuestSaveRestoreState restoredState,
            Action<List<QuestData>> restoreQuestMarkers,
            Action<QuestData> onCurrentQuestRestored)
        {
            restoreQuestMarkers?.Invoke(restoredState.ActiveQuests);

            if (restoredState.CurrentQuest != null)
            {
                onCurrentQuestRestored?.Invoke(restoredState.CurrentQuest);
            }
        }

        private static List<QuestSaveData.QuestRecord> ConvertQuestList(IReadOnlyList<QuestData> quests)
        {
            List<QuestSaveData.QuestRecord> records = new List<QuestSaveData.QuestRecord>();
            if (quests == null)
            {
                return records;
            }

            foreach (QuestData quest in quests)
            {
                if (quest == null)
                {
                    continue;
                }

                records.Add(QuestSaveData.QuestRecord.FromQuestData(quest));
            }

            return records;
        }

        private static List<QuestData> ConvertQuestRecords(IReadOnlyList<QuestSaveData.QuestRecord> records)
        {
            List<QuestData> quests = new List<QuestData>();
            if (records == null)
            {
                return quests;
            }

            foreach (QuestSaveData.QuestRecord record in records)
            {
                if (record == null)
                {
                    continue;
                }

                quests.Add(record.ToQuestData());
            }

            return quests;
        }
    }
}
