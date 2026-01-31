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
    [SerializeField] private float spawnHeight = 1f;
    [SerializeField] private float minDistanceBetweenPoints = 20f;

    [Header("Delivery Settings")]
    [SerializeField] private float deliveryRadius = 5f;
    [SerializeField] private bool autoGenerateSpawnPoints = true;
    [SerializeField] private int numberOfAutoSpawnPoints = 10;
    [SerializeField] private Vector2 spawnAreaMin = new Vector2(-50, -50);
    [SerializeField] private Vector2 spawnAreaMax = new Vector2(50, 50);

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    private DeliveryBox currentBox;
    private GameObject currentPickupIndicator;
    private GameObject currentDeliveryIndicator;
    private Vector3 currentDeliveryPoint;
    private bool isDeliveryActive = false;
    private List<Vector3> availableSpawnPoints = new List<Vector3>();

    private void Start()
    {
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
    /// Get random position on ground
    /// </summary>
    private Vector3 GetRandomGroundPosition()
    {
        float x = Random.Range(spawnAreaMin.x, spawnAreaMax.x);
        float z = Random.Range(spawnAreaMin.y, spawnAreaMax.y);
        Vector3 position = new Vector3(x, 100f, z);

        // Raycast down to find ground
        if (Physics.Raycast(position, Vector3.down, out RaycastHit hit, 200f))
        {
            return hit.point + Vector3.up * spawnHeight;
        }

        return new Vector3(x, spawnHeight, z);
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

        // Spawn box
        currentBox = Instantiate(boxPrefab, spawnPos, Quaternion.identity).GetComponent<DeliveryBox>();
        if (currentBox == null)
        {
            currentBox = Instantiate(boxPrefab, spawnPos, Quaternion.identity).AddComponent<DeliveryBox>();
        }

        // Spawn pickup indicator
        if (pickupIndicatorPrefab != null)
        {
            currentPickupIndicator = Instantiate(pickupIndicatorPrefab, spawnPos, Quaternion.identity);
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
            currentDeliveryIndicator = Instantiate(deliveryIndicatorPrefab, currentDeliveryPoint, Quaternion.identity);
        }

        if (showDebugInfo)
        {
            Debug.Log($"[DeliveryManager] Delivery point set at {currentDeliveryPoint}");
        }
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
        Invoke(nameof(SpawnNewBox), 1f);
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
