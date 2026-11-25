using NUnit.Framework;
using TrafficSystem;
using UnityEngine;

namespace DeliveryDriver.Tests
{
    public class NpcTrafficLodPolicyTests
    {
        [TestCase(129f, false, false)]
        [TestCase(130f, false, true)]
        [TestCase(110f, true, true)]
        [TestCase(100f, true, false)]
        [TestCase(110f, false, false)]
        [TestCase(float.NaN, true, false)]
        [TestCase(float.PositiveInfinity, true, false)]
        public void Hysteresis_RequiresSeparateEntryAndExit(float distance, bool lightweight, bool expected)
        {
            Assert.That(NpcTrafficLodPolicy.UseWaypointMode(distance, lightweight, 130f, 100f), Is.EqualTo(expected));
        }

        [TestCase(20f, false, false, true, 1, 2)]
        [TestCase(90f, false, false, true, 2, 3)]
        [TestCase(200f, false, false, true, 4, 5)]
        [TestCase(400f, false, false, true, 5, 8)]
        [TestCase(400f, false, true, true, 1, 2)]
        [TestCase(400f, true, false, true, 1, 1)]
        [TestCase(400f, false, false, false, 1, 1)]
        public void Context_DeterminesPathAndSafetyCadence(float distance, bool urgent,
            bool maneuver, bool adaptive, int path, int safety)
        {
            Assert.That(NpcTrafficLodPolicy.PathInterval(distance, urgent, maneuver, adaptive), Is.EqualTo(path));
            Assert.That(NpcTrafficLodPolicy.SafetyInterval(distance, urgent, maneuver, adaptive), Is.EqualTo(safety));
        }

        [Test]
        public void TickDeadline_HandlesWrapAndShorterUrgentInterval()
        {
            Assert.That(NpcTrafficLodPolicy.IsDue(2u, uint.MaxValue, 3), Is.True);
            Assert.That(NpcTrafficLodPolicy.IsDue(11u, 10u, 5), Is.False);
            Assert.That(NpcTrafficLodPolicy.IsDue(11u, 10u, 1), Is.True);
        }

        [Test]
        public void WaypointStep_DoesNotOvershootOrTeleport()
        {
            Vector3 destination = new Vector3(0f, 0f, 2f);
            Assert.That(NpcTrafficLodPolicy.StepTowards(Vector3.zero, destination, 10f, 0.02f).z, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(NpcTrafficLodPolicy.StepTowards(Vector3.zero, destination, 1000f, 0.02f), Is.EqualTo(destination));
            Assert.That(NpcTrafficLodPolicy.StepTowards(Vector3.zero, destination, -10f, 0.02f), Is.EqualTo(Vector3.zero));
        }
    }
}
