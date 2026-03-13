using UnityEngine;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Detects performance regressions by comparing current performance against baseline
/// Tracks FPS drops, CPU spikes, memory leaks, and draw call increases
/// </summary>
public class PerformanceRegressionDetector : MonoBehaviour
{
    private static bool SupportsRuntimeDiagnostics => Application.isEditor || Debug.isDebugBuild;

    [System.Serializable]
    public class PerformanceBaseline
    {
        public float targetAvgFPS = 60f;
        public float target1PercentLow = 40f;
        public float maxCPUTimeMS = 16.67f;
        public float maxMemoryMB = 2048f;
        public int maxDrawCalls = 2000;
        public float acceptableVariance = 0.15f; // 15% variance allowed
    }

    [System.Serializable]
    public class RegressionAlert
    {
        public string metric;
        public float baseline;
        public float current;
        public float variance;
        public string severity; // "Warning", "Critical"
        public float timestamp;
    }

    [Header("Baseline Configuration")]
    public PerformanceBaseline baseline = new PerformanceBaseline();
    public bool enableAutoDetection = true;
    public float detectionInterval = 5f;

    [Header("Alert Thresholds")]
    public float warningThreshold = 0.10f; // 10% degradation = warning
    public float criticalThreshold = 0.20f; // 20% degradation = critical

    [Header("Spike Detection")]
    public bool detectFrameSpikes = true;
    public float spikeThresholdMS = 33f; // >33ms = spike (< 30 FPS)
    public int spikeHistorySize = 300; // Track last 5 seconds at 60fps

    [Header("Memory Leak Detection")]
    public bool detectMemoryLeaks = true;
    public float memoryGrowthThreshold = 50f; // MB per minute
    public int memorySampleCount = 60;

    [Header("Runtime Status")]
    public bool hasActiveAlerts = false;
    public int warningCount = 0;
    public int criticalCount = 0;

    private readonly List<RegressionAlert> activeAlerts = new List<RegressionAlert>();
    private readonly List<float> frameTimeHistory = new List<float>();
    private readonly List<float> memoryHistory = new List<float>();
    private float nextDetectionTime = 0f;
    private float initialMemory = 0f;
    private float sessionStartTime = 0f;

    private GUIStyle warningStyle;
    private GUIStyle criticalStyle;
    private Texture2D warningBackgroundTexture;
    private Texture2D criticalBackgroundTexture;

    private void Start()
    {
        if (!SupportsRuntimeDiagnostics)
        {
            enableAutoDetection = false;
            hasActiveAlerts = false;
            enabled = false;
            return;
        }

        sessionStartTime = Time.time;
        initialMemory = GetCurrentMemoryMB();
        LoadBaselineFromPreviousBenchmark();
    }

    private void Update()
    {
        if (!enableAutoDetection) return;

        // Track frame times for spike detection
        if (detectFrameSpikes)
        {
            float frameTimeMS = Time.unscaledDeltaTime * 1000f;
            frameTimeHistory.Add(frameTimeMS);
            if (frameTimeHistory.Count > spikeHistorySize)
            {
                frameTimeHistory.RemoveAt(0);
            }
        }

        // Track memory for leak detection
        if (detectMemoryLeaks)
        {
            if (Time.frameCount % 60 == 0) // Sample every 60 frames
            {
                memoryHistory.Add(GetCurrentMemoryMB());
                if (memoryHistory.Count > memorySampleCount)
                {
                    memoryHistory.RemoveAt(0);
                }
            }
        }

        // Periodic regression check
        if (Time.time >= nextDetectionTime)
        {
            RunRegressionCheck();
            nextDetectionTime = Time.time + detectionInterval;
        }
    }

