using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeliveryDriver.Quest;
using DeliveryDriver.City;
using TrafficSystem;

/// <summary>
/// Manages delivery missions - spawning boxes and delivery points
/// Integrates with Quest system to show missions in UI
/// </summary>
public class DeliveryManager : MonoBehaviour
{
    private enum DeliveryMissionType
    {
        Standard,
        Timed,
        Fragile,
        MultiStop
    }

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
    [SerializeField] private bool showDebugInfo = true;

    [Header("MiniMap Objective Marker")]
    [SerializeField] private bool enableMiniMapObjectiveMarker = true;
    [SerializeField] private float miniMapMarkerHeight = 24f;
    [SerializeField] private Vector3 miniMapMarkerScale = new Vector3(4f, 10f, 4f);
    [SerializeField] private float miniMapMarkerSpinSpeed = 120f;
    [SerializeField] private float miniMapMarkerPulseSpeed = 3f;
    [SerializeField] private float miniMapMarkerPulseAmount = 0.2f;
    [SerializeField] private Color miniMapPickupMarkerColor = new Color(0.1f, 1f, 1f, 1f);
    [SerializeField] private Color miniMapDeliveryMarkerColor = new Color(1f, 0.9f, 0.05f, 1f);
    [SerializeField] private string miniMapMarkerLayerName = "MiniMapMarker";
    [SerializeField] private bool clampMiniMapMarkerToEdgeWhenOffscreen = true;
    [SerializeField, Range(0f, 0.45f)] private float miniMapMarkerEdgePadding = 0.08f;
    [SerializeField] private bool showMiniMapEdgeIndicator = true;
    [SerializeField] private float miniMapEdgeIndicatorSize = 22f;
    [SerializeField] private float miniMapEdgeIndicatorOffset = 0f;
    [SerializeField] private float miniMapEdgeIndicatorPulseSpeed = 4f;
    [SerializeField, Range(0f, 0.6f)] private float miniMapEdgeIndicatorPulseAmount = 0.2f;
    [SerializeField] private float miniMapMarkerFollowSmoothTime = 0.08f;
    [SerializeField] private float miniMapEdgeFollowSmoothTime = 0.12f;
    [SerializeField] private Sprite miniMapEdgeIndicatorSprite;

    [Header("Speedometer UI")]
    [SerializeField] private bool showSpeedometer = true;
    [SerializeField] private string speedometerLabel = "Hiz";
    [SerializeField] private Vector2 speedometerAnchoredPosition = new Vector2(-28f, 24f);
    [SerializeField] private int speedometerFontSize = 34;
    [SerializeField] private Color speedometerColor = Color.white;
    [SerializeField] private Vector2 speedometerPanelSize = new Vector2(280f, 78f);
    [SerializeField] private Color speedometerPanelBaseColor = new Color(0.07f, 0.08f, 0.1f, 0.72f);
    [SerializeField] private Color speedometerPanelEcoColor = new Color(0.08f, 0.2f, 0.12f, 0.78f);
    [SerializeField] private Color speedometerPanelCruiseColor = new Color(0.08f, 0.13f, 0.2f, 0.78f);
    [SerializeField] private Color speedometerPanelFastColor = new Color(0.24f, 0.18f, 0.06f, 0.8f);
    [SerializeField] private Color speedometerPanelMaxColor = new Color(0.3f, 0.08f, 0.08f, 0.84f);
    [SerializeField] private Color speedometerIconEcoColor = new Color(0.35f, 0.9f, 0.45f, 1f);
    [SerializeField] private Color speedometerIconCruiseColor = new Color(0.35f, 0.7f, 1f, 1f);
    [SerializeField] private Color speedometerIconFastColor = new Color(1f, 0.78f, 0.3f, 1f);
    [SerializeField] private Color speedometerIconMaxColor = new Color(1f, 0.32f, 0.32f, 1f);
    [SerializeField] private float cruiseThresholdKmh = 20f;
    [SerializeField] private float fastThresholdKmh = 60f;
    [SerializeField] private float maxThresholdKmh = 100f;
    [SerializeField] private Sprite speedIconEcoSprite;
    [SerializeField] private Sprite speedIconCruiseSprite;
    [SerializeField] private Sprite speedIconFastSprite;
    [SerializeField] private Sprite speedIconMaxSprite;

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
    private GameObject miniMapObjectiveMarker;
    private Material miniMapObjectiveMarkerMaterial;
    private int cachedMiniMapMarkerLayer = int.MinValue;
    private Camera cachedMiniMapCamera;
    private Canvas miniMapEdgeCanvas;
    private RectTransform miniMapEdgeIndicatorRect;
    private Image miniMapEdgeIndicatorImage;
    private Vector3 miniMapMarkerVelocity;
    private Vector2 miniMapEdgeIndicatorVelocity;
    private bool hasMiniMapMarkerPosition;
    private bool hasMiniMapEdgeIndicatorPosition;
    private RectTransform speedometerPanelRect;
    private Image speedometerPanelImage;
    private Image speedometerIconImage;
    private TextMeshProUGUI speedometerText;
    private Sprite _cachedFallbackSprite;
    private int cachedBuildingLayer = int.MinValue;
    private readonly Dictionary<int, bool> roadColliderGuessCache = new Dictionary<int, bool>(256);
    private bool usingRoadGraphSpawnCache;

