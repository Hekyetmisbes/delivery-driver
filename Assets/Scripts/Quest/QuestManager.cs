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

        [Header("Marker Prefabs")]
        [SerializeField] private GameObject pickupMarkerPrefab;
        [SerializeField] private GameObject deliveryMarkerPrefab;

        [Header("Quest Zones")]
        [SerializeField] private GameObject questZonePrefab;
        [SerializeField] private List<QuestZone> activeZones = new List<QuestZone>();

        [Header("Quest SFX")]
        [SerializeField] private AudioSource questSfxSource;
        [SerializeField] private AudioClip pickupClip;
        [SerializeField] private AudioClip deliveryClip;
        [SerializeField] private AudioClip failureClip;

        [Header("Cargo Visuals")]
        [SerializeField] private CargoVisual cargoVisual;

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
        public Transform PlayerTransform => playerTransform;
        public string LastFailureReason { get; private set; } = string.Empty;
        public int LastCompletionReward { get; private set; }

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
            ClearAllZones();
            SpawnQuestZone(quest.PickupLocation, QuestZoneType.Pickup);
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
            ClearAllZones();

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
            LastCompletionReward = reward;
            LastFailureReason = string.Empty;
            TryAwardRewards(quest, reward);

            OnQuestCompleted.Invoke(quest);
            CleanupQuestMarkers(quest);
            ClearAllZones();
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
            LastFailureReason = reason ?? string.Empty;
            OnQuestFailed.Invoke(quest);
            Debug.LogWarning($"[QuestManager] Quest failed: {quest.QuestName}. Reason: {reason}");

            CleanupQuestMarkers(quest);
            ClearAllZones();
            PlayQuestClip(failureClip);
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
            if (quest.PickupLocation != null && quest.PickupLocation.VisualMarker == null && pickupMarkerPrefab != null)
            {
                quest.PickupLocation.VisualMarker = pickupMarkerPrefab;
            }
            quest.DeliveryLocations ??= new List<QuestLocation>();
            quest.DeliveryLocations.Clear();

            if (delivery != null)
            {
                if (delivery.VisualMarker == null && deliveryMarkerPrefab != null)
                {
                    delivery.VisualMarker = deliveryMarkerPrefab;
                }
                quest.DeliveryLocations.Add(delivery);
            }

            if (quest.QuestType == QuestType.MultiStopDelivery)
            {
                QuestLocation extraStop = GenerateRandomLocation("Delivery");
                if (extraStop != null)
                {
                    if (extraStop.VisualMarker == null && deliveryMarkerPrefab != null)
                    {
                        extraStop.VisualMarker = deliveryMarkerPrefab;
                    }
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

            OnCargoPickedUp();
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

            OnCargoDelivered();
        }

        public QuestZone SpawnQuestZone(QuestLocation location, QuestZoneType type)
        {
            if (location == null)
            {
                return null;
            }

            GameObject zoneObject = questZonePrefab != null
                ? Instantiate(questZonePrefab, location.Position, Quaternion.identity)
                : new GameObject("QuestZone");

            zoneObject.transform.position = location.Position;

            QuestZone zone = zoneObject.GetComponent<QuestZone>();
            if (zone == null)
            {
                zone = zoneObject.AddComponent<QuestZone>();
            }

            zone.Configure(location, type);

            if (location.VisualMarker == null)
            {
                if (type == QuestZoneType.Pickup && pickupMarkerPrefab != null)
                {
                    location.VisualMarker = pickupMarkerPrefab;
                }
                else if (type == QuestZoneType.Delivery && deliveryMarkerPrefab != null)
                {
                    location.VisualMarker = deliveryMarkerPrefab;
                }
            }

            Collider zoneCollider = zoneObject.GetComponent<Collider>();
            if (zoneCollider == null)
            {
                zoneCollider = zoneObject.AddComponent<SphereCollider>();
            }

            if (zoneCollider is SphereCollider sphere)
            {
                sphere.isTrigger = true;
                sphere.radius = Mathf.Max(0.1f, location.TriggerRadius);
            }
            else if (zoneCollider is BoxCollider box)
            {
                box.isTrigger = true;
                float size = Mathf.Max(0.1f, location.TriggerRadius * 2f);
                box.size = new Vector3(size, size, size);
            }

            zone.SetActive(true);
            activeZones.Add(zone);

            return zone;
        }

        public void OnPlayerEnteredZone(QuestZone zone)
        {
            if (zone == null || currentQuest == null || currentQuest.Status != QuestStatus.Active)
            {
                return;
            }

            if (zone.ZoneType == QuestZoneType.Pickup)
            {
                OnCargoPickedUp();
            }
            else if (zone.ZoneType == QuestZoneType.Delivery)
            {
                OnCargoDelivered();
            }
        }

        public void ClearAllZones()
        {
            if (activeZones == null || activeZones.Count == 0)
            {
                return;
            }

            for (int i = activeZones.Count - 1; i >= 0; i--)
            {
                QuestZone zone = activeZones[i];
                if (zone != null)
                {
                    Destroy(zone.gameObject);
                }
            }

            activeZones.Clear();
        }

        private void OnCargoPickedUp()
        {
            if (currentQuest == null || currentQuest.HasPickedUpCargo)
            {
                return;
            }

            currentQuest.HasPickedUpCargo = true;
            currentQuest.PickupLocation?.HideMarker();
            ClearAllZones();
            PlayQuestClip(pickupClip);
            TryApplyCargoWeight(currentQuest.Cargo);
            cargoVisual?.AttachCargo(currentQuest.Cargo);

            QuestLocation delivery = GetCurrentDeliveryLocation();
            SpawnQuestZone(delivery, QuestZoneType.Delivery);
            OnQuestUpdated.Invoke(currentQuest);
            Debug.Log($"[QuestManager] Cargo loaded! Deliver to {delivery?.LocationName ?? "destination"}.");
        }

        private void OnCargoDelivered()
        {
            if (currentQuest == null || !currentQuest.HasPickedUpCargo)
            {
                return;
            }

            QuestLocation currentDelivery = GetCurrentDeliveryLocation();
            if (currentDelivery != null)
            {
                currentDelivery.HideMarker();
            }

            ClearAllZones();

            if (currentQuest.CurrentDeliveryIndex < currentQuest.DeliveryLocations.Count - 1)
            {
                currentQuest.CurrentDeliveryIndex++;
                SpawnQuestZone(GetCurrentDeliveryLocation(), QuestZoneType.Delivery);
                OnQuestUpdated.Invoke(currentQuest);
            }
            else
            {
                TryRemoveCargoWeight();
                cargoVisual?.DetachCargo();
                PlayQuestClip(deliveryClip);
                CompleteQuest(currentQuest);
            }
        }

        public void OnCargoDestroyed()
        {
            if (currentQuest == null)
            {
                return;
            }

            TryRemoveCargoWeight();
            cargoVisual?.DetachCargo();
            FailQuest(currentQuest, "Cargo destroyed");
        }

        private void PlayQuestClip(AudioClip clip)
        {
            if (questSfxSource == null || clip == null)
            {
                return;
            }

            questSfxSource.PlayOneShot(clip);
        }

        private void TryApplyCargoWeight(CargoData cargo)
        {
            if (playerController == null || cargo == null)
            {
                return;
            }

            var method = playerController.GetType().GetMethod("AddCargoWeight", new[] { typeof(float) });
            if (method != null)
            {
                method.Invoke(playerController, new object[] { cargo.Weight });
                return;
            }

            playerController.SendMessage("AddCargoWeight", cargo.Weight, SendMessageOptions.DontRequireReceiver);
        }

        private void TryRemoveCargoWeight()
        {
            if (playerController == null)
            {
                return;
            }

            var method = playerController.GetType().GetMethod("RemoveCargoWeight", Type.EmptyTypes);
            if (method != null)
            {
                method.Invoke(playerController, null);
                return;
            }

            playerController.SendMessage("RemoveCargoWeight", SendMessageOptions.DontRequireReceiver);
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
            ClearAllZones();
            foreach (QuestData quest in activeQuests)
            {
                if (quest == null)
                {
                    continue;
                }

                if (!quest.HasPickedUpCargo)
                {
                    SpawnQuestZone(quest.PickupLocation, QuestZoneType.Pickup);
                }
                else
                {
                    QuestLocation delivery = GetCurrentDeliveryLocation(quest);
                    SpawnQuestZone(delivery, QuestZoneType.Delivery);
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
