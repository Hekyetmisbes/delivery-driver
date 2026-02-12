using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Editor tool for Sprint 4: Final Profiling and Regression Safety setup
/// Helps configure benchmark scenes, quality presets, and testing tools
/// </summary>
public class Sprint4SetupTool : EditorWindow
{
    private Vector2 scrollPosition;
    private string benchmarkName = "City Performance Test";
    private float benchmarkDuration = 60f;
    private bool createBenchmarkRoute = true;

    [MenuItem("Tools/Performance/Sprint 4 Setup")]
    public static void ShowWindow()
    {
        Sprint4SetupTool window = GetWindow<Sprint4SetupTool>("Sprint 4 Setup");
        window.minSize = new Vector2(500, 600);
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        DrawHeader();
        EditorGUILayout.Space(10);

        DrawBenchmarkSetup();
        EditorGUILayout.Space(10);

        DrawRegressionSetup();
        EditorGUILayout.Space(10);

        DrawMemoryProfilingSetup();
        EditorGUILayout.Space(10);

        DrawQualityPresetTools();
        EditorGUILayout.Space(10);

        DrawTestingTools();

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
        headerStyle.fontSize = 16;
        headerStyle.alignment = TextAnchor.MiddleCenter;

        EditorGUILayout.LabelField("Sprint 4: Final Profiling & Regression Safety", headerStyle);
        EditorGUILayout.Space(5);

        EditorGUILayout.HelpBox(
            "Sprint 4 focuses on:\n" +
            "• Benchmark scene creation\n" +
            "• Performance regression testing\n" +
            "• Memory leak detection\n" +
            "• Quality preset fine-tuning\n" +
            "• Final profiling and polish",
            MessageType.Info);
    }

    private void DrawBenchmarkSetup()
    {
        EditorGUILayout.LabelField("1. Benchmark Setup", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Create a benchmark scene with automated camera path for consistent performance testing.",
            MessageType.None);

        benchmarkName = EditorGUILayout.TextField("Benchmark Name", benchmarkName);
        benchmarkDuration = EditorGUILayout.FloatField("Duration (seconds)", benchmarkDuration);
        createBenchmarkRoute = EditorGUILayout.Toggle("Auto-create Route", createBenchmarkRoute);

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Add Benchmark System to Scene", GUILayout.Height(30)))
        {
            AddBenchmarkSystem();
        }

        // Check if already exists
        PerformanceBenchmark existing = FindObjectOfType<PerformanceBenchmark>();
        if (existing != null)
        {
            EditorGUILayout.HelpBox("✓ Benchmark system already in scene", MessageType.Info);
            if (GUILayout.Button("Select Benchmark Component"))
            {
                Selection.activeGameObject = existing.gameObject;
            }
        }
    }

    private void DrawRegressionSetup()
    {
        EditorGUILayout.LabelField("2. Regression Detection Setup", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Automatically detect performance regressions by comparing against baseline metrics.",
            MessageType.None);

        if (GUILayout.Button("Add Regression Detector to Scene", GUILayout.Height(30)))
        {
            AddRegressionDetector();
        }

        PerformanceRegressionDetector existingRegression = FindObjectOfType<PerformanceRegressionDetector>();
        if (existingRegression != null)
        {
            EditorGUILayout.HelpBox("✓ Regression detector already in scene", MessageType.Info);

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Quick Baseline Configuration:", EditorStyles.miniBoldLabel);

            if (GUILayout.Button("Set Baseline from Last Benchmark"))
            {
                SetBaselineFromBenchmark(existingRegression);
            }
        }
    }

    private void DrawMemoryProfilingSetup()
    {
        EditorGUILayout.LabelField("3. Memory Profiling Setup", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Track memory usage, detect leaks, and monitor GC allocations in real-time.",
            MessageType.None);

        if (GUILayout.Button("Add Memory Profiler to Scene", GUILayout.Height(30)))
        {
            AddMemoryProfiler();
        }

        MemoryProfiler existingMemory = FindObjectOfType<MemoryProfiler>();
        if (existingMemory != null)
        {
            EditorGUILayout.HelpBox("✓ Memory profiler already in scene\n\nIn Play Mode:\n• F3: Toggle overlay\n• F4: Force GC\n• F5: Export data", MessageType.Info);
        }
    }

