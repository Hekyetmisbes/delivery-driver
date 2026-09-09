using System.Collections.Generic;
using UnityEngine;

namespace TrafficSystem.Spline
{
    public class SplineRoad : MonoBehaviour
    {
        [SerializeField, HideInInspector]
        private List<Vector3> controlPoints = new List<Vector3>();

        [Tooltip("The width of a single lane in meters. Cars will drive this far to the right of the center line.")]
        public float laneWidth = 2.5f;

        [Tooltip("Intersection at the START of this road (t=0)")]
        public RoadIntersection startIntersection;

        [Tooltip("Intersection at the END of this road (t=1)")]
        public RoadIntersection endIntersection;

        [Tooltip("City or Highway sector this road belongs to (e.g., 'Ankara', 'İstanbul', 'Highways')")]
        public string sectorTag = "Ankara";

        // Cached distance lookup for constant speed traversal
        [SerializeField, HideInInspector]
        private float[] distances;

        public int ControlPointCount => controlPoints.Count;
        public int SegmentCount => (controlPoints.Count - 1) / 3;
        public bool IsUsable => HasValidControlPoints() && GetTotalLength() > 1f;

        private void Reset()
        {
            ResetToDefault();
        }

        public void ResetToDefault()
        {
            controlPoints.Clear();
            Vector3 center = transform.position;
            controlPoints.Add(center + Vector3.forward * -10f);
            controlPoints.Add(center + Vector3.forward * -3f);
            controlPoints.Add(center + Vector3.forward * 3f);
            controlPoints.Add(center + Vector3.forward * 10f);
            CalculateDistances();
        }

        public Vector3 GetControlPoint(int index)
        {
            if (index < 0 || index >= controlPoints.Count) return Vector3.zero;
            return controlPoints[index];
        }

        public void SetControlPoint(int index, Vector3 position)
        {
            if (index < 0 || index >= controlPoints.Count) return;

            // Constrain C1 connectivity for smooth curves
            if (index % 3 == 0)
            {
                Vector3 delta = position - controlPoints[index];
                if (index > 0) controlPoints[index - 1] += delta;
                if (index < controlPoints.Count - 1) controlPoints[index + 1] += delta;
                controlPoints[index] = position;
            }
            else
            {
                controlPoints[index] = position;
                bool nextIsAnchor = (index + 1) % 3 == 0;
                int correspondingAnchorIndex = nextIsAnchor ? index + 1 : index - 1;
                int otherControlIndex = nextIsAnchor ? index + 2 : index - 2;

                if (otherControlIndex >= 0 && otherControlIndex < controlPoints.Count)
                {
                    Vector3 anchor = controlPoints[correspondingAnchorIndex];
                    Vector3 dir = (anchor - position).normalized;
                    float dist = Vector3.Distance(anchor, controlPoints[otherControlIndex]);
                    controlPoints[otherControlIndex] = anchor + dir * dist;
                }
            }

            CalculateDistances();
        }

        public List<Vector3> GetAnchorPoints()
        {
            List<Vector3> anchors = new List<Vector3>();
            if (!HasValidControlPoints()) return anchors;
            for (int i = 0; i < controlPoints.Count; i += 3)
            {
                anchors.Add(controlPoints[i]);
            }
            return anchors;
        }

        public List<Vector3> GetAllControlPoints()
        {
            return controlPoints != null ? new List<Vector3>(controlPoints) : new List<Vector3>();
        }

        public void SetControlPoints(List<Vector3> newPoints)
        {
            if (newPoints == null || newPoints.Count < 4) return;
            controlPoints = new List<Vector3>(newPoints);
            CalculateDistances();
        }

        public void AddSegment()
        {
            Vector3 lastPoint = controlPoints[controlPoints.Count - 1];
            Vector3 secondToLastPoint = controlPoints[controlPoints.Count - 2];
            Vector3 dir = (lastPoint - secondToLastPoint).normalized;

            controlPoints.Add(lastPoint + dir * 5f);
            controlPoints.Add(lastPoint + dir * 10f);
            controlPoints.Add(lastPoint + dir * 15f);

            CalculateDistances();
        }

        public void SetLinearPath(IReadOnlyList<Vector3> anchors)
        {
            controlPoints.Clear();

            if (anchors == null || anchors.Count < 2)
            {
                ResetToDefault();
                return;
            }

            Vector3 previousAnchor = anchors[0];
            controlPoints.Add(previousAnchor);

            for (int i = 1; i < anchors.Count; i++)
            {
                Vector3 nextAnchor = anchors[i];
                Vector3 delta = nextAnchor - previousAnchor;

                controlPoints.Add(previousAnchor + delta * (1f / 3f));
                controlPoints.Add(previousAnchor + delta * (2f / 3f));
                controlPoints.Add(nextAnchor);

                previousAnchor = nextAnchor;
            }

            CalculateDistances();
        }

