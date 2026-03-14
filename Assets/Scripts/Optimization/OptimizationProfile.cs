using UnityEngine;

namespace DeliveryDriver.Optimization
{
    [CreateAssetMenu(menuName = "Delivery Driver/Optimization Profile")]
    public class OptimizationProfile : ScriptableObject
    {
        [Header("Traffic Distances")]
        public float nearDistance = 50f;
        public float midDistance = 150f;
        public float farDistance = 300f;
        public float veryFarDistance = 500f;
        public float stateUpdateInterval = 0.12f;

        [Header("Spatial Grid")]
        public float cellSize = 50f;
        public float gridUpdateInterval = 0.1f;

        [Header("World Streaming")]
        public float chunkSize = 64f;
        public float nearRing = 90f;
        public float midRing = 150f;
        public float farRing = 170f;
        public float chunkUpdateInterval = 0.35f;
        public int maxChunkUpdatesPerFrame = 12;
        public float cacheRefreshInterval = 120f;

        [Header("Rendering")]
        public float shadowDistance = 40f;
        public int shadowCascades = 2;
        public bool streamingMipmaps = true;
        public int antiAliasing = 2;

        [Header("Layer Culling")]
        public LayerCullingEntry[] layerCullingDistances = new LayerCullingEntry[]
        {
            new LayerCullingEntry { layerName = "Water", distance = 500f },
            new LayerCullingEntry { layerName = "MiniMapBuilding", distance = 0f },
            new LayerCullingEntry { layerName = "MiniMapMarker", distance = 0f },
        };

        [Header("Physics")]
        [Range(0.01f, 0.04f)] public float fixedTimestep = 0.02f;
        public int solverIterations = 6;
        public int solverVelocityIterations = 1;

        [System.Serializable]
        public class LayerCullingEntry
        {
            public string layerName;
            public float distance;
        }
    }
}
