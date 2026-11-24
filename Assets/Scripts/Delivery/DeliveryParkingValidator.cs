using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Vehicle footprint containment, independent of delivery lifecycle and presentation.</summary>
public sealed class DeliveryParkingValidator
{
    private readonly List<Collider> colliders = new List<Collider>();
    private readonly List<Renderer> renderers = new List<Renderer>();
    private readonly Vector3[] corners = new Vector3[4];

    public bool ContainsVehicle(Transform player, Vector3 center, Quaternion rotation,
        Vector2 size, float tolerance)
    {
        if (player == null)
            return false;

        Quaternion inverseRotation = Quaternion.Inverse(rotation);
        float halfWidth = Mathf.Max(1f, size.x) * 0.5f + Mathf.Max(0f, tolerance);
        float halfLength = Mathf.Max(size.x, size.y) * 0.5f + Mathf.Max(0f, tolerance);

        if (!TryGetFootprint(player))
            return ContainsPoint(player.position, center, inverseRotation, halfWidth, halfLength);

        for (int i = 0; i < corners.Length; i++)
        {
            if (!ContainsPoint(corners[i], center, inverseRotation, halfWidth, halfLength))
                return false;
        }
        return true;
    }

    private static bool ContainsPoint(Vector3 point, Vector3 center, Quaternion inverseRotation,
        float halfWidth, float halfLength)
    {
        Vector3 local = inverseRotation * (point - center);
        return Mathf.Abs(local.x) <= halfWidth && Mathf.Abs(local.z) <= halfLength;
    }

    private bool TryGetFootprint(Transform player)
    {
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        bool hasBounds = false;

        // Refill reusable lists: children may change after cargo pickup or vehicle switch.
        colliders.Clear();
        player.GetComponentsInChildren(false, colliders);
        foreach (Collider collider in colliders)
        {
            if (collider == null || collider.isTrigger || collider is WheelCollider)
                continue;
            Encapsulate(player, collider.bounds, ref minX, ref maxX, ref minZ, ref maxZ);
            hasBounds = true;
        }

        if (!hasBounds)
        {
            renderers.Clear();
            player.GetComponentsInChildren(false, renderers);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || ShouldIgnoreRenderer(renderer))
                    continue;
                Encapsulate(player, renderer.bounds, ref minX, ref maxX, ref minZ, ref maxZ);
                hasBounds = true;
            }
        }

        colliders.Clear();
        renderers.Clear();
        if (!hasBounds)
            return false;

        corners[0] = player.TransformPoint(new Vector3(minX, 0f, minZ));
        corners[1] = player.TransformPoint(new Vector3(minX, 0f, maxZ));
        corners[2] = player.TransformPoint(new Vector3(maxX, 0f, maxZ));
        corners[3] = player.TransformPoint(new Vector3(maxX, 0f, minZ));
        return true;
    }

    private static bool ShouldIgnoreRenderer(Renderer renderer)
    {
        if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer)
            return true;

        string name = renderer.name;
        return name.IndexOf("smoke", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("exhaust", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("trail", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("effect", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("hologram", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("marker", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void Encapsulate(Transform vehicle, Bounds bounds,
        ref float minX, ref float maxX, ref float minZ, ref float maxZ)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        for (int i = 0; i < 8; i++)
        {
            Vector3 point = new Vector3((i & 4) == 0 ? min.x : max.x,
                (i & 2) == 0 ? min.y : max.y, (i & 1) == 0 ? min.z : max.z);
            Vector3 local = vehicle.InverseTransformPoint(point);
            minX = Mathf.Min(minX, local.x);
            maxX = Mathf.Max(maxX, local.x);
            minZ = Mathf.Min(minZ, local.z);
            maxZ = Mathf.Max(maxZ, local.z);
        }
    }
}
