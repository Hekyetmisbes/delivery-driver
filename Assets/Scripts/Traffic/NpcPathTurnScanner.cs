using System.Collections.Generic;
using UnityEngine;

namespace TrafficSystem
{
    public readonly struct NpcPathTurnScan
    {
        public readonly float MaxLookaheadAngle;
        public readonly float MaxLocalTurnAngle;
        public readonly int LastTurnIndex;

        public NpcPathTurnScan(float maxLookaheadAngle, float maxLocalTurnAngle, int lastTurnIndex)
        {
            MaxLookaheadAngle = maxLookaheadAngle;
            MaxLocalTurnAngle = maxLocalTurnAngle;
            LastTurnIndex = lastTurnIndex;
        }
    }

    public static class NpcPathTurnScanner
    {
        // Keep the original exclusive end bounds (15/10 waypoint windows).
        public static NpcPathTurnScan Scan(IReadOnlyList<Waypoint> waypoints, int start, bool scanLocalTurn)
        {
            float maxAngle = 0f, maxLocalAngle = 0f;
            int lastTurn = -1;
            if (waypoints == null || start < 0 || start >= waypoints.Count)
                return new NpcPathTurnScan(0f, 0f, -1);

            int end = Mathf.Min(start + 15, waypoints.Count - 1) - 1;
            int localEnd = Mathf.Min(start + 10, waypoints.Count - 1) - 1;
            for (int i = start; i < end; i++)
            {
                float angle = Vector3.Angle(waypoints[i].forward, waypoints[i + 1].forward);
                if (angle > maxAngle)
                    maxAngle = angle;
                if (scanLocalTurn && i < localEnd && angle > 15f)
                {
                    maxLocalAngle = Mathf.Max(maxLocalAngle, angle);
                    lastTurn = i; // Preserve the old last significant turn, not the first.
                }
            }

            return new NpcPathTurnScan(maxAngle, maxLocalAngle, lastTurn);
        }
    }
}
