using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages delivery missions - spawning boxes and delivery points
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

    [Header("Delivery Settings")]
    [SerializeField] private float deliveryRadius = 5f;
    [SerializeField] private bool autoGenerateSpawnPoints = true;
    [SerializeField] private int numberOfAutoSpawnPoints = 10;
    [SerializeField] private Vector2 spawnAreaMin = new Vector2(-50, -50);
    [SerializeField] private Vector2 spawnAreaMax = new Vector2(50, 50);
    [SerializeField] private float raycastStartHeight = 300f;
    [SerializeField] private float raycastMaxDistance = 400f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    private DeliveryBox currentBox;
    private GameObject currentPickupIndicator;
    private GameObject currentDeliveryIndicator;
    private Vector3 currentDeliveryPoint;
    private bool isDeliveryActive = false;
    private List<Vector3> availableSpawnPoints = new List<Vector3>();
    private DeliveryUI deliveryUI;

    public bool IsDeliveryActive => isDeliveryActive;
    public Vector3 CurrentDeliveryPoint => currentDeliveryPoint;

    private void Start()
    {
        deliveryUI = FindFirstObjectByType<DeliveryUI>();
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
                    availableSpawnPoints.Add(point.position);
                }
            }
        }
        else
        {
            // Auto-generate spawn points
            for (int i = 0; i < numberOfAutoSpawnPoints; i++)
            {
                Vector3 randomPoint = GetRandomGroundPosition();
                availableSpawnPoints.Add(randomPoint);
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
                // Make sure it's not too steep and not a trigger
                if (Vector3.Dot(hit.normal, Vector3.up) > 0.7f && !hit.collider.isTrigger)
                {
                    Vector3 spawnPos = hit.point + Vector3.up * spawnHeight;

                    // Validate spawn position with sphere check (ensure there's space)
                    if (!Physics.CheckSphere(spawnPos, 1f, groundMask, QueryTriggerInteraction.Ignore))
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
                Debug.LogWarning($"[DeliveryManager] Using fallback spawn near player at {hit.point}");
                return hit.point + Vector3.up * spawnHeight;
            }
        }

        Debug.LogError("[DeliveryManager] Failed to find valid spawn position! Using fallback.");
        return new Vector3(0, 10f, 0);
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

        // Get random spawn point
        Vector3 spawnPos = availableSpawnPoints.Count > 0 ?
            availableSpawnPoints[Random.Range(0, availableSpawnPoints.Count)] :
            GetRandomGroundPosition();

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

        // Notify UI
        if (deliveryUI != null)
        {
            deliveryUI.OnBoxPickedUp(currentDeliveryPoint);
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
    /// Get delivery point far from pickup location
    /// </summary>
    private Vector3 GetDeliveryPoint(Vector3 pickupPoint)
    {
        Vector3 deliveryPoint;
        int maxAttempts = 20;
        int attempts = 0;

        do
        {
            deliveryPoint = availableSpawnPoints.Count > 0 ?
                availableSpawnPoints[Random.Range(0, availableSpawnPoints.Count)] :
                GetRandomGroundPosition();

            attempts++;
        }
        while (Vector3.Distance(pickupPoint, deliveryPoint) < minDistanceBetweenPoints && attempts < maxAttempts);

        return deliveryPoint;
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

        // Destroy box
        if (currentBox != null)
        {
            Destroy(currentBox.gameObject);
        }

        if (showDebugInfo)
        {
            Debug.Log("[DeliveryManager] Delivery completed! Spawning new box...");
        }

        // Spawn new box after delay
        Invoke(nameof(SpawnNewBox), 2f);
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

        // Draw delivery point
        if (isDeliveryActive)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(currentDeliveryPoint, deliveryRadius);
        }
    }
}
