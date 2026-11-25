using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDriver.City
{
    // Scene-owned manifest: only these generated roots may be cleared by the editor.
    [AddComponentMenu("")]
    public sealed class ProceduralWorldBuildState : MonoBehaviour
    {
        public List<GameObject> generatedRoots = new List<GameObject>();
        public List<Terrain> terrains = new List<Terrain>();
        public List<TerrainData> originalData = new List<TerrainData>();
        public List<TerrainData> generatedData = new List<TerrainData>();
        public string assetFolder;
        public int seed;
    }
}
