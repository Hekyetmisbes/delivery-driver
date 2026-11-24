using UnityEngine;

namespace TrafficSystem
{
    public static class NpcTrafficLodPolicy
    {
        public static bool UseWaypointMode(float distance, bool alreadyLightweight, float enter, float exit)
        {
            if (float.IsNaN(distance) || float.IsInfinity(distance))
                return false;
            return alreadyLightweight ? distance > exit : distance >= enter;
        }

        public static int SafetyInterval(float distance, bool urgent, bool maneuver, bool adaptive)
        {
            if (!adaptive || urgent) return 1;
            if (maneuver || distance < 50f) return 2;
            return distance < 150f ? 3 : distance < 300f ? 5 : 8;
        }

        public static int PathInterval(float distance, bool urgent, bool maneuver, bool adaptive)
        {
            if (!adaptive || urgent || maneuver || distance < 50f) return 1;
            return distance < 150f ? 2 : distance < 300f ? 4 : 5;
        }

        public static bool IsDue(uint tick, uint previous, int interval) =>
            unchecked(tick - previous) >= (uint)Mathf.Max(1, interval);

        public static Vector3 StepTowards(Vector3 position, Vector3 target, float speedMs, float deltaTime) =>
            Vector3.MoveTowards(position, target, Mathf.Max(0f, speedMs) * Mathf.Max(0f, deltaTime));
    }
}
