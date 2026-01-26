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
                roadGraphBuilder = FindAnyObjectByType<RoadGraphBuilder>();
            }

            if (playerTransform == null)
            {
                CarController controller = FindAnyObjectByType<CarController>();
                if (controller != null)
                {
                    playerController = controller;
                    playerTransform = controller.transform;
                }
            }
        }

        private void Update()
        {
            if (currentQuest == null || currentQuest.Status != QuestStatus.Active)
            {
                return;
            }

            currentQuest.UpdateTimer(Time.deltaTime);
            if (currentQuest.IsTimeExpired())
            {
                FailQuest(currentQuest, "Time expired");
                return;
            }

            if (!currentQuest.HasPickedUpCargo)
            {
                CheckPickupProximity();
            }
            else
            {
                CheckDeliveryProximity();
            }

            if (currentQuest == null)
            {
                return;
            }

            if (currentQuest.Cargo != null && currentQuest.Cargo.IsFragile && currentQuest.Cargo.IsDestroyed())
            {
                FailQuest(currentQuest, "Cargo destroyed");
                return;
            }

            OnQuestUpdated.Invoke(currentQuest);
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

                bool assigned = AssignQuestLocations(quest);
                if (!assigned)
                {
                    continue;
                }

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

            if (quest.PickupLocation == null || quest.DeliveryLocations == null || quest.DeliveryLocations.Count == 0)
            {
                AssignQuestLocations(quest);
            }

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

        public void ApplyCargoDamage(float amount)
        {
            if (currentQuest?.Cargo == null)
            {
                return;
            }

            currentQuest.Cargo.TakeDamage(amount);
        }

        private bool AssignQuestLocations(QuestData quest)
        {
            if (quest == null)
            {
                return false;
            }

            QuestLocation pickup = null;
            QuestLocation delivery = null;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                pickup = GenerateRandomLocation("Pickup");
                delivery = GenerateRandomLocation("Delivery");

                if (AreLocationsValid(pickup, delivery, quest.Difficulty))
                {
                    break;
                }
            }

            if (pickup == null || delivery == null)
            {
                Debug.LogWarning("[QuestManager] Failed to generate valid quest locations.");
                return false;
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

            return true;
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
            string locationName = $"{prefix} {GenerateLocationName()}";
            float triggerRadius = UnityEngine.Random.Range(10f, 15f);

            QuestLocation location = new QuestLocation(position, locationName, triggerRadius)
            {
                RoadSegmentIndex = segment.id,
                WaypointIndex = waypointIndex
            };

            return location;
        }

        private bool AreLocationsValid(QuestLocation pickup, QuestLocation delivery, QuestDifficulty difficulty)
        {
            if (pickup == null || delivery == null)
            {
                return false;
            }

            if (pickup.RoadSegmentIndex < 0 || delivery.RoadSegmentIndex < 0)
            {
                return false;
            }

            float distance = Vector3.Distance(pickup.Position, delivery.Position);
            float minDistance = difficulty switch
            {
                QuestDifficulty.Easy => 500f,
                QuestDifficulty.Medium => 1000f,
                QuestDifficulty.Hard => 1500f,
                QuestDifficulty.Expert => 2000f,
                _ => 500f
            };

            return distance >= minDistance;
        }

        private string GenerateLocationName()
        {
            string[] directions = { "North", "South", "East", "West", "Central" };
            string[] locationTypes = { "Warehouse", "Depot", "Station", "Hub", "Terminal" };

            string direction = directions[UnityEngine.Random.Range(0, directions.Length)];
            string locationType = locationTypes[UnityEngine.Random.Range(0, locationTypes.Length)];

            return $"{direction} {locationType}";
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

        private void CheckPickupProximity()
        {
            if (playerTransform == null || currentQuest?.PickupLocation == null)
            {
                return;
            }

            if (!currentQuest.PickupLocation.IsPlayerInRange(playerTransform))
            {
                return;
            }

            currentQuest.HasPickedUpCargo = true;
            currentQuest.PickupLocation.HideMarker();

            QuestLocation delivery = GetCurrentDeliveryLocation();
            delivery?.ShowMarker();
        }

        private void CheckDeliveryProximity()
        {
            if (playerTransform == null)
            {
                return;
            }

            QuestLocation currentDelivery = GetCurrentDeliveryLocation();
            if (currentDelivery == null)
            {
                return;
            }

            if (!currentDelivery.IsPlayerInRange(playerTransform))
            {
                return;
            }

            currentDelivery.HideMarker();

            if (currentQuest.CurrentDeliveryIndex < currentQuest.DeliveryLocations.Count - 1)
            {
                currentQuest.CurrentDeliveryIndex++;
                GetCurrentDeliveryLocation()?.ShowMarker();
            }
            else
            {
                CompleteQuest(currentQuest);
            }
        }

        private QuestLocation GetCurrentDeliveryLocation()
        {
            if (currentQuest?.DeliveryLocations == null || currentQuest.DeliveryLocations.Count == 0)
            {
                return null;
            }

            int index = Mathf.Clamp(currentQuest.CurrentDeliveryIndex, 0, currentQuest.DeliveryLocations.Count - 1);
            return currentQuest.DeliveryLocations[index];
        }

        public QuestSaveData GetSaveData()
        {
            QuestSaveData data = new QuestSaveData
            {
                ActiveQuests = ConvertQuestList(activeQuests),
                AvailableQuests = ConvertQuestList(availableQuests),
                CompletedQuests = ConvertQuestList(completedQuests),
                CurrentQuestID = currentQuest?.QuestID
            };

            return data;
        }

        public void LoadSaveData(QuestSaveData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[QuestManager] LoadSaveData called with null data.");
                return;
            }

            activeQuests = ConvertQuestRecords(data.ActiveQuests);
            availableQuests = ConvertQuestRecords(data.AvailableQuests);
            completedQuests = ConvertQuestRecords(data.CompletedQuests);

            currentQuest = null;
            if (!string.IsNullOrWhiteSpace(data.CurrentQuestID))
            {
                currentQuest = activeQuests.Find(q => q.QuestID == data.CurrentQuestID);
            }

            RestoreQuestMarkers();

            if (currentQuest != null)
            {
                OnQuestStarted.Invoke(currentQuest);
                OnQuestUpdated.Invoke(currentQuest);
            }
        }

        private void RestoreQuestMarkers()
        {
            foreach (QuestData quest in activeQuests)
            {
                if (quest == null)
                {
                    continue;
                }

                if (!quest.HasPickedUpCargo)
                {
                    quest.PickupLocation?.ShowMarker();
                }
                else
                {
                    QuestLocation delivery = GetCurrentDeliveryLocation(quest);
                    delivery?.ShowMarker();
                }
            }
        }

        private QuestLocation GetCurrentDeliveryLocation(QuestData quest)
        {
            if (quest?.DeliveryLocations == null || quest.DeliveryLocations.Count == 0)
            {
                return null;
            }

            int index = Mathf.Clamp(quest.CurrentDeliveryIndex, 0, quest.DeliveryLocations.Count - 1);
            return quest.DeliveryLocations[index];
        }

        private List<QuestSaveData.QuestRecord> ConvertQuestList(List<QuestData> quests)
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

        private List<QuestData> ConvertQuestRecords(List<QuestSaveData.QuestRecord> records)
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

            UnityEngine.Object manager = FindAnyObjectByType(progressionType);
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
