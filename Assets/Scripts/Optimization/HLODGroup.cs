using UnityEngine;
using System.Collections.Generic;

namespace DeliveryDriver.Optimization
{
    /// <summary>
    /// HLOD Group for managing multiple building blocks as a single proxy
    /// Ideal for city blocks, building clusters, or neighborhood groups
    /// Sprint 3: HLOD Building Block System
    /// </summary>
    public class HLODGroup : MonoBehaviour
    {
        [Header("Group Configuration")]
        [Tooltip("Buildings/objects in this HLOD group")]
        public List<GameObject> groupMembers = new List<GameObject>();

        [Tooltip("Grid-based grouping (e.g., 4x4 building block)")]
        public Vector2Int groupSize = new Vector2Int(4, 4);

        [Header("LOD Distances")]
        [Tooltip("Distance for LOD0 (full detail)")]
        public float lod0Distance = 100f;

        [Tooltip("Distance for LOD1 (medium detail)")]
        public float lod1Distance = 200f;

        [Tooltip("Distance for LOD2 (proxy/merged)")]
        public float lod2Distance = 400f;

        [Header("Proxy Settings")]
        [Tooltip("Auto-generated HLOD proxy")]
        public GameObject proxyObject;

        [Tooltip("Reduce polygon count for proxy")]
        [Range(0.1f, 1.0f)]
        public float proxyQuality = 0.5f;

        [Tooltip("Use texture atlas for proxy")]
        public bool useTextureAtlas = true;

        [Tooltip("Atlas material")]
        public Material atlasMaterial;

        [Header("Runtime")]
        public Transform viewerTransform;
        public int currentLOD = 0;

        private float currentDistance;
        private List<Renderer> lod0Renderers = new List<Renderer>();
        private List<Collider> lod0Colliders = new List<Collider>();
        private HLODProxy proxyComponent;

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            // Auto-find viewer
            if (viewerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    viewerTransform = player.transform;
                }
            }

            // Cache components
            CacheGroupComponents();

