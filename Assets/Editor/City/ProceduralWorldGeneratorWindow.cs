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
    public sealed partial class ProceduralWorldGeneratorWindow : EditorWindow
    {
        private const string AssetParent = "Assets/Art/Terrains";
        private const string AssetRootName = "ProceduralGenerated";
        private readonly ProceduralWorldPlanner.Settings settings = new ProceduralWorldPlanner.Settings();
        private ProceduralWorldPlanner.Layout layout;
        private ProceduralWorldPlanner.Plan preview;
        private ProceduralWorldPlanner.Plan cachedPreview;
        private readonly Dictionary<ProceduralWorldPlanner.Edge, Vector3[]> previewGeometry = new Dictionary<ProceduralWorldPlanner.Edge, Vector3[]>();
        private Vector2 scroll;
        private string status = "Detect the 24 land tiles and optional 4 sea tiles in GameScene.";
        private MessageType statusType = MessageType.Info;
        private bool showPreview = true;
        private bool showArterials = true, showCollectors = true, showLocals = true, showLinks = true, showBridgeLines = true;
        private bool showDetached = true;
        private bool showGrading = true, xrayPreview = true;
        private bool showTerrainAnalysis = false, analysisOnly = false;
        private enum TerrainLayer { Slope, Elevation, Buildability, Contours, Seams, Earthworks, GradingZones, Hotspots, VerticalProfile, VirtualTerrain }
        private TerrainLayer terrainLayer = TerrainLayer.Slope;
        private bool showParcels = false, showOpenSpaces = true, pickAnchor, fastPreview = true;
        private SplineRoad anchorRoad;
        private float anchorT = .5f;
        private readonly Dictionary<int,Vector3[]> lineBatches = new Dictionary<int,Vector3[]>();
        private readonly Dictionary<ProceduralWorldPlanner.TerrainAnalysisCell,Vector3[]> cellQuads = new Dictionary<ProceduralWorldPlanner.TerrainAnalysisCell,Vector3[]>();
        private float elevationMin,elevationMax;
        private ProceduralWorldPlanner.Plan statsPlan;
        private string roadClassCounts, roadLengthText;
        private List<(SplineRoad road,float t,Vector3 point)> anchorSamples;
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
            SceneView.duringSceneGui += PickAnchorInScene;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorSceneManager.activeSceneChangedInEditMode += OnPreviewSceneChanged;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawPreview;
            SceneView.duringSceneGui -= PickAnchorInScene;
            anchorSamples=null;pickAnchor=false;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorSceneManager.activeSceneChangedInEditMode -= OnPreviewSceneChanged;
        }

        private void OnUndoRedo()
        {
            preview = null;
            previewFingerprint=null;
            layout = null;
            RefreshNetwork(SceneManager.GetActiveScene(), false);
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Procedural World Generator V7.2 - Local Optimization Preview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Preview iteration only. No scene, mesh or terrain asset changes. Generation is intentionally locked until the road layout is approved.", MessageType.Info);
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
                settings.arterialSpacing = Mathf.Clamp(EditorGUILayout.FloatField("Arterial spacing (m)", settings.arterialSpacing), 300f, 1200f);
                settings.collectorSpacing = Mathf.Clamp(EditorGUILayout.FloatField("Collector spacing (m)", settings.collectorSpacing), 120f, 450f);
                settings.spacing = Mathf.Clamp(EditorGUILayout.FloatField("Local street spacing (m)", settings.spacing), 55f, 200f);
                settings.nodesPerTile = EditorGUILayout.IntSlider("Candidate budget per land tile", settings.nodesPerTile, 60, 500);
                settings.straightPreference = EditorGUILayout.Slider("Straight road preference", settings.straightPreference, 0f, 1f);
                settings.curve = EditorGUILayout.Slider("Organic curvature", settings.curve, 0f, 1f);
                settings.loops = EditorGUILayout.Slider("Local alternate routes", settings.loops, 0f, 1f);
                settings.authoredLinkTarget = EditorGUILayout.IntSlider("Existing road connection target", settings.authoredLinkTarget, 0, 8);
                settings.bridgeSeparation = EditorGUILayout.Slider("Bridge separation target (%)", settings.bridgeSeparation * 100f, 10f, 90f) / 100f;
                settings.width = Mathf.Clamp(EditorGUILayout.FloatField("Planning asphalt width (m)", settings.width), 5f, 12f);
                settings.sidewalk = Mathf.Clamp(EditorGUILayout.FloatField("Sidewalk width (m)", settings.sidewalk), .5f, 3f);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("V6 Urban Design", EditorStyles.boldLabel);
                settings.urbanDesign = EditorGUILayout.Toggle("Enable V6 urban design", settings.urbanDesign);
                using (new EditorGUI.DisabledScope(!settings.urbanDesign))
                {
                    settings.urbanMode = (ProceduralWorldPlanner.UrbanMode)EditorGUILayout.EnumPopup("Planning mode", settings.urbanMode);
                    settings.contourPreference = EditorGUILayout.Slider("Contour road preference", settings.contourPreference, 0f, 1f);
                    settings.gradingOptimization = EditorGUILayout.Slider("Earthwork optimization", settings.gradingOptimization, 0f, 1f);
                    settings.openSpaceFraction = EditorGUILayout.Slider("Open space target (%)", settings.openSpaceFraction*100f, 0f, 30f)/100f;
                    settings.urbanGuard = EditorGUILayout.Toggle("Preserve V5 coverage", settings.urbanGuard);
                }
                EditorGUILayout.HelpBox("V6 separates neighborhood streets inside the major-road framework. Open-space area and access count are targets, not guarantees. Guard can select V5 if connectivity or coverage regresses.", MessageType.Info);
                DrawQualityControls();
                EditorGUILayout.LabelField("Terrain intelligence", EditorStyles.boldLabel);
                settings.terrainDesign = EditorGUILayout.Toggle("Terrain-adaptive city design", settings.terrainDesign);
                using (new EditorGUI.DisabledScope(!settings.terrainDesign))
                {
                    settings.terrainInfluence = EditorGUILayout.Slider("Terrain direction influence", settings.terrainInfluence, 0f, 1f);
                    settings.districtVariation = EditorGUILayout.Slider("District orientation variation", settings.districtVariation, 0f, 1f);
                    settings.earthworkPreference = EditorGUILayout.Slider("Prefer lower earthworks", settings.earthworkPreference, 0f, 1f);
                    settings.preserveV4Coverage = EditorGUILayout.Toggle("Preserve V4 coverage", settings.preserveV4Coverage);
                }
                EditorGUILayout.HelpBox("Coverage guard compares two plans (slower). Terrain analysis guides street direction and candidate priority; original terrain still determines feasibility.", MessageType.Info);
                EditorGUILayout.LabelField("Terrain and bridges", EditorStyles.boldLabel);
                settings.terrainRepair = EditorGUILayout.Toggle("Terrain-aware network repair", settings.terrainRepair);
                settings.ankaraBackbone = EditorGUILayout.Toggle("Ankara backbone first", settings.ankaraBackbone);
                using (new EditorGUI.DisabledScope(!settings.ankaraBackbone || !settings.buildAnkara))
                {
                    settings.districtGrading = EditorGUILayout.Toggle("Ankara virtual district grading", settings.districtGrading);
                    using (new EditorGUI.DisabledScope(!settings.districtGrading))
                        settings.districtGradingLimit = EditorGUILayout.Slider("District grading limit (m)", settings.districtGradingLimit, 6f, 80f);
                }
                EditorGUILayout.HelpBox("Ankara only: virtual grading is an explicit total cut/fill allowance (at least normal road cut/fill). No TerrainData writes. Red markers identify road samples requiring more than the normal budget.", MessageType.Info);
                settings.grade = EditorGUILayout.Slider("Maximum grade (%)", settings.grade * 100f, 4f, 15f) / 100f;
                settings.maxCutFill = Mathf.Clamp(EditorGUILayout.FloatField("Max cut/fill (m)", settings.maxCutFill), 1f, 20f);
                settings.blend = Mathf.Clamp(EditorGUILayout.FloatField("Terrain shoulder blend (m)", settings.blend), 3f, 30f);
                settings.seaY = EditorGUILayout.FloatField("Sea surface Y", settings.seaY);
                if(Mathf.Abs(settings.seaY)<.0001f)EditorGUILayout.HelpBox("Sea Y is effectively zero; scientific notation such as -2E-05 is valid. Keep the same value for comparisons.",MessageType.Info);
                settings.bridgeClearance = Mathf.Clamp(EditorGUILayout.FloatField("Bridge clearance (m)", settings.bridgeClearance), 3f, 30f);
                if (EditorGUI.EndChangeCheck())
                {
                    preview = null;
                    SceneView.RepaintAll();
                }
                DrawAnchorControls();
                showPreview = EditorGUILayout.Toggle("Show Scene View preview", showPreview);
                EditorGUI.BeginChangeCheck();
                xrayPreview = EditorGUILayout.Toggle("X-ray road preview", xrayPreview);
                showArterials = EditorGUILayout.Toggle("Arterials (blue)", showArterials);
                showCollectors = EditorGUILayout.Toggle("Collectors (orange)", showCollectors);
                showLocals = EditorGUILayout.Toggle("Local streets (gray)", showLocals);
                showBridgeLines = EditorGUILayout.Toggle("Bridges (yellow)", showBridgeLines);
                showLinks = EditorGUILayout.Toggle("Existing-road proposals (green)", showLinks);
                showDetached = EditorGUILayout.Toggle("Detached roads (purple)", showDetached);
                showGrading = EditorGUILayout.Toggle("Extra grading markers (red)", showGrading);
                showTerrainAnalysis = EditorGUILayout.Toggle("Show terrain analysis layer", showTerrainAnalysis);
                terrainLayer = (TerrainLayer)EditorGUILayout.EnumPopup("Analysis layer", terrainLayer);
                showOpenSpaces = EditorGUILayout.Toggle("Park / plaza reservations", showOpenSpaces);
                showParcels = EditorGUILayout.Toggle("Building lot preview", showParcels);
                fastPreview = EditorGUILayout.Toggle("Fast overview rendering", fastPreview);
                if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();

                EditorGUILayout.Space();
                DrawPreviewStorage();
                if (GUILayout.Button("Analyze Terrain (read only)", GUILayout.Height(28f))) Run(AnalyzeOnly);
                if (GUILayout.Button("Preview Layout (no scene changes)", GUILayout.Height(32f))) Run(PlanOnly);
                if (showTerrainAnalysis)
                {
                    string legend = terrainLayer == TerrainLayer.Slope ? "Slope: green < half grade, amber < selected grade, red above. Gray protected." :
                        terrainLayer == TerrainLayer.Elevation ? "Elevation: blue low, tan high (combined land range)." :
                        terrainLayer == TerrainLayer.Buildability ? "Buildability screening: green gentle/smooth, amber moderate, red steep/rough, gray protected. Not a foundation certification." :
                        terrainLayer == TerrainLayer.Contours ? "Contours: 10 m interval, sampled independently per terrain tile." :
                        terrainLayer == TerrainLayer.Seams ? "Seams: green <=1 m, orange <=6 m, red >6 m sampled height discontinuity." :
                        terrainLayer == TerrainLayer.Hotspots ? "Hotspots: red sampled road cross-sections above preferred grading cap." :
                        terrainLayer == TerrainLayer.VerticalProfile ? "Vertical profile: flagged interior grade transitions; junction transitions reported separately." :
                        terrainLayer == TerrainLayer.VirtualTerrain ? "Virtual terrain: gray original, green proposed, orange flagged, magenta issue markers, yellow selected issue. Sampled only, no TerrainData writes." :
                        terrainLayer == TerrainLayer.GradingZones ? "Grading zones: green <=3 m, yellow ordinary budget, orange <=10 m, red preferred cap, purple above preferred cap. Sampled road segments only." :
                        "Earthworks: red cut, blue fill; brighter = beyond ordinary road grading budget. Available after road preview.";
                    EditorGUILayout.HelpBox(legend, MessageType.Info);
                }
                if (preview != null && analysisOnly)
                {
                    foreach (string note in preview.notes) EditorGUILayout.HelpBox(note, MessageType.Info);
                    foreach (string warning in preview.warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
                }
                if (preview != null && !analysisOnly)
                {
                    EditorGUILayout.LabelField("Selected layout", preview.version);
                    DrawQualityReport();
                    EditorGUILayout.LabelField("Park/plaza reservations", preview.openSpaces.Count.ToString());
                    EditorGUILayout.LabelField("Building lot proposals", preview.parcels.Count.ToString());
                    EditorGUILayout.LabelField("Manual anchor proposals", preview.accessResults.Count(a=>a.connected) + "/" + preview.accessResults.Count);
                    foreach(var result in preview.accessResults)
                        EditorGUILayout.HelpBox((result.anchor.road!=null?result.anchor.road.name:"Missing road")+": "+result.message,
                            result.connected?MessageType.Info:MessageType.Warning);
                    EditorGUILayout.LabelField("Retained road segments", preview.edges.Count.ToString());
                    EditorGUILayout.LabelField("Detached preview segments", preview.detachedEdges.Count.ToString());
                    if (preview.detachedEdges.Count > 0)
                        EditorGUILayout.HelpBox("Purple streets passed their individual path checks but do NOT connect to the retained network. They are diagnostic only.", MessageType.Warning);
                    if(statsPlan!=preview)
                    {
                        statsPlan=preview;
                        roadClassCounts=preview.edges.Count(e=>e.kind==ProceduralWorldPlanner.RoadKind.Arterial)+" / "+
                            preview.edges.Count(e=>e.kind==ProceduralWorldPlanner.RoadKind.Collector)+" / "+
                            preview.edges.Count(e=>e.kind==ProceduralWorldPlanner.RoadKind.Local);
                        roadLengthText=(preview.edges.Sum(e=>PathLength(e.points))/1000f).ToString("F2")+" km";
                    }
                    EditorGUILayout.LabelField("Arterial / collector / local",roadClassCounts);
                    EditorGUILayout.LabelField("Existing-road proposals", preview.authoredLinks.Count.ToString());
                    foreach (string note in preview.notes) EditorGUILayout.HelpBox(note, MessageType.Info);
                    EditorGUILayout.LabelField("Planned bridge links", preview.BridgeCount.ToString());
                    if (settings.bridges && settings.buildAnkara && settings.buildIstanbul && preview.BridgeCount != 2)
                        EditorGUILayout.HelpBox("Requested bridge links are missing from this preview. Check the bridge diagnostic below.", MessageType.Warning);
                    EditorGUILayout.LabelField("Road length",roadLengthText);
                    foreach (string warning in preview.warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
                }
                bool hasBuild = FindStates(SceneManager.GetActiveScene()).Count > 0;
                using (new EditorGUI.DisabledScope(true))
                    GUILayout.Button("Generate Roads & Bridges (locked for preview)", GUILayout.Height(36f));
                EditorGUILayout.HelpBox("Road classes affect the plan and preview, not mesh widths. Green connections are proposals. District grading is hypothetical and must be validated before baking. Existing roads and terrain are unchanged.", MessageType.Info);
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

        private void AnalyzeOnly()
        {
            Scene scene = SceneManager.GetActiveScene();
            layout = ProceduralWorldPlanner.DetectLayout(scene);
            string key=ProceduralWorldPlanner.PreviewFingerprint(settings,layout,scene);
            if(!cacheEnabled||analysisCache==null||analysisCacheKey!=key)
            {analysisCache=ProceduralWorldPlanner.AnalyzeOnly(settings,layout,scene);analysisCacheKey=key;}
            preview=analysisCache;previewFingerprint=key;previewSettings=ProceduralWorldPlanner.DescribeSettings(settings);previewScenePath=scene.path;
            analysisOnly = true;
            showTerrainAnalysis = true;
            terrainLayer = TerrainLayer.Slope;
            SetStatus("Read-only terrain analysis ready: " + preview.terrainCells.Count + " samples. No scene assets changed.");
        }

        private void PlanOnly() => PlanOnly(true);

        private void PlanOnly(bool allowBridgeFallback)
        {
            bool wasAnalysis=analysisOnly;
            Scene scene = SceneManager.GetActiveScene();
            if (FindStates(scene).Count > 0) throw new InvalidOperationException("Clear the previous build before previewing a new one.");
            if (layout == null) Detect();
            // Re-detect in case terrain transforms changed after the last preview.
            layout = ProceduralWorldPlanner.DetectLayout(scene);
            ValidateRoots(scene);
            if (!settings.buildAnkara && !settings.buildIstanbul) throw new InvalidOperationException("Select a city.");
            if (EditorUtility.DisplayCancelableProgressBar("Procedural World", "Planning safe roads and bridge approaches", .05f))
                throw new OperationCanceledException();
            var stopwatch=System.Diagnostics.Stopwatch.StartNew();
            string key=ProceduralWorldPlanner.PreviewFingerprint(settings,layout,scene);
            if(TryCachedPlan(key,out var cached))
            {
                preview=cached;comparisonOwner=cached;analysisOnly=false;previewFingerprint=key;previewSettings=ProceduralWorldPlanner.DescribeSettings(settings);previewScenePath=scene.path;
                finalStageKey=key;finalStageSettings=previewSettings;finalStageScene=scene.path;
                SetStatus("Preview cache hit: "+stopwatch.Elapsed.TotalSeconds.ToString("F2")+" s including input verification.");return;
            }
            // Preserve the last successful preview if planning is canceled or fails.
            var previous=preview;
            try
            {
                preview = ProceduralWorldPlanner.Build(settings, layout, scene);
                preview.notes.Insert(0,"Planning elapsed: "+stopwatch.Elapsed.TotalSeconds.ToString("F1")+" s (including input verification and selected guard comparisons).");
                previewFingerprint=key;previewSettings=ProceduralWorldPlanner.DescribeSettings(settings);previewScenePath=scene.path;analysisOnly=false;
                CachePlan(key,preview);
                comparisonOwner=preview;
                WriteStageCheckpoints(preview,key,scene.path);
                SetStatus("Preview ready: " + preview.edges.Count + " retained segments, " + preview.detachedEdges.Count + " detached, " + preview.BridgeCount + " planned bridges. No scene assets changed.", preview.detachedEdges.Count > 0 ? MessageType.Warning : MessageType.Info);
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
                    previewFingerprint=null;previewSettings=ProceduralWorldPlanner.DescribeSettings(settings);previewScenePath=scene.path;analysisOnly=false;
                    comparisonOwner=preview;finalStageKey=null;finalStageSettings=previewSettings;finalStageScene=scene.path;preview.previewCriteriaMet=false;
                    SetStatus("City-only preview shown. Adjust bridge settings or try another seed to preview both bridge links. Scene generation is locked during preview iteration.", MessageType.Warning);
                    Debug.LogWarning("[ProceduralWorld] " + ex.Message);
                }
                catch {preview=previous;analysisOnly=wasAnalysis;throw;}
                finally { settings.bridges = requested; }
            }
            catch {preview=previous;analysisOnly=wasAnalysis;throw;}
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
            var oldZTest = Handles.zTest;
            try
            {
                if (xrayPreview) Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
                Handles.color = new Color(.25f, .75f, .95f, .9f);
                DrawArea(layout.Ankara(settings.ankaraLow), "Ankara (12 tiles)");
                Handles.color = new Color(.95f, .65f, .25f, .9f);
                DrawArea(layout.Istanbul(settings.ankaraLow), "Istanbul (12 tiles)");
                Handles.color = new Color(.25f, .45f, 1f, .8f);
                DrawArea(layout.sea, layout.SeaTerrainCount == 0 ? "Sea (empty corridor)" : "Sea (4 tiles)");
                if (cachedPreview != preview)
                {
                    previewGeometry.Clear();lineBatches.Clear();cellQuads.Clear();
                    cachedPreview = preview;
                    elevationMin=preview!=null&&preview.terrainCells.Count>0?preview.terrainCells.Min(c=>c.center.y):0f;
                    elevationMax=preview!=null&&preview.terrainCells.Count>0?preview.terrainCells.Max(c=>c.center.y):1f;
                }
                if (preview == null) return;
                if (showTerrainAnalysis) DrawAnalysisLayer();
                if (!analysisOnly) DrawPilotOverlay();
                if (!analysisOnly)
                {
                    if (showOpenSpaces)
                        foreach(var space in preview.openSpaces)
                            Handles.DrawSolidRectangleWithOutline(space.corners,
                                preview.version.StartsWith("V7")&&!space.accessible?new Color(.95f,.55f,.12f,.32f):
                                space.kind==ProceduralWorldPlanner.AreaKind.Plaza?new Color(.85f,.71f,.32f,.30f):new Color(.12f,.8f,.25f,.28f),
                                new Color(.30f,.92f,.38f,.7f));
                    if (showParcels)
                    {
                        Handles.color=new Color(.87f,.78f,.52f,.8f);
                        Handles.DrawLines(Batch(100,()=>preview.parcels.SelectMany(l=>new[]{l.corners[0],l.corners[1],
                            l.corners[1],l.corners[2],l.corners[2],l.corners[3],l.corners[3],l.corners[0]})));
                    }
                }
                if (analysisOnly) return;
                foreach (var kind in new[] { ProceduralWorldPlanner.RoadKind.Local, ProceduralWorldPlanner.RoadKind.Collector,
                    ProceduralWorldPlanner.RoadKind.Arterial, ProceduralWorldPlanner.RoadKind.Bridge })
                {
                    if (kind == ProceduralWorldPlanner.RoadKind.Local && !showLocals ||
                        kind == ProceduralWorldPlanner.RoadKind.Collector && !showCollectors ||
                        kind == ProceduralWorldPlanner.RoadKind.Arterial && !showArterials ||
                        kind == ProceduralWorldPlanner.RoadKind.Bridge && !showBridgeLines) continue;
                    float thickness;
                    switch (kind)
                    {
                        case ProceduralWorldPlanner.RoadKind.Arterial:
                            Handles.color = new Color(.10f, .68f, 1f, 1f); thickness = 4.5f; break;
                        case ProceduralWorldPlanner.RoadKind.Collector:
                            Handles.color = new Color(1f, .55f, .12f, .95f); thickness = 2.8f; break;
                        case ProceduralWorldPlanner.RoadKind.Bridge:
                            Handles.color = Color.yellow; thickness = 5f; break;
                        default:
                            Handles.color = new Color(.88f, .91f, .93f, .82f); thickness = 1.4f; break;
                    }
                    if(fastPreview && sceneView.size>350f && kind!=ProceduralWorldPlanner.RoadKind.Bridge)
                        Handles.DrawLines(Batch(10+(int)kind,()=>Segments(preview.edges.Where(e=>e.kind==kind))));
                    else foreach (var edge in preview.edges)
                    {
                        if (edge.kind != kind) continue;
                        Handles.DrawAAPolyLine(thickness, PreviewPoints(edge));
                    }
                }
                if (showDetached)
                {
                    Handles.color = new Color(.92f, .35f, .95f, .9f);
                    if(fastPreview && sceneView.size>350f)Handles.DrawLines(Batch(20,()=>Segments(preview.detachedEdges)));
                    else foreach (var edge in preview.detachedEdges)
                        Handles.DrawAAPolyLine(2f, PreviewPoints(edge));
                }
                if (showGrading)
                {
                    Handles.color = new Color(1f, .24f, .20f, .95f);
                    Handles.DrawLines(Batch(21,()=>preview.gradingMarkers.SelectMany(point=>new[]{point+Vector3.up*2f,point+Vector3.up*9f})));
                }
                if (showLinks)
                {
                    foreach(var anchor in settings.accessAnchors)
                    {
                        if(anchor.road==null||!anchor.road.IsUsable)continue;
                        var result=preview.accessResults.FirstOrDefault(r=>r.anchor==anchor);
                        Handles.color=result!=null&&result.connected?Color.green:Color.red;
                        Vector3 at=anchor.road.GetPoint(anchor.t)+Vector3.up*1.5f;
                        Handles.SphereHandleCap(0,at,Quaternion.identity,HandleUtility.GetHandleSize(at)*.14f,EventType.Repaint);
                    }
                    Handles.color = new Color(.24f, 1f, .35f, 1f);
                    foreach (var link in preview.authoredLinks)
                    {
                        Handles.DrawAAPolyLine(4f, PreviewPoints(link));
                        Vector3 anchor = link.points[0] + Vector3.up;
                        Handles.SphereHandleCap(0, anchor, Quaternion.identity,
                            HandleUtility.GetHandleSize(anchor) * .12f, EventType.Repaint);
                    }
                }
            }
            finally { Handles.zTest = oldZTest; }
        }

        private void InvalidateAnchorPreview()
        {
            preview=null;previewFingerprint=null;Repaint();SceneView.RepaintAll();
        }

        private void DrawAnchorControls()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Manual existing-road anchors (preview only)",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Choose an existing spline and position, or arm Scene View picking then Shift+left click near its road. Select coastal/district boundary access points. No roads are split.",MessageType.Info);
            using(new EditorGUI.DisabledScope(!settings.urbanDesign))
            {
                anchorRoad=(SplineRoad)EditorGUILayout.ObjectField("Existing spline road",anchorRoad,typeof(SplineRoad),true);
                anchorT=EditorGUILayout.Slider("Normalized spline position",anchorT,0f,1f);
                using(new EditorGUI.DisabledScope(anchorRoad==null || !anchorRoad.IsUsable))
                    if(GUILayout.Button("Add selected access anchor"))AddAnchor(anchorRoad,anchorT);
                if(GUILayout.Button(pickAnchor?"Cancel Scene View anchor picking":"Pick anchor in Scene View (Shift+click)"))
                {
                    pickAnchor=!pickAnchor;anchorSamples=null;
                    if(pickAnchor)CacheAnchorSamples();
                    SceneView.RepaintAll();
                }
                for(int i=0;i<settings.accessAnchors.Count;i++)
                {
                    var a=settings.accessAnchors[i];
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField((i+1)+". "+(a.road!=null?a.road.name:"Missing spline")+" @ "+a.t.ToString("F3"));
                    bool remove=GUILayout.Button("Remove",GUILayout.Width(70));
                    EditorGUILayout.EndHorizontal();
                    if(remove){settings.accessAnchors.RemoveAt(i);InvalidateAnchorPreview();break;}
                }
                if(settings.accessAnchors.Count>0&&GUILayout.Button("Clear preview anchors"))
                {settings.accessAnchors.Clear();InvalidateAnchorPreview();}
            }
        }

        private void AddAnchor(SplineRoad road,float t)
        {
            if(road==null||!road.IsUsable||road.gameObject.scene!=SceneManager.GetActiveScene())return;
            t=Mathf.Clamp01(t);
            Vector3 point=road.GetPoint(t);
            if(settings.accessAnchors.Any(a=>a.road!=null&&Vector3.Distance(a.road.GetPoint(a.t),point)<8f))return;
            settings.accessAnchors.Add(new ProceduralWorldPlanner.AccessAnchor{road=road,t=t});
            InvalidateAnchorPreview();
        }

        private void CacheAnchorSamples()
        {
            anchorSamples=new List<(SplineRoad,float,Vector3)>();
            foreach(var road in FindComponents<SplineRoad>(SceneManager.GetActiveScene()))
            {
                if(!road.IsUsable)continue;
                float length=road.GetTotalLength();
                int count=Mathf.Clamp(Mathf.CeilToInt(length/6f),2,300);
                for(int i=0;i<=count;i++)
                {
                    float t=road.DistanceToT(length*i/count);
                    anchorSamples.Add((road,t,road.GetPoint(t)));
                }
            }
        }

        private void PickAnchorInScene(SceneView view)
        {
            if(!pickAnchor||!settings.urbanDesign||EditorApplication.isPlayingOrWillChangePlaymode)return;
            Event evt=Event.current;
            if(evt.type==EventType.KeyDown&&evt.keyCode==KeyCode.Escape)
            {pickAnchor=false;anchorSamples=null;evt.Use();Repaint();return;}
            if(evt.type==EventType.Layout&&evt.shift)HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            if(evt.type!=EventType.MouseDown||evt.button!=0||!evt.shift||evt.alt||view.camera==null)return;
            if(anchorSamples==null)CacheAnchorSamples();
            SplineRoad best=null;float t=0f,dist=22f*22f;
            foreach(var sample in anchorSamples)
            {
                if(sample.road==null)continue;
                Vector3 screen=view.camera.WorldToScreenPoint(sample.point);
                if(screen.z<=0f)continue;
                float d=(HandleUtility.WorldToGUIPoint(sample.point)-evt.mousePosition).sqrMagnitude;
                if(d>=dist)continue;
                best=sample.road;t=sample.t;dist=d;
            }
            if(best!=null)
            {
                float step=.025f;
                for(int pass=0;pass<4;pass++,step*=.25f)
                    foreach(float sign in new[]{-1f,1f})
                    {
                        float candidate=Mathf.Clamp01(t+step*sign);
                        float d=(HandleUtility.WorldToGUIPoint(best.GetPoint(candidate))-evt.mousePosition).sqrMagnitude;
                        if(d<dist){dist=d;t=candidate;}
                    }
                AddAnchor(best,t);pickAnchor=false;anchorSamples=null;evt.Use();
            }
            else SetStatus("No usable existing road within 22 screen pixels. Zoom closer or use the road field.",MessageType.Warning);
        }

        private Vector3[] Batch(int key,Func<IEnumerable<Vector3>> create)
        {
            if(!lineBatches.TryGetValue(key,out Vector3[] result))
            {result=create().ToArray();lineBatches.Add(key,result);}
            return result;
        }

        private IEnumerable<Vector3> Segments(IEnumerable<ProceduralWorldPlanner.Edge> edges)
        {
            foreach(var e in edges)
            {
                Vector3[] points=PreviewPoints(e);
                for(int i=1;i<points.Length;i++){yield return points[i-1];yield return points[i];}
            }
        }

        private void DrawAnalysisLayer()
        {
            if(terrainLayer==TerrainLayer.Hotspots||terrainLayer==TerrainLayer.VerticalProfile)
            {
                bool hot=terrainLayer==TerrainLayer.Hotspots;
                Handles.color=hot?Color.red:new Color(1f,.35f,.8f,1f);
                Handles.DrawLines(Batch(hot?210:211,()=>(hot?preview.hotspots:preview.verticalFlags).SelectMany(l=>new[]{l.a+Vector3.up*2f,l.b+Vector3.up*2f})));
                return;
            }
            if(terrainLayer==TerrainLayer.VirtualTerrain)
            {
                IEnumerable<Vector3> Lines(Vector3[] a){for(int i=1;i<a.Length;i++){yield return a[i-1];yield return a[i];}}
                Handles.color=new Color(.7f,.72f,.72f,.65f);
                Handles.DrawLines(Batch(212,()=>preview.virtualSections.SelectMany(x=>Lines(x.original))));
                Handles.color=new Color(.28f,.9f,.48f,.85f);
                Handles.DrawLines(Batch(213,()=>preview.virtualSections.Where(x=>!x.flagged).SelectMany(x=>Lines(x.proposed))));
                Handles.color=new Color(1f,.47f,.1f,1f);
                Handles.DrawLines(Batch(214,()=>preview.virtualSections.Where(x=>x.flagged).SelectMany(x=>Lines(x.proposed))));
                var report=preview.virtualReport;
                if(report!=null&&report.diagnosticSchema==1&&report.issues!=null)
                {
                    IEnumerable<Vector3> Markers()
                    {
                        foreach(var issue in report.issues)
                        {
                            Vector3 p=issue.position+Vector3.up*2f;
                            yield return p-Vector3.right*2f;yield return p+Vector3.right*2f;
                            yield return p-Vector3.forward*2f;yield return p+Vector3.forward*2f;
                        }
                    }
                    Handles.color=new Color(1f,.2f,.65f,.9f);
                    Handles.DrawLines(Batch(215,Markers));
                    if(selectedVirtualReport==report&&selectedVirtualIssue!=null)
                    {
                        Handles.color=Color.yellow;
                        var issue=selectedVirtualIssue;
                        Handles.DrawWireDisc(issue.position+Vector3.up*2f,Vector3.up,5f);
                        foreach(var sample in issue.samples)
                            Handles.DrawLine(sample.position,sample.position+Vector3.up*3f);
                    }
                }
                return;
            }
            if(terrainLayer==TerrainLayer.GradingZones)
            {
                var colors=new[]{new Color(.24f,.85f,.4f),Color.yellow,new Color(1f,.5f,.1f),Color.red,new Color(.85f,.2f,1f)};
                for(int i=0;i<colors.Length;i++)
                {
                    int band=i;Handles.color=colors[i];
                    Handles.DrawLines(Batch(50+i,()=>preview.gradingZones.Where(l=>(int)l.value==band)
                        .SelectMany(l=>new[]{l.a+Vector3.up*2f,l.b+Vector3.up*2f})));
                }
                return;
            }
            if(terrainLayer==TerrainLayer.Contours||terrainLayer==TerrainLayer.Seams||terrainLayer==TerrainLayer.Earthworks)
            {
                if(terrainLayer==TerrainLayer.Contours)
                {
                    Handles.color=new Color(.92f,.88f,.51f,.85f);
                    Handles.DrawLines(Batch(30,()=>preview.contours.SelectMany(l=>new[]{l.a,l.b})));
                }
                else if(terrainLayer==TerrainLayer.Seams)
                {
                    for(int i=0;i<3;i++)
                    {
                        int band=i;
                        Handles.color=i==0?new Color(.3f,.8f,.4f,.9f):i==1?new Color(1f,.65f,.15f,1f):Color.red;
                        Handles.DrawLines(Batch(31+i,()=>preview.seams.Where(l=>(l.value<=1f?0:l.value<=6f?1:2)==band)
                            .SelectMany(l=>new[]{l.a,l.b})));
                    }
                }
                else
                {
                    for(int i=0;i<4;i++)
                    {
                        int band=i;
                        Handles.color=i==0?new Color(.8f,.35f,.24f,.65f):i==1?Color.red:
                            i==2?new Color(.30f,.55f,.9f,.65f):new Color(.10f,.50f,1f,1f);
                        Handles.DrawLines(Batch(34+i,()=>preview.earthworks.Where(l=>(l.value<0f?0:2)+(Mathf.Abs(l.value)>settings.maxCutFill?1:0)==band)
                            .SelectMany(l=>new[]{l.a+Vector3.up*1.5f,l.b+Vector3.up*1.5f})));
                    }
                }
                return;
            }
            foreach(var cell in preview.terrainCells)
            {
                Color fill;
                if(cell.protectedLand)fill=new Color(.5f,.5f,.5f,.26f);
                else if(terrainLayer==TerrainLayer.Elevation)
                    fill=Color.Lerp(new Color(.16f,.46f,.83f,.36f),new Color(.94f,.69f,.32f,.36f),
                        Mathf.InverseLerp(elevationMin,elevationMax,cell.center.y));
                else if(terrainLayer==TerrainLayer.Buildability)
                    fill=cell.slope>.12f||cell.roughness>2f?new Color(.92f,.23f,.18f,.32f):
                        cell.slope>.05f||cell.roughness>.6f?new Color(1f,.65f,.12f,.28f):new Color(.2f,.8f,.35f,.24f);
                else fill=cell.slope>settings.grade?new Color(.93f,.21f,.16f,.32f):
                    cell.slope>settings.grade*.5f?new Color(1f,.65f,.12f,.26f):new Color(.2f,.8f,.35f,.22f);
                if(!cellQuads.TryGetValue(cell,out Vector3[] quad))
                {
                    Rect r=cell.area;float y=cell.center.y+1.2f;
                    quad=new[]{new Vector3(r.xMin,y,r.yMin),new Vector3(r.xMax,y,r.yMin),
                        new Vector3(r.xMax,y,r.yMax),new Vector3(r.xMin,y,r.yMax)};
                    cellQuads.Add(cell,quad);
                }
                Handles.DrawSolidRectangleWithOutline(quad,fill,Color.clear);
            }
        }

        private Vector3[] PreviewPoints(ProceduralWorldPlanner.Edge edge)
        {
            if (!previewGeometry.TryGetValue(edge, out Vector3[] points))
            {
                float offset = edge.kind == ProceduralWorldPlanner.RoadKind.AuthoredLink ? .8f : .5f;
                points = edge.points.Select(p => p + Vector3.up * offset).ToArray();
                previewGeometry.Add(edge, points);
            }
            return points;
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
