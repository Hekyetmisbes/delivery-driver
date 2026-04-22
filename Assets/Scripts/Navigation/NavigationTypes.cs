using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDriver.Navigation
{
    public enum ObjectiveType { None, Pickup, Delivery }
    public enum RouteKind { None, Graph, StaleGraph, Fallback }

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
        public static readonly RouteResult Unavailable = new RouteResult(null, RouteKind.None, false);

        public IReadOnlyList<Vector3> Points { get; private set; }
        public RouteKind Kind { get; private set; }
        public float TotalDistance { get; private set; }
        public bool IsRenderable { get; private set; }

        public bool IsValid => Points != null && Points.Count >= 2;
        public bool UsedRoadGraph => IsGraphRoute;
        public bool IsGraphRoute => Kind == RouteKind.Graph || Kind == RouteKind.StaleGraph;
        public bool IsFallback => Kind == RouteKind.Fallback;
        public bool IsStale => Kind == RouteKind.StaleGraph;

        public RouteResult(List<Vector3> points, RouteKind kind, bool isRenderable = true)
        {
            Update(points, kind, isRenderable);
        }

        internal void Update(List<Vector3> points, RouteKind kind, bool isRenderable = true)
        {
            Points = points;
            Kind = kind;
            IsRenderable = isRenderable && points != null && points.Count >= 2;

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
