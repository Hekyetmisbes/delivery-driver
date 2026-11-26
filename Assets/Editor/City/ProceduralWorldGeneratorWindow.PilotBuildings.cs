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
            public float frontageDistance,groundRelief,accessGrade,curbStep,approachLength,approachSeam;
            public bool curbMeasured;
            public bool placed;
        }
        [Serializable] private sealed class PilotBuildingReport
        {
            public int candidates,placed,skipped,uniquePrefabs,boxColliders,accessMeshes,accessRaySamples,curbsMeasured;
            public float maxGroundRelief,maxAccessGrade,maxCurbStep,maxApproachSeam;
            public List<PilotLotRecord> records=new List<PilotLotRecord>();
            public string Summary()=>"Buildings placed / eligible plan lots "+placed+"/"+candidates+
                ", skipped "+skipped+", distinct prefab assets "+uniquePrefabs+", building box colliders "+boxColliders+
                ", maximum sampled foundation relief "+maxGroundRelief.ToString("F3")+" m, maximum sampled approach grade "+
                (maxAccessGrade*100f).ToString("F1")+"%, raw sidewalk/ground step max "+maxCurbStep.ToString("F3")+
                " m ("+curbsMeasured+" measured lots), pedestrian approach meshes "+accessMeshes+", approach ray samples "+accessRaySamples+
                ", maximum approach seam "+maxApproachSeam.ToString("F3")+" m; zero grading. Unplaced lots remain empty.";
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
        private sealed class PilotApproach
        {
            public Vector3[] top,bottom;
            public int[] triangles;
            public Rect bounds;
            public float grade,length,seam;
            public MeshCollider collider;
            public Collider sidewalk;
            public BoxCollider facade;
        }
        internal static float PilotTriangleGrade(Vector3 a,Vector3 b,Vector3 c)
        {
            Vector3 normal=Vector3.Cross(b-a,c-a);
            return Mathf.Abs(normal.y)<.000001f?float.PositiveInfinity:
                new Vector2(normal.x,normal.z).magnitude/Mathf.Abs(normal.y);
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
        // Physical pedestrian approach: a narrow supported strip from the
        // existing outer sidewalk to the nominal building front. Never flatten
        // source terrain or accept a raw curb gap as a safe transition.
        private bool TryPlanPilotApproach(Vector2 start,Vector2 end,Vector2 right,float facadeY,float roadY,
            Collider sidewalk,TerrainCollider terrain,Rect region,out PilotApproach approach,out string reason)
        {
            approach=null;reason="Approach mesh could not be planned";
            const float half=.6f;
            float length=Vector2.Distance(start,end);
            if(length<.5f||length>20f)return false;
            int steps=Mathf.CeilToInt(length/.5f);
            var top=new Vector3[(steps+1)*3];var bottom=new Vector3[top.Length];
            var starts=new float[3];var tris=new List<int>();float grade=0f;
            for(int c=0;c<3;c++)
                if(!PilotSurfaceRay(sidewalk,start+right*((c-1)*half),roadY,out starts[c]))
                {reason="Approach start missing sidewalk across width";return false;}
            for(int i=0;i<=steps;i++)
            for(int c=0;c<3;c++)
            {
                float t=i/(float)steps;
                Vector2 pos=Vector2.Lerp(start,end,t)+right*((c-1)*half);
                if(!ProceduralWorldPlanner.Contains(region,pos)||!PilotSurfaceRay(terrain,pos,roadY,out float ground))
                {reason="Approach outside pilot or missing ground";return false;}
                float y=Mathf.Lerp(starts[c],facadeY,t);
                if(y<ground-.001f){reason="Approach surface below sampled terrain";return false;}
                top[i*3+c]=new Vector3(pos.x,y,pos.y);
                bottom[i*3+c]=new Vector3(pos.x,Mathf.Min(y-.005f,ground-.02f),pos.y);
            }
            void Tri(int a,int b,int c)
            {
                if(Vector3.Cross(top[b]-top[a],top[c]-top[a]).y<0f){int swap=b;b=c;c=swap;}
                tris.Add(a);tris.Add(b);tris.Add(c);
                grade=Mathf.Max(grade,PilotTriangleGrade(top[a],top[b],top[c]));
            }
            for(int i=0;i<steps;i++)for(int c=0;c<2;c++)
            {
                int a=i*3+c,b=a+1,d=a+3,e=d+1;
                Tri(a,b,d);Tri(b,e,d);
            }
            if(grade>settings.parcelAccessGrade+.001f){reason="Constructed approach grade exceeds limit: "+(grade*100f).ToString("F1")+"%";return false;}
            for(int i=0;i<tris.Count;i+=3)
            {
                Vector3 a=top[tris[i]],b=top[tris[i+1]],c=top[tris[i+2]];
                foreach(var sample in new[]{(a+b+c)/3f,(a+b)*.5f,(b+c)*.5f,(c+a)*.5f})
                    if(!PilotSurfaceRay(terrain,PilotXZ(sample),roadY,out float ground)||sample.y<ground-.001f)
                    {reason="Approach triangle intersects sampled terrain";return false;}
            }
            approach=new PilotApproach{top=top,bottom=bottom,triangles=tris.ToArray(),grade=grade,length=length,
                bounds=PilotBox(top.Select(PilotXZ)),sidewalk=sidewalk};
            reason=null;return true;
        }
        private void BuildPilotApproach(Scene test,Transform root,Material material,PilotApproach approach,int lot)
        {
            var obj=NewRoot(test,"Pedestrian approach "+lot.ToString("D3"),root);
            var vertices=approach.top.Concat(approach.bottom).Select(obj.transform.InverseTransformPoint).ToArray();
            var triangles=new List<int>(approach.triangles);
            int count=approach.top.Length,rows=count/3;
            // Close the support skirt on both sides and ends. Top triangles
            // alone define the walkable surface and its measured gradient.
            void Wall(int a,int b)
            {
                triangles.Add(a);triangles.Add(b);triangles.Add(a+count);
                triangles.Add(b);triangles.Add(b+count);triangles.Add(a+count);
            }
            for(int i=0;i<rows-1;i++){Wall(i*3,(i+1)*3);Wall((i+1)*3+2,i*3+2);}
            for(int c=0;c<2;c++){Wall(c+1,c);Wall((rows-1)*3+c,(rows-1)*3+c+1);}
            var mesh=new Mesh{name="Pilot pedestrian approach "+lot};
            mesh.vertices=vertices;mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
            var filter=Undo.AddComponent<MeshFilter>(obj);filter.sharedMesh=mesh;
            var renderer=Undo.AddComponent<MeshRenderer>(obj);renderer.sharedMaterial=material;
            var collider=Undo.AddComponent<MeshCollider>(obj);collider.sharedMesh=mesh;collider.convex=false;
            approach.collider=collider;
        }
        private static int ValidatePilotApproach(PilotApproach approach,TerrainCollider terrain,out float seam)
        {
            var c=approach.collider;
            if(c==null||!c.enabled||c.sharedMesh==null||c.GetComponent<MeshFilter>()?.sharedMesh!=c.sharedMesh)
                throw new InvalidOperationException("Pilot pedestrian approach collider missing.");
            int probes=0;seam=0f;
            for(int i=0;i<approach.triangles.Length;i+=3)
            {
                Vector3 a=approach.top[approach.triangles[i]],b=approach.top[approach.triangles[i+1]],
                    d=approach.top[approach.triangles[i+2]];
                Vector3 sample=(a+b+d)/3f;
                if(!PilotSurfaceRay(c,PilotXZ(sample),sample.y,out float y)||Mathf.Abs(y-sample.y)>.01f||
                    !PilotSurfaceRay(terrain,PilotXZ(sample),sample.y,out float ground)||ground-y>.001f)
                    throw new InvalidOperationException("Pilot pedestrian approach collider surface mismatch at "+sample.ToString("F3"));
                probes++;
            }
            // First row lies 5cm inside the existing sidewalk. Probe just
            // inside the new strip and compare the actual overlapping surfaces.
            for(int col=0;col<2;col++)
            {
                Vector3 a=(approach.top[col]+approach.top[col+1])*.5f;
                Vector3 b=(approach.top[col+3]+approach.top[col+4])*.5f;
                Vector3 q=Vector3.Lerp(a,b,.02f);
                if(!PilotSurfaceRay(c,PilotXZ(q),q.y,out float rampY)||
                    !PilotSurfaceRay(approach.sidewalk,PilotXZ(q),q.y,out float sidewalkY))
                    throw new InvalidOperationException("Pilot approach/sidewalk seam ray missing.");
                seam=Mathf.Max(seam,Mathf.Abs(rampY-sidewalkY));probes++;
            }
            if(approach.facade==null||!approach.facade.enabled)
                throw new InvalidOperationException("Pilot building facade collider missing.");
            var box=approach.facade;
            Vector3 min=box.center-box.size*.5f,max=box.center+box.size*.5f;
            int last=approach.top.Length-3;
            for(int col=0;col<3;col++)
            {
                Vector3 point=approach.top[last+col];
                Vector3 local=box.transform.InverseTransformPoint(point);
                Vector3 target=box.transform.TransformPoint(new Vector3(Mathf.Clamp(local.x,min.x,max.x),min.y,max.z));
                float error=Vector3.Distance(target,point);
                if(error>.02f)throw new InvalidOperationException("Pilot approach does not reach nominal facade base.");
                seam=Mathf.Max(seam,error);
            }
            if(seam>.02f)throw new InvalidOperationException("Pilot approach seam exceeds 0.02m.");
            return probes;
        }
        private PilotBuildingReport PlacePilotBuildings(Scene test,Transform parent,Transform roadsRoot,TerrainCollider terrain,
            ProceduralWorldPlanner.Plan source,ProceduralWorldPlanner.PilotSurface pilot,
            ProceduralWorldPlanner.PilotNeighborhoodScope scope,Dictionary<ProceduralWorldPlanner.Edge,SplineRoad> roads,
            string folder,Material approachMaterial)
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
            var approaches=new List<PilotApproach>();
            var uses=new Dictionary<string,int>();
            var root=NewRoot(test,"Buildings - verified pilot lots",parent).transform;
            var accessRoot=NewRoot(test,"Pedestrian approaches - verified local mesh",parent).transform;
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
                // Record the original step even for rejected lots. A large
                // raw step is not accepted as-is: build and validate an actual
                // graded pedestrian approach instead of raising the threshold.
                float run=frontage.distance-paved;
                if(run<=0f){record.reason="Invalid approach length";continue;}
                Vector2 edgePoint=roadCenter+inward*(paved-.05f),approachEnd=front+inward;
                if(!PilotSurfaceRay(sidewalk,edgePoint,roadY,out float outerSidewalkY)||
                    !PilotSurfaceRay(terrain,roadCenter+inward*paved,roadY,out float adjacentGround))
                {record.reason="Missing outer sidewalk or adjacent ground";continue;}
                record.curbStep=Mathf.Abs(outerSidewalkY-adjacentGround);record.curbMeasured=true;
                report.curbsMeasured++;report.maxCurbStep=Mathf.Max(report.maxCurbStep,record.curbStep);
                Rect accessBox=PilotBox(new[]{edgePoint-right*.6f,edgePoint+right*.6f,approachEnd-right*.6f,approachEnd+right*.6f});
                if(!PilotRectInside(pilot.region,accessBox)||occupied.Any(r=>r.Overlaps(accessBox))||
                    source.protectedAreas.Any(r=>r.Overlaps(accessBox))||source.openSpaces.Any(s=>s.bounds.Overlaps(accessBox))||
                    source.parcels.Any(l=>l!=lot&&l.bounds.Overlaps(accessBox)))
                {record.reason="Approach corridor overlaps reserved area";continue;}
                bool access=true;
                int steps=Mathf.Max(1,Mathf.CeilToInt((run+1f)/.5f));
                for(int k=0;k<=steps&&access;k++)for(int col=-1;col<=1;col++)
                {
                    Vector2 pos=roadCenter+inward*(paved+(run+1f)*k/steps)+right*(col*.6f);
                    if(source.protectedAreas.Any(r=>ProceduralWorldPlanner.Contains(r,pos,1f))||
                        scope.edges.Any(e=>e!=frontage.edge&&e.points.Skip(1).Select((v,j)=>
                            ProceduralWorldPlanner.DistanceSquared(pos,PilotXZ(e.points[j]),PilotXZ(v))<paved*paved).Any(x=>x)))
                    {access=false;break;}
                }
                if(!access){record.reason="Approach corridor blocked by another road or protected area";continue;}
                // No paved mesh overlap after the 5cm intended sidewalk seam
                // and a small transition band; test actual paved triangles.
                Vector2 corridorCenter=(edgePoint+approachEnd)*.5f;
                Vector2 CorridorLocal(Vector2 pos)=>new Vector2(Vector2.Dot(pos-corridorCenter,right),Vector2.Dot(pos-corridorCenter,inward));
                float accessLength=Vector2.Distance(edgePoint,approachEnd);
                var corridorRect=new Rect(-.6f,-accessLength*.5f+.15f,1.2f,accessLength-.15f);
                if(triangles.Any(t=>t.bounds.xMax>=accessBox.xMin&&t.bounds.xMin<=accessBox.xMax&&
                    t.bounds.yMax>=accessBox.yMin&&t.bounds.yMin<=accessBox.yMax&&
                    PilotTriangleOverlapsRect(CorridorLocal(t.a),CorridorLocal(t.b),CorridorLocal(t.c),corridorRect)))
                {record.reason="Approach corridor intersects actual paved mesh";continue;}
                var choices=catalog.Where(m=>m.width>=1.2f&&m.width+2f<=width+.001f&&m.depth+2f<=depth+.001f)
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
                    if(!TryPlanPilotApproach(edgePoint,approachEnd,right,high+.02f,roadY,sidewalk,terrain,pilot.region,
                        out var approach,out string approachReason))
                    {record.reason=approachReason;continue;}
                    float modelPeak=approach.grade;
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
                    approach.facade=collision;
                    BuildPilotApproach(test,accessRoot,approachMaterial,approach,i);
                    Physics.SyncTransforms();
                    ValidatePilotApproach(approach,terrain,out float seam);
                    approach.seam=seam;approaches.Add(approach);
                    occupied.Add(box);occupied.Add(approach.bounds);
                    uses.TryGetValue(model.path,out int count);uses[model.path]=count+1;
                    record.placed=true;record.placedPrefab=model.path;record.position=instance.transform.position;
                    record.groundRelief=high-low;record.accessGrade=modelPeak;record.approachLength=approach.length;
                    record.approachSeam=seam;record.reason="Placed; foundation and physical pedestrian approach passed";
                    report.placed++;report.boxColliders++;
                    report.maxGroundRelief=Mathf.Max(report.maxGroundRelief,high-low);
                    report.maxAccessGrade=Mathf.Max(report.maxAccessGrade,modelPeak);
                    report.maxApproachSeam=Mathf.Max(report.maxApproachSeam,seam);break;
                }
            }
            if(approaches.Count>0)
            {
                report.accessMeshes=PersistPilotMeshes(accessRoot.gameObject,folder,"AccessMeshes");
                Physics.SyncTransforms();
                foreach(var approach in approaches)
                {
                    report.accessRaySamples+=ValidatePilotApproach(approach,terrain,out float seam);
                    if(Mathf.Abs(seam-approach.seam)>.0001f)
                        throw new InvalidOperationException("Pilot approach seam changed after mesh persistence.");
                }
            }
            report.uniquePrefabs=uses.Count;report.skipped=report.candidates-report.placed;
            if(report.records.Count!=report.candidates||root.childCount!=report.placed||
                accessRoot.childCount!=report.placed||report.accessMeshes!=report.placed)
                throw new InvalidOperationException("Pilot building accounting mismatch.");
            return report;
        }
    }
}
