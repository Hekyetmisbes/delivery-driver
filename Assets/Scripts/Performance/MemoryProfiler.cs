using UnityEngine;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Profiling;

/// <summary>
/// Runtime memory profiling tool for detecting leaks and allocation hotspots
/// Tracks managed memory, native memory, and GC allocations
/// </summary>
public class MemoryProfiler : MonoBehaviour
{
    [System.Serializable]
    public class MemorySnapshot
    {
        public float timestamp;
        public long managedMemoryMB;
        public long nativeMemoryMB;
        public long totalMemoryMB;
        public long gcReservedMB;
        public long gcUsedMB;
        public int textureMemoryMB;
        public int meshMemoryMB;
        public int audioMemoryMB;
    }

    [Header("Monitoring Settings")]
    public bool enableMonitoring = true;
    public float snapshotInterval = 5f;
    public int maxSnapshotHistory = 120; // 10 minutes at 5s intervals

    [Header("Leak Detection")]
    public bool enableLeakDetection = true;
    public float leakThresholdMBPerMin = 30f;
    public int minSnapshotsForDetection = 10;

    [Header("GC Monitoring")]
    public bool trackGCAllocations = true;
    public float gcWarningThresholdKB = 100f; // Warn if >100KB allocated per frame

    [Header("Display Settings")]
    public bool showMemoryOverlay = true;
    public KeyCode toggleKey = KeyCode.F3;

    [Header("Runtime Status")]
    public long currentManagedMB = 0;
    public long currentNativeMB = 0;
    public bool leakDetected = false;
    public float leakRateMBPerMin = 0f;

    private readonly List<MemorySnapshot> snapshotHistory = new List<MemorySnapshot>();
    private float nextSnapshotTime = 0f;
    private long lastGCMemory = 0;
    private float lastGCAllocationKB = 0f;
    private bool overlayVisible = false;

    // Cached GUI resources to avoid per-frame allocations in OnGUI.
    private GUIStyle overlayBoxStyle;
    private Texture2D overlayBackgroundTexture;
    private readonly StringBuilder overlayTextBuilder = new StringBuilder(512);

    private void Start()
    {
        TakeSnapshot();
        overlayVisible = showMemoryOverlay;
        EnsureOverlayStyle();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            overlayVisible = !overlayVisible;
        }

        if (!enableMonitoring) return;

        // Track GC allocations
        if (trackGCAllocations)
        {
            long currentGC = System.GC.GetTotalMemory(false);
            long allocatedSinceLastFrame = currentGC - lastGCMemory;
            lastGCAllocationKB = allocatedSinceLastFrame / 1024f;

            if (lastGCAllocationKB > gcWarningThresholdKB)
            {
                Debug.LogWarning($"[MemoryProfiler] High GC allocation: {lastGCAllocationKB:F1} KB in this frame!");
            }

            lastGCMemory = currentGC;
        }

