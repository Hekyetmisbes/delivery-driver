using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using UnityEngine;

namespace TrafficSystem
{
    public static class RoadGraphPathfinder
    {
        public const float InitialNavigationTransferDistance = 6f;
        public enum IncrementalSearchStatus
        {
            Running,
            Succeeded,
            Failed
        }

        private sealed class SearchBuffers
        {
            public readonly List<WaypointCandidate> candidateBuffer = new List<WaypointCandidate>(CandidateWaypointLimit + 4);
            public readonly List<long> startCandidateKeys = new List<long>(CandidateWaypointLimit);
            public readonly List<long> endCandidateKeys = new List<long>(CandidateWaypointLimit);
            public readonly Dictionary<long, float> gScore = new Dictionary<long, float>(256);
            public readonly Dictionary<long, long> prev = new Dictionary<long, long>(256);
            public readonly HashSet<long> closed = new HashSet<long>();
            public readonly StableRoadPriorityQueue priorityQueue = new StableRoadPriorityQueue();
            public readonly List<long> pathKeys = new List<long>(256);

            public void PrepareCandidateBuffer(List<long> resultKeys)
            {
                candidateBuffer.Clear();
                resultKeys.Clear();
            }

            public void PreparePathSearch()
            {
                gScore.Clear();
                prev.Clear();
                closed.Clear();
                pathKeys.Clear();

                priorityQueue.Clear();
            }
        }

        public sealed class IncrementalPathSearchSession
        {
            private readonly RoadGraphSpatialIndex idx;
            private readonly SearchBuffers buffers = new SearchBuffers();
            private readonly Vector3 projectedStart;
            private readonly Vector3 projectedEnd;
            private readonly Vector3 desiredTravelDirection;
            private readonly float transferMaxDistance;
            private readonly float transferMaxDistanceSqr;
            private readonly float spatialCellSize;
            private readonly float heuristicCellSize;
            private readonly List<long> startCandidateKeys = new List<long>(CandidateWaypointLimit);
            private readonly List<long> endCandidateKeys = new List<long>(CandidateWaypointLimit);

            private int startCandidateIndex;
            private int endCandidateIndex;
            private bool pairInitialized;
            private long currentStartKey;
            private long currentEndKey;
            private Vector3 currentEndPosition;

            public IncrementalSearchStatus Status { get; private set; }
            public List<Vector3> Path { get; private set; }
            public float PathCost { get; private set; } = float.MaxValue;
            public int ExpandedNodes { get; private set; }
            public int QueuePops { get; private set; }
            public double LastStepMilliseconds { get; private set; }

            internal IncrementalPathSearchSession(
                RoadGraph graph,
                Vector3 startWorld,
                Vector3 endWorld,
                float transferDistance)
            {
                Status = IncrementalSearchStatus.Failed;

                if (graph == null || graph.roadSegments == null || graph.roadSegments.Count == 0)
                {
                    return;
                }

                var (projectedStartSegment, projectedStartEdgeIndex, projectedStartPoint, projectedStartTangent) = graph.ProjectPointOnRoad(startWorld);
                var (projectedEndSegment, projectedEndEdgeIndex, projectedEndPoint, projectedEndTangent) = graph.ProjectPointOnRoad(endWorld);

                if (projectedStartSegment == null || projectedEndSegment == null)
                {
                    return;
                }

                projectedStart = projectedStartPoint;
                projectedEnd = projectedEndPoint;
                transferMaxDistance = transferDistance;
                transferMaxDistanceSqr = transferDistance * transferDistance;
                spatialCellSize = Mathf.Max(1f, transferDistance);
                heuristicCellSize = Mathf.Max(2f, spatialCellSize * 0.5f);
                idx = graph.GetOrBuildSpatialIndex(spatialCellSize);

                Vector3 routeDirection = GetPlanarDirection(projectedEnd - projectedStart);
                if (routeDirection.sqrMagnitude < 0.0001f)
                {
                    routeDirection = GetPlanarDirection(projectedStartTangent);
                }

                if (routeDirection.sqrMagnitude < 0.0001f)
                {
                    routeDirection = GetPlanarDirection(projectedEndTangent);
                }

                desiredTravelDirection = routeDirection;

                int projectedStartFallbackIndex = Mathf.Clamp(
                    projectedStartEdgeIndex + 1,
                    0,
                    projectedStartSegment.waypoints.Count - 1);
                int projectedEndFallbackIndex = Mathf.Clamp(
                    projectedEndEdgeIndex,
                    0,
                    projectedEndSegment.waypoints.Count - 1);

                float waypointSearchRadius = Mathf.Max(6f, transferDistance * 1.5f);
                List<long> scratchStart = buffers.startCandidateKeys;
                List<long> scratchEnd = buffers.endCandidateKeys;

                CollectPreferredWaypointKeys(
                    idx,
                    projectedStart,
                    desiredTravelDirection,
                    waypointSearchRadius,
                    RoadGraphSpatialIndex.PackKey(projectedStartSegment.id, projectedStartFallbackIndex),
                    buffers,
                    scratchStart);

                CollectPreferredWaypointKeys(
                    idx,
                    projectedEnd,
                    desiredTravelDirection,
                    waypointSearchRadius,
                    RoadGraphSpatialIndex.PackKey(projectedEndSegment.id, projectedEndFallbackIndex),
                    buffers,
                    scratchEnd);

                if (scratchStart.Count == 0 || scratchEnd.Count == 0)
                {
                    return;
                }

                startCandidateKeys.AddRange(scratchStart);
                endCandidateKeys.AddRange(scratchEnd);
                Status = IncrementalSearchStatus.Running;
            }