    public bool IsDeliveryActive => isDeliveryActive;
    public bool HasBox => currentBox != null;
    public bool IsCarryingBox => currentBox != null && currentBox.IsPickedUp;
    public bool HasObjectivePoint => HasBox || isDeliveryActive;
    public Vector3 CurrentObjectivePoint => isDeliveryActive ? currentDeliveryPoint : currentPickupPoint;
    public Vector3 CurrentDeliveryPoint => currentDeliveryPoint;
    public Vector3 CurrentPickupPoint => currentPickupPoint;
    public string CurrentPickupNeighborhoodName => currentPickupNeighborhoodName;
    public string CurrentDeliveryNeighborhoodName => currentDeliveryNeighborhoodName;

    private void Awake()
    {
        EnsureRoadSurfaceMask();
    }

    private System.Collections.IEnumerator Start()
    {
        EnsureRoadSurfaceMask();
        deliveryUI = FindFirstObjectByType<DeliveryUI>();

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
        if (requirePhoneMissionAccept)
        {
            ScheduleMissionOffer(initialPhoneOfferDelay);
        }
        else
        {
            SpawnNewBox();
        }

        EnsureSpeedometerUI();
        SubscribeToQuestEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromQuestEvents();
        RemoveMiniMapObjectiveMarker();
        RemoveMiniMapEdgeIndicator();

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
        UpdateMiniMapObjectiveMarker();
        UpdateSpeedometerUI();

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

        // Wrong neighborhood delivery attempts are treated as hard failure for mission clarity.
        if (distance <= deliveryRadius * 1.1f && !IsPlayerInExpectedNeighborhood(player.position))
        {
            if (QuestManager.Instance != null && currentDeliveryQuest != null && currentDeliveryQuest.Status == QuestStatus.Active)
            {
                QuestManager.Instance.FailQuest(currentDeliveryQuest, "Wrong neighborhood delivery");
            }
            else
            {
                HandleDeliveryFailure("Wrong neighborhood delivery");
            }
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

        UpdateMiniMapObjectiveMarker();
    }

    private bool TryGetPickupSpawnPoint(out Vector3 spawnPoint)
    {
        ResolvePlayerTransform();

        int attempts = Mathf.Max(40, numberOfAutoSpawnPoints * 8);
        float minDistanceSqr = Mathf.Max(0f, minPickupSpawnDistanceFromPlayer) * Mathf.Max(0f, minPickupSpawnDistanceFromPlayer);

        for (int i = 0; i < attempts; i++)
        {
            if (!TryGetValidSpawnPoint(Vector3.zero, false, out Vector3 candidate))
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

        return TryGetValidSpawnPoint(Vector3.zero, false, out spawnPoint);
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

        UpdateMiniMapObjectiveMarker();
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
        Material indicatorMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        indicatorMat.color = new Color(1f, 0.8f, 0f, 1f); // Yellow-orange
        indicator.GetComponent<MeshRenderer>().material = indicatorMat;

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
        if (!HasRoadMask)
        {
            return false;
        }

        Collider[] sceneColliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        if (sceneColliders == null || sceneColliders.Length == 0)
        {
            return false;
        }

        const int samplesPerCollider = 4;
        foreach (Collider col in sceneColliders)
        {
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

        if (buildingNameKeywords == null || buildingNameKeywords.Length == 0)
        {
            return false;
        }

        string lowerName = obj.name.ToLowerInvariant();
        for (int i = 0; i < buildingNameKeywords.Length; i++)
        {
            string keyword = buildingNameKeywords[i];
            if (string.IsNullOrWhiteSpace(keyword))
            {
                continue;
            }

            if (lowerName.Contains(keyword.ToLowerInvariant()))
            {
                return true;
            }
        }

        return false;
    }

    private string ResolveNeighborhoodName(Vector3 position)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(position, neighborhoodCheckRadius, sharedOverlapBuffer, ~0, QueryTriggerInteraction.Collide);
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

        QuestType questType = ToQuestType(missionType);
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

        GetMissionRewardValues(missionType, currentMissionRewardMultiplier, out int baseReward, out int bonusReward);
        string missionLabel = GetMissionLabel(missionType);

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

        RemoveMiniMapObjectiveMarker();

        currentDeliveryStops.Clear();
        currentDeliveryStopNeighborhoods.Clear();
        currentDeliveryStopIndex = 0;
        lastObservedQuestDeliveryIndex = -1;
        currentPickupNeighborhoodName = string.Empty;
        currentDeliveryNeighborhoodName = string.Empty;
        currentDeliveryQuest = null;

        hasPendingPhoneOffer = false;
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

        UpdateMiniMapObjectiveMarker();
    }

    private void EnsureMiniMapObjectiveMarker()
    {
        if (miniMapObjectiveMarker != null)
        {
            return;
        }

        miniMapObjectiveMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        miniMapObjectiveMarker.name = "MiniMapObjectiveMarker";
        miniMapObjectiveMarker.transform.localScale = miniMapMarkerScale;
        int markerLayer = ResolveMiniMapMarkerLayer();
        if (markerLayer >= 0)
        {
            miniMapObjectiveMarker.layer = markerLayer;
        }

        Collider markerCollider = miniMapObjectiveMarker.GetComponent<Collider>();
        if (markerCollider != null)
        {
            Destroy(markerCollider);
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader != null)
        {
            miniMapObjectiveMarkerMaterial = new Material(shader);
            miniMapObjectiveMarker.GetComponent<MeshRenderer>().material = miniMapObjectiveMarkerMaterial;
        }
    }

    private enum SpeedometerTier
    {
        Eco,
        Cruise,
        Fast,
        Max
    }

    private void EnsureSpeedometerUI()
    {
        if (!showSpeedometer || speedometerText != null)
        {
            return;
        }

        Canvas targetCanvas = FindBestHudCanvas();
        if (targetCanvas == null)
        {
            GameObject canvasObject = new GameObject("GameplayHUDCanvas");
            targetCanvas = canvasObject.AddComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        GameObject panelObject = new GameObject("SpeedometerPanel");
        panelObject.transform.SetParent(targetCanvas.transform, false);
        speedometerPanelRect = panelObject.AddComponent<RectTransform>();
        speedometerPanelRect.anchorMin = new Vector2(1f, 0f);
        speedometerPanelRect.anchorMax = new Vector2(1f, 0f);
        speedometerPanelRect.pivot = new Vector2(1f, 0f);
        speedometerPanelRect.anchoredPosition = speedometerAnchoredPosition;
        speedometerPanelRect.sizeDelta = speedometerPanelSize;

        speedometerPanelImage = panelObject.AddComponent<Image>();
        speedometerPanelImage.color = speedometerPanelBaseColor;
        speedometerPanelImage.raycastTarget = false;
        speedometerPanelImage.sprite = GetFallbackSprite();
        speedometerPanelImage.type = Image.Type.Simple;

        GameObject iconObject = new GameObject("SpeedometerIcon");
        iconObject.transform.SetParent(panelObject.transform, false);
        RectTransform iconRect = iconObject.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(12f, 0f);
        iconRect.sizeDelta = new Vector2(42f, 42f);

        speedometerIconImage = iconObject.AddComponent<Image>();
        speedometerIconImage.raycastTarget = false;
        speedometerIconImage.sprite = speedIconEcoSprite != null ? speedIconEcoSprite : GetFallbackSprite();
        speedometerIconImage.color = speedometerIconEcoColor;
        speedometerIconImage.preserveAspect = true;

        GameObject speedTextObject = new GameObject("SpeedometerText");
        speedTextObject.transform.SetParent(panelObject.transform, false);
        RectTransform textRect = speedTextObject.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = new Vector2(62f, 6f);
        textRect.offsetMax = new Vector2(-14f, -6f);

        speedometerText = speedTextObject.AddComponent<TextMeshProUGUI>();
        speedometerText.fontSize = Mathf.Max(12, speedometerFontSize);
        speedometerText.color = speedometerColor;
        speedometerText.alignment = TextAlignmentOptions.MidlineRight;
        speedometerText.text = $"{speedometerLabel}: 0 km/h";
    }

    private void UpdateSpeedometerUI()
    {
        if (!showSpeedometer)
        {
            if (speedometerPanelRect != null)
            {
                speedometerPanelRect.gameObject.SetActive(false);
            }
            return;
        }

        if (speedometerText == null || speedometerPanelRect == null)
        {
            EnsureSpeedometerUI();
        }

        if (speedometerText == null || speedometerPanelRect == null)
        {
            return;
        }

        if (cachedPlayerTransform == null)
        {
            ResolvePlayerTransform();
        }

        if (cachedPlayerRigidbody == null && cachedPlayerTransform != null)
        {
            cachedPlayerRigidbody = cachedPlayerTransform.GetComponent<Rigidbody>();
        }

        float speedKmh = cachedPlayerRigidbody != null
            ? cachedPlayerRigidbody.linearVelocity.magnitude * 3.6f
            : 0f;

        SpeedometerTier tier = EvaluateSpeedometerTier(speedKmh);
        ApplySpeedometerTierVisual(tier);

        speedometerPanelRect.gameObject.SetActive(true);
        speedometerText.text = $"{speedometerLabel}: {speedKmh:0} km/h";
    }

    private SpeedometerTier EvaluateSpeedometerTier(float speedKmh)
    {
        if (speedKmh >= maxThresholdKmh)
        {
            return SpeedometerTier.Max;
        }

        if (speedKmh >= fastThresholdKmh)
        {
            return SpeedometerTier.Fast;
        }

        if (speedKmh >= cruiseThresholdKmh)
        {
            return SpeedometerTier.Cruise;
        }

        return SpeedometerTier.Eco;
    }

    private void ApplySpeedometerTierVisual(SpeedometerTier tier)
    {
        if (speedometerPanelImage == null || speedometerIconImage == null)
        {
            return;
        }

        Sprite fallback = GetFallbackSprite();
        Sprite selectedSprite = fallback;
        float fallbackFillAmount = 0.35f;
        switch (tier)
        {
            case SpeedometerTier.Max:
                speedometerPanelImage.color = speedometerPanelMaxColor;
                speedometerIconImage.color = speedometerIconMaxColor;
                selectedSprite = speedIconMaxSprite != null ? speedIconMaxSprite : fallback;
                fallbackFillAmount = 1f;
                break;
            case SpeedometerTier.Fast:
                speedometerPanelImage.color = speedometerPanelFastColor;
                speedometerIconImage.color = speedometerIconFastColor;
                selectedSprite = speedIconFastSprite != null ? speedIconFastSprite : fallback;
                fallbackFillAmount = 0.78f;
                break;
            case SpeedometerTier.Cruise:
                speedometerPanelImage.color = speedometerPanelCruiseColor;
                speedometerIconImage.color = speedometerIconCruiseColor;
                selectedSprite = speedIconCruiseSprite != null ? speedIconCruiseSprite : fallback;
                fallbackFillAmount = 0.58f;
                break;
            default:
                speedometerPanelImage.color = speedometerPanelEcoColor;
                speedometerIconImage.color = speedometerIconEcoColor;
                selectedSprite = speedIconEcoSprite != null ? speedIconEcoSprite : fallback;
                fallbackFillAmount = 0.35f;
                break;
        }

        speedometerIconImage.sprite = selectedSprite;
        bool isFallbackIcon = selectedSprite == fallback;
        if (isFallbackIcon)
        {
            speedometerIconImage.type = Image.Type.Filled;
            speedometerIconImage.fillMethod = Image.FillMethod.Radial360;
            speedometerIconImage.fillOrigin = 2;
            speedometerIconImage.fillAmount = fallbackFillAmount;
        }
        else
        {
            speedometerIconImage.type = Image.Type.Simple;
            speedometerIconImage.fillAmount = 1f;
        }
    }

    private Canvas FindBestHudCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null && canvases[i].isActiveAndEnabled && canvases[i].renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return canvases[i];
            }
        }

        return FindFirstObjectByType<Canvas>();
    }

