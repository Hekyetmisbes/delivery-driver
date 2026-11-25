using UnityEngine;

namespace TrafficSystem.Spline
{
    public static class BezierUtility
    {
        public static Vector3 EvaluateCubic(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector3 p = uuu * p0; // (1-t)^3 * P0
            p += 3f * uu * t * p1; // 3(1-t)^2 * t * P1
            p += 3f * u * tt * p2; // 3(1-t) * t^2 * P2
            p += ttt * p3; // t^3 * P3

            return p;
        }

        public static Vector3 EvaluateCubicDerivative(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;

            Vector3 d = 3f * uu * (p1 - p0);
            d += 6f * u * t * (p2 - p1);
            d += 3f * tt * (p3 - p2);

            return d;
        }

        public static Vector3 EvaluateQuadratic(Vector3 p0, Vector3 p1, Vector3 p2, float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            return (u * u * p0) + (2f * u * t * p1) + (t * t * p2);
        }
    }
}
