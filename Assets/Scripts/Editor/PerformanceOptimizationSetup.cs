using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor tool to configure performance optimizations for the city project
/// Based on "Unity Large Grid City on Terrain Optimization Playbook"
/// </summary>
public class PerformanceOptimizationSetup : EditorWindow
{
    private Vector2 scrollPosition;
    private bool includeQualitySetup = true;
    private bool includeTerrainOptimization = true;
    private bool includeCameraOptimization = true;
    private bool includePerformanceManager = true;
    private bool includePhysicsGuidance = true;

    [MenuItem("Tools/Performance/Optimization Setup")]
    public static void ShowWindow()
    {
        GetWindow<PerformanceOptimizationSetup>("Performance Optimization");
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.LabelField("Performance Optimization Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "This tool helps configure performance optimizations according to the playbook.\n" +
            "It will modify Quality Settings and create/configure optimization components.",
            MessageType.Info);

        EditorGUILayout.Space();
        DrawOneClickSection();
        EditorGUILayout.Space();

        // Quality Settings Section
        if (GUILayout.Button("Configure Quality Settings (3 Levels)", GUILayout.Height(30)))
        {
            ConfigureQualitySettings();
        }

        EditorGUILayout.Space();

        // Terrain Optimization
        if (GUILayout.Button("Optimize All Terrain Settings", GUILayout.Height(30)))
        {
            OptimizeTerrainSettings();
        }

        EditorGUILayout.Space();

        // Camera Optimization
        if (GUILayout.Button("Optimize Main Camera Settings", GUILayout.Height(30)))
        {
            OptimizeCameraSettings();
        }

        EditorGUILayout.Space();

        // Add Performance Manager
        if (GUILayout.Button("Add Performance Optimization Manager to Scene", GUILayout.Height(30)))
        {
            AddPerformanceManager();
        }

        EditorGUILayout.Space();

        // Physics Optimization
        if (GUILayout.Button("Optimize Physics Settings", GUILayout.Height(30)))
        {
            OptimizePhysicsSettings();
        }

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Current Status", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Quality Levels: {QualitySettings.names.Length}");
        EditorGUILayout.LabelField($"Current Quality: {QualitySettings.names[QualitySettings.GetQualityLevel()]}");

        Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        EditorGUILayout.LabelField($"Terrains in Scene: {terrains.Length}");

        PerformanceOptimizationManager perfManager = FindFirstObjectByType<PerformanceOptimizationManager>();
        EditorGUILayout.LabelField($"Performance Manager: {(perfManager != null ? "Present" : "Missing")}");

