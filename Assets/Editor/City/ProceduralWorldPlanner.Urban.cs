using System;
using System.Collections.Generic;
using System.Linq;
using TrafficSystem.Spline;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryDriver.EditorTools
{
    internal sealed partial class ProceduralWorldPlanner
    {
        internal enum UrbanMode { TerrainAdaptive, ContourFirst, DistrictBased }
        internal enum AreaKind { Park, Plaza, BuildingLot }
        internal sealed class AccessAnchor
        {
            public SplineRoad road;
            public float t;
        }
        internal sealed class AnchorResult
        {
            public AccessAnchor anchor;
            public Vector3 position;
            public bool connected;
            public string message;
        }
        [Serializable]
        internal sealed class UrbanArea
        {
            public AreaKind kind;
            public string sector, district;
            public Vector3[] corners;
            public Rect bounds;
            public bool accessible;
            public string prefab;
            public float frontage, depth, foundationRelief, accessGrade;
        }
        [Serializable]
        internal sealed class AnalysisLine
        {
            public Vector3 a, b;
            public float value;
        }

        // Baseline comparison is read-only and uses the exact V5 code path with V6
        // features disabled. No scene state is shared between candidate planners.
        private static Plan BuildV6(Settings settings, Layout layout, Scene scene)
        {
            if (!settings.urbanDesign) return BuildV5(settings, layout, scene);
            Plan candidate = null, baseline = null;
            InvalidOperationException failure = null;
            try { candidate = BuildOnce(settings, layout, scene); }
            catch (InvalidOperationException ex) { failure = ex; }
            if(candidate==null && !settings.urbanGuard) throw failure ?? new InvalidOperationException("V6 candidate failed.");
            if (settings.urbanGuard)
            {
                try
                {
                    settings.urbanDesign = false;
                    baseline = BuildV5(settings, layout, scene);
                }
                catch (InvalidOperationException) { if (candidate == null) throw; }
                finally { settings.urbanDesign = true; }
            }
            if (candidate == null && baseline == null)
                throw failure ?? new InvalidOperationException("No urban layout could be planned.");
            bool regression = candidate == null || (baseline != null &&
                (candidate.BridgeCount < baseline.BridgeCount ||
                (GatesConnected(baseline) && !GatesConnected(candidate)) ||
                new[] { "Ankara", "İstanbul" }.Any(s => Coverage(candidate,s) < Coverage(baseline,s)) ||
                new[] { "Ankara", "İstanbul" }.Any(s => candidate.edges.Count(e=>e.sector==s) <
                    baseline.edges.Count(e=>e.sector==s)*.60f) ||
                candidate.detachedEdges.Count > baseline.detachedEdges.Count + Mathf.Max(10,baseline.edges.Count/30)));
            string comparison = candidate != null && baseline != null ?
                "Ankara sampled earthworks: V5 " + EarthworkSummary(baseline,"Ankara",settings) +
                "; V6 " + EarthworkSummary(candidate,"Ankara",settings) + "." : null;
            if (baseline != null && regression)
            {
                baseline.version = "V5 fallback";
                baseline.warnings.Insert(0, "V6 coverage guard selected V5. V6 district layout, parks and anchors were NOT accepted. Disable guard to inspect the candidate. " + (failure?.Message ?? ""));
                var analyzer = AnalyzeOnly(settings,layout,scene);
                baseline.terrainCells.Clear(); baseline.terrainCells.AddRange(analyzer.terrainCells);
                baseline.contours.AddRange(analyzer.contours); baseline.seams.AddRange(analyzer.seams);
                foreach(var anchor in settings.accessAnchors)
                    if(anchor.road!=null)baseline.accessResults.Add(new AnchorResult{anchor=anchor,
                        position=anchor.road.GetPoint(anchor.t),message="Not evaluated in V5 fallback."});
                if(comparison!=null)baseline.notes.Insert(0,comparison);
                return baseline;
            }
            candidate.version = "V6";
            if(comparison!=null)candidate.notes.Insert(0,comparison);
            candidate.notes.Insert(0,baseline!=null?"V6 retained by coverage/connectivity guard.":"V6 candidate (no baseline comparison).");
            return candidate;
        }

        private float BridgeApproachCost(List<Vector3> path)
        {
            float cost=0f;
            for(int i=1;i<path.Count;i+=4)
            {
                Vector3 p=path[i];
                if(plan.layout.sea.Contains(XZ(p)))continue;
                if(plan.layout.TryHeight(XZ(p),out float h))
                    cost+=Mathf.Abs(p.y-h)*Vector3.Distance(path[i-1],p);
            }
            float endpointPenalty=0f;
            foreach(var pos in new[]{XZ(path[0]),XZ(path[path.Count-1])})
            {
                var cell=AnalysisAt(pos);
                if(cell!=null)endpointPenalty+=cell.slope*35f+cell.roughness*2f;
            }
            return cost/Mathf.Max(1,path.Count*.25f)+endpointPenalty;
        }

        private float ContourPenalty(Vector2 a,Vector2 b)
        {
            Vector2 direction=(b-a).normalized;
            float penalty=0f;
            for(int i=1;i<=3;i++)
            {
                var cell=AnalysisAt(Vector2.Lerp(a,b,i*.25f));
                if(cell==null)continue;
                penalty+=Mathf.Abs(Vector2.Dot(direction,cell.uphill))*cell.slope*6f;
            }
            return penalty/3f*settings.contourPreference*(settings.urbanMode==UrbanMode.ContourFirst?2f:settings.urbanMode==UrbanMode.DistrictBased?.40f:1f);
        }

        private float UrbanProfileCost(List<Vector3> path)
        {
            float cost=0f;int count=0;
            for(int i=1;i<path.Count;i+=2)
            {
                Vector3 p=path[i];
                Vector2 pos=XZ(p);
                Vector2 d=XZ(path[i]-path[i-1]).normalized;
                Vector2 side=new Vector2(-d.y,d.x)*(settings.width*.5f+settings.sidewalk);
                for(int s=-1;s<=1;s++)
                {
                    if(!plan.layout.TryHeight(pos+side*s,out float ground))continue;
                    float delta=Mathf.Abs(p.y-.12f-ground);
                    cost+=delta+Mathf.Pow(Mathf.Max(0f,delta-settings.maxCutFill),2f)*.12f;
                    count++;
                }
                cost+=ContourPenalty(XZ(path[i-1]),pos);
            }
            return settings.gradingOptimization*cost/Mathf.Max(1,count);
        }

        private bool Reserved(Vector2 p,float margin) => plan.openSpaces.Any(a=>Contains(a.bounds,p,margin));

        internal static bool SegmentHitsRect(Vector2 a,Vector2 b,Rect rect,float margin)
        {
            Vector2 delta=b-a;float lo=0f,hi=1f;
            bool Clip(float p,float q)
            {
                if(Mathf.Abs(p)<.000001f)return q>=0f;
                float t=q/p;
                if(p<0f)lo=Mathf.Max(lo,t);else hi=Mathf.Min(hi,t);
                return lo<=hi;
            }
            return Clip(-delta.x,a.x-rect.xMin+margin)&&Clip(delta.x,rect.xMax+margin-a.x)&&
                Clip(-delta.y,a.y-rect.yMin+margin)&&Clip(delta.y,rect.yMax+margin-a.y);
        }
        private bool PathHitsReservation(List<Vector3> path)
        {
            Rect box=BoundsOf(path,RoadClearance);
            foreach(var space in plan.openSpaces)
            {
                if(!space.bounds.Overlaps(box))continue;
                for(int i=1;i<path.Count;i++)
                    if(SegmentHitsRect(XZ(path[i-1]),XZ(path[i]),space.bounds,RoadClearance))return true;
            }
            return false;
        }

        private UrbanArea MakeArea(Rect area,string sector,string district,AreaKind kind)
        {
            var corners=new[]{new Vector2(area.xMin,area.yMin),new Vector2(area.xMax,area.yMin),
                new Vector2(area.xMax,area.yMax),new Vector2(area.xMin,area.yMax)};
            return new UrbanArea{bounds=area,sector=sector,district=district,kind=kind,
                corners=corners.Select(p=>{plan.layout.TryHeight(p,out float h);return XYZ(p,h+.4f);}).ToArray()};
        }

        private IEnumerable<Rect> UrbanBlocks(List<AxisLine> xs,List<AxisLine> zs)
        {
            var x=xs.Where(a=>a.kind!=RoadKind.Local).Select(a=>a.position).ToList();
            var z=zs.Where(a=>a.kind!=RoadKind.Local).Select(a=>a.position).ToList();
            for(int i=1;i<x.Count;i++)
            for(int j=1;j<z.Count;j++)
                yield return Rect.MinMaxRect(x[i-1],z[j-1],x[i],z[j]);
        }
        private string DistrictName(Vector2 p,string sector)
        {
            var district=districts.FirstOrDefault(d=>d.area.Contains(p));
            if(district!=null)return district.name;
            var tile=plan.layout.tiles.FirstOrDefault(t=>t.area.Contains(p));
            return tile==null?sector:sector+" tile "+tile.row+"/"+tile.column;
        }
        private int StableHash(string s)
        {
            unchecked {int h=settings.seed;foreach(char c in s)h=h*31+c;return h;}
        }

        private void ReserveOpenSpaces(string sector,Rect bounds,List<AxisLine> xs,List<AxisLine> zs,float local)
        {
            if(settings.openSpaceFraction<=0f)return;
            var candidates=new List<(Rect area,float rank,AreaKind kind,string district)>();
            foreach(Rect block in UrbanBlocks(xs,zs))
            {
                float inset=Mathf.Max(RoadClearance*2f+8f,Mathf.Min(block.width,block.height)*.12f);
                if(block.width<=inset*2f+30f||block.height<=inset*2f+30f)continue;
                Rect r=Rect.MinMaxRect(block.xMin+inset,block.yMin+inset,block.xMax-inset,block.yMax-inset);
                if(protectedAreas.Any(p=>p.Overlaps(r)))continue;
                if(plan.edges.Any(e=>e.Bridge&&Enumerable.Range(1,e.points.Count-1).Any(i=>
                    SegmentHitsRect(XZ(e.points[i-1]),XZ(e.points[i]),r,RoadClearance*2f))))continue;
                var samples=new[]{r.center,new Vector2(r.xMin,r.yMin),new Vector2(r.xMax,r.yMin),
                    new Vector2(r.xMax,r.yMax),new Vector2(r.xMin,r.yMax)};
                if(samples.Any(p=>obstacles.Near(p,RoadClearance*2f)))continue;
                bool authoredOverlap=false;
                for(float x=r.xMin;x<=r.xMax&&!authoredOverlap;x+=12f)
                for(float z=r.yMin;z<=r.yMax;z+=12f)
                    if(obstacles.Near(new Vector2(x,z),RoadClearance*2f)){authoredOverlap=true;break;}
                if(authoredOverlap)continue;
                // Reserve steep interior blocks as green space; occasional flatter plaza.
                var cell=AnalysisAt(r.center);
                float slope=cell?.slope??0f;
                string district=DistrictName(r.center,sector);
                float noise=((uint)StableHash(district+block.center.ToString("F1"))%1000)/1000f;
                bool plaza=slope<.035f&&noise<.23f;
                candidates.Add((r,slope*2f+noise*.25f,plaza?AreaKind.Plaza:AreaKind.Park,district));
            }
            float target=bounds.width*bounds.height*settings.openSpaceFraction;
            float reserved=0f;
            foreach(var c in candidates.OrderByDescending(c=>c.rank))
            {
                float area=c.area.width*c.area.height;
                if(reserved+area>target*1.06f)continue;
                plan.openSpaces.Add(MakeArea(c.area,sector,c.district,c.kind));reserved+=area;
                if(reserved>=target*.96f)break;
            }
            plan.notes.Add(sector+": open space reserved "+(reserved/(bounds.width*bounds.height)*100f).ToString("F1")+
                "% of city area (target "+(settings.openSpaceFraction*100f).ToString("F0")+"%).");
        }

        // Independent oriented subgrids live INSIDE major blocks. The V5 major street
        // scaffold remains continuous; each neighborhood shares a stable orientation.
        // Explicit gateway candidates join the two graphs, subject to all road checks.
        private void AddDistrictStreets(string sector,Rect bounds,List<AxisLine> xs,List<AxisLine> zs,
            Node[,] grid,float local,int budget,List<Node> nodes,Action<Node,Node,RoadKind> add)
        {
            var boundary=nodes.Where(n=>!n.gate).ToList();
            int total=0,blocks=0;
            int scanned=0;
            foreach(Rect block in UrbanBlocks(xs,zs))
            {
                if(++scanned%24==0&&EditorUtility.DisplayCancelableProgressBar("Procedural World",sector+": independent neighborhood blocks "+scanned,.24f))
                    throw new OperationCanceledException();
                if(nodes.Count>=budget)break;
                var regions=districts.Where(d=>d.area.Overlaps(block)).Select(d=>
                    Rect.MinMaxRect(Mathf.Max(block.xMin,d.area.xMin),Mathf.Max(block.yMin,d.area.yMin),
                        Mathf.Min(block.xMax,d.area.xMax),Mathf.Min(block.yMax,d.area.yMax))).ToList();
                if(regions.Count==0)regions.Add(block);
                // A district boundary can cut a major block. Build separately in each
                // clipped region, without placing nodes into overlapping authored zones.
                foreach(Rect region in regions)
                {
                    float margin=RoadClearance*2.7f;
                    if(region.width<margin*2f+20f||region.height<margin*2f+20f)continue;
                    var style=StreetStyle(region.center);
                    string district=DistrictName(region.center,sector);
                    float angle=(((uint)StableHash(district)%1001)/1000f-.5f)*1.20f*settings.districtVariation;
                    var cell=AnalysisAt(region.center);
                    if(cell!=null&&cell.slope>.025f&&settings.urbanMode!=UrbanMode.DistrictBased)
                    {
                        Vector2 contour=new Vector2(-cell.uphill.y,cell.uphill.x);
                        float heading=Mathf.Atan2(contour.y,contour.x);
                        heading=Mathf.Repeat(heading+Mathf.PI*.5f,Mathf.PI)-Mathf.PI*.5f;
                        angle=Mathf.Lerp(angle,heading,settings.contourPreference);
                    }
                    Vector2 u=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)),v=new Vector2(-u.y,u.x);
                    float spacing=Mathf.Clamp(local/Mathf.Max(.60f,style.density),55f,local*1.65f);
                    float cross=spacing*(cell!=null&&cell.slope>.05f?1.28f:1f);
                    int count=Mathf.CeilToInt(region.size.magnitude/spacing)+1;
                    var interior=new Dictionary<Vector2Int,Node>();
                    for(int i=-count;i<=count&&nodes.Count<budget;i++)
                    for(int j=-count;j<=count&&nodes.Count<budget;j++)
                    {
                        Vector2 p=region.center+u*(i*spacing)+v*(j*cross);
                        if(!Contains(region,p,-margin)||!Contains(bounds,p,-margin)||Reserved(p,RoadClearance)||
                            IsProtected(p,RoadClearance)||obstacles.Near(p,RoadClearance*2f)||
                            nodes.Any(n=>(XZ(n.position)-p).sqrMagnitude<margin*margin)||
                            !plan.layout.TryHeight(p,out _))continue;
                        var n=AddLandNode(p,sector);nodes.Add(n);interior[new Vector2Int(i,j)]=n;total++;
                    }
                    if(interior.Count==0)continue;
                    blocks++;
                    foreach(var pair in interior)
                    {
                        foreach(var offset in new[]{Vector2Int.right,Vector2Int.up})
                            if(interior.TryGetValue(pair.Key+offset,out Node other))add(pair.Value,other,RoadKind.Local);
                        // In steeper neighborhoods add feasible contour-directed diagonal
                        // options. Geometry checks prevent overlapping junction approaches.
                        if(cell!=null&&cell.slope>.04f&&settings.urbanMode!=UrbanMode.DistrictBased)
                            foreach(var offset in new[]{new Vector2Int(1,1),new Vector2Int(1,-1)})
                                if(interior.TryGetValue(pair.Key+offset,out Node diagonal))add(pair.Value,diagonal,RoadKind.Local);
                        Node n=pair.Value;
                        foreach(Node b in boundary.Where(b=>Contains(block,XZ(b.position),local*.35f))
                            .OrderBy(b=>Vector2.Distance(XZ(b.position),XZ(n.position))+
                                Mathf.Abs(b.position.y-n.position.y)*3f).Take(3))
                            add(n,b,RoadKind.Local);
                    }
                }
            }
            plan.notes.Add(sector+": "+blocks+" independent neighborhood subregions, "+total+" local nodes inside major blocks.");
        }

        private readonly Dictionary<AccessAnchor,Edge> manualProposals=new Dictionary<AccessAnchor,Edge>();

        private void ProposeManualAnchors(string sector,Rect bounds,List<Node> targets,StreetIndex index)
        {
            foreach(var anchor in settings.accessAnchors)
            {
                if(anchor.road==null||!manualRoads.Contains(anchor.road)||!anchor.road.IsUsable)continue;
                Vector3 position=anchor.road.GetPoint(Mathf.Clamp01(anchor.t));
                if(!Contains(bounds,XZ(position)))continue;
                if(manualProposals.TryGetValue(anchor,out Edge prior)&&plan.authoredLinks.Contains(prior))continue;
                Vector2 tangent=XZ(anchor.road.GetTangent(anchor.t)).normalized;
                foreach(Node n in targets.Where(n=>n.degree<4 && !plan.authoredLinks.Any(e=>e.b==n))
                    .OrderBy(n=>Vector2.Distance(XZ(n.position),XZ(position))+Mathf.Abs(n.position.y-position.y)*3f).Take(80))
                {
                    Vector2 d=XZ(n.position)-XZ(position);
                    if(d.magnitude<RoadClearance*2.5f||d.magnitude>Mathf.Max(550f,settings.spacing*5f))continue;
                    float angle=Vector2.Angle(tangent,d);
                    if(anchor.t>.015f&&anchor.t<.985f&&(angle<40f||angle>140f))continue;
                    if(plan.authoredLinks.Any(e=>Vector2.Distance(XZ(e.a.position),XZ(position))<RoadClearance*2.5f))continue;
                    Node start=new Node{id=-1,position=position,sector=sector};
                    if(!TryStreet(start,n,bounds,RoadKind.AuthoredLink,index,true,out List<Vector3> path))continue;
                    var proposal=new Edge{a=start,b=n,points=path,kind=RoadKind.AuthoredLink,sector=sector};
                    plan.authoredLinks.Add(proposal);index.Add(proposal);manualProposals[anchor]=proposal;break;
                }
            }
        }

        private void FinishUrbanPlan()
        {
            plan.version=settings.qualityDesign?"V7":"V6";
            if(settings.qualityDesign) CleanAndBalanceRoads();
            BuildAnalysisLines();
            BuildEarthworkLines();
            BuildParcels();
            if(settings.qualityDesign) { EvaluateOpenSpaceAccess(); BuildGradingZones(); }
            ValidateUrbanPlan();
            foreach(var anchor in settings.accessAnchors)
            {
                Vector3 pos=anchor.road!=null?anchor.road.GetPoint(Mathf.Clamp01(anchor.t)):Vector3.zero;
                bool ok=manualProposals.TryGetValue(anchor,out Edge e)&&plan.authoredLinks.Contains(e)&&e.b.degree>0;
                plan.accessResults.Add(new AnchorResult{anchor=anchor,position=pos,connected=ok,
                    message=ok?"Feasible proposal attached to retained network; not baked.":"No feasible protected/grade-safe route to retained network."});
            }
            if(plan.accessResults.Any(a=>!a.connected))
                plan.warnings.Add("Manual anchors: "+plan.accessResults.Count(a=>a.connected)+"/"+plan.accessResults.Count+
                    " feasible. Unresolved anchors remain visible in red; no authored roads were modified.");
            plan.notes.Add("Parcel preview: "+plan.parcels.Count+" sampled building lots (cap 3000 total); "+plan.openSpaces.Count+" park/plaza reservations. No buildings placed.");
            foreach(string sector in new[]{"Ankara","İstanbul"})
                if(plan.edges.Any(e=>e.sector==sector))plan.notes.Add(sector+" earthworks: "+EarthworkSummary(plan,sector,settings)+".");
        }

        private void ValidateUrbanPlan()
        {
            foreach(var edge in plan.edges.Concat(plan.authoredLinks))
                if(PathHitsReservation(edge.points))
                    throw new InvalidOperationException("Road corridor crosses a reserved open space; V6 candidate rejected.");
            foreach(var lot in plan.parcels)
            {
                if(plan.openSpaces.Any(a=>a.bounds.Overlaps(lot.bounds)))
                    throw new InvalidOperationException("Building lot overlaps a reserved open space.");
                if(protectedAreas.Any(a=>a.Overlaps(lot.bounds)))
                    throw new InvalidOperationException("Building lot overlaps protected district.");
            }
            if(plan.edges.Any(e=>e.points.Count<2 || e.points[0]!=e.a.position || e.points[e.points.Count-1]!=e.b.position))
                throw new InvalidOperationException("V6 graph has an invalid path endpoint.");
        }

        private void BuildAnalysisLines()
        {
            // Marching squares sampled from each tile; seams are deliberately separate.
            // Bounded spacing and contour interval keep the overlay tractable.
            foreach(Tile tile in plan.layout.tiles.Where(t=>t.row!=3))
            {
                int nx=Mathf.Clamp(Mathf.CeilToInt(tile.area.width/55f),4,40);
                int nz=Mathf.Clamp(Mathf.CeilToInt(tile.area.height/55f),4,40);
                var grid=new Vector3[nx+1,nz+1];
                for(int x=0;x<=nx;x++)
                for(int z=0;z<=nz;z++)
                {
                    Vector2 p=new Vector2(Mathf.Lerp(tile.area.xMin,tile.area.xMax,x/(float)nx),
                        Mathf.Lerp(tile.area.yMin,tile.area.yMax,z/(float)nz));
                    grid[x,z]=XYZ(p,tile.Height(p));
                }
                for(int x=0;x<nx;x++)
                for(int z=0;z<nz;z++)
                {
                    var q=new[]{grid[x,z],grid[x+1,z],grid[x+1,z+1],grid[x,z+1]};
                    float low=q.Min(p=>p.y),high=q.Max(p=>p.y);
                    const float interval=10f;
                    if(high-low<.001f)continue;
                    for(float h=Mathf.Ceil(low/interval)*interval;h<high&&h-low<200f;h+=interval)
                    {
                        var cross=new List<Vector3>(4);
                        for(int j=0;j<4;j++)
                        {
                            Vector3 a=q[j],b=q[(j+1)%4];
                            if((a.y<h)==(b.y<h))continue;
                            cross.Add(Vector3.Lerp(a,b,(h-a.y)/(b.y-a.y))+Vector3.up*.8f);
                        }
                        for(int j=1;j<cross.Count;j+=2)
                            plan.contours.Add(new AnalysisLine{a=cross[j-1],b=cross[j],value=h});
                    }
                }
            }
            foreach(Tile a in plan.layout.tiles.Where(t=>t.row!=3))
            foreach(Tile b in plan.layout.tiles.Where(t=>t.row!=3))
            {
                bool row=b.row==a.row+1&&b.column==a.column;
                bool col=b.column==a.column+1&&b.row==a.row;
                if(!row&&!col)continue;
                bool x=row?plan.layout.rowsOnX:!plan.layout.rowsOnX;
                Vector3 prev=default;float prevDiff=0;
                for(int i=0;i<=24;i++)
                {
                    float t=i/24f;
                    Vector2 p=x?new Vector2(a.area.xMax,Mathf.Lerp(a.area.yMin,a.area.yMax,t)):
                        new Vector2(Mathf.Lerp(a.area.xMin,a.area.xMax,t),a.area.yMax);
                    Vector2 q=x?new Vector2(b.area.xMin,Mathf.Lerp(b.area.yMin,b.area.yMax,t)):
                        new Vector2(Mathf.Lerp(b.area.xMin,b.area.xMax,t),b.area.yMin);
                    float y=a.Height(p),other=b.Height(q),diff=Mathf.Abs(y-other);
                    Vector3 point=XYZ(p,Mathf.Max(y,other)+1.5f);
                    if(i>0)plan.seams.Add(new AnalysisLine{a=prev,b=point,value=Mathf.Max(diff,prevDiff)});
                    prev=point;prevDiff=diff;
                }
            }
        }

        private void BuildEarthworkLines()
        {
            foreach(var edge in plan.edges.Where(e=>!e.Bridge))
            for(int i=1;i<edge.points.Count;i++)
            {
                Vector3 a=edge.points[i-1],b=edge.points[i],mid=(a+b)*.5f;
                Vector2 d=XZ(b-a).normalized,side=new Vector2(-d.y,d.x)*(settings.width*.5f+settings.sidewalk);
                float worst=0f;
                for(int j=-1;j<=1;j++)
                {
                    if(!plan.layout.TryHeight(XZ(mid)+side*j,out float ground))continue;
                    float diff=mid.y-.12f-ground;
                    if(Mathf.Abs(diff)>Mathf.Abs(worst))worst=diff;
                }
                if(Mathf.Abs(worst)>.5f)plan.earthworks.Add(new AnalysisLine{a=a+Vector3.up,b=b+Vector3.up,value=worst});
            }
        }

        private void BuildParcels()
        {
            var roadIndex=new StreetIndex();
            foreach(Edge e in plan.edges)roadIndex.Add(e);
            foreach(Edge e in plan.authoredLinks)roadIndex.Add(e);
            var occupied=new Dictionary<Vector2Int,List<Rect>>();
            const float cellSize=40f;
            IEnumerable<Vector2Int> Cells(Rect r)
            {
                for(int x=Mathf.FloorToInt(r.xMin/cellSize);x<=Mathf.FloorToInt(r.xMax/cellSize);x++)
                for(int z=Mathf.FloorToInt(r.yMin/cellSize);z<=Mathf.FloorToInt(r.yMax/cellSize);z++)
                    yield return new Vector2Int(x,z);
            }
            var counts=new Dictionary<string,int>();
            var catalog=settings.qualityDesign?RoadsideBuildingPlacementTool.ReadPreviewFootprints():null;
            if(settings.qualityDesign)plan.notes.Add("Prefab footprint catalog: "+catalog.Count+" usable measured models (unmatched lots are not counted as verified prefab fits).");
            int perCity=settings.buildAnkara&&settings.buildIstanbul?1500:3000;
            int processed=0;
            foreach(var e in plan.edges.Where(e=>!e.Bridge).OrderBy(e=>e.kind==RoadKind.Local?0:1).ThenBy(e=>e.a.id))
            {
                if(++processed%120==0&&EditorUtility.DisplayCancelableProgressBar("Procedural World","Screening roadside lots ("+processed+" roads)",.82f))
                    throw new OperationCanceledException();
                if(plan.parcels.Count>=3000)break;
                counts.TryGetValue(e.sector,out int sectorCount);
                if(sectorCount>=perCity)continue;
                Rect city=e.sector=="Ankara"?plan.layout.Ankara(settings.ankaraLow):plan.layout.Istanbul(settings.ankaraLow);
                float distance=0f,next=24f;
                for(int i=1;i<e.points.Count&&plan.parcels.Count<3000;i++)
                {
                    Vector3 a=e.points[i-1],b=e.points[i];float len=Vector2.Distance(XZ(a),XZ(b));
                    if(len<.001f)continue;
                    while(next<=distance+len)
                    {
                        Vector2 p=XZ(Vector3.Lerp(a,b,(next-distance)/len));next+=32f;
                        if(Vector2.Distance(p,XZ(e.a.position))<25f||Vector2.Distance(p,XZ(e.b.position))<25f)continue;
                        Vector2 forward=XZ(b-a).normalized, right=new Vector2(-forward.y,forward.x);
                        foreach(int sign in new[]{-1,1})
                        {
                            if(sectorCount>=perCity)break;
                            float halfWidth=10f,depth=22f;
                            if(StreetStyle(p).density<.65f){halfWidth=14f;depth=30f;}
                            (string name,Vector2 size) model=catalog==null?default:catalog.Where(m=>m.size.x+2f<=halfWidth*2f && m.size.y+2f<=depth)
                                .OrderByDescending(m=>m.size.x*m.size.y).FirstOrDefault();
                            if(catalog!=null && catalog.Count>0 && string.IsNullOrEmpty(model.name))continue;
                            if(!string.IsNullOrEmpty(model.name)){halfWidth=Mathf.Max(3f,model.size.x*.5f+1f);depth=Mathf.Max(6f,model.size.y+2f);}
                            Vector2 front=p+right*sign*(RoadClearance+4f);
                            Vector2 back=front+right*sign*depth;
                            var corners=new[]{front-forward*halfWidth,front+forward*halfWidth,
                                back+forward*halfWidth,back-forward*halfWidth};
                            Rect box=Rect.MinMaxRect(corners.Min(v=>v.x),corners.Min(v=>v.y),corners.Max(v=>v.x),corners.Max(v=>v.y));
                            if(corners.Any(q=>!Contains(city,q,0f))||plan.openSpaces.Any(r=>r.bounds.Overlaps(box))||protectedAreas.Any(r=>r.Overlaps(box)))continue;
                            if(Cells(box).Any(k=>occupied.TryGetValue(k,out List<Rect> list)&&list.Any(r=>r.Overlaps(box))))continue;
                            var samples=corners.Concat(new[]{(front+back)*.5f,(corners[0]+corners[1])*.5f,
                                (corners[2]+corners[3])*.5f,(corners[1]+corners[2])*.5f,(corners[0]+corners[3])*.5f}).ToList();
                            float lo=float.PositiveInfinity,hi=float.NegativeInfinity;bool ok=true;
                            var nearby=roadIndex.Query(box);
                            foreach(Vector2 q in samples)
                            {
                                if(obstacles.Near(q,RoadClearance)||!plan.layout.TryHeight(q,out float y)){ok=false;break;}
                                lo=Mathf.Min(lo,y);hi=Mathf.Max(hi,y);
                                foreach(Edge other in nearby)
                                {
                                    for(int k=1;k<other.points.Count;k++)
                                        if(DistanceSquared(q,XZ(other.points[k-1]),XZ(other.points[k]))<RoadClearance*RoadClearance){ok=false;break;}
                                    if(!ok)break;
                                }
                                if(!ok)break;
                            }
                            if(!ok||hi-lo>3f)continue;
                            float access=0f;
                            if(settings.qualityDesign)
                            {
                                float roadY=Mathf.Lerp(a.y,b.y,Mathf.Clamp01(Vector2.Distance(XZ(a),p)/len));
                                if(!plan.layout.TryHeight(front,out float frontY))continue;
                                access=Mathf.Abs(frontY-roadY)/Mathf.Max(1f,Vector2.Distance(front,p));
                                if(access>settings.parcelAccessGrade)continue;
                            }
                            // Conservative bounding-box clearance test: any road sample
                            // inside the inflated lot rejects it, even between lot probes.
                            if(roadIndex.Query(Rect.MinMaxRect(box.xMin-RoadClearance,box.yMin-RoadClearance,
                                box.xMax+RoadClearance,box.yMax+RoadClearance)).Any(r=>r.points.Any(q=>Contains(box,XZ(q),RoadClearance))))continue;
                            var lot=new UrbanArea{kind=AreaKind.BuildingLot,sector=e.sector,district=DistrictName(p,e.sector),
                                bounds=box,corners=corners.Select(q=>{plan.layout.TryHeight(q,out float y);return XYZ(q,y+.5f);}).ToArray(),
                                accessible=settings.qualityDesign, prefab=model.name,frontage=halfWidth*2f,depth=depth,foundationRelief=hi-lo,accessGrade=access};
                            plan.parcels.Add(lot);sectorCount++;counts[e.sector]=sectorCount;
                            foreach(var k in Cells(box))
                            {if(!occupied.TryGetValue(k,out List<Rect> list))occupied[k]=list=new List<Rect>();list.Add(box);}
                        }
                    }
                    distance+=len;
                }
            }
        }
    }
}
