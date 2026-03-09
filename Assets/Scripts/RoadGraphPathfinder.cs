using System.Collections.Generic;
using UnityEngine;

namespace TrafficSystem
{
    public static class RoadGraphPathfinder
    {
        private readonly struct WaypointRef
        {
            public readonly RoadSegment segment;
            public readonly int index;
            public readonly Vector3 position;

            public WaypointRef(RoadSegment waypointSegment, int waypointIndex, Vector3 waypointPosition)
            {
                segment = waypointSegment;
                index = waypointIndex;
                position = waypointPosition;
            }
        }

        public static List<Vector3> FindPath(
            RoadGraph graph,
            Vector3 startWorld,
            Vector3 endWorld,
            float transferMaxDistance = 8f)
        {
            if (graph == null || graph.roadSegments.Count == 0)
            {
                return null;
            }

            var (_, _, projectedStart, _) = graph.ProjectPointOnRoad(startWorld);
            var (_, _, projectedEnd, _) = graph.ProjectPointOnRoad(endWorld);
            var (startSeg, startIdx, _) = graph.FindNearestPoint(projectedStart);
            var (endSeg, endIdx, _) = graph.FindNearestPoint(projectedEnd);

            if (startSeg == null || endSeg == null)
            {
                return null;
            }

            // Same segment shortcut
            if (startSeg.id == endSeg.id)
            {
                return CollectWaypointsBetween(startSeg, startIdx, endIdx, projectedStart, projectedEnd);
            }

            // Build lookups and a spatial grid index for fast neighbor discovery.
            float spatialCellSize = Mathf.Max(1f, transferMaxDistance);
            float heuristicCellSize = Mathf.Max(2f, spatialCellSize * 0.5f);
            float transferMaxDistanceSqr = transferMaxDistance * transferMaxDistance;

            var segmentById = new Dictionary<int, RoadSegment>(graph.roadSegments.Count);
            var waypointByKey = new Dictionary<long, WaypointRef>();
            var spatialGrid = new Dictionary<Vector2Int, List<long>>();
            foreach (var seg in graph.roadSegments)
            {
                segmentById[seg.id] = seg;
                for (int i = 0; i < seg.waypoints.Count; i++)
                {
                    Vector3 wpPos = seg.waypoints[i].position;
                    long key = PackKey(seg.id, i);
                    waypointByKey[key] = new WaypointRef(seg, i, wpPos);

                    Vector2Int cell = ToGridCell(wpPos, spatialCellSize);
                    if (!spatialGrid.TryGetValue(cell, out var bucket))
                    {
                        bucket = new List<long>();
                        spatialGrid[cell] = bucket;
                    }
                    bucket.Add(key);
                }
            }

            // A* search with Manhattan-grid heuristic.
            var gScore = new Dictionary<long, float>();
            var prev = new Dictionary<long, long>();
            var closed = new HashSet<long>();

            // Priority queue by fScore
            var pq = new SortedList<float, Queue<long>>();

            void Enqueue(long key, float cost)
            {
                if (!pq.ContainsKey(cost))
                {
                    pq[cost] = new Queue<long>();
                }

                pq[cost].Enqueue(key);
            }

            long Dequeue(out float cost)
            {
                var first = pq.Keys[0];
                cost = first;
                var queue = pq[first];
                long key = queue.Dequeue();
                if (queue.Count == 0) pq.Remove(first);
                return key;
            }

            long startKey = PackKey(startSeg.id, startIdx);
            long endKey = PackKey(endSeg.id, endIdx);

            Vector3 endPos = endSeg.waypoints[endIdx].position;
            gScore[startKey] = 0f;
            Enqueue(startKey, GridManhattanHeuristic(startSeg.waypoints[startIdx].position, endPos, heuristicCellSize));

            while (pq.Count > 0)
            {
                long currentKey = Dequeue(out _);

                if (currentKey == endKey)
                {
                    break;
                }

                if (closed.Contains(currentKey))
                {
                    continue;
                }

                closed.Add(currentKey);
                if (!gScore.TryGetValue(currentKey, out float currentG))
                {
                    continue;
                }

                UnpackKey(currentKey, out int curSegId, out int curWpIdx);

                if (!segmentById.TryGetValue(curSegId, out RoadSegment curSeg))
                {
                    continue;
                }

                Vector3 curPos = curSeg.waypoints[curWpIdx].position;

                void TryNeighbor(int neighborIdx)
                {
                    if (neighborIdx < 0 || neighborIdx >= curSeg.waypoints.Count)
                    {
                        return;
                    }

                    long nKey = PackKey(curSegId, neighborIdx);
                    if (closed.Contains(nKey))
                    {
                        return;
                    }

                    Vector3 nPos = curSeg.waypoints[neighborIdx].position;
                    float tentativeG = currentG + Vector3.Distance(curPos, nPos);
                    if (!gScore.TryGetValue(nKey, out float bestKnownG) || tentativeG < bestKnownG)
                    {
                        gScore[nKey] = tentativeG;
                        prev[nKey] = currentKey;
                        float f = tentativeG + GridManhattanHeuristic(nPos, endPos, heuristicCellSize);
                        Enqueue(nKey, f);
                    }
                }

                // Intra-segment edges (both directions).
                TryNeighbor(curWpIdx - 1);
                TryNeighbor(curWpIdx + 1);

                // Inter-segment edges (explicit road connections).
                foreach (var conn in curSeg.connections)
                {
                    if (conn.fromWaypointIndex != curWpIdx)
                    {
                        continue;
                    }

                    long nKey = PackKey(conn.toSegment.id, conn.toWaypointIndex);
                    if (closed.Contains(nKey))
                    {
                        continue;
                    }

                    Vector3 nPos = conn.toSegment.waypoints[conn.toWaypointIndex].position;
                    float tentativeG = currentG + Vector3.Distance(curPos, nPos);
                    if (!gScore.TryGetValue(nKey, out float bestKnownG) || tentativeG < bestKnownG)
                    {
                        gScore[nKey] = tentativeG;
                        prev[nKey] = currentKey;
                        float f = tentativeG + GridManhattanHeuristic(nPos, endPos, heuristicCellSize);
                        Enqueue(nKey, f);
                    }
                }

                // Recovery edges: short transfer hops, searched through nearby spatial grid cells.
                if (transferMaxDistance > 0.01f)
                {
                    Vector2Int curCell = ToGridCell(curPos, spatialCellSize);
                    int radius = Mathf.CeilToInt(transferMaxDistance / spatialCellSize);
                    for (int gx = curCell.x - radius; gx <= curCell.x + radius; gx++)
                    {
                        for (int gz = curCell.y - radius; gz <= curCell.y + radius; gz++)
                        {
                            var lookupCell = new Vector2Int(gx, gz);
                            if (!spatialGrid.TryGetValue(lookupCell, out var bucket))
                            {
                                continue;
                            }

                            for (int i = 0; i < bucket.Count; i++)
                            {
                                long nKey = bucket[i];
                                if (nKey == currentKey || closed.Contains(nKey))
                                {
                                    continue;
                                }

                                if (!waypointByKey.TryGetValue(nKey, out WaypointRef candidate))
                                {
                                    continue;
                                }

                                Vector3 delta = candidate.position - curPos;
                                float d2 = delta.sqrMagnitude;
                                if (d2 <= 0.0001f || d2 > transferMaxDistanceSqr)
                                {
                                    continue;
                                }

                                float tentativeG = currentG + (Mathf.Sqrt(d2) * 1.05f);
                                if (!gScore.TryGetValue(nKey, out float bestKnownG) || tentativeG < bestKnownG)
                                {
                                    gScore[nKey] = tentativeG;
                                    prev[nKey] = currentKey;
                                    float f = tentativeG + GridManhattanHeuristic(candidate.position, endPos, heuristicCellSize);
                                    Enqueue(nKey, f);
                                }
                            }
                        }
                    }
                }
            }

            // Reconstruct path
            if (!prev.ContainsKey(endKey) && startKey != endKey)
            {
                return null; // No path found
            }

            var pathKeys = new List<long>();
            long cur = endKey;
            while (cur != startKey)
            {
                pathKeys.Add(cur);
                if (!prev.ContainsKey(cur))
                    return null;
                cur = prev[cur];
            }
            pathKeys.Add(startKey);
            pathKeys.Reverse();

            var path = new List<Vector3>(pathKeys.Count + 2);
            path.Add(projectedStart);
            foreach (long key in pathKeys)
            {
                UnpackKey(key, out int segId, out int wpIdx);
                if (segmentById.TryGetValue(segId, out RoadSegment seg) &&
                    wpIdx < seg.waypoints.Count)
                {
                    path.Add(seg.waypoints[wpIdx].position);
                }
            }
            path.Add(projectedEnd);

            return path;
        }

