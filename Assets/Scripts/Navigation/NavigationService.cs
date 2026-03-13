using System;
using System.Collections.Generic;
using UnityEngine;
using DeliveryDriver.Company;
using DeliveryDriver.Quest;
using DeliveryDriver.UI;
using TrafficSystem;

namespace DeliveryDriver.Navigation
{
    public class NavigationService : MonoBehaviour
    {
        private readonly struct RouteProjection
        {
            public RouteProjection(int segmentIndex, Vector3 projectedPoint, float distanceSqr)
            {
                SegmentIndex = segmentIndex;
                ProjectedPoint = projectedPoint;
                DistanceSqr = distanceSqr;
            }

            public int SegmentIndex { get; }
            public Vector3 ProjectedPoint { get; }
            public float DistanceSqr { get; }
            public bool IsValid => SegmentIndex >= 0;
        }

        private readonly struct RouteIssueDiagnostics
        {
            public RouteIssueDiagnostics(
                RoadGraphPathfinder.PathSearchDiagnostics pathDiagnostics,
                int startComponent,
                int endComponent,
                bool sameComponent,
                string playerSource)
            {
                PathDiagnostics = pathDiagnostics;
                StartComponent = startComponent;
                EndComponent = endComponent;
                SameComponent = sameComponent;
                PlayerSource = playerSource;
            }

            public RoadGraphPathfinder.PathSearchDiagnostics PathDiagnostics { get; }
            public int StartComponent { get; }
            public int EndComponent { get; }
            public bool SameComponent { get; }
            public string PlayerSource { get; }
        }

        public static NavigationService Instance { get; private set; }

        [Header("Route Refresh")]
        [SerializeField] private float routeRefreshInterval = 0.2f;
        [SerializeField] private float routePublishDistanceThreshold = 4f;
        [SerializeField] private float objectiveMoveThreshold = 3f;
        [SerializeField] private float playerResolveRetryInterval = 0.5f;
        [SerializeField] private float roadGraphResolveRetryInterval = 1f;

        [Header("Reroute")]
        [SerializeField] private float offRouteDistanceThreshold = 14f;
        [SerializeField] private float staleRouteKeepDistance = 20f;
        [SerializeField] private float rerouteCooldown = 1f;
        [SerializeField] private float routeRetryInterval = 1.5f;
        [SerializeField] private float rerouteNotificationCooldown = 6f;

        public NavigationObjective CurrentObjective { get; private set; }
        public RouteResult CurrentRoute { get; private set; } = RouteResult.Unavailable;

        public event Action<NavigationObjective> OnObjectiveChanged;
        public event Action<RouteResult> OnRouteChanged;
        public event Action OnNavigationCleared;

        private Transform cachedPlayerTransform;
        private PlayerVehicleManager cachedVehicleManager;
        private RoadGraphBuilder cachedRoadGraphBuilder;
        private RoadGraph cachedRoadGraph;

        private readonly List<Vector3> routePoints = new List<Vector3>();
        private readonly List<Vector3> renderRoutePoints = new List<Vector3>();
        private readonly List<Vector3> lastGoodRoutePoints = new List<Vector3>();

        private Vector3 lastRouteObjectivePosition;
        private Vector3 lastPublishedStart;
        private int lastPublishedSegmentIndex = -1;
        private RouteKind lastPublishedKind = RouteKind.None;
        private float routeRefreshTimer;
        private float nextRouteRetryTime;
        private float nextAllowedRerouteTime;
        private float nextNotificationTime;
        private float nextRouteLogTime;
        private float nextPlayerResolveTime;
        private float nextRoadGraphResolveTime;
        private bool routeBuildPending = true;
        private string lastResolvedPlayerSource = string.Empty;

        private static readonly float[] TransferDistances = { 6f, 10f, 16f, 24f };
        private const float ProjectionEpsilon = 0.15f;
        private const float RouteLogCooldown = 2f;

        private int currentTransferStep;
        private int baselineTransferStep;

        public static NavigationService EnsureInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            NavigationService existing = UnityEngine.Object.FindFirstObjectByType<NavigationService>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            GameObject serviceObject = new GameObject("NavigationService");
            return serviceObject.AddComponent<NavigationService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            PlayerVehicleManager.ActiveVehicleChanged += HandleActiveVehicleChanged;
        }

