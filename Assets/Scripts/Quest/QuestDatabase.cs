using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DeliveryDriver.Quest
{
    /// <summary>
    /// ScriptableObject database that stores quest templates and provides quest generation
    /// </summary>
    [CreateAssetMenu(fileName = "QuestDatabase", menuName = "Quest System/Quest Database", order = 1)]
    public class QuestDatabase : ScriptableObject
    {
        /// <summary>
        /// List of all available quest templates
        /// </summary>
        [SerializeField]
        public List<QuestTemplate> AvailableQuests = new List<QuestTemplate>();

        /// <summary>
        /// Gets a quest by its unique ID
        /// </summary>
        /// <param name="id">The quest ID to search for</param>
        /// <returns>QuestData instance if found, null otherwise</returns>
        public QuestData GetQuestByID(string id)
        {
            QuestTemplate template = AvailableQuests.FirstOrDefault(q => q.TemplateID == id);

            if (template != null)
            {
                return template.CreateQuestData();
            }

            Debug.LogWarning($"Quest with ID '{id}' not found in database");
            return null;
        }

        /// <summary>
        /// Gets all quests of a specific difficulty level
        /// </summary>
        /// <param name="difficulty">The difficulty level to filter by</param>
        /// <returns>List of QuestData instances matching the difficulty</returns>
        public List<QuestData> GetQuestsByDifficulty(QuestDifficulty difficulty)
        {
            List<QuestData> quests = new List<QuestData>();

            foreach (QuestTemplate template in AvailableQuests)
            {
                if (template.Difficulty == difficulty)
                {
                    quests.Add(template.CreateQuestData());
                }
            }

            return quests;
        }

        /// <summary>
        /// Generates a random quest of the specified difficulty
        /// </summary>
        /// <param name="difficulty">The desired difficulty level</param>
        /// <returns>A random QuestData instance, or null if none available</returns>
        public QuestData GenerateRandomQuest(QuestDifficulty difficulty)
        {
            List<QuestTemplate> matchingTemplates = AvailableQuests
                .Where(q => q.Difficulty == difficulty)
                .ToList();

            if (matchingTemplates.Count == 0)
            {
                Debug.LogWarning($"No quest templates found for difficulty: {difficulty}");
                return null;
            }

            int randomIndex = UnityEngine.Random.Range(0, matchingTemplates.Count);
            return matchingTemplates[randomIndex].CreateQuestData();
        }

        /// <summary>
        /// Gets a random quest from all available templates
        /// </summary>
        /// <returns>A random QuestData instance</returns>
        public QuestData GetRandomQuest()
        {
            if (AvailableQuests.Count == 0)
            {
                Debug.LogWarning("Quest database is empty");
                return null;
            }

            int randomIndex = UnityEngine.Random.Range(0, AvailableQuests.Count);
            return AvailableQuests[randomIndex].CreateQuestData();
        }

        /// <summary>
        /// Gets all quest templates available for the given player level
        /// </summary>
        /// <param name="playerLevel">Current player level</param>
        /// <returns>List of quest templates that meet the level requirement</returns>
        public List<QuestTemplate> GetAvailableQuestsForLevel(int playerLevel)
        {
            return AvailableQuests
                .Where(q => q.RequiredLevel <= playerLevel)
                .ToList();
        }

        /// <summary>
        /// Generates a random quest appropriate for the player's level
        /// </summary>
        /// <param name="playerLevel">Current player level</param>
        /// <returns>A random QuestData instance appropriate for the level</returns>
        public QuestData GenerateRandomQuestForLevel(int playerLevel)
        {
            List<QuestTemplate> availableTemplates = GetAvailableQuestsForLevel(playerLevel);

            if (availableTemplates.Count == 0)
            {
                Debug.LogWarning($"No quest templates available for level {playerLevel}");
                return null;
            }

            // Get appropriate difficulties for player level
            List<QuestDifficulty> appropriateDifficulties = GetAppropriateDifficultiesForLevel(playerLevel);

            // Filter templates by appropriate difficulties
            List<QuestTemplate> filteredTemplates = availableTemplates
                .Where(q => appropriateDifficulties.Contains(q.Difficulty))
                .ToList();

            // Fallback to all available templates if no filtered matches
            if (filteredTemplates.Count == 0)
            {
                filteredTemplates = availableTemplates;
            }

            int randomIndex = UnityEngine.Random.Range(0, filteredTemplates.Count);
            return filteredTemplates[randomIndex].CreateQuestData();
        }

        /// <summary>
        /// Gets appropriate quest difficulties for the player's level
        /// </summary>
        /// <param name="playerLevel">Current player level</param>
        /// <returns>List of appropriate difficulties</returns>
        private List<QuestDifficulty> GetAppropriateDifficultiesForLevel(int playerLevel)
        {
            List<QuestDifficulty> difficulties = new List<QuestDifficulty>();

            if (playerLevel >= 1 && playerLevel <= 5)
            {
                // Level 1-5: Easy and Medium quests
                difficulties.Add(QuestDifficulty.Easy);
                difficulties.Add(QuestDifficulty.Medium);
            }
            else if (playerLevel >= 6 && playerLevel <= 15)
            {
                // Level 6-15: Medium and Hard quests
                difficulties.Add(QuestDifficulty.Medium);
                difficulties.Add(QuestDifficulty.Hard);
            }
            else if (playerLevel >= 16 && playerLevel <= 30)
            {
                // Level 16-30: Hard and Expert quests
                difficulties.Add(QuestDifficulty.Hard);
                difficulties.Add(QuestDifficulty.Expert);
            }
            else if (playerLevel >= 31)
            {
                // Level 31+: All difficulties (all quests + special challenges)
                difficulties.Add(QuestDifficulty.Easy);
                difficulties.Add(QuestDifficulty.Medium);
                difficulties.Add(QuestDifficulty.Hard);
                difficulties.Add(QuestDifficulty.Expert);
            }

            return difficulties;
        }

        /// <summary>
        /// Checks if a quest template is unlocked for the given player level
        /// </summary>
        /// <param name="template">Quest template to check</param>
        /// <param name="playerLevel">Current player level</param>
        /// <returns>True if quest is unlocked, false otherwise</returns>
        public bool IsQuestUnlockedForLevel(QuestTemplate template, int playerLevel)
        {
            return template.RequiredLevel <= playerLevel;
        }

        /// <summary>
        /// Template class for quest data - used as blueprint for generating QuestData instances
        /// </summary>
        [System.Serializable]
        public class QuestTemplate
        {
            /// <summary>
            /// Unique identifier for this template
            /// </summary>
            public string TemplateID;

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
            /// Type of quest
            /// </summary>
            public QuestType QuestType;

            /// <summary>
            /// Difficulty level
            /// </summary>
            public QuestDifficulty Difficulty;

            /// <summary>
            /// Number of delivery stops (1 for single delivery, 2+ for multi-stop)
            /// </summary>
            [Range(1, 5)]
            public int DeliveryStopCount = 1;

            /// <summary>
            /// Time limit in seconds
            /// </summary>
            public float TimeLimit;

            /// <summary>
            /// Base currency reward
            /// </summary>
            public int BaseReward;

            /// <summary>
            /// Bonus reward for fast completion
            /// </summary>
            public int BonusReward;

            /// <summary>
            /// Percentage of time remaining needed for bonus
            /// </summary>
            [Range(0f, 1f)]
            public float BonusTimeThreshold = 0.5f;

            /// <summary>
            /// Player level required to accept this quest
            /// </summary>
            public int RequiredLevel;

            /// <summary>
            /// Whether this quest is unlocked for the player
            /// </summary>
            [System.NonSerialized]
            public bool IsUnlocked = true;

            /// <summary>
            /// Whether this quest can be repeated
            /// </summary>
            public bool IsRepeatable = true;

            /// <summary>
            /// XP reward for completion
            /// </summary>
            public int XPReward;

            /// <summary>
            /// Cargo name for this quest
            /// </summary>
            public string CargoName;

            /// <summary>
            /// Cargo weight in kg
            /// </summary>
            [Range(0f, 500f)]
            public float CargoWeight = 100f;

            /// <summary>
            /// Whether the cargo is fragile
            /// </summary>
            public bool IsCargoFragile = false;

            /// <summary>
            /// Cargo description
            /// </summary>
            [TextArea(2, 3)]
            public string CargoDescription;

            /// <summary>
            /// Creates a QuestData instance from this template
            /// </summary>
            /// <returns>New QuestData instance with unique ID</returns>
            public QuestData CreateQuestData()
            {
                QuestData quest = new QuestData
                {
                    QuestID = Guid.NewGuid().ToString(), // Generate unique ID for each instance
                    QuestName = QuestName,
                    QuestDescription = QuestDescription,
                    QuestType = QuestType,
                    Difficulty = Difficulty,
                    Status = QuestStatus.NotStarted,
                    TimeLimit = TimeLimit,
                    TimeRemaining = TimeLimit,
                    BaseReward = BaseReward,
                    BonusReward = BonusReward,
                    BonusTimeThreshold = BonusTimeThreshold,
                    RequiredLevel = RequiredLevel,
                    IsRepeatable = IsRepeatable,
                    XPReward = XPReward,
                    DeliveryLocations = new List<QuestLocation>()
                };

                // Create cargo data
                quest.Cargo = new CargoData(CargoName, CargoWeight, IsCargoFragile, CargoDescription);

                // Note: Pickup and delivery locations will be generated at runtime by QuestManager
                // based on the road network

                return quest;
            }
        }
    }
}
