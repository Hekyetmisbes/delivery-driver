using System;
using System.Collections.Generic;
using UnityEngine;
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

    [Header("Quest Integration")]
    [SerializeField] private bool useQuestSystem = true;
    [SerializeField] private CargoLibrary cargoLibrary;

    [Header("Neighborhood Integration")]
    [SerializeField] private bool spawnOnlyInNeighborhoods = true;
    [SerializeField] private float neighborhoodCheckRadius = 2f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

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
    private List<Vector3> availableSpawnPoints = new List<Vector3>();
    private DeliveryUI deliveryUI;
    private QuestData currentDeliveryQuest;
    private Transform cachedPlayerTransform;
    private static Collider[] sharedOverlapBuffer = new Collider[32];
    private Bounds[] cachedTerrainBounds;
    private bool hasTerrainBounds;
    private bool isFinishingDeliveryLifecycle;

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

        GenerateSpawnPoints();
        SpawnNewBox();

        // Cache player and UI references after scene is ready
        ResolvePlayerTransform();
        CacheTerrainBounds();
        SubscribeToQuestEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromQuestEvents();
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

    private void ResolvePlayerTransform()
    {
        CarController car = FindFirstObjectByType<CarController>();
        if (car != null)
        {
            cachedPlayerTransform = car.transform;
            return;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            cachedPlayerTransform = playerObj.transform;
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
                if (showDebugInfo)
                {
                    Debug.Log($"[DeliveryManager] Generated {availableSpawnPoints.Count} road-graph spawn points");
                }
                return;
            }

            // Auto-generate spawn points
            for (int i = 0; i < numberOfAutoSpawnPoints; i++)
            {
                Vector3 randomPoint = GetRandomGroundPosition();
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
    /// Get random position on ground with proper ground detection
    /// </summary>
    private Vector3 GetRandomGroundPosition()
    {
        int maxAttempts = 30;
        for (int i = 0; i < maxAttempts; i++)
        {
            float x = Random.Range(spawnAreaMin.x, spawnAreaMax.x);
            float z = Random.Range(spawnAreaMin.y, spawnAreaMax.y);
            Vector3 position = new Vector3(x, raycastStartHeight, z);

            // Raycast down to find ground
            if (Physics.Raycast(position, Vector3.down, out RaycastHit hit, raycastMaxDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                // Spawn only on road colliders, never on buildings/other meshes.
                if (!IsRoadCollider(hit.collider))
                {
                    continue;
                }

                // Keep points inside terrain bounds.
                if (!IsWithinAnyTerrainBounds(hit.point))
                {
                    continue;
                }

                // Make sure it's not too steep and not a trigger
                if (Vector3.Dot(hit.normal, Vector3.up) > 0.7f && !hit.collider.isTrigger)
                {
                    Vector3 spawnPos = hit.point + Vector3.up * spawnHeight;

                    // Validate spawn position (ensure there's space and no blocking geometry)
                    if (!IsSpawnSpaceBlocked(spawnPos) && IsValidSpawnPosition(spawnPos))
                    {
                        if (showDebugInfo && i > 0)
                        {
                            Debug.Log($"[DeliveryManager] Found valid spawn point at {spawnPos} (attempt {i + 1})");
                        }
                        return spawnPos;
                    }
                }
            }
        }

        // Fallback - use player position if available
        if (cachedPlayerTransform == null)
            ResolvePlayerTransform();

        if (cachedPlayerTransform != null)
        {
            Vector3 playerPos = cachedPlayerTransform.position;
            Vector3 offset = Random.insideUnitCircle * 20f;
            Vector3 fallbackPos = new Vector3(playerPos.x + offset.x, raycastStartHeight, playerPos.z + offset.y);

            if (Physics.Raycast(fallbackPos, Vector3.down, out RaycastHit hit, raycastMaxDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 fallbackSpawn = hit.point + Vector3.up * spawnHeight;
                if (IsRoadCollider(hit.collider) && IsValidSpawnPosition(fallbackSpawn))
                {
                    if (showDebugInfo)
                    {
                        Debug.Log($"[DeliveryManager] Using fallback spawn near player at {hit.point}");
                    }
                    return fallbackSpawn;
                }
            }
        }

        Debug.LogError("[DeliveryManager] Failed to find valid spawn position! Using fallback.");
        return new Vector3(0, 10f, 0);
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
            return;
        }

        // Get a valid random spawn point
        if (!TryGetValidSpawnPoint(Vector3.zero, false, out Vector3 spawnPos))
        {
            Debug.LogError("[DeliveryManager] Could not find a valid road spawn position.");
            return;
        }

        // Spawn box with slight rotation variation
        Quaternion rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        GameObject boxObj = Instantiate(boxPrefab, spawnPos, rotation);

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

        // Ensure rigidbody exists
        Rigidbody boxRb = boxObj.GetComponent<Rigidbody>();
        if (boxRb == null)
        {
            boxRb = boxObj.AddComponent<Rigidbody>();
            boxRb.mass = 5f;
            boxRb.linearDamping = 0.5f;
            boxRb.angularDamping = 0.5f;
        }
        boxRb.isKinematic = true; // Start kinematic

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
        currentMissionType = PickMissionType();
        EvaluateMissionConditions();

        // Create quest in quest system
        if (useQuestSystem)
        {
            CreateDeliveryQuest(spawnPos, currentMissionType);
        }

        if (showDebugInfo)
        {
            Debug.Log($"[DeliveryManager] Spawned box at {spawnPos}. MissionType={currentMissionType}, Conditions x{currentMissionRewardMultiplier:F2}");
        }
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

        for (int i = 0; i < maxAttempts; i++)
        {
            Vector3 candidate = availableSpawnPoints.Count > 0
                ? availableSpawnPoints[Random.Range(0, availableSpawnPoints.Count)]
                : GetRandomGroundPosition();

            if (!IsValidSpawnPosition(candidate))
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

        spawnPoint = Vector3.zero;
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

        while (availableSpawnPoints.Count < targetCount && attempts < maxAttempts)
        {
            attempts++;
            if (!TryGetRandomRoadGraphPoint(out Vector3 point))
            {
                continue;
            }

            if (!IsWithinAnyTerrainBounds(point))
            {
                continue;
            }

            if (IsSpawnSpaceBlocked(point))
            {
                continue;
            }

            availableSpawnPoints.Add(point);
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

        point = wp.position + Vector3.up * spawnHeight;
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
    }

    private bool IsRoadCollider(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        return (roadSurfaceMask.value & (1 << collider.gameObject.layer)) != 0;
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
        int hitCount = Physics.OverlapSphereNonAlloc(position, checkRadius, sharedOverlapBuffer, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = sharedOverlapBuffer[i];
            if (col == null || col.isTrigger)
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

    private bool IsValidSpawnPosition(Vector3 position)
    {
        if (!IsWithinAnyTerrainBounds(position))
        {
            return false;
        }

        Vector3 rayOrigin = position + Vector3.up * 5f;
        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        bool requireRoadCollider = !(useRoadGraphSpawnPoints && HasRoadGraphData());
        if (requireRoadCollider && !IsRoadCollider(hit.collider))
        {
            return false;
        }

        if (spawnOnlyInNeighborhoods && !IsInsideNeighborhood(position))
        {
            return false;
        }

        return !IsSpawnSpaceBlocked(position);
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

        int baseReward = missionType switch
        {
            DeliveryMissionType.Timed => 150,
            DeliveryMissionType.Fragile => 175,
            DeliveryMissionType.MultiStop => 220,
            _ => 100
        };

        int bonusReward = missionType switch
        {
            DeliveryMissionType.Timed => 95,
            DeliveryMissionType.Fragile => 110,
            DeliveryMissionType.MultiStop => 140,
            _ => 50
        };

        baseReward = Mathf.RoundToInt(baseReward * currentMissionRewardMultiplier);
        bonusReward = Mathf.RoundToInt(bonusReward * currentMissionRewardMultiplier);

        string missionLabel = missionType switch
        {
            DeliveryMissionType.Timed => "Timed Run",
            DeliveryMissionType.Fragile => "Fragile Cargo",
            DeliveryMissionType.MultiStop => "Multi-Stop Route",
            _ => "Package Delivery"
        };

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

        currentDeliveryStops.Clear();
        currentDeliveryStopNeighborhoods.Clear();
        currentDeliveryStopIndex = 0;
        lastObservedQuestDeliveryIndex = -1;
        currentPickupNeighborhoodName = string.Empty;
        currentDeliveryNeighborhoodName = string.Empty;
        currentDeliveryQuest = null;

        CancelInvoke(nameof(SpawnNewBox));
        Invoke(nameof(SpawnNewBox), success ? 2f : 2.5f);
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
        int hour = DateTime.Now.Hour;
        hasRushHourBonus = (hour >= 7 && hour <= 9) || (hour >= 17 && hour <= 19);
        hasNightBonus = hour >= 22 || hour <= 5;
        hasRainRiskBonus = WeatherManager.Instance != null &&
                           WeatherManager.Instance.GetCurrentWeather() == WeatherCondition.Rain;

        currentMissionRewardMultiplier = 1f;
        if (hasRushHourBonus)
        {
            currentMissionRewardMultiplier *= Mathf.Max(1f, rushHourRewardMultiplier);
        }

        if (hasNightBonus)
        {
            currentMissionRewardMultiplier *= Mathf.Max(1f, nightRewardMultiplier);
        }

        if (hasRainRiskBonus)
        {
            currentMissionRewardMultiplier *= Mathf.Max(1f, rainyRiskRewardMultiplier);
        }
    }

    private string BuildMissionConditionSummary()
    {
        List<string> tags = new List<string>();
        if (hasRushHourBonus) tags.Add("Rush Hour");
        if (hasNightBonus) tags.Add("Night");
        if (hasRainRiskBonus) tags.Add("Rain Risk");

        return tags.Count == 0
            ? string.Empty
            : $"\nConditions: {string.Join(", ", tags)} (x{currentMissionRewardMultiplier:F2} reward)";
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
