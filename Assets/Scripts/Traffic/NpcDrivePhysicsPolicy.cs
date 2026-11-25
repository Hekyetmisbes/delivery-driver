using UnityEngine;

namespace TrafficSystem
{
    /// <summary>Pure actuator safeguards, independent of traffic queries and route planning.</summary>
    public static class NpcDrivePhysicsPolicy
    {
        public static bool RequiresFreshDecision(bool dynamicBody, bool hasDrivePlan, bool urgent) =>
            dynamicBody && (!hasDrivePlan || urgent);

        public static float LimitForwardTarget(float targetKmh, bool mustStop, bool imminentRisk)
        {
            if (mustStop || float.IsNaN(targetKmh) || float.IsInfinity(targetKmh))
                return 0f;
            targetKmh = Mathf.Max(0f, targetKmh);
            return imminentRisk ? Mathf.Min(targetKmh, 12f) : targetKmh;
        }

        public static bool CanAccelerate(float speedDifference, float tolerance, bool mustStop) =>
            !mustStop && speedDifference > Mathf.Max(0f, tolerance);

        // Same finite denominator and travel clamp as the player CarController.
        public static float SuspensionTravel(float localContactY, float wheelRadius, float suspensionDistance) =>
            Mathf.Clamp01((-localContactY - wheelRadius) / Mathf.Max(0.001f, suspensionDistance));
    }
}
