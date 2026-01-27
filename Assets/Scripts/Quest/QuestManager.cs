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
        private bool timeWarningPlayed = false;
        private bool isCrossfading = false;

        [Header("Cargo Visuals")]
        [SerializeField] private CargoVisual cargoVisual;

        [Header("Task 10.2: Particle Effects")]
        [SerializeField] private GameObject questMarkerParticlePrefab;
        [SerializeField] private GameObject pickupEffectPrefab;
        [SerializeField] private GameObject deliveryEffectPrefab;
        [SerializeField] private GameObject damageEffectPrefab;
        [SerializeField] private GameObject levelUpEffectPrefab;
        [SerializeField] private int particlePoolSize = 10;
        private Queue<GameObject> particlePool = new Queue<GameObject>();

        [Header("Fragile Cargo Damage")]
        [SerializeField] private float collisionDamageThreshold = 10000f;
        [SerializeField] private float collisionDamageDivider = 1000f;
        [SerializeField] private float collisionDamageCooldown = 0.25f;
        private float lastCollisionTime;

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

        private int lastTimeRemainingSeconds = -1;
        private int lastDeliveryIndex = -1;
        private bool lastHasPickedUpCargo = false;
        private float lastCargoHealth = -1f;
        private string lastQuestId = string.Empty;
        private bool questUiDirty = true;

        public UnityEvent<QuestData> OnQuestStarted = new UnityEvent<QuestData>();
        public UnityEvent<QuestData> OnQuestCompleted = new UnityEvent<QuestData>();
        public UnityEvent<QuestData> OnQuestFailed = new UnityEvent<QuestData>();
        public UnityEvent<QuestData> OnQuestUpdated = new UnityEvent<QuestData>();
        public UnityEvent<QuestData> OnDailyChallengeGenerated = new UnityEvent<QuestData>();

        public QuestData CurrentQuest => currentQuest;
        public IReadOnlyList<QuestData> ActiveQuests => activeQuests;
        public IReadOnlyList<QuestData> AvailableQuests => availableQuests;
        public IReadOnlyList<QuestData> CompletedQuests => completedQuests;
        public Transform PlayerTransform => playerTransform;
        public string LastFailureReason { get; private set; } = string.Empty;
        public int LastCompletionReward { get; private set; }
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
            InitializeParticlePool();

            if (markerPool != null)
            {
                markerPool.Prewarm(pickupMarkerPrefab, markerPoolSize);
                markerPool.Prewarm(deliveryMarkerPrefab, markerPoolSize);
            }

            // Task 10.1: Start exploration music
            if (musicSource != null && explorationMusicClip != null)
            {
                musicSource.clip = explorationMusicClip;
                musicSource.loop = true;
                musicSource.Play();
            }

            // Try to load saved game
            if (SaveManager.Instance != null)
            {
                GameSaveData saveData = SaveManager.Instance.LoadGame();

                if (saveData != null && saveData.QuestData != null)
                {
                    // Load quest data from save
                    LoadSaveData(saveData.QuestData);
                    Debug.Log("[QuestManager] Loaded quest data from save file.");
                }
                else
                {
                    // No save found - generate initial quests
                    Debug.Log("[QuestManager] No save found. Generating initial quests.");
                    GenerateAvailableQuests(5);
                }
            }
            else
            {
                // SaveManager not found - generate initial quests
                Debug.LogWarning("[QuestManager] SaveManager not found. Generating initial quests.");
                GenerateAvailableQuests(5);
            }

            // Check and generate daily challenge if needed
            CheckDailyChallenge();
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
            {
                currentQuest.UpdateTimer(Time.deltaTime);
            }
#else
            currentQuest.UpdateTimer(Time.deltaTime);
#endif

            // Task 10.1: Time Warning Audio
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!debugInfiniteTime && currentQuest.TimeRemaining < timeWarningThreshold && !timeWarningPlayed)
#else
            if (currentQuest.TimeRemaining < timeWarningThreshold && !timeWarningPlayed)
#endif
            {
                PlayTimeWarning();
                timeWarningPlayed = true;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!debugInfiniteTime && currentQuest.IsTimeExpired())
#else
            if (currentQuest.IsTimeExpired())
