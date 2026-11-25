using DeliveryDriver.Quest;
using NUnit.Framework;

namespace DeliveryDriver.Tests
{
    public class QuestSnapshotPolicyTests
    {
        [Test]
        public void Countdown_IsThrottled_ButProgressIsImmediate()
        {
            var policy = new QuestSnapshotPolicy();
            Assert.That(policy.ShouldWrite("q", false, 0, 0, 0, 15), Is.True);
            policy.Record("q", false, 0, 0, 0);
            Assert.That(policy.ShouldWrite("q", false, 0, 0, 1, 15), Is.False);
            Assert.That(policy.ShouldWrite("q", false, 0, 2, 1, 15), Is.True);
            Assert.That(policy.ShouldWrite("q", true, 0, 0, 1, 15), Is.True);
            Assert.That(policy.ShouldWrite("q", false, 1, 0, 1, 15), Is.True);
            Assert.That(policy.ShouldWrite("q", false, 0, 0, 15, 15), Is.True);
            Assert.That(policy.ShouldWrite("new", false, 0, 0, 1, 15), Is.True);
        }

        [Test]
        public void Reset_RequiresNewSnapshot()
        {
            var policy = new QuestSnapshotPolicy();
            policy.Record("q", true, 1, 3, 10);
            policy.Reset();
            Assert.That(policy.ShouldWrite("q", true, 1, 3, 11, 15), Is.True);
        }
    }
}
