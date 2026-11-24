using System.Collections.Generic;
using DeliveryDriver.Quest;
using NUnit.Framework;
using UnityEngine;

namespace DeliveryDriver.Tests
{
    public class QuestGenerationRulesTests
    {
        [TestCase(QuestType.StandardDelivery, QuestDifficulty.Easy, 1, 1100f, 200f, 110, 55, 1)]
        [TestCase(QuestType.MultiStopDelivery, QuestDifficulty.Medium, 2, 1100f, 450f, 756, 378, 5)]
        [TestCase(QuestType.ExpressDelivery, QuestDifficulty.Medium, 1, 1100f, 90f, 420, 210, 5)]
        [TestCase(QuestType.FragileDelivery, QuestDifficulty.Easy, 1, 1100f, 250f, 110, 55, 1)]
        [TestCase(QuestType.TimeTrial, QuestDifficulty.Hard, 1, 1100f, 60f, 415, 415, 15)]
        [TestCase(QuestType.ExpressDelivery, QuestDifficulty.Medium, 1, 1006f, 82.30909f, 402, 201, 5)]
        [TestCase(QuestType.FragileDelivery, QuestDifficulty.Medium, 1, 0f, 90f, 100, 50, 5)]
        public void DefaultRules_PreserveOriginalFormulas(QuestType type, QuestDifficulty difficulty,
            int stopCount, float distance, float time, int reward, int bonus, int level)
        {
            var quest = new QuestData { QuestType = type, Difficulty = difficulty,
                DeliveryLocations = new List<QuestLocation>(new QuestLocation[stopCount]) };
            QuestGenerationRules.Apply(quest, distance, null);
            Assert.That(quest.TimeLimit, Is.EqualTo(time).Within(0.001f));
            Assert.That(quest.TimeRemaining, Is.EqualTo(quest.TimeLimit));
            Assert.That(quest.BaseReward, Is.EqualTo(reward));
            Assert.That(quest.BonusReward, Is.EqualTo(bonus));
            Assert.That(quest.RequiredLevel, Is.EqualTo(level));
            Assert.That(quest.XPReward, Is.EqualTo(50 * ((int)difficulty + 1) *
                (type == QuestType.MultiStopDelivery ? stopCount : 1)));
            Assert.That(quest.IsRepeatable, Is.True);
            Assert.That(quest.BonusTimeThreshold, Is.EqualTo(0.5f));
        }

        [Test]
        public void ConfiguredRules_UseSettingsInsteadOfFallbacks()
        {
            var settings = ScriptableObject.CreateInstance<QuestSystemSettings>();
            try
            {
                var quest = new QuestData { QuestType = QuestType.TimeTrial, Difficulty = QuestDifficulty.Expert };
                QuestGenerationRules.Apply(quest, 4321f, settings);
                Assert.That(quest.TimeLimit, Is.EqualTo(Mathf.Max(settings.MinimumTimeLimit,
                    4321f / 11f * settings.GetTimeMultiplier(quest.Difficulty) * settings.TimeTrialTimeScale)));
                Assert.That(quest.BaseReward, Is.EqualTo(Mathf.RoundToInt(
                    (4321f * settings.BaseRewardPerMeter + settings.GetDifficultyBonus(quest.Difficulty))
                    * settings.TimeTrialRewardMultiplier)));
                Assert.That(quest.BonusReward, Is.EqualTo(Mathf.RoundToInt(quest.BaseReward * settings.BonusRewardMultiplier)));
            }
            finally { Object.DestroyImmediate(settings); }
        }
    }
}
