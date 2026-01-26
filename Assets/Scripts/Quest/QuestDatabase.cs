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
