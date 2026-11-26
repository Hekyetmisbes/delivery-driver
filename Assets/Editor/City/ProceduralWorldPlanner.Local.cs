using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DeliveryDriver.EditorTools
{
    internal sealed partial class ProceduralWorldPlanner
    {
        [Serializable] internal sealed class VirtualSection
        {
            public Vector3[] original,proposed;
            public bool flagged;
        }
        [Serializable] internal sealed class VirtualTerrainReport
        {
            public int sections,missingSections,missingSamples,conflictingCells,steepSections,protectedSamples,parcelSamples;
            public float spacing;
            // Schema 0 is the legacy snapshot; schema 1 has bounded diagnostic evidence.
            public int diagnosticSchema,diagnosticCells,additionalConflictCells,overflowCells,overflowSamples;
            public int sharedJunctionCells,independentRoadCells,ambiguousCells,closeSampleCells,coarseOnlyCells,unknownProximityCells;
            public int topologySchema,connectedOnlyCells,unrelatedOnlyCells,mixedTopologyCells,unknownTopologyCells;
            public int missingCenterSections,missingFootprintSections,missingOuterSections,issueCount,omittedIssues;
            public int missingCoastSamples,missingCityGapSamples,missingOutsideSamples;
            public float maxShoulderSlope,maxConflictDelta;
            public List<VirtualIssue> issues;
        }
        internal const int VirtualIssueLimit=12000,VirtualCellSampleLimit=64;
        internal enum VirtualIssueKind { SharedJunction, IndependentRoad, AmbiguousOverlap, MissingSection, MissingShoulder, SteepSection, ProtectedSample, ParcelSample }
        [Serializable] internal sealed class VirtualSample
        {
            public int roadA,roadB,kind;
            public float distanceFromA,distanceFromB,ground;
            public Vector3 position,center;
            public bool hasGround;
            public string context;
        }
        [Serializable] internal sealed class VirtualIssue
        {
            public VirtualIssueKind kind;
            public Vector3 position;
            public Vector2Int cell;
            public bool legacyConflict,overflow,hasConnectedPair,hasUnrelatedPair;
            public float maxCloseDelta;
            public float maxDelta,minSeparation,leftSlope,rightSlope;
            public int maxSampleA=-1,maxSampleB=-1,closestSampleA=-1,closestSampleB=-1;
            public string reason;
            public List<VirtualSample> samples;
        }
        private sealed class VirtualCell
        {
            public readonly List<(VirtualSample sample,Edge owner,VirtualSection section)> samples=new List<(VirtualSample,Edge,VirtualSection)>();
            public bool overflow;
        }
        internal static string VirtualSampleName(int kind)
        {
            switch(kind)
            {
                case 0:return "Left outer blend";case 1:return "Left sidewalk edge";case 2:return "Left asphalt edge";
                case 3:return "Center";case 4:return "Right asphalt edge";case 5:return "Right sidewalk edge";
                case 6:return "Right outer blend";
                case 7:return "Left shoulder 25%";case 8:return "Left shoulder 50%";case 9:return "Left shoulder 75%";
                case 10:return "Right shoulder 25%";case 11:return "Right shoulder 50%";case 12:return "Right shoulder 75%";
                default:return "Unknown sample";
            }
        }
        internal static string VirtualMissingReason(int mask)
        {
            if((mask&(1<<3))!=0)return "Center";
            if((mask&0x3e)!=0)return "Road footprint";
            return "Outer blend";
        }
        internal static int SampleVirtualSection(Layout layout,Vector3 mid,Vector2 side,float[] offsets,VirtualSection section)
        {
            int missingMask=0;
            for(int j=0;j<7;j++)
            {
                Vector2 pos=XZ(mid)+side*offsets[j];
                if(!layout.TryHeight(pos,out float ground)){missingMask|=1<<j;continue;}
                section.original[j]=XYZ(pos,ground);
                section.proposed[j]=XYZ(pos,j==0||j==6?ground:mid.y-.12f);
            }
            return missingMask;
        }
        internal static bool VirtualHeightConflict(float a,float b)=>Mathf.Abs(a-b)>.5f;
        internal static bool ShareVirtualNode(Edge a,Edge b)=>
            a.a!=null&&(a.a==b.a||a.a==b.b)||a.b!=null&&(a.b==b.a||a.b==b.b);
        internal static VirtualIssueKind VirtualPairKind(Edge a,VirtualSample sa,Edge b,VirtualSample sb,float half)
        {
            // Topology alone is insufficient. Both station distances and actual XZ
            // positions must be near the same endpoint, and both samples in footprint.
            bool near=false;
            float reach=2f*half,reachSq=reach*reach;
            foreach(Node node in new[]{a.a,a.b})
            {
                if(node==null||(node!=b.a&&node!=b.b))continue;
                Vector2 p=XZ(node.position);
                if((XZ(sa.position)-p).sqrMagnitude>reachSq||(XZ(sb.position)-p).sqrMagnitude>reachSq)continue;
                near=true;
                float da=node==a.a?sa.distanceFromA:sa.distanceFromB,db=node==b.a?sb.distanceFromA:sb.distanceFromB;
                if(da<=reach&&db<=reach&&(XZ(sa.center)-p).sqrMagnitude<=reachSq&&(XZ(sb.center)-p).sqrMagnitude<=reachSq&&
                    sa.kind>=1&&sa.kind<=5&&sb.kind>=1&&sb.kind<=5)return VirtualIssueKind.SharedJunction;
            }
            return near?VirtualIssueKind.AmbiguousOverlap:VirtualIssueKind.IndependentRoad;
        }
        private static void AddVirtualIssue(VirtualTerrainReport report,VirtualIssue issue)
        {
            report.issueCount++;
            if(report.issues.Count<VirtualIssueLimit)report.issues.Add(issue);
            else report.omittedIssues++;
        }
        private string VirtualMissingContext(Vector2 p,VirtualTerrainReport report)
        {
            // Geometric context, not a diagnosis of the underlying terrain asset.
            if(Contains(plan.layout.sea,p,.01f)){report.missingCoastSamples++;return "Sea / shore bounds (no sampled terrain)";}
            if(Contains(plan.layout.low,p,.01f)||Contains(plan.layout.high,p,.01f))
            {report.missingCityGapSamples++;return "Inside city bounds, no covering tile";}
            report.missingOutsideSamples++;return "Outside mapped city/sea bounds";
        }
        internal static string VirtualDiagnosticSummary(VirtualTerrainReport r)
        {
            if(r==null||r.diagnosticSchema!=1)return "Virtual issue details unavailable (legacy or intermediate preview).";
            return "Virtual diagnostics: legacy conflict cells "+r.conflictingCells+", additional all-pair conflict cells "+r.additionalConflictCells+
                ", unresolved/overflow union "+r.diagnosticCells+" (overflow cells/samples "+r.overflowCells+"/"+r.overflowSamples+
                "); shared-junction / independent-road / ambiguous "+r.sharedJunctionCells+" / "+r.independentRoadCells+" / "+r.ambiguousCells+
                "; close (<=0.5m XZ) / coarse-only / unknown proximity "+r.closeSampleCells+" / "+r.coarseOnlyCells+" / "+r.unknownProximityCells+
                (r.topologySchema==1?"; topology connected-only / unrelated-only / mixed / unknown "+r.connectedOnlyCells+" / "+r.unrelatedOnlyCells+" / "+r.mixedTopologyCells+" / "+r.unknownTopologyCells:"")+
                "; max observed height delta "+r.maxConflictDelta.ToString("F2")+" m. All remain warnings/blockers. "+
                "Missing sections center / footprint / outer-only "+r.missingCenterSections+" / "+r.missingFootprintSections+" / "+r.missingOuterSections+
                "; missing point context sea/shore / city gap / outside "+r.missingCoastSamples+" / "+r.missingCityGapSamples+" / "+r.missingOutsideSamples+
                "; steep sections "+r.steepSections+", max observed shoulder grade "+(r.maxShoulderSlope*100f).ToString("F1")+"%; "+
                "protected / parcel "+r.protectedSamples+" / "+r.parcelSamples+"; issue records "+r.issues.Count+"/"+r.issueCount+
                " (omitted "+r.omittedIssues+"). Sparse samples, not physical collision, clearance or wall validation.";
        }

        // A split is valid only when the concatenated geometry is byte-for-byte
        // (Vector3 equality via SequenceEqual) the old sampled path. No old land
        // connection may disappear. Bridges can move only in their own stage.
        // Authored spline endpoints use -1 as a sentinel, not as a generated
        // graph identity. Only actual link-only sentinel nodes may share it.
        private static string NetworkIdentityIssue(Plan p)
        {
            var generated=new HashSet<Node>(p.edges.Concat(p.detachedEdges).SelectMany(e=>new[]{e.a,e.b}));
            var authored=new HashSet<Node>(p.authoredLinks.SelectMany(e=>new[]{e.a,e.b}));
            var ids=new HashSet<int>();
            foreach(var n in p.nodes.Concat(generated).Distinct())
            {
                if(n.id<0)
                {
                    if(n.id!=-1||generated.Contains(n)||!authored.Contains(n))
                        return "invalid negative node identity "+n.id;
                }
                else if(!ids.Add(n.id))return "duplicate generated node identity "+n.id;
            }
            return null;
        }

        internal static bool PreservedNetwork(Plan before,Plan after,bool allowSplit,bool allowBridgeMove)
        {
            if(NetworkIdentityIssue(before)!=null||NetworkIdentityIssue(after)!=null)return false;
            var oldNodes=new HashSet<int>(before.nodes.Where(n=>n.id>=0).Select(n=>n.id));
            var incident=after.edges.SelectMany(e=>new[]{(id:e.a.id,e),(id:e.b.id,e)})
                .GroupBy(x=>x.id).ToDictionary(g=>g.Key,g=>g.Select(x=>x.e).ToList());
            foreach(var old in before.edges)
            {
                if(old.Bridge&&allowBridgeMove)
                {
                    if(!incident.TryGetValue(old.a.id,out var left)||!left.Any(e=>!e.Bridge)||
                       !incident.TryGetValue(old.b.id,out var right)||!right.Any(e=>!e.Bridge))return false;
                    continue;
                }
                if(!incident.TryGetValue(old.a.id,out var start))return false;
                if(start.Any(e=>e.a.id==old.a.id&&e.b.id==old.b.id&&e.kind==old.kind&&e.sector==old.sector&&
                    e.a.position.Equals(old.a.position)&&e.b.position.Equals(old.b.position)&&e.points.SequenceEqual(old.points)))continue;
                if(!allowSplit||old.Bridge)return false;
                int cursor=0,id=old.a.id;
                while(cursor<old.points.Count-1)
                {
                    if(!incident.TryGetValue(id,out var choices))return false;
                    var next=choices.FirstOrDefault(e=>e.a.id==id&&e.kind==old.kind&&e.sector==old.sector&&
                        e.points.Count>=2&&cursor+e.points.Count<=old.points.Count&&
                        e.points.SequenceEqual(old.points.Skip(cursor).Take(e.points.Count)));
                    if(next==null)return false;
                    cursor+=next.points.Count-1;id=next.b.id;
                    if(id!=old.b.id&&oldNodes.Contains(id))return false;
                }
                if(id!=old.b.id)return false;
            }
            return true;
        }

        private Rect CityBounds(string sector)=>sector=="Ankara"?plan.layout.Ankara(settings.ankaraLow):plan.layout.Istanbul(settings.ankaraLow);
        private void SetLocalGrading(string sector)
        {
            gradingSurface=sector=="Ankara"&&settings.districtGrading?new GradingSurface(plan.layout,CityBounds(sector),settings.grade):null;
        }
        private float CrossAdjustment(Vector3 p,Vector2 dir)
        {
            Vector2 side=new Vector2(-dir.y,dir.x).normalized*(settings.width*.5f+settings.sidewalk);
            float worst=0;
            for(int j=-1;j<=1;j++)
            {
                if(!plan.layout.TryHeight(XZ(p)+side*j,out float h))return float.PositiveInfinity;
                worst=Mathf.Max(worst,Mathf.Abs(p.y-.12f-h));
            }
            return worst;
        }
        private (float peak,float mean,float extra,int index) RoadEarth(List<Vector3> path)
        {
            float peak=0,sum=0,total=0,extra=0;int worst=1;
            for(int i=1;i<path.Count;i++)
            {
                float len=Vector2.Distance(XZ(path[i]),XZ(path[i-1]));
                float val=CrossAdjustment((path[i]+path[i-1])*.5f,XZ(path[i]-path[i-1]));
                if(val>peak){peak=val;worst=i;}
                sum+=val*len;total+=len;if(val>settings.maxCutFill+.01f)extra+=len;
            }
            return (peak,sum/Mathf.Max(1f,total),extra,worst);
        }
        // Cross-section bound at samples and midpoints; grade constraints are
        // never relaxed. Local optimization does not invoke blanket node shifts.
        private bool LocalProfileSafe(List<Vector3> path)
        {
            bool At(Vector3 p,Vector2 dir)
            {
                Vector2 side=new Vector2(-dir.y,dir.x).normalized*(settings.width*.5f+settings.sidewalk);
                for(int j=-1;j<=1;j++)
                {
                    Vector2 pos=XZ(p)+side*j;
                    if(!plan.layout.TryHeight(pos,out float h)||Mathf.Abs(p.y-.12f-h)>StageBudget(pos)+.002f)return false;
                }
                return true;
            }
            for(int i=0;i<path.Count;i++)
            {
                if(!At(path[i],XZ(path[Mathf.Min(path.Count-1,i+1)]-path[Mathf.Max(0,i-1)])))return false;
                if(i>0&&(!GradeOk(path[i-1],path[i])||!At((path[i-1]+path[i])*.5f,XZ(path[i]-path[i-1]))))return false;
            }
            return true;
        }

        internal static float VerticalCurvature(Vector3 a,Vector3 b,Vector3 c)
        {
            float l=Vector2.Distance(XZ(a),XZ(b)),r=Vector2.Distance(XZ(b),XZ(c));
            if(l<.001f||r<.001f)return float.PositiveInfinity;
            return Mathf.Abs((c.y-b.y)/r-(b.y-a.y)/l)/((l+r)*.5f);
        }
        private static (int count,float severity,int worst) VerticalStats(List<Vector3> points,float limit)
        {
            int count=0,worst=1;float sum=0,max=0;
            for(int i=1;i<points.Count-1;i++)
            {
                float k=VerticalCurvature(points[i-1],points[i],points[i+1]);
                if(k>limit){count++;sum+=k-limit;}
                if(k>max){max=k;worst=i;}
            }
            return (count,sum,worst);
        }
        private static void MeasureVertical(Plan p,Settings s,QualityReport q)
        {
            float limit=1f/Mathf.Max(1f,s.verticalRadius);
            var incident=new Dictionary<Node,List<(Vector2 direction,float slope,float length)>>();
            foreach(var e in p.edges.Concat(p.authoredLinks))
            {
                var stats=VerticalStats(e.points,limit);
                q.verticalDefects+=stats.count;q.verticalSeverity+=stats.severity;
                foreach(Node n in new[]{e.a,e.b})
                {
                    Vector3 delta=n==e.a?e.points[1]-e.points[0]:e.points[e.points.Count-2]-e.points[e.points.Count-1];
                    float len=XZ(delta).magnitude;if(len<.001f)continue;
                    if(!incident.TryGetValue(n,out var list))incident[n]=list=new List<(Vector2,float,float)>();
                    list.Add((XZ(delta)/len,delta.y/len,len));
                }
            }
            foreach(var list in incident.Values)
            for(int i=0;i<list.Count;i++)for(int j=i+1;j<list.Count;j++)
            {
                var a=list[i];var b=list[j];
                // Only approximately straight-through movements; turning movements
                // require an actual junction surface / swept-path test at bake time.
                if(Vector2.Dot(a.direction,b.direction)>-.7071f)continue;
                float k=Mathf.Abs(a.slope+b.slope)/Mathf.Max(1f,(a.length+b.length)*.5f);
                if(k>limit)q.junctionGradeFlags++;
            }
        }
        // Refine a bounded interior window, projecting each height update into
        // both neighboring grade bounds and the actual cross-section allowance.
        // Endpoints and all samples outside the window are untouched.
        private void FitLocalHeights(List<Vector3> path,int pivot,int window)
        {
            for(int pass=0;pass<8;pass++)
            {
                int first=Mathf.Max(1,pivot-window),last=Mathf.Min(path.Count-2,pivot+window);
                for(int j=first;j<=last;j++)
                {
                    int i=pass%2==0?j:first+last-j;
                    Vector3 p=path[i],a=path[i-1],b=path[i+1];
                    float dl=Vector2.Distance(XZ(p),XZ(a)),dr=Vector2.Distance(XZ(p),XZ(b));
                    if(dl<.001f||dr<.001f)continue;
                    float[] ground;
                    float lo,hi;
                    try{ground=StageGround(XZ(p),XZ(b-a),out lo,out hi);}
                    catch(InvalidOperationException){continue;}
                    lo=Mathf.Max(lo,Mathf.Max(a.y-dl*settings.grade,b.y-dr*settings.grade));
                    hi=Mathf.Min(hi,Mathf.Min(a.y+dl*settings.grade,b.y+dr*settings.grade));
                    if(lo>hi)continue;
                    p.y=Mathf.Clamp(Mathf.Lerp(p.y,ground.Average(),.65f),lo,hi);path[i]=p;
                }
            }
        }
        private void RefineLocalRoads(int mode,IReadOnlyCollection<Edge> detachedScope=null)
        {
            var index=new StreetIndex();foreach(var e in plan.edges.Concat(plan.authoredLinks))index.Add(e);
            if(detachedScope!=null)
            {
                if(detachedScope.Any(e=>e.Bridge||!plan.detachedEdges.Contains(e)))
                    throw new InvalidOperationException("Detached smoothing scope contains a retained or invalid road.");
                foreach(var e in detachedScope)index.Add(e);
            }
            IEnumerable<Edge> localEdges=detachedScope==null?(IEnumerable<Edge>)plan.edges:detachedScope;
            var roads=localEdges.Where(e=>!e.Bridge&&e.points.Count>3).Select(e=>(e,earth:RoadEarth(e.points),
                radius:MinimumRadius(e.points),vertical:VerticalStats(e.points,1f/settings.verticalRadius)))
                .Where(x=>mode==0?x.e.sector=="Ankara"&&x.earth.peak>settings.preferredGrading:
                    mode==1?!RadiusOk(x.e.points,x.e.kind):x.vertical.count>0)
                .OrderByDescending(x=>mode==0?x.earth.peak:mode==1?-x.radius:x.vertical.severity)
                .Take(settings.localRepairBudget).ToList();
            int changed=0,processed=0;
            foreach(var item in roads)
            {
                if(++processed%12==0&&EditorUtility.DisplayCancelableProgressBar("V7.2 local refinement","Road "+processed+"/"+roads.Count,.62f))throw new OperationCanceledException();
                var e=item.e;var original=e.points;var best=original;
                var baseEarth=item.earth;var bestEarth=baseEarth;
                float bestRadius=item.radius;var bestVertical=item.vertical;
                int pivot=mode==0?Mathf.Clamp(baseEarth.index,1,original.Count-2):mode==2?item.vertical.worst:
                    Enumerable.Range(1,original.Count-2).OrderBy(i=>TurnRadius(XZ(original[i-1]),XZ(original[i]),XZ(original[i+1]))).First();
                Rect bounds=CityBounds(e.sector);
                index.Remove(e);
                try
                {
                    foreach(int window in new[]{3,6,12})
                    foreach(float amount in new[]{.25f,.5f,1f})
                    {
                        var path=new List<Vector3>(original);
                        for(int pass=0;pass<4;pass++)
                        {
                            var prev=new List<Vector3>(path);
                            for(int i=Mathf.Max(1,pivot-window);i<=Mathf.Min(path.Count-2,pivot+window);i++)
                            {
                                float l=Vector2.Distance(XZ(prev[i-1]),XZ(prev[i])),r=Vector2.Distance(XZ(prev[i]),XZ(prev[i+1]));
                                float f=l/Mathf.Max(.001f,l+r);
                                Vector3 target=Vector3.Lerp(prev[i-1],prev[i+1],f);
                                float weight=amount*Mathf.Clamp01(1f-Mathf.Abs(i-pivot)/(float)(window+1));
                                if(mode!=1)
                                {
                                    if(mode==0)
                                    {
                                        Vector2 tangent=XZ(prev[i+1]-prev[i-1]).normalized;
                                        try
                                        {
                                            var ground=StageGround(XZ(prev[i]),tangent,out float lo,out float hi);
                                            if(lo>hi)continue;
                                            target.y=Mathf.Clamp(ground.Average(),lo,hi);
                                        }
                                        catch(InvalidOperationException){continue;}
                                    }
                                    target.x=prev[i].x;target.z=prev[i].z;
                                }
                                else target.y=prev[i].y;
                                path[i]=Vector3.Lerp(prev[i],target,weight);
                            }
                        }
                        if(!RouteClear(path,bounds)||!LocalProfileSafe(path)||!StreetAngles(e.a,e.b,path,index)||StreetCrosses(e.a,e.b,path,index))continue;
                        var earth=RoadEarth(path);float radius=MinimumRadius(path);var vertical=VerticalStats(path,1f/settings.verticalRadius);
                        if(earth.peak>baseEarth.peak+.01f||earth.mean>baseEarth.mean+.001f||earth.extra>baseEarth.extra+.01f||
                            vertical.severity>item.vertical.severity+.0001f||radius+.01f<item.radius)continue;
                        bool better=mode==0?earth.peak<bestEarth.peak-.01f||earth.peak<=bestEarth.peak+.001f&&earth.mean<bestEarth.mean-.001f:
                            mode==1?radius>bestRadius+.01f:vertical.count<bestVertical.count||vertical.count==bestVertical.count&&vertical.severity<bestVertical.severity-.0001f;
                        if(better){best=path;bestEarth=earth;bestRadius=radius;bestVertical=vertical;}
                    }
                    if(mode==0)
                    {
                        // Try genuine local lateral reroutes around the high-cost
                        // sample, rather than restarting the complete road layout.
                        foreach(int window in new[]{6,12,24})
                        foreach(float offset in new[]{-18f,-9f,-4f,4f,9f,18f})
                        {
                            var trial=new List<Vector3>(original);
                            for(int i=Mathf.Max(1,pivot-window);i<=Mathf.Min(trial.Count-2,pivot+window);i++)
                            {
                                Vector2 tangent=XZ(original[i+1]-original[i-1]).normalized;
                                Vector2 side=new Vector2(-tangent.y,tangent.x);
                                float f=Mathf.Clamp01(1f-Mathf.Abs(i-pivot)/(float)window);
                                float shift=offset*Mathf.Pow(Mathf.Sin(Mathf.PI*.5f*f),2f);
                                trial[i]=XYZ(XZ(original[i])+side*shift,original[i].y);
                            }
                            FitLocalHeights(trial,pivot,window);
                            if(!RouteClear(trial,bounds)||!LocalProfileSafe(trial)||!StreetAngles(e.a,e.b,trial,index)||StreetCrosses(e.a,e.b,trial,index))continue;
                            var earth=RoadEarth(trial);float radius=MinimumRadius(trial);var vertical=VerticalStats(trial,1f/settings.verticalRadius);
                            if(earth.mean>baseEarth.mean+.001f||earth.extra>baseEarth.extra+.01f||vertical.severity>item.vertical.severity+.0001f||
                                radius+.01f<Mathf.Min(item.radius,settings.minTurnRadius*(e.kind==RoadKind.Arterial?1.5f:e.kind==RoadKind.Collector?1.2f:1f)))continue;
                            if(earth.peak<bestEarth.peak-.01f||earth.peak<=bestEarth.peak+.001f&&earth.mean<bestEarth.mean-.001f)
                            {best=trial;bestEarth=earth;bestRadius=radius;bestVertical=vertical;}
                        }
                    }
                    if(best!=original){e.points=best;changed++;}
                }
                finally{index.Add(e);}
            }
            plan.notes.Add("Local "+(mode==0?"hotspot":mode==1?"bend":"vertical")+" refinement: "+changed+"/"+roads.Count+" eligible roads improved; unchanged endpoints.");
        }

        private sealed class HotspotHeightSample
        {
            public Vector3 position;
            public Node node;
            public bool locked;
            public readonly List<(HotspotHeightSample other,float length)> adjacent=new List<(HotspotHeightSample,float)>();
        }

        // Propagate only the displacement necessary to maintain the original grade
        // bound. A saturated slope has zero slack; an unconstrained neighbor can
        // absorb the displacement without moving.
        internal static float HotspotNeighborShift(float shift,float signedRise,float maximumRise)
        {
            return Mathf.Max(0f,shift-Mathf.Max(0f,maximumRise-signedRise));
        }

        private void RefineHotspotHeightChains()
        {
            var targets=plan.edges.Where(e=>e.sector=="Ankara"&&!e.Bridge)
                .Select(e=>(edge:e,earth:RoadEarth(e.points)))
                .Where(x=>x.earth.peak>settings.preferredGrading)
                .OrderByDescending(x=>x.earth.peak).ThenBy(x=>x.edge.a.id).Take(5).Select(x=>x.edge).ToList();
            if(targets.Count==0)return;
            var stageInput=Measure(plan,settings);
            int tested=0,blocked=0,profileRejected=0,qualityRejected=0,accepted=0;
            foreach(var target in targets)
            {
                var earth=RoadEarth(target.points);
                if(earth.peak<=settings.preferredGrading)continue;
                var baseline=Measure(plan,settings);
                var movable=plan.edges.Where(e=>e.sector=="Ankara"&&!e.Bridge).ToList();
                var fixedNodes=new HashSet<Node>(plan.edges.Where(e=>e.Bridge||e.sector!="Ankara")
                    .Concat(plan.authoredLinks).Concat(plan.detachedEdges).SelectMany(e=>new[]{e.a,e.b}));
                var shared=new Dictionary<Node,HotspotHeightSample>();
                var paths=new Dictionary<Edge,HotspotHeightSample[]>();
                HotspotHeightSample Junction(Node n)
                {
                    if(!shared.TryGetValue(n,out var v))
                        shared[n]=v=new HotspotHeightSample{position=n.position,node=n,locked=n.gate||fixedNodes.Contains(n)};
                    return v;
                }
                foreach(var e in movable)
                {
                    var path=new HotspotHeightSample[e.points.Count];
                    for(int i=0;i<path.Length;i++)
                    {
                        path[i]=i==0?Junction(e.a):i==path.Length-1?Junction(e.b):new HotspotHeightSample{position=e.points[i]};
                        if(i>0)
                        {
                            float len=Vector2.Distance(XZ(path[i-1].position),XZ(path[i].position));
                            path[i-1].adjacent.Add((path[i],len));path[i].adjacent.Add((path[i-1],len));
                        }
                    }
                    paths[e]=path;
                }
                var seedPath=paths[target];int pivot=Mathf.Clamp(earth.index,1,seedPath.Length-1);
                Vector2 center=XZ((seedPath[pivot-1].position+seedPath[pivot].position)*.5f);
                Dictionary<HotspotHeightSample,float> best=null;
                QualityReport bestQuality=null;
                float bestTargetPeak=earth.peak;
                bool CanMove(HotspotHeightSample v)
                {
                    Vector2 p=XZ(v.position);
                    return !v.locked&&Vector2.Distance(center,p)<=450f&&
                        !IsProtected(p,RoadClearance+settings.blend)&&!obstacles.Near(p,RoadClearance*2f+settings.blend);
                }
                // All paths and shared node positions must agree throughout a trial.
                void Apply(Dictionary<HotspotHeightSample,float> changes,Dictionary<Edge,List<Vector3>> originals)
                {
                    foreach(var entry in changes)
                        if(entry.Key.node!=null)entry.Key.node.position=entry.Key.position+Vector3.up*entry.Value;
                    foreach(var e in originals.Keys)
                    {
                        var path=paths[e];var points=new List<Vector3>(path.Length);
                        foreach(var v in path)
                            points.Add(v.position+Vector3.up*(changes.TryGetValue(v,out float dy)?dy:0f));
                        e.points=points;
                    }
                }
                void RestoreTrial(Dictionary<HotspotHeightSample,float> changes,Dictionary<Edge,List<Vector3>> originals)
                {
                    foreach(var entry in changes)if(entry.Key.node!=null)entry.Key.node.position=entry.Key.position;
                    foreach(var entry in originals)entry.Key.points=entry.Value;
                }
                foreach(float sign in new[]{-1f,1f})
                foreach(float amount in new[]{.5f,1.5f,3f,6f})
                {
                    tested++;
                    if(EditorUtility.DisplayCancelableProgressBar("V7.2 hotspot height chain","Candidate "+tested,.63f))throw new OperationCanceledException();
                    var magnitudes=new Dictionary<HotspotHeightSample,float>();
                    var queue=new Queue<HotspotHeightSample>();
                    bool feasible=true;
                    void RequireShift(HotspotHeightSample v,float value)
                    {
                        if(value<=.0001f||magnitudes.TryGetValue(v,out float prior)&&value<=prior+.0001f)return;
                        if(!CanMove(v)||magnitudes.Count>=5000){feasible=false;return;}
                        magnitudes[v]=value;queue.Enqueue(v);
                    }
                    RequireShift(seedPath[pivot-1],amount);RequireShift(seedPath[pivot],amount);
                    int relaxations=0;
                    while(feasible&&queue.Count>0)
                    {
                        if(++relaxations>100000){feasible=false;break;}
                        var v=queue.Dequeue();float shift=magnitudes[v];
                        foreach(var link in v.adjacent)
                        {
                            float signedRise=sign*(v.position.y-link.other.position.y);
                            RequireShift(link.other,HotspotNeighborShift(shift,signedRise,settings.grade*link.length));
                        }
                    }
                    if(!feasible){blocked++;continue;}
                    var changes=magnitudes.ToDictionary(x=>x.Key,x=>sign*x.Value);
                    var originals=paths.Where(x=>x.Value.Any(v=>changes.ContainsKey(v))).ToDictionary(x=>x.Key,x=>x.Key.points);
                    try
                    {
                        Apply(changes,originals);
                        if(originals.Keys.Any(e=>!e.points[0].Equals(e.a.position)||!e.points[e.points.Count-1].Equals(e.b.position)||!LocalProfileSafe(e.points)))
                        {profileRejected++;continue;}
                        float targetPeak=RoadEarth(target.points).peak;
                        if(targetPeak>=earth.peak-.001f){qualityRejected++;continue;}
                        var q=Measure(plan,settings);
                        if(!NoTerrainRegression(baseline,q,settings)||!NoTerrainRegression(stageInput,q,settings)||
                            !NoLocalQualityRegression(baseline,q)||!NoLocalQualityRegression(stageInput,q)||
                            (q.peak>=baseline.peak-.001f&&BalancedCost(q,settings)>=BalancedCost(baseline,settings)-.001f))
                        {qualityRejected++;continue;}
                        if(bestQuality==null||q.peak<bestQuality.peak-.001f||
                            Mathf.Abs(q.peak-bestQuality.peak)<=.001f&&BalancedCost(q,settings)<BalancedCost(bestQuality,settings))
                        {best=changes;bestQuality=q;bestTargetPeak=targetPeak;}
                    }
                    finally{RestoreTrial(changes,originals);}
                }
                if(best!=null)
                {
                    var originals=paths.Where(x=>x.Value.Any(v=>best.ContainsKey(v))).ToDictionary(x=>x.Key,x=>x.Key.points);
                    Apply(best,originals);accepted++;
                    plan.notes.Add("Hotspot height chain "+target.a.id+"-"+target.b.id+": "+earth.peak.ToString("F3")+" -> "+bestTargetPeak.ToString("F3")+
                        " m; "+originals.Count+" affected roads, "+best.Count+" samples; XZ topology unchanged.");
                }
            }
            plan.notes.Add("Hotspot height chains: "+targets.Count+" target roads, "+tested+" trials, "+accepted+
                " accepted; "+blocked+" fixed/boundary rejects, "+profileRejected+" profile rejects, "+qualityRejected+
                " quality/no-benefit rejects. Shared endpoints, maximum 450 m radius; no XZ changes.");
        }

        private void RepairWithInteriorJunctions()
        {
            var index=new StreetIndex();foreach(var e in plan.edges.Concat(plan.authoredLinks))index.Add(e);
            int attempts=0,promoted=0,splits=0,routed=0,groups=0,profileReject=0,geometryReject=0,connectorReject=0;
            int terrainReject=0,qualityReject=0,graphReject=0,measured=0;
            // The outer stage still verifies the original network and quality.
            // Every group must also pass relative to both its input and stage input.
            var stageQuality=Measure(plan,settings);
            foreach(string city in new[]{"Ankara","İstanbul"})
            {
                if(!plan.detachedEdges.Any(e=>e.sector==city))continue;
                SetLocalGrading(city);
                var left=new HashSet<Edge>(plan.detachedEdges.Where(e=>e.sector==city));
                while(left.Count>0&&attempts<settings.localRepairBudget)
                {
                    var group=new List<Edge>();var nodes=new HashSet<Node>();
                    var todo=new Queue<Edge>();var first=left.OrderBy(e=>e.a.id).ThenBy(e=>e.b.id).First();
                    todo.Enqueue(first);left.Remove(first);
                    while(todo.Count>0)
                    {
                        var e=todo.Dequeue();group.Add(e);nodes.Add(e.a);nodes.Add(e.b);
                        foreach(var next in left.Where(x=>nodes.Contains(x.a)||nodes.Contains(x.b)).ToList()){left.Remove(next);todo.Enqueue(next);}
                    }
                    groups++;
                    string groupLabel="Interior group "+groups+" ("+group.Count+" segments, first node "+first.a.id+")";
                    if(group.Any(e=>!LocalProfileSafe(e.points))){profileReject++;plan.notes.Add(groupLabel+": profile rejected.");continue;}
                    if(group.Any(e=>!RouteClear(e.points,CityBounds(city))||StreetCrosses(e.a,e.b,e.points,index)||!StreetAngles(e.a,e.b,e.points,index)))
                    {geometryReject++;plan.notes.Add(groupLabel+": geometry/protection rejected.");continue;}
                    var groupQuality=Measure(plan,settings);
                    var savedPoints=group.ToDictionary(e=>e,e=>new List<Vector3>(e.points));
                    var savedPositions=nodes.ToDictionary(n=>n,n=>n.position);
                    void RestoreGroupGeometry()
                    {
                        foreach(var item in savedPositions)item.Key.position=item.Value;
                        foreach(var item in savedPoints)item.Key.points=new List<Vector3>(item.Value);
                    }
                    float groupSeverity()=>group.Sum(e=>VerticalStats(e.points,1f/Mathf.Max(1f,settings.verticalRadius)).severity);
                    int groupRadiusFlags()=>group.Count(e=>!RadiusOk(e.points,e.kind));
                    float beforeSeverity=groupSeverity();
                    int beforeRadius=groupRadiusFlags();
                    // Detached geometry is not part of the retained reference. Prepare
                    // only this group, with shared heights and locked external nodes.
                    // If preparation is invalid, try the original shape instead.
                    bool prepared=false;
                    try
                    {
                        OptimizeSharedProfiles(Mathf.Max(16,settings.profileSweeps),true,group);
                        RefineLocalRoads(1,group);
                        RefineLocalRoads(2,group);
                        var checkIndex=new StreetIndex();
                        foreach(var e in plan.edges.Concat(plan.authoredLinks))checkIndex.Add(e);
                        foreach(var e in group)checkIndex.Add(e);
                        bool valid=group.All(e=>e.points[0].Equals(e.a.position)&&e.points[e.points.Count-1].Equals(e.b.position)&&LocalProfileSafe(e.points)&&RouteClear(e.points,CityBounds(city)));
                        foreach(var e in group)
                        {
                            checkIndex.Remove(e);
                            valid&=StreetAngles(e.a,e.b,e.points,checkIndex)&&!StreetCrosses(e.a,e.b,e.points,checkIndex);
                            checkIndex.Add(e);
                        }
                        prepared=valid&&groupRadiusFlags()<=beforeRadius;
                        if(!prepared)RestoreGroupGeometry();
                    }
                    catch(InvalidOperationException ex)
                    {
                        RestoreGroupGeometry();
                        plan.notes.Add(groupLabel+": preparation fallback: "+ex.Message);
                    }
                    plan.notes.Add(groupLabel+": preparation "+(prepared?"VALID":"ORIGINAL")+
                        "; radius flags "+beforeRadius+" -> "+groupRadiusFlags()+
                        ", vertical severity "+beforeSeverity.ToString("F5")+" -> "+groupSeverity().ToString("F5")+".");
                    // Snapshot the lists, not the whole scene or sampled geometry. Trial
                    // connections only change membership and degrees, never old retained paths.
                    var oldEdges=plan.edges.ToList();
                    var oldDetached=plan.detachedEdges.ToList();
                    int groupStartAttempts=attempts,groupStartMeasured=measured;
                    int groupStartTerrain=terrainReject,groupStartQuality=qualityReject,groupStartGraph=graphReject;
                    int noPath=0,profileFailures=0,radiusFailures=0,clearanceFailures=0,angleFailures=0,crossFailures=0;
                    QualityReport bestRejected=null;
                    float bestRejectedCost=float.PositiveInfinity;
                    // Detached degrees are diagnostic and may be stale after pruning.
                    var oldDegrees=nodes.ToDictionary(n=>n,n=>n.degree);
                    foreach(var n in nodes)n.degree=index.At(n).Count()+group.Count(e=>e.a==n||e.b==n);
                    foreach(var e in group)index.Add(e);
                    bool success=false;
                    try
                    {
                        var options=new List<(Edge edge,int sample,Node source,float cost)>();
                        foreach(var b in nodes.Where(n=>n.degree<DegreeLimit(n)&&!n.gate))
                        {
                            var pos=XZ(b.position);
                            foreach(var e in index.Query(new Rect(pos.x-450,pos.y-450,900,900)).Where(e=>plan.edges.Contains(e)&&!e.Bridge&&e.sector==city))
                            {
                                float endMargin=RoadClearance*3f;
                                for(int i=1;i<e.points.Count-1;i+=3)
                                {
                                    var v=e.points[i];
                                    if(Vector2.Distance(XZ(v),XZ(e.a.position))<endMargin||Vector2.Distance(XZ(v),XZ(e.b.position))<endMargin)continue;
                                    float d=Vector2.Distance(pos,XZ(v));if(d<endMargin||d>450f||Mathf.Abs(v.y-b.position.y)>d*settings.grade*1.7f)continue;
                                    options.Add((e,i,b,d+Mathf.Abs(v.y-b.position.y)*5f));
                                }
                            }
                        }
                        // Prefer healthy host edges, but do not rule out a split of
                        // a flagged edge if its full-network metrics still pass.
                        var bestCost=float.PositiveInfinity;
                        float bestSeverity=float.PositiveInfinity;
                        Edge bestOld=null,bestA=null,bestB=null,bestConnector=null;
                        Node bestJunction=null,bestSource=null;
                        QualityReport bestQuality=null;
                        int measuredInGroup=0;
                        int routedInGroup=0;
                        var hostHealthy=options.Select(x=>x.edge).Distinct().ToDictionary(e=>e,e=>RadiusOk(e.points,e.kind));
                        var nearest=options.OrderBy(x=>x.cost).ThenBy(x=>x.edge.a.id).ThenBy(x=>x.edge.b.id).ThenBy(x=>x.sample).ThenBy(x=>x.source.id).ToList();
                        // Keep a nearest-first lane: healthy-first alone can starve
                        // viable hosts. Then diversify source/host pairs before
                        // using remaining slots on nearby samples of the same host.
                        var diverse=nearest.GroupBy(x=>(x.source,x.edge)).Select(g=>g.First())
                            .OrderBy(x=>hostHealthy[x.edge]?0:1).ThenBy(x=>x.cost);
                        var ordered=nearest.Take(16).Concat(diverse).Concat(nearest).Distinct().Take(64);
                        foreach(var item in ordered)
                        {
                            // Bound full city remeasurement independently of cheap
                            // connector attempts. Continue after the first viable route.
                            if(attempts>=settings.localRepairBudget||measuredInGroup>=12)break;
                            attempts++;
                            if(attempts%12==0&&EditorUtility.DisplayCancelableProgressBar("V7.2","Interior junction attempt "+attempts,.65f))throw new OperationCanceledException();
                            var old=item.edge;
                            var junction=new Node{id=plan.nodes.Max(n=>n.id)+1,position=old.points[item.sample],sector=city,degree=2};
                            var a=new Edge{a=old.a,b=junction,kind=old.kind,sector=city,spine=old.spine,points=old.points.Take(item.sample+1).ToList()};
                            var b=new Edge{a=junction,b=old.b,kind=old.kind,sector=city,spine=old.spine,points=old.points.Skip(item.sample).ToList()};
                            index.Remove(old);index.Add(a);index.Add(b);
                            List<Vector3> path=null;
                            try
                            {
                                if(!TryStreet(item.source,junction,CityBounds(city),RoadKind.Collector,index,false,out path)&&routed<12&&routedInGroup<6&&settings.terrainRepair)
                                {
                                    int beforeRouted=routed;
                                    TryRepairRoute(item.source,junction,CityBounds(city),index,ref routed,out path);
                                    routedInGroup+=routed-beforeRouted;
                                }
                                if(path==null){noPath++;connectorReject++;continue;}
                                if(!LocalProfileSafe(path)){profileFailures++;connectorReject++;continue;}
                                if(!RadiusOk(path,RoadKind.Collector)){radiusFailures++;connectorReject++;continue;}
                                if(!RouteClear(path,CityBounds(city))){clearanceFailures++;connectorReject++;continue;}
                                if(!StreetAngles(item.source,junction,path,index)){angleFailures++;connectorReject++;continue;}
                                if(StreetCrosses(item.source,junction,path,index)){crossFailures++;connectorReject++;continue;}
                                var connector=new Edge{a=item.source,b=junction,sector=city,kind=RoadKind.Collector,points=path};
                                int at=plan.edges.IndexOf(old);
                                plan.edges[at]=a;plan.edges.Insert(at+1,b);
                                plan.nodes.Add(junction);junction.degree++;item.source.degree++;
                                foreach(var e in group){plan.detachedEdges.Remove(e);plan.edges.Add(e);}
                                plan.edges.Add(connector);
                                try
                                {
                                    measured++;measuredInGroup++;
                                    var q=Measure(plan,settings);
                                    float measuredCost=BalancedCost(q,settings);
                                    if(measuredCost<bestRejectedCost){bestRejectedCost=measuredCost;bestRejected=q;}
                                    if(q.ankaraTiles<groupQuality.ankaraTiles||q.istanbulTiles<groupQuality.istanbulTiles||
                                        (groupQuality.bridgeGraphConnected&&!q.bridgeGraphConnected)||
                                        q.bridgeGap+.01f<groupQuality.bridgeGap||NetworkIdentityIssue(plan)!=null)
                                    {graphReject++;continue;}
                                    if(!NoTerrainRegression(groupQuality,q,settings)||!NoTerrainRegression(stageQuality,q,settings))
                                    {terrainReject++;continue;}
                                    if(!NoLocalQualityRegression(groupQuality,q)||!NoLocalQualityRegression(stageQuality,q))
                                    {qualityReject++;continue;}
                                    // Actual candidate quality, not endpoint distance, decides.
                                    float cost=BalancedCost(q,settings);
                                    if(bestOld==null||cost<bestCost-.000001f||
                                        Mathf.Abs(cost-bestCost)<=.000001f&&q.verticalSeverity<bestSeverity)
                                    {
                                        bestCost=cost;bestSeverity=q.verticalSeverity;bestQuality=q;
                                        bestOld=old;bestA=a;bestB=b;bestConnector=connector;
                                        bestJunction=junction;bestSource=item.source;
                                    }
                                }
                                finally
                                {
                                    item.source.degree--;junction.degree--;plan.nodes.Remove(junction);
                                    plan.edges.Clear();plan.edges.AddRange(oldEdges);
                                    plan.detachedEdges.Clear();plan.detachedEdges.AddRange(oldDetached);
                                }
                            }
                            finally{index.Remove(a);index.Remove(b);index.Add(old);}
                        }
                        if(bestOld!=null)
                        {
                            int at=plan.edges.IndexOf(bestOld);plan.edges[at]=bestA;plan.edges.Insert(at+1,bestB);
                            plan.nodes.Add(bestJunction);bestJunction.degree++;bestSource.degree++;
                            foreach(var e in group){plan.detachedEdges.Remove(e);plan.edges.Add(e);}
                            plan.edges.Add(bestConnector);
                            index.Remove(bestOld);index.Add(bestA);index.Add(bestB);index.Add(bestConnector);
                            splits++;promoted+=group.Count;success=true;
                        }
                        plan.notes.Add(groupLabel+": "+(success?"ACCEPTED; cut/fill "+bestQuality.meanCut.ToString("F3")+"/"+bestQuality.meanFill.ToString("F3")+" m, extra "+(bestQuality.extraFraction*100f).ToString("F2")+"%, radius flags "+bestQuality.sharpRoads:"NO SAFE CANDIDATE")+
                            "; attempts "+(attempts-groupStartAttempts)+", measured "+(measured-groupStartMeasured)+
                            ", terrain rejects "+(terrainReject-groupStartTerrain)+", quality rejects "+(qualityReject-groupStartQuality)+
                            ", graph rejects "+(graphReject-groupStartGraph)+".");
                        plan.notes.Add(groupLabel+": connector rejects by first failed check: no path "+noPath+", profile "+profileFailures+", radius "+radiusFailures+
                            ", clearance "+clearanceFailures+", angle "+angleFailures+", crossing "+crossFailures+".");
                        if(!success&&bestRejected!=null)
                            plan.notes.Add(groupLabel+": lowest measured earthwork cost candidate (not accepted): cut/fill "+bestRejected.meanCut.ToString("F4")+"/"+bestRejected.meanFill.ToString("F4")+
                                " m, extra "+(bestRejected.extraFraction*100f).ToString("F3")+"%, radius flags "+bestRejected.sharpRoads+
                                ", vertical severity "+bestRejected.verticalSeverity.ToString("F5")+", junction flags "+bestRejected.junctionGradeFlags+".");
                    }
                    finally
                    {
                        if(!success)
                        {
                            foreach(var e in group)index.Remove(e);
                            foreach(var n in nodes)n.degree=oldDegrees[n];
                            RestoreGroupGeometry();
                        }
                    }
                }
            }
            gradingSurface=null;
            plan.notes.Add("Interior T-junction repair: "+splits+" preview splits, "+promoted+" promoted segments, "+attempts+" candidates, "+routed+" routed attempts; groups "+groups+", profile rejected "+profileReject+", geometry/protection rejected "+geometryReject+", connector rejects "+connectorReject+", measured "+measured+", terrain rejects "+terrainReject+", quality rejects "+qualityReject+", graph rejects "+graphReject+". Independent group trials; no spline asset split.");
        }

        private void RelocateBridgeGate()
        {
            var bridges=plan.edges.Where(e=>e.Bridge).ToList();if(bridges.Count!=2)return;
            var l=plan.layout;
            float Along(Vector3 v)=>l.rowsOnX?v.x:v.z;
            float Across(Vector3 v)=>l.rowsOnX?v.z:v.x;
            float Gap(List<Vector3> a,List<Vector3> b)=>Mathf.Max(0f,Mathf.Max(a.Min(Across)-b.Max(Across),b.Min(Across)-a.Max(Across)));
            var index=new StreetIndex();foreach(var e in plan.edges.Concat(plan.authoredLinks))index.Add(e);
            int attempts=0,valid=0,protectedReject=0,moved=0;
            foreach(var old in bridges)
            {
                if(attempts>=settings.localRepairBudget)break;
                var other=plan.edges.First(e=>e.Bridge&&e!=old);
                float originalGap=Gap(old.points,other.points),bestGap=originalGap;
                Edge best=null;
                // New gates must be real dead-end generated road nodes. Never infer
                // permission to traverse Kizilay from a nearby authored spline.
                var low=plan.edges.Where(e=>!e.Bridge).SelectMany(e=>new[]{e.a,e.b}).Distinct()
                    .Where(n=>Contains(l.low,XZ(n.position),-RoadClearance)&&l.AlongMax(l.low)-Along(n.position)<900f&&
                        index.At(n).Count()==1&&!n.gate&&!IsProtected(XZ(n.position),RoadClearance)).ToList();
                var high=plan.edges.Where(e=>!e.Bridge).SelectMany(e=>new[]{e.a,e.b}).Distinct()
                    .Where(n=>Contains(l.high,XZ(n.position),-RoadClearance)&&Along(n.position)-l.AlongMin(l.high)<900f&&
                        index.At(n).Count()==1&&!n.gate&&!IsProtected(XZ(n.position),RoadClearance)).ToList();
                float otherMin=other.points.Min(Across),otherMax=other.points.Max(Across);
                var pairs=low.SelectMany(a=>high.Where(b=>Mathf.Abs(Across(a.position)-Across(b.position))<700f).Select(b=>(a,b,
                    gap:Mathf.Max(Mathf.Min(Across(a.position),Across(b.position))-otherMax,otherMin-Mathf.Max(Across(a.position),Across(b.position))))))
                    .Where(x=>x.gap>originalGap+10f).OrderByDescending(x=>x.gap).ThenBy(x=>x.a.id).ThenBy(x=>x.b.id).Take(60).ToList();
                index.Remove(old);
                try
                {
                    foreach(var pair in pairs)
                    foreach(float bow in new[]{0f,100f,-100f})
                    {
                        if(attempts>=settings.localRepairBudget)break;
                        attempts++;
                        if(attempts%12==0&&EditorUtility.DisplayCancelableProgressBar("V7.2","Bridge gate candidates "+attempts,.73f))throw new OperationCanceledException();
                        if(bow==0f&&protectedAreas.Any(r=>SegmentHitsRect(XZ(pair.a.position),XZ(pair.b.position),r,RoadClearance))){protectedReject++;continue;}
                        if(!TryBridge(XZ(pair.a.position),XZ(pair.b.position),out List<Vector3> path,out BridgeRejection rejection,
                            settings.ankaraLow?bow:0f,settings.ankaraLow?0f:bow,pair.a.position.y,pair.b.position.y))
                        {if(rejection==BridgeRejection.ProtectedDistrict)protectedReject++;continue;}
                        bool EndOk(Node n,Vector2 direction)=>index.At(n).All(e=>Vector2.Angle(direction,
                            n==e.a?XZ(e.points[1]-e.points[0]):XZ(e.points[e.points.Count-2]-e.points[e.points.Count-1]))>=100f);
                        if(!EndOk(pair.a,XZ(path[1]-path[0]))||!EndOk(pair.b,XZ(path[path.Count-2]-path[path.Count-1]))||
                            !RadiusOk(path,RoadKind.Arterial)||StreetCrosses(pair.a,pair.b,path,index))continue;
                        valid++;float gap=Gap(path,other.points);if(gap<=bestGap+.01f)continue;
                        bestGap=gap;best=new Edge{a=pair.a,b=pair.b,sector="Highways",kind=RoadKind.Bridge,spine=old.spine,points=path};
                    }
                    if(best!=null)
                    {
                        int at=plan.edges.IndexOf(old);plan.edges[at]=best;
                        old.a.degree--;old.b.degree--;old.a.gate=false;old.b.gate=false;
                        best.a.degree++;best.b.degree++;best.a.gate=true;best.b.gate=true;
                        index.Add(best);moved++;
                    }
                    else index.Add(old);
                }
                catch{if(best==null)index.Add(old);throw;}
            }
            plan.notes.Add("Alternative bridge gates: "+moved+" replaced crossings; "+attempts+" tests, "+valid+" feasible, "+protectedReject+" protected rejects. Only generated dead-end nodes; authored corridors still require explicit verified access design.");
        }

        private void FindAlternateParkAccess()
        {
            var index=new StreetIndex();foreach(var e in plan.edges.Where(e=>!e.Bridge))index.Add(e);
            int gained=0;
            foreach(var park in plan.openSpaces.Where(x=>!x.accessible))
            {
                Rect r=park.bounds;const float reach=65f;
                var gates=new List<Vector2>();
                for(int i=0;i<=6;i++)
                {
                    float f=i/6f;gates.Add(new Vector2(Mathf.Lerp(r.xMin,r.xMax,f),r.yMin));gates.Add(new Vector2(Mathf.Lerp(r.xMin,r.xMax,f),r.yMax));
                    gates.Add(new Vector2(r.xMin,Mathf.Lerp(r.yMin,r.yMax,f)));gates.Add(new Vector2(r.xMax,Mathf.Lerp(r.yMin,r.yMax,f)));
                }
                foreach(var edge in index.Query(Rect.MinMaxRect(r.xMin-reach,r.yMin-reach,r.xMax+reach,r.yMax+reach)).Where(e=>e.sector==park.sector))
                {
                    foreach(var point in edge.points)
                    {
                        foreach(var target in gates.OrderBy(x=>(x-XZ(point)).sqrMagnitude).Take(3))
                        {
                            if(Vector2.Distance(XZ(point),target)>reach||!ScreenAccess(point,target,edge,index,out float grade))continue;
                            if(plan.parcels.Any(l=>l.sector==park.sector&&SegmentHitsRect(XZ(point),target,l.bounds,1f)))continue;
                            park.accessible=true;park.accessGrade=grade;gained++;break;
                        }
                        if(park.accessible)break;
                    }
                    if(park.accessible)break;
                }
            }
            plan.warnings.RemoveAll(x=>x.StartsWith("Open spaces without screened access:"));
            int missing=plan.openSpaces.Count(x=>!x.accessible);
            if(missing>0)plan.warnings.Add("Open spaces without screened access: "+missing+" (amber overlay).");
            plan.notes.Add("Alternate park perimeter entrances: "+gained+" additional screened parks. No paths baked.");
        }

        // True junction transition curvature uses the same direction and denominator
        // as MeasureVertical. Negative value means not a straight-through pair.
        internal static float JunctionCurvature(Vector3 center,Vector3 a,Vector3 b)
        {
            Vector3 da=a-center,db=b-center;
            float dl=XZ(da).magnitude,dr=XZ(db).magnitude;
            if(dl<.001f||dr<.001f||Vector2.Dot(XZ(da)/dl,XZ(db)/dr)>-.7071f)return -1f;
            return Mathf.Abs(da.y/dl+db.y/dr)/Mathf.Max(1f,(dl+dr)*.5f);
        }
        private static Vector3 JunctionNeighbor(Node n,Edge e)=>n==e.a?e.points[1]:e.points[e.points.Count-2];
        private static (int count,float severity) JunctionStats(Node n,List<Edge> roads,float limit)
        {
            int count=0;float severity=0;
            for(int i=0;i<roads.Count;i++)for(int j=i+1;j<roads.Count;j++)
            {
                float k=JunctionCurvature(n.position,JunctionNeighbor(n,roads[i]),JunctionNeighbor(n,roads[j]));
                if(k>limit){count++;severity+=k-limit;}
            }
            return (count,severity);
        }
        private static bool JunctionImproved((int count,float severity) a,(int count,float severity) b)=>
            b.count<=a.count&&b.severity<=a.severity+.000001f&&(b.count<a.count||b.severity<a.severity-.000001f);

        // Coordinate descent over one shared endpoint and bounded approach windows.
        // Uses the same sample/midpoint bounds as the earlier shared profile solver.
        // Everything beyond the window, and all other nodes, remains immutable.
        private bool SolveJunctionWindow(Node node,List<Edge> roads,bool moveNode,float weight)
        {
            var vars=new List<HeightVar>();
            var paths=new Dictionary<Edge,HeightVar[]>();
            var terms=new Dictionary<HeightVar,List<(HeightVar a,HeightVar b,HeightVar c,float dl,float dr,float scale,bool junction)>>();
            bool SafeLocation(Vector2 p)=>!IsProtected(p,RoadClearance+settings.blend)&&!obstacles.Near(p,RoadClearance*2f+settings.blend);
            HeightVar New(Vector3 p,bool fixedHeight)
            {
                var v=new HeightVar{pos=XZ(p),y=p.y,fixedHeight=fixedHeight||!SafeLocation(XZ(p))};
                vars.Add(v);return v;
            }
            var shared=New(node.position,!moveNode);
            foreach(var e in roads)
            {
                var path=new HeightVar[e.points.Count];float distance=0f;
                for(int k=0;k<path.Length;k++)
                {
                    int i=e.a==node?k:path.Length-1-k;
                    Vector3 point=e.points[i];
                    if(k>0)distance+=Vector2.Distance(XZ(point),path[e.a==node?i-1:i+1].pos);
                    bool end=k==path.Length-1;
                    var v=k==0?shared:New(point,end||distance>40f);
                    Vector2 tangent=XZ(e.points[Mathf.Min(i+1,path.Length-1)]-e.points[Mathf.Max(i-1,0)]);
                    var ground=StageGround(XZ(point),tangent,out float lo,out float hi);
                    v.lo=Mathf.Max(v.lo,lo);v.hi=Mathf.Min(v.hi,hi);v.ground.AddRange(ground);path[i]=v;
                }
                for(int i=1;i<path.Length;i++)
                {
                    var a=path[i-1];var b=path[i];float len=Vector2.Distance(a.pos,b.pos);
                    if(len<.001f)return false;
                    Vector2 midpoint=(a.pos+b.pos)*.5f;
                    if(!SafeLocation(midpoint)){a.fixedHeight=true;b.fixedHeight=true;}
                    var ground=StageGround(midpoint,b.pos-a.pos,out float lo,out float hi);
                    var pair=new HeightPair{a=a,b=b,rise=len*settings.grade,length=len,lo=lo,hi=hi,midGround=ground};
                    a.adjacent.Add(pair);b.adjacent.Add(pair);
                }
                paths.Add(e,path);
            }
            void Term(HeightVar a,HeightVar b,HeightVar c,bool junction)
            {
                float dl=Vector2.Distance(a.pos,b.pos),dr=Vector2.Distance(c.pos,b.pos);
                if(dl<.001f||dr<.001f)return;
                float scale=junction?Mathf.Max(1f,(dl+dr)*.5f):(dl+dr)*.5f;
                foreach(var v in new[]{a,b,c}.Distinct())
                {
                    if(v.fixedHeight)continue;
                    if(!terms.TryGetValue(v,out var list))terms[v]=list=new List<(HeightVar,HeightVar,HeightVar,float,float,float,bool)>();
                    list.Add((a,b,c,dl,dr,scale,junction));
                }
            }
            foreach(var path in paths.Values)
                for(int i=1;i<path.Length-1;i++)Term(path[i-1],path[i],path[i+1],false);
            // Include fixed incident approaches too; eligible roads can fit them
            // without moving bridge/authored geometry or the locked shared node.
            var first=new List<HeightVar>();
            foreach(var e in plan.edges.Concat(plan.authoredLinks).Where(e=>e.a==node||e.b==node))
            {
                if(paths.TryGetValue(e,out var path))first.Add(path[e.a==node?1:path.Length-2]);
                else {var p=JunctionNeighbor(node,e);first.Add(new HeightVar{pos=XZ(p),y=p.y,fixedHeight=true});}
            }
            for(int i=0;i<first.Count;i++)for(int j=i+1;j<first.Count;j++)
                if(Vector2.Dot((first[i].pos-shared.pos).normalized,(first[j].pos-shared.pos).normalized)<=-.7071f)
                    Term(first[i],shared,first[j],true);
            float limit=1f/Mathf.Max(1f,settings.verticalRadius);
            bool moved=false;
            for(int pass=0;pass<12;pass++)
            for(int i=0;i<vars.Count;i++)
            {
                var v=vars[pass%2==0?i:vars.Count-1-i];
                if(v.fixedHeight||v.ground.Count==0||!terms.TryGetValue(v,out var bends))continue;
                float lo=v.lo,hi=v.hi;
                foreach(var pair in v.adjacent)
                {
                    float other=pair.Other(v).y;
                    lo=Mathf.Max(lo,Mathf.Max(other-pair.rise,2f*pair.lo-other));
                    hi=Mathf.Min(hi,Mathf.Min(other+pair.rise,2f*pair.hi-other));
                }
                if(lo>hi||v.y<lo-.002f||v.y>hi+.002f)continue;
                float Cost(float y)
                {
                    float total=0;
                    foreach(var pair in v.adjacent)
                    foreach(float h in pair.midGround)
                    {
                        float d=Mathf.Abs((y+pair.Other(v).y)*.5f-h);
                        total+=pair.length*(d+2f*Mathf.Max(0,d-settings.maxCutFill)+4f*Mathf.Max(0,d-settings.preferredGrading));
                    }
                    float Y(HeightVar x)=>x==v?y:x.y;
                    foreach(var t in bends)
                    {
                        float k=Mathf.Abs((Y(t.c)-Y(t.b))/t.dr-(Y(t.b)-Y(t.a))/t.dl)/t.scale;
                        total+=weight*(t.junction?3f:1f)*Mathf.Max(0,k-limit);
                    }
                    return total;
                }
                float left=lo,right=hi;
                for(int k=0;k<12;k++)
                {
                    float a=(2f*left+right)/3f,b=(left+2f*right)/3f;
                    if(Cost(a)<Cost(b))right=b;else left=a;
                }
                float next=(left+right)*.5f;
                if(Cost(next)+.0001f<Cost(v.y)&&Mathf.Abs(next-v.y)>.0001f){v.y=next;moved=true;}
            }
            if(!moved)return false;
            node.position=XYZ(shared.pos,shared.y);
            foreach(var entry in paths)
                entry.Key.points=entry.Value.Select(v=>XYZ(v.pos,v.y)).ToList();
            return true;
        }
        private static Dictionary<Node,List<Edge>> JunctionIncident(Plan p)=>p.edges.Concat(p.authoredLinks)
            .SelectMany(e=>new[]{(node:e.a,edge:e),(node:e.b,edge:e)})
            .GroupBy(x=>x.node).ToDictionary(g=>g.Key,g=>g.Select(x=>x.edge).Distinct().ToList());
        private static bool JunctionEditable(Edge e,HashSet<Edge> retained)=>e.sector=="Ankara"&&!e.Bridge&&retained.Contains(e)&&e.points.Count>2;
        private static List<int> SharedJunctionTargets(Plan p,Settings s)
        {
            var retained=new HashSet<Edge>(p.edges);
            float limit=1f/Mathf.Max(1f,s.verticalRadius);
            return JunctionIncident(p).Where(x=>x.Key.id>=0&&x.Value.Any(e=>JunctionEditable(e,retained)))
                .Select(x=>(node:x.Key,stats:JunctionStats(x.Key,x.Value,limit)))
                .Where(x=>x.stats.count>0).OrderByDescending(x=>x.stats.severity).ThenBy(x=>x.node.id)
                .Take(32).Select(x=>x.node.id).ToList();
        }
        private void FinishSharedJunctions(IReadOnlyList<int> targetIds)
        {
            var incident=JunctionIncident(plan);
            var detachedNodes=new HashSet<Node>(plan.detachedEdges.SelectMany(e=>new[]{e.a,e.b}));
            var retained=new HashSet<Edge>(plan.edges);
            bool Editable(Edge e)=>JunctionEditable(e,retained);
            float limit=1f/Mathf.Max(1f,settings.verticalRadius);
            var byId=incident.Keys.Where(n=>n.id>=0).ToDictionary(n=>n.id);
            var targets=targetIds.Where(byId.ContainsKey).Select(id=>byId[id]).ToList();
            var stageInput=Measure(plan,settings);var current=stageInput;
            int attempts=0,accepted=0,profileReject=0,qualityReject=0,noBenefit=0;
            foreach(var n in targets)
            {
                var all=incident[n];var roads=all.Where(Editable).ToList();
                var prior=JunctionStats(n,all,limit);if(prior.count==0||roads.Count==0)continue;
                var original=roads.ToDictionary(e=>e,e=>e.points);
                var position=n.position;
                bool allowMove=!n.gate&&!detachedNodes.Contains(n)&&all.All(Editable);
                Dictionary<Edge,List<Vector3>> best=null;Vector3 bestNode=position;
                QualityReport bestQuality=null;var bestStats=prior;
                foreach(bool move in allowMove?new[]{false,true}:new[]{false})
                {
                    attempts++;
                    if(EditorUtility.DisplayCancelableProgressBar("V7.2.6 city finish","Shared junction "+n.id+" / trial "+attempts,.88f))throw new OperationCanceledException();
                    try
                    {
                        if(!SolveJunctionWindow(n,roads,move,2400f)){noBenefit++;continue;}
                        if(roads.Any(e=>!e.points[0].Equals(e.a.position)||!e.points[e.points.Count-1].Equals(e.b.position)||!LocalProfileSafe(e.points)))
                        {profileReject++;continue;}
                        var stats=JunctionStats(n,all,limit);
                        if(!JunctionImproved(prior,stats)){noBenefit++;continue;}
                        var q=Measure(plan,settings);
                        if(!NoTerrainRegression(current,q,settings)||!NoTerrainRegression(stageInput,q,settings)||
                            !NoLocalQualityRegression(current,q)||!NoLocalQualityRegression(stageInput,q)||
                            q.verticalDefects>current.verticalDefects||q.verticalDefects>stageInput.verticalDefects||
                            q.junctionGradeFlags>current.junctionGradeFlags||q.score+.0001f<stageInput.score)
                        {qualityReject++;continue;}
                        if(best==null||stats.count<bestStats.count||stats.count==bestStats.count&&stats.severity<bestStats.severity)
                        {best=roads.ToDictionary(e=>e,e=>e.points);bestNode=n.position;bestQuality=q;bestStats=stats;}
                    }
                    catch(InvalidOperationException){profileReject++;}
                    finally{n.position=position;foreach(var entry in original)entry.Key.points=entry.Value;}
                }
                if(best!=null)
                {
                    n.position=bestNode;foreach(var entry in best)entry.Key.points=entry.Value;
                    current=bestQuality;accepted++;
                    plan.notes.Add("Junction "+n.id+" "+n.position.ToString("F1")+": "+prior.count+" -> "+bestStats.count+
                        " flags; excess curvature "+prior.severity.ToString("F5")+" -> "+bestStats.severity.ToString("F5")+
                        "; "+roads.Count+" approaches, node Y shift "+(bestNode.y-position.y).ToString("F3")+" m.");
                }
            }
            plan.notes.Add("Shared junction finish: "+accepted+"/"+targets.Count+" target junctions improved locally; "+attempts+
                " trials, "+profileReject+" profile rejects, "+qualityReject+" quality rejects, "+noBenefit+
                " no-benefit; junction flags "+stageInput.junctionGradeFlags+" -> "+current.junctionGradeFlags+
                ", vertical "+stageInput.verticalDefects+" -> "+current.verticalDefects+". Final city/virtual guards still apply.");
        }
        // Do not trade a better vertical profile for worse virtual ground. New
        // close cells are forbidden, and existing close-cell deltas cannot grow.
        internal static bool NoVirtualRegression(VirtualTerrainReport a,VirtualTerrainReport b)
        {
            if(a==null||b==null||a.topologySchema!=1||b.topologySchema!=1||a.issues==null||b.issues==null||
                a.omittedIssues>0||b.omittedIssues>0||a.overflowCells>0||b.overflowCells>0)return false;
            if(b.sections!=a.sections||b.missingSections>a.missingSections||b.missingSamples>a.missingSamples||
                b.missingCenterSections>a.missingCenterSections||b.missingFootprintSections>a.missingFootprintSections||
                b.protectedSamples>a.protectedSamples||b.parcelSamples>a.parcelSamples||b.steepSections>a.steepSections||
                b.conflictingCells>a.conflictingCells||b.diagnosticCells>a.diagnosticCells||b.closeSampleCells>a.closeSampleCells||
                b.unrelatedOnlyCells+b.mixedTopologyCells>a.unrelatedOnlyCells+a.mixedTopologyCells||
                b.maxConflictDelta>a.maxConflictDelta+.001f||b.maxShoulderSlope>a.maxShoulderSlope+.0001f)return false;
            var prior=a.issues.Where(x=>x.minSeparation>=0&&x.minSeparation<=.5f&&
                (int)x.kind<=2).ToDictionary(x=>x.cell,x=>x.maxCloseDelta);
            return b.issues.Where(x=>x.minSeparation>=0&&x.minSeparation<=.5f&&(int)x.kind<=2)
                .All(x=>prior.TryGetValue(x.cell,out float delta)&&x.maxCloseDelta<=delta+.001f);
        }

        private void BuildLocalDiagnostics()
        {
            plan.hotspots.Clear();plan.verticalFlags.Clear();
            float limit=1f/settings.verticalRadius;
            foreach(var e in plan.edges.Concat(plan.authoredLinks))
            {
                for(int i=1;i<e.points.Count;i++)
                {
                    Vector3 a=e.points[i-1],b=e.points[i];
                    if(!e.Bridge)
                    {
                        float delta=CrossAdjustment((a+b)*.5f,XZ(b-a));
                        if(delta>settings.preferredGrading&& !float.IsInfinity(delta))
                            plan.hotspots.Add(new AnalysisLine{a=a,b=b,value=delta});
                    }
                    if(i<e.points.Count-1)
                    {
                        float k=VerticalCurvature(a,b,e.points[i+1]);
                        if(k>limit&&!float.IsInfinity(k))plan.verticalFlags.Add(new AnalysisLine{a=a,b=e.points[i+1],value=k});
                    }
                }
            }
            plan.notes.Add("Short local stubs: "+plan.quality.shortStubs+" retained (no automatic demolition); inspect their access value before pruning.");
            plan.notes.Add("Local diagnostics: "+plan.hotspots.Count+" sampled intervals above preferred grading; "+plan.quality.verticalDefects+" vertical curvature flags and "+plan.quality.junctionGradeFlags+" straight-through junction pairs.");
            foreach(var h in plan.hotspots.OrderByDescending(x=>x.value).Take(10))
                plan.notes.Add("Hotspot "+((h.a+h.b)*.5f).ToString("F1")+": "+h.value.ToString("F2")+" m adjustment.");
        }

        private void BuildVirtualTerrain()
        {
            plan.virtualSections.Clear();
            var report=new VirtualTerrainReport{diagnosticSchema=1,topologySchema=1,issues=new List<VirtualIssue>()};plan.virtualReport=report;
            var roads=plan.edges.Where(e=>!e.Bridge).ToList();
            float spacing=Mathf.Max(settings.virtualSectionSpacing,roads.Sum(e=>Length(e.points))/12000f);
            report.spacing=spacing;
            float paved=settings.width*.5f,half=paved+settings.sidewalk,blend=Mathf.Max(.1f,settings.blend);
            float[] offsets={-half-blend,-half,-paved,0f,paved,half,half+blend};
            var cells=new Dictionary<Vector2Int,(float y,Edge owner,VirtualSection section)>();
            var conflicts=new HashSet<Vector2Int>();
            var evidence=new Dictionary<Vector2Int,VirtualCell>();
            void RecordCell(Vector2Int key,VirtualSample sample,Edge owner,VirtualSection section)
            {
                if(!evidence.TryGetValue(key,out var cell))evidence[key]=cell=new VirtualCell();
                if(cell.samples.Count<VirtualCellSampleLimit)cell.samples.Add((sample,owner,section));
                else {cell.overflow=true;report.overflowSamples++;section.flagged=true;}
            }
            var lots=new Dictionary<Vector2Int,List<UrbanArea>>();
            Vector2Int LotCell(Vector2 p)=>new Vector2Int(Mathf.FloorToInt(p.x/80f),Mathf.FloorToInt(p.y/80f));
            foreach(var lot in plan.parcels)
            {
                var lo=LotCell(lot.bounds.min);var hi=LotCell(lot.bounds.max);
                for(int x=lo.x;x<=hi.x;x++)for(int y=lo.y;y<=hi.y;y++)
                {
                    var key=new Vector2Int(x,y);
                    if(!lots.TryGetValue(key,out var list))lots[key]=list=new List<UrbanArea>();
                    list.Add(lot);
                }
            }
            int processed=0;
            foreach(var e in roads)
            {
                if(++processed%120==0&&EditorUtility.DisplayCancelableProgressBar("V7.2 virtual terrain","Cross sections "+processed+"/"+roads.Count,.94f))throw new OperationCanceledException();
                float next=0f,travel=0f,totalLength=Length(e.points);
                for(int i=1;i<e.points.Count;i++)
                {
                    Vector3 a=e.points[i-1],b=e.points[i];float len=Vector2.Distance(XZ(a),XZ(b));if(len<.001f)continue;
                    if(i==1)next=Mathf.Min(spacing*.5f,totalLength*.5f);
                    while(next<=travel+len)
                    {
                        float station=next;
                        Vector3 mid=Vector3.Lerp(a,b,Mathf.Clamp01((next-travel)/len));next+=spacing;
                        VirtualSample Sample(int kind,Vector3 pos,float ground,bool hasGround,string context=null)=>new VirtualSample{
                            roadA=e.a.id,roadB=e.b.id,kind=kind,position=pos,center=mid,distanceFromA=station,
                            distanceFromB=Mathf.Max(0f,totalLength-station),ground=ground,hasGround=hasGround,context=context};
                        void PointIssue(VirtualIssueKind kind,VirtualSample sample,string reason)
                        {
                            AddVirtualIssue(report,new VirtualIssue{kind=kind,position=sample.position,reason=reason,
                                samples=new List<VirtualSample>{sample}});
                        }
                        var tangent=XZ(b-a).normalized;var side=new Vector2(-tangent.y,tangent.x);
                        var section=new VirtualSection{original=new Vector3[7],proposed=new Vector3[7]};
                        int missingMask=SampleVirtualSection(plan.layout,mid,side,offsets,section);
                        if(missingMask!=0)
                        {
                            var missing=new List<VirtualSample>();
                            for(int j=0;j<7;j++)
                            {
                                if((missingMask&(1<<j))==0)continue;
                                Vector2 pos=XZ(mid)+side*offsets[j];
                                missing.Add(Sample(j,XYZ(pos,mid.y-.12f),0f,false,VirtualMissingContext(pos,report)));
                            }
                            report.missingSections++;
                            string reason=VirtualMissingReason(missingMask);
                            if(reason=="Center")report.missingCenterSections++;
                            else if(reason=="Road footprint")report.missingFootprintSections++;
                            else report.missingOuterSections++;
                            AddVirtualIssue(report,new VirtualIssue{kind=VirtualIssueKind.MissingSection,position=missing[0].position,
                                reason=reason+" missing (all failed points recorded; display Y is road reference)",samples=missing});
                            continue;
                        }
                        float leftSlope=Mathf.Abs(section.proposed[1].y-section.proposed[0].y)/blend,
                            rightSlope=Mathf.Abs(section.proposed[5].y-section.proposed[6].y)/blend;
                        report.maxShoulderSlope=Mathf.Max(report.maxShoulderSlope,Mathf.Max(leftSlope,rightSlope));
                        if(leftSlope>settings.shoulderSlopeLimit||rightSlope>settings.shoulderSlopeLimit)
                        {
                            section.flagged=true;report.steepSections++;
                            var points=new List<VirtualSample>();
                            foreach(int j in new[]{0,1,5,6})points.Add(Sample(j,section.proposed[j],section.original[j].y,true));
                            AddVirtualIssue(report,new VirtualIssue{kind=VirtualIssueKind.SteepSection,position=mid,
                                reason="Sampled shoulder grade exceeds limit; retaining-wall candidate only",leftSlope=leftSlope,rightSlope=rightSlope,samples=points});
                        }
                        for(int j=1;j<6;j++)
                        {
                            Vector2 pos=XZ(section.proposed[j]);float y=section.proposed[j].y;
                            if(Mathf.Abs(y-section.original[j].y)>.5f)
                            {
                                if(IsProtected(pos,1f)||obstacles.Near(pos,2f))
                                {
                                    report.protectedSamples++;section.flagged=true;
                                    PointIssue(VirtualIssueKind.ProtectedSample,Sample(j,section.proposed[j],section.original[j].y,true),"Protected / existing-road adjustment");
                                }
                                // Sparse shoulder midpoint screen for lot foundations.
                            }
                            var key=new Vector2Int(Mathf.FloorToInt(pos.x/4f),Mathf.FloorToInt(pos.y/4f));
                            RecordCell(key,Sample(j,section.proposed[j],section.original[j].y,true),e,section);
                            if(cells.TryGetValue(key,out var old)&&old.owner!=e&&Mathf.Abs(old.y-y)>.5f)
                            {conflicts.Add(key);old.section.flagged=true;section.flagged=true;}
                            else cells[key]=(y,e,section);
                        }
                        foreach(int j in new[]{0,6})
                        {
                            Vector3 outer=section.proposed[j],inner=section.proposed[j==0?1:5];
                            foreach(float f in new[]{.25f,.5f,.75f})
                            {
                                int kind=(j==0?7:10)+Mathf.RoundToInt(f*4f)-1;
                                Vector3 midpoint=Vector3.Lerp(inner,outer,f);Vector2 pos=XZ(midpoint);
                                if(!plan.layout.TryHeight(pos,out float ground))
                                {
                                    section.flagged=true;report.missingSamples++;
                                    PointIssue(VirtualIssueKind.MissingShoulder,Sample(kind,midpoint,0f,false,VirtualMissingContext(pos,report)),"Missing interior shoulder terrain");
                                    continue;
                                }
                                if(Mathf.Abs(midpoint.y-ground)<=.5f)continue;
                                var sample=Sample(kind,midpoint,ground,true);
                                if(IsProtected(pos,1f)||obstacles.Near(pos,2f))
                                {
                                    report.protectedSamples++;section.flagged=true;
                                    PointIssue(VirtualIssueKind.ProtectedSample,sample,"Protected / existing-road adjustment");
                                }
                                if(lots.TryGetValue(LotCell(pos),out var list)&&list.Any(l=>l.bounds.Contains(pos)))
                                {
                                    report.parcelSamples++;section.flagged=true;
                                    PointIssue(VirtualIssueKind.ParcelSample,sample,"Sampled shoulder intersects parcel bounds");
                                }
                                var key=new Vector2Int(Mathf.FloorToInt(pos.x/4f),Mathf.FloorToInt(pos.y/4f));
                                RecordCell(key,sample,e,section);
                                if(cells.TryGetValue(key,out var old)&&old.owner!=e&&Mathf.Abs(old.y-midpoint.y)>.5f)
                                {conflicts.Add(key);old.section.flagged=true;section.flagged=true;}
                                else cells[key]=(midpoint.y,e,section);
                            }
                        }
                        plan.virtualSections.Add(section);
                    }
                    travel+=len;
                }
            }
            // Keep the original owner algorithm and count exactly as before. Independently
            // compare all retained provenance in each cell, including overwritten owners.
            report.sections=plan.virtualSections.Count;report.conflictingCells=conflicts.Count;
            int examined=0;
            foreach(var entry in evidence.OrderBy(x=>x.Key.x).ThenBy(x=>x.Key.y))
            {
                if(++examined%4096==0&&EditorUtility.DisplayCancelableProgressBar("V7.2.5 virtual diagnostics","Classifying sample cells "+examined+"/"+evidence.Count,.97f))
                    throw new OperationCanceledException();
                var cell=entry.Value;bool legacy=conflicts.Contains(entry.Key),found=false,shared=false,independent=false,ambiguous=false;
                bool connectedPair=false,unrelatedPair=false;
                float maxDelta=0f,maxCloseDelta=0f,minSeparation=float.PositiveInfinity;
                int maxA=-1,maxB=-1,closestA=-1,closestB=-1;
                Vector3 focus=cell.samples[0].sample.position;
                for(int i=0;i<cell.samples.Count;i++)
                for(int j=i+1;j<cell.samples.Count;j++)
                {
                    var a=cell.samples[i];var b=cell.samples[j];
                    if(a.owner==b.owner||!VirtualHeightConflict(a.sample.position.y,b.sample.position.y))continue;
                    found=true;a.section.flagged=true;b.section.flagged=true;
                    float delta=Mathf.Abs(a.sample.position.y-b.sample.position.y);
                    if(delta>maxDelta){maxDelta=delta;maxA=i;maxB=j;focus=(a.sample.position+b.sample.position)*.5f;}
                    float separation=Vector2.Distance(XZ(a.sample.position),XZ(b.sample.position));
                    if(ShareVirtualNode(a.owner,b.owner))connectedPair=true;else unrelatedPair=true;
                    if(separation<=.5f)maxCloseDelta=Mathf.Max(maxCloseDelta,delta);
                    if(separation<minSeparation){minSeparation=separation;closestA=i;closestB=j;}
                    var kind=VirtualPairKind(a.owner,a.sample,b.owner,b.sample,half);
                    shared|=kind==VirtualIssueKind.SharedJunction;independent|=kind==VirtualIssueKind.IndependentRoad;
                    ambiguous|=kind==VirtualIssueKind.AmbiguousOverlap;
                }
                if(!legacy&&!found&&!cell.overflow)continue;
                report.diagnosticCells++;
                if(found&&!legacy)report.additionalConflictCells++;
                if(cell.overflow)report.overflowCells++;
                if(cell.overflow||!found)report.unknownTopologyCells++;
                else if(connectedPair&&unrelatedPair)report.mixedTopologyCells++;
                else if(connectedPair)report.connectedOnlyCells++;
                else report.unrelatedOnlyCells++;
                VirtualIssueKind classification=VirtualIssueKind.AmbiguousOverlap;
                if(!cell.overflow&&!ambiguous&&found&&shared&&!independent)classification=VirtualIssueKind.SharedJunction;
                else if(!cell.overflow&&!ambiguous&&found&&independent&&!shared)classification=VirtualIssueKind.IndependentRoad;
                if(classification==VirtualIssueKind.SharedJunction)report.sharedJunctionCells++;
                else if(classification==VirtualIssueKind.IndependentRoad)report.independentRoadCells++;
                else report.ambiguousCells++;
                if(found&&minSeparation<=.5f)report.closeSampleCells++;
                else if(found&&!cell.overflow)report.coarseOnlyCells++;
                else report.unknownProximityCells++;
                report.maxConflictDelta=Mathf.Max(report.maxConflictDelta,maxDelta);
                AddVirtualIssue(report,new VirtualIssue{kind=classification,position=focus,cell=entry.Key,legacyConflict=legacy,overflow=cell.overflow,
                    maxDelta=maxDelta,maxCloseDelta=maxCloseDelta,hasConnectedPair=connectedPair,hasUnrelatedPair=unrelatedPair,minSeparation=found?minSeparation:-1f,
                    maxSampleA=maxA,maxSampleB=maxB,closestSampleA=closestA,closestSampleB=closestB,
                    reason=cell.overflow?"Cell sample cap reached; unresolved":!found?"Legacy conflict without complete pair evidence":
                        minSeparation<=.5f?"Close sample height mismatch; not a mesh collision":"Coarse-cell alias possible; not cleared",
                    samples=cell.samples.Select(x=>x.sample).ToList()});
            }
            plan.notes.Add("Virtual terrain: "+report.sections+" sampled cross-sections at nominal "+spacing.ToString("F1")+" m, "+report.missingSections+" missing sections, "+report.missingSamples+" missing shoulder samples; "+report.conflictingCells+" incompatible 4m sample cells, "+report.steepSections+" steep shoulder sections (retaining-wall candidates), "+report.protectedSamples+" protected/existing-road adjustment samples, "+report.parcelSamples+" parcel shoulder samples. Not a continuous heightmap or bake.");
            plan.notes.Add(VirtualDiagnosticSummary(report));
        }
    }
}