    private Sprite GetFallbackSprite()
    {
        if (_cachedFallbackSprite != null) return _cachedFallbackSprite;

        Texture2D tex = new Texture2D(4, 4);
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        _cachedFallbackSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        return _cachedFallbackSprite;
    }

    private int ResolveMiniMapMarkerLayer()
    {
        if (cachedMiniMapMarkerLayer == int.MinValue)
        {
            cachedMiniMapMarkerLayer = LayerMask.NameToLayer(miniMapMarkerLayerName);
        }

        return cachedMiniMapMarkerLayer;
    }

    private void UpdateMiniMapObjectiveMarker()
    {
        if (!enableMiniMapObjectiveMarker)
        {
            RemoveMiniMapObjectiveMarker();
            return;
        }

        bool hasPickupTarget = currentBox != null && !currentBox.IsPickedUp;
        bool hasDeliveryTarget = isDeliveryActive;
        bool hasTarget = hasPickupTarget || hasDeliveryTarget;

        if (!hasTarget)
        {
            HideMiniMapEdgeIndicator();
            if (miniMapObjectiveMarker != null)
            {
                miniMapObjectiveMarker.SetActive(false);
            }
            hasMiniMapMarkerPosition = false;
            return;
        }

        EnsureMiniMapObjectiveMarker();
        if (miniMapObjectiveMarker == null)
        {
            return;
        }

        Vector3 targetPoint = hasDeliveryTarget
            ? currentDeliveryPoint
            : (currentBox != null ? currentBox.transform.position : currentPickupPoint);
        bool targetIsOffscreen = false;
        Vector3 markerPoint = GetMiniMapMarkerTargetPoint(targetPoint, out targetIsOffscreen);
        Color markerColor = hasDeliveryTarget ? miniMapDeliveryMarkerColor : miniMapPickupMarkerColor;
        if (targetIsOffscreen)
        {
            miniMapObjectiveMarker.SetActive(false);
            hasMiniMapMarkerPosition = false;
            if (showMiniMapEdgeIndicator)
            {
                UpdateMiniMapEdgeIndicator(targetPoint, markerColor);
            }
            else
            {
                HideMiniMapEdgeIndicator();
            }
        }
        else
        {
            miniMapObjectiveMarker.SetActive(true);
            Vector3 desiredMarkerPosition = markerPoint + Vector3.up * miniMapMarkerHeight;
            if (!hasMiniMapMarkerPosition)
            {
                miniMapObjectiveMarker.transform.position = desiredMarkerPosition;
                miniMapMarkerVelocity = Vector3.zero;
                hasMiniMapMarkerPosition = true;
            }
            else
            {
                float smoothTime = Mathf.Max(0.01f, miniMapMarkerFollowSmoothTime);
                miniMapObjectiveMarker.transform.position = Vector3.SmoothDamp(
                    miniMapObjectiveMarker.transform.position,
                    desiredMarkerPosition,
                    ref miniMapMarkerVelocity,
                    smoothTime);
            }
            float pulse = 1f + Mathf.Sin(Time.time * miniMapMarkerPulseSpeed) * miniMapMarkerPulseAmount;
            miniMapObjectiveMarker.transform.localScale = miniMapMarkerScale * pulse;
            miniMapObjectiveMarker.transform.Rotate(Vector3.up, miniMapMarkerSpinSpeed * Time.deltaTime, Space.World);
            HideMiniMapEdgeIndicator();
        }

        if (miniMapObjectiveMarkerMaterial != null)
        {
            miniMapObjectiveMarkerMaterial.color = markerColor;
        }
    }

