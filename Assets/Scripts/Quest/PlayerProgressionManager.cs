using System;
using UnityEngine;
using UnityEngine.Events;

namespace DeliveryDriver.Quest
{
    /// <summary>
    /// Manages player progression, currency, level, and experience points
    /// </summary>
    public class PlayerProgressionManager : MonoBehaviour
    {
        public static PlayerProgressionManager Instance { get; private set; }

        [Header("Currency")]
        [SerializeField] private int currentMoney = 500; // Starting money

        [Header("Level & XP")]
        [SerializeField] private int currentLevel = 1;
        [SerializeField] private int currentXP = 0;
        [SerializeField] private int xpToNextLevel = 100;

        [Header("Statistics")]
        [SerializeField] private int totalQuestsCompleted = 0;
        [SerializeField] private float totalDistanceTraveled = 0f; // In meters
        [SerializeField] private float totalTimePlayed = 0f; // In seconds

        [Header("Events")]
        public UnityEvent<int> OnMoneyChanged = new UnityEvent<int>();
        public UnityEvent<int> OnLevelUp = new UnityEvent<int>();
        public UnityEvent<int> OnXPGained = new UnityEvent<int>();

        // Public read-only properties
        public int CurrentMoney => currentMoney;
        public int CurrentLevel => currentLevel;
        public int CurrentXP => currentXP;
        public int XPToNextLevel => xpToNextLevel;
        public int TotalQuestsCompleted => totalQuestsCompleted;
        public float TotalDistanceTraveled => totalDistanceTraveled;
        public float TotalTimePlayed => totalTimePlayed;

        private void Awake()
        {
            // Singleton pattern implementation
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
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
        /// Resets progression data (for new game)
        /// </summary>
        public void ResetProgression()
        {
            currentMoney = 500;
            currentLevel = 1;
            currentXP = 0;
            xpToNextLevel = CalculateXPForLevel(2);
            totalQuestsCompleted = 0;
            totalDistanceTraveled = 0f;
            totalTimePlayed = 0f;

            OnMoneyChanged.Invoke(currentMoney);
            OnLevelUp.Invoke(currentLevel);

            Debug.Log("[PlayerProgressionManager] Progression reset to defaults.");
        }
    }
}
