using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrafficSystem.Spline;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeliveryDriver.EditorTools
{
    public sealed partial class ProceduralWorldGeneratorWindow
    {
        private const string PhysicalPilotRoot="Assets/ProceduralPilotTests";
        private bool physicalPilotBuilding,physicalPilotQueued;
        private ProceduralWorldPlanner.Plan queuedPilotPlan;
        private ProceduralWorldPlanner.PilotSurface queuedPilotSurface;
        private Scene queuedPilotScene;
        private string lastPhysicalPilotSummary,lastPhysicalPilotScene;

        // Scene creation, modal dialogs and asset import must not run inside the
        // window's IMGUI layout event. Revalidate the exact request after it ends.
        private void QueuePhysicalPilot()
        {
            if(busy||physicalPilotBuilding||physicalPilotQueued||!HasPilot||!PhysicalPilotEligible(pilotSurface))return;
            physicalPilotQueued=true;
            queuedPilotPlan=preview;queuedPilotSurface=pilotSurface;
            queuedPilotScene=SceneManager.GetActiveScene();
            EditorApplication.delayCall+=RunQueuedPhysicalPilot;
            SetStatus("Physical pilot queued for the next editor update.");
            Repaint();
        }

        private void CancelQueuedPhysicalPilot()
        {
            EditorApplication.delayCall-=RunQueuedPhysicalPilot;
            physicalPilotQueued=false;
            queuedPilotPlan=null;queuedPilotSurface=null;queuedPilotScene=default;
        }

        private void RunQueuedPhysicalPilot()
        {
            var expectedPlan=queuedPilotPlan;var expectedSurface=queuedPilotSurface;var expectedScene=queuedPilotScene;
            bool queued=physicalPilotQueued;
            CancelQueuedPhysicalPilot();
            if(!queued||this==null)return;
            Run(()=>
            {
                if(preview!=expectedPlan||pilotSurface!=expectedSurface||SceneManager.GetActiveScene()!=expectedScene)
                    throw new InvalidOperationException("Pilot selection or active scene changed before the queued build. Run preflight again.");
                BuildPhysicalPilot();
            });
        }

        private static string PilotSceneState(Scene scene)
        {
            return scene.IsValid()?"handle "+scene.handle+", name '"+scene.name+"', path '"+scene.path+
                "', loaded "+scene.isLoaded+", dirty "+scene.isDirty:"invalid handle "+scene.handle;
        }

        private static void ActivatePilotScene(Scene scene,string context)
        {
            if(!scene.IsValid()||!scene.isLoaded)
                throw new InvalidOperationException(context+": scene is invalid or not loaded ("+PilotSceneState(scene)+
                    "); active "+PilotSceneState(SceneManager.GetActiveScene())+", open count "+SceneManager.sceneCount+".");
            // NewScene(Additive) can already select the new scene. Verify actual
            // active identity, not just the return value of a redundant request.
            if(SceneManager.GetActiveScene()==scene)return;
            bool accepted=SceneManager.SetActiveScene(scene);
            if(SceneManager.GetActiveScene()!=scene)
                throw new InvalidOperationException(context+": SetActiveScene returned "+accepted+
                    "; target "+PilotSceneState(scene)+"; active "+PilotSceneState(SceneManager.GetActiveScene())+
                    "; open count "+SceneManager.sceneCount+".");
        }

        private static bool PhysicalPilotEligible(ProceduralWorldPlanner.PilotSurface p)
        {
            // This first physical proof is explicitly zero-grading Istanbul only.
            return p!=null&&p.Pass&&p.city=="İstanbul"&&p.changedCells==0&&p.original!=null&&
                p.proposed!=null&&p.original.Length==p.proposed.Length&&p.original.Length>0&&
                p.original.SequenceEqual(p.proposed);
        }

        private static Material PilotMaterial(string folder,string name,Color color,bool markings=false)
        {
            Shader shader=Shader.Find(markings?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit")??Shader.Find("Standard");
            if(shader==null)throw new InvalidOperationException("No suitable pilot material shader.");
            var mat=new Material(shader){name=name,color=color};
            if(mat.HasProperty("_BaseColor"))mat.SetColor("_BaseColor",color);
            if(mat.HasProperty("_Smoothness"))mat.SetFloat("_Smoothness",.18f);
            if(markings)
            {
                mat.renderQueue=2100;
                if(mat.HasProperty("_QueueOffset"))mat.SetFloat("_QueueOffset",100f);
            }
            AssetDatabase.CreateAsset(mat,folder+"/"+name+".mat");
            return mat;
        }

        private void BuildPhysicalPilot()
        {
            if(!HasPilot||!PhysicalPilotEligible(pilotSurface))
                throw new InvalidOperationException("Run a passing zero-grading Istanbul pilot preflight first.");
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)
                throw new InvalidOperationException("Physical pilot requires idle edit mode, outside compilation and play mode.");
            if(PrefabStageUtility.GetCurrentPrefabStage()!=null)
                throw new InvalidOperationException("Exit Prefab Mode before building a physical pilot.");
            Scene source=SceneManager.GetActiveScene();
            if(SceneManager.sceneCount!=1||source.isDirty||source.path!="Assets/Scenes/GameScene.unity")
                throw new InvalidOperationException("Open only the saved GameScene in edit mode. Save or discard your own changes before building a pilot.");
            var detected=ProceduralWorldPlanner.DetectLayout(source);
            string key=ProceduralWorldPlanner.PreviewFingerprint(settings,detected,source);
            var pilot=pilotSurface;var plan=preview;
            if(key!=pilot.fingerprint||key!=previewFingerprint||source.path!=pilot.scenePath)
                throw new InvalidOperationException("Pilot inputs changed. Recalculate and preflight before building.");
            var edges=ProceduralWorldPlanner.PhysicalPilotRoads(plan,pilot,settings);
            var sourceTile=detected.tiles.Single(t=>t.row==pilot.row&&t.column==pilot.column);
            var nodes=edges.SelectMany(e=>new[]{e.a,e.b}).Distinct().OrderBy(n=>n.id).ToList();
            if(nodes.Select(n=>n.position).Distinct().Count()!=nodes.Count||nodes.Any(n=>n.gate||n.existing!=null))
                throw new InvalidOperationException("Pilot includes coincident distinct nodes, gate or authored intersection.");
            var degrees=nodes.ToDictionary(n=>n,n=>edges.Count(e=>e.a==n||e.b==n));
            var open=nodes.Where(n=>plan.edges.Concat(plan.authoredLinks).Any(e=>
                (e.a==n||e.b==n)&&!edges.Contains(e))).ToHashSet();
            if(LayerMask.NameToLayer("Road")<0)throw new InvalidOperationException("Required Road physics layer is missing.");
            if(!EditorUtility.DisplayDialog("Build isolated physical pilot?",
                "Create a new, separate test scene and independent assets under "+PhysicalPilotRoot+
                ". Only "+edges.Count+" complete connected roads are included; "+(pilot.roadCount-edges.Count)+
                " other touching corridors, all buildings and gameplay systems are excluded. GameScene is not modified. Continue?",
                "Build separate scene","Cancel"))return;

            if(SceneManager.sceneCount!=1||SceneManager.GetActiveScene()!=source||source.isDirty)
                throw new InvalidOperationException("Source scene changed during confirmation; pilot not created.");
            var originalData=sourceTile.terrain.terrainData;
            var originalMaterial=sourceTile.terrain.materialTemplate;
            bool originalDataDirty=EditorUtility.IsDirty(originalData);
            bool originalMaterialDirty=originalMaterial!=null&&EditorUtility.IsDirty(originalMaterial);
            lastPhysicalPilotSummary=null;lastPhysicalPilotScene=null;
            Scene test=default;
            string folder=null,scenePath=null;
            bool saved=false;
            var looseMeshes=new HashSet<Mesh>();
            int group=-1;
            physicalPilotBuilding=true;
            try
            {
                if(!AssetDatabase.IsValidFolder(PhysicalPilotRoot))
                {
                    string rootGuid=AssetDatabase.CreateFolder("Assets","ProceduralPilotTests");
                    if(string.IsNullOrEmpty(rootGuid)||!AssetDatabase.IsValidFolder(PhysicalPilotRoot))
                        throw new InvalidOperationException("Could not create pilot asset root.");
                }
                string folderGuid=AssetDatabase.CreateFolder(PhysicalPilotRoot,
                    "Istanbul_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N").Substring(0,8));
                if(string.IsNullOrEmpty(folderGuid))throw new InvalidOperationException("Could not create unique pilot folder.");
                folder=AssetDatabase.GUIDToAssetPath(folderGuid);
                scenePath=folder+"/PhysicalPilot.unity";

                test=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
                if(!test.IsValid()||test==source||!test.isLoaded||SceneManager.sceneCount!=2||
                    !source.IsValid()||!source.isLoaded||source.isDirty)
                    throw new InvalidOperationException("Isolated pilot scene creation failed: target "+PilotSceneState(test)+
                        "; source "+PilotSceneState(source)+"; active "+PilotSceneState(SceneManager.GetActiveScene())+
                        "; open count "+SceneManager.sceneCount+".");
                ActivatePilotScene(test,"Isolated pilot scene activation failed");
                Debug.Log("[ProceduralWorld] V7.3.1 isolated scene active: "+PilotSceneState(test)+
                    "; source "+PilotSceneState(source)+".");
                // Do not create or revert an Undo group until the target scene
                // has been verified. Early activation failures own no edits.
                Undo.IncrementCurrentGroup();group=Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Build isolated physical pilot");
                var root=NewRoot(test,"PW Physical Pilot - Infrastructure Only",null);
                var roadsRoot=NewRoot(test,"Roads and junction visuals",root.transform).transform;
                var nodesRoot=NewRoot(test,"Nodes - open means omitted city branches",root.transform).transform;

                // Clone the complete native tile to retain original resolution and
                // halo. No SetHeights, no mutation or assignment of source data.
                var data=Object.Instantiate(originalData);
                data.name="PilotTerrainData";
                AssetDatabase.CreateAsset(data,folder+"/PilotTerrainData.asset");
                var terrainObject=NewRoot(test,"Pilot terrain - full ungraded source tile copy",root.transform);
                terrainObject.transform.position=sourceTile.terrain.transform.position;
                // Never auto-connect a co-located clone to the loaded source tile.
                var terrain=Undo.AddComponent<Terrain>(terrainObject);
                terrain.allowAutoConnect=false;
                terrain.groupingID=-1;
                terrain.terrainData=data;
                var terrainCollider=Undo.AddComponent<TerrainCollider>(terrainObject);
                terrainCollider.terrainData=data;
                Material terrainMaterial=null;
                if(originalMaterial!=null)
                {
                    terrainMaterial=new Material(originalMaterial){name="PilotTerrainMaterial"};
                    AssetDatabase.CreateAsset(terrainMaterial,folder+"/PilotTerrainMaterial.mat");
                    terrain.materialTemplate=terrainMaterial;
                }
                if(terrain.terrainData!=data||terrainCollider.terrainData!=data||data==originalData)
                    throw new InvalidOperationException("Pilot terrain ownership check failed.");
                int resolution=data.heightmapResolution;
                float dx=sourceTile.area.width/(resolution-1),dz=sourceTile.area.height/(resolution-1);
                int hx=Mathf.RoundToInt((pilot.originX-sourceTile.area.xMin)/dx);
                int hz=Mathf.RoundToInt((pilot.originZ-sourceTile.area.yMin)/dz);
                if(hx<0||hz<0||hx+pilot.nx>resolution||hz+pilot.nz>resolution)
                    throw new InvalidOperationException("Pilot native heightmap window is invalid.");
                var native=data.GetHeights(hx,hz,pilot.nx,pilot.nz);
                for(int z=0;z<pilot.nz;z++)for(int x=0;x<pilot.nx;x++)
                    if(Mathf.Abs(terrain.transform.position.y+native[z,x]*data.size.y-pilot.original[z*pilot.nx+x])>.0001f)
                        throw new InvalidOperationException("Cloned heightmap differs from the verified pilot surface.");

                var asphalt=PilotMaterial(folder,"PilotAsphalt",new Color(.13f,.14f,.14f));
                var sidewalk=PilotMaterial(folder,"PilotSidewalk",new Color(.55f,.55f,.53f));
                var markings=PilotMaterial(folder,"PilotMarkings",Color.white,true);

                var intersections=new Dictionary<ProceduralWorldPlanner.Node,RoadIntersection>();
                var junctionParents=new Dictionary<RoadIntersection,Transform>();
                foreach(var n in nodes)
                {
                    var obj=NewRoot(test,(open.Contains(n)?"Pilot_OpenNode_":"Pilot_Node_")+n.id,nodesRoot);
                    obj.transform.position=n.position;
                    var intersection=Undo.AddComponent<RoadIntersection>(obj);
                    intersection.sectorTag=n.sector;
                    intersection.isStopSignControlled=degrees[n]>=3;
                    intersections.Add(n,intersection);
                    junctionParents.Add(intersection,roadsRoot);
                }
                var paths=new Dictionary<SplineRoad,List<Vector3>>();
                foreach(var e in edges)
                {
                    var obj=NewRoot(test,"Pilot_Road_"+e.a.id+"_"+e.b.id,roadsRoot);
                    var road=Undo.AddComponent<SplineRoad>(obj);
                    road.sectorTag=e.sector;
                    road.laneWidth=Mathf.Min(settings.width*.25f,2.1f);
                    var path=new List<Vector3>(e.points);
                    road.SetLinearPath(path);
                    road.startIntersection=intersections[e.a];road.endIntersection=intersections[e.b];
                    road.startIntersection.RegisterConnectedRoad(road);
                    road.endIntersection.RegisterConnectedRoad(road);
                    paths.Add(road,path);
                }
                SimpleRoadPlacementTool.BakePlannedRoads(paths,junctionParents,detected.sea,
                    settings.width,settings.sidewalk,settings.seaY,asphalt,sidewalk,markings);
                foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
                    if(filter.sharedMesh!=null&&!AssetDatabase.Contains(filter.sharedMesh))looseMeshes.Add(filter.sharedMesh);

                var localLayout=new ProceduralWorldPlanner.Layout{low=detected.low,high=detected.high,sea=detected.sea,rowsOnX=detected.rowsOnX};
                localLayout.tiles.Add(new ProceduralWorldPlanner.Tile{terrain=terrain,area=sourceTile.area,row=sourceTile.row,column=sourceTile.column});
                var localPlan=new ProceduralWorldPlanner.Plan{layout=localLayout};
                localPlan.edges.AddRange(edges);
                ProceduralWorldTerrain.Validate(localPlan,settings);
                ValidateGeometry(paths);
                ValidateJunctionGeometry(junctionParents,localLayout);
                foreach(var entry in intersections)
                    if(entry.Value.connectedRoads.Count!=degrees[entry.Key]||
                        entry.Value.connectedRoads.Any(r=>r==null||r.gameObject.scene!=test))
                        throw new InvalidOperationException("Pilot junction road membership mismatch.");
                int raySamples=ValidatePilotColliders(paths,junctionParents,localLayout,out float maxSurfaceError);

                // Scene-local lighting only. No global RenderSettings or game
                // manager/bootstrap objects; this is not a playable game scene yet.
                var sun=NewRoot(test,"Pilot Sun",root.transform);
                sun.transform.rotation=Quaternion.Euler(50f,-30f,0f);
                var light=Undo.AddComponent<Light>(sun);light.type=LightType.Directional;light.intensity=1.1f;

                if(source.isDirty||sourceTile.terrain.terrainData!=originalData||sourceTile.terrain.materialTemplate!=originalMaterial||
                    EditorUtility.IsDirty(originalData)!=originalDataDirty||
                    (originalMaterial!=null&&EditorUtility.IsDirty(originalMaterial)!=originalMaterialDirty)||
                    ProceduralWorldPlanner.PreviewFingerprint(settings,detected,source)!=key)
                    throw new InvalidOperationException("Source scene/input changed unexpectedly; abort pilot without saving GameScene.");
                PersistMeshes(new List<GameObject>{root},folder);
                foreach(var mesh in looseMeshes)
                    if(mesh==null||!AssetDatabase.Contains(mesh))throw new InvalidOperationException("Pilot mesh persistence failed.");
                AssetDatabase.SaveAssetIfDirty(data);
                if(terrainMaterial!=null)AssetDatabase.SaveAssetIfDirty(terrainMaterial);
                AssetDatabase.SaveAssetIfDirty(asphalt);
                AssetDatabase.SaveAssetIfDirty(sidewalk);
                AssetDatabase.SaveAssetIfDirty(markings);
                var colliderCount=root.GetComponentsInChildren<MeshCollider>(true).Count(c=>c.enabled&&c.sharedMesh!=null);
                var roadIds=string.Join(", ",edges.Select(e=>e.a.id+"-"+e.b.id));
                string summary="V7.3.1 PHYSICAL INFRASTRUCTURE PASS: "+scenePath+
                    "; source fingerprint "+key+"; tile ["+pilot.row+","+pilot.column+"] "+pilot.region.ToString("F1")+
                    "; roads "+paths.Count+" ("+roadIds+"), nodes "+nodes.Count+
                    ", open/external branch nodes "+open.Count+", full junction meshes "+junctionParents.Count(x=>x.Key.connectedRoads.Count>=3)+
                    ", mesh colliders "+colliderCount+", asphalt collider ray samples "+raySamples+
                    ", max ray surface deviation "+maxSurfaceError.ToString("F3")+" m; terrain original clone, zero grading; "+
                    (pilot.roadCount-edges.Count)+" touching corridors excluded, 0 buildings placed. "+
                    "Scene-local infrastructure proof only; boundary connections, lot access, building placement and driving not validated. Full Generate locked.";
                File.WriteAllText(folder+"/PilotReport.txt",summary);
                AssetDatabase.ImportAsset(folder+"/PilotReport.txt");
                if(!EditorSceneManager.SaveScene(test,scenePath))
                    throw new InvalidOperationException("Pilot scene could not be saved.");
                saved=true;
                lastPhysicalPilotScene=scenePath;lastPhysicalPilotSummary=summary;
                Undo.CollapseUndoOperations(group);
                Debug.Log("[ProceduralWorld] "+summary);
                SetStatus("Physical pilot saved. Select its scene asset and open it separately for Scene View inspection. GameScene was not saved or modified.");
            }
            catch
            {
                // Undo only our own group while the test scene is active.
                // Never clear, save, refresh network or delete assets in GameScene.
                if(group>=0)
                {
                    if(test.IsValid()&&test.isLoaded&&test!=source)
                    {
                        foreach(var obj in test.GetRootGameObjects())
                            foreach(var filter in obj.GetComponentsInChildren<MeshFilter>(true))
                                if(filter.sharedMesh!=null&&!AssetDatabase.Contains(filter.sharedMesh))looseMeshes.Add(filter.sharedMesh);
                    }
                    Undo.RevertAllDownToGroup(group);
                }
                throw;
            }
            finally
            {
                try
                {
                    if(source.IsValid()&&source.isLoaded)
                    {
                        try{ActivatePilotScene(source,"Restore original active scene failed");}
                        catch(Exception ex){Debug.LogException(ex);}
                    }
                    if(test.IsValid()&&test!=source&&!EditorSceneManager.CloseScene(test,true))
                        Debug.LogError("[ProceduralWorld] Could not close isolated pilot scene. Close it manually without saving any failed result.");
                    if(!saved)
                    {
                        foreach(var mesh in looseMeshes)
                            if(mesh!=null&&!AssetDatabase.Contains(mesh))Object.DestroyImmediate(mesh);
                        if(!string.IsNullOrEmpty(folder)&&AssetDatabase.IsValidFolder(folder)&&!AssetDatabase.DeleteAsset(folder))
                            Debug.LogError("[ProceduralWorld] Failed to delete disposable pilot folder: "+folder);
                    }
                }
                finally
                {
                    physicalPilotBuilding=false;
                    if(SceneManager.GetActiveScene()!=source)
                        OnPreviewSceneChanged(source,SceneManager.GetActiveScene());
                }
            }
            var asset=AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if(asset!=null){Selection.activeObject=asset;EditorGUIUtility.PingObject(asset);}
            SceneView.RepaintAll();
        }

        private int ValidatePilotColliders(Dictionary<SplineRoad,List<Vector3>> roads,
            Dictionary<RoadIntersection,Transform> junctionParents,ProceduralWorldPlanner.Layout ground,out float maxError)
        {
            var asphalt=new List<MeshCollider>();
            void AddCollider(Transform parent,string name,List<MeshCollider> list)
            {
                var child=parent.Find(name);
                var c=child!=null?child.GetComponent<MeshCollider>():null;
                var f=child!=null?child.GetComponent<MeshFilter>():null;
                if(c==null||f==null||!c.enabled||!child.gameObject.activeInHierarchy||
                    c.sharedMesh==null||c.sharedMesh!=f.sharedMesh||c.sharedMesh.vertexCount<3||c.sharedMesh.triangles.Length<3)
                    throw new InvalidOperationException("Missing or mismatched pilot "+name+" collider on "+parent.name);
                if(list!=null)
                {
                    if(c.gameObject.layer!=LayerMask.NameToLayer("Road"))
                        throw new InvalidOperationException("Pilot asphalt collider is not on the Road layer.");
                    list.Add(c);
                }
            }
            foreach(var road in roads.Keys)
            {
                AddCollider(road.transform,"Asphalt",asphalt);
                if(settings.sidewalk>.01f)AddCollider(road.transform,"Sidewalks",null);
            }
            foreach(var entry in junctionParents)
            {
                if(entry.Key.connectedRoads.Count<3)continue;
                var visual=entry.Value.Find("Intersection_"+entry.Key.name);
                if(visual==null)throw new InvalidOperationException("Pilot junction visual missing.");
                AddCollider(visual,"Asphalt",asphalt);
                if(settings.sidewalk>.01f)AddCollider(visual,"SidewalkBase",null);
            }
            Physics.SyncTransforms();
            int probes=0;float largest=0f;
            void Probe(Vector3 expected)
            {
                Vector2 xz=ProceduralWorldPlanner.XZ(expected);
                if(!ground.TryHeight(xz,out float terrainY))throw new InvalidOperationException("Pilot collider probe outside cloned land.");
                var ray=new Ray(expected+Vector3.up*5f,Vector3.down);
                bool found=false;float top=float.NegativeInfinity;
                foreach(var c in asphalt)
                    if(c.Raycast(ray,out RaycastHit hit,10f)&&hit.point.y>top){top=hit.point.y;found=true;}
                float error=Mathf.Abs(top-(expected.y+.03f));
                if(!found||error>.15f||terrainY-top>.08f)
                    throw new InvalidOperationException("Pilot asphalt collider continuity/clearance failed at "+expected+
                        (found?"; surface Y "+top.ToString("F3")+", terrain "+terrainY.ToString("F3"):"; no collider ray hit"));
                largest=Mathf.Max(largest,error);probes++;
            }
            foreach(var pair in roads)
            for(int i=1;i<pair.Value.Count;i++)
            {
                Vector3 a=pair.Value[i-1],b=pair.Value[i];Vector2 d=ProceduralWorldPlanner.XZ(b-a);
                if(d.sqrMagnitude<.000001f)throw new InvalidOperationException("Duplicate pilot road sample.");
                Vector2 right=new Vector2(-d.y,d.x).normalized;
                int stations=Mathf.Max(1,Mathf.CeilToInt(d.magnitude));
                // Interior stations avoid exact outer mesh edge floating-point
                // ambiguity; shared node center is checked separately below.
                for(int j=0;j<stations;j++)
                {
                    Vector3 mid=Vector3.Lerp(a,b,(j+.5f)/stations);
                    foreach(float offset in new[]{-settings.width*.4f,0f,settings.width*.4f})
                        Probe(mid+new Vector3(right.x*offset,0,right.y*offset));
                }
            }
            foreach(var entry in junctionParents)
                if(entry.Key.connectedRoads.Count>=2)Probe(entry.Key.transform.position);
            maxError=largest;
            return probes;
        }
    }
}
