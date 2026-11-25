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
                    seed = 4281, spacing = 90f, nodesPerTile = 80, arterialSpacing = 400f, collectorSpacing = 200f,
                    urbanDesign = false,
                    grade = .12f, bridgeClearance = 4f, seaY = 0f,
                    buildAnkara = true, buildIstanbul = false, protectKizilay = false, bridges = false
                };
                var first = ProceduralWorldPlanner.Build(settings, layout, scene);
                var repeat = ProceduralWorldPlanner.Build(settings, layout, scene);
                Require(first.edges.Count == repeat.edges.Count, "Seed changed edge count.");
                for (int i = 0; i < first.edges.Count; i++)
                {
                    Require(first.edges[i].points.SequenceEqual(repeat.edges[i].points) && first.edges[i].kind == repeat.edges[i].kind, "Seed did not reproduce path/class " + i);
                }
                Verify(first, settings, 0);
                Require(first.edges.Any(e => e.kind == ProceduralWorldPlanner.RoadKind.Arterial) &&
                    first.edges.Any(e => e.kind == ProceduralWorldPlanner.RoadKind.Collector) &&
                    first.edges.Any(e => e.kind == ProceduralWorldPlanner.RoadKind.Local), "Missing a street hierarchy class.");
                settings.buildIstanbul = true;
                settings.bridges = true;
                ProceduralWorldPlanner.Plan both = null;
                // A random candidate graph can legitimately reject an infeasible seed.
                foreach (int seed in new[] { 4281, 5342, 22, 12345 })
                {
                    settings.seed = seed;
                    try { var candidate = ProceduralWorldPlanner.Build(settings, layout, scene); Verify(candidate, settings, 2); both = candidate; break; }
                    catch (InvalidOperationException ex) { Debug.LogWarning("[ProceduralWorldSmoke] Seed " + seed + ": " + ex.Message); }
                }
                Require(both != null, "No flat synthetic two-city seed could be planned.");
                Verify(both, settings, 2);
                // Remove only the sea row and repeat with 24 real Terrain components.
                foreach (var tile in layout.tiles.Where(t => t.row == 3).ToList())
                    Object.DestroyImmediate(tile.terrain.gameObject);
                var emptySea = ProceduralWorldPlanner.DetectLayout(scene);
                Require(emptySea.tiles.Count == 24 && emptySea.SeaTerrainCount == 0 && emptySea.rowsOnX,
                    "24-tile empty-sea layout detection failed.");
                Require(emptySea.sea == layout.sea, "Inferred sea bounds changed after removing sea tiles.");
                Require(!emptySea.TryHeight(emptySea.sea.center, out _), "Empty sea unexpectedly reports terrain.");
                var emptyPlan = ProceduralWorldPlanner.Build(settings, emptySea, scene);
                Verify(emptyPlan, settings, 2);
                // Raise the high-side city by 18m. The deck must adapt to asymmetric
                // endpoint heights and still respect the same maximum grade.
                foreach (var tile in emptySea.tiles.Where(t => t.row > 3))
                {
                    float[,] heights = new float[33, 33];
                    for (int z = 0; z < 33; z++)
                    for (int x = 0; x < 33; x++) heights[z, x] = 18f / 80f;
                    tile.terrain.terrainData.SetHeights(0, 0, heights);
                }
                var asymmetric = ProceduralWorldPlanner.Build(settings, emptySea, scene);
                Verify(asymmetric, settings, 2);
                // V5 read-only analyzer: one sample raster per actual land tile.
                var analysis = ProceduralWorldPlanner.AnalyzeOnly(settings, emptySea, scene);
                Require(analysis.terrainCells.Count > 0 &&
                    analysis.terrainCells.All(c => !emptySea.sea.Contains(ProceduralWorldPlanner.XZ(c.center))),
                    "Terrain analyzer did not exclude the sea corridor.");
                Require(analysis.terrainCells.All(c => c.slope >= 0f && !float.IsNaN(c.slope)),
                    "Invalid slope sample.");
                // Ankara regression: three height bands with sharp tile seams, on a
                // temporary scene only. V4 should connect all three depth bands
                // using the declared virtual grading budget, without editing terrain.
                settings.buildIstanbul = false;
                settings.bridges = false;
                settings.ankaraLow = true;
                settings.ankaraBackbone = true;
                settings.districtGrading = true;
                settings.districtGradingLimit = 30f;
                settings.grade = .10f;
                settings.maxCutFill = 6f;
                var land = emptySea.tiles.Where(t => t.row < 3).ToList();
                foreach (var tile in land)
                {
                    float[,] heights = new float[33, 33];
                    for (int z=0;z<33;z++)
                    for (int x=0;x<33;x++) heights[z,x] = (2-tile.row)*18f/80f;
                    tile.terrain.terrainData.SetHeights(0,0,heights);
                }
                var before = land.Select(t => t.terrain.terrainData.GetHeights(0,0,33,33).Cast<float>().ToArray()).ToList();
                var hillside = ProceduralWorldPlanner.Build(settings, emptySea, scene);
                Verify(hillside, settings, 0);
                Require(hillside.edges.Any(e => e.kind == ProceduralWorldPlanner.RoadKind.Arterial),
                    "Hillside preview has no backbone arterials.");
                Require(land.Where(t => hillside.edges.Any(e => e.points.Any(p =>
                    t.area.Contains(ProceduralWorldPlanner.XZ(p))))).Select(t => t.row).Distinct().Count() == 3,
                    "Hillside retained network did not reach all three depth bands.");
                Require(hillside.gradingMarkers.Count > 0, "Stepped terrain did not report any extra grading samples.");
                for(int i=0;i<land.Count;i++)
                    Require(before[i].SequenceEqual(land[i].terrain.terrainData.GetHeights(0,0,33,33).Cast<float>()),
                        "Preview modified synthetic terrain height data.");
                // Exercise V6 itself, without allowing the coverage guard to hide a failure.
                // Flat Istanbul permits an independent urban-layout regression.
                settings.urbanDesign = true;
                settings.urbanGuard = false;
                settings.buildAnkara = false;
                settings.buildIstanbul = true;
                settings.bridges = false;
                settings.openSpaceFraction = .12f;
                settings.seed = 4281;
                var urban = ProceduralWorldPlanner.Build(settings,emptySea,scene);
                Verify(urban,settings,0);
                Require(urban.version=="V6","V6 test unexpectedly selected a fallback.");
                Require(urban.openSpaces.Count>0,"No open-space reservations in the synthetic flat city.");
                Require(urban.seams.Count>0,"No seam analysis lines.");
                Require(urban.parcels.Count>0,"No building-lot proposals in flat city.");
                float margin=settings.width*.5f+settings.sidewalk+2f;
                foreach(var space in urban.openSpaces)
                foreach(var edge in urban.edges.Concat(urban.authoredLinks))
                for(int i=1;i<edge.points.Count;i++)
                    Require(!ProceduralWorldPlanner.SegmentHitsRect(ProceduralWorldPlanner.XZ(edge.points[i-1]),
                        ProceduralWorldPlanner.XZ(edge.points[i]),space.bounds,margin),"Road crosses reserved park/plaza.");
                Require(urban.parcels.All(l=>!urban.openSpaces.Any(r=>r.bounds.Overlaps(l.bounds))),"Lot overlaps open space.");
                Require(ProceduralWorldPlanner.SegmentHitsRect(new Vector2(-2,0),new Vector2(2,0),new Rect(-1,-1,2,2),0f),
                    "Reservation clipping missed a crossing.");
                Require(!ProceduralWorldPlanner.SegmentHitsRect(new Vector2(-2,3),new Vector2(2,3),new Rect(-1,-1,2,2),0f),
                    "Reservation clipping rejected a separated segment.");
                for(int i=0;i<land.Count;i++)
                    Require(before[i].SequenceEqual(land[i].terrain.terrainData.GetHeights(0,0,33,33).Cast<float>()),
                        "V6 modified original terrain.");
                // Rotate the placement convention (not the Terrain objects): the same layout must work on Z.
                foreach (var tile in emptySea.tiles)
                {
                    Vector3 pos = tile.terrain.transform.position;
                    tile.terrain.transform.position = new Vector3(pos.z, pos.y, pos.x);
                }
                var transposed = ProceduralWorldPlanner.DetectLayout(scene);
                Require(!transposed.rowsOnX && transposed.tiles.Count == 24 &&
                    Mathf.Abs(transposed.sea.height - 200f) < .01f, "Z-axis empty-sea detection failed.");
                // A wider empty sea is also valid; bridge length follows the actual shores.
                foreach (var tile in transposed.tiles.Where(t => t.row > 3))
                    tile.terrain.transform.position += Vector3.forward * 100f;
                var wider = ProceduralWorldPlanner.DetectLayout(scene);
                Require(Mathf.Abs(wider.sea.height - 300f) < .01f, "Actual sea gap width was not inferred.");
                Debug.Log("[ProceduralWorldSmoke] PASS: 28/24 terrain detection, empty sea bridges, X/Z orientation, real sea width, deterministic road plan, stepped Ankara coverage, V6 reservations/parcels and read-only grading checks. No assets written.");
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
            Require(plan.edges.All(e => e.kind != ProceduralWorldPlanner.RoadKind.AuthoredLink), "Preview-only proposals leaked into the bake graph.");
            Require(plan.edges.All(e => (e.kind == ProceduralWorldPlanner.RoadKind.Bridge) == e.Bridge), "Bridge class mismatch.");
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
