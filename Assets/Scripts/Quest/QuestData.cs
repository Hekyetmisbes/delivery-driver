using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDriver.Quest
{
    /// <summary>
    /// Represents a quest/delivery mission with all its data and runtime state
    /// </summary>
    [System.Serializable]
    public class QuestData
    {
        /// <summary>
        /// Unique identifier for this quest (GUID)
        /// </summary>
        public string QuestID;

        /// <summary>
        /// Display name of the quest
        /// </summary>
        public string QuestName;

        /// <summary>
        /// Full description of the quest
        /// </summary>
        [TextArea(3, 5)]
        public string QuestDescription;

        /// <summary>
        /// Type of quest (StandardDelivery, Express, Fragile, etc.)
        /// </summary>
        public QuestType QuestType;

        /// <summary>
        /// Difficulty level of the quest
        /// </summary>
        public QuestDifficulty Difficulty;

        /// <summary>
        /// Current status of the quest
        /// </summary>
        public QuestStatus Status;

        /// <summary>
        /// Location where cargo should be picked up
        /// </summary>
        public QuestLocation PickupLocation;

        /// <summary>
        /// List of delivery locations (supports multi-stop quests)
        /// </summary>
        public List<QuestLocation> DeliveryLocations;

        /// <summary>
        /// The cargo to be transported
        /// </summary>
        public CargoData Cargo;

        /// <summary>
        /// Total time allowed to complete the quest in seconds
        /// </summary>
        public float TimeLimit;

        /// <summary>
        /// Current time remaining in seconds (countdown)
        /// </summary>
        public float TimeRemaining;

        /// <summary>
        /// Base currency reward for completing the quest
        /// </summary>
        public int BaseReward;

        /// <summary>
        /// Bonus reward for fast completion
        /// </summary>
        public int BonusReward;

        /// <summary>
        /// Percentage of time remaining needed to earn bonus (e.g., 0.5 = 50% time left)
        /// </summary>
        [Range(0f, 1f)]
        public float BonusTimeThreshold;

        /// <summary>
        /// Player level required to accept this quest
        /// </summary>
        public int RequiredLevel;

        /// <summary>
        /// Whether this quest can be done multiple times
        /// </summary>
        public bool IsRepeatable;

        /// <summary>
        /// XP reward for completing the quest
        /// </summary>
        public int XPReward;

        /// <summary>
        /// Default constructor with default initialization
        /// </summary>
        public QuestData()
        {
            QuestID = Guid.NewGuid().ToString();
            QuestName = "New Quest";
            QuestDescription = "";
            QuestType = QuestType.StandardDelivery;
            Difficulty = QuestDifficulty.Easy;
            Status = QuestStatus.NotStarted;
            DeliveryLocations = new List<QuestLocation>();
            TimeLimit = 300f; // 5 minutes default
            TimeRemaining = TimeLimit;
            BaseReward = 100;
            BonusReward = 50;
            BonusTimeThreshold = 0.5f;
            RequiredLevel = 1;
            IsRepeatable = true;
            XPReward = 50;
        }

        /// <summary>
        /// Starts the quest, setting status to Active and initializing timer
        /// </summary>
        public void StartQuest()
        {
            if (Status != QuestStatus.NotStarted)
            {
                Debug.LogWarning($"Quest '{QuestName}' cannot be started. Current status: {Status}");
                return;
            }

            Status = QuestStatus.Active;
            TimeRemaining = TimeLimit;

            // Initialize cargo health if fragile
            if (Cargo != null && Cargo.IsFragile)
            {
                Cargo.RestoreHealth();
            }

            Debug.Log($"Quest '{QuestName}' started! Time limit: {TimeLimit} seconds");
        }

        /// <summary>
        /// Updates the quest timer by decrementing time
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update (typically Time.deltaTime)</param>
        public void UpdateTimer(float deltaTime)
        {
            if (Status != QuestStatus.Active)
            {
                return;
            }

            TimeRemaining -= deltaTime;
            TimeRemaining = Mathf.Max(0f, TimeRemaining);
        }

        /// <summary>
        /// Checks if the quest timer has expired
        /// </summary>
        /// <returns>True if time remaining is zero or below, false otherwise</returns>
        public bool IsTimeExpired()
        {
            return TimeRemaining <= 0f;
        }

        /// <summary>
        /// Calculates the final reward based on completion time
        /// </summary>
        /// <returns>Total reward amount including bonus if applicable</returns>
        public int CalculateFinalReward()
        {
            // Check if player earned the time bonus
            if (TimeRemaining > TimeLimit * BonusTimeThreshold)
            {
                return BaseReward + BonusReward;
            }
            else
            {
                return BaseReward;
            }
        }

        /// <summary>
        /// Gets the time remaining formatted as MM:SS
        /// </summary>
        /// <returns>Formatted time string</returns>
        public string GetFormattedTimeRemaining()
        {
            int minutes = Mathf.FloorToInt(TimeRemaining / 60f);
            int seconds = Mathf.FloorToInt(TimeRemaining % 60f);
            return $"{minutes:00}:{seconds:00}";
        }

        /// <summary>
        /// Gets the percentage of time remaining (0-1)
        /// </summary>
        /// <returns>Time remaining as a percentage of total time</returns>
        public float GetTimeRemainingPercentage()
        {
            if (TimeLimit <= 0f)
            {
                return 0f;
            }
            return Mathf.Clamp01(TimeRemaining / TimeLimit);
        }

        /// <summary>
        /// Checks if the bonus time threshold has been met
        /// </summary>
        /// <returns>True if player will earn bonus reward</returns>
        public bool WillEarnBonus()
        {
            return TimeRemaining > TimeLimit * BonusTimeThreshold;
        }
    }
}