        // Take periodic snapshots
        if (Time.time >= nextSnapshotTime)
        {
            TakeSnapshot();
            nextSnapshotTime = Time.time + snapshotInterval;

            if (enableLeakDetection && snapshotHistory.Count >= minSnapshotsForDetection)
            {
                DetectMemoryLeak();
            }
        }
    }

    private void TakeSnapshot()
    {
        MemorySnapshot snapshot = new MemorySnapshot();
        snapshot.timestamp = Time.time;
        snapshot.managedMemoryMB = System.GC.GetTotalMemory(false) / 1048576;
        snapshot.nativeMemoryMB = (long)(Profiler.GetTotalAllocatedMemoryLong() / 1048576);
        snapshot.totalMemoryMB = (long)(Profiler.GetTotalReservedMemoryLong() / 1048576);
        snapshot.gcReservedMB = (long)(Profiler.GetMonoHeapSizeLong() / 1048576);
        snapshot.gcUsedMB = (long)(Profiler.GetMonoUsedSizeLong() / 1048576);
        snapshot.textureMemoryMB = (int)(Profiler.GetAllocatedMemoryForGraphicsDriver() / 1048576);
        snapshot.meshMemoryMB = 0; // Would need custom tracking
        snapshot.audioMemoryMB = 0; // Would need custom tracking

        snapshotHistory.Add(snapshot);
        if (snapshotHistory.Count > maxSnapshotHistory)
        {
            snapshotHistory.RemoveAt(0);
        }

        currentManagedMB = snapshot.managedMemoryMB;
        currentNativeMB = snapshot.nativeMemoryMB;
    }

    private void DetectMemoryLeak()
    {
        if (snapshotHistory.Count < 2) return;

        // Calculate memory growth rate using linear regression
        int sampleCount = Mathf.Min(snapshotHistory.Count, minSnapshotsForDetection);
        MemorySnapshot first = snapshotHistory[snapshotHistory.Count - sampleCount];
        MemorySnapshot last = snapshotHistory[snapshotHistory.Count - 1];

        float timeDeltaMinutes = (last.timestamp - first.timestamp) / 60f;
        if (timeDeltaMinutes <= 0f) return;

        long memoryGrowthMB = last.totalMemoryMB - first.totalMemoryMB;
        leakRateMBPerMin = memoryGrowthMB / timeDeltaMinutes;

        if (leakRateMBPerMin > leakThresholdMBPerMin)
        {
            if (!leakDetected)
            {
                Debug.LogError($"[MemoryProfiler] MEMORY LEAK DETECTED! Growth rate: {leakRateMBPerMin:F2} MB/min");
                leakDetected = true;
            }
        }
        else
        {
            leakDetected = false;
        }
    }

    public void ForceGarbageCollection()
    {
        Debug.Log("[MemoryProfiler] Forcing garbage collection...");
        long before = System.GC.GetTotalMemory(false);
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
        long after = System.GC.GetTotalMemory(false);
        long freedMB = (before - after) / 1048576;
        Debug.Log($"[MemoryProfiler] GC freed {freedMB} MB");
    }

    public void ExportSnapshotHistory()
    {
        if (snapshotHistory.Count == 0)
        {
            Debug.LogWarning("[MemoryProfiler] No snapshots to export");
            return;
        }

        string csv = "Timestamp,Managed MB,Native MB,Total MB,GC Reserved MB,GC Used MB,Texture MB\n";
        foreach (MemorySnapshot snapshot in snapshotHistory)
        {
            csv += $"{snapshot.timestamp:F2},{snapshot.managedMemoryMB},{snapshot.nativeMemoryMB}," +
                   $"{snapshot.totalMemoryMB},{snapshot.gcReservedMB},{snapshot.gcUsedMB},{snapshot.textureMemoryMB}\n";
        }

        string path = System.IO.Path.Combine(Application.dataPath, "..", "BenchmarkResults", "memory_profile.csv");
        System.IO.File.WriteAllText(path, csv);
        Debug.Log($"[MemoryProfiler] Exported snapshot history to: {path}");
    }

    private void OnGUI()
    {
        if (!overlayVisible) return;

        EnsureOverlayStyle();
        overlayTextBuilder.Length = 0;
        overlayTextBuilder.AppendLine("=== MEMORY PROFILE ===");
        overlayTextBuilder.AppendLine($"Managed: {currentManagedMB} MB");
        overlayTextBuilder.AppendLine($"Native: {currentNativeMB} MB");
        overlayTextBuilder.AppendLine($"Total: {Profiler.GetTotalReservedMemoryLong() / 1048576} MB");
        overlayTextBuilder.AppendLine($"GC Heap: {Profiler.GetMonoUsedSizeLong() / 1048576} / {Profiler.GetMonoHeapSizeLong() / 1048576} MB");
        overlayTextBuilder.AppendLine($"GPU: {Profiler.GetAllocatedMemoryForGraphicsDriver() / 1048576} MB");
        overlayTextBuilder.AppendLine();
        overlayTextBuilder.AppendLine($"Frame GC Alloc: {lastGCAllocationKB:F1} KB");
        overlayTextBuilder.AppendLine($"Snapshots: {snapshotHistory.Count} / {maxSnapshotHistory}");
        overlayTextBuilder.AppendLine();

        if (leakDetected)
        {
            overlayTextBuilder.AppendLine("LEAK DETECTED!");
        }

        overlayTextBuilder.AppendLine($"Growth: {leakRateMBPerMin:F2} MB/min");
        overlayTextBuilder.AppendLine();
        overlayTextBuilder.Append($"[{toggleKey}] Toggle | [F4] GC | [F5] Export");

        float width = 300f;
        float height = 280f;
        GUI.Box(new Rect(Screen.width - width - 10, 10, width, height), overlayTextBuilder.ToString(), overlayBoxStyle);

        // Handle hotkeys
        if (Event.current.type == EventType.KeyDown)
        {
            if (Event.current.keyCode == KeyCode.F4)
            {
                ForceGarbageCollection();
            }
            else if (Event.current.keyCode == KeyCode.F5)
            {
                ExportSnapshotHistory();
            }
        }
    }

    private void EnsureOverlayStyle()
    {
        if (overlayBoxStyle != null)
        {
            return;
        }

        overlayBoxStyle = new GUIStyle(GUI.skin.box);
        overlayBackgroundTexture = CreateTexture(2, 2, new Color(0f, 0f, 0f, 0.7f));
        overlayBoxStyle.normal.background = overlayBackgroundTexture;
        overlayBoxStyle.normal.textColor = Color.white;
        overlayBoxStyle.fontSize = 12;
        overlayBoxStyle.alignment = TextAnchor.UpperLeft;
        overlayBoxStyle.padding = new RectOffset(10, 10, 10, 10);
    }

    private static Texture2D CreateTexture(int width, int height, Color color)
    {
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private void OnDestroy()
    {
        if (overlayBackgroundTexture != null)
        {
            Destroy(overlayBackgroundTexture);
            overlayBackgroundTexture = null;
        }
    }

    // Draw memory graph
    private void DrawMemoryGraph(Rect rect)
    {
        if (snapshotHistory.Count < 2) return;

        // Find min/max for scaling
        long minMem = long.MaxValue;
        long maxMem = long.MinValue;
        foreach (MemorySnapshot s in snapshotHistory)
        {
            minMem = System.Math.Min(minMem, s.totalMemoryMB);
            maxMem = System.Math.Max(maxMem, s.totalMemoryMB);
        }

        if (maxMem <= minMem) return;

        // Draw graph lines
        for (int i = 1; i < snapshotHistory.Count; i++)
        {
            float x1 = rect.x + (i - 1) * rect.width / snapshotHistory.Count;
            float x2 = rect.x + i * rect.width / snapshotHistory.Count;

            float y1 = rect.y + rect.height - ((snapshotHistory[i - 1].totalMemoryMB - minMem) / (float)(maxMem - minMem)) * rect.height;
            float y2 = rect.y + rect.height - ((snapshotHistory[i].totalMemoryMB - minMem) / (float)(maxMem - minMem)) * rect.height;

            Drawing.DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), Color.green, 2f);
        }
    }
}

// Simple line drawing utility
public static class Drawing
{
    private static Texture2D lineTex;

    public static void DrawLine(Vector2 start, Vector2 end, Color color, float width)
    {
        if (!lineTex)
        {
            lineTex = new Texture2D(1, 1);
            lineTex.SetPixel(0, 0, Color.white);
            lineTex.Apply();
        }

        Vector2 d = end - start;
        float a = Mathf.Rad2Deg * Mathf.Atan2(d.y, d.x);
        if (d.x < 0) a += 180f;

        Color savedColor = GUI.color;
        GUI.color = color;

        GUIUtility.RotateAroundPivot(a, start);
        GUI.DrawTexture(new Rect(start.x, start.y - width / 2, d.magnitude, width), lineTex);
        GUIUtility.RotateAroundPivot(-a, start);

        GUI.color = savedColor;
    }
}
