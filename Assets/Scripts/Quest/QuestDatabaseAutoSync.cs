using System.Collections;
using UnityEngine;

namespace DeliveryDriver.Quest
{
    public class QuestDatabaseAutoSync : MonoBehaviour
    {
        private static QuestDatabaseAutoSync instance;

        [SerializeField] private string playerId = "local-player";
        [SerializeField] private string playerDisplayName = "Local Player";
        [SerializeField] private bool verboseLogs = false;

        private QuestManager manager;
        private bool subscribed;

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
            while (QuestDatabaseService.Instance == null || !QuestDatabaseService.Instance.IsReady)
            {
                yield return null;
            }

            QuestDatabaseService.Instance.EnsurePlayer(playerId, playerDisplayName);

            while (manager == null)
            {
                manager = FindAnyObjectByType<QuestManager>();
                yield return null;
            }

            if (!subscribed)
            {
                manager.OnQuestStarted.AddListener(OnQuestStarted);
                manager.OnQuestCompleted.AddListener(OnQuestCompleted);
                manager.OnQuestFailed.AddListener(OnQuestFailed);
                manager.OnQuestUpdated.AddListener(OnQuestUpdated);
                subscribed = true;
            }

            if (verboseLogs)
            {
                Debug.Log("[QuestDatabaseAutoSync] Bound to QuestManager and DB service.");
            }
        }

        private void OnDestroy()
        {
            if (manager != null && subscribed)
            {
                manager.OnQuestStarted.RemoveListener(OnQuestStarted);
                manager.OnQuestCompleted.RemoveListener(OnQuestCompleted);
                manager.OnQuestFailed.RemoveListener(OnQuestFailed);
                manager.OnQuestUpdated.RemoveListener(OnQuestUpdated);
                subscribed = false;
            }
        }

        private void OnQuestStarted(QuestData quest)
        {
            if (quest == null || QuestDatabaseService.Instance == null)
            {
                return;
            }

            QuestDatabaseService.Instance.SaveQuestInstance(playerId, quest);
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

            QuestDatabaseService.Instance.SaveQuestInstance(playerId, quest);
        }

        private void OnQuestCompleted(QuestData quest)
        {
            if (quest == null || QuestDatabaseService.Instance == null)
            {
                return;
            }

            QuestDatabaseService.Instance.SaveQuestInstance(playerId, quest);
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

            QuestDatabaseService.Instance.SaveQuestInstance(playerId, quest);
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
