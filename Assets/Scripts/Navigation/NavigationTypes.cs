using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDriver.Navigation
{
    public enum ObjectiveType { None, Pickup, Delivery }

    public readonly struct NavigationObjective
    {
        public readonly ObjectiveType Type;
        public readonly Vector3 WorldPosition;
        public readonly int DeliveryIndex;
        public readonly int TotalDeliveries;

        public bool IsValid => Type != ObjectiveType.None;

        public NavigationObjective(ObjectiveType type, Vector3 worldPosition, int deliveryIndex = 0, int totalDeliveries = 1)
        {
            Type = type;
            WorldPosition = worldPosition;
            DeliveryIndex = deliveryIndex;
            TotalDeliveries = totalDeliveries;
        }

        public static readonly NavigationObjective Empty = new NavigationObjective(ObjectiveType.None, Vector3.zero);
    }

    public class RouteResult
    {
        public readonly IReadOnlyList<Vector3> Points;
        public readonly bool UsedRoadGraph;
        public readonly float TotalDistance;

        public bool IsValid => Points != null && Points.Count >= 2;

        public RouteResult(List<Vector3> points, bool usedRoadGraph)
        {
            Points = points;
            UsedRoadGraph = usedRoadGraph;

            float dist = 0f;
            if (points != null)
            {
                for (int i = 1; i < points.Count; i++)
                {
                    dist += Vector3.Distance(points[i - 1], points[i]);
                }
            }
            TotalDistance = dist;
        }
    }
}
