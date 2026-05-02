using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Creates a holographic visual zone at the fuel station location.
/// Spawns at runtime for each configured fuel station position.
/// Managed by FuelSystem.
/// </summary>
public class FuelStationZone : MonoBehaviour
{
    private const string ZoneObjectName = "FuelStationZone";

    private static readonly List<FuelStationZone> instances = new List<FuelStationZone>();

    [SerializeField] private float hologramHeight = 3f;
    [SerializeField, Range(0.25f, 1f)] private float hologramDiameterScale = 0.75f;
    [SerializeField] private float pulsateSpeed = 1.5f;
    [SerializeField] private float pulsateAmount = 0.08f;
    [SerializeField] private Color hologramColor = new Color(0.15f, 0.95f, 0.45f, 0.25f);
    [SerializeField] private Color ringColor = new Color(0.15f, 0.95f, 0.45f, 0.5f);

    private GameObject hologramCylinder;
    private GameObject ringIndicator;
    private Material hologramMaterial;
    private Material ringMaterial;
    private float baseScale;

    public static FuelStationZone Instance => instances.Count > 0 ? instances[0] : null;

    public static void EnsureInstance(Vector3 position, float radius)
    {
        EnsureInstances(new[] { position }, radius);
    }

    public static void EnsureInstances(Vector3[] positions, float radius)
    {
        if (positions == null || positions.Length == 0)
        {
            return;
        }

        instances.RemoveAll(zone => zone == null || zone.gameObject == null);

        for (int i = 0; i < positions.Length; i++)
        {
            FuelStationZone zone = GetOrCreateZone(i);
            zone.transform.position = positions[i];
            zone.ApplyRadius(radius);

            if (zone.hologramCylinder == null)
            {
                zone.BuildVisuals();
            }
        }

        for (int i = instances.Count - 1; i >= positions.Length; i--)
        {
            FuelStationZone extraZone = instances[i];
            instances.RemoveAt(i);

            if (extraZone != null)
            {
                Destroy(extraZone.gameObject);
            }
        }
    }

    private void Awake()
    {
        if (!instances.Contains(this))
        {
            instances.Add(this);
        }
    }

    private void OnDestroy()
    {
        instances.Remove(this);

        if (hologramMaterial != null) Destroy(hologramMaterial);
        if (ringMaterial != null) Destroy(ringMaterial);
    }

    private void Update()
    {
        if (hologramCylinder == null) return;

        // Gentle pulsation
        float pulse = 1f + Mathf.Sin(Time.time * pulsateSpeed) * pulsateAmount;
        float scale = baseScale * pulse;
        hologramCylinder.transform.localScale = new Vector3(scale, hologramHeight * 0.5f, scale);

        if (ringIndicator != null)
        {
            ringIndicator.transform.Rotate(Vector3.up, 20f * Time.deltaTime, Space.World);
        }

        // Fade hologram alpha
        if (hologramMaterial != null)
        {
            Color c = hologramMaterial.color;
            c.a = hologramColor.a * (0.8f + 0.2f * Mathf.Sin(Time.time * pulsateSpeed * 1.3f));
            hologramMaterial.color = c;
        }
    }