            public void Step(int maxIterations) => Step(maxIterations, 0f);

            /// <summary>Cooperative time budget; checked between node expansions, not a hard deadline.</summary>
            public void Step(int maxIterations, float maxMilliseconds)
            {
                if (Status != IncrementalSearchStatus.Running)
                    return;

                long started = Stopwatch.GetTimestamp();
                long budgetTicks = maxMilliseconds > 0f
                    ? (long)(maxMilliseconds * Stopwatch.Frequency / 1000d)
                    : long.MaxValue;
                int remaining = Mathf.Max(1, maxIterations);
                try
                {
                    while (remaining > 0 && Status == IncrementalSearchStatus.Running)
                    {
                        if (!pairInitialized && !TryBeginNextPair())
                        {
                            Status = IncrementalSearchStatus.Failed;
                            return;
                        }

                        if (Status != IncrementalSearchStatus.Running)
                            return;

                        int consumed = ContinueCurrentPair(remaining, started, budgetTicks);
                        remaining -= consumed;
                        if (consumed <= 0 || IsBudgetExhausted(started, budgetTicks))
                            break;
                    }
                }
                finally
                {
                    LastStepMilliseconds = (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
                }
            }

            private static bool IsBudgetExhausted(long started, long budgetTicks) =>
                budgetTicks != long.MaxValue && Stopwatch.GetTimestamp() - started >= budgetTicks;

            private bool TryBeginNextPair()
            {
                while (startCandidateIndex < startCandidateKeys.Count)
                {
                    while (endCandidateIndex < endCandidateKeys.Count)
                    {
                        currentStartKey = startCandidateKeys[startCandidateIndex];
                        currentEndKey = endCandidateKeys[endCandidateIndex++];

                        if (!idx.waypointByKey.TryGetValue(currentStartKey, out RoadGraphSpatialIndex.WaypointRef startRef) ||
                            !idx.waypointByKey.TryGetValue(currentEndKey, out RoadGraphSpatialIndex.WaypointRef endRef))
                        {
                            continue;
                        }

                        if (startRef.segment.id == endRef.segment.id && startRef.index <= endRef.index)
                        {
                            Path = CollectWaypointsBetween(startRef.segment, startRef.index, endRef.index, projectedStart, projectedEnd);
                            PathCost = Vector3.Distance(projectedStart, projectedEnd);
                            Status = Path != null && Path.Count >= 2
                                ? IncrementalSearchStatus.Succeeded
                                : IncrementalSearchStatus.Failed;
                            return true;
                        }

                        buffers.PreparePathSearch();
                        buffers.gScore[currentStartKey] = 0f;
                        Enqueue(buffers, currentStartKey, GridManhattanHeuristic(startRef.position, endRef.position, heuristicCellSize));
                        currentEndPosition = endRef.position;
                        pairInitialized = true;
                        return true;
                    }

                    startCandidateIndex++;
                    endCandidateIndex = 0;
                }

                return false;
            }

            private int ContinueCurrentPair(int maxIterations, long started, long budgetTicks)
            {
                int iterations = 0;

                while (buffers.priorityQueue.Count > 0 && iterations < maxIterations && Status == IncrementalSearchStatus.Running)
                {
                    // Always allow at least one pop so tiny budgets cannot stall the session.
                    if (iterations > 0 && IsBudgetExhausted(started, budgetTicks))
                        break;

                    iterations++;
                    QueuePops++;
                    long currentKey = Dequeue(buffers);

                    if (currentKey == currentEndKey)
                    {
                        if (TryBuildCurrentPath(out List<Vector3> path, out float pathCost))
                        {
                            Path = path;
                            PathCost = pathCost;
                            Status = IncrementalSearchStatus.Succeeded;
                        }

                        pairInitialized = false;
                        return iterations;
                    }

                    if (buffers.closed.Contains(currentKey))
                    {
                        continue;
                    }

                    buffers.closed.Add(currentKey);
                    ExpandedNodes++;
                    if (!buffers.gScore.TryGetValue(currentKey, out float currentG))
                    {
                        continue;
                    }

                    RoadGraphSpatialIndex.UnpackKey(currentKey, out int curSegId, out int curWpIdx);
                    if (!idx.segmentById.TryGetValue(curSegId, out RoadSegment curSeg))
                    {
                        continue;
                    }

                    Vector3 curPos = curSeg.waypoints[curWpIdx].position;
                    Vector3 curForward = idx.waypointByKey.TryGetValue(currentKey, out RoadGraphSpatialIndex.WaypointRef curRef)
                        ? curRef.forward
                        : GetWaypointForward(curSeg, curWpIdx);

                    int forwardNeighborIndex = curWpIdx + 1;
                    if (forwardNeighborIndex < curSeg.waypoints.Count)
                    {
                        Vector3 nextPos = curSeg.waypoints[forwardNeighborIndex].position;
                        long forwardKey = RoadGraphSpatialIndex.PackKey(curSegId, forwardNeighborIndex);
                        Vector3 nextForward = idx.waypointByKey.TryGetValue(forwardKey, out RoadGraphSpatialIndex.WaypointRef forwardRef)
                            ? forwardRef.forward
                            : GetWaypointForward(curSeg, forwardNeighborIndex);
                        float forwardCost = GetLanePenalty(curSeg) +
                                            GetAlignmentPenalty(nextForward, desiredTravelDirection) +
                                            GetTurnPenalty(curForward, nextForward);
                        TryNeighbor(currentKey, curPos, currentG, forwardKey, nextPos, forwardCost);
                    }

                    for (int connectionIndex = 0; connectionIndex < curSeg.connections.Count; connectionIndex++)
                    {
                        RoadConnection connection = curSeg.connections[connectionIndex];
                        if (connection.fromWaypointIndex != curWpIdx)
                        {
                            continue;
                        }

                        long connectionKey = RoadGraphSpatialIndex.PackKey(connection.toSegment.id, connection.toWaypointIndex);
                        Vector3 connectionPos = connection.toSegment.waypoints[connection.toWaypointIndex].position;
                        Vector3 connectionForward = idx.waypointByKey.TryGetValue(connectionKey, out RoadGraphSpatialIndex.WaypointRef connectionRef)
                            ? connectionRef.forward
                            : GetWaypointForward(connection.toSegment, connection.toWaypointIndex);
                        float connectionCost = GetLanePenalty(connection.toSegment) +
                                               GetAlignmentPenalty(connectionForward, desiredTravelDirection) +
                                               GetTurnPenalty(curForward, connectionForward);
                        TryNeighbor(currentKey, curPos, currentG, connectionKey, connectionPos, connectionCost);
                    }

                    if (transferMaxDistance > 0.01f)
                    {
                        Vector2Int currentCell = RoadGraphSpatialIndex.ToGridCell(curPos, spatialCellSize);
                        int radius = Mathf.CeilToInt(transferMaxDistance / spatialCellSize);

                        for (int gx = currentCell.x - radius; gx <= currentCell.x + radius; gx++)
                        {
                            for (int gz = currentCell.y - radius; gz <= currentCell.y + radius; gz++)
                            {
                                Vector2Int lookupCell = new Vector2Int(gx, gz);
                                if (!idx.spatialGrid.TryGetValue(lookupCell, out List<long> bucket))
                                {
                                    continue;
                                }

                                for (int bucketIndex = 0; bucketIndex < bucket.Count; bucketIndex++)
                                {
                                    long transferKey = bucket[bucketIndex];
                                    if (transferKey == currentKey || buffers.closed.Contains(transferKey))
                                    {
                                        continue;
                                    }

                                    if (!idx.waypointByKey.TryGetValue(transferKey, out RoadGraphSpatialIndex.WaypointRef candidate))
                                    {
                                        continue;
                                    }

                                    Vector3 delta = candidate.position - curPos;
                                    delta.y = 0f;
                                    float distanceSqr = delta.sqrMagnitude;
                                    if (distanceSqr <= 0.0001f || distanceSqr > transferMaxDistanceSqr)
                                    {
                                        continue;
                                    }

                                    if (!IsTransferCandidateEligible(curSeg, curWpIdx, candidate.segment, candidate.index))
                                    {
                                        continue;
                                    }

                                    if (!TryGetTransferPenalty(curPos, curForward, candidate.position, transferMaxDistance, out float transferPenalty))
                                    {
                                        continue;
                                    }

                                    float transferDistance = Mathf.Sqrt(distanceSqr);
                                    float normalizedTransferDistance = transferMaxDistance > 0.01f
                                        ? transferDistance / transferMaxDistance
                                        : 1f;
                                    float transferCost = (transferDistance * TransferDistancePenaltyMultiplier) +
                                                         (normalizedTransferDistance * normalizedTransferDistance * TransferNormalizedPenalty) +
                                                         transferPenalty +
                                                         GetLanePenalty(candidate.segment) +
                                                         GetAlignmentPenalty(candidate.forward, desiredTravelDirection) +
                                                         GetTurnPenalty(curForward, candidate.forward);
                                    TryNeighbor(currentKey, curPos, currentG, transferKey, candidate.position, transferCost);
                                }
                            }
                        }
                    }
                }

                if (buffers.priorityQueue.Count == 0)
                {
                    pairInitialized = false;
                }

                return iterations;
            }

            private void TryNeighbor(long currentKey, Vector3 currentPosition, float currentG, long neighborKey, Vector3 neighborPosition, float extraCost)
            {
                if (buffers.closed.Contains(neighborKey))
                {
                    return;
                }

                float tentativeG = currentG + Vector3.Distance(currentPosition, neighborPosition) + extraCost;
                if (!buffers.gScore.TryGetValue(neighborKey, out float bestKnownG) || tentativeG < bestKnownG)
                {
                    buffers.gScore[neighborKey] = tentativeG;
                    buffers.prev[neighborKey] = currentKey;
                    float priority = tentativeG + GridManhattanHeuristic(neighborPosition, currentEndPosition, heuristicCellSize);
                    Enqueue(buffers, neighborKey, priority);
                }
            }

            private bool TryBuildCurrentPath(out List<Vector3> path, out float pathCost)
            {
                path = null;
                pathCost = float.MaxValue;

                if (!buffers.prev.ContainsKey(currentEndKey) && currentStartKey != currentEndKey)
                {
                    return false;
                }

                List<long> pathKeys = buffers.pathKeys;
                pathKeys.Clear();

                long currentKey = currentEndKey;
                while (currentKey != currentStartKey)
                {
                    pathKeys.Add(currentKey);
                    if (!buffers.prev.ContainsKey(currentKey))
                    {
                        return false;
                    }

                    currentKey = buffers.prev[currentKey];
                }

                pathKeys.Add(currentStartKey);
                pathKeys.Reverse();

                path = new List<Vector3>(pathKeys.Count + 2);
                path.Add(projectedStart);

                for (int i = 0; i < pathKeys.Count; i++)
                {
                    RoadGraphSpatialIndex.UnpackKey(pathKeys[i], out int segmentId, out int waypointIndex);
                    if (idx.segmentById.TryGetValue(segmentId, out RoadSegment segment) &&
                        waypointIndex >= 0 &&
                        waypointIndex < segment.waypoints.Count)
                    {
                        path.Add(segment.waypoints[waypointIndex].position);
                    }
                }

                path.Add(projectedEnd);
                pathCost = buffers.gScore.TryGetValue(currentEndKey, out float totalCost)
                    ? totalCost
                    : CalculatePathLength(path);
                return path.Count >= 2;
            }
        }

