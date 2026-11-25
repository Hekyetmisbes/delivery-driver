using UnityEngine;

namespace TrafficSystem
{
    /// <summary>Main-thread ground query with complete-query fallback when the buffer fills.</summary>
    public static class NpcGroundingService
    {
        private static readonly RaycastHit[] GroundHitBuffer = new RaycastHit[32];

        public static Vector3 GetGroundedPosition(Transform vehicle, Vector3 desiredPosition,
            float groundRaycastHeight, float groundRaycastDistance, LayerMask groundMask, float clearance)
        {
            Vector3 origin = desiredPosition + Vector3.up * groundRaycastHeight;
            float maxDistance = groundRaycastHeight + groundRaycastDistance;
            RaycastHit[] hits = GroundHitBuffer;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, maxDistance,
                groundMask, QueryTriggerInteraction.Ignore);
            // NonAlloc can omit a closer hit when full. Preserve correctness with a rare fallback.
            if (count == hits.Length)
            {
                hits = Physics.RaycastAll(origin, Vector3.down, maxDistance, groundMask,
                    QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }

            float closestDistance = float.PositiveInfinity;
            Vector3 closestPoint = desiredPosition;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                Collider collider = hit.collider;
                if (collider == null || collider.transform.IsChildOf(vehicle) ||
                    hit.distance >= closestDistance)
                    continue;

                closestDistance = hit.distance;
                closestPoint = hit.point;
            }

            return float.IsPositiveInfinity(closestDistance)
                ? desiredPosition
                : closestPoint + Vector3.up * clearance;
        }
    }
}
