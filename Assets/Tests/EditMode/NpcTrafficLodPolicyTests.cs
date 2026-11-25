using System.Reflection;
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

        [TestCase(true)]
        [TestCase(false)]
        public void WaypointExit_RestoresAdditionalWheelsAndRigidbodySettings(bool resumeMotion)
        {
            var vehicle = new GameObject("LOD test vehicle");
            try
            {
                vehicle.transform.position = new Vector3(4000f, 40f, 0f);
                var agent = vehicle.AddComponent<NpcCarAgent>();
                var body = vehicle.GetComponent<Rigidbody>();
                body.interpolation = RigidbodyInterpolation.Extrapolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                var wheels = new WheelCollider[6];
                for (int i = 0; i < wheels.Length; i++)
                {
                    var wheelObject = new GameObject("Wheel " + i);
                    wheelObject.transform.SetParent(vehicle.transform, false);
                    wheels[i] = wheelObject.AddComponent<WheelCollider>();
                }
                wheels[2].enabled = false;

                var road = new RoadSegment(1, "Straight");
                road.waypoints.Add(new Waypoint(body.position - Vector3.up, Vector3.forward, road.id));
                road.waypoints.Add(new Waypoint(body.position - Vector3.up + Vector3.forward * 50f, Vector3.forward, road.id));
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var type = typeof(NpcCarAgent);
                type.GetField("rb", flags).SetValue(agent, body);
                type.GetField("bodyWheels", flags).SetValue(agent, wheels);
                type.GetField("currentSegment", flags).SetValue(agent, road);
                type.GetField("currentWaypointIndex", flags).SetValue(agent, 1);
                Physics.SyncTransforms();
                body.linearVelocity = Vector3.forward * 8f;
                Assert.That(body.linearVelocity.z, Is.EqualTo(8f), "Initial dynamic speed");

                Assert.That(type.GetMethod("TryEnterWaypointLod", flags).Invoke(agent, null), Is.True);
                Assert.That(body.isKinematic, Is.True);
                Assert.That(agent.SimulationVelocity.z, Is.EqualTo(8f), "Speed captured on LOD entry");
                Assert.That(wheels[5].enabled, Is.False);

                if (resumeMotion)
                    type.GetMethod("ExitWaypointLod", flags).Invoke(agent, new object[] { true });
                else
                    type.GetMethod("OnDisable", flags).Invoke(agent, null);

                Assert.That(agent.IsWaypointLod, Is.False);
                Assert.That(body.isKinematic, Is.False);
                Assert.That(body.interpolation, Is.EqualTo(RigidbodyInterpolation.Extrapolate));
                Assert.That(body.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode.ContinuousDynamic));
                Assert.That(body.linearVelocity.z, Is.EqualTo(resumeMotion ? 8f : 0f));
                for (int i = 0; i < wheels.Length; i++)
                    Assert.That(wheels[i].enabled, Is.EqualTo(i != 2), "Wheel " + i);
            }
            finally
            {
                Object.DestroyImmediate(vehicle);
            }
        }
    }
}
