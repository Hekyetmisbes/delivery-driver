using UnityEngine;

/// <summary>
/// Delivery box that can be picked up by the player
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class DeliveryBox : MonoBehaviour
{
    [Header("Pickup Settings")]
    [SerializeField] private float pickupRadius = 3f;

    [Header("Pickup Hologram")]
    [SerializeField] private bool showPickupHologram = true;
    [SerializeField] private Vector2 hologramSize = new Vector2(5.5f, 7.5f);
    [SerializeField] private Color hologramFillColor = new Color(0.1f, 0.85f, 1f, 0.22f);
    [SerializeField] private Color hologramLineColor = new Color(0.35f, 1f, 0.9f, 0.85f);
    [SerializeField] private float hologramGroundOffset = 0.035f;
    [SerializeField] private float hologramPulseSpeed = 2.2f;
    [SerializeField] private float hologramPulseScale = 0.04f;
    [SerializeField] private float hologramLineWidth = 0.08f;

    [Header("Safety Settings")]
    [SerializeField] private float fallDistanceFromSpawn = 25f;
    [SerializeField] private bool enableFallProtection = true;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundRayStartHeight = 200f;
    [SerializeField] private float groundRayDistance = 500f;
    [SerializeField] private float respawnHeightOffset = 0.05f;

    private bool isPickedUp = false;
    private Rigidbody rb;
    private Vector3 spawnPosition;
    private Transform cachedPlayerTransform;
    private DeliveryManager cachedDeliveryManager;
    private float runtimeFallThreshold;
    private Transform hologramRoot;
    private Transform hologramFill;
    private LineRenderer hologramBorder;
    private LineRenderer hologramCenterLine;
    private Material hologramFillMaterial;
    private Material hologramLineMaterial;

    public bool IsPickedUp => isPickedUp;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        spawnPosition = transform.position;
        SnapSpawnToGround();
        transform.position = spawnPosition;
        runtimeFallThreshold = spawnPosition.y - Mathf.Abs(fallDistanceFromSpawn);
        EnsurePickupHologram();
        UpdatePickupHologramPose();

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
        EnsurePickupHologram();
        UpdatePickupHologramPose();
        SetPickupHologramVisible(true);

        if (cachedPlayerTransform == null)
        {
            ResolvePlayerTransform();
        }

        if (rb != null)
        {
            rb.mass = 5f;
            ResetRigidbodyMotion();
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
            UpdatePickupHologramAnimation();

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
        UpdatePickupHologramPose();

        if (rb != null)
        {
            ResetRigidbodyMotion();
            rb.useGravity = false;
            rb.isKinematic = true;
        }
    }

    private void ResetRigidbodyMotion()
    {
        if (rb == null || rb.isKinematic)
        {
            return;
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void EnsurePickupHologram()
    {
        if (!showPickupHologram)
        {
            SetPickupHologramVisible(false);
            return;
        }

        if (hologramRoot == null)
        {
            GameObject root = new GameObject("PickupParkingHologram");
            root.transform.SetParent(transform, false);
            hologramRoot = root.transform;
        }

        if (hologramFill == null)
        {
            GameObject fillObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fillObject.name = "HologramParkingBayFill";
            fillObject.transform.SetParent(hologramRoot, false);
            Collider fillCollider = fillObject.GetComponent<Collider>();
            if (fillCollider != null)
            {
                fillCollider.enabled = false;
            }

            MeshRenderer fillRenderer = fillObject.GetComponent<MeshRenderer>();
            hologramFillMaterial = CreateTransparentHologramMaterial(hologramFillColor, fillRenderer);
            if (fillRenderer != null && hologramFillMaterial != null)
            {
                fillRenderer.material = hologramFillMaterial;
                fillRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                fillRenderer.receiveShadows = false;
            }

            hologramFill = fillObject.transform;
        }

        if (hologramBorder == null)
        {
            hologramBorder = CreateHologramLine("HologramParkingBayBorder", true);
        }

        if (hologramCenterLine == null)
        {
            hologramCenterLine = CreateHologramLine("HologramParkingBayCenterLine", false);
        }

        SetPickupHologramVisible(true);
    }

    private LineRenderer CreateHologramLine(string objectName, bool loop)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(hologramRoot, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = loop;
        line.widthMultiplier = Mathf.Max(0.02f, hologramLineWidth);
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        if (hologramLineMaterial == null)
        {
            hologramLineMaterial = CreateTransparentHologramMaterial(hologramLineColor, null);
        }

        if (hologramLineMaterial != null)
        {
            line.material = hologramLineMaterial;
        }

        return line;
    }

    private Material CreateTransparentHologramMaterial(Color color, MeshRenderer fallbackRenderer)
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

    private void UpdatePickupHologramPose()
    {
        if (!showPickupHologram || hologramRoot == null)
        {
            return;
        }

        Vector3 groundPoint = ResolveHologramGroundPoint();
        hologramRoot.position = groundPoint + Vector3.up * Mathf.Max(0.005f, hologramGroundOffset);
        hologramRoot.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        float width = Mathf.Max(1f, hologramSize.x);
        float length = Mathf.Max(width, hologramSize.y);

        if (hologramFill != null)
        {
            hologramFill.localPosition = Vector3.zero;
            hologramFill.localRotation = Quaternion.identity;
            hologramFill.localScale = new Vector3(width, 0.025f, length);
        }

        if (hologramBorder != null)
        {
            float halfWidth = width * 0.5f;
            float halfLength = length * 0.5f;
            hologramBorder.positionCount = 4;
            hologramBorder.SetPosition(0, new Vector3(-halfWidth, 0.035f, -halfLength));
            hologramBorder.SetPosition(1, new Vector3(-halfWidth, 0.035f, halfLength));
            hologramBorder.SetPosition(2, new Vector3(halfWidth, 0.035f, halfLength));
            hologramBorder.SetPosition(3, new Vector3(halfWidth, 0.035f, -halfLength));
        }

        if (hologramCenterLine != null)
        {
            float halfLength = length * 0.38f;
            hologramCenterLine.positionCount = 2;
            hologramCenterLine.SetPosition(0, new Vector3(0f, 0.04f, -halfLength));
            hologramCenterLine.SetPosition(1, new Vector3(0f, 0.04f, halfLength));
        }
    }

    private Vector3 ResolveHologramGroundPoint()
    {
        Vector3 origin = transform.position + Vector3.up * groundRayStartHeight;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, groundRayDistance + groundRayStartHeight, groundMask, QueryTriggerInteraction.Ignore);
        if (hits != null && hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                return hits[i].point;
            }
        }

        return new Vector3(transform.position.x, spawnPosition.y - GetGroundSnapOffset(), transform.position.z);
    }

    private void UpdatePickupHologramAnimation()
    {
        if (!showPickupHologram || hologramRoot == null)
        {
            return;
        }

        float pulse = 1f + Mathf.Sin(Time.time * hologramPulseSpeed) * hologramPulseScale;
        hologramRoot.localScale = new Vector3(pulse, 1f, pulse);

        float alphaPulse = 0.75f + Mathf.Sin(Time.time * hologramPulseSpeed) * 0.25f;
        SetMaterialAlpha(hologramFillMaterial, hologramFillColor.a * alphaPulse);
        SetMaterialAlpha(hologramLineMaterial, hologramLineColor.a * alphaPulse);
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

    private void SetPickupHologramVisible(bool visible)
    {
        if (hologramRoot != null)
        {
            hologramRoot.gameObject.SetActive(showPickupHologram && visible);
        }
    }

    private void SnapSpawnToGround()
    {
        Vector3 rayStart = new Vector3(spawnPosition.x, spawnPosition.y + groundRayStartHeight, spawnPosition.z);
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, groundRayDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            spawnPosition = hit.point + Vector3.up * GetGroundSnapOffset();
        }
    }

    private float GetGroundSnapOffset()
    {
        float clearance = Mathf.Clamp(respawnHeightOffset, 0.01f, 0.12f);
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        float lowestPoint = float.MaxValue;
        bool foundSolidCollider = false;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider.isTrigger || !collider.enabled)
            {
                continue;
            }

            foundSolidCollider = true;
            lowestPoint = Mathf.Min(lowestPoint, collider.bounds.min.y);
        }

        if (!foundSolidCollider)
        {
            return clearance;
        }

        float colliderOffset = transform.position.y - lowestPoint;
        return Mathf.Max(clearance, colliderOffset + clearance);
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
