using System;
using System.Collections.Generic;
using UnityEngine;
using DeliveryDriver.Quest;
using DeliveryDriver.City;
using DeliveryDriver.Navigation;
using DeliveryDriver.UI;
using TrafficSystem;

/// <summary>
/// Manages delivery missions - spawning boxes and delivery points
/// Integrates with Quest system to show missions in UI
/// </summary>
public class DeliveryManager : MonoBehaviour
{
    // SpawnReachabilityTransferDistance and MaxSpawnReachabilityPathChecks removed:
    // Full A* reachability checks during spawn selection caused multi-second freezes.
    // The BFS component connectivity check (TryCheckSpawnConnectivity) is sufficient.

    [Header("Prefabs")]
    [SerializeField] private GameObject boxPrefab;
    [SerializeField] private GameObject deliveryIndicatorPrefab;
    [SerializeField] private bool showFloatingObjectiveMarkers = false;

    [Header("Spawn Settings")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float spawnHeight = 0.5f;
    [SerializeField] private float minDistanceBetweenPoints = 20f;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private LayerMask roadSurfaceMask;
    [SerializeField] private RoadGraphBuilder roadGraphBuilder;
    [SerializeField] private bool useRoadGraphSpawnPoints = true;
    [SerializeField] private bool spawnOnBuildingFrontSidewalk = true;
    [SerializeField] private float buildingSearchRadius = 30f;
    [SerializeField] private float buildingFrontOffset = 2.2f;
    [SerializeField] private float sidewalkToRoadMaxDistance = 14f;
    [SerializeField] private float sidewalkValidationBuildingRadius = 6f;
    [SerializeField] private float minPickupSpawnDistanceFromPlayer = 18f;
    [SerializeField] private string[] buildingNameKeywords =
    {
        "building", "house", "shop", "market", "restaurant", "factory",
        "stadium", "residential", "apartment", "office", "hospital", "school"
    };

    [Header("Delivery Settings")]
    [SerializeField] private float deliveryRadius = 5f;
    [SerializeField] private Vector2 deliveryParkingSize = new Vector2(6.5f, 9f);
    [SerializeField] private float deliveryParkingCompletionTolerance = 0.75f;
    [SerializeField] private float deliveryQuestTriggerRadius = 0.35f;
    [SerializeField] private Color deliveryParkingFillColor = new Color(0.1f, 0.85f, 1f, 0.2f);
    [SerializeField] private Color deliveryParkingLineColor = new Color(0.35f, 1f, 0.9f, 0.9f);
    [SerializeField] private float deliveryParkingGroundOffset = 0.04f;
    [SerializeField] private float deliveryParkingLineWidth = 0.08f;
    [SerializeField] private float deliveryParkingPulseSpeed = 2.2f;
    [SerializeField] private float deliveryParkingPulseScale = 0.035f;
    [SerializeField] private bool autoGenerateSpawnPoints = true;
    [SerializeField] private int numberOfAutoSpawnPoints = 10;
    [SerializeField] private Vector2 spawnAreaMin = new Vector2(-50, -50);
    [SerializeField] private Vector2 spawnAreaMax = new Vector2(50, 50);
    [SerializeField] private float raycastStartHeight = 300f;
    [SerializeField] private float raycastMaxDistance = 400f;

    [Header("Mission Variety (Roadmap 1.1)")]
    [SerializeField] private int standardMissionWeight = 45;
    [SerializeField] private int timedMissionWeight = 25;
    [SerializeField] private int fragileMissionWeight = 20;
    [SerializeField] private int multiStopMissionWeight = 10;
    [SerializeField] private int multiStopMinStops = 2;
    [SerializeField] private int multiStopMaxStops = 3;
    [SerializeField] private bool preventConsecutiveSameDeliveryNeighborhood = true;

    [Header("Mission Conditions (Roadmap 1.1)")]
    [SerializeField] private float rushHourRewardMultiplier = 1.15f;
    [SerializeField] private float nightRewardMultiplier = 1.12f;
    [SerializeField] private float rainyRiskRewardMultiplier = 1.20f;

    [Header("Phone Mission Offers")]
    [SerializeField] private bool requirePhoneMissionAccept = true;
    [SerializeField] private float initialPhoneOfferDelay = 1.5f;
    [SerializeField] private float rejectedOfferRetryDelay = 8f;
    [SerializeField] private float nextOfferDelayAfterSuccess = 3f;
    [SerializeField] private float nextOfferDelayAfterFailure = 4f;
    [SerializeField] private PhoneMissionUI phoneMissionUI;

    [Header("Quest Integration")]
    [SerializeField] private bool useQuestSystem = true;
    [SerializeField] private CargoLibrary cargoLibrary;

    [Header("Neighborhood Integration")]
    [SerializeField] private bool spawnOnlyInNeighborhoods = true;
    [SerializeField] private float neighborhoodCheckRadius = 2f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    [Header("Extracted Components")]
    [SerializeField] private SpeedometerUI speedometerUI;
    [SerializeField] private DeliveryUI deliveryUI;

    private DeliveryBox currentBox;
    private GameObject currentDeliveryIndicator;
    private GameObject currentDeliveryPreview; // Ghost box at delivery location
    private DeliveryBox pooledBox;
    private GameObject pooledDeliveryIndicator;
    private GameObject pooledDeliveryPreview;
    private Vector3 currentPickupPoint;
    private Vector3 currentDeliveryPoint;
    private string currentPickupNeighborhoodName = "";
    private string currentDeliveryNeighborhoodName = "";
    private string lastCompletedDeliveryNeighborhoodName = "";
    private bool isDeliveryActive = false;
    private DeliveryMissionType currentMissionType = DeliveryMissionType.Standard;
    private readonly List<Vector3> currentDeliveryStops = new List<Vector3>();
    private readonly List<string> currentDeliveryStopNeighborhoods = new List<string>();
    private int currentDeliveryStopIndex;
    private Quaternion currentDeliveryParkingRotation = Quaternion.identity;
    private int lastObservedQuestDeliveryIndex = -1;
    private float currentMissionRewardMultiplier = 1f;
    private bool hasRushHourBonus;
    private bool hasNightBonus;
    private bool hasRainRiskBonus;
    private bool hasPendingPhoneOffer;
    private bool hasAcceptedMission;
    private bool missionSettingsPreparedForSpawn;
    private DeliveryMissionType pendingMissionType = DeliveryMissionType.Standard;
    private float pendingMissionRewardMultiplier = 1f;
    private bool pendingRushHourBonus;
    private bool pendingNightBonus;
    private bool pendingRainRiskBonus;
    private List<Vector3> availableSpawnPoints = new List<Vector3>();
    private QuestData currentDeliveryQuest;
    private Transform cachedPlayerTransform;
    private Rigidbody cachedPlayerRigidbody;
    private DeliverySpawnEnvironment spawnEnvironment;
    private Bounds[] cachedTerrainBounds;
    private bool hasTerrainBounds;
    private bool isFinishingDeliveryLifecycle;
    private bool usingRoadGraphSpawnCache;
    private readonly Dictionary<int, int> roadGraphComponentBySegmentId = new Dictionary<int, int>();
    private RoadGraph cachedRoadGraphConnectivitySource;
    private int cachedRoadGraphConnectivitySegmentCount = -1;
    private Transform deliveryParkingFill;
    private LineRenderer deliveryParkingBorder;
    private LineRenderer deliveryParkingCenterLine;
    private Material deliveryParkingFillMaterial;
    private Material deliveryParkingLineMaterial;

    public bool IsDeliveryActive => isDeliveryActive;
    public bool HasBox => currentBox != null;
    public bool IsCarryingBox => currentBox != null && currentBox.IsPickedUp;
    public bool HasObjectivePoint => HasBox || isDeliveryActive;
    public bool ShouldShowObjectiveUI => hasAcceptedMission && HasObjectivePoint;
    public Vector3 CurrentObjectivePoint => isDeliveryActive ? currentDeliveryPoint : currentPickupPoint;
    public Vector3 CurrentDeliveryPoint => currentDeliveryPoint;
    public Vector3 CurrentPickupPoint => currentPickupPoint;
    public string CurrentPickupNeighborhoodName => currentPickupNeighborhoodName;
    public string CurrentDeliveryNeighborhoodName => currentDeliveryNeighborhoodName;

    private void Awake()
    {
        spawnEnvironment = new DeliverySpawnEnvironment(buildingNameKeywords);
        EnsureRoadSurfaceMask();
        NavigationService.EnsureInstance();

        if (speedometerUI == null)
            speedometerUI = GetComponent<SpeedometerUI>();
        if (speedometerUI == null)
            speedometerUI = gameObject.AddComponent<SpeedometerUI>();
    }

    private System.Collections.IEnumerator Start()
    {
        EnsureRoadSurfaceMask();
        if (deliveryUI == null)
        {
            deliveryUI = FindFirstObjectByType<DeliveryUI>();
        }

        // This project flow requires phone acceptance before showing delivery objective UI.
        // Force this at runtime so scene/prefab inspector mismatches cannot bypass it.
        requirePhoneMissionAccept = true;
        useQuestSystem = true;

        if (roadGraphBuilder == null)
        {
            roadGraphBuilder = FindFirstObjectByType<RoadGraphBuilder>();
        }

        // Wait a short time so RoadGraphBuilder can finish building on scene start.
        int waitFrames = 0;
        while (useRoadGraphSpawnPoints && waitFrames < 120 && !HasRoadGraphData())
        {
            waitFrames++;
            yield return null;
        }

        // Cache terrain bounds BEFORE generating spawn points so terrain validation
        // and auto-calculated spawn area work correctly.
        ResolvePlayerTransform();
        CacheTerrainBounds();
        AutoCalculateSpawnAreaFromTerrain();

        Debug.Log($"[DeliveryManager] Init: hasTerrainBounds={hasTerrainBounds}, " +
                  $"hasRoadGraph={HasRoadGraphData()}, " +
                  $"roadMask={roadSurfaceMask.value}, " +
                  $"player={(cachedPlayerTransform != null ? cachedPlayerTransform.position.ToString() : "NULL")}, " +
                  $"spawnArea=({spawnAreaMin} -> {spawnAreaMax})");

        GenerateSpawnPoints();
        EnsurePhoneMissionUI();
        hasAcceptedMission = false;
        if (requirePhoneMissionAccept)
        {
            ScheduleMissionOffer(initialPhoneOfferDelay);
        }
        else
        {
            SpawnNewBox();
        }

        speedometerUI?.Initialize(cachedPlayerRigidbody);
        SubscribeToQuestEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromQuestEvents();

        if (phoneMissionUI != null)
        {
            phoneMissionUI.BindCallbacks(null, null);
        }
    }

    [ContextMenu("Auto Assign Startup References")]
    private void AutoAssignStartupReferencesFromContextMenu()
    {
        AutoAssignStartupReferences();
    }

    public bool AutoAssignStartupReferences()
    {
        bool changed = false;

        if (roadGraphBuilder == null)
        {
            roadGraphBuilder = FindSceneComponent<RoadGraphBuilder>();
            changed |= roadGraphBuilder != null;
        }

        if (deliveryUI == null)
        {
            deliveryUI = FindSceneComponent<DeliveryUI>();
            changed |= deliveryUI != null;
        }

        if (phoneMissionUI == null)
        {
            phoneMissionUI = FindSceneComponent<PhoneMissionUI>();
            changed |= phoneMissionUI != null;
        }

        if (speedometerUI == null)
        {
            speedometerUI = GetComponent<SpeedometerUI>();
            if (speedometerUI == null)
            {
                speedometerUI = FindSceneComponent<SpeedometerUI>();
            }

            changed |= speedometerUI != null;
        }

        if (cargoLibrary == null)
        {
            cargoLibrary = Resources.Load<CargoLibrary>("CargoLibrary");
            changed |= cargoLibrary != null;
        }

        return changed;
    }

    private void CacheTerrainBounds()
    {
        Terrain[] terrains = Terrain.activeTerrains;
        if (terrains == null || terrains.Length == 0)
        {
            hasTerrainBounds = false;
            spawnEnvironment?.SetTerrainBounds(null);
            return;
        }

        var boundsList = new List<Bounds>();
        foreach (Terrain terrain in terrains)
        {
            if (terrain == null || terrain.terrainData == null) continue;
            Vector3 terrainMin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            Vector3 center = terrainMin + size * 0.5f;
            boundsList.Add(new Bounds(center, size));
        }
        cachedTerrainBounds = boundsList.ToArray();
        hasTerrainBounds = cachedTerrainBounds.Length > 0;
        spawnEnvironment?.SetTerrainBounds(cachedTerrainBounds);
    }

    /// <summary>
    /// Auto-calculate spawnAreaMin/Max from terrain bounds so packages spawn
    /// across the actual playable area instead of the default (-50, 50) range.
    /// </summary>
    private void AutoCalculateSpawnAreaFromTerrain()
    {
        if (!hasTerrainBounds || cachedTerrainBounds.Length == 0) return;

        float minX = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxZ = float.MinValue;

        foreach (Bounds b in cachedTerrainBounds)
        {
            if (b.min.x < minX) minX = b.min.x;
            if (b.min.z < minZ) minZ = b.min.z;
            if (b.max.x > maxX) maxX = b.max.x;
            if (b.max.z > maxZ) maxZ = b.max.z;
        }

        // Shrink slightly so we don't spawn right at terrain edges
        float margin = 10f;
        spawnAreaMin = new Vector2(minX + margin, minZ + margin);
        spawnAreaMax = new Vector2(maxX - margin, maxZ - margin);

        if (showDebugInfo)
        {
            Debug.Log($"[DeliveryManager] Auto-calculated spawn area from terrain: min={spawnAreaMin}, max={spawnAreaMax}");
        }
    }

    private void ResolvePlayerTransform()
    {
        if (cachedPlayerTransform != null) return;

        CarController car = FindFirstObjectByType<CarController>();
        if (car != null)
        {
            cachedPlayerTransform = car.transform;
            cachedPlayerRigidbody = car.GetComponent<Rigidbody>();
            return;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            cachedPlayerTransform = playerObj.transform;
            cachedPlayerRigidbody = playerObj.GetComponent<Rigidbody>();
        }
    }

    public void SetPlayerVehicle(CarController controller)
    {
        cachedPlayerTransform = controller != null ? controller.transform : null;
        cachedPlayerRigidbody = controller != null ? controller.GetComponent<Rigidbody>() : null;

        if (deliveryUI == null)
        {
            deliveryUI = FindFirstObjectByType<DeliveryUI>();
        }

        if (deliveryUI != null)
        {
            deliveryUI.SetPlayerTransform(cachedPlayerTransform);
        }

        speedometerUI?.Initialize(cachedPlayerRigidbody);
    }

    private static T FindSceneComponent<T>() where T : Component
    {
        T[] components = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return components != null && components.Length > 0 ? components[0] : null;
    }

    private void SubscribeToQuestEvents()
    {
        if (QuestManager.Instance == null)
        {
            return;
        }

        QuestManager.Instance.OnQuestCompleted.AddListener(HandleQuestCompleted);
        QuestManager.Instance.OnQuestFailed.AddListener(HandleQuestFailed);
    }

    private void UnsubscribeFromQuestEvents()
    {
        if (QuestManager.Instance == null)
        {
            return;
        }

        QuestManager.Instance.OnQuestCompleted.RemoveListener(HandleQuestCompleted);
        QuestManager.Instance.OnQuestFailed.RemoveListener(HandleQuestFailed);
    }

    private void Update()
    {
        if (!isDeliveryActive || currentBox == null || !currentBox.IsPickedUp)
        {
            return;
        }

        if (cachedPlayerTransform == null)
        {
            ResolvePlayerTransform();
        }

        Transform player = cachedPlayerTransform;
        if (player == null)
        {
            return;
        }

        SyncDeliveryTargetFromQuestProgress();

        float distance = Vector3.Distance(player.position, currentDeliveryPoint);
        if (deliveryUI != null)
        {
            deliveryUI.UpdateDistance(distance);
        }

        UpdateDeliveryParkingHologramAnimation();

        if (IsPlayerFullyInsideDeliveryParkingBay(player))
        {
            TryCompleteCurrentDeliveryStop();
            return;
        }

        // Delivery success is already validated by the actual target point / quest trigger.
        // Keep neighborhood text in sync, but do not hard-fail when zone borders are noisy.
        if (distance <= deliveryRadius * 1.1f)
        {
            RefreshDeliveryNeighborhoodLabel();
        }
    }

    /// <summary>
    /// Generate spawn points across the map
    /// </summary>
    private void GenerateSpawnPoints()
    {
        availableSpawnPoints.Clear();
        usingRoadGraphSpawnCache = false;

        if (showDebugInfo)
        {
            Debug.Log($"[DeliveryManager] Generating spawn points. SpawnArea: min={spawnAreaMin}, max={spawnAreaMax}");
        }

        // Use manual spawn points if available
        if (spawnPoints != null && spawnPoints.Length > 0 && !autoGenerateSpawnPoints)
        {
            foreach (Transform point in spawnPoints)
            {
                if (point != null)
                {
                    if (IsValidSpawnPosition(point.position))
                    {
                        availableSpawnPoints.Add(point.position);
                    }
                    else if (showDebugInfo)
                    {
                        Debug.LogWarning($"[DeliveryManager] Ignoring invalid manual spawn point at {point.position}");
                    }
                }
            }
        }
        else
        {
            if (useRoadGraphSpawnPoints && TryGenerateSpawnPointsFromRoadGraph())
            {
                usingRoadGraphSpawnCache = true;
                if (showDebugInfo)
                {
                    Debug.Log($"[DeliveryManager] Generated {availableSpawnPoints.Count} road-graph spawn points");
                }
                return;
            }

            // Auto-generate spawn points
            for (int i = 0; i < numberOfAutoSpawnPoints; i++)
            {
                Vector3 randomPoint = GetRandomGroundPosition(false);
                if (IsValidSpawnPosition(randomPoint))
                {
                    availableSpawnPoints.Add(randomPoint);
                }
            }
        }

        if (showDebugInfo)
        {
            Debug.Log($"[DeliveryManager] Generated {availableSpawnPoints.Count} spawn points");
        }
    }

    /// <summary>
    /// Get random position on road surface around a center point.
    /// </summary>
    private Vector3 GetRandomGroundPosition(bool logOnFailure = true)
    {
        if (cachedPlayerTransform == null)
            ResolvePlayerTransform();

        Vector3 searchCenter = cachedPlayerTransform != null
            ? cachedPlayerTransform.position
            : Vector3.zero;

        // Search around the center with increasing radius
        float[] radii = { 40f, 70f, 110f, 160f, 250f };
        foreach (float radius in radii)
        {
            for (int i = 0; i < 15; i++)
            {
                Vector2 rndCircle = UnityEngine.Random.insideUnitCircle.normalized
                                    * UnityEngine.Random.Range(radius * 0.4f, radius);
                Vector3 probePos = new Vector3(
                    searchCenter.x + rndCircle.x,
                    raycastStartHeight,
                    searchCenter.z + rndCircle.y);

                if (TryFindRoadsidePoint(probePos, out Vector3 result))
                {
                    if (showDebugInfo)
                    {
                        Debug.Log($"[DeliveryManager] Found roadside spawn at {result} (radius={radius})");
                    }
                    return result;
                }
            }
        }

        // Also sample across the configured spawn area, not only around player.
        for (int i = 0; i < 120; i++)
        {
            Vector3 probePos = new Vector3(
                UnityEngine.Random.Range(spawnAreaMin.x, spawnAreaMax.x),
                raycastStartHeight,
                UnityEngine.Random.Range(spawnAreaMin.y, spawnAreaMax.y));

            if (TryFindRoadsidePoint(probePos, out Vector3 result))
            {
                if (showDebugInfo)
                {
                    Debug.Log($"[DeliveryManager] Found road spawn in area sample at {result}");
                }
                return result;
            }
        }

        // Last resort: any flat ground near search center
        for (int i = 0; i < 30; i++)
        {
            Vector2 off = UnityEngine.Random.insideUnitCircle * UnityEngine.Random.Range(20f, 120f);
            Vector3 probe = new Vector3(
                searchCenter.x + off.x, raycastStartHeight, searchCenter.z + off.y);

            if (Physics.Raycast(probe, Vector3.down, out RaycastHit hit, raycastMaxDistance, ~0, QueryTriggerInteraction.Ignore)
                && !hit.collider.isTrigger
                && (!HasRoadMask || IsRoadCollider(hit.collider))
                && Vector3.Dot(hit.normal, Vector3.up) > 0.7f
                && !IsSpawnSpaceBlocked(hit.point + Vector3.up * spawnHeight))
            {
                Vector3 fallback = hit.point + Vector3.up * spawnHeight;
                Debug.LogWarning($"[DeliveryManager] Using last-resort ground spawn at {fallback}");
                return fallback;
            }
        }

        if (logOnFailure)
        {
            Debug.LogError("[DeliveryManager] Failed to find a valid spawn position.");
        }
        return Vector3.positiveInfinity;
    }

    /// <summary>
    /// From a raycast origin, find a valid road spawn point below.
    /// </summary>
    private bool TryFindRoadsidePoint(Vector3 rayOrigin, out Vector3 roadsidePoint)
    {
        roadsidePoint = Vector3.positiveInfinity;

        // Cast down to find whatever surface is below
        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastMaxDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        if (hit.collider.isTrigger || Vector3.Dot(hit.normal, Vector3.up) < 0.7f)
        {
            return false;
        }

        Vector3 groundPoint = hit.point;

        // If we hit a road collider, spawn directly on road surface.
        if (IsRoadCollider(hit.collider))
        {
            roadsidePoint = groundPoint + Vector3.up * spawnHeight;
            return !IsSpawnSpaceBlocked(roadsidePoint);
        }

        // No road mask configured: accept generic ground.
        if (!HasRoadMask)
        {
            roadsidePoint = groundPoint + Vector3.up * spawnHeight;
            return !IsSpawnSpaceBlocked(roadsidePoint);
        }

        // We hit non-road ground: search nearby road colliders and try to place on road.
        if (TryFindRoadSurfaceNearPoint(groundPoint, out roadsidePoint))
        {
            return true;
        }

        return false;
    }

    private bool TryFindRoadSurfaceNearPoint(Vector3 center, out Vector3 roadPoint)
    {
        roadPoint = Vector3.positiveInfinity;
        return spawnEnvironment != null &&
               spawnEnvironment.TryFindRoadSurfaceNearPoint(center, spawnHeight, out roadPoint);
    }

    /// <summary>
    /// Given a point on a road, probe sideways to find the road edge / curb area.
    /// </summary>
    private bool TryOffsetToRoadside(Vector3 roadPoint, Collider roadCollider, out Vector3 roadsidePoint)
    {
        roadsidePoint = Vector3.positiveInfinity;

        // Try 4 cardinal + 4 diagonal directions to find the road edge
        Vector3[] directions =
        {
            Vector3.right, Vector3.left, Vector3.forward, Vector3.back,
            (Vector3.right + Vector3.forward).normalized,
            (Vector3.right + Vector3.back).normalized,
            (Vector3.left + Vector3.forward).normalized,
            (Vector3.left + Vector3.back).normalized,
        };

        // Shuffle so we don't always pick the same direction
        for (int i = directions.Length - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (directions[i], directions[j]) = (directions[j], directions[i]);
        }

        foreach (Vector3 dir in directions)
        {
            // Step outward from road center to find where road ends
            for (float dist = buildingFrontOffset; dist <= buildingFrontOffset + 6f; dist += 1f)
            {
                Vector3 candidate = roadPoint + dir * dist;
                Vector3 probeRayStart = candidate + Vector3.up * 10f;

                if (!Physics.Raycast(probeRayStart, Vector3.down, out RaycastHit sideHit, 30f, groundMask, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                if (sideHit.collider.isTrigger)
                {
                    continue;
                }

                // We want to be OFF the road (curb/sidewalk) but still close to it
                if (!IsRoadCollider(sideHit.collider))
                {
                    Vector3 spawnCandidate = sideHit.point + Vector3.up * spawnHeight;
                    if (!IsSpawnSpaceBlocked(spawnCandidate))
                    {
                        roadsidePoint = spawnCandidate;
                        return true;
                    }
                }
            }
        }

        // Could not find road edge - place right next to road with offset
        Vector3 randomDir = new Vector3(UnityEngine.Random.Range(-1f, 1f), 0, UnityEngine.Random.Range(-1f, 1f)).normalized;
        Vector3 offsetPoint = roadPoint + randomDir * buildingFrontOffset + Vector3.up * spawnHeight;
        if (!IsSpawnSpaceBlocked(offsetPoint))
        {
            roadsidePoint = offsetPoint;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Check if a position is inside any neighborhood zone
    /// </summary>
    private bool IsInsideNeighborhood(Vector3 position)
    {
        return spawnEnvironment != null &&
               spawnEnvironment.IsInsideNeighborhood(position, neighborhoodCheckRadius);
    }

    /// <summary>
    /// Spawn a new delivery box at random location
    /// </summary>
    private void SpawnNewBox()
    {
        if (boxPrefab == null)
        {
            Debug.LogError("[DeliveryManager] Box prefab is not assigned!");
            if (requirePhoneMissionAccept)
            {
                ScheduleMissionOffer(nextOfferDelayAfterFailure);
            }
            return;
        }

        if (!missionSettingsPreparedForSpawn)
        {
            currentMissionType = PickMissionType();
            EvaluateMissionConditions();
        }
        missionSettingsPreparedForSpawn = false;

        // Get a valid random spawn point (keep it away from player so it does not auto-pickup instantly)
        if (!TryGetPickupSpawnPoint(out Vector3 spawnPos) || spawnPos == Vector3.zero)
        {
            Debug.LogError("[DeliveryManager] Could not find a valid spawn position for delivery box.");
            if (requirePhoneMissionAccept)
            {
                ScheduleMissionOffer(nextOfferDelayAfterFailure);
            }
            return;
        }

        // Cache pickup and delivery targets before the player reaches the box.
        currentPickupPoint = spawnPos;
        currentPickupNeighborhoodName = ResolveNeighborhoodName(currentPickupPoint);
        currentDeliveryPoint = Vector3.zero;
        currentDeliveryNeighborhoodName = "";
        currentDeliveryStops.Clear();
        currentDeliveryStopNeighborhoods.Clear();
        currentDeliveryStopIndex = 0;
        lastObservedQuestDeliveryIndex = -1;
        isDeliveryActive = false;

        if (!PrepareDeliveryRoute(currentPickupPoint))
        {
            Debug.LogError("[DeliveryManager] Could not prepare delivery route for spawned box.");
            if (requirePhoneMissionAccept)
            {
                ScheduleMissionOffer(nextOfferDelayAfterFailure);
            }
            return;
        }

        // Spawn box with slight rotation variation
        Quaternion rotation = Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0);
        currentBox = AcquireDeliveryBox(spawnPos, rotation);
        if (currentBox == null)
        {
            Debug.LogError("[DeliveryManager] Failed to acquire delivery box.");
            return;
        }

        // Create quest in quest system
        if (useQuestSystem)
        {
            CreateDeliveryQuest(spawnPos, currentMissionType);
            UpdateQuestWithDelivery(currentDeliveryStops, currentDeliveryStopNeighborhoods);
        }

        if (showDebugInfo)
        {
            Debug.Log($"[DeliveryManager] Spawned box at {spawnPos}. MissionType={currentMissionType}, Conditions x{currentMissionRewardMultiplier:F2}");
        }

        NavigationService.EnsureInstance()?.SetObjective(new NavigationObjective(ObjectiveType.Pickup, currentPickupPoint));
    }

    private bool TryGetPickupSpawnPoint(out Vector3 spawnPoint)
    {
        ResolvePlayerTransform();

        int attempts = Mathf.Max(40, numberOfAutoSpawnPoints * 8);
        float minDistanceSqr = Mathf.Max(0f, minPickupSpawnDistanceFromPlayer) * Mathf.Max(0f, minPickupSpawnDistanceFromPlayer);
        Vector3 routeReferencePoint = cachedPlayerTransform != null ? cachedPlayerTransform.position : Vector3.zero;

        for (int i = 0; i < attempts; i++)
        {
            if (!TryGetValidSpawnPoint(routeReferencePoint, false, out Vector3 candidate))
            {
                continue;
            }

            if (cachedPlayerTransform != null && minDistanceSqr > 0f)
            {
                Vector3 delta = candidate - cachedPlayerTransform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude < minDistanceSqr)
                {
                    continue;
                }
            }

            spawnPoint = candidate;
            return true;
        }

        return TryGetValidSpawnPoint(routeReferencePoint, false, out spawnPoint);
    }

    /// <summary>
    /// Called when player picks up the box
    /// </summary>
    public void OnBoxPickedUp(DeliveryBox box)
    {
        if (box != currentBox) return;

        if (currentDeliveryStops.Count == 0)
        {
            HandleDeliveryFailure("No valid delivery location");
            return;
        }

        currentDeliveryStopIndex = 0;
        currentDeliveryPoint = currentDeliveryStops[currentDeliveryStopIndex];
        currentDeliveryNeighborhoodName = currentDeliveryStopNeighborhoods[currentDeliveryStopIndex];
        currentDeliveryParkingRotation = ResolveDeliveryParkingRotation(currentDeliveryPoint);

        if (useQuestSystem && currentDeliveryQuest != null)
        {
            bool pickupCommitted = QuestManager.Instance != null &&
                                   QuestManager.Instance.CommitExternalPickup(currentDeliveryQuest, BuildDeliveryObjectiveDescription(0));
            if (!pickupCommitted)
            {
                HandleDeliveryFailure("Quest pickup handoff failed");
                return;
            }

            lastObservedQuestDeliveryIndex = 0;
        }

        isDeliveryActive = true;

        // The physical box and ghost preview already communicate the objective clearly.
        // Keep floating arrows opt-in so they do not overlap the delivery cargo.
        if (showFloatingObjectiveMarkers && deliveryIndicatorPrefab != null)
        {
            currentDeliveryIndicator = AcquireDeliveryIndicatorFromPrefab();
        }
        else if (showFloatingObjectiveMarkers)
        {
            // Create default delivery indicator
            CreateDefaultDeliveryIndicator();
        }

        if (currentDeliveryIndicator != null)
        {
            currentDeliveryIndicator.transform.SetPositionAndRotation(currentDeliveryPoint + Vector3.up * 2f, Quaternion.identity);
            currentDeliveryIndicator.SetActive(true);
        }

        // Create ghost box preview at delivery location
        CreateDeliveryPreview();

        // Notify UI
        if (deliveryUI != null)
        {
            deliveryUI.OnBoxPickedUp(currentDeliveryPoint, currentPickupNeighborhoodName, currentDeliveryNeighborhoodName);
        }

        if (showDebugInfo)
        {
            float distance = Vector3.Distance(box.transform.position, currentDeliveryPoint);
            Debug.Log($"[DeliveryManager] Delivery route prepared. Stops={currentDeliveryStops.Count}, FirstTarget={currentDeliveryPoint}, Distance={distance:F1}m");
        }

        NavigationService.EnsureInstance()?.SetObjective(new NavigationObjective(ObjectiveType.Delivery, currentDeliveryPoint, currentDeliveryStopIndex, currentDeliveryStops.Count));
    }

    private bool PrepareDeliveryRoute(Vector3 pickupPoint)
    {
        BuildDeliveryStops(pickupPoint);
        return currentDeliveryStops.Count > 0;
    }

    /// <summary>
    /// Create default delivery indicator if none assigned
    /// </summary>
    private void CreateDefaultDeliveryIndicator()
    {
        if (pooledDeliveryIndicator == null)
        {
            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            indicator.name = "DeliveryIndicator";
            indicator.transform.localScale = new Vector3(2f, 3f, 2f);

            Collider indicatorCollider = indicator.GetComponent<Collider>();
            if (indicatorCollider != null)
            {
                indicatorCollider.enabled = false;
            }

            MeshRenderer indicatorRenderer = indicator.GetComponent<MeshRenderer>();
            Material indicatorMat = RuntimeColorMaterialHelper.CreateColorMaterial(new Color(1f, 0.8f, 0f, 1f), indicatorRenderer);
            if (indicatorMat != null && indicatorRenderer != null)
            {
                indicatorRenderer.material = indicatorMat;
            }

            if (indicator.GetComponent<DeliveryIndicator>() == null)
            {
                indicator.AddComponent<DeliveryIndicator>();
            }

            indicator.SetActive(false);
            pooledDeliveryIndicator = indicator;
        }

        currentDeliveryIndicator = pooledDeliveryIndicator;
    }

    /// <summary>
    /// Create parking bay hologram at delivery location
    /// </summary>
    private void CreateDeliveryPreview()
    {
        if (pooledDeliveryPreview == null)
        {
            pooledDeliveryPreview = new GameObject("DeliveryParkingHologram");
            BuildDeliveryParkingHologram(pooledDeliveryPreview.transform);

            pooledDeliveryPreview.SetActive(false);
        }

        currentDeliveryPreview = pooledDeliveryPreview;
        currentDeliveryParkingRotation = ResolveDeliveryParkingRotation(currentDeliveryPoint);
        currentDeliveryPreview.transform.SetPositionAndRotation(currentDeliveryPoint, currentDeliveryParkingRotation);
        UpdateDeliveryParkingHologramPose();
        currentDeliveryPreview.SetActive(true);

        if (showDebugInfo)
        {
            Debug.Log($"[DeliveryManager] Created delivery parking hologram at {currentDeliveryPoint}");
        }
    }

    private void BuildDeliveryParkingHologram(Transform root)
    {
        if (root == null)
        {
            return;
        }

        GameObject fillObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fillObject.name = "DeliveryParkingBayFill";
        fillObject.transform.SetParent(root, false);
        Collider fillCollider = fillObject.GetComponent<Collider>();
        if (fillCollider != null)
        {
            fillCollider.enabled = false;
        }

        MeshRenderer fillRenderer = fillObject.GetComponent<MeshRenderer>();
        deliveryParkingFillMaterial = CreateTransparentDeliveryParkingMaterial(deliveryParkingFillColor, fillRenderer);
        if (fillRenderer != null && deliveryParkingFillMaterial != null)
        {
            fillRenderer.material = deliveryParkingFillMaterial;
            fillRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fillRenderer.receiveShadows = false;
        }

        deliveryParkingFill = fillObject.transform;
        deliveryParkingBorder = CreateDeliveryParkingLine(root, "DeliveryParkingBayBorder", true);
        deliveryParkingCenterLine = CreateDeliveryParkingLine(root, "DeliveryParkingBayCenterLine", false);
    }

    private LineRenderer CreateDeliveryParkingLine(Transform parent, string objectName, bool loop)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(parent, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = loop;
        line.widthMultiplier = Mathf.Max(0.02f, deliveryParkingLineWidth);
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        if (deliveryParkingLineMaterial == null)
        {
            deliveryParkingLineMaterial = CreateTransparentDeliveryParkingMaterial(deliveryParkingLineColor, null);
        }

        if (deliveryParkingLineMaterial != null)
        {
            line.material = deliveryParkingLineMaterial;
        }

        return line;
    }

    private Material CreateTransparentDeliveryParkingMaterial(Color color, MeshRenderer fallbackRenderer)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null && fallbackRenderer != null && fallbackRenderer.sharedMaterial != null)
        {
            shader = fallbackRenderer.sharedMaterial.shader;
        }

        if (shader == null)
        {
            return null;
        }

        Material material = new Material(shader);
        material.color = color;
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.SetFloat("_Surface", 1f);
        material.renderQueue = 3000;
        return material;
    }

    private void UpdateDeliveryParkingHologramPose()
    {
        if (currentDeliveryPreview == null)
        {
            return;
        }

        Vector3 groundPoint = ResolveGroundPoint(currentDeliveryPoint);
        currentDeliveryPreview.transform.SetPositionAndRotation(
            groundPoint + Vector3.up * Mathf.Max(0.005f, deliveryParkingGroundOffset),
            currentDeliveryParkingRotation);

        float width = Mathf.Max(1f, deliveryParkingSize.x);
        float length = Mathf.Max(width, deliveryParkingSize.y);

        if (deliveryParkingFill != null)
        {
            deliveryParkingFill.localPosition = Vector3.zero;
            deliveryParkingFill.localRotation = Quaternion.identity;
            deliveryParkingFill.localScale = new Vector3(width, 0.025f, length);
        }

        if (deliveryParkingBorder != null)
        {
            float halfWidth = width * 0.5f;
            float halfLength = length * 0.5f;
            deliveryParkingBorder.positionCount = 4;
            deliveryParkingBorder.SetPosition(0, new Vector3(-halfWidth, 0.035f, -halfLength));
            deliveryParkingBorder.SetPosition(1, new Vector3(-halfWidth, 0.035f, halfLength));
            deliveryParkingBorder.SetPosition(2, new Vector3(halfWidth, 0.035f, halfLength));
            deliveryParkingBorder.SetPosition(3, new Vector3(halfWidth, 0.035f, -halfLength));
        }

        if (deliveryParkingCenterLine != null)
        {
            float halfLength = length * 0.38f;
            deliveryParkingCenterLine.positionCount = 2;
            deliveryParkingCenterLine.SetPosition(0, new Vector3(0f, 0.04f, -halfLength));
            deliveryParkingCenterLine.SetPosition(1, new Vector3(0f, 0.04f, halfLength));
        }
    }

    private void UpdateDeliveryParkingHologramAnimation()
    {
        if (currentDeliveryPreview == null || !currentDeliveryPreview.activeSelf)
        {
            return;
        }

        float pulse = 1f + Mathf.Sin(Time.time * deliveryParkingPulseSpeed) * deliveryParkingPulseScale;
        currentDeliveryPreview.transform.localScale = new Vector3(pulse, 1f, pulse);
        float alphaPulse = 0.75f + Mathf.Sin(Time.time * deliveryParkingPulseSpeed) * 0.25f;
        SetMaterialAlpha(deliveryParkingFillMaterial, deliveryParkingFillColor.a * alphaPulse);
        SetMaterialAlpha(deliveryParkingLineMaterial, deliveryParkingLineColor.a * alphaPulse);
    }

    private static void SetMaterialAlpha(Material material, float alpha)
    {
        if (material == null)
        {
            return;
        }

        Color color = material.color;
        color.a = Mathf.Clamp01(alpha);
        material.color = color;
    }

    private Vector3 ResolveGroundPoint(Vector3 position)
    {
        Vector3 origin = position + Vector3.up * raycastStartHeight;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, raycastMaxDistance + raycastStartHeight, groundMask, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }

        return position;
    }

