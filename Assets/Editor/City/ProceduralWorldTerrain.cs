using System;
using System.Collections.Generic;
using System.Linq;
using DeliveryDriver.City;
using UnityEditor;
using UnityEngine;

namespace DeliveryDriver.EditorTools
{
    internal static class ProceduralWorldTerrain
    {
        private struct Stamp
        {
            public Vector3 a, b;
            public bool bridge;
        }

        // Clone the source TerrainData instead of modifying original LFS assets.
        // The middle row is deliberately never written.
        internal static void Apply(
            ProceduralWorldPlanner.Plan plan,
            ProceduralWorldPlanner.Settings settings,
            ProceduralWorldBuildState state,
            string folder)
        {
            var stamps = new List<Stamp>();
            foreach (var edge in plan.edges)
            for (int i = 1; i < edge.points.Count; i++)
                stamps.Add(new Stamp { a = edge.points[i - 1], b = edge.points[i], bridge = edge.Bridge });

            int total = plan.layout.tiles.Count;
            for (int tileIndex = 0; tileIndex < total; tileIndex++)
            {
                var tile = plan.layout.tiles[tileIndex];
                if (tile.row == 3) continue;
                if (EditorUtility.DisplayCancelableProgressBar("Procedural World Generator", "Grading land tile " + (tileIndex + 1) + "/" + total, .08f + .48f * tileIndex / total))
                    throw new OperationCanceledException();
                Terrain terrain = tile.terrain;
                TerrainData original = terrain.terrainData;
                int resolution = original.heightmapResolution;
                float stepX = tile.area.width / (resolution - 1);
                float stepZ = tile.area.height / (resolution - 1);
                // Include a heightmap cell diagonal so interpolation at road edges does not lift terrain through asphalt.
                float core = settings.width * .5f + settings.sidewalk + Mathf.Sqrt(stepX * stepX + stepZ * stepZ);
                float radius = core + settings.blend;
                var relevant = stamps.Where(s => Overlaps(tile.area, s, radius)).ToList();
                if (relevant.Count == 0) continue;
                float[,] heights = original.GetHeights(0, 0, resolution, resolution);
                var best = new float[resolution, resolution];
                var target = new float[resolution, resolution];
                for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++) best[z, x] = float.PositiveInfinity;
                float radiusSq = radius * radius;
                float coreSq = core * core;
                foreach (Stamp stamp in relevant)
                {
                    Vector2 a = ProceduralWorldPlanner.XZ(stamp.a);
                    Vector2 b = ProceduralWorldPlanner.XZ(stamp.b);
                    Vector2 delta = b - a;
                    int x0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x, b.x) - radius - tile.area.xMin) / stepX), 0, resolution - 1);
                    int x1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.x, b.x) + radius - tile.area.xMin) / stepX), 0, resolution - 1);
                    int z0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.y, b.y) - radius - tile.area.yMin) / stepZ), 0, resolution - 1);
                    int z1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.y, b.y) + radius - tile.area.yMin) / stepZ), 0, resolution - 1);
                    for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                    {
                        Vector2 p = new Vector2(tile.area.xMin + x * stepX, tile.area.yMin + z * stepZ);
                        float t = delta.sqrMagnitude > .00001f ? Mathf.Clamp01(Vector2.Dot(p - a, delta) / delta.sqrMagnitude) : 0f;
                        float distanceSq = (p - (a + t * delta)).sqrMagnitude;
                        if (distanceSq > radiusSq || distanceSq >= best[z, x]) continue;
                        float current = terrain.transform.position.y + heights[z, x] * original.size.y;
                        float roadHeight = Mathf.Lerp(stamp.a.y, stamp.b.y, t);
                        // Elevated bridge approach sections stay elevated; the existing bridge mesh provides supports.
                        if (stamp.bridge && roadHeight - current >= 2.5f) continue;
                        best[z, x] = distanceSq;
                        target[z, x] = roadHeight - .20f;
                    }
                }
                bool modified = false;
                for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++)
                {
                    float d2 = best[z, x];
                    if (float.IsInfinity(d2)) continue;
                    Vector2 p = new Vector2(tile.area.xMin + x * stepX, tile.area.yMin + z * stepZ);
                    // Do not reshape protected districts or terrain under existing authored roads.
                    if (plan.protectedAreas.Any(r => ProceduralWorldPlanner.Contains(r, p)) ||
                        (plan.manualRoadNear != null && plan.manualRoadNear(p, settings.width * .5f + settings.sidewalk + 1f))) continue;
                    float current = terrain.transform.position.y + heights[z, x] * original.size.y;
                    float blend = d2 <= coreSq ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (Mathf.Sqrt(d2) - core) / settings.blend);
                    float shift = Mathf.Clamp(target[z, x] - current, -settings.maxCutFill, settings.maxCutFill) * blend;
                    float next = Mathf.Clamp01((current + shift - terrain.transform.position.y) / original.size.y);
                    if (Mathf.Abs(next - heights[z, x]) < .0000001f) continue;
                    heights[z, x] = next;
                    modified = true;
                }
                if (!modified) continue;
                TerrainData clone = UnityEngine.Object.Instantiate(original);
                clone.name = "GeneratedTerrain_" + tile.row + "_" + tile.column;
                AssetDatabase.CreateAsset(clone, folder + "/" + clone.name + ".asset");
                clone.SetHeights(0, 0, heights);
                clone.SyncHeightmap();
                EditorUtility.SetDirty(clone);
                state.terrains.Add(terrain);
                state.originalData.Add(original);
                state.generatedData.Add(clone);
                Assign(terrain, clone, "Assign generated terrain");
            }
        }

        private static bool Overlaps(Rect rect, Stamp stamp, float radius)
        {
            return Mathf.Max(stamp.a.x, stamp.b.x) + radius >= rect.xMin &&
                   Mathf.Min(stamp.a.x, stamp.b.x) - radius <= rect.xMax &&
                   Mathf.Max(stamp.a.z, stamp.b.z) + radius >= rect.yMin &&
                   Mathf.Min(stamp.a.z, stamp.b.z) - radius <= rect.yMax;
        }

        internal static void Assign(Terrain terrain, TerrainData data, string undoName)
        {
            Undo.RecordObject(terrain, undoName);
            terrain.terrainData = data;
            TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
            if (collider != null)
            {
                Undo.RecordObject(collider, undoName);
                collider.terrainData = data;
                EditorUtility.SetDirty(collider);
            }
            terrain.Flush();
            EditorUtility.SetDirty(terrain);
        }

        // Check center and both asphalt edges against the final, actually sampled heightmap.
        // Roll back a build rather than leave a buried road in the user's scene.
        internal static void Validate(ProceduralWorldPlanner.Plan plan, ProceduralWorldPlanner.Settings settings)
        {
            int buried = 0;
            float worst = 0f;
            Vector3 worstPoint = Vector3.zero;
            foreach (var edge in plan.edges)
            for (int i = 1; i < edge.points.Count; i++)
            {
                Vector3 a = edge.points[i - 1], b = edge.points[i];
                Vector2 delta = ProceduralWorldPlanner.XZ(b - a);
                if (delta.sqrMagnitude < .000001f) continue;
                Vector2 right = new Vector2(-delta.y, delta.x).normalized;
                int count = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude));
                for (int j = 0; j <= count; j++)
                {
                    Vector3 center = Vector3.Lerp(a, b, (float)j / count);
                    for (int side = -1; side <= 1; side++)
                    {
                        Vector2 p = ProceduralWorldPlanner.XZ(center) + right * side * settings.width * .5f;
                        if (!plan.layout.TryHeight(p, out float ground)) continue;
                        float penetration = ground - (center.y + .03f);
                        if (penetration > .08f)
                        {
                            buried++;
                            if (penetration > worst)
                            {
                                worst = penetration; worstPoint = ProceduralWorldPlanner.XYZ(p, center.y);
                            }
                        }
                    }
                }
            }
            if (buried > 0)
                throw new InvalidOperationException("Terrain clearance failed at " + buried + " samples (worst " + worst.ToString("F2") +
                    " m near " + worstPoint + "). The build was rolled back. Try more cut/fill, a different seed, or inspect protected terrain/heightmap resolution.");
        }
    }
}