        public void SetSmoothPath(IReadOnlyList<Vector3> anchors)
        {
            controlPoints.Clear();

            if (anchors == null || anchors.Count < 2)
            {
                ResetToDefault();
                return;
            }

            if (anchors.Count == 2)
            {
                SetLinearPath(anchors);
                return;
            }

            int n = anchors.Count;
            Vector3[] tangents = new Vector3[n];

            for (int i = 0; i < n; i++)
            {
                if (i == 0)
                {
                    tangents[i] = anchors[1] - anchors[0];
                }
                else if (i == n - 1)
                {
                    tangents[i] = anchors[n - 1] - anchors[n - 2];
                }
                else
                {
                    // Continuous C1 Catmull-Rom tangent
                    tangents[i] = (anchors[i + 1] - anchors[i - 1]) * 0.5f;
                }
            }

            controlPoints.Add(anchors[0]);
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 p0 = anchors[i];
                Vector3 p1 = anchors[i + 1];

                Vector3 cp1 = p0 + tangents[i] * (1f / 3f);
                Vector3 cp2 = p1 - tangents[i + 1] * (1f / 3f);

                controlPoints.Add(cp1);
                controlPoints.Add(cp2);
                controlPoints.Add(p1);
            }

            CalculateDistances();
        }

        /// <summary>
        /// Simplifies a dense spline down to clean, well-spaced anchor points (like newly drawn roads)
        /// while preserving the overall curve shape, start/end connections, and smoothness.
        /// </summary>
        public bool Simplify(float targetSpacingMeters = 15f)
        {
            if (controlPoints == null || controlPoints.Count < 4)
            {
                return false;
            }

            float totalLength = GetTotalLength();
            if (totalLength < 1f)
            {
                return false;
            }

            targetSpacingMeters = Mathf.Max(4f, targetSpacingMeters);
            int targetSegments = Mathf.Max(2, Mathf.RoundToInt(totalLength / targetSpacingMeters));
            int currentAnchors = (controlPoints.Count - 1) / 3 + 1;

            if (currentAnchors <= targetSegments + 1 && controlPoints.Count <= 16)
            {
                return false;
            }

            List<Vector3> newAnchors = new List<Vector3>(targetSegments + 1);

            Vector3 startPoint = GetPoint(0f);
            Vector3 endPoint = GetPoint(1f);

            newAnchors.Add(startPoint);

            for (int i = 1; i < targetSegments; i++)
            {
                float targetDist = i * (totalLength / targetSegments);
                float t = DistanceToT(targetDist);
                newAnchors.Add(GetPoint(t));
            }

            newAnchors.Add(endPoint);

            SetSmoothPath(newAnchors);
            CalculateDistances();
            return true;
        }

        public void CalculateDistances()
        {
            int resolution = SegmentCount * 10;
            if (!HasValidControlPoints() || resolution < 2)
            {
                distances = System.Array.Empty<float>();
                return;
            }

            distances = new float[resolution];
            distances[0] = 0f;
            Vector3 prev = GetPoint(0f);
            for (int i = 1; i < resolution; i++)
            {
                float t = i / (float)(resolution - 1);
                Vector3 curr = GetPoint(t);
                distances[i] = distances[i - 1] + Vector3.Distance(prev, curr);
                prev = curr;
            }
        }

        public float GetTotalLength()
        {
            if (distances == null || distances.Length == 0) CalculateDistances();
            if (distances == null || distances.Length == 0) return 0f;
            return distances[distances.Length - 1];
        }

        public float TToDistance(float t)
        {
            if (distances == null || distances.Length == 0) CalculateDistances();
            if (distances == null || distances.Length == 0) return 0f;
            if (t <= 0f) return 0f;
            if (t >= 1f) return distances[distances.Length - 1];

            int resolution = distances.Length;
            float floatIndex = t * (resolution - 1);
            int idx1 = Mathf.FloorToInt(floatIndex);
            int idx2 = Mathf.CeilToInt(floatIndex);

            if (idx1 == idx2) return distances[idx1];
            return Mathf.Lerp(distances[idx1], distances[idx2], floatIndex - idx1);
        }

        public float DistanceToT(float distance)
        {
            if (distances == null || distances.Length == 0) CalculateDistances();
            if (distances == null || distances.Length == 0) return 0f;
            if (distance <= 0f) return 0f;
            float totalLen = distances[distances.Length - 1];
            if (distance >= totalLen) return 1f;

            for (int i = 0; i < distances.Length - 1; i++)
            {
                if (distance >= distances[i] && distance <= distances[i + 1])
                {
                    float segmentLen = distances[i + 1] - distances[i];
                    float tLocal = (distance - distances[i]) / segmentLen;
                    return (i + tLocal) / (distances.Length - 1);
                }
            }
            return 1f;
        }

