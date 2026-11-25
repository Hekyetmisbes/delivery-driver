using System;
using System.Collections.Generic;
using System.Linq;
using DeliveryDriver.City;
using TrafficSystem.Spline;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryDriver.EditorTools
{
    internal sealed class ProceduralWorldPlanner
    {
        internal sealed class Settings
        {
            public int seed = 4281;
            public float spacing = 110f, width = 8.5f, sidewalk = 2.2f, grade = 0.10f;
            public float maxCutFill = 6f, blend = 10f, seaY, bridgeClearance = 8f;
            public float curve = 0.65f, loops = 0.55f;
            public bool ankaraLow = true, protectKizilay = true, buildAnkara = true, buildIstanbul = true, bridges = true;
            public Transform ankara, istanbul;
        }

        internal sealed class Tile
        {
            public Terrain terrain;
            public Rect area;
            public int row, column;
            public float Height(Vector2 p)
            {
                return terrain.transform.position.y + terrain.terrainData.GetInterpolatedHeight(
                    Mathf.Clamp01((p.x - area.xMin) / area.width),
                    Mathf.Clamp01((p.y - area.yMin) / area.height));
            }
        }

        internal sealed class Layout
        {
            public readonly List<Tile> tiles = new List<Tile>();
            public Rect low, high, sea;
            public bool rowsOnX;
            public Rect Ankara(bool lowSide) => lowSide ? low : high;
            public Rect Istanbul(bool lowSide) => lowSide ? high : low;
            public bool TryHeight(Vector2 p, out float height)
            {
                foreach (Tile tile in tiles)
                {
                    if (!Contains(tile.area, p, 0.01f)) continue;
                    height = tile.Height(p);
                    return true;
                }
                height = 0f;
                return false;
            }
            public Vector2 Point(float along, float across) => rowsOnX ? new Vector2(along, across) : new Vector2(across, along);
            public float AlongMin(Rect r) => rowsOnX ? r.xMin : r.yMin;
            public float AlongMax(Rect r) => rowsOnX ? r.xMax : r.yMax;
            public float AcrossMin(Rect r) => rowsOnX ? r.yMin : r.xMin;
            public float AcrossMax(Rect r) => rowsOnX ? r.yMax : r.xMax;
        }

        internal sealed class Node
        {
            public int id;
            public Vector3 position;
            public string sector;
            public RoadIntersection existing;
            public bool gate;
            public int degree;
        }

        internal sealed class Edge
        {
            public Node a, b;
            public List<Vector3> points;
            public string sector;
            public bool Bridge => sector == "Highways";
        }

        internal sealed class Plan
        {
            public Layout layout;
            public readonly List<Node> nodes = new List<Node>();
            public readonly List<Edge> edges = new List<Edge>();
            public readonly List<string> warnings = new List<string>();
            public int BridgeCount => edges.Count(e => e.Bridge);
        }

        private struct Segment
        {
            public Vector2 a, b;
        }

        // Spatial hash for existing manual roads. No scene objects are edited during planning.
        private sealed class Obstacles
        {
            private const float Cell = 25f;
            private readonly Dictionary<Vector2Int, List<Segment>> cells = new Dictionary<Vector2Int, List<Segment>>();
            private static int Index(float v) => Mathf.FloorToInt(v / Cell);
            public void Add(Vector2 a, Vector2 b)
            {
                var segment = new Segment { a = a, b = b };
                for (int x = Index(Mathf.Min(a.x, b.x)); x <= Index(Mathf.Max(a.x, b.x)); x++)
                for (int y = Index(Mathf.Min(a.y, b.y)); y <= Index(Mathf.Max(a.y, b.y)); y++)
                {
                    var key = new Vector2Int(x, y);
                    if (!cells.TryGetValue(key, out List<Segment> list))
                        cells[key] = list = new List<Segment>();
                    list.Add(segment);
                }
            }
            public bool Near(Vector2 p, float radius)
            {
                for (int x = Index(p.x - radius); x <= Index(p.x + radius); x++)
                for (int y = Index(p.y - radius); y <= Index(p.y + radius); y++)
                {
                    if (!cells.TryGetValue(new Vector2Int(x, y), out List<Segment> list)) continue;
                    foreach (Segment s in list)
                        if (DistanceSquared(p, s.a, s.b) < radius * radius) return true;
                }
                return false;
            }
        }

        private sealed class District
        {
            public Rect area;
            public string name;
        }

        private readonly Settings settings;
        private readonly System.Random random;
        private readonly Plan plan;
        private readonly Obstacles obstacles = new Obstacles();
        private readonly List<District> districts = new List<District>();
        private readonly List<Rect> protectedAreas = new List<Rect>();
        private readonly List<SplineRoad> manualRoads = new List<SplineRoad>();

        private ProceduralWorldPlanner(Settings settings, Layout layout, Scene scene)
        {
            this.settings = settings;
            plan = new Plan { layout = layout };
            random = new System.Random(settings.seed);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (NeighborhoodZone zone in root.GetComponentsInChildren<NeighborhoodZone>(true))
                {
                    BoxCollider box = zone.GetComponent<BoxCollider>();
                    if (box == null) continue;
                    Rect area = WorldRect(box);
                    string name = zone.NeighborhoodName ?? zone.name;
                    districts.Add(new District { area = area, name = name });
                    if (settings.protectKizilay &&
                        (name.IndexOf("Kizilay", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("Kızılay", StringComparison.OrdinalIgnoreCase) >= 0))
                        protectedAreas.Add(area);
                }
                foreach (SplineRoad road in root.GetComponentsInChildren<SplineRoad>(true))
                {
                    if (!road.IsUsable) continue;
                    manualRoads.Add(road);
                    float length = road.GetTotalLength();
                    int count = Mathf.Max(1, Mathf.CeilToInt(length / 4f));
                    Vector2 prev = XZ(road.GetPoint(0f));
                    for (int i = 1; i <= count; i++)
                    {
                        Vector2 p = XZ(road.GetPoint(road.DistanceToT(length * i / count)));
                        obstacles.Add(prev, p);
                        prev = p;
                    }
                }
            }
        }

        internal static Layout DetectLayout(Scene scene)
        {
            var terrains = new List<Terrain>();
            foreach (GameObject root in scene.GetRootGameObjects())
                terrains.AddRange(root.GetComponentsInChildren<Terrain>(true).Where(t => t.terrainData != null));
            if (terrains.Count != 28)
                throw new InvalidOperationException("Expected 28 Terrain objects (12 Ankara + 4 sea + 12 Istanbul); found " + terrains.Count + ".");
            var layout = new Layout();
            Vector3 size = terrains[0].terrainData.size;
            float epsilon = Mathf.Max(0.1f, Mathf.Min(size.x, size.z) * 0.002f);
            foreach (Terrain t in terrains)
            {
                Vector3 p = t.transform.position, s = t.terrainData.size;
                if (Mathf.Abs(s.x - size.x) > epsilon || Mathf.Abs(s.z - size.z) > epsilon ||
                    Quaternion.Angle(t.transform.rotation, Quaternion.identity) > 0.01f ||
                    (t.transform.lossyScale - Vector3.one).sqrMagnitude > 0.0001f)
                    throw new InvalidOperationException("Terrain tiles must be axis-aligned, equally sized and unscaled.");
                layout.tiles.Add(new Tile { terrain = t, area = new Rect(p.x, p.z, s.x, s.z) });
            }
            List<float> xs = Cluster(layout.tiles.Select(t => t.area.xMin), epsilon);
            List<float> zs = Cluster(layout.tiles.Select(t => t.area.yMin), epsilon);
            if (!((xs.Count == 7 && zs.Count == 4) || (xs.Count == 4 && zs.Count == 7)))
                throw new InvalidOperationException("Terrain layout must be 4 x 7 contiguous tiles; detected " + xs.Count + " x " + zs.Count + ".");
            for (int i = 1; i < xs.Count; i++)
                if (Mathf.Abs(xs[i] - xs[i - 1] - size.x) > epsilon) throw new InvalidOperationException("Terrain X seam has a gap or overlap.");
            for (int i = 1; i < zs.Count; i++)
                if (Mathf.Abs(zs[i] - zs[i - 1] - size.z) > epsilon) throw new InvalidOperationException("Terrain Z seam has a gap or overlap.");
            layout.rowsOnX = xs.Count == 7;
            var occupied = new HashSet<Vector2Int>();
            foreach (Tile tile in layout.tiles)
            {
                int x = xs.FindIndex(v => Mathf.Abs(v - tile.area.xMin) <= epsilon);
                int z = zs.FindIndex(v => Mathf.Abs(v - tile.area.yMin) <= epsilon);
                tile.row = layout.rowsOnX ? x : z;
                tile.column = layout.rowsOnX ? z : x;
                if (!occupied.Add(new Vector2Int(tile.row, tile.column)))
                    throw new InvalidOperationException("Overlapping terrain tile coordinates.");
            }
            layout.tiles.Sort((a, b) => a.row == b.row ? a.column.CompareTo(b.column) : a.row.CompareTo(b.row));
            layout.low = Union(layout.tiles.Where(t => t.row < 3).Select(t => t.area));
            layout.sea = Union(layout.tiles.Where(t => t.row == 3).Select(t => t.area));
            layout.high = Union(layout.tiles.Where(t => t.row > 3).Select(t => t.area));
            return layout;
        }

        internal static Plan Build(Settings settings, Layout layout, Scene scene)
        {
            var planner = new ProceduralWorldPlanner(settings, layout, scene);
            if (settings.bridges && settings.buildAnkara && settings.buildIstanbul)
                planner.PlanBridges();
            if (settings.buildAnkara) planner.PlanCity("Ankara", layout.Ankara(settings.ankaraLow));
            if (settings.buildIstanbul) planner.PlanCity("İstanbul", layout.Istanbul(settings.ankaraLow));
            if (planner.plan.edges.Count == 0) throw new InvalidOperationException("No valid roads. Increase spacing or cut/fill tolerance, or check the terrain.");
            return planner.plan;
        }

        private void PlanBridges()
        {
            Layout l = plan.layout;
            float min = l.AcrossMin(l.sea), span = l.AcrossMax(l.sea) - min;
            float lowDepth = l.AlongMax(l.low) - l.AlongMin(l.low);
            float highDepth = l.AlongMax(l.high) - l.AlongMin(l.high);
            var used = new List<float>();
            // Search across the actual shore, not a fixed 220m offset.
            float[] candidates = { .18f, .82f, .33f, .67f, .48f, .10f, .90f, .27f, .73f, .42f, .58f };
            foreach (float fraction in candidates)
            {
                if (used.Any(v => Mathf.Abs(v - fraction) < .14f)) continue;
                float across = min + span * fraction;
                bool accepted = false;
                foreach (float inset in new[] { .12f, .20f, .30f, .42f })
                {
                    Vector2 a = l.Point(l.AlongMax(l.low) - lowDepth * inset, across);
                    Vector2 b = l.Point(l.AlongMin(l.high) + highDepth * inset, across);
                    if (!TryBridge(a, b, out List<Vector3> path)) continue;
                    Node first = AddNode(path[0], settings.ankaraLow ? "Ankara" : "İstanbul", true);
                    Node last = AddNode(path[path.Count - 1], settings.ankaraLow ? "İstanbul" : "Ankara", true);
                    AddEdge(first, last, path, "Highways");
                    used.Add(fraction);
                    accepted = true;
                    break;
                }
                if (accepted && used.Count == 2) return;
            }
            throw new InvalidOperationException("Could not plan two safe bridges. Check Sea Level, protected Kizilay, grade, terrain height and existing roads. Try a higher cut/fill budget or lower bridge clearance.");
        }

        private bool TryBridge(Vector2 a, Vector2 b, out List<Vector3> points)
        {
            points = new List<Vector3>();
            Layout l = plan.layout;
            float shoreA = l.AlongMin(l.sea), shoreB = l.AlongMax(l.sea);
            float alongA = l.rowsOnX ? a.x : a.y, alongB = l.rowsOnX ? b.x : b.y;
            float across = l.rowsOnX ? a.y : a.x;
            if (!l.TryHeight(a, out float yA) || !l.TryHeight(b, out float yB)) return false;
            float deck = settings.seaY + settings.bridgeClearance;
            int seaSteps = Mathf.Max(2, Mathf.CeilToInt((shoreB - shoreA) / 4f));
            for (int i = 0; i <= seaSteps; i++)
            {
                float along = Mathf.Lerp(shoreA, shoreB, (float)i / seaSteps);
                for (int side = -1; side <= 1; side++)
                    if (l.TryHeight(l.Point(along, across + side * settings.width * 0.5f), out float ground))
                        deck = Mathf.Max(deck, ground + 3f);
            }
            yA += .12f; yB += .12f;
            deck = Mathf.Max(deck, Mathf.Max(yA, yB));
            int steps = Mathf.Max(2, Mathf.CeilToInt((alongB - alongA) / 2.5f));
            for (int i = 0; i <= steps; i++)
            {
                float along = Mathf.Lerp(alongA, alongB, (float)i / steps);
                Vector2 p = l.Point(along, across);
                if (obstacles.Near(p, RoadClearance) || IsProtected(p, RoadClearance)) return false;
                float y = along < shoreA ? Mathf.SmoothStep(yA, deck, (along - alongA) / (shoreA - alongA)) :
                    along > shoreB ? Mathf.SmoothStep(deck, yB, (along - shoreB) / (alongB - shoreB)) : deck;
                if (!l.TryHeight(p, out float ground) || ground > y + settings.maxCutFill - .12f) return false;
                Vector3 point = XYZ(p, y);
                if (points.Count > 0 && !GradeOk(points[points.Count - 1], point)) return false;
                points.Add(point);
            }
            return true;
        }

        private void PlanCity(string sector, Rect bounds)
        {
            var nodes = plan.nodes.Where(n => n.sector == sector).ToList();
            float spacing = Mathf.Max(settings.spacing, Mathf.Sqrt(bounds.width * bounds.height / 170f));
            if (spacing > settings.spacing + 1f)
                plan.warnings.Add(sector + ": spacing increased to " + spacing.ToString("F0") + " m for MVP road budget.");
            float inset = RoadClearance + 3f;
            foreach (District district in districts)
            {
                Vector2 p = district.area.center;
                if (Contains(bounds, p, -inset) && CanPlaceNode(p, nodes, spacing * .55f))
                    nodes.Add(AddLandNode(p, sector));
            }
            int target = Mathf.Clamp(Mathf.RoundToInt(bounds.width * bounds.height / (spacing * spacing)), 8, 170);
            for (int i = 0; i < target * 45 && nodes.Count < target; i++)
            {
                Vector2 p = new Vector2(Mathf.Lerp(bounds.xMin + inset, bounds.xMax - inset, Next()),
                                        Mathf.Lerp(bounds.yMin + inset, bounds.yMax - inset, Next()));
                float localSpacing = spacing * DistrictSpacing(p);
                if (CanPlaceNode(p, nodes, localSpacing * .62f) && plan.layout.TryHeight(p, out _))
                    nodes.Add(AddLandNode(p, sector));
            }

            // Only attach to an existing dead end; do not split or alter authored geometry.
            Node external = FindManualAnchor(sector, bounds, nodes);
            if (external != null) nodes.Add(external);
            else if (manualRoads.Any(r => Contains(bounds, XZ(r.GetPoint(.5f)))))
                plan.warnings.Add(sector + ": no suitable authored dead end found; existing roads remain untouched.");

            var candidates = new List<(Node a, Node b, float cost)>();
            var seen = new HashSet<(int, int)>();
            foreach (Node a in nodes)
            {
                foreach (Node b in nodes.Where(n => n != a).OrderBy(n => (XZ(n.position) - XZ(a.position)).sqrMagnitude).ThenBy(n => n.id).Take(14))
                {
                    int first = Mathf.Min(a.id, b.id), second = Mathf.Max(a.id, b.id);
                    if (!seen.Add((first, second))) continue;
                    float dist = Vector2.Distance(XZ(a.position), XZ(b.position));
                    if (dist < 28f || dist > spacing * 3.2f) continue;
                    candidates.Add((a, b, dist + Mathf.Abs(a.position.y - b.position.y) * 2f));
                }
            }
            candidates.Sort((x, y) =>
            {
                int c = x.cost.CompareTo(y.cost);
                if (c != 0) return c;
                c = x.a.id.CompareTo(y.a.id);
                return c != 0 ? c : x.b.id.CompareTo(y.b.id);
            });

            var groups = nodes.ToDictionary(n => n, n => n);
            Node Find(Node n)
            {
                while (groups[n] != n) n = groups[n];
                return n;
            }
            var added = new HashSet<(int, int)>();
            // First spanning forest, then extra cycles, using the same safety checks.
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var c in candidates)
                {
                    var key = (Mathf.Min(c.a.id, c.b.id), Mathf.Max(c.a.id, c.b.id));
                    if (added.Contains(key) || c.a.degree >= DegreeLimit(c.a) || c.b.degree >= DegreeLimit(c.b)) continue;
                    bool same = Find(c.a) == Find(c.b);
                    if ((pass == 0 && same) || (pass == 1 && (!same || Next() > settings.loops))) continue;
                    if (!TryLandRoad(c.a, c.b, bounds, out List<Vector3> path)) continue;
                    AddEdge(c.a, c.b, path, sector);
                    added.Add(key);
                    if (!same) groups[Find(c.b)] = Find(c.a);
                }
            }

            // Keep only the connected component containing both bridge gates, or the largest component.
            List<Node> gates = nodes.Where(n => n.gate).ToList();
            Node keep = gates.Count > 0 ? Find(gates[0]) :
                nodes.GroupBy(Find).OrderByDescending(g => g.Count()).ThenBy(g => g.Key.id).Select(g => g.Key).FirstOrDefault();
            if (keep == null || gates.Any(g => Find(g) != keep))
                throw new InvalidOperationException(sector + ": both bridge approaches could not be joined through safe roads. Try a different seed or spacing.");
            int dropped = nodes.Count(n => Find(n) != keep);
            foreach (Edge e in plan.edges.Where(e => e.sector == sector && Find(e.a) != keep).ToList())
            {
                e.a.degree--; e.b.degree--; plan.edges.Remove(e);
            }
            if (dropped > 0) plan.warnings.Add(sector + ": " + dropped + " isolated/steep candidate nodes skipped.");
            if (external != null && external.degree == 0)
                plan.warnings.Add(sector + ": authored road connection rejected by terrain or clearance rules.");
            if (!plan.edges.Any(e => e.sector == sector))
                throw new InvalidOperationException(sector + ": no connected city streets could be planned.");
            MergeTwoWayRoads(sector);
        }

        private void MergeTwoWayRoads(string sector)
        {
            // Keep bends continuous without building a separate two-way intersection mesh.
            bool changed;
            do
            {
                changed = false;
                foreach (Node n in plan.nodes)
                {
                    if (n.sector != sector || n.degree != 2 || n.gate || n.existing != null) continue;
                    var incident = plan.edges.Where(e => e.sector == sector && (e.a == n || e.b == n)).ToList();
                    if (incident.Count != 2) continue;
                    Edge first = incident[0], second = incident[1];
                    Node a = first.a == n ? first.b : first.a, b = second.a == n ? second.b : second.a;
                    if (a == b) continue;
                    // Avoid excessive control point/mesh counts on very long merged roads.
                    if (first.points.Count + second.points.Count > 220) continue;
                    var path = new List<Vector3>(first.points);
                    if (first.a == n) path.Reverse();
                    var tail = new List<Vector3>(second.points);
                    if (second.b == n) tail.Reverse();
                    path.AddRange(tail.Skip(1));
                    plan.edges.Remove(first); plan.edges.Remove(second);
                    a.degree--; b.degree--; n.degree = 0;
                    AddEdge(a, b, path, sector);
                    changed = true;
                    break;
                }
            } while (changed);
        }

        private Node FindManualAnchor(string sector, Rect bounds, List<Node> nodes)
        {
            RoadIntersection best = null;
            Vector3 bestPosition = default;
            float bestDist = float.MaxValue;
            foreach (SplineRoad road in manualRoads)
            {
                foreach (bool start in new[] { true, false })
                {
                    RoadIntersection junction = start ? road.startIntersection : road.endIntersection;
                    if (junction == null || junction.connectedRoads == null ||
                        junction.connectedRoads.Count != 1 || junction.connectedRoads[0] != road) continue;
                    Vector3 p = road.GetPoint(start ? 0f : 1f);
                    if (!Contains(bounds, XZ(p), -RoadClearance) ||
                        Vector3.Distance(p, junction.transform.position) > .3f) continue;
                    float dist = nodes.Count == 0 ? float.MaxValue :
                        nodes.Min(n => (XZ(n.position) - XZ(p)).sqrMagnitude);
                    if (dist < bestDist)
                    {
                        best = junction; bestPosition = p; bestDist = dist;
                    }
                }
            }
            if (best == null) return null;
            Node result = AddNode(bestPosition, sector, false);
            result.existing = best;
            return result;
        }

        private float RoadClearance => settings.width * .5f + settings.sidewalk + 2f;
        private int DegreeLimit(Node node) => node.existing != null ? 1 : 4;
        private bool CanPlaceNode(Vector2 p, List<Node> nodes, float spacing) =>
            !IsProtected(p, RoadClearance) && !obstacles.Near(p, RoadClearance * 2f) &&
            nodes.All(n => (XZ(n.position) - p).sqrMagnitude > spacing * spacing);

        private bool IsProtected(Vector2 p, float margin) => protectedAreas.Any(r => Contains(r, p, margin));

        private float DistrictSpacing(Vector2 p)
        {
            foreach (District district in districts)
            {
                if (!district.area.Contains(p)) continue;
                string n = district.name.ToLowerInvariant();
                if (n.Contains("hamam") || n.Contains("galata") || n.Contains("taksim")) return .72f;
                if (n.Contains("ostim") || n.Contains("plato") || n.Contains("havaliman")) return 1.20f;
                if (n.Contains("atakule") || n.Contains("yama")) return 1.12f;
            }
            return 1f;
        }

        private bool TryLandRoad(Node a, Node b, Rect bounds, out List<Vector3> points)
        {
            points = null;
            Vector2 start = XZ(a.position), end = XZ(b.position);
            float length = Vector2.Distance(start, end);
            Vector2 direction = (end - start).normalized, side = new Vector2(-direction.y, direction.x);
            float sign = Next() < .5f ? -1f : 1f;
            float bend = Mathf.Min(length * .18f, 24f) * settings.curve * sign;
            foreach (float offset in new[] { bend, -bend, 0f, bend * 1.7f, -bend * 1.7f })
            {
                int steps = Mathf.Max(2, Mathf.CeilToInt(length / 3f));
                var path = new List<Vector3>(steps + 1);
                bool valid = true;
                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    Vector2 p = Vector2.Lerp(start, end, t) + side * (4f * t * (1f - t) * offset);
                    bool anchor = (a.existing != null && Vector2.Distance(p, start) < 20f) ||
                                  (b.existing != null && Vector2.Distance(p, end) < 20f);
                    if (!Contains(bounds, p, -RoadClearance) ||
                        (!anchor && (IsProtected(p, RoadClearance) || obstacles.Near(p, RoadClearance * 2f))) ||
                        !plan.layout.TryHeight(p, out float ground))
                    {
                        valid = false; break;
                    }
                    path.Add(XYZ(p, ground + .12f));
                }
                if (!valid) continue;
                path[0] = a.position;
                path[path.Count - 1] = b.position;
                if (!SolveElevation(path) || !AnglesOk(a, b, path) || CrossesRoad(a, b, path)) continue;
                points = path;
                return true;
            }
            return false;
        }

        private bool SolveElevation(List<Vector3> path)
        {
            int n = path.Count;
            var lo = new float[n]; var hi = new float[n]; var target = new float[n]; var step = new float[n];
            float roadHalf = settings.width * .5f + settings.sidewalk;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = XZ(path[i]);
                if (!plan.layout.TryHeight(p, out float h)) return false;
                Vector2 tangent = XZ(path[Mathf.Min(n - 1, i + 1)]) - XZ(path[Mathf.Max(0, i - 1)]);
                Vector2 right = new Vector2(-tangent.y, tangent.x).normalized;
                lo[i] = h + .12f - settings.maxCutFill;
                hi[i] = h + .12f + settings.maxCutFill;
                for (int s = -1; s <= 1; s += 2)
                {
                    if (!plan.layout.TryHeight(p + right * roadHalf * s, out float cross)) return false;
                    lo[i] = Mathf.Max(lo[i], cross + .12f - settings.maxCutFill);
                    hi[i] = Mathf.Min(hi[i], cross + .12f + settings.maxCutFill);
                }
                target[i] = h + .12f;
                if (i > 0) step[i] = Vector2.Distance(XZ(path[i - 1]), p) * settings.grade;
                if (lo[i] > hi[i]) return false;
            }
            if (path[0].y < lo[0] || path[0].y > hi[0] ||
                path[n - 1].y < lo[n - 1] || path[n - 1].y > hi[n - 1]) return false;
            lo[0] = hi[0] = path[0].y;
            lo[n - 1] = hi[n - 1] = path[n - 1].y;
            for (int i = 1; i < n; i++)
            {
                lo[i] = Mathf.Max(lo[i], lo[i - 1] - step[i]);
                hi[i] = Mathf.Min(hi[i], hi[i - 1] + step[i]);
                if (lo[i] > hi[i] + .0001f) return false;
            }
            for (int i = n - 2; i >= 0; i--)
            {
                lo[i] = Mathf.Max(lo[i], lo[i + 1] - step[i + 1]);
                hi[i] = Mathf.Min(hi[i], hi[i + 1] + step[i + 1]);
                if (lo[i] > hi[i] + .0001f) return false;
            }
            // Smooth preferred profile, then clamp to the proven feasible interval.
            for (int pass = 0; pass < 3; pass++)
            {
                float[] old = (float[])target.Clone();
                for (int i = 1; i < n - 1; i++) target[i] = (old[i - 1] + 2f * old[i] + old[i + 1]) * .25f;
            }
            for (int i = 1; i < n - 1; i++)
            {
                float min = Mathf.Max(lo[i], path[i - 1].y - step[i]);
                float max = Mathf.Min(hi[i], path[i - 1].y + step[i]);
                if (min > max + .0001f) return false;
                Vector3 p = path[i]; p.y = Mathf.Clamp(target[i], min, Mathf.Max(min, max)); path[i] = p;
            }
            for (int i = 1; i < n; i++) if (!GradeOk(path[i - 1], path[i])) return false;
            return true;
        }

        private bool AnglesOk(Node a, Node b, List<Vector3> path)
        {
            foreach (Node n in new[] { a, b })
            {
                Vector2 dir = n == a ? XZ(path[1] - path[0]).normalized : XZ(path[path.Count - 2] - path[path.Count - 1]).normalized;
                foreach (Edge e in plan.edges)
                {
                    if (e.a != n && e.b != n) continue;
                    Vector2 old = e.a == n ? XZ(e.points[1] - e.points[0]).normalized :
                        XZ(e.points[e.points.Count - 2] - e.points[e.points.Count - 1]).normalized;
                    // Avoid nearly overlapping approaches; bridge approach should be nearly straight.
                    float minimum = e.Bridge ? 140f : 42f;
                    if (Vector2.Angle(dir, old) < minimum) return false;
                }
                if (n.existing != null)
                {
                    SplineRoad road = n.existing.connectedRoads[0];
                    Vector3 old = road.startIntersection == n.existing ? road.GetTangent(0f) : -road.GetTangent(1f);
                    if (Vector2.Angle(dir, XZ(old)) < 150f) return false;
                }
            }
            return true;
        }

        private bool CrossesRoad(Node a, Node b, List<Vector3> path)
        {
            float gap = RoadClearance * 2f;
            Rect box = BoundsOf(path, gap);
            foreach (Edge edge in plan.edges)
            {
                if (!box.Overlaps(BoundsOf(edge.points, gap))) continue;
                Node shared = edge.a == a || edge.b == a ? a : edge.a == b || edge.b == b ? b : null;
                for (int i = 0; i < path.Count; i++)
                {
                    Vector2 p = XZ(path[i]);
                    if (shared != null && Vector2.Distance(p, XZ(shared.position)) < gap * 1.65f) continue;
                    for (int j = 1; j < edge.points.Count; j++)
                    {
                        if (DistanceSquared(p, XZ(edge.points[j - 1]), XZ(edge.points[j])) < gap * gap) return true;
                    }
                }
            }
            return false;
        }

        private bool GradeOk(Vector3 a, Vector3 b) =>
            Mathf.Abs(b.y - a.y) <= Vector2.Distance(XZ(a), XZ(b)) * settings.grade + .002f;

        private Node AddLandNode(Vector2 p, string sector)
        {
            plan.layout.TryHeight(p, out float h);
            return AddNode(XYZ(p, h + .12f), sector, false);
        }
        private Node AddNode(Vector3 p, string sector, bool gate)
        {
            var node = new Node { id = plan.nodes.Count, position = p, sector = sector, gate = gate };
            plan.nodes.Add(node); return node;
        }
        private void AddEdge(Node a, Node b, List<Vector3> points, string sector)
        {
            plan.edges.Add(new Edge { a = a, b = b, points = points, sector = sector });
            a.degree++; b.degree++;
        }
        private float Next() => (float)random.NextDouble();

        internal static Vector2 XZ(Vector3 p) => new Vector2(p.x, p.z);
        internal static Vector3 XYZ(Vector2 p, float y) => new Vector3(p.x, y, p.y);
        internal static bool Contains(Rect r, Vector2 p, float margin = 0f) =>
            p.x >= r.xMin - margin && p.x <= r.xMax + margin && p.y >= r.yMin - margin && p.y <= r.yMax + margin;
        internal static float DistanceSquared(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 delta = b - a;
            float t = delta.sqrMagnitude < .000001f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, delta) / delta.sqrMagnitude);
            return (p - (a + t * delta)).sqrMagnitude;
        }
        private static Rect BoundsOf(List<Vector3> path, float margin)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (Vector3 p in path)
            {
                x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x);
                y0 = Mathf.Min(y0, p.z); y1 = Mathf.Max(y1, p.z);
            }
            return Rect.MinMaxRect(x0 - margin, y0 - margin, x1 + margin, y1 + margin);
        }
        private static List<float> Cluster(IEnumerable<float> values, float epsilon)
        {
            var list = new List<float>();
            foreach (float v in values.OrderBy(x => x))
                if (list.Count == 0 || Mathf.Abs(v - list[list.Count - 1]) > epsilon) list.Add(v);
            return list;
        }
        private static Rect Union(IEnumerable<Rect> areas)
        {
            Rect result = areas.First();
            foreach (Rect r in areas)
                result = Rect.MinMaxRect(Mathf.Min(result.xMin, r.xMin), Mathf.Min(result.yMin, r.yMin),
                    Mathf.Max(result.xMax, r.xMax), Mathf.Max(result.yMax, r.yMax));
            return result;
        }
        private static Rect WorldRect(BoxCollider box)
        {
            Vector3 half = box.size * .5f;
            var points = new List<Vector3>();
            for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
                points.Add(box.transform.TransformPoint(box.center + new Vector3(half.x * x, 0f, half.z * z)));
            return BoundsOf(points, 0f);
        }
    }
}
