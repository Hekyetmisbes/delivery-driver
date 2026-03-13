using System;
using System.Collections.Generic;
using UnityEngine;
using DeliveryDriver.Quest;
using DeliveryDriver.City;
using DeliveryDriver.Navigation;
using TrafficSystem;

/// <summary>
/// Manages delivery missions - spawning boxes and delivery points
/// Integrates with Quest system to show missions in UI
/// </summary>
public class DeliveryManager : MonoBehaviour
{
    private const float SpawnReachabilityTransferDistance = 24f;

    [Header("Prefabs")]
    [SerializeField] private GameObject boxPrefab;
    [SerializeField] private GameObject pickupIndicatorPrefab;
    [SerializeField] private GameObject deliveryIndicatorPrefab;

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

    private DeliveryBox currentBox;
    private GameObject currentPickupIndicator;
    private GameObject currentDeliveryIndicator;
    private GameObject currentDeliveryPreview; // Ghost box at delivery location
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
    private DeliveryUI deliveryUI;
    private QuestData currentDeliveryQuest;
    private Transform cachedPlayerTransform;
    private Rigidbody cachedPlayerRigidbody;
    private static Collider[] sharedOverlapBuffer = new Collider[32];
    private Bounds[] cachedTerrainBounds;
    private bool hasTerrainBounds;
    private bool isFinishingDeliveryLifecycle;
    private int cachedBuildingLayer = int.MinValue;
    private readonly Dictionary<int, bool> roadColliderGuessCache = new Dictionary<int, bool>(256);
    private bool usingRoadGraphSpawnCache;

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
        deliveryUI = FindFirstObjectByType<DeliveryUI>();

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

    private void CacheTerrainBounds()
    {
        Terrain[] terrains = Terrain.activeTerrains;
        if (terrains == null || terrains.Length == 0)
        {
            hasTerrainBounds = false;
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
        if (!HasRoadMask)
        {
            return false;
        }

        float[] searchRadii = { 6f, 10f, 16f, 24f, 36f };
        foreach (float radius in searchRadii)
        {
            int hitCount = Physics.OverlapSphereNonAlloc(center, radius, sharedOverlapBuffer, roadSurfaceMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Collider roadCol = sharedOverlapBuffer[i];
                if (roadCol == null || roadCol.isTrigger || !IsRoadCollider(roadCol))
                {
                    continue;
                }

                if (!TryGetClosestPointSafe(roadCol, center, out Vector3 closePoint))
                {
                    continue;
                }

                Vector3 rayStart = closePoint + Vector3.up * 20f;
                if (!Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                if (hit.collider == null || hit.collider.isTrigger || !IsRoadCollider(hit.collider))
                {
                    continue;
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

                roadPoint = candidate;
                return true;
            }
        }

        return false;
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
        // Find all colliders at this position
        int hitCount = Physics.OverlapSphereNonAlloc(position, neighborhoodCheckRadius, sharedOverlapBuffer, ~0, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = sharedOverlapBuffer[i];
            // Check if this collider belongs to a NeighborhoodZone
            NeighborhoodZone zone = col.GetComponent<NeighborhoodZone>();
            if (zone == null)
            {
                zone = col.GetComponentInParent<NeighborhoodZone>();
            }

            if (zone != null)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"[DeliveryManager] Position {position} is inside neighborhood: {zone.NeighborhoodName}");
                }
                return true;
            }
        }

        return false;
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

        // Spawn box with slight rotation variation
        Quaternion rotation = Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0);
        GameObject boxObj = Instantiate(boxPrefab, spawnPos, rotation);

        // Ensure rigidbody exists before adding DeliveryBox (avoids RequireComponent auto-add log spam).
        Rigidbody boxRb = boxObj.GetComponent<Rigidbody>();
        if (boxRb == null)
        {
            boxRb = boxObj.AddComponent<Rigidbody>();
        }
        boxRb.mass = 5f;
        boxRb.linearDamping = 0.5f;
        boxRb.angularDamping = 0.5f;
        boxRb.isKinematic = true; // Start kinematic

        currentBox = boxObj.GetComponent<DeliveryBox>();
        if (currentBox == null)
        {
            currentBox = boxObj.AddComponent<DeliveryBox>();
        }

        // Setup colliders
        Collider[] existingColliders = boxObj.GetComponentsInChildren<Collider>();
        bool hasMainCollider = false;
        bool hasTriggerCollider = false;

