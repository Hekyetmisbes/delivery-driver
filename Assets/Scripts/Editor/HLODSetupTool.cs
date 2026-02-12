using UnityEngine;
using UnityEditor;
using DeliveryDriver.Optimization;
using System.Collections.Generic;

namespace DeliveryDriver.Editor
{
    /// <summary>
    /// Editor tool for setting up HLOD (Hierarchical Level of Detail) system
    /// Tools > Performance > HLOD Setup
    /// </summary>
    public class HLODSetupTool : EditorWindow
    {
        private float switchDistance = 200f;
        private Vector2Int groupSize = new Vector2Int(4, 4);
        private float lod0Distance = 100f;
        private float lod1Distance = 200f;
        private float lod2Distance = 400f;
        private Material simplifiedMaterial;
        private bool autoGenerateProxies = true;

        private Vector2 scrollPosition;

        [MenuItem("Tools/Performance/HLOD Setup")]
        public static void ShowWindow()
        {
            GetWindow<HLODSetupTool>("HLOD Setup Tool");
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            GUILayout.Label("HLOD Setup Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "HLOD (Hierarchical Level of Detail) replaces distant objects with optimized proxy meshes.\n" +
                "Sprint 3: HLOD + Simulation Throttling",
                MessageType.Info
            );

            EditorGUILayout.Space();

            // Configuration
            GUILayout.Label("HLOD Configuration", EditorStyles.boldLabel);
            switchDistance = EditorGUILayout.FloatField("Switch Distance (m)", switchDistance);
            simplifiedMaterial = EditorGUILayout.ObjectField("Simplified Material", simplifiedMaterial, typeof(Material), false) as Material;
            autoGenerateProxies = EditorGUILayout.Toggle("Auto-Generate Proxies", autoGenerateProxies);

            EditorGUILayout.Space();

            // HLOD Group Settings
            GUILayout.Label("HLOD Group Settings", EditorStyles.boldLabel);
            groupSize = EditorGUILayout.Vector2IntField("Group Size (buildings)", groupSize);
            lod0Distance = EditorGUILayout.FloatField("LOD0 Distance (full)", lod0Distance);
            lod1Distance = EditorGUILayout.FloatField("LOD1 Distance (medium)", lod1Distance);
            lod2Distance = EditorGUILayout.FloatField("LOD2 Distance (proxy)", lod2Distance);

            EditorGUILayout.Space();

            // Quick Setup
            GUILayout.Label("Quick Setup", EditorStyles.boldLabel);

            if (GUILayout.Button("Add HLOD Proxy to Selected Objects", GUILayout.Height(30)))
            {
                AddHLODProxyToSelection();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Create HLOD Group from Selection", GUILayout.Height(30)))
            {
                CreateHLODGroupFromSelection();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Auto-Generate HLOD Groups (Grid-Based)", GUILayout.Height(30)))
            {
                AutoGenerateHLODGroups();
            }

            EditorGUILayout.Space();

            // Advanced Tools
            GUILayout.Label("Advanced Tools", EditorStyles.boldLabel);

            if (GUILayout.Button("Generate Proxy Meshes for All HLOD Groups"))
            {
                GenerateAllProxyMeshes();
            }

            if (GUILayout.Button("Optimize All HLOD Proxies"))
            {
                OptimizeAllProxies();
            }

            EditorGUILayout.Space();

            // Utilities
            GUILayout.Label("Utilities", EditorStyles.boldLabel);

            if (GUILayout.Button("Validate HLOD Setup"))
            {
                ValidateHLODSetup();
            }

            if (GUILayout.Button("Calculate Potential Savings"))
            {
                CalculatePotentialSavings();
            }

            EditorGUILayout.EndScrollView();
        }

        private void AddHLODProxyToSelection()
        {
            if (Selection.gameObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("No Selection",
                    "Please select one or more objects to add HLOD proxy to.",
                    "OK");
                return;
            }

            int added = 0;
            foreach (GameObject obj in Selection.gameObjects)
            {
                HLODProxy proxy = obj.GetComponent<HLODProxy>();
                if (proxy == null)
                {
                    proxy = obj.AddComponent<HLODProxy>();
                    proxy.switchDistance = switchDistance;
                    proxy.simplifiedMaterial = simplifiedMaterial;
                    proxy.AddAllChildrenAsSource();

                    if (autoGenerateProxies)
                    {
                        proxy.GenerateProxyMesh(true);
                    }

                    Undo.RegisterCreatedObjectUndo(proxy, "Add HLOD Proxy");
                    added++;
                }
            }

            Debug.Log($"[HLODSetupTool] Added HLOD Proxy to {added} objects");
            EditorUtility.DisplayDialog("Success",
                $"Added HLOD Proxy to {added} objects.\n\n" +
                $"Switch Distance: {switchDistance}m",
                "OK");
        }

        private void CreateHLODGroupFromSelection()
        {
            if (Selection.gameObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("No Selection",
                    "Please select objects to group into an HLOD group.",
                    "OK");
                return;
            }

            // Calculate center of selection
            Vector3 center = Vector3.zero;
            foreach (GameObject obj in Selection.gameObjects)
            {
                center += obj.transform.position;
            }
            center /= Selection.gameObjects.Length;

            // Create group object
            GameObject groupObj = new GameObject("HLOD_Group");
            groupObj.transform.position = center;

            HLODGroup group = groupObj.AddComponent<HLODGroup>();
            group.groupSize = groupSize;
            group.lod0Distance = lod0Distance;
            group.lod1Distance = lod1Distance;
            group.lod2Distance = lod2Distance;
            group.atlasMaterial = simplifiedMaterial;

            // Add selected objects to group
            foreach (GameObject obj in Selection.gameObjects)
            {
                group.groupMembers.Add(obj);
            }

            // Generate proxy if enabled
            if (autoGenerateProxies)
            {
                group.GenerateGroupProxy();
            }

            Undo.RegisterCreatedObjectUndo(groupObj, "Create HLOD Group");
            Selection.activeGameObject = groupObj;

            Debug.Log($"[HLODSetupTool] Created HLOD Group with {Selection.gameObjects.Length} members");
            EditorUtility.DisplayDialog("Success",
                $"Created HLOD Group with {Selection.gameObjects.Length} members.\n\n" +
                $"LOD Distances: {lod0Distance}m / {lod1Distance}m / {lod2Distance}m",
                "OK");
        }

        private void AutoGenerateHLODGroups()
        {
            MeshRenderer[] allRenderers = FindObjectsOfType<MeshRenderer>();
            if (allRenderers.Length == 0)
            {
                EditorUtility.DisplayDialog("No Objects",
                    "No mesh renderers found in scene.",
                    "OK");
                return;
            }

            // Calculate scene bounds
            Bounds sceneBounds = new Bounds(allRenderers[0].transform.position, Vector3.zero);
            foreach (var renderer in allRenderers)
            {
                sceneBounds.Encapsulate(renderer.bounds);
            }

            // Calculate grid dimensions
            float groupSizeMeters = groupSize.x * 25f; // Assuming ~25m per building
            int gridX = Mathf.CeilToInt(sceneBounds.size.x / groupSizeMeters);
            int gridZ = Mathf.CeilToInt(sceneBounds.size.z / groupSizeMeters);

            GameObject parentObj = new GameObject("HLOD_Groups");
            int groupsCreated = 0;

            // Create grid of HLOD groups
            for (int x = 0; x < gridX; x++)
            {
                for (int z = 0; z < gridZ; z++)
                {
                    Vector3 groupCenter = sceneBounds.min + new Vector3(
                        (x + 0.5f) * groupSizeMeters,
                        0,
                        (z + 0.5f) * groupSizeMeters
                    );

                    Bounds groupBounds = new Bounds(groupCenter, new Vector3(groupSizeMeters, 1000, groupSizeMeters));

                    // Find objects in this group
                    List<GameObject> groupMembers = new List<GameObject>();
                    foreach (var renderer in allRenderers)
                    {
                        if (groupBounds.Contains(renderer.transform.position))
                        {
                            GameObject root = renderer.transform.root.gameObject;
                            if (!groupMembers.Contains(root))
                            {
                                groupMembers.Add(root);
                            }
                        }
                    }

                    // Create group if it has members
                    if (groupMembers.Count > 0)
                    {
                        GameObject groupObj = new GameObject($"HLOD_Group_{x}_{z}");
                        groupObj.transform.position = groupCenter;
                        groupObj.transform.parent = parentObj.transform;

                        HLODGroup group = groupObj.AddComponent<HLODGroup>();
                        group.groupMembers = groupMembers;
                        group.lod0Distance = lod0Distance;
                        group.lod1Distance = lod1Distance;
                        group.lod2Distance = lod2Distance;
                        group.atlasMaterial = simplifiedMaterial;

                        if (autoGenerateProxies)
                        {
                            group.GenerateGroupProxy();
                        }

                        groupsCreated++;
                    }
                }
            }

            Debug.Log($"[HLODSetupTool] Auto-generated {groupsCreated} HLOD groups");
            EditorUtility.DisplayDialog("Success",
                $"Auto-generated {groupsCreated} HLOD groups.\n\n" +
                $"Grid: {gridX}x{gridZ}\n" +
                $"Group Size: {groupSizeMeters}m",
                "OK");
        }

        private void GenerateAllProxyMeshes()
        {
            HLODProxy[] proxies = FindObjectsOfType<HLODProxy>();
            HLODGroup[] groups = FindObjectsOfType<HLODGroup>();

            int generated = 0;

            foreach (var proxy in proxies)
            {
                proxy.GenerateProxyMesh(true);
                generated++;
            }

            foreach (var group in groups)
            {
                group.GenerateGroupProxy();
                generated++;
            }

            Debug.Log($"[HLODSetupTool] Generated {generated} proxy meshes");
            EditorUtility.DisplayDialog("Success",
                $"Generated {generated} proxy meshes.\n\n" +
                $"Check console for details.",
                "OK");
        }

        private void OptimizeAllProxies()
        {
            HLODProxy[] proxies = FindObjectsOfType<HLODProxy>();

            int optimized = 0;
            foreach (var proxy in proxies)
            {
                MeshFilter mf = proxy.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    mf.sharedMesh.Optimize();
                    optimized++;
                }
            }

            Debug.Log($"[HLODSetupTool] Optimized {optimized} proxy meshes");
            EditorUtility.DisplayDialog("Success",
                $"Optimized {optimized} proxy meshes.",
                "OK");
        }

        private void ValidateHLODSetup()
        {
            HLODProxy[] proxies = FindObjectsOfType<HLODProxy>();
            HLODGroup[] groups = FindObjectsOfType<HLODGroup>();

            string report = "=== HLOD Setup Validation ===\n\n";

            report += $"HLOD Proxies: {proxies.Length}\n";
            report += $"HLOD Groups: {groups.Length}\n\n";

            int proxiesWithMesh = 0;
            int proxiesWithMaterial = 0;

            foreach (var proxy in proxies)
            {
                MeshFilter mf = proxy.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) proxiesWithMesh++;
                if (proxy.simplifiedMaterial != null) proxiesWithMaterial++;
            }

            report += $"Proxies with Mesh: {proxiesWithMesh}/{proxies.Length}\n";
            report += $"Proxies with Material: {proxiesWithMaterial}/{proxies.Length}\n\n";

            report += "=== Recommendations ===\n";
            if (proxies.Length == 0 && groups.Length == 0)
                report += "• No HLOD setup found. Use the tools to create HLOD groups.\n";
            if (proxiesWithMesh < proxies.Length)
                report += "• Some proxies don't have meshes. Use 'Generate Proxy Meshes'.\n";
            if (proxiesWithMaterial < proxies.Length)
                report += "• Some proxies don't have materials. Assign simplified materials.\n";

            Debug.Log(report);
            EditorUtility.DisplayDialog("Validation Complete", report, "OK");
        }