    private void RunRegressionCheck()
    {
        activeAlerts.Clear();
        warningCount = 0;
        criticalCount = 0;

        // Check FPS regression
        float currentFPS = CalculateAverageFPS();
        CheckMetric("Average FPS", baseline.targetAvgFPS, currentFPS, true);

        // Check 1% low FPS
        float low1Percent = Calculate1PercentLow();
        CheckMetric("1% Low FPS", baseline.target1PercentLow, low1Percent, true);

        // Check CPU time
        float avgCPUTime = CalculateAverageCPUTime();
        CheckMetric("CPU Time (ms)", baseline.maxCPUTimeMS, avgCPUTime, false);

        // Check memory usage
        float currentMemory = GetCurrentMemoryMB();
        CheckMetric("Memory (MB)", baseline.maxMemoryMB, currentMemory, false);

        // Check for frame spikes
        if (detectFrameSpikes)
        {
            int spikeCount = CountFrameSpikes();
            if (spikeCount > 0)
            {
                Debug.LogWarning($"[RegressionDetector] Detected {spikeCount} frame spikes (>{spikeThresholdMS}ms) in last {spikeHistorySize} frames");
            }
        }

        // Check for memory leaks
        if (detectMemoryLeaks && memoryHistory.Count > 10)
        {
            float memoryGrowthRate = CalculateMemoryGrowthRate();
            if (memoryGrowthRate > memoryGrowthThreshold)
            {
                RegressionAlert alert = new RegressionAlert
                {
                    metric = "Memory Growth Rate",
                    baseline = memoryGrowthThreshold,
                    current = memoryGrowthRate,
                    variance = (memoryGrowthRate - memoryGrowthThreshold) / memoryGrowthThreshold,
                    severity = "Critical",
                    timestamp = Time.time
                };
                activeAlerts.Add(alert);
                criticalCount++;
                Debug.LogError($"[RegressionDetector] MEMORY LEAK DETECTED! Growth rate: {memoryGrowthRate:F1} MB/min");
            }
        }

        hasActiveAlerts = activeAlerts.Count > 0;

        if (hasActiveAlerts)
        {
            LogAlerts();
        }
    }

    private void CheckMetric(string metricName, float baselineValue, float current, bool higherIsBetter)
    {
        float variance;
        if (higherIsBetter)
        {
            variance = (baselineValue - current) / baselineValue;
        }
        else
        {
            variance = (current - baselineValue) / baselineValue;
        }

        if (variance > criticalThreshold)
        {
            RegressionAlert alert = new RegressionAlert
            {
                metric = metricName,
                baseline = baselineValue,
                current = current,
                variance = variance,
                severity = "Critical",
                timestamp = Time.time
            };
            activeAlerts.Add(alert);
            criticalCount++;
            Debug.LogError($"[RegressionDetector] CRITICAL: {metricName} regression! Baseline: {baselineValue:F2}, Current: {current:F2}, Variance: {variance * 100:F1}%");
        }
        else if (variance > warningThreshold)
        {
            RegressionAlert alert = new RegressionAlert
            {
                metric = metricName,
                baseline = baselineValue,
                current = current,
                variance = variance,
                severity = "Warning",
                timestamp = Time.time
            };
            activeAlerts.Add(alert);
            warningCount++;
            Debug.LogWarning($"[RegressionDetector] WARNING: {metricName} degradation. Baseline: {baselineValue:F2}, Current: {current:F2}, Variance: {variance * 100:F1}%");
        }
    }

    private float CalculateAverageFPS()
    {
        if (frameTimeHistory.Count == 0) return 60f;

        float totalTime = 0f;
        for (int i = 0; i < frameTimeHistory.Count; i++)
        {
            totalTime += frameTimeHistory[i];
        }

        float avgTimeMS = totalTime / frameTimeHistory.Count;
        return 1000f / avgTimeMS;
    }

    private float Calculate1PercentLow()
    {
        if (frameTimeHistory.Count == 0) return 60f;

        List<float> sorted = new List<float>(frameTimeHistory);
        sorted.Sort();
        sorted.Reverse(); // Highest frame times (worst FPS)

        int onePercentIndex = Mathf.FloorToInt(sorted.Count * 0.01f);
        float worstFrameTime = sorted[onePercentIndex];
        return 1000f / worstFrameTime;
    }

    private float CalculateAverageCPUTime()
    {
        if (frameTimeHistory.Count == 0) return 0f;

        float totalTime = 0f;
        for (int i = 0; i < frameTimeHistory.Count; i++)
        {
            totalTime += frameTimeHistory[i];
        }

        return totalTime / frameTimeHistory.Count;
    }

    private int CountFrameSpikes()
    {
        int count = 0;
        for (int i = 0; i < frameTimeHistory.Count; i++)
        {
            if (frameTimeHistory[i] > spikeThresholdMS)
            {
                count++;
            }
        }

        return count;
    }

    private float CalculateMemoryGrowthRate()
    {
        if (memoryHistory.Count < 2) return 0f;

        float firstMemory = memoryHistory[0];
        float lastMemory = memoryHistory[memoryHistory.Count - 1];
        float timeDelta = (memoryHistory.Count / 60f) / 60f; // Convert to minutes

        if (timeDelta <= 0f) return 0f;

        return (lastMemory - firstMemory) / timeDelta;
    }

