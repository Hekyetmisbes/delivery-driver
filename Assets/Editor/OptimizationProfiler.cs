using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DeliveryDriver.Optimization;
using TrafficSystem;

public class OptimizationProfiler
{
    public enum Severity { Critical, High, Medium, Info }

    public class Issue
    {
        public string id;
        public Severity severity;
        public string title;
        public string description;
        public string file;
        public bool canAutoFix;
    }

    private static readonly List<Issue> cachedIssues = new List<Issue>();
    public static IReadOnlyList<Issue> CachedIssues => cachedIssues;

    // --- Play mode metrics ---

    public static float GetCurrentFPS()
    {
        if (!Application.isPlaying) return 0f;
        var controller = UnifiedOptimizationController.Instance;
        return controller != null ? controller.CurrentFPS : (1f / Time.unscaledDeltaTime);
    }

    public static long GetManagedMemoryBytes()
    {
        return UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
    }

    public static int GetDrawCallCount()
    {
#if UNITY_EDITOR
        // UnityStats only available in editor play mode
        if (Application.isPlaying)
            return UnityEditor.UnityStats.drawCalls;
#endif
        return 0;
    }

    // --- Scene scan ---

    public static List<Issue> ScanAll()
    {
        cachedIssues.Clear();
        ScanScene(cachedIssues);
        ScanProjectSettings(cachedIssues);
        ScanCodePatterns(cachedIssues);
        cachedIssues.Sort((a, b) => a.severity.CompareTo(b.severity));
        return cachedIssues;
    }

    public static void ScanScene(List<Issue> issues)
    {
        // Check for missing optimization components
        if (Object.FindAnyObjectByType<WorldChunkManager>() == null)
        {
            issues.Add(new Issue
            {
                id = "scene_no_chunk_manager",
                severity = Severity.High,
                title = "WorldChunkManager missing",
                description = "No WorldChunkManager found in scene. World streaming disabled.",
                canAutoFix = false
            });
        }

        if (Object.FindAnyObjectByType<TrafficSimulationOptimizer>() == null)
        {
            issues.Add(new Issue
            {
                id = "scene_no_traffic_opt",
                severity = Severity.High,
                title = "TrafficSimulationOptimizer missing",
                description = "No TrafficSimulationOptimizer found. NPC throttling disabled.",
                canAutoFix = false
            });
        }

        if (TrafficCommunicationSystem.Instance == null &&
            Object.FindAnyObjectByType<TrafficCommunicationSystem>() == null)
        {
            issues.Add(new Issue
            {
                id = "scene_no_traffic_comms",
                severity = Severity.Medium,
                title = "TrafficCommunicationSystem missing",
                description = "No TrafficCommunicationSystem found. Spatial grid queries unavailable.",
                canAutoFix = false
            });
        }

        // Check for occlusion culling
        if (UnityEditor.StaticOcclusionCulling.umbraDataSize == 0)
        {
            issues.Add(new Issue
            {
                id = "scene_no_occlusion",
                severity = Severity.Medium,
                title = "Occlusion Culling not baked",
                description = "No occlusion culling data. Bake via Window > Rendering > Occlusion Culling.",
                canAutoFix = false
            });
        }
    }

    public static void ScanProjectSettings(List<Issue> issues)
    {
        // Shadow distance
        if (QualitySettings.shadowDistance > 100f)
        {
            issues.Add(new Issue
            {
                id = "settings_shadow_distance",
                severity = Severity.Medium,
                title = $"Shadow distance too high ({QualitySettings.shadowDistance}m)",
                description = "Reduce shadow distance to 40-75m for better performance.",
                canAutoFix = true
            });
        }

        // Physics timestep
        if (Time.fixedDeltaTime < 0.015f)
        {
            issues.Add(new Issue
            {
                id = "settings_physics_timestep",
                severity = Severity.Medium,
                title = $"Physics timestep very small ({Time.fixedDeltaTime:F4}s)",
                description = "Consider 0.02s (50Hz) for vehicle sim. Current rate is high.",
                canAutoFix = true
            });
        }

        // Anti-aliasing
        if (QualitySettings.antiAliasing > 4)
        {
            issues.Add(new Issue
            {
                id = "settings_aa_high",
                severity = Severity.Medium,
                title = $"Anti-aliasing set to {QualitySettings.antiAliasing}x",
                description = "8x MSAA is expensive. Consider 2x or 4x.",
                canAutoFix = true
            });
        }
    }