    private void DrawQualityPresetTools()
    {
        EditorGUILayout.LabelField("4. Quality Preset Fine-Tuning", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Configure quality settings for Low/Medium/High tiers based on hardware capabilities.",
            MessageType.None);

        int currentQualityCount = QualitySettings.names.Length;
        EditorGUILayout.LabelField($"Current Quality Levels: {currentQualityCount}");

        if (currentQualityCount < 3)
        {
            EditorGUILayout.HelpBox("⚠️ Recommended: 3+ quality levels (Low/Medium/High)", MessageType.Warning);
        }

        if (GUILayout.Button("Open Quality Settings"))
        {
            SettingsService.OpenProjectSettings("Project/Quality");
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Recommended Quality Settings:", EditorStyles.miniBoldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Low:", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("• Shadow Distance: 30m");
        EditorGUILayout.LabelField("• Shadow Cascades: 2");
        EditorGUILayout.LabelField("• Pixel Light Count: 2");
        EditorGUILayout.LabelField("• Texture Quality: Medium");
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Medium:", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("• Shadow Distance: 50m");
        EditorGUILayout.LabelField("• Shadow Cascades: 3");
        EditorGUILayout.LabelField("• Pixel Light Count: 4");
        EditorGUILayout.LabelField("• Texture Quality: High");
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("High:", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("• Shadow Distance: 75m");
        EditorGUILayout.LabelField("• Shadow Cascades: 4");
        EditorGUILayout.LabelField("• Pixel Light Count: 6");
        EditorGUILayout.LabelField("• Texture Quality: Full");
        EditorGUILayout.EndVertical();
    }

    private void DrawTestingTools()
    {
        EditorGUILayout.LabelField("5. Testing & Validation", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Run benchmarks and validate performance across all quality levels.",
            MessageType.None);

        if (GUILayout.Button("Create Benchmark Results Folder"))
        {
            CreateBenchmarkResultsFolder();
        }

        if (GUILayout.Button("Open Benchmark Results Folder"))
        {
            OpenBenchmarkResultsFolder();
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "Testing Checklist:\n" +
            "□ Run benchmark on all quality levels\n" +
            "□ Test at different player speeds\n" +
            "□ Run 30-minute soak test for leaks\n" +
            "□ Verify regression detector alerts\n" +
            "□ Profile CPU/GPU/Memory in Development Build\n" +
            "□ Compare results with baseline metrics",
            MessageType.None);
    }

    private void AddBenchmarkSystem()
    {
        // Find or create camera
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject camObj = new GameObject("Benchmark Camera");
            mainCamera = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
        }

        // Add benchmark component
        PerformanceBenchmark benchmark = mainCamera.gameObject.GetComponent<PerformanceBenchmark>();
        if (benchmark == null)
        {
            benchmark = mainCamera.gameObject.AddComponent<PerformanceBenchmark>();
        }

        benchmark.benchmarkName = benchmarkName;
        benchmark.benchmarkDuration = benchmarkDuration;
        benchmark.useWaypoints = createBenchmarkRoute;

        EditorUtility.SetDirty(benchmark);
        Selection.activeGameObject = mainCamera.gameObject;

        Debug.Log("[Sprint4Setup] Benchmark system added to camera");
        EditorUtility.DisplayDialog("Benchmark Setup",
            "Benchmark system added!\n\n" +
            "Next steps:\n" +
            "1. Position camera at starting point\n" +
            "2. Click 'Setup Waypoints from Current Position'\n" +
            "3. Or manually add waypoints\n" +
            "4. Enter Play Mode and click 'Start Benchmark'",
            "OK");
    }

    private void AddRegressionDetector()
    {
        GameObject managerObj = GameObject.Find("PerformanceManagers");
        if (managerObj == null)
        {
            managerObj = new GameObject("PerformanceManagers");
        }

        PerformanceRegressionDetector detector = managerObj.GetComponent<PerformanceRegressionDetector>();
        if (detector == null)
        {
            detector = managerObj.AddComponent<PerformanceRegressionDetector>();
        }

        EditorUtility.SetDirty(detector);
        Selection.activeGameObject = managerObj;

        Debug.Log("[Sprint4Setup] Regression detector added");
        EditorUtility.DisplayDialog("Regression Detector",
            "Regression detector added!\n\n" +
            "It will automatically:\n" +
            "• Load baseline from previous benchmarks\n" +
            "• Monitor FPS, CPU, memory\n" +
            "• Alert on performance degradation\n" +
            "• Detect memory leaks and spikes",
            "OK");
    }

    private void AddMemoryProfiler()
    {
        GameObject managerObj = GameObject.Find("PerformanceManagers");
        if (managerObj == null)
        {
            managerObj = new GameObject("PerformanceManagers");
        }

        MemoryProfiler profiler = managerObj.GetComponent<MemoryProfiler>();
        if (profiler == null)
        {
            profiler = managerObj.AddComponent<MemoryProfiler>();
        }

        EditorUtility.SetDirty(profiler);
        Selection.activeGameObject = managerObj;

        Debug.Log("[Sprint4Setup] Memory profiler added");
        EditorUtility.DisplayDialog("Memory Profiler",
            "Memory profiler added!\n\n" +
            "In Play Mode:\n" +
            "• F3: Toggle memory overlay\n" +
            "• F4: Force garbage collection\n" +
            "• F5: Export snapshot history\n\n" +
            "Monitors:\n" +
            "• Managed/Native memory\n" +
            "• GC allocations per frame\n" +
            "• Memory leak detection",
            "OK");
    }

    private void SetBaselineFromBenchmark(PerformanceRegressionDetector detector)
    {
        string directory = Path.Combine(Application.dataPath, "..", "BenchmarkResults");
        string csvPath = Path.Combine(directory, "benchmark_history.csv");

        if (!File.Exists(csvPath))
        {
            EditorUtility.DisplayDialog("No Baseline", "No benchmark history found. Run a benchmark first.", "OK");
            return;
        }

        try
        {
            string[] lines = File.ReadAllLines(csvPath);
            if (lines.Length < 2)
            {
                EditorUtility.DisplayDialog("No Data", "Benchmark history is empty.", "OK");
                return;
            }

            string lastLine = lines[lines.Length - 1];
            string[] values = lastLine.Split(',');

            if (values.Length >= 9)
            {
                float avgFPS = float.Parse(values[2]);
                float low1Percent = float.Parse(values[3]);
                float cpuTime = float.Parse(values[4]);
                float memoryMB = float.Parse(values[6]);
                int drawCalls = int.Parse(values[7]);

                detector.SetBaseline(avgFPS, low1Percent, cpuTime, memoryMB, drawCalls);
                EditorUtility.SetDirty(detector);

                EditorUtility.DisplayDialog("Baseline Set",
                    $"Baseline configured:\n\n" +
                    $"FPS: {avgFPS:F1}\n" +
                    $"1% Low: {low1Percent:F1}\n" +
                    $"CPU: {cpuTime:F2}ms\n" +
                    $"Memory: {memoryMB:F1}MB",
                    "OK");
            }
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("Error", $"Failed to load baseline: {e.Message}", "OK");
        }
    }

    private void CreateBenchmarkResultsFolder()
    {
        string directory = Path.Combine(Application.dataPath, "..", "BenchmarkResults");
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            Debug.Log($"[Sprint4Setup] Created benchmark results folder: {directory}");
            EditorUtility.DisplayDialog("Folder Created", $"Benchmark results folder created at:\n{directory}", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Already Exists", "Benchmark results folder already exists", "OK");
        }
    }

    private void OpenBenchmarkResultsFolder()
    {
        string directory = Path.Combine(Application.dataPath, "..", "BenchmarkResults");
        if (Directory.Exists(directory))
        {
            EditorUtility.RevealInFinder(directory);
        }
        else
        {
            CreateBenchmarkResultsFolder();
        }
    }
}
