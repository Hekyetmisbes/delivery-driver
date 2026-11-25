using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TrafficSystem;

namespace DeliveryDriver.Quest
{
    public class QuestManager : MonoBehaviour
    {
        [Serializable]
        public struct RewardPenaltyBreakdown
        {
            public int DistanceReward;
            public float TimePressureMultiplier;
            public float CargoDifficultyMultiplier;
            public float NeighborhoodRiskMultiplier;
            public int GrossReward;
            public int SpeedBonus;
            public int DriftBonus;
            public float StreakMultiplier;
            public int CollisionPenalty;
            public int HardBrakePenalty;
            public int DelayPenalty;
            public int CargoDamagePenalty;
            public int TotalPenalty;
            public int FinalReward;
            public int FailurePenalty;
        }

        public static QuestManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private QuestDatabase questDatabase;
        [SerializeField] private CargoLibrary cargoLibrary;
        [SerializeField] private RoadGraphBuilder roadGraphBuilder;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private CarController playerController;

        [Header("Configuration")]
        [SerializeField] private QuestSystemSettings questSystemSettings;

        [Header("Marker Prefabs")]
        [SerializeField] private GameObject pickupMarkerPrefab;
        [SerializeField] private GameObject deliveryMarkerPrefab;

        [Header("Task 10.6: Marker Pool")]
        [SerializeField] private QuestMarkerPool markerPool;
        [SerializeField] private int markerPoolSize = 10;

        [Header("Quest Zones")]
        [SerializeField] private GameObject questZonePrefab;
        [SerializeField] private List<QuestZone> activeZones = new List<QuestZone>();

        [Header("Quest SFX")]
        [SerializeField] private AudioSource questSfxSource;
        [SerializeField] private AudioClip pickupClip;
        [SerializeField] private AudioClip deliveryClip;
        [SerializeField] private AudioClip failureClip;
        [SerializeField] private AudioClip damageClip;
        [SerializeField] private AudioClip destroyedClip;

        [Header("Task 10.1: Additional Audio")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioClip questAcceptedClip;
        [SerializeField] private AudioClip timeWarningClip;
        [SerializeField] private AudioClip levelUpClip;
        [SerializeField] private AudioClip explorationMusicClip;
        [SerializeField] private AudioClip deliveryMusicClip;
        [SerializeField] private float timeWarningThreshold = 30f;
        [SerializeField] private float musicCrossfadeDuration = 2f;

        [Header("Cargo Visuals")]
        [SerializeField] private CargoVisual cargoVisual;

        [Header("Task 10.2: Particle Effects")]
        [SerializeField] private GameObject questMarkerParticlePrefab;
        [SerializeField] private GameObject pickupEffectPrefab;
        [SerializeField] private GameObject deliveryEffectPrefab;
        [SerializeField] private GameObject damageEffectPrefab;
        [SerializeField] private GameObject levelUpEffectPrefab;
        [SerializeField] private int particlePoolSize = 10;

        [Header("Fragile Cargo Damage")]
        [SerializeField] private float collisionDamageThreshold = 10000f;
        [SerializeField] private float collisionDamageDivider = 1000f;
        [SerializeField] private float collisionDamageCooldown = 0.25f;
        [SerializeField] private float collisionDamageGraceAfterPickupSeconds = 2.5f;
        [SerializeField] private float maxCargoDamagePerCollision = 25f;
        [SerializeField] private int minDamagingCollisionsBeforeCargoFail = 2;
        [Tooltip("Teslimat iptali için gereken minimum toplam çarpışma sayısı.")]
        [SerializeField] private int maxCollisionsBeforeCancel = 10;
        private float lastCollisionTime;
        private int damagingCollisionCount;
        private float lastCargoPickupTime = -999f;

        [Header("Quest Lists")]
        [SerializeField] private int maxActiveQuests = 3;
        [SerializeField] private List<QuestData> activeQuests = new List<QuestData>();
        [SerializeField] private List<QuestData> availableQuests = new List<QuestData>();
        [SerializeField] private List<QuestData> completedQuests = new List<QuestData>();

        [Header("Runtime State")]
        [SerializeField] private QuestData currentQuest;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Header("Task 10.7: Debug Tools")]
        [SerializeField] private bool debugInfiniteTime = false;
        [SerializeField] private bool debugInvincibleCargo = false;
        [SerializeField] private bool debugDrawGizmos = true;
        [SerializeField] private bool debugDrawRoute = true;
        [SerializeField] private bool debugDrawLabels = true;
        [SerializeField] private Color debugPickupColor = new Color(0.2f, 0.9f, 1f, 0.9f);
        [SerializeField] private Color debugDeliveryColor = new Color(0.3f, 1f, 0.5f, 0.9f);
        [SerializeField] private Color debugRouteColor = new Color(1f, 0.85f, 0.2f, 0.9f);
#endif

        [Header("Streak System")]
        [SerializeField] private int consecutiveSuccesses = 0;
        [SerializeField] private float streakMultiplier = 1.0f;
        [SerializeField] private float maxStreakMultiplier = 2.0f;
        [SerializeField] private float streakMultiplierIncrement = 0.1f;

        [Header("Reward Formula")]
        [SerializeField] private float payoutAverageSpeedMetersPerSecond = 11f;
        [SerializeField] private float minTimePressureMultiplier = 0.85f;
        [SerializeField] private float maxTimePressureMultiplier = 1.6f;
        [SerializeField] private float fragileCargoDifficultyBonus = 0.2f;
        [SerializeField] private float heavyCargoWeightStartKg = 120f;
        [SerializeField] private float heavyCargoWeightMaxBonus = 0.2f;

        [Header("Neighborhood Risk Multipliers")]
        [SerializeField] private float mediumRiskMultiplier = 1.1f;
        [SerializeField] private float hardRiskMultiplier = 1.22f;
        [SerializeField] private float expertRiskMultiplier = 1.35f;
        [SerializeField] private float expressRiskBonus = 0.06f;
        [SerializeField] private float fragileRiskBonus = 0.08f;
        [SerializeField] private float multiStopRiskBonus = 0.1f;

        [Header("Penalty System")]
        [SerializeField] private int collisionPenaltyBase = 25;
        [SerializeField] private int collisionPenaltyStep = 15;
        [SerializeField] private int npcCollisionPenaltyBase = 75;
        [SerializeField] private int npcCollisionPenaltyStep = 35;
        [SerializeField] private int hardBrakePenaltyBase = 5;
        [SerializeField] private int hardBrakePenaltyStep = 5;
        [SerializeField] private float delayPenaltyStartsAtRemainingRatio = 0.2f;
        [SerializeField] private float delayPenaltyMaxRatio = 0.25f;
        [SerializeField] private float cargoDamagePenaltyPerPercent = 3f;
        [SerializeField] private int timeExpiredFailurePenalty = 120;
        [SerializeField] private int cargoDestroyedFailurePenalty = 180;

        [Header("Drift Bonus")]
        [SerializeField] private bool enableDriftBonus = true;
        [SerializeField] private int driftPointsPerRewardStep = 10;
        [SerializeField] private int driftRewardStepAmount = 8;
        [SerializeField] private int maxDriftBonusReward = 600;

        [Header("Daily Challenge")]
        [SerializeField] private QuestData dailyChallenge;
        [SerializeField] private string lastDailyChallengeDate = "";
        [SerializeField] private float dailyChallengeRewardMultiplier = 2.0f;
        
        [Header("Generation Settings")]
        [SerializeField] private float locationCooldownDistance = 100f;
        private List<Vector3> usedLocations = new List<Vector3>();

        [Header("Quest Pool Refresh")]
        [SerializeField] private float questRefreshInterval = 300f; // 5 minutes
        [SerializeField] private float manualRefreshCooldown = 30f; // 30 seconds
        [SerializeField] private int targetQuestPoolSize = 5;
        private float timeSinceLastRefresh = 0f;
        private float lastManualRefreshTime = -999f;

        private readonly QuestUiUpdateTracker questUiUpdateTracker = new QuestUiUpdateTracker();
        private Coroutine startupInitCoroutine;
        private bool committingDeliveryStop;
        private QuestData restoringQuest;
        public bool IsSaveReady { get; private set; }
        private QuestLocationAssignmentService questLocationAssignmentService;
        private QuestZoneMarkerService questZoneMarkerService;
        private QuestAudioPresentationService questAudioPresentationService;
        private QuestParticleEffectService questParticleEffectService;

        public UnityEvent<QuestData> OnQuestStarted = new UnityEvent<QuestData>();
        public UnityEvent<QuestData> OnQuestCompleted = new UnityEvent<QuestData>();
        public UnityEvent<QuestData> OnQuestFailed = new UnityEvent<QuestData>();
        public UnityEvent<QuestData> OnQuestUpdated = new UnityEvent<QuestData>();
        public UnityEvent<QuestData> OnDailyChallengeGenerated = new UnityEvent<QuestData>();
        public UnityEvent<string, int> OnDrivingFeedback = new UnityEvent<string, int>();
        internal event Action<QuestData> QuestAbandoned;