    public static void ScanCodePatterns(List<Issue> issues)
    {
        // Search for known problematic patterns in script files
        string[] scriptGuids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/Scripts" });

        foreach (string guid in scriptGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string content = System.IO.File.ReadAllText(path);
            string fileName = System.IO.Path.GetFileName(path);

            // Check for allocating OverlapSphere (not NonAlloc)
            if (Regex.IsMatch(content, @"Physics\.OverlapSphere\(") &&
                !Regex.IsMatch(content, @"Physics\.OverlapSphereNonAlloc\("))
            {
                issues.Add(new Issue
                {
                    id = $"code_overlap_sphere_{fileName}",
                    severity = Severity.Critical,
                    title = $"Allocating OverlapSphere in {fileName}",
                    description = "Physics.OverlapSphere allocates a new array each call. Use OverlapSphereNonAlloc with a static buffer.",
                    file = path,
                    canAutoFix = false
                });
            }

            // Check for FindObjectsByType in Update/FixedUpdate/LateUpdate method bodies
            if (!path.Contains("/Editor/") && Regex.IsMatch(content, @"FindObjectsByType<"))
            {
                // Extract individual method bodies for Update-family methods and check each
                if (HasFindObjectsInUpdateMethod(content))
                {
                    issues.Add(new Issue
                    {
                        id = $"code_find_objects_{fileName}",
                        severity = Severity.High,
                        title = $"FindObjectsByType in hot path ({fileName})",
                        description = "FindObjectsByType in Update/FixedUpdate causes massive allocation. Cache results.",
                        file = path,
                        canAutoFix = false
                    });
                }
            }

            // Check for new WaitForSeconds inside while/for loop bodies
            if (Regex.IsMatch(content, @"yield\s+return\s+new\s+WaitForSeconds"))
            {
                if (HasUncachedWaitInLoop(content))
                {
                    issues.Add(new Issue
                    {
                        id = $"code_waitforseconds_{fileName}",
                        severity = Severity.Medium,
                        title = $"Uncached WaitForSeconds in loop ({fileName})",
                        description = "new WaitForSeconds inside a loop allocates each iteration. Cache it outside the loop.",
                        file = path,
                        canAutoFix = false
                    });
                }
            }
        }
    }

    /// <summary>
    /// Extract a brace-balanced method body starting from a given index and check
    /// whether it contains FindObjectsByType.
    /// </summary>
    private static bool HasFindObjectsInUpdateMethod(string content)
    {
        // Find each Update-family method declaration and extract its body
        var methodPattern = new Regex(@"\b(void\s+(?:Update|FixedUpdate|LateUpdate)\s*\([^)]*\))");
        foreach (Match m in methodPattern.Matches(content))
        {
            string body = ExtractBraceBlock(content, m.Index + m.Length);
            if (body != null && body.Contains("FindObjectsByType"))
                return true;
        }
        return false;
    }

    private static bool HasUncachedWaitInLoop(string content)
    {
        // Find while/for loop headers and extract their brace-balanced bodies
        var loopPattern = new Regex(@"\b(while|for)\s*\([^)]*\)");
        foreach (Match m in loopPattern.Matches(content))
        {
            string body = ExtractBraceBlock(content, m.Index + m.Length);
            if (body != null && Regex.IsMatch(body, @"yield\s+return\s+new\s+WaitForSeconds"))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Starting after a method/loop header, find the opening '{' and return
    /// everything up to the matching closing '}'.  Returns null on failure.
    /// </summary>
    private static string ExtractBraceBlock(string content, int startIndex)
    {
        int idx = content.IndexOf('{', startIndex);
        if (idx < 0) return null;

        int depth = 0;
        int blockStart = idx;
        for (int i = idx; i < content.Length; i++)
        {
            if (content[i] == '{') depth++;
            else if (content[i] == '}') depth--;

            if (depth == 0)
                return content.Substring(blockStart, i - blockStart + 1);
        }
        return null;
    }

    public static void ApplyFix(string fixId)
    {
        switch (fixId)
        {
            case "settings_shadow_distance":
                QualitySettings.shadowDistance = 40f;
                Debug.Log("[OptProfiler] Shadow distance set to 40m");
                break;
            case "settings_physics_timestep":
                Time.fixedDeltaTime = 0.02f;
                Debug.Log("[OptProfiler] Physics timestep set to 0.02s (50Hz)");
                break;
            case "settings_aa_high":
                QualitySettings.antiAliasing = 2;
                Debug.Log("[OptProfiler] Anti-aliasing set to 2x");
                break;
        }
    }
}