    private Quaternion ResolveDeliveryParkingRotation(Vector3 point)
    {
        if (roadGraphBuilder != null && roadGraphBuilder.RoadGraph != null)
        {
            var (_, _, _, tangent) = roadGraphBuilder.RoadGraph.ProjectPointOnRoad(point);
            tangent.y = 0f;
            if (tangent.sqrMagnitude > 0.0001f)
            {
                return Quaternion.LookRotation(tangent.normalized, Vector3.up);
            }
        }

        if (cachedPlayerTransform != null)
        {
            Vector3 forward = cachedPlayerTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f)
            {
                return Quaternion.LookRotation(forward.normalized, Vector3.up);
            }
        }

        return Quaternion.identity;
    }

    private bool IsPlayerFullyInsideDeliveryParkingBay(Transform player)
    {
        if (!isDeliveryActive || player == null)
        {
            return false;
        }

        Quaternion parkingRotation = currentDeliveryParkingRotation == Quaternion.identity
            ? ResolveDeliveryParkingRotation(currentDeliveryPoint)
            : currentDeliveryParkingRotation;
        Vector3 bayCenter = ResolveGroundPoint(currentDeliveryPoint);
        Quaternion inverseRotation = Quaternion.Inverse(parkingRotation);
        float halfWidth = Mathf.Max(1f, deliveryParkingSize.x) * 0.5f + Mathf.Max(0f, deliveryParkingCompletionTolerance);
        float halfLength = Mathf.Max(deliveryParkingSize.x, deliveryParkingSize.y) * 0.5f + Mathf.Max(0f, deliveryParkingCompletionTolerance);

        if (!TryGetVehicleFootprintCorners(player, out Vector3[] corners))
        {
            Vector3 local = inverseRotation * (player.position - bayCenter);
            return Mathf.Abs(local.x) <= halfWidth && Mathf.Abs(local.z) <= halfLength;
        }

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 local = inverseRotation * (corners[i] - bayCenter);
            if (Mathf.Abs(local.x) > halfWidth || Mathf.Abs(local.z) > halfLength)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetVehicleFootprintCorners(Transform player, out Vector3[] corners)
    {
        corners = null;
        if (player == null)
        {
            return false;
        }

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;
        bool hasBounds = false;

        Collider[] colliders = player.GetComponentsInChildren<Collider>(false);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null || col.isTrigger || col is WheelCollider)
            {
                continue;
            }

            EncapsulateBoundsInVehicleSpace(player, col.bounds, ref minX, ref maxX, ref minZ, ref maxZ, ref hasBounds);
        }

        if (!hasBounds)
        {
            Renderer[] renderers = player.GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || ShouldIgnoreVehicleFootprintRenderer(renderer))
                {
                    continue;
                }

                EncapsulateBoundsInVehicleSpace(player, renderer.bounds, ref minX, ref maxX, ref minZ, ref maxZ, ref hasBounds);
            }
        }

        if (!hasBounds)
        {
            return false;
        }

        corners = new[]
        {
            player.TransformPoint(new Vector3(minX, 0f, minZ)),
            player.TransformPoint(new Vector3(minX, 0f, maxZ)),
            player.TransformPoint(new Vector3(maxX, 0f, maxZ)),
            player.TransformPoint(new Vector3(maxX, 0f, minZ))
        };
        return true;
    }

    private static bool ShouldIgnoreVehicleFootprintRenderer(Renderer renderer)
    {
        if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer)
        {
            return true;
        }

        string lowerName = renderer.name.ToLowerInvariant();
        return lowerName.Contains("smoke") ||
               lowerName.Contains("exhaust") ||
               lowerName.Contains("trail") ||
               lowerName.Contains("effect") ||
               lowerName.Contains("hologram") ||
               lowerName.Contains("marker");
    }

    private static void EncapsulateBoundsInVehicleSpace(
        Transform vehicle,
        Bounds bounds,
        ref float minX,
        ref float maxX,
        ref float minZ,
        ref float maxZ,
        ref bool hasBounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3[] points =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z)
        };

        for (int i = 0; i < points.Length; i++)
        {
            Vector3 local = vehicle.InverseTransformPoint(points[i]);
            minX = Mathf.Min(minX, local.x);
            maxX = Mathf.Max(maxX, local.x);
            minZ = Mathf.Min(minZ, local.z);
            maxZ = Mathf.Max(maxZ, local.z);
        }

        hasBounds = true;
    }

    private void TryCompleteCurrentDeliveryStop()
    {
        if (!isDeliveryActive)
        {
            return;
        }

        if (useQuestSystem && currentDeliveryQuest != null && QuestManager.Instance != null && currentDeliveryQuest.Status == QuestStatus.Active)
        {
            if (!QuestManager.Instance.CommitExternalDeliveryStop(currentDeliveryQuest))
            {
                HandleDeliveryFailure("Delivery parking handoff failed");
            }
            return;
        }

        CompleteDelivery();
    }

    /// <summary>
    /// Builds delivery stops based on mission type and neighborhood repetition rules.
    /// </summary>
    private void BuildDeliveryStops(Vector3 pickupPoint)
    {
        currentDeliveryStops.Clear();
        currentDeliveryStopNeighborhoods.Clear();

        int requestedStops = currentMissionType == DeliveryMissionType.MultiStop
            ? Mathf.Max(2, UnityEngine.Random.Range(multiStopMinStops, multiStopMaxStops + 1))
            : 1;

        HashSet<string> usedNeighborhoods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(currentPickupNeighborhoodName))
        {
            usedNeighborhoods.Add(currentPickupNeighborhoodName);
        }

        if (preventConsecutiveSameDeliveryNeighborhood && !string.IsNullOrWhiteSpace(lastCompletedDeliveryNeighborhoodName))
        {
            usedNeighborhoods.Add(lastCompletedDeliveryNeighborhoodName);
        }

        Vector3 referencePoint = pickupPoint;
        for (int i = 0; i < requestedStops; i++)
        {
            bool requireMinDistance = i == 0;
            if (!TryGetDeliveryPoint(referencePoint, requireMinDistance, usedNeighborhoods, out Vector3 point, out string neighborhood))
            {
                // Fallback keeps progression moving even in dense maps.
                if (TryGetValidSpawnPoint(referencePoint, false, out Vector3 fallback))
                {
                    point = fallback;
                    neighborhood = ResolveNeighborhoodName(fallback);
                }
                else
                {
                    break;
                }
            }

            currentDeliveryStops.Add(point);
            currentDeliveryStopNeighborhoods.Add(neighborhood);
            referencePoint = point;

            if (!string.IsNullOrWhiteSpace(neighborhood))
            {
                usedNeighborhoods.Add(neighborhood);
            }
        }
    }

    private bool TryGetDeliveryPoint(
        Vector3 referencePoint,
        bool requireMinDistance,
        HashSet<string> excludedNeighborhoods,
        out Vector3 point,
        out string neighborhood)
    {
        point = Vector3.zero;
        neighborhood = string.Empty;

        List<Vector3> orderedCandidates = new List<Vector3>();
        if (!TryGetOrderedSpawnCandidates(referencePoint, requireMinDistance, orderedCandidates))
        {
            return false;
        }

        bool allowExcludedNeighborhood = orderedCandidates.Count <= 2;
        Vector3 deferredCandidate = Vector3.zero;
        string deferredNeighborhood = string.Empty;
        bool hasDeferredCandidate = false;

        for (int i = 0; i < orderedCandidates.Count; i++)
        {
            Vector3 candidate = orderedCandidates[i];
            string candidateNeighborhood = ResolveNeighborhoodName(candidate);
            bool isExcludedNeighborhood = !string.IsNullOrWhiteSpace(candidateNeighborhood) &&
                                          excludedNeighborhoods != null &&
                                          excludedNeighborhoods.Contains(candidateNeighborhood);
            if (isExcludedNeighborhood)
            {
                if (!allowExcludedNeighborhood && !hasDeferredCandidate)
                {
                    deferredCandidate = candidate;
                    deferredNeighborhood = candidateNeighborhood;
                    hasDeferredCandidate = true;
                }

                continue;
            }

            point = candidate;
            neighborhood = candidateNeighborhood;
            return true;
        }

        if (hasDeferredCandidate)
        {
            point = deferredCandidate;
            neighborhood = deferredNeighborhood;
            return true;
        }

        return false;
    }

    private bool TryGetValidSpawnPoint(Vector3 referencePoint, bool enforceMinDistance, out Vector3 spawnPoint)
    {
        List<Vector3> orderedCandidates = new List<Vector3>();
        if (TryGetOrderedSpawnCandidates(referencePoint, enforceMinDistance, orderedCandidates) &&
            orderedCandidates.Count > 0)
        {
            spawnPoint = orderedCandidates[0];
            return true;
        }

        // Last resort: try a random ground position directly instead of returning origin
        Vector3 fallback = GetRandomGroundPosition(false);
        if (IsValidSpawnPosition(fallback))
        {
            spawnPoint = fallback;
            return true;
        }

        // Emergency fallback: if strict roadside constraints fail repeatedly,
        // allow any safe ground point so gameplay can continue.
        if (TryGetEmergencyGroundSpawn(out fallback))
        {
            spawnPoint = fallback;
            return true;
        }

        if (TryFindRoadPointFromRoadGraphWaypoints(out fallback))
        {
            spawnPoint = fallback;
            return true;
        }

        spawnPoint = Vector3.zero;
        return false;
    }

    private bool TryGetOrderedSpawnCandidates(Vector3 referencePoint, bool enforceMinDistance, List<Vector3> orderedCandidates)
    {
        orderedCandidates.Clear();

        const int maxAttempts = 40;
        bool validateAsRoadGraph = usingRoadGraphSpawnCache && availableSpawnPoints.Count > 0;
        bool preferReachableSpawn = TryGetReachabilityReferencePoint(referencePoint, out Vector3 reachabilityReferencePoint);
        int reachabilityRejectCount = 0;

        List<int> shuffledIndices = availableSpawnPoints.Count > 0 ? BuildShuffledSpawnIndices() : null;
        int preferredAttemptCount = shuffledIndices != null ? shuffledIndices.Count : maxAttempts;

        for (int i = 0; i < preferredAttemptCount; i++)
        {
            Vector3 candidate = shuffledIndices != null
                ? availableSpawnPoints[shuffledIndices[i]]
                : GetRandomGroundPosition(false);

            if (!IsSpawnCandidateEligible(candidate, validateAsRoadGraph, referencePoint, enforceMinDistance))
            {
                continue;
            }

            // Use the cheap BFS component check only. Full A* pathfinding was removed
            // because it caused multi-second freezes when accepting phone missions.
            // If two points share the same road-graph connected component they are
            // reachable; that is sufficient for spawn-point selection.
            if (preferReachableSpawn &&
                TryCheckSpawnConnectivity(reachabilityReferencePoint, candidate, out bool sharesRoadComponent) &&
                !sharesRoadComponent)
            {
                reachabilityRejectCount++;
                continue;
            }

            orderedCandidates.Add(candidate);
        }

        if (orderedCandidates.Count > 0)
        {
            return true;
        }

        if (preferReachableSpawn &&
            reachabilityRejectCount > 0 &&
            TryFindConnectedRoadGraphSpawnPoint(reachabilityReferencePoint, enforceMinDistance, out Vector3 connectedFallback))
        {
            orderedCandidates.Add(connectedFallback);
            return true;
        }

        if (preferReachableSpawn && reachabilityRejectCount > 0)
        {
            Debug.LogWarning($"[DeliveryManager] No graph-reachable spawn found after rejecting {reachabilityRejectCount} unreachable candidate(s). Falling back to legacy spawn selection.");
        }

        int fallbackAttemptCount = shuffledIndices != null ? shuffledIndices.Count : maxAttempts;
        for (int i = 0; i < fallbackAttemptCount; i++)
        {
            Vector3 candidate = shuffledIndices != null
                ? availableSpawnPoints[shuffledIndices[i]]
                : GetRandomGroundPosition(false);

            if (IsSpawnCandidateEligible(candidate, validateAsRoadGraph, referencePoint, enforceMinDistance))
            {
                orderedCandidates.Add(candidate);
            }
        }

        return orderedCandidates.Count > 0;
    }

    private bool TryFindConnectedRoadGraphSpawnPoint(Vector3 referencePoint, bool enforceMinDistance, out Vector3 spawnPoint)
    {
        spawnPoint = Vector3.zero;
        if (!HasRoadGraphData())
        {
            return false;
        }

        RoadGraph graph = roadGraphBuilder.RoadGraph;
        if (!TryEnsureRoadGraphConnectivityCache(graph))
        {
            return false;
        }

        var (referenceSegment, _, _, _) = graph.ProjectPointOnRoad(referencePoint);
        if (referenceSegment == null ||
            !roadGraphComponentBySegmentId.TryGetValue(referenceSegment.id, out int referenceComponent))
        {
            return false;
        }

        const int attempts = 260;
        for (int i = 0; i < attempts; i++)
        {
            var randomWp = graph.GetRandomWaypoint();
            if (randomWp.segment == null ||
                !roadGraphComponentBySegmentId.TryGetValue(randomWp.segment.id, out int candidateComponent) ||
                candidateComponent != referenceComponent)
            {
                continue;
            }

            Waypoint wp = randomWp.segment.GetWaypoint(randomWp.waypointIndex);
            if (wp == null)
            {
                continue;
            }

            Vector3 rayStart = wp.position + Vector3.up * 60f;
            if (!Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 140f, ~0, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            if (hit.collider == null || hit.collider.isTrigger || Vector3.Dot(hit.normal, Vector3.up) < 0.65f)
            {
                continue;
            }

            bool acceptedRoad = HasRoadMask ? (IsRoadCollider(hit.collider) || IsLikelyRoadCollider(hit.collider)) : true;
            if (!acceptedRoad)
            {
                Vector3 wpDelta = hit.point - wp.position;
                wpDelta.y = 0f;
                if (wpDelta.sqrMagnitude > 12.25f)
                {
                    continue;
                }
            }

            Vector3 candidate = hit.point + Vector3.up * spawnHeight;
            if (!IsSpawnCandidateEligible(candidate, true, referencePoint, enforceMinDistance))
            {
                continue;
            }

            spawnPoint = candidate;
            return true;
        }

        return false;
    }

    private bool IsSpawnCandidateEligible(Vector3 candidate, bool validateAsRoadGraph, Vector3 referencePoint, bool enforceMinDistance)
    {
        bool isValid = validateAsRoadGraph
            ? IsValidRoadGraphSpawnPosition(candidate)
            : IsValidSpawnPosition(candidate);
        if (!isValid)
        {
            return false;
        }

        if (enforceMinDistance && Vector3.Distance(referencePoint, candidate) < minDistanceBetweenPoints)
        {
            return false;
        }

        return true;
    }

    private List<int> BuildShuffledSpawnIndices()
    {
        List<int> indices = new List<int>(availableSpawnPoints.Count);
        for (int i = 0; i < availableSpawnPoints.Count; i++)
        {
            indices.Add(i);
        }

        for (int i = indices.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            int temp = indices[i];
            indices[i] = indices[swapIndex];
            indices[swapIndex] = temp;
        }

        return indices;
    }

    private bool TryGetReachabilityReferencePoint(Vector3 referencePoint, out Vector3 resolvedReferencePoint)
    {
        resolvedReferencePoint = referencePoint;
        if (!HasRoadGraphData())
        {
            return false;
        }

        if (!float.IsFinite(resolvedReferencePoint.x) ||
            !float.IsFinite(resolvedReferencePoint.y) ||
            !float.IsFinite(resolvedReferencePoint.z) ||
            resolvedReferencePoint.sqrMagnitude <= 0.0001f)
        {
            ResolvePlayerTransform();
            if (cachedPlayerTransform == null)
            {
                return false;
            }

            resolvedReferencePoint = cachedPlayerTransform.position;
        }

        return true;
    }

    // IsSpawnReachableOnRoadGraph removed: replaced by TryCheckSpawnConnectivity
    // which uses cheap BFS component membership instead of full A* pathfinding.

    private bool TryCheckSpawnConnectivity(Vector3 referencePoint, Vector3 candidatePoint, out bool sharesRoadComponent)
    {
        sharesRoadComponent = false;
        if (!HasRoadGraphData())
        {
            return false;
        }

        RoadGraph graph = roadGraphBuilder.RoadGraph;
        if (!TryEnsureRoadGraphConnectivityCache(graph))
        {
            return false;
        }

        var (referenceSegment, _, _, _) = graph.ProjectPointOnRoad(referencePoint);
        var (candidateSegment, _, _, _) = graph.ProjectPointOnRoad(candidatePoint);
        if (referenceSegment == null || candidateSegment == null)
        {
            return false;
        }

        if (!roadGraphComponentBySegmentId.TryGetValue(referenceSegment.id, out int referenceComponent) ||
            !roadGraphComponentBySegmentId.TryGetValue(candidateSegment.id, out int candidateComponent))
        {
            return false;
        }

        sharesRoadComponent = referenceComponent == candidateComponent;
        return true;
    }

    private bool TryEnsureRoadGraphConnectivityCache(RoadGraph graph)
    {
        if (graph == null || graph.roadSegments == null || graph.roadSegments.Count == 0)
        {
            roadGraphComponentBySegmentId.Clear();
            cachedRoadGraphConnectivitySource = null;
            cachedRoadGraphConnectivitySegmentCount = -1;
            return false;
        }

        if (ReferenceEquals(cachedRoadGraphConnectivitySource, graph) &&
            cachedRoadGraphConnectivitySegmentCount == graph.roadSegments.Count &&
            roadGraphComponentBySegmentId.Count > 0)
        {
            return true;
        }

        roadGraphComponentBySegmentId.Clear();
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

                AddAdjacentRoadSegment(adjacency, segment.id, connection.toSegment.id);
                AddAdjacentRoadSegment(adjacency, connection.toSegment.id, segment.id);
            }
        }

        int componentId = 0;
        Queue<int> pending = new Queue<int>();
        foreach (KeyValuePair<int, List<int>> pair in adjacency)
        {
            if (roadGraphComponentBySegmentId.ContainsKey(pair.Key))
            {
                continue;
            }

            pending.Enqueue(pair.Key);
            roadGraphComponentBySegmentId[pair.Key] = componentId;
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
                    if (roadGraphComponentBySegmentId.ContainsKey(neighbor))
                    {
                        continue;
                    }

                    roadGraphComponentBySegmentId[neighbor] = componentId;
                    pending.Enqueue(neighbor);
                }
            }

            componentId++;
        }

        cachedRoadGraphConnectivitySource = graph;
        cachedRoadGraphConnectivitySegmentCount = graph.roadSegments.Count;
        return roadGraphComponentBySegmentId.Count > 0;
    }

    private static void AddAdjacentRoadSegment(Dictionary<int, List<int>> adjacency, int fromSegmentId, int toSegmentId)
    {
        if (!adjacency.TryGetValue(fromSegmentId, out List<int> neighbors))
        {
            neighbors = new List<int>();
            adjacency[fromSegmentId] = neighbors;
        }

        if (!neighbors.Contains(toSegmentId))
        {
            neighbors.Add(toSegmentId);
        }
    }

    private bool TryGetEmergencyGroundSpawn(out Vector3 spawnPoint)
    {
        spawnPoint = Vector3.zero;

        if (cachedPlayerTransform == null)
        {
            ResolvePlayerTransform();
        }

        Vector3 center = cachedPlayerTransform != null ? cachedPlayerTransform.position : Vector3.zero;
        const int attempts = 80;
        for (int i = 0; i < attempts; i++)
        {
            Vector2 off = UnityEngine.Random.insideUnitCircle * UnityEngine.Random.Range(14f, 140f);
            Vector3 probe = new Vector3(center.x + off.x, raycastStartHeight, center.z + off.y);
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, raycastMaxDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            if (hit.collider == null || hit.collider.isTrigger || Vector3.Dot(hit.normal, Vector3.up) < 0.65f)
            {
                continue;
            }

            if (HasRoadMask && !IsRoadCollider(hit.collider))
            {
                continue;
            }

            Vector3 candidate = hit.point + Vector3.up * spawnHeight;
            if (!IsValidSpawnPosition(candidate))
            {
                continue;
            }

            spawnPoint = candidate;
            Debug.LogWarning($"[DeliveryManager] Emergency fallback spawn used at {spawnPoint}");
            return true;
        }

        if (TryFindRoadPointFromSceneColliders(out spawnPoint))
        {
            Debug.LogWarning($"[DeliveryManager] Road-collider fallback spawn used at {spawnPoint}");
            return true;
        }

        if (TryFindRoadPointFromRoadGraphWaypoints(out spawnPoint))
        {
            Debug.LogWarning($"[DeliveryManager] RoadGraph fallback spawn used at {spawnPoint}");
            return true;
        }

        return false;
    }

    private bool TryFindRoadPointFromSceneColliders(out Vector3 spawnPoint)
    {
        spawnPoint = Vector3.zero;
        Vector3 center = cachedPlayerTransform != null ? cachedPlayerTransform.position : Vector3.zero;
        return spawnEnvironment != null &&
               spawnEnvironment.TryFindRoadPointFromSceneColliders(center, spawnHeight, out spawnPoint);
    }

    private bool TryFindRoadPointFromRoadGraphWaypoints(out Vector3 spawnPoint)
    {
        spawnPoint = Vector3.zero;
        if (!HasRoadGraphData())
        {
            return false;
        }

        const int attempts = 220;
        for (int i = 0; i < attempts; i++)
        {
            var randomWp = roadGraphBuilder.RoadGraph.GetRandomWaypoint();
            if (randomWp.segment == null)
            {
                continue;
            }

            Waypoint wp = randomWp.segment.GetWaypoint(randomWp.waypointIndex);
            if (wp == null)
            {
                continue;
            }

            Vector3 rayStart = wp.position + Vector3.up * 60f;
            if (!Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 140f, ~0, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            if (hit.collider == null || hit.collider.isTrigger || Vector3.Dot(hit.normal, Vector3.up) < 0.65f)
            {
                continue;
            }

            bool acceptedRoad = HasRoadMask ? (IsRoadCollider(hit.collider) || IsLikelyRoadCollider(hit.collider)) : true;
            if (!acceptedRoad)
            {
                // Some scenes provide road graph waypoints but the raycast hits non-road ground.
                // Accept only when the hit remains close to the source waypoint.
                Vector3 wpDelta = hit.point - wp.position;
                wpDelta.y = 0f;
                if (wpDelta.sqrMagnitude > 12.25f)
                {
                    continue;
                }
            }

            Vector3 candidate = hit.point + Vector3.up * spawnHeight;
            if (hasTerrainBounds && !IsWithinAnyTerrainBounds(candidate))
            {
                continue;
            }

            if (IsSpawnSpaceBlocked(candidate))
            {
                continue;
            }

            spawnPoint = candidate;
            return true;
        }

        return false;
    }

    private bool IsLikelyRoadCollider(Collider collider)
    {
        return spawnEnvironment != null && spawnEnvironment.IsLikelyRoadCollider(collider);
    }

    private bool HasRoadGraphData()
    {
        return roadGraphBuilder != null &&
               roadGraphBuilder.RoadGraph != null &&
               roadGraphBuilder.RoadGraph.roadSegments != null &&
               roadGraphBuilder.RoadGraph.roadSegments.Count > 0;
    }

    private bool TryGenerateSpawnPointsFromRoadGraph()
    {
        if (!HasRoadGraphData())
        {
            return false;
        }

        int targetCount = Mathf.Max(1, numberOfAutoSpawnPoints);
        int maxAttempts = targetCount * 20;
        int attempts = 0;
        int failNoRoadGraphPoint = 0;
        int failOutOfTerrain = 0;
        int failBlocked = 0;
        int failValidation = 0;

        while (availableSpawnPoints.Count < targetCount && attempts < maxAttempts)
        {
            attempts++;
            if (!TryGetRandomRoadGraphPoint(out Vector3 point))
            {
                failNoRoadGraphPoint++;
                continue;
            }

            if (!IsWithinAnyTerrainBounds(point))
            {
                failOutOfTerrain++;
                continue;
            }

            if (IsSpawnSpaceBlocked(point))
            {
                failBlocked++;
                continue;
            }

            if (!IsValidRoadGraphSpawnPosition(point))
            {
                failValidation++;
                continue;
            }

            availableSpawnPoints.Add(point);
        }

        if (showDebugInfo)
        {
            Debug.Log($"[DeliveryManager] Road graph spawn generation: {availableSpawnPoints.Count}/{targetCount} valid points found in {attempts} attempts " +
                      $"(noPoint={failNoRoadGraphPoint}, outOfTerrain={failOutOfTerrain}, blocked={failBlocked}, invalid={failValidation})");
        }

        return availableSpawnPoints.Count > 0;
    }

    private bool TryGetRandomRoadGraphPoint(out Vector3 point)
    {
        point = Vector3.zero;

        if (!HasRoadGraphData())
        {
            return false;
        }

        var randomWp = roadGraphBuilder.RoadGraph.GetRandomWaypoint();
        if (randomWp.segment == null)
        {
            return false;
        }

        Waypoint wp = randomWp.segment.GetWaypoint(randomWp.waypointIndex);
        if (wp == null)
        {
            return false;
        }

        bool wantsSidewalkPoint = spawnOnBuildingFrontSidewalk && !HasRoadMask;
        if (wantsSidewalkPoint)
        {
            if (!TryGetBuildingFrontSidewalkPoint(wp.position, out point))
            {
                return false;
            }
        }
        else
        {
            Vector3 rayStart = wp.position + Vector3.up * 30f;
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider != null
                && !hit.collider.isTrigger
                && Vector3.Dot(hit.normal, Vector3.up) > 0.6f)
            {
                point = hit.point + Vector3.up * spawnHeight;
            }
            else
            {
                point = wp.position + Vector3.up * spawnHeight;
            }
        }

        return true;
    }

    private void EnsureRoadSurfaceMask()
    {
        if (spawnEnvironment == null)
        {
            spawnEnvironment = new DeliverySpawnEnvironment(buildingNameKeywords);
        }

        roadSurfaceMask = spawnEnvironment.EnsureRoadSurfaceMask(roadSurfaceMask);
    }

    private bool HasRoadMask => spawnEnvironment != null && spawnEnvironment.HasRoadMask;

    private bool IsRoadCollider(Collider collider)
    {
        return spawnEnvironment != null && spawnEnvironment.IsRoadCollider(collider);
    }

    private bool IsWithinAnyTerrainBounds(Vector3 worldPos)
    {
        return spawnEnvironment == null || spawnEnvironment.IsWithinAnyTerrainBounds(worldPos);
    }

    private bool IsSpawnSpaceBlocked(Vector3 position)
    {
        return spawnEnvironment != null && spawnEnvironment.IsSpawnSpaceBlocked(position);
    }

    /// <summary>
    /// Validation path for points sourced from the RoadGraph.
    /// These points are already road-biased, so avoid strict road-collider gating.
    /// </summary>
    private bool IsValidRoadGraphSpawnPosition(Vector3 position)
    {
        return spawnEnvironment != null && spawnEnvironment.IsValidRoadGraphSpawnPosition(position);
    }

    private bool IsValidSpawnPosition(Vector3 position)
    {
        return spawnEnvironment != null &&
               spawnEnvironment.IsValidSpawnPosition(position, spawnOnlyInNeighborhoods, neighborhoodCheckRadius);
    }

    private bool TryGetBuildingFrontSidewalkPoint(Vector3 roadAnchorPoint, out Vector3 sidewalkPoint)
    {
        sidewalkPoint = Vector3.zero;
        return spawnEnvironment != null &&
               spawnEnvironment.TryGetBuildingFrontSidewalkPoint(
                   roadAnchorPoint,
                   groundMask,
                   spawnHeight,
                   buildingSearchRadius,
                   buildingFrontOffset,
                   sidewalkValidationBuildingRadius,
                   sidewalkToRoadMaxDistance,
                   out sidewalkPoint);
    }

    private bool TryFindNearestBuildingCollider(Vector3 origin, float radius, out Collider nearestBuilding, out Vector3 nearestPoint)
    {
        nearestBuilding = null;
        nearestPoint = Vector3.zero;
        return nearestBuilding != null;
    }

    private bool TryGetClosestPointSafe(Collider collider, Vector3 origin, out Vector3 point)
    {
        point = Vector3.zero;
        return false;
    }

    private bool IsNearBuilding(Vector3 position, float radius)
    {
        return false;
    }

    private bool IsNearRoad(Vector3 position, float radius)
    {
        return false;
    }

    private bool IsBuildingCollider(Collider collider)
    {
        return false;
    }

    private bool IsBuildingObject(GameObject obj)
    {
        return false;
    }

    private string ResolveNeighborhoodName(Vector3 position)
    {
        return spawnEnvironment != null
            ? spawnEnvironment.ResolveNeighborhoodName(position, neighborhoodCheckRadius)
            : "Bilinmiyor";
    }

    /// <summary>
    /// Called when box is delivered
    /// </summary>
    public void OnBoxDelivered(DeliveryBox box)
    {
        CompleteDelivery();
    }

    /// <summary>
    /// Fallback completion path. Normal flow completes through QuestManager events.
    /// </summary>
    private void CompleteDelivery()
    {
        if (!isDeliveryActive && currentBox == null)
        {
            return;
        }

        if (useQuestSystem && currentDeliveryQuest != null && QuestManager.Instance != null && currentDeliveryQuest.Status == QuestStatus.Active)
        {
            CompleteDeliveryQuest();
            return;
        }

        FinalizeDeliveryLifecycle(true);
    }

    /// <summary>
    /// Create a delivery quest in the quest system
    /// </summary>
    private void CreateDeliveryQuest(Vector3 pickupPos, DeliveryMissionType missionType)
    {
        currentDeliveryQuest = DeliveryQuestFlow.CreateDeliveryQuest(
            pickupPos,
            missionType,
            currentMissionRewardMultiplier,
            BuildMissionConditionSummary(),
            cargoLibrary,
            showFloatingObjectiveMarkers ? deliveryIndicatorPrefab : null,
            deliveryRadius,
            showDebugInfo);
    }

    /// <summary>
    /// Populate the quest's delivery targets before the physical pickup occurs.
    /// </summary>
    private void UpdateQuestWithDelivery(List<Vector3> deliveryStops, List<string> deliveryNeighborhoods)
    {
        DeliveryQuestFlow.UpdateQuestWithDelivery(
            currentDeliveryQuest,
            deliveryStops,
            deliveryNeighborhoods,
            ResolveNeighborhoodName,
            showFloatingObjectiveMarkers ? deliveryIndicatorPrefab : null,
            deliveryRadius,
            showDebugInfo,
            null,
            Mathf.Max(0.05f, deliveryQuestTriggerRadius));
    }

    /// <summary>
    /// Complete the current delivery quest
    /// </summary>
    private void CompleteDeliveryQuest()
    {
        DeliveryQuestFlow.CompleteDeliveryQuest(currentDeliveryQuest);
    }

    private void HandleQuestCompleted(QuestData quest)
    {
        if (quest == null || currentDeliveryQuest == null || !ReferenceEquals(quest, currentDeliveryQuest))
        {
            return;
        }

        FinalizeDeliveryLifecycle(true);
    }

    private void HandleQuestFailed(QuestData quest)
    {
        if (quest == null || currentDeliveryQuest == null || !ReferenceEquals(quest, currentDeliveryQuest))
        {
            return;
        }

        HandleDeliveryFailure(QuestManager.Instance != null ? QuestManager.Instance.LastFailureReason : "Delivery failed");
    }

    private void HandleDeliveryFailure(string reason)
    {
        if (showDebugInfo)
        {
            Debug.LogWarning($"[DeliveryManager] Delivery failed: {reason}");
        }

        FinalizeDeliveryLifecycle(false);
    }

    private void FinalizeDeliveryLifecycle(bool success)
    {
        if (isFinishingDeliveryLifecycle)
        {
            return;
        }

        isFinishingDeliveryLifecycle = true;
        isDeliveryActive = false;

        if (success && (QuestManager.Instance == null || currentDeliveryQuest == null))
        {
            DeliveryMissionRules.GetMissionRewardValues(currentMissionType, currentMissionRewardMultiplier, out int baseReward, out int bonusReward);
            int fallbackReward = Mathf.Max(0, baseReward + bonusReward);
            if (fallbackReward > 0 && PlayerProgressionManager.Instance != null)
            {
                PlayerProgressionManager.Instance.AwardMoney(fallbackReward);
            }
        }

        if (success && currentDeliveryStopNeighborhoods.Count > 0)
        {
            lastCompletedDeliveryNeighborhoodName = currentDeliveryStopNeighborhoods[currentDeliveryStopNeighborhoods.Count - 1];
        }

        if (deliveryUI != null)
        {
            if (success)
            {
                deliveryUI.OnDeliveryComplete();
            }
            else
            {
                deliveryUI.OnDeliveryComplete();
            }
        }

        if (currentDeliveryIndicator != null)
        {
            currentDeliveryIndicator.SetActive(false);
            currentDeliveryIndicator = null;
        }

        if (currentDeliveryPreview != null)
        {
            currentDeliveryPreview.SetActive(false);
            currentDeliveryPreview = null;
        }

        if (currentBox != null)
        {
            currentBox.gameObject.SetActive(false);
            currentBox = null;
        }

        NavigationService.EnsureInstance()?.ClearObjective();

        currentDeliveryStops.Clear();
        currentDeliveryStopNeighborhoods.Clear();
        currentDeliveryStopIndex = 0;
        lastObservedQuestDeliveryIndex = -1;
        currentPickupNeighborhoodName = string.Empty;
        currentDeliveryNeighborhoodName = string.Empty;
        currentDeliveryQuest = null;

        hasPendingPhoneOffer = false;
        hasAcceptedMission = false;
        DeliveryPhoneMissionFlow.HideOffer(phoneMissionUI);

        CancelInvoke(nameof(SpawnNewBox));
        CancelInvoke(nameof(OfferMissionToPhone));

        if (requirePhoneMissionAccept)
        {
            ScheduleMissionOffer(success ? nextOfferDelayAfterSuccess : nextOfferDelayAfterFailure);
        }
        else
        {
            Invoke(nameof(SpawnNewBox), success ? 2f : 2.5f);
        }
        isFinishingDeliveryLifecycle = false;
    }

    private DeliveryBox AcquireDeliveryBox(Vector3 spawnPos, Quaternion rotation)
    {
        if (boxPrefab == null)
        {
            return null;
        }

        if (pooledBox == null)
        {
            GameObject boxObj = Instantiate(boxPrefab, spawnPos, rotation);
            pooledBox = boxObj.GetComponent<DeliveryBox>();
            if (pooledBox == null)
            {
                pooledBox = boxObj.AddComponent<DeliveryBox>();
            }
        }

        GameObject pooledBoxObject = pooledBox.gameObject;
        EnsureDeliveryBoxComponents(pooledBoxObject);
        pooledBox.PrepareForSpawn(this, spawnPos, rotation);
        return pooledBox;
    }

    private static void EnsureDeliveryBoxComponents(GameObject boxObj)
    {
        Rigidbody boxRb = boxObj.GetComponent<Rigidbody>();
        if (boxRb == null)
        {
            boxRb = boxObj.AddComponent<Rigidbody>();
        }
        boxRb.mass = 5f;
        boxRb.linearDamping = 0.5f;
        boxRb.angularDamping = 0.5f;
        boxRb.isKinematic = true;

        Collider[] existingColliders = boxObj.GetComponentsInChildren<Collider>();
        bool hasMainCollider = false;
        bool hasTriggerCollider = false;

        foreach (Collider col in existingColliders)
        {
            if (col.isTrigger)
            {
                hasTriggerCollider = true;
            }
            else
            {
                hasMainCollider = true;
            }
        }

        if (!hasMainCollider)
        {
            BoxCollider collider = boxObj.AddComponent<BoxCollider>();
            collider.isTrigger = false;
            collider.size = new Vector3(1f, 1f, 1f);
        }

        if (!hasTriggerCollider)
        {
            BoxCollider triggerCollider = boxObj.AddComponent<BoxCollider>();
            triggerCollider.isTrigger = true;
            triggerCollider.center = Vector3.zero;
            triggerCollider.size = new Vector3(3f, 3f, 3f);
        }
    }

    private GameObject AcquireDeliveryIndicatorFromPrefab()
    {
        if (deliveryIndicatorPrefab == null)
        {
            return null;
        }

        if (pooledDeliveryIndicator == null)
        {
            pooledDeliveryIndicator = Instantiate(deliveryIndicatorPrefab, currentDeliveryPoint + Vector3.up * 2f, Quaternion.identity);
            Collider[] indicatorColliders = pooledDeliveryIndicator.GetComponentsInChildren<Collider>(true);
            foreach (Collider indicatorCollider in indicatorColliders)
            {
                indicatorCollider.enabled = false;
            }
            pooledDeliveryIndicator.SetActive(false);
        }

        return pooledDeliveryIndicator;
    }

    private void SyncDeliveryTargetFromQuestProgress()
    {
        if (currentDeliveryQuest == null || currentDeliveryStops.Count == 0)
        {
            return;
        }

        int questIndex = Mathf.Clamp(currentDeliveryQuest.CurrentDeliveryIndex, 0, currentDeliveryStops.Count - 1);
        if (questIndex == lastObservedQuestDeliveryIndex)
        {
            return;
        }

        lastObservedQuestDeliveryIndex = questIndex;
        currentDeliveryStopIndex = questIndex;
        currentDeliveryPoint = currentDeliveryStops[currentDeliveryStopIndex];
        currentDeliveryNeighborhoodName = currentDeliveryStopNeighborhoods[currentDeliveryStopIndex];
        currentDeliveryParkingRotation = ResolveDeliveryParkingRotation(currentDeliveryPoint);
        RefreshDeliveryNeighborhoodLabel();

        if (currentDeliveryIndicator != null)
        {
            currentDeliveryIndicator.transform.position = currentDeliveryPoint + Vector3.up * 2f;
        }

        if (currentDeliveryPreview != null)
        {
            currentDeliveryPreview.transform.SetPositionAndRotation(currentDeliveryPoint, currentDeliveryParkingRotation);
            UpdateDeliveryParkingHologramPose();
        }

        if (currentDeliveryQuest != null)
        {
            currentDeliveryQuest.QuestDescription = BuildDeliveryObjectiveDescription(currentDeliveryStopIndex);
            QuestManager.Instance?.OnQuestUpdated?.Invoke(currentDeliveryQuest);
        }

        NavigationService.EnsureInstance()?.SetObjective(new NavigationObjective(ObjectiveType.Delivery, currentDeliveryPoint, currentDeliveryStopIndex, currentDeliveryStops.Count));
    }

    private void RefreshDeliveryNeighborhoodLabel()
    {
        if (currentDeliveryStops.Count == 0 || currentDeliveryStopIndex < 0 || currentDeliveryStopIndex >= currentDeliveryStops.Count)
        {
            return;
        }

        string resolvedNeighborhood = ResolveNeighborhoodName(currentDeliveryPoint);
        if (string.IsNullOrWhiteSpace(resolvedNeighborhood) || resolvedNeighborhood == "Bilinmiyor")
        {
            return;
        }

        currentDeliveryNeighborhoodName = resolvedNeighborhood;
        if (currentDeliveryStopIndex < currentDeliveryStopNeighborhoods.Count)
        {
            currentDeliveryStopNeighborhoods[currentDeliveryStopIndex] = resolvedNeighborhood;
        }
    }

    private void EnsurePhoneMissionUI()
    {
        phoneMissionUI = DeliveryPhoneMissionFlow.EnsurePhoneMissionUI(
            gameObject,
            phoneMissionUI,
            requirePhoneMissionAccept,
            HandlePhoneMissionAccepted,
            HandlePhoneMissionRejected);
    }

    private void ScheduleMissionOffer(float delaySeconds)
    {
        if (!requirePhoneMissionAccept)
        {
            return;
        }

        CancelInvoke(nameof(OfferMissionToPhone));
        float safeDelay = Mathf.Max(0.05f, delaySeconds);
        Invoke(nameof(OfferMissionToPhone), safeDelay);
    }

    private void OfferMissionToPhone()
    {
        EnsurePhoneMissionUI();

        pendingMissionType = PickMissionType();
        EvaluateMissionConditions(out pendingMissionRewardMultiplier, out pendingRushHourBonus, out pendingNightBonus, out pendingRainRiskBonus);
        hasPendingPhoneOffer = DeliveryPhoneMissionFlow.TryShowOffer(
            phoneMissionUI,
            requirePhoneMissionAccept,
            hasPendingPhoneOffer,
            isDeliveryActive,
            currentBox,
            isFinishingDeliveryLifecycle,
            pendingMissionType,
            pendingMissionRewardMultiplier,
            pendingRushHourBonus,
            pendingNightBonus,
            pendingRainRiskBonus,
            multiStopMinStops,
            multiStopMaxStops);

        if (!hasPendingPhoneOffer)
        {
            hasAcceptedMission = false;
        }
    }

    private void HandlePhoneMissionAccepted()
    {
        if (!hasPendingPhoneOffer || isDeliveryActive || currentBox != null)
        {
            return;
        }

        hasPendingPhoneOffer = false;
        hasAcceptedMission = true;
        CancelInvoke(nameof(OfferMissionToPhone));
        DeliveryPhoneMissionFlow.HideOffer(phoneMissionUI);

        PrepareMissionSettingsForSpawn(
            pendingMissionType,
            pendingMissionRewardMultiplier,
            pendingRushHourBonus,
            pendingNightBonus,
            pendingRainRiskBonus);
        SpawnNewBox();
    }

    private void HandlePhoneMissionRejected()
    {
        if (!hasPendingPhoneOffer)
        {
            return;
        }

        hasPendingPhoneOffer = false;
        hasAcceptedMission = false;
        DeliveryPhoneMissionFlow.HideOffer(phoneMissionUI);

        ScheduleMissionOffer(rejectedOfferRetryDelay);
    }

    private void PrepareMissionSettingsForSpawn(
        DeliveryMissionType missionType,
        float rewardMultiplier,
        bool rushHourBonus,
        bool nightBonus,
        bool rainRiskBonus)
    {
        currentMissionType = missionType;
        currentMissionRewardMultiplier = Mathf.Max(1f, rewardMultiplier);
        hasRushHourBonus = rushHourBonus;
        hasNightBonus = nightBonus;
        hasRainRiskBonus = rainRiskBonus;
        missionSettingsPreparedForSpawn = true;
    }

    private DeliveryMissionType PickMissionType()
    {
        return DeliveryMissionRules.PickMissionType(
            standardMissionWeight,
            timedMissionWeight,
            fragileMissionWeight,
            multiStopMissionWeight);
    }

    private void EvaluateMissionConditions()
    {
        EvaluateMissionConditions(out currentMissionRewardMultiplier, out hasRushHourBonus, out hasNightBonus, out hasRainRiskBonus);
    }

    private void EvaluateMissionConditions(
        out float rewardMultiplier,
        out bool rushHourBonus,
        out bool nightBonus,
        out bool rainRiskBonus)
    {
        DeliveryMissionConditionEvaluation evaluation = DeliveryMissionRules.EvaluateMissionConditions(
            rushHourRewardMultiplier,
            nightRewardMultiplier,
            rainyRiskRewardMultiplier);
        rewardMultiplier = evaluation.RewardMultiplier;
        rushHourBonus = evaluation.RushHourBonus;
        nightBonus = evaluation.NightBonus;
        rainRiskBonus = evaluation.RainRiskBonus;
    }

    private string BuildMissionConditionSummary()
    {
        return BuildMissionConditionSummary(currentMissionRewardMultiplier, hasRushHourBonus, hasNightBonus, hasRainRiskBonus);
    }

    private string BuildMissionConditionSummary(float rewardMultiplier, bool rushHourBonus, bool nightBonus, bool rainRiskBonus)
    {
        string summary = DeliveryMissionRules.BuildMissionConditionSummary(rewardMultiplier, rushHourBonus, nightBonus, rainRiskBonus);
        return string.IsNullOrEmpty(summary)
            ? string.Empty
            : LocalizationTable.CurrentLocale == LocalizationTable.EnglishLocale
                ? $"\nConditions: {summary}"
                : $"\nKoşullar: {summary}";
    }

    private string BuildDeliveryObjectiveDescription(int currentStopIndex)
    {
        return DeliveryQuestFlow.BuildDeliveryObjectiveDescription(
            currentStopIndex,
            currentDeliveryStops.Count,
            currentDeliveryPoint,
            currentDeliveryNeighborhoodName,
            BuildMissionConditionSummary());
    }

    private void OnDrawGizmos()
    {
        if (!showDebugInfo) return;

        // Draw spawn points
        Gizmos.color = Color.green;
        foreach (Vector3 point in availableSpawnPoints)
        {
            Gizmos.DrawWireSphere(point, 2f);
        }

        // Draw pickup point
        if (currentBox != null && !currentBox.IsPickedUp)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(currentPickupPoint, deliveryRadius);
        }

        // Draw delivery point
        if (isDeliveryActive)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(currentDeliveryPoint, deliveryRadius);
        }
    }
}