        private void OnDestroy()
        {
            PlayerVehicleManager.ActiveVehicleChanged -= HandleActiveVehicleChanged;

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
            bool objectiveMoved = routePoints.Count > 0 &&
                                  (end - lastRouteObjectivePosition).sqrMagnitude > (objectiveMoveThreshold * objectiveMoveThreshold);

            if (routeBuildPending || objectiveMoved)
            {
                TryRebuildRoute(start, end, true);
                return;
            }

            if (routePoints.Count < 2)
            {
                if (Time.time >= nextRouteRetryTime)
                {
                    TryRebuildRoute(start, end, true);
                }

                return;
            }

            RouteProjection projection = ProjectOntoRoute(routePoints, start);
            float offRouteThresholdSqr = offRouteDistanceThreshold * offRouteDistanceThreshold;
            if (!projection.IsValid || projection.DistanceSqr > offRouteThresholdSqr)
            {
                if (Time.time >= nextAllowedRerouteTime)
                {
                    TryRebuildRoute(start, end, true);
                }
                else if (CurrentRoute != null && CurrentRoute.IsRenderable)
                {
                    PublishUnavailableRoute();
                }

                return;
            }

            PublishProjectedRoute(start, projection, RouteKind.Graph, false);
        }

        public void SetObjective(NavigationObjective objective)
        {
            CurrentObjective = objective;
            InvalidateRoute();
            PublishUnavailableRoute();
            OnObjectiveChanged?.Invoke(objective);
            ForceRouteRebuild();
        }

        public void ClearObjective()
        {
            CurrentObjective = NavigationObjective.Empty;
            InvalidateRoute();
            PublishUnavailableRoute();
            OnNavigationCleared?.Invoke();
        }

        public void SetPlayerTransform(Transform player)
        {
            cachedPlayerTransform = player;
            nextPlayerResolveTime = 0f;
            if (CurrentObjective.IsValid)
            {
                ForceRouteRebuild();
            }
        }

        private void ForceRouteRebuild()
        {
            if (!CurrentObjective.IsValid || !TryResolvePlayerTransform(out Transform player))
            {
                return;
            }

            TryRebuildRoute(player.position, CurrentObjective.WorldPosition, true);
        }

        private void InvalidateRoute()
        {
            routePoints.Clear();
            renderRoutePoints.Clear();
            lastGoodRoutePoints.Clear();
            routeBuildPending = true;
            nextRouteRetryTime = 0f;
            nextAllowedRerouteTime = 0f;
            nextRouteLogTime = 0f;
            currentTransferStep = 0;
            baselineTransferStep = 0;
            lastPublishedSegmentIndex = -1;
            lastPublishedKind = RouteKind.None;
            lastPublishedStart = Vector3.zero;
            lastRouteObjectivePosition = Vector3.zero;
            routeRefreshTimer = 0f;
        }