        internal bool IsRestoringQuest(QuestData quest) => ReferenceEquals(restoringQuest, quest);

        public QuestData CurrentQuest => currentQuest;
        public IReadOnlyList<QuestData> ActiveQuests => activeQuests;
        public IReadOnlyList<QuestData> AvailableQuests => availableQuests;
        public IReadOnlyList<QuestData> CompletedQuests => completedQuests;
        public Transform PlayerTransform => playerTransform;
        public string LastFailureReason { get; private set; } = string.Empty;
        public int LastCompletionReward { get; private set; }
        public int LastFailurePenalty { get; private set; }
        public RewardPenaltyBreakdown LastQuestBreakdown { get; private set; }
        public int ConsecutiveSuccesses => consecutiveSuccesses;
        public float StreakMultiplier => streakMultiplier;
        public QuestData DailyChallenge => dailyChallenge;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool DebugInfiniteTimeEnabled => debugInfiniteTime;
        public bool DebugInvincibleCargoEnabled => debugInvincibleCargo;
        public bool DebugDrawGizmosEnabled => debugDrawGizmos;
        public bool DebugDrawRouteEnabled => debugDrawRoute;
        public bool DebugDrawLabelsEnabled => debugDrawLabels;
#endif

        private QuestLocationAssignmentService GetLocationAssignmentService()
        {
            usedLocations ??= new List<Vector3>();
            questLocationAssignmentService ??= new QuestLocationAssignmentService(
                () => roadGraphBuilder,
                () => pickupMarkerPrefab,
                () => deliveryMarkerPrefab,
                usedLocations,
                locationCooldownDistance);

            return questLocationAssignmentService;
        }

        private QuestZoneMarkerService GetZoneMarkerService()
        {
            activeZones ??= new List<QuestZone>();
            questZoneMarkerService ??= new QuestZoneMarkerService(
                () => questZonePrefab,
                () => pickupMarkerPrefab,
                () => deliveryMarkerPrefab,
                activeZones,
                position => GetParticleEffectService().SpawnMarkerParticles(position));

            return questZoneMarkerService;
        }

        private QuestAudioPresentationService GetAudioPresentationService()
        {
            questAudioPresentationService ??= new QuestAudioPresentationService(
                questSfxSource,
                musicSource,
                timeWarningClip,
                levelUpClip,
                explorationMusicClip,
                deliveryMusicClip,
                timeWarningThreshold,
                musicCrossfadeDuration);

            return questAudioPresentationService;
        }

        private QuestParticleEffectService GetParticleEffectService()
        {
            questParticleEffectService ??= new QuestParticleEffectService(
                this,
                questMarkerParticlePrefab,
                pickupEffectPrefab,
                deliveryEffectPrefab,
                damageEffectPrefab,
                levelUpEffectPrefab,
                particlePoolSize);

            return questParticleEffectService;
        }

        private Coroutine StartManagedCoroutine(IEnumerator routine)
        {
            return StartCoroutine(routine);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (questSystemSettings != null)
            {
                QuestLogger.EnableLogs = questSystemSettings.EnableDebugLogging;
            }

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
            
            if (cargoLibrary == null)
            {
                // Try to load from Resources if not assigned
                cargoLibrary = Resources.Load<CargoLibrary>("CargoLibrary");
            }

            if (questDatabase == null)
            {
                questDatabase = Resources.Load<QuestDatabase>("QuestDatabase");
            }

            if (SaveManager.Instance == null)
            {
                GameObject saveManagerObject = new GameObject("SaveManager");
                saveManagerObject.AddComponent<SaveManager>();
            }

            if (markerPool == null)
            {
                markerPool = FindAnyObjectByType<QuestMarkerPool>();
                if (markerPool == null)
                {
                    GameObject poolObject = new GameObject("QuestMarkerPool");
                    markerPool = poolObject.AddComponent<QuestMarkerPool>();
                }
            }
        }

        private void Start()
        {
            // Task 10.2: Initialize particle pool
            GetParticleEffectService().InitializeParticlePool();

            if (markerPool != null)
            {
                markerPool.Prewarm(pickupMarkerPrefab, markerPoolSize);
                markerPool.Prewarm(deliveryMarkerPrefab, markerPoolSize);
            }

            // Task 10.1: Start exploration music
            GetAudioPresentationService().InitializeExplorationMusic();

            // Delay quest generation until road graph is built to avoid startup warnings.
            startupInitCoroutine = StartCoroutine(InitializeQuestStateWhenReady());
        }

        [ContextMenu("Auto Assign Startup References")]
        private void AutoAssignStartupReferencesFromContextMenu()
        {
            AutoAssignStartupReferences();
        }

        public bool AutoAssignStartupReferences()
        {
            bool changed = false;

            if (roadGraphBuilder == null)
            {
                roadGraphBuilder = FindSceneComponent<RoadGraphBuilder>();
                changed |= roadGraphBuilder != null;
            }

            if (playerController == null)
            {
                playerController = FindSceneComponent<CarController>();
                changed |= playerController != null;
            }

            if (playerTransform == null && playerController != null)
            {
                playerTransform = playerController.transform;
                changed = true;
            }

            if (cargoLibrary == null)
            {
                cargoLibrary = Resources.Load<CargoLibrary>("CargoLibrary");
                changed |= cargoLibrary != null;
            }

            if (questDatabase == null)
            {
                questDatabase = Resources.Load<QuestDatabase>("QuestDatabase");
                changed |= questDatabase != null;
            }

            if (markerPool == null)
            {
                markerPool = FindSceneComponent<QuestMarkerPool>();
                changed |= markerPool != null;
            }

            return changed;
        }

        private IEnumerator InitializeQuestStateWhenReady()
        {
            if (roadGraphBuilder == null)
            {
                roadGraphBuilder = FindAnyObjectByType<RoadGraphBuilder>();
            }

            // Wait briefly for RoadGraphBuilder.Start() to run and build the graph.
            float timeoutAt = Time.time + 8f;
            while (!IsRoadGraphReady() && Time.time < timeoutAt)
            {
                yield return null;
            }

            bool hasRuntimeQuestState = currentQuest != null || activeQuests.Count > 0 || availableQuests.Count > 0;
            if (hasRuntimeQuestState)
            {
                Debug.Log("[QuestManager] Skipping startup save/quest initialization because runtime quest state already exists.");

                if (IsRoadGraphReady())
                {
                    CheckDailyChallenge();
                }

                IsSaveReady = true;
                yield break;
            }

            if (SaveManager.Instance != null)
            {
                GameSaveData saveData = SaveManager.Instance.LoadGame();

                if (saveData != null && saveData.QuestData != null)
                {
                    LoadSaveData(saveData.QuestData);
                    Debug.Log("[QuestManager] Loaded quest data from save file.");
                }
                else if (IsRoadGraphReady())
                {
                    Debug.Log("[QuestManager] No save found. Generating initial quests.");
                    GenerateAvailableQuests(5);
                }
            }
            else if (IsRoadGraphReady())
            {
                GenerateAvailableQuests(5);
            }

            if (IsRoadGraphReady())
            {
                CheckDailyChallenge();
            }

            IsSaveReady = true;
        }

        private bool IsRoadGraphReady()
        {
            return roadGraphBuilder != null &&
                   roadGraphBuilder.RoadGraph != null &&
                   roadGraphBuilder.RoadGraph.roadSegments != null &&
                   roadGraphBuilder.RoadGraph.roadSegments.Count > 0;
        }

        private void Update()
        {
            // Task 9.5: Quest Pool Refresh Timer
            timeSinceLastRefresh += Time.deltaTime;
            if (timeSinceLastRefresh >= questRefreshInterval)
            {
                RefreshAvailableQuests();
                timeSinceLastRefresh = 0f;
            }

            if (currentQuest == null || currentQuest.Status != QuestStatus.Active)
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!debugInfiniteTime)
#endif
            {
                currentQuest.UpdateTimer(Time.deltaTime);
                GetAudioPresentationService().TryPlayTimeWarning(currentQuest.TimeRemaining);
                if (currentQuest.IsTimeExpired())
                {
                    FailQuest(currentQuest, "Time expired");
                    return;
                }
            }

            // Track distance if cargo has been picked up
            if (currentQuest.HasPickedUpCargo && playerTransform != null)
            {
                currentQuest.UpdateDistanceTracking(playerTransform.position);
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!debugInvincibleCargo && ShouldFailForCargoDestroyed(currentQuest))
#else
            if (ShouldFailForCargoDestroyed(currentQuest))
#endif
            {
                FailQuest(currentQuest, "Cargo destroyed");
                return;
            }

            TryNotifyQuestUpdated();
        }