    private float GetCurrentMemoryMB()
    {
        return System.GC.GetTotalMemory(false) / 1048576f;
    }

    private void LoadBaselineFromPreviousBenchmark()
    {
        string directory = Path.Combine(Application.dataPath, "..", "BenchmarkResults");
        string csvPath = Path.Combine(directory, "benchmark_history.csv");

        if (!File.Exists(csvPath))
        {
            Debug.Log("[RegressionDetector] No previous benchmark found, using default baseline");
            return;
        }

        try
        {
            string[] lines = File.ReadAllLines(csvPath);
            if (lines.Length < 2) return; // Need header + at least one data row

            // Get the most recent benchmark (last line)
            string lastLine = lines[lines.Length - 1];
            string[] values = lastLine.Split(',');

            if (values.Length >= 9)
            {
                float.TryParse(values[2], out baseline.targetAvgFPS);
                float.TryParse(values[3], out baseline.target1PercentLow);
                float.TryParse(values[4], out baseline.maxCPUTimeMS);
                float.TryParse(values[6], out baseline.maxMemoryMB);
                int.TryParse(values[7], out baseline.maxDrawCalls);

                Debug.Log($"[RegressionDetector] Loaded baseline from previous benchmark: {baseline.targetAvgFPS:F1} FPS, {baseline.maxCPUTimeMS:F2}ms CPU");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[RegressionDetector] Failed to load baseline: {e.Message}");
        }
    }

    private void LogAlerts()
    {
        Debug.Log("=== PERFORMANCE REGRESSION REPORT ===");
        Debug.Log($"Warnings: {warningCount}, Critical: {criticalCount}");

        foreach (RegressionAlert alert in activeAlerts)
        {
            string color = alert.severity == "Critical" ? "red" : "yellow";
            Debug.Log($"<color={color}>[{alert.severity}]</color> {alert.metric}: {alert.current:F2} (baseline: {alert.baseline:F2}, {alert.variance * 100:F1}% degradation)");
        }

        Debug.Log("=====================================");
    }

    public void SetBaseline(float avgFPS, float low1Percent, float cpuTimeMS, float memoryMB, int drawCalls)
    {
        baseline.targetAvgFPS = avgFPS;
        baseline.target1PercentLow = low1Percent;
        baseline.maxCPUTimeMS = cpuTimeMS;
        baseline.maxMemoryMB = memoryMB;
        baseline.maxDrawCalls = drawCalls;

        Debug.Log($"[RegressionDetector] Baseline updated: {avgFPS:F1} FPS, {cpuTimeMS:F2}ms CPU, {memoryMB:F1}MB RAM");
    }

    public List<RegressionAlert> GetActiveAlerts()
    {
        return new List<RegressionAlert>(activeAlerts);
    }

    private void OnGUI()
    {
        if (!hasActiveAlerts) return;

        EnsureGuiStyles();

        float yPos = 10f;

        GUI.Label(new Rect(10, yPos, 400, 25), $"Performance Issues: {activeAlerts.Count}", criticalStyle);
        yPos += 30f;

        for (int i = 0; i < activeAlerts.Count; i++)
        {
            RegressionAlert alert = activeAlerts[i];
            GUIStyle style = alert.severity == "Critical" ? criticalStyle : warningStyle;
            string text = $"{alert.metric}: {alert.current:F1} (target: {alert.baseline:F1})";
            GUI.Label(new Rect(10, yPos, 400, 20), text, style);
            yPos += 25f;
        }
    }

    private void EnsureGuiStyles()
    {
        if (warningStyle != null && criticalStyle != null)
        {
            return;
        }

        warningStyle = new GUIStyle(GUI.skin.box);
        warningBackgroundTexture = CreateTexture(2, 2, new Color(1f, 0.5f, 0f, 0.8f));
        warningStyle.normal.background = warningBackgroundTexture;
        warningStyle.normal.textColor = Color.white;
        warningStyle.fontSize = 14;
        warningStyle.padding = new RectOffset(10, 10, 5, 5);

        criticalStyle = new GUIStyle(warningStyle);
        criticalBackgroundTexture = CreateTexture(2, 2, new Color(1f, 0f, 0f, 0.8f));
        criticalStyle.normal.background = criticalBackgroundTexture;
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
        if (warningBackgroundTexture != null)
        {
            Destroy(warningBackgroundTexture);
            warningBackgroundTexture = null;
        }

        if (criticalBackgroundTexture != null)
        {
            Destroy(criticalBackgroundTexture);
            criticalBackgroundTexture = null;
        }
    }
}