    private void BuildVisuals()
    {
        // Create semi-transparent cylinder as hologram effect
        hologramCylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hologramCylinder.name = "HologramCylinder";
        hologramCylinder.transform.SetParent(transform, false);
        hologramCylinder.transform.localPosition = new Vector3(0f, hologramHeight * 0.5f, 0f);

        float diameter = baseScale;
        hologramCylinder.transform.localScale = new Vector3(diameter, hologramHeight * 0.5f, diameter);

        // Remove collider to not interfere with physics
        Collider cylinderCollider = hologramCylinder.GetComponent<Collider>();
        if (cylinderCollider != null) Destroy(cylinderCollider);

        // Apply transparent material
        Renderer cylinderRenderer = hologramCylinder.GetComponent<Renderer>();
        if (cylinderRenderer != null)
        {
            hologramMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (hologramMaterial != null)
            {
                hologramMaterial.SetFloat("_Surface", 1f); // Transparent
                hologramMaterial.SetFloat("_Blend", 0f); // Alpha
                hologramMaterial.SetOverrideTag("RenderType", "Transparent");
                hologramMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                hologramMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                hologramMaterial.SetInt("_ZWrite", 0);
                hologramMaterial.DisableKeyword("_ALPHATEST_ON");
                hologramMaterial.EnableKeyword("_ALPHABLEND_ON");
                hologramMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                hologramMaterial.renderQueue = 3000;
                hologramMaterial.color = hologramColor;
                hologramMaterial.SetFloat("_Smoothness", 0.9f);
                hologramMaterial.SetColor("_EmissionColor", new Color(hologramColor.r * 0.5f, hologramColor.g * 0.5f, hologramColor.b * 0.5f, 1f));
                hologramMaterial.EnableKeyword("_EMISSION");
                cylinderRenderer.material = hologramMaterial;
                cylinderRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                cylinderRenderer.receiveShadows = false;
            }
        }

        // Create ring indicator on the ground
        ringIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ringIndicator.name = "GroundRing";
        ringIndicator.transform.SetParent(transform, false);
        ringIndicator.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        ringIndicator.transform.localScale = new Vector3(diameter + 1f, 0.05f, diameter + 1f);

        Collider ringCollider = ringIndicator.GetComponent<Collider>();
        if (ringCollider != null) Destroy(ringCollider);

        Renderer ringRenderer = ringIndicator.GetComponent<Renderer>();
        if (ringRenderer != null)
        {
            ringMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (ringMaterial != null)
            {
                ringMaterial.SetFloat("_Surface", 1f);
                ringMaterial.SetOverrideTag("RenderType", "Transparent");
                ringMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                ringMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                ringMaterial.SetInt("_ZWrite", 0);
                ringMaterial.EnableKeyword("_ALPHABLEND_ON");
                ringMaterial.renderQueue = 3000;
                ringMaterial.color = ringColor;
                ringMaterial.SetColor("_EmissionColor", new Color(ringColor.r * 0.3f, ringColor.g * 0.3f, ringColor.b * 0.3f, 1f));
                ringMaterial.EnableKeyword("_EMISSION");
                ringRenderer.material = ringMaterial;
                ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ringRenderer.receiveShadows = false;
            }
        }
    }

    private void ApplyRadius(float radius)
    {
        baseScale = radius * 2f * Mathf.Max(0.25f, hologramDiameterScale);

        if (hologramCylinder != null)
        {
            hologramCylinder.transform.localScale = new Vector3(baseScale, hologramHeight * 0.5f, baseScale);
        }

        if (ringIndicator != null)
        {
            ringIndicator.transform.localScale = new Vector3(baseScale + 1f, 0.05f, baseScale + 1f);
        }
    }

    private static FuelStationZone GetOrCreateZone(int index)
    {
        if (index < instances.Count && instances[index] != null)
        {
            return instances[index];
        }

        string objectName = GetZoneObjectName(index);
        GameObject existing = GameObject.Find(objectName);
        FuelStationZone zone = existing != null ? existing.GetComponent<FuelStationZone>() : null;

        if (zone == null)
        {
            GameObject zoneObject = new GameObject(objectName);
            zone = zoneObject.AddComponent<FuelStationZone>();
        }

        zone.gameObject.name = objectName;

        if (!instances.Contains(zone))
        {
            instances.Add(zone);
        }

        while (instances.Count <= index)
        {
            instances.Add(null);
        }

        instances[index] = zone;
        return zone;
    }

    private static string GetZoneObjectName(int index)
    {
        return index == 0 ? ZoneObjectName : $"{ZoneObjectName}_{index + 1}";
    }
}