        public void GenerateAvailableQuests(int count)
        {
            if (count <= 0)
            {
                return;
            }

            // Get player level for quest filtering
            int playerLevel = 1;
            if (PlayerProgressionManager.Instance != null)
            {
                playerLevel = PlayerProgressionManager.Instance.CurrentLevel;
            }
            
            for (int i = 0; i < count; i++)
            {
                if (questDatabase == null)
                {
                    Debug.LogError("[QuestManager] QuestDatabase is not assigned. Available quest generation aborted.");
                    break;
                }

                QuestData quest = questDatabase.GenerateRandomQuestForLevel(playerLevel);
                if (quest == null)
                {
                    Debug.LogError($"[QuestManager] Could not fetch quest template from QuestDatabase for player level {playerLevel}. Fallback is disabled.");
                    continue;
                }

                // For template quests, we still need to assign locations.
                quest.Status = QuestStatus.NotStarted;
                if (!GetLocationAssignmentService().AssignQuestLocations(quest))
                {
                    Debug.LogError($"[QuestManager] Quest template '{quest.QuestName}' could not be assigned runtime locations. Fallback is disabled.");
                    continue;
                }

                availableQuests.Add(quest);
            }
        }

        public void SetPlayerVehicle(CarController controller)
        {
            playerController = controller;
            playerTransform = controller != null ? controller.transform : null;
        }

        private static T FindSceneComponent<T>() where T : Component
        {
            T[] components = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return components != null && components.Length > 0 ? components[0] : null;
        }
        
        /// <summary>
        /// Task 9.2: Generates a quest procedurally based on difficulty parameters.
        /// </summary>
        public QuestData GenerateQuestByDifficulty(QuestDifficulty difficulty)
        {
            CargoData cargo = (cargoLibrary != null ? cargoLibrary.GetRandomCargo() : null)
                ?? new CargoData("Generic Cargo", 100f, false, "Standard cargo.");
            Vector2 distanceRange = questSystemSettings != null
                ? questSystemSettings.GetDistanceRange(difficulty)
                : QuestGenerationRules.StandardDistanceRange(difficulty);

            if (!TryGenerateLocationPair("Pickup", "Delivery", distanceRange, true,
                out var pickup, out var delivery, out float distance))
                return null;

            return CreateGeneratedQuest($"{difficulty} Delivery",
                $"Transport {cargo.CargoName} from {pickup.LocationName} to {delivery.LocationName}.",
                QuestType.StandardDelivery, difficulty, cargo, pickup,
                new List<QuestLocation> { delivery }, distance);
        }

        private bool TryGenerateLocationPair(string pickupName, string deliveryName,
            Vector2 distanceRange, bool allowFallback, out QuestLocation pickup,
            out QuestLocation delivery, out float distance)
        {
            var locations = GetLocationAssignmentService();
            pickup = delivery = null;
            distance = 0f;
            for (int i = 0; i < 15; i++)
            {
                pickup = locations.GenerateRandomLocation(pickupName);
                delivery = locations.GenerateRandomLocation(deliveryName);
                if (pickup == null || delivery == null)
                    continue;

                distance = Vector3.Distance(pickup.Position, delivery.Position);
                if (distance >= distanceRange.x && distance <= distanceRange.y)
                    return true;
            }

            if (!allowFallback)
                return false;

            pickup = locations.GenerateRandomLocation(pickupName);
            delivery = locations.GenerateRandomLocation(deliveryName);
            if (pickup == null || delivery == null)
                return false;

            distance = Vector3.Distance(pickup.Position, delivery.Position);
            return true;
        }

        private QuestData CreateGeneratedQuest(string name, string description, QuestType type,
            QuestDifficulty difficulty, CargoData cargo, QuestLocation pickup,
            List<QuestLocation> deliveries, float distance)
        {
            var quest = new QuestData
            {
                QuestID = Guid.NewGuid().ToString(),
                QuestName = name,
                QuestDescription = description,
                QuestType = type,
                Difficulty = difficulty,
                Status = QuestStatus.NotStarted,
                PickupLocation = pickup,
                DeliveryLocations = deliveries,
                Cargo = cargo
            };
            GetLocationAssignmentService().EnsureQuestMarkersAssigned(quest);
            QuestGenerationRules.Apply(quest, distance, questSystemSettings);
            return quest;
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

            return TryAcceptQuest(quest);
        }

        private bool TryAcceptQuest(QuestData quest)
        {
            if (!ShouldQuestHandlePickup(quest) || !CanActivateQuest(quest))
                return false;

            if ((quest.PickupLocation == null || GetDeliveryStop(quest, 0) == null) &&
                !GetLocationAssignmentService().AssignQuestLocations(quest))
                return false;

            quest.StartQuest();
            RegisterStartedQuest(quest);
            GetZoneMarkerService().ClearAllZones();
            if (ShouldQuestUseWorldZoneMarkers(quest))
            {
                GetZoneMarkerService().SpawnQuestZone(quest.PickupLocation, QuestZoneType.Pickup);
            }

            if (PlayerProgressionManager.Instance != null)
            {
                PlayerProgressionManager.Instance.RecordQuestAttempt(quest);
            }

            // Task 10.1: Play quest accepted sound and switch music
            GetAudioPresentationService().PlayQuestClip(questAcceptedClip);
            GetAudioPresentationService().SwitchToDeliveryMusic(StartManagedCoroutine);

            // Reset time warning flag
            GetAudioPresentationService().ResetTimeWarning();
            MarkQuestUiDirty();

            OnQuestStarted.Invoke(quest);

            return true;
        }

        private bool CanActivateQuest(QuestData quest)
        {
            if (quest.Status != QuestStatus.NotStarted || string.IsNullOrWhiteSpace(quest.QuestID) ||
                activeQuests.Exists(active => active.QuestID == quest.QuestID))
                return false;

            if (activeQuests.Count < maxActiveQuests)
                return true;

            Debug.LogWarning("[QuestManager] Cannot accept quest. Active quest limit reached.");
            return false;
        }

        private void RegisterStartedQuest(QuestData quest)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugInfiniteTime)
            {
                debugInfiniteTime = false;
                Debug.Log("[QuestManager] Debug infinite time auto-disabled on quest start.");
            }
