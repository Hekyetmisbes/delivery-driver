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
        private void RefineLocalRoads(int mode)
        {
            var index=new StreetIndex();foreach(var e in plan.edges.Concat(plan.authoredLinks))index.Add(e);
            var roads=plan.edges.Where(e=>!e.Bridge&&e.points.Count>3).Select(e=>(e,earth:RoadEarth(e.points),
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

        private void RepairWithInteriorJunctions()
        {
            var index=new StreetIndex();foreach(var e in plan.edges.Concat(plan.authoredLinks))index.Add(e);
            int attempts=0,promoted=0,splits=0,routed=0,groups=0,profileReject=0,geometryReject=0,connectorReject=0;
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
                    if(group.Any(e=>!LocalProfileSafe(e.points))){profileReject++;continue;}
                    if(group.Any(e=>!RouteClear(e.points,CityBounds(city))||StreetCrosses(e.a,e.b,e.points,index)||!StreetAngles(e.a,e.b,e.points,index)))
                    {geometryReject++;continue;}
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
                        foreach(var item in options.OrderBy(x=>x.cost).ThenBy(x=>x.edge.a.id).ThenBy(x=>x.sample).Take(64))
                        {
                            if(++attempts>settings.localRepairBudget)break;
                            if(attempts%12==0&&EditorUtility.DisplayCancelableProgressBar("V7.2","Interior junction attempt "+attempts,.65f))throw new OperationCanceledException();
                            var old=item.edge;
                            var junction=new Node{id=plan.nodes.Max(n=>n.id)+1,position=old.points[item.sample],sector=city,degree=2};
                            var a=new Edge{a=old.a,b=junction,kind=old.kind,sector=city,spine=old.spine,points=old.points.Take(item.sample+1).ToList()};
                            var b=new Edge{a=junction,b=old.b,kind=old.kind,sector=city,spine=old.spine,points=old.points.Skip(item.sample).ToList()};
                            index.Remove(old);index.Add(a);index.Add(b);
                            List<Vector3> path=null;
                            try
                            {
                                if(!TryStreet(item.source,junction,CityBounds(city),RoadKind.Collector,index,false,out path)&&routed<12&&settings.terrainRepair)
                                    TryRepairRoute(item.source,junction,CityBounds(city),index,ref routed,out path);
                                if(path==null||!LocalProfileSafe(path)||!RadiusOk(path,RoadKind.Collector)||!RouteClear(path,CityBounds(city))||
                                    !StreetAngles(item.source,junction,path,index)||StreetCrosses(item.source,junction,path,index))
                                {connectorReject++;continue;}
                                var connector=new Edge{a=item.source,b=junction,sector=city,kind=RoadKind.Collector,points=path};
                                int at=plan.edges.IndexOf(old);plan.edges[at]=a;plan.edges.Insert(at+1,b);
                                plan.nodes.Add(junction);junction.degree++;item.source.degree++;
                                foreach(var e in group){plan.detachedEdges.Remove(e);plan.edges.Add(e);}
                                plan.edges.Add(connector);index.Add(connector);splits++;promoted+=group.Count;success=true;
                                break;
                            }
                            finally{if(!success){index.Remove(a);index.Remove(b);index.Add(old);}}
                        }
                    }
                    finally{if(!success){foreach(var e in group)index.Remove(e);foreach(var n in nodes)n.degree=oldDegrees[n];}}
                }
            }
            gradingSurface=null;
            plan.notes.Add("Interior T-junction repair: "+splits+" preview splits, "+promoted+" promoted segments, "+attempts+" candidates, "+routed+" routed attempts; groups "+groups+", profile rejected "+profileReject+", geometry/protection rejected "+geometryReject+", connector rejects "+connectorReject+". No spline asset split.");
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
            var report=new VirtualTerrainReport();plan.virtualReport=report;
            var roads=plan.edges.Where(e=>!e.Bridge).ToList();
            float spacing=Mathf.Max(settings.virtualSectionSpacing,roads.Sum(e=>Length(e.points))/12000f);
            report.spacing=spacing;
            float paved=settings.width*.5f,half=paved+settings.sidewalk,blend=Mathf.Max(.1f,settings.blend);
            float[] offsets={-half-blend,-half,-paved,0f,paved,half,half+blend};
            var cells=new Dictionary<Vector2Int,(float y,Edge owner,VirtualSection section)>();
            var conflicts=new HashSet<Vector2Int>();
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
                float next=0f,travel=0f;
                for(int i=1;i<e.points.Count;i++)
                {
                    Vector3 a=e.points[i-1],b=e.points[i];float len=Vector2.Distance(XZ(a),XZ(b));if(len<.001f)continue;
                    if(i==1)next=Mathf.Min(spacing*.5f,Length(e.points)*.5f);
                    while(next<=travel+len)
                    {
                        Vector3 mid=Vector3.Lerp(a,b,Mathf.Clamp01((next-travel)/len));next+=spacing;
                        var tangent=XZ(b-a).normalized;var side=new Vector2(-tangent.y,tangent.x);
                        var section=new VirtualSection{original=new Vector3[7],proposed=new Vector3[7]};
                        bool missing=false;
                        for(int j=0;j<7;j++)
                        {
                            Vector2 pos=XZ(mid)+side*offsets[j];
                            if(!plan.layout.TryHeight(pos,out float ground)){missing=true;break;}
                            float target=j==0||j==6?ground:mid.y-.12f;
                            section.original[j]=XYZ(pos,ground);section.proposed[j]=XYZ(pos,target);
                        }
                        if(missing){report.missingSections++;continue;}
                        bool steep=Mathf.Abs(section.proposed[1].y-section.proposed[0].y)/blend>settings.shoulderSlopeLimit||
                            Mathf.Abs(section.proposed[5].y-section.proposed[6].y)/blend>settings.shoulderSlopeLimit;
                        if(steep){section.flagged=true;report.steepSections++;}
                        for(int j=1;j<6;j++)
                        {
                            Vector2 pos=XZ(section.proposed[j]);float y=section.proposed[j].y;
                            if(Mathf.Abs(y-section.original[j].y)>.5f)
                            {
                                if(IsProtected(pos,1f)||obstacles.Near(pos,2f)){report.protectedSamples++;section.flagged=true;}
                                // Sparse shoulder midpoint screen for lot foundations.
                            }
                            var key=new Vector2Int(Mathf.FloorToInt(pos.x/4f),Mathf.FloorToInt(pos.y/4f));
                            if(cells.TryGetValue(key,out var old)&&old.owner!=e&&Mathf.Abs(old.y-y)>.5f)
                            {conflicts.Add(key);old.section.flagged=true;section.flagged=true;}
                            else cells[key]=(y,e,section);
                        }
                        foreach(int j in new[]{0,6})
                        {
                            Vector3 outer=section.proposed[j],inner=section.proposed[j==0?1:5];
                            foreach(float f in new[]{.25f,.5f,.75f})
                            {
                                Vector3 midpoint=Vector3.Lerp(inner,outer,f);Vector2 pos=XZ(midpoint);
                                if(!plan.layout.TryHeight(pos,out float ground)){section.flagged=true;report.missingSamples++;continue;}
                                if(Mathf.Abs(midpoint.y-ground)<=.5f)continue;
                                if(IsProtected(pos,1f)||obstacles.Near(pos,2f)){report.protectedSamples++;section.flagged=true;}
                                if(lots.TryGetValue(LotCell(pos),out var list)&&list.Any(l=>l.bounds.Contains(pos)))
                                {report.parcelSamples++;section.flagged=true;}
                                var key=new Vector2Int(Mathf.FloorToInt(pos.x/4f),Mathf.FloorToInt(pos.y/4f));
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
            report.sections=plan.virtualSections.Count;report.conflictingCells=conflicts.Count;
            plan.notes.Add("Virtual terrain: "+report.sections+" sampled cross-sections at nominal "+spacing.ToString("F1")+" m, "+report.missingSections+" missing sections, "+report.missingSamples+" missing shoulder samples; "+report.conflictingCells+" incompatible 4m sample cells, "+report.steepSections+" steep shoulder sections (retaining-wall candidates), "+report.protectedSamples+" protected/existing-road adjustment samples, "+report.parcelSamples+" parcel shoulder samples. Not a continuous heightmap or bake.");
        }
    }
}
