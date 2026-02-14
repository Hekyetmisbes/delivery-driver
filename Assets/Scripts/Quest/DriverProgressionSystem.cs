using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DeliveryDriver.Quest
{
    public enum DriverSkillType
    {
        FuelEfficiency,
        CargoDurability,
        RouteAssist
    }

    public enum LevelRewardType
    {
        QuestTypeUnlock,
        RegionUnlock,
        VehiclePartUnlock
    }

    [Serializable]
    public class SkillNodeState
    {
        public DriverSkillType SkillType;
        public string DisplayName;
        [TextArea(2, 3)] public string Description;
        public int Rank;
        public int MaxRank = 3;
    }

    [Serializable]
    public class LevelRewardDefinition
    {
        public string RewardId;
        public int RequiredLevel;
        public string Title;
        [TextArea(2, 3)] public string Description;
        public LevelRewardType RewardType;
        public QuestType QuestType;
        public string RegionName;
        public string VehiclePartName;
    }

    [Serializable]
    public class SkillRankSaveData
    {
        public DriverSkillType SkillType;
        public int Rank;
    }

    [Serializable]
    public class DriverProgressionSaveData
    {
        public int UnspentSkillPoints;
        public List<SkillRankSaveData> SkillRanks = new List<SkillRankSaveData>();
        public List<string> UnlockedRewardIds = new List<string>();
    }

    /// <summary>
    /// Step 2.1 progression extensions: performance XP, level rewards and lightweight skill tree.
    /// </summary>
    public class DriverProgressionSystem : MonoBehaviour
    {
        public static DriverProgressionSystem Instance { get; private set; }

        [Header("Skill Tree")]
        [SerializeField] private int unspentSkillPoints = 0;
        [SerializeField] private List<SkillNodeState> skills = new List<SkillNodeState>();

        [Header("Level Rewards")]
        [SerializeField] private List<LevelRewardDefinition> levelRewards = new List<LevelRewardDefinition>();
        [SerializeField] private List<string> unlockedRewardIds = new List<string>();

        public event Action<int> OnSkillPointsChanged;
        public event Action<DriverSkillType, int> OnSkillRankChanged;
        public event Action<LevelRewardDefinition> OnLevelRewardUnlocked;

        public int UnspentSkillPoints => unspentSkillPoints;
        public IReadOnlyList<SkillNodeState> Skills => skills;
        public IReadOnlyList<LevelRewardDefinition> LevelRewards => levelRewards;
        public IReadOnlyList<string> UnlockedRewardIds => unlockedRewardIds;

        private bool subscribedToLevelUp;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            InitializeDefaults();
        }

        private void Start()
        {
            SubscribeToProgressionEvents();
            LoadFromSaveIfAvailable();
            EvaluateLevelRewards(PlayerProgressionManager.Instance != null ? PlayerProgressionManager.Instance.CurrentLevel : 1);
            NotifyAllState();
        }

        private void OnDestroy()
        {
            if (subscribedToLevelUp && PlayerProgressionManager.Instance != null)
            {
                PlayerProgressionManager.Instance.OnLevelUp.RemoveListener(HandleLevelUp);
                subscribedToLevelUp = false;
            }
        }

        public int CalculatePerformanceXP(QuestData quest, int finalReward, float completionTimeSeconds)
        {
            if (quest == null)
            {
                return 0;
            }

            int baseXP = Mathf.Max(20, quest.XPReward / 2);
            int rankBonus = quest.Rating switch
            {
                PerformanceRating.S => 50,
                PerformanceRating.A => 35,
                PerformanceRating.B => 20,
                PerformanceRating.C => 10,
                PerformanceRating.D => 0,
                _ => -20
            };

            int safetyPenalty = (quest.CollisionCount * 6) + (quest.HardBrakeCount * 2);
            int payoutBonus = Mathf.Clamp(finalReward / 120, 0, 35);
            int timeBonus = 0;
            if (quest.TimeLimit > 0f)
            {
                float completionRatio = 1f - Mathf.Clamp01(completionTimeSeconds / quest.TimeLimit);
                timeBonus = Mathf.RoundToInt(completionRatio * 25f);
            }

            int fragileBonus = 0;
            if (quest.Cargo != null && quest.Cargo.IsFragile)
            {
                fragileBonus = Mathf.RoundToInt(Mathf.Clamp(quest.Cargo.CargoHealth, 0f, 100f) * 0.15f);
            }

            return Mathf.Max(5, baseXP + rankBonus + payoutBonus + timeBonus + fragileBonus - safetyPenalty);
        }

        public void OnQuestCompleted(QuestData quest, int finalReward, float completionTimeSeconds)
        {
            PlayerProgressionManager progression = PlayerProgressionManager.Instance;
            if (progression == null || quest == null)
            {
                return;
            }

            int performanceXP = CalculatePerformanceXP(quest, finalReward, completionTimeSeconds);
            progression.AwardXP(performanceXP);
        }

        public bool TryUnlockSkill(DriverSkillType skillType)
        {
            SkillNodeState node = skills.FirstOrDefault(entry => entry.SkillType == skillType);
            if (node == null || node.Rank >= node.MaxRank || unspentSkillPoints <= 0)
            {
                return false;
            }

            node.Rank++;
            unspentSkillPoints = Mathf.Max(0, unspentSkillPoints - 1);

            OnSkillRankChanged?.Invoke(node.SkillType, node.Rank);
            OnSkillPointsChanged?.Invoke(unspentSkillPoints);
            return true;
        }

        public int GetSkillRank(DriverSkillType skillType)
        {
            SkillNodeState node = skills.FirstOrDefault(entry => entry.SkillType == skillType);
            return node != null ? Mathf.Max(0, node.Rank) : 0;
        }

        public float GetFuelEfficiencyPercent()
        {
            return Mathf.Clamp01(GetSkillRank(DriverSkillType.FuelEfficiency) * 0.05f);
        }

        public float GetCargoDamageMultiplier()
        {
            float reduction = GetSkillRank(DriverSkillType.CargoDurability) * 0.15f;
            return Mathf.Clamp(1f - reduction, 0.5f, 1f);
        }

        public float GetRoutePenaltyMultiplier()
        {
            float reduction = GetSkillRank(DriverSkillType.RouteAssist) * 0.1f;
            return Mathf.Clamp(1f - reduction, 0.65f, 1f);
        }

        public int CalculateFuelEfficiencyRebate(QuestData quest)
        {
            if (quest == null)
            {
                return 0;
            }

            float distanceKm = Mathf.Max(0f, quest.TotalDistanceTraveled / 1000f);
            float estimatedFuelCost = distanceKm * 24f;
            float rebate = estimatedFuelCost * GetFuelEfficiencyPercent();
            return Mathf.RoundToInt(rebate);
        }

        public bool IsQuestTypeUnlocked(QuestType questType)
        {
            if (questType == QuestType.StandardDelivery || questType == QuestType.TimeTrial)
            {
                return true;
            }

            List<LevelRewardDefinition> questRewards = levelRewards
                .Where(entry => entry.RewardType == LevelRewardType.QuestTypeUnlock && entry.QuestType == questType)
                .ToList();

            if (questRewards.Count == 0)
            {
                return true;
            }

            return questRewards.Any(entry => unlockedRewardIds.Contains(entry.RewardId));
        }

        public bool IsRewardUnlocked(string rewardId)
        {
            return !string.IsNullOrWhiteSpace(rewardId) && unlockedRewardIds.Contains(rewardId);
        }

        public DriverProgressionSaveData GetSaveData()
        {
            DriverProgressionSaveData data = new DriverProgressionSaveData
            {
                UnspentSkillPoints = unspentSkillPoints,
                SkillRanks = skills.Select(entry => new SkillRankSaveData
                {
                    SkillType = entry.SkillType,
                    Rank = entry.Rank
                }).ToList(),
                UnlockedRewardIds = new List<string>(unlockedRewardIds)
            };

            return data;
        }

        public void LoadSaveData(DriverProgressionSaveData data)
        {
            if (data == null)
            {
                return;
            }

            unspentSkillPoints = Mathf.Max(0, data.UnspentSkillPoints);

            Dictionary<DriverSkillType, int> rankLookup = new Dictionary<DriverSkillType, int>();
            if (data.SkillRanks != null)
            {
                foreach (SkillRankSaveData rankData in data.SkillRanks)
                {
                    if (!rankLookup.ContainsKey(rankData.SkillType))
                    {
                        rankLookup.Add(rankData.SkillType, Mathf.Max(0, rankData.Rank));
                    }
                }
            }

            foreach (SkillNodeState node in skills)
            {
                if (node == null)
                {
                    continue;
                }

                int savedRank = rankLookup.TryGetValue(node.SkillType, out int value) ? value : 0;
                node.Rank = Mathf.Clamp(savedRank, 0, node.MaxRank);
            }

            unlockedRewardIds = data.UnlockedRewardIds != null
                ? data.UnlockedRewardIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList()
                : new List<string>();
        }

        private void SubscribeToProgressionEvents()
        {
            if (subscribedToLevelUp || PlayerProgressionManager.Instance == null)
            {
                return;
            }

            PlayerProgressionManager.Instance.OnLevelUp.AddListener(HandleLevelUp);
            subscribedToLevelUp = true;
        }

        private void HandleLevelUp(int newLevel)
        {
            unspentSkillPoints++;
            OnSkillPointsChanged?.Invoke(unspentSkillPoints);
            EvaluateLevelRewards(newLevel);
        }

        private void EvaluateLevelRewards(int currentLevel)
        {
            foreach (LevelRewardDefinition reward in levelRewards)
            {
                if (reward == null || string.IsNullOrWhiteSpace(reward.RewardId))
                {
                    continue;
                }

                if (currentLevel < reward.RequiredLevel || unlockedRewardIds.Contains(reward.RewardId))
                {
                    continue;
                }

                unlockedRewardIds.Add(reward.RewardId);
                OnLevelRewardUnlocked?.Invoke(reward);
            }
        }

        private void LoadFromSaveIfAvailable()
        {
            if (SaveManager.Instance == null)
            {
                return;
            }

            GameSaveData saveData = SaveManager.Instance.LoadGame();
            if (saveData?.PlayerData?.DriverProgressionData == null)
            {
                return;
            }

            LoadSaveData(saveData.PlayerData.DriverProgressionData);
        }

        private void NotifyAllState()
        {
            OnSkillPointsChanged?.Invoke(unspentSkillPoints);
            foreach (SkillNodeState node in skills)
            {
                if (node != null)
                {
                    OnSkillRankChanged?.Invoke(node.SkillType, node.Rank);
                }
            }
        }

        private void InitializeDefaults()
        {
            if (skills == null || skills.Count == 0)
            {
                skills = new List<SkillNodeState>
                {
                    new SkillNodeState
                    {
                        SkillType = DriverSkillType.FuelEfficiency,
                        DisplayName = "Yakit Tasarrufu",
                        Description = "Her rank teslimat sonunda yakit iadesini artirir.",
                        Rank = 0,
                        MaxRank = 3
                    },
                    new SkillNodeState
                    {
                        SkillType = DriverSkillType.CargoDurability,
                        DisplayName = "Kargo Dayanikliligi",
                        Description = "Kirilgan kargolarin carpisma hasarini azaltir.",
                        Rank = 0,
                        MaxRank = 3
                    },
                    new SkillNodeState
                    {
                        SkillType = DriverSkillType.RouteAssist,
                        DisplayName = "Rota Okuma Yardimi",
                        Description = "Gecikme ve sert fren cezalarini azaltir.",
                        Rank = 0,
                        MaxRank = 3
                    }
                };
            }

            if (levelRewards == null || levelRewards.Count == 0)
            {
                levelRewards = new List<LevelRewardDefinition>
                {
                    new LevelRewardDefinition
                    {
                        RewardId = "unlock_express_delivery",
                        RequiredLevel = 3,
                        Title = "Express Teslimat",
                        Description = "Yeni gorev turu acildi: Express Delivery.",
                        RewardType = LevelRewardType.QuestTypeUnlock,
                        QuestType = QuestType.ExpressDelivery
                    },
                    new LevelRewardDefinition
                    {
                        RewardId = "unlock_fragile_delivery",
                        RequiredLevel = 6,
                        Title = "Kirigan Kargo Teslimati",
                        Description = "Yeni gorev turu acildi: Fragile Delivery.",
                        RewardType = LevelRewardType.QuestTypeUnlock,
                        QuestType = QuestType.FragileDelivery
                    },
                    new LevelRewardDefinition
                    {
                        RewardId = "unlock_multistop_delivery",
                        RequiredLevel = 9,
                        Title = "Cok Durakli Teslimat",
                        Description = "Yeni gorev turu acildi: Multi Stop Delivery.",
                        RewardType = LevelRewardType.QuestTypeUnlock,
                        QuestType = QuestType.MultiStopDelivery
                    },
                    new LevelRewardDefinition
                    {
                        RewardId = "unlock_region_downtown",
                        RequiredLevel = 5,
                        Title = "Yeni Bolge",
                        Description = "Downtown bolgesi kullanima acildi.",
                        RewardType = LevelRewardType.RegionUnlock,
                        RegionName = "Downtown"
                    },
                    new LevelRewardDefinition
                    {
                        RewardId = "unlock_region_harbor",
                        RequiredLevel = 8,
                        Title = "Yeni Bolge",
                        Description = "Harbor bolgesi kullanima acildi.",
                        RewardType = LevelRewardType.RegionUnlock,
                        RegionName = "Harbor"
                    },
                    new LevelRewardDefinition
                    {
                        RewardId = "unlock_part_eco_tires",
                        RequiredLevel = 4,
                        Title = "Arac Parcasi",
                        Description = "Eco Tire seti garajda acildi.",
                        RewardType = LevelRewardType.VehiclePartUnlock,
                        VehiclePartName = "Eco Tires"
                    },
                    new LevelRewardDefinition
                    {
                        RewardId = "unlock_part_reinforced_suspension",
                        RequiredLevel = 7,
                        Title = "Arac Parcasi",
                        Description = "Reinforced Suspension garajda acildi.",
                        RewardType = LevelRewardType.VehiclePartUnlock,
                        VehiclePartName = "Reinforced Suspension"
                    }
                };
            }
        }
    }
}
