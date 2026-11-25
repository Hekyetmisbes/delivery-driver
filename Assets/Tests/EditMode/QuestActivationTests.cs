using System.Reflection;
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

        [Test]
        public void ExternalActivation_RejectsAnotherInstanceWithTheSameActiveId()
        {
            var first = new QuestData();
            var duplicate = new QuestData { QuestID = first.QuestID };

            manager.StartQuest(first);
            manager.StartQuest(duplicate);

            Assert.That(manager.CurrentQuest, Is.SameAs(first));
            Assert.That(manager.ActiveQuests.Count, Is.EqualTo(1));
            Assert.That(duplicate.Status, Is.EqualTo(QuestStatus.NotStarted));
        }

        [Test]
        public void RejectedPickupAndDelivery_KeepTheSelectedQuest()
        {
            var invalid = new QuestData { PickupAuthority = QuestPickupAuthority.PhysicalBox };
            var selected = new QuestData();
            manager.StartQuest(invalid);
            manager.StartQuest(selected);

            Assert.That(manager.CommitExternalPickup(invalid), Is.False);
            Assert.That(manager.CurrentQuest, Is.SameAs(selected));

            invalid.HasPickedUpCargo = true;
            invalid.DeliveryLocations.Add(new QuestLocation(Vector3.zero, "First stop", 1f));
            invalid.CurrentDeliveryIndex = 1;
            Assert.That(manager.CommitExternalDeliveryStop(invalid), Is.False);
            Assert.That(manager.CurrentQuest, Is.SameAs(selected));
        }

        [Test]
        public void PhysicalBoxStop_RequiresExternalCommitAndRejectsNestedAdvance()
        {
            var quest = new QuestData { PickupAuthority = QuestPickupAuthority.PhysicalBox };
            quest.DeliveryLocations.Add(new QuestLocation(Vector3.zero, "First stop", 1f));
            quest.DeliveryLocations.Add(new QuestLocation(Vector3.forward, "Second stop", 1f));
            manager.StartQuest(quest);
            Assert.That(manager.CommitExternalPickup(quest), Is.True);

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(QuestManager).GetField("playerTransform", flags).SetValue(manager, owner.transform);
            typeof(QuestManager).GetMethod("CheckDeliveryProximity", flags).Invoke(manager, null);
            Assert.That(quest.CurrentDeliveryIndex, Is.Zero);

            bool? nestedCommit = null;
            manager.OnQuestUpdated.AddListener(updated => nestedCommit = manager.CommitExternalDeliveryStop(updated));
            Assert.That(manager.CommitExternalDeliveryStop(quest), Is.True);
            Assert.That(nestedCommit, Is.False);
            Assert.That(quest.CurrentDeliveryIndex, Is.EqualTo(1));
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Active));
        }

        [Test]
        public void Abandon_ResumesThePreviousQuest_AndReturnsRepeatableQuest()
        {
            var first = new QuestData();
            var repeatable = new QuestData { IsRepeatable = true };
            manager.StartQuest(first);
            manager.StartQuest(repeatable);

            manager.AbandonQuest(repeatable.QuestID);

            Assert.That(manager.CurrentQuest, Is.SameAs(first));
            Assert.That(manager.ActiveQuests.Count, Is.EqualTo(1));
            Assert.That(manager.AvailableQuests, Does.Contain(repeatable));
            Assert.That(repeatable.Status, Is.EqualTo(QuestStatus.NotStarted));
        }

        [Test]
        public void Abandon_NonRepeatableQuest_BecomesTerminal()
        {
            var quest = new QuestData { IsRepeatable = false };
            manager.StartQuest(quest);
            manager.AbandonQuest(quest.QuestID);
            manager.StartQuest(quest);

            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Expired));
            Assert.That(manager.CurrentQuest, Is.Null);
            Assert.That(manager.ActiveQuests, Is.Empty);
        }

        [Test]
        public void Restore_FiresPresentationEventsWithinARestoreScope()
        {
            var quest = new QuestData { Status = QuestStatus.Active, TimeRemaining = 128f };
            var saved = new QuestSaveData { CurrentQuestID = quest.QuestID };
            saved.ActiveQuests.Add(QuestSaveData.QuestRecord.FromQuestData(quest));
            var isRestoring = typeof(QuestManager).GetMethod("IsRestoringQuest", BindingFlags.Instance | BindingFlags.NonPublic);
            int started = 0;
            int updated = 0;
            manager.OnQuestStarted.AddListener(restored =>
            {
                Assert.That(isRestoring.Invoke(manager, new object[] { restored }), Is.True);
                Assert.That(restored.TimeRemaining, Is.EqualTo(128f));
                started++;
            });
            manager.OnQuestUpdated.AddListener(_ => updated++);

            manager.LoadSaveData(saved);

            Assert.That(started, Is.EqualTo(1));
            Assert.That(updated, Is.EqualTo(1));
            Assert.That(isRestoring.Invoke(manager, new object[] { manager.CurrentQuest }), Is.False);
        }
    }
}
