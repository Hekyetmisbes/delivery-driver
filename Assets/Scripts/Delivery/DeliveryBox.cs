using UnityEngine;

/// <summary>
/// Delivery box that can be picked up by the player
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class DeliveryBox : MonoBehaviour
{
    [Header("Pickup Settings")]
    [SerializeField] private float pickupRadius = 3f;

    [Header("Safety Settings")]
    [SerializeField] private float fallDistanceFromSpawn = 25f;
    [SerializeField] private bool enableFallProtection = true;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundRayStartHeight = 200f;
    [SerializeField] private float groundRayDistance = 500f;
    [SerializeField] private float respawnHeightOffset = 0.75f;

    private bool isPickedUp = false;
    private Rigidbody rb;
    private Vector3 spawnPosition;
    private Transform cachedPlayerTransform;
    private DeliveryManager cachedDeliveryManager;
    private float runtimeFallThreshold;

    public bool IsPickedUp => isPickedUp;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        spawnPosition = transform.position;
        SnapSpawnToGround();
        transform.position = spawnPosition;
        runtimeFallThreshold = spawnPosition.y - Mathf.Abs(fallDistanceFromSpawn);

        // Cache references once
        ResolvePlayerTransform();
        cachedDeliveryManager = FindFirstObjectByType<DeliveryManager>();

        // Setup rigidbody - start as kinematic to prevent falling during spawn
        if (rb != null)
        {
            rb.mass = 5f;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

    }

    public void PrepareForSpawn(DeliveryManager deliveryManager, Vector3 position, Quaternion rotation)
    {
        cachedDeliveryManager = deliveryManager;
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        isPickedUp = false;
        spawnPosition = position;
        transform.SetPositionAndRotation(position, rotation);
        SnapSpawnToGround();
        transform.position = spawnPosition;
        runtimeFallThreshold = spawnPosition.y - Mathf.Abs(fallDistanceFromSpawn);

        if (cachedPlayerTransform == null)
        {
            ResolvePlayerTransform();
        }

        if (rb != null)
        {
            rb.mass = 5f;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        gameObject.SetActive(true);
    }

    private void Update()
    {
        // Check for nearby player (distance-based pickup)
        if (!isPickedUp)
        {
            CheckForPlayer();

            // Fall protection - respawn if box falls through world
            if (enableFallProtection && transform.position.y < runtimeFallThreshold)
            {
                RespawnBox();
            }
        }
    }

    /// <summary>
    /// Respawn box at original position if it falls
    /// </summary>
    private void RespawnBox()
    {
        Debug.LogWarning("[DeliveryBox] Box fell below world! Respawning...");

        SnapSpawnToGround();
        transform.position = spawnPosition;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }
    }

    private void SnapSpawnToGround()
    {
        Vector3 rayStart = new Vector3(spawnPosition.x, spawnPosition.y + groundRayStartHeight, spawnPosition.z);
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, groundRayDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            spawnPosition = hit.point + Vector3.up * respawnHeightOffset;
        }
    }

    private void ResolvePlayerTransform()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            cachedPlayerTransform = playerObj.transform;
            return;
        }

        CarController car = FindFirstObjectByType<CarController>();
        if (car != null) cachedPlayerTransform = car.transform;
    }

    private void CheckForPlayer()
    {
        if (cachedPlayerTransform == null)
        {
            ResolvePlayerTransform();
        }

        if (cachedPlayerTransform != null)
        {
            float distance = Vector3.Distance(transform.position, cachedPlayerTransform.position);
            if (distance < pickupRadius)
            {
                PickupBox(cachedPlayerTransform);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isPickedUp) return;

        // Check if player picked up the box
        if (other.CompareTag("Player") || other.GetComponent<CarController>() != null)
        {
            PickupBox(other.transform);
        }
    }

    private void PickupBox(Transform player)
    {
        if (isPickedUp) return;

        isPickedUp = true;

        // Completely hide the box gameobject
        gameObject.SetActive(false);

        // Notify delivery manager
        if (cachedDeliveryManager == null)
            cachedDeliveryManager = FindFirstObjectByType<DeliveryManager>();
        if (cachedDeliveryManager != null)
        {
            cachedDeliveryManager.OnBoxPickedUp(this);
        }

        Debug.Log("[DeliveryBox] Box picked up by player!");
    }

    /// <summary>
    /// Deliver the box at target location
    /// </summary>
    public void DeliverBox()
    {
        if (!isPickedUp) return;

        // Notify delivery manager
        if (cachedDeliveryManager == null)
            cachedDeliveryManager = FindFirstObjectByType<DeliveryManager>();
        if (cachedDeliveryManager != null)
        {
            cachedDeliveryManager.OnBoxDelivered(this);
        }

        gameObject.SetActive(false);
        Debug.Log("[DeliveryBox] Box delivered!");
    }

    private void OnDrawGizmos()
    {
        if (!isPickedUp)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, pickupRadius);
        }
    }
}
