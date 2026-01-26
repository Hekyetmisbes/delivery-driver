using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TrafficSystem;

namespace DeliveryDriver.Quest
{
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private QuestDatabase questDatabase;
        [SerializeField] private RoadGraphBuilder roadGraphBuilder;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private CarController playerController;

        [Header("Quest Lists")]
        [SerializeField] private int maxActiveQuests = 3;
        [SerializeField] private List<QuestData> activeQuests = new List<QuestData>();
        [SerializeField] private List<QuestData> availableQuests = new List<QuestData>();
        [SerializeField] private List<QuestData> completedQuests = new List<QuestData>();

        [Header("Runtime State")]
        [SerializeField] private QuestData currentQuest;

        public UnityEvent<QuestData> OnQuestStarted = new UnityEvent<QuestData>();
        public UnityEvent<QuestData> OnQuestCompleted = new UnityEvent<QuestData>();
        public UnityEvent<QuestData> OnQuestFailed = new UnityEvent<QuestData>();
        public UnityEvent<QuestData> OnQuestUpdated = new UnityEvent<QuestData>();

        public QuestData CurrentQuest => currentQuest;
        public IReadOnlyList<QuestData> ActiveQuests => activeQuests;
        public IReadOnlyList<QuestData> AvailableQuests => availableQuests;
        public IReadOnlyList<QuestData> CompletedQuests => completedQuests;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (roadGraphBuilder == null)
            {
                roadGraphBuilder = FindObjectOfType<RoadGraphBuilder>();
            }
        }

        public void GenerateAvailableQuests(int count)
        {
            if (count <= 0)
            {
                return;
            }

            if (questDatabase == null)
            {
                Debug.LogWarning("[QuestManager] QuestDatabase is not assigned.");
                return;
            }

            QuestDifficulty[] difficulties = (QuestDifficulty[])Enum.GetValues(typeof(QuestDifficulty));

            for (int i = 0; i < count; i++)
            {
                QuestDifficulty difficulty = difficulties[i % difficulties.Length];
                QuestData quest = questDatabase.GenerateRandomQuest(difficulty) ?? questDatabase.GetRandomQuest();

                if (quest == null)
                {
                    continue;
                }

                quest.Status = QuestStatus.NotStarted;
                AssignQuestLocations(quest);
                availableQuests.Add(quest);
            }
        }

        public bool AcceptQuest(string questID)
        {
            if (string.IsNullOrWhiteSpace(questID))
            {
                return false;
            }

            QuestData quest = availableQuests.Find(q => q.QuestID == questID);
            if (quest == null)
            {
                Debug.LogWarning($"[QuestManager] AcceptQuest failed. Quest '{questID}' not found.");
                return false;
            }

            if (activeQuests.Contains(quest))
            {
                return false;
            }

            if (activeQuests.Count >= maxActiveQuests)
            {
                Debug.LogWarning("[QuestManager] Cannot accept quest. Active quest limit reached.");
                return false;
            }

            availableQuests.Remove(quest);
            activeQuests.Add(quest);
            currentQuest = quest;

            quest.StartQuest();
            quest.PickupLocation?.ShowMarker();
            OnQuestStarted.Invoke(quest);

            return true;
        }

        public void AbandonQuest(string questID)
        {
            if (string.IsNullOrWhiteSpace(questID))
            {
                return;
            }

            QuestData quest = activeQuests.Find(q => q.QuestID == questID);
            if (quest == null)
            {
                Debug.LogWarning($"[QuestManager] AbandonQuest failed. Quest '{questID}' not found.");
                return;
            }

            activeQuests.Remove(quest);
            if (currentQuest == quest)
            {
                currentQuest = null;
            }

            CleanupQuestMarkers(quest);

            if (quest.IsRepeatable)
            {
                quest.Status = QuestStatus.NotStarted;
                availableQuests.Add(quest);
            }
        }

        public void CompleteQuest(QuestData quest)
        {
            if (quest == null)
            {
                return;
            }

            activeQuests.Remove(quest);
            if (currentQuest == quest)
            {
                currentQuest = null;
            }

            quest.Status = QuestStatus.Completed;
            completedQuests.Add(quest);

            int reward = quest.CalculateFinalReward();
            TryAwardRewards(quest, reward);

            OnQuestCompleted.Invoke(quest);
            CleanupQuestMarkers(quest);
            GenerateAvailableQuests(1);
        }

        public void FailQuest(QuestData quest, string reason)
        {
            if (quest == null)
            {
                return;
            }

            activeQuests.Remove(quest);
            if (currentQuest == quest)
            {
                currentQuest = null;
            }

            quest.Status = QuestStatus.Failed;
            OnQuestFailed.Invoke(quest);
            Debug.LogWarning($"[QuestManager] Quest failed: {quest.QuestName}. Reason: {reason}");

            CleanupQuestMarkers(quest);
            GenerateAvailableQuests(1);
        }

        private void AssignQuestLocations(QuestData quest)
        {
            if (quest == null)
            {
                return;
            }

            QuestLocation pickup = null;
            QuestLocation delivery = null;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                pickup = GenerateRandomLocation("Pickup");
                delivery = GenerateRandomLocation("Delivery");

                if (pickup != null && delivery != null && pickup.Position != delivery.Position)
                {
                    break;
                }
            }

            quest.PickupLocation = pickup;
            quest.DeliveryLocations ??= new List<QuestLocation>();
            quest.DeliveryLocations.Clear();

            if (delivery != null)
            {
                quest.DeliveryLocations.Add(delivery);
            }

            if (quest.QuestType == QuestType.MultiStopDelivery)
            {
                QuestLocation extraStop = GenerateRandomLocation("Delivery");
                if (extraStop != null)
                {
                    quest.DeliveryLocations.Add(extraStop);
                }
            }
        }

        private QuestLocation GenerateRandomLocation(string prefix)
        {
            if (roadGraphBuilder == null || roadGraphBuilder.RoadGraph == null)
            {
                Debug.LogWarning("[QuestManager] RoadGraphBuilder is not ready. Cannot generate quest locations.");
                return null;
            }

            var (segment, waypointIndex) = roadGraphBuilder.RoadGraph.GetRandomWaypoint();
            if (segment == null || segment.waypoints.Count == 0)
            {
                return null;
            }

            Vector3 position = segment.waypoints[waypointIndex].position;
            string locationName = $"{prefix} {segment.name}";

            QuestLocation location = new QuestLocation(position, locationName, 10f)
            {
                RoadSegmentIndex = segment.id,
                WaypointIndex = waypointIndex
            };

            return location;
        }

        private void CleanupQuestMarkers(QuestData quest)
        {
            quest?.PickupLocation?.DestroyMarker();

            if (quest?.DeliveryLocations == null)
            {
                return;
            }

            foreach (QuestLocation location in quest.DeliveryLocations)
            {
                location?.DestroyMarker();
            }
        }

        private void TryAwardRewards(QuestData quest, int reward)
        {
            if (quest == null)
            {
                return;
            }

            Type progressionType = Type.GetType("PlayerProgressionManager");
            if (progressionType == null)
            {
                Debug.Log($"[QuestManager] Reward granted: {reward} currency, {quest.XPReward} XP.");
                return;
            }

            UnityEngine.Object manager = FindObjectOfType(progressionType);
            if (manager == null)
            {
                Debug.Log($"[QuestManager] Reward granted: {reward} currency, {quest.XPReward} XP.");
                return;
            }

            bool invoked = false;
            foreach (string methodName in new[] { "AddCurrency", "AddMoney", "AddCash" })
            {
                var method = progressionType.GetMethod(methodName, new[] { typeof(int) });
                if (method != null)
                {
                    method.Invoke(manager, new object[] { reward });
                    invoked = true;
                    break;
                }
            }

            foreach (string methodName in new[] { "AddXP", "AddExperience" })
            {
                var method = progressionType.GetMethod(methodName, new[] { typeof(int) });
                if (method != null)
                {
                    method.Invoke(manager, new object[] { quest.XPReward });
                    invoked = true;
                    break;
                }
            }

            if (!invoked)
            {
                Debug.Log($"[QuestManager] Reward granted: {reward} currency, {quest.XPReward} XP.");
            }
        }
    }
}