        private void TryRebuildRoute(Vector3 start, Vector3 end, bool allowNotification)
        {
            routeBuildPending = false;
            lastRouteObjectivePosition = end;

            if (!TryResolveRoadGraph(out RoadGraph graph))
            {
                HandleRouteBuildFailure(start, allowNotification);
                return;
            }

            int step = Mathf.Clamp(Mathf.Max(currentTransferStep, baselineTransferStep), 0, TransferDistances.Length - 1);
            float transferDistance = TransferDistances[step];
            List<Vector3> path = RoadGraphPathfinder.FindPath(graph, start, end, transferDistance);

            if (path != null && path.Count >= 2)
            {
                routePoints.Clear();
                routePoints.AddRange(path);
                lastGoodRoutePoints.Clear();
                lastGoodRoutePoints.AddRange(path);
                baselineTransferStep = Mathf.Max(0, step - 1);
                currentTransferStep = baselineTransferStep;
                nextRouteRetryTime = 0f;
                nextAllowedRerouteTime = Time.time + rerouteCooldown;
                PublishProjectedRoute(start, ProjectOntoRoute(routePoints, start), RouteKind.Graph, true);
                return;
            }

            // Failed - escalate transfer distance for next retry
            currentTransferStep = Mathf.Min(step + 1, TransferDistances.Length - 1);
            nextAllowedRerouteTime = Time.time + rerouteCooldown;
            nextRouteRetryTime = Time.time + routeRetryInterval;

            if (lastGoodRoutePoints.Count >= 2)
            {
                RouteProjection staleProjection = ProjectOntoRoute(lastGoodRoutePoints, start);
                float staleKeepDistanceSqr = staleRouteKeepDistance * staleRouteKeepDistance;
                if (!staleProjection.IsValid || staleProjection.DistanceSqr > staleKeepDistanceSqr)
                {
                    HandleUnavailableRoute(start, end, allowNotification, "Road-graph route could not be recalculated.");
                    return;
                }

                routePoints.Clear();
                routePoints.AddRange(lastGoodRoutePoints);
                nextRouteRetryTime = Time.time + routeRetryInterval;
                if (allowNotification && Time.time >= nextNotificationTime)
                {
                    nextNotificationTime = Time.time + rerouteNotificationCooldown;
                    NotificationQueue.Enqueue(
                        "Navigasyon",
                        "Rota yeniden hesaplanıyor",
                        2f,
                        NotificationPriority.Normal);
                }
                PublishProjectedRoute(start, staleProjection, RouteKind.StaleGraph, true);
                return;
            }

            HandleUnavailableRoute(start, end, allowNotification, "Road-graph pathfinder could not produce a valid route.");
            return;
        }

        private void HandleRouteBuildFailure(Vector3 start, bool allowNotification)
        {
            if (lastGoodRoutePoints.Count >= 2)
            {
                RouteProjection staleProjection = ProjectOntoRoute(lastGoodRoutePoints, start);
                float staleKeepDistanceSqr = staleRouteKeepDistance * staleRouteKeepDistance;
                if (!staleProjection.IsValid || staleProjection.DistanceSqr > staleKeepDistanceSqr)
                {
                    HandleUnavailableRoute(start, CurrentObjective.WorldPosition, allowNotification, "Road graph is unavailable or not ready.");
                    return;
                }

                routePoints.Clear();
                routePoints.AddRange(lastGoodRoutePoints);

                if (allowNotification && Time.time >= nextNotificationTime)
                {
                    nextNotificationTime = Time.time + rerouteNotificationCooldown;
                    NotificationQueue.Enqueue(
                        "Navigasyon",
                        "Rota yeniden hesaplaniyor",
                        2f,
                        NotificationPriority.Normal);
                }

                PublishProjectedRoute(start, staleProjection, RouteKind.StaleGraph, true);
                return;
            }

            HandleUnavailableRoute(start, CurrentObjective.WorldPosition, allowNotification, "Road graph is unavailable or not ready.");
        }

        private void HandleUnavailableRoute(Vector3 start, Vector3 end, bool showNotification, string reason)
        {
            routePoints.Clear();
            renderRoutePoints.Clear();
            PublishUnavailableRoute();
            nextRouteRetryTime = Mathf.Max(nextRouteRetryTime, Time.time + routeRetryInterval);
            TryLogRouteIssue(reason, start, end);

            if (showNotification && Time.time >= nextNotificationTime)
            {
                nextNotificationTime = Time.time + rerouteNotificationCooldown;
                NotificationQueue.Enqueue(
                    "Navigasyon",
                    "Rota yeniden hesaplanıyor",
                    2f,
                    NotificationPriority.Normal);
            }
        }

        private void TryLogRouteIssue(string reason, Vector3 start, Vector3 end)
        {
            if (Time.time < nextRouteLogTime)
            {
                return;
            }

            nextRouteLogTime = Time.time + RouteLogCooldown;
            if (TryBuildRouteIssueDiagnostics(start, end, out RouteIssueDiagnostics diagnostics))
            {
                RoadSegment startSegment = diagnostics.PathDiagnostics.StartSegment;
                RoadSegment endSegment = diagnostics.PathDiagnostics.EndSegment;
                Debug.LogWarning(
                    $"[NavigationService] {reason} Start={start}, Objective={end}, " +
                    $"playerSource={diagnostics.PlayerSource}, graphSegments={diagnostics.PathDiagnostics.SegmentCount}, explicitConnections={diagnostics.PathDiagnostics.ConnectionCount}, " +
                    $"startSegment={FormatSegmentLabel(startSegment, diagnostics.PathDiagnostics.StartWaypointIndex)}, startProjectionDistance={diagnostics.PathDiagnostics.StartProjectionDistance:F2}, " +
                    $"endSegment={FormatSegmentLabel(endSegment, diagnostics.PathDiagnostics.EndWaypointIndex)}, endProjectionDistance={diagnostics.PathDiagnostics.EndProjectionDistance:F2}, " +
                    $"sameComponent={diagnostics.SameComponent}, startComponent={diagnostics.StartComponent}, endComponent={diagnostics.EndComponent}.");
                return;
            }

            Debug.LogWarning($"[NavigationService] {reason} Start={start}, Objective={end}.");
        }

