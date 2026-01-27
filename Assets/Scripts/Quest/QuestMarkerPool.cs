using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDriver.Quest
{
    public class QuestMarkerPool : MonoBehaviour
    {
        public static QuestMarkerPool Instance { get; private set; }

        [SerializeField] private int poolSizePerPrefab = 10;
        [SerializeField] private Transform poolRoot;

        private readonly Dictionary<GameObject, Queue<GameObject>> pools = new Dictionary<GameObject, Queue<GameObject>>();
        private readonly Dictionary<GameObject, GameObject> instanceToPrefab = new Dictionary<GameObject, GameObject>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (poolRoot == null)
            {
                GameObject root = new GameObject("QuestMarkerPool");
                root.transform.SetParent(transform, false);
                poolRoot = root.transform;
            }
        }

        public void Prewarm(GameObject prefab, int count = -1)
        {
            if (prefab == null)
            {
                return;
            }

            if (count < 0)
            {
                count = poolSizePerPrefab;
            }

            Queue<GameObject> pool = GetOrCreatePool(prefab);
            int toCreate = Mathf.Max(0, count - pool.Count);

            for (int i = 0; i < toCreate; i++)
            {
                GameObject instance = CreateInstance(prefab);
                instance.SetActive(false);
                pool.Enqueue(instance);
                instanceToPrefab[instance] = prefab;
            }
        }

        public GameObject GetMarker(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                return null;
            }

            Queue<GameObject> pool = GetOrCreatePool(prefab);
            GameObject instance = pool.Count > 0 ? pool.Dequeue() : CreateInstance(prefab);

            if (instance == null)
            {
                return null;
            }

            instanceToPrefab[instance] = prefab;
            Transform instanceTransform = instance.transform;
            instanceTransform.SetParent(null, false);
            instanceTransform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);

            return instance;
        }

        public void ReturnMarker(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            if (!instanceToPrefab.TryGetValue(instance, out GameObject prefab) || prefab == null)
            {
                Destroy(instance);
                return;
            }

            Queue<GameObject> pool = GetOrCreatePool(prefab);
            instance.SetActive(false);
            instance.transform.SetParent(poolRoot, false);
            pool.Enqueue(instance);
        }

        private Queue<GameObject> GetOrCreatePool(GameObject prefab)
        {
            if (!pools.TryGetValue(prefab, out Queue<GameObject> pool))
            {
                pool = new Queue<GameObject>();
                pools[prefab] = pool;
            }

            return pool;
        }

        private GameObject CreateInstance(GameObject prefab)
        {
            GameObject instance = Instantiate(prefab, poolRoot);
            instance.SetActive(false);
            return instance;
        }
    }
}
