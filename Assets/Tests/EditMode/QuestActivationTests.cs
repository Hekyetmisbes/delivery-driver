using DeliveryDriver.Quest;
using NUnit.Framework;
using UnityEngine;

namespace DeliveryDriver.Tests
{
    public class QuestActivationTests
    {
        private GameObject owner;
        private QuestManager manager;

        [SetUp]
        public void SetUp()
        {
            // No scene bootstrap or singleton registration is needed for these transition checks.
            owner = new GameObject("QuestActivationTests");
            owner.SetActive(false);
            manager = owner.AddComponent<QuestManager>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(owner);

        [Test]
        public void PhysicalBoxStart_PreservesPreparedState_AndNotifiesOnce()
        {
            var quest = new QuestData
            {
                PickupAuthority = QuestPickupAuthority.PhysicalBox,
                TimeLimit = 300f,
                TimeRemaining = 240f
            };
            int starts = 0, updates = 0;
            manager.OnQuestStarted.AddListener(_ => starts++);
            manager.OnQuestUpdated.AddListener(_ => updates++);
            manager.AddAvailableQuest(quest);
            manager.StartQuest(quest);

            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Active));
            Assert.That(manager.CurrentQuest, Is.SameAs(quest));
            Assert.That(manager.ActiveQuests.Count, Is.EqualTo(1));
            Assert.That(manager.AvailableQuests.Count, Is.Zero);
            Assert.That(quest.DeliveryLocations.Count, Is.Zero);
            Assert.That(quest.TimeRemaining, Is.EqualTo(240f));
            Assert.That(manager.IsSaveReady, Is.True);

            manager.StartQuest(quest);
            Assert.That(manager.ActiveQuests.Count, Is.EqualTo(1));
            Assert.That(starts, Is.EqualTo(1));
            Assert.That(updates, Is.EqualTo(1));
        }

        [TestCase(QuestStatus.Completed)]
        [TestCase(QuestStatus.Failed)]
        public void TerminalQuest_CannotBeStartedOrAccepted(QuestStatus status)
        {
            var quest = new QuestData { Status = status };
            int starts = 0;
            manager.OnQuestStarted.AddListener(_ => starts++);
            manager.AddAvailableQuest(quest);

            Assert.That(manager.AcceptQuest(quest.QuestID), Is.False);
            manager.StartQuest(quest);

            Assert.That(quest.Status, Is.EqualTo(status));
            Assert.That(manager.CurrentQuest, Is.Null);
            Assert.That(manager.ActiveQuests.Count, Is.Zero);
            Assert.That(manager.AvailableQuests.Count, Is.EqualTo(1));
            Assert.That(starts, Is.Zero);
        }
    }
}
