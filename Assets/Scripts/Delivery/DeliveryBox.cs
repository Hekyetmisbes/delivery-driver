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
    [SerializeField] private float indicatorBobAmount = 0.18f;
    [SerializeField] private float indicatorBobSpeed = 2.8f;
    [SerializeField] private float indicatorPulseAmount = 0.08f;
    [SerializeField] private float indicatorPulseSpeed = 4.2f;
    [SerializeField] private Color indicatorColor = new Color(0.1f, 1f, 1f, 1f);
    [SerializeField] private Color indicatorNearColor = new Color(0.25f, 1f, 0.35f, 1f);
    [SerializeField] private float indicatorNearDistance = 10f;
    [SerializeField] private float indicatorEmissionIntensity = 3.5f;

    [Header("Pickup Settings")]
    [SerializeField] private float pickupRadius = 3f;
    [SerializeField] private LayerMask playerLayer = ~0;

    [Header("Safety Settings")]
    [SerializeField] private float fallDistanceFromSpawn = 25f;
    [SerializeField] private bool enableFallProtection = true;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundRayStartHeight = 200f;
    [SerializeField] private float groundRayDistance = 500f;
    [SerializeField] private float respawnHeightOffset = 0.75f;

    private bool isPickedUp = false;
    private Transform playerTransform;
    private Rigidbody rb;
    private MeshRenderer[] meshRenderers;
    private Vector3 spawnPosition;
    private Transform cachedPlayerTransform;
    private DeliveryManager cachedDeliveryManager;
    private float runtimeFallThreshold;
    private Material pickupIndicatorMaterial;
    private Vector3 pickupIndicatorBaseLocalPosition;
    private Vector3 pickupIndicatorBaseLocalScale = Vector3.one;
    private float indicatorPhaseOffset;

    public bool IsPickedUp => isPickedUp;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        meshRenderers = GetComponentsInChildren<MeshRenderer>();
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

        // Create pickup indicator if not assigned
        if (pickupIndicator == null)
        {
            CreateDefaultIndicator();
        }

    }

    private void Update()
    {
        // Rotate indicator
        if (pickupIndicator != null && !isPickedUp)
        {
            AnimatePickupIndicator();
        }

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
        playerTransform = player;

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

        // Destroy box
        Destroy(gameObject);
        Debug.Log("[DeliveryBox] Box delivered!");
    }

    private void CreateDefaultIndicator()
    {
        GameObject indicatorRoot = new GameObject("PickupIndicator");
        indicatorRoot.transform.SetParent(transform);
        indicatorRoot.transform.localPosition = Vector3.up * indicatorHeightOffset;
        indicatorRoot.transform.localRotation = Quaternion.identity;
        indicatorRoot.transform.localScale = Vector3.one;

        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "Ring";
        ring.transform.SetParent(indicatorRoot.transform, false);
        ring.transform.localPosition = new Vector3(0f, -0.15f, 0f);
        ring.transform.localScale = new Vector3(0.75f, 0.03f, 0.75f);
        RemoveIndicatorCollider(ring);

        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shaft.name = "Shaft";
        shaft.transform.SetParent(indicatorRoot.transform, false);
        shaft.transform.localPosition = new Vector3(0f, 0f, 0.02f);
        shaft.transform.localScale = new Vector3(0.18f, 0.18f, 1.15f);
        RemoveIndicatorCollider(shaft);

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "Head";
        head.transform.SetParent(indicatorRoot.transform, false);
        head.transform.localPosition = new Vector3(0f, 0f, 0.7f);
        head.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        head.transform.localScale = new Vector3(0.42f, 0.18f, 0.42f);
        RemoveIndicatorCollider(head);

        GameObject leftWing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftWing.name = "LeftWing";
        leftWing.transform.SetParent(indicatorRoot.transform, false);
        leftWing.transform.localPosition = new Vector3(-0.22f, 0f, 0.3f);
        leftWing.transform.localRotation = Quaternion.Euler(0f, -28f, 0f);
        leftWing.transform.localScale = new Vector3(0.15f, 0.18f, 0.55f);
        RemoveIndicatorCollider(leftWing);

        GameObject rightWing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightWing.name = "RightWing";
        rightWing.transform.SetParent(indicatorRoot.transform, false);
        rightWing.transform.localPosition = new Vector3(0.22f, 0f, 0.3f);
        rightWing.transform.localRotation = Quaternion.Euler(0f, 28f, 0f);
        rightWing.transform.localScale = new Vector3(0.15f, 0.18f, 0.55f);
        RemoveIndicatorCollider(rightWing);

        MeshRenderer indicatorRenderer = shaft.GetComponent<MeshRenderer>();
        pickupIndicatorMaterial = RuntimeColorMaterialHelper.CreateColorMaterial(indicatorColor, indicatorRenderer);
        ApplyIndicatorMaterial(ring.GetComponent<MeshRenderer>());
        ApplyIndicatorMaterial(shaft.GetComponent<MeshRenderer>());
        ApplyIndicatorMaterial(head.GetComponent<MeshRenderer>());
        ApplyIndicatorMaterial(leftWing.GetComponent<MeshRenderer>());
        ApplyIndicatorMaterial(rightWing.GetComponent<MeshRenderer>());

        pickupIndicator = indicatorRoot;
        pickupIndicatorBaseLocalPosition = pickupIndicator.transform.localPosition;
        pickupIndicatorBaseLocalScale = pickupIndicator.transform.localScale;
        indicatorPhaseOffset = Random.Range(0f, Mathf.PI * 2f);
        UpdateIndicatorColor(999f);
    }

    private void AnimatePickupIndicator()
    {
        if (pickupIndicator == null)
        {
            return;
        }

        pickupIndicator.transform.Rotate(Vector3.up, indicatorRotationSpeed * Time.deltaTime, Space.Self);

        float bob = Mathf.Sin((Time.time + indicatorPhaseOffset) * indicatorBobSpeed) * indicatorBobAmount;
        pickupIndicator.transform.localPosition = pickupIndicatorBaseLocalPosition + Vector3.up * bob;

        float pulse = 1f + Mathf.Sin((Time.time + indicatorPhaseOffset) * indicatorPulseSpeed) * indicatorPulseAmount;
        pickupIndicator.transform.localScale = pickupIndicatorBaseLocalScale * pulse;

        float playerDistance = cachedPlayerTransform != null
            ? Vector3.Distance(transform.position, cachedPlayerTransform.position)
            : indicatorNearDistance;
        UpdateIndicatorColor(playerDistance);
    }

    private void UpdateIndicatorColor(float distanceToPlayer)
    {
        if (pickupIndicatorMaterial == null)
        {
            return;
        }

        float normalizedDistance = indicatorNearDistance <= 0.01f
            ? 1f
            : Mathf.Clamp01(distanceToPlayer / indicatorNearDistance);
        float highlightFactor = 1f - normalizedDistance;
        Color color = Color.Lerp(indicatorColor, indicatorNearColor, highlightFactor);
        pickupIndicatorMaterial.color = color;

        if (pickupIndicatorMaterial.HasProperty("_EmissionColor"))
        {
            pickupIndicatorMaterial.EnableKeyword("_EMISSION");
            pickupIndicatorMaterial.SetColor("_EmissionColor", color * Mathf.Lerp(indicatorEmissionIntensity, indicatorEmissionIntensity * 1.5f, highlightFactor));
        }
    }

    private void ApplyIndicatorMaterial(MeshRenderer renderer)
    {
        if (renderer != null && pickupIndicatorMaterial != null)
        {
            renderer.material = pickupIndicatorMaterial;
        }
    }

    private void RemoveIndicatorCollider(GameObject target)
    {
        Collider collider = target.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }
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
