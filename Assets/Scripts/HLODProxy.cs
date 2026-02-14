using UnityEngine;
using System.Collections.Generic;

namespace DeliveryDriver.Optimization
{
    /// <summary>
    /// HLOD (Hierarchical Level of Detail) Proxy component
    /// Replaces multiple distant objects with a single merged mesh for performance
    /// Sprint 3: HLOD System
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class HLODProxy : MonoBehaviour
    {
        [Header("HLOD Configuration")]
        [Tooltip("Original objects that this proxy replaces")]
        public List<GameObject> sourceObjects = new List<GameObject>();

        [Tooltip("Distance at which to switch to HLOD proxy")]
        public float switchDistance = 200f;

        [Tooltip("Automatically disable source objects when proxy is active")]
        public bool autoManageSourceObjects = true;

        [Header("Optimization Settings")]
        [Tooltip("Disable colliders on proxy (recommended for distant objects)")]
        public bool disableColliders = true;

        [Tooltip("Use simplified material (single material for all meshes)")]
        public bool useSimplifiedMaterial = true;

        [Tooltip("Simplified material to use when active")]
        public Material simplifiedMaterial;

        [Header("Runtime State")]
        public bool isProxyActive = false;
        public Transform viewerTransform;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Collider[] proxyColliders;
        private float distanceToViewer;

        // Cache for source object renderers
        private List<Renderer> sourceRenderers = new List<Renderer>();

        private void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            proxyColliders = GetComponents<Collider>();

            // Cache source renderers
            CacheSourceRenderers();

            // Start with proxy disabled
            SetProxyActive(false);
        }

        private void Start()
        {
            // Auto-find viewer if not set
            if (viewerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    viewerTransform = player.transform;
                }
            }
        }

        private void CacheSourceRenderers()
        {
            sourceRenderers.Clear();
            foreach (var obj in sourceObjects)
            {
                if (obj != null)
                {
                    sourceRenderers.AddRange(obj.GetComponentsInChildren<Renderer>());
                }
            }
        }

        private void Update()
        {
            if (viewerTransform == null) return;

            // Calculate distance to viewer
            distanceToViewer = Vector3.Distance(transform.position, viewerTransform.position);

            // Determine if proxy should be active
            bool shouldBeActive = distanceToViewer >= switchDistance;

            if (shouldBeActive != isProxyActive)
            {
                SetProxyActive(shouldBeActive);
            }
        }

        /// <summary>
        /// Activate or deactivate the HLOD proxy
        /// </summary>
        public void SetProxyActive(bool active)
        {
            isProxyActive = active;

            // Toggle proxy renderer
            if (meshRenderer != null)
            {
                meshRenderer.enabled = active;
            }

            // Manage source objects
            if (autoManageSourceObjects)
            {
                foreach (var renderer in sourceRenderers)
                {
                    if (renderer != null)
                    {
                        renderer.enabled = !active;
                    }
                }
            }

            // Manage colliders
            if (disableColliders && active)
            {
                foreach (var collider in proxyColliders)
                {
                    if (collider != null)
                    {
                        collider.enabled = false;
                    }
                }
            }

            // Apply simplified material
            if (useSimplifiedMaterial && active && simplifiedMaterial != null && meshRenderer != null)
            {
                meshRenderer.sharedMaterial = simplifiedMaterial;
            }
        }

        /// <summary>
        /// Generate HLOD proxy mesh from source objects
        /// </summary>
        public void GenerateProxyMesh(bool combineSubmeshes = true)
        {
            if (sourceObjects.Count == 0)
            {
                Debug.LogWarning("[HLODProxy] No source objects to generate proxy from");
                return;
            }

            List<CombineInstance> combineInstances = new List<CombineInstance>();
            List<Material> materials = new List<Material>();

            foreach (var sourceObj in sourceObjects)
            {
                if (sourceObj == null) continue;

                MeshFilter[] meshFilters = sourceObj.GetComponentsInChildren<MeshFilter>();
                foreach (var mf in meshFilters)
                {
                    if (mf.sharedMesh == null) continue;

                    CombineInstance ci = new CombineInstance();
                    ci.mesh = mf.sharedMesh;
                    ci.transform = mf.transform.localToWorldMatrix;
                    combineInstances.Add(ci);

                    // Collect materials
                    MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                    if (mr != null && mr.sharedMaterial != null)
                    {
                        if (!materials.Contains(mr.sharedMaterial))
                        {
                            materials.Add(mr.sharedMaterial);
                        }
                    }
                }
            }

            if (combineInstances.Count == 0)
            {
                Debug.LogWarning("[HLODProxy] No valid meshes found in source objects");
                return;
            }

            // Create combined mesh
            Mesh combinedMesh = new Mesh();
            combinedMesh.name = "HLOD_Proxy_" + gameObject.name;
            combinedMesh.CombineMeshes(combineInstances.ToArray(), combineSubmeshes, true);

            // Optimize mesh
            combinedMesh.RecalculateBounds();
            combinedMesh.RecalculateNormals();
            combinedMesh.Optimize();

            // Assign to mesh filter
            if (meshFilter != null)
            {
                meshFilter.sharedMesh = combinedMesh;
            }

            Debug.Log($"[HLODProxy] Generated proxy mesh: {combineInstances.Count} meshes combined, {combinedMesh.vertexCount} vertices");
        }

        /// <summary>
        /// Get current distance to viewer
        /// </summary>
        public float GetDistanceToViewer()
        {
            return distanceToViewer;
        }

        private void OnDrawGizmosSelected()
        {
            // Draw switch distance sphere
            Gizmos.color = isProxyActive ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, switchDistance);

            // Draw line to viewer if set
            if (viewerTransform != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, viewerTransform.position);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Generate Proxy Mesh")]
        private void GenerateProxyMeshContext()
        {
            GenerateProxyMesh(true);
            UnityEditor.EditorUtility.SetDirty(gameObject);
        }

        [ContextMenu("Add All Children as Source Objects")]
        public void AddAllChildrenAsSource()
        {
            sourceObjects.Clear();
            foreach (Transform child in transform)
            {
                sourceObjects.Add(child.gameObject);
            }
            Debug.Log($"[HLODProxy] Added {sourceObjects.Count} children as source objects");
        }
#endif
    }
}