        public readonly struct PathSearchDiagnostics
        {
            public PathSearchDiagnostics(
                RoadSegment startSegment,
                int startWaypointIndex,
                Vector3 projectedStartPoint,
                float startProjectionDistance,
                RoadSegment endSegment,
                int endWaypointIndex,
                Vector3 projectedEndPoint,
                float endProjectionDistance,
                int segmentCount,
                int connectionCount)
            {
                StartSegment = startSegment;
                StartWaypointIndex = startWaypointIndex;
                ProjectedStartPoint = projectedStartPoint;
                StartProjectionDistance = startProjectionDistance;
                EndSegment = endSegment;
                EndWaypointIndex = endWaypointIndex;
                ProjectedEndPoint = projectedEndPoint;
                EndProjectionDistance = endProjectionDistance;
                SegmentCount = segmentCount;
                ConnectionCount = connectionCount;
            }

            public RoadSegment StartSegment { get; }
            public int StartWaypointIndex { get; }
            public Vector3 ProjectedStartPoint { get; }
            public float StartProjectionDistance { get; }
            public RoadSegment EndSegment { get; }
            public int EndWaypointIndex { get; }
            public Vector3 ProjectedEndPoint { get; }
            public float EndProjectionDistance { get; }
            public int SegmentCount { get; }
            public int ConnectionCount { get; }
        }

