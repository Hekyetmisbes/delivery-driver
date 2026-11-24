using System.Collections.Generic;
using NUnit.Framework;
using TrafficSystem;
using UnityEngine;

namespace DeliveryDriver.Tests
{
    public class NpcPathTurnScannerTests
    {
        [TestCase(0, true)]
        [TestCase(3, true)]
        [TestCase(13, true)]
        [TestCase(0, false)]
        public void SharedScan_MatchesIndependentOriginalWindows(int start, bool local)
        {
            var points = new List<Waypoint>();
            for (int i = 0; i < 18; i++)
                points.Add(new Waypoint(new Vector3(i, 0, 0),
                    Quaternion.Euler(0, (i * i * 13) % 360, 0) * Vector3.forward, 0));

            float expectedMax = 0f, expectedLocal = 0f;
            int expectedLast = -1;
            for (int i = start; i < Mathf.Min(start + 15, points.Count - 1) - 1; i++)
                expectedMax = Mathf.Max(expectedMax, Vector3.Angle(points[i].forward, points[i + 1].forward));

            if (local)
                for (int i = start; i < Mathf.Min(start + 10, points.Count - 1) - 1; i++)
                {
                    float angle = Vector3.Angle(points[i].forward, points[i + 1].forward);
                    if (angle > 15f)
                    {
                        expectedLocal = Mathf.Max(expectedLocal, angle);
                        expectedLast = i;
                    }
                }

            var actual = NpcPathTurnScanner.Scan(points, start, local);
            Assert.That(actual.MaxLookaheadAngle, Is.EqualTo(expectedMax));
            Assert.That(actual.MaxLocalTurnAngle, Is.EqualTo(expectedLocal));
            Assert.That(actual.LastTurnIndex, Is.EqualTo(expectedLast));
        }

        [Test]
        public void EmptyPath_HasNoTurn()
        {
            var result = NpcPathTurnScanner.Scan(new List<Waypoint>(), 0, true);
            Assert.That(result.LastTurnIndex, Is.EqualTo(-1));
            Assert.That(result.MaxLookaheadAngle, Is.Zero);
        }
    }
}
