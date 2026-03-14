using System.Collections.Generic;
using UnityEngine;

namespace TrafficSystem
{
    /// <summary>
    /// Represents a single waypoint along a road path
    /// </summary>
    [System.Serializable]
    public class Waypoint
    {
        public Vector3 position;
        public Vector3 forward;  // Tangent direction at this point
        public int roadSegmentIndex; // Which road segment this belongs to

        public Waypoint(Vector3 pos, Vector3 fwd, int segmentIndex)
        {
            position = pos;
            forward = fwd.normalized;
            roadSegmentIndex = segmentIndex;
        }
    }

    /// <summary>
    /// Represents a complete road segment with ordered waypoints
    /// </summary>
    [System.Serializable]
    public class RoadSegment
    {
        public int id;
        public string name;
        public List<Waypoint> waypoints = new List<Waypoint>();

        [System.NonSerialized] // Prevent serialization cycle
        public List<RoadConnection> connections = new List<RoadConnection>();

        public RoadSegment(int segmentId, string segmentName)
        {
            id = segmentId;
            name = segmentName;
        }

        /// <summary>
        /// Get waypoint at specific index, with wraparound support
        /// </summary>
        public Waypoint GetWaypoint(int index)
        {
            if (waypoints.Count == 0) return null;
            index = Mathf.Clamp(index, 0, waypoints.Count - 1);
            return waypoints[index];
        }

        /// <summary>
        /// Find nearest waypoint index to a given position
        /// </summary>
        public int FindNearestWaypointIndex(Vector3 position)
        {
            int nearest = 0;
            float minDist = float.MaxValue;

            for (int i = 0; i < waypoints.Count; i++)
            {
                float dist = Vector3.Distance(position, waypoints[i].position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = i;
                }
            }

            return nearest;
        }
    }

    /// <summary>
    /// Represents a connection between two road segments (intersection)
    /// </summary>
    [System.Serializable]
    public class RoadConnection
    {
        public RoadSegment fromSegment;
        public RoadSegment toSegment;
        public int fromWaypointIndex; // Where this connection starts
        public int toWaypointIndex;   // Where it connects to target

        public RoadConnection(RoadSegment from, RoadSegment to, int fromIndex, int toIndex)
        {
            fromSegment = from;
            toSegment = to;
            fromWaypointIndex = fromIndex;
            toWaypointIndex = toIndex;
        }
    }

    /// <summary>
    /// Pre-built spatial lookup for fast pathfinding queries.
    /// Built once per road graph and reused across all FindPath calls.
    /// </summary>
    public class RoadGraphSpatialIndex
    {
        public readonly struct WaypointRef
        {
            public readonly RoadSegment segment;
            public readonly int index;
            public readonly Vector3 position;
            public readonly Vector3 forward;

            public WaypointRef(RoadSegment s, int i, Vector3 p, Vector3 f)
            {
                segment = s;
                index = i;
                position = p;
                forward = f;
            }
        }

        public Dictionary<int, RoadSegment> segmentById;
        public Dictionary<long, WaypointRef> waypointByKey;
        public Dictionary<Vector2Int, List<long>> spatialGrid;
        public float cellSize;

        public static long PackKey(int segmentId, int waypointIndex)
        {
            return ((long)segmentId << 20) | (uint)waypointIndex;
        }

        public static void UnpackKey(long key, out int segmentId, out int waypointIndex)
        {
            segmentId = (int)(key >> 20);
            waypointIndex = (int)(key & 0xFFFFF);
        }

        public static Vector2Int ToGridCell(Vector3 position, float cs)
        {
            return new Vector2Int(
                Mathf.FloorToInt(position.x / cs),
                Mathf.FloorToInt(position.z / cs));
        }
    }

    /// <summary>
    /// Complete road network graph
    /// </summary>
    [System.Serializable]
    public class RoadGraph
    {
        public List<RoadSegment> roadSegments = new List<RoadSegment>();

        [System.NonSerialized]
        private RoadGraphSpatialIndex cachedIndex;