    private Vector3 GetMiniMapMarkerTargetPoint(Vector3 worldTargetPoint, out bool targetIsOffscreen)
    {
        targetIsOffscreen = false;

        if (!clampMiniMapMarkerToEdgeWhenOffscreen)
        {
            return worldTargetPoint;
        }

        if (!TryGetMiniMapCamera(out Camera miniMapCamera))
        {
            return worldTargetPoint;
        }

        Vector3 viewportPoint = miniMapCamera.WorldToViewportPoint(worldTargetPoint);
        if (viewportPoint.z <= 0f)
        {
            return worldTargetPoint;
        }

        bool isOutsideViewport =
            viewportPoint.x < 0f || viewportPoint.x > 1f ||
            viewportPoint.y < 0f || viewportPoint.y > 1f;
        targetIsOffscreen = isOutsideViewport;

        if (!isOutsideViewport)
        {
            return worldTargetPoint;
        }

        float padding = Mathf.Clamp(miniMapMarkerEdgePadding, 0f, 0.45f);
        viewportPoint.x = Mathf.Clamp(viewportPoint.x, padding, 1f - padding);
        viewportPoint.y = Mathf.Clamp(viewportPoint.y, padding, 1f - padding);

        Vector3 edgePoint = miniMapCamera.ViewportToWorldPoint(viewportPoint);
        edgePoint.y = worldTargetPoint.y;
        return edgePoint;
    }

