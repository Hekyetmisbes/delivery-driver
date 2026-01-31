using UnityEngine;

namespace DeliveryDriver.Quest
{
    /// <summary>
    /// Represents an achievement that can be unlocked by completing specific tasks
    /// </summary>
    [System.Serializable]
    public class Achievement
    {
        /// <summary>
        /// Unique identifier for this achievement
        /// </summary>
        public string AchievementID;

        /// <summary>
        /// Display name of the achievement
        /// </summary>
        public string Name;

        /// <summary>
        /// Description of how to unlock this achievement
        /// </summary>
        [TextArea(2, 3)]
        public string Description;

        /// <summary>
        /// Icon sprite for UI display
        /// </summary>
        public Sprite Icon;

        /// <summary>
        /// Whether this achievement has been unlocked
        /// </summary>
        public bool IsUnlocked;

        /// <summary>
        /// Money reward for unlocking this achievement
        /// </summary>
        public int RewardMoney;

        /// <summary>
        /// XP reward for unlocking this achievement
        /// </summary>
        public int RewardXP;

        /// <summary>
        /// Default constructor
        /// </summary>
        public Achievement()
        {
            AchievementID = "";
            Name = "New Achievement";
            Description = "Complete this task to unlock.";
            Icon = null;
            IsUnlocked = false;
            RewardMoney = 100;
            RewardXP = 50;
        }

        /// <summary>
        /// Constructor with parameters
        /// </summary>
        public Achievement(string id, string name, string description, int rewardMoney = 100, int rewardXP = 50)
        {
            AchievementID = id;
            Name = name;
            Description = description;
            Icon = null;
            IsUnlocked = false;
            RewardMoney = rewardMoney;
            RewardXP = rewardXP;
        }

        /// <summary>
        /// Unlocks the achievement
        /// </summary>
        public void Unlock()
        {
            if (!IsUnlocked)
            {
                IsUnlocked = true;
                Debug.Log($"[Achievement] Unlocked: {Name}");
            }
        }
    }
}