        public RoadGraphSpatialIndex GetOrBuildSpatialIndex(float cellSize)
        {
            if (cachedIndex != null && Mathf.Approximately(cachedIndex.cellSize, cellSize))
            {
                return cachedIndex;
            }

            var index = new RoadGraphSpatialIndex();
            index.cellSize = cellSize;
            index.segmentById = new Dictionary<int, RoadSegment>(roadSegments.Count);
            index.waypointByKey = new Dictionary<long, RoadGraphSpatialIndex.WaypointRef>();
            index.spatialGrid = new Dictionary<Vector2Int, List<long>>();

            foreach (var seg in roadSegments)
            {
                index.segmentById[seg.id] = seg;
                for (int i = 0; i < seg.waypoints.Count; i++)
                {
                    Vector3 wpPos = seg.waypoints[i].position;
                    Vector3 wpFwd = GetWaypointForward(seg, i);
                    long key = RoadGraphSpatialIndex.PackKey(seg.id, i);
                    index.waypointByKey[key] = new RoadGraphSpatialIndex.WaypointRef(seg, i, wpPos, wpFwd);

                    Vector2Int cell = RoadGraphSpatialIndex.ToGridCell(wpPos, cellSize);
                    if (!index.spatialGrid.TryGetValue(cell, out var bucket))
                    {
                        bucket = new List<long>();
                        index.spatialGrid[cell] = bucket;
                    }
                    bucket.Add(key);
                }
            }

            cachedIndex = index;
            return index;
        }

        public void InvalidateSpatialIndex()
        {
            cachedIndex = null;
        }

        private static Vector3 GetWaypointForward(RoadSegment segment, int waypointIndex)
        {
            if (segment == null || segment.waypoints == null || segment.waypoints.Count == 0)
                return Vector3.forward;

            waypointIndex = Mathf.Clamp(waypointIndex, 0, segment.waypoints.Count - 1);
            Vector3 forward = segment.waypoints[waypointIndex].forward;

            if (forward.sqrMagnitude < 0.0001f)
            {
                if (waypointIndex < segment.waypoints.Count - 1)
                    forward = segment.waypoints[waypointIndex + 1].position - segment.waypoints[waypointIndex].position;
                else if (waypointIndex > 0)
                    forward = segment.waypoints[waypointIndex].position - segment.waypoints[waypointIndex - 1].position;
            }

            forward.y = 0f;
            return forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
        }

        /// <summary>
        /// Get random road segment from network
        /// </summary>
        public RoadSegment GetRandomRoadSegment()
        {
            if (roadSegments.Count == 0) return null;
            return roadSegments[Random.Range(0, roadSegments.Count)];
        }

        /// <summary>
        /// Get random waypoint from entire network
        /// </summary>
        public (RoadSegment segment, int waypointIndex) GetRandomWaypoint()
        {
            var segment = GetRandomRoadSegment();
            if (segment == null || segment.waypoints.Count == 0)
                return (null, 0);

            int index = Random.Range(0, segment.waypoints.Count);
            return (segment, index);
        }

        /// <summary>
        /// Find nearest road segment and waypoint to a world position
        /// </summary>
        public (RoadSegment segment, int waypointIndex, float distance) FindNearestPoint(Vector3 worldPos)
        {
            RoadSegment nearestSegment = null;
            int nearestIndex = 0;
            float minDistance = float.MaxValue;

            foreach (var segment in roadSegments)
            {
                for (int i = 0; i < segment.waypoints.Count; i++)
                {
                    float dist = Vector3.Distance(worldPos, segment.waypoints[i].position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        nearestSegment = segment;
                        nearestIndex = i;
                    }
                }
            }

            return (nearestSegment, nearestIndex, minDistance);
        }

        /// <summary>
        /// Project point onto nearest road segment (more accurate than nearest waypoint)
        /// </summary>
        public (RoadSegment segment, int waypointIndex, Vector3 projectedPoint, Vector3 tangent) ProjectPointOnRoad(Vector3 worldPos)
        {
            RoadSegment bestSegment = null;
            int bestWaypointIndex = 0;
            Vector3 bestPoint = worldPos;
            Vector3 bestTangent = Vector3.forward;
            float minDistance = float.MaxValue;

            foreach (var segment in roadSegments)
            {
                for (int i = 0; i < segment.waypoints.Count - 1; i++)
                {
                    Vector3 p1 = segment.waypoints[i].position;
                    Vector3 p2 = segment.waypoints[i + 1].position;

                    // Project point onto line segment
                    Vector3 projected = ProjectPointOnLineSegment(worldPos, p1, p2);
                    float dist = Vector3.Distance(worldPos, projected);

                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        bestSegment = segment;
                        bestWaypointIndex = i;
                        bestPoint = projected;
                        bestTangent = (p2 - p1).normalized;
                    }
                }
            }

            return (bestSegment, bestWaypointIndex, bestPoint, bestTangent);
        }

        /// <summary>
        /// Project a point onto a line segment
        /// </summary>
        private Vector3 ProjectPointOnLineSegment(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
        {
            Vector3 lineDir = lineEnd - lineStart;
            float lineLength = lineDir.magnitude;
            lineDir.Normalize();

            Vector3 pointVector = point - lineStart;
            float dotProduct = Vector3.Dot(pointVector, lineDir);
            dotProduct = Mathf.Clamp(dotProduct, 0f, lineLength);

            return lineStart + lineDir * dotProduct;
        }
    }
}