    private bool TryGetMiniMapCamera(out Camera miniMapCamera)
    {
        if (cachedMiniMapCamera == null)
        {
            GameObject miniMapCameraObject = GameObject.Find("MiniMapCamera");
            if (miniMapCameraObject != null)
            {
                cachedMiniMapCamera = miniMapCameraObject.GetComponent<Camera>();
            }
        }

        miniMapCamera = cachedMiniMapCamera;
        return miniMapCamera != null && miniMapCamera.gameObject.activeInHierarchy && miniMapCamera.enabled;
    }

    private void UpdateMiniMapEdgeIndicator(Vector3 worldTargetPoint, Color indicatorColor)
    {
        if (!TryGetMiniMapCamera(out Camera miniMapCamera))
        {
            HideMiniMapEdgeIndicator();
            return;
        }

        EnsureMiniMapEdgeIndicator();
        if (miniMapEdgeIndicatorRect == null || miniMapEdgeCanvas == null)
        {
            return;
        }

        Vector3 viewportPoint = miniMapCamera.WorldToViewportPoint(worldTargetPoint);
        if (viewportPoint.z < 0f)
        {
            viewportPoint.x = 1f - viewportPoint.x;
            viewportPoint.y = 1f - viewportPoint.y;
            viewportPoint.z = -viewportPoint.z;
        }

        Vector2 direction = new Vector2(viewportPoint.x - 0.5f, viewportPoint.y - 0.5f);
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector2.up;
        }
        direction.Normalize();

