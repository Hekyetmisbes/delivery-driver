using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using DeliveryDriver.Optimization;
using TrafficSystem;

public class OptimizationDashboard : EditorWindow
{
    private enum Tab { Dashboard, TrafficNPC, WorldStreaming, PhysicsRendering, IssueScanner }

    private Tab selectedTab;
    private Vector2 scrollPosition;
    private double lastRepaintTime;
    private const double REPAINT_INTERVAL = 0.25;

    // FPS graph
    private const int FPS_GRAPH_WIDTH = 280;
    private const int FPS_GRAPH_HEIGHT = 80;

    // Issue scanner
    private List<OptimizationProfiler.Issue> scannedIssues;

    // Profile references
    private OptimizationProfile selectedProfile;

    // Cached styles
    private GUIStyle headerStyle;
    private GUIStyle subHeaderStyle;
    private GUIStyle boxStyle;
    private bool stylesInitialized;

    [MenuItem("Tools/Delivery Driver/Optimization Dashboard")]
    public static void ShowWindow()
    {
        var window = GetWindow<OptimizationDashboard>("Optimization Dashboard");
        window.minSize = new Vector2(520, 500);
    }

    [MenuItem("Tools/Delivery Driver/Create Optimization Presets")]
    public static void CreatePresets()
    {
        string dir = "Assets/Settings";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Settings");

        CreatePreset(dir, "OptProfile_Mobile", 30, 100, 200, 350, 0.15f, 50, 0.15f,
            64, 70, 110, 130, 0.5f, 8, 120, 25, 1, true, 0, 0.02f, 6, 1);
        CreatePreset(dir, "OptProfile_PC_Balanced", 50, 150, 300, 500, 0.12f, 50, 0.1f,
            64, 90, 150, 170, 0.35f, 12, 120, 40, 2, true, 2, 0.02f, 6, 1);
        CreatePreset(dir, "OptProfile_PC_High", 75, 200, 400, 600, 0.1f, 50, 0.08f,
            64, 120, 200, 250, 0.25f, 16, 120, 75, 4, true, 4, 0.02f, 8, 2);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Dashboard] Created 3 optimization presets in Assets/Settings/");
    }

    private static void CreatePreset(string dir, string name,
        float nearDist, float midDist, float farDist, float veryFarDist, float stateInterval,
        float cellSize, float gridInterval,
        float chunkSize, float nearRing, float midRing, float farRing, float chunkInterval, int maxChunks, float cacheRefresh,
        float shadowDist, int shadowCascades, bool streamMipmaps, int aa,
        float fixedTimestep, int solverIter, int solverVelIter)
    {
        string path = $"{dir}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<OptimizationProfile>(path);
        if (existing != null)
        {
            Debug.Log($"[Dashboard] Preset already exists: {path}");
            return;
        }

        var profile = ScriptableObject.CreateInstance<OptimizationProfile>();
        profile.nearDistance = nearDist;
        profile.midDistance = midDist;
        profile.farDistance = farDist;
        profile.veryFarDistance = veryFarDist;
        profile.stateUpdateInterval = stateInterval;
        profile.cellSize = cellSize;
        profile.gridUpdateInterval = gridInterval;
        profile.chunkSize = chunkSize;
        profile.nearRing = nearRing;
        profile.midRing = midRing;
        profile.farRing = farRing;
        profile.chunkUpdateInterval = chunkInterval;
        profile.maxChunkUpdatesPerFrame = maxChunks;
        profile.cacheRefreshInterval = cacheRefresh;
        profile.shadowDistance = shadowDist;
        profile.shadowCascades = shadowCascades;
        profile.streamingMipmaps = streamMipmaps;
        profile.antiAliasing = aa;
        profile.fixedTimestep = fixedTimestep;
        profile.solverIterations = solverIter;
        profile.solverVelocityIterations = solverVelIter;

        AssetDatabase.CreateAsset(profile, path);
    }

    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
        selectedTab = (Tab)EditorPrefs.GetInt("OptDashboard_Tab", 0);
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorPrefs.SetInt("OptDashboard_Tab", (int)selectedTab);
    }

    private void OnEditorUpdate()
    {
        if (!Application.isPlaying) return;
        if (EditorApplication.timeSinceStartup - lastRepaintTime < REPAINT_INTERVAL) return;
        lastRepaintTime = EditorApplication.timeSinceStartup;
        Repaint();
    }

    private void InitStyles()
    {
        if (stylesInitialized) return;
        headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        subHeaderStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
        boxStyle = new GUIStyle("box") { padding = new RectOffset(8, 8, 8, 8) };
        stylesInitialized = true;
    }

    private void OnGUI()
    {
        InitStyles();

        // Tab bar
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        DrawTabButton("Dashboard", Tab.Dashboard);
        DrawTabButton("Traffic & NPC", Tab.TrafficNPC);
        DrawTabButton("World Streaming", Tab.WorldStreaming);
        DrawTabButton("Physics & Rendering", Tab.PhysicsRendering);
        DrawTabButton("Issue Scanner", Tab.IssueScanner);
        EditorGUILayout.EndHorizontal();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        switch (selectedTab)
        {
            case Tab.Dashboard: DrawDashboard(); break;
            case Tab.TrafficNPC: DrawTrafficNPC(); break;
            case Tab.WorldStreaming: DrawWorldStreaming(); break;
            case Tab.PhysicsRendering: DrawPhysicsRendering(); break;
            case Tab.IssueScanner: DrawIssueScanner(); break;
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawTabButton(string label, Tab tab)
    {
        bool isSelected = selectedTab == tab;
        if (GUILayout.Toggle(isSelected, label, EditorStyles.toolbarButton))
        {
            if (!isSelected) selectedTab = tab;
        }
    }

    // ==================== DASHBOARD ====================

    private void DrawDashboard()
    {
        EditorGUILayout.LabelField("Performance Dashboard", headerStyle);
        EditorGUILayout.Space(4);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to see real-time metrics.", MessageType.Info);

            // Profile selector (works in edit mode too)
            DrawProfileSelector();
            return;
        }

        var controller = UnifiedOptimizationController.Instance;

        // FPS
        EditorGUILayout.BeginVertical(boxStyle);
        float fps = OptimizationProfiler.GetCurrentFPS();
        float smoothFps = controller != null ? controller.SmoothedFPS : fps;
        Color fpsColor = fps >= 55 ? Color.green : fps >= 40 ? Color.yellow : Color.red;
        GUI.color = fpsColor;
        EditorGUILayout.LabelField($"FPS: {fps:F0} (avg: {smoothFps:F0})", subHeaderStyle);
        GUI.color = Color.white;

        // FPS Graph
        if (controller != null)
        {
            DrawFPSGraph(controller.FPSHistory, controller.FPSHistoryIndex);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        // Memory
        EditorGUILayout.BeginVertical(boxStyle);
        long memMB = OptimizationProfiler.GetManagedMemoryBytes() / (1024 * 1024);
        bool pressure = controller != null && controller.MemoryPressure;
        GUI.color = pressure ? Color.red : Color.white;
        EditorGUILayout.LabelField($"Memory: {memMB} MB {(pressure ? "(PRESSURE)" : "")}", subHeaderStyle);
        GUI.color = Color.white;
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        // Draw calls
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField($"Draw Calls: {OptimizationProfiler.GetDrawCallCount()}", subHeaderStyle);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        // System stats
        DrawSystemStats();

        // Profile selector
        DrawProfileSelector();
    }

    private void DrawFPSGraph(float[] history, int currentIndex)
    {
        Rect graphRect = GUILayoutUtility.GetRect(FPS_GRAPH_WIDTH, FPS_GRAPH_HEIGHT);
        if (Event.current.type != EventType.Repaint) return;

        // Background
        EditorGUI.DrawRect(graphRect, new Color(0.15f, 0.15f, 0.15f));

        // 60fps line
        float y60 = graphRect.yMax - (60f / 120f) * graphRect.height;
        EditorGUI.DrawRect(new Rect(graphRect.x, y60, graphRect.width, 1), new Color(0f, 0.5f, 0f, 0.5f));

        // 30fps line
        float y30 = graphRect.yMax - (30f / 120f) * graphRect.height;
        EditorGUI.DrawRect(new Rect(graphRect.x, y30, graphRect.width, 1), new Color(0.5f, 0f, 0f, 0.5f));

        // Draw FPS bars
        int count = history.Length;
        float barWidth = graphRect.width / count;

        for (int i = 0; i < count; i++)
        {
            int idx = (currentIndex + i) % count;
            float val = Mathf.Clamp(history[idx], 0, 120);
            float h = (val / 120f) * graphRect.height;

            Color barColor = val >= 55 ? Color.green : val >= 40 ? Color.yellow : Color.red;
            barColor.a = 0.7f;

            Rect bar = new Rect(
                graphRect.x + i * barWidth,
                graphRect.yMax - h,
                Mathf.Max(barWidth - 0.5f, 1f),
                h
            );
            EditorGUI.DrawRect(bar, barColor);
        }
    }

    private void DrawSystemStats()
    {
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("System Status", subHeaderStyle);

        // Traffic stats
        var trafficOpt = Object.FindAnyObjectByType<TrafficSimulationOptimizer>();
        if (trafficOpt != null)
        {
            EditorGUILayout.LabelField($"NPCs - Total: {trafficOpt.totalNPCs}, Active: {trafficOpt.activeNPCs}, Throttled: {trafficOpt.throttledNPCs}, Disabled: {trafficOpt.disabledNPCs}");
        }

        // Chunk stats
        var chunkMgr = Object.FindAnyObjectByType<WorldChunkManager>();
        if (chunkMgr != null)
        {
            EditorGUILayout.LabelField($"Chunks: {chunkMgr.name} active");
        }

        // Traffic comms stats
        var comms = TrafficCommunicationSystem.Instance;
        if (comms != null)
        {
            var stats = comms.GetStats();
            EditorGUILayout.LabelField($"Spatial Grid - Vehicles: {stats.vehicleCount}, Cells: {stats.cellCount}");
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawProfileSelector()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("Optimization Profile", subHeaderStyle);

        selectedProfile = (OptimizationProfile)EditorGUILayout.ObjectField(
            "Profile", selectedProfile, typeof(OptimizationProfile), false);

        if (selectedProfile != null && Application.isPlaying)
        {
            if (GUILayout.Button("Apply Profile"))
            {
                var controller = UnifiedOptimizationController.Instance;
                if (controller != null)
                {
                    controller.ApplyProfile(selectedProfile);
                }
            }
        }

        EditorGUILayout.EndVertical();
    }

    // ==================== TRAFFIC & NPC ====================

    private void DrawTrafficNPC()
    {
        EditorGUILayout.LabelField("Traffic & NPC Settings", headerStyle);
        EditorGUILayout.Space(4);

        var trafficOpt = Object.FindAnyObjectByType<TrafficSimulationOptimizer>();
        if (trafficOpt == null)
        {
            EditorGUILayout.HelpBox("TrafficSimulationOptimizer not found in scene.", MessageType.Warning);
            return;
        }

        var so = new SerializedObject(trafficOpt);
        so.Update();

        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("Distance Thresholds", subHeaderStyle);
        EditorGUILayout.PropertyField(so.FindProperty("nearDistance"));
        EditorGUILayout.PropertyField(so.FindProperty("midDistance"));
        EditorGUILayout.PropertyField(so.FindProperty("farDistance"));
        EditorGUILayout.PropertyField(so.FindProperty("veryFarDistance"));
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("Update Settings", subHeaderStyle);
        EditorGUILayout.PropertyField(so.FindProperty("stateUpdateInterval"));
        EditorGUILayout.PropertyField(so.FindProperty("disableVeryFarAI"));
        EditorGUILayout.PropertyField(so.FindProperty("simplifyDistantPhysics"));
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        if (Application.isPlaying)
        {
            EditorGUILayout.BeginVertical(boxStyle);
            EditorGUILayout.LabelField("Live Stats", subHeaderStyle);
            EditorGUILayout.LabelField($"Total NPCs: {trafficOpt.totalNPCs}");
            EditorGUILayout.LabelField($"Active: {trafficOpt.activeNPCs}");
            EditorGUILayout.LabelField($"Throttled: {trafficOpt.throttledNPCs}");
            EditorGUILayout.LabelField($"Disabled: {trafficOpt.disabledNPCs}");
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(4);
        if (GUILayout.Button("Apply Recommended Settings"))
        {
            trafficOpt.nearDistance = 50f;
            trafficOpt.midDistance = 150f;
            trafficOpt.farDistance = 300f;
            trafficOpt.veryFarDistance = 500f;
            trafficOpt.stateUpdateInterval = 0.12f;
            EditorUtility.SetDirty(trafficOpt);
        }

        so.ApplyModifiedProperties();
    }

    // ==================== WORLD STREAMING ====================

    private void DrawWorldStreaming()
    {
        EditorGUILayout.LabelField("World Streaming Settings", headerStyle);
        EditorGUILayout.Space(4);

        var chunkMgr = Object.FindAnyObjectByType<WorldChunkManager>();
        if (chunkMgr == null)
        {
            EditorGUILayout.HelpBox("WorldChunkManager not found in scene.", MessageType.Warning);
            return;
        }

        var so = new SerializedObject(chunkMgr);
        so.Update();

        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("Chunk Configuration", subHeaderStyle);
        EditorGUILayout.PropertyField(so.FindProperty("chunkSize"));
        EditorGUILayout.PropertyField(so.FindProperty("updateInterval"));
        EditorGUILayout.PropertyField(so.FindProperty("maxChunkUpdatesPerFrame"));
        EditorGUILayout.PropertyField(so.FindProperty("cacheRefreshInterval"));
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("Ring Distances", subHeaderStyle);
        EditorGUILayout.PropertyField(so.FindProperty("nearRingDistance"));
        EditorGUILayout.PropertyField(so.FindProperty("midRingDistance"));
        EditorGUILayout.PropertyField(so.FindProperty("farRingDistance"));

        // Visual ring diagram
        EditorGUILayout.Space(4);
        DrawRingDiagram(chunkMgr.nearRingDistance, chunkMgr.midRingDistance, chunkMgr.farRingDistance);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("Hard Radius Culling", subHeaderStyle);
        EditorGUILayout.PropertyField(so.FindProperty("enableHardRadiusCulling"));
        EditorGUILayout.PropertyField(so.FindProperty("cullStaticRenderersOnly"));
        EditorGUILayout.PropertyField(so.FindProperty("rendererCullingPadding"));
        EditorGUILayout.EndVertical();

        so.ApplyModifiedProperties();
    }

    private void DrawRingDiagram(float near, float mid, float far)
    {
        Rect rect = GUILayoutUtility.GetRect(200, 100);
        if (Event.current.type != EventType.Repaint) return;

        Vector2 center = rect.center;
        float maxRadius = 45f;
        float scale = maxRadius / Mathf.Max(far, 1f);

        // Far ring
        DrawCircleGUI(center, far * scale, new Color(1f, 0f, 0f, 0.2f));
        // Mid ring
        DrawCircleGUI(center, mid * scale, new Color(1f, 1f, 0f, 0.2f));
        // Near ring
        DrawCircleGUI(center, near * scale, new Color(0f, 1f, 0f, 0.2f));
        // Center dot
        EditorGUI.DrawRect(new Rect(center.x - 2, center.y - 2, 4, 4), Color.white);

        // Labels
        GUI.Label(new Rect(center.x + near * scale + 2, center.y - 8, 60, 16), $"{near}m", EditorStyles.miniLabel);
        GUI.Label(new Rect(center.x + mid * scale + 2, center.y - 8, 60, 16), $"{mid}m", EditorStyles.miniLabel);
        GUI.Label(new Rect(center.x + far * scale + 2, center.y - 8, 60, 16), $"{far}m", EditorStyles.miniLabel);
    }

    private void DrawCircleGUI(Vector2 center, float radius, Color color)
    {
        // Simple circle approximation using filled rect outline
        int segments = 24;
        float angleStep = 360f / segments;
        for (int i = 0; i < segments; i++)
        {
            float a1 = angleStep * i * Mathf.Deg2Rad;
            float a2 = angleStep * (i + 1) * Mathf.Deg2Rad;
            Vector2 p1 = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius;
            Vector2 p2 = center + new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * radius;

            // Draw line segment as thin rect
            Vector2 dir = (p2 - p1).normalized;
            float len = Vector2.Distance(p1, p2);
            Rect lineRect = new Rect(p1.x, p1.y, len, 1.5f);

            // Rotate via matrix
            var oldMatrix = GUI.matrix;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle, p1);
            EditorGUI.DrawRect(lineRect, color);
            GUI.matrix = oldMatrix;
        }
    }

    // ==================== PHYSICS & RENDERING ====================

    private void DrawPhysicsRendering()
    {
        EditorGUILayout.LabelField("Physics & Rendering", headerStyle);
        EditorGUILayout.Space(4);

        // Rendering
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("Rendering Settings", subHeaderStyle);
        EditorGUILayout.LabelField($"Quality Level: {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
        EditorGUILayout.LabelField($"Shadow Distance: {QualitySettings.shadowDistance}m");
        EditorGUILayout.LabelField($"Shadow Cascades: {QualitySettings.shadowCascades}");
        EditorGUILayout.LabelField($"Anti-Aliasing: {QualitySettings.antiAliasing}x");
        EditorGUILayout.LabelField($"VSync: {QualitySettings.vSyncCount}");
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        // Physics
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("Physics Settings", subHeaderStyle);
        EditorGUILayout.LabelField($"Fixed Timestep: {Time.fixedDeltaTime:F4}s ({1f / Time.fixedDeltaTime:F0} Hz)");
        EditorGUILayout.LabelField($"Solver Iterations: {Physics.defaultSolverIterations}");
        EditorGUILayout.LabelField($"Solver Velocity Iterations: {Physics.defaultSolverVelocityIterations}");
        EditorGUILayout.LabelField($"Auto Simulation: {Physics.simulationMode}");
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        // Layer culling distances
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("Layer Culling", subHeaderStyle);
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            float[] distances = mainCam.layerCullDistances;
            bool hasCustom = false;
            for (int i = 0; i < 32; i++)
            {
                if (distances[i] > 0)
                {
                    string layerName = LayerMask.LayerToName(i);
                    if (!string.IsNullOrEmpty(layerName))
                    {
                        EditorGUILayout.LabelField($"  {layerName}: {distances[i]}m");
                        hasCustom = true;
                    }
                }
            }
            if (!hasCustom)
                EditorGUILayout.LabelField("  No custom layer culling distances set");
        }
        else
        {
            EditorGUILayout.LabelField("  No main camera found");
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(8);
        if (GUILayout.Button("Optimize Physics Settings"))
        {
            Time.fixedDeltaTime = 0.02f;
            Physics.defaultSolverIterations = 6;
            Physics.defaultSolverVelocityIterations = 1;
            Debug.Log("[Dashboard] Physics optimized: 50Hz, 6 solver iters, 1 velocity iter");
        }
    }

    // ==================== ISSUE SCANNER ====================

    private void DrawIssueScanner()
    {
        EditorGUILayout.LabelField("Issue Scanner", headerStyle);
        EditorGUILayout.Space(4);

        if (GUILayout.Button("Scan for Issues", GUILayout.Height(30)))
        {
            scannedIssues = OptimizationProfiler.ScanAll();
        }

        if (scannedIssues == null || scannedIssues.Count == 0)
        {
            if (scannedIssues != null)
                EditorGUILayout.HelpBox("No issues found!", MessageType.Info);
            else
                EditorGUILayout.HelpBox("Click 'Scan for Issues' to analyze the project.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField($"Found {scannedIssues.Count} issues:", subHeaderStyle);
        EditorGUILayout.Space(4);

        foreach (var issue in scannedIssues)
        {
            DrawIssueEntry(issue);
        }
    }

    private void DrawIssueEntry(OptimizationProfiler.Issue issue)
    {
        Color bgColor;
        switch (issue.severity)
        {
            case OptimizationProfiler.Severity.Critical: bgColor = new Color(0.6f, 0.1f, 0.1f, 0.3f); break;
            case OptimizationProfiler.Severity.High: bgColor = new Color(0.6f, 0.4f, 0.1f, 0.3f); break;
            case OptimizationProfiler.Severity.Medium: bgColor = new Color(0.6f, 0.6f, 0.1f, 0.3f); break;
            default: bgColor = new Color(0.2f, 0.2f, 0.2f, 0.3f); break;
        }

        EditorGUILayout.BeginVertical(boxStyle);

        // Severity badge
        EditorGUILayout.BeginHorizontal();
        GUI.color = bgColor * 3f;
        EditorGUILayout.LabelField($"[{issue.severity}]", GUILayout.Width(70));
        GUI.color = Color.white;
        EditorGUILayout.LabelField(issue.title, EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField(issue.description, EditorStyles.wordWrappedLabel);

        if (!string.IsNullOrEmpty(issue.file))
        {
            if (GUILayout.Button($"Open: {issue.file}", EditorStyles.linkLabel))
            {
                var obj = AssetDatabase.LoadAssetAtPath<Object>(issue.file);
                if (obj != null) AssetDatabase.OpenAsset(obj);
            }
        }

        if (issue.canAutoFix)
        {
            if (GUILayout.Button("Auto Fix", GUILayout.Width(80)))
            {
                OptimizationProfiler.ApplyFix(issue.id);
                scannedIssues = OptimizationProfiler.ScanAll(); // Re-scan
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }
}