        public Vector3 GetPoint(float t)
        {
            if (!HasValidControlPoints()) return Vector3.zero;
            t = Mathf.Clamp01(t);

            int i;
            if (t >= 1f)
            {
                t = 1f;
                i = controlPoints.Count - 4;
            }
            else
            {
                t *= SegmentCount;
                i = (int)t;
                t -= i;
                i *= 3;
            }

            return BezierUtility.EvaluateCubic(
                controlPoints[i],
                controlPoints[i + 1],
                controlPoints[i + 2],
                controlPoints[i + 3],
                t);
        }

        public Vector3 GetTangent(float t)
        {
            if (!HasValidControlPoints()) return Vector3.forward;
            t = Mathf.Clamp01(t);

            int i;
            if (t >= 1f)
            {
                t = 1f;
                i = controlPoints.Count - 4;
            }
            else
            {
                t *= SegmentCount;
                i = (int)t;
                t -= i;
                i *= 3;
            }

            Vector3 tangent = BezierUtility.EvaluateCubicDerivative(
                controlPoints[i],
                controlPoints[i + 1],
                controlPoints[i + 2],
                controlPoints[i + 3],
                t);

            return tangent.normalized;
        }

        public Vector3 GetRight(float t)
        {
            Vector3 tangent = GetTangent(t);
            return Vector3.Cross(Vector3.up, tangent).normalized;
        }

        public Vector3 GetLanePosition(float t, bool isForwardDirection)
        {
            Vector3 center = GetPoint(t);
            Vector3 right = GetRight(t);
            // Right hand traffic:
            // If driving forward (t: 0->1), lane is on the right.
            // If driving backward (t: 1->0), lane is on the left relative to Spline's forward, which is the driver's right.
            float offset = isForwardDirection ? laneWidth : -laneWidth;
            return center + right * offset;
        }

        /// <summary>
        /// Projects a world position onto the spline center line to find the closest t [0..1]
        /// </summary>
        public float GetClosestT(Vector3 worldPosition, int resolution = 100)
        {
            if (!HasValidControlPoints())
            {
                return 0f;
            }

            float bestT = 0f;
            float minDistSq = float.MaxValue;
            
            // First pass: coarse search
            for (int i = 0; i <= resolution; i++)
            {
                float t = i / (float)resolution;
                Vector3 p = GetPoint(t);
                float distSq = (p - worldPosition).sqrMagnitude;
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    bestT = t;
                }
            }
            
            // Second pass: fine search around bestT
            float step = 1f / resolution;
            float searchStart = Mathf.Max(0f, bestT - step);
            float searchEnd = Mathf.Min(1f, bestT + step);
            
            minDistSq = float.MaxValue;
            for (int i = 0; i <= 10; i++)
            {
                float t = Mathf.Lerp(searchStart, searchEnd, i / 10f);
                Vector3 p = GetPoint(t);
                float distSq = (p - worldPosition).sqrMagnitude;
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    bestT = t;
                }
            }

            return bestT;
        }

        private void OnDrawGizmos()
        {
            DrawSpline(Color.white, 0.5f); // Draw faded when not selected
        }

        private void OnDrawGizmosSelected()
        {
            DrawSpline(Color.cyan, 1f); // Draw bright when selected
            
            // Draw Control points
            Gizmos.color = Color.yellow;
            for (int i = 0; i < controlPoints.Count; i++)
            {
                Gizmos.DrawSphere(controlPoints[i], 0.3f);
            }
            // Draw Anchor lines
            Gizmos.color = Color.gray;
            for (int i = 0; i < controlPoints.Count - 1; i++)
            {
                if (i % 3 != 2) // Don't draw line between anchor and next anchor's control
                {
                    Gizmos.DrawLine(controlPoints[i], controlPoints[i+1]);
                }
            }
        }

        private void DrawSpline(Color color, float alpha)
        {
            if (!HasValidControlPoints()) return;
            
            int resolution = SegmentCount * 10;
            Vector3 prevCenter = GetPoint(0f);
            Vector3 prevForwardLane = GetLanePosition(0f, true);
            Vector3 prevBackwardLane = GetLanePosition(0f, false);
            
            for (int i = 1; i <= resolution; i++)
            {
                float t = i / (float)resolution;
                Vector3 center = GetPoint(t);
                Vector3 forwardLane = GetLanePosition(t, true);
                Vector3 backwardLane = GetLanePosition(t, false);
                
                // Center line
                Gizmos.color = new Color(color.r, color.g, color.b, alpha * 0.3f);
                Gizmos.DrawLine(prevCenter, center);
                
                // Forward Lane (Right side)
                Gizmos.color = new Color(0f, 1f, 0f, alpha);
                Gizmos.DrawLine(prevForwardLane, forwardLane);
                
                // Backward Lane (Left side)
                Gizmos.color = new Color(1f, 0f, 0f, alpha);
                Gizmos.DrawLine(prevBackwardLane, backwardLane);
                
                prevCenter = center;
                prevForwardLane = forwardLane;
                prevBackwardLane = backwardLane;
            }
        }

        private bool HasValidControlPoints()
        {
            return controlPoints != null && controlPoints.Count >= 4 && SegmentCount > 0;
        }
    }
}
