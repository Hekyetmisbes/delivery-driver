using System.Collections.Generic;
using DeliveryDriver.Quest;
using NUnit.Framework;
using TrafficSystem;
using UnityEngine;

namespace DeliveryDriver.Tests
{
    public class CoreModuleTests
    {
        [TestCase(6, false, 0.85f)]
        [TestCase(7, false, 1.05f)]
        [TestCase(9, true, 1.15f)]
        [TestCase(10, true, 1f)]
        [TestCase(19, true, 1.15f)]
        [TestCase(22, false, 0.85f)]
        public void TrafficTimePolicy_PreservesExistingMultipliers(int hour, bool aggressive, float expected)
        {
            Assert.That(NpcTimeOfDayPolicy.GetSpeedMultiplier(hour, aggressive), Is.EqualTo(expected));
        }

        [Test]
        public void QuestUiTracker_OnlyPublishesChangedPresentation()
        {
            var quest = new QuestData { QuestID = "q", TimeRemaining = 10f };
            var tracker = new QuestUiUpdateTracker();
            Assert.That(tracker.TryCaptureChanged(quest), Is.True);
            quest.TimeRemaining = 9.9f;
            Assert.That(tracker.TryCaptureChanged(quest), Is.False);
            quest.TimeRemaining = 9f;
            Assert.That(tracker.TryCaptureChanged(quest), Is.True);
            quest.HasPickedUpCargo = true;
            Assert.That(tracker.TryCaptureChanged(quest), Is.True);
            tracker.MarkDirty();
            Assert.That(tracker.TryCaptureChanged(quest), Is.True);
        }

        [Test]
        public void RouteOrdering_PreservesEqualDistanceInsertionOrderAndInput()
        {
            var origin = new QuestLocation(Vector3.zero, "origin", 1f);
            var first = new QuestLocation(Vector3.right, "first", 1f);
            var second = new QuestLocation(Vector3.left, "second", 1f);
            var stops = new List<QuestLocation> { first, second };
            var ordered = QuestRouteOrdering.Optimize(origin, stops);
            Assert.That(ordered, Is.EqualTo(new[] { first, second }));
            Assert.That(stops, Is.EqualTo(new[] { first, second }));
        }

        [Test]
        public void ParkingValidator_RequiresEntireVehicleFootprint()
        {
            var vehicle = new GameObject("test-vehicle");
            try
            {
                var box = vehicle.AddComponent<BoxCollider>();
                box.size = new Vector3(2f, 1f, 4f);
                Physics.SyncTransforms();
                var validator = new DeliveryParkingValidator();
                Assert.That(validator.ContainsVehicle(vehicle.transform, Vector3.zero,
                    Quaternion.identity, new Vector2(3f, 6f), 0f), Is.True);

                vehicle.transform.position = Vector3.right;
                Physics.SyncTransforms();
                Assert.That(validator.ContainsVehicle(vehicle.transform, Vector3.zero,
                    Quaternion.identity, new Vector2(3f, 6f), 0f), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(vehicle);
            }
        }
    }
}
