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
        internal sealed class StageView
        {
            public string label;
            public bool accepted;
            public PreviewSnapshot snapshot;
        }
        private static Plan CopyStage(Plan source,Settings s,Scene scene)
        {
            return Restore(JsonUtility.FromJson<PreviewSnapshot>(JsonUtility.ToJson(
                Capture(source,null,scene.path,DescribeSettings(s)))),source.layout,s);
        }
        private static string StageMetrics(Plan p)
        {
            var q=p.quality;
            return "tiles "+q.ankaraTiles+"/"+q.istanbulTiles+", retained/detached "+p.edges.Count+"/"+p.detachedEdges.Count+
                ", links "+p.authoredLinks.Count+", bridges "+p.BridgeCount+", bridge gap "+q.bridgeGap.ToString("F0")+" m, "+
                "cut/fill "+q.meanCut.ToString("F2")+"/"+q.meanFill.ToString("F2")+" m, peak "+q.peak.ToString("F1")+
                " m, extra "+(q.extraFraction*100f).ToString("F1")+"%, radius flags "+q.sharpRoads+", lots "+q.matchedLots+
                ", park access "+q.accessibleParks+"/"+q.totalParks+", vertical flags "+q.verticalDefects+", junction transitions "+q.junctionGradeFlags;
        }
        private static float BalancedCost(QualityReport q,Settings s)
        {
            return q.meanCut+q.meanFill+.2f*Mathf.Abs(q.meanCut-q.meanFill)+
                4f*q.extraFraction+.12f*Mathf.Max(0f,q.peak-s.preferredGrading);
        }
        private static bool NoTerrainRegression(QualityReport a,QualityReport b,Settings s)
        {
            return BalancedCost(b,s)<=BalancedCost(a,s)+.005f&&b.meanCut+b.meanFill<=a.meanCut+a.meanFill+.005f&&
                b.peak<=a.peak+.02f&&b.extraFraction<=a.extraFraction+.0005f;
        }
        internal static bool NoLocalQualityRegression(QualityReport a,QualityReport b)
        {
            return b.meanCut<=a.meanCut+.005f&&b.meanFill<=a.meanFill+.005f&&
                b.sharpRoads<=a.sharpRoads&&b.verticalSeverity<=a.verticalSeverity+.0001f&&
                b.junctionGradeFlags<=a.junctionGradeFlags;
        }
        private static string StageGraphIssue(Plan before,Plan after,Settings s,bool allowSplit=false,bool allowBridgeMove=false)
        {
            if(after.edges.Count<before.edges.Count)return "retained edge count decreased";
            if(after.detachedEdges.Count>before.detachedEdges.Count)return "detached edge count increased";
            if(after.BridgeCount!=before.BridgeCount)return "bridge count changed";
            if(before.quality.bridgeGraphConnected&&!after.quality.bridgeGraphConnected)return "bridge graph disconnected";
            if(after.quality.ankaraTiles<before.quality.ankaraTiles||after.quality.istanbulTiles<before.quality.istanbulTiles)return "city coverage decreased";
            if(after.quality.bridgeGap+.01f<before.quality.bridgeGap)return "bridge separation decreased";
            if(after.accessResults.Count(a=>a.connected)<before.accessResults.Count(a=>a.connected))return "connected manual access count decreased";
            if(after.authoredLinks.Count!=before.authoredLinks.Count||
                new[]{"Ankara","İstanbul"}.Any(city=>after.authoredLinks.Count(e=>e.sector==city)!=before.authoredLinks.Count(e=>e.sector==city)))return "authored proposal count changed";
            for(int i=0;i<before.authoredLinks.Count;i++)
                if(!before.authoredLinks[i].points.SequenceEqual(after.authoredLinks[i].points))return "authored proposal geometry changed at index "+i;
            if(allowSplit||allowBridgeMove)
            {
                string issue=NetworkIdentityIssue(before);
                if(issue!=null)return "baseline "+issue;
                issue=NetworkIdentityIssue(after);
                if(issue!=null)return "candidate "+issue;
                return PreservedNetwork(before,after,allowSplit,allowBridgeMove)?null:"original edge/split chain or bridge endpoint connection not preserved";
            }
            // Existing network edges are never removed or rewired. Compare the stable
            // original prefix; new repair edges can only be appended.
            for(int i=0;i<before.edges.Count;i++)
            {
                var a=before.edges[i];var b=after.edges[i];
                if(a.a.id!=b.a.id||a.b.id!=b.b.id||a.sector!=b.sector||
                    XZ(a.a.position)!=XZ(b.a.position)||XZ(a.b.position)!=XZ(b.b.position))return "original edge topology changed at index "+i;
            }
            return null;
        }
        private static Plan BuildStaged(Settings s,Layout layout,Scene scene)
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();
            Plan baseline;
            // Build the same raw V6 reference as the V7 comparator, not the broken V7 graph.
            s.qualityDesign=false;
            try
            {
                try{baseline=BuildOnce(s,layout,scene);}
                catch(InvalidOperationException){baseline=BuildV6(s,layout,scene);}
            }
            finally{s.qualityDesign=true;}
            baseline.quality=Measure(baseline,s);
            if((s.buildAnkara&&baseline.quality.ankaraTiles<12)||(s.buildIstanbul&&baseline.quality.istanbulTiles<12)||!baseline.quality.bridgeGraphConnected)
            {
                s.qualityDesign=false;
                try
                {
                    var guarded=BuildV6(s,layout,scene);guarded.quality=Measure(guarded,s);
                    if(guarded.quality.ankaraTiles>=baseline.quality.ankaraTiles&&guarded.quality.istanbulTiles>=baseline.quality.istanbulTiles&&
                        (!baseline.quality.bridgeGraphConnected||guarded.quality.bridgeGraphConnected)&&guarded.BridgeCount>=baseline.BridgeCount&&
                        guarded.authoredLinks.Count>=baseline.authoredLinks.Count)baseline=guarded;
                }
                finally{s.qualityDesign=true;}
            }
            var views=new List<StageView>();
            var reports=new List<string>();
            string settingsText=DescribeSettings(s);
            void SaveView(Plan value,string label,bool accepted)
            {
                var snap=Capture(value,null,scene.path,settingsText);
                // Deep-copy snapshots because the next stage edits its own geometry.
                views.Add(new StageView{label=label,accepted=accepted,snapshot=JsonUtility.FromJson<PreviewSnapshot>(JsonUtility.ToJson(snap))});
            }
            reports.Add("Stage 0 base "+baseline.version+" ("+timer.Elapsed.TotalSeconds.ToString("F1")+" s): "+StageMetrics(baseline));
            SaveView(baseline,"0 "+baseline.version+" base",true);
            Plan current=baseline;
            void Stage(string name,Action<ProceduralWorldPlanner> change,Func<Plan,Plan,bool> benefit,bool allowSplit=false,bool allowBridgeMove=false)
            {
                var clock=System.Diagnostics.Stopwatch.StartNew();
                if(EditorUtility.DisplayCancelableProgressBar("V7.1 staged optimization",name,.5f))throw new OperationCanceledException();
                var candidate=CopyStage(current,s,scene);
                var planner=new ProceduralWorldPlanner(s,layout,scene,candidate);
                string reason=null;
                int startNotes=candidate.notes.Count;
                try
                {
                    candidate.terrainCells.Clear();planner.AnalyzeTerrain();
                    candidate.manualRoadNear=planner.obstacles.Near;
                    change(planner);
                    planner.ValidateUrbanPlan();
                    planner.ValidateStageGrades();
                    candidate.quality=Measure(candidate,s);
                    string graphIssue=StageGraphIssue(current,candidate,s,allowSplit,allowBridgeMove);
                    if(graphIssue!=null)reason="network/access regression: "+graphIssue;
                    else if(!NoTerrainRegression(current.quality,candidate.quality,s))reason="earthworks regression";
                    else if(s.localOptimization&&name.StartsWith("5",StringComparison.Ordinal)&&name.Length>1&&char.IsLetter(name[1])&&
                        !NoLocalQualityRegression(current.quality,candidate.quality))reason="local quality regression";
                    else if(!benefit(current,candidate))reason="no measurable improvement";
                }
                catch(InvalidOperationException ex){reason=ex.Message;}
                candidate.quality=Measure(candidate,s);
                bool accepted=reason==null;
                string line=name+" ["+(accepted?"ACCEPTED":"ROLLED BACK: "+reason)+"] "+
                    clock.Elapsed.TotalSeconds.ToString("F1")+" s; before "+StageMetrics(current)+"; candidate "+StageMetrics(candidate);
                string details=string.Join(" | ",candidate.notes.Skip(startNotes));
                if(!string.IsNullOrEmpty(details))line+="; "+details;
                reports.Add(line);
                candidate.stageReports.Clear();candidate.stageReports.AddRange(reports);
                candidate.notes.Insert(0,line);
                // Candidate paths are preserved even when a stage rolls back. Overlays
                // reflect current paths; parcel/park accessibility is only final-stage data.
                candidate.earthworks.Clear();candidate.gradingZones.Clear();candidate.gradingMarkers.Clear();
                planner.BuildEarthworkLines();planner.BuildGradingZones();
                candidate.version=(s.localOptimization?"V7.2 ":"V7.1 ")+name+(accepted?"":" rejected (diagnostic)");
                SaveView(candidate,name+(accepted?" accepted":" rejected"),accepted);
                if(accepted)current=candidate;
            }
            bool ImprovedEarth(Plan a,Plan b)=>BalancedCost(b.quality,s)<BalancedCost(a.quality,s)-.001f;
            Stage("1 Junction/profile solve",x=>x.OptimizeSharedProfiles(s.profileSweeps,false),ImprovedEarth);
            Stage("2 Balanced earthworks",x=>x.OptimizeSharedProfiles(s.profileSweeps,true),ImprovedEarth);
            Stage("3 Detached component repair",x=>x.ReconnectDetached(),(a,b)=>b.detachedEdges.Count<a.detachedEdges.Count);
            Stage("4 Geometry refinement",x=>x.RefineExistingGeometry(),(a,b)=>b.quality.sharpRoads<a.quality.sharpRoads);
            Stage("5 Bridge approach refinement",x=>x.RefineFixedGateBridges(),(a,b)=>b.quality.bridgeGap>a.quality.bridgeGap+.01f);
            if(s.localOptimization)
            {
                Stage("5a Hotspot optimization",x=>x.RefineLocalRoads(0),(a,b)=>b.quality.peak<a.quality.peak-.001f||BalancedCost(b.quality,s)<BalancedCost(a.quality,s)-.001f);
                Stage("5b Interior T-junction repair",x=>x.RepairWithInteriorJunctions(),(a,b)=>b.detachedEdges.Count<a.detachedEdges.Count,true);
                Stage("5c Local bend smoothing",x=>x.RefineLocalRoads(1),(a,b)=>b.quality.sharpRoads<a.quality.sharpRoads);
                Stage("5d Vertical curve smoothing",x=>x.RefineLocalRoads(2),(a,b)=>b.quality.verticalDefects<a.quality.verticalDefects||b.quality.verticalSeverity<a.quality.verticalSeverity-.0001f);
                if(s.relocateBridgeGates)Stage("5e Alternative bridge gates",x=>x.RelocateBridgeGate(),(a,b)=>b.quality.bridgeGap>a.quality.bridgeGap+.01f,false,true);
            }
            Stage("6 Prefab and park screening",x=>
            {
                x.plan.parcels.Clear();x.BuildParcels();x.EvaluateOpenSpaceAccess();
                if(s.localOptimization)x.FindAlternateParkAccess();
            },(a,b)=>true);
            bool urbanScreened=views.Last().accepted;
            current.version=s.localOptimization?"V7.2 optimized":"V7.1 optimized";
            current.stageReports.Clear();current.stageReports.AddRange(reports);
            current.stageViews.AddRange(views);
            current.quality=Measure(current,s);
            bool validGrades=true;
            try{new ProceduralWorldPlanner(s,layout,scene,current).ValidateStageGrades();}
            catch(InvalidOperationException){validGrades=false;}
            current.previewCriteriaMet=validGrades&&urbanScreened&&(!s.buildAnkara||current.quality.ankaraTiles==12)&&
                (!s.buildIstanbul||current.quality.istanbulTiles==12)&&current.quality.bridgeGraphConnected&&
                (!s.protectKizilay||!s.buildAnkara||current.authoredLinks.Count(e=>e.sector=="Ankara")>=s.authoredLinkTarget)&&
                current.detachedEdges.Count==0&&current.quality.sharpRoads==0&&current.accessResults.All(a=>a.connected);
            // Historical warnings and reports are kept in checkpoints, not presented
            // as current final results.
            current.notes.Clear();current.warnings.Clear();
            current.notes.Add((s.localOptimization?"V7.2":"V7.1")+" base vs final: "+StageMetrics(baseline)+" / "+StageMetrics(current));
            current.notes.Add("Mandatory preview checks: "+(current.previewCriteriaMet?"PASS":"NOT MET")+"; Generate remains locked.");
            current.notes.Add(s.localOptimization?"Original land graph and authored link paths preserved; V7.2 may split an unchanged road or replace a bridge at existing generated nodes. No protected-zone exception. Short stubs remain diagnostics.":"Original graph and authored link paths preserved; bridge endpoints fixed. Short stubs remain diagnostics.");
            current.notes.Add(urbanScreened?"Park and lot overlays: "+current.parcels.Count+" sampled lots, "+current.quality.matchedLots+" measured prefab fits, "+current.quality.accessibleParks+"/"+current.openSpaces.Count+" screened park access.":"Urban screening rolled back; parcel/park data is historical and not verified for the final plan.");
            if(!validGrades)current.warnings.Add("Final mandatory sampled grade validation failed.");
            if(current.detachedEdges.Count>0)current.warnings.Add(current.detachedEdges.Count+" detached segments still unresolved; purple diagnostic only.");
            if(!current.previewCriteriaMet)current.warnings.Add("Mandatory preview criteria incomplete; inspect stage reports. No generation.");
            if(current.quality.bridgeGap< (layout.AcrossMax(layout.sea)-layout.AcrossMin(layout.sea))*s.bridgeSeparation && s.bridges&&s.buildAnkara&&s.buildIstanbul)
                current.warnings.Add("Requested bridge separation unavailable; no protected-area crossing exception. Authored coastal spline audit is diagnostic only.");
            var finalPlanner=new ProceduralWorldPlanner(s,layout,scene,current);
            current.gradingMarkers.Clear();finalPlanner.ReportGrading("Ankara");finalPlanner.ReportTerrainSeams(layout.Ankara(s.ankaraLow));
            if(s.localOptimization)
            {
                finalPlanner.BuildLocalDiagnostics();
                finalPlanner.BuildVirtualTerrain();
                if(current.quality.verticalDefects>0||current.quality.junctionGradeFlags>0||current.virtualReport.conflictingCells>0||current.virtualReport.protectedSamples>0||current.virtualReport.missingSections>0||current.virtualReport.missingSamples>0||current.virtualReport.parcelSamples>0||current.virtualReport.steepSections>0||current.quality.peak>s.preferredGrading+.01f)
                    current.previewCriteriaMet=false;
                current.notes.RemoveAll(n=>n.StartsWith("Mandatory preview checks:"));
                current.notes.Add("Mandatory preview checks: "+(current.previewCriteriaMet?"PASS":"NOT MET")+"; Generate remains locked.");
                if(!current.previewCriteriaMet&&!current.warnings.Any(n=>n.StartsWith("Mandatory preview criteria")))current.warnings.Add("Mandatory preview criteria incomplete; no generation.");
            }
            AddQualityNotes(current);
            return current;
        }

        private sealed class HeightVar
        {
            public Vector2 pos;
            public float y,lo=float.NegativeInfinity,hi=float.PositiveInfinity;
            public bool fixedHeight;
            public readonly List<float> ground=new List<float>();
            public readonly List<HeightPair> adjacent=new List<HeightPair>();
        }
        private sealed class HeightPair
        {
            public HeightVar a,b;
            public float rise,lo,hi,length;
            public float[] midGround;
            public HeightVar Other(HeightVar x)=>x==a?b:a;
        }
        private float StageBudget(Vector2 p)
        {
            return settings.districtGrading&&Contains(plan.layout.Ankara(settings.ankaraLow),p)&&
                !IsProtected(p,RoadClearance+settings.blend)&&!obstacles.Near(p,RoadClearance*2f+settings.blend)?
                Mathf.Max(settings.maxCutFill,settings.districtGradingLimit):settings.maxCutFill;
        }
        private float[] StageGround(Vector2 p,Vector2 tangent,out float lo,out float hi)
        {
            lo=float.NegativeInfinity;hi=float.PositiveInfinity;
            Vector2 side=new Vector2(-tangent.y,tangent.x).normalized*(settings.width*.5f+settings.sidewalk);
            var values=new float[3];
            for(int j=-1;j<=1;j++)
            {
                Vector2 q=p+side*j;
                if(!plan.layout.TryHeight(q,out float h))throw new InvalidOperationException("Missing road cross-section terrain sample.");
                h+=.12f;values[j+1]=h;
                float budget=StageBudget(q);lo=Mathf.Max(lo,h-budget);hi=Mathf.Min(hi,h+budget);
            }
            return values;
        }

        // One shared variable per junction, and one per interior road sample.
        // Feasible coordinate sweeps constrain each update against all incident
        // grades and cross-section bounds, with actual baseline as the starting point.
        private void OptimizeSharedProfiles(int sweeps,bool balanced,IReadOnlyCollection<Edge> detachedScope=null)
        {
            var fixedNodes=new HashSet<Node>(plan.edges.Where(e=>e.Bridge).Concat(plan.authoredLinks).Concat(plan.detachedEdges)
                .SelectMany(e=>new[]{e.a,e.b}));
            if(detachedScope!=null)
            {
                var scope=new HashSet<Edge>(detachedScope);
                if(scope.Any(e=>e.Bridge||!plan.detachedEdges.Contains(e)))
                    throw new InvalidOperationException("Detached profile scope contains a retained or invalid road.");
                fixedNodes=new HashSet<Node>(plan.edges.Concat(plan.authoredLinks).Concat(plan.detachedEdges.Where(e=>!scope.Contains(e)))
                    .SelectMany(e=>new[]{e.a,e.b}));
            }
            var junctions=new Dictionary<Node,HeightVar>();
            var vars=new List<HeightVar>();
            var paths=new Dictionary<Edge,HeightVar[]>();
            HeightVar New(Vector3 v,bool locked)
            {
                var result=new HeightVar{pos=XZ(v),y=v.y,fixedHeight=locked};vars.Add(result);return result;
            }
            HeightVar Junction(Node n)
            {
                if(!junctions.TryGetValue(n,out HeightVar value))
                    junctions[n]=value=New(n.position,n.gate||fixedNodes.Contains(n));
                return value;
            }
            IEnumerable<Edge> profileEdges=detachedScope==null?plan.edges.Where(e=>e.sector=="Ankara"&&!e.Bridge):detachedScope;
            foreach(var edge in profileEdges)
            {
                var a=new HeightVar[edge.points.Count];
                for(int i=0;i<a.Length;i++)
                {
                    Vector3 p=edge.points[i];
                    var value=i==0?Junction(edge.a):i==a.Length-1?Junction(edge.b):New(p,false);
                    Vector2 tangent=XZ(edge.points[Mathf.Min(i+1,a.Length-1)]-edge.points[Mathf.Max(i-1,0)]);
                    var ground=StageGround(XZ(p),tangent,out float low,out float high);
                    value.ground.AddRange(ground);value.lo=Mathf.Max(value.lo,low);value.hi=Mathf.Min(value.hi,high);
                    a[i]=value;
                    if(i==0)continue;
                    var prev=a[i-1];float len=Vector2.Distance(prev.pos,value.pos);
                    if(len<.001f)throw new InvalidOperationException("Duplicate road profile sample.");
                    var mid=StageGround((prev.pos+value.pos)*.5f,value.pos-prev.pos,out float ml,out float mh);
                    var pair=new HeightPair{a=prev,b=value,rise=len*settings.grade,length=len,lo=ml,hi=mh,midGround=mid};
                    prev.adjacent.Add(pair);value.adjacent.Add(pair);
                }
                paths.Add(edge,a);
            }
            // Add a curvature penalty only for the detached preparation mode.
            // Every term is the absolute linear change of adjacent slopes.
            var localBends=new Dictionary<HeightVar,List<(HeightVar a,HeightVar b,HeightVar c,float dl,float dr)>>();
            if(detachedScope!=null)
            foreach(var path in paths.Values)
            for(int i=1;i<path.Length-1;i++)
            {
                var a=path[i-1];var b=path[i];var c=path[i+1];
                float dl=Vector2.Distance(a.pos,b.pos),dr=Vector2.Distance(b.pos,c.pos);
                if(dl<.001f||dr<.001f)continue;
                foreach(var v in new[]{a,b,c}.Distinct())
                {
                    if(!localBends.TryGetValue(v,out var list))localBends[v]=list=new List<(HeightVar,HeightVar,HeightVar,float,float)>();
                    list.Add((a,b,c,dl,dr));
                }
            }
            // A legacy sample can be slightly outside a newly sampled midpoint
            // bound. Freeze that segment instead of expanding its grading allowance.
            foreach(var x in vars)
            {
                if(x.y<x.lo-.001f||x.y>x.hi+.001f)x.fixedHeight=true;
                foreach(var pair in x.adjacent)
                {
                    float y=(x.y+pair.Other(x).y)*.5f;
                    if(y<pair.lo-.001f||y>pair.hi+.001f){x.fixedHeight=true;pair.Other(x).fixedHeight=true;}
                }
            }
            int moved=0;
            for(int pass=0;pass<sweeps;pass++)
            {
                if(pass%4==0&&EditorUtility.DisplayCancelableProgressBar("V7.1","Shared height solve "+(pass+1)+"/"+sweeps,.35f))throw new OperationCanceledException();
                for(int i=0;i<vars.Count;i++)
                {
                    var x=vars[pass%2==0?i:vars.Count-1-i];
                    if(x.fixedHeight||x.ground.Count==0)continue;
                    float lo=x.lo,hi=x.hi;
                    foreach(var pair in x.adjacent)
                    {
                        float other=pair.Other(x).y;
                        lo=Mathf.Max(lo,Mathf.Max(other-pair.rise,2f*pair.lo-other));
                        hi=Mathf.Min(hi,Mathf.Min(other+pair.rise,2f*pair.hi-other));
                    }
                    if(lo>hi)continue;
                    float Local(float y)
                    {
                        float total=0;
                        foreach(var pair in x.adjacent)
                        foreach(float h in pair.midGround)
                        {
                            float d=Mathf.Abs((y+pair.Other(x).y)*.5f-h);
                            total+=pair.length*(d+(balanced?2f:1f)*Mathf.Max(0,d-settings.maxCutFill)+
                                (balanced?4f:2f)*Mathf.Max(0,d-settings.preferredGrading));
                        }
                        foreach(float h in x.ground)total+=.1f*Mathf.Abs(y-h);
                        if(detachedScope!=null&&localBends.TryGetValue(x,out var bends))
                        {
                            float Y(HeightVar v)=>v==x?y:v.y;
                            foreach(var bend in bends)
                            {
                                float k=Mathf.Abs((Y(bend.c)-Y(bend.b))/bend.dr-(Y(bend.b)-Y(bend.a))/bend.dl)/((bend.dl+bend.dr)*.5f);
                                total+=1200f*Mathf.Max(0f,k-1f/Mathf.Max(1f,settings.verticalRadius));
                            }
                        }
                        return total;
                    }
                    // Convex local objective; bounded ternary search, keeping old
                    // value if it is better or already at a feasible optimum.
                    float l=lo,r=hi;
                    for(int k=0;k<12;k++){float a=(2*l+r)/3,b=(l+2*r)/3;if(Local(a)<Local(b))r=b;else l=a;}
                    float y=(l+r)*.5f;
                    if(Local(y)+.0001f<Local(x.y)&&Mathf.Abs(y-x.y)>.0001f){x.y=y;moved++;}
                }
            }
            foreach(var j in junctions)j.Key.position=XYZ(j.Value.pos,j.Value.y);
            foreach(var item in paths)
                for(int i=0;i<item.Value.Length;i++)item.Key.points[i]=XYZ(item.Value[i].pos,item.Value[i].y);
            plan.notes.Add((detachedScope==null?"Shared profile":"Detached shared profile")+" variables "+vars.Count+", junctions "+junctions.Count+", coordinate improvements "+moved+".");
        }
        private void ValidateStageGrades()
        {
            foreach(var e in plan.edges.Concat(plan.authoredLinks))
            {
                if(e.points.Count<2||e.points[0]!=e.a.position||e.points[e.points.Count-1]!=e.b.position)
                    throw new InvalidOperationException("Road endpoint mismatch.");
                for(int i=1;i<e.points.Count;i++)if(!GradeOk(e.points[i-1],e.points[i]))
                    throw new InvalidOperationException("Retained road exceeds maximum grade.");
            }
        }

        private void ReconnectDetached()
        {
            var index=new StreetIndex();
            foreach(var e in plan.edges.Concat(plan.authoredLinks))index.Add(e);
            int attempts=0,joined=0,routed=0;
            foreach(string city in new[]{"Ankara","İstanbul"})
            {
                Rect bounds=city=="Ankara"?plan.layout.Ankara(settings.ankaraLow):plan.layout.Istanbul(settings.ankaraLow);
                gradingSurface=city=="Ankara"&&settings.districtGrading?new GradingSurface(plan.layout,bounds,settings.grade):null;
                var left=new HashSet<Edge>(plan.detachedEdges.Where(e=>e.sector==city));
                var adj=left.SelectMany(e=>new[]{(n:e.a,e),(n:e.b,e)}).GroupBy(x=>x.n).ToDictionary(g=>g.Key,g=>g.Select(x=>x.e).ToList());
                while(left.Count>0&&attempts<160)
                {
                    var group=new List<Edge>();var nodes=new HashSet<Node>();var pending=new Queue<Edge>();
                    var first=left.OrderBy(e=>e.a.id).ThenBy(e=>e.b.id).First();
                    pending.Enqueue(first);left.Remove(first);
                    while(pending.Count>0)
                    {
                        var e=pending.Dequeue();group.Add(e);
                        foreach(Node n in new[]{e.a,e.b})if(nodes.Add(n))
                            foreach(var next in adj[n])if(left.Remove(next))pending.Enqueue(next);
                    }
                    // Validate the complete component against the retained network
                    // before promoting it. No crossing is ignored.
                    bool valid=true;
                    foreach(var e in group)
                    {
                        if(e.points.Any(v=>IsProtected(XZ(v),RoadClearance)||obstacles.Near(XZ(v),RoadClearance*2f))||
                            StreetCrosses(e.a,e.b,e.points,index)||!StreetAngles(e.a,e.b,e.points,index))
                        {valid=false;break;}
                        for(int i=1;i<e.points.Count;i++)if(!GradeOk(e.points[i-1],e.points[i])){valid=false;break;}
                        if(!valid)break;
                    }
                    if(!valid)continue;
                    foreach(var e in group){index.Add(e);e.a.degree++;e.b.degree++;}
                    Edge connector=null;
                    try
                    {
                        var sources=plan.edges.Where(e=>e.sector==city).SelectMany(e=>new[]{e.a,e.b}).Distinct()
                            .Where(n=>!n.gate&&n.degree<DegreeLimit(n)).ToList();
                        var options=nodes.Where(n=>!n.gate&&n.degree<DegreeLimit(n)).SelectMany(b=>sources
                            .OrderBy(a=>(XZ(a.position)-XZ(b.position)).sqrMagnitude).Take(5).Select(a=>(a,b)))
                            .Where(x=>Vector2.Distance(XZ(x.a.position),XZ(x.b.position))>=RoadClearance*2.5f&&
                                Vector2.Distance(XZ(x.a.position),XZ(x.b.position))<=650f)
                            .OrderBy(x=>Vector2.Distance(XZ(x.a.position),XZ(x.b.position))+Mathf.Abs(x.a.position.y-x.b.position.y)*4).Take(30);
                        bool old=settings.qualityDesign;settings.qualityDesign=false;
                        try
                        {
                            foreach(var x in options)
                            {
                                if(++attempts>160)break;
                                if(attempts%12==0&&EditorUtility.DisplayCancelableProgressBar("V7.1","Detached repair "+attempts,.55f))throw new OperationCanceledException();
                                List<Vector3> path;
                                if(!TryStreet(x.a,x.b,bounds,RoadKind.Collector,index,false,out path)&&
                                    (routed>=8||!settings.terrainRepair||!TryRepairRoute(x.a,x.b,bounds,index,ref routed,out path)))continue;
                                connector=new Edge{a=x.a,b=x.b,sector=city,kind=RoadKind.Collector,points=path};
                                break;
                            }
                        }
                        finally{settings.qualityDesign=old;}
                    }
                    finally
                    {
                        if(connector==null)foreach(var e in group){index.Remove(e);e.a.degree--;e.b.degree--;}
                    }
                    if(connector==null)continue;
                    foreach(var e in group){plan.detachedEdges.Remove(e);plan.edges.Add(e);}
                    plan.edges.Add(connector);connector.a.degree++;connector.b.degree++;index.Add(connector);joined+=group.Count;
                }
            }
            gradingSurface=null;
            plan.notes.Add("Detached repair: "+joined+" promoted segments, "+attempts+" direct attempts, "+routed+" routed attempts.");
        }
        private bool TryRepairRoute(Node a,Node b,Rect bounds,StreetIndex index,ref int routed,out List<Vector3> path)
        {
            routed++;return TryTerrainConnector(a,b,bounds,index,out path,RoadKind.Collector);
        }

        private void RefineExistingGeometry()
        {
            var index=new StreetIndex();foreach(var e in plan.edges.Concat(plan.authoredLinks))index.Add(e);
            int count=0;
            string sector=null;
            foreach(var e in plan.edges.Where(e=>!e.Bridge&&!RadiusOk(e.points,e.kind)).ToList())
            {
                if(++count>100)break;
                Rect bounds=e.sector=="Ankara"?plan.layout.Ankara(settings.ankaraLow):plan.layout.Istanbul(settings.ankaraLow);
                if(sector!=e.sector)
                {
                    sector=e.sector;
                    gradingSurface=e.sector=="Ankara"&&settings.districtGrading?new GradingSurface(plan.layout,bounds,settings.grade):null;
                }
                index.Remove(e);
                try
                {
                    float cost=QualityPathCost(e.points);
                    if(TryStreet(e.a,e.b,bounds,e.kind,index,false,out List<Vector3> path)&&
                        RadiusOk(path,e.kind)&&QualityPathCost(path)<=cost+.001f)e.points=path;
                }
                finally{index.Add(e);}
            }
            gradingSurface=null;
        }

        private void RefineFixedGateBridges()
        {
            var bridges=plan.edges.Where(e=>e.Bridge).ToList();
            if(bridges.Count!=2)return;
            var index=new StreetIndex();foreach(var e in plan.edges.Concat(plan.authoredLinks))index.Add(e);
            var l=plan.layout;
            float Across(Vector3 p)=>l.rowsOnX?p.z:p.x;
            float Gap(List<Vector3> a,List<Vector3> b)=>Mathf.Max(0f,Mathf.Max(a.Min(Across)-b.Max(Across),b.Min(Across)-a.Max(Across)));
            int tested=0;
            foreach(var edge in bridges)
            {
                var other=bridges.First(e=>e!=edge);
                index.Remove(edge);
                try
                {
                    var best=edge.points;float gap=Gap(best,other.points);
                    foreach(float bow in new[]{-240f,-120f,-60f,0f,60f,120f,240f})
                    {
                        tested++;
                        if(!TryBridge(XZ(edge.a.position),XZ(edge.b.position),out List<Vector3> path,out _,
                            settings.ankaraLow?bow:0f,settings.ankaraLow?0f:bow))continue;
                        if(path[0]!=edge.a.position||path[path.Count-1]!=edge.b.position||!RadiusOk(path,RoadKind.Arterial)||
                            StreetCrosses(edge.a,edge.b,path,index)||!StreetAngles(edge.a,edge.b,path,index))continue;
                        float candidate=Gap(path,other.points);
                        if(candidate>gap+.01f){gap=candidate;best=path;}
                    }
                    edge.points=best;
                }
                finally{index.Add(edge);}
            }
            // Existing-road corridor opportunities are reported, never treated as
            // permission to cross protected buildings or to split an authored spline.
            int corridorEnds=manualRoads.Where(r=>r.IsUsable).SelectMany(r=>new[]{r.GetPoint(0f),r.GetPoint(1f)})
                .Count(p=>Contains(l.Ankara(settings.ankaraLow),XZ(p))&&
                    Mathf.Abs((l.rowsOnX?p.x:p.z)-(settings.ankaraLow?l.AlongMax(l.low):l.AlongMin(l.high)))<250f);
            plan.notes.Add("Bridge stage: "+tested+" fixed-gate approaches tested; "+corridorEnds+" existing coastal spline ends within 250 m (audit only; new gates require explicit corridor/graph redesign).");
        }
    }
}
