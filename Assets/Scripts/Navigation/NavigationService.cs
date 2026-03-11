using System;
using System.Collections.Generic;
using UnityEngine;
using DeliveryDriver.Quest;
using DeliveryDriver.UI;
using TrafficSystem;

namespace DeliveryDriver.Navigation
{
    public class NavigationService : MonoBehaviour
    {
        public static NavigationService Instance { get; private set; }

        [Header("Route Settings")]
        [SerializeField] private float routeRefreshInterval = 0.25f;
        [SerializeField] private float routeRefreshDistanceThreshold = 5f;

        public NavigationObjective CurrentObjective { get; private set; }
        public RouteResult CurrentRoute { get; private set; }

        public event Action<NavigationObjective> OnObjectiveChanged;
        public event Action<RouteResult> OnRouteChanged;
        public event Action OnNavigationCleared;

        private Transform cachedPlayerTransform;
        private RoadGraphBuilder cachedRoadGraphBuilder;
        private RoadGraph cachedRoadGraph;

        private readonly List<Vector3> cachedRoutePoints = new List<Vector3>();
        private Vector3 cachedRouteStart;
        private Vector3 cachedRouteEnd;
        private bool hasCachedRouteBounds;
        private bool cachedRouteUsedRoadGraph = true;
        private bool routeRecalcActive;
        private float nextRouteRetryTime;
        private float routeRefreshTimer;

        private static readonly float[] TransferDistances = { 10f, 25f, 50f, 100f };
        private int currentTransferStep;
        private int lastSuccessfulTransferStep;
        private readonly List<Vector3> lastGoodRoutePoints = new List<Vector3>();

        private const float RouteRetryInterval = 1f;

        public static NavigationService EnsureInstance()
        {
            if (Instance != null)
            {
                Instance.EnsureRuntimeVisualizers();
                return Instance;
            }

            NavigationService existing = UnityEngine.Object.FindFirstObjectByType<NavigationService>();
            if (existing != null)
            {
                Instance = existing;
                Instance.EnsureRuntimeVisualizers();
                return Instance;
            }

            GameObject serviceObject = new GameObject("NavigationService");
            NavigationService navigationService = serviceObject.AddComponent<NavigationService>();
            navigationService.EnsureRuntimeVisualizers();
            return navigationService;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            EnsureRuntimeVisualizers();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (!CurrentObjective.IsValid)
            {
                return;
            }

            routeRefreshTimer += Time.deltaTime;
            if (routeRefreshTimer < routeRefreshInterval)
            {
                return;
            }
            routeRefreshTimer = 0f;

            if (!TryResolvePlayerTransform(out Transform player))
            {
                return;
            }

            Vector3 start = player.position;
            Vector3 end = CurrentObjective.WorldPosition;
            bool shouldRetryFailedRoute = !cachedRouteUsedRoadGraph && Time.time >= nextRouteRetryTime;

            bool needsRebuild = !hasCachedRouteBounds ||
                                shouldRetryFailedRoute ||
                                Vector3.Distance(start, cachedRouteStart) > routeRefreshDistanceThreshold ||
                                Vector3.Distance(end, cachedRouteEnd) > routeRefreshDistanceThreshold;

            if (needsRebuild)
            {
                if (RebuildRoute(start, end))
                {
                    cachedRouteStart = start;
                    cachedRouteEnd = end;
                    hasCachedRouteBounds = true;

                    CurrentRoute = new RouteResult(new List<Vector3>(cachedRoutePoints), cachedRouteUsedRoadGraph);
                    OnRouteChanged?.Invoke(CurrentRoute);
                }
            }
        }

        public void SetObjective(NavigationObjective objective)
        {
            CurrentObjective = objective;
            InvalidateRoute();
            OnObjectiveChanged?.Invoke(objective);
            ForceRouteRebuild();
        }

        public void ClearObjective()
        {
            CurrentObjective = NavigationObjective.Empty;
            InvalidateRoute();
            CurrentRoute = null;
            OnNavigationCleared?.Invoke();
        }

        public void SetPlayerTransform(Transform player)
        {
            cachedPlayerTransform = player;
            if (CurrentObjective.IsValid)
            {
                ForceRouteRebuild();
            }
        }

        private void ForceRouteRebuild()
        {
            if (!CurrentObjective.IsValid)
            {
                return;
            }

            if (!TryResolvePlayerTransform(out Transform player))
            {
                return;
            }

            Vector3 start = player.position;
            Vector3 end = CurrentObjective.WorldPosition;

            if (RebuildRoute(start, end))
            {
                cachedRouteStart = start;
                cachedRouteEnd = end;
                hasCachedRouteBounds = true;

                CurrentRoute = new RouteResult(new List<Vector3>(cachedRoutePoints), cachedRouteUsedRoadGraph);
                OnRouteChanged?.Invoke(CurrentRoute);
            }
        }

