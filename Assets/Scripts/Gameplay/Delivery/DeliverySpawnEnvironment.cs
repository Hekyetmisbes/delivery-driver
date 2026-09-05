using System;
using System.Collections.Generic;
using DeliveryDriver.City;
using UnityEngine;

internal sealed class DeliverySpawnEnvironment
{
    private readonly string[] buildingNameKeywords;
    private readonly Collider[] sharedOverlapBuffer = new Collider[64];
    private readonly Collider[] roadSearchBuffer = new Collider[64];
    private readonly Dictionary<int, bool> roadColliderGuessCache = new Dictionary<int, bool>(256);
    private readonly Dictionary<int, bool> buildingObjectCache = new Dictionary<int, bool>(256);
    private HashSet<string> buildingKeywordSet;
    private Bounds[] cachedTerrainBounds;
    private bool hasTerrainBounds;
    private int cachedBuildingLayer = int.MinValue;
    private int cachedNeighborhoodLayer = int.MinValue;
    private LayerMask neighborhoodLayerMask;
    private LayerMask roadSurfaceMask;

    public DeliverySpawnEnvironment(string[] buildingNameKeywords)
    {
        this.buildingNameKeywords = buildingNameKeywords ?? Array.Empty<string>();
    }

    public bool HasRoadMask => roadSurfaceMask.value != 0;

    public LayerMask EnsureRoadSurfaceMask(LayerMask currentRoadSurfaceMask)
    {
        roadSurfaceMask = currentRoadSurfaceMask;
        if (roadSurfaceMask.value != 0)
        {
            return roadSurfaceMask;
        }

        int roadLayer = LayerMask.NameToLayer("Road");
        if (roadLayer >= 0)
        {
            roadSurfaceMask = 1 << roadLayer;
        }
        else
        {
            Debug.LogWarning("[DeliveryManager] No 'Road' layer found and roadSurfaceMask is not set. Road-based spawn constraints will be skipped.");
        }

        return roadSurfaceMask;
    }

    public void SetTerrainBounds(Bounds[] terrainBounds)
    {
        cachedTerrainBounds = terrainBounds;
        hasTerrainBounds = cachedTerrainBounds != null && cachedTerrainBounds.Length > 0;
    }