        foreach (Collider col in existingColliders)
        {
            if (col.isTrigger) hasTriggerCollider = true;
            else hasMainCollider = true;
        }

        // Add main collider if missing
        if (!hasMainCollider)
        {
            BoxCollider collider = boxObj.AddComponent<BoxCollider>();
            collider.isTrigger = false;
            collider.size = new Vector3(1f, 1f, 1f);
        }

        // Add trigger collider for pickup if missing
        if (!hasTriggerCollider)
        {
            BoxCollider triggerCollider = boxObj.AddComponent<BoxCollider>();
            triggerCollider.isTrigger = true;
            triggerCollider.center = Vector3.zero;
            triggerCollider.size = new Vector3(3f, 3f, 3f); // Large pickup area
        }

        // Spawn pickup indicator
        if (pickupIndicatorPrefab != null)
        {
            currentPickupIndicator = Instantiate(pickupIndicatorPrefab, spawnPos + Vector3.up * 2f, Quaternion.identity);
            currentPickupIndicator.transform.SetParent(currentBox.transform);
        }

        // Store pickup point
        currentPickupPoint = spawnPos;
        currentPickupNeighborhoodName = ResolveNeighborhoodName(currentPickupPoint);
        currentDeliveryNeighborhoodName = "";
        currentDeliveryStops.Clear();
        currentDeliveryStopNeighborhoods.Clear();
        currentDeliveryStopIndex = 0;
        lastObservedQuestDeliveryIndex = -1;
        isDeliveryActive = false;

        // Create quest in quest system
        if (useQuestSystem)
        {
            CreateDeliveryQuest(spawnPos, currentMissionType);
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

        BuildDeliveryStops(box.transform.position);
        if (currentDeliveryStops.Count == 0)
        {
            HandleDeliveryFailure("No valid delivery location");
            return;
        }

        currentDeliveryStopIndex = 0;
        currentDeliveryPoint = currentDeliveryStops[currentDeliveryStopIndex];
        currentDeliveryNeighborhoodName = currentDeliveryStopNeighborhoods[currentDeliveryStopIndex];
        isDeliveryActive = true;

        // Spawn delivery indicator
        if (deliveryIndicatorPrefab != null)
        {
            currentDeliveryIndicator = Instantiate(deliveryIndicatorPrefab, currentDeliveryPoint + Vector3.up * 2f, Quaternion.identity);
        }
        else
        {
            // Create default delivery indicator
            CreateDefaultDeliveryIndicator();
        }

        // Create ghost box preview at delivery location
        CreateDeliveryPreview();

        // Update quest with delivery location
        if (useQuestSystem)
        {
            UpdateQuestWithDelivery(currentDeliveryStops, currentDeliveryStopNeighborhoods);
        }

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

    /// <summary>
    /// Create default delivery indicator if none assigned
    /// </summary>
    private void CreateDefaultDeliveryIndicator()
    {
        // Create a tall cylinder
        GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        indicator.name = "DeliveryIndicator";
        indicator.transform.position = currentDeliveryPoint + Vector3.up * 2f;
        indicator.transform.localScale = new Vector3(2f, 3f, 2f);

        // Remove collider
        Destroy(indicator.GetComponent<Collider>());

        // Create glowing material
        MeshRenderer indicatorRenderer = indicator.GetComponent<MeshRenderer>();
        Material indicatorMat = MinimapShaderHelper.CreateColorMaterial(new Color(1f, 0.8f, 0f, 1f), indicatorRenderer);
        if (indicatorMat != null && indicatorRenderer != null)
        {
            indicatorRenderer.material = indicatorMat;
        }

        // Add rotation script
        DeliveryIndicator script = indicator.AddComponent<DeliveryIndicator>();

        currentDeliveryIndicator = indicator;
    }

    /// <summary>
    /// Create ghost box preview at delivery location
    /// </summary>
    private void CreateDeliveryPreview()
    {
        if (boxPrefab == null || currentBox == null) return;

        // Instantiate ghost box
        currentDeliveryPreview = Instantiate(boxPrefab, currentDeliveryPoint, Quaternion.identity);
        currentDeliveryPreview.name = "DeliveryPreview_GhostBox";

        // Remove scripts and physics
        DeliveryBox previewBox = currentDeliveryPreview.GetComponent<DeliveryBox>();
        if (previewBox != null) Destroy(previewBox);

        Rigidbody previewRb = currentDeliveryPreview.GetComponent<Rigidbody>();
        if (previewRb != null) Destroy(previewRb);

        Collider[] previewColliders = currentDeliveryPreview.GetComponentsInChildren<Collider>();
        foreach (Collider col in previewColliders)
        {
            Destroy(col);
        }

        // Make it transparent/ghost-like
        MeshRenderer[] renderers = currentDeliveryPreview.GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer renderer in renderers)
        {
            foreach (Material mat in renderer.materials)
            {
                // Make transparent
                mat.SetFloat("_Surface", 1); // Transparent mode
                mat.SetFloat("_AlphaClip", 0);

                Color color = mat.color;
                color.a = 0.3f; // 30% opacity
                mat.color = color;

                // Enable transparency
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
            }
        }

        if (showDebugInfo)
        {
            Debug.Log($"[DeliveryManager] Created ghost box preview at {currentDeliveryPoint}");
        }
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

        const int maxAttempts = 70;
        for (int i = 0; i < maxAttempts; i++)
        {
            if (!TryGetValidSpawnPoint(referencePoint, requireMinDistance, out Vector3 candidate))
            {
                continue;
            }

            string candidateNeighborhood = ResolveNeighborhoodName(candidate);
            bool isExcludedNeighborhood = !string.IsNullOrWhiteSpace(candidateNeighborhood) &&
                                          excludedNeighborhoods != null &&
                                          excludedNeighborhoods.Contains(candidateNeighborhood);
            if (isExcludedNeighborhood && i < maxAttempts - 8)
            {
                continue;
            }

            point = candidate;
            neighborhood = candidateNeighborhood;
            return true;
        }

        return false;
    }