        private readonly struct WaypointCandidate
        {
            public WaypointCandidate(long key, float score)
            {
                Key = key;
                Score = score;
            }

            public long Key { get; }
            public float Score { get; }
        }

        private const float LeftLanePenalty = 6f;
        private const float UnnamedLanePenalty = 1.25f;
        private const float ReverseAlignmentPenalty = 8f;
        private const float HardReverseTurnPenalty = 10f;
        private const float TransferBackwardDotThreshold = -0.85f;
        private const float TransferLeftAllowance = 4f;
        private const float TransferDistancePenaltyMultiplier = 4f;
        private const float TransferNormalizedPenalty = 40f;
        private const float TransferHeightPenalty = 10f;
        private const float MaxTransferHeightDelta = 2.5f;
        private const int CandidateWaypointLimit = 10;
        private const int MaxAStarIterations = 18000;
        public static List<Vector3> FindPath(
            RoadGraph graph,
            Vector3 startWorld,
            Vector3 endWorld,
            float transferMaxDistance = 8f)
        {
            IncrementalPathSearchSession session = StartIncrementalSearch(graph, startWorld, endWorld, transferMaxDistance);
            while (session != null && session.Status == IncrementalSearchStatus.Running)
            {
                session.Step(MaxAStarIterations);
            }

            return session != null && session.Status == IncrementalSearchStatus.Succeeded
                ? session.Path
                : null;
        }

