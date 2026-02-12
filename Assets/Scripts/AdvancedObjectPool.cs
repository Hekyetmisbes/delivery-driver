using UnityEngine;
using System.Collections.Generic;

namespace DeliveryDriver.Optimization
{
    /// <summary>
    /// Advanced object pooling system for props, VFX, projectiles, and other frequently spawned objects
    /// Eliminates runtime instantiate/destroy spikes
    /// Sprint 3: Expanded pooling system
    /// </summary>
    public class AdvancedObjectPool : MonoBehaviour
    {
        [System.Serializable]
        public class PoolConfig
        {
            public string poolName;
            public GameObject prefab;
            public int initialSize = 10;
            public int maxSize = 100;
            public bool allowGrowth = true;
            public bool warmupOnStart = true;
        }

        [Header("Pool Configurations")]
        public List<PoolConfig> poolConfigs = new List<PoolConfig>();

        [Header("Performance Settings")]
        [Tooltip("Maximum objects to instantiate per frame during warmup")]
        public int warmupBudgetPerFrame = 5;

        [Tooltip("Auto-shrink pools that are underutilized")]
        public bool autoShrink = true;

        [Tooltip("Check for shrink every N seconds")]
        public float shrinkCheckInterval = 30f;

        [Header("Debug")]
        public bool showDebugInfo = false;

        // Internal pool storage
        private Dictionary<string, Queue<GameObject>> pools = new Dictionary<string, Queue<GameObject>>();
        private Dictionary<string, List<GameObject>> activeObjects = new Dictionary<string, List<GameObject>>();
        private Dictionary<string, PoolConfig> configLookup = new Dictionary<string, PoolConfig>();
        private Dictionary<string, Transform> poolParents = new Dictionary<string, Transform>();

        // Statistics
        private Dictionary<string, int> spawnCounts = new Dictionary<string, int>();
        private Dictionary<string, int> peakActiveCounts = new Dictionary<string, int>();

        private float lastShrinkCheck;
        private static AdvancedObjectPool instance;

