using System.Collections.Generic;
using TrafficSystem;
using UnityEngine;

public static class MinimapRoadTextureBuilder
{
    private const int MediumGraphSegmentThreshold = 400;
    private const int LargeGraphSegmentThreshold = 900;
    private const float PairHeadingAlignmentThreshold = 0.92f;
    private const float PairMaxAverageSeparation = 6f;
    private const float PairMaxSampleSeparation = 8f;
    private const float PairMaxEndpointDistance = 18f;
    private const float PairMaxCentroidDistance = 10f;
    private const float EndpointClusterDistance = 5f;
    private const float BridgeAlignmentThreshold = 0.9f;
    private const float MinBridgeLength = 0.05f;
    private const int PairComparisonSamples = 12;

    private enum LaneSide
    {
        Unknown,
        Left,
        Right
    }

    private sealed class LanePolyline
    {
        public List<Vector3> Points;
        public string BaseName;
        public LaneSide Side;
        public Vector3 Start;
        public Vector3 End;
        public Vector3 Centroid;
    }

    private readonly struct PolylineEndpoint
    {
        public readonly int PolylineIndex;
        public readonly bool IsStart;
        public readonly Vector3 Position;

        public PolylineEndpoint(int polylineIndex, bool isStart, Vector3 position)
        {
            PolylineIndex = polylineIndex;
            IsStart = isStart;
            Position = position;
        }
    }

    public static Texture2D Build(
        RoadGraph graph,
        Bounds worldBounds,
        int resolution,
        Color backgroundColor,
        Color roadColor,
        Color roadOutlineColor,
        int roadWidthPixels)
    {
        int requestedSize = Mathf.Clamp(resolution, 256, 4096);
        int size = ResolveTextureSize(graph, requestedSize);
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
        texture.name = "ProceduralMinimapTexture";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[size * size];
        Color32 background = backgroundColor;
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = background;
        }

