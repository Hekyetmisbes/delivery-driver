using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryDriver.EditorTools
{
    internal static partial class ProceduralWorldPilotDriveCheck
    {
        private const string DynamicMenu="Tools/Delivery Driver/City/Run Pilot Minivan Physics Test";
        private const string LoopMenu="Tools/Delivery Driver/City/Run Pilot Minivan Closed Loop Physics Test";
        private const float Step=.02f,TestMass=1500f,TargetSpeed=10f/3.6f;
        private sealed class DynamicStats
        {
            public string phase="setup";
            public int steps,groundedMin=4,groundSamples,stableSteps;
            public float clearance=float.PositiveInfinity,maxSpeed,maxForwardSpeed,maxLateral,maxTilt,distance,stopDistance,settledClearance;
            public float routeProgress,routeLength,maxSteer;
            public int curveSteps;
            public string Summary()=>"phase "+phase+", physics steps "+steps+", distance "+distance.ToString("F2")+
                " m, speed peak "+(maxSpeed*3.6f).ToString("F2")+" km/h, minimum grounded asphalt wheels after initial settling "+
                (groundSamples==0?"unmeasured":groundedMin+"/4")+" ("+groundSamples+" samples), min sampled body clearance "+
                (float.IsPositiveInfinity(clearance)?"unmeasured":clearance.ToString("F3")+" m")+
                ", maximum lateral deviation "+maxLateral.ToString("F3")+" m, max tilt "+maxTilt.ToString("F2")+" deg"+
                (routeLength>0f?", closed route progress "+routeProgress.ToString("F2")+"/"+routeLength.ToString("F2")+
                " m, peak front steering "+maxSteer.ToString("F2")+" deg, curve-speed steps "+curveSteps:"");
        }

        [MenuItem(DynamicMenu,false,3)]
        private static void RunDynamicMenu()
        {
            try{Check(true);}
            catch(OperationCanceledException){Debug.Log("[ProceduralWorld] V7.6 Minivan physics test cancelled; temporary objects removed.");}
            catch(Exception ex){Debug.LogException(ex);}
            finally{EditorUtility.ClearProgressBar();}
        }
        [MenuItem(DynamicMenu,true)]
        private static bool CanRunDynamics()=>CanRun();
        [MenuItem(LoopMenu,false,4)]
        private static void RunLoopMenu()
        {
            try{Check(true,true);}
            catch(OperationCanceledException){Debug.Log("[ProceduralWorld] V7.7 Minivan loop cancelled; temporary objects removed.");}
            catch(Exception ex){Debug.LogException(ex);}
            finally{EditorUtility.ClearProgressBar();}
        }
        [MenuItem(LoopMenu,true)]
        private static bool CanRunLoop()=>CanRun();

        // Unwrapped arc-distance tracking on the static-proven closed
        // polyline. Local search only after initialization; no jumping
        // across the loop to claim progress.
        internal sealed class PilotLoopTracker
        {
            private readonly List<Vector3> path;
            private readonly float[] cumulative;
            private int cursor;
            private bool initialized;
            public float Progress{get;private set;}
            public float Length=>cumulative[path.Count];
            public PilotLoopTracker(List<Vector3> points)
            {
                if(points==null||points.Count<3)throw new InvalidOperationException("Invalid dynamic loop path.");
                path=new List<Vector3>(points);cumulative=new float[path.Count+1];
                for(int i=0;i<path.Count;i++)
                {
                    float length=Vector2.Distance(XZ(path[i]),XZ(path[(i+1)%path.Count]));
                    if(length<.0001f)throw new InvalidOperationException("Duplicate dynamic loop sample.");
                    cumulative[i+1]=cumulative[i]+length;
                }
            }
            private int Index(int segment)=>((segment%path.Count)+path.Count)%path.Count;
            private float Station(int segment)
            {
                int index=Index(segment);
                return ((segment-index)/path.Count)*Length+cumulative[index];
            }
            public float Project(Vector3 position,bool update,out float station)
            {
                int first=initialized?cursor-16:0,last=initialized?cursor+16:path.Count-1,best=first;
                float distance=float.PositiveInfinity;station=0f;
                for(int i=first;i<=last;i++)
                {
                    int index=Index(i);Vector2 a=XZ(path[index]),d=XZ(path[(index+1)%path.Count])-a;
                    float t=Mathf.Clamp01(Vector2.Dot(XZ(position)-a,d)/d.sqrMagnitude);
                    float error=(XZ(position)-(a+d*t)).sqrMagnitude;
                    if(error<distance){distance=error;best=i;station=Station(i)+d.magnitude*t;}
                }
                if(update)
                {
                    if(initialized&&Mathf.Abs(station-Progress)>.5f)
                        throw new InvalidOperationException("Unexpected route progress jump "+(station-Progress).ToString("F3")+" m.");
                    initialized=true;cursor=best;Progress=station;
                }
                return Mathf.Sqrt(distance);
            }
            public Vector3 At(float station)
            {
                float local=Mathf.Repeat(station,Length);
                int i=Array.BinarySearch(cumulative,local);
                if(i<0)i=~i-1;
                i=Mathf.Clamp(i,0,path.Count-1);
                return Vector3.Lerp(path[i],path[(i+1)%path.Count],(local-cumulative[i])/(cumulative[i+1]-cumulative[i]));
            }
            public bool CurveAhead(float station)
            {
                // Check 8m ahead in 1m windows. Begin slowing before the
                // arc, not after steering has already begun.
                for(int i=0;i<8;i++)
                {
                    Vector2 a=XZ(At(station+i+1f)-At(station+i));
                    Vector2 b=XZ(At(station+i+2f)-At(station+i+1f));
                    if(Vector2.Angle(a,b)>2f)return true;
                }
                return false;
            }
        }

        // Find an already statically verified straight corridor. Leave five
        // metres at each end; never use an open boundary terminal.
        internal static bool TryPilotStraight(List<Vector3> path,out Vector3 start,out Vector3 direction)
        {
            start=direction=Vector3.zero;
            if(path==null||path.Count<2)return false;
            for(int i=0;i<path.Count;i++)
            {
                Vector3 a=path[i],b=path[(i+1)%path.Count];
                Vector2 d=XZ(b-a).normalized;
                if(d.sqrMagnitude<.99f)continue;
                for(int j=1;j<path.Count;j++)
                {
                    Vector3 q=path[(i+j)%path.Count];
                    Vector2 delta=XZ(q-a);float along=Vector2.Dot(delta,d);
                    if(along<0f||Mathf.Abs(Cross(d,delta))>.02f||Mathf.Abs(q.y-a.y)>.02f)break;
                    if(along>=45f)
                    {
                        direction=XYZ(d,0f);start=a+direction*5f;
                        return true;
                    }
                }
            }
            return false;
        }
        private static GameObject LocalObject(Scene staging,Scene target,string name)
        {
            if(SceneManager.GetActiveScene()!=staging)
                throw new InvalidOperationException("Physics staging scene is not active.");
            var obj=new GameObject(name);
            try{SceneManager.MoveGameObjectToScene(obj,target);}
            catch{UnityEngine.Object.DestroyImmediate(obj);throw;}
            if(obj.scene!=target){UnityEngine.Object.DestroyImmediate(obj);throw new InvalidOperationException("Local physics object moved to wrong scene.");}
            return obj;
        }
        private static void CopyCommon(Collider from,Collider to)
        {
            // Native WheelCollider friction is set via its dedicated curves.
            // Unity does not support assigning Collider.sharedMaterial here.
            if(!(to is WheelCollider))to.sharedMaterial=from.sharedMaterial;
            to.contactOffset=from.contactOffset;
            to.includeLayers=from.includeLayers;to.excludeLayers=from.excludeLayers;
            to.layerOverridePriority=from.layerOverridePriority;
            to.isTrigger=false;to.enabled=true;
        }
        private static Collider CloneStaticCollider(Collider source,Scene staging,Scene local)
        {
            if(source.attachedRigidbody!=null||source.isTrigger)
                throw new InvalidOperationException("Unexpected dynamic/trigger source collider "+source.name);
            Transform t=source.transform;
            var matrix=Matrix4x4.TRS(t.position,t.rotation,t.lossyScale);
            for(int i=0;i<16;i++)
                if(Mathf.Abs(matrix[i]-t.localToWorldMatrix[i])>.001f)
                    throw new InvalidOperationException("Cannot faithfully copy sheared collider "+source.name);
            var obj=LocalObject(staging,local,"Static "+source.name);
            obj.layer=source.gameObject.layer;
            obj.transform.SetPositionAndRotation(t.position,t.rotation);obj.transform.localScale=t.lossyScale;
            Collider result;
            if(source is MeshCollider mesh)
            {
                if(mesh.sharedMesh==null||mesh.convex)throw new InvalidOperationException("Unexpected source mesh collider "+mesh.name);
                var copy=obj.AddComponent<MeshCollider>();copy.cookingOptions=mesh.cookingOptions;copy.sharedMesh=mesh.sharedMesh;result=copy;
            }
            else if(source is TerrainCollider terrain)
            {
                if(terrain.terrainData==null)throw new InvalidOperationException("Missing pilot terrain data.");
                var copy=obj.AddComponent<TerrainCollider>();copy.terrainData=terrain.terrainData;result=copy;
            }
            else if(source is BoxCollider box)
            {
                var copy=obj.AddComponent<BoxCollider>();copy.center=box.center;copy.size=box.size;result=copy;
            }
            else throw new InvalidOperationException("Unsupported source collider "+source.GetType().Name);
            CopyCommon(source,result);
            return result;
        }
        private static float ReadFloat(SerializedObject so,string key)
        {
            var p=so.FindProperty(key);
            if(p==null||float.IsNaN(p.floatValue)||float.IsInfinity(p.floatValue))
                throw new InvalidOperationException("Missing vehicle setting "+key);
            return p.floatValue;
        }
        private static void RunIsolatedDynamics(Scene source,GameObject sourceRoot,Vehicle car,List<Vector3> path,List<MeshCollider> sourceAsphalt,bool loopTest)
        {
            if(!TryPilotStraight(path,out Vector3 start,out Vector3 forward))
                throw new InvalidOperationException("V7.6 physics test requires 45m verified straight route; no simulation performed.");
            if(source.isDirty||SceneManager.sceneCount!=1||SceneManager.GetActiveScene()!=source)
                throw new InvalidOperationException("Source pilot must remain the only clean active scene.");
            var sourceColliders=sourceRoot.GetComponentsInChildren<Collider>(false).Where(c=>c.enabled&&!c.isTrigger).ToList();
            var allSceneColliders=source.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Collider>(false))
                .Where(c=>c.enabled&&!c.isTrigger).ToList();
            if(sourceColliders.Count==0||sourceColliders.Any(c=>c.attachedRigidbody!=null)||
                !new HashSet<Collider>(sourceColliders).SetEquals(allSceneColliders))
                throw new InvalidOperationException("Source pilot must contain only accounted static environment colliders.");
            if(Physics.gravity.y>=0f||Mathf.Abs(Physics.gravity.x)>.001f||Mathf.Abs(Physics.gravity.z)>.001f)
                throw new InvalidOperationException("Expected downward project gravity for upright Minivan test.");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var controller=prefab.GetComponent<CarController>();
            var settings=new SerializedObject(controller);
            // Current CarController.SetupRigidbody sets mass to 1500kg.
            // This native-component harness intentionally does not execute it.
            var centerOfMass=settings.FindProperty("centerOfMassOffset");
            var driveTrain=settings.FindProperty("driveTrain");
            if(centerOfMass==null||driveTrain==null||driveTrain.enumValueIndex!=0)
                throw new InvalidOperationException("Expected current AWD Minivan configuration.");
            float motor=ReadFloat(settings,"motorTorque"),brake=ReadFloat(settings,"brakeTorque"),
                bias=ReadFloat(settings,"frontDriveBias"),steerLimit=ReadFloat(settings,"maxSteeringAngle"),
                ackermann=ReadFloat(settings,"ackermannSteerMultiplier");
            if(motor<=0f||brake<=0f||bias<=0f||bias>=1f||steerLimit<=0f)
                throw new InvalidOperationException("Invalid native test drivetrain settings.");
            var originalWheels=new[]{controller.frontLeftCollider,controller.frontRightCollider,
                controller.rearLeftCollider,controller.rearRightCollider};
            var stats=new DynamicStats();Scene staging=default,local=default;
            string result=null;
            try
            {
                local=EditorSceneManager.NewPreviewScene();
                if(!local.IsValid()||!local.isLoaded)throw new InvalidOperationException("Could not create preview physics scene.");
                PhysicsScene physics=local.GetPhysicsScene();
                if(!physics.IsValid()||physics.Equals(Physics.defaultPhysicsScene)||physics.Equals(source.GetPhysicsScene()))
                    throw new InvalidOperationException("Editor did not supply a separate local PhysicsScene. No default/global simulation allowed.");
                // Objects are created in an owned temporary staging scene,
                // not in the saved source. Move before adding physics components.
                staging=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
                if(!staging.IsValid()||!staging.isLoaded||staging==source||staging==local||
                    (SceneManager.GetActiveScene()!=staging&&!SceneManager.SetActiveScene(staging))||
                    SceneManager.GetActiveScene()!=staging||source.isDirty)
                    throw new InvalidOperationException("Could not activate clean temporary physics staging scene.");
                var copies=new Dictionary<Collider,Collider>();
                foreach(var c in sourceColliders)copies.Add(c,CloneStaticCollider(c,staging,local));
                var asphalt=sourceAsphalt.Select(c=>(MeshCollider)copies[c]).ToList();
                var asphaltSet=new HashSet<Collider>(asphalt);
                Quaternion rotation=Quaternion.LookRotation(forward,Vector3.up);
                Physics.SyncTransforms();
                var surface=new float[4];
                for(int i=0;i<4;i++)
                {
                    Vector3 pos=start+rotation*new Vector3(car.wheels[i].x,0f,car.wheels[i].z);
                    if(!AsphaltHeight(asphalt,pos,out surface[i]))
                        throw new InvalidOperationException("Copied asphalt missing under starting wheel "+i);
                }
                if(!TryPilotSuspensionPose(surface,car.compressedContact,car.travel,car.target,out float nominal,out _,out _))
                    throw new InvalidOperationException("No nominal starting suspension pose.");
                var vehicle=LocalObject(staging,local,"Minivan native physics test rig");
                vehicle.layer=prefab.layer;
                vehicle.transform.SetPositionAndRotation(new Vector3(start.x,nominal,start.z),rotation);
                var body=vehicle.AddComponent<BoxCollider>();body.center=car.center;body.size=car.size;
                var rb=vehicle.AddComponent<Rigidbody>();rb.mass=TestMass;rb.useGravity=true;
                rb.centerOfMass=centerOfMass.vector3Value;
                rb.linearDamping=.02f;rb.angularDamping=.4f;rb.sleepThreshold=0f;
                rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation=RigidbodyInterpolation.None;rb.maxAngularVelocity=30f;
                var wheels=new WheelCollider[4];
                for(int i=0;i<4;i++)
                {
                    var original=originalWheels[i];
                    var wheel=LocalObject(staging,local,"Wheel "+i);
                    wheel.layer=original.gameObject.layer;wheel.transform.SetParent(vehicle.transform,false);
                    wheel.transform.localRotation=Quaternion.Inverse(prefab.transform.rotation)*original.transform.rotation;
                    Vector3 position=prefab.transform.InverseTransformPoint(original.transform.position);
                    position.y+=car.compressedContact[i]-(car.wheels[i].y-original.radius);
                    wheel.transform.localPosition=position;
                    var wc=wheel.AddComponent<WheelCollider>();
                    wc.center=original.center;wc.radius=original.radius;wc.mass=original.mass;
                    wc.suspensionDistance=original.suspensionDistance;wc.suspensionSpring=original.suspensionSpring;
                    wc.forceAppPointDistance=original.forceAppPointDistance;wc.wheelDampingRate=original.wheelDampingRate;
                    wc.forwardFriction=original.forwardFriction;wc.sidewaysFriction=original.sidewaysFriction;
                    CopyCommon(original,wc);wheels[i]=wc;
                }
                rb.centerOfMass=centerOfMass.vector3Value;
                wheels[0].ResetSprungMasses();
                Physics.SyncTransforms();
                if(source.isDirty||staging.rootCount!=0||vehicle.scene!=local||
                    local.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)).Any()||
                    !physics.Equals(vehicle.scene.GetPhysicsScene()))
                    throw new InvalidOperationException("Physics isolation verification failed. No simulation started.");
                var contacts=new Collider[128];
                float sampledClearance=0f;
                var tracker=loopTest?new PilotLoopTracker(path):null;
                float rearZ=(car.wheels[2].z+car.wheels[3].z)*.5f;
                float originStation=0f;Vector3 lastPosition=rb.position;
                if(loopTest)
                {
                    tracker.Project(rb.position+rb.rotation*new Vector3(0f,0f,rearZ),true,out originStation);
                    stats.routeLength=tracker.Length;
                }
                void Sample(bool requireGround)
                {
                    int grounded=0;
                    foreach(var w in wheels)
                    {
                        if(w.GetGroundHit(out WheelHit hit))
                        {
                            if(hit.collider==null||!asphaltSet.Contains(hit.collider))
                                throw new InvalidOperationException("Wheel contacted non-asphalt "+(hit.collider==null?"unknown":hit.collider.name));
                            grounded++;
                        }
                    }
                    if(requireGround)
                    {
                        stats.groundSamples++;stats.groundedMin=Mathf.Min(stats.groundedMin,grounded);
                        if(grounded!=4)throw new InvalidOperationException("Lost four-wheel asphalt support ("+grounded+"/4).");
                    }
                    stats.maxSpeed=Mathf.Max(stats.maxSpeed,rb.linearVelocity.magnitude);
                    if(stats.phase=="straight acceleration"||stats.phase=="closed loop")
                        stats.maxForwardSpeed=Mathf.Max(stats.maxForwardSpeed,Vector3.Dot(rb.linearVelocity,rb.rotation*Vector3.forward));
                    stats.maxTilt=Mathf.Max(stats.maxTilt,Vector3.Angle(rb.rotation*Vector3.up,Vector3.up));
                    Vector3 delta=rb.position-start;
                    float lateral;
                    if(loopTest)
                    {
                        Vector3 rear=rb.position+rb.rotation*new Vector3(0f,0f,rearZ);
                        float rearError=tracker.Project(rear,true,out float station);
                        float rootError=tracker.Project(rb.position,false,out _);
                        lateral=Mathf.Max(rearError,rootError);
                        stats.routeProgress=station-originStation;
                        if(stats.phase!="gravity settling")
                            stats.distance+=Vector2.Distance(XZ(rb.position),XZ(lastPosition));
                        lastPosition=rb.position;
                        Vector3 tangent=tracker.At(station+1f)-tracker.At(station);
                        if(Vector2.Angle(XZ(rb.rotation*Vector3.forward),XZ(tangent))>45f||
                            stats.routeProgress<-.5f||stats.routeProgress>stats.routeLength+10f)
                            throw new InvalidOperationException("Vehicle reversed or left bounded closed route.");
                    }
                    else
                    {
                        stats.distance=Vector3.Dot(delta,forward);
                        lateral=Mathf.Abs(Vector3.Dot(delta,rotation*Vector3.right));
                    }
                    stats.maxLateral=Mathf.Max(stats.maxLateral,lateral);
                    if(lateral>1f||(!loopTest&&(stats.distance<-.5f||stats.distance>35f))||stats.maxTilt>15f)
                        throw new InvalidOperationException("Vehicle left bounded test corridor or exceeded tilt limit.");
                    float clearance=float.PositiveInfinity;
                    for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)
                    {
                        Vector3 point=rb.position+rb.rotation*(car.center+new Vector3(x*car.size.x*.5f,-car.size.y*.5f,z*car.size.z*.5f));
                        if(!AsphaltHeight(asphalt,point,out float y))
                            throw new InvalidOperationException("Body footprint left copied asphalt at "+point.ToString("F3"));
                        clearance=Mathf.Min(clearance,point.y-y);
                    }
                    sampledClearance=clearance;stats.clearance=Mathf.Min(stats.clearance,clearance);
                    if(clearance<.01f)
                        throw new InvalidOperationException("Loaded body clearance below 0.010m: "+clearance.ToString("F3")+" m.");
                    int count=physics.OverlapBox(rb.position+rb.rotation*car.center,car.size*.5f,contacts,rb.rotation,~0,QueryTriggerInteraction.Ignore);
                    if(count==contacts.Length)throw new InvalidOperationException("Body overlap query overflow.");
                    for(int i=0;i<count;i++)
                        if(contacts[i]!=body&&!wheels.Contains(contacts[i] as WheelCollider))
                            throw new InvalidOperationException("Body overlaps copied environment collider "+contacts[i].name);
                }
                void Tick(bool grounded)
                {
                    physics.Simulate(Step);stats.steps++;
                    if(float.IsNaN(rb.position.y)||float.IsInfinity(rb.position.y))throw new InvalidOperationException("Invalid physics pose.");
                    Sample(grounded);
                    if(stats.steps%25==0&&EditorUtility.DisplayCancelableProgressBar("V7.6 isolated Minivan physics",
                        stats.Summary(),loopTest?Mathf.Clamp01(stats.routeProgress/stats.routeLength):Mathf.Clamp01(stats.steps/2500f)))
                        throw new OperationCanceledException();
                }
                stats.phase="gravity settling";
                foreach(var w in wheels){w.motorTorque=0f;w.brakeTorque=brake;w.steerAngle=0f;}
                for(int i=0;i<300;i++)
                {
                    Tick(i>=50);
                    bool stable=i>=50&&rb.linearVelocity.magnitude<.05f&&rb.angularVelocity.magnitude<.05f;
                    stats.stableSteps=stable?stats.stableSteps+1:0;
                    if(stats.stableSteps>=50)break;
                }
                if(stats.stableSteps<50)throw new InvalidOperationException("Minivan did not settle under gravity in six simulated seconds.");
                stats.settledClearance=sampledClearance;
                if(loopTest)
                {
                    originStation=tracker.Progress;stats.routeProgress=0f;
                    stats.phase="closed loop";
                    for(int i=0;i<24000&&stats.routeProgress<stats.routeLength;i++)
                    {
                        float station=tracker.Progress;
                        bool curve=tracker.CurveAhead(station);
                        float target=curve?5f/3.6f:TargetSpeed;
                        if(curve)stats.curveSteps++;
                        float error=target-Vector3.Dot(rb.linearVelocity,rb.rotation*Vector3.forward);
                        float torque=Mathf.Clamp(error*180f,0f,motor*.25f);
                        float brakeTorque=Mathf.Clamp(-error*300f,0f,brake*.25f);
                        Vector3 rear=rb.position+rb.rotation*new Vector3(0f,0f,rearZ);
                        Vector3 aim=tracker.At(station+2.5f);
                        Vector3 localAim=Quaternion.Inverse(rb.rotation)*(aim-rear);
                        float curvature=2f*localAim.x/Mathf.Max(.01f,localAim.x*localAim.x+localAim.z*localAim.z);
                        float requested=Mathf.Atan(car.wheelbase*curvature)*Mathf.Rad2Deg;
                        // Bound by actual prefab steering limit; no pose
                        // teleport, forced path velocity or extra grip.
                        float steer=Mathf.Clamp(requested,-steerLimit,steerLimit);
                        wheels[0].steerAngle=steer<0f?Mathf.Max(-steerLimit,steer*ackermann):steer;
                        wheels[1].steerAngle=steer>0f?Mathf.Min(steerLimit,steer*ackermann):steer;
                        stats.maxSteer=Mathf.Max(stats.maxSteer,Mathf.Max(Mathf.Abs(wheels[0].steerAngle),Mathf.Abs(wheels[1].steerAngle)));
                        for(int w=0;w<4;w++){wheels[w].motorTorque=torque*(w<2?bias:1f-bias)*.5f;wheels[w].brakeTorque=brakeTorque;}
                        Tick(true);
                    }
                    if(stats.routeProgress<stats.routeLength||stats.curveSteps==0||stats.maxForwardSpeed<2f||
                        stats.distance<stats.routeLength-2f)
                        throw new InvalidOperationException("Minivan failed to complete one bounded physical lap.");
                }
                else
                {
                stats.phase="straight acceleration";
                for(int i=0;i<1500&&stats.distance<20f;i++)
                {
                    float speed=Vector3.Dot(rb.linearVelocity,forward);
                    float error=TargetSpeed-speed;
                    // Bounded test harness only: no gameplay input, steering
                    // assists, downforce, fuel or CarController FixedUpdate.
                    float torque=Mathf.Clamp(error*180f,0f,motor*.25f);
                    float brakeTorque=Mathf.Clamp(-error*300f,0f,brake*.25f);
                    float lateral=Vector3.Dot(rb.position-start,rotation*Vector3.right);
                    float yaw=Vector3.SignedAngle(rb.rotation*Vector3.forward,forward,Vector3.up);
                    float steer=Mathf.Clamp(yaw*.7f-lateral*8f,-Mathf.Min(steerLimit,12f),Mathf.Min(steerLimit,12f));
                    wheels[0].steerAngle=steer<0f?steer*ackermann:steer;
                    wheels[1].steerAngle=steer>0f?steer*ackermann:steer;
                    for(int w=0;w<4;w++){wheels[w].motorTorque=torque*(w<2?bias:1f-bias)*.5f;wheels[w].brakeTorque=brakeTorque;}
                    Tick(true);
                }
                if(stats.distance<20f||stats.maxForwardSpeed<2f)throw new InvalidOperationException("Minivan failed to accelerate/cover 20m.");
                }
                stats.phase="braking";float brakingStart=stats.distance;stats.stableSteps=0;
                foreach(var w in wheels){w.motorTorque=0f;w.brakeTorque=brake*.5f;w.steerAngle=0f;}
                for(int i=0;i<400;i++)
                {
                    Tick(true);
                    stats.stableSteps=rb.linearVelocity.magnitude<.05f&&rb.angularVelocity.magnitude<.05f?stats.stableSteps+1:0;
                    if(stats.stableSteps>=25)break;
                }
                stats.stopDistance=stats.distance-brakingStart;
                if(stats.stableSteps<25||stats.stopDistance<-.1f||stats.stopDistance>10f)
                    throw new InvalidOperationException("Minivan failed bounded braking and stop.");
                if(source.isDirty||source.GetPhysicsScene().Equals(physics))throw new InvalidOperationException("Source isolation changed during test.");
                result="[ProceduralWorld] "+(loopTest?"V7.7 MINIVAN ISOLATED CLOSED LOOP PHYSICS PASS: ":"V7.6 MINIVAN ISOLATED STRAIGHT PHYSICS PASS: ")+source.path+
                    "; native wheel rig "+TestMass.ToString("F0")+" kg, original spring/damper/friction, copied static colliders "+copies.Count+
                    "; "+stats.Summary()+", forward speed peak "+(stats.maxForwardSpeed*3.6f).ToString("F2")+
                    " km/h, settled clearance "+stats.settledClearance.ToString("F3")+" m, braking distance "+
                    stats.stopDistance.ToString("F2")+" m; "+(loopTest?
                    "Actual isolated native-wheel closed lap and stop; low-speed centerline test only. No CarController update, cargo, lane routing, user driving or traffic certification.":
                    "Actual simulated acceleration and stop only. No CarController update, cargo, full loop, user driving or traffic certification.");
            }
            catch(OperationCanceledException){throw;}
            catch(Exception ex){throw new InvalidOperationException((loopTest?"V7.7 MINIVAN ISOLATED CLOSED LOOP PHYSICS BLOCKED: ":
                "V7.6 MINIVAN ISOLATED PHYSICS BLOCKED: ")+stats.Summary()+"; "+ex.Message,ex);}
            finally
            {
                // Never save these temporary scenes or enter Play Mode.
                // Do not clear source dirty state or alter global physics.
                bool closed=true;
                if(local.IsValid()&&!EditorSceneManager.ClosePreviewScene(local))
                {closed=false;Debug.LogError("[ProceduralWorld] Could not close temporary physics preview scene.");}
                if(staging.IsValid()&&staging.isLoaded)
                {
                    if(SceneManager.GetActiveScene()!=source)SceneManager.SetActiveScene(source);
                    if(!EditorSceneManager.CloseScene(staging,true))
                    {closed=false;Debug.LogError("[ProceduralWorld] Could not close temporary staging scene.");}
                }
                if(!closed||source.isDirty||SceneManager.GetActiveScene()!=source||SceneManager.sceneCount!=1)
                    throw new InvalidOperationException("Physics test cleanup did not restore the original clean source scene. Do not save unexpected changes.");
            }
            if(result!=null)Debug.Log(result+" Temporary scenes closed; source unchanged, no Play Mode or save changes.");
        }
    }
}