        private void InvalidateRoute()
        {
            cachedRoutePoints.Clear();
            hasCachedRouteBounds = false;
            cachedRouteUsedRoadGraph = true;
            routeRecalcActive = false;
            nextRouteRetryTime = 0f;
            lastGoodRoutePoints.Clear();
            currentTransferStep = 0;
            lastSuccessfulTransferStep = 0;
            routeRefreshTimer = 0f;
        }

        private bool RebuildRoute(Vector3 start, Vector3 end)
        {
            if (!TryResolveRoadGraph(out RoadGraph graph))
            {
                SetFallbackRoute(start, end, false);
                return true;
            }

            int step = Mathf.Max(currentTransferStep, lastSuccessfulTransferStep);
            float transferDist = TransferDistances[Mathf.Min(step, TransferDistances.Length - 1)];
            List<Vector3> path = RoadGraphPathfinder.FindPath(graph, start, end, transferDist);

            if (path != null && path.Count >= 2)
            {
                cachedRoutePoints.Clear();
                cachedRoutePoints.AddRange(path);
                cachedRouteUsedRoadGraph = true;
                lastGoodRoutePoints.Clear();
                lastGoodRoutePoints.AddRange(path);
                lastSuccessfulTransferStep = step;
                currentTransferStep = 0;
                routeRecalcActive = false;
                return true;
            }

            // Failed - escalate transfer distance for next retry
            currentTransferStep = Mathf.Min(step + 1, TransferDistances.Length - 1);

            if (lastGoodRoutePoints.Count >= 2)
            {
                cachedRoutePoints.Clear();
                cachedRoutePoints.AddRange(lastGoodRoutePoints);
                cachedRoutePoints[0] = start;
                cachedRouteUsedRoadGraph = false;
                nextRouteRetryTime = Time.time + RouteRetryInterval;
                if (!routeRecalcActive)
                {
                    routeRecalcActive = true;
                    NotificationQueue.Enqueue(
                        "Navigasyon",
                        "Rota yeniden hesaplanıyor",
                        2f,
                        NotificationPriority.Normal);
                }
                return true;
            }

            SetFallbackRoute(start, end, true);
            return true;
        }

        private void SetFallbackRoute(Vector3 start, Vector3 end, bool showNotification)
        {
            cachedRoutePoints.Clear();
            cachedRoutePoints.Add(start);
            cachedRoutePoints.Add(end);
            cachedRouteUsedRoadGraph = false;
            nextRouteRetryTime = Time.time + RouteRetryInterval;

            if (showNotification && !routeRecalcActive)
            {
                routeRecalcActive = true;
                NotificationQueue.Enqueue(
                    "Navigasyon",
                    "Rota yeniden hesaplanıyor",
                    2f,
                    NotificationPriority.Normal);
            }
        }

        private bool TryResolvePlayerTransform(out Transform player)
        {
            if (cachedPlayerTransform == null)
            {
                if (QuestManager.Instance != null && QuestManager.Instance.PlayerTransform != null)
                {
                    cachedPlayerTransform = QuestManager.Instance.PlayerTransform;
                }
                else
                {
                    CarController car = FindFirstObjectByType<CarController>();
                    if (car != null)
                    {
                        cachedPlayerTransform = car.transform;
                    }
                    else
                    {
                        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                        if (playerObj != null)
                        {
                            cachedPlayerTransform = playerObj.transform;
                        }
                    }
                }
            }

            player = cachedPlayerTransform;
            return player != null;
        }

        private bool TryResolveRoadGraph(out RoadGraph graph)
        {
            if (cachedRoadGraph != null && cachedRoadGraph.roadSegments != null && cachedRoadGraph.roadSegments.Count > 0)
            {
                graph = cachedRoadGraph;
                return true;
            }

            if (cachedRoadGraphBuilder == null)
            {
                cachedRoadGraphBuilder = FindFirstObjectByType<RoadGraphBuilder>();
            }

            if (cachedRoadGraphBuilder == null)
            {
                graph = null;
                return false;
            }

            if (cachedRoadGraphBuilder.HasBuiltRoadGraph)
            {
                cachedRoadGraph = cachedRoadGraphBuilder.RoadGraph;
                graph = cachedRoadGraph;
                return graph != null && graph.roadSegments != null && graph.roadSegments.Count > 0;
            }

            if (!cachedRoadGraphBuilder.HasPendingBuild)
            {
                cachedRoadGraphBuilder.BeginBuildWithDelay(0f);
            }

            graph = null;
            return false;
        }

        private void EnsureRuntimeVisualizers()
        {
            if (GetComponent<ObjectiveMarker3D>() == null)
            {
                gameObject.AddComponent<ObjectiveMarker3D>();
            }

            if (GetComponent<WorldRouteRenderer>() == null)
            {
                gameObject.AddComponent<WorldRouteRenderer>();
            }

            if (GetComponent<EdgeIndicator>() == null)
            {
                gameObject.AddComponent<EdgeIndicator>();
            }
        }
    }
}
