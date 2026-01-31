using UnityEngine;

/// <summary>
/// Delivery box that can be picked up by the player
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class DeliveryBox : MonoBehaviour
{
    [Header("Visual Feedback")]
    [SerializeField] private GameObject pickupIndicator;
    [SerializeField] private float indicatorRotationSpeed = 50f;
    [SerializeField] private float indicatorHeightOffset = 2f;

    [Header("Pickup Settings")]
    [SerializeField] private float pickupRadius = 3f;
    [SerializeField] private LayerMask playerLayer = ~0;

    [Header("Safety Settings")]
    [SerializeField] private float fallThreshold = -10f;
    [SerializeField] private bool enableFallProtection = true;

    private bool isPickedUp = false;
    private Transform playerTransform;
    private Rigidbody rb;
    private MeshRenderer[] meshRenderers;
    private Vector3 spawnPosition;

    public bool IsPickedUp => isPickedUp;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        meshRenderers = GetComponentsInChildren<MeshRenderer>();
        spawnPosition = transform.position;

        // Setup rigidbody - start as kinematic to prevent falling during spawn
        if (rb != null)
        {
            rb.mass = 5f;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;
            rb.isKinematic = true; // Start kinematic, will enable physics after delay
        }

        // Create pickup indicator if not assigned
        if (pickupIndicator == null)
        {
            CreateDefaultIndicator();
        }

        // Enable physics after short delay to ensure proper ground placement
        Invoke(nameof(EnablePhysics), 0.5f);
    }

    /// <summary>
    /// Enable physics after spawn delay
    /// </summary>
    private void EnablePhysics()
    {
        if (rb != null && !isPickedUp)
        {
            rb.isKinematic = false;
        }
    }

    private void Update()
    {
        // Rotate indicator
        if (pickupIndicator != null && !isPickedUp)
        {
            pickupIndicator.transform.Rotate(Vector3.up, indicatorRotationSpeed * Time.deltaTime);
        }

        // Check for nearby player (distance-based pickup)
        if (!isPickedUp)
        {
            CheckForPlayer();

            // Fall protection - respawn if box falls through world
            if (enableFallProtection && transform.position.y < fallThreshold)
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

        transform.position = spawnPosition;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            Invoke(nameof(EnablePhysics), 0.5f);
        }
    }

    private void CheckForPlayer()
    {
        // Find player
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            CarController car = FindFirstObjectByType<CarController>();
            if (car != null) playerObj = car.gameObject;
        }

        if (playerObj != null)
        {
            float distance = Vector3.Distance(transform.position, playerObj.transform.position);
            if (distance < pickupRadius)
            {
                PickupBox(playerObj.transform);
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
        playerTransform = player;

        // Completely hide the box gameobject
        gameObject.SetActive(false);

        // Notify delivery manager
        DeliveryManager deliveryManager = FindFirstObjectByType<DeliveryManager>();
        if (deliveryManager != null)
        {
            deliveryManager.OnBoxPickedUp(this);
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
        DeliveryManager deliveryManager = FindFirstObjectByType<DeliveryManager>();
        if (deliveryManager != null)
        {
            deliveryManager.OnBoxDelivered(this);
        }

        // Destroy box
        Destroy(gameObject);
        Debug.Log("[DeliveryBox] Box delivered!");
    }

    private void CreateDefaultIndicator()
    {
        // Create a simple cylinder indicator
        GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        indicator.name = "PickupIndicator";
        indicator.transform.SetParent(transform);
        indicator.transform.localPosition = Vector3.up * indicatorHeightOffset;
        indicator.transform.localScale = new Vector3(0.5f, 1.5f, 0.5f);

        // Remove collider
        Destroy(indicator.GetComponent<Collider>());

        // Create glowing material
        Material indicatorMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        indicatorMat.color = Color.green;
        indicator.GetComponent<MeshRenderer>().material = indicatorMat;

        pickupIndicator = indicator;
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
