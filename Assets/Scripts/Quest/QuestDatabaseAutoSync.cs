using System.Collections;
using UnityEngine;

namespace DeliveryDriver.Quest
{
    public class QuestDatabaseAutoSync : MonoBehaviour
    {
        private static QuestDatabaseAutoSync instance;

        [SerializeField] private string playerId = QuestDatabaseService.DefaultPlayerId;
        [SerializeField] private string playerDisplayName = QuestDatabaseService.DefaultPlayerDisplayName;
        [SerializeField] private bool verboseLogs = false;

        private QuestManager manager;
        private bool subscribed;
        private readonly QuestSnapshotPolicy snapshotPolicy = new QuestSnapshotPolicy();
        private const float SnapshotIntervalSeconds = 15f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null)
            {
                return;
            }

            GameObject go = new GameObject("QuestDatabaseAutoSync");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<QuestDatabaseAutoSync>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            StartCoroutine(BindRoutine());
        }

        private IEnumerator BindRoutine()
        {
            var delay = new WaitForSecondsRealtime(0.5f);
            while (true)
            {
                var database = QuestDatabaseService.Instance;
                var nextManager = QuestManager.Instance;
                if (database != null && database.IsReady &&
                    nextManager != null && (!subscribed || manager != nextManager))
                {
                    Unbind();
                    if (database.EnsurePlayer(playerId, playerDisplayName))
                    {
                        manager = nextManager;
                        manager.OnQuestStarted.AddListener(OnQuestStarted);
                        manager.OnQuestCompleted.AddListener(OnQuestCompleted);
                        manager.OnQuestFailed.AddListener(OnQuestFailed);
                        manager.OnQuestUpdated.AddListener(OnQuestUpdated);
                        subscribed = true;
                        if (manager.CurrentQuest != null)
                            OnQuestUpdated(manager.CurrentQuest);
                        if (verboseLogs)
                            Debug.Log("[QuestDatabaseAutoSync] Bound to QuestManager and DB service.");
                    }
                }
                else if (nextManager == null || database == null || !database.IsReady)
                {
                    Unbind();
                }
                yield return delay;
            }
        }

        private void Unbind()
        {
            if (manager != null && subscribed)
            {
                manager.OnQuestStarted.RemoveListener(OnQuestStarted);
                manager.OnQuestCompleted.RemoveListener(OnQuestCompleted);
                manager.OnQuestFailed.RemoveListener(OnQuestFailed);
                manager.OnQuestUpdated.RemoveListener(OnQuestUpdated);
            }
            manager = null;
            subscribed = false;
            snapshotPolicy.Reset();
        }

        private void OnDestroy()
        {
            Unbind();
            if (instance == this)
                instance = null;
        }

        private bool PersistSnapshot(QuestData quest)
        {
            var database = QuestDatabaseService.Instance;
            if (database == null || !database.SaveQuestInstance(playerId, quest))
                return false;

            snapshotPolicy.Record(quest.QuestID, quest.HasPickedUpCargo, quest.CurrentDeliveryIndex,
                quest.DeliveryLocations?.Count ?? 0, Time.unscaledTime);
            return true;
        }

        private void OnQuestStarted(QuestData quest)
        {
            if (quest == null || QuestDatabaseService.Instance == null)
            {
                return;
            }

            if (!PersistSnapshot(quest)) return;
            QuestDatabaseService.Instance.InsertQuestEvent(
                quest.QuestID,
                playerId,
                "QUEST_ACCEPTED",
                quest.QuestName,
                quest.PickupLocation != null ? quest.PickupLocation.Position : (Vector3?)null,
                "{\"source\":\"runtime\"}");
        }

        private void OnQuestUpdated(QuestData quest)
        {
            if (quest == null || QuestDatabaseService.Instance == null)
            {
                return;
            }

            if (snapshotPolicy.ShouldWrite(quest.QuestID, quest.HasPickedUpCargo, quest.CurrentDeliveryIndex,
                    quest.DeliveryLocations?.Count ?? 0, Time.unscaledTime, SnapshotIntervalSeconds))
                PersistSnapshot(quest);
        }

        private void OnQuestCompleted(QuestData quest)
        {
            if (quest == null || QuestDatabaseService.Instance == null)
            {
                return;
            }

            if (!PersistSnapshot(quest)) return;
            int fuelRebate = DriverProgressionSystem.Instance != null
                ? DriverProgressionSystem.Instance.CalculateFuelEfficiencyRebate(quest)
                : 0;
            int totalAward = Mathf.Max(0, QuestManager.Instance != null ? QuestManager.Instance.LastCompletionReward : quest.CalculateFinalReward()) + Mathf.Max(0, fuelRebate);
            int balanceAfter = PlayerProgressionManager.Instance != null
                ? PlayerProgressionManager.Instance.CurrentMoney
                : QuestDatabaseService.Instance.GetPlayerBalance(playerId, 0);

            if (totalAward > 0)
            {
                string description = fuelRebate > 0
                    ? $"{quest.QuestName} completed (+fuel rebate ${fuelRebate})"
                    : $"{quest.QuestName} completed";
                QuestDatabaseService.Instance.InsertWalletTransaction(playerId, quest.QuestID, "QUEST_REWARD", totalAward, balanceAfter, description);
            }

            QuestDatabaseService.Instance.InsertQuestEvent(
                quest.QuestID,
                playerId,
                "QUEST_COMPLETED",
                $"Reward:{quest.CalculateFinalReward()}",
                null,
                "{\"source\":\"runtime\"}");
        }

        private void OnQuestFailed(QuestData quest)
        {
            if (quest == null || QuestDatabaseService.Instance == null)
            {
                return;
            }

            if (!PersistSnapshot(quest)) return;
            int penaltyAmount = QuestManager.Instance != null ? QuestManager.Instance.LastFailurePenalty : 0;
            int balanceAfter = PlayerProgressionManager.Instance != null
                ? PlayerProgressionManager.Instance.CurrentMoney
                : QuestDatabaseService.Instance.GetPlayerBalance(playerId, 0);

            if (penaltyAmount > 0)
            {
                QuestDatabaseService.Instance.InsertWalletTransaction(
                    playerId,
                    quest.QuestID,
                    "PENALTY",
                    -penaltyAmount,
                    balanceAfter,
                    $"{quest.QuestName} failed");
            }

            QuestDatabaseService.Instance.InsertQuestEvent(
                quest.QuestID,
                playerId,
                "QUEST_FAILED",
                quest.QuestName,
                null,
                "{\"source\":\"runtime\"}");
        }
    }
}