        private void PublishProjectedRoute(Vector3 start, RouteProjection projection, RouteKind kind, bool forcePublish)
        {
            if (!projection.IsValid)
            {
                PublishUnavailableRoute();
                return;
            }

            BuildRenderableRoute(start, projection);
            if (renderRoutePoints.Count < 2)
            {
                PublishUnavailableRoute();
                return;
            }

            float publishDistanceThresholdSqr = routePublishDistanceThreshold * routePublishDistanceThreshold;
            bool shouldPublish = forcePublish ||
                                 kind != lastPublishedKind ||
                                 projection.SegmentIndex != lastPublishedSegmentIndex ||
                                 (start - lastPublishedStart).sqrMagnitude >= publishDistanceThresholdSqr;

            if (!shouldPublish)
            {
                return;
            }

            lastPublishedStart = start;
            lastPublishedSegmentIndex = projection.SegmentIndex;
            lastPublishedKind = kind;

            CurrentRoute = new RouteResult(new List<Vector3>(renderRoutePoints), kind);
            OnRouteChanged?.Invoke(CurrentRoute);
        }

        private void PublishUnavailableRoute()
        {
            if (lastPublishedKind == RouteKind.None && (CurrentRoute == null || !CurrentRoute.IsRenderable))
            {
                CurrentRoute = RouteResult.Unavailable;
                return;
            }

            renderRoutePoints.Clear();
            CurrentRoute = RouteResult.Unavailable;
            lastPublishedKind = RouteKind.None;
            lastPublishedSegmentIndex = -1;
            OnRouteChanged?.Invoke(CurrentRoute);
        }

        private void BuildRenderableRoute(Vector3 start, RouteProjection projection)
        {
            renderRoutePoints.Clear();
            AddUniquePoint(renderRoutePoints, start);
            AddUniquePoint(renderRoutePoints, projection.ProjectedPoint);

            for (int i = projection.SegmentIndex + 1; i < routePoints.Count; i++)
            {
                AddUniquePoint(renderRoutePoints, routePoints[i]);
            }
        }

        private static void AddUniquePoint(List<Vector3> points, Vector3 point)
        {
            if (points.Count > 0)
            {
                Vector3 previous = points[points.Count - 1];
                if ((previous - point).sqrMagnitude <= ProjectionEpsilon * ProjectionEpsilon)
                {
                    points[points.Count - 1] = point;
                    return;
                }
            }

            points.Add(point);
        }

        private static RouteProjection ProjectOntoRoute(List<Vector3> points, Vector3 worldPosition)
        {
            if (points == null || points.Count < 2)
            {
                return new RouteProjection(-1, worldPosition, float.MaxValue);
            }

            int bestSegmentIndex = -1;
            Vector3 bestProjection = worldPosition;
            float bestDistanceSqr = float.MaxValue;

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 projectedPoint = ProjectPointOnSegment(worldPosition, points[i], points[i + 1]);
                float distanceSqr = (worldPosition - projectedPoint).sqrMagnitude;
                if (distanceSqr < bestDistanceSqr)
                {
                    bestDistanceSqr = distanceSqr;
                    bestProjection = projectedPoint;
                    bestSegmentIndex = i;
                }
            }