        public static IncrementalPathSearchSession StartIncrementalSearch(
            RoadGraph graph,
            Vector3 startWorld,
            Vector3 endWorld,
            float transferMaxDistance = 8f)
        {
            return new IncrementalPathSearchSession(graph, startWorld, endWorld, transferMaxDistance);
        }

        public static bool TryGetPathDiagnostics(
            RoadGraph graph,
            Vector3 startWorld,
            Vector3 endWorld,
            out PathSearchDiagnostics diagnostics)
        {
            diagnostics = default;
            if (graph == null || graph.roadSegments == null || graph.roadSegments.Count == 0)
            {
                return false;
            }

            var (projectedStartSegment, projectedStartEdgeIndex, projectedStart, _) = graph.ProjectPointOnRoad(startWorld);
            var (projectedEndSegment, projectedEndEdgeIndex, projectedEnd, _) = graph.ProjectPointOnRoad(endWorld);
            if (projectedStartSegment == null || projectedEndSegment == null)
            {
                return false;
            }

            int connectionCount = 0;
            for (int i = 0; i < graph.roadSegments.Count; i++)
            {
                RoadSegment segment = graph.roadSegments[i];
                if (segment != null && segment.connections != null)
                {
                    connectionCount += segment.connections.Count;
                }
            }

            diagnostics = new PathSearchDiagnostics(
                projectedStartSegment,
                projectedStartEdgeIndex,
                projectedStart,
                Vector3.Distance(startWorld, projectedStart),
                projectedEndSegment,
                projectedEndEdgeIndex,
                projectedEnd,
                Vector3.Distance(endWorld, projectedEnd),
                graph.roadSegments.Count,
                connectionCount);
            return true;
        }

