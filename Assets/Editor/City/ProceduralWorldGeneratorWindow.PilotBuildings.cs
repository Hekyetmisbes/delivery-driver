using System;
using System.Collections.Generic;
using System.Linq;
using TrafficSystem.Spline;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryDriver.EditorTools
{
    public sealed partial class ProceduralWorldGeneratorWindow
    {
        // Stricter local flat-terrain proof than the 3m planning relief cap.
        // No terrain edits, artificial fill, foundations or prefab rescaling.
        internal const float PilotFoundationRelief=.25f;
        [Serializable] private sealed class PilotLotRecord
        {
            public int index,sourceA,sourceB;
            public string plannedPrefab,placedPrefab,reason;
            public Vector3 position;
            public float frontageDistance,groundRelief,accessGrade,curbStep;
            public bool placed;
        }
        [Serializable] private sealed class PilotBuildingReport
        {
            public int candidates,placed,skipped,uniquePrefabs,boxColliders;
            public float maxGroundRelief,maxAccessGrade,maxCurbStep;
            public List<PilotLotRecord> records=new List<PilotLotRecord>();
            public string Summary()=>"Buildings placed / eligible plan lots "+placed+"/"+candidates+
                ", skipped "+skipped+", distinct prefab assets "+uniquePrefabs+", building box colliders "+boxColliders+
                ", maximum sampled foundation relief "+maxGroundRelief.ToString("F3")+" m, maximum sampled approach grade "+
                (maxAccessGrade*100f).ToString("F1")+"%, maximum sampled curb step "+maxCurbStep.ToString("F3")+
                " m; zero grading. Unplaced lots remain empty.";
        }
        private sealed class PilotPrefab
        {
            public string path;
            public GameObject asset;
            public Bounds bounds;
            public Vector3 scale;
            public float width,depth;
        }
        private sealed class PilotFrontage
        {
            public ProceduralWorldPlanner.Edge edge;
            public SplineRoad road;
            public Vector3 center;
            public float distance;
        }
        private struct PavedTriangle
        {
            public Vector2 a,b,c;
            public Rect bounds;
        }

        internal static bool PilotTriangleOverlapsRect(Vector2 a,Vector2 b,Vector2 c,Rect r)
        {
            // Separating axis test in lot-local XZ, including contact.
            Vector2 center=r.center,extent=r.size*.5f;
            a-=center;b-=center;c-=center;
            bool Separates(Vector2 axis)
            {
                if(axis.sqrMagnitude<.00000001f)return false;
                float p=Vector2.Dot(a,axis),q=Vector2.Dot(b,axis),s=Vector2.Dot(c,axis);
                float radius=extent.x*Mathf.Abs(axis.x)+extent.y*Mathf.Abs(axis.y);
                return Mathf.Min(p,Mathf.Min(q,s))>radius||Mathf.Max(p,Mathf.Max(q,s))< -radius;
            }
            Vector2 Normal(Vector2 v)=>new Vector2(-v.y,v.x);
            return !Separates(Vector2.right)&&!Separates(Vector2.up)&&!Separates(Normal(b-a))&&
                !Separates(Normal(c-b))&&!Separates(Normal(a-c));
        }
        private static Vector2 PilotXZ(Vector3 v)=>new Vector2(v.x,v.z);
        private static Rect PilotBox(IEnumerable<Vector2> points)
        {
            var p=points.ToArray();
            return Rect.MinMaxRect(p.Min(v=>v.x),p.Min(v=>v.y),p.Max(v=>v.x),p.Max(v=>v.y));
        }
        private static bool PilotRectInside(Rect outer,Rect inner)=>
            ProceduralWorldPlanner.Contains(outer,inner.min)&&ProceduralWorldPlanner.Contains(outer,inner.max);
        private static bool PilotSurfaceRay(Collider collider,Vector2 point,float referenceY,out float y)
        {
            y=0;
            if(collider==null||!collider.enabled||!collider.gameObject.activeInHierarchy)return false;
            var ray=new Ray(new Vector3(point.x,referenceY+10f,point.y),Vector3.down);
            if(!collider.Raycast(ray,out RaycastHit hit,20f))return false;
            y=hit.point.y;return true;
        }
        private PilotFrontage FindBuildingFrontage(ProceduralWorldPlanner.UrbanArea lot,Vector2 front,Vector2 inward,
            ProceduralWorldPlanner.PilotNeighborhoodScope scope,Dictionary<ProceduralWorldPlanner.Edge,SplineRoad> roads)
        {
            float expected=settings.width*.5f+settings.sidewalk+6f;
            PilotFrontage best=null;
            foreach(var edge in scope.edges)
            {
                var source=scope.sourceEdges[edge];
                for(int i=1;i<edge.points.Count;i++)
                {
                    Vector3 a=edge.points[i-1],b=edge.points[i];Vector2 d=PilotXZ(b-a);
                    if(d.sqrMagnitude<.000001f)continue;
                    float t=Vector2.Dot(front-PilotXZ(a),d)/d.sqrMagnitude;
                    if(t<0f||t>1f)continue;
                    Vector3 center=Vector3.Lerp(a,b,t);
                    Vector2 delta=front-PilotXZ(center);float distance=delta.magnitude;
                    if(Mathf.Abs(distance-expected)>.15f||Vector2.Dot(delta.normalized,inward)<.995f||
                        Mathf.Abs(Vector2.Dot(d.normalized,inward))>.02f)continue;
                    if(Vector2.Distance(PilotXZ(center),PilotXZ(source.a.position))<25f-.01f||
                        Vector2.Distance(PilotXZ(center),PilotXZ(source.b.position))<25f-.01f||
                        Vector2.Distance(PilotXZ(center),PilotXZ(edge.a.position))<settings.width*.5f+settings.sidewalk+2f||
                        Vector2.Distance(PilotXZ(center),PilotXZ(edge.b.position))<settings.width*.5f+settings.sidewalk+2f)continue;
                    if(best!=null&&best.edge!=edge)return null; // ambiguous frontage
                    best=new PilotFrontage{edge=edge,road=roads[edge],center=center,distance=distance};
                }
            }
            return best;
        }
        private PilotBuildingReport PlacePilotBuildings(Scene test,Transform parent,Transform roadsRoot,TerrainCollider terrain,
            ProceduralWorldPlanner.Plan source,ProceduralWorldPlanner.PilotSurface pilot,
            ProceduralWorldPlanner.PilotNeighborhoodScope scope,Dictionary<ProceduralWorldPlanner.Edge,SplineRoad> roads)
        {
            var report=new PilotBuildingReport();
            var lots=source.parcels.Where(l=>l.sector==pilot.city&&l.accessible&&!string.IsNullOrEmpty(l.prefab)&&
                PilotRectInside(pilot.region,l.bounds)).ToList();
            if(lots.Count!=pilot.lotCount)throw new InvalidOperationException("Building lot scope differs from verified preflight.");
            report.candidates=lots.Count;
            var catalog=new List<PilotPrefab>();
            foreach(var item in RoadsideBuildingPlacementTool.ReadPreviewFootprints().OrderBy(x=>x.name,StringComparer.Ordinal))
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(item.name);
                if(prefab==null||!prefab.activeSelf||prefab.GetComponentsInChildren<MonoBehaviour>(true).Length>0||
                    prefab.GetComponentsInChildren<Rigidbody>(true).Length>0||
                    prefab.GetComponentsInChildren<Animator>(true).Length>0||
                    !RoadsideBuildingPlacementTool.TryCalculatePreciseLocalBounds(prefab.transform,out Bounds bounds))continue;
                Vector3 scale=prefab.transform.localScale;
                if(scale.x<=0||scale.y<=0||scale.z<=0||bounds.size.y*scale.y<1f)continue;
                catalog.Add(new PilotPrefab{path=item.name,asset=prefab,bounds=bounds,scale=scale,
                    width=bounds.size.x*scale.x,depth=bounds.size.z*scale.z});
            }
            var triangles=new List<PavedTriangle>();
            foreach(var filter in roadsRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!filter.gameObject.activeInHierarchy||filter.sharedMesh==null)continue;
                var vertices=filter.sharedMesh.vertices.Select(v=>PilotXZ(filter.transform.TransformPoint(v))).ToArray();
                var indices=filter.sharedMesh.triangles;
                for(int i=0;i<indices.Length;i+=3)
                {
                    Vector2 a=vertices[indices[i]],b=vertices[indices[i+1]],c=vertices[indices[i+2]];
                    triangles.Add(new PavedTriangle{a=a,b=b,c=c,bounds=PilotBox(new[]{a,b,c})});
                }
            }
            var occupied=new List<Rect>();
            var uses=new Dictionary<string,int>();
            var root=NewRoot(test,"Buildings - verified pilot lots",parent).transform;
            Physics.SyncTransforms();
            for(int i=0;i<lots.Count;i++)
            {
                var lot=lots[i];var record=new PilotLotRecord{index=i,plannedPrefab=lot.prefab,reason="Unverified"};
                report.records.Add(record);
                if(EditorUtility.DisplayCancelableProgressBar("V7.4 building placement","Lot "+(i+1)+"/"+lots.Count,.93f))
                    throw new OperationCanceledException();
                if(lot.corners==null||lot.corners.Length!=4){record.reason="Invalid lot corners";continue;}
                Vector2 front=(PilotXZ(lot.corners[0])+PilotXZ(lot.corners[1]))*.5f;
                Vector2 back=(PilotXZ(lot.corners[2])+PilotXZ(lot.corners[3]))*.5f;
                Vector2 inward=(back-front).normalized,center=(front+back)*.5f;
                if(inward.sqrMagnitude<.99f){record.reason="Degenerate lot";continue;}
                // Local +Z faces road, consistent with existing roadside tool.
                Quaternion rotation=Quaternion.LookRotation(new Vector3(-inward.x,0,-inward.y),Vector3.up);
                Vector2 right=PilotXZ(rotation*Vector3.right);
                float width=Vector2.Distance(PilotXZ(lot.corners[0]),PilotXZ(lot.corners[1]));
                float depth=Vector2.Distance(front,back);
                if(width<2f||depth<2f||Mathf.Abs(Vector2.Dot(right,inward))>.001f||
                    source.protectedAreas.Any(r=>r.Overlaps(lot.bounds))||
                    source.openSpaces.Any(s=>s.bounds.Overlaps(lot.bounds)))
                {record.reason="Lot bounds/protected/park screen";continue;}
                var frontage=FindBuildingFrontage(lot,front,inward,scope,roads);
                if(frontage==null){record.reason="No unique matching built road frontage";continue;}
                var original=scope.sourceEdges[frontage.edge];
                record.sourceA=original.a.id;record.sourceB=original.b.id;record.frontageDistance=frontage.distance;
                var asphalt=frontage.road.transform.Find("Asphalt")?.GetComponent<MeshCollider>();
                var sidewalk=frontage.road.transform.Find("Sidewalks")?.GetComponent<MeshCollider>();
                float paved=settings.width*.5f+settings.sidewalk;
                Vector2 roadCenter=PilotXZ(frontage.center);
                if(!PilotSurfaceRay(asphalt,roadCenter,frontage.center.y,out float roadY)||
                    Mathf.Abs(roadY-frontage.center.y-.03f)>.15f||
                    !PilotSurfaceRay(sidewalk,roadCenter+inward*(settings.width*.5f+settings.sidewalk*.5f),roadY,out float sidewalkY))
                {record.reason="Missing actual road/sidewalk frontage collider";continue;}
                // Probe walking approach to front lot line, not a built driveway.
                // Sample at <=1m, using actual cloned terrain collider only.
                bool access=true;float peak=0f,run=frontage.distance-paved;
                if(run<=0f){record.reason="Invalid approach length";continue;}
                Vector2 edgePoint=roadCenter+inward*(paved-.05f);
                if(!PilotSurfaceRay(sidewalk,edgePoint,roadY,out float outerSidewalkY)||
                    !PilotSurfaceRay(terrain,roadCenter+inward*paved,roadY,out float previous))
                {record.reason="Missing outer sidewalk or adjacent ground";continue;}
                // A curb is a discrete pedestrian step, not a terrain slope.
                // Screen it separately; do not certify a driveway/ramp.
                record.curbStep=Mathf.Abs(outerSidewalkY-previous);
                if(record.curbStep>.25f){record.reason="Curb step exceeds 0.25m";continue;}
                int steps=Mathf.Max(1,Mathf.CeilToInt(run));
                float previousRoad=frontage.center.y;
                for(int k=1;k<=steps;k++)
                {
                    Vector2 pos=roadCenter+inward*(paved+run*k/steps);
                    if(source.protectedAreas.Any(r=>ProceduralWorldPlanner.Contains(r,pos,1f))||
                        source.openSpaces.Any(s=>ProceduralWorldPlanner.Contains(s.bounds,pos))||
                        lots.Any(l=>l!=lot&&ProceduralWorldPlanner.Contains(l.bounds,pos))||
                        scope.edges.Any(e=>e!=frontage.edge&&e.points.Skip(1).Select((v,j)=>
                            ProceduralWorldPlanner.DistanceSquared(pos,PilotXZ(e.points[j]),PilotXZ(v))<paved*paved).Any(x=>x))||
                        !PilotSurfaceRay(terrain,pos,roadY,out float h)){access=false;break;}
                    peak=Mathf.Max(peak,Mathf.Max(Mathf.Abs(h-previous),Mathf.Abs(h-previousRoad))/(run/steps));
                    previous=h;previousRoad=h;
                }
                record.accessGrade=peak;
                if(!access||peak>settings.parcelAccessGrade+.001f)
                {record.reason="Sampled physical approach blocked or too steep";continue;}
                var choices=catalog.Where(m=>m.width+2f<=width+.001f&&m.depth+2f<=depth+.001f)
                    .OrderBy(m=>uses.TryGetValue(m.path,out int used)?used:0)
                    .ThenByDescending(m=>m.path==lot.prefab).ThenByDescending(m=>m.width*m.depth)
                    .ThenBy(m=>m.path,StringComparer.Ordinal).ToList();
                if(choices.Count==0){record.reason="No safe catalogue prefab fits";continue;}
                foreach(var model in choices)
                {
                    float hw=model.width*.5f,hd=model.depth*.5f;
                    // Position building one metre behind the lot frontage,
                    // rather than floating small models in the lot center.
                    Vector2 footprintCenter=front+inward*(1f+hd);
                    Vector2[] footprint={footprintCenter-right*hw-inward*hd,footprintCenter+right*hw-inward*hd,
                        footprintCenter+right*hw+inward*hd,footprintCenter-right*hw+inward*hd};
                    Rect box=PilotBox(footprint);
                    if(!PilotRectInside(lot.bounds,box)||occupied.Any(r=>r.Overlaps(box))||
                        source.protectedAreas.Any(r=>r.Overlaps(box))||source.openSpaces.Any(s=>s.bounds.Overlaps(box)))
                    {record.reason="Building footprint overlaps reserved area";continue;}
                    Vector2 Local(Vector2 pos)=>new Vector2(Vector2.Dot(pos-footprintCenter,right),Vector2.Dot(pos-footprintCenter,inward));
                    var localRect=new Rect(-hw-.05f,-hd-.05f,2f*hw+.1f,2f*hd+.1f);
                    var expandedBox=Rect.MinMaxRect(box.xMin-.1f,box.yMin-.1f,box.xMax+.1f,box.yMax+.1f);
                    if(triangles.Any(t=>t.bounds.xMax>=expandedBox.xMin&&t.bounds.xMin<=expandedBox.xMax&&
                        t.bounds.yMax>=expandedBox.yMin&&t.bounds.yMin<=expandedBox.yMax&&
                        PilotTriangleOverlapsRect(Local(t.a),Local(t.b),Local(t.c),localRect)))
                    {record.reason="Building footprint intersects actual paved mesh";continue;}
                    float low=float.PositiveInfinity,high=float.NegativeInfinity;
                    bool groundOk=true;
                    int nx=Mathf.Max(1,Mathf.CeilToInt(model.width)),nz=Mathf.Max(1,Mathf.CeilToInt(model.depth));
                    for(int z=0;z<=nz&&groundOk;z++)for(int x=0;x<=nx;x++)
                    {
                        Vector2 pos=footprintCenter+right*Mathf.Lerp(-hw,hw,x/(float)nx)+inward*Mathf.Lerp(-hd,hd,z/(float)nz);
                        if(!PilotSurfaceRay(terrain,pos,roadY,out float h)){groundOk=false;break;}
                        low=Mathf.Min(low,h);high=Mathf.Max(high,h);
                    }
                    if(!groundOk||high-low>PilotFoundationRelief)
                    {record.reason="Building footprint ground missing or relief exceeds 0.25m";continue;}
                    // Continue the sampled approach from the lot line to the
                    // model's nominal front face. No fabricated access mesh.
                    if(!PilotSurfaceRay(terrain,front+inward,roadY,out float facadeGround)||
                        Mathf.Abs(facadeGround-previous)>settings.parcelAccessGrade+.001f||
                        high+.02f-facadeGround>.27f)
                    {record.reason="Front setback ground or foundation step failed";continue;}
                    float modelPeak=Mathf.Max(peak,Mathf.Abs(facadeGround-previous));
                    // Use the original prefab scale and center offset; base at
                    // highest sampled ground plus 2cm, no retaining foundation.
                    var instance=PrefabUtility.InstantiatePrefab(model.asset,root) as GameObject;
                    if(instance==null||instance.scene!=test)throw new InvalidOperationException("Pilot prefab instance not in isolated scene.");
                    Undo.RegisterCreatedObjectUndo(instance,"Place pilot building");
                    instance.name="Lot_"+i.ToString("D3")+"_"+model.asset.name;
                    instance.transform.localScale=model.scale;
                    instance.transform.rotation=rotation;
                    Vector3 offset=rotation*Vector3.Scale(new Vector3(model.bounds.center.x,model.bounds.min.y,model.bounds.center.z),model.scale);
                    instance.transform.position=new Vector3(footprintCenter.x,high+.02f,footprintCenter.y)-offset;
                    // Standard static bounds collider on the instance only.
                    // Existing child colliders remain on prefab assets unchanged.
                    foreach(var collider in instance.GetComponentsInChildren<Collider>(true))
                    {Undo.RecordObject(collider,"Pilot building collider override");collider.enabled=false;}
                    var collision=Undo.AddComponent<BoxCollider>(instance);
                    collision.center=model.bounds.center;collision.size=model.bounds.size;collision.isTrigger=false;
                    Physics.SyncTransforms();
                    if(!instance.GetComponentsInChildren<MeshRenderer>(false).Any(r=>r.enabled&&r.gameObject.activeInHierarchy)||
                        !RoadsideBuildingPlacementTool.TryCalculatePreciseLocalBounds(instance.transform,out Bounds measured)||
                        (measured.center-model.bounds.center).sqrMagnitude>.0001f||(measured.size-model.bounds.size).sqrMagnitude>.0001f||
                        !collision.enabled||collision.bounds.size.sqrMagnitude<.01f||
                        !PilotRectInside(lot.bounds,new Rect(collision.bounds.min.x,collision.bounds.min.z,
                            collision.bounds.size.x,collision.bounds.size.z)))
                        throw new InvalidOperationException("Actual pilot building bounds/collider differ from planned fit at lot "+i);
                    occupied.Add(box);uses.TryGetValue(model.path,out int count);uses[model.path]=count+1;
                    record.placed=true;record.placedPrefab=model.path;record.position=instance.transform.position;
                    record.groundRelief=high-low;record.accessGrade=modelPeak;record.reason="Placed; sampled foundation and road approach passed";
                    report.placed++;report.boxColliders++;
                    report.maxGroundRelief=Mathf.Max(report.maxGroundRelief,high-low);
                    report.maxAccessGrade=Mathf.Max(report.maxAccessGrade,modelPeak);
                    report.maxCurbStep=Mathf.Max(report.maxCurbStep,record.curbStep);break;
                }
            }
            report.uniquePrefabs=uses.Count;report.skipped=report.candidates-report.placed;
            if(report.records.Count!=report.candidates||root.childCount!=report.placed)
                throw new InvalidOperationException("Pilot building accounting mismatch.");
            return report;
        }
    }
}