        public static AdvancedObjectPool Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<AdvancedObjectPool>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("AdvancedObjectPool");
                        instance = go.AddComponent<AdvancedObjectPool>();
                    }
                }
                return instance;
            }
        }

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }

            InitializePools();
        }

        private void InitializePools()
        {
            foreach (var config in poolConfigs)
            {
                if (config.prefab == null)
                {
                    Debug.LogWarning($"[AdvancedObjectPool] Pool '{config.poolName}' has no prefab assigned");
                    continue;
                }

                CreatePool(config);
            }
        }

        private void CreatePool(PoolConfig config)
        {
            if (pools.ContainsKey(config.poolName))
            {
                Debug.LogWarning($"[AdvancedObjectPool] Pool '{config.poolName}' already exists");
                return;
            }

            // Create pool structures
            pools[config.poolName] = new Queue<GameObject>();
            activeObjects[config.poolName] = new List<GameObject>();
            configLookup[config.poolName] = config;
            spawnCounts[config.poolName] = 0;
            peakActiveCounts[config.poolName] = 0;

            // Create parent container
            GameObject parentObj = new GameObject($"Pool_{config.poolName}");
            parentObj.transform.parent = transform;
            poolParents[config.poolName] = parentObj.transform;

            // Warmup
            if (config.warmupOnStart)
            {
                StartCoroutine(WarmupPool(config));
            }

            Debug.Log($"[AdvancedObjectPool] Created pool '{config.poolName}' with initial size {config.initialSize}");
        }

        private System.Collections.IEnumerator WarmupPool(PoolConfig config)
        {
            int created = 0;
            int budgetThisFrame = 0;

            while (created < config.initialSize)
            {
                GameObject obj = Instantiate(config.prefab);
                obj.name = $"{config.poolName}_{created}";
                obj.transform.parent = poolParents[config.poolName];
                obj.SetActive(false);
                pools[config.poolName].Enqueue(obj);

                created++;
                budgetThisFrame++;

                // Spread creation across multiple frames
                if (budgetThisFrame >= warmupBudgetPerFrame)
                {
                    budgetThisFrame = 0;
                    yield return null;
                }
            }

            Debug.Log($"[AdvancedObjectPool] Warmup completed for '{config.poolName}': {created} objects");
        }

        /// <summary>
        /// Spawn an object from the pool
        /// </summary>
        public GameObject Spawn(string poolName, Vector3 position, Quaternion rotation)
        {
            if (!pools.ContainsKey(poolName))
            {
                Debug.LogError($"[AdvancedObjectPool] Pool '{poolName}' does not exist");
                return null;
            }

            GameObject obj = null;
            PoolConfig config = configLookup[poolName];

            // Get from pool or create new
            if (pools[poolName].Count > 0)
            {
                obj = pools[poolName].Dequeue();
            }
            else if (config.allowGrowth)
            {
                // Check max size
                int totalCount = activeObjects[poolName].Count + pools[poolName].Count;
                if (totalCount >= config.maxSize)
                {
                    Debug.LogWarning($"[AdvancedObjectPool] Pool '{poolName}' at max size ({config.maxSize})");
                    return null;
                }

                // Create new object
                obj = Instantiate(config.prefab);
                obj.name = $"{poolName}_Dynamic_{spawnCounts[poolName]}";
                obj.transform.parent = poolParents[poolName];
            }
            else
            {
                Debug.LogWarning($"[AdvancedObjectPool] Pool '{poolName}' exhausted and growth disabled");
                return null;
            }

            // Setup object
            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.SetActive(true);

            // Track
            activeObjects[poolName].Add(obj);
            spawnCounts[poolName]++;

            // Update peak
            if (activeObjects[poolName].Count > peakActiveCounts[poolName])
            {
                peakActiveCounts[poolName] = activeObjects[poolName].Count;
            }

            return obj;
        }

        /// <summary>
        /// Return an object to the pool
        /// </summary>
        public void Despawn(string poolName, GameObject obj)
        {
            if (!pools.ContainsKey(poolName))
            {
                Debug.LogError($"[AdvancedObjectPool] Pool '{poolName}' does not exist");
                return;
            }

            if (obj == null) return;

            // Remove from active
            activeObjects[poolName].Remove(obj);

            // Reset and return to pool
            obj.SetActive(false);
            obj.transform.parent = poolParents[poolName];
            pools[poolName].Enqueue(obj);
        }

        /// <summary>
        /// Despawn all active objects in a pool
        /// </summary>
        public void DespawnAll(string poolName)
        {
            if (!activeObjects.ContainsKey(poolName)) return;

            List<GameObject> toReturn = new List<GameObject>(activeObjects[poolName]);
            foreach (var obj in toReturn)
            {
                Despawn(poolName, obj);
            }
        }

        /// <summary>
        /// Get pool statistics
        /// </summary>
        public void GetPoolStats(string poolName, out int available, out int active, out int total, out int peak)
        {
            available = pools.ContainsKey(poolName) ? pools[poolName].Count : 0;
            active = activeObjects.ContainsKey(poolName) ? activeObjects[poolName].Count : 0;
            total = available + active;
            peak = peakActiveCounts.ContainsKey(poolName) ? peakActiveCounts[poolName] : 0;
        }

        private void Update()
        {
            // Auto-shrink check
            if (autoShrink && Time.time - lastShrinkCheck >= shrinkCheckInterval)
            {
                lastShrinkCheck = Time.time;
                CheckForShrink();
            }
        }

        private void CheckForShrink()
        {
            foreach (var poolName in pools.Keys)
            {
                PoolConfig config = configLookup[poolName];
                int available = pools[poolName].Count;
                int active = activeObjects[poolName].Count;

                // Shrink if we have more than 2x initial size available and less than 50% utilization
                if (available > config.initialSize * 2 && active < config.initialSize / 2)
                {
                    int toRemove = available - config.initialSize;
                    ShrinkPool(poolName, toRemove);
                }
            }
        }

        private void ShrinkPool(string poolName, int count)
        {
            if (!pools.ContainsKey(poolName)) return;

            int removed = 0;
            while (removed < count && pools[poolName].Count > 0)
            {
                GameObject obj = pools[poolName].Dequeue();
                if (obj != null)
                {
                    Destroy(obj);
                    removed++;
                }
            }

            if (removed > 0)
            {
                Debug.Log($"[AdvancedObjectPool] Shrunk pool '{poolName}' by {removed} objects");
            }
        }

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, 360, 350, 300));
            GUILayout.BeginVertical("box");

            GUILayout.Label($"<b>Object Pool Stats</b>", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label($"Total Pools: {pools.Count}");
            GUILayout.Space(5);

            foreach (var poolName in pools.Keys)
            {
                GetPoolStats(poolName, out int available, out int active, out int total, out int peak);
                GUILayout.Label($"{poolName}: {active}/{total} (peak: {peak})");
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

#if UNITY_EDITOR
        [ContextMenu("Reset All Pools")]
        private void ResetAllPools()
        {
            foreach (var poolName in pools.Keys)
            {
                DespawnAll(poolName);
            }
            Debug.Log("[AdvancedObjectPool] All pools reset");
        }
#endif
    }
}
