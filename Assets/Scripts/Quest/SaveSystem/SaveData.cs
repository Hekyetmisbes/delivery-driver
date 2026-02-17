using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDriver.Quest
{
    /// <summary>
    /// Main save data structure containing all game state
    /// </summary>
    [Serializable]
    public class GameSaveData
    {
        public PlayerProgressionData PlayerData;
        public QuestSaveData QuestData;
        public string SaveDate; // Timestamp
        public int SaveVersion; // For backwards compatibility

        public GameSaveData()
        {
            PlayerData = new PlayerProgressionData();
            QuestData = new QuestSaveData();
            SaveDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            SaveVersion = 3;
        }
    }

    /// <summary>
    /// Serializable player progression data
    /// </summary>
    [Serializable]
    public class PlayerProgressionData
    {
        public int Money;
        public int Level;
        public int XP;
        public int XPToNextLevel;
        public int TotalQuestsCompleted;
        public int TotalQuestsAttempted;
        public int TotalQuestsFailed;
        public int TotalMoneyEarned;
        public float TotalDistanceTraveled;
        public float TotalTimePlayed;
        public float TotalDeliveryTimeSeconds;
        public float FastestDeliveryTimeSeconds;
        public int SpeedBonusesEarned;
        public int SRanksAchieved;
        public float TotalCargoWeightDelivered;
        public int FragileCargoDeliveredUndamaged;
        public List<PlayerProgressionManager.CargoTypeStat> CargoTypeStats = new List<PlayerProgressionManager.CargoTypeStat>();
        public List<PlayerProgressionManager.DailyStat> DailyStats = new List<PlayerProgressionManager.DailyStat>();
        public List<PlayerProgressionManager.LevelSnapshot> LevelSnapshots = new List<PlayerProgressionManager.LevelSnapshot>();
        public List<string> UnlockedAchievements = new List<string>();
        public DriverProgressionSaveData DriverProgressionData = new DriverProgressionSaveData();

        /// <summary>
        /// Creates PlayerProgressionData from PlayerProgressionManager
        /// </summary>
        public static PlayerProgressionData FromManager(PlayerProgressionManager manager)
        {
            if (manager == null)
            {
                return new PlayerProgressionData();
            }

            PlayerProgressionData data = new PlayerProgressionData
            {
                Money = manager.CurrentMoney,
                Level = manager.CurrentLevel,
                XP = manager.CurrentXP,
                XPToNextLevel = manager.XPToNextLevel,
                TotalQuestsCompleted = manager.TotalQuestsCompleted,
                TotalQuestsAttempted = manager.TotalQuestsAttempted,
                TotalQuestsFailed = manager.TotalQuestsFailed,
                TotalMoneyEarned = manager.TotalMoneyEarned,
                TotalDistanceTraveled = manager.TotalDistanceTraveled,
                TotalTimePlayed = manager.TotalTimePlayed,
                TotalDeliveryTimeSeconds = manager.TotalDeliveryTimeSeconds,
                FastestDeliveryTimeSeconds = manager.FastestDeliveryTimeSeconds,
                SpeedBonusesEarned = manager.SpeedBonusesEarned,
                SRanksAchieved = manager.SRanksAchieved,
                TotalCargoWeightDelivered = manager.TotalCargoWeightDelivered,
                FragileCargoDeliveredUndamaged = manager.FragileCargoDeliveredUndamaged,
                CargoTypeStats = new List<PlayerProgressionManager.CargoTypeStat>(manager.CargoTypeStats),
                DailyStats = new List<PlayerProgressionManager.DailyStat>(manager.DailyStats),
                LevelSnapshots = new List<PlayerProgressionManager.LevelSnapshot>(manager.LevelSnapshots),
                UnlockedAchievements = new List<string>(),
                DriverProgressionData = DriverProgressionSystem.Instance != null
                    ? DriverProgressionSystem.Instance.GetSaveData()
                    : new DriverProgressionSaveData()
            };

            // Store unlocked achievement IDs
            foreach (Achievement achievement in manager.Achievements)
            {
                if (achievement.IsUnlocked)
                {
                    data.UnlockedAchievements.Add(achievement.AchievementID);
                }
            }

            return data;
        }

        /// <summary>
        /// Loads data into PlayerProgressionManager
        /// </summary>
        public void LoadIntoManager(PlayerProgressionManager manager)
        {
            if (manager == null)
            {
                Debug.LogWarning("[SaveData] Cannot load into null manager.");
                return;
            }

            manager.LoadSaveData(this);
            Debug.Log($"[SaveData] Loaded player data into manager: Level {Level}, Money ${Money}, XP {XP}/{XPToNextLevel}");
        }
    }
}
