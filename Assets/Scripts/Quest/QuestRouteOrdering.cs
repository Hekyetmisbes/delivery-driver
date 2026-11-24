using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDriver.Quest
{
    /// <summary>Preserves the original nearest-neighbor route ordering and tie order.</summary>
    public static class QuestRouteOrdering
    {
        public static List<QuestLocation> Optimize(QuestLocation start, List<QuestLocation> stops)
        {
            List<QuestLocation> optimized = new List<QuestLocation>();
            List<QuestLocation> remaining = new List<QuestLocation>(stops);
            QuestLocation current = start;

            while (remaining.Count > 0)
            {
                QuestLocation nearest = FindNearest(current, remaining);
                if (nearest != null)
                {
                    optimized.Add(nearest);
                    remaining.Remove(nearest);
                    current = nearest;
                }
                else
                {
                    // Fallback: just add the first remaining
                    optimized.Add(remaining[0]);
                    remaining.RemoveAt(0);
                }
            }

            return optimized;
        }

        /// <summary>
        /// Finds nearest location from a list
        /// </summary>
        private static QuestLocation FindNearest(QuestLocation current, List<QuestLocation> candidates)
        {
            if (candidates == null || candidates.Count == 0 || current == null)
            {
                return null;
            }

            QuestLocation nearest = null;
            float minDistance = float.MaxValue;

            foreach (QuestLocation candidate in candidates)
            {
                if (candidate == null) continue;

                float distance = Vector3.Distance(current.Position, candidate.Position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = candidate;
                }
            }

            return nearest;
        }
    }
}