            // Setup proxy if exists
            if (proxyObject != null)
            {
                proxyComponent = proxyObject.GetComponent<HLODProxy>();
                if (proxyComponent == null)
                {
                    proxyComponent = proxyObject.AddComponent<HLODProxy>();
                    proxyComponent.sourceObjects = new List<GameObject>(groupMembers);
                    proxyComponent.switchDistance = lod2Distance;
                }
            }
        }

        private void CacheGroupComponents()
        {
            lod0Renderers.Clear();
            lod0Colliders.Clear();

            foreach (var member in groupMembers)
            {
                if (member == null) continue;

                lod0Renderers.AddRange(member.GetComponentsInChildren<Renderer>());
                lod0Colliders.AddRange(member.GetComponentsInChildren<Collider>());
            }
        }

        private void Update()
        {
            if (viewerTransform == null) return;

            // Calculate distance
            currentDistance = Vector3.Distance(transform.position, viewerTransform.position);

            // Determine appropriate LOD
            int targetLOD = DetermineLOD(currentDistance);

            if (targetLOD != currentLOD)
            {
                SwitchToLOD(targetLOD);
            }
        }

        private int DetermineLOD(float distance)
        {
            if (distance < lod0Distance)
                return 0; // Full detail
            else if (distance < lod1Distance)
                return 1; // Medium detail
            else
                return 2; // Proxy
        }

        private void SwitchToLOD(int lodLevel)
        {
            currentLOD = lodLevel;

            switch (lodLevel)
            {
                case 0: // Full detail
                    SetFullDetail(true);
                    SetProxy(false);
                    break;

                case 1: // Medium detail
                    SetFullDetail(true);
                    SetProxy(false);
                    ReduceDetailLevel();
                    break;

                case 2: // Proxy
                    SetFullDetail(false);
                    SetProxy(true);
                    break;
            }
        }

        private void SetFullDetail(bool enabled)
        {
            foreach (var renderer in lod0Renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = enabled;
                }
            }

            // Manage colliders at distance
            if (!enabled)
            {
                foreach (var collider in lod0Colliders)
                {
                    if (collider != null && !(collider is MeshCollider))
                    {
                        collider.enabled = false;
                    }
                }
            }
        }

        private void SetProxy(bool enabled)
        {
            if (proxyObject != null)
            {
                proxyObject.SetActive(enabled);
            }

            if (proxyComponent != null)
            {
                proxyComponent.SetProxyActive(enabled);
            }
        }

        private void ReduceDetailLevel()
        {
            // LOD1: Disable small colliders, reduce shadow quality, etc.
            foreach (var collider in lod0Colliders)
            {
                if (collider != null && collider.bounds.size.magnitude < 2f)
                {
                    collider.enabled = false;
                }
            }
        }

        /// <summary>
        /// Auto-collect group members within bounds
        /// </summary>
        public void AutoCollectMembers(Bounds bounds)
        {
            groupMembers.Clear();
            MeshRenderer[] allRenderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);

            foreach (var renderer in allRenderers)
            {
                if (bounds.Contains(renderer.transform.position))
                {
                    GameObject root = renderer.transform.root.gameObject;
                    if (!groupMembers.Contains(root) && root != gameObject)
                    {
                        groupMembers.Add(root);
                    }
                }
            }

            Debug.Log($"[HLODGroup] Auto-collected {groupMembers.Count} members");
        }

        /// <summary>
        /// Generate optimized proxy for this group
        /// </summary>
        public GameObject GenerateGroupProxy()
        {
            if (groupMembers.Count == 0)
            {
                Debug.LogWarning("[HLODGroup] No group members to generate proxy from");
                return null;
            }

            // Create proxy object
            GameObject proxy = new GameObject($"HLOD_Proxy_{gameObject.name}");
            proxy.transform.position = transform.position;
            proxy.transform.parent = transform;

            // Add HLOD proxy component
            HLODProxy hlodProxy = proxy.AddComponent<HLODProxy>();
            hlodProxy.sourceObjects = new List<GameObject>(groupMembers);
            hlodProxy.switchDistance = lod2Distance;
            hlodProxy.simplifiedMaterial = atlasMaterial;
            hlodProxy.useSimplifiedMaterial = atlasMaterial != null;

            // Generate mesh
            hlodProxy.GenerateProxyMesh(true);

            proxyObject = proxy;
            proxyComponent = hlodProxy;

            Debug.Log($"[HLODGroup] Generated proxy for {groupMembers.Count} members");
            return proxy;
        }

        private void OnDrawGizmosSelected()
        {
            // Draw LOD distances
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, lod0Distance);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, lod1Distance);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, lod2Distance);

            // Draw group bounds
            if (groupMembers.Count > 0)
            {
                Bounds groupBounds = CalculateGroupBounds();
                Gizmos.color = new Color(0, 1, 1, 0.3f);
                Gizmos.DrawWireCube(groupBounds.center, groupBounds.size);
            }
        }

        private Bounds CalculateGroupBounds()
        {
            if (groupMembers.Count == 0)
                return new Bounds(transform.position, Vector3.one);

            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            foreach (var member in groupMembers)
            {
                if (member == null) continue;

                Renderer[] renderers = member.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return bounds;
        }

#if UNITY_EDITOR
        [ContextMenu("Generate Proxy")]
        private void GenerateProxyContext()
        {
            GenerateGroupProxy();
            UnityEditor.EditorUtility.SetDirty(gameObject);
        }

        [ContextMenu("Auto-Collect Members (100m radius)")]
        private void AutoCollectMembersContext()
        {
            Bounds bounds = new Bounds(transform.position, new Vector3(100, 100, 100));
            AutoCollectMembers(bounds);
            UnityEditor.EditorUtility.SetDirty(gameObject);
        }
#endif
    }
}