        private static long PackKey(int segmentId, int waypointIndex)
        {
            return ((long)segmentId << 20) | (uint)waypointIndex;
        }

        private static void UnpackKey(long key, out int segmentId, out int waypointIndex)
        {
            segmentId = (int)(key >> 20);
            waypointIndex = (int)(key & 0xFFFFF);
        }

        private static Vector2Int ToGridCell(Vector3 position, float cellSize)
        {
            return new Vector2Int(
                Mathf.FloorToInt(position.x / cellSize),
                Mathf.FloorToInt(position.z / cellSize));
        }

        private static float GridManhattanHeuristic(Vector3 from, Vector3 to, float cellSize)
        {
            Vector2Int fromCell = ToGridCell(from, cellSize);
            Vector2Int toCell = ToGridCell(to, cellSize);
            int manhattanSteps = Mathf.Abs(fromCell.x - toCell.x) + Mathf.Abs(fromCell.y - toCell.y);
            return manhattanSteps * cellSize * 0.70710678f;
        }

        private static List<Vector3> CollectWaypointsBetween(
            RoadSegment segment, int fromIdx, int toIdx,
            Vector3 startWorld, Vector3 endWorld)
        {
            var path = new List<Vector3>();
            path.Add(startWorld);

            int step = fromIdx <= toIdx ? 1 : -1;
            for (int i = fromIdx; i != toIdx + step; i += step)
            {
                if (i >= 0 && i < segment.waypoints.Count)
                {
                    path.Add(segment.waypoints[i].position);
                }
            }

            path.Add(endWorld);
            return path;
        }
    }
}