        private static void CollectPreferredWaypointKeys(
            RoadGraphSpatialIndex idx,
            Vector3 targetPosition,
            Vector3 desiredTravelDirection,
            float searchRadius,
            long fallbackKey,
            SearchBuffers buffers,
            List<long> keys)
        {
            buffers.PrepareCandidateBuffer(keys);
            List<WaypointCandidate> candidates = buffers.candidateBuffer;
            float searchRadiusSqr = searchRadius * searchRadius;
            int gridRadius = Mathf.CeilToInt(searchRadius / idx.cellSize) + 1;
            Vector2Int centerCell = RoadGraphSpatialIndex.ToGridCell(targetPosition, idx.cellSize);

            for (int gx = centerCell.x - gridRadius; gx <= centerCell.x + gridRadius; gx++)
            {
                for (int gz = centerCell.y - gridRadius; gz <= centerCell.y + gridRadius; gz++)
                {
                    if (!idx.spatialGrid.TryGetValue(new Vector2Int(gx, gz), out var bucket))
                    {
                        continue;
                    }

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        long key = bucket[i];
                        if (!idx.waypointByKey.TryGetValue(key, out RoadGraphSpatialIndex.WaypointRef candidate))
                        {
                            continue;
                        }

                        Vector3 delta = candidate.position - targetPosition;
                        delta.y = 0f;
                        float distanceSqr = delta.sqrMagnitude;
                        if (distanceSqr > searchRadiusSqr)
                        {
                            continue;
                        }

                        float distance = Mathf.Sqrt(distanceSqr);
                        float score = distance +
                                      (GetLanePenalty(candidate.segment) * 2f) +
                                      GetAlignmentPenalty(candidate.forward, desiredTravelDirection);
                        AddCandidate(candidates, new WaypointCandidate(key, score));
                    }
                }
            }

            if (!ContainsCandidateKey(candidates, fallbackKey))
            {
                AddCandidate(candidates, new WaypointCandidate(fallbackKey, 1000000f));
            }

            candidates.Sort((a, b) => a.Score.CompareTo(b.Score));
            for (int i = 0; i < candidates.Count && keys.Count < CandidateWaypointLimit; i++)
            {
                if (!ContainsKey(keys, candidates[i].Key))
                {
                    keys.Add(candidates[i].Key);
                }
            }

            if (keys.Count == 0)
            {
                keys.Add(fallbackKey);
            }
        }

        private static void Enqueue(SearchBuffers buffers, long key, float cost) =>
            buffers.priorityQueue.Enqueue(key, cost);

        private static long Dequeue(SearchBuffers buffers) =>
            buffers.priorityQueue.Dequeue();

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

        private static Vector3 GetWaypointForward(RoadSegment segment, int waypointIndex)
        {
            if (segment == null || segment.waypoints == null || segment.waypoints.Count == 0)
            {
                return Vector3.forward;
            }

            waypointIndex = Mathf.Clamp(waypointIndex, 0, segment.waypoints.Count - 1);
            Vector3 forward = segment.waypoints[waypointIndex].forward;

            if (forward.sqrMagnitude < 0.0001f)
            {
                if (waypointIndex < segment.waypoints.Count - 1)
                {
                    forward = segment.waypoints[waypointIndex + 1].position - segment.waypoints[waypointIndex].position;
                }
                else if (waypointIndex > 0)
                {
                    forward = segment.waypoints[waypointIndex].position - segment.waypoints[waypointIndex - 1].position;
                }
            }

            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                return Vector3.forward;
            }

            return forward.normalized;
        }

        private static Vector3 GetPlanarDirection(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude < 0.0001f ? Vector3.zero : direction.normalized;
        }

        private static float GetLanePenalty(RoadSegment segment)
        {
            if (segment == null || string.IsNullOrEmpty(segment.name))
            {
                return UnnamedLanePenalty;
            }

            string lowerName = segment.name.ToLowerInvariant();
            if (lowerName.Contains("lane_right") || lowerName.Contains("right_lane"))
            {
                return 0f;
            }

            if (lowerName.Contains("lane_left") || lowerName.Contains("left_lane"))
            {
                return LeftLanePenalty;
            }

            return UnnamedLanePenalty;
        }