#endif
            {
                FailQuest(currentQuest, "Time expired");
                return;
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
            if (!debugInvincibleCargo && currentQuest.Cargo != null && currentQuest.Cargo.IsFragile && currentQuest.Cargo.IsDestroyed())
#else
            if (currentQuest.Cargo != null && currentQuest.Cargo.IsFragile && currentQuest.Cargo.IsDestroyed())
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
            
            // Determine difficulty based on level (simple logic)
            QuestDifficulty difficulty = QuestDifficulty.Easy;
            if (GameSettings.Instance != null)
            {
                difficulty = GameSettings.Instance.ResolveQuestDifficulty(playerLevel);
            }
            else
            {
                if (playerLevel >= 5) difficulty = QuestDifficulty.Medium;
                if (playerLevel >= 15) difficulty = QuestDifficulty.Hard;
                if (playerLevel >= 30) difficulty = QuestDifficulty.Expert;
            }

            for (int i = 0; i < count; i++)
            {
                QuestData quest = null;
                
                // 50% chance to use procedural generation if not daily challenge
                // or if database is missing/fails
                bool useProcedural = UnityEngine.Random.value > 0.5f;

                if (!useProcedural && questDatabase != null)
                {
                     quest = questDatabase.GenerateRandomQuestForLevel(playerLevel);
                     if (quest == null) quest = questDatabase.GetRandomQuest();
                     
                     if (quest != null)
                     {
                         // For template quests, we still need to assign locations
                         quest.Status = QuestStatus.NotStarted;
                         if (!AssignQuestLocations(quest))
                         {
                             quest = null; // Failed to assign locations
                         }
                     }
                }
                
                if (quest == null)
                {
                    // Fallback to procedural generation (Task 9.2)
                    // Vary difficulty slightly
                    QuestDifficulty targetDiff = difficulty;
                    float r = UnityEngine.Random.value;
                    if (r < 0.2f && targetDiff > QuestDifficulty.Easy) targetDiff--;
                    else if (r > 0.8f && targetDiff < QuestDifficulty.Expert) targetDiff++;
                    
                    quest = GenerateQuestByDifficulty(targetDiff);
                }

                if (quest == null)
                {
                    continue;
                }

                availableQuests.Add(quest);
            }
        }
        
        /// <summary>
        /// Task 9.2: Generates a quest procedurally based on difficulty parameters.
        /// </summary>
        public QuestData GenerateQuestByDifficulty(QuestDifficulty difficulty)
        {
            // 1. Pick Cargo
            CargoData cargo = null;
            if (cargoLibrary != null)
            {
                // Simple logic: prefer fragile/heavy for harder quests?
                // For now random is fine, or we could add filters to CargoLibrary later.
                cargo = cargoLibrary.GetRandomCargo();
            }
            
            if (cargo == null)
            {
                cargo = new CargoData("Generic Cargo", 100f, false, "Standard cargo.");
            }

            // 2. Determine Distance and Time Settings
            Vector2 distanceRange = questSystemSettings != null
                ? questSystemSettings.GetDistanceRange(difficulty)
                : (difficulty == QuestDifficulty.Medium ? new Vector2(2000f, 4000f) :
                   difficulty == QuestDifficulty.Hard ? new Vector2(4000f, 6000f) :
                   difficulty == QuestDifficulty.Expert ? new Vector2(6000f, 10000f) :
                   new Vector2(1000f, 2000f));
            float minDistance = distanceRange.x;
            float maxDistance = distanceRange.y;
            float timeMultiplier = questSystemSettings != null
                ? questSystemSettings.GetTimeMultiplier(difficulty)
                : (difficulty == QuestDifficulty.Easy ? 2.0f :
                   difficulty == QuestDifficulty.Medium ? 1.5f :
                   difficulty == QuestDifficulty.Hard ? 1.2f : 1.0f);
            int difficultyBonus = questSystemSettings != null
                ? questSystemSettings.GetDifficultyBonus(difficulty)
                : (difficulty == QuestDifficulty.Medium ? 100 :
                   difficulty == QuestDifficulty.Hard ? 250 :
                   difficulty == QuestDifficulty.Expert ? 500 : 0);

            // 3. Find Locations
            QuestLocation pickup = null;
            QuestLocation delivery = null;
            float actualDistance = 0f;

            // Try multiple times to find locations matching the distance criteria
            for (int i = 0; i < 15; i++)
            {
                pickup = GenerateRandomLocation("Pickup");
                delivery = GenerateRandomLocation("Delivery");
                
                if (pickup == null || delivery == null) continue;

                actualDistance = Vector3.Distance(pickup.Position, delivery.Position);
                if (actualDistance >= minDistance && actualDistance <= maxDistance)
                {
                    break; // Found valid pair
                }
                
                // Failed, clear and retry
                pickup = null;
                delivery = null;
            }

            // Fallback: If strict distance generation failed, just take whatever valid pair we can get
            if (pickup == null || delivery == null)
            {
                pickup = GenerateRandomLocation("Pickup");
                delivery = GenerateRandomLocation("Delivery");
                
                // Ensure they are at least somewhat valid (min distance check inside AreLocationsValid logic, 
                // but we are bypassing AssignQuestLocations helper here, so we do manual check or just accept)
                if (pickup != null && delivery != null)
                {
                    actualDistance = Vector3.Distance(pickup.Position, delivery.Position);
                }
                else
                {
                    // Total failure to generate locations
                    return null;
                }
            }

            // 4. Create QuestData
            QuestData quest = new QuestData
            {
                QuestID = System.Guid.NewGuid().ToString(),
                QuestName = $"{difficulty} Delivery",
                QuestDescription = $"Transport {cargo.CargoName} from {pickup.LocationName} to {delivery.LocationName}.",
                QuestType = QuestType.StandardDelivery,
                Difficulty = difficulty,
                Status = QuestStatus.NotStarted,
                PickupLocation = pickup,
                DeliveryLocations = new List<QuestLocation> { delivery },
                Cargo = cargo
            };
            
            // Assign markers
            if (quest.PickupLocation.VisualMarker == null) quest.PickupLocation.VisualMarker = pickupMarkerPrefab;
            foreach(var loc in quest.DeliveryLocations) if (loc.VisualMarker == null) loc.VisualMarker = deliveryMarkerPrefab;

            // 5. Calculate Stats
            float avgSpeed = 11f; // ~40 km/h in m/s
            // Ensure time limit is reasonable (at least 60 seconds)
            float minTimeLimit = questSystemSettings != null ? questSystemSettings.MinimumTimeLimit : 60f;
            quest.TimeLimit = Mathf.Max(minTimeLimit, (actualDistance / avgSpeed) * timeMultiplier);
            quest.TimeRemaining = quest.TimeLimit;
            
            // Reward calculation
            float rewardPerMeter = questSystemSettings != null ? questSystemSettings.BaseRewardPerMeter : 0.1f;
            float bonusMultiplier = questSystemSettings != null ? questSystemSettings.BonusRewardMultiplier : 0.5f;
            quest.BaseReward = Mathf.RoundToInt((actualDistance * rewardPerMeter) + difficultyBonus); 
            quest.BonusReward = Mathf.RoundToInt(quest.BaseReward * bonusMultiplier);
            quest.BonusTimeThreshold = 0.5f;
            quest.RequiredLevel = difficulty == QuestDifficulty.Easy ? 1 : 
                                  (difficulty == QuestDifficulty.Medium ? 5 : 
                                  (difficulty == QuestDifficulty.Hard ? 15 : 30));
            quest.IsRepeatable = true;
            quest.XPReward = 50 * ((int)difficulty + 1);

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

            if (PlayerProgressionManager.Instance != null)
            {
                PlayerProgressionManager.Instance.RecordQuestAttempt(quest);
            }

            // Task 10.1: Play quest accepted sound and switch music
            PlayQuestClip(questAcceptedClip);
            SwitchToDeliveryMusic();

            // Reset time warning flag
            timeWarningPlayed = false;
            MarkQuestUiDirty();

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

            if (PlayerProgressionManager.Instance != null)
            {
                PlayerProgressionManager.Instance.RecordQuestFailure(quest);
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

            // Calculate performance rating
            quest.CalculateRating();

            // Increment streak
            consecutiveSuccesses++;
            streakMultiplier = Mathf.Min(1.0f + (consecutiveSuccesses * streakMultiplierIncrement), maxStreakMultiplier);

            // Calculate base reward and apply streak multiplier
            int baseReward = quest.CalculateFinalReward();
            int finalReward = Mathf.RoundToInt(baseReward * streakMultiplier);
            LastCompletionReward = finalReward;
            LastFailureReason = string.Empty;
            float completionTimeSeconds = Mathf.Max(0f, quest.TimeLimit - quest.TimeRemaining);

            completedQuests.Add(quest);
            TryAwardRewards(quest, finalReward);

            // Record quest completion for achievements and statistics
            if (PlayerProgressionManager.Instance != null)
            {
                PlayerProgressionManager.Instance.RecordQuestCompletion(quest, finalReward, completionTimeSeconds);
            }

            Debug.Log($"[QuestManager] Quest completed with {quest.Rating} rank! Streak: {consecutiveSuccesses}x (Multiplier: {streakMultiplier:F1}x)");

            // Task 10.1: Play success sound
            PlayQuestClip(deliveryClip);

            // Task 10.2: Play delivery particle effect
            if (playerTransform != null)
            {
                PlayParticleEffect(deliveryEffectPrefab, playerTransform.position);
            }

            // Task 10.1: Switch back to exploration music
            SwitchToExplorationMusic();

            OnQuestCompleted.Invoke(quest);
            CleanupQuestMarkers(quest);
            ClearAllZones();
            GenerateAvailableQuests(1);

            // Trigger auto-save after quest completion
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.TriggerAutoSave();
            }
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
            OnQuestFailed.Invoke(quest);
            Debug.LogWarning($"[QuestManager] Quest failed: {quest.QuestName}. Reason: {reason}");

            if (PlayerProgressionManager.Instance != null)
            {
                PlayerProgressionManager.Instance.RecordQuestFailure(quest);
            }

            CleanupQuestMarkers(quest);
            ClearAllZones();
            PlayQuestClip(failureClip);

            // Task 10.1: Switch back to exploration music
            SwitchToExplorationMusic();

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

            // Task 9.1: Procedural Location Picker with validation and variety
            int attempts = 0;
            RoadSegment segment = null;
            int waypointIndex = -1;
            Vector3 candidatePosition = Vector3.zero;

            while (attempts < 20)
            {
                attempts++;
                
                var result = roadGraphBuilder.RoadGraph.GetRandomWaypoint();
                segment = result.Item1;
                waypointIndex = result.Item2;

                if (segment == null || segment.waypoints.Count == 0)
                    continue;

                candidatePosition = segment.waypoints[waypointIndex].position;

                // Check 1: Cooldown/Used Locations
                bool tooClose = false;
                foreach (Vector3 used in usedLocations)
                {
                    Vector3 delta = candidatePosition - used;
                    if (delta.sqrMagnitude < locationCooldownDistance * locationCooldownDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                // Check 2: Ground Validation (Raycast)
                if (Physics.Raycast(candidatePosition + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 100f))
                {
                    // Snap to ground if needed, though waypoints are usually correct
                    // candidatePosition = hit.point; 
                }
                else
                {
                    // No ground found (void/floating)
                    continue;
                }

                // If valid, accept
                break;
            }

            if (segment == null) return null;

            // Track usage
            usedLocations.Add(candidatePosition);
            if (usedLocations.Count > 20) usedLocations.RemoveAt(0);

            // Determine Location Type (Mock)
            string[] locationTypes;
            if (segment.id % 4 == 0) locationTypes = new[] { "Industrial Park", "Factory", "Plant", "Refinery" }; // Industrial
            else if (segment.id % 4 == 1) locationTypes = new[] { "Mall", "Plaza", "Store", "Market", "Shop" }; // Commercial
            else if (segment.id % 4 == 2) locationTypes = new[] { "Residence", "Apartments", "Estate", "Manor" }; // Residential
            else locationTypes = new[] { "Warehouse", "Depot", "Station", "Hub", "Terminal" }; // Logistics

            string locationType = locationTypes[UnityEngine.Random.Range(0, locationTypes.Length)];
            
            // Generate Name
            string[] directions = { "North", "South", "East", "West", "Central", "Upper", "Lower" };
            string direction = directions[UnityEngine.Random.Range(0, directions.Length)];
            string locationName = $"{direction} {locationType}";

            float triggerRadius = UnityEngine.Random.Range(10f, 15f);

            QuestLocation location = new QuestLocation(candidatePosition, locationName, triggerRadius)
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

            Vector3 delta = pickup.Position - delivery.Position;
            float distanceSqr = delta.sqrMagnitude;
            float minDistance = difficulty switch
            {
                QuestDifficulty.Easy => 500f,
                QuestDifficulty.Medium => 1000f,
                QuestDifficulty.Hard => 1500f,
                QuestDifficulty.Expert => 2000f,
                _ => 500f
            };

            return distanceSqr >= minDistance * minDistance;
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

            // Task 10.2: Spawn marker particles at quest zone
            SpawnMarkerParticles(location.Position);

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

            // Task 10.2: Play pickup particle effect
            if (playerTransform != null)
            {
                PlayParticleEffect(pickupEffectPrefab, playerTransform.position);
            }

            TryApplyCargoWeight(currentQuest.Cargo);
            cargoVisual?.AttachCargo(currentQuest.Cargo);

            // Initialize distance tracking starting position
            if (playerTransform != null)
            {
                currentQuest.LastPosition = playerTransform.position;
            }

            QuestLocation delivery = GetCurrentDeliveryLocation();
            SpawnQuestZone(delivery, QuestZoneType.Delivery);
            MarkQuestUiDirty();
            Debug.Log($"[QuestManager] Cargo loaded! Deliver to {delivery?.LocationName ?? "destination"}.");

            // Task 10.4: Tutorial integration
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.OnCargoPickedUp();
            }
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
                MarkQuestUiDirty();
            }
            else
            {
                TryRemoveCargoWeight();
                cargoVisual?.DetachCargo();
                PlayQuestClip(deliveryClip);

                // Task 10.4: Tutorial integration
                if (TutorialManager.Instance != null)
                {
                    TutorialManager.Instance.OnCargoDelivered();
                }

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
            PlayQuestClip(destroyedClip);
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

            lastCollisionTime = Time.time;

            // Record collision for penalty calculation
            bool isNpcCollision = collision.gameObject.CompareTag("NPC") ||
                                  collision.gameObject.CompareTag("Traffic") ||
                                  collision.gameObject.layer == LayerMask.NameToLayer("NPC");
            currentQuest.RecordCollision(isNpcCollision);

            // Apply damage to fragile cargo
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (currentQuest.Cargo != null && currentQuest.Cargo.IsFragile && !debugInvincibleCargo)
#else
            if (currentQuest.Cargo != null && currentQuest.Cargo.IsFragile)
#endif
            {
                float damage = (force - collisionDamageThreshold) / collisionDamageDivider;
                currentQuest.Cargo.TakeDamage(damage);
                cargoVisual?.PlayDamageEffect();
                PlayQuestClip(damageClip);

                // Task 10.2: Play damage particle effect
                if (playerTransform != null)
                {
                    PlayParticleEffect(damageEffectPrefab, playerTransform.position);
                }

                if (currentQuest.Cargo.IsDestroyed())
                {
                    OnCargoDestroyed();
                    return;
                }
            }

            MarkQuestUiDirty();
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

        private void MarkQuestUiDirty()
        {
            questUiDirty = true;
        }

        private void TryNotifyQuestUpdated()
        {
            if (currentQuest == null)
            {
                return;
            }

            int timeRemainingSeconds = Mathf.CeilToInt(currentQuest.TimeRemaining);
            float cargoHealth = currentQuest.Cargo != null && currentQuest.Cargo.IsFragile
                ? currentQuest.Cargo.CargoHealth
                : -1f;

            bool isDirty = questUiDirty ||
                           !string.Equals(currentQuest.QuestID, lastQuestId, StringComparison.Ordinal) ||
                           timeRemainingSeconds != lastTimeRemainingSeconds ||
                           currentQuest.CurrentDeliveryIndex != lastDeliveryIndex ||
                           currentQuest.HasPickedUpCargo != lastHasPickedUpCargo ||
                           Mathf.Abs(cargoHealth - lastCargoHealth) > 0.1f;

            if (!isDirty)
            {
                return;
            }

            CacheQuestUiState(currentQuest);
            OnQuestUpdated.Invoke(currentQuest);
        }

        private void CacheQuestUiState(QuestData quest)
        {
            if (quest == null)
            {
                return;
            }

            lastQuestId = quest.QuestID ?? string.Empty;
            lastTimeRemainingSeconds = Mathf.CeilToInt(quest.TimeRemaining);
            lastDeliveryIndex = quest.CurrentDeliveryIndex;
            lastHasPickedUpCargo = quest.HasPickedUpCargo;
            lastCargoHealth = quest.Cargo != null && quest.Cargo.IsFragile ? quest.Cargo.CargoHealth : -1f;
            questUiDirty = false;
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
                CacheQuestUiState(currentQuest);
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

            // Try to use PlayerProgressionManager directly via singleton
            if (PlayerProgressionManager.Instance != null)
            {
                PlayerProgressionManager.Instance.AwardMoney(reward);
                PlayerProgressionManager.Instance.AwardXP(quest.XPReward);
                PlayerProgressionManager.Instance.IncrementQuestsCompleted();
                PlayerProgressionManager.Instance.AddDistanceTraveled(quest.TotalDistanceTraveled);
                return;
            }

            // Fallback to reflection for backwards compatibility
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

        #region Task 10.1: Audio & Music System

        /// <summary>
        /// Task 10.1: Plays the time warning sound effect
        /// </summary>
        private void PlayTimeWarning()
        {
            if (questSfxSource != null && timeWarningClip != null)
            {
                questSfxSource.PlayOneShot(timeWarningClip);
                Debug.Log("[QuestManager] Time warning! Less than 30 seconds remaining!");
            }
        }

        /// <summary>
        /// Task 10.1: Switches background music to delivery mode (intense)
        /// </summary>
        private void SwitchToDeliveryMusic()
        {
            if (musicSource == null || deliveryMusicClip == null)
            {
                return;
            }

            if (musicSource.clip == deliveryMusicClip)
            {
                return; // Already playing delivery music
            }

            StartCoroutine(CrossfadeMusic(deliveryMusicClip));
        }

        /// <summary>
        /// Task 10.1: Switches background music to exploration mode (calm)
        /// </summary>
        private void SwitchToExplorationMusic()
        {
            if (musicSource == null || explorationMusicClip == null)
            {
                return;
            }

            if (musicSource.clip == explorationMusicClip)
            {
                return; // Already playing exploration music
            }

            StartCoroutine(CrossfadeMusic(explorationMusicClip));
        }

        /// <summary>
        /// Task 10.1: Crossfades between music tracks smoothly
        /// </summary>
        private System.Collections.IEnumerator CrossfadeMusic(AudioClip newClip)
        {
            if (isCrossfading)
            {
                yield break; // Don't interrupt existing crossfade
            }

            isCrossfading = true;
            float startVolume = musicSource.volume;

            // Fade out current music
            float elapsed = 0f;
            while (elapsed < musicCrossfadeDuration / 2f)
            {
                elapsed += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / (musicCrossfadeDuration / 2f));
                yield return null;
            }

            // Switch clip
            musicSource.clip = newClip;
            musicSource.Play();

            // Fade in new music
            elapsed = 0f;
            while (elapsed < musicCrossfadeDuration / 2f)
            {
                elapsed += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(0f, startVolume, elapsed / (musicCrossfadeDuration / 2f));
                yield return null;
            }

            musicSource.volume = startVolume;
            isCrossfading = false;
        }

        /// <summary>
        /// Task 10.1: Plays a level up sound effect (can be called from PlayerProgressionManager)
        /// </summary>
        public void PlayLevelUpSound()
        {
            if (questSfxSource != null && levelUpClip != null)
            {
                questSfxSource.PlayOneShot(levelUpClip);
            }
        }

        /// <summary>
        /// Task 10.1: Sets the music volume (0-1)
        /// </summary>
        public void SetMusicVolume(float volume)
        {
            if (musicSource != null)
            {
                musicSource.volume = Mathf.Clamp01(volume);
            }
        }

        /// <summary>
        /// Task 10.1: Sets the SFX volume (0-1)
        /// </summary>
        public void SetSFXVolume(float volume)
        {
            if (questSfxSource != null)
            {
                questSfxSource.volume = Mathf.Clamp01(volume);
            }
        }

        #endregion

        #region Task 10.2: Particle Effects System

        /// <summary>
        /// Task 10.2: Initializes the particle effect object pool
        /// </summary>
        private void InitializeParticlePool()
        {
            particlePool.Clear();

            // Pre-instantiate particle effects for pooling
            GameObject[] prefabs = { pickupEffectPrefab, deliveryEffectPrefab, damageEffectPrefab, levelUpEffectPrefab };

            foreach (GameObject prefab in prefabs)
            {
                if (prefab == null) continue;

                for (int i = 0; i < particlePoolSize / prefabs.Length; i++)
                {
                    GameObject particle = Instantiate(prefab);
                    particle.SetActive(false);
                    particlePool.Enqueue(particle);
                }
            }

            Debug.Log($"[QuestManager] Particle pool initialized with {particlePool.Count} objects.");
        }

        /// <summary>
        /// Task 10.2: Plays a particle effect at the specified position
        /// </summary>
        private void PlayParticleEffect(GameObject effectPrefab, Vector3 position)
        {
            if (effectPrefab == null)
            {
                return;
            }

            GameObject effect = null;

            // Try to get from pool first
            if (particlePool.Count > 0)
            {
                effect = particlePool.Dequeue();
            }

            // If pool is empty, instantiate new one
            if (effect == null)
            {
                effect = Instantiate(effectPrefab, position, Quaternion.identity);
            }
            else
            {
                effect.transform.position = position;
                effect.transform.rotation = Quaternion.identity;
                effect.SetActive(true);
            }

            // Get particle system and play
            ParticleSystem ps = effect.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Play();

                // Return to pool after particle duration
                float duration = ps.main.duration + ps.main.startLifetime.constantMax;
                StartCoroutine(ReturnParticleToPool(effect, duration));
            }
            else
            {
                // If no particle system, just destroy after 5 seconds
                StartCoroutine(ReturnParticleToPool(effect, 5f));
            }
        }

        /// <summary>
        /// Task 10.2: Returns a particle effect to the pool after duration
        /// </summary>
        private System.Collections.IEnumerator ReturnParticleToPool(GameObject particle, float delay)
        {
            yield return new UnityEngine.WaitForSeconds(delay);

            if (particle != null)
            {
                particle.SetActive(false);
                particlePool.Enqueue(particle);
            }
        }

        /// <summary>
        /// Task 10.2: Plays a level up particle effect (can be called from PlayerProgressionManager)
        /// </summary>
        public void PlayLevelUpEffect(Vector3 position)
        {
            PlayParticleEffect(levelUpEffectPrefab, position);
        }

        /// <summary>
        /// Task 10.2: Spawns marker particles at quest zone locations
        /// </summary>
        private void SpawnMarkerParticles(Vector3 position)
        {
            if (questMarkerParticlePrefab == null)
            {
                return;
            }

            GameObject markerParticle = Instantiate(questMarkerParticlePrefab, position, Quaternion.identity);
            ParticleSystem ps = markerParticle.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Play();
            }
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
            if (stopCount < 2 || stopCount > 4)
            {
                Debug.LogWarning($"[QuestManager] Invalid stop count {stopCount}. Must be 2-4.");
                return null;
            }

            // Pick cargo
            CargoData cargo = null;
            if (cargoLibrary != null)
            {
                cargo = cargoLibrary.GetRandomCargo();
            }
            if (cargo == null)
            {
                cargo = new CargoData("Generic Cargo", 100f, false, "Standard cargo for multi-stop delivery.");
            }

            // Generate pickup location
            QuestLocation pickup = GenerateRandomLocation("Pickup");
            if (pickup == null)
            {
                Debug.LogWarning("[QuestManager] Failed to generate pickup location for multi-stop quest.");
                return null;
            }

            // Generate delivery locations
            List<QuestLocation> deliveryLocations = new List<QuestLocation>();
            for (int i = 0; i < stopCount; i++)
            {
                QuestLocation delivery = GenerateRandomLocation($"Delivery {i + 1}");
                if (delivery != null)
                {
                    if (delivery.VisualMarker == null && deliveryMarkerPrefab != null)
                    {
                        delivery.VisualMarker = deliveryMarkerPrefab;
                    }
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
            List<QuestLocation> optimizedRoute = OptimizeRoute(pickup, deliveryLocations);

            // Calculate total route distance
            float totalDistance = 0f;
            Vector3 lastPos = pickup.Position;
            foreach (QuestLocation loc in optimizedRoute)
            {
                totalDistance += Vector3.Distance(lastPos, loc.Position);
                lastPos = loc.Position;
            }

            // Create quest
            QuestData quest = new QuestData
            {
                QuestID = System.Guid.NewGuid().ToString(),
                QuestName = $"Multi-Stop: {optimizedRoute.Count} Deliveries",
                QuestDescription = $"Transport {cargo.CargoName} to {optimizedRoute.Count} locations. Complete all deliveries in order.",
                QuestType = QuestType.MultiStopDelivery,
                Difficulty = difficulty,
                Status = QuestStatus.NotStarted,
                PickupLocation = pickup,
                DeliveryLocations = optimizedRoute,
                Cargo = cargo
            };

            // Assign markers
            if (quest.PickupLocation.VisualMarker == null) quest.PickupLocation.VisualMarker = pickupMarkerPrefab;

            // Calculate time and rewards scaled for multi-stop
            float avgSpeed = 11f; // ~40 km/h in m/s
            float timeMultiplier = questSystemSettings != null
                ? questSystemSettings.GetTimeMultiplier(difficulty)
                : (difficulty == QuestDifficulty.Easy ? 2.0f :
                   difficulty == QuestDifficulty.Medium ? 1.5f :
                   difficulty == QuestDifficulty.Hard ? 1.2f : 1.0f);

            // Scale time limit: baseTime * stopCount * 1.5
            float multiStopScale = questSystemSettings != null ? questSystemSettings.MultiStopTimeScale : 1.5f;
            float minMultiStopTime = questSystemSettings != null ? questSystemSettings.MinimumMultiStopTimeLimit : 120f;
            quest.TimeLimit = Mathf.Max(minMultiStopTime, ((totalDistance / avgSpeed) * timeMultiplier * optimizedRoute.Count * multiStopScale));
            quest.TimeRemaining = quest.TimeLimit;

            // Scale reward: baseReward * stopCount * 1.8
            int difficultyBonus = questSystemSettings != null
                ? questSystemSettings.GetDifficultyBonus(difficulty)
                : (difficulty == QuestDifficulty.Medium ? 100 :
                   difficulty == QuestDifficulty.Hard ? 250 :
                   difficulty == QuestDifficulty.Expert ? 500 : 0);
            float rewardPerMeter = questSystemSettings != null ? questSystemSettings.BaseRewardPerMeter : 0.1f;
            float multiStopRewardScale = questSystemSettings != null ? questSystemSettings.MultiStopRewardScale : 1.8f;
            float bonusMultiplier = questSystemSettings != null ? questSystemSettings.BonusRewardMultiplier : 0.5f;
            int baseRewardPerStop = Mathf.RoundToInt((totalDistance * rewardPerMeter) + difficultyBonus);
            quest.BaseReward = Mathf.RoundToInt(baseRewardPerStop * optimizedRoute.Count * multiStopRewardScale);
            quest.BonusReward = Mathf.RoundToInt(quest.BaseReward * bonusMultiplier);
            quest.BonusTimeThreshold = 0.5f;
            quest.RequiredLevel = difficulty == QuestDifficulty.Easy ? 1 :
                                  (difficulty == QuestDifficulty.Medium ? 5 :
                                  (difficulty == QuestDifficulty.Hard ? 15 : 30));
            quest.IsRepeatable = true;
            quest.XPReward = 50 * ((int)difficulty + 1) * optimizedRoute.Count;

            QuestLogger.Log($"[QuestManager] Generated multi-stop quest: {optimizedRoute.Count} stops, {totalDistance:F0}m total distance");

            return quest;
        }

        /// <summary>
        /// Optimizes route using nearest-neighbor algorithm
        /// </summary>
        private List<QuestLocation> OptimizeRoute(QuestLocation start, List<QuestLocation> stops)
        {
            List<QuestLocation> optimized = new List<QuestLocation>();
            List<QuestLocation> remaining = new List<QuestLocation>(stops);
            QuestLocation current = start;

            while (remaining.Count > 0)
            {
                QuestLocation nearest = FindNearest(current, remaining);
                if (nearest != null)
                {
                    optimized.Add(nearest);
                    remaining.Remove(nearest);
                    current = nearest;
                }
                else
                {
                    // Fallback: just add the first remaining
                    optimized.Add(remaining[0]);
                    remaining.RemoveAt(0);
                }
            }

            return optimized;
        }

        /// <summary>
        /// Finds nearest location from a list
        /// </summary>
        private QuestLocation FindNearest(QuestLocation current, List<QuestLocation> candidates)
        {
            if (candidates == null || candidates.Count == 0 || current == null)
            {
                return null;
            }

            QuestLocation nearest = null;
            float minDistance = float.MaxValue;

            foreach (QuestLocation candidate in candidates)
            {
                if (candidate == null) continue;

                float distance = Vector3.Distance(current.Position, candidate.Position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        #endregion

        #region Task 9.4: Special Quest Types

        /// <summary>
        /// Task 9.4: Generates an express delivery quest with tight time limit and high reward
        /// </summary>
        public QuestData GenerateExpressDelivery()
        {
            // Pick difficulty weighted toward medium/hard
            QuestDifficulty difficulty = UnityEngine.Random.value < 0.5f ? QuestDifficulty.Medium : QuestDifficulty.Hard;

            // Pick cargo (any type)
            CargoData cargo = null;
            if (cargoLibrary != null)
            {
                cargo = cargoLibrary.GetRandomCargo();
            }
            if (cargo == null)
            {
                cargo = new CargoData("Express Package", 50f, false, "Time-critical express delivery.");
            }

            // Generate locations
            float minDistance = difficulty == QuestDifficulty.Medium ? 2000f : 3000f;
            float maxDistance = difficulty == QuestDifficulty.Medium ? 4000f : 6000f;

            QuestLocation pickup = null;
            QuestLocation delivery = null;
            float actualDistance = 0f;

            for (int i = 0; i < 15; i++)
            {
                pickup = GenerateRandomLocation("Express Pickup");
                delivery = GenerateRandomLocation("Express Delivery");

                if (pickup == null || delivery == null) continue;

                actualDistance = Vector3.Distance(pickup.Position, delivery.Position);
                if (actualDistance >= minDistance && actualDistance <= maxDistance)
                {
                    break;
                }

                pickup = null;
                delivery = null;
            }

            if (pickup == null || delivery == null)
            {
                Debug.LogWarning("[QuestManager] Failed to generate express delivery locations.");
                return null;
            }

            // Create quest
            QuestData quest = new QuestData
            {
                QuestID = System.Guid.NewGuid().ToString(),
                QuestName = $"EXPRESS: Rush Delivery",
                QuestDescription = $"Express delivery of {cargo.CargoName}. Very tight deadline - drive fast!",
                QuestType = QuestType.ExpressDelivery,
                Difficulty = difficulty,
                Status = QuestStatus.NotStarted,
                PickupLocation = pickup,
                DeliveryLocations = new List<QuestLocation> { delivery },
                Cargo = cargo
            };

            // Assign markers
            if (quest.PickupLocation.VisualMarker == null) quest.PickupLocation.VisualMarker = pickupMarkerPrefab;
            if (delivery.VisualMarker == null) delivery.VisualMarker = deliveryMarkerPrefab;

            // Time limit = 0.6x normal (very tight)
            float avgSpeed = 11f; // ~40 km/h in m/s
            float normalTimeMultiplier = questSystemSettings != null
                ? questSystemSettings.GetTimeMultiplier(difficulty)
                : (difficulty == QuestDifficulty.Medium ? 1.5f : 1.2f);
            float expressScale = questSystemSettings != null ? questSystemSettings.ExpressTimeScale : 0.6f;
            float minTimeLimit = questSystemSettings != null ? questSystemSettings.MinimumTimeLimit : 60f;
            quest.TimeLimit = Mathf.Max(minTimeLimit, ((actualDistance / avgSpeed) * normalTimeMultiplier * expressScale));
            quest.TimeRemaining = quest.TimeLimit;

            // Reward = 2.0x normal
            int difficultyBonus = questSystemSettings != null
                ? questSystemSettings.GetDifficultyBonus(difficulty)
                : (difficulty == QuestDifficulty.Medium ? 100 : 250);
            float rewardPerMeter = questSystemSettings != null ? questSystemSettings.BaseRewardPerMeter : 0.1f;
            float expressRewardMultiplier = questSystemSettings != null ? questSystemSettings.ExpressRewardMultiplier : 2.0f;
            float bonusMultiplier = questSystemSettings != null ? questSystemSettings.BonusRewardMultiplier : 0.5f;
            int baseReward = Mathf.RoundToInt((actualDistance * rewardPerMeter) + difficultyBonus);
            quest.BaseReward = Mathf.RoundToInt(baseReward * expressRewardMultiplier);
            quest.BonusReward = Mathf.RoundToInt(quest.BaseReward * bonusMultiplier);
            quest.BonusTimeThreshold = 0.5f;
            quest.RequiredLevel = difficulty == QuestDifficulty.Medium ? 5 : 15;
            quest.IsRepeatable = true;
            quest.XPReward = 50 * ((int)difficulty + 1);

            QuestLogger.Log($"[QuestManager] Generated EXPRESS delivery: {actualDistance:F0}m, {quest.TimeLimit:F0}s, ${quest.BaseReward}");

            return quest;
        }

        /// <summary>
        /// Task 9.4: Generates a fragile delivery quest with damage penalties
        /// </summary>
        public QuestData GenerateFragileDelivery()
        {
            // Pick difficulty
            QuestDifficulty difficulty = UnityEngine.Random.value < 0.5f ? QuestDifficulty.Easy : QuestDifficulty.Medium;

            // Force fragile cargo selection
            CargoData cargo = null;
            if (cargoLibrary != null)
            {
                cargo = cargoLibrary.GetCargoByFragility(true);
            }
            if (cargo == null)
            {
                cargo = new CargoData("Fragile Electronics", 80f, true, "Handle with extreme care - easily damaged.");
                cargo.CargoHealth = 100f;
            }

            // Generate locations
            float minDistance = difficulty == QuestDifficulty.Easy ? 1000f : 2000f;
            float maxDistance = difficulty == QuestDifficulty.Easy ? 2000f : 4000f;

            QuestLocation pickup = null;
            QuestLocation delivery = null;
            float actualDistance = 0f;

            for (int i = 0; i < 15; i++)
            {
                pickup = GenerateRandomLocation("Fragile Pickup");
                delivery = GenerateRandomLocation("Fragile Delivery");

                if (pickup == null || delivery == null) continue;

                actualDistance = Vector3.Distance(pickup.Position, delivery.Position);
                if (actualDistance >= minDistance && actualDistance <= maxDistance)
                {
                    break;
                }

                pickup = null;
                delivery = null;
            }

            if (pickup == null || delivery == null)
            {
                Debug.LogWarning("[QuestManager] Failed to generate fragile delivery locations.");
                return null;
            }

            // Create quest
            QuestData quest = new QuestData
            {
                QuestID = System.Guid.NewGuid().ToString(),
                QuestName = $"FRAGILE: Handle With Care",
                QuestDescription = $"Delicate {cargo.CargoName} - avoid collisions! Bonus for zero damage.",
                QuestType = QuestType.FragileDelivery,
                Difficulty = difficulty,
                Status = QuestStatus.NotStarted,
                PickupLocation = pickup,
                DeliveryLocations = new List<QuestLocation> { delivery },
                Cargo = cargo
            };

            // Assign markers
            if (quest.PickupLocation.VisualMarker == null) quest.PickupLocation.VisualMarker = pickupMarkerPrefab;
            if (delivery.VisualMarker == null) delivery.VisualMarker = deliveryMarkerPrefab;

            // Slightly longer time limit (player must drive carefully)
            float avgSpeed = 11f; // ~40 km/h in m/s
            float timeMultiplier = questSystemSettings != null
                ? (difficulty == QuestDifficulty.Easy
                    ? questSystemSettings.FragileEasyTimeMultiplier
                    : questSystemSettings.FragileMediumTimeMultiplier)
                : (difficulty == QuestDifficulty.Easy ? 2.5f : 1.8f);
            float minTimeLimit = questSystemSettings != null ? Mathf.Max(90f, questSystemSettings.MinimumTimeLimit) : 90f;
            quest.TimeLimit = Mathf.Max(minTimeLimit, ((actualDistance / avgSpeed) * timeMultiplier));
            quest.TimeRemaining = quest.TimeLimit;

            // Bonus for zero damage: +50% reward
            int difficultyBonus = questSystemSettings != null
                ? questSystemSettings.GetDifficultyBonus(difficulty)
                : (difficulty == QuestDifficulty.Easy ? 0 : 100);
            float rewardPerMeter = questSystemSettings != null ? questSystemSettings.BaseRewardPerMeter : 0.1f;
            float bonusMultiplier = questSystemSettings != null ? questSystemSettings.BonusRewardMultiplier : 0.5f;
            int baseReward = Mathf.RoundToInt((actualDistance * rewardPerMeter) + difficultyBonus);
            quest.BaseReward = baseReward;
            quest.BonusReward = Mathf.RoundToInt(baseReward * bonusMultiplier); // Will be awarded if cargo health > 90%
            quest.BonusTimeThreshold = 0.5f;
            quest.RequiredLevel = difficulty == QuestDifficulty.Easy ? 1 : 5;
            quest.IsRepeatable = true;
            quest.XPReward = 50 * ((int)difficulty + 1);

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

            // Generate locations
            float minDistance = difficulty == QuestDifficulty.Hard ? 4000f : 6000f;
            float maxDistance = difficulty == QuestDifficulty.Hard ? 6000f : 10000f;

            QuestLocation pickup = null;
            QuestLocation delivery = null;
            float actualDistance = 0f;

            for (int i = 0; i < 15; i++)
            {
                pickup = GenerateRandomLocation("Trial Start");
                delivery = GenerateRandomLocation("Trial Finish");

                if (pickup == null || delivery == null) continue;

                actualDistance = Vector3.Distance(pickup.Position, delivery.Position);
                if (actualDistance >= minDistance && actualDistance <= maxDistance)
                {
                    break;
                }

                pickup = null;
                delivery = null;
            }

            if (pickup == null || delivery == null)
            {
                Debug.LogWarning("[QuestManager] Failed to generate time trial locations.");
                return null;
            }

            // Create quest
            QuestData quest = new QuestData
            {
                QuestID = System.Guid.NewGuid().ToString(),
                QuestName = $"TIME TRIAL: Speed Run",
                QuestDescription = $"Pure speed challenge - complete the delivery as fast as possible! Reward scales with remaining time.",
                QuestType = QuestType.TimeTrial,
                Difficulty = difficulty,
                Status = QuestStatus.NotStarted,
                PickupLocation = pickup,
                DeliveryLocations = new List<QuestLocation> { delivery },
                Cargo = cargo
            };

            // Assign markers
            if (quest.PickupLocation.VisualMarker == null) quest.PickupLocation.VisualMarker = pickupMarkerPrefab;
            if (delivery.VisualMarker == null) delivery.VisualMarker = deliveryMarkerPrefab;

            // Very short time limit (0.5x normal)
            float avgSpeed = 11f; // ~40 km/h in m/s
            float timeMultiplier = questSystemSettings != null
                ? questSystemSettings.GetTimeMultiplier(difficulty)
                : (difficulty == QuestDifficulty.Hard ? 1.2f : 1.0f);
            float timeTrialScale = questSystemSettings != null ? questSystemSettings.TimeTrialTimeScale : 0.5f;
            float minTimeLimit = questSystemSettings != null ? questSystemSettings.MinimumTimeLimit : 60f;
            quest.TimeLimit = Mathf.Max(minTimeLimit, ((actualDistance / avgSpeed) * timeMultiplier * timeTrialScale));
            quest.TimeRemaining = quest.TimeLimit;

            // Reward scales with remaining time (more time = more money)
            int difficultyBonus = questSystemSettings != null
                ? questSystemSettings.GetDifficultyBonus(difficulty)
                : (difficulty == QuestDifficulty.Hard ? 250 : 500);
            float rewardPerMeter = questSystemSettings != null ? questSystemSettings.BaseRewardPerMeter : 0.15f;
            float timeTrialRewardMultiplier = questSystemSettings != null ? questSystemSettings.TimeTrialRewardMultiplier : 1.0f;
            float bonusMultiplier = questSystemSettings != null ? questSystemSettings.BonusRewardMultiplier : 1.0f;
            quest.BaseReward = Mathf.RoundToInt(((actualDistance * rewardPerMeter) + difficultyBonus) * timeTrialRewardMultiplier);
            quest.BonusReward = Mathf.RoundToInt(quest.BaseReward * bonusMultiplier);
            quest.BonusTimeThreshold = 0.5f;
            quest.RequiredLevel = difficulty == QuestDifficulty.Hard ? 15 : 30;
            quest.IsRepeatable = true;
            quest.XPReward = 50 * ((int)difficulty + 1);

            QuestLogger.Log($"[QuestManager] Generated TIME TRIAL: {actualDistance:F0}m, extreme time pressure!");

            return quest;
        }

        /// <summary>
        /// Task 9.4: Generates a quest with weighted quest type selection
        /// </summary>
        public QuestData GenerateRandomQuestWithTypes(QuestDifficulty difficulty)
        {
            float typeRoll = UnityEngine.Random.value;

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
            else if (typeRoll < 0.80f)
            {
                // Express Delivery (20%)
                return GenerateExpressDelivery();
            }
            else if (typeRoll < 0.95f)
            {
                // Fragile Delivery (15%)
                return GenerateFragileDelivery();
            }
            else
            {
                // Multi-Stop Delivery (5%)
                int stopCount = UnityEngine.Random.Range(2, 4); // 2-3 stops
                return GenerateMultiStopQuest(stopCount, difficulty);
            }
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
                lastDailyChallengeDate = todayDate;
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
                Debug.LogWarning("[QuestManager] Cannot generate daily challenge - QuestDatabase not assigned");
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
            bool assigned = AssignQuestLocations(quest);
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

            return AcceptQuest(dailyChallenge.QuestID);
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
    }
}