#endif
            availableQuests.Remove(quest);
            activeQuests.Add(quest);
            currentQuest = quest;
            damagingCollisionCount = 0;
            lastCargoPickupTime = -999f;
        }

        private bool RemoveActiveQuest(QuestData quest, out bool wasCurrent)
        {
            wasCurrent = ReferenceEquals(currentQuest, quest);
            if (!activeQuests.Remove(quest))
                return false;

            if (wasCurrent)
                currentQuest = null;
            return true;
        }

        private bool IsActiveQuest(QuestData quest) =>
            quest != null && quest.Status == QuestStatus.Active && activeQuests.Contains(quest);

        private void ClearQuestPresentation(QuestData quest, bool wasCurrent)
        {
            var markers = GetZoneMarkerService();
            markers.CleanupQuestMarkers(quest);
            if (wasCurrent && currentQuest == null)
                markers.ClearAllZones();
        }

        private void ReleaseQuestCargo(QuestData quest, bool wasCurrent)
        {
            if (!wasCurrent || !quest.HasPickedUpCargo)
                return;

            TryRemoveCargoWeight();
            cargoVisual?.DetachCargo();
        }

        private void ResumeRemainingQuest()
        {
            if (currentQuest != null || activeQuests.Count == 0)
                return;

            currentQuest = activeQuests[activeQuests.Count - 1];
            damagingCollisionCount = 0;
            lastCargoPickupTime = -999f;
            if (currentQuest.HasPickedUpCargo)
            {
                TryApplyCargoWeight(currentQuest.Cargo);
                cargoVisual?.AttachCargo(currentQuest.Cargo);
                if (playerTransform != null)
                    currentQuest.LastPosition = playerTransform.position;
            }

            if (ShouldQuestUseWorldZoneMarkers(currentQuest))
            {
                var markers = GetZoneMarkerService();
                if (currentQuest.HasPickedUpCargo)
                    markers.SpawnQuestZone(GetCurrentDeliveryLocation(), QuestZoneType.Delivery);
                else
                    markers.SpawnQuestZone(currentQuest.PickupLocation, QuestZoneType.Pickup);
            }

            GetAudioPresentationService().ResetTimeWarning();
            MarkQuestUiDirty();
            TryNotifyQuestUpdated();
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

            if (!IsActiveQuest(quest) || !RemoveActiveQuest(quest, out bool wasCurrent))
                return;

            quest.Status = QuestStatus.Expired;
            ReleaseQuestCargo(quest, wasCurrent);
            ClearQuestPresentation(quest, wasCurrent);

            if (quest.IsRepeatable && ShouldQuestHandlePickup(quest))
            {
                quest.Status = QuestStatus.NotStarted;
                if (!ReferenceEquals(quest, dailyChallenge))
                    availableQuests.Add(quest);
            }

            if (PlayerProgressionManager.Instance != null)
            {
                PlayerProgressionManager.Instance.RecordQuestFailure(quest);
            }

            QuestAbandoned?.Invoke(quest);
            if (wasCurrent && activeQuests.Count == 0)
            {
                GetAudioPresentationService().SwitchToExplorationMusic(StartManagedCoroutine);
                OnQuestUpdated?.Invoke(null);
            }
            ResumeRemainingQuest();
        }

        public void CompleteQuest(QuestData quest)
        {
            if (quest == null || quest.Status != QuestStatus.Active || !RemoveActiveQuest(quest, out bool wasCurrent))
                return;

            quest.Status = QuestStatus.Completed;
            ReleaseQuestCargo(quest, wasCurrent);
            quest.CalculateFinalReward(); // Keeps EarnedBonus state up-to-date for rating/stats.

            float completionStreakMultiplier = CalculateStreakMultiplier(consecutiveSuccesses + 1);
            RewardPenaltyBreakdown payout = CalculateRewardPenaltyBreakdown(quest, completionStreakMultiplier, true);
            int finalReward = payout.FinalReward;
            LastCompletionReward = finalReward;
            LastFailureReason = string.Empty;
            LastFailurePenalty = 0;
            LastQuestBreakdown = payout;

            // Increment streak after a successful completion.
            consecutiveSuccesses++;
            streakMultiplier = completionStreakMultiplier;

            // Calculate performance rating
            quest.CalculateRating();

            float completionTimeSeconds = Mathf.Max(0f, quest.TimeLimit - quest.TimeRemaining);

            completedQuests.Add(quest);
            TryAwardRewards(quest, finalReward);

            // Record quest completion for achievements and statistics
            if (PlayerProgressionManager.Instance != null)
            {
                PlayerProgressionManager.Instance.RecordQuestCompletion(quest, finalReward, completionTimeSeconds);
            }

            DriverProgressionSystem.Instance?.OnQuestCompleted(quest, finalReward, completionTimeSeconds);

            Debug.Log($"[QuestManager] Quest completed with {quest.Rating} rank! Reward={finalReward}, Penalty={payout.TotalPenalty}, Streak={consecutiveSuccesses} ({streakMultiplier:F2}x)");

            // Task 10.1: Play success sound
            GetAudioPresentationService().PlayQuestClip(deliveryClip);

            // Task 10.2: Play delivery particle effect
            if (playerTransform != null)
            {
                GetParticleEffectService().PlayParticleEffect(deliveryEffectPrefab, playerTransform.position);
            }

            // Task 10.1: Switch back to exploration music
            if (activeQuests.Count == 0)
                GetAudioPresentationService().SwitchToExplorationMusic(StartManagedCoroutine);

            OnQuestCompleted.Invoke(quest);
            ClearQuestPresentation(quest, wasCurrent);
            ResumeRemainingQuest();
            GenerateAvailableQuests(1);

            // Trigger auto-save after quest completion
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.TriggerAutoSave();
            }
        }

        public void FailQuest(QuestData quest, string reason)
        {
            if (quest == null || quest.Status != QuestStatus.Active || !RemoveActiveQuest(quest, out bool wasCurrent))
                return;

            quest.Status = QuestStatus.Failed;
            ReleaseQuestCargo(quest, wasCurrent);

            // Calculate rating (will be F for failed quests)
            quest.CalculateRating();

            // Reset streak on failure
            if (consecutiveSuccesses > 0)
            {
                Debug.Log($"[QuestManager] Streak broken! Lost {consecutiveSuccesses}x streak.");
            }
            consecutiveSuccesses = 0;
            streakMultiplier = 1.0f;

            LastFailureReason = reason ?? string.Empty;
            LastCompletionReward = 0;
            LastFailurePenalty = CalculateFailurePenalty(quest, LastFailureReason);
            RewardPenaltyBreakdown failureBreakdown = CalculateRewardPenaltyBreakdown(quest, streakMultiplier, true);
            failureBreakdown.FailurePenalty = LastFailurePenalty;
            LastQuestBreakdown = failureBreakdown;
            TryApplyFailurePenalty(LastFailurePenalty);
            OnQuestFailed.Invoke(quest);
            Debug.LogWarning($"[QuestManager] Quest failed: {quest.QuestName}. Reason: {reason}. Penalty={LastFailurePenalty}");

            if (PlayerProgressionManager.Instance != null)
            {
                PlayerProgressionManager.Instance.RecordQuestFailure(quest);
            }

            ClearQuestPresentation(quest, wasCurrent);
            GetAudioPresentationService().PlayQuestClip(failureClip);

            // Task 10.1: Switch back to exploration music
            if (activeQuests.Count == 0)
                GetAudioPresentationService().SwitchToExplorationMusic(StartManagedCoroutine);
            ResumeRemainingQuest();

            GenerateAvailableQuests(1);
        }

        public void ApplyCargoDamage(float amount)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugInvincibleCargo)
            {
                return;
            }
