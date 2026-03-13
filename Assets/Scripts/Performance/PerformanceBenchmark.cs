using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Automated benchmark system for consistent performance testing
/// Creates fixed camera paths and measures FPS, frame times, memory
/// </summary>
public class PerformanceBenchmark : MonoBehaviour
{
    private static bool SupportsRuntimeDiagnostics => Application.isEditor || Debug.isDebugBuild;

    [System.Serializable]
    public class BenchmarkResult
    {
        public string testName;
        public string timestamp;
        public float avgFPS;
        public float onePercentLow;
        public float avgCPUTime;
        public float avgGPUTime;
        public float peakMemoryMB;
        public int drawCalls;
        public int triangles;
        public string notes;
    }

    [System.Serializable]
    public class Waypoint
    {
        public Vector3 position;
        public Quaternion rotation;
        public float duration = 3f;
    }

    [Header("Benchmark Configuration")]
    public string benchmarkName = "City Performance Test";
    public float benchmarkDuration = 60f;
    public bool useWaypoints = true;
    public List<Waypoint> waypoints = new List<Waypoint>();

    [Header("Measurement Settings")]
    public int warmupFrames = 120;
    public bool autoSaveResults = true;
    public string resultsPath = "BenchmarkResults";

    [Header("Runtime Info")]
    public bool isRunning = false;
    public float progress = 0f;

    private List<float> fpsHistory = new List<float>();
    private List<float> cpuTimeHistory = new List<float>();
    private float peakMemory = 0f;
    private int frameCount = 0;
    private float elapsedTime = 0f;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private int currentWaypointIndex = 0;
    private float waypointTimer = 0f;

    private void Start()
    {
        if (!SupportsRuntimeDiagnostics)
        {
            enabled = false;
            return;
        }

        if (waypoints.Count == 0)
        {
            SetupDefaultWaypoints();
        }
    }

    private void SetupDefaultWaypoints()
    {
        // Create a simple circular path around the city center
        int waypointCount = 8;
        float radius = 200f;
        float height = 50f;

        for (int i = 0; i < waypointCount; i++)
        {
            float angle = (i / (float)waypointCount) * Mathf.PI * 2f;
            Waypoint wp = new Waypoint();
            wp.position = new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
            wp.rotation = Quaternion.LookRotation(Vector3.zero - wp.position);
            wp.duration = benchmarkDuration / waypointCount;
            waypoints.Add(wp);
        }
    }

    public void StartBenchmark()
    {
        if (isRunning) return;
        StartCoroutine(RunBenchmark());
    }

    private IEnumerator RunBenchmark()
    {
        isRunning = true;
        progress = 0f;
        fpsHistory.Clear();
        cpuTimeHistory.Clear();
        peakMemory = 0f;
        frameCount = 0;
        elapsedTime = 0f;
        currentWaypointIndex = 0;
        waypointTimer = 0f;

        // Store original camera position
        originalPosition = transform.position;
        originalRotation = transform.rotation;

        Debug.Log($"[PerformanceBenchmark] Starting benchmark: {benchmarkName}");
        Debug.Log($"[PerformanceBenchmark] Warmup: {warmupFrames} frames");

        // Warmup phase
        for (int i = 0; i < warmupFrames; i++)
        {
            yield return null;
        }

        Debug.Log("[PerformanceBenchmark] Warmup complete, starting measurement...");

        // Measurement phase
        float startTime = Time.time;
        while (elapsedTime < benchmarkDuration)
        {
            // Update camera position along waypoint path
            if (useWaypoints && waypoints.Count > 0)
            {
                UpdateCameraPath();
            }

            // Collect performance metrics
            float fps = 1f / Time.unscaledDeltaTime;
            float cpuTime = Time.deltaTime * 1000f;
            float memoryMB = (System.GC.GetTotalMemory(false) / 1048576f);

            fpsHistory.Add(fps);
            cpuTimeHistory.Add(cpuTime);
            peakMemory = Mathf.Max(peakMemory, memoryMB);

            frameCount++;
            elapsedTime = Time.time - startTime;
            progress = Mathf.Clamp01(elapsedTime / benchmarkDuration);

            yield return null;
        }

        // Restore camera position
        transform.position = originalPosition;
        transform.rotation = originalRotation;

        // Generate results
        BenchmarkResult result = GenerateResult();
        Debug.Log($"[PerformanceBenchmark] Benchmark complete!");
        Debug.Log($"Average FPS: {result.avgFPS:F1}, 1% Low: {result.onePercentLow:F1}");
        Debug.Log($"CPU Time: {result.avgCPUTime:F2}ms, Peak Memory: {result.peakMemoryMB:F1}MB");

        if (autoSaveResults)
        {
            SaveResultToFile(result);
        }

        isRunning = false;
    }