    private bool TryGetValidSpawnPoint(Vector3 referencePoint, bool enforceMinDistance, out Vector3 spawnPoint)
    {
        const int maxAttempts = 40;
        bool validateAsRoadGraph = usingRoadGraphSpawnCache && availableSpawnPoints.Count > 0;
        bool preferReachableSpawn = TryGetReachabilityReferencePoint(referencePoint, out Vector3 reachabilityReferencePoint);
        int reachabilityRejectCount = 0;

        for (int i = 0; i < maxAttempts; i++)
        {
            Vector3 candidate = availableSpawnPoints.Count > 0
                ? availableSpawnPoints[UnityEngine.Random.Range(0, availableSpawnPoints.Count)]
                : GetRandomGroundPosition(false);

            bool isValid = validateAsRoadGraph
                ? IsValidRoadGraphSpawnPosition(candidate)
                : IsValidSpawnPosition(candidate);
            if (!isValid)
            {
                continue;
            }

            if (enforceMinDistance && Vector3.Distance(referencePoint, candidate) < minDistanceBetweenPoints)
            {
                continue;
            }

            if (preferReachableSpawn && !IsSpawnReachableOnRoadGraph(reachabilityReferencePoint, candidate))
            {
                reachabilityRejectCount++;
                continue;
            }

            spawnPoint = candidate;
            return true;
        }

        if (preferReachableSpawn && reachabilityRejectCount > 0)
        {
            Debug.LogWarning($"[DeliveryManager] No graph-reachable spawn found after rejecting {reachabilityRejectCount} unreachable candidate(s). Falling back to legacy spawn selection.");
        }

        for (int i = 0; i < maxAttempts; i++)
        {
            Vector3 candidate = availableSpawnPoints.Count > 0
                ? availableSpawnPoints[UnityEngine.Random.Range(0, availableSpawnPoints.Count)]
                : GetRandomGroundPosition(false);

            bool isValid = validateAsRoadGraph
                ? IsValidRoadGraphSpawnPosition(candidate)
                : IsValidSpawnPosition(candidate);
            if (!isValid)
            {
                continue;
            }

            if (enforceMinDistance && Vector3.Distance(referencePoint, candidate) < minDistanceBetweenPoints)
            {
                continue;
            }

            spawnPoint = candidate;
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

    private bool IsSpawnReachableOnRoadGraph(Vector3 referencePoint, Vector3 candidatePoint)
    {
        if (!HasRoadGraphData())
        {
            return true;
        }

        List<Vector3> path = RoadGraphPathfinder.FindPath(
            roadGraphBuilder.RoadGraph,
            referencePoint,
            candidatePoint,
            SpawnReachabilityTransferDistance);

        return path != null && path.Count >= 2;
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

    private static readonly Collider[] roadSearchBuffer = new Collider[64];

    private bool TryFindRoadPointFromSceneColliders(out Vector3 spawnPoint)
    {
        spawnPoint = Vector3.zero;
        if (!HasRoadMask)
        {
            return false;
        }

        Vector3 center = cachedPlayerTransform != null ? cachedPlayerTransform.position : Vector3.zero;
        Vector3 halfExtents = new Vector3(150f, 100f, 150f);
        int hitCount = Physics.OverlapBoxNonAlloc(center, halfExtents, roadSearchBuffer, Quaternion.identity, roadSurfaceMask, QueryTriggerInteraction.Ignore);
        if (hitCount == 0)
        {
            return false;
        }

        const int samplesPerCollider = 4;
        for (int c = 0; c < hitCount; c++)
        {
            Collider col = roadSearchBuffer[c];
            if (col == null || col.isTrigger || !IsRoadCollider(col))
            {
                continue;
            }

            Bounds b = col.bounds;
            for (int i = 0; i < samplesPerCollider; i++)
            {
                float x = UnityEngine.Random.Range(b.min.x, b.max.x);
                float z = UnityEngine.Random.Range(b.min.z, b.max.z);
                Vector3 rayStart = new Vector3(x, b.max.y + 30f, z);
                if (!Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 120f, ~0, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                if (hit.collider == null || hit.collider.isTrigger || !IsRoadCollider(hit.collider))
                {
                    continue;
                }

                Vector3 candidate = hit.point + Vector3.up * spawnHeight;
                if (!IsValidSpawnPosition(candidate))
                {
                    continue;
                }

                spawnPoint = candidate;
                return true;
            }
        }

        return false;
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
        if (collider == null)
        {
            return false;
        }

        string n = collider.name;
        if (!string.IsNullOrEmpty(n))
        {
            string lower = n.ToLowerInvariant();
            if (lower.Contains("road") || lower.Contains("street") || lower.Contains("asphalt") || lower.Contains("highway"))
            {
                return true;
            }
        }

        string tag = collider.tag;
        if (!string.IsNullOrEmpty(tag))
        {
            string lowerTag = tag.ToLowerInvariant();
            if (lowerTag.Contains("road") || lowerTag.Contains("street"))
            {
                return true;
            }
        }

        Transform t = collider.transform;
        for (int i = 0; t != null && i < 6; i++)
        {
            string tn = t.name;
            if (!string.IsNullOrEmpty(tn))
            {
                string lower = tn.ToLowerInvariant();
                if (lower.Contains("road") || lower.Contains("street") || lower.Contains("asphalt") || lower.Contains("highway"))
                {
                    return true;
                }
            }
            t = t.parent;
        }

        return false;
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
        if (roadSurfaceMask.value != 0)
        {
            return;
        }

        int roadLayer = LayerMask.NameToLayer("Road");
        if (roadLayer >= 0)
        {
            roadSurfaceMask = 1 << roadLayer;
        }
        else
        {
            Debug.LogWarning("[DeliveryManager] No 'Road' layer found and roadSurfaceMask is not set. " +
                             "Road-based spawn constraints will be skipped.");
        }
    }

    private bool HasRoadMask => roadSurfaceMask.value != 0;

    private bool IsRoadCollider(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        if (HasRoadMask && (roadSurfaceMask.value & (1 << collider.gameObject.layer)) != 0)
        {
            return true;
        }

        int colliderId = collider.GetInstanceID();
        if (roadColliderGuessCache.TryGetValue(colliderId, out bool cachedIsRoad))
        {
            return cachedIsRoad;
        }

        bool inferredIsRoad = IsLikelyRoadCollider(collider);
        if (roadColliderGuessCache.Count >= 512)
        {
            roadColliderGuessCache.Clear();
        }
        roadColliderGuessCache[colliderId] = inferredIsRoad;
        return inferredIsRoad;
    }

    private bool IsWithinAnyTerrainBounds(Vector3 worldPos)
    {
        if (!hasTerrainBounds)
        {
            return true;
        }

        for (int i = 0; i < cachedTerrainBounds.Length; i++)
        {
            Bounds b = cachedTerrainBounds[i];
            float halfX = b.extents.x;
            float halfZ = b.extents.z;
            if (worldPos.x >= b.center.x - halfX && worldPos.x <= b.center.x + halfX &&
                worldPos.z >= b.center.z - halfZ && worldPos.z <= b.center.z + halfZ)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsSpawnSpaceBlocked(Vector3 position)
    {
        const float checkRadius = 0.6f;
        Collider supportCollider = null;
        Vector3 supportRayOrigin = position + Vector3.up * 2f;
        if (Physics.Raycast(supportRayOrigin, Vector3.down, out RaycastHit supportHit, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            supportCollider = supportHit.collider;
        }

        int hitCount = Physics.OverlapSphereNonAlloc(position, checkRadius, sharedOverlapBuffer, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = sharedOverlapBuffer[i];
            if (col == null || col.isTrigger)
            {
                continue;
            }

            if (supportCollider != null && col == supportCollider)
            {
                continue;
            }

            if (col is TerrainCollider || IsRoadCollider(col))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Validation path for points sourced from the RoadGraph.
    /// These points are already road-biased, so avoid strict road-collider gating.
    /// </summary>
    private bool IsValidRoadGraphSpawnPosition(Vector3 position)
    {
        if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z))
        {
            return false;
        }

        if (hasTerrainBounds && !IsWithinAnyTerrainBounds(position))
        {
            return false;
        }

        Vector3 rayOrigin = position + Vector3.up * 5f;
        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        if (hit.collider == null || hit.collider.isTrigger || Vector3.Dot(hit.normal, Vector3.up) < 0.6f)
        {
            return false;
        }

        return !IsSpawnSpaceBlocked(position);
    }

    private bool IsValidSpawnPosition(Vector3 position)
    {
        if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z))
        {
            return false;
        }

        if (hasTerrainBounds && !IsWithinAnyTerrainBounds(position))
        {
            return false;
        }

        // When road-only spawning is active, don't additionally gate by neighborhood;
        // this combination can make valid road spawns impossible in some scenes.
        if (spawnOnlyInNeighborhoods && !HasRoadMask && !IsInsideNeighborhood(position))
        {
            return false;
        }

        // Must have ground below
        Vector3 rayOrigin = position + Vector3.up * 5f;
        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        // Road-based checks only when road mask is configured
        if (HasRoadMask)
        {
            // Must be directly on road surface.
            if (!IsRoadCollider(hit.collider))
            {
                return false;
            }
        }

        return !IsSpawnSpaceBlocked(position);
    }

    private bool TryGetBuildingFrontSidewalkPoint(Vector3 roadAnchorPoint, out Vector3 sidewalkPoint)
    {
        sidewalkPoint = Vector3.zero;

        if (!TryFindNearestBuildingCollider(roadAnchorPoint, buildingSearchRadius, out Collider buildingCollider, out Vector3 buildingFrontPoint))
        {
            return false;
        }

        Vector3 directionToRoad = roadAnchorPoint - buildingFrontPoint;
        directionToRoad.y = 0f;
        if (directionToRoad.sqrMagnitude < 0.01f)
        {
            directionToRoad = roadAnchorPoint - buildingCollider.bounds.center;
            directionToRoad.y = 0f;
        }

        if (directionToRoad.sqrMagnitude < 0.01f)
        {
            return false;
        }

        directionToRoad.Normalize();
        Vector3 probePoint = buildingFrontPoint + directionToRoad * Mathf.Max(0.5f, buildingFrontOffset);

        Vector3 rayStart = probePoint + Vector3.up * 12f;
        if (!Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 40f, groundMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        if (hit.collider == null || hit.collider.isTrigger || (HasRoadMask && IsRoadCollider(hit.collider)))
        {
            return false;
        }

        Vector3 candidate = hit.point + Vector3.up * spawnHeight;
        if (!IsNearBuilding(candidate, sidewalkValidationBuildingRadius))
        {
            return false;
        }

        if (HasRoadMask && !IsNearRoad(candidate, sidewalkToRoadMaxDistance))
        {
            return false;
        }

        sidewalkPoint = candidate;
        return true;
    }

    private bool TryFindNearestBuildingCollider(Vector3 origin, float radius, out Collider nearestBuilding, out Vector3 nearestPoint)
    {
        nearestBuilding = null;
        nearestPoint = Vector3.zero;

        int hitCount = Physics.OverlapSphereNonAlloc(origin, radius, sharedOverlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        float bestSqrDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = sharedOverlapBuffer[i];
            if (!IsBuildingCollider(col))
            {
                continue;
            }

            if (!TryGetClosestPointSafe(col, origin, out Vector3 point))
            {
                continue;
            }

            Vector3 delta = point - origin;
            delta.y = 0f;
            float sqrDistance = delta.sqrMagnitude;

            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                nearestBuilding = col;
                nearestPoint = point;
            }
        }

        return nearestBuilding != null;
    }

    private bool TryGetClosestPointSafe(Collider collider, Vector3 origin, out Vector3 point)
    {
        point = Vector3.zero;
        if (collider == null)
        {
            return false;
        }

        // Non-convex MeshCollider can throw in Collider.ClosestPoint.
        if (collider is MeshCollider meshCollider && !meshCollider.convex)
        {
            point = collider.bounds.ClosestPoint(origin);
            return float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z);
        }

        try
        {
            point = collider.ClosestPoint(origin);
            return float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z);
        }
        catch (Exception)
        {
            point = collider.bounds.ClosestPoint(origin);
            return float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z);
        }
    }

    private bool IsNearBuilding(Vector3 position, float radius)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(position, Mathf.Max(0.2f, radius), sharedOverlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            if (IsBuildingCollider(sharedOverlapBuffer[i]))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsNearRoad(Vector3 position, float radius)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(position, Mathf.Max(0.2f, radius), sharedOverlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = sharedOverlapBuffer[i];
            if (col == null || col.isTrigger)
            {
                continue;
            }

            if (IsRoadCollider(col))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsBuildingCollider(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        Transform current = collider.transform;
        for (int depth = 0; current != null && depth < 6; depth++)
        {
            if (IsBuildingObject(current.gameObject))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private HashSet<string> buildingKeywordSet;
    private readonly Dictionary<int, bool> buildingObjectCache = new Dictionary<int, bool>(256);

    private bool IsBuildingObject(GameObject obj)
    {
        if (obj == null)
        {
            return false;
        }

        if (cachedBuildingLayer == int.MinValue)
        {
            cachedBuildingLayer = LayerMask.NameToLayer("MiniMapBuilding");
        }

        if (cachedBuildingLayer >= 0 && obj.layer == cachedBuildingLayer)
        {
            return true;
        }

        int instanceId = obj.GetInstanceID();
        if (buildingObjectCache.TryGetValue(instanceId, out bool cached))
        {
            return cached;
        }

        if (buildingKeywordSet == null)
        {
            buildingKeywordSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (buildingNameKeywords != null)
            {
                foreach (string kw in buildingNameKeywords)
                {
                    if (!string.IsNullOrWhiteSpace(kw))
                        buildingKeywordSet.Add(kw.ToLowerInvariant());
                }
            }
        }

        bool result = false;
        if (buildingKeywordSet.Count > 0)
        {
            string lowerName = obj.name.ToLowerInvariant();
            foreach (string keyword in buildingKeywordSet)
            {
                if (lowerName.Contains(keyword))
                {
                    result = true;
                    break;
                }
            }
        }

        if (buildingObjectCache.Count >= 256)
        {
            buildingObjectCache.Clear();
        }
        buildingObjectCache[instanceId] = result;
        return result;
    }

    private int cachedNeighborhoodLayer = int.MinValue;
    private LayerMask neighborhoodLayerMask;

    private string ResolveNeighborhoodName(Vector3 position)
    {
        if (cachedNeighborhoodLayer == int.MinValue)
        {
            cachedNeighborhoodLayer = LayerMask.NameToLayer("Neighborhood");
            neighborhoodLayerMask = cachedNeighborhoodLayer >= 0 ? (1 << cachedNeighborhoodLayer) : ~0;
        }

        int hitCount = Physics.OverlapSphereNonAlloc(position, neighborhoodCheckRadius, sharedOverlapBuffer, neighborhoodLayerMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = sharedOverlapBuffer[i];
            if (col == null) continue;

            NeighborhoodZone zone = col.GetComponent<NeighborhoodZone>();
            if (zone == null)
            {
                zone = col.GetComponentInParent<NeighborhoodZone>();
            }

            if (zone != null && !string.IsNullOrWhiteSpace(zone.NeighborhoodName))
            {
                return zone.NeighborhoodName;
            }
        }

        return "Bilinmiyor";
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
        if (QuestManager.Instance == null)
        {
            Debug.LogWarning("[DeliveryManager] QuestManager not found! Quest will not be created.");
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        QuestManager.Instance.SetDebugInfiniteTime(false);
#endif

        QuestType questType = DeliveryMissionRules.ToQuestType(missionType);
        QuestDifficulty difficulty = missionType switch
        {
            DeliveryMissionType.MultiStop => QuestDifficulty.Medium,
            DeliveryMissionType.Fragile => QuestDifficulty.Medium,
            DeliveryMissionType.Timed => QuestDifficulty.Medium,
            _ => QuestDifficulty.Easy
        };

        float baseTimeLimit = missionType switch
        {
            DeliveryMissionType.Timed => 180f,
            DeliveryMissionType.Fragile => 260f,
            DeliveryMissionType.MultiStop => 420f,
            _ => 300f
        };

        DeliveryMissionRules.GetMissionRewardValues(missionType, currentMissionRewardMultiplier, out int baseReward, out int bonusReward);
        string missionLabel = DeliveryMissionRules.GetMissionLabel(missionType);

        string conditionLine = BuildMissionConditionSummary();

        // Create quest data
        currentDeliveryQuest = new QuestData
        {
            QuestID = System.Guid.NewGuid().ToString(),
            QuestName = missionLabel,
            QuestDescription = $"Pick up package at {FormatCoordinates(pickupPos)}{conditionLine}",
            QuestType = questType,
            Difficulty = difficulty,
            Status = QuestStatus.NotStarted,
            TimeLimit = baseTimeLimit,
            TimeRemaining = baseTimeLimit,
            BaseReward = baseReward,
            BonusReward = bonusReward,
            PickupLocation = new QuestLocation(pickupPos, $"Pickup: {FormatCoordinates(pickupPos)}", deliveryRadius),
            DeliveryLocations = new List<QuestLocation>()
        };

        if (currentDeliveryQuest.PickupLocation != null)
        {
            currentDeliveryQuest.PickupLocation.VisualMarker = pickupIndicatorPrefab != null
                ? pickupIndicatorPrefab
                : deliveryIndicatorPrefab;
        }

        // Add cargo if available
        if (cargoLibrary != null)
        {
            CargoData randomCargo = cargoLibrary.GetRandomCargo();
            if (randomCargo != null)
            {
                currentDeliveryQuest.Cargo = randomCargo;
            }
        }

        if (currentDeliveryQuest.Cargo == null)
        {
            currentDeliveryQuest.Cargo = new CargoData("Package", 50f, false, "Delivery package");
        }

        if (missionType == DeliveryMissionType.Fragile)
        {
            currentDeliveryQuest.Cargo.IsFragile = true;
            currentDeliveryQuest.Cargo.CargoHealth = 100f;
        }

        // Add quest to QuestManager
        QuestManager.Instance.AddAvailableQuest(currentDeliveryQuest);
        QuestManager.Instance.StartQuest(currentDeliveryQuest);

        if (showDebugInfo)
        {
            Debug.Log($"[DeliveryManager] Created delivery quest: {currentDeliveryQuest.QuestName}");
        }
    }

    /// <summary>
    /// Update quest with delivery location
    /// </summary>
    private void UpdateQuestWithDelivery(List<Vector3> deliveryStops, List<string> deliveryNeighborhoods)
    {
        if (currentDeliveryQuest == null || deliveryStops == null || deliveryStops.Count == 0)
        {
            return;
        }

        currentDeliveryQuest.DeliveryLocations.Clear();
        float questTriggerRadius = Mathf.Max(2f, deliveryRadius * 0.65f);
        for (int i = 0; i < deliveryStops.Count; i++)
        {
            Vector3 stop = deliveryStops[i];
            string neighborhood = (deliveryNeighborhoods != null && i < deliveryNeighborhoods.Count)
                ? deliveryNeighborhoods[i]
                : ResolveNeighborhoodName(stop);
            QuestLocation deliveryLocation = new QuestLocation(
                stop,
                $"Delivery {i + 1}: {FormatCoordinates(stop)} ({neighborhood})",
                questTriggerRadius
            );
            deliveryLocation.VisualMarker = deliveryIndicatorPrefab;
            currentDeliveryQuest.DeliveryLocations.Add(deliveryLocation);
        }

        currentDeliveryQuest.QuestDescription = BuildDeliveryObjectiveDescription(0);
        currentDeliveryQuest.Status = QuestStatus.Active;
        currentDeliveryQuest.HasPickedUpCargo = true;
        currentDeliveryQuest.CurrentDeliveryIndex = 0;
        lastObservedQuestDeliveryIndex = 0;
        QuestManager.Instance?.OnQuestUpdated?.Invoke(currentDeliveryQuest);

        if (showDebugInfo)
        {
            Debug.Log($"[DeliveryManager] Updated quest with {deliveryStops.Count} delivery stop(s).");
        }
    }

    /// <summary>
    /// Complete the current delivery quest
    /// </summary>
    private void CompleteDeliveryQuest()
    {
        if (currentDeliveryQuest == null || QuestManager.Instance == null) return;

        if (currentDeliveryQuest.Status == QuestStatus.Active)
        {
            currentDeliveryQuest.Status = QuestStatus.Completed;
            QuestManager.Instance.CompleteQuest(currentDeliveryQuest);
        }
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
            Destroy(currentDeliveryIndicator);
            currentDeliveryIndicator = null;
        }

        if (currentDeliveryPreview != null)
        {
            Destroy(currentDeliveryPreview);
            currentDeliveryPreview = null;
        }

        if (currentBox != null)
        {
            Destroy(currentBox.gameObject);
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
        if (phoneMissionUI != null)
        {
            phoneMissionUI.HideOffer();
        }

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
        RefreshDeliveryNeighborhoodLabel();

        if (currentDeliveryIndicator != null)
        {
            currentDeliveryIndicator.transform.position = currentDeliveryPoint + Vector3.up * 2f;
        }

        if (currentDeliveryPreview != null)
        {
            currentDeliveryPreview.transform.position = currentDeliveryPoint;
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
        if (!requirePhoneMissionAccept)
        {
            return;
        }

        if (phoneMissionUI == null)
        {
            phoneMissionUI = FindFirstObjectByType<PhoneMissionUI>();
        }

        if (phoneMissionUI == null)
        {
            phoneMissionUI = gameObject.GetComponent<PhoneMissionUI>();
        }

        if (phoneMissionUI == null)
        {
            phoneMissionUI = gameObject.AddComponent<PhoneMissionUI>();
        }

        phoneMissionUI.BindCallbacks(HandlePhoneMissionAccepted, HandlePhoneMissionRejected);
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
        if (!requirePhoneMissionAccept || hasPendingPhoneOffer || isDeliveryActive || currentBox != null || isFinishingDeliveryLifecycle)
        {
            return;
        }

        EnsurePhoneMissionUI();
        if (phoneMissionUI == null)
        {
            Debug.LogError("[DeliveryManager] PhoneMissionUI not found. Mission offer cannot be shown.");
            hasAcceptedMission = false;
            return;
        }

        pendingMissionType = PickMissionType();
        EvaluateMissionConditions(out pendingMissionRewardMultiplier, out pendingRushHourBonus, out pendingNightBonus, out pendingRainRiskBonus);
        hasPendingPhoneOffer = true;

        phoneMissionUI.ShowOffer(
            DeliveryMissionRules.GetMissionLabel(pendingMissionType),
            BuildMissionOfferBody(pendingMissionType, pendingMissionRewardMultiplier, pendingRushHourBonus, pendingNightBonus, pendingRainRiskBonus),
            BuildMissionRewardPreview(pendingMissionType, pendingMissionRewardMultiplier));
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
        if (phoneMissionUI != null)
        {
            phoneMissionUI.HideOffer();
        }

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
        if (phoneMissionUI != null)
        {
            phoneMissionUI.HideOffer();
        }

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

    private string BuildMissionOfferBody(
        DeliveryMissionType missionType,
        float rewardMultiplier,
        bool rushHourBonus,
        bool nightBonus,
        bool rainRiskBonus)
    {
        string body = DeliveryMissionRules.BuildMissionOfferBody(
            missionType,
            rewardMultiplier,
            rushHourBonus,
            nightBonus,
            rainRiskBonus,
            multiStopMinStops,
            multiStopMaxStops);
        return $"Yeni gorev teklifi\n{body}\nKabul edersen gorev olusacak.";
    }

    private string BuildMissionRewardPreview(DeliveryMissionType missionType, float rewardMultiplier)
    {
        return DeliveryMissionRules.BuildMissionRewardPreview(missionType, rewardMultiplier);
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
            : $"\nConditions: {summary}";
    }

    private string BuildDeliveryObjectiveDescription(int currentStopIndex)
    {
        int totalStops = Mathf.Max(1, currentDeliveryStops.Count);
        int shownIndex = Mathf.Clamp(currentStopIndex + 1, 1, totalStops);
        string target = FormatCoordinates(currentDeliveryPoint);
        string neighborhood = string.IsNullOrWhiteSpace(currentDeliveryNeighborhoodName) ? "Bilinmiyor" : currentDeliveryNeighborhoodName;
        return $"Deliver package to stop {shownIndex}/{totalStops} at {target} ({neighborhood}){BuildMissionConditionSummary()}";
    }

    /// <summary>
    /// Format coordinates for display
    /// </summary>
    private string FormatCoordinates(Vector3 position)
    {
        return $"({position.x:F0}, {position.z:F0})";
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