        Rect miniMapRect = miniMapCamera.rect;
        float rectCenterX = (miniMapRect.x + (miniMapRect.width * 0.5f)) * Screen.width;
        float rectCenterY = (miniMapRect.y + (miniMapRect.height * 0.5f)) * Screen.height;
        float rectHalfWidth = miniMapRect.width * Screen.width * 0.5f;
        float rectHalfHeight = miniMapRect.height * Screen.height * 0.5f;

        float inset = Mathf.Max(0f, miniMapEdgeIndicatorOffset);
        float usableHalfWidth = Mathf.Max(1f, rectHalfWidth - inset);
        float usableHalfHeight = Mathf.Max(1f, rectHalfHeight - inset);
        float tx = Mathf.Approximately(direction.x, 0f) ? float.PositiveInfinity : usableHalfWidth / Mathf.Abs(direction.x);
        float ty = Mathf.Approximately(direction.y, 0f) ? float.PositiveInfinity : usableHalfHeight / Mathf.Abs(direction.y);
        float t = Mathf.Min(tx, ty);
        Vector2 screenPoint = new Vector2(rectCenterX, rectCenterY) + direction * t;

        RectTransform canvasRect = miniMapEdgeCanvas.transform as RectTransform;
        if (canvasRect != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out Vector2 localPoint))
        {
            if (!hasMiniMapEdgeIndicatorPosition)
            {
                miniMapEdgeIndicatorRect.anchoredPosition = localPoint;
                miniMapEdgeIndicatorVelocity = Vector2.zero;
                hasMiniMapEdgeIndicatorPosition = true;
            }
            else
            {
                float smoothTime = Mathf.Max(0.01f, miniMapEdgeFollowSmoothTime);
                miniMapEdgeIndicatorRect.anchoredPosition = Vector2.SmoothDamp(
                    miniMapEdgeIndicatorRect.anchoredPosition,
                    localPoint,
                    ref miniMapEdgeIndicatorVelocity,
                    smoothTime);
            }
        }

        float edgePulse = 1f + Mathf.Sin(Time.time * miniMapEdgeIndicatorPulseSpeed) * Mathf.Clamp(miniMapEdgeIndicatorPulseAmount, 0f, 0.6f);
        float size = Mathf.Max(8f, miniMapEdgeIndicatorSize) * edgePulse;
        miniMapEdgeIndicatorRect.sizeDelta = new Vector2(size, size);
        miniMapEdgeIndicatorRect.localRotation = Quaternion.identity;

        if (miniMapEdgeIndicatorImage != null)
        {
            miniMapEdgeIndicatorImage.color = indicatorColor;
            miniMapEdgeIndicatorImage.enabled = true;
        }
    }

    private void EnsureMiniMapEdgeIndicator()
    {
        if (miniMapEdgeCanvas == null)
        {
            GameObject canvasObject = new GameObject("MiniMapEdgeIndicatorCanvas");
            miniMapEdgeCanvas = canvasObject.AddComponent<Canvas>();
            miniMapEdgeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            miniMapEdgeCanvas.sortingOrder = 1000;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        if (miniMapEdgeIndicatorRect == null)
        {
            GameObject indicatorObject = new GameObject("MiniMapEdgeIndicator");
            indicatorObject.transform.SetParent(miniMapEdgeCanvas.transform, false);
            miniMapEdgeIndicatorRect = indicatorObject.AddComponent<RectTransform>();
            miniMapEdgeIndicatorRect.anchorMin = new Vector2(0.5f, 0.5f);
            miniMapEdgeIndicatorRect.anchorMax = new Vector2(0.5f, 0.5f);
            miniMapEdgeIndicatorRect.pivot = new Vector2(0.5f, 0.5f);

            miniMapEdgeIndicatorImage = indicatorObject.AddComponent<Image>();
            miniMapEdgeIndicatorImage.raycastTarget = false;
            miniMapEdgeIndicatorImage.sprite = miniMapEdgeIndicatorSprite != null
                ? miniMapEdgeIndicatorSprite
                : GetFallbackSprite();
            miniMapEdgeIndicatorImage.preserveAspect = true;
        }
    }

    private void HideMiniMapEdgeIndicator()
    {
        if (miniMapEdgeIndicatorImage != null)
        {
            miniMapEdgeIndicatorImage.enabled = false;
        }
        hasMiniMapEdgeIndicatorPosition = false;
    }

    private void RemoveMiniMapEdgeIndicator()
    {
        if (miniMapEdgeIndicatorRect != null)
        {
            Destroy(miniMapEdgeIndicatorRect.gameObject);
            miniMapEdgeIndicatorRect = null;
            miniMapEdgeIndicatorImage = null;
        }

        if (miniMapEdgeCanvas != null)
        {
            Destroy(miniMapEdgeCanvas.gameObject);
            miniMapEdgeCanvas = null;
        }
    }

    private void RemoveMiniMapObjectiveMarker()
    {
        HideMiniMapEdgeIndicator();

        if (miniMapObjectiveMarker != null)
        {
            Destroy(miniMapObjectiveMarker);
            miniMapObjectiveMarker = null;
            hasMiniMapMarkerPosition = false;
        }

        if (miniMapObjectiveMarkerMaterial != null)
        {
            Destroy(miniMapObjectiveMarkerMaterial);
            miniMapObjectiveMarkerMaterial = null;
        }
    }

    private bool IsPlayerInExpectedNeighborhood(Vector3 playerPosition)
    {
        if (string.IsNullOrWhiteSpace(currentDeliveryNeighborhoodName) || currentDeliveryNeighborhoodName == "Bilinmiyor")
        {
            return true;
        }

        string playerNeighborhood = ResolveNeighborhoodName(playerPosition);
        if (string.IsNullOrWhiteSpace(playerNeighborhood) || playerNeighborhood == "Bilinmiyor")
        {
            return false;
        }

        return string.Equals(playerNeighborhood, currentDeliveryNeighborhoodName, StringComparison.OrdinalIgnoreCase);
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
        phoneMissionUI.HideOffer();
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
            Debug.LogWarning("[DeliveryManager] PhoneMissionUI not found. Falling back to auto spawn flow.");
            SpawnNewBox();
            return;
        }

        pendingMissionType = PickMissionType();
        EvaluateMissionConditions(out pendingMissionRewardMultiplier, out pendingRushHourBonus, out pendingNightBonus, out pendingRainRiskBonus);
        hasPendingPhoneOffer = true;

        phoneMissionUI.ShowOffer(
            GetMissionLabel(pendingMissionType),
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
        string modeText = missionType switch
        {
            DeliveryMissionType.Timed => "Sureli teslimat. Hedefe hizli ulasman gerekiyor.",
            DeliveryMissionType.Fragile => "Kirilgan kargo. Carpmalardan kacinarak tasimalisin.",
            DeliveryMissionType.MultiStop => $"Cok durakli rota. Genelde {Mathf.Max(2, multiStopMinStops)}-{Mathf.Max(multiStopMinStops, multiStopMaxStops)} teslim noktasi olur.",
            _ => "Standart paket teslimati."
        };

        string conditionSummary = BuildMissionConditionSummary(rewardMultiplier, rushHourBonus, nightBonus, rainRiskBonus);
        return $"Yeni gorev teklifi\n{modeText}{conditionSummary}\nKabul edersen gorev olusacak.";
    }

    private string BuildMissionRewardPreview(DeliveryMissionType missionType, float rewardMultiplier)
    {
        GetMissionRewardValues(missionType, rewardMultiplier, out int baseReward, out int bonusReward);
        return $"Odul: ${baseReward} (+Bonus ${bonusReward})";
    }

    private DeliveryMissionType PickMissionType()
    {
        int standard = Mathf.Max(0, standardMissionWeight);
        int timed = Mathf.Max(0, timedMissionWeight);
        int fragile = Mathf.Max(0, fragileMissionWeight);
        int multiStop = Mathf.Max(0, multiStopMissionWeight);
        int totalWeight = standard + timed + fragile + multiStop;
        if (totalWeight <= 0)
        {
            return DeliveryMissionType.Standard;
        }

        int roll = UnityEngine.Random.Range(0, totalWeight);
        if (roll < standard) return DeliveryMissionType.Standard;
        roll -= standard;
        if (roll < timed) return DeliveryMissionType.Timed;
        roll -= timed;
        if (roll < fragile) return DeliveryMissionType.Fragile;
        return DeliveryMissionType.MultiStop;
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
        int hour = DateTime.Now.Hour;
        rushHourBonus = (hour >= 7 && hour <= 9) || (hour >= 17 && hour <= 19);
        nightBonus = hour >= 22 || hour <= 5;
        rainRiskBonus = WeatherManager.Instance != null &&
                        WeatherManager.Instance.GetCurrentWeather() == WeatherCondition.Rain;

        rewardMultiplier = 1f;
        if (rushHourBonus)
        {
            rewardMultiplier *= Mathf.Max(1f, rushHourRewardMultiplier);
        }

        if (nightBonus)
        {
            rewardMultiplier *= Mathf.Max(1f, nightRewardMultiplier);
        }

        if (rainRiskBonus)
        {
            rewardMultiplier *= Mathf.Max(1f, rainyRiskRewardMultiplier);
        }
    }

    private string BuildMissionConditionSummary()
    {
        return BuildMissionConditionSummary(currentMissionRewardMultiplier, hasRushHourBonus, hasNightBonus, hasRainRiskBonus);
    }

    private string BuildMissionConditionSummary(float rewardMultiplier, bool rushHourBonus, bool nightBonus, bool rainRiskBonus)
    {
        List<string> tags = new List<string>();
        if (rushHourBonus) tags.Add("Rush Hour");
        if (nightBonus) tags.Add("Night");
        if (rainRiskBonus) tags.Add("Rain Risk");

        return tags.Count == 0
            ? string.Empty
            : $"\nConditions: {string.Join(", ", tags)} (x{Mathf.Max(1f, rewardMultiplier):F2} reward)";
    }

    private string GetMissionLabel(DeliveryMissionType missionType)
    {
        return missionType switch
        {
            DeliveryMissionType.Timed => "Timed Run",
            DeliveryMissionType.Fragile => "Fragile Cargo",
            DeliveryMissionType.MultiStop => "Multi-Stop Route",
            _ => "Package Delivery"
        };
    }

    private void GetMissionRewardValues(DeliveryMissionType missionType, float rewardMultiplier, out int baseReward, out int bonusReward)
    {
        int baseRaw = missionType switch
        {
            DeliveryMissionType.Timed => 150,
            DeliveryMissionType.Fragile => 175,
            DeliveryMissionType.MultiStop => 220,
            _ => 100
        };

        int bonusRaw = missionType switch
        {
            DeliveryMissionType.Timed => 95,
            DeliveryMissionType.Fragile => 110,
            DeliveryMissionType.MultiStop => 140,
            _ => 50
        };

        float safeMultiplier = Mathf.Max(1f, rewardMultiplier);
        baseReward = Mathf.RoundToInt(baseRaw * safeMultiplier);
        bonusReward = Mathf.RoundToInt(bonusRaw * safeMultiplier);
    }

    private QuestType ToQuestType(DeliveryMissionType missionType)
    {
        return missionType switch
        {
            DeliveryMissionType.Timed => QuestType.ExpressDelivery,
            DeliveryMissionType.Fragile => QuestType.FragileDelivery,
            DeliveryMissionType.MultiStop => QuestType.MultiStopDelivery,
            _ => QuestType.StandardDelivery
        };
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
