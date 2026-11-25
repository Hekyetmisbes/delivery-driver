using System;
using System.Collections.Generic;
using System.Linq;
using DeliveryDriver.City;
using TrafficSystem.Spline;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryDriver.EditorTools
{
    internal sealed class ProceduralWorldPlanner
    {
        internal sealed class Settings
        {
            public int seed = 4281, maxNodesPerCity = 170;
            public float spacing = 95f, width = 8.5f, sidewalk = 2.2f, grade = 0.10f;
            public float maxCutFill = 6f, blend = 10f, seaY, bridgeClearance = 8f;
            public float curve = 0.35f, loops = 0.75f;
            public float arterialSpacing = 600f, collectorSpacing = 250f, straightPreference = .70f, bridgeSeparation = .65f;
            public int nodesPerTile = 180, authoredLinkTarget = 4;
            public bool terrainRepair = true;
            public bool terrainDesign = true, preserveV4Coverage = true;
            public float terrainInfluence = .65f, districtVariation = .65f, earthworkPreference = .8f;
            public bool ankaraBackbone = true, districtGrading = true;
            public float districtGradingLimit = 30f;
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
            public int SeaTerrainCount => tiles.Count(t => t.row == 3);
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

        internal enum RoadKind { Arterial, Collector, Local, Bridge, AuthoredLink }

        internal sealed class Edge
        {
            public Node a, b;
            public List<Vector3> points;
            public string sector;
            public RoadKind kind = RoadKind.Local;
            public bool Bridge => sector == "Highways";
        }

        internal sealed class Plan
        {
            public Layout layout;
            public readonly List<Node> nodes = new List<Node>();
            public readonly List<Edge> edges = new List<Edge>();
            public readonly List<string> warnings = new List<string>();
            public readonly List<string> notes = new List<string>();
            public readonly List<Edge> authoredLinks = new List<Edge>();
            public readonly List<Vector3> gradingMarkers = new List<Vector3>();
            public readonly List<TerrainAnalysisCell> terrainCells = new List<TerrainAnalysisCell>();
            public readonly List<Edge> detachedEdges = new List<Edge>(); // Feasible individual roads, not connected to the retained network.
            public readonly List<Rect> protectedAreas = new List<Rect>();
            public Func<Vector2, float, bool> manualRoadNear;
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
            if (terrains.Count != 24 && terrains.Count != 28)
                throw new InvalidOperationException("Expected 24 land Terrain objects, with either an empty sea corridor or 4 additional sea Terrain objects (28 total); found " + terrains.Count + ".");
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
            // The sea may be empty space rather than four Unity Terrain components.
            // Keep logical row 3 reserved for water in both cases.
            bool hasSeaTiles = terrains.Count == 28;
            int physicalRows = hasSeaTiles ? 7 : 6;
            if (!((xs.Count == physicalRows && zs.Count == 4) ||
                  (xs.Count == 4 && zs.Count == physicalRows)))
                throw new InvalidOperationException("Expected two 4 x 3 land blocks " +
                    (hasSeaTiles ? "with a four-tile sea row" : "separated by an empty sea gap") +
                    "; detected " + xs.Count + " x " + zs.Count + " distinct terrain coordinates.");
            layout.rowsOnX = xs.Count == physicalRows;
            List<float> along = layout.rowsOnX ? xs : zs;
            List<float> across = layout.rowsOnX ? zs : xs;
            float alongSize = layout.rowsOnX ? size.x : size.z;
            float acrossSize = layout.rowsOnX ? size.z : size.x;
            for (int i = 1; i < across.Count; i++)
                if (Mathf.Abs(across[i] - across[i - 1] - acrossSize) > epsilon)
                    throw new InvalidOperationException("Terrain columns have a gap or overlap.");
            for (int i = 1; i < along.Count; i++)
            {
                float gap = along[i] - along[i - 1] - alongSize;
                if (!hasSeaTiles && i == 3)
                {
                    if (gap <= epsilon)
                        throw new InvalidOperationException("24 land tiles detected, but no positive sea gap separates the two 4 x 3 city blocks.");
                }
                else if (Mathf.Abs(gap) > epsilon)
                    throw new InvalidOperationException("Unexpected terrain seam gap or overlap at city row " + i + ".");
            }
            var occupied = new HashSet<Vector2Int>();
            foreach (Tile tile in layout.tiles)
            {
                int x = xs.FindIndex(v => Mathf.Abs(v - tile.area.xMin) <= epsilon);
                int z = zs.FindIndex(v => Mathf.Abs(v - tile.area.yMin) <= epsilon);
                int physicalRow = layout.rowsOnX ? x : z;
                tile.row = !hasSeaTiles && physicalRow >= 3 ? physicalRow + 1 : physicalRow;
                tile.column = layout.rowsOnX ? z : x;
                if (!occupied.Add(new Vector2Int(tile.row, tile.column)))
                    throw new InvalidOperationException("Overlapping terrain tile coordinates.");
            }
            layout.tiles.Sort((a, b) => a.row == b.row ? a.column.CompareTo(b.column) : a.row.CompareTo(b.row));
            layout.low = Union(layout.tiles.Where(t => t.row < 3).Select(t => t.area));
            layout.high = Union(layout.tiles.Where(t => t.row > 3).Select(t => t.area));
            // Infer the real corridor width from the facing city shores.
            // Never fabricate a TerrainData asset for an empty sea row.
            layout.sea = layout.rowsOnX
                ? Rect.MinMaxRect(layout.low.xMax, layout.low.yMin, layout.high.xMin, layout.low.yMax)
                : Rect.MinMaxRect(layout.low.xMin, layout.low.yMax, layout.low.xMax, layout.high.yMin);
            return layout;
        }

        internal sealed class TerrainAnalysisCell
        {
            public Rect area;
            public Vector3 center;
            public float slope, roughness;
            public Vector2 uphill;
            public bool protectedLand;
        }

        private readonly Dictionary<Tile, TerrainAnalysisCell[,]> terrainAnalysis =
            new Dictionary<Tile, TerrainAnalysisCell[,]>();

        // Preview-only sample raster; the original heightmap is still authoritative for
        // the final road grade and cross-section constraints.
        internal static Plan AnalyzeOnly(Settings settings, Layout layout, Scene scene)
        {
            var planner = new ProceduralWorldPlanner(settings, layout, scene);
            planner.AnalyzeTerrain();
            return planner.plan;
        }

        private void AnalyzeTerrain()
        {
            foreach (Tile tile in plan.layout.tiles)
            {
                if (tile.row == 3) continue;
                int nx = Mathf.Clamp(Mathf.CeilToInt(tile.area.width / 85f), 3, 32);
                int nz = Mathf.Clamp(Mathf.CeilToInt(tile.area.height / 85f), 3, 32);
                float dx = tile.area.width / nx, dz = tile.area.height / nz;
                var cells = new TerrainAnalysisCell[nx,nz];
                for (int x=0;x<nx;x++)
                for (int z=0;z<nz;z++)
                {
                    Rect r = new Rect(tile.area.xMin+x*dx,tile.area.yMin+z*dz,dx,dz);
                    Vector2 c = r.center;
                    float h = tile.Height(c);
                    float radius = Mathf.Min(20f, Mathf.Min(dx,dz)*.35f);
                    float left=tile.Height(c-Vector2.right*radius), right=tile.Height(c+Vector2.right*radius);
                    float down=tile.Height(c-Vector2.up*radius), up=tile.Height(c+Vector2.up*radius);
                    Vector2 gradient = new Vector2(right-left,up-down)/(2f*radius);
                    var cell = new TerrainAnalysisCell { area=r, center=XYZ(c,h), slope=gradient.magnitude,
                        roughness=Mathf.Max(Mathf.Abs((left+right)*.5f-h),Mathf.Abs((up+down)*.5f-h)),
                        uphill=gradient.normalized,
                        protectedLand=IsProtected(c,RoadClearance)||obstacles.Near(c,RoadClearance*2f) };
                    cells[x,z]=cell; plan.terrainCells.Add(cell);
                }
                terrainAnalysis.Add(tile,cells);
            }
            foreach (var sector in new[] { "Ankara", "İstanbul" })
            {
                Rect bounds=sector=="Ankara"?plan.layout.Ankara(settings.ankaraLow):plan.layout.Istanbul(settings.ankaraLow);
                var cells=plan.terrainCells.Where(c=>bounds.Contains(XZ(c.center))).ToList();
                if(cells.Count==0)continue;
                plan.notes.Add(sector+" terrain analysis: "+cells.Count+" samples, elevation "+
                    cells.Min(c=>c.center.y).ToString("F1")+"–"+cells.Max(c=>c.center.y).ToString("F1")+
                    " m; "+cells.Count(c=>c.slope>settings.grade)+" cells above selected road grade (raw ground, not final roads).");
            }
        }

        private TerrainAnalysisCell AnalysisAt(Vector2 p)
        {
            foreach(var pair in terrainAnalysis)
            {
                if(!Contains(pair.Key.area,p,.001f))continue;
                var a=pair.Value;
                int x=Mathf.Clamp(Mathf.FloorToInt((p.x-pair.Key.area.xMin)/pair.Key.area.width*a.GetLength(0)),0,a.GetLength(0)-1);
                int z=Mathf.Clamp(Mathf.FloorToInt((p.y-pair.Key.area.yMin)/pair.Key.area.height*a.GetLength(1)),0,a.GetLength(1)-1);
                return a[x,z];
            }
            return null;
        }

        // Coherent district rotation field, smoothly fading near its boundary.
        // Small displacement preserves neighbor relationships of the V4 scaffold.
        private Vector2 ShapeStreetNode(Vector2 pos, Rect bounds, float local)
        {
            if(IsProtected(pos,RoadClearance*2f)||obstacles.Near(pos,RoadClearance*2f))return pos;
            District district=districts.FirstOrDefault(d=>d.area.Contains(pos));
            Rect area=district!=null?district.area:bounds;
            string name=district!=null?district.name:"";
            int hash=17;
            unchecked { foreach(char ch in name)hash=hash*31+ch; hash=hash*31+settings.seed; }
            float angle=((uint)hash%1001/1000f-.5f)*.75f;
            var style=StreetStyle(pos);
            angle*=settings.districtVariation*style.jitter;
            TerrainAnalysisCell cell=AnalysisAt(pos);
            if(cell!=null&&cell.slope>.015f)
            {
                Vector2 contour=new Vector2(-cell.uphill.y,cell.uphill.x);
                float preferred=Mathf.Atan2(contour.y,contour.x);
                preferred=Mathf.Repeat(preferred+Mathf.PI*.25f,Mathf.PI*.5f)-Mathf.PI*.25f;
                float weight=Mathf.Clamp01(cell.slope/Mathf.Max(.01f,settings.grade))*settings.terrainInfluence;
                angle=Mathf.Lerp(angle,preferred,weight);
            }
            Vector2 v=pos-area.center;
            Vector2 rotated=new Vector2(v.x*Mathf.Cos(angle)-v.y*Mathf.Sin(angle),v.x*Mathf.Sin(angle)+v.y*Mathf.Cos(angle));
            float boundary=Mathf.Min(Mathf.Min(pos.x-area.xMin,area.xMax-pos.x),Mathf.Min(pos.y-area.yMin,area.yMax-pos.y));
            float fade=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(boundary/(local*1.4f)));
            Vector2 shift=Vector2.ClampMagnitude(rotated-v,local*.24f)*fade;
            return pos+shift;
        }

        private float TerrainCandidateCost(StreetCandidate c)
        {
            Vector2 a=XZ(c.a.position),b=XZ(c.b.position);
            Vector2 direction=(b-a).normalized;
            float penalty=0f;
            for(int i=1;i<=3;i++)
            {
                TerrainAnalysisCell cell=AnalysisAt(Vector2.Lerp(a,b,i*.25f));
                if(cell==null)continue;
                float along=cell.slope*Mathf.Abs(Vector2.Dot(direction,cell.uphill));
                penalty+=Mathf.Max(0f,along-settings.grade*.5f)*6f+cell.roughness*.07f;
            }
            return c.length*(1f+settings.terrainInfluence*penalty/3f);
        }

        // A sampled cost proxy, not a cubic-meter earthworks estimate.
        private float ProfileCost(List<Vector3> path)
        {
            float sum=0f, max=0f;
            for(int i=0;i<path.Count;i+=2)
            {
                Vector3 p=path[i];
                Vector2 center=XZ(p);
                Vector2 tangent=XZ(path[Mathf.Min(i+1,path.Count-1)]-path[Mathf.Max(0,i-1)]);
                Vector2 side=new Vector2(-tangent.y,tangent.x).normalized*(settings.width*.5f+settings.sidewalk);
                for(int j=-1;j<=1;j++)
                {
                    if(!plan.layout.TryHeight(center+j*side,out float h))continue;
                    float d=Mathf.Abs(p.y-.12f-h);
                    sum+=d;max=Mathf.Max(max,d);
                }
            }
            return settings.earthworkPreference*(sum/Mathf.Max(1,((path.Count+1)/2)*3)+max*.25f);
        }

        private static string EarthworkSummary(Plan p,string sector,Settings settings)
        {
            double cut=0,fill=0,total=0,extra=0;
            float peak=0;
            foreach(Edge edge in p.edges.Where(e=>e.sector==sector))
            for(int i=1;i<edge.points.Count;i++)
            {
                Vector3 a=edge.points[i-1],b=edge.points[i];
                float length=Vector2.Distance(XZ(a),XZ(b));
                Vector3 mid=(a+b)*.5f;
                if(length<.001f)continue;
                Vector2 tangent=(XZ(b)-XZ(a)).normalized;
                Vector2 side=new Vector2(-tangent.y,tangent.x)*(settings.width*.5f+settings.sidewalk);
                float worst=0;
                for(int j=-1;j<=1;j++)
                {
                    if(!p.layout.TryHeight(XZ(mid)+side*j,out float ground))continue;
                    float d=mid.y-.12f-ground;
                    cut+=Mathf.Max(0,-d)*length/3.0;
                    fill+=Mathf.Max(0,d)*length/3.0;
                    worst=Mathf.Max(worst,Mathf.Abs(d));
                }
                peak=Mathf.Max(peak,worst);total+=length;
                if(worst>settings.maxCutFill+.01f)extra+=length;
            }
            return total<.01?"no roads": "mean cut "+(cut/total).ToString("F2")+" m, mean fill "+(fill/total).ToString("F2")+
                " m, peak "+peak.ToString("F1")+" m, extra grading "+(extra/total*100).ToString("F1")+"% of sampled road length";
        }

        private static int Coverage(Plan p,string sector)
        {
            var edges=p.edges.Where(e=>e.sector==sector).ToList();
            return p.layout.tiles.Count(t=>t.row!=3&&edges.Any(e=>e.points.Any(v=>t.area.Contains(XZ(v)))));
        }

        private static bool GatesConnected(Plan p)
        {
            if(p.BridgeCount==0)return true;
            var links=new Dictionary<Node,List<Node>>();
            foreach(Edge e in p.edges)
            {
                if(!links.TryGetValue(e.a,out List<Node> a))links[e.a]=a=new List<Node>();
                if(!links.TryGetValue(e.b,out List<Node> b))links[e.b]=b=new List<Node>();
                a.Add(e.b);b.Add(e.a);
            }
            Node start=p.edges.First(e=>e.Bridge).a;
            var visited=new HashSet<Node>{start};
            var queue=new Queue<Node>();queue.Enqueue(start);
            while(queue.Count>0)
                foreach(Node n in links[queue.Dequeue()])if(visited.Add(n))queue.Enqueue(n);
            return p.edges.All(e=>visited.Contains(e.a)&&visited.Contains(e.b));
        }

        internal static Plan Build(Settings settings, Layout layout, Scene scene)
        {
            if(!settings.terrainDesign || !settings.preserveV4Coverage)return BuildOnce(settings,layout,scene);
            // Compare against the same-seed V4 algorithm, without modifying the scene.
            // This is a coverage guard, not a claim of equal visual quality.
            Plan adaptive=null,baseline=null;
            Exception adaptiveError=null;
            try { adaptive=BuildOnce(settings,layout,scene); }
            catch(InvalidOperationException ex){adaptiveError=ex;}
            try
            {
                settings.terrainDesign=false;
                baseline=BuildOnce(settings,layout,scene);
            }
            catch(InvalidOperationException)
            {
                if(adaptive==null)throw;
            }
            finally { settings.terrainDesign=true; }
            if(adaptive==null&&baseline==null)throw adaptiveError??new InvalidOperationException("No preview.");
            string comparison = adaptive!=null && baseline!=null ? "Ankara grading (sampled, not volumes): V4 " + EarthworkSummary(baseline,"Ankara",settings) + "; V5 " + EarthworkSummary(adaptive,"Ankara",settings) + ". Authored proposals V4/V5: " + baseline.authoredLinks.Count + "/" + adaptive.authoredLinks.Count + "." : null;
            if(baseline!=null && (adaptive==null ||
                adaptive.BridgeCount<baseline.BridgeCount ||
                (GatesConnected(baseline)&&!GatesConnected(adaptive)) ||
                new[]{"Ankara","İstanbul"}.Any(s=>Coverage(adaptive,s)<Coverage(baseline,s)) ||
                adaptive.detachedEdges.Count>baseline.detachedEdges.Count+Mathf.Max(10,baseline.edges.Count/20) ||
                new[]{"Ankara","İstanbul"}.Any(s=>adaptive.edges.Count(e=>e.sector==s)<baseline.edges.Count(e=>e.sector==s)*.75f)))
            {
                if(adaptive!=null)baseline.terrainCells.AddRange(adaptive.terrainCells);
                else baseline.terrainCells.AddRange(AnalyzeOnly(settings,layout,scene).terrainCells);
                baseline.warnings.Insert(0,"V5 coverage guard selected the V4 layout: adaptive candidate lost coverage or connectivity. Disable guard to inspect the V5 candidate."+(adaptiveError!=null?" "+adaptiveError.Message:""));
                if(comparison!=null)baseline.notes.Insert(0,comparison);
                return baseline;
            }
            if(comparison!=null)adaptive.notes.Insert(0,comparison);
            adaptive.notes.Insert(0,baseline!=null?"V5 coverage guard: adaptive layout retained (same-seed V4 coverage/connectivity comparison).":"V5 adaptive layout retained; V4 comparison unavailable.");
            return adaptive;
        }

        private static Plan BuildOnce(Settings settings, Layout layout, Scene scene)
        {
            var planner = new ProceduralWorldPlanner(settings, layout, scene);
            planner.plan.protectedAreas.AddRange(planner.protectedAreas);
            planner.plan.manualRoadNear = planner.obstacles.Near;
            if (settings.terrainDesign) planner.AnalyzeTerrain();
            if (settings.bridges && settings.buildAnkara && settings.buildIstanbul)
                planner.PlanBridges();
            if (settings.buildAnkara) planner.PlanHierarchy("Ankara", layout.Ankara(settings.ankaraLow));
            planner.gradingSurface = null;
            if (settings.buildIstanbul) planner.PlanHierarchy("İstanbul", layout.Istanbul(settings.ankaraLow));
            if (planner.plan.edges.Count == 0) throw new InvalidOperationException("No valid roads. Increase spacing or cut/fill tolerance, or check the terrain.");
            return planner.plan;
        }

        internal sealed class BridgePlanningException : InvalidOperationException
        {
            public BridgePlanningException(string message) : base(message) { }
        }

        private enum BridgeRejection
        {
            ProtectedDistrict, ExistingRoad, MissingLand, RoadWidth, GradeOrHeight
        }

        private sealed class BridgeCandidate
        {
            public List<Vector3> path;
            public float acrossMin, acrossMax;
        }

        private void PlanBridges()
        {
            Layout l = plan.layout;
            float min = l.AcrossMin(l.sea), span = l.AcrossMax(l.sea) - min;
            float lowDepth = l.AlongMax(l.low) - l.AlongMin(l.low);
            float highDepth = l.AlongMax(l.high) - l.AlongMin(l.high);
            float margin = RoadClearance + 4f;
            float separation = Mathf.Max(35f, RoadClearance * 4f);
            var failures = new int[5];
            var accepted = new List<BridgeCandidate>();
            BridgeCandidate bestA = null, bestB = null;
            float bestGap = -1f, desiredGap = span * settings.bridgeSeparation;
            int attempts = 0;
            // Prefer well-separated crossings, but permit both in the same free coastal district.
            // Search both sides independently; the gap's actual measured width is used.
            int laneCount = Mathf.Clamp(Mathf.CeilToInt(span / Mathf.Max(20f, separation * .5f)), 12, 48);
            if (span <= margin * 2f)
                throw new BridgePlanningException("The sea corridor is too narrow across its shoreline for the selected road width.");
            var lanes = Enumerable.Range(0, laneCount + 1)
                .Select(i => Mathf.Lerp(min + margin, min + span - margin, i / (float)laneCount))
                .OrderBy(x => Mathf.Min(Mathf.Abs((x - min) / span - .18f), Mathf.Abs((x - min) / span - .82f)))
                .ToList();
            float[] depths = { .08f, .16f, .26f, .38f, .52f, .68f, .82f };
            var depthPairs = (from low in depths from high in depths
                              orderby low + high, Mathf.Max(low, high)
                              select (low, high)).ToList();
            // First test perpendicular crossings, then diagonals to reach a free corridor
            // when one city's coastline is blocked by existing content.
            foreach (float diagonal in new[] { 0f, .07f, -.07f, .15f, -.15f, .25f, -.25f })
            {
                foreach (float across in lanes)
                {
                    float otherAcross = across + span * diagonal;
                    if (otherAcross < min + margin || otherAcross > min + span - margin) continue;
                    float candidateMin = Mathf.Min(across, otherAcross), candidateMax = Mathf.Max(across, otherAcross);
                    if (accepted.Any(c => Mathf.Abs(c.acrossMin - candidateMin) < 2f &&
                                          Mathf.Abs(c.acrossMax - candidateMax) < 2f)) continue;
                    foreach (var pair in depthPairs)
                    {
                        if (++attempts % 128 == 0 &&
                            EditorUtility.DisplayCancelableProgressBar("Procedural World",
                                "Searching bridge approaches (" + attempts + " candidates)", .06f))
                            throw new OperationCanceledException();
                        Vector2 a = l.Point(l.AlongMax(l.low) - Mathf.Clamp(lowDepth * pair.low, margin, lowDepth - margin), across);
                        Vector2 b = l.Point(l.AlongMin(l.high) + Mathf.Clamp(highDepth * pair.high, margin, highDepth - margin), otherAcross);
                        if (!TryBridge(a, b, out List<Vector3> path, out BridgeRejection rejection))
                        {
                            failures[(int)rejection]++;
                            continue;
                        }
                        var candidate = new BridgeCandidate { path = path, acrossMin = candidateMin, acrossMax = candidateMax };
                        // Disjoint lateral envelopes guarantee that the two straight crossings
                        // and their approach ramps cannot cross each other.
                        foreach (BridgeCandidate partner in accepted)
                        {
                            float gap = Mathf.Max(candidate.acrossMin - partner.acrossMax,
                                partner.acrossMin - candidate.acrossMax);
                            if (gap < separation || gap <= bestGap) continue;
                            bestGap = gap; bestA = partner; bestB = candidate;
                        }
                        accepted.Add(candidate);
                        break;
                    }
                }
                if (bestGap >= desiredGap) break;
            }
            if (bestA != null)
            {
                AddBridge(bestA.path); AddBridge(bestB.path);
                string report = "Bridge separation: " + bestGap.ToString("F0") + " m (" +
                    (bestGap / span * 100f).ToString("F0") + "% of shoreline), " + attempts + " candidates.";
                if (bestGap < desiredGap) plan.warnings.Add(report + " Target separation unavailable.");
                else plan.notes.Add(report);
                return;
            }
            throw new BridgePlanningException("Could not plan two separated bridges after " + attempts +
                " candidates (usable corridors: " + accepted.Count + "). " +
                "Rejected: protected district=" + failures[(int)BridgeRejection.ProtectedDistrict] +
                ", existing road=" + failures[(int)BridgeRejection.ExistingRoad] +
                ", missing land=" + failures[(int)BridgeRejection.MissingLand] +
                ", width/bounds=" + failures[(int)BridgeRejection.RoadWidth] +
                ", grade/height=" + failures[(int)BridgeRejection.GradeOrHeight] +
                ". Sea Y=" + settings.seaY.ToString("F1") +
                ", clearance=" + settings.bridgeClearance.ToString("F1") +
                " m, max grade=" + (settings.grade * 100f).ToString("F1") +
                "%. Check Sea surface Y and the dominant rejection reason. Protected content and slope limits were not bypassed.");
        }

        private void AddBridge(List<Vector3> path)
        {
            Node first = AddNode(path[0], settings.ankaraLow ? "Ankara" : "İstanbul", true);
            Node last = AddNode(path[path.Count - 1], settings.ankaraLow ? "İstanbul" : "Ankara", true);
            AddEdge(first, last, path, "Highways");
        }

        private bool TryBridge(Vector2 a, Vector2 b, out List<Vector3> points, out BridgeRejection rejection)
        {
            points = null;
            rejection = BridgeRejection.MissingLand;
            Layout l = plan.layout;
            if (!l.TryHeight(a, out float yA) || !l.TryHeight(b, out float yB)) return false;
            Vector2 delta = b - a;
            float length = delta.magnitude;
            if (length < 1f) return false;
            Vector2 right = new Vector2(-delta.y, delta.x).normalized;
            float halfWidth = settings.width * .5f + settings.sidewalk;
            int steps = Mathf.Max(2, Mathf.CeilToInt(length / 2.5f));
            float step = length / steps;
            var lower = new float[steps + 1];
            var upper = new float[steps + 1];
            var preferred = new float[steps + 1];
            var positions = new Vector2[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
                positions[i] = p;
                if (IsProtected(p, RoadClearance))
                {
                    rejection = BridgeRejection.ProtectedDistrict;
                    return false;
                }
                if (obstacles.Near(p, RoadClearance))
                {
                    rejection = BridgeRejection.ExistingRoad;
                    return false;
                }
                bool sea = Contains(l.sea, p);
                bool hasGround = l.TryHeight(p, out float center);
                if (!sea && !hasGround) return false;
                float required = sea ? settings.seaY + settings.bridgeClearance : float.NegativeInfinity;
                // A bridge may climb over its land approaches on supports, rather than
                // requiring a constant high deck and two symmetric smoothstep ramps.
                for (int side = -1; side <= 1; side++)
                {
                    Vector2 q = p + right * halfWidth * side;
                    bool overSea = Contains(l.sea, q);
                    bool overLand = Contains(l.low, q) || Contains(l.high, q);
                    if (!overSea && !overLand)
                    {
                        rejection = BridgeRejection.RoadWidth;
                        return false;
                    }
                    if (overSea) required = Mathf.Max(required, settings.seaY + settings.bridgeClearance);
                    if (l.TryHeight(q, out float ground))
                    {
                        // Shore boundary samples can return land; the required water height
                        // is independent of terrain and physical sea tiles remain untouched.
                        float clearance = overSea && !overLand ? 3f : .20f - settings.maxCutFill;
                        required = Mathf.Max(required, ground + clearance);
                    }
                    else if (!overSea)
                    {
                        rejection = BridgeRejection.MissingLand;
                        return false;
                    }
                }
                lower[i] = required;
                upper[i] = float.PositiveInfinity;
                preferred[i] = sea ? settings.seaY + settings.bridgeClearance : center + .12f;
            }

            // Solve a grade-bounded profile over the entire crossing. A deck can slope
            // across the sea, while both land endpoints stay at their own terrain heights.
            rejection = BridgeRejection.GradeOrHeight;
            yA += .12f;
            yB += .12f;
            if (yA + .001f < lower[0] || yB + .001f < lower[steps]) return false;
            lower[0] = upper[0] = yA;
            lower[steps] = upper[steps] = yB;
            float rise = step * settings.grade;
            for (int i = 1; i <= steps; i++)
            {
                lower[i] = Mathf.Max(lower[i], lower[i - 1] - rise);
                upper[i] = Mathf.Min(upper[i], upper[i - 1] + rise);
                if (lower[i] > upper[i] + .0001f) return false;
            }
            for (int i = steps - 1; i >= 0; i--)
            {
                lower[i] = Mathf.Max(lower[i], lower[i + 1] - rise);
                upper[i] = Mathf.Min(upper[i], upper[i + 1] + rise);
                if (lower[i] > upper[i] + .0001f) return false;
            }

            // Smooth the desired elevation before constraining it to the feasible envelope.
            for (int pass = 0; pass < 8; pass++)
            {
                float[] old = (float[])preferred.Clone();
                for (int i = 1; i < steps; i++)
                    preferred[i] = (old[i - 1] + 2f * old[i] + old[i + 1]) * .25f;
            }
            var result = new List<Vector3>(steps + 1) { XYZ(a, yA) };
            for (int i = 1; i < steps; i++)
            {
                float last = result[i - 1].y;
                float lo = Mathf.Max(lower[i], last - rise);
                float hi = Mathf.Min(upper[i], last + rise);
                if (lo > hi + .0001f) return false;
                float y = Mathf.Clamp(preferred[i], lo, Mathf.Max(lo, hi));
                result.Add(XYZ(positions[i], y));
            }
            result.Add(XYZ(b, yB));
            for (int i = 1; i < result.Count; i++)
                if (!GradeOk(result[i - 1], result[i])) return false;
            points = result;
            return true;
        }

        private sealed class AxisLine
        {
            public float position;
            public RoadKind kind;
        }
        private sealed class StreetCandidate
        {
            public Node a, b;
            public RoadKind kind;
            public float length;
        }

        // Coherent nested streets, instead of connecting arbitrary scattered points.
        private List<AxisLine> MakeStreetAxis(float min, float max, float local)
        {
            float size = max - min;
            int main = Mathf.Max(1, Mathf.RoundToInt(size / Mathf.Max(settings.arterialSpacing, settings.collectorSpacing * 1.5f)));
            var primary = new List<float> { min };
            for (int i = 1; i < main; i++) primary.Add(min + size * (i + (Next() - .5f) * .22f) / main);
            primary.Add(max);
            var result = new List<AxisLine>();
            for (int i = 0; i < primary.Count - 1; i++)
            {
                float a = primary[i], b = primary[i + 1];
                result.Add(new AxisLine { position = a, kind = RoadKind.Arterial });
                int count = Mathf.Max(1, Mathf.RoundToInt((b - a) / Mathf.Max(local * 1.7f, settings.collectorSpacing)));
                var secondary = new List<float> { a };
                for (int j = 1; j < count; j++) secondary.Add(Mathf.Lerp(a, b, (j + (Next() - .5f) * .25f) / count));
                secondary.Add(b);
                for (int j = 0; j < secondary.Count - 1; j++)
                {
                    float start = secondary[j], end = secondary[j + 1];
                    if (j > 0) result.Add(new AxisLine { position = start, kind = RoadKind.Collector });
                    int small = Mathf.Max(1, Mathf.RoundToInt((end - start) / local));
                    for (int k = 1; k < small; k++)
                        result.Add(new AxisLine { position = Mathf.Lerp(start, end, (k + (Next() - .5f) * .24f) / small), kind = RoadKind.Local });
                }
            }
            result.Add(new AxisLine { position = max, kind = RoadKind.Arterial });
            return result;
        }

        private (float density, float bend, float jitter) StreetStyle(Vector2 p)
        {
            foreach (District district in districts)
            {
                if (!district.area.Contains(p)) continue;
                string n = district.name.ToLowerInvariant();
                if (n.Contains("ostim") || n.Contains("liman") || n.Contains("sanayi")) return (.58f, .12f, .20f);
                if (n.Contains("hamam") || n.Contains("galata") || n.Contains("taksim")) return (1f, .85f, .85f);
                if (n.Contains("atakule")) return (.55f, .75f, .75f);
                if (n.Contains("yama") || n.Contains("ulus")) return (.78f, .90f, .75f);
                if (n.Contains("plato") || n.Contains("sıhhiye") || n.Contains("sihhiye")) return (.92f, .20f, .30f);
            }
            return (.85f, .45f, .55f);
        }

        private sealed class StreetIndex
        {
            private const float Cell = 160f;
            private readonly Dictionary<Vector2Int, List<Edge>> cells = new Dictionary<Vector2Int, List<Edge>>();
            private readonly Dictionary<Node, List<Edge>> incident = new Dictionary<Node, List<Edge>>();
            private static int I(float v) => Mathf.FloorToInt(v / Cell);
            public void Add(Edge e)
            {
                Rect r = BoundsOf(e.points, 0f);
                for (int x = I(r.xMin); x <= I(r.xMax); x++)
                for (int y = I(r.yMin); y <= I(r.yMax); y++)
                {
                    var key = new Vector2Int(x, y);
                    if (!cells.TryGetValue(key, out List<Edge> list)) cells[key] = list = new List<Edge>();
                    list.Add(e);
                }
                foreach (Node n in new[] { e.a, e.b })
                {
                    if (!incident.TryGetValue(n, out List<Edge> list)) incident[n] = list = new List<Edge>();
                    list.Add(e);
                }
            }
            public IEnumerable<Edge> At(Node n) =>
                incident.TryGetValue(n, out List<Edge> list) ? (IEnumerable<Edge>)list : Enumerable.Empty<Edge>();
            public HashSet<Edge> Query(Rect r)
            {
                var result = new HashSet<Edge>();
                for (int x = I(r.xMin); x <= I(r.xMax); x++)
                for (int y = I(r.yMin); y <= I(r.yMax); y++)
                    if (cells.TryGetValue(new Vector2Int(x, y), out List<Edge> list))
                        foreach (Edge e in list) result.Add(e);
                return result;
            }
        }

        private sealed class StreetDiagnostics
        {
            public int blockedNodes, protectedNodes, missingNodes, elevationRejected, geometryRejected, routed, directRepair, hillsideInfill;
        }
        private StreetDiagnostics diagnostics;

        private void PlanHierarchy(string sector, Rect bounds)
        {
            diagnostics = new StreetDiagnostics();
            bool backbone = sector == "Ankara" && settings.ankaraBackbone;
            gradingSurface = backbone && settings.districtGrading ? new GradingSurface(plan.layout, bounds, settings.grade) : null;
            bool adaptive = settings.terrainDesign;
            var gates = plan.nodes.Where(n => n.sector == sector && n.gate).ToList();
            var nodes = new List<Node>(gates);
            var index = new StreetIndex();
            foreach (Edge edge in plan.edges) index.Add(edge);
            float local = Mathf.Max(52f, settings.spacing);
            float margin = RoadClearance + 14f;
            if (bounds.width < margin * 2f + local || bounds.height < margin * 2f + local)
                throw new InvalidOperationException(sector + ": terrain too small for street spacing.");
            int budget = settings.nodesPerTile * 12;
            var xs = MakeStreetAxis(bounds.xMin + margin, bounds.xMax - margin, local);
            var zs = MakeStreetAxis(bounds.yMin + margin, bounds.yMax - margin, local);
            // No silent 170-node cap. Report any adjustment needed by the explicit tile-scaled budget.
            while ((long)xs.Count * zs.Count > budget)
            {
                local *= 1.12f;
                xs = MakeStreetAxis(bounds.xMin + margin, bounds.xMax - margin, local);
                zs = MakeStreetAxis(bounds.yMin + margin, bounds.yMax - margin, local);
            }
            if (local > settings.spacing + 1f)
                plan.warnings.Add(sector + ": local spacing adjusted to " + local.ToString("F0") + " m to fit " + budget + " candidate nodes.");
            var grid = new Node[xs.Count, zs.Count];
            float phase = Next() * Mathf.PI * 2f, phase2 = Next() * Mathf.PI * 2f;
            for (int x = 0; x < xs.Count; x++)
            for (int z = 0; z < zs.Count; z++)
            {
                Vector2 pos = new Vector2(xs[x].position, zs[z].position);
                var style = StreetStyle(pos);
                float warp = Mathf.Min(18f, local * .25f * settings.curve);
                pos.x += Mathf.Sin((pos.y - bounds.yMin) / 340f + phase) * warp;
                pos.y += Mathf.Sin((pos.x - bounds.xMin) / 420f + phase2) * warp;
                if (xs[x].kind == RoadKind.Local && zs[z].kind == RoadKind.Local)
                    pos += new Vector2(Next() - .5f, Next() - .5f) * local * .24f * style.jitter;
                if (adaptive) pos = ShapeStreetNode(pos, bounds, local);
                if (IsProtected(pos, RoadClearance)) { diagnostics.protectedNodes++; continue; }
                if (!Contains(bounds, pos, -RoadClearance) || obstacles.Near(pos, RoadClearance * 2f) ||
                    gates.Any(g => Vector2.Distance(pos, XZ(g.position)) < RoadClearance * 3f))
                { diagnostics.blockedNodes++; continue; }
                if (!plan.layout.TryHeight(pos, out _)) { diagnostics.missingNodes++; continue; }
                Node node = AddLandNode(pos, sector);
                grid[x, z] = node; nodes.Add(node);
            }
            var candidates = new List<StreetCandidate>();
            var unique = new HashSet<(int, int)>();
            void AddCandidate(Node a, Node b, RoadKind kind)
            {
                if (a == null || b == null || a == b) return;
                float len = Vector2.Distance(XZ(a.position), XZ(b.position));
                if (len < RoadClearance * 2.5f || len > local * 3.5f) return;
                if (!unique.Add((Mathf.Min(a.id,b.id),Mathf.Max(a.id,b.id)))) return;
                candidates.Add(new StreetCandidate { a = a, b = b, length = len, kind = kind });
            }
            for (int x = 0; x < xs.Count; x++)
            for (int z = 0; z < zs.Count; z++)
            {
                Node n = grid[x,z];
                if (x+1 < xs.Count) AddCandidate(n,grid[x+1,z],zs[z].kind);
                if (z+1 < zs.Count) AddCandidate(n,grid[x,z+1],xs[x].kind);
            }
            var groups = nodes.ToDictionary(n => n, n => n);
            Node Find(Node n)
            {
                Node root = n;
                while (groups[root] != root) root = groups[root];
                while (groups[n] != n) { Node next = groups[n]; groups[n] = root; n = next; }
                return root;
            }
            if (adaptive && sector == "Ankara")
                ProposeLinks(sector, bounds, nodes.Where(n => !n.gate).ToList(), index);
            var built = new HashSet<(int,int)>();
            int attempts = 0;
            bool TryBuild(StreetCandidate c, bool route = false)
            {
                var key = (Mathf.Min(c.a.id,c.b.id),Mathf.Max(c.a.id,c.b.id));
                int Limit(Node n) => adaptive && plan.authoredLinks.Any(e => e.b == n) ? Mathf.Min(3, DegreeLimit(n)) : DegreeLimit(n);
                if (built.Contains(key) || c.a.degree >= Limit(c.a) || c.b.degree >= Limit(c.b)) return false;
                if (++attempts % 80 == 0 && EditorUtility.DisplayCancelableProgressBar("Procedural World",
                    sector + ": hierarchy candidates " + attempts, .30f)) throw new OperationCanceledException();
                List<Vector3> path;
                if (route)
                {
                    if (!TryTerrainConnector(c.a, c.b, bounds, index, out path)) return false;
                }
                else if (!TryStreet(c.a,c.b,bounds,c.kind,index,false,out path)) return false;
                var edge = new Edge { a=c.a,b=c.b,sector=sector,kind=c.kind,points=path };
                plan.edges.Add(edge); index.Add(edge); built.Add(key);
                c.a.degree++; c.b.degree++; groups[Find(c.b)] = Find(c.a);
                return true;
            }

            // Attach bridge gates first to ensure local roads cannot block them.
            foreach (Node gate in gates)
            {
                foreach (Node n in nodes.Where(n=>!n.gate).OrderBy(n=>(XZ(n.position)-XZ(gate.position)).sqrMagnitude).Take(64))
                {
                    float len = Vector2.Distance(XZ(n.position),XZ(gate.position));
                    if (len < RoadClearance*2.5f || len > Mathf.Max(500f,local*5f)) continue;
                    if (TryBuild(new StreetCandidate { a=gate,b=n,kind=RoadKind.Arterial,length=len })) break;
                }
            }
            Node backboneRoot = null;
            if (backbone)
            {
                backboneRoot = gates.FirstOrDefault();
                if (backboneRoot == null)
                {
                    // Prefer a reachable low-altitude coastal seed, not an arbitrary isolated summit.
                    float shore = settings.ankaraLow ? plan.layout.AlongMax(bounds) : plan.layout.AlongMin(bounds);
                    backboneRoot = adaptive ? plan.authoredLinks.Where(e=>e.sector==sector).Select(e=>e.b).FirstOrDefault() : null;
                    if (backboneRoot == null) backboneRoot = nodes.Where(n => !n.gate)
                        .OrderBy(n => Mathf.Abs((plan.layout.rowsOnX ? n.position.x : n.position.z) - shore)
                            + Mathf.Abs((plan.layout.rowsOnX ? n.position.z : n.position.x) -
                            (plan.layout.rowsOnX ? bounds.center.y : bounds.center.x)) * .18f)
                        .ThenBy(n => n.id).FirstOrDefault();
                }
                if (backboneRoot != null)
                {
                    // Reserve long city-to-city-band routes before local streets occupy their space.
                    var spineCandidates = new List<StreetCandidate>(candidates);
                    for (int x=0;x<xs.Count;x++)
                    for (int z=0;z<zs.Count;z++)
                    {
                        Node n=grid[x,z];
                        if(n==null)continue;
                        foreach(Vector2Int o in new[]{new Vector2Int(1,1),new Vector2Int(1,-1),new Vector2Int(2,1),
                            new Vector2Int(2,-1),new Vector2Int(1,2),new Vector2Int(1,-2),new Vector2Int(2,0),new Vector2Int(0,2)})
                        {
                            int xx=x+o.x,zz=z+o.y;
                            if(xx>=xs.Count||zz<0||zz>=zs.Count)continue;
                            Node other=grid[xx,zz];
                            if(other==null)continue;
                            float length=Vector2.Distance(XZ(n.position),XZ(other.position));
                            if(length>=RoadClearance*2.5f && length<=local*3.2f)
                                spineCandidates.Add(new StreetCandidate{a=n,b=other,kind=RoadKind.Arterial,length=length});
                        }
                    }
                    foreach(Node gate in gates)
                    {
                        foreach(Node n in nodes.Where(n=>!n.gate).OrderBy(n=>(XZ(n.position)-XZ(gate.position)).sqrMagnitude).Take(18))
                        {
                            float length=Vector2.Distance(XZ(n.position),XZ(gate.position));
                            if(length>=RoadClearance*2.5f && length<=local*5f)
                                spineCandidates.Add(new StreetCandidate{a=gate,b=n,kind=RoadKind.Arterial,length=length});
                        }
                    }
                    PlanBackbone(bounds, backboneRoot, nodes, gates, spineCandidates, Find, TryBuild);
                    // Grow new streets only from the connected backbone first; subsequent
                    // diagnostic passes may still expose genuinely detached fragments.
                    var frontier = candidates.OrderBy(c=>c.kind).ThenBy(c=>adaptive?TerrainCandidateCost(c):c.length).ToList();
                    var rejected = new HashSet<StreetCandidate>();
                    int attached = 0;
                    for (int pass=0;pass<96;pass++)
                    {
                        int added=0;
                        foreach(var c in frontier)
                        {
                            if(rejected.Contains(c))continue;
                            bool ac=Find(c.a)==Find(backboneRoot),bc=Find(c.b)==Find(backboneRoot);
                            if(ac==bc)continue;
                            if(TryBuild(c)){added++;attached++;}
                            else rejected.Add(c);
                        }
                        if(added==0)break;
                    }
                    plan.notes.Add("Ankara: "+attached+" street segments extended directly from the backbone.");
                }
            }
            foreach (RoadKind kind in new[] {RoadKind.Arterial,RoadKind.Collector})
                foreach (StreetCandidate c in candidates.Where(c=>c.kind==kind).OrderBy(c=>adaptive?TerrainCandidateCost(c):c.length)) TryBuild(c);

            var locals = candidates.Where(c=>c.kind==RoadKind.Local).OrderBy(c=>adaptive?TerrainCandidateCost(c):c.length).ToList();
            foreach (StreetCandidate c in locals)
            {
                var style = StreetStyle((XZ(c.a.position)+XZ(c.b.position))*.5f);
                if (Find(c.a)==Find(c.b) && Next()>style.density*settings.loops) continue;
                if (c.a.degree==0 && c.b.degree==0 && Next()>style.density) continue;
                TryBuild(c);
            }
            // Repair gaps without converting the road layout into a triangulated graph.
            foreach (StreetCandidate c in locals)
                if (Find(c.a)!=Find(c.b) && (c.a.degree>0 || c.b.degree>0)) TryBuild(c);
            for (int x=0;x<xs.Count;x++)
            for (int z=0;z<zs.Count;z++)
            {
                Node n=grid[x,z];
                if(n==null||n.degree==0)continue;
                foreach(Vector2Int o in new[]{new Vector2Int(1,1),new Vector2Int(1,-1),new Vector2Int(2,0),new Vector2Int(0,2)})
                {
                    int xx=x+o.x,zz=z+o.y;
                    if(xx>=xs.Count||zz<0||zz>=zs.Count)continue;
                    Node other=grid[xx,zz];
                    if(other==null||other.degree==0||Find(n)==Find(other))continue;
                    float len=Vector2.Distance(XZ(n.position),XZ(other.position));
                    if(len<=local*2.4f)TryBuild(new StreetCandidate{a=n,b=other,kind=RoadKind.Collector,length=len});
                }
            }
            if (settings.terrainRepair)
            {
                // The regular street scaffold may leave every uphill edge infeasible.
                // In steep regions, attempt contour-oriented links even for isolated nodes.
                // Unlike component repair, this pass can create streets in completely empty bands.
                var available = nodes.Where(n => !n.gate).ToList();
                float maxReach = local * 3.5f;
                int infillAttempts = 0;
                foreach (Node a in available.Where(n => n.degree == 0).OrderBy(n => n.id).ToList())
                {
                    if (a.degree > 0) continue;
                    var alternatives = available.Where(b => b != a && b.degree < DegreeLimit(b))
                        .Select(b => new { node=b, distance=Vector2.Distance(XZ(a.position),XZ(b.position)),
                            rise=Mathf.Abs(a.position.y-b.position.y) })
                        .Where(c => c.distance >= RoadClearance*2.5f && c.distance <= maxReach &&
                            c.rise <= c.distance*settings.grade+.001f)
                        .OrderBy(c => c.distance + c.rise*5f + (c.node.degree==0 ? local*.25f : 0f))
                        .ThenBy(c=>c.node.id).Take(8);
                    foreach (var c in alternatives)
                    {
                        if (++infillAttempts > 2400) break;
                        if (TryBuild(new StreetCandidate {a=a,b=c.node,kind=RoadKind.Local,length=c.distance}))
                        { diagnostics.hillsideInfill++; break; }
                    }
                    if (infillAttempts > 2400) break;
                }
                RepairComponents(sector, bounds, local, nodes, Find, TryBuild, diagnostics);
            }
            var active=nodes.Where(n=>n.degree>0).ToList();
            Node keep=backboneRoot!=null && backboneRoot.degree>0?Find(backboneRoot):gates.Count>0?Find(gates[0]):active.GroupBy(Find).OrderByDescending(g=>g.Count()).ThenBy(g=>g.Key.id).Select(g=>g.Key).FirstOrDefault();
            if (keep == null) throw new InvalidOperationException(sector + ": no connected street component.");
            // Preview must show the attempted layout even when gate connectivity is incomplete.
            // Generation remains locked; detached roads/bridges are explicitly diagnosed.
            if (gates.Any(g => g.degree < 2 || Find(g) != keep))
                plan.warnings.Add(sector + ": bridge gates are NOT both connected to the retained city network. Preview only.");
            int dropped=0;
            foreach(Edge e in plan.edges.Where(e=>e.sector==sector&&Find(e.a)!=keep).ToList())
            {
                e.a.degree--;e.b.degree--;plan.edges.Remove(e);plan.detachedEdges.Add(e);dropped++;
            }
            if(dropped>0)plan.warnings.Add(sector+": "+dropped+" detached street segments shown in purple; NOT connected to the retained network.");
            int retained = plan.edges.Count(e=>e.sector==sector);
            plan.notes.Add(sector+": "+retained+" connected / "+dropped+" detached street segments; local scaffold "+local.ToString("F0")+" m, "+nodes.Count+"/"+budget+" candidate nodes.");
            plan.notes.Add(sector + ": hillside infill " + diagnostics.hillsideInfill + "; repair links " + diagnostics.directRepair + " direct, " + diagnostics.routed +
                " terrain-routed; rejected path variants: elevation " + diagnostics.elevationRejected +
                ", geometry " + diagnostics.geometryRejected + "; skipped nodes: protected " +
                diagnostics.protectedNodes + ", obstacles/bounds " + diagnostics.blockedNodes + ", missing terrain " + diagnostics.missingNodes + ".");
            AddCoverageReport(sector, bounds);
            if (adaptive) plan.authoredLinks.RemoveAll(e=>e.sector==sector && (e.b.degree==0 || Find(e.b)!=keep));
            if (backbone) ReportTerrainSeams(bounds);
            if (gradingSurface != null) ReportGrading(sector);
            index=new StreetIndex();
            foreach(Edge e in plan.edges)index.Add(e);
            if (adaptive) foreach(Edge e in plan.authoredLinks) index.Add(e);
            ProposeLinks(sector,bounds,nodes.Where(n=>n.degree>0&&!n.gate&&n.degree<4).ToList(),index);
            gradingSurface = null;
        }

        // Read-only, coarse virtual terrain surface. The grade envelopes reduce abrupt
        // height bands; actual roads are still checked against original terrain + explicit budget.
        private sealed class GradingSurface
        {
            private readonly Rect area;
            private readonly int nx,nz;
            private readonly float dx,dz;
            private readonly float[,] heights;

            public GradingSurface(Layout layout,Rect bounds,float grade)
            {
                area=bounds;
                nx=Mathf.Clamp(Mathf.CeilToInt(bounds.width/40f)+1,3,161);
                nz=Mathf.Clamp(Mathf.CeilToInt(bounds.height/40f)+1,3,161);
                dx=bounds.width/(nx-1);dz=bounds.height/(nz-1);
                var raw=new float[nx,nz];
                for(int x=0;x<nx;x++)
                for(int z=0;z<nz;z++)
                {
                    Vector2 pos=new Vector2(bounds.xMin+x*dx,bounds.yMin+z*dz);
                    if(!layout.TryHeight(pos,out raw[x,z]))
                        throw new InvalidOperationException("Missing land sample in Ankara virtual terrain at "+pos);
                }
                var smooth=(float[,])raw.Clone();
                for(int pass=0;pass<6;pass++)
                {
                    var prev=(float[,])smooth.Clone();
                    for(int x=0;x<nx;x++)
                    for(int z=0;z<nz;z++)
                    {
                        float sum=prev[x,z]*4f;int weight=4;
                        foreach(Vector2Int off in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down})
                        {
                            int xx=x+off.x,zz=z+off.y;
                            if(xx<0||xx>=nx||zz<0||zz>=nz)continue;
                            sum+=prev[xx,zz];weight++;
                        }
                        smooth[x,z]=sum/weight;
                    }
                }
                // Approximate lower/upper grade-limited envelopes on an 8-neighbor raster.
                var lower=(float[,])smooth.Clone();
                var upper=(float[,])smooth.Clone();
                float limit=grade*.78f;
                for(int pass=0;pass<4;pass++)
                {
                    bool reverse=pass%2==1;
                    for(int ix=0;ix<nx;ix++)
                    for(int iz=0;iz<nz;iz++)
                    {
                        int x=reverse?nx-1-ix:ix,z=reverse?nz-1-iz:iz;
                        for(int ox=-1;ox<=1;ox++)
                        for(int oz=-1;oz<=1;oz++)
                        {
                            if(ox==0&&oz==0)continue;
                            int xx=x+ox,zz=z+oz;
                            if(xx<0||xx>=nx||zz<0||zz>=nz)continue;
                            float rise=Mathf.Sqrt(ox*ox*dx*dx+oz*oz*dz*dz)*limit;
                            lower[x,z]=Mathf.Min(lower[x,z],lower[xx,zz]+rise);
                            upper[x,z]=Mathf.Max(upper[x,z],upper[xx,zz]-rise);
                        }
                    }
                }
                heights=new float[nx,nz];
                for(int x=0;x<nx;x++)
                for(int z=0;z<nz;z++)heights[x,z]=(lower[x,z]+upper[x,z])*.5f;
            }

            public float Sample(Vector2 pos)
            {
                float x=Mathf.Clamp((pos.x-area.xMin)/dx,0f,nx-1);
                float z=Mathf.Clamp((pos.y-area.yMin)/dz,0f,nz-1);
                int ix=Mathf.Min(nx-2,Mathf.FloorToInt(x)),iz=Mathf.Min(nz-2,Mathf.FloorToInt(z));
                return Mathf.Lerp(Mathf.Lerp(heights[ix,iz],heights[ix+1,iz],x-ix),
                    Mathf.Lerp(heights[ix,iz+1],heights[ix+1,iz+1],x-ix),z-iz);
            }
        }

        private GradingSurface gradingSurface;

        private bool CanGradeDistrict(Vector2 pos) => gradingSurface!=null &&
            !IsProtected(pos,RoadClearance+settings.blend) && !obstacles.Near(pos,RoadClearance*2f+settings.blend);

        private float GradingBudget(Vector2 pos) => CanGradeDistrict(pos)?
            Mathf.Max(settings.maxCutFill,settings.districtGradingLimit):settings.maxCutFill;

        private float PreferredGround(Vector2 pos,float original)
        {
            if(!CanGradeDistrict(pos))return original;
            float reserve=Mathf.Max(0f,settings.districtGradingLimit-settings.maxCutFill-1f);
            return original+Mathf.Clamp(gradingSurface.Sample(pos)-original,-reserve,reserve);
        }

        private void ReportTerrainSeams(Rect bounds)
        {
            var tiles = plan.layout.tiles.Where(t => Contains(bounds, t.area.center)).ToList();
            float worst = 0f; int large = 0, total = 0; string pair = "none";
            foreach (Tile a in tiles)
            foreach (Tile b in tiles)
            {
                bool row = b.row == a.row + 1 && b.column == a.column;
                bool column = b.column == a.column + 1 && b.row == a.row;
                if (!row && !column) continue;
                bool alongX = row ? plan.layout.rowsOnX : !plan.layout.rowsOnX;
                float diff = 0f;
                for (int i=0;i<=24;i++)
                {
                    float t = i / 24f;
                    Vector2 first = alongX ? new Vector2(a.area.xMax, Mathf.Lerp(a.area.yMin,a.area.yMax,t)) :
                        new Vector2(Mathf.Lerp(a.area.xMin,a.area.xMax,t),a.area.yMax);
                    Vector2 second = alongX ? new Vector2(b.area.xMin,Mathf.Lerp(b.area.yMin,b.area.yMax,t)) :
                        new Vector2(Mathf.Lerp(b.area.xMin,b.area.xMax,t),b.area.yMin);
                    diff = Mathf.Max(diff,Mathf.Abs(a.Height(first)-b.Height(second)));
                }
                total++;
                if (diff > 1f) large++;
                if (diff > worst) {worst=diff;pair=a.terrain.name+" / "+b.terrain.name;}
            }
            string note = "Ankara terrain seams: "+large+"/"+total+" boundaries have >1m sampled height discontinuity; worst "+worst.ToString("F1")+" m ("+pair+").";
            if (large>0) plan.warnings.Add(note); else plan.notes.Add(note);
        }

        private void ReportGrading(string sector)
        {
            int segments=0;float max=0f;
            foreach(var edge in plan.edges.Where(e=>e.sector==sector).Concat(plan.detachedEdges.Where(e=>e.sector==sector)))
            {
                bool marked=false;
                for(int i=0;i<edge.points.Count;i++)
                {
                    Vector3 point=edge.points[i];
                    Vector2 pos=XZ(point);
                    Vector2 tangent=XZ(edge.points[Mathf.Min(i+1,edge.points.Count-1)]-edge.points[Mathf.Max(0,i-1)]);
                    Vector2 right=new Vector2(-tangent.y,tangent.x).normalized;
                    float worst=0f;
                    for(int side=-1;side<=1;side++)
                    {
                        Vector2 q=pos+right*side*(settings.width*.5f+settings.sidewalk);
                        if(plan.layout.TryHeight(q,out float original))
                            worst=Mathf.Max(worst,Mathf.Abs(point.y-.12f-original));
                    }
                    max=Mathf.Max(max,worst);
                    if(worst>settings.maxCutFill+.01f)
                    {
                        marked=true;
                        if(i%6==0||i==edge.points.Count-1)plan.gradingMarkers.Add(point+Vector3.up);
                    }
                }
                if(marked)segments++;
            }
            plan.notes.Add(sector+": virtual grading needed on "+segments+" street segments; maximum sampled original-ground adjustment "+max.ToString("F1")+" m (incl. detached). Red markers are preview only.");
        }

        // Multi-source Dijkstra from the already connected component, routing to each
        // land tile before minor roads. Failed geometry/grade edges are blacklisted and retried.
        private void PlanBackbone(Rect bounds,Node root,List<Node> nodes,List<Node> gates,
            List<StreetCandidate> candidates,Func<Node,Node> find,Func<StreetCandidate,bool,bool> build)
        {
            var adjacency=new Dictionary<Node,List<StreetCandidate>>();
            foreach(var c in candidates)
            {
                foreach(Node n in new[]{c.a,c.b})
                {
                    if(!adjacency.TryGetValue(n,out List<StreetCandidate> list))adjacency[n]=list=new List<StreetCandidate>();
                    list.Add(c);
                }
            }
            var failed=new HashSet<(int,int)>();
            var targets=new List<List<Node>>();
            float shore= settings.ankaraLow?plan.layout.AlongMax(bounds):plan.layout.AlongMin(bounds);
            foreach(Tile tile in plan.layout.tiles.Where(t=>Contains(bounds,t.area.center)).OrderBy(t=>
                Mathf.Abs((plan.layout.rowsOnX?t.area.center.x:t.area.center.y)-shore)))
            {
                var group=nodes.Where(n=>!n.gate&&tile.area.Contains(XZ(n.position)))
                    .OrderBy(n=>(XZ(n.position)-tile.area.center).sqrMagnitude).ThenBy(n=>n.id).Take(18).ToList();
                if(group.Count>0)targets.Add(group);
            }
            foreach(Node gate in gates.Where(g=>g!=root))targets.Add(new List<Node>{gate});
            if(settings.terrainDesign)
                targets.InsertRange(0,plan.authoredLinks.Where(e=>e.sector==root.sector && e.b!=root).Select(e=>new List<Node>{e.b}));
            int reached=0,links=0,routed=0;
            foreach(var goalNodes in targets)
            {
                if(EditorUtility.DisplayCancelableProgressBar("Procedural World",
                    "Ankara: reserving backbone corridor "+(reached+1)+"/"+targets.Count,.20f))
                    throw new OperationCanceledException();
                for(int retry=0;retry<18&&!goalNodes.Any(n=>find(n)==find(root)&&n.degree>0);retry++)
                {
                    var goals=new HashSet<Node>(goalNodes);
                    var cost=new Dictionary<Node,float>();
                    var previous=new Dictionary<Node,(Node from,StreetCandidate edge)>();
                    var queue=new SortedSet<(float cost,int id,Node node)>(
                        Comparer<(float cost,int id,Node node)>.Create((a,b)=>{
                            int c=a.cost.CompareTo(b.cost);return c!=0?c:a.id.CompareTo(b.id);}));
                    Node group=find(root);
                    foreach(Node n in nodes.Where(n=>find(n)==group && (n.degree<DegreeLimit(n)||goals.Contains(n))))
                    {cost[n]=0f;queue.Add((0f,n.id,n));}
                    Node goal=null;
                    while(queue.Count>0)
                    {
                        var item=queue.Min;queue.Remove(item);
                        Node n=item.node;
                        if(cost[n]<item.cost)continue;
                        if(goals.Contains(n)&&n!=root){goal=n;break;}
                        if(!adjacency.TryGetValue(n,out List<StreetCandidate> edges))continue;
                        foreach(var c in edges)
                        {
                            Node next=c.a==n?c.b:c.a;
                            var key=(Mathf.Min(n.id,next.id),Mathf.Max(n.id,next.id));
                            if(failed.Contains(key))continue;
                            bool same=find(n)==find(next);
                            if(!same&&(n.degree>=DegreeLimit(n)||next.degree>=DegreeLimit(next)))continue;
                            float rise=Mathf.Abs(n.position.y-next.position.y);
                            if(!same&&rise>c.length*settings.grade+.01f)continue;
                            float price = item.cost + (same ? .001f : (settings.terrainDesign ? TerrainCandidateCost(c) : c.length) * (c.kind == RoadKind.Arterial ? .8f : c.kind == RoadKind.Collector ? 1f : 1.18f) + rise * 2f);
                            if(cost.TryGetValue(next,out float old)&&old<=price)continue;
                            cost[next]=price;previous[next]=(n,c);queue.Add((price,next.id,next));
                        }
                    }
                    if(goal==null)break;
                    var route=new List<StreetCandidate>();
                    Node cursor=goal;
                    while(previous.TryGetValue(cursor,out var prev)){route.Add(prev.edge);cursor=prev.from;}
                    route.Reverse();
                    if(route.Count==0)break;
                    bool progress=false;
                    foreach(var c in route)
                    {
                        if(find(c.a)==find(c.b))continue;
                        var arterial=new StreetCandidate{a=c.a,b=c.b,kind=RoadKind.Arterial,length=c.length};
                        bool accepted=build(arterial,false);
                        if(!accepted&&routed<12&&settings.terrainRepair)
                        {
                            routed++;
                            accepted=build(arterial,true);
                        }
                        if(!accepted)
                        {
                            failed.Add((Mathf.Min(c.a.id,c.b.id),Mathf.Max(c.a.id,c.b.id)));
                            break;
                        }
                        progress=true;links++;
                    }
                    if(!progress&&route.All(c=>find(c.a)==find(c.b)))break;
                }
                if(goalNodes.Any(n=>find(n)==find(root)&&n.degree>0))reached++;
            }
            plan.notes.Add("Ankara backbone: "+reached+"/"+targets.Count+" tile-center / bridge targets reached before local streets, "+links+" reserved arterial segments, "+failed.Count+" rejected connections.");
            if(reached<targets.Count)plan.warnings.Add("Ankara backbone could not reach every target at the selected grade and grading limits. Check coverage and red grading markers.");
        }

        // Repair the component graph before pruning. Long, terrain-aware connector
        // candidates can reach a contour street on another height band instead of
        // demanding that every short grid edge run straight uphill.
        private void RepairComponents(string sector, Rect bounds, float local, List<Node> nodes,
            Func<Node,Node> find, Func<StreetCandidate,bool,bool> build, StreetDiagnostics stats)
        {
            var active = nodes.Where(n => n.degree > 0 && n.degree < DegreeLimit(n)).ToList();
            var seen = new HashSet<(int,int)>();
            var candidates = new List<StreetCandidate>();
            float range = Mathf.Max(500f, local * 7f);
            foreach (Node a in active)
            {
                foreach (Node b in active.Where(n => n != a && find(n) != find(a) &&
                    (XZ(n.position)-XZ(a.position)).sqrMagnitude <= range*range)
                    .OrderBy(n => Vector2.Distance(XZ(n.position),XZ(a.position)) +
                        Mathf.Abs(n.position.y-a.position.y)*2f).Take(6))
                {
                    if (!seen.Add((Mathf.Min(a.id,b.id),Mathf.Max(a.id,b.id)))) continue;
                    float length = Vector2.Distance(XZ(a.position),XZ(b.position));
                    if (length < RoadClearance*2.5f) continue;
                    candidates.Add(new StreetCandidate { a=a,b=b,kind=RoadKind.Collector,length=length });
                }
            }
            var ordered = candidates.OrderBy(c => c.length + Mathf.Abs(c.a.position.y-c.b.position.y)*2f).ToList();
            int directAttempts = 0;
            foreach (var c in ordered)
            {
                if (find(c.a)==find(c.b) || c.a.degree>=DegreeLimit(c.a) || c.b.degree>=DegreeLimit(c.b)) continue;
                if (++directAttempts > 400) break;
                if (build(c,false)) stats.directRepair++;
            }
            int routedAttempts = 0;
            var pairAttempts = new Dictionary<(int,int),int>();
            // Favor connecting the largest remaining fragments and bridge gates.
            var sizes = nodes.Where(n=>n.degree>0).GroupBy(find).ToDictionary(g=>g.Key,g=>g.Count());
            foreach (var c in ordered.OrderByDescending(c =>
                (c.a.gate||c.b.gate ? 100000 : 0) +
                Mathf.Min(sizes.TryGetValue(find(c.a),out int sa)?sa:0,
                          sizes.TryGetValue(find(c.b),out int sb)?sb:0))
                .ThenBy(c=>c.length))
            {
                if (find(c.a)==find(c.b) || c.a.degree>=DegreeLimit(c.a) || c.b.degree>=DegreeLimit(c.b)) continue;
                Node groupA=find(c.a),groupB=find(c.b);
                var pair=(Mathf.Min(groupA.id,groupB.id),Mathf.Max(groupA.id,groupB.id));
                pairAttempts.TryGetValue(pair,out int tried);
                if(tried>=4)continue;
                pairAttempts[pair]=tried+1;
                if (++routedAttempts > 48) break;
                if (EditorUtility.DisplayCancelableProgressBar("Procedural World",
                    sector + ": terrain connector " + routedAttempts + "/48", .45f))
                    throw new OperationCanceledException();
                if (build(c,true)) stats.routed++;
            }
        }

        private void AddCoverageReport(string sector, Rect bounds)
        {
            // Report both successful and detached coverage by actual terrain tiles.
            var tiles = plan.layout.tiles.Where(t => Contains(bounds, t.area.center)).ToList();
            var connected = new HashSet<Tile>();
            var detached = new HashSet<Tile>();
            foreach (var edge in plan.edges.Where(e=>e.sector==sector))
                foreach (Tile tile in tiles)
                    if (edge.points.Any(p=>tile.area.Contains(XZ(p)))) connected.Add(tile);
            foreach (var edge in plan.detachedEdges.Where(e=>e.sector==sector))
                foreach (Tile tile in tiles)
                    if (edge.points.Any(p=>tile.area.Contains(XZ(p)))) detached.Add(tile);
            plan.notes.Add(sector + ": connected terrain coverage " + connected.Count + "/" + tiles.Count +
                "; detached roads in " + detached.Count + " tiles (may overlap connected tiles). Protected land can be intentionally empty.");
            foreach (District district in districts.Where(d=>bounds.Overlaps(d.area)))
            {
                if (protectedAreas.Any(r=>r==district.area)) continue;
                int live = plan.edges.Count(e=>e.sector==sector && e.points.Any(p=>district.area.Contains(XZ(p))));
                int orphan = plan.detachedEdges.Count(e=>e.sector==sector && e.points.Any(p=>district.area.Contains(XZ(p))));
                if (live==0) plan.warnings.Add(sector + " / " + district.name + ": no connected planned streets (" + orphan + " detached).");
            }
        }

        private static readonly Vector2Int[] SearchMoves = {
            new Vector2Int(1,0),new Vector2Int(-1,0),new Vector2Int(0,1),new Vector2Int(0,-1),
            new Vector2Int(1,1),new Vector2Int(-1,1),new Vector2Int(1,-1),new Vector2Int(-1,-1),
            new Vector2Int(2,1),new Vector2Int(2,-1),new Vector2Int(-2,1),new Vector2Int(-2,-1),
            new Vector2Int(1,2),new Vector2Int(-1,2),new Vector2Int(1,-2),new Vector2Int(-1,-2)
        };

        // Bounded terrain-aware A* on a temporary raster. Final geometry is still a
        // sampled polyline and is checked by the same elevation/clearance validators.
        private bool TryTerrainConnector(Node a, Node b, Rect bounds, StreetIndex index, out List<Vector3> points)
        {
            points = null;
            Vector2 start=XZ(a.position), end=XZ(b.position);
            float step=Mathf.Clamp(settings.spacing*.22f,14f,26f);
            float dist=Vector2.Distance(start,end);
            float padding=Mathf.Clamp(Mathf.Max(dist*.65f, Mathf.Abs(a.position.y-b.position.y)/settings.grade*.45f),100f,440f);
            Rect search=Rect.MinMaxRect(Mathf.Max(bounds.xMin+RoadClearance,Mathf.Min(start.x,end.x)-padding),
                Mathf.Max(bounds.yMin+RoadClearance,Mathf.Min(start.y,end.y)-padding),
                Mathf.Min(bounds.xMax-RoadClearance,Mathf.Max(start.x,end.x)+padding),
                Mathf.Min(bounds.yMax-RoadClearance,Mathf.Max(start.y,end.y)+padding));
            float gap=RoadClearance*2f;
            // Local point queries are substantially cheaper than scanning all edges in the search rectangle.
            var clearCache = new Dictionary<Vector2,bool>();
            bool PointClear(Vector2 pos)
            {
                if (clearCache.TryGetValue(pos,out bool cached)) return cached;
                bool result=PointClearUncached(pos);
                clearCache[pos]=result;
                return result;
            }
            bool PointClearUncached(Vector2 pos)
            {
                if(!Contains(search,pos)||IsProtected(pos,RoadClearance)||obstacles.Near(pos,gap))return false;
                foreach(Edge edge in index.Query(new Rect(pos.x-gap,pos.y-gap,gap*2f,gap*2f)))
                {
                    if ((edge.a==a||edge.b==a)&&Vector2.Distance(pos,start)<gap*1.5f)continue;
                    if ((edge.a==b||edge.b==b)&&Vector2.Distance(pos,end)<gap*1.5f)continue;
                    for(int i=1;i<edge.points.Count;i++)
                        if(DistanceSquared(pos,XZ(edge.points[i-1]),XZ(edge.points[i]))<gap*gap)return false;
                }
                return true;
            }
            bool SegmentOk(Vector2 from,Vector2 to,out float cost)
            {
                cost=0f;
                float len=Vector2.Distance(from,to);
                if(len<.01f)return false;
                int steps=Mathf.Max(1,Mathf.CeilToInt(len/8f));
                if(!plan.layout.TryHeight(from,out float previous))return false;
                previous=PreferredGround(from,previous);
                float totalRise=0f;
                for(int i=1;i<=steps;i++)
                {
                    Vector2 pos=Vector2.Lerp(from,to,i/(float)steps);
                    if(!PointClear(pos)||!plan.layout.TryHeight(pos,out float h))return false;
                    h=PreferredGround(pos,h);
                    float dy=Mathf.Abs(h-previous);
                    // A small amount of terrain cut/fill is allowed during search;
                    // the final solver enforces the actual road grade and full width.
                    if(dy>len/steps*settings.grade+Mathf.Min(2f,settings.maxCutFill*.25f))return false;
                    totalRise+=dy;previous=h;
                }
                cost=len+totalRise*5f;
                return true;
            }
            Vector2 Position(Vector2Int cell)=>start+new Vector2(cell.x*step,cell.y*step);
            var origin=Vector2Int.zero;
            var costs=new Dictionary<Vector2Int,float>{{origin,0f}};
            var parents=new Dictionary<Vector2Int,Vector2Int>();
            var queue=new SortedSet<(float priority,int sequence,Vector2Int cell)>(
                Comparer<(float priority,int sequence,Vector2Int cell)>.Create((x,y)=>{
                    int c=x.priority.CompareTo(y.priority);return c!=0?c:x.sequence.CompareTo(y.sequence);}));
            int seq=0,expanded=0;
            queue.Add((dist,seq++,origin));
            var closed=new HashSet<Vector2Int>();
            Vector2Int goal=default;bool found=false;
            while(queue.Count>0&&expanded<3800)
            {
                var item=queue.Min;queue.Remove(item);
                Vector2Int cell=item.cell;
                if(!closed.Add(cell))continue;
                expanded++;
                if (expanded % 400 == 0 && EditorUtility.DisplayCancelableProgressBar("Procedural World", "Searching terrain connector: " + expanded + " cells", .45f))
                    throw new OperationCanceledException();
                Vector2 pos=Position(cell);
                if(Vector2.Distance(pos,end)<=step*1.5f&&SegmentOk(pos,end,out _))
                {goal=cell;found=true;break;}
                foreach(Vector2Int move in SearchMoves)
                {
                    Vector2Int next=cell+move;
                    if(closed.Contains(next))continue;
                    Vector2 target=Position(next);
                    if(!SegmentOk(pos,target,out float price))continue;
                    if(parents.TryGetValue(cell,out Vector2Int previous))
                        price+=Vector2.Angle(pos-Position(previous),target-pos)*.25f;
                    float g=costs[cell]+price;
                    if(costs.TryGetValue(next,out float known)&&g>=known)continue;
                    costs[next]=g;parents[next]=cell;
                    float h=Vector2.Distance(target,end);
                    queue.Add((g+h,seq++,next));
                }
            }
            if(!found)return false;
            var route=new List<Vector2>{end,Position(goal)};
            while(goal!=origin){goal=parents[goal];route.Add(Position(goal));}
            route.Reverse();
            // Remove redundant raster vertices by safe line-of-sight shortcuts.
            var simplified=new List<Vector2>{route[0]};
            int at=0;
            while(at<route.Count-1)
            {
                int next=at+1;
                for(int i=Mathf.Min(route.Count-1,at+8);i>next;i--)
                    if(SegmentOk(route[at],route[i],out _)){next=i;break;}
                if(Vector2.Distance(simplified[simplified.Count-1],route[next])>.01f)
                    simplified.Add(route[next]);
                at=next;
            }
            foreach (var polyline in new[] { simplified, route })
            {
                var path=new List<Vector3>();
                for(int i=1;i<polyline.Count;i++)
                {
                    Vector2 from=polyline[i-1],to=polyline[i];
                    if(Vector2.Distance(from,to)<.01f)continue;
                    int n=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(from,to)/3f));
                    for(int j=path.Count==0?0:1;j<=n;j++)
                    {
                        Vector2 pos=Vector2.Lerp(from,to,j/(float)n);
                        if(!plan.layout.TryHeight(pos,out float ground))return false;
                        path.Add(XYZ(pos,ground+.12f));
                    }
                }
                if(path.Count<2)continue;
                path[0]=a.position;path[path.Count-1]=b.position;
                if(!SolveElevation(path)||!StreetAngles(a,b,path,index)||StreetCrosses(a,b,path,index))continue;
                points=path;return true;
            }
            return false;
        }

        private bool TryStreet(Node a,Node b,Rect bounds,RoadKind kind,StreetIndex index,bool authored,out List<Vector3> points)
        {
            points=null;
            Vector2 start=XZ(a.position),end=XZ(b.position);
            float len=Vector2.Distance(start,end);
            if(len<1f)return false;
            Vector2 dir=(end-start).normalized,side=new Vector2(-dir.y,dir.x);
            var style=StreetStyle((start+end)*.5f);
            float bend=Mathf.Min(len*.13f,22f)*settings.curve*style.bend;
            if(kind==RoadKind.Arterial)bend*=.22f;
            if(kind==RoadKind.Collector)bend*=.6f;
            if(Next()<.5f)bend=-bend;
            bool straight=kind==RoadKind.Arterial||Next()<settings.straightPreference;
            float bestCost = float.PositiveInfinity;
            foreach(float offset in straight?new[]{0f,bend,-bend,bend*2f,-bend*2f}:new[]{bend,-bend,0f,bend*2f,-bend*2f})
            {
                int steps=Mathf.Max(2,Mathf.CeilToInt(len/3f));
                var path=new List<Vector3>(steps+1);bool valid=true;
                for(int i=0;i<=steps;i++)
                {
                    float t=(float)i/steps;
                    Vector2 pos=Vector2.Lerp(start,end,t)+side*(4f*t*(1f-t)*offset);
                    float fromAnchor=Vector2.Distance(pos,start);
                    if(!Contains(bounds,pos,-RoadClearance)||
                        (!(authored&&fromAnchor<=55f)&&IsProtected(pos,RoadClearance))||
                        (!(authored&&fromAnchor<=RoadClearance*2.2f)&&obstacles.Near(pos,authored?RoadClearance*1.4f:RoadClearance*2f))||
                        !plan.layout.TryHeight(pos,out float ground))
                    {valid=false;break;}
                    path.Add(XYZ(pos,ground+.12f));
                }
                if(!valid)continue;
                path[0]=a.position;path[path.Count-1]=b.position;
                if(!SolveElevation(path)) { if (diagnostics != null) diagnostics.elevationRejected++; continue; }
                if(!StreetAngles(a,b,path,index)||StreetCrosses(a,b,path,index))
                { if (diagnostics != null) diagnostics.geometryRejected++; continue; }
                if(authored&&!ProtectedSurfaceOk(path))continue;
                if (!settings.terrainDesign) { points=path;return true; }
                float cost = ProfileCost(path) + Mathf.Abs(offset) * .02f;
                if (cost < bestCost) { bestCost=cost; points=path; }
                // If the straight candidate needs negligible earthworks, avoid unnecessary bends.
                if (cost < .08f && straight) break;
            }
            return points != null;
        }

        private bool StreetAngles(Node a,Node b,List<Vector3> path,StreetIndex index)
        {
            foreach(Node n in new[]{a,b})
            {
                Vector2 dir=n==a?XZ(path[1]-path[0]).normalized:XZ(path[path.Count-2]-path[path.Count-1]).normalized;
                foreach(Edge e in index.At(n))
                {
                    Vector2 old=e.a==n?XZ(e.points[1]-e.points[0]).normalized:XZ(e.points[e.points.Count-2]-e.points[e.points.Count-1]).normalized;
                    if(Vector2.Angle(dir,old)<(e.Bridge?100f:48f))return false;
                }
            }
            return true;
        }

        private bool StreetCrosses(Node a,Node b,List<Vector3> path,StreetIndex index)
        {
            float gap=RoadClearance*2f;
            Rect box=BoundsOf(path,gap);
            foreach(Edge e in index.Query(box))
            {
                if(!box.Overlaps(BoundsOf(e.points,gap)))continue;
                Node shared=e.a==a||e.b==a?a:e.a==b||e.b==b?b:null;
                for(int i=0;i<path.Count;i++)
                {
                    Vector2 pos=XZ(path[i]);
                    if(shared!=null&&Vector2.Distance(pos,XZ(shared.position))<gap*1.6f)continue;
                    for(int j=1;j<e.points.Count;j++)
                        if(DistanceSquared(pos,XZ(e.points[j-1]),XZ(e.points[j]))<gap*gap)return true;
                }
            }
            return false;
        }

        private sealed class LinkCandidate
        {
            public Vector3 position;
            public Node target;
            public float score;
        }

        // Green proposals are deliberately separate from the graph; no manual road is split in preview.
        private void ProposeLinks(string sector,Rect bounds,List<Node> targets,StreetIndex index)
        {
            if(settings.authoredLinkTarget<=0||targets.Count==0)return;
            var links=new List<LinkCandidate>();bool hasManual=false;
            foreach(var road in manualRoads)
            {
                if(!Contains(bounds,XZ(road.GetPoint(.5f))))continue;
                hasManual=true;
                float length=road.GetTotalLength();
                int count=Mathf.Max(1,Mathf.CeilToInt(length/18f));
                for(int i=0;i<=count;i++)
                {
                    float t=road.DistanceToT(length*i/count);
                    Vector3 point=road.GetPoint(t);Vector2 pos=XZ(point);
                    if(!Contains(bounds,pos,-RoadClearance)||protectedAreas.Any(r=>Contains(r,pos,-45f)))continue;
                    Vector2 tangent=XZ(road.GetTangent(t)).normalized;
                    foreach(Node target in targets.OrderBy(n=>(XZ(n.position)-pos).sqrMagnitude).Take(settings.terrainDesign?12:5))
                    {
                        Vector2 delta=XZ(target.position)-pos;
                        float dist=delta.magnitude;
                        if(dist<RoadClearance*2.5f||dist>Mathf.Max(settings.terrainDesign?380f:260f,settings.spacing*3f))continue;
                        float angle=Vector2.Angle(tangent,delta);
                        bool endpoint=i==0||i==count;
                        if(!endpoint&&(angle<55f||angle>125f))continue;
                        links.Add(new LinkCandidate{position=point,target=target,score=dist+(endpoint?0f:10f)+Mathf.Abs(point.y-target.position.y)*3f});
                    }
                }
            }
            if(!hasManual)return;
            int made=plan.authoredLinks.Count(e=>e.sector==sector),attempts=0;
            foreach(LinkCandidate link in links.OrderBy(c=>c.score))
            {
                if(made>=settings.authoredLinkTarget||attempts>=250)break;
                if(plan.authoredLinks.Any(e=>e.sector==sector&&(Vector2.Distance(XZ(e.a.position),XZ(link.position))<85f||e.b==link.target)))continue;
                attempts++;
                Node anchor=new Node{id=-1,sector=sector,position=link.position};
                if(!TryStreet(anchor,link.target,bounds,RoadKind.AuthoredLink,index,true,out List<Vector3> path))continue;
                var proposal=new Edge{a=anchor,b=link.target,points=path,sector=sector,kind=RoadKind.AuthoredLink};
                plan.authoredLinks.Add(proposal);index.Add(proposal);made++;
            }
            string report=sector+": "+made+"/"+settings.authoredLinkTarget+" existing-road connection proposals (green; preview only).";
            if (!settings.terrainDesign || targets.Any(n=>n.degree>0))
            { if(made<settings.authoredLinkTarget)plan.warnings.Add(report);else plan.notes.Add(report); }
        }

        private void PlanCity(string sector, Rect bounds)
        {
            var nodes = plan.nodes.Where(n => n.sector == sector).ToList();
            float spacing = Mathf.Max(settings.spacing, Mathf.Sqrt(bounds.width * bounds.height / settings.maxNodesPerCity));
            if (spacing > settings.spacing + 1f)
                plan.warnings.Add(sector + ": spacing increased to " + spacing.ToString("F0") + " m for MVP road budget.");
            float inset = RoadClearance + 3f;
            foreach (District district in districts)
            {
                Vector2 p = district.area.center;
                if (Contains(bounds, p, -inset) && CanPlaceNode(p, nodes, spacing * .55f))
                    nodes.Add(AddLandNode(p, sector));
            }
            int target = Mathf.Clamp(Mathf.RoundToInt(bounds.width * bounds.height / (spacing * spacing)), 8, settings.maxNodesPerCity);
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
                if (!ProtectedSurfaceOk(path)) continue;
                points = path;
                return true;
            }
            return false;
        }

        private bool ProtectedSurfaceOk(List<Vector3> path)
        {
            for (int i = 0; i < path.Count; i++)
            {
                Vector2 p = XZ(path[i]);
                if (!IsProtected(p, RoadClearance) && !obstacles.Near(p, RoadClearance * 2f)) continue;
                Vector2 tangent = XZ(path[Mathf.Min(i + 1, path.Count - 1)]) - XZ(path[Mathf.Max(0, i - 1)]);
                Vector2 right = new Vector2(-tangent.y, tangent.x).normalized;
                for (int side = -1; side <= 1; side++)
                    if (!plan.layout.TryHeight(p + right * side * settings.width * .5f, out float ground) || ground > path[i].y + .02f) return false;
            }
            return true;
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
                float budget = GradingBudget(p);
                lo[i] = h + .12f - budget;
                hi[i] = h + .12f + budget;
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector2 sidePoint = p + right * roadHalf * s;
                    if (!plan.layout.TryHeight(sidePoint, out float cross)) return false;
                    float sideBudget = GradingBudget(sidePoint);
                    lo[i] = Mathf.Max(lo[i], cross + .12f - sideBudget);
                    hi[i] = Mathf.Min(hi[i], cross + .12f + sideBudget);
                }
                target[i] = PreferredGround(p, h) + .12f;
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
                    float minimum = e.Bridge ? 105f : 42f;
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
            return AddNode(XYZ(p, PreferredGround(p, h) + .12f), sector, false);
        }
        private Node AddNode(Vector3 p, string sector, bool gate)
        {
            var node = new Node { id = plan.nodes.Count, position = p, sector = sector, gate = gate };
            plan.nodes.Add(node); return node;
        }
        private void AddEdge(Node a, Node b, List<Vector3> points, string sector)
        {
            plan.edges.Add(new Edge { a = a, b = b, points = points, sector = sector, kind = sector == "Highways" ? RoadKind.Bridge : RoadKind.Local });
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