    public bool TryFindRoadSurfaceNearPoint(Vector3 center, float spawnHeight, out Vector3 roadPoint)
    {
        roadPoint = Vector3.positiveInfinity;
        if (!HasRoadMask)
        {
            return false;
        }

        float[] searchRadii = { 6f, 10f, 16f, 24f, 36f };
        foreach (float radius in searchRadii)
        {
            int hitCount = Physics.OverlapSphereNonAlloc(center, radius, sharedOverlapBuffer, roadSurfaceMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Collider roadCol = sharedOverlapBuffer[i];
                if (roadCol == null || roadCol.isTrigger || !IsRoadCollider(roadCol))
                {
                    continue;
                }

                if (!TryGetClosestPointSafe(roadCol, center, out Vector3 closePoint))
                {
                    continue;
                }

                Vector3 rayStart = closePoint + Vector3.up * 20f;
                if (!Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                if (hit.collider == null || hit.collider.isTrigger || !IsRoadCollider(hit.collider))
                {
                    continue;
                }

                Vector3 candidate = hit.point + Vector3.up * spawnHeight;
                if (hasTerrainBounds && !IsWithinAnyTerrainBounds(candidate))
                {
                    continue;
                }

                if (IsSpawnSpaceBlocked(candidate))
                {
                    continue;
                }

                roadPoint = candidate;
                return true;
            }
        }

        return false;
    }

    public bool IsInsideNeighborhood(Vector3 position, float neighborhoodCheckRadius)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(position, neighborhoodCheckRadius, sharedOverlapBuffer, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = sharedOverlapBuffer[i];
            NeighborhoodZone zone = col != null ? col.GetComponent<NeighborhoodZone>() : null;
            if (zone == null && col != null)
            {
                zone = col.GetComponentInParent<NeighborhoodZone>();
            }

            if (zone != null)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryGetBuildingFrontSidewalkPoint(
        Vector3 roadAnchorPoint,
        LayerMask groundMask,
        float spawnHeight,
        float buildingSearchRadius,
        float buildingFrontOffset,
        float sidewalkValidationBuildingRadius,
        float sidewalkToRoadMaxDistance,
        out Vector3 sidewalkPoint)
    {
        sidewalkPoint = Vector3.zero;

        if (!TryFindNearestBuildingCollider(roadAnchorPoint, buildingSearchRadius, out Collider buildingCollider, out Vector3 buildingFrontPoint))
        {
            return false;
        }

        Vector3 directionToRoad = roadAnchorPoint - buildingFrontPoint;
        directionToRoad.y = 0f;
        if (directionToRoad.sqrMagnitude < 0.01f)
        {
            directionToRoad = roadAnchorPoint - buildingCollider.bounds.center;
            directionToRoad.y = 0f;
        }

        if (directionToRoad.sqrMagnitude < 0.01f)
        {
            return false;
        }

        directionToRoad.Normalize();
        Vector3 probePoint = buildingFrontPoint + directionToRoad * Mathf.Max(0.5f, buildingFrontOffset);

        Vector3 rayStart = probePoint + Vector3.up * 12f;
        if (!Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 40f, groundMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        if (hit.collider == null || hit.collider.isTrigger || (HasRoadMask && IsRoadCollider(hit.collider)))
        {
            return false;
        }

        Vector3 candidate = hit.point + Vector3.up * spawnHeight;
        if (!IsNearBuilding(candidate, sidewalkValidationBuildingRadius))
        {
            return false;
        }

        if (HasRoadMask && !IsNearRoad(candidate, sidewalkToRoadMaxDistance))
        {
            return false;
        }

        sidewalkPoint = candidate;
        return true;
    }

    public bool TryFindRoadPointFromSceneColliders(Vector3 center, float spawnHeight, out Vector3 spawnPoint)
    {
        spawnPoint = Vector3.zero;
        if (!HasRoadMask)
        {
            return false;
        }

        Vector3 halfExtents = new Vector3(150f, 100f, 150f);
        int hitCount = Physics.OverlapBoxNonAlloc(center, halfExtents, roadSearchBuffer, Quaternion.identity, roadSurfaceMask, QueryTriggerInteraction.Ignore);
        if (hitCount == 0)
        {
            return false;
        }

        const int samplesPerCollider = 4;
        for (int c = 0; c < hitCount; c++)
        {
            Collider col = roadSearchBuffer[c];
            if (col == null || col.isTrigger || !IsRoadCollider(col))
            {
                continue;
            }

            Bounds bounds = col.bounds;
            for (int i = 0; i < samplesPerCollider; i++)
            {
                float x = UnityEngine.Random.Range(bounds.min.x, bounds.max.x);
                float z = UnityEngine.Random.Range(bounds.min.z, bounds.max.z);
                Vector3 rayStart = new Vector3(x, bounds.max.y + 30f, z);
                if (!Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 120f, ~0, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                if (hit.collider == null || hit.collider.isTrigger || !IsRoadCollider(hit.collider))
                {
                    continue;
                }

                Vector3 candidate = hit.point + Vector3.up * spawnHeight;
                if (!IsValidSpawnPosition(candidate, false, 0f))
                {
                    continue;
                }

                spawnPoint = candidate;
                return true;
            }
        }

        return false;
    }

    public bool IsRoadCollider(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        if (HasRoadMask && (roadSurfaceMask.value & (1 << collider.gameObject.layer)) != 0)
        {
            return true;
        }

        int colliderId = collider.GetInstanceID();
        if (roadColliderGuessCache.TryGetValue(colliderId, out bool cachedIsRoad))
        {
            return cachedIsRoad;
        }

        bool inferredIsRoad = IsLikelyRoadCollider(collider);
        if (roadColliderGuessCache.Count >= 512)
        {
            roadColliderGuessCache.Clear();
        }

        roadColliderGuessCache[colliderId] = inferredIsRoad;
        return inferredIsRoad;
    }

    public bool IsLikelyRoadCollider(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        string name = collider.name;
        if (!string.IsNullOrEmpty(name))
        {
            string lower = name.ToLowerInvariant();
            if (lower.Contains("road") || lower.Contains("street") || lower.Contains("asphalt") || lower.Contains("highway"))
            {
                return true;
            }
        }

        string tag = collider.tag;
        if (!string.IsNullOrEmpty(tag))
        {
            string lowerTag = tag.ToLowerInvariant();
            if (lowerTag.Contains("road") || lowerTag.Contains("street"))
            {
                return true;
            }
        }

        Transform current = collider.transform;
        for (int i = 0; current != null && i < 6; i++)
        {
            string currentName = current.name;
            if (!string.IsNullOrEmpty(currentName))
            {
                string lower = currentName.ToLowerInvariant();
                if (lower.Contains("road") || lower.Contains("street") || lower.Contains("asphalt") || lower.Contains("highway"))
                {
                    return true;
                }
            }

            current = current.parent;
        }

        return false;
    }

    public bool IsWithinAnyTerrainBounds(Vector3 worldPos)
    {
        if (!hasTerrainBounds)
        {
            return true;
        }

        for (int i = 0; i < cachedTerrainBounds.Length; i++)
        {
            Bounds bounds = cachedTerrainBounds[i];
            float halfX = bounds.extents.x;
            float halfZ = bounds.extents.z;
            if (worldPos.x >= bounds.center.x - halfX && worldPos.x <= bounds.center.x + halfX &&
                worldPos.z >= bounds.center.z - halfZ && worldPos.z <= bounds.center.z + halfZ)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsSpawnSpaceBlocked(Vector3 position)
    {
        const float checkRadius = 0.6f;
        Collider supportCollider = null;
        Vector3 supportRayOrigin = position + Vector3.up * 2f;
        if (Physics.Raycast(supportRayOrigin, Vector3.down, out RaycastHit supportHit, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            supportCollider = supportHit.collider;
        }

        int hitCount = Physics.OverlapSphereNonAlloc(position, checkRadius, sharedOverlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = sharedOverlapBuffer[i];
            if (col == null || col.isTrigger)
            {
                continue;
            }

            if (supportCollider != null && col == supportCollider)
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

    public bool IsValidRoadGraphSpawnPosition(Vector3 position)
    {
        if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z))
        {
            return false;
        }

        if (hasTerrainBounds && !IsWithinAnyTerrainBounds(position))
        {
            return false;
        }

        Vector3 rayOrigin = position + Vector3.up * 5f;
        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        if (hit.collider == null || hit.collider.isTrigger || Vector3.Dot(hit.normal, Vector3.up) < 0.6f)
        {
            return false;
        }

        return !IsSpawnSpaceBlocked(position);
    }

    public bool IsValidSpawnPosition(Vector3 position, bool spawnOnlyInNeighborhoods, float neighborhoodCheckRadius)
    {
        if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z))
        {
            return false;
        }

        if (hasTerrainBounds && !IsWithinAnyTerrainBounds(position))
        {
            return false;
        }

        if (spawnOnlyInNeighborhoods && !HasRoadMask && !IsInsideNeighborhood(position, neighborhoodCheckRadius))
        {
            return false;
        }

        Vector3 rayOrigin = position + Vector3.up * 5f;
        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        if (HasRoadMask && !IsRoadCollider(hit.collider))
        {
            return false;
        }

        return !IsSpawnSpaceBlocked(position);
    }

    public string ResolveNeighborhoodName(Vector3 position, float neighborhoodCheckRadius)
    {
        if (cachedNeighborhoodLayer == int.MinValue)
        {
            cachedNeighborhoodLayer = LayerMask.NameToLayer("Neighborhood");
            neighborhoodLayerMask = cachedNeighborhoodLayer >= 0 ? (1 << cachedNeighborhoodLayer) : ~0;
        }

        int hitCount = Physics.OverlapSphereNonAlloc(position, neighborhoodCheckRadius, sharedOverlapBuffer, neighborhoodLayerMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = sharedOverlapBuffer[i];
            if (col == null)
            {
                continue;
            }

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

    private bool TryFindNearestBuildingCollider(Vector3 origin, float radius, out Collider nearestBuilding, out Vector3 nearestPoint)
    {
        nearestBuilding = null;
        nearestPoint = Vector3.zero;

        int hitCount = Physics.OverlapSphereNonAlloc(origin, radius, sharedOverlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        float bestSqrDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = sharedOverlapBuffer[i];
            if (!IsBuildingCollider(col))
            {
                continue;
            }

            if (!TryGetClosestPointSafe(col, origin, out Vector3 point))
            {
                continue;
            }

            Vector3 delta = point - origin;
            delta.y = 0f;
            float sqrDistance = delta.sqrMagnitude;
            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                nearestBuilding = col;
                nearestPoint = point;
            }
        }

        return nearestBuilding != null;
    }

    private bool TryGetClosestPointSafe(Collider collider, Vector3 origin, out Vector3 point)
    {
        point = Vector3.zero;
        if (collider == null)
        {
            return false;
        }

        if (collider is MeshCollider meshCollider && !meshCollider.convex)
        {
            point = collider.bounds.ClosestPoint(origin);
            return float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z);
        }

        try
        {
            point = collider.ClosestPoint(origin);
            return float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z);
        }
        catch (Exception)
        {
            point = collider.bounds.ClosestPoint(origin);
            return float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z);
        }
    }

    private bool IsNearBuilding(Vector3 position, float radius)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(position, Mathf.Max(0.2f, radius), sharedOverlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            if (IsBuildingCollider(sharedOverlapBuffer[i]))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsNearRoad(Vector3 position, float radius)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(position, Mathf.Max(0.2f, radius), sharedOverlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = sharedOverlapBuffer[i];
            if (col == null || col.isTrigger)
            {
                continue;
            }

            if (IsRoadCollider(col))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsBuildingCollider(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        Transform current = collider.transform;
        for (int depth = 0; current != null && depth < 6; depth++)
        {
            if (IsBuildingObject(current.gameObject))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private bool IsBuildingObject(GameObject obj)
    {
        if (obj == null)
        {
            return false;
        }

        if (cachedBuildingLayer == int.MinValue)
        {
            cachedBuildingLayer = LayerMask.NameToLayer("Building");
        }

        if (cachedBuildingLayer >= 0 && obj.layer == cachedBuildingLayer)
        {
            return true;
        }

        int instanceId = obj.GetInstanceID();
        if (buildingObjectCache.TryGetValue(instanceId, out bool cached))
        {
            return cached;
        }

        if (buildingKeywordSet == null)
        {
            buildingKeywordSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < buildingNameKeywords.Length; i++)
            {
                string keyword = buildingNameKeywords[i];
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    buildingKeywordSet.Add(keyword.ToLowerInvariant());
                }
            }
        }

        bool result = false;
        if (buildingKeywordSet.Count > 0)
        {
            string lowerName = obj.name.ToLowerInvariant();
            foreach (string keyword in buildingKeywordSet)
            {
                if (lowerName.Contains(keyword))
                {
                    result = true;
                    break;
                }
            }
        }

        if (buildingObjectCache.Count >= 256)
        {
            buildingObjectCache.Clear();
        }

        buildingObjectCache[instanceId] = result;
        return result;
    }
}