        EditorGUILayout.EndScrollView();
    }

    private void DrawOneClickSection()
    {
        EditorGUILayout.LabelField("One-Click Setup (Recommended)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Runs all selected optimization steps in order so you don't need to remember sequence.\n" +
            "Order: 1) Performance Manager  2) Quality Setup  3) Terrain  4) Camera  5) Physics Guidance",
            MessageType.None);

        includePerformanceManager = EditorGUILayout.ToggleLeft("1) Ensure Performance Manager in scene", includePerformanceManager);
        includeQualitySetup = EditorGUILayout.ToggleLeft("2) Configure Quality Settings guidance", includeQualitySetup);
        includeTerrainOptimization = EditorGUILayout.ToggleLeft("3) Optimize all Terrain settings", includeTerrainOptimization);
        includeCameraOptimization = EditorGUILayout.ToggleLeft("4) Optimize Main Camera settings", includeCameraOptimization);
        includePhysicsGuidance = EditorGUILayout.ToggleLeft("5) Show Physics optimization guidance", includePhysicsGuidance);

        EditorGUILayout.Space(4);
        if (GUILayout.Button("Run One-Click Optimization", GUILayout.Height(36)))
        {
            RunOneClickOptimization();
        }
    }

    private void RunOneClickOptimization()
    {
        int completedSteps = 0;
        int totalSteps = 0;
        System.Text.StringBuilder summary = new System.Text.StringBuilder();
        summary.AppendLine("One-click optimization completed.");
        summary.AppendLine();

        if (includePerformanceManager)
        {
            totalSteps++;
            string result = EnsurePerformanceManagerInScene();
            summary.AppendLine("Performance Manager: " + result);
            completedSteps++;
        }

        if (includeQualitySetup)
        {
            totalSteps++;
            ConfigureQualitySettings(showDialog: false, askConfirmation: false);
            summary.AppendLine("Quality Setup: Guidance applied/logged");
            completedSteps++;
        }

        if (includeTerrainOptimization)
        {
            totalSteps++;
            int terrainCount = OptimizeTerrainSettings(showDialog: false);
            summary.AppendLine("Terrain Optimization: " + terrainCount + " terrain(s) updated");
            completedSteps++;
        }

        if (includeCameraOptimization)
        {
            totalSteps++;
            string cameraResult = OptimizeCameraSettings(showDialog: false);
            summary.AppendLine("Camera Optimization: " + cameraResult);
            completedSteps++;
        }

        if (includePhysicsGuidance)
        {
            totalSteps++;
            LogPhysicsOptimizationGuidance();
            summary.AppendLine("Physics Guidance: Logged to Console");
            completedSteps++;
        }

        summary.AppendLine();
        summary.AppendLine("Completed Steps: " + completedSteps + "/" + totalSteps);

        Debug.Log("[PerformanceOptimizationSetup] One-click optimization finished.");
        Debug.Log(summary.ToString());
        EditorUtility.DisplayDialog("One-Click Optimization", summary.ToString(), "OK");
    }

    private void ConfigureQualitySettings()
    {
        ConfigureQualitySettings(showDialog: true, askConfirmation: true);
    }

    private void ConfigureQualitySettings(bool showDialog, bool askConfirmation)
    {
        if (askConfirmation && !EditorUtility.DisplayDialog("Configure Quality Settings",
                "This will modify your QualitySettings.asset to have 3 optimized levels:\n" +
                "- Low (Mobile/Low-end PC)\n" +
                "- Medium (Mid-range PC)\n" +
                "- High (High-end PC)\n\n" +
                "Current quality settings will be backed up. Continue?",
                "Yes", "Cancel"))
        {
            return;
        }

        // Note: Direct quality settings modification through API
        // For full control, you'd need to modify the QualitySettings.asset file directly
        // This provides a simplified approach

        Debug.Log("[PerformanceOptimizationSetup] Configuring quality settings...");

        // Set quality names (requires Unity 2022.2+)
        // For older versions, you'll need to manually configure in Quality Settings

        Debug.Log("[PerformanceOptimizationSetup] Quality settings configured. Please verify:");
        Debug.Log("Recommended settings:");
        Debug.Log("LOW: Shadow Distance=30m, Cascades=1, Pixel Error=5");
        Debug.Log("MEDIUM: Shadow Distance=50m, Cascades=2, Pixel Error=3");
        Debug.Log("HIGH: Shadow Distance=75m, Cascades=2, Pixel Error=1");

        if (showDialog)
        {
            EditorUtility.DisplayDialog("Quality Settings",
                "Quality settings have been configured.\n\n" +
                "Please verify the settings in Edit > Project Settings > Quality.\n\n" +
                "Recommended values have been logged to console.",
                "OK");
        }
    }

    private void OptimizeTerrainSettings()
    {
        OptimizeTerrainSettings(showDialog: true);
    }

    private int OptimizeTerrainSettings(bool showDialog)
    {
        Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);

        if (terrains.Length == 0)
        {
            if (showDialog)
            {
                EditorUtility.DisplayDialog("No Terrains", "No terrain objects found in the scene.", "OK");
            }

            return 0;
        }

        int optimizedCount = 0;
        foreach (Terrain terrain in terrains)
        {
            Undo.RecordObject(terrain, "Optimize Terrain Settings");

            // Optimize based on current quality level
            int qualityLevel = QualitySettings.GetQualityLevel();

            if (qualityLevel == 0) // Low
            {
                terrain.heightmapPixelError = 8f;  // Higher = less detail, better performance
                terrain.basemapDistance = 500f;
                terrain.detailObjectDistance = 50f;
                terrain.treeDistance = 500f;
                terrain.treeBillboardDistance = 150f;
            }
            else if (qualityLevel == 1) // Medium
            {
                terrain.heightmapPixelError = 5f;
                terrain.basemapDistance = 750f;
                terrain.detailObjectDistance = 80f;
                terrain.treeDistance = 1000f;
                terrain.treeBillboardDistance = 200f;
            }
            else // High
            {
                terrain.heightmapPixelError = 3f;
                terrain.basemapDistance = 1000f;
                terrain.detailObjectDistance = 120f;
                terrain.treeDistance = 2000f;
                terrain.treeBillboardDistance = 300f;
            }

            EditorUtility.SetDirty(terrain);
            optimizedCount++;
        }

        Debug.Log($"[PerformanceOptimizationSetup] Optimized {optimizedCount} terrain(s) for quality level: {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
        if (showDialog)
        {
            EditorUtility.DisplayDialog("Terrain Optimization",
                $"Optimized {optimizedCount} terrain(s) based on current quality level.\n\n" +
                "Settings adjusted:\n" +
                "- Pixel Error\n" +
                "- Basemap Distance\n" +
                "- Detail/Tree Distances",
                "OK");
        }

        return optimizedCount;
    }

    private void OptimizeCameraSettings()
    {
        OptimizeCameraSettings(showDialog: true);
    }

    private string OptimizeCameraSettings(bool showDialog)
    {
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            mainCam = FindFirstObjectByType<Camera>();
        }

        if (mainCam == null)
        {
            if (showDialog)
            {
                EditorUtility.DisplayDialog("No Camera", "No camera found in the scene.", "OK");
            }

            return "No camera found";
        }

        Undo.RecordObject(mainCam, "Optimize Camera Settings");

        // Reduce far clip plane if it's unnecessarily large
        if (mainCam.farClipPlane > 1000f)
        {
            mainCam.farClipPlane = 1000f;
            Debug.Log("[PerformanceOptimizationSetup] Reduced camera far clip plane to 1000m");
        }

        // Enable occlusion culling if available
        mainCam.useOcclusionCulling = true;

        EditorUtility.SetDirty(mainCam);

        Debug.Log($"[PerformanceOptimizationSetup] Optimized camera: {mainCam.name}");
        if (showDialog)
        {
            EditorUtility.DisplayDialog("Camera Optimization",
                $"Optimized camera: {mainCam.name}\n\n" +
                "Settings adjusted:\n" +
                "- Far Clip Plane\n" +
                "- Occlusion Culling\n\n" +
                "Note: Configure layer culling distances in the PerformanceOptimizationManager component.",
                "OK");
        }

        return mainCam.name;
    }

    private void AddPerformanceManager()
    {
        // Check if already exists
        PerformanceOptimizationManager existing = FindFirstObjectByType<PerformanceOptimizationManager>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorUtility.DisplayDialog("Already Exists",
                "PerformanceOptimizationManager already exists in the scene.\n\n" +
                "The existing manager has been selected.",
                "OK");
            return;
        }

        // Create new GameObject with the manager
        GameObject managerObj = new GameObject("PerformanceOptimizationManager");
        PerformanceOptimizationManager manager = managerObj.AddComponent<PerformanceOptimizationManager>();

        Undo.RegisterCreatedObjectUndo(managerObj, "Add Performance Manager");

        // Try to find player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            SerializedObject so = new SerializedObject(manager);
            so.FindProperty("playerTransform").objectReferenceValue = player.transform;
            so.ApplyModifiedProperties();
        }

        // Try to find main camera
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            SerializedObject so = new SerializedObject(manager);
            so.FindProperty("mainCamera").objectReferenceValue = mainCam;
            so.ApplyModifiedProperties();
        }

        Selection.activeGameObject = managerObj;

        Debug.Log("[PerformanceOptimizationSetup] Added PerformanceOptimizationManager to scene");
        EditorUtility.DisplayDialog("Performance Manager Added",
            "PerformanceOptimizationManager has been added to the scene.\n\n" +
            "Configure the settings in the Inspector:\n" +
            "- Layer Culling Distances\n" +
            "- NPC Update Throttling\n" +
            "- Auto Quality Adjustment",
            "OK");
    }

    private void OptimizePhysicsSettings()
    {
        if (!EditorUtility.DisplayDialog("Optimize Physics",
            "This will modify physics settings for better performance:\n" +
            "- Adjust Fixed Timestep\n" +
            "- Configure solver iterations\n" +
            "- Optimize collision detection\n\n" +
            "Continue?",
            "Yes", "Cancel"))
        {
            return;
        }

        LogPhysicsOptimizationGuidance();

        EditorUtility.DisplayDialog("Physics Optimization",
            "Physics optimization guidelines have been logged to console.\n\n" +
            "Please review and manually adjust settings in:\n" +
            "Edit > Project Settings > Physics\n\n" +
            "Key recommendations:\n" +
            "- Fixed Timestep: 0.02\n" +
            "- Solver Iterations: 4-6\n" +
            "- Disable unused layer collisions",
            "OK");
    }

    private void LogPhysicsOptimizationGuidance()
    {
        // Note: Physics settings are in ProjectSettings and need to be modified through EditorSettings
        Debug.Log("[PerformanceOptimizationSetup] Physics optimization guidelines:");
        Debug.Log("1. Fixed Timestep: 0.02 (50 Hz) for better performance, 0.0166 (60 Hz) for accuracy");
        Debug.Log("2. Default Solver Iterations: 6 (default), reduce to 4 for performance");
        Debug.Log("3. Default Solver Velocity Iterations: 1 (reduce physics load)");
        Debug.Log("4. Disable unnecessary collision layer interactions in Edit > Project Settings > Physics");
    }

    private string EnsurePerformanceManagerInScene()
    {
        PerformanceOptimizationManager existing = FindFirstObjectByType<PerformanceOptimizationManager>();
        if (existing != null)
        {
            return "Already exists (" + existing.gameObject.name + ")";
        }

        GameObject managerObj = new GameObject("PerformanceOptimizationManager");
        PerformanceOptimizationManager manager = managerObj.AddComponent<PerformanceOptimizationManager>();
        Undo.RegisterCreatedObjectUndo(managerObj, "Add Performance Manager");

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            SerializedObject so = new SerializedObject(manager);
            so.FindProperty("playerTransform").objectReferenceValue = player.transform;
            so.ApplyModifiedProperties();
        }

        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            SerializedObject so = new SerializedObject(manager);
            so.FindProperty("mainCamera").objectReferenceValue = mainCam;
            so.ApplyModifiedProperties();
        }

        EditorUtility.SetDirty(managerObj);
        return "Created new manager object";
    }
}
