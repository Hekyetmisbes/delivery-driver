using System.Collections.Generic;
using UnityEngine;
using DeliveryDriver.Quest;
using DeliveryDriver.Quest.UI;
using DeliveryDriver.City;
using TrafficSystem;

/// <summary>
/// Manages delivery missions - spawning boxes and delivery points
/// Integrates with Quest system to show missions in UI
/// </summary>
public class DeliveryManager : MonoBehaviour
{
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
    private bool isDeliveryActive = false;
    private List<Vector3> availableSpawnPoints = new List<Vector3>();
    private DeliveryUI deliveryUI;
    private QuestData currentDeliveryQuest;

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
    }

    private void Update()
    {
        // Check delivery proximity
        if (isDeliveryActive && currentBox != null && currentBox.IsPickedUp)
        {
            Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (player == null)
            {
                // Try to find CarController
                CarController car = FindFirstObjectByType<CarController>();
                if (car != null) player = car.transform;
            }

            if (player != null)
            {
                float distance = Vector3.Distance(player.position, currentDeliveryPoint);

                // Update UI with distance
                if (deliveryUI != null)
                {
                    deliveryUI.UpdateDistance(distance);
                }

                if (distance < deliveryRadius)
                {
                    CompleteDelivery();
                }
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
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            CarController car = FindFirstObjectByType<CarController>();
            if (car != null) player = car.gameObject;
        }

        if (player != null)
        {
            Vector3 playerPos = player.transform.position;
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
        Collider[] colliders = Physics.OverlapSphere(position, neighborhoodCheckRadius, ~0, QueryTriggerInteraction.Collide);

        foreach (Collider col in colliders)
        {
            // Check if this collider belongs to a NeighborhoodZone
            NeighborhoodZone zone = col.GetComponent<NeighborhoodZone>();
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

        // Create quest in quest system
        if (useQuestSystem)
        {
            CreateDeliveryQuest(spawnPos);
        }

        if (showDebugInfo)
        {
            Debug.Log($"[DeliveryManager] Spawned box at {spawnPos}");
        }
    }

    /// <summary>
    /// Called when player picks up the box
    /// </summary>
    public void OnBoxPickedUp(DeliveryBox box)
    {
        if (box != currentBox) return;

        // Generate delivery point (different from pickup)
        currentDeliveryPoint = GetDeliveryPoint(box.transform.position);
        currentDeliveryNeighborhoodName = ResolveNeighborhoodName(currentDeliveryPoint);
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
            UpdateQuestWithDelivery(currentDeliveryPoint);
        }

        // Notify UI
        if (deliveryUI != null)
        {
            deliveryUI.OnBoxPickedUp(currentDeliveryPoint, currentPickupNeighborhoodName, currentDeliveryNeighborhoodName);
        }

        if (showDebugInfo)
        {
            float distance = Vector3.Distance(box.transform.position, currentDeliveryPoint);
            Debug.Log($"[DeliveryManager] Delivery point set at {currentDeliveryPoint} (Distance: {distance:F1}m)");
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
    /// Get delivery point far from pickup location
    /// </summary>
    private Vector3 GetDeliveryPoint(Vector3 pickupPoint)
    {
        Vector3 deliveryPoint = pickupPoint;
        int maxAttempts = 20;
        int attempts = 0;

        while (attempts < maxAttempts)
        {
            if (TryGetValidSpawnPoint(pickupPoint, true, out Vector3 candidate))
            {
                deliveryPoint = candidate;
                if (Vector3.Distance(pickupPoint, deliveryPoint) >= minDistanceBetweenPoints)
                {
                    return deliveryPoint;
                }
            }

            attempts++;
        }

        if (TryGetValidSpawnPoint(pickupPoint, false, out Vector3 fallbackPoint))
        {
            return fallbackPoint;
        }

        return pickupPoint;
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
        Terrain[] terrains = Terrain.activeTerrains;
        if (terrains == null || terrains.Length == 0)
        {
            return true;
        }

        foreach (Terrain terrain in terrains)
        {
            if (terrain == null || terrain.terrainData == null)
            {
                continue;
            }

            Vector3 terrainMin = terrain.transform.position;
            Vector3 terrainMax = terrainMin + terrain.terrainData.size;
            if (worldPos.x >= terrainMin.x && worldPos.x <= terrainMax.x &&
                worldPos.z >= terrainMin.z && worldPos.z <= terrainMax.z)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsSpawnSpaceBlocked(Vector3 position)
    {
        const float checkRadius = 0.6f;
        Collider[] overlaps = Physics.OverlapSphere(position, checkRadius, ~0, QueryTriggerInteraction.Ignore);

        foreach (Collider col in overlaps)
        {
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

        return !IsSpawnSpaceBlocked(position);
    }

    private string ResolveNeighborhoodName(Vector3 position)
    {
        Collider[] colliders = Physics.OverlapSphere(position, neighborhoodCheckRadius, ~0, QueryTriggerInteraction.Collide);
        foreach (Collider col in colliders)
        {
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
    /// Complete current delivery and spawn new box
    /// </summary>
    private void CompleteDelivery()
    {
        if (!isDeliveryActive) return;

        isDeliveryActive = false;

        QuestData questToShow = currentDeliveryQuest;

        // Complete quest
        if (useQuestSystem)
        {
            CompleteDeliveryQuest();
        }

        // Notify UI
        if (deliveryUI != null)
        {
            deliveryUI.OnDeliveryComplete();
        }

        // Destroy indicators
        if (currentDeliveryIndicator != null)
        {
            Destroy(currentDeliveryIndicator);
        }

        // Destroy ghost box preview
        if (currentDeliveryPreview != null)
        {
            Destroy(currentDeliveryPreview);
        }

        // Destroy box
        if (currentBox != null)
        {
            Destroy(currentBox.gameObject);
        }

        // Show quest complete UI
        ShowQuestCompleteUI(questToShow);

        currentDeliveryQuest = null;
        currentPickupNeighborhoodName = "";
        currentDeliveryNeighborhoodName = "";

        if (showDebugInfo)
        {
            Debug.Log("[DeliveryManager] Delivery completed! Spawning new box...");
        }

        // Spawn new box after delay
        Invoke(nameof(SpawnNewBox), 2f);
    }

    /// <summary>
    /// Create a delivery quest in the quest system
    /// </summary>
    private void CreateDeliveryQuest(Vector3 pickupPos)
    {
        if (QuestManager.Instance == null)
        {
            Debug.LogWarning("[DeliveryManager] QuestManager not found! Quest will not be created.");
            return;
        }

        // Create quest data
        currentDeliveryQuest = new QuestData
        {
            QuestID = System.Guid.NewGuid().ToString(),
            QuestName = "Package Delivery",
            QuestDescription = $"Pick up package at {FormatCoordinates(pickupPos)}",
            QuestType = QuestType.StandardDelivery,
            Difficulty = QuestDifficulty.Easy,
            Status = QuestStatus.NotStarted,
            TimeLimit = 300f, // 5 minutes
            TimeRemaining = 300f,
            BaseReward = 100,
            BonusReward = 50,
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
    private void UpdateQuestWithDelivery(Vector3 deliveryPos)
    {
        if (currentDeliveryQuest == null) return;

        // Add delivery location
        QuestLocation deliveryLocation = new QuestLocation(
            deliveryPos,
            $"Delivery: {FormatCoordinates(deliveryPos)}",
            deliveryRadius
        );

        currentDeliveryQuest.DeliveryLocations.Add(deliveryLocation);
        currentDeliveryQuest.QuestDescription = $"Deliver package to {FormatCoordinates(deliveryPos)}";
        currentDeliveryQuest.Status = QuestStatus.Active;

        // Show marker
        deliveryLocation.VisualMarker = deliveryIndicatorPrefab;
        deliveryLocation.ShowMarker();

        if (showDebugInfo)
        {
            Debug.Log($"[DeliveryManager] Updated quest with delivery location: {FormatCoordinates(deliveryPos)}");
        }
    }

    /// <summary>
    /// Complete the current delivery quest
    /// </summary>
    private void CompleteDeliveryQuest()
    {
        if (currentDeliveryQuest == null || QuestManager.Instance == null) return;

        currentDeliveryQuest.Status = QuestStatus.Completed;
        QuestManager.Instance.CompleteQuest(currentDeliveryQuest);

        if (showDebugInfo)
        {
            Debug.Log($"[DeliveryManager] Completed delivery quest!");
        }

    }

    /// <summary>
    /// Show quest complete UI
    /// </summary>
    private void ShowQuestCompleteUI(QuestData quest)
    {
        // Try to find and use the quest complete UI
        if (QuestUIManager.Instance != null)
        {
            QuestCompleteUI questCompleteUI = FindFirstObjectByType<QuestCompleteUI>();
            if (questCompleteUI != null)
            {
                int reward = QuestManager.Instance != null ? QuestManager.Instance.LastCompletionReward : 0;
                questCompleteUI.ShowCompleteScreen(quest, reward);
            }
        }
        else
        {
            // Fallback: simple debug message
            Debug.Log("=== QUEST COMPLETED ===");
        }
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
