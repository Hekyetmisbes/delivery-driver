using System.Collections.Generic;
using UnityEngine;

namespace TrafficSystem
{
    internal static class RoadGraphMeshSampler
    {
        public static void SampleFromMesh(MeshFilter meshFilter, RoadSegment segment, int segmentId, float sampleStepMeters)
        {
            Mesh mesh = meshFilter.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            Transform meshTransform = meshFilter.transform;

            if (vertices.Length < 2)
            {
                Debug.LogWarning($"[RoadGraphBuilder] Mesh has too few vertices: {vertices.Length}");
                return;
            }

            List<Vector3> centerlinePoints = ExtractCenterline(vertices, meshTransform, sampleStepMeters);
            if (centerlinePoints.Count < 2)
            {
                Debug.LogWarning("[RoadGraphBuilder] Could not extract centerline from mesh");
                return;
            }

            for (int i = 0; i < centerlinePoints.Count; i++)
            {
                Vector3 pos = centerlinePoints[i];
                Vector3 forward = Vector3.forward;

                if (i < centerlinePoints.Count - 1)
                {
                    forward = (centerlinePoints[i + 1] - pos).normalized;
                }
                else if (i > 0)
                {
                    forward = (pos - centerlinePoints[i - 1]).normalized;
                }

                segment.waypoints.Add(new Waypoint(pos, forward, segmentId));
            }

            NormalizeWaypointForwards(segment);
        }

        public static List<Vector3> ExtractCenterline(Vector3[] localVertices, Transform meshTransform, float sampleStepMeters)
        {
            List<Vector3> centerline = new List<Vector3>();
            if (localVertices == null || localVertices.Length == 0 || meshTransform == null)
            {
                return centerline;
            }

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minZ = float.MaxValue;
            float maxZ = float.MinValue;
            foreach (Vector3 vertex in localVertices)
            {
                if (vertex.x < minX) minX = vertex.x;
                if (vertex.x > maxX) maxX = vertex.x;
                if (vertex.z < minZ) minZ = vertex.z;
                if (vertex.z > maxZ) maxZ = vertex.z;
            }

            float spanX = maxX - minX;
            float spanZ = maxZ - minZ;
            bool useZ = spanZ >= spanX;
            float dominantScale = Mathf.Abs(useZ ? meshTransform.lossyScale.z : meshTransform.lossyScale.x);
            if (dominantScale < 0.0001f)
            {
                dominantScale = 1f;
            }

            float roadLength = (useZ ? spanZ : spanX) * dominantScale;
            if (roadLength < 1f)
            {
                return centerline;
            }

            int sampleCount = Mathf.Max(2, Mathf.CeilToInt(roadLength / sampleStepMeters));
            float axisToleranceLocal = (sampleStepMeters * 0.5f) / dominantScale;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / (sampleCount - 1);
                float targetAxis = useZ ? Mathf.Lerp(minZ, maxZ, t) : Mathf.Lerp(minX, maxX, t);
                List<Vector3> nearVertices = new List<Vector3>();

                foreach (Vector3 vertex in localVertices)
                {
                    float axisValue = useZ ? vertex.z : vertex.x;
                    if (Mathf.Abs(axisValue - targetAxis) < axisToleranceLocal)
                    {
                        nearVertices.Add(vertex);
                    }
                }

                if (nearVertices.Count == 0)
                {
                    continue;
                }

                Vector3 center = Vector3.zero;
                foreach (Vector3 vertex in nearVertices)
                {
                    center += vertex;
                }

                center /= nearVertices.Count;
                centerline.Add(meshTransform.TransformPoint(center));
            }

            return centerline;
        }

        public static List<Vector3> ResamplePolyline(List<Vector3> points, float step)
        {
            List<Vector3> result = new List<Vector3>();
            if (points == null || points.Count == 0)
            {
                return result;
            }

            if (step <= 0.01f)
            {
                result.AddRange(points);
                return result;
            }

            result.Add(points[0]);
            float remaining = step;

            for (int i = 1; i < points.Count; i++)
            {
                Vector3 start = points[i - 1];
                Vector3 end = points[i];
                float dist = Vector3.Distance(start, end);
                if (dist < 0.001f)
                {
                    continue;
                }

                Vector3 dir = (end - start).normalized;
                float traveled = 0f;

                while (traveled + remaining <= dist)
                {
                    Vector3 pos = start + dir * (traveled + remaining);
                    result.Add(pos);
                    traveled += remaining;
                    remaining = step;
                }

                remaining -= dist - traveled;
                if (remaining < 0.001f)
                {
                    remaining = step;
                }
            }

            if (Vector3.Distance(result[result.Count - 1], points[points.Count - 1]) > 0.01f)
            {
                result.Add(points[points.Count - 1]);
            }

            return result;
        }

        public static void NormalizeWaypointForwards(RoadSegment segment)
        {
            if (segment == null || segment.waypoints.Count == 0)
            {
                return;
            }

            Vector3 previousForward = segment.waypoints[0].forward;
            if (previousForward.sqrMagnitude < 0.01f && segment.waypoints.Count > 1)
            {
                previousForward = segment.waypoints[1].position - segment.waypoints[0].position;
            }

            previousForward.y = 0f;
            if (previousForward.sqrMagnitude < 0.01f)
            {
                previousForward = Vector3.forward;
            }

            previousForward.Normalize();
            segment.waypoints[0].forward = previousForward;

            for (int i = 1; i < segment.waypoints.Count; i++)
            {
                Vector3 forward = segment.waypoints[i].forward;
                if (forward.sqrMagnitude < 0.01f)
                {
                    forward = previousForward;
                }

                forward.y = 0f;
                if (forward.sqrMagnitude < 0.01f)
                {
                    forward = previousForward;
                }

                if (Vector3.Dot(forward, previousForward) < 0f)
                {
                    forward = -forward;
                }

                forward.Normalize();
                segment.waypoints[i].forward = forward;
                previousForward = forward;
            }
        }
    }
}
