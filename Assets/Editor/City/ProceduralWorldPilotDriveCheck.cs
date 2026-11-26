using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrafficSystem.Spline;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryDriver.EditorTools
{
    // Read-only geometry proof in the saved pilot scene. No Play Mode, prefab
    // instantiation, save/load systems, scene writes or global physics changes.
    internal static class ProceduralWorldPilotDriveCheck
    {
        private const string Menu="Tools/Delivery Driver/City/Validate Pilot Minivan Loop";
        private const string PrefabPath="Assets/Resources/Company/Vehicles/Minivan.prefab";
        private const float Spacing=.25f;
        private sealed class Vehicle
        {
            public Vector3 center,size;
            public Vector3[] wheels;
            public float radius,wheelbase;
            public float[] compressedContact,travel,target;
        }
        private sealed class Cycle
        {
            public List<SplineRoad> roads=new List<SplineRoad>();
            public List<RoadIntersection> nodes=new List<RoadIntersection>();
        }
        private static Vector2 XZ(Vector3 v)=>new Vector2(v.x,v.z);
        private static Vector3 XYZ(Vector2 p,float y)=>new Vector3(p.x,y,p.y);
        private static Vector2 Left(Vector2 d)=>new Vector2(-d.y,d.x);
        private static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;

        [MenuItem(Menu,false,2)]
        private static void Run()
        {
            try{Check();}
            catch(OperationCanceledException){Debug.Log("[ProceduralWorld] Pilot minivan check cancelled.");}
            catch(Exception ex){Debug.LogException(ex);}
            finally{EditorUtility.ClearProgressBar();}
        }
        [MenuItem(Menu,true)]
        private static bool CanRun()=>!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling;

        private static Vehicle ReadVehicle()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var car=prefab!=null?prefab.GetComponent<CarController>():null;
            if(car==null||car.GetComponent<Rigidbody>()==null||prefab.transform.localScale!=Vector3.one)
                throw new InvalidOperationException("Expected original company Minivan controller and unit scale.");
            var so=new SerializedObject(car);
            var simple=so.FindProperty("useSimpleBodyCollider");
            var center=so.FindProperty("simpleBodyColliderCenter");var size=so.FindProperty("simpleBodyColliderSize");
            var steer=so.FindProperty("maxSteeringAngle");
            if(simple==null||!simple.boolValue||center==null||size==null||steer==null||
                steer.floatValue<=0f||steer.floatValue>=89f)
                throw new InvalidOperationException("Minivan simplified body or steering configuration unavailable.");
            var wheels=new[]{car.frontLeftCollider,car.frontRightCollider,car.rearLeftCollider,car.rearRightCollider};
            if(wheels.Any(w=>w==null||w.radius<=0f||!w.enabled||w.suspensionDistance<=0f||
                w.suspensionSpring.targetPosition<0f||w.suspensionSpring.targetPosition>1f)||wheels.Distinct().Count()!=4)
                throw new InvalidOperationException("Four configured Minivan wheels with valid suspension required.");
            // This upright static envelope assumes vertical, unit-scale wheel
            // suspension axes. Reject unsupported rigs instead of guessing.
            if(wheels.Any(w=>(w.transform.lossyScale-Vector3.one).sqrMagnitude>.000001f||
                Vector3.Dot(w.transform.up,car.transform.up)<.99999f))
                throw new InvalidOperationException("Nonvertical or scaled Minivan wheel suspension unsupported.");
            Vector3[] positions=wheels.Select(w=>car.transform.InverseTransformPoint(w.transform.TransformPoint(w.center))).ToArray();
            float[] contacts=wheels.Select((w,i)=>positions[i].y-w.radius).ToArray();
            float[] travel=wheels.Select(w=>w.suspensionDistance).ToArray();
            float[] target=wheels.Select(w=>w.suspensionSpring.targetPosition).ToArray();
            // Match the existing controller's height normalization for this
            // vertical unit-scale rig, without changing a prefab or scene.
            var normalize=so.FindProperty("autoNormalizeWheelColliderHeights");
            float min=contacts.Min();
            if(normalize!=null&&normalize.boolValue&&contacts.Max()-min>=.005f)
                for(int i=0;i<contacts.Length;i++)
                    if(contacts[i]-min>.003f)contacts[i]=min;
            float wheelbase=Mathf.Abs((positions[0].z+positions[1].z-positions[2].z-positions[3].z)*.5f);
            // Conservative kinematic centerline approximation. Add body
            // length margin; not an Ackermann or suspension simulation.
            float radius=wheelbase/Mathf.Tan(steer.floatValue*Mathf.Deg2Rad)+size.vector3Value.z*.5f;
            if(wheelbase<.5f||radius<=0f||size.vector3Value.x<=0f||size.vector3Value.y<=0f||size.vector3Value.z<=0f)
                throw new InvalidOperationException("Invalid measured Minivan dimensions.");
            return new Vehicle{center=center.vector3Value,size=size.vector3Value,wheels=positions,
                radius=radius,wheelbase=wheelbase,compressedContact=contacts,travel=travel,target=target};
        }

        // Unity 6: targetPosition=0 fully extended, 1 fully compressed;
        // WheelCollider.center is the upper end of downward suspension travel.
        // https://docs.unity3d.com/6000.0/Documentation/ScriptReference/WheelCollider-suspensionSpring.html
        // Solve a common upright root height for all four sampled wheel
        // contacts. Nominal target is not a loaded static equilibrium.
        internal static bool TryPilotSuspensionPose(float[] surface,float[] compressed,float[] travel,float[] target,
            out float nominal,out float minRoot,out float maxRoot)
        {
            nominal=float.NegativeInfinity;minRoot=float.NegativeInfinity;maxRoot=float.PositiveInfinity;
            if(surface==null||compressed==null||travel==null||target==null||surface.Length!=4||
                compressed.Length!=4||travel.Length!=4||target.Length!=4)return false;
            for(int i=0;i<4;i++)
            {
                if(float.IsNaN(surface[i])||float.IsInfinity(surface[i])||float.IsNaN(compressed[i])||float.IsInfinity(compressed[i])||
                    float.IsNaN(travel[i])||float.IsInfinity(travel[i])||travel[i]<=0f||
                    float.IsNaN(target[i])||target[i]<0f||target[i]>1f)return false;
                float lower=surface[i]-compressed[i],upper=lower+travel[i];
                minRoot=Mathf.Max(minRoot,lower);maxRoot=Mathf.Min(maxRoot,upper);
                nominal=Mathf.Max(nominal,lower+travel[i]*(1f-target[i]));
            }
            if(minRoot>maxRoot+.00001f)return false;
            // Do not raise the vehicle to pass the body test. The common
            // target-derived pose must be inside actual travel for every wheel.
            return nominal>=minRoot-.00001f&&nominal<=maxRoot+.00001f;
        }
        private static List<Cycle> FindCycles(List<SplineRoad> roads)
        {
            var adj=roads.SelectMany(r=>new[]{(node:r.startIntersection,road:r),(node:r.endIntersection,road:r)})
                .GroupBy(p=>p.node).ToDictionary(g=>g.Key,g=>g.Select(p=>p.road).ToList());
            var found=new List<Cycle>();var signatures=new HashSet<string>();
            // For each edge, find an alternate shortest graph path. Open
            // terminals cannot be members of a cycle.
            foreach(var closing in roads)
            {
                var start=closing.startIntersection;var end=closing.endIntersection;
                var queue=new Queue<RoadIntersection>();queue.Enqueue(start);
                var seen=new HashSet<RoadIntersection>{start};
                var prev=new Dictionary<RoadIntersection,(RoadIntersection node,SplineRoad road)>();
                while(queue.Count>0&&!seen.Contains(end))
                {
                    var current=queue.Dequeue();
                    foreach(var road in adj[current])
                    {
                        if(road==closing)continue;
                        var next=road.startIntersection==current?road.endIntersection:road.startIntersection;
                        if(!seen.Add(next))continue;
                        prev[next]=(current,road);queue.Enqueue(next);
                    }
                }
                if(!seen.Contains(end))continue;
                var cycle=new Cycle();var n=end;cycle.nodes.Add(n);
                while(n!=start)
                {
                    var step=prev[n];cycle.roads.Add(step.road);n=step.node;cycle.nodes.Add(n);
                }
                cycle.nodes.Reverse();cycle.roads.Reverse();cycle.roads.Add(closing);
                if(cycle.roads.Count<3)continue;
                string signature=string.Join("|",cycle.roads.Select(r=>r.name).OrderBy(x=>x,StringComparer.Ordinal));
                if(signatures.Add(signature))found.Add(cycle);
            }
            return found.OrderBy(c=>c.roads.Count).ThenBy(c=>string.Join("|",c.roads.Select(r=>r.name)),StringComparer.Ordinal).ToList();
        }
        // Simplify only a candidate trajectory's reference polyline, never
        // the source spline. Keep both original road endpoints (junctions).
        // A bounded 3D segment-error test prevents shortcutting sharp bends
        // beyond the declared tolerance. Collider validation remains mandatory.
        internal static List<Vector3> SimplifyPilotRoad(List<Vector3> points,float tolerance)
        {
            if(points==null||points.Count<2||tolerance<0f||tolerance>1f)
                throw new InvalidOperationException("Invalid pilot trajectory simplification.");
            if(tolerance==0f)return new List<Vector3>(points);
            var keep=new bool[points.Count];keep[0]=keep[points.Count-1]=true;
            var pending=new Stack<(int first,int last)>();pending.Push((0,points.Count-1));
            float limit=tolerance*tolerance;
            while(pending.Count>0)
            {
                var range=pending.Pop();
                Vector3 a=points[range.first],d=points[range.last]-a;
                float len=d.sqrMagnitude,best=-1f;int split=-1;
                for(int i=range.first+1;i<range.last;i++)
                {
                    float t=len>.00000001f?Mathf.Clamp01(Vector3.Dot(points[i]-a,d)/len):0f;
                    float error=(points[i]-(a+d*t)).sqrMagnitude;
                    if(error>best){best=error;split=i;}
                }
                if(split>=0&&best>limit)
                {
                    keep[split]=true;
                    pending.Push((range.first,split));pending.Push((split,range.last));
                }
            }
            return points.Where((v,i)=>keep[i]).ToList();
        }
        private static List<Vector3> Anchors(Cycle cycle,float tolerance)
        {
            var points=new List<Vector3>();
            for(int j=0;j<cycle.roads.Count;j++)
            {
                var road=cycle.roads[j];bool forward=road.startIntersection==cycle.nodes[j];
                if(road.SegmentCount<1)throw new InvalidOperationException("Invalid pilot spline.");
                for(int k=0;k<road.ControlPointCount-1;k+=3)
                {
                    Vector3 a=road.GetControlPoint(k),b=road.GetControlPoint(k+3);
                    if(Vector3.Distance(road.GetControlPoint(k+1),Vector3.Lerp(a,b,1f/3f))>.001f||
                        Vector3.Distance(road.GetControlPoint(k+2),Vector3.Lerp(a,b,2f/3f))>.001f)
                        throw new InvalidOperationException("Nonlinear pilot spline requires a different trajectory sampler.");
                }
                var anchors=new List<Vector3>();
                for(int i=0;i<=road.SegmentCount;i++)
                {
                    int k=(forward?i:road.SegmentCount-i)*3;
                    anchors.Add(road.GetControlPoint(k));
                }
                var simplified=SimplifyPilotRoad(anchors,tolerance);
                if(points.Count>0)
                {
                    if(Vector3.Distance(points[points.Count-1],simplified[0])>.001f)
                        throw new InvalidOperationException("Cycle spline endpoints differ.");
                    points.AddRange(simplified.Skip(1));
                }
                else points.AddRange(simplified);
            }
            if(Vector3.Distance(points[0],points[points.Count-1])>.001f)
                throw new InvalidOperationException("Cycle is not closed.");
            points.RemoveAt(points.Count-1);return points;
        }
        // Sample a constant-radius fillet at each actual path corner. This is
        // a test trajectory, not a change to the road network or lane graph.
        internal static List<Vector3> RoundedLoop(List<Vector3> points,float radius)
        {
            int n=points.Count;if(n<3||radius<=0f)throw new InvalidOperationException("Invalid loop or radius.");
            var enter=new Vector3[n];var exit=new Vector3[n];var centers=new Vector2[n];var angles=new float[n];
            var trims=new float[n];var lengths=new float[n];
            for(int i=0;i<n;i++)
            {
                Vector3 a=points[(i+n-1)%n],b=points[i],c=points[(i+1)%n];
                Vector2 incoming=XZ(b-a),outgoing=XZ(c-b);
                float l=incoming.magnitude,r=outgoing.magnitude;lengths[i]=r;
                if(l<.001f||r<.001f)throw new InvalidOperationException("Duplicate loop anchors.");
                Vector2 u=incoming/l,v=outgoing/r;
                float angle=Mathf.Atan2(Cross(u,v),Vector2.Dot(u,v));
                enter[i]=exit[i]=b;
                if(Mathf.Abs(angle)<.0001f)continue;
                if(Mathf.Abs(angle)>Mathf.PI-.01f)throw new InvalidOperationException("Reversing loop corner.");
                float trim=radius*Mathf.Tan(Mathf.Abs(angle)*.5f);
                if(trim>=l||trim>=r)throw new InvalidOperationException("Insufficient length for Minivan turn radius at "+b.ToString("F2")+
                    " (angle "+(Mathf.Abs(angle)*Mathf.Rad2Deg).ToString("F2")+" deg, radius "+radius.ToString("F3")+
                    " m, required tangent "+trim.ToString("F3")+" m, incoming/outgoing "+l.ToString("F3")+"/"+r.ToString("F3")+" m)");
                trims[i]=trim;angles[i]=angle;
                enter[i]=Vector3.Lerp(b,a,trim/l);exit[i]=Vector3.Lerp(b,c,trim/r);
                centers[i]=XZ(enter[i])+Left(u)*(Mathf.Sign(angle)*radius);
            }
            var result=new List<Vector3>();
            for(int i=0;i<n;i++)
            {
                int next=(i+1)%n;
                if(trims[i]+trims[next]>=lengths[i]-.001f)
                    throw new InvalidOperationException("Adjacent Minivan turn fillets overlap between "+points[i].ToString("F2")+
                        " and "+points[next].ToString("F2")+" (required "+(trims[i]+trims[next]).ToString("F3")+
                        " m, available "+lengths[i].ToString("F3")+" m).");
                if(Mathf.Abs(angles[i])>.0001f)
                {
                    Vector2 radial=XZ(enter[i])-centers[i];
                    int count=Mathf.Max(1,Mathf.CeilToInt(radius*Mathf.Abs(angles[i])/Spacing));
                    for(int j=0;j<count;j++)
                    {
                        float t=j/(float)count,angle=angles[i]*t;
                        Vector2 p=centers[i]+radial*Mathf.Cos(angle)+Left(radial)*Mathf.Sin(angle);
                        result.Add(XYZ(p,Mathf.Lerp(enter[i].y,exit[i].y,t)));
                    }
                }
                int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(exit[i],enter[next])/Spacing));
                for(int j=0;j<steps;j++)result.Add(Vector3.Lerp(exit[i],enter[next],j/(float)steps));
                if(result.Count>20000)throw new InvalidOperationException("Pilot trajectory exceeds bounded sample budget.");
            }
            return result;
        }
        private static bool AsphaltHeight(List<MeshCollider> asphalt,Vector3 point,out float height)
        {
            height=float.NegativeInfinity;
            var ray=new Ray(point+Vector3.up*5f,Vector3.down);
            foreach(var c in asphalt)
                if(c.Raycast(ray,out RaycastHit hit,10f))height=Mathf.Max(height,hit.point.y);
            return !float.IsNegativeInfinity(height);
        }
        private static string CheckRoute(List<Vector3> path,Vehicle car,List<MeshCollider> asphalt,TerrainCollider ground,
            out int rays,out float length,out float maxSpread,out float minNominalClearance,out float minCompressedClearance)
        {
            rays=0;length=0;maxSpread=0;
            minNominalClearance=float.PositiveInfinity;minCompressedClearance=float.PositiveInfinity;
            var asphaltSet=new HashSet<Collider>(asphalt);
            for(int i=0;i<path.Count;i++)
            {
                if(i%100==0&&EditorUtility.DisplayCancelableProgressBar("Pilot Minivan loop","Static vehicle pose "+i+"/"+path.Count,i/(float)path.Count))
                    throw new OperationCanceledException();
                Vector3 p=path[i],previous=path[(i+path.Count-1)%path.Count],next=path[(i+1)%path.Count];
                length+=Vector2.Distance(XZ(p),XZ(next));
                Vector2 d=(XZ(next)-XZ(previous)).normalized;
                if(d.sqrMagnitude<.99f)return "Degenerate trajectory heading.";
                Quaternion rot=Quaternion.LookRotation(new Vector3(d.x,0,d.y),Vector3.up);
                if(!AsphaltHeight(asphalt,p,out float roadY)||Mathf.Abs(roadY-p.y-.03f)>.15f)
                    return "Centerline surface missing/deviates at "+p.ToString("F3");
                rays++;
                float lo=float.PositiveInfinity,hi=float.NegativeInfinity,bodySurface=float.NegativeInfinity;
                var wheelSurface=new float[4];
                string Probe(Vector3 local,int wheelIndex)
                {
                    Vector3 q=p+rot*new Vector3(local.x,0,local.z);
                    if(!AsphaltHeight(asphalt,q,out float y))
                        return "Vehicle footprint leaves asphalt at "+q.ToString("F3");
                    if(!ground.Raycast(new Ray(q+Vector3.up*5f,Vector3.down),out RaycastHit terrainHit,10f)||
                        terrainHit.point.y-y>.08f)return "Vehicle footprint ground clearance failed at "+q.ToString("F3");
                    if(wheelIndex>=0){lo=Mathf.Min(lo,y);hi=Mathf.Max(hi,y);wheelSurface[wheelIndex]=y;}
                    else bodySurface=Mathf.Max(bodySurface,y);
                    return null;
                }
                for(int w=0;w<car.wheels.Length;w++)
                {
                    string issue=Probe(car.wheels[w],w);if(issue!=null)return issue;rays++;
                }
                for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)
                {
                    string issue=Probe(car.center+new Vector3(x*car.size.x*.5f,0,z*car.size.z*.5f),-1);
                    if(issue!=null)return issue;rays++;
                }
                maxSpread=Mathf.Max(maxSpread,hi-lo);
                if(hi-lo>.15f)return "Wheel surface height spread exceeds 0.15m at "+p.ToString("F3");
                if(!TryPilotSuspensionPose(wheelSurface,car.compressedContact,car.travel,car.target,
                    out float nominal,out float lower,out float upper))
                    return "No common nominal suspension pose at "+p.ToString("F3")+
                        " (root min/target/max "+lower.ToString("F3")+"/"+nominal.ToString("F3")+"/"+upper.ToString("F3")+" m).";
                Vector3 root=new Vector3(p.x,nominal,p.z);
                Vector3 center=root+rot*car.center;
                float surface=Mathf.Max(roadY,bodySurface),bodyBottom=car.center.y-car.size.y*.5f;
                float clearance=nominal+bodyBottom-surface,compressedClearance=lower+bodyBottom-surface;
                minNominalClearance=Mathf.Min(minNominalClearance,clearance);
                minCompressedClearance=Mathf.Min(minCompressedClearance,compressedClearance);
                if(clearance<.01f)
                    return "Nominal static body underclearance at "+p.ToString("F3")+
                        " (clearance "+clearance.ToString("F3")+" m, minimum 0.010 m; compressed-envelope clearance "+
                        compressedClearance.ToString("F3")+" m, root min/target/max "+lower.ToString("F3")+"/"+
                        nominal.ToString("F3")+"/"+upper.ToString("F3")+" m).";
                var overlaps=Physics.OverlapBox(center,car.size*.5f,rot,~0,QueryTriggerInteraction.Ignore);
                foreach(var collider in overlaps)
                    if(!asphaltSet.Contains(collider))return "Vehicle body overlaps "+collider.name+" at "+p.ToString("F3");
            }
            return null;
        }
        private static void Check()
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||
                SceneManager.sceneCount!=1||scene.isDirty||!scene.path.StartsWith("Assets/ProceduralPilotTests/Istanbul_Buildings_",StringComparison.Ordinal)||
                scene.name!="PhysicalPilot")
                throw new InvalidOperationException("Open only a saved, unchanged V7.4 building pilot scene in Edit Mode.");
            string reportPath=Path.Combine(Path.GetDirectoryName(scene.path),"PilotReport.txt");
            if(!File.Exists(reportPath)||!File.ReadAllText(reportPath).StartsWith("V7.4 PHYSICAL BUILDING PILOT PASS: "+scene.path+";",StringComparison.Ordinal))
                throw new InvalidOperationException("Saved building pilot PASS report required.");
            var root=scene.GetRootGameObjects().SingleOrDefault(o=>o.name=="PW Physical Pilot - Neighborhood and Buildings");
            if(root==null)throw new InvalidOperationException("Expected generated pilot root missing.");
            var roads=root.GetComponentsInChildren<SplineRoad>(false).OrderBy(r=>r.name,StringComparer.Ordinal).ToList();
            if(roads.Count==0||roads.Any(r=>!r.IsUsable||r.startIntersection==null||r.endIntersection==null||
                r.startIntersection.gameObject.scene!=scene||r.endIntersection.gameObject.scene!=scene))
                throw new InvalidOperationException("Invalid saved pilot road graph.");
            var roadRoot=root.transform.Find("Roads and junction visuals");
            if(roadRoot==null)throw new InvalidOperationException("Pilot road visuals missing.");
            var asphalt=roadRoot.GetComponentsInChildren<MeshCollider>(false).Where(c=>c.name=="Asphalt"&&c.enabled&&
                c.gameObject.layer==LayerMask.NameToLayer("Road")&&c.sharedMesh!=null).ToList();
            var ground=root.GetComponentsInChildren<TerrainCollider>(false).Single();
            if(asphalt.Count<roads.Count||!ground.enabled)throw new InvalidOperationException("Pilot asphalt/terrain colliders missing.");
            var car=ReadVehicle();var cycles=FindCycles(roads);
            if(cycles.Count==0)throw new InvalidOperationException("No closed route in pilot graph; open terminals excluded.");
            Physics.SyncTransforms();
            var failures=new List<string>();int trials=0,constructed=0;
            // Exact original first, then progressively simplify redundant
            // dense samples on each edge, keeping junction positions fixed.
            // No radius, vehicle clearance or collider guard is relaxed.
            foreach(var cycle in cycles.Take(24))
            {
                string names=string.Join(", ",cycle.roads.Select(r=>r.name));
                var original=Anchors(cycle,0f);
                List<Vector3> previous=null;
                foreach(float tolerance in new[]{0f,.01f,.05f,.15f,.35f,.75f,1f})
                {
                    var anchors=tolerance==0f?original:Anchors(cycle,tolerance);
                    if(previous!=null&&anchors.SequenceEqual(previous))continue;
                    previous=anchors;trials++;
                    string label="trajectory reference "+anchors.Count+"/"+original.Count+" anchors, tolerance "+
                        tolerance.ToString("F2")+" m";
                    List<Vector3> path;
                    try{path=RoundedLoop(anchors,car.radius);}
                    catch(InvalidOperationException ex){failures.Add(names+" ["+label+"]: "+ex.Message);continue;}
                    constructed++;
                    string issue=CheckRoute(path,car,asphalt,ground,out int rays,out float length,out float spread,
                        out float nominalClearance,out float compressedClearance);
                    if(issue!=null){failures.Add(names+" ["+label+"]: "+issue);continue;}
                    Debug.Log("[ProceduralWorld] V7.5 MINIVAN STATIC LOOP PASS: "+scene.path+
                        "; prefab "+PrefabPath+"; body "+car.size.ToString("F2")+" m, wheelbase "+car.wheelbase.ToString("F2")+
                        " m, conservative turn radius "+car.radius.ToString("F2")+" m; closed route "+length.ToString("F1")+
                        " m, "+cycle.roads.Count+" roads, "+path.Count+" poses, "+rays+" asphalt probes, max wheel height spread "+
                        spread.ToString("F3")+" m; nominal minimum body clearance "+nominalClearance.ToString("F3")+
                        " m; minimum common compressed-envelope clearance "+compressedClearance.ToString("F3")+
                        " m; suspension travel/target "+string.Join(", ",car.travel.Select((v,i)=>v.ToString("F3")+"m/"+car.target[i].ToString("F2")))+
                        "; "+label+"; candidates tried/constructed "+trials+"/"+constructed+
                        "; route "+names+". Nominal upright static geometry only; compressed-envelope clearance is diagnostic, negative means potential bottoming. "+
                        "Not loaded equilibrium, lane routing, dynamic steering, braking, suspension, traffic or driving PASS. No scene/save changes.");
                    var view=SceneView.lastActiveSceneView;
                    if(view!=null){view.pivot=path[0];view.size=35f;view.Repaint();}
                    return;
                }
            }
            throw new InvalidOperationException("V7.5 MINIVAN STATIC LOOP BLOCKED: candidates tried/constructed "+
                trials+"/"+constructed+"; "+string.Join(" | ",failures)+". No road geometry changed; no Play Mode performed.");
        }
    }
}
