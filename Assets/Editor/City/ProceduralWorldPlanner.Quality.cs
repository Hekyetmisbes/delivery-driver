using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryDriver.EditorTools
{
    internal sealed partial class ProceduralWorldPlanner
    {
        [Serializable]
        internal sealed class QualityReport
        {
            public float score, connectivity, terrainFit, geometry, parcels, bridges, parks;
            public float meanCut, meanFill, peak, extraFraction, roadKm, arterialKm, collectorKm, localKm, bridgeGap;
            public int ankaraTiles, istanbulTiles, sharpRoads, shortStubs, matchedLots, accessibleParks, totalParks;
            public bool bridgeGraphConnected;
        }

        private float profileCap=float.PositiveInfinity;
        private List<List<Node>> priorityTargets;

        // V6 remains a real comparator, not a label on a V7 graph. To avoid running
        // three nested guards per candidate, compare with the same-seed V6 raw planner.
        // If it cannot produce a plan, invoke its original fallback workflow.
        internal static Plan Build(Settings settings,Layout layout,Scene scene)
        {
            layout.rawTerrainCache.Clear();
            if(!settings.qualityDesign||!settings.urbanDesign)
            {
                bool old=settings.qualityDesign;settings.qualityDesign=false;
                try{return BuildV6(settings,layout,scene);}
                finally{settings.qualityDesign=old;}
            }
            Plan candidate=null,baseline=null;
            InvalidOperationException error=null;
            try {candidate=BuildOnce(settings,layout,scene);}
            catch(InvalidOperationException ex){error=ex;}
            if(settings.qualityGuard)
            {
                settings.qualityDesign=false;
                try
                {
                    try{baseline=BuildOnce(settings,layout,scene);}
                    catch(InvalidOperationException){baseline=BuildV6(settings,layout,scene);}
                }
                catch(InvalidOperationException){if(candidate==null)throw;}
                finally{settings.qualityDesign=true;}
            }
            if(candidate==null&&baseline==null)throw error??new InvalidOperationException("V7 did not produce a plan.");
            if(candidate!=null)candidate.quality=Measure(candidate,settings);
            if(baseline!=null)baseline.quality=Measure(baseline,settings);
            var regressions=new List<string>();
            if(candidate==null)regressions.Add(error?.Message??"candidate failed");
            if(candidate!=null&&baseline!=null)
            {
                var a=candidate.quality;var b=baseline.quality;
                if(a.ankaraTiles<b.ankaraTiles||a.istanbulTiles<b.istanbulTiles)regressions.Add("tile coverage");
                if(candidate.BridgeCount<baseline.BridgeCount||(b.bridgeGraphConnected&&!a.bridgeGraphConnected))regressions.Add("bridge connectivity");
                if(candidate.authoredLinks.Count<baseline.authoredLinks.Count ||
                    candidate.accessResults.Count(x=>x.connected)<baseline.accessResults.Count(x=>x.connected))regressions.Add("existing-road access");
                if(candidate.detachedEdges.Count>baseline.detachedEdges.Count)regressions.Add("detached streets");
                if(a.roadKm<b.roadKm*.75f)regressions.Add("network length");
                if(a.bridgeGap+1f<b.bridgeGap)regressions.Add("bridge separation");
                if(a.extraFraction>b.extraFraction+.01f||a.peak>b.peak+.25f||
                    a.meanCut+a.meanFill>b.meanCut+b.meanFill+.10f)regressions.Add("earthworks");
            }
            string compare=candidate!=null&&baseline!=null?
                "V6 / V7 Ankara sampled grading: "+EarthworkSummary(baseline,"Ankara",settings)+" / "+EarthworkSummary(candidate,"Ankara",settings)+
                ". Baseline parcel/park fit was not screened with V7 rules; quality scores are not directly comparable.":null;
            Plan selected=baseline!=null&&regressions.Count>0?baseline:candidate;
            if(selected==null)throw error??new InvalidOperationException("No quality preview.");
            if(selected==baseline)
            {
                selected.version=selected.version.StartsWith("V6")?"V6 fallback":selected.version+" (V7 fallback)";
                selected.warnings.Add("Fallback uses baseline road geometry and unscreened V6 parcel/park data; V7 score is diagnostic only.");
                selected.warnings.Insert(0,"V7 guard rejected candidate: "+string.Join(", ",regressions)+". Disable Preserve V6 quality to inspect V7.");
            }
            else selected.notes.Insert(0,baseline!=null?"V7 passed V6 quality guard (same-seed raw V6 comparison).":"V7 candidate without baseline comparison.");
            if(compare!=null)selected.notes.Insert(0,compare);
            AddQualityNotes(selected);
            return selected;
        }

        private static float Length(List<Vector3> p)
        {
            float total=0;
            for(int i=1;i<p.Count;i++)total+=Vector2.Distance(XZ(p[i]),XZ(p[i-1]));
            return total;
        }

        // Radius estimate from three planar samples; straight sections have infinite radius.
        internal static float TurnRadius(Vector2 a,Vector2 b,Vector2 c)
        {
            float ab=Vector2.Distance(a,b),bc=Vector2.Distance(b,c),ac=Vector2.Distance(a,c);
            float cross=Mathf.Abs((b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x));
            if(ab<.001f||bc<.001f)return 0f;
            if(Vector2.Dot((b-a).normalized,(c-b).normalized)<-.98f)return 0f;
            if(cross<.0001f)return float.PositiveInfinity;
            return ab*bc*ac/(2f*cross);
        }
        private static float MinimumRadius(List<Vector3> p)
        {
            float r=float.PositiveInfinity;
            for(int i=1;i<p.Count-1;i++)r=Mathf.Min(r,TurnRadius(XZ(p[i-1]),XZ(p[i]),XZ(p[i+1])));
            return r;
        }
        private bool RadiusOk(List<Vector3> p,RoadKind kind) =>
            MinimumRadius(p)>=settings.minTurnRadius*(kind==RoadKind.Arterial?1.5f:kind==RoadKind.Collector?1.2f:1f);
        private static List<Vector2> RoundRoute(List<Vector2> source)
        {
            var p=source;
            for(int pass=0;pass<2;pass++)
            {
                var next=new List<Vector2>{p[0]};
                for(int i=1;i<p.Count;i++)
                {
                    next.Add(Vector2.Lerp(p[i-1],p[i],.25f));
                    next.Add(Vector2.Lerp(p[i-1],p[i],.75f));
                }
                next.Add(p[p.Count-1]);p=next;
            }
            return p;
        }
        private bool RouteClear(List<Vector3> path,Rect bounds)
        {
            return path.All(v=>Contains(bounds,XZ(v),-RoadClearance)&&!IsProtected(XZ(v),RoadClearance)&&
                !obstacles.Near(XZ(v),RoadClearance*2f)&&!Reserved(XZ(v),RoadClearance));
        }
        private float QualityPathCost(List<Vector3> p)
        {
            float sum=0,max=0;int count=0;
            for(int i=1;i<p.Count;i+=2)
            {
                Vector2 d=XZ(p[i]-p[i-1]).normalized,side=new Vector2(-d.y,d.x)*(settings.width*.5f+settings.sidewalk);
                for(int j=-1;j<=1;j++)
                {
                    if(!plan.layout.TryHeight(XZ(p[i])+side*j,out float h))continue;
                    float v=Mathf.Abs(p[i].y-.12f-h);
                    sum+=v+Mathf.Max(0f,v-settings.maxCutFill)*2f+Mathf.Max(0f,v-settings.preferredGrading)*8f;
                    max=Mathf.Max(max,v);count++;
                }
            }
            return sum/Mathf.Max(1,count)+max*.45f;
        }

        // Final focused repair, before the diagnostic components are removed.
        // Includes disconnected authored targets and isolated tile-center candidates.
        private void RepairPriorityTargets(string sector,Rect bounds,float local,List<Node> nodes,List<Node> gates,Node root,
            Func<Node,Node> find,Func<StreetCandidate,bool,bool> build)
        {
            root=root??gates.FirstOrDefault()??nodes.Where(n=>n.degree>0).GroupBy(find)
                .OrderByDescending(g=>g.Count()).Select(g=>g.First()).FirstOrDefault();
            if(root==null)return;
            var targets=new List<List<Node>>();
            if(priorityTargets!=null)targets.AddRange(priorityTargets);
            foreach(var fragment in nodes.Where(n=>n.degree>0&&find(n)!=find(root)).GroupBy(find).OrderByDescending(g=>g.Count()))
                targets.Add(fragment.ToList());
            int connected=0,routes=0,direct=0;
            foreach(var target in targets)
            {
                if(target.Any(n=>find(n)==find(root)&&n.degree>0)){connected++;continue;}
                var sources=nodes.Where(n=>find(n)==find(root)&&n.degree<DegreeLimit(n)).ToList();
                var options=new List<StreetCandidate>();
                foreach(Node b in target.Where(n=>n.degree<DegreeLimit(n)).Take(80))
                    foreach(Node a in sources.OrderBy(n=>(XZ(n.position)-XZ(b.position)).sqrMagnitude).Take(8))
                    {
                        float len=Vector2.Distance(XZ(a.position),XZ(b.position));
                        if(len<RoadClearance*2.5f||len>Mathf.Max(650f,local*8f))continue;
                        options.Add(new StreetCandidate{a=a,b=b,kind=RoadKind.Collector,length=len,spine=true});
                    }
                foreach(var c in options.OrderBy(c=>TerrainCandidateCost(c)+Mathf.Abs(c.a.position.y-c.b.position.y)*4f).Take(24))
                {
                    if(++direct>360)break;
                    if(build(c,false))break;
                    if(routes<18&&settings.terrainRepair&&++routes>0&&build(c,true))break;
                }
                if(target.Any(n=>find(n)==find(root)&&n.degree>0))connected++;
                if(direct>360)break;
            }
            plan.notes.Add(sector+": final priority repair reached "+connected+"/"+targets.Count+" targets/groups; "+direct+" direct and "+routes+" routed attempts.");
            if(priorityTargets!=null)
            {
                int reached=priorityTargets.Count(g=>g.Any(n=>find(n)==find(root)&&n.degree>0));
                plan.notes.Add("Ankara final backbone targets: "+reached+"/"+priorityTargets.Count+".");
                if(reached<priorityTargets.Count)plan.warnings.Add("Ankara: "+(priorityTargets.Count-reached)+" priority targets remain unresolved after final repair.");
            }
        }

        private void CleanAndBalanceRoads()
        {
            int pruned=0;
            foreach(var e in plan.edges.Where(e=>!e.Bridge&&e.kind==RoadKind.Local&&!e.spine).ToList())
            {
                if(Length(e.points)>=settings.minLocalStub)continue;
                Node leaf=e.a.degree==1?e.a:e.b.degree==1?e.b:null;
                if(leaf==null||leaf.gate||plan.authoredLinks.Any(l=>l.b==leaf))continue;
                // Do not lose any covered tile or a park frontage just to improve a score.
                if(plan.openSpaces.Any(s=>s.sector==e.sector&&Contains(s.bounds,XZ(leaf.position),60f)))continue;
                if(plan.layout.tiles.Any(t=>e.points.Any(v=>t.area.Contains(XZ(v)))&&
                    !plan.edges.Any(other=>other!=e&&other.sector==e.sector&&other.points.Any(v=>t.area.Contains(XZ(v))))))continue;
                plan.edges.Remove(e);e.a.degree--;e.b.degree--;pruned++;
            }
            foreach(string sector in new[]{"Ankara","İstanbul"})
            {
                var roads=plan.edges.Where(e=>e.sector==sector).ToList();
                var lengths=roads.ToDictionary(e=>e,e=>Length(e.points));
                float total=lengths.Values.Sum();
                float arterial=roads.Where(e=>e.kind==RoadKind.Arterial).Sum(e=>lengths[e]);
                float collector=roads.Where(e=>e.kind==RoadKind.Collector).Sum(e=>lengths[e]);
                // Preserve gate/backbone arterials. Downgrade only complete non-spine
                // chains of the same class between junctions, not individual fragments.
                foreach(RoadKind kind in new[]{RoadKind.Arterial,RoadKind.Collector})
                {
                    var eligible=roads.Where(e=>e.kind==kind&&!e.spine&&!e.a.gate&&!e.b.gate).ToList();
                    var seen=new HashSet<Edge>();
                    var chains=new List<List<Edge>>();
                    var adj=eligible.SelectMany(e=>new[]{(node:e.a,edge:e),(node:e.b,edge:e)})
                        .GroupBy(x=>x.node).ToDictionary(g=>g.Key,g=>g.Select(x=>x.edge).ToList());
                    foreach(var seed in eligible)
                    {
                        if(!seen.Add(seed))continue;
                        var chain=new List<Edge>{seed};var queue=new Queue<Edge>();queue.Enqueue(seed);
                        while(queue.Count>0)
                        {
                            var e=queue.Dequeue();
                            foreach(Node n in new[]{e.a,e.b})
                                if(adj[n].Count==2&&n.degree==2)
                                    foreach(var next in adj[n])if(seen.Add(next)){chain.Add(next);queue.Enqueue(next);}
                        }
                        chains.Add(chain);
                    }
                    foreach(var chain in chains.OrderBy(c=>c.Sum(e=>lengths[e])))
                    {
                        float len=chain.Sum(e=>lengths[e]);
                        if(kind==RoadKind.Arterial)
                        {
                            if(arterial-len<total*settings.arterialLengthTarget)continue;
                            foreach(var e in chain)e.kind=RoadKind.Collector;
                            arterial-=len;collector+=len;
                        }
                        else
                        {
                            if(collector-len<total*settings.collectorLengthTarget)continue;
                            foreach(var e in chain)e.kind=RoadKind.Local;
                            collector-=len;
                        }
                    }
                }
            }
            plan.notes.Add("V7 geometry cleanup: "+pruned+" short non-essential local stubs removed; road class targets are by length, backbone protected.");
        }

        private void BuildGradingZones()
        {
            plan.gradingZones.Clear();
            foreach(var line in plan.earthworks)
            {
                float magnitude=Mathf.Abs(line.value);
                int band=magnitude<=3f?0:magnitude<=settings.maxCutFill?1:magnitude<=10f?2:magnitude<=settings.preferredGrading?3:4;
                plan.gradingZones.Add(new AnalysisLine{a=line.a,b=line.b,value=band});
            }
        }

        private void EvaluateOpenSpaceAccess()
        {
            var index=new StreetIndex();
            foreach(var e in plan.edges.Where(e=>!e.Bridge))index.Add(e);
            foreach(var space in plan.openSpaces)
            {
                space.accessible=false;
                Rect r=space.bounds;const float reach=55f;
                Rect query=Rect.MinMaxRect(r.xMin-reach,r.yMin-reach,r.xMax+reach,r.yMax+reach);
                foreach(var e in index.Query(query).Where(e=>e.sector==space.sector))
                {
                    foreach(Vector3 point in e.points)
                    {
                        Vector2 p=XZ(point),q=new Vector2(Mathf.Clamp(p.x,r.xMin,r.xMax),Mathf.Clamp(p.y,r.yMin,r.yMax));
                        float d=Vector2.Distance(p,q);
                        if(d<RoadClearance||d>reach||!plan.layout.TryHeight(q,out float h))continue;
                        if(Mathf.Abs(h-point.y)/d>settings.parcelAccessGrade)continue;
                        bool clear=true;
                        for(int i=1;i<=4;i++)
                        {
                            Vector2 mid=Vector2.Lerp(p,q,i/4f);
                            if(IsProtected(mid,2f)||obstacles.Near(mid,2f)){clear=false;break;}
                        }
                        if(clear){space.accessible=true;space.accessGrade=Mathf.Abs(h-point.y)/d;break;}
                    }
                    if(space.accessible)break;
                }
            }
            int inaccessible=plan.openSpaces.Count(s=>!s.accessible);
            if(inaccessible>0)plan.warnings.Add("Open spaces without screened access: "+inaccessible+" (amber overlay). Access is a sampled pedestrian/driveway feasibility proxy, not a built path.");
        }

        internal static QualityReport Measure(Plan p,Settings s)
        {
            var q=new QualityReport();
            q.ankaraTiles=Coverage(p,"Ankara");q.istanbulTiles=Coverage(p,"İstanbul");
            q.bridgeGraphConnected=GatesConnected(p) && (!s.bridges||!s.buildAnkara||!s.buildIstanbul||p.BridgeCount==2);
            double cut=0,fill=0,extra=0,total=0;
            int nonBridge=0;
            foreach(var e in p.edges)
            {
                float len=Length(e.points);q.roadKm+=len/1000f;
                if(e.Bridge)continue;
                nonBridge++;
                if(e.kind==RoadKind.Arterial)q.arterialKm+=len/1000f;
                if(e.kind==RoadKind.Collector)q.collectorKm+=len/1000f;
                if(e.kind==RoadKind.Local)q.localKm+=len/1000f;
                if(MinimumRadius(e.points)<s.minTurnRadius*(e.kind==RoadKind.Arterial?1.5f:e.kind==RoadKind.Collector?1.2f:1f))q.sharpRoads++;
                if(e.kind==RoadKind.Local&&len<s.minLocalStub&&(e.a.degree==1||e.b.degree==1))q.shortStubs++;
                if(e.sector!="Ankara")continue;
                for(int i=1;i<e.points.Count;i++)
                {
                    Vector3 a=e.points[i-1],b=e.points[i],m=(a+b)*.5f;
                    float d=Vector2.Distance(XZ(a),XZ(b));if(d<.001f)continue;
                    Vector2 dir=XZ(b-a).normalized,side=new Vector2(-dir.y,dir.x)*(s.width*.5f+s.sidewalk);
                    float worst=0;
                    for(int j=-1;j<=1;j++)
                        if(p.layout.TryHeight(XZ(m)+side*j,out float h))
                        {
                            float v=m.y-.12f-h;cut+=Mathf.Max(0,-v)*d/3;fill+=Mathf.Max(0,v)*d/3;worst=Mathf.Max(worst,Mathf.Abs(v));
                        }
                    q.peak=Mathf.Max(q.peak,worst);total+=d;if(worst>s.maxCutFill+.01f)extra+=d;
                }
            }
            q.meanCut=(float)(cut/Math.Max(1,total));q.meanFill=(float)(fill/Math.Max(1,total));q.extraFraction=(float)(extra/Math.Max(1,total));
            var bridges=p.edges.Where(e=>e.Bridge).ToList();
            float span=p.layout.AcrossMax(p.layout.sea)-p.layout.AcrossMin(p.layout.sea);
            if(bridges.Count==2)
            {
                Func<Vector3,float> across=v=>p.layout.rowsOnX?v.z:v.x;
                q.bridgeGap=Mathf.Max(0,Mathf.Max(bridges[0].points.Min(across)-bridges[1].points.Max(across),
                    bridges[1].points.Min(across)-bridges[0].points.Max(across)));
            }
            q.matchedLots=p.parcels.Count(l=>l.accessible&&!string.IsNullOrEmpty(l.prefab));q.accessibleParks=p.openSpaces.Count(l=>l.accessible);q.totalParks=p.openSpaces.Count;
            int tiles=(s.buildAnkara?12:0)+(s.buildIstanbul?12:0);
            q.connectivity=100f*Mathf.Clamp01((q.ankaraTiles+q.istanbulTiles)/(float)Mathf.Max(1,tiles))*
                (1f-p.detachedEdges.Count/(float)Mathf.Max(1,p.edges.Count+p.detachedEdges.Count));
            if(!q.bridgeGraphConnected)q.connectivity*=.5f;
            q.terrainFit=100f*Mathf.Clamp01(1f-q.extraFraction*.7f-Mathf.Max(0,q.peak-s.maxCutFill)/Mathf.Max(1,s.districtGradingLimit)*.3f);
            q.geometry=100f*Mathf.Clamp01(1f-(q.sharpRoads+q.shortStubs)/(float)Mathf.Max(1,nonBridge));
            q.parcels=100f*q.matchedLots/Mathf.Max(1,p.parcels.Count);
            q.bridges=!s.bridges||!s.buildAnkara||!s.buildIstanbul?100f:bridges.Count==2?100f*Mathf.Clamp01(q.bridgeGap/Mathf.Max(1,span*s.bridgeSeparation)):0f;
            q.parks=100f*q.accessibleParks/Mathf.Max(1,q.totalParks);
            float weights=s.connectivityWeight+s.terrainWeight+s.geometryWeight+s.parcelWeight+s.bridgeWeight+s.parkWeight;
            q.score=(q.connectivity*s.connectivityWeight+q.terrainFit*s.terrainWeight+q.geometry*s.geometryWeight+
                q.parcels*s.parcelWeight+q.bridges*s.bridgeWeight+q.parks*s.parkWeight)/Mathf.Max(1,weights);
            return q;
        }
        private static void AddQualityNotes(Plan p)
        {
            QualityReport q=p.quality;
            p.notes.Insert(0,"Quality "+q.score.ToString("F1")+"/100 (heuristic; not driveability certification). Connected tiles Ankara/İstanbul "+q.ankaraTiles+"/12, "+q.istanbulTiles+"/12; sharp roads "+q.sharpRoads+", short stubs "+q.shortStubs+
                "; prefab-fit accessible lots "+q.matchedLots+"/"+p.parcels.Count+"; screened parks "+q.accessibleParks+"/"+q.totalParks+".");
            p.notes.Insert(1,"Road length (km): arterial "+q.arterialKm.ToString("F1")+", collector "+q.collectorKm.ToString("F1")+", local "+q.localKm.ToString("F1")+
                "; bridge corridor separation "+q.bridgeGap.ToString("F0")+" m.");
            if(q.sharpRoads>0)p.warnings.Add("Radius screening: "+q.sharpRoads+" retained streets below configured radius (check fallback / reclassified road class).");
        }
    }
}