    private void UpdateCameraPath()
    {
        if (waypoints.Count == 0) return;

        waypointTimer += Time.deltaTime;
        Waypoint current = waypoints[currentWaypointIndex];

        if (waypointTimer >= current.duration)
        {
            waypointTimer = 0f;
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Count;
        }

        // Smooth movement to next waypoint
        Waypoint next = waypoints[(currentWaypointIndex + 1) % waypoints.Count];
        float t = waypointTimer / current.duration;
        t = Mathf.SmoothStep(0f, 1f, t);

        transform.position = Vector3.Lerp(current.position, next.position, t);
        transform.rotation = Quaternion.Slerp(current.rotation, next.rotation, t);
    }

    private BenchmarkResult GenerateResult()
    {
        BenchmarkResult result = new BenchmarkResult();
        result.testName = benchmarkName;
        result.timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // Calculate average FPS
        float totalFPS = 0f;
        foreach (float fps in fpsHistory)
        {
            totalFPS += fps;
        }
        result.avgFPS = totalFPS / fpsHistory.Count;

        // Calculate 1% low FPS
        fpsHistory.Sort();
        int onePercentIndex = Mathf.FloorToInt(fpsHistory.Count * 0.01f);
        result.onePercentLow = fpsHistory[onePercentIndex];

        // Calculate average CPU time
        float totalCPU = 0f;
        foreach (float cpu in cpuTimeHistory)
        {
            totalCPU += cpu;
        }
        result.avgCPUTime = totalCPU / cpuTimeHistory.Count;

        result.avgGPUTime = 0f; // Would need GPU profiler integration
        result.peakMemoryMB = peakMemory;

        #if UNITY_EDITOR
        result.drawCalls = UnityEditor.UnityStats.drawCalls;
        result.triangles = UnityEditor.UnityStats.triangles;
        #endif

        result.notes = $"Quality: {QualitySettings.names[QualitySettings.GetQualityLevel()]}, Platform: {Application.platform}";

        return result;
    }