        private static float GetAlignmentPenalty(Vector3 waypointForward, Vector3 desiredTravelDirection)
        {
            if (desiredTravelDirection.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            float dot = Vector3.Dot(GetPlanarDirection(waypointForward), desiredTravelDirection);
            if (dot < -0.5f)
            {
                return ReverseAlignmentPenalty + ((-dot - 0.5f) * 6f);
            }

            if (dot < 0f)
            {
                return (-dot) * 4f;
            }

            return (1f - dot) * 1.5f;
        }

        private static float GetTurnPenalty(Vector3 currentForward, Vector3 nextForward)
        {
            Vector3 a = GetPlanarDirection(currentForward);
            Vector3 b = GetPlanarDirection(nextForward);
            if (a.sqrMagnitude < 0.0001f || b.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            float dot = Vector3.Dot(a, b);
            if (dot < -0.1f)
            {
                return HardReverseTurnPenalty;
            }

            return Mathf.Max(0f, (1f - dot) * 0.75f);
        }

        private static bool IsTransferCandidateEligible(
            RoadSegment currentSegment,
            int currentWaypointIndex,
            RoadSegment candidateSegment,
            int candidateWaypointIndex)
        {
            // Only reject transfers to the same segment.  Spatial distance, alignment,
            // lateral offset, and height checks in TryGetTransferPenalty already guard
            // against bad transfers.  The previous endpoint-proximity requirement was
            // blocking valid routes on SimplePoly dual-lane road layouts where segments
            // are short mesh pieces and the player or destination projects to a
            // mid-segment waypoint.
            return currentSegment != null &&
                   candidateSegment != null &&
                   candidateSegment != currentSegment;
        }

        private static bool TryGetTransferPenalty(
            Vector3 currentPosition,
            Vector3 currentForward,
            Vector3 candidatePosition,
            float transferMaxDistance,
            out float transferPenalty)
        {
            transferPenalty = 0f;

            Vector3 planarForward = GetPlanarDirection(currentForward);
            if (planarForward.sqrMagnitude < 0.0001f)
            {
                return true;
            }

            Vector3 delta = candidatePosition - currentPosition;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            Vector3 moveDirection = delta.normalized;
            float forwardDot = Vector3.Dot(moveDirection, planarForward);
            if (forwardDot < TransferBackwardDotThreshold)
            {
                return false;
            }

            Vector3 right = Vector3.Cross(Vector3.up, planarForward).normalized;
            if (right.sqrMagnitude < 0.0001f)
            {
                return true;
            }

            float heightDelta = Mathf.Abs(candidatePosition.y - currentPosition.y);
            if (heightDelta > MaxTransferHeightDelta)
            {
                return false;
            }

            float lateralOffset = Vector3.Dot(delta, right);
            if (lateralOffset < -TransferLeftAllowance)
            {
                return false;
            }

            if (lateralOffset < 0f)
            {
                transferPenalty = Mathf.Abs(lateralOffset) * 2.5f;
            }

            transferPenalty += heightDelta * TransferHeightPenalty;

            return true;
        }

        private static List<Vector3> CollectWaypointsBetween(
            RoadSegment segment, int fromIdx, int toIdx,
            Vector3 startWorld, Vector3 endWorld)
        {
            int estimatedWaypointCount = Mathf.Abs(toIdx - fromIdx) + 3;
            var path = new List<Vector3>(estimatedWaypointCount);
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

        private static void AddCandidate(List<WaypointCandidate> candidates, WaypointCandidate candidate)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Key != candidate.Key)
                {
                    continue;
                }

                if (candidate.Score < candidates[i].Score)
                {
                    candidates[i] = candidate;
                }

                return;
            }

            candidates.Add(candidate);
        }

        private static bool ContainsCandidateKey(List<WaypointCandidate> candidates, long key)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Key == key)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsKey(List<long> keys, long key)
        {
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i] == key)
                {
                    return true;
                }
            }

            return false;
        }

        private static float CalculatePathLength(List<Vector3> path)
        {
            if (path == null || path.Count < 2)
            {
                return 0f;
            }

            float length = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                length += Vector3.Distance(path[i - 1], path[i]);
            }

            return length;
        }
    }
}