            return new RouteProjection(bestSegmentIndex, bestProjection, bestDistanceSqr);
        }

        private static Vector3 ProjectPointOnSegment(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd)
        {
            Vector3 segment = segmentEnd - segmentStart;
            float lengthSqr = segment.sqrMagnitude;
            if (lengthSqr <= Mathf.Epsilon)
            {
                return segmentStart;
            }

            float t = Vector3.Dot(point - segmentStart, segment) / lengthSqr;
            t = Mathf.Clamp01(t);
            return segmentStart + (segment * t);
        }

        private bool TryResolvePlayerTransform(out Transform player)
        {
            if (IsUsablePlayerTransform(cachedPlayerTransform))
            {
                player = cachedPlayerTransform;
                return player != null;
            }

            if (Time.unscaledTime < nextPlayerResolveTime)
            {
                player = null;
                return false;
            }

            nextPlayerResolveTime = Time.unscaledTime + Mathf.Max(0.1f, playerResolveRetryInterval);
            if (TryResolveAuthoritativePlayerTransform(out Transform resolvedPlayerTransform, out string playerSource))
            {
                cachedPlayerTransform = resolvedPlayerTransform;
                lastResolvedPlayerSource = playerSource;
                player = cachedPlayerTransform;
                return player != null;
            }

            cachedPlayerTransform = null;
            player = null;
            return false;
        }

        private bool TryResolveRoadGraph(out RoadGraph graph)
        {
            if (cachedRoadGraphBuilder == null && Time.unscaledTime >= nextRoadGraphResolveTime)
            {
                cachedRoadGraphBuilder = FindFirstObjectByType<RoadGraphBuilder>();
                nextRoadGraphResolveTime = Time.unscaledTime + Mathf.Max(0.25f, roadGraphResolveRetryInterval);
            }

            if (cachedRoadGraphBuilder == null)
            {
                cachedRoadGraph = null;
                graph = null;
                return false;
            }

            if (cachedRoadGraphBuilder.HasBuiltRoadGraph)
            {
                cachedRoadGraph = cachedRoadGraphBuilder.RoadGraph;
                graph = cachedRoadGraph;
                return graph != null && graph.roadSegments != null && graph.roadSegments.Count > 0;
            }

            cachedRoadGraph = null;
            if (!cachedRoadGraphBuilder.HasPendingBuild)
            {
                cachedRoadGraphBuilder.BeginBuildWithDelay(0f);
            }

            graph = null;
            nextRouteRetryTime = Mathf.Max(nextRouteRetryTime, Time.time + routeRetryInterval);
            return false;
        }

        private bool TryResolveAuthoritativePlayerTransform(out Transform player, out string source)
        {
            player = null;
            source = string.Empty;

            if (QuestManager.Instance != null && IsUsablePlayerTransform(QuestManager.Instance.PlayerTransform))
            {
                player = QuestManager.Instance.PlayerTransform;
                source = "QuestManager.PlayerTransform";
                return true;
            }

            PlayerVehicleManager vehicleManager = TryGetVehicleManager();
            if (vehicleManager != null &&
                vehicleManager.ActiveVehicleController != null &&
                IsUsablePlayerTransform(vehicleManager.ActiveVehicleController.transform))
            {
                player = vehicleManager.ActiveVehicleController.transform;
                source = "PlayerVehicleManager.ActiveVehicleController";
                return true;
            }

            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null && IsUsablePlayerTransform(taggedPlayer.transform))
            {
                player = taggedPlayer.transform;
                source = "PlayerTag";
                return true;
            }

            CarController[] controllers = FindObjectsByType<CarController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < controllers.Length; i++)
            {
                CarController controller = controllers[i];
                if (controller == null || !IsUsablePlayerTransform(controller.transform))
                {
                    continue;
                }

                player = controller.transform;
                source = "SceneCarController";
                return true;
            }

            return false;
        }

        private PlayerVehicleManager TryGetVehicleManager()
        {
            if (cachedVehicleManager == null)
            {
                cachedVehicleManager = PlayerVehicleManager.Instance ?? FindFirstObjectByType<PlayerVehicleManager>();
            }

            return cachedVehicleManager;
        }

        private void HandleActiveVehicleChanged(CarController controller)
        {
            SetPlayerTransform(controller != null ? controller.transform : null);
        }

        private static bool IsUsablePlayerTransform(Transform candidate)
        {
            return candidate != null &&
                   candidate.gameObject != null &&
                   candidate.gameObject.activeInHierarchy;
        }

        private bool TryBuildRouteIssueDiagnostics(Vector3 start, Vector3 end, out RouteIssueDiagnostics diagnostics)
        {
            diagnostics = default;
            if (!TryResolveRoadGraph(out RoadGraph graph) ||
                !RoadGraphPathfinder.TryGetPathDiagnostics(graph, start, end, out RoadGraphPathfinder.PathSearchDiagnostics pathDiagnostics))
            {
                return false;
            }

            BuildSegmentConnectivityMap(graph, out Dictionary<int, int> componentBySegmentId, out int _);
            int startComponent = ResolveSegmentComponent(componentBySegmentId, pathDiagnostics.StartSegment);
            int endComponent = ResolveSegmentComponent(componentBySegmentId, pathDiagnostics.EndSegment);
            diagnostics = new RouteIssueDiagnostics(
                pathDiagnostics,
                startComponent,
                endComponent,
                startComponent >= 0 && startComponent == endComponent,
                string.IsNullOrEmpty(lastResolvedPlayerSource) ? "unknown" : lastResolvedPlayerSource);
            return true;
        }

        private static void BuildSegmentConnectivityMap(
            RoadGraph graph,
            out Dictionary<int, int> componentBySegmentId,
            out int componentCount)
        {
            componentBySegmentId = new Dictionary<int, int>();
            componentCount = 0;
            if (graph == null || graph.roadSegments == null)
            {
                return;
            }

            Dictionary<int, List<int>> adjacency = new Dictionary<int, List<int>>(graph.roadSegments.Count);
            for (int i = 0; i < graph.roadSegments.Count; i++)
            {
                RoadSegment segment = graph.roadSegments[i];
                if (segment == null)
                {
                    continue;
                }

                if (!adjacency.ContainsKey(segment.id))
                {
                    adjacency[segment.id] = new List<int>();
                }

                if (segment.connections == null)
                {
                    continue;
                }

                for (int connectionIndex = 0; connectionIndex < segment.connections.Count; connectionIndex++)
                {
                    RoadConnection connection = segment.connections[connectionIndex];
                    if (connection == null || connection.toSegment == null)
                    {
                        continue;
                    }

                    AddAdjacentSegment(adjacency, segment.id, connection.toSegment.id);
                    AddAdjacentSegment(adjacency, connection.toSegment.id, segment.id);
                }
            }

            foreach (KeyValuePair<int, List<int>> pair in adjacency)
            {
                if (componentBySegmentId.ContainsKey(pair.Key))
                {
                    continue;
                }

                Queue<int> pending = new Queue<int>();
                pending.Enqueue(pair.Key);
                componentBySegmentId[pair.Key] = componentCount;

                while (pending.Count > 0)
                {
                    int current = pending.Dequeue();
                    if (!adjacency.TryGetValue(current, out List<int> neighbors))
                    {
                        continue;
                    }

                    for (int neighborIndex = 0; neighborIndex < neighbors.Count; neighborIndex++)
                    {
                        int neighbor = neighbors[neighborIndex];
                        if (componentBySegmentId.ContainsKey(neighbor))
                        {
                            continue;
                        }

                        componentBySegmentId[neighbor] = componentCount;
                        pending.Enqueue(neighbor);
                    }
                }

                componentCount++;
            }
        }

        private static void AddAdjacentSegment(Dictionary<int, List<int>> adjacency, int segmentId, int neighborSegmentId)
        {
            if (!adjacency.TryGetValue(segmentId, out List<int> neighbors))
            {
                neighbors = new List<int>();
                adjacency[segmentId] = neighbors;
            }

            if (!neighbors.Contains(neighborSegmentId))
            {
                neighbors.Add(neighborSegmentId);
            }
        }

        private static int ResolveSegmentComponent(Dictionary<int, int> componentBySegmentId, RoadSegment segment)
        {
            if (segment == null)
            {
                return -1;
            }

            return componentBySegmentId.TryGetValue(segment.id, out int componentId)
                ? componentId
                : -1;
        }

        private static string FormatSegmentLabel(RoadSegment segment, int waypointIndex)
        {
            if (segment == null)
            {
                return "null";
            }

            string segmentName = string.IsNullOrWhiteSpace(segment.name) ? "unnamed" : segment.name;
            return $"{segment.id}:{segmentName}@{waypointIndex}";
        }
    }
}
