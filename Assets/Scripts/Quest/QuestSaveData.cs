using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDriver.Quest
{
    [Serializable]
    public class QuestSaveData
    {
        public List<QuestRecord> ActiveQuests = new List<QuestRecord>();
        public List<QuestRecord> AvailableQuests = new List<QuestRecord>();
        public List<QuestRecord> CompletedQuests = new List<QuestRecord>();
        public string CurrentQuestID;

        [Serializable]
        public class QuestRecord
        {
            public string QuestID;
            public string QuestName;
            public string QuestDescription;
            public QuestType QuestType;
            public QuestDifficulty Difficulty;
            public QuestStatus Status;
            public float TimeLimit;
            public float TimeRemaining;
            public int BaseReward;
            public int BonusReward;
            public float BonusTimeThreshold;
            public int RequiredLevel;
            public bool IsRepeatable;
            public int XPReward;
            public bool HasPickedUpCargo;
            public int CurrentDeliveryIndex;
            public CargoRecord Cargo;
            public QuestLocationRecord PickupLocation;
            public List<QuestLocationRecord> DeliveryLocations = new List<QuestLocationRecord>();

            public static QuestRecord FromQuestData(QuestData quest)
            {
                QuestRecord record = new QuestRecord
                {
                    QuestID = quest.QuestID,
                    QuestName = quest.QuestName,
                    QuestDescription = quest.QuestDescription,
                    QuestType = quest.QuestType,
                    Difficulty = quest.Difficulty,
                    Status = quest.Status,
                    TimeLimit = quest.TimeLimit,
                    TimeRemaining = quest.TimeRemaining,
                    BaseReward = quest.BaseReward,
                    BonusReward = quest.BonusReward,
                    BonusTimeThreshold = quest.BonusTimeThreshold,
                    RequiredLevel = quest.RequiredLevel,
                    IsRepeatable = quest.IsRepeatable,
                    XPReward = quest.XPReward,
                    HasPickedUpCargo = quest.HasPickedUpCargo,
                    CurrentDeliveryIndex = quest.CurrentDeliveryIndex,
                    Cargo = CargoRecord.FromCargoData(quest.Cargo),
                    PickupLocation = QuestLocationRecord.FromQuestLocation(quest.PickupLocation)
                };

                if (quest.DeliveryLocations != null)
                {
                    foreach (QuestLocation location in quest.DeliveryLocations)
                    {
                        record.DeliveryLocations.Add(QuestLocationRecord.FromQuestLocation(location));
                    }
                }

                return record;
            }

            public QuestData ToQuestData()
            {
                QuestData quest = new QuestData
                {
                    QuestID = QuestID,
                    QuestName = QuestName,
                    QuestDescription = QuestDescription,
                    QuestType = QuestType,
                    Difficulty = Difficulty,
                    Status = Status,
                    TimeLimit = TimeLimit,
                    TimeRemaining = TimeRemaining,
                    BaseReward = BaseReward,
                    BonusReward = BonusReward,
                    BonusTimeThreshold = BonusTimeThreshold,
                    RequiredLevel = RequiredLevel,
                    IsRepeatable = IsRepeatable,
                    XPReward = XPReward,
                    HasPickedUpCargo = HasPickedUpCargo,
                    CurrentDeliveryIndex = CurrentDeliveryIndex
                };

                quest.Cargo = CargoRecord.ToCargoData(Cargo);
                quest.PickupLocation = QuestLocationRecord.ToQuestLocation(PickupLocation);
                quest.DeliveryLocations = new List<QuestLocation>();

                if (DeliveryLocations != null)
                {
                    foreach (QuestLocationRecord locationRecord in DeliveryLocations)
                    {
                        quest.DeliveryLocations.Add(QuestLocationRecord.ToQuestLocation(locationRecord));
                    }
                }

                return quest;
            }
        }

        [Serializable]
        public class CargoRecord
        {
            public string CargoName;
            public float Weight;
            public bool IsFragile;
            public float CargoHealth;
            public string Description;

            public static CargoRecord FromCargoData(CargoData cargo)
            {
                if (cargo == null)
                {
                    return null;
                }

                return new CargoRecord
                {
                    CargoName = cargo.CargoName,
                    Weight = cargo.Weight,
                    IsFragile = cargo.IsFragile,
                    CargoHealth = cargo.CargoHealth,
                    Description = cargo.Description
                };
            }

            public static CargoData ToCargoData(CargoRecord record)
            {
                if (record == null)
                {
                    return null;
                }

                CargoData cargo = new CargoData(record.CargoName, record.Weight, record.IsFragile, record.Description)
                {
                    CargoHealth = record.CargoHealth
                };

                return cargo;
            }
        }

        [Serializable]
        public class QuestLocationRecord
        {
            public Vector3 Position;
            public string LocationName;
            public int RoadSegmentIndex;
            public int WaypointIndex;
            public float TriggerRadius;

            public static QuestLocationRecord FromQuestLocation(QuestLocation location)
            {
                if (location == null)
                {
                    return null;
                }

                return new QuestLocationRecord
                {
                    Position = location.Position,
                    LocationName = location.LocationName,
                    RoadSegmentIndex = location.RoadSegmentIndex,
                    WaypointIndex = location.WaypointIndex,
                    TriggerRadius = location.TriggerRadius
                };
            }

            public static QuestLocation ToQuestLocation(QuestLocationRecord record)
            {
                if (record == null)
                {
                    return null;
                }

                QuestLocation location = new QuestLocation(record.Position, record.LocationName, record.TriggerRadius)
                {
                    RoadSegmentIndex = record.RoadSegmentIndex,
                    WaypointIndex = record.WaypointIndex
                };

                return location;
            }
        }
    }
}