        private void CalculatePotentialSavings()
        {
            HLODProxy[] proxies = FindObjectsOfType<HLODProxy>();
            HLODGroup[] groups = FindObjectsOfType<HLODGroup>();

            int totalSourceObjects = 0;
            int totalSourceVertices = 0;
            int totalProxyVertices = 0;

            foreach (var proxy in proxies)
            {
                totalSourceObjects += proxy.sourceObjects.Count;

                foreach (var source in proxy.sourceObjects)
                {
                    if (source != null)
                    {
                        MeshFilter[] mfs = source.GetComponentsInChildren<MeshFilter>();
                        foreach (var mf in mfs)
                        {
                            if (mf.sharedMesh != null)
                            {
                                totalSourceVertices += mf.sharedMesh.vertexCount;
                            }
                        }
                    }
                }

                MeshFilter proxyMF = proxy.GetComponent<MeshFilter>();
                if (proxyMF != null && proxyMF.sharedMesh != null)
                {
                    totalProxyVertices += proxyMF.sharedMesh.vertexCount;
                }
            }

            string report = "=== HLOD Potential Savings ===\n\n";
            report += $"Total HLOD Proxies: {proxies.Length}\n";
            report += $"Source Objects: {totalSourceObjects}\n";
            report += $"Source Vertices: {totalSourceVertices:N0}\n";
            report += $"Proxy Vertices: {totalProxyVertices:N0}\n\n";

            if (totalSourceVertices > 0)
            {
                float reduction = ((float)(totalSourceVertices - totalProxyVertices) / totalSourceVertices) * 100f;
                report += $"Vertex Reduction: {reduction:F1}%\n";
                report += $"Draw Call Reduction: ~{totalSourceObjects - proxies.Length} fewer calls\n";
            }

            Debug.Log(report);
            EditorUtility.DisplayDialog("Potential Savings", report, "OK");
        }
    }
}
