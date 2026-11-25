using System;
using System.Collections.Generic;
using System.Linq;
using DeliveryDriver.City;
using TrafficSystem.Spline;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeliveryDriver.EditorTools
{
    public sealed class ProceduralWorldGeneratorWindow : EditorWindow
    {
        private const string AssetParent = "Assets/Art/Terrains";
        private const string AssetRootName = "ProceduralGenerated";
        private readonly ProceduralWorldPlanner.Settings settings = new ProceduralWorldPlanner.Settings();
        private ProceduralWorldPlanner.Layout layout;
        private ProceduralWorldPlanner.Plan preview;
        private Vector2 scroll;
        private string status = "Detect the 24 land tiles and optional 4 sea tiles in GameScene.";
        private MessageType statusType = MessageType.Info;
        private bool showPreview = true;
        private bool busy;

        [MenuItem("Tools/Delivery Driver/City/Procedural World Generator", false, 1)]
        public static void Open()
        {
            var window = GetWindow<ProceduralWorldGeneratorWindow>("Procedural World");
            window.minSize = new Vector2(420f, 580f);
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += DrawPreview;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawPreview;
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnUndoRedo()
        {
            preview = null;
            layout = null;
            RefreshNetwork(SceneManager.GetActiveScene(), false);
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Procedural World Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Roads-only MVP: 4 x 3 Ankara + sea corridor + 4 x 3 Istanbul. Sea can be empty or contain four terrain tiles. Existing Simple Road Placement geometry; no EasyRoads.", MessageType.Info);
            using (new EditorGUI.DisabledScope(busy || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Detect Terrain Layout & City Roots", GUILayout.Height(28f))) Run(Detect);
                if (layout != null)
                {
                    EditorGUILayout.LabelField("Layout", "12 land + " + layout.SeaTerrainCount + " sea terrains + 12 land");
                    EditorGUILayout.LabelField("Sea corridor width", (layout.AlongMax(layout.sea) - layout.AlongMin(layout.sea)).ToString("F1") + " m");
                    EditorGUILayout.LabelField("Row direction", layout.rowsOnX ? "World X" : "World Z");
                    EditorGUILayout.LabelField("Tile size", layout.tiles[0].area.size.ToString());
                }

                EditorGUI.BeginChangeCheck();
                settings.ankara = (Transform)EditorGUILayout.ObjectField("Ankara Root", settings.ankara, typeof(Transform), true);
                settings.istanbul = (Transform)EditorGUILayout.ObjectField("Istanbul Root", settings.istanbul, typeof(Transform), true);
                settings.ankaraLow = EditorGUILayout.Toggle("Ankara on low coordinate side", settings.ankaraLow);
                settings.buildAnkara = EditorGUILayout.Toggle("Generate Ankara", settings.buildAnkara);
                settings.buildIstanbul = EditorGUILayout.Toggle("Generate Istanbul", settings.buildIstanbul);
                using (new EditorGUI.DisabledScope(!settings.buildAnkara || !settings.buildIstanbul))
                    settings.bridges = EditorGUILayout.Toggle("Connect with two bridges", settings.bridges);
                settings.protectKizilay = EditorGUILayout.Toggle("Protect existing Kizilay district", settings.protectKizilay);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Road layout", EditorStyles.boldLabel);
                settings.seed = EditorGUILayout.IntField("Seed", settings.seed);
                settings.spacing = Mathf.Clamp(EditorGUILayout.FloatField("Target spacing (m)", settings.spacing), 45f, 500f);
                settings.maxNodesPerCity = EditorGUILayout.IntSlider("Node budget per city", settings.maxNodesPerCity, 40, 350);
                settings.curve = EditorGUILayout.Slider("Curvature", settings.curve, 0f, 1f);
                settings.loops = EditorGUILayout.Slider("Extra route probability", settings.loops, 0f, 1f);
                settings.width = Mathf.Clamp(EditorGUILayout.FloatField("Uniform asphalt width (m)", settings.width), 5f, 12f);
                settings.sidewalk = Mathf.Clamp(EditorGUILayout.FloatField("Sidewalk width (m)", settings.sidewalk), .5f, 3f);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Terrain and bridges", EditorStyles.boldLabel);
                settings.grade = EditorGUILayout.Slider("Maximum grade (%)", settings.grade * 100f, 4f, 15f) / 100f;
                settings.maxCutFill = Mathf.Clamp(EditorGUILayout.FloatField("Max cut/fill (m)", settings.maxCutFill), 1f, 20f);
                settings.blend = Mathf.Clamp(EditorGUILayout.FloatField("Terrain shoulder blend (m)", settings.blend), 3f, 30f);
                settings.seaY = EditorGUILayout.FloatField("Sea surface Y", settings.seaY);
                settings.bridgeClearance = Mathf.Clamp(EditorGUILayout.FloatField("Bridge clearance (m)", settings.bridgeClearance), 3f, 30f);
                if (EditorGUI.EndChangeCheck())
                {
                    preview = null;
                    SceneView.RepaintAll();
                }
                showPreview = EditorGUILayout.Toggle("Show Scene View preview", showPreview);

                EditorGUILayout.Space();
                if (GUILayout.Button("Preview Layout (no scene changes)", GUILayout.Height(32f))) Run(PlanOnly);
                if (preview != null)
                {
                    EditorGUILayout.LabelField("Planned roads", preview.edges.Count.ToString());
                    EditorGUILayout.LabelField("Planned bridge links", preview.BridgeCount.ToString());
                    if (settings.bridges && settings.buildAnkara && settings.buildIstanbul && preview.BridgeCount != 2)
                        EditorGUILayout.HelpBox("Preview only: requested bridge links are missing. Generate requires two valid bridges, or explicitly disable bridges.", MessageType.Warning);
                    EditorGUILayout.LabelField("Road length", (preview.edges.Sum(e => PathLength(e.points)) / 1000f).ToString("F2") + " km");
                    foreach (string warning in preview.warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
                }
                bool hasBuild = FindStates(SceneManager.GetActiveScene()).Count > 0;
                using (new EditorGUI.DisabledScope(hasBuild || (!settings.buildAnkara && !settings.buildIstanbul)))
                {
                    if (GUILayout.Button("Generate Roads & Bridges", GUILayout.Height(36f)))
                    {
                        if (EditorUtility.DisplayDialog("Generate procedural roads",
                            "Only new generated roots will be created. TerrainData will be copied before grading. Existing manual roads and buildings are not deleted. Continue?",
                            "Generate", "Cancel")) Run(Generate);
                    }
                }
                using (new EditorGUI.DisabledScope(!hasBuild))
                {
                    if (GUILayout.Button("Clear Generated Roads / Restore Terrain", GUILayout.Height(28f)) &&
                        EditorUtility.DisplayDialog("Clear procedural build",
                            "Remove only the recorded generated roots (including edits inside those roots) and restore original TerrainData references? Generated asset files remain for Undo.", "Restore", "Cancel"))
                        Run(Clear);
                }
                if (hasBuild)
                    EditorGUILayout.HelpBox("A procedural build already exists. Clear it before generating again. Do not manually reparent its roots.", MessageType.Warning);
            }

            EditorGUILayout.HelpBox(status, statusType);
            EditorGUILayout.HelpBox("Do not run the old Ankara/Istanbul Master Builder on this build. Buildings can be placed later with the existing Roadside Building Placement tool; select only generated roads and disable global clearing. This MVP does not reshape the sea tiles or create water.", MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        private void Run(Action action)
        {
            if (busy) return;
            busy = true;
            try { action(); }
            catch (OperationCanceledException) { SetStatus("Canceled; no partial build retained.", MessageType.Warning); }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                SetStatus(ex.Message, MessageType.Error);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                busy = false;
                Repaint();
                SceneView.RepaintAll();
            }
        }

        private void SetStatus(string message, MessageType type = MessageType.Info)
        {
            status = message;
            statusType = type;
            if (type == MessageType.Info) Debug.Log("[ProceduralWorld] " + message);
        }

        private void Detect()
        {
            Scene scene = SceneManager.GetActiveScene();
            preview = null;
            layout = null;
            layout = ProceduralWorldPlanner.DetectLayout(scene);
            settings.ankara = FindNamed(scene, "Ankara");
            settings.istanbul = FindNamed(scene, "İstanbul", "Istanbul");
            Transform sea = FindNamed(scene, "Sea Reference");
            if (sea != null) settings.seaY = sea.position.y;
            bool sideKnown = true;
            if (TryInferSide(settings.ankara, out bool isLow)) settings.ankaraLow = isLow;
            else if (TryInferSide(settings.istanbul, out isLow)) settings.ankaraLow = !isLow;
            else sideKnown = false;
            SetStatus("Detected " + layout.tiles.Count + " terrains (24 land, " + layout.SeaTerrainCount +
                " sea). " + (sideKnown ? "Verify city roots, Ankara side and Sea surface Y; then preview." :
                    "City side could not be inferred; set the Ankara side checkbox and city roots manually."),
                sideKnown ? MessageType.Info : MessageType.Warning);
        }

        private bool TryInferSide(Transform root, out bool low)
        {
            low = false;
            if (root == null || layout == null) return false;
            int lowCount = 0, highCount = 0;
            foreach (var tile in layout.tiles)
            {
                if (!tile.terrain.transform.IsChildOf(root)) continue;
                if (tile.row < 3) lowCount++;
                if (tile.row > 3) highCount++;
            }
            if (lowCount != highCount)
            {
                low = lowCount > highCount;
                return true;
            }
            foreach (NeighborhoodZone zone in root.GetComponentsInChildren<NeighborhoodZone>(true))
            {
                Vector2 p = ProceduralWorldPlanner.XZ(zone.transform.position);
                if (layout.low.Contains(p)) lowCount++;
                if (layout.high.Contains(p)) highCount++;
            }
            if (lowCount == highCount) return false;
            low = lowCount > highCount;
            return true;
        }

        private void PlanOnly() => PlanOnly(true);

        private void PlanOnly(bool allowBridgeFallback)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (FindStates(scene).Count > 0) throw new InvalidOperationException("Clear the previous build before previewing a new one.");
            if (layout == null) Detect();
            // Re-detect in case terrain transforms changed after the last preview.
            layout = ProceduralWorldPlanner.DetectLayout(scene);
            ValidateRoots(scene);
            if (!settings.buildAnkara && !settings.buildIstanbul) throw new InvalidOperationException("Select a city.");
            if (EditorUtility.DisplayCancelableProgressBar("Procedural World", "Planning safe roads and bridge approaches", .05f))
                throw new OperationCanceledException();
            preview = null;
            try
            {
                preview = ProceduralWorldPlanner.Build(settings, layout, scene);
                SetStatus("Preview ready: " + preview.edges.Count + " roads, " + preview.BridgeCount + " bridges. No scene assets changed.");
            }
            catch (ProceduralWorldPlanner.BridgePlanningException ex) when (allowBridgeFallback)
            {
                // Show the city roads even if the bridge constraints are infeasible.
                // Generation remains strict unless the user explicitly disables bridges.
                bool requested = settings.bridges;
                try
                {
                    settings.bridges = false;
                    preview = ProceduralWorldPlanner.Build(settings, layout, scene);
                    preview.warnings.Insert(0, "BRIDGES NOT PLANNED: " + ex.Message);
                    SetStatus("City-only preview shown. Two-bridge generation is blocked until a valid bridge plan exists. " +
                        "Adjust the bridge settings or explicitly uncheck Connect with two bridges to generate just city roads.", MessageType.Warning);
                    Debug.LogWarning("[ProceduralWorld] " + ex.Message);
                }
                finally { settings.bridges = requested; }
            }
        }

        private void ValidateRoots(Scene scene)
        {
            if (settings.buildAnkara && (settings.ankara == null || settings.ankara.gameObject.scene != scene))
                throw new InvalidOperationException("Assign the existing Ankara root from the active scene.");
            if (settings.buildIstanbul && (settings.istanbul == null || settings.istanbul.gameObject.scene != scene))
                throw new InvalidOperationException("Assign the existing Istanbul root from the active scene.");
            if (settings.buildAnkara && settings.buildIstanbul &&
                (settings.ankara == settings.istanbul || settings.ankara.IsChildOf(settings.istanbul) || settings.istanbul.IsChildOf(settings.ankara)))
                throw new InvalidOperationException("City roots must be distinct, independent objects.");
            if (settings.buildAnkara && TryInferSide(settings.ankara, out bool sideA) && sideA != settings.ankaraLow)
                throw new InvalidOperationException("Ankara root terrain/district positions disagree with the selected side. Correct the side or root.");
            if (settings.buildIstanbul && TryInferSide(settings.istanbul, out bool sideI) && sideI == settings.ankaraLow)
                throw new InvalidOperationException("Istanbul root terrain/district positions disagree with the selected side. Correct the side or root.");
        }

        private void Generate()
        {
            PlanOnly(false);
            Scene scene = SceneManager.GetActiveScene();
            var managers = FindComponents<RoadNetworkManager>(scene);
            if (managers.Count != 1)
                throw new InvalidOperationException("Expected exactly one RoadNetworkManager in the active scene; found " + managers.Count + ".");
            RoadNetworkManager manager = managers[0];

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Generate procedural world");
            string folder = null;
            try
            {
                folder = CreateBuildFolder();
                var stateObject = NewRoot(scene, "Procedural World Build State", null);
                var state = Undo.AddComponent<ProceduralWorldBuildState>(stateObject);
                Undo.RegisterCompleteObjectUndo(state, "Record procedural build manifest");
                state.seed = settings.seed;
                state.assetFolder = folder;
                var roots = new Dictionary<string, Transform>();
                if (settings.buildAnkara)
                {
                    state.cityRoots.Add(settings.ankara);
                    roots["Ankara"] = CreateGeneratedRoot(scene, settings.ankara, state, "Ankara");
                }
                if (settings.buildIstanbul)
                {
                    state.cityRoots.Add(settings.istanbul);
                    roots["İstanbul"] = CreateGeneratedRoot(scene, settings.istanbul, state, "Istanbul");
                }
                if (preview.BridgeCount > 0)
                {
                    Transform highways = FindNamed(scene, "Highways & Bridges", "Highways");
                    if (highways == null)
                        highways = NewRoot(scene, "Highways & Bridges", FindNamed(scene, "World")).transform;
                    if ((settings.ankara != null && highways.IsChildOf(settings.ankara)) ||
                        (settings.istanbul != null && highways.IsChildOf(settings.istanbul)))
                        throw new InvalidOperationException("Highways & Bridges must not be parented under a streamed city.");
                    roots["Highways"] = CreateGeneratedRoot(scene, highways, state, "Highways");
                }
                ProceduralWorldTerrain.Apply(preview, settings, state, folder);
                ProceduralWorldTerrain.Validate(preview, settings);

                var junctionRoot = NewRoot(scene, "Generated Road Intersections", stateObject.transform).transform;
                var junctions = new Dictionary<ProceduralWorldPlanner.Node, RoadIntersection>();
                var junctionParents = new Dictionary<RoadIntersection, Transform>();
                foreach (var node in preview.nodes)
                {
                    if (node.degree == 0) continue;
                    RoadIntersection intersection = node.existing;
                    if (intersection != null)
                    {
                        Undo.RecordObject(intersection, "Connect authored dead end");
                    }
                    else
                    {
                        GameObject obj = NewRoot(scene, "PW_Node_" + node.id.ToString("D4"), junctionRoot);
                        obj.transform.position = node.position;
                        intersection = Undo.AddComponent<RoadIntersection>(obj);
                        Undo.RecordObject(intersection, "Configure generated intersection");
                        intersection.sectorTag = node.sector;
                        intersection.isStopSignControlled = node.degree >= 3;
                        junctionParents.Add(intersection, roots[node.sector]);
                    }
                    junctions.Add(node, intersection);
                }

                var paths = new Dictionary<SplineRoad, List<Vector3>>();
                for (int i = 0; i < preview.edges.Count; i++)
                {
                    if (i % 10 == 0 && EditorUtility.DisplayCancelableProgressBar("Procedural World", "Creating spline roads " + i + "/" + preview.edges.Count, .59f + .14f * i / preview.edges.Count))
                        throw new OperationCanceledException();
                    var edge = preview.edges[i];
                    string name = edge.Bridge ? "PW_Bridge_" : "PW_Road_";
                    GameObject obj = NewRoot(scene, name + i.ToString("D4"), roots[edge.sector]);
                    SplineRoad road = Undo.AddComponent<SplineRoad>(obj);
                    Undo.RecordObject(road, "Configure generated spline");
                    road.sectorTag = edge.sector;
                    road.laneWidth = Mathf.Min(settings.width * .25f, 2.1f);
                    // The same sampled polyline is the source of both the spline and its visual surface.
                    road.SetLinearPath(edge.points);
                    road.startIntersection = junctions[edge.a];
                    road.endIntersection = junctions[edge.b];
                    road.startIntersection.RegisterConnectedRoad(road);
                    road.endIntersection.RegisterConnectedRoad(road);
                    paths.Add(road, edge.points);
                    EditorUtility.SetDirty(road);
                }

                if (EditorUtility.DisplayCancelableProgressBar("Procedural World", "Baking existing road and intersection geometry", .76f))
                    throw new OperationCanceledException();
                SimpleRoadPlacementTool.BakePlannedRoads(paths, junctionParents, preview.layout.sea,
                    settings.width, settings.sidewalk, settings.seaY);
                ValidateGeometry(paths);
                ValidateJunctionGeometry(junctionParents, preview.layout);
                PersistMeshes(state.generatedRoots, folder);
                Undo.RecordObject(manager, "Refresh generated road network");
                manager.RefreshNetwork();
                EditorUtility.SetDirty(manager);
                EditorUtility.SetDirty(state);
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(undoGroup);
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = stateObject;
                SetStatus("Generated " + paths.Count + " roads including " + preview.BridgeCount + " bridges. Original TerrainData preserved. Save the scene only after inspecting the result.");
                preview = null;
                layout = null;
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                if (!string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.DeleteAsset(folder);
                RefreshNetwork(scene, false);
                throw;
            }
        }

        private void Clear()
        {
            Scene scene = SceneManager.GetActiveScene();
            List<ProceduralWorldBuildState> states = FindStates(scene);
            if (states.Count != 1) throw new InvalidOperationException("Expected exactly one build manifest; cannot safely clear.");
            var state = states[0];
            if (state.terrains.Count != state.originalData.Count || state.terrains.Count != state.generatedData.Count)
                throw new InvalidOperationException("Build manifest is incomplete; restore terrain manually.");
            for (int i = 0; i < state.terrains.Count; i++)
            {
                Terrain terrain = state.terrains[i];
                if (terrain == null || state.originalData[i] == null || state.generatedData[i] == null ||
                    (terrain.terrainData != state.generatedData[i] && terrain.terrainData != state.originalData[i]))
                    throw new InvalidOperationException("Terrain reference changed since generation; refusing to overwrite it.");
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Clear procedural world");
            try
            {
                for (int i = 0; i < state.terrains.Count; i++)
                    ProceduralWorldTerrain.Assign(state.terrains[i], state.originalData[i], "Restore original terrain");
                foreach (GameObject root in state.generatedRoots)
                {
                    if (root != null) Undo.DestroyObjectImmediate(root);
                }
                Undo.DestroyObjectImmediate(state.gameObject);
                RefreshNetwork(scene, true);
                Undo.CollapseUndoOperations(group);
                EditorSceneManager.MarkSceneDirty(scene);
                layout = null;
                preview = null;
                SetStatus("Generated roads cleared, original TerrainData restored. Generated assets retained for Undo; no authored content deleted.");
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                RefreshNetwork(scene, false);
                throw;
            }
        }

        private static void ValidateGeometry(Dictionary<SplineRoad, List<Vector3>> roads)
        {
            foreach (var pair in roads)
            {
                SplineRoad road = pair.Key;
                List<Vector3> points = pair.Value;
                Transform asphalt = road.transform.Find("Asphalt");
                MeshCollider collider = asphalt != null ? asphalt.GetComponent<MeshCollider>() : null;
                if (!road.IsUsable || road.startIntersection == null || road.endIntersection == null ||
                    Vector3.Distance(road.GetPoint(0f), points[0]) > .02f ||
                    Vector3.Distance(road.GetPoint(1f), points[points.Count - 1]) > .02f ||
                    collider == null || collider.sharedMesh == null || collider.sharedMesh.vertexCount < 3)
                    throw new InvalidOperationException("Generated road failed spline/collider validation: " + road.name);
                if (road.sectorTag == "Highways")
                {
                    Transform structure = road.transform.Find("Bridge Structures");
                    if (structure == null || !structure.gameObject.activeSelf)
                        throw new InvalidOperationException("Bridge did not produce a support structure: " + road.name);
                }
            }
        }

        private static void ValidateJunctionGeometry(Dictionary<RoadIntersection, Transform> junctionParents, ProceduralWorldPlanner.Layout layout)
        {
            foreach (var pair in junctionParents)
            {
                if (pair.Key.connectedRoads.Count < 3) continue;
                Transform visual = pair.Value.Find("Intersection_" + pair.Key.name);
                Transform asphalt = visual != null ? visual.Find("Asphalt") : null;
                MeshCollider collider = asphalt != null ? asphalt.GetComponent<MeshCollider>() : null;
                if (collider == null || collider.sharedMesh == null || collider.sharedMesh.vertexCount < 3)
                    throw new InvalidOperationException("Missing generated junction mesh: " + pair.Key.name);
                foreach (Vector3 local in collider.sharedMesh.vertices)
                {
                    Vector3 point = asphalt.TransformPoint(local);
                    if (layout.TryHeight(ProceduralWorldPlanner.XZ(point), out float ground) && ground - point.y > .10f)
                        throw new InvalidOperationException("Junction terrain clearance failed near " + point + ". Try another seed or a larger grading budget.");
                }
            }
        }

        private static string CreateBuildFolder()
        {
            string root = AssetParent + "/" + AssetRootName;
            if (!AssetDatabase.IsValidFolder(root))
                AssetDatabase.CreateFolder(AssetParent, AssetRootName);
            string guid = AssetDatabase.CreateFolder(root, "Build_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
            if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("Could not create generated asset folder.");
            return AssetDatabase.GUIDToAssetPath(guid);
        }

        private static void PersistMeshes(List<GameObject> roots, string folder)
        {
            var meshes = new HashSet<Mesh>();
            foreach (GameObject root in roots)
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.sharedMesh != null && !AssetDatabase.Contains(filter.sharedMesh))
                        meshes.Add(filter.sharedMesh);
            string path = folder + "/RoadMeshes.asset";
            bool first = true;
            foreach (Mesh mesh in meshes)
            {
                if (first) { AssetDatabase.CreateAsset(mesh, path); first = false; }
                else AssetDatabase.AddObjectToAsset(mesh, path);
            }
            if (!first) AssetDatabase.ImportAsset(path);
        }

        private static Transform CreateGeneratedRoot(Scene scene, Transform parent, ProceduralWorldBuildState state, string suffix)
        {
            GameObject root = NewRoot(scene, "Generated Procedural Roads - " + suffix, parent);
            state.generatedRoots.Add(root);
            return root.transform;
        }

        private static GameObject NewRoot(Scene scene, string name, Transform parent)
        {
            var obj = new GameObject(name);
            SceneManager.MoveGameObjectToScene(obj, scene);
            Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
            if (parent != null) Undo.SetTransformParent(obj.transform, parent, "Parent " + name);
            return obj;
        }

        private static List<T> FindComponents<T>(Scene scene) where T : Component
        {
            var list = new List<T>();
            if (!scene.IsValid() || !scene.isLoaded) return list;
            foreach (GameObject root in scene.GetRootGameObjects())
                list.AddRange(root.GetComponentsInChildren<T>(true));
            return list;
        }

        private static List<ProceduralWorldBuildState> FindStates(Scene scene) => FindComponents<ProceduralWorldBuildState>(scene);

        private static Transform FindNamed(Scene scene, params string[] names)
        {
            return FindComponents<Transform>(scene).FirstOrDefault(t =>
                names.Any(n => string.Equals(t.name, n, StringComparison.OrdinalIgnoreCase)));
        }

        private static void RefreshNetwork(Scene scene, bool recordUndo)
        {
            foreach (RoadNetworkManager manager in FindComponents<RoadNetworkManager>(scene))
            {
                if (recordUndo) Undo.RecordObject(manager, "Refresh procedural network");
                manager.RefreshNetwork();
                EditorUtility.SetDirty(manager);
            }
        }

        private void DrawPreview(SceneView sceneView)
        {
            if (!showPreview || layout == null || Event.current.type != EventType.Repaint) return;
            Handles.color = new Color(.25f, .75f, .95f, .9f);
            DrawArea(layout.Ankara(settings.ankaraLow), "Ankara (12 tiles)");
            Handles.color = new Color(.95f, .65f, .25f, .9f);
            DrawArea(layout.Istanbul(settings.ankaraLow), "Istanbul (12 tiles)");
            Handles.color = new Color(.25f, .45f, 1f, .8f);
            DrawArea(layout.sea, layout.SeaTerrainCount == 0 ? "Sea (empty corridor)" : "Sea (4 tiles)");
            if (preview == null) return;
            foreach (var edge in preview.edges)
            {
                Handles.color = edge.Bridge ? Color.yellow : edge.sector == "Ankara" ? Color.cyan : new Color(1f, .65f, .2f);
                Handles.DrawAAPolyLine(edge.Bridge ? 5f : 2.5f, edge.points.Select(p => p + Vector3.up * .5f).ToArray());
            }
        }

        private void DrawArea(Rect r, string title)
        {
            float y = settings.seaY + 3f;
            Vector3[] points = {
                new Vector3(r.xMin, y, r.yMin), new Vector3(r.xMax, y, r.yMin),
                new Vector3(r.xMax, y, r.yMax), new Vector3(r.xMin, y, r.yMax),
                new Vector3(r.xMin, y, r.yMin)
            };
            Handles.DrawAAPolyLine(3f, points);
            Handles.Label(new Vector3(r.center.x, y + 4f, r.center.y), title);
        }

        private static float PathLength(List<Vector3> points)
        {
            float result = 0f;
            for (int i = 1; i < points.Count; i++) result += Vector3.Distance(points[i - 1], points[i]);
            return result;
        }
    }
}