        if (graph != null && graph.roadSegments != null)
        {
            List<Vector3> junctionCenters;
            List<List<Vector3>> bridgeSegments;
            List<List<Vector3>> drawablePolylines = BuildDrawablePolylines(graph, out bridgeSegments, out junctionCenters);
            for (int polylineIndex = 0; polylineIndex < drawablePolylines.Count; polylineIndex++)
            {
                List<Vector3> polyline = drawablePolylines[polylineIndex];
                if (polyline == null || polyline.Count < 2)
                {
                    continue;
                }

                for (int waypointIndex = 1; waypointIndex < polyline.Count; waypointIndex++)
                {
                    Vector2Int a = WorldToPixel(polyline[waypointIndex - 1], worldBounds, size);
                    Vector2Int b = WorldToPixel(polyline[waypointIndex], worldBounds, size);
                    DrawThickLine(pixels, size, a, b, roadColor, roadWidthPixels);
                }
            }

            for (int bridgeIndex = 0; bridgeIndex < bridgeSegments.Count; bridgeIndex++)
            {
                List<Vector3> bridge = bridgeSegments[bridgeIndex];
                if (bridge == null || bridge.Count < 2)
                {
                    continue;
                }

                for (int pointIndex = 1; pointIndex < bridge.Count; pointIndex++)
                {
                    Vector2Int a = WorldToPixel(bridge[pointIndex - 1], worldBounds, size);
                    Vector2Int b = WorldToPixel(bridge[pointIndex], worldBounds, size);
                    DrawThickLine(pixels, size, a, b, roadColor, roadWidthPixels);
                }
            }

            int junctionRadius = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(roadWidthPixels, 1) * 0.8f));
            for (int i = 0; i < junctionCenters.Count; i++)
            {
                Vector2Int pixel = WorldToPixel(junctionCenters[i], worldBounds, size);
                DrawDisc(pixels, size, pixel.x, pixel.y, junctionRadius, roadColor);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static int ResolveTextureSize(RoadGraph graph, int requestedSize)
    {
        if (graph == null || graph.roadSegments == null)
        {
            return requestedSize;
        }

        int segmentCount = graph.roadSegments.Count;
        if (segmentCount >= LargeGraphSegmentThreshold)
        {
            return Mathf.Min(requestedSize, 256);
        }

        if (segmentCount >= MediumGraphSegmentThreshold)
        {
            return Mathf.Min(requestedSize, 512);
        }

        return requestedSize;
    }

    private static List<List<Vector3>> BuildDrawablePolylines(
        RoadGraph graph,
        out List<List<Vector3>> bridgeSegments,
        out List<Vector3> junctionCenters)
    {
        List<LanePolyline> polylines = new List<LanePolyline>();

        for (int i = 0; i < graph.roadSegments.Count; i++)
        {
            RoadSegment segment = graph.roadSegments[i];
            if (segment == null || segment.waypoints == null || segment.waypoints.Count < 2)
            {
                continue;
            }

            List<Vector3> points = new List<Vector3>(segment.waypoints.Count);
            for (int j = 0; j < segment.waypoints.Count; j++)
            {
                points.Add(segment.waypoints[j].position);
            }

            if (points.Count < 2)
            {
                continue;
            }

            polylines.Add(new LanePolyline
            {
                Points = points,
                BaseName = NormalizeSegmentName(segment.name),
                Side = GetLaneSide(segment.name),
                Start = points[0],
                End = points[points.Count - 1],
                Centroid = ComputeCentroid(points)
            });
        }

        List<List<Vector3>> result = new List<List<Vector3>>();
        bool[] used = new bool[polylines.Count];
        for (int i = 0; i < polylines.Count; i++)
        {
            if (used[i])
            {
                continue;
            }

            LanePolyline current = polylines[i];
            int pairIndex = FindBestLanePair(polylines, used, i);
            used[i] = true;

            if (pairIndex >= 0)
            {
                used[pairIndex] = true;
                result.Add(BuildAverageCenterline(current.Points, polylines[pairIndex].Points));
                continue;
            }

            result.Add(ResamplePolyline(current.Points, Mathf.Max(8, current.Points.Count * 2)));
        }

        bridgeSegments = BuildEndpointBridges(result, out junctionCenters);
        return result;
    }

    private static List<List<Vector3>> BuildEndpointBridges(List<List<Vector3>> polylines, out List<Vector3> junctionCenters)
    {
        junctionCenters = new List<Vector3>();
        if (polylines == null || polylines.Count < 2)
        {
            return new List<List<Vector3>>();
        }

        List<List<Vector3>> bridgeSegments = new List<List<Vector3>>();
        List<List<PolylineEndpoint>> clusters = BuildEndpointClusters(polylines);
        for (int i = 0; i < clusters.Count; i++)
        {
            List<PolylineEndpoint> cluster = clusters[i];
            if (cluster == null || cluster.Count < 2)
            {
                continue;
            }

            HashSet<int> uniquePolylineIndices = new HashSet<int>();
            Vector3 centroid = Vector3.zero;
            for (int endpointIndex = 0; endpointIndex < cluster.Count; endpointIndex++)
            {
                uniquePolylineIndices.Add(cluster[endpointIndex].PolylineIndex);
                centroid += cluster[endpointIndex].Position;
            }

            if (uniquePolylineIndices.Count < 2)
            {
                continue;
            }

            centroid /= cluster.Count;
            junctionCenters.Add(centroid);
            if (cluster.Count == 2 && TryBuildDirectBridge(cluster[0], cluster[1], polylines, out List<Vector3> directBridge))
            {
                bridgeSegments.Add(directBridge);
                continue;
            }

            for (int endpointIndex = 0; endpointIndex < cluster.Count; endpointIndex++)
            {
                Vector3 start = cluster[endpointIndex].Position;
                if (Vector3.Distance(start, centroid) <= MinBridgeLength)
                {
                    continue;
                }

                bridgeSegments.Add(new List<Vector3>(2) { start, centroid });
            }
        }

        return bridgeSegments;
    }

    private static bool TryBuildDirectBridge(
        PolylineEndpoint first,
        PolylineEndpoint second,
        List<List<Vector3>> polylines,
        out List<Vector3> bridge)
    {
        bridge = null;
        if (first.PolylineIndex == second.PolylineIndex)
        {
            return false;
        }

        List<Vector3> a = polylines[first.PolylineIndex];
        List<Vector3> b = polylines[second.PolylineIndex];
        if (a == null || b == null || a.Count < 2 || b.Count < 2)
        {
            return false;
        }

        Vector3 aDirection = first.IsStart ? GetStartDirection(a) : GetEndDirection(a);
        Vector3 bDirection = second.IsStart ? GetStartDirection(b) : GetEndDirection(b);
        if (aDirection.sqrMagnitude < 0.001f || bDirection.sqrMagnitude < 0.001f)
        {
            return false;
        }

        float alignment = Mathf.Abs(Vector3.Dot(aDirection, bDirection));
        if (alignment < BridgeAlignmentThreshold)
        {
            return false;
        }

        if (Vector3.Distance(first.Position, second.Position) > EndpointClusterDistance)
        {
            return false;
        }

        bridge = new List<Vector3>(2) { first.Position, second.Position };
        return true;
    }

    private static List<List<PolylineEndpoint>> BuildEndpointClusters(List<List<Vector3>> polylines)
    {
        List<PolylineEndpoint> endpoints = new List<PolylineEndpoint>(polylines.Count * 2);
        Dictionary<Vector2Int, List<int>> grid = new Dictionary<Vector2Int, List<int>>();

        for (int i = 0; i < polylines.Count; i++)
        {
            List<Vector3> polyline = polylines[i];
            if (polyline == null || polyline.Count < 2)
            {
                continue;
            }

            AddEndpointToClusterGrid(endpoints, grid, new PolylineEndpoint(i, true, polyline[0]));
            AddEndpointToClusterGrid(endpoints, grid, new PolylineEndpoint(i, false, polyline[polyline.Count - 1]));
        }

        List<List<PolylineEndpoint>> clusters = new List<List<PolylineEndpoint>>();
        bool[] visited = new bool[endpoints.Count];

        for (int endpointIndex = 0; endpointIndex < endpoints.Count; endpointIndex++)
        {
            if (visited[endpointIndex])
            {
                continue;
            }

            List<PolylineEndpoint> cluster = new List<PolylineEndpoint>();
            Queue<int> pending = new Queue<int>();
            pending.Enqueue(endpointIndex);
            visited[endpointIndex] = true;

            while (pending.Count > 0)
            {
                int currentIndex = pending.Dequeue();
                PolylineEndpoint current = endpoints[currentIndex];
                cluster.Add(current);

                Vector2Int cell = ToEndpointCell(current.Position);
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    for (int offsetY = -1; offsetY <= 1; offsetY++)
                    {
                        Vector2Int neighborCell = new Vector2Int(cell.x + offsetX, cell.y + offsetY);
                        if (!grid.TryGetValue(neighborCell, out List<int> neighborIndices))
                        {
                            continue;
                        }

                        for (int neighborListIndex = 0; neighborListIndex < neighborIndices.Count; neighborListIndex++)
                        {
                            int neighborIndex = neighborIndices[neighborListIndex];
                            if (visited[neighborIndex])
                            {
                                continue;
                            }

                            if (Vector3.Distance(current.Position, endpoints[neighborIndex].Position) > EndpointClusterDistance)
                            {
                                continue;
                            }

                            visited[neighborIndex] = true;
                            pending.Enqueue(neighborIndex);
                        }
                    }
                }
            }

            clusters.Add(cluster);
        }

        return clusters;
    }

    private static void AddEndpointToClusterGrid(
        List<PolylineEndpoint> endpoints,
        Dictionary<Vector2Int, List<int>> grid,
        PolylineEndpoint endpoint)
    {
        int endpointIndex = endpoints.Count;
        endpoints.Add(endpoint);

        Vector2Int cell = ToEndpointCell(endpoint.Position);
        if (!grid.TryGetValue(cell, out List<int> indices))
        {
            indices = new List<int>();
            grid[cell] = indices;
        }

        indices.Add(endpointIndex);
    }

    private static Vector2Int ToEndpointCell(Vector3 position)
    {
        float safeCellSize = Mathf.Max(0.1f, EndpointClusterDistance);
        return new Vector2Int(
            Mathf.FloorToInt(position.x / safeCellSize),
            Mathf.FloorToInt(position.z / safeCellSize));
    }

    private static string NormalizeSegmentName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "unnamed";
        }

        string normalized = name.ToLowerInvariant();
        normalized = normalized.Replace("lane_right", string.Empty);
        normalized = normalized.Replace("right_lane", string.Empty);
        normalized = normalized.Replace("lane_left", string.Empty);
        normalized = normalized.Replace("left_lane", string.Empty);
        normalized = normalized.Replace("_", string.Empty);
        normalized = normalized.Replace("-", string.Empty);
        normalized = normalized.Replace(" ", string.Empty);
        return normalized;
    }

    private static LaneSide GetLaneSide(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return LaneSide.Unknown;
        }

        string normalized = name.ToLowerInvariant();
        if (normalized.Contains("lane_left") || normalized.Contains("left_lane"))
        {
            return LaneSide.Left;
        }

        if (normalized.Contains("lane_right") || normalized.Contains("right_lane"))
        {
            return LaneSide.Right;
        }

        return LaneSide.Unknown;
    }

    private static int FindBestLanePair(List<LanePolyline> polylines, bool[] used, int index)
    {
        LanePolyline source = polylines[index];
        int bestIndex = -1;
        float bestScore = float.MaxValue;

        for (int i = 0; i < polylines.Count; i++)
        {
            if (i == index || used[i])
            {
                continue;
            }

            LanePolyline candidate = polylines[i];
            if (!ArePairable(source, candidate, out float score))
            {
                continue;
            }

            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static bool ArePairable(LanePolyline a, LanePolyline b, out float score)
    {
        score = float.MaxValue;

        float centroidDistance = Vector3.Distance(a.Centroid, b.Centroid);
        Vector3 aDirection = GetPolylineDirection(a.Points);
        Vector3 bDirection = GetPolylineDirection(b.Points);
        if (aDirection.sqrMagnitude < 0.001f || bDirection.sqrMagnitude < 0.001f)
        {
            return false;
        }

        float headingAlignment = Mathf.Abs(Vector3.Dot(aDirection, bDirection));
        if (headingAlignment < PairHeadingAlignmentThreshold)
        {
            return false;
        }

        float directEndpoints = Vector3.Distance(a.Start, b.Start) + Vector3.Distance(a.End, b.End);
        float reversedEndpoints = Vector3.Distance(a.Start, b.End) + Vector3.Distance(a.End, b.Start);
        float endpointDistance = Mathf.Min(directEndpoints, reversedEndpoints);
        if (endpointDistance > PairMaxEndpointDistance)
        {
            return false;
        }

        List<Vector3> comparisonPoints = b.Points;
        if (reversedEndpoints < directEndpoints)
        {
            comparisonPoints = new List<Vector3>(b.Points);
            comparisonPoints.Reverse();
        }

        CalculatePairSeparation(a.Points, comparisonPoints, out float averageSeparation, out float maxSeparation);
        if (averageSeparation > PairMaxAverageSeparation || maxSeparation > PairMaxSampleSeparation)
        {
            return false;
        }

        if (centroidDistance > Mathf.Max(PairMaxCentroidDistance, averageSeparation * 2.5f))
        {
            return false;
        }

        bool namesMatch = a.BaseName == b.BaseName;
        bool oppositeNamedLanes =
            (a.Side == LaneSide.Left && b.Side == LaneSide.Right) ||
            (a.Side == LaneSide.Right && b.Side == LaneSide.Left);
        float namingPenalty = namesMatch ? -1.5f : 0f;
        if (oppositeNamedLanes)
        {
            namingPenalty -= 0.5f;
        }

        score = (averageSeparation * 4f) +
                maxSeparation +
                (centroidDistance * 0.35f) +
                (endpointDistance * 0.2f) +
                ((1f - headingAlignment) * 20f) +
                namingPenalty;
        return true;
    }

    private static void CalculatePairSeparation(List<Vector3> a, List<Vector3> b, out float averageSeparation, out float maxSeparation)
    {
        averageSeparation = 0f;
        maxSeparation = 0f;

        int sampleCount = PairComparisonSamples;
        for (int i = 0; i < sampleCount; i++)
        {
            float t = sampleCount <= 1 ? 0f : i / (float)(sampleCount - 1);
            float separation = Vector3.Distance(EvaluatePolyline(a, t), EvaluatePolyline(b, t));
            averageSeparation += separation;
            if (separation > maxSeparation)
            {
                maxSeparation = separation;
            }
        }

        averageSeparation /= Mathf.Max(1, sampleCount);
    }

    private static List<Vector3> BuildAverageCenterline(List<Vector3> a, List<Vector3> b)
    {
        if (a == null || a.Count < 2)
        {
            return b;
        }

        if (b == null || b.Count < 2)
        {
            return a;
        }

        if (ShouldReverseForAlignment(a, b))
        {
            b = new List<Vector3>(b);
            b.Reverse();
        }

        int sampleCount = Mathf.Max(Mathf.Max(a.Count, b.Count) * 2, 12);
        List<Vector3> centerline = new List<Vector3>(sampleCount);
        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            float t = sampleCount <= 1 ? 0f : sampleIndex / (float)(sampleCount - 1);
            Vector3 pa = EvaluatePolyline(a, t);
            Vector3 pb = EvaluatePolyline(b, t);
            centerline.Add((pa + pb) * 0.5f);
        }

        return centerline;
    }

    private static bool ShouldReverseForAlignment(List<Vector3> a, List<Vector3> b)
    {
        float direct = Vector3.Distance(a[0], b[0]) + Vector3.Distance(a[a.Count - 1], b[b.Count - 1]);
        float reversed = Vector3.Distance(a[0], b[b.Count - 1]) + Vector3.Distance(a[a.Count - 1], b[0]);
        return reversed < direct;
    }

    private static Vector3 ComputeCentroid(List<Vector3> points)
    {
        Vector3 centroid = Vector3.zero;
        if (points == null || points.Count == 0)
        {
            return centroid;
        }

        for (int i = 0; i < points.Count; i++)
        {
            centroid += points[i];
        }

        return centroid / points.Count;
    }

    private static Vector3 GetPolylineDirection(List<Vector3> points)
    {
        if (points == null || points.Count < 2)
        {
            return Vector3.zero;
        }

        Vector3 direction = points[points.Count - 1] - points[0];
        direction.y = 0f;
        return direction.sqrMagnitude < 0.001f ? Vector3.zero : direction.normalized;
    }

    private static Vector3 GetStartDirection(List<Vector3> points)
    {
        if (points == null || points.Count < 2)
        {
            return Vector3.zero;
        }

        for (int i = 1; i < points.Count; i++)
        {
            Vector3 direction = points[i] - points[0];
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                return direction.normalized;
            }
        }

        return Vector3.zero;
    }

    private static Vector3 GetEndDirection(List<Vector3> points)
    {
        if (points == null || points.Count < 2)
        {
            return Vector3.zero;
        }

        for (int i = points.Count - 2; i >= 0; i--)
        {
            Vector3 direction = points[points.Count - 1] - points[i];
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                return direction.normalized;
            }
        }

        return Vector3.zero;
    }

    private static List<Vector3> ResamplePolyline(List<Vector3> polyline, int sampleCount)
    {
        if (polyline == null || polyline.Count < 2)
        {
            return polyline;
        }

        List<Vector3> result = new List<Vector3>(sampleCount);
        int count = Mathf.Max(2, sampleCount);
        for (int i = 0; i < count; i++)
        {
            float t = count <= 1 ? 0f : i / (float)(count - 1);
            result.Add(EvaluatePolyline(polyline, t));
        }

        return result;
    }

    private static Vector3 EvaluatePolyline(List<Vector3> polyline, float normalizedDistance)
    {
        if (polyline == null || polyline.Count == 0)
        {
            return Vector3.zero;
        }

        if (polyline.Count == 1)
        {
            return polyline[0];
        }

        float totalLength = 0f;
        for (int i = 1; i < polyline.Count; i++)
        {
            totalLength += Vector3.Distance(polyline[i - 1], polyline[i]);
        }

        if (totalLength <= 0.001f)
        {
            return polyline[0];
        }

        float targetDistance = Mathf.Clamp01(normalizedDistance) * totalLength;
        float traversed = 0f;
        for (int i = 1; i < polyline.Count; i++)
        {
            Vector3 start = polyline[i - 1];
            Vector3 end = polyline[i];
            float segmentLength = Vector3.Distance(start, end);
            if (segmentLength <= 0.001f)
            {
                continue;
            }

            if (traversed + segmentLength >= targetDistance)
            {
                float segmentT = (targetDistance - traversed) / segmentLength;
                return Vector3.Lerp(start, end, segmentT);
            }

            traversed += segmentLength;
        }

        return polyline[polyline.Count - 1];
    }

    private static Vector2Int WorldToPixel(Vector3 worldPosition, Bounds bounds, int textureSize)
    {
        float u = bounds.size.x > 0.01f ? Mathf.InverseLerp(bounds.min.x, bounds.max.x, worldPosition.x) : 0.5f;
        float v = bounds.size.z > 0.01f ? Mathf.InverseLerp(bounds.min.z, bounds.max.z, worldPosition.z) : 0.5f;
        int x = Mathf.Clamp(Mathf.RoundToInt(u * (textureSize - 1)), 0, textureSize - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(v * (textureSize - 1)), 0, textureSize - 1);
        return new Vector2Int(x, y);
    }

    private static void DrawThickLine(Color32[] pixels, int textureSize, Vector2Int start, Vector2Int end, Color color, int thickness)
    {
        int radius = Mathf.Max(1, thickness) / 2;
        int dx = Mathf.Abs(end.x - start.x);
        int dy = Mathf.Abs(end.y - start.y);
        int sx = start.x < end.x ? 1 : -1;
        int sy = start.y < end.y ? 1 : -1;
        int err = dx - dy;
        int x = start.x;
        int y = start.y;

        while (true)
        {
            DrawDisc(pixels, textureSize, x, y, radius, color);
            if (x == end.x && y == end.y)
            {
                break;
            }

            int e2 = err * 2;
            if (e2 > -dy)
            {
                err -= dy;
                x += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y += sy;
            }
        }
    }

    private static void DrawDisc(Color32[] pixels, int textureSize, int centerX, int centerY, int radius, Color color)
    {
        int radiusSqr = radius * radius;
        int minX = Mathf.Max(0, centerX - radius);
        int maxX = Mathf.Min(textureSize - 1, centerX + radius);
        int minY = Mathf.Max(0, centerY - radius);
        int maxY = Mathf.Min(textureSize - 1, centerY + radius);

        for (int y = minY; y <= maxY; y++)
        {
            int dy = y - centerY;
            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - centerX;
                if ((dx * dx) + (dy * dy) > radiusSqr)
                {
                    continue;
                }

                pixels[(y * textureSize) + x] = color;
            }
        }
    }
}