    private void SaveResultToFile(BenchmarkResult result)
    {
        string directory = Path.Combine(Application.dataPath, "..", resultsPath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string filename = $"benchmark_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";
        string filepath = Path.Combine(directory, filename);

        string json = JsonUtility.ToJson(result, true);
        File.WriteAllText(filepath, json);

        Debug.Log($"[PerformanceBenchmark] Results saved to: {filepath}");

        // Also append to CSV for easy comparison
        AppendToCSV(result);
    }

    private void AppendToCSV(BenchmarkResult result)
    {
        string directory = Path.Combine(Application.dataPath, "..", resultsPath);
        string csvPath = Path.Combine(directory, "benchmark_history.csv");

        bool fileExists = File.Exists(csvPath);

        using (StreamWriter writer = new StreamWriter(csvPath, true))
        {
            if (!fileExists)
            {
                // Write header
                writer.WriteLine("Date,Test Name,Avg FPS,1% Low,CPU ms,GPU ms,RAM MB,Draw Calls,Triangles,Notes");
            }

            // Write data
            writer.WriteLine($"{result.timestamp},{result.testName},{result.avgFPS:F1},{result.onePercentLow:F1}," +
                           $"{result.avgCPUTime:F2},{result.avgGPUTime:F2},{result.peakMemoryMB:F1}," +
                           $"{result.drawCalls},{result.triangles},\"{result.notes}\"");
        }

        Debug.Log($"[PerformanceBenchmark] Results appended to CSV: {csvPath}");
    }

    private void OnDrawGizmos()
    {
        if (waypoints.Count == 0) return;

        // Draw waypoint path
        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Count; i++)
        {
            Waypoint current = waypoints[i];
            Waypoint next = waypoints[(i + 1) % waypoints.Count];

            Gizmos.DrawSphere(current.position, 2f);
            Gizmos.DrawLine(current.position, next.position);

            // Draw direction arrow
            Vector3 forward = current.rotation * Vector3.forward * 10f;
            Gizmos.DrawRay(current.position, forward);
        }

        // Highlight current waypoint during benchmark
        if (isRunning && waypoints.Count > 0)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(waypoints[currentWaypointIndex].position, 5f);
        }
    }

    #if UNITY_EDITOR
    [CustomEditor(typeof(PerformanceBenchmark))]
    public class PerformanceBenchmarkEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PerformanceBenchmark benchmark = (PerformanceBenchmark)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Benchmark Control", EditorStyles.boldLabel);

            if (benchmark.isRunning)
            {
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), benchmark.progress,
                    $"Running... {(benchmark.progress * 100f):F0}%");
                GUI.enabled = false;
            }

            if (GUILayout.Button("Start Benchmark", GUILayout.Height(30)))
            {
                if (Application.isPlaying)
                {
                    benchmark.StartBenchmark();
                }
                else
                {
                    EditorUtility.DisplayDialog("Benchmark", "Enter Play Mode to run benchmark", "OK");
                }
            }

            GUI.enabled = true;

            EditorGUILayout.Space();
            if (GUILayout.Button("Setup Waypoints from Current Position"))
            {
                SetupWaypointsFromCurrentPosition(benchmark);
            }

            if (GUILayout.Button("Add Waypoint at Current Position"))
            {
                AddWaypointAtCurrentPosition(benchmark);
            }

            if (benchmark.waypoints.Count > 0 && GUILayout.Button("Clear All Waypoints"))
            {
                if (EditorUtility.DisplayDialog("Clear Waypoints", "Remove all waypoints?", "Yes", "Cancel"))
                {
                    benchmark.waypoints.Clear();
                    EditorUtility.SetDirty(benchmark);
                }
            }
        }

        private void SetupWaypointsFromCurrentPosition(PerformanceBenchmark benchmark)
        {
            benchmark.waypoints.Clear();
            Vector3 center = benchmark.transform.position;
            float radius = 200f;
            float height = benchmark.transform.position.y;

            for (int i = 0; i < 8; i++)
            {
                float angle = (i / 8f) * Mathf.PI * 2f;
                Waypoint wp = new Waypoint();
                wp.position = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                wp.position.y = height;
                wp.rotation = Quaternion.LookRotation(center - wp.position);
                wp.duration = benchmark.benchmarkDuration / 8f;
                benchmark.waypoints.Add(wp);
            }

            EditorUtility.SetDirty(benchmark);
            Debug.Log("[PerformanceBenchmark] Created 8 waypoints in circular path");
        }

        private void AddWaypointAtCurrentPosition(PerformanceBenchmark benchmark)
        {
            Waypoint wp = new Waypoint();
            wp.position = benchmark.transform.position;
            wp.rotation = benchmark.transform.rotation;
            wp.duration = 5f;
            benchmark.waypoints.Add(wp);
            EditorUtility.SetDirty(benchmark);
            Debug.Log($"[PerformanceBenchmark] Added waypoint {benchmark.waypoints.Count} at current position");
        }
    }
    #endif
}