#endif
            if (currentQuest?.Cargo == null)
            {
                return;
            }

            currentQuest.Cargo.TakeDamage(amount);
        }

        private void CheckPickupProximity()
        {
            if (playerTransform == null || currentQuest?.PickupLocation == null)
            {
                return;
            }

            if (!ShouldQuestHandlePickup(currentQuest))
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
            if (playerTransform == null || !ShouldQuestUseWorldZoneMarkers(currentQuest))
            {
                return;
            }

            QuestLocation currentDelivery = GetDeliveryStop(currentQuest, currentQuest.CurrentDeliveryIndex);
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
            return GetZoneMarkerService().SpawnQuestZone(location, type);
        }

        public void OnPlayerEnteredZone(QuestZone zone)
        {
            if (zone == null || !zone.IsActive || !IsActiveQuest(currentQuest) ||
                !ShouldQuestUseWorldZoneMarkers(currentQuest))
            {
                return;
            }

            if (zone.ZoneType == QuestZoneType.Pickup)
            {
                if (zone.Location == null || !ReferenceEquals(zone.Location, currentQuest.PickupLocation))
                {
                    return;
                }

                OnCargoPickedUp();
            }
            else if (zone.ZoneType == QuestZoneType.Delivery && zone.Location != null &&
                ReferenceEquals(zone.Location, GetDeliveryStop(currentQuest, currentQuest.CurrentDeliveryIndex)))
            {
                OnCargoDelivered();
            }
        }

        public void ClearAllZones()
        {
            GetZoneMarkerService().ClearAllZones();
        }

        public bool CommitExternalPickup(QuestData quest, string deliveryObjectiveDescription = null)
        {
            if (!CanPickUpCargo(quest) || quest.PickupAuthority != QuestPickupAuthority.PhysicalBox)
                return false;

            if (!string.IsNullOrWhiteSpace(deliveryObjectiveDescription))
            {
                quest.QuestDescription = deliveryObjectiveDescription;
            }

            currentQuest = quest;
            OnCargoPickedUp();
            return true;
        }

        public bool CommitExternalDeliveryStop(QuestData quest)
            => CommitExternalDeliveryStop(quest, quest?.CurrentDeliveryIndex ?? -1);

        internal bool CommitExternalDeliveryStop(QuestData quest, int expectedStopIndex)
        {
            if (committingDeliveryStop || !IsActiveQuest(quest) || !quest.HasPickedUpCargo ||
                quest.PickupAuthority != QuestPickupAuthority.PhysicalBox ||
                quest.CurrentDeliveryIndex != expectedStopIndex || GetDeliveryStop(quest, expectedStopIndex) == null)
                return false;

            currentQuest = quest;
            OnCargoDelivered();
            return true;
        }

        private bool CanPickUpCargo(QuestData quest) =>
            IsActiveQuest(quest) && !quest.HasPickedUpCargo && GetDeliveryStop(quest, 0) != null;

        private void OnCargoPickedUp()
        {
            if (!CanPickUpCargo(currentQuest))
            {
                return;
            }

            currentQuest.CurrentDeliveryIndex = 0;
            currentQuest.HasPickedUpCargo = true;
            damagingCollisionCount = 0;
            lastCargoPickupTime = Time.time;
            currentQuest.PickupLocation?.HideMarker();
            GetZoneMarkerService().ClearAllZones();
            GetAudioPresentationService().PlayQuestClip(pickupClip);

            // Task 10.2: Play pickup particle effect
            if (playerTransform != null)
            {
                GetParticleEffectService().PlayParticleEffect(pickupEffectPrefab, playerTransform.position);
            }

            TryApplyCargoWeight(currentQuest.Cargo);
            cargoVisual?.AttachCargo(currentQuest.Cargo);

            // Initialize distance tracking starting position
            if (playerTransform != null)
            {
                currentQuest.LastPosition = playerTransform.position;
            }

            QuestLocation delivery = GetCurrentDeliveryLocation();
            if (ShouldQuestUseWorldZoneMarkers(currentQuest))
            {
                GetZoneMarkerService().SpawnQuestZone(delivery, QuestZoneType.Delivery);
            }
            MarkQuestUiDirty();
            TryNotifyQuestUpdated();
            Debug.Log($"[QuestManager] Cargo loaded! Deliver to {delivery?.LocationName ?? "destination"}.");

            // Task 10.4: Tutorial integration
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.OnCargoPickedUp();
            }
        }

        private void OnCargoDelivered()
        {
            QuestData quest = currentQuest;
            if (committingDeliveryStop || !IsActiveQuest(quest) || !quest.HasPickedUpCargo)
                return;

            QuestLocation currentDelivery = GetDeliveryStop(quest, quest.CurrentDeliveryIndex);
            if (currentDelivery == null)
                return;

            committingDeliveryStop = true;
            try
            {
                currentDelivery.HideMarker();
                AdvanceDeliveryStop(quest);
            }
            finally
            {
                committingDeliveryStop = false;
            }
        }

        private void AdvanceDeliveryStop(QuestData quest)
        {
            GetZoneMarkerService().ClearAllZones();

            if (quest.CurrentDeliveryIndex < quest.DeliveryLocations.Count - 1)
            {
                quest.CurrentDeliveryIndex++;
                if (ShouldQuestUseWorldZoneMarkers(quest))
                {
                    GetZoneMarkerService().SpawnQuestZone(GetCurrentDeliveryLocation(), QuestZoneType.Delivery);
                }
                MarkQuestUiDirty();
                TryNotifyQuestUpdated();
            }
            else
            {
                GetAudioPresentationService().PlayQuestClip(deliveryClip);

                // Task 10.4: Tutorial integration
                if (TutorialManager.Instance != null)
                {
                    TutorialManager.Instance.OnCargoDelivered();
                }

                CompleteQuest(quest);
            }
        }

        public void OnCargoDestroyed()
        {
            if (!ShouldFailForCargoDestroyed(currentQuest))
            {
                return;
            }

            GetAudioPresentationService().PlayQuestClip(destroyedClip);
            FailQuest(currentQuest, "Cargo destroyed");
        }

        public void OnVehicleCollision(Collision collision)
        {
            if (currentQuest == null || currentQuest.Status != QuestStatus.Active)
            {
                return;
            }

            if (Time.time - lastCollisionTime < collisionDamageCooldown)
            {
                return;
            }

            float force = collision.impulse.magnitude / Time.fixedDeltaTime;
            if (force < collisionDamageThreshold)
            {
                return;
            }

            int previousCollisionPenalty = CalculateCollisionPenalty(currentQuest, true);

            lastCollisionTime = Time.time;

            // Record collision for penalty calculation
            bool isNpcCollision = SafeCompareTag(collision.gameObject, "NPC") ||
                                  SafeCompareTag(collision.gameObject, "Traffic") ||
                                  collision.gameObject.layer == LayerMask.NameToLayer("NPC");
            currentQuest.RecordCollision(isNpcCollision);
            int currentCollisionPenalty = CalculateCollisionPenalty(currentQuest, true);
            int deltaPenalty = Mathf.Max(0, currentCollisionPenalty - previousCollisionPenalty);
            if (deltaPenalty > 0)
            {
                string collisionLabel = isNpcCollision ? "NPC Carpisma!" : "Carpisma!";
                OnDrivingFeedback.Invoke(collisionLabel, -deltaPenalty);
            }

            if (currentQuest.CollisionCount >= maxCollisionsBeforeCancel)
            {
                FailQuest(currentQuest, $"Çok fazla çarpışma ({currentQuest.CollisionCount})");
                return;
            }

            // Apply damage to fragile cargo
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (currentQuest.HasPickedUpCargo && currentQuest.Cargo != null && currentQuest.Cargo.IsFragile && !debugInvincibleCargo)
#else
            if (currentQuest.HasPickedUpCargo && currentQuest.Cargo != null && currentQuest.Cargo.IsFragile)
#endif
            {
                if (Time.time - lastCargoPickupTime < Mathf.Max(0f, collisionDamageGraceAfterPickupSeconds))
                {
                    MarkQuestUiDirty();
                    return;
                }

                float damage = (force - collisionDamageThreshold) / collisionDamageDivider;
                damage = Mathf.Clamp(damage, 0f, Mathf.Max(0.1f, maxCargoDamagePerCollision));
                if (DriverProgressionSystem.Instance != null)
                {
                    damage *= DriverProgressionSystem.Instance.GetCargoDamageMultiplier();
                }

                if (damage <= 0.01f)
                {
                    MarkQuestUiDirty();
                    return;
                }

                damagingCollisionCount++;
                currentQuest.Cargo.TakeDamage(damage);
                cargoVisual?.PlayDamageEffect();
                GetAudioPresentationService().PlayQuestClip(damageClip);

                // Task 10.2: Play damage particle effect
                if (playerTransform != null)
                {
                    GetParticleEffectService().PlayParticleEffect(damageEffectPrefab, playerTransform.position);
                }

                if (currentQuest.Cargo.IsDestroyed())
                {
                    OnCargoDestroyed();
                    return;
                }
            }

            MarkQuestUiDirty();
        }

        public void OnHardBrakeDetected(float decelerationMetersPerSec)
        {
            if (currentQuest == null || currentQuest.Status != QuestStatus.Active)
            {
                return;
            }

            int previousHardBrakePenalty = CalculateHardBrakePenalty(currentQuest, true);
            currentQuest.RecordHardBrake();
            int currentHardBrakePenalty = CalculateHardBrakePenalty(currentQuest, true);
            int deltaPenalty = Mathf.Max(0, currentHardBrakePenalty - previousHardBrakePenalty);

            MarkQuestUiDirty();

            if (deltaPenalty <= 0)
            {
                return;
            }

            string label = decelerationMetersPerSec >= 14f ? "Cok Sert Fren!" : "Sert Fren!";
            OnDrivingFeedback.Invoke(label, -deltaPenalty);
        }

        public void OnDriftDetected(int driftScoreDelta)
        {
            if (currentQuest == null || currentQuest.Status != QuestStatus.Active || driftScoreDelta <= 0)
            {
                return;
            }

            int previousDriftBonus = CalculateDriftBonus(currentQuest, true);
            currentQuest.RecordDriftScore(driftScoreDelta);
            int currentDriftBonus = CalculateDriftBonus(currentQuest, true);
            int deltaBonus = Mathf.Max(0, currentDriftBonus - previousDriftBonus);

            MarkQuestUiDirty();

            if (deltaBonus > 0)
            {
                OnDrivingFeedback.Invoke("Drift Bonus!", deltaBonus);
            }
        }

        private static bool SafeCompareTag(GameObject gameObject, string tag)
        {
            if (gameObject == null || string.IsNullOrEmpty(tag))
            {
                return false;
            }

            // CompareTag logs an error when the target tag does not exist in TagManager.
            // Direct string compare avoids console spam for optional tags like NPC/Traffic.
            return string.Equals(gameObject.tag, tag, System.StringComparison.Ordinal);
        }

        private bool ShouldFailForCargoDestroyed(QuestData quest)
        {
            if (quest == null || !quest.HasPickedUpCargo || quest.Cargo == null || !quest.Cargo.IsFragile)
            {
                return false;
            }

            if (!quest.Cargo.IsDestroyed())
            {
                return false;
            }

            int requiredDamagingCollisions = Mathf.Max(1, minDamagingCollisionsBeforeCargoFail);
            return damagingCollisionCount >= requiredDamagingCollisions;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Enables or disables infinite quest time for debug builds.
        /// </summary>
        /// <param name="enabled">True to pause the quest timer, false to resume normal timing.</param>
        public void SetDebugInfiniteTime(bool enabled)
        {
            debugInfiniteTime = enabled;
        }

        /// <summary>
        /// Enables or disables fragile cargo damage in debug builds.
        /// </summary>
        /// <param name="enabled">True to ignore cargo damage, false to apply damage.</param>
        public void SetDebugInvincibleCargo(bool enabled)
        {
            debugInvincibleCargo = enabled;
        }

        /// <summary>
        /// Enables or disables quest gizmo rendering in debug builds.
        /// </summary>
        /// <param name="enabled">True to render gizmos, false to hide them.</param>
        public void SetDebugDrawGizmos(bool enabled)
        {
            debugDrawGizmos = enabled;
        }

        /// <summary>
        /// Enables or disables route line rendering in debug builds.
        /// </summary>
        /// <param name="enabled">True to render route lines, false to hide them.</param>
        public void SetDebugDrawRoute(bool enabled)
        {
            debugDrawRoute = enabled;
        }

        /// <summary>
        /// Enables or disables debug labels in debug builds.
        /// </summary>
        /// <param name="enabled">True to render labels, false to hide them.</param>
        public void SetDebugDrawLabels(bool enabled)
        {
            debugDrawLabels = enabled;
        }

        /// <summary>
        /// Teleports the player to the current quest objective for debug testing.
        /// </summary>
        public void TeleportToActiveObjective()
        {
            if (currentQuest == null)
            {
                return;
            }

            QuestLocation target = currentQuest.HasPickedUpCargo ? GetCurrentDeliveryLocation() : currentQuest.PickupLocation;
            TeleportPlayerToLocation(target);
        }

        /// <summary>
        /// Teleports the player to the pickup location for debug testing.
        /// </summary>
        public void TeleportToPickup()
        {
            TeleportPlayerToLocation(currentQuest?.PickupLocation);
        }

        /// <summary>
        /// Teleports the player to the current delivery location for debug testing.
        /// </summary>
        public void TeleportToDelivery()
        {
            TeleportPlayerToLocation(GetCurrentDeliveryLocation());
        }

        private void TeleportPlayerToLocation(QuestLocation location)
        {
            if (location == null || playerTransform == null)
            {
                return;
            }

            Vector3 targetPosition = location.Position + Vector3.up * 1.5f;
            playerTransform.position = targetPosition;

            Rigidbody rb = playerTransform.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        private void OnDrawGizmos()
        {
            if (!debugDrawGizmos)
            {
                return;
            }

            QuestData quest = currentQuest;
            if (quest == null)
            {
                return;
            }

            DrawLocationGizmo(quest.PickupLocation, debugPickupColor, "Pickup");

            if (quest.DeliveryLocations != null)
            {
                foreach (QuestLocation delivery in quest.DeliveryLocations)
                {
                    DrawLocationGizmo(delivery, debugDeliveryColor, "Delivery");
                }
            }

            if (debugDrawRoute)
            {
                DrawRouteGizmos(quest);
            }

#if UNITY_EDITOR
            if (debugDrawLabels)
            {
                Vector3 labelAnchor = quest.PickupLocation != null ? quest.PickupLocation.Position : transform.position;
                string status = $"{quest.QuestName} [{quest.Status}]";
                string timeInfo = debugInfiniteTime ? "Time: Infinite" : $"Time: {quest.GetFormattedTimeRemaining()}";
                UnityEditor.Handles.Label(labelAnchor + Vector3.up * 4f, $"{status}\n{timeInfo}");
            }
#endif
        }

        private void DrawLocationGizmo(QuestLocation location, Color color, string labelPrefix)
        {
            if (location == null)
            {
                return;
            }

            Gizmos.color = color;
            float radius = Mathf.Max(0.5f, location.TriggerRadius);
            Gizmos.DrawWireSphere(location.Position, radius);

#if UNITY_EDITOR
            if (debugDrawLabels && !string.IsNullOrWhiteSpace(location.LocationName))
            {
                string label = $"{labelPrefix}: {location.LocationName}";
                UnityEditor.Handles.Label(location.Position + Vector3.up * (radius + 0.5f), label);
            }
#endif
        }

        private void DrawRouteGizmos(QuestData quest)
        {
            if (quest.PickupLocation == null || quest.DeliveryLocations == null || quest.DeliveryLocations.Count == 0)
            {
                return;
            }

            Gizmos.color = debugRouteColor;
            Vector3 current = quest.PickupLocation.Position;

            foreach (QuestLocation delivery in quest.DeliveryLocations)
            {
                if (delivery == null)
                {
                    continue;
                }

                Gizmos.DrawLine(current, delivery.Position);
                current = delivery.Position;
            }
        }
#endif

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

        private QuestLocation GetCurrentDeliveryLocation() => GetCurrentDeliveryLocation(currentQuest);

        private static bool ShouldQuestHandlePickup(QuestData quest)
        {
            return quest != null && quest.PickupAuthority != QuestPickupAuthority.PhysicalBox;
        }

        private static bool ShouldQuestUseWorldZoneMarkers(QuestData quest) =>
            ShouldQuestHandlePickup(quest);

        private void MarkQuestUiDirty() => questUiUpdateTracker.MarkDirty();

        private void TryNotifyQuestUpdated()
        {
            if (questUiUpdateTracker.TryCaptureChanged(currentQuest))
                OnQuestUpdated.Invoke(currentQuest);
        }

        private void CacheQuestUiState(QuestData quest) => questUiUpdateTracker.Capture(quest);

        public QuestSaveData GetSaveData()
        {
            return QuestSaveRestoreService.BuildSaveData(activeQuests, availableQuests, completedQuests, currentQuest);
        }

        public void LoadSaveData(QuestSaveData data)
        {
            if (!QuestSaveRestoreService.TryRestoreSaveData(data, out QuestSaveRestoreState restoredState))
            {
                return;
            }

            activeQuests = restoredState.ActiveQuests;
            availableQuests = restoredState.AvailableQuests;
            completedQuests = restoredState.CompletedQuests;
            currentQuest = restoredState.CurrentQuest;

            QuestSaveRestoreService.RestoreLoadedQuestState(
                restoredState,
                quests =>
                {
                    List<QuestData> questsWithWorldMarkers = new List<QuestData>();
                    if (quests != null)
                    {
                        foreach (QuestData quest in quests)
                        {
                            if (ShouldQuestUseWorldZoneMarkers(quest))
                            {
                                questsWithWorldMarkers.Add(quest);
                            }
                        }
                    }

                    GetZoneMarkerService().RestoreQuestMarkers(questsWithWorldMarkers, quest => GetCurrentDeliveryLocation(quest));
                },
                quest =>
                {
                    QuestData previousRestore = restoringQuest;
                    restoringQuest = quest;
                    try
                    {
                        OnQuestStarted.Invoke(quest);
                        if (!ReferenceEquals(currentQuest, quest) || !IsActiveQuest(quest))
                            return;

                        CacheQuestUiState(quest);
                        OnQuestUpdated.Invoke(quest);
                    }
                    finally
                    {
                        restoringQuest = previousRestore;
                    }
                });
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

        private static QuestLocation GetDeliveryStop(QuestData quest, int index) =>
            quest?.DeliveryLocations != null && index >= 0 && index < quest.DeliveryLocations.Count
                ? quest.DeliveryLocations[index]
                : null;

        public RewardPenaltyBreakdown GetQuestRewardPreview(QuestData quest)
        {
            return QuestRewardService.GetQuestRewardPreview(quest, consecutiveSuccesses, BuildRewardConfig());
        }

        private RewardPenaltyBreakdown CalculateRewardPenaltyBreakdown(QuestData quest, float appliedStreakMultiplier, bool useRuntimePenalties)
        {
            return QuestRewardService.CalculateRewardPenaltyBreakdown(
                quest,
                appliedStreakMultiplier,
                useRuntimePenalties,
                LastFailureReason,
                BuildRewardConfig());
        }

        private float CalculateStreakMultiplier(int successCountAfterCompletion)
        {
            return QuestRewardService.CalculateStreakMultiplier(successCountAfterCompletion, BuildRewardConfig());
        }

        private int CalculateFailurePenalty(QuestData quest, string reason)
        {
            return QuestRewardService.CalculateFailurePenalty(quest, reason, BuildRewardConfig());
        }

        private void TryApplyFailurePenalty(int amount)
        {
            QuestRewardService.TryApplyFailurePenalty(amount);
        }

        private void TryAwardRewards(QuestData quest, int reward)
        {
            QuestRewardService.TryAwardRewards(quest, reward);
        }

        private int CalculateCollisionPenalty(QuestData quest, bool useRuntimeState)
        {
            return QuestRewardService.CalculateCollisionPenalty(quest, useRuntimeState, BuildRewardConfig());
        }

        private int CalculateHardBrakePenalty(QuestData quest, bool useRuntimeState)
        {
            return QuestRewardService.CalculateHardBrakePenalty(quest, useRuntimeState, BuildRewardConfig());
        }

        private int CalculateDriftBonus(QuestData quest, bool useRuntimeState)
        {
            return QuestRewardService.CalculateDriftBonus(quest, useRuntimeState, BuildRewardConfig());
        }

        private QuestRewardConfig BuildRewardConfig()
        {
            return new QuestRewardConfig(
                questSystemSettings,
                payoutAverageSpeedMetersPerSecond,
                minTimePressureMultiplier,
                maxTimePressureMultiplier,
                fragileCargoDifficultyBonus,
                heavyCargoWeightStartKg,
                heavyCargoWeightMaxBonus,
                mediumRiskMultiplier,
                hardRiskMultiplier,
                expertRiskMultiplier,
                expressRiskBonus,
                fragileRiskBonus,
                multiStopRiskBonus,
                collisionPenaltyBase,
                collisionPenaltyStep,
                npcCollisionPenaltyBase,
                npcCollisionPenaltyStep,
                hardBrakePenaltyBase,
                hardBrakePenaltyStep,
                delayPenaltyStartsAtRemainingRatio,
                delayPenaltyMaxRatio,
                cargoDamagePenaltyPerPercent,
                timeExpiredFailurePenalty,
                cargoDestroyedFailurePenalty,
                enableDriftBonus,
                driftPointsPerRewardStep,
                driftRewardStepAmount,
                maxDriftBonusReward,
                streakMultiplierIncrement,
                maxStreakMultiplier);
        }

        #region Task 10.1: Audio & Music System

        /// <summary>
        /// Task 10.1: Plays a level up sound effect (can be called from PlayerProgressionManager)
        /// </summary>
        public void PlayLevelUpSound()
        {
            GetAudioPresentationService().PlayLevelUpSound();
        }

        /// <summary>
        /// Task 10.1: Sets the music volume (0-1)
        /// </summary>
        public void SetMusicVolume(float volume)
        {
            GetAudioPresentationService().SetMusicVolume(volume);
        }

        /// <summary>
        /// Task 10.1: Sets the SFX volume (0-1)
        /// </summary>
        public void SetSFXVolume(float volume)
        {
            GetAudioPresentationService().SetSfxVolume(volume);
        }

        #endregion

        #region Task 10.2: Particle Effects System

        /// <summary>
        /// Task 10.2: Plays a level up particle effect (can be called from PlayerProgressionManager)
        /// </summary>
        public void PlayLevelUpEffect(Vector3 position)
        {
            GetParticleEffectService().PlayLevelUpEffect(position);
        }

        #endregion

        #region Task 9.5: Quest Pool Refresh System

        /// <summary>
        /// Task 9.5: Refreshes the available quest pool by removing old quests and generating new ones
        /// </summary>
        public void RefreshAvailableQuests()
        {
            // Remove old quests that haven't been accepted
            int removedCount = availableQuests.Count;
            availableQuests.Clear();

            // Calculate how many quests to generate to reach target pool size
            int questsToGenerate = targetQuestPoolSize;

            Debug.Log($"[QuestManager] Refreshing quest pool. Removed {removedCount} old quests. Generating {questsToGenerate} new quests.");

            // Generate new quests with variety
            GenerateAvailableQuests(questsToGenerate);

            // Notify player
            Debug.Log("[QuestManager] New deliveries available!");

            // You can invoke an event here for UI notification if needed
            // OnQuestPoolRefreshed?.Invoke();
        }

        /// <summary>
        /// Task 9.5: Manually refreshes the quest pool (with cooldown to prevent spam)
        /// </summary>
        /// <returns>True if refresh was successful, false if on cooldown</returns>
        public bool ManualRefreshQuests()
        {
            float timeSinceLastManualRefresh = Time.time - lastManualRefreshTime;

            if (timeSinceLastManualRefresh < manualRefreshCooldown)
            {
                float remainingCooldown = manualRefreshCooldown - timeSinceLastManualRefresh;
                Debug.LogWarning($"[QuestManager] Manual refresh on cooldown. Wait {remainingCooldown:F0} seconds.");
                return false;
            }

            lastManualRefreshTime = Time.time;
            RefreshAvailableQuests();

            // Reset the automatic refresh timer
            timeSinceLastRefresh = 0f;

            Debug.Log("[QuestManager] Quest pool manually refreshed.");
            return true;
        }

        /// <summary>
        /// Gets the remaining cooldown time for manual refresh
        /// </summary>
        /// <returns>Seconds remaining, or 0 if ready</returns>
        public float GetManualRefreshCooldown()
        {
            float timeSinceLastManualRefresh = Time.time - lastManualRefreshTime;
            float remaining = Mathf.Max(0f, manualRefreshCooldown - timeSinceLastManualRefresh);
            return remaining;
        }

        /// <summary>
        /// Checks if manual refresh is available (not on cooldown)
        /// </summary>
        public bool CanManuallyRefresh()
        {
            return GetManualRefreshCooldown() <= 0f;
        }

        /// <summary>
        /// Gets the time remaining until automatic refresh
        /// </summary>
        public float GetTimeUntilAutoRefresh()
        {
            return Mathf.Max(0f, questRefreshInterval - timeSinceLastRefresh);
        }

        #endregion

        #region Task 9.3: Multi-Stop Quest Generation

        /// <summary>
        /// Task 9.3: Generates a multi-stop delivery quest with route optimization
        /// </summary>
        public QuestData GenerateMultiStopQuest(int stopCount, QuestDifficulty difficulty)
        {
            if (DriverProgressionSystem.Instance != null &&
                !DriverProgressionSystem.Instance.IsQuestTypeUnlocked(QuestType.MultiStopDelivery))
            {
                return GenerateQuestByDifficulty(difficulty);
            }

            if (stopCount < 2 || stopCount > 4)
            {
                Debug.LogWarning($"[QuestManager] Invalid stop count {stopCount}. Must be 2-4.");
                return null;
            }

            CargoData cargo = (cargoLibrary != null ? cargoLibrary.GetRandomCargo() : null)
                ?? new CargoData("Generic Cargo", 100f, false, "Standard cargo for multi-stop delivery.");

            // Generate pickup location
            QuestLocation pickup = GetLocationAssignmentService().GenerateRandomLocation("Pickup");
            if (pickup == null)
            {
                Debug.LogWarning("[QuestManager] Failed to generate pickup location for multi-stop quest.");
                return null;
            }

            // Generate delivery locations
            List<QuestLocation> deliveryLocations = new List<QuestLocation>();
            for (int i = 0; i < stopCount; i++)
            {
                QuestLocation delivery = GetLocationAssignmentService().GenerateRandomLocation($"Delivery {i + 1}");
                if (delivery != null)
                {
                    deliveryLocations.Add(delivery);
                }
            }

            if (deliveryLocations.Count < stopCount)
            {
                Debug.LogWarning($"[QuestManager] Only generated {deliveryLocations.Count} of {stopCount} delivery locations.");
                // Continue with fewer stops if we got at least one
                if (deliveryLocations.Count == 0)
                {
                    return null;
                }
            }

            // Optimize route using nearest-neighbor algorithm
            List<QuestLocation> optimizedRoute = QuestRouteOrdering.Optimize(pickup, deliveryLocations);

            // Calculate total route distance
            float totalDistance = 0f;
            Vector3 lastPos = pickup.Position;
            foreach (QuestLocation loc in optimizedRoute)
            {
                totalDistance += Vector3.Distance(lastPos, loc.Position);
                lastPos = loc.Position;
            }

            QuestData quest = CreateGeneratedQuest($"Multi-Stop: {optimizedRoute.Count} Deliveries",
                $"Transport {cargo.CargoName} to {optimizedRoute.Count} locations. Complete all deliveries in order.",
                QuestType.MultiStopDelivery, difficulty, cargo, pickup, optimizedRoute, totalDistance);

            QuestLogger.Log($"[QuestManager] Generated multi-stop quest: {optimizedRoute.Count} stops, {totalDistance:F0}m total distance");

            return quest;
        }

        #endregion

        #region Task 9.4: Special Quest Types

        /// <summary>
        /// Task 9.4: Generates an express delivery quest with tight time limit and high reward
        /// </summary>
        public QuestData GenerateExpressDelivery()
        {
            if (DriverProgressionSystem.Instance != null &&
                !DriverProgressionSystem.Instance.IsQuestTypeUnlocked(QuestType.ExpressDelivery))
            {
                return GenerateQuestByDifficulty(QuestDifficulty.Medium);
            }

            // Pick difficulty weighted toward medium/hard
            QuestDifficulty difficulty = UnityEngine.Random.value < 0.5f ? QuestDifficulty.Medium : QuestDifficulty.Hard;

            CargoData cargo = (cargoLibrary != null ? cargoLibrary.GetRandomCargo() : null)
                ?? new CargoData("Express Package", 50f, false, "Time-critical express delivery.");
            Vector2 range = difficulty == QuestDifficulty.Medium
                ? new Vector2(2000f, 4000f) : new Vector2(3000f, 6000f);
            if (!TryGenerateLocationPair("Express Pickup", "Express Delivery", range, false,
                out var pickup, out var delivery, out float actualDistance))
            {
                Debug.LogWarning("[QuestManager] Failed to generate express delivery locations.");
                return null;
            }

            QuestData quest = CreateGeneratedQuest("EXPRESS: Rush Delivery",
                $"Express delivery of {cargo.CargoName}. Very tight deadline - drive fast!",
                QuestType.ExpressDelivery, difficulty, cargo, pickup,
                new List<QuestLocation> { delivery }, actualDistance);

            QuestLogger.Log($"[QuestManager] Generated EXPRESS delivery: {actualDistance:F0}m, {quest.TimeLimit:F0}s, ${quest.BaseReward}");

            return quest;
        }

        /// <summary>
        /// Task 9.4: Generates a fragile delivery quest with damage penalties
        /// </summary>
        public QuestData GenerateFragileDelivery()
        {
            if (DriverProgressionSystem.Instance != null &&
                !DriverProgressionSystem.Instance.IsQuestTypeUnlocked(QuestType.FragileDelivery))
            {
                return GenerateQuestByDifficulty(QuestDifficulty.Medium);
            }

            // Pick difficulty
            QuestDifficulty difficulty = UnityEngine.Random.value < 0.5f ? QuestDifficulty.Easy : QuestDifficulty.Medium;

            CargoData cargo = cargoLibrary != null ? cargoLibrary.GetCargoByFragility(true) : null;
            if (cargo == null)
                cargo = new CargoData("Fragile Electronics", 80f, true, "Handle with extreme care - easily damaged.")
                { CargoHealth = 100f };

            if (!TryGenerateLocationPair("Fragile Pickup", "Fragile Delivery",
                QuestGenerationRules.StandardDistanceRange(difficulty), false,
                out var pickup, out var delivery, out float actualDistance))
            {
                Debug.LogWarning("[QuestManager] Failed to generate fragile delivery locations.");
                return null;
            }

            QuestData quest = CreateGeneratedQuest("FRAGILE: Handle With Care",
                $"Delicate {cargo.CargoName} - avoid collisions! Bonus for zero damage.",
                QuestType.FragileDelivery, difficulty, cargo, pickup,
                new List<QuestLocation> { delivery }, actualDistance);

            QuestLogger.Log($"[QuestManager] Generated FRAGILE delivery: {actualDistance:F0}m, fragile cargo with health bonus");

            return quest;
        }

        /// <summary>
        /// Task 9.4: Generates a time trial quest with very tight time limit
        /// </summary>
        public QuestData GenerateTimeTrial()
        {
            // Time trials are challenging
            QuestDifficulty difficulty = UnityEngine.Random.value < 0.5f ? QuestDifficulty.Hard : QuestDifficulty.Expert;

            // Pick light cargo
            CargoData cargo = new CargoData("Time Trial Package", 50f, false, "Speed is everything!");

            if (!TryGenerateLocationPair("Trial Start", "Trial Finish",
                QuestGenerationRules.StandardDistanceRange(difficulty), false,
                out var pickup, out var delivery, out float actualDistance))
            {
                Debug.LogWarning("[QuestManager] Failed to generate time trial locations.");
                return null;
            }

            QuestData quest = CreateGeneratedQuest("TIME TRIAL: Speed Run",
                "Pure speed challenge - complete the delivery as fast as possible! Reward scales with remaining time.",
                QuestType.TimeTrial, difficulty, cargo, pickup,
                new List<QuestLocation> { delivery }, actualDistance);

            QuestLogger.Log($"[QuestManager] Generated TIME TRIAL: {actualDistance:F0}m, extreme time pressure!");

            return quest;
        }

        /// <summary>
        /// Task 9.4: Generates a quest with weighted quest type selection
        /// </summary>
        public QuestData GenerateRandomQuestWithTypes(QuestDifficulty difficulty)
        {
            float typeRoll = UnityEngine.Random.value;
            bool expressUnlocked = DriverProgressionSystem.Instance == null ||
                                   DriverProgressionSystem.Instance.IsQuestTypeUnlocked(QuestType.ExpressDelivery);
            bool fragileUnlocked = DriverProgressionSystem.Instance == null ||
                                   DriverProgressionSystem.Instance.IsQuestTypeUnlocked(QuestType.FragileDelivery);
            bool multiStopUnlocked = DriverProgressionSystem.Instance == null ||
                                     DriverProgressionSystem.Instance.IsQuestTypeUnlocked(QuestType.MultiStopDelivery);

            // Quest type selection logic:
            // 60% Standard Delivery
            // 20% Express Delivery
            // 15% Fragile Delivery
            // 5% Multi-Stop

            if (typeRoll < 0.60f)
            {
                // Standard Delivery (use existing GenerateQuestByDifficulty)
                return GenerateQuestByDifficulty(difficulty);
            }
            else if (typeRoll < 0.80f && expressUnlocked)
            {
                // Express Delivery (20%)
                return GenerateExpressDelivery();
            }
            else if (typeRoll < 0.95f && fragileUnlocked)
            {
                // Fragile Delivery (15%)
                return GenerateFragileDelivery();
            }
            else if (multiStopUnlocked)
            {
                // Multi-Stop Delivery (5%)
                int stopCount = UnityEngine.Random.Range(2, 4); // 2-3 stops
                return GenerateMultiStopQuest(stopCount, difficulty);
            }

            return GenerateQuestByDifficulty(difficulty);
        }

        #endregion

        #region Daily Challenge System

        /// <summary>
        /// Checks if a new daily challenge should be generated
        /// </summary>
        private void CheckDailyChallenge()
        {
            string todayDate = DateTime.Now.Date.ToString("yyyy-MM-dd");

            // Check if we need to generate a new daily challenge
            if (string.IsNullOrEmpty(lastDailyChallengeDate) || lastDailyChallengeDate != todayDate)
            {
                GenerateDailyChallenge();
            }
            else if (dailyChallenge != null)
            {
                // Daily challenge exists and is for today
                Debug.Log($"[QuestManager] Daily challenge already available: {dailyChallenge.QuestName}");
            }
        }

        /// <summary>
        /// Generates a new daily challenge quest
        /// </summary>
        public void GenerateDailyChallenge()
        {
            if (questDatabase == null)
            {
                Debug.Log("[QuestManager] Daily challenge skipped: QuestDatabase not assigned.");
                return;
            }

            // Get player level for appropriate difficulty
            int playerLevel = 1;
            if (PlayerProgressionManager.Instance != null)
            {
                playerLevel = PlayerProgressionManager.Instance.CurrentLevel;
            }

            // Generate a challenging quest (prefer Hard or Expert difficulty)
            QuestDifficulty difficulty = playerLevel < 10 ? QuestDifficulty.Medium :
                                        (playerLevel < 20 ? QuestDifficulty.Hard : QuestDifficulty.Expert);

            QuestData quest = questDatabase.GenerateRandomQuest(difficulty);

            if (quest == null)
            {
                quest = questDatabase.GetRandomQuest();
            }

            if (quest == null)
            {
                Debug.LogWarning("[QuestManager] Failed to generate daily challenge");
                return;
            }

            // Assign locations
            bool assigned = GetLocationAssignmentService().AssignQuestLocations(quest);
            if (!assigned)
            {
                Debug.LogWarning("[QuestManager] Failed to assign locations for daily challenge");
                return;
            }

            // Mark as daily challenge and enhance rewards
            quest.QuestName = "DAILY: " + quest.QuestName;
            quest.BaseReward = Mathf.RoundToInt(quest.BaseReward * dailyChallengeRewardMultiplier);
            quest.BonusReward = Mathf.RoundToInt(quest.BonusReward * dailyChallengeRewardMultiplier);
            quest.XPReward = Mathf.RoundToInt(quest.XPReward * dailyChallengeRewardMultiplier);

            // Make it more challenging - tighter time limit
            quest.TimeLimit *= 0.8f;
            quest.TimeRemaining = quest.TimeLimit;

            // Add special requirement description
            quest.QuestDescription += "\n\nDaily Challenge - 2x Rewards! Complete before midnight.";

            dailyChallenge = quest;
            lastDailyChallengeDate = DateTime.Now.Date.ToString("yyyy-MM-dd");

            OnDailyChallengeGenerated.Invoke(dailyChallenge);

            Debug.Log($"[QuestManager] Daily challenge generated: {dailyChallenge.QuestName} (2x rewards)");
        }

        /// <summary>
        /// Checks if the daily challenge is available
        /// </summary>
        /// <returns>True if daily challenge is available and not completed</returns>
        public bool IsDailyChallengeAvailable()
        {
            return dailyChallenge != null &&
                   dailyChallenge.Status == QuestStatus.NotStarted &&
                   !string.IsNullOrEmpty(lastDailyChallengeDate) &&
                   lastDailyChallengeDate == DateTime.Now.Date.ToString("yyyy-MM-dd");
        }

        /// <summary>
        /// Accepts the daily challenge quest
        /// </summary>
        /// <returns>True if successfully accepted</returns>
        public bool AcceptDailyChallenge()
        {
            if (!IsDailyChallengeAvailable())
            {
                Debug.LogWarning("[QuestManager] Daily challenge is not available");
                return false;
            }

            return TryAcceptQuest(dailyChallenge);
        }

        /// <summary>
        /// Gets the time remaining until daily challenge resets
        /// </summary>
        /// <returns>Formatted time string until midnight</returns>
        public string GetTimeUntilDailyChallengeReset()
        {
            DateTime now = DateTime.Now;
            DateTime midnight = now.Date.AddDays(1);
            TimeSpan timeUntilReset = midnight - now;

            int hours = timeUntilReset.Hours;
            int minutes = timeUntilReset.Minutes;

            return $"{hours:D2}:{minutes:D2}";
        }

        #endregion

        #region External Quest Integration

        /// <summary>
        /// Add a quest to available quests (for external systems like DeliveryManager)
        /// </summary>
        public void AddAvailableQuest(QuestData quest)
        {
            if (quest == null)
            {
                Debug.LogError("[QuestManager] Cannot add null quest");
                return;
            }

            if (!availableQuests.Contains(quest))
            {
                availableQuests.Add(quest);
                Debug.Log($"[QuestManager] Added quest: {quest.QuestName}");
            }
        }

        /// <summary>
        /// Start a quest immediately (for external systems)
        /// </summary>
        public void StartQuest(QuestData quest)
        {
            TryStartExternalQuest(quest);
        }

        internal bool TryStartExternalQuest(QuestData quest)
        {
            if (quest == null)
            {
                Debug.LogError("[QuestManager] Cannot start null quest");
                return false;
            }

            // External physical-box quests may not have delivery stops yet.
            // Do not reset their prepared timer/cargo data through QuestData.StartQuest.
            if (!CanActivateQuest(quest))
                return false;

            if (startupInitCoroutine != null)
            {
                StopCoroutine(startupInitCoroutine);
                startupInitCoroutine = null;
            }

            quest.Status = QuestStatus.Active;
            RegisterStartedQuest(quest);
            IsSaveReady = true;

            // Show pickup marker
            if (quest.PickupLocation != null)
            {
                quest.PickupLocation.ShowMarker();
            }

            // Notify listeners so UI/HUD updates immediately for externally started quests.
            OnQuestStarted?.Invoke(quest);
            if (!IsActiveQuest(quest))
                return false;

            OnQuestUpdated?.Invoke(quest);
            MarkQuestUiDirty();

            Debug.Log($"[QuestManager] Started quest: {quest.QuestName}");
            return IsActiveQuest(quest);
        }

        #endregion
    }
}
