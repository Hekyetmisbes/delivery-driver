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
        /// True if the player has picked up the cargo
        /// </summary>
        public bool HasPickedUpCargo;

        /// <summary>
        /// Index of the current delivery target (for multi-stop quests)
        /// </summary>
        public int CurrentDeliveryIndex;

        /// <summary>
        /// Time when quest was started (Time.time)
        /// </summary>
        public float StartTime;

        /// <summary>
        /// Accumulated pause time (for pause menu support)
        /// </summary>
        public float PausedTime;

        /// <summary>
        /// Whether the timer is currently paused
        /// </summary>
        public bool IsPaused;

        /// <summary>
        /// True if the player earned the bonus reward
        /// </summary>
        public bool EarnedBonus { get; private set; }

        /// <summary>
        /// Total distance traveled during quest (in meters)
        /// </summary>
        public float TotalDistanceTraveled;

        /// <summary>
        /// Last recorded player position (for distance tracking)
        /// </summary>
        [System.NonSerialized]
        public Vector3 LastPosition;

        /// <summary>
        /// Total number of collisions during quest
        /// </summary>
        public int CollisionCount;

        /// <summary>
        /// Number of collisions with NPC vehicles
        /// </summary>
        public int NpcCollisionCount;

        /// <summary>
        /// Number of hard braking events during quest
        /// </summary>
        public int HardBrakeCount;

        /// <summary>
        /// Accumulated drift score gathered during quest.
        /// </summary>
        public int DriftScorePoints;

        /// <summary>
        /// Performance rating for this quest
        /// </summary>
        public PerformanceRating Rating { get; private set; }

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
            HasPickedUpCargo = false;
            CurrentDeliveryIndex = 0;
            StartTime = 0f;
            PausedTime = 0f;
            IsPaused = false;
            EarnedBonus = false;
            TotalDistanceTraveled = 0f;
            LastPosition = Vector3.zero;
            CollisionCount = 0;
            NpcCollisionCount = 0;
            HardBrakeCount = 0;
            DriftScorePoints = 0;
            Rating = PerformanceRating.C;
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
            HasPickedUpCargo = false;
            CurrentDeliveryIndex = 0;
            StartTime = Time.time;
            PausedTime = 0f;
            IsPaused = false;
            EarnedBonus = false;
            TotalDistanceTraveled = 0f;
            LastPosition = Vector3.zero;
            CollisionCount = 0;
            NpcCollisionCount = 0;
            HardBrakeCount = 0;
            DriftScorePoints = 0;

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

            // Don't decrement timer if paused
            if (IsPaused)
            {
                return;
            }

            TimeRemaining -= deltaTime;
            TimeRemaining = Mathf.Max(0f, TimeRemaining);
        }

        /// <summary>
        /// Pauses the quest timer (for pause menu)
        /// </summary>
        public void PauseQuest()
        {
            if (Status == QuestStatus.Active && !IsPaused)
            {
                IsPaused = true;
                Debug.Log($"Quest '{QuestName}' paused.");
            }
        }

        /// <summary>
        /// Resumes the quest timer after pause
        /// </summary>
        public void ResumeQuest()
        {
            if (Status == QuestStatus.Active && IsPaused)
            {
                IsPaused = false;
                Debug.Log($"Quest '{QuestName}' resumed.");
            }
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
            int totalReward = BaseReward;

            // Calculate completion percentage
            float completionPercent = 0f;
            if (TimeLimit > 0f)
            {
                completionPercent = TimeRemaining / TimeLimit;
            }

            // Determine if time bonus was earned
            if (completionPercent >= BonusTimeThreshold)
            {
                EarnedBonus = true;

                // Speed bonus multiplier tiers
                float bonusMultiplier = 1.0f;
                if (completionPercent >= 0.75f)
                {
                    // Very fast completion (75%+ time left): 1.5x bonus
                    bonusMultiplier = 1.5f;
                }
                else if (completionPercent >= 0.5f)
                {
                    // Fast completion (50-75% time left): 1.0x bonus
                    bonusMultiplier = 1.0f;
                }

                int bonusAmount = Mathf.RoundToInt(BonusReward * bonusMultiplier);
                totalReward += bonusAmount;
            }
            else
            {
                EarnedBonus = false;
            }

            // Perfect delivery bonus (no collisions)
            if (CollisionCount == 0)
            {
                totalReward += 200; // $200 perfect delivery bonus
            }

            // Drift bonus rewards controlled sliding during delivery.
            if (DriftScorePoints > 0)
            {
                totalReward += Mathf.RoundToInt(DriftScorePoints * 2f);
            }

            // Apply collision penalties
            int penalty = (CollisionCount * 10) + (NpcCollisionCount * 100);
            totalReward -= penalty;

            // Ensure reward is never negative
            return Mathf.Max(0, totalReward);
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

        /// <summary>
        /// Updates distance tracking based on player position
        /// </summary>
        /// <param name="currentPosition">Current player position</param>
        public void UpdateDistanceTracking(Vector3 currentPosition)
        {
            if (LastPosition == Vector3.zero)
            {
                LastPosition = currentPosition;
                return;
            }

            float distance = Vector3.Distance(currentPosition, LastPosition);
            TotalDistanceTraveled += distance;
            LastPosition = currentPosition;
        }

        /// <summary>
        /// Gets the total distance traveled formatted in kilometers
        /// </summary>
        /// <returns>Formatted distance string (e.g., "2.3 km")</returns>
        public string GetFormattedDistance()
        {
            float distanceKm = TotalDistanceTraveled / 1000f;
            return $"{distanceKm:F1} km";
        }

        /// <summary>
        /// Calculates the straight-line distance from pickup to delivery
        /// </summary>
        /// <returns>Distance in meters</returns>
        public float GetOptimalRouteDistance()
        {
            if (PickupLocation == null || DeliveryLocations == null || DeliveryLocations.Count == 0)
            {
                return 0f;
            }

            float totalDistance = 0f;
            Vector3 currentPos = PickupLocation.Position;

            foreach (QuestLocation delivery in DeliveryLocations)
            {
                if (delivery != null)
                {
                    totalDistance += Vector3.Distance(currentPos, delivery.Position);
                    currentPos = delivery.Position;
                }
            }

            // Account for road network (straight line * 1.5 approximation)
            return totalDistance * 1.5f;
        }

        /// <summary>
        /// Increments collision count
        /// </summary>
        /// <param name="isNpcCollision">True if collision was with an NPC vehicle</param>
        public void RecordCollision(bool isNpcCollision = false)
        {
            CollisionCount++;
            if (isNpcCollision)
            {
                NpcCollisionCount++;
            }
        }

        /// <summary>
        /// Increments hard brake count.
        /// </summary>
        public void RecordHardBrake()
        {
            HardBrakeCount++;
        }

        /// <summary>
        /// Adds drift score points for the active quest.
        /// </summary>
        public void RecordDriftScore(int points)
        {
            if (points <= 0)
            {
                return;
            }

            DriftScorePoints += points;
        }

        /// <summary>
        /// Gets whether this delivery was perfect (no collisions)
        /// </summary>
        /// <returns>True if no collisions occurred</returns>
        public bool IsPerfectDelivery()
        {
            return CollisionCount == 0;
        }

        /// <summary>
        /// Calculates the performance rating based on quest completion quality
        /// </summary>
        /// <returns>Performance rating from F to S</returns>
        public PerformanceRating CalculateRating()
        {
            // Failed quests always get F rank
            if (Status == QuestStatus.Failed)
            {
                Rating = PerformanceRating.F;
                return Rating;
            }

            // Check cargo health percentage (if fragile cargo exists)
            float cargoHealthPercent = 100f;
            if (Cargo != null && Cargo.IsFragile)
            {
                cargoHealthPercent = Cargo.CargoHealth;
            }

            // Calculate time remaining percentage
            float timePercent = GetTimeRemainingPercentage();

            // S Rank: Bonus earned + zero collisions + cargo health > 90%
            if (EarnedBonus && CollisionCount == 0 && cargoHealthPercent > 90f)
            {
                Rating = PerformanceRating.S;
            }
            // A Rank: Bonus earned + less than 2 collisions
            else if (EarnedBonus && CollisionCount < 2)
            {
                Rating = PerformanceRating.A;
            }
            // B Rank: Completed with time remaining (>10%)
            else if (timePercent > 0.1f)
            {
                Rating = PerformanceRating.B;
            }
            // C Rank: Completed barely (< 10% time remaining)
            else if (Status == QuestStatus.Completed)
            {
                Rating = PerformanceRating.C;
            }
            // D Rank: Cargo damaged significantly
            else if (cargoHealthPercent < 50f)
            {
                Rating = PerformanceRating.D;
            }
            else
            {
                Rating = PerformanceRating.C;
            }

            return Rating;
        }

        /// <summary>
        /// Gets the rating as a display string with color indicator
        /// </summary>
        /// <returns>Formatted rating string</returns>
        public string GetRatingDisplay()
        {
            return Rating.ToString();
        }
    }
}
