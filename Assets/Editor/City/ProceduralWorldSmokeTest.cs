using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeliveryDriver.EditorTools
{
    internal static class ProceduralWorldSmokeTest
    {
        [MenuItem("Tools/Delivery Driver/City/Run Procedural Planner Smoke Test", false, 2)]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene original = SceneManager.GetActiveScene();
            Scene scene = default;
            var data = new List<TerrainData>();
            try
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                for (int row = 0; row < 7; row++)
                for (int column = 0; column < 4; column++)
                {
                    var tileData = new TerrainData { heightmapResolution = 33, size = new Vector3(200f, 80f, 200f) };
                    data.Add(tileData);
                    GameObject tile = Terrain.CreateTerrainGameObject(tileData);
                    SceneManager.MoveGameObjectToScene(tile, scene);
                    tile.transform.position = new Vector3(row * 200f, 0f, column * 200f);
                }
                var layout = ProceduralWorldPlanner.DetectLayout(scene);
                Require(layout.tiles.Count == 28 && layout.rowsOnX, "28-tile layout detection failed.");
                Require(Mathf.Abs(layout.sea.width - 200f) < .01f && Mathf.Abs(layout.sea.height - 800f) < .01f,
                    "Incorrect sea corridor dimensions.");

                var settings = new ProceduralWorldPlanner.Settings
                {
                    seed = 4281, spacing = 90f, maxNodesPerCity = 80,
                    grade = .12f, bridgeClearance = 4f, seaY = 0f,
                    buildAnkara = true, buildIstanbul = false, protectKizilay = false, bridges = false
                };
                var first = ProceduralWorldPlanner.Build(settings, layout, scene);
                var repeat = ProceduralWorldPlanner.Build(settings, layout, scene);
                Require(first.edges.Count == repeat.edges.Count, "Seed changed edge count.");
                for (int i = 0; i < first.edges.Count; i++)
                {
                    Require(first.edges[i].points.SequenceEqual(repeat.edges[i].points), "Seed did not reproduce path " + i);
                }
                Verify(first, settings, 0);
                settings.buildIstanbul = true;
                settings.bridges = true;
                ProceduralWorldPlanner.Plan both = null;
                // A random candidate graph can legitimately reject an infeasible seed.
                foreach (int seed in new[] { 4281, 5342, 22, 12345 })
                {
                    settings.seed = seed;
                    try { both = ProceduralWorldPlanner.Build(settings, layout, scene); break; }
                    catch (InvalidOperationException ex) { Debug.LogWarning("[ProceduralWorldSmoke] Seed " + seed + ": " + ex.Message); }
                }
                Require(both != null, "No flat synthetic two-city seed could be planned.");
                Verify(both, settings, 2);
                Debug.Log("[ProceduralWorldSmoke] PASS: 28-tile detection, deterministic Ankara plan, two-city graph, two bridge paths and grade checks. No assets written.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[ProceduralWorldSmoke] FAIL: " + ex);
            }
            finally
            {
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                foreach (TerrainData tileData in data) Object.DestroyImmediate(tileData);
            }
        }

        private static void Verify(ProceduralWorldPlanner.Plan plan, ProceduralWorldPlanner.Settings settings, int bridges)
        {
            Require(plan.edges.Count > 0 && plan.BridgeCount == bridges, "Unexpected bridge or road count.");
            foreach (var e in plan.edges)
            {
                Require(e.points.Count >= 2 && e.points[0] == e.a.position && e.points[e.points.Count - 1] == e.b.position,
                    "Path endpoint mismatch.");
                bool crossesSea = false;
                for (int i = 0; i < e.points.Count; i++)
                {
                    Vector3 p = e.points[i];
                    if (plan.layout.sea.Contains(ProceduralWorldPlanner.XZ(p))) crossesSea = true;
                    if (i == 0) continue;
                    Vector3 prev = e.points[i - 1];
                    float horizontal = Vector2.Distance(ProceduralWorldPlanner.XZ(p), ProceduralWorldPlanner.XZ(prev));
                    Require(Mathf.Abs(p.y - prev.y) <= horizontal * settings.grade + .003f, "Grade limit exceeded.");
                }
                Require(crossesSea == e.Bridge, "A road has the wrong sea corridor classification.");
            }
            var visited = new HashSet<ProceduralWorldPlanner.Node>();
            var queue = new Queue<ProceduralWorldPlanner.Node>();
            queue.Enqueue(plan.edges[0].a);
            visited.Add(plan.edges[0].a);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                foreach (var e in plan.edges)
                {
                    var other = e.a == node ? e.b : e.b == node ? e.a : null;
                    if (other != null && visited.Add(other)) queue.Enqueue(other);
                }
            }
            Require(plan.edges.All(e => visited.Contains(e.a) && visited.Contains(e.b)), "Disconnected road graph.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
