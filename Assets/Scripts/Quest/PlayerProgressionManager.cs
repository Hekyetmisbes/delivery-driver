using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace DeliveryDriver.Quest
{
    /// <summary>
    /// Manages player progression, currency, level, and experience points
    /// </summary>
    public class PlayerProgressionManager : MonoBehaviour
    {
        private const float DatabaseReadyWaitTimeoutSeconds = 5f;
        public static PlayerProgressionManager Instance { get; private set; }

        [Header("Currency")]
        [SerializeField] private int currentMoney = 500; // Starting money

        [Header("Level & XP")]
        [SerializeField] private int currentLevel = 1;
        [SerializeField] private int currentXP = 0;
        [SerializeField] private int xpToNextLevel = 100;

        [Header("Statistics")]
        [SerializeField] private int totalQuestsCompleted = 0;
        [SerializeField] private int totalQuestsAttempted = 0;
        [SerializeField] private int totalQuestsFailed = 0;
        [SerializeField] private int totalMoneyEarned = 0;
        [SerializeField] private float totalDistanceTraveled = 0f; // In meters
        [SerializeField] private float totalTimePlayed = 0f; // In seconds
        [SerializeField] private float totalDeliveryTimeSeconds = 0f;
        [SerializeField] private float fastestDeliveryTimeSeconds = 0f;
        [SerializeField] private int speedBonusesEarned = 0;
        [SerializeField] private int sRanksAchieved = 0;
        [SerializeField] private float totalCargoWeightDelivered = 0f; // In kg
        [SerializeField] private int fragileCargoDeliveredUndamaged = 0;
        [SerializeField] private List<CargoTypeStat> cargoTypeStats = new List<CargoTypeStat>();
        [SerializeField] private List<DailyStat> dailyStats = new List<DailyStat>();
        [SerializeField] private List<LevelSnapshot> levelSnapshots = new List<LevelSnapshot>();

        [Header("Achievements")]
        [SerializeField] private List<Achievement> achievements = new List<Achievement>();

        [Header("Events")]
        public UnityEvent<int> OnMoneyChanged = new UnityEvent<int>();
        public UnityEvent<int> OnLevelUp = new UnityEvent<int>();
        public UnityEvent<int> OnXPGained = new UnityEvent<int>();
        public UnityEvent<Achievement> OnAchievementUnlocked = new UnityEvent<Achievement>();

        // Public read-only properties
        public int CurrentMoney => currentMoney;
        public int CurrentLevel => currentLevel;
        public int CurrentXP => currentXP;
        public int XPToNextLevel => xpToNextLevel;
        public int TotalQuestsCompleted => totalQuestsCompleted;
        public int TotalQuestsAttempted => totalQuestsAttempted;
        public int TotalQuestsFailed => totalQuestsFailed;
        public int TotalMoneyEarned => totalMoneyEarned;
        public float TotalDistanceTraveled => totalDistanceTraveled;
        public float TotalTimePlayed => totalTimePlayed;
        public float TotalDeliveryTimeSeconds => totalDeliveryTimeSeconds;
        public float FastestDeliveryTimeSeconds => fastestDeliveryTimeSeconds;
        public int SpeedBonusesEarned => speedBonusesEarned;
        public int SRanksAchieved => sRanksAchieved;
        public float TotalCargoWeightDelivered => totalCargoWeightDelivered;
        public int FragileCargoDeliveredUndamaged => fragileCargoDeliveredUndamaged;
        public IReadOnlyList<Achievement> Achievements => achievements;
        public IReadOnlyList<CargoTypeStat> CargoTypeStats => cargoTypeStats;
        public IReadOnlyList<DailyStat> DailyStats => dailyStats;
        public IReadOnlyList<LevelSnapshot> LevelSnapshots => levelSnapshots;

        [Serializable]
        public class CargoTypeStat
        {
            public string CargoName;
            public int Count;
        }

        [Serializable]
        public class DailyStat
        {
            public string Date;
            public int QuestsCompleted;
            public int MoneyEarned;
        }

        [Serializable]
        public class LevelSnapshot
        {
            public string DateTime;
            public int Level;
        }

        private void Awake()
        {
            // Singleton pattern implementation
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Initialize achievements
            InitializeAchievements();
        }

        private void Start()
        {
            // Try to load saved game
            SaveManager saveManager = SaveManager.Instance != null
                ? SaveManager.Instance
                : FindFirstObjectByType<SaveManager>();

            if (saveManager != null)
            {
                GameSaveData saveData = saveManager.LoadGame();

                if (saveData != null && saveData.PlayerData != null)
                {
                    // Load player progression from save
                    LoadSaveData(saveData.PlayerData);
                    Debug.Log("[PlayerProgressionManager] Loaded player data from save file.");
                }
                else
                {
                    // No save found - use defaults (level 1, $500 starting money)
                    Debug.Log("[PlayerProgressionManager] No save found. Starting with default values.");
                    // Default values are already set in the serialized fields
                }
            }
            else
            {
                Debug.Log("[PlayerProgressionManager] SaveManager not found. Starting with default values.");
            }

            StartCoroutine(InitializeMoneyFromDatabaseRoutine());
        }

        private void Update()
        {
            // Track total time played
            totalTimePlayed += Time.deltaTime;
        }

        /// <summary>
        /// Awards money to the player
        /// </summary>
        /// <param name="amount">Amount of money to award</param>
        public void AwardMoney(int amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"[PlayerProgressionManager] Cannot award {amount} money. Amount must be positive.");
                return;
            }

            currentMoney += amount;
            totalMoneyEarned += amount;
            RecordDailyMoney(amount);
            SyncMoneyToDatabase();
            OnMoneyChanged.Invoke(currentMoney);

            Debug.Log($"[PlayerProgressionManager] Awarded ${amount}. Total: ${currentMoney}");
        }

        /// <summary>
        /// Adds money (alias for AwardMoney for compatibility)
        /// </summary>
        public void AddMoney(int amount)
        {
            AwardMoney(amount);
        }

        /// <summary>
        /// Adds currency (alias for AwardMoney for compatibility)
        /// </summary>
        public void AddCurrency(int amount)
        {
            AwardMoney(amount);
        }

        /// <summary>
        /// Spends money from the player's balance
        /// </summary>
        /// <param name="amount">Amount of money to spend</param>
        /// <returns>True if purchase was successful, false if insufficient funds</returns>
        public bool SpendMoney(int amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"[PlayerProgressionManager] Cannot spend {amount} money. Amount must be positive.");
                return false;
            }

            if (currentMoney < amount)
            {
                Debug.LogWarning($"[PlayerProgressionManager] Insufficient funds. Need ${amount}, have ${currentMoney}");
                return false;
            }

            currentMoney -= amount;
            SyncMoneyToDatabase();
            OnMoneyChanged.Invoke(currentMoney);

            Debug.Log($"[PlayerProgressionManager] Spent ${amount}. Remaining: ${currentMoney}");
            return true;
        }

        /// <summary>
        /// Awards experience points and handles level-up logic
        /// </summary>
        /// <param name="amount">Amount of XP to award</param>
        public void AwardXP(int amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"[PlayerProgressionManager] Cannot award {amount} XP. Amount must be positive.");
                return;
            }

            currentXP += amount;
            OnXPGained.Invoke(amount);

            Debug.Log($"[PlayerProgressionManager] Awarded {amount} XP. Total: {currentXP}/{xpToNextLevel}");

            // Check for level up (can level up multiple times if enough XP is earned)
            while (currentXP >= xpToNextLevel)
            {
                LevelUp();
            }
        }

        /// <summary>
        /// Adds XP (alias for AwardXP for compatibility)
        /// </summary>
        public void AddXP(int amount)
        {
            AwardXP(amount);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Sets the player level directly for debug/testing builds.
        /// </summary>
        /// <param name="level">Target level to apply.</param>
        public void SetLevelForDebug(int level)
        {
            currentLevel = Mathf.Max(1, level);
            currentXP = 0;
            xpToNextLevel = CalculateXPForLevel(currentLevel + 1);
            OnLevelUp.Invoke(currentLevel);
            RecordLevelSnapshot(currentLevel);

            Debug.Log($"[PlayerProgressionManager] Debug level set to {currentLevel}.");
        }
#endif

        /// <summary>
        /// Adds experience (alias for AwardXP for compatibility)
        /// </summary>
        public void AddExperience(int amount)
        {
            AwardXP(amount);
        }

        /// <summary>
        /// Levels up the player, resetting XP and calculating new XP requirement
        /// </summary>
        private void LevelUp()
        {
            // Carry over excess XP to next level
            int excessXP = currentXP - xpToNextLevel;

            // Increment level
            currentLevel++;

            // Reset current XP with excess
            currentXP = Mathf.Max(0, excessXP);

            // Calculate XP needed for next level (exponential growth)
            xpToNextLevel = CalculateXPForLevel(currentLevel + 1);

            // Invoke level up event
            OnLevelUp.Invoke(currentLevel);
            RecordLevelSnapshot(currentLevel);

            // Task 10.1 & 10.2: Play level up sound and particle effect
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.PlayLevelUpSound();

                if (QuestManager.Instance.PlayerTransform != null)
                {
                    QuestManager.Instance.PlayLevelUpEffect(QuestManager.Instance.PlayerTransform.position);
                }
            }

            Debug.Log($"[PlayerProgressionManager] LEVEL UP! Now level {currentLevel}. Next level requires {xpToNextLevel} XP.");
        }

        /// <summary>
        /// Calculates the total XP required to reach a specific level
        /// </summary>
        /// <param name="level">Target level</param>
        /// <returns>XP required to reach that level</returns>
        public int CalculateXPForLevel(int level)
        {
            // Exponential growth formula: 100 * level^2
            return 100 * level * level;
        }

        /// <summary>
        /// Increments the total quests completed counter
        /// </summary>
        public void IncrementQuestsCompleted()
        {
            totalQuestsCompleted++;
            Debug.Log($"[PlayerProgressionManager] Total quests completed: {totalQuestsCompleted}");
        }

        /// <summary>
        /// Adds distance to the total distance traveled statistic
        /// </summary>
        /// <param name="distance">Distance in meters</param>
        public void AddDistanceTraveled(float distance)
        {
            if (distance > 0)
            {
                totalDistanceTraveled += distance;
            }
        }

        /// <summary>
        /// Gets progress to next level as a percentage (0-1)
        /// </summary>
        /// <returns>XP progress percentage</returns>
        public float GetLevelProgressPercentage()
        {
            if (xpToNextLevel <= 0)
            {
                return 1f;
            }

            return Mathf.Clamp01((float)currentXP / xpToNextLevel);
        }

        /// <summary>
        /// Gets the total distance traveled formatted in kilometers
        /// </summary>
        /// <returns>Formatted distance string</returns>
        public string GetFormattedDistanceTraveled()
        {
            float distanceKm = totalDistanceTraveled / 1000f;
            return $"{distanceKm:F1} km";
        }

        /// <summary>
        /// Gets the total time played formatted as hours:minutes
        /// </summary>
        /// <returns>Formatted time string</returns>
        public string GetFormattedTimePlayed()
        {
            int hours = Mathf.FloorToInt(totalTimePlayed / 3600f);
            int minutes = Mathf.FloorToInt((totalTimePlayed % 3600f) / 60f);

            if (hours > 0)
            {
                return $"{hours}h {minutes}m";
            }
            else
            {
                return $"{minutes}m";
            }
        }

        /// <summary>
        /// Gets the success rate as a percentage (0-100).
        /// </summary>
        /// <returns>Success rate percentage.</returns>
        public float GetSuccessRatePercentage()
        {
            if (totalQuestsAttempted <= 0)
            {
                return 0f;
            }

            return Mathf.Clamp01((float)totalQuestsCompleted / totalQuestsAttempted) * 100f;
        }

        /// <summary>
        /// Gets the average delivery time in seconds.
        /// </summary>
        /// <returns>Average delivery time in seconds.</returns>
        public float GetAverageDeliveryTimeSeconds()
        {
            if (totalQuestsCompleted <= 0)
            {
                return 0f;
            }

            return totalDeliveryTimeSeconds / totalQuestsCompleted;
        }

        /// <summary>
        /// Gets the fastest delivery time in seconds.
        /// </summary>
        /// <returns>Fastest delivery time in seconds.</returns>
        public float GetFastestDeliveryTimeSeconds()
        {
            return fastestDeliveryTimeSeconds;
        }

        /// <summary>
        /// Gets the most frequently delivered cargo name.
        /// </summary>
        /// <returns>Favorite cargo name or "N/A" if no data.</returns>
        public string GetFavoriteCargoType()
        {
            if (cargoTypeStats == null || cargoTypeStats.Count == 0)
            {
                return "N/A";
            }

            CargoTypeStat top = cargoTypeStats.OrderByDescending(stat => stat.Count).FirstOrDefault();
            return top != null && !string.IsNullOrWhiteSpace(top.CargoName) ? top.CargoName : "N/A";
        }

        /// <summary>
        /// Formats a duration in seconds as MM:SS.
        /// </summary>
        /// <param name="seconds">Duration in seconds.</param>
        /// <returns>Formatted time string.</returns>
        public string FormatDuration(float seconds)
        {
            if (seconds <= 0f)
            {
                return "00:00";
            }

            int minutes = Mathf.FloorToInt(seconds / 60f);
            int remainingSeconds = Mathf.FloorToInt(seconds % 60f);
            return $"{minutes:00}:{remainingSeconds:00}";
        }

        private void UpdateCargoTypeStats(string cargoName)
        {
            if (string.IsNullOrWhiteSpace(cargoName))
            {
                return;
            }

            CargoTypeStat stat = cargoTypeStats.FirstOrDefault(entry => entry.CargoName == cargoName);
            if (stat == null)
            {
                stat = new CargoTypeStat { CargoName = cargoName, Count = 0 };
                cargoTypeStats.Add(stat);
            }

            stat.Count++;
        }

        private void RecordDailyMoney(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            DailyStat stat = GetOrCreateDailyStat(DateTime.Now.Date.ToString("yyyy-MM-dd"));
            stat.MoneyEarned += amount;
        }

        private void RecordDailyQuestCompletion()
        {
            DailyStat stat = GetOrCreateDailyStat(DateTime.Now.Date.ToString("yyyy-MM-dd"));
            stat.QuestsCompleted++;
        }

        private void RecordLevelSnapshot(int level)
        {
            levelSnapshots.Add(new LevelSnapshot
            {
                DateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Level = level
            });
        }

        private DailyStat GetOrCreateDailyStat(string dateKey)
        {
            DailyStat stat = dailyStats.FirstOrDefault(entry => entry.Date == dateKey);
            if (stat == null)
            {
                stat = new DailyStat { Date = dateKey, QuestsCompleted = 0, MoneyEarned = 0 };
                dailyStats.Add(stat);
            }

            return stat;
        }

        /// <summary>
        /// Resets progression data (for new game)
        /// </summary>
        public void ResetProgression()
        {
            currentMoney = 500;
            currentLevel = 1;
            currentXP = 0;
            xpToNextLevel = CalculateXPForLevel(2);
            totalQuestsCompleted = 0;
            totalQuestsAttempted = 0;
            totalQuestsFailed = 0;
            totalMoneyEarned = 0;
            totalDistanceTraveled = 0f;
            totalTimePlayed = 0f;
            totalDeliveryTimeSeconds = 0f;
            fastestDeliveryTimeSeconds = 0f;
            speedBonusesEarned = 0;
            sRanksAchieved = 0;
            totalCargoWeightDelivered = 0f;
            fragileCargoDeliveredUndamaged = 0;
            cargoTypeStats ??= new List<CargoTypeStat>();
            dailyStats ??= new List<DailyStat>();
            levelSnapshots ??= new List<LevelSnapshot>();
            cargoTypeStats.Clear();
            dailyStats.Clear();
            levelSnapshots.Clear();

            // Reset achievements
            foreach (Achievement achievement in achievements)
            {
                achievement.IsUnlocked = false;
            }

            SyncMoneyToDatabase();
            OnMoneyChanged.Invoke(currentMoney);
            OnLevelUp.Invoke(currentLevel);

            Debug.Log("[PlayerProgressionManager] Progression reset to defaults.");
        }

        #region Achievement System

        /// <summary>
        /// Initializes the achievement list with predefined achievements
        /// </summary>
        private void InitializeAchievements()
        {
            if (achievements.Count > 0)
            {
                // Achievements already initialized (loaded from save)
                return;
            }

            achievements = new List<Achievement>
            {
                new Achievement("first_delivery", "First Delivery", "Complete your first delivery quest", 100, 25),
                new Achievement("delivery_pro", "Delivery Pro", "Complete 10 delivery quests", 500, 100),
                new Achievement("speed_demon", "Speed Demon", "Earn 5 speed bonuses", 300, 75),
                new Achievement("perfect_run", "Perfect Run", "Complete a quest with S rank", 250, 50),
                new Achievement("marathon_driver", "Marathon Driver", "Travel 100 km total", 1000, 200),
                new Achievement("heavy_hauler", "Heavy Hauler", "Deliver 500 kg of cargo total", 750, 150),
                new Achievement("fragile_expert", "Fragile Expert", "Deliver 10 fragile cargos undamaged", 500, 100),
                new Achievement("veteran_driver", "Veteran Driver", "Reach level 10", 1000, 250),
                new Achievement("wealthy_driver", "Wealthy Driver", "Earn $10,000 total", 2000, 500),
                new Achievement("perfect_streak", "Perfect Streak", "Achieve 5 consecutive S ranks", 1500, 300)
            };

            Debug.Log($"[PlayerProgressionManager] Initialized {achievements.Count} achievements");
        }

        /// <summary>
        /// Checks all achievements and unlocks any that meet their requirements
        /// </summary>
        public void CheckAchievements()
        {
            foreach (Achievement achievement in achievements)
            {
                if (achievement.IsUnlocked)
                {
                    continue; // Skip already unlocked achievements
                }

                bool shouldUnlock = achievement.AchievementID switch
                {
                    "first_delivery" => totalQuestsCompleted >= 1,
                    "delivery_pro" => totalQuestsCompleted >= 10,
                    "speed_demon" => speedBonusesEarned >= 5,
                    "perfect_run" => sRanksAchieved >= 1,
                    "marathon_driver" => totalDistanceTraveled >= 100000f, // 100 km in meters
                    "heavy_hauler" => totalCargoWeightDelivered >= 500f,
                    "fragile_expert" => fragileCargoDeliveredUndamaged >= 10,
                    "veteran_driver" => currentLevel >= 10,
                    "wealthy_driver" => currentMoney >= 10000,
                    "perfect_streak" => sRanksAchieved >= 5,
                    _ => false
                };

                if (shouldUnlock)
                {
                    UnlockAchievement(achievement.AchievementID);
                }
            }
        }

        /// <summary>
        /// Unlocks a specific achievement by ID and awards its rewards
        /// </summary>
        /// <param name="achievementID">The ID of the achievement to unlock</param>
        public void UnlockAchievement(string achievementID)
        {
            Achievement achievement = achievements.FirstOrDefault(a => a.AchievementID == achievementID);

            if (achievement == null)
            {
                Debug.LogWarning($"[PlayerProgressionManager] Achievement '{achievementID}' not found");
                return;
            }

            if (achievement.IsUnlocked)
            {
                return; // Already unlocked
            }

            achievement.Unlock();

            // Award rewards
            if (achievement.RewardMoney > 0)
            {
                AwardMoney(achievement.RewardMoney);
            }

            if (achievement.RewardXP > 0)
            {
                AwardXP(achievement.RewardXP);
            }

            // Invoke event for UI notification
            OnAchievementUnlocked.Invoke(achievement);

            Debug.Log($"[PlayerProgressionManager] Achievement unlocked: {achievement.Name} (+${achievement.RewardMoney}, +{achievement.RewardXP} XP)");
        }

        /// <summary>
        /// Records a quest attempt when the player accepts a quest.
        /// </summary>
        /// <param name="quest">The accepted quest data.</param>
        public void RecordQuestAttempt(QuestData quest)
        {
            if (quest == null)
            {
                return;
            }

            totalQuestsAttempted++;
        }

        /// <summary>
        /// Records a quest failure for analytics tracking.
        /// </summary>
        /// <param name="quest">The failed quest data.</param>
        public void RecordQuestFailure(QuestData quest)
        {
            if (quest == null)
            {
                return;
            }

            totalQuestsFailed++;
        }

        /// <summary>
        /// Records a quest completion and updates relevant statistics.
        /// </summary>
        /// <param name="quest">The completed quest data.</param>
        /// <param name="reward">Final reward awarded for the quest.</param>
        /// <param name="completionTimeSeconds">Completion time in seconds.</param>
        public void RecordQuestCompletion(QuestData quest, int reward, float completionTimeSeconds)
        {
            if (quest == null)
            {
                return;
            }

            // Update statistics based on quest performance
            if (quest.EarnedBonus)
            {
                speedBonusesEarned++;
            }

            if (quest.Rating == PerformanceRating.S)
            {
                sRanksAchieved++;
            }

            if (quest.Cargo != null)
            {
                totalCargoWeightDelivered += quest.Cargo.Weight;
                UpdateCargoTypeStats(quest.Cargo.CargoName);

                // Check if fragile cargo was delivered undamaged
                if (quest.Cargo.IsFragile && quest.Cargo.CargoHealth >= 90f)
                {
                    fragileCargoDeliveredUndamaged++;
                }
            }

            float clampedCompletionTime = Mathf.Max(0f, completionTimeSeconds);
            if (clampedCompletionTime > 0f)
            {
                totalDeliveryTimeSeconds += clampedCompletionTime;
                if (fastestDeliveryTimeSeconds <= 0f || clampedCompletionTime < fastestDeliveryTimeSeconds)
                {
                    fastestDeliveryTimeSeconds = clampedCompletionTime;
                }
            }

            RecordDailyQuestCompletion();

            // Check achievements after recording stats
            CheckAchievements();
        }

        /// <summary>
        /// Gets the number of unlocked achievements
        /// </summary>
        /// <returns>Count of unlocked achievements</returns>
        public int GetUnlockedAchievementCount()
        {
            return achievements.Count(a => a.IsUnlocked);
        }

        /// <summary>
        /// Gets the total number of achievements
        /// </summary>
        /// <returns>Total achievement count</returns>
        public int GetTotalAchievementCount()
        {
            return achievements.Count;
        }

        /// <summary>
        /// Gets achievement progress as a percentage
        /// </summary>
        /// <returns>Percentage of achievements unlocked (0-1)</returns>
        public float GetAchievementProgress()
        {
            if (achievements.Count == 0)
            {
                return 0f;
            }

            return (float)GetUnlockedAchievementCount() / achievements.Count;
        }

        #endregion

        #region Save/Load System

        /// <summary>
        /// Loads player progression data from save
        /// </summary>
        /// <param name="data">Player progression data to load</param>
        public void LoadSaveData(PlayerProgressionData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[PlayerProgressionManager] LoadSaveData called with null data.");
                return;
            }

            currentMoney = data.Money;
            currentLevel = data.Level;
            currentXP = data.XP;
            xpToNextLevel = data.XPToNextLevel;
            totalQuestsCompleted = data.TotalQuestsCompleted;
            totalQuestsAttempted = data.TotalQuestsAttempted;
            totalQuestsFailed = data.TotalQuestsFailed;
            totalMoneyEarned = data.TotalMoneyEarned;
            totalDistanceTraveled = data.TotalDistanceTraveled;
            totalTimePlayed = data.TotalTimePlayed;
            totalDeliveryTimeSeconds = data.TotalDeliveryTimeSeconds;
            fastestDeliveryTimeSeconds = data.FastestDeliveryTimeSeconds;
            speedBonusesEarned = data.SpeedBonusesEarned;
            sRanksAchieved = data.SRanksAchieved;
            totalCargoWeightDelivered = data.TotalCargoWeightDelivered;
            fragileCargoDeliveredUndamaged = data.FragileCargoDeliveredUndamaged;
            cargoTypeStats = data.CargoTypeStats ?? new List<CargoTypeStat>();
            dailyStats = data.DailyStats ?? new List<DailyStat>();
            levelSnapshots = data.LevelSnapshots ?? new List<LevelSnapshot>();

            // Load unlocked achievements
            foreach (string achievementID in data.UnlockedAchievements)
            {
                Achievement achievement = achievements.FirstOrDefault(a => a.AchievementID == achievementID);
                if (achievement != null && !achievement.IsUnlocked)
                {
                    achievement.IsUnlocked = true;
                }
            }

            SyncMoneyToDatabase();
            // Invoke events to update UI
            OnMoneyChanged.Invoke(currentMoney);
            OnLevelUp.Invoke(currentLevel);

            Debug.Log($"[PlayerProgressionManager] Loaded save data: Level {currentLevel}, ${currentMoney}, {currentXP}/{xpToNextLevel} XP");
        }

        private IEnumerator InitializeMoneyFromDatabaseRoutine()
        {
            float timeoutAt = Time.realtimeSinceStartup + DatabaseReadyWaitTimeoutSeconds;

            while ((QuestDatabaseService.Instance == null || !QuestDatabaseService.Instance.IsReady) &&
                   Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            QuestDatabaseService database = QuestDatabaseService.Instance;
            if (database == null || !database.IsReady)
            {
                yield break;
            }

            bool playerExisted = database.PlayerExists(QuestDatabaseService.DefaultPlayerId);
            if (!database.EnsureDefaultPlayer())
            {
                yield break;
            }

            int databaseBalance = database.GetDefaultPlayerBalance(currentMoney);
            bool shouldSeedDatabaseBalance = !playerExisted || (databaseBalance <= 0 && currentMoney > 0);

            if (shouldSeedDatabaseBalance)
            {
                database.SetDefaultPlayerBalance(currentMoney);
            }
            else if (databaseBalance != currentMoney)
            {
                currentMoney = Mathf.Max(0, databaseBalance);
            }

            OnMoneyChanged.Invoke(currentMoney);
            Debug.Log($"[PlayerProgressionManager] Active balance synced from database: ${currentMoney}");
        }

        private void SyncMoneyToDatabase()
        {
            QuestDatabaseService database = QuestDatabaseService.Instance;
            if (database == null || !database.IsReady)
            {
                return;
            }

            database.EnsureDefaultPlayer();
            database.SetDefaultPlayerBalance(currentMoney);
        }

        #endregion
    }
}
