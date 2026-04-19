using System;
using System.Collections;
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
        // ────────────────────────────────────────────────────────────────────
        // Singleton
        // ────────────────────────────────────────────────────────────────────

        public static NavigationService Instance { get; private set; }

        public static NavigationService EnsureInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            NavigationService existing = FindFirstObjectByType<NavigationService>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            GameObject go = new GameObject("NavigationService");
            return go.AddComponent<NavigationService>();
        }

        // ────────────────────────────────────────────────────────────────────
        // Inspector
        // ────────────────────────────────────────────────────────────────────

        [Header("Route Refresh")]
        [SerializeField] private float routeRefreshInterval = 0.2f;
        [SerializeField] private float routePublishDistanceThreshold = 4f;
        [SerializeField] private float objectiveMoveThreshold = 3f;
        [SerializeField] private float playerResolveRetryInterval = 0.5f;
        [SerializeField] private float roadGraphResolveRetryInterval = 1f;

        [Header("Reroute")]
        [SerializeField] private float offRouteDistanceThreshold = 14f;
        [SerializeField] private float staleRouteKeepDistance = 20f;
        [SerializeField] private float rerouteCooldown = 1.5f;
        [SerializeField] private float routeRetryInterval = 2.5f;
        [SerializeField] private float rerouteNotificationCooldown = 6f;
        [SerializeField] private float minAsyncRerouteInterval = 0.75f;
        [SerializeField] private float reroutePlayerMovementThreshold = 6f;
        [SerializeField] private float rerouteObjectiveMovementThreshold = 3f;
        [SerializeField] private float hardOffRouteDistanceThreshold = 26f;
        [SerializeField] private float initialAsyncRouteDelay = 0.02f;
        [SerializeField] private int pathSearchIterationsPerFrame = 220;
        [SerializeField] private int maxTransferStepsPerRequest = 1;
        [SerializeField] private float continuedRouteAttemptDelay = 0.08f;

        // ────────────────────────────────────────────────────────────────────
        // Public state & events
        // ────────────────────────────────────────────────────────────────────

        public NavigationObjective CurrentObjective { get; private set; }
        public RouteResult CurrentRoute { get; private set; } = RouteResult.Unavailable;

        public event Action<NavigationObjective> OnObjectiveChanged;
        public event Action<RouteResult> OnRouteChanged;
        public event Action OnNavigationCleared;

        // ────────────────────────────────────────────────────────────────────
        // Transfer distance steps for pathfinding
        // ────────────────────────────────────────────────────────────────────

        private static readonly float[] TransferDistances = { 6f, 10f, 16f, 24f, 32f };

        // ────────────────────────────────────────────────────────────────────
        // Internal state
        // ────────────────────────────────────────────────────────────────────

        // Cached references
        private Transform cachedPlayerTransform;
        private PlayerVehicleManager cachedVehicleManager;
        private RoadGraphBuilder cachedRoadGraphBuilder;

        // Route data
        private readonly List<Vector3> routePoints = new List<Vector3>();
        private readonly List<Vector3> renderPoints = new List<Vector3>();
        private readonly List<Vector3> lastGoodRoute = new List<Vector3>();

        // Route state
        private Vector3 lastObjectivePosition;
        private Vector3 lastPublishedStart;
        private int lastPublishedSegmentIndex = -1;
        private RouteKind lastPublishedKind = RouteKind.None;
        private int currentTransferStep;
        private int baselineTransferStep;
        private bool needsRouteBuild = true;

        // Timers
        private float routeRefreshTimer;
        private float nextRouteRetryTime;
        private float nextRerouteTime;
        private float nextNotificationTime;
        private float nextPlayerResolveTime;
        private float nextRoadGraphResolveTime;
        private int pendingTransferStartStep;

        // Async reroute
        private Coroutine asyncRouteCoroutine;
        private int consecutiveFailures;
        private float nextRouteLogTime;
        private Vector3 lastRequestedRouteStart;
        private Vector3 lastRequestedRouteObjective;
        private float nextAsyncRouteAllowedTime;
        private bool hasRequestedRoute;

        // Diagnostics
        private string lastPlayerSource = string.Empty;

        // ────────────────────────────────────────────────────────────────────
        // Constants
        // ────────────────────────────────────────────────────────────────────

        private const float ProjectionEpsilon = 0.15f;
        private const float FallbackMinDistance = 0.5f;
        private const float RouteLogCooldown = 5f;

        // ────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ────────────────────────────────────────────────────────────────────

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
            PlayerVehicleManager.ActiveVehicleChanged += HandleVehicleChanged;
        }

        private void OnDestroy()
        {
            PlayerVehicleManager.ActiveVehicleChanged -= HandleVehicleChanged;
            if (Instance == this) Instance = null;
        }

        // ────────────────────────────────────────────────────────────────────
        // Public API
        // ────────────────────────────────────────────────────────────────────

        public void SetObjective(NavigationObjective objective)
        {
            CurrentObjective = objective;
            ResetRouteState();
            PublishUnavailable();
            OnObjectiveChanged?.Invoke(objective);
            TryImmediateRouteBuild();
        }

        public void ClearObjective()
        {
            CurrentObjective = NavigationObjective.Empty;
            ResetRouteState();
            PublishUnavailable();
            OnNavigationCleared?.Invoke();
        }

        public void SetPlayerTransform(Transform player)
        {
            cachedPlayerTransform = player;
            nextPlayerResolveTime = 0f;
            if (CurrentObjective.IsValid)
                TryImmediateRouteBuild();
        }

        // ────────────────────────────────────────────────────────────────────
        // Update loop
        // ────────────────────────────────────────────────────────────────────

        private void Update()
        {
            if (!CurrentObjective.IsValid) return;

            routeRefreshTimer += Time.deltaTime;
            if (routeRefreshTimer < routeRefreshInterval) return;
            routeRefreshTimer = 0f;

            if (!ResolvePlayer(out Transform player)) return;

            Vector3 playerPos = player.position;
            Vector3 objectivePos = CurrentObjective.WorldPosition;

            // Check if objective moved significantly
            bool objectiveMoved = routePoints.Count > 0 &&
                (objectivePos - lastObjectivePosition).sqrMagnitude > objectiveMoveThreshold * objectiveMoveThreshold;

            // Need to build route?
            if (needsRouteBuild || objectiveMoved)
            {
                RequestAsyncRoute(playerPos, objectivePos, force: objectiveMoved || !hasRequestedRoute);
                return;
            }

            // No valid route? Retry on timer
            if (routePoints.Count < 2)
            {
                if (Time.time >= nextRouteRetryTime)
                    RequestAsyncRoute(playerPos, objectivePos, force: false);
                return;
            }

            // Check if player is on route
            ProjectOnRoute(routePoints, playerPos, out int segIdx, out Vector3 projected, out float distSqr);
            float offRouteSqr = offRouteDistanceThreshold * offRouteDistanceThreshold;

            if (segIdx < 0 || distSqr > offRouteSqr)
            {
                // Off route - reroute if cooldown allows
                if (Time.time >= nextRerouteTime)
                {
                    float hardOffRouteSqr = hardOffRouteDistanceThreshold * hardOffRouteDistanceThreshold;
                    bool force = segIdx < 0 || distSqr > hardOffRouteSqr;
                    RequestAsyncRoute(playerPos, objectivePos, force);
                }
                return;
            }

            // On route - publish projected view
            PublishProjected(playerPos, segIdx, projected, RouteKind.Graph, false);
        }

        // ────────────────────────────────────────────────────────────────────
        // Route building
        // ────────────────────────────────────────────────────────────────────

        private void TryImmediateRouteBuild()
        {
            if (!CurrentObjective.IsValid) return;
            if (!ResolvePlayer(out Transform player)) return;
            RequestAsyncRoute(player.position, CurrentObjective.WorldPosition, force: true);
        }

        private void BuildRoute(Vector3 start, Vector3 end)
        {
            needsRouteBuild = false;
            lastObjectivePosition = end;

            // Try road graph path
            if (TryResolveRoadGraph(out RoadGraph graph))
            {
                if (TryFindGraphRoute(graph, start, end))
                {
                    consecutiveFailures = 0;
                    return;
                }
            }

            // Graph route failed - try stale route
            if (TryUseStaleRoute(start))
            {
                consecutiveFailures = 0;
                return;
            }

            // All failed - use fallback straight line
            consecutiveFailures++;
            PublishFallback(start, end);
            ScheduleRetry();
            LogRouteIssue("Rota hesaplanamadi.", start, end);
            ShowRerouteNotification();
        }

        private bool TryFindGraphRoute(RoadGraph graph, Vector3 start, Vector3 end)
        {
            int startStep = Mathf.Clamp(Mathf.Max(currentTransferStep, baselineTransferStep), 0, TransferDistances.Length - 1);
            List<Vector3> path = null;
            int resolvedStep = -1;
            long budgetTicks = System.Diagnostics.Stopwatch.Frequency / 20; // ~50ms total budget
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();

            for (int step = startStep; step < TransferDistances.Length; step++)
            {
                path = RoadGraphPathfinder.FindPath(graph, start, end, TransferDistances[step]);
                if (path != null && path.Count >= 2)
                {
                    resolvedStep = step;
                    break;
                }

                // Don't burn more than ~50ms trying progressively wider transfers
                if (System.Diagnostics.Stopwatch.GetTimestamp() - t0 > budgetTicks)
                {
                    break;
                }
            }

            if (resolvedStep < 0 || path == null || path.Count < 2)
            {
                currentTransferStep = TransferDistances.Length - 1;
                nextRerouteTime = Time.time + rerouteCooldown;
                return false;
            }

            // Success
            routePoints.Clear();
            routePoints.AddRange(path);
            lastGoodRoute.Clear();
            lastGoodRoute.AddRange(path);
            baselineTransferStep = Mathf.Max(0, resolvedStep - 1);
            currentTransferStep = baselineTransferStep;
            nextRouteRetryTime = 0f;
            nextRerouteTime = Time.time + rerouteCooldown;

            ProjectOnRoute(routePoints, start, out int segIdx, out Vector3 projected, out _);
            PublishProjected(start, segIdx, projected, RouteKind.Graph, true);
            return true;
        }

        private bool TryUseStaleRoute(Vector3 playerPos)
        {
            if (lastGoodRoute.Count < 2) return false;

            ProjectOnRoute(lastGoodRoute, playerPos, out int segIdx, out Vector3 projected, out float distSqr);
            float keepDistSqr = staleRouteKeepDistance * staleRouteKeepDistance;

            if (segIdx < 0 || distSqr > keepDistSqr)
                return false;

            routePoints.Clear();
            routePoints.AddRange(lastGoodRoute);
            nextRouteRetryTime = Time.time + routeRetryInterval;
            nextRerouteTime = Time.time + rerouteCooldown;

            ShowRerouteNotification();
            PublishProjected(playerPos, segIdx, projected, RouteKind.StaleGraph, true);
            return true;
        }

        // ────────────────────────────────────────────────────────────────────
        // Route projection
        // ────────────────────────────────────────────────────────────────────

        private static void ProjectOnRoute(List<Vector3> points, Vector3 worldPos,
            out int bestSegment, out Vector3 bestProjection, out float bestDistSqr)
        {
            bestSegment = -1;
            bestProjection = worldPos;
            bestDistSqr = float.MaxValue;

            if (points == null || points.Count < 2) return;

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 proj = ProjectPointOnSegment(worldPos, points[i], points[i + 1]);
                float dSqr = (worldPos - proj).sqrMagnitude;
                if (dSqr < bestDistSqr)
                {
                    bestDistSqr = dSqr;
                    bestProjection = proj;
                    bestSegment = i;
                }
            }
        }

        private static Vector3 ProjectPointOnSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float lenSqr = ab.sqrMagnitude;
            if (lenSqr <= Mathf.Epsilon) return a;
            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lenSqr);
            return a + ab * t;
        }

        // ────────────────────────────────────────────────────────────────────
        // Route publishing
        // ────────────────────────────────────────────────────────────────────

        private void PublishProjected(Vector3 start, int segIdx, Vector3 projected, RouteKind kind, bool force)
        {
            if (segIdx < 0)
            {
                PublishUnavailable();
                return;
            }

            // Build renderable route: start -> projected -> remaining waypoints
            renderPoints.Clear();
            AddUniquePoint(renderPoints, start);
            AddUniquePoint(renderPoints, projected);
            for (int i = segIdx + 1; i < routePoints.Count; i++)
                AddUniquePoint(renderPoints, routePoints[i]);

            if (renderPoints.Count < 2)
            {
                PublishUnavailable();
                return;
            }

            // Throttle publishes
            float pubDistSqr = routePublishDistanceThreshold * routePublishDistanceThreshold;
            bool shouldPublish = force ||
                kind != lastPublishedKind ||
                segIdx != lastPublishedSegmentIndex ||
                (start - lastPublishedStart).sqrMagnitude >= pubDistSqr;

            if (!shouldPublish) return;

            lastPublishedStart = start;
            lastPublishedSegmentIndex = segIdx;
            lastPublishedKind = kind;

            CurrentRoute = new RouteResult(new List<Vector3>(renderPoints), kind);
            OnRouteChanged?.Invoke(CurrentRoute);
        }

        private void PublishFallback(Vector3 start, Vector3 end)
        {
            if ((end - start).sqrMagnitude <= FallbackMinDistance * FallbackMinDistance)
            {
                PublishUnavailable();
                return;
            }

            float pubDistSqr = routePublishDistanceThreshold * routePublishDistanceThreshold;
            bool shouldPublish = lastPublishedKind != RouteKind.Fallback ||
                (start - lastPublishedStart).sqrMagnitude >= pubDistSqr;

            if (!shouldPublish) return;

            renderPoints.Clear();
            AddUniquePoint(renderPoints, start);
            AddUniquePoint(renderPoints, end);

            if (renderPoints.Count < 2)
            {
                PublishUnavailable();
                return;
            }

            lastPublishedStart = start;
            lastPublishedSegmentIndex = -1;
            lastPublishedKind = RouteKind.Fallback;

            CurrentRoute = new RouteResult(new List<Vector3>(renderPoints), RouteKind.Fallback);
            OnRouteChanged?.Invoke(CurrentRoute);
        }

        private void PublishUnavailable()
        {
            if (lastPublishedKind == RouteKind.None && (CurrentRoute == null || !CurrentRoute.IsRenderable))
            {
                CurrentRoute = RouteResult.Unavailable;
                return;
            }

            renderPoints.Clear();
            CurrentRoute = RouteResult.Unavailable;
            lastPublishedKind = RouteKind.None;
            lastPublishedSegmentIndex = -1;
            OnRouteChanged?.Invoke(CurrentRoute);
        }

        // ────────────────────────────────────────────────────────────────────
        // State management
        // ────────────────────────────────────────────────────────────────────

        private void ResetRouteState()
        {
            if (asyncRouteCoroutine != null)
            {
                StopCoroutine(asyncRouteCoroutine);
                asyncRouteCoroutine = null;
            }
            consecutiveFailures = 0;
            routePoints.Clear();
            renderPoints.Clear();
            lastGoodRoute.Clear();
            needsRouteBuild = true;
            nextRouteRetryTime = 0f;
            nextRerouteTime = 0f;
            nextRouteLogTime = 0f;
            currentTransferStep = 0;
            baselineTransferStep = 0;
            pendingTransferStartStep = 0;
            lastPublishedSegmentIndex = -1;
            lastPublishedKind = RouteKind.None;
            lastPublishedStart = Vector3.zero;
            lastObjectivePosition = Vector3.zero;
            routeRefreshTimer = 0f;
            lastRequestedRouteStart = Vector3.zero;
            lastRequestedRouteObjective = Vector3.zero;
            nextAsyncRouteAllowedTime = 0f;
            hasRequestedRoute = false;
        }

        private void RequestAsyncRoute(Vector3 start, Vector3 end, bool force = false)
        {
            if (asyncRouteCoroutine != null)
            {
                return;
            }

            if (!force && !ShouldRequestAsyncRoute(start, end))
            {
                return;
            }

            hasRequestedRoute = true;
            lastRequestedRouteStart = start;
            lastRequestedRouteObjective = end;
            nextAsyncRouteAllowedTime = Time.time + Mathf.Max(0.1f, minAsyncRerouteInterval);
            asyncRouteCoroutine = StartCoroutine(BuildRouteAsync(start, end));
        }

        private bool ShouldRequestAsyncRoute(Vector3 start, Vector3 end)
        {
            if (!hasRequestedRoute)
            {
                return true;
            }

            if (needsRouteBuild)
            {
                return Time.time >= nextAsyncRouteAllowedTime;
            }

            if (Time.time >= nextRouteRetryTime)
            {
                return true;
            }

            if (Time.time < nextAsyncRouteAllowedTime)
            {
                return false;
            }

            float playerThresholdSqr = reroutePlayerMovementThreshold * reroutePlayerMovementThreshold;
            float objectiveThresholdSqr = rerouteObjectiveMovementThreshold * rerouteObjectiveMovementThreshold;

            bool playerMovedEnough = (start - lastRequestedRouteStart).sqrMagnitude >= playerThresholdSqr;
            bool objectiveMovedEnough = (end - lastRequestedRouteObjective).sqrMagnitude >= objectiveThresholdSqr;
            return playerMovedEnough || objectiveMovedEnough;
        }

        private IEnumerator BuildRouteAsync(Vector3 start, Vector3 end)
        {
            needsRouteBuild = false;
            lastObjectivePosition = end;

            bool found = false;

            if (initialAsyncRouteDelay > 0f)
            {
                yield return null;
            }

            if (TryResolveRoadGraph(out RoadGraph graph))
            {
                int startStep = Mathf.Clamp(
                    Mathf.Max(pendingTransferStartStep, baselineTransferStep),
                    0,
                    TransferDistances.Length - 1);
                int transferStepsTried = 0;

                for (int step = startStep; step < TransferDistances.Length; step++)
                {
                    // Re-read player position for freshness
                    if (ResolvePlayer(out Transform player) && CurrentObjective.IsValid)
                    {
                        start = player.position;
                        end = CurrentObjective.WorldPosition;
                    }

                    RoadGraphPathfinder.IncrementalPathSearchSession session =
                        RoadGraphPathfinder.StartIncrementalSearch(graph, start, end, TransferDistances[step]);

                    while (session != null && session.Status == RoadGraphPathfinder.IncrementalSearchStatus.Running)
                    {
                        session.Step(Mathf.Max(16, pathSearchIterationsPerFrame));
                        if (session.Status == RoadGraphPathfinder.IncrementalSearchStatus.Running)
                        {
                            yield return null;
                        }
                    }

                    List<Vector3> path = session != null && session.Status == RoadGraphPathfinder.IncrementalSearchStatus.Succeeded
                        ? session.Path
                        : null;
                    transferStepsTried++;
                    if (path != null && path.Count >= 2)
                    {
                        routePoints.Clear();
                        routePoints.AddRange(path);
                        lastGoodRoute.Clear();
                        lastGoodRoute.AddRange(path);
                        baselineTransferStep = Mathf.Max(0, step - 1);
                        currentTransferStep = baselineTransferStep;
                        pendingTransferStartStep = baselineTransferStep;
                        nextRouteRetryTime = 0f;
                        nextRerouteTime = Time.time + rerouteCooldown;
                        consecutiveFailures = 0;

                        ProjectOnRoute(routePoints, start, out int segIdx, out Vector3 projected, out _);
                        PublishProjected(start, segIdx, projected, RouteKind.Graph, true);
                        found = true;
                        break;
                    }

                    currentTransferStep = Mathf.Min(step + 1, TransferDistances.Length - 1);
                    pendingTransferStartStep = currentTransferStep;

                    if (transferStepsTried >= Mathf.Max(1, maxTransferStepsPerRequest) &&
                        step < TransferDistances.Length - 1)
                    {
                        break;
                    }
                }
            }

            if (!found)
            {
                if (currentTransferStep < TransferDistances.Length - 1)
                {
                    needsRouteBuild = true;
                    nextRouteRetryTime = Mathf.Min(nextRouteRetryTime <= 0f ? float.MaxValue : nextRouteRetryTime, Time.time + continuedRouteAttemptDelay);
                    nextAsyncRouteAllowedTime = Time.time + Mathf.Max(0.01f, continuedRouteAttemptDelay);

                    if (TryUseStaleRoute(start))
                    {
                        consecutiveFailures = 0;
                    }

                    asyncRouteCoroutine = null;
                    yield break;
                }

                currentTransferStep = TransferDistances.Length - 1;
                pendingTransferStartStep = currentTransferStep;

                if (TryUseStaleRoute(start))
                {
                    consecutiveFailures = 0;
                }
                else
                {
                    consecutiveFailures++;
                    PublishFallback(start, end);
                    float backoff = Mathf.Min(routeRetryInterval * Mathf.Pow(1.5f, consecutiveFailures), 10f);
                    nextRouteRetryTime = Mathf.Max(nextRouteRetryTime, Time.time + backoff);
                    nextRerouteTime = Time.time + Mathf.Min(rerouteCooldown * Mathf.Pow(1.5f, consecutiveFailures), 8f);
                    LogRouteIssue("Rota hesaplanamadi.", start, end);
                    ShowRerouteNotification();
                }
            }

            asyncRouteCoroutine = null;
        }

        private void ScheduleRetry()
        {
            nextRouteRetryTime = Mathf.Max(nextRouteRetryTime, Time.time + routeRetryInterval);
            nextRerouteTime = Time.time + rerouteCooldown;
        }

        // ────────────────────────────────────────────────────────────────────
        // Player resolution
        // ────────────────────────────────────────────────────────────────────

        private bool ResolvePlayer(out Transform player)
        {
            if (IsUsable(cachedPlayerTransform))
            {
                player = cachedPlayerTransform;
                return true;
            }

            if (Time.unscaledTime < nextPlayerResolveTime)
            {
                player = null;
                return false;
            }

            nextPlayerResolveTime = Time.unscaledTime + Mathf.Max(0.1f, playerResolveRetryInterval);

            // 1. QuestManager
            if (QuestManager.Instance != null && IsUsable(QuestManager.Instance.PlayerTransform))
            {
                cachedPlayerTransform = QuestManager.Instance.PlayerTransform;
                lastPlayerSource = "QuestManager";
                player = cachedPlayerTransform;
                return true;
            }

            // 2. PlayerVehicleManager
            PlayerVehicleManager vm = GetVehicleManager();
            if (vm != null && vm.ActiveVehicleController != null &&
                IsUsable(vm.ActiveVehicleController.transform))
            {
                cachedPlayerTransform = vm.ActiveVehicleController.transform;
                lastPlayerSource = "PlayerVehicleManager";
                player = cachedPlayerTransform;
                return true;
            }

            // 3. Player tag
            GameObject tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null && IsUsable(tagged.transform))
            {
                cachedPlayerTransform = tagged.transform;
                lastPlayerSource = "PlayerTag";
                player = cachedPlayerTransform;
                return true;
            }

            // 4. Any CarController
            CarController[] controllers = FindObjectsByType<CarController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] != null && IsUsable(controllers[i].transform))
                {
                    cachedPlayerTransform = controllers[i].transform;
                    lastPlayerSource = "CarController";
                    player = cachedPlayerTransform;
                    return true;
                }
            }

            cachedPlayerTransform = null;
            player = null;
            return false;
        }

        private void HandleVehicleChanged(CarController controller)
        {
            SetPlayerTransform(controller != null ? controller.transform : null);
        }

        private PlayerVehicleManager GetVehicleManager()
        {
            if (cachedVehicleManager == null)
                cachedVehicleManager = PlayerVehicleManager.Instance ?? FindFirstObjectByType<PlayerVehicleManager>();
            return cachedVehicleManager;
        }

        // ────────────────────────────────────────────────────────────────────
        // Road graph resolution
        // ────────────────────────────────────────────────────────────────────

        private bool TryResolveRoadGraph(out RoadGraph graph)
        {
            graph = null;

            if (cachedRoadGraphBuilder == null && Time.unscaledTime >= nextRoadGraphResolveTime)
            {
                cachedRoadGraphBuilder = FindFirstObjectByType<RoadGraphBuilder>();
                nextRoadGraphResolveTime = Time.unscaledTime + Mathf.Max(0.25f, roadGraphResolveRetryInterval);
            }

            if (cachedRoadGraphBuilder == null) return false;

            if (cachedRoadGraphBuilder.HasBuiltRoadGraph)
            {
                graph = cachedRoadGraphBuilder.RoadGraph;
                return graph != null && graph.roadSegments != null && graph.roadSegments.Count > 0;
            }

            // Trigger build if not pending
            if (!cachedRoadGraphBuilder.HasPendingBuild)
                cachedRoadGraphBuilder.BeginBuildWithDelay(0f);

            nextRouteRetryTime = Mathf.Max(nextRouteRetryTime, Time.time + routeRetryInterval);
            return false;
        }

        // ────────────────────────────────────────────────────────────────────
        // Notifications & logging
        // ────────────────────────────────────────────────────────────────────

        private void ShowRerouteNotification()
        {
            if (Time.time < nextNotificationTime) return;
            nextNotificationTime = Time.time + rerouteNotificationCooldown;

            NotificationQueue.Enqueue(
                "Navigasyon",
                "Rota yeniden hesaplanıyor",
                2f,
                NotificationPriority.Normal);
        }

        private void LogRouteIssue(string reason, Vector3 start, Vector3 end)
        {
            if (Time.time < nextRouteLogTime) return;
            nextRouteLogTime = Time.time + RouteLogCooldown;

            if (TryResolveRoadGraph(out RoadGraph graph) &&
                RoadGraphPathfinder.TryGetPathDiagnostics(graph, start, end, out var diag))
            {
                Debug.LogWarning(
                    $"[NavigationService] {reason} Start={start}, End={end}, " +
                    $"player={lastPlayerSource}, segments={diag.SegmentCount}, " +
                    $"connections={diag.ConnectionCount}, " +
                    $"startDist={diag.StartProjectionDistance:F2}, endDist={diag.EndProjectionDistance:F2}");
                return;
            }

            Debug.LogWarning($"[NavigationService] {reason} Start={start}, End={end}");
        }

        // ────────────────────────────────────────────────────────────────────
        // Utility
        // ────────────────────────────────────────────────────────────────────

        private static bool IsUsable(Transform t)
        {
            return t != null && t.gameObject != null && t.gameObject.activeInHierarchy;
        }

        private static void AddUniquePoint(List<Vector3> points, Vector3 point)
        {
            if (points.Count > 0)
            {
                Vector3 prev = points[points.Count - 1];
                if ((prev - point).sqrMagnitude <= ProjectionEpsilon * ProjectionEpsilon)
                {
                    points[points.Count - 1] = point;
                    return;
                }
            }
            points.Add(point);
        }
    }
}
