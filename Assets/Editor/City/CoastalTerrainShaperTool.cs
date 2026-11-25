using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeliveryDriver.EditorTools
{
    public class CoastalTerrainShaperTool : EditorWindow
    {
        private enum TerrainTargetMode
        {
            SelectedTerrains,
            ActiveTerrains
        }

        [SerializeField] private TerrainTargetMode targetMode = TerrainTargetMode.SelectedTerrains;
        [SerializeField] private Transform seaReference;
        [SerializeField] private Vector3 manualSeaOrigin = Vector3.zero;
        [SerializeField] private Vector2 mountainDirection = Vector2.up;

        [SerializeField] private float seaLevel = 0f;
        [SerializeField] private float underwaterDepth = 2.5f;
        [SerializeField] private float shorelineWidth = 55f;
        [SerializeField] private float beachHeight = 1.6f;
        [SerializeField] private float coastWidth = 240f;
        [SerializeField] private float cityPlateauHeight = 7.5f;
        [SerializeField] private float cityPlateauWidth = 520f;
        [SerializeField] private float mountainRampWidth = 520f;
        [SerializeField] private float mountainHeight = 95f;

        [SerializeField] private float cityNoiseAmplitude = 1.2f;
        [SerializeField] private float mountainNoiseAmplitude = 22f;
        [SerializeField] private float noiseScale = 0.0025f;
        [SerializeField] private int noiseOctaves = 4;
        [SerializeField] private int noiseSeed = 4812;
        [SerializeField] private int smoothPasses = 1;
        [SerializeField] private float blendStrength = 1f;

        [MenuItem("Tools/Delivery Driver/City/Coastal Terrain Shaper")]
        public static void Open()
        {
            CoastalTerrainShaperTool window = GetWindow<CoastalTerrainShaperTool>("Coastal Terrain");
            window.minSize = new Vector2(420f, 500f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Coastal Terrain Shaper", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Selected terrain'leri denizden daglara dogru alcak sahil, duz sehir platosu ve noise'lu dag rampasi olarak sekillendirir. Apply islemi Undo desteklidir.", MessageType.Info);

            targetMode = (TerrainTargetMode)EditorGUILayout.EnumPopup("Target Terrains", targetMode);
            seaReference = (Transform)EditorGUILayout.ObjectField("Sea Reference", seaReference, typeof(Transform), true);
            using (new EditorGUI.DisabledScope(seaReference != null))
            {
                manualSeaOrigin = EditorGUILayout.Vector3Field("Manual Sea Origin", manualSeaOrigin);
            }

            mountainDirection = EditorGUILayout.Vector2Field("Mountain Direction XZ", mountainDirection);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Height Bands", EditorStyles.boldLabel);
            seaLevel = EditorGUILayout.FloatField("Sea Level", seaLevel);
            underwaterDepth = Mathf.Max(0f, EditorGUILayout.FloatField("Underwater Depth", underwaterDepth));
            shorelineWidth = Mathf.Max(1f, EditorGUILayout.FloatField("Shoreline Width", shorelineWidth));
            beachHeight = Mathf.Max(0.1f, EditorGUILayout.FloatField("Beach Height", beachHeight));
            coastWidth = Mathf.Max(1f, EditorGUILayout.FloatField("Coast Width", coastWidth));
            cityPlateauHeight = Mathf.Max(beachHeight, EditorGUILayout.FloatField("City Plateau Height", cityPlateauHeight));
            cityPlateauWidth = Mathf.Max(1f, EditorGUILayout.FloatField("City Plateau Width", cityPlateauWidth));
            mountainRampWidth = Mathf.Max(1f, EditorGUILayout.FloatField("Mountain Ramp Width", mountainRampWidth));
            mountainHeight = Mathf.Max(cityPlateauHeight, EditorGUILayout.FloatField("Mountain Height", mountainHeight));

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Natural Variation", EditorStyles.boldLabel);
            cityNoiseAmplitude = Mathf.Max(0f, EditorGUILayout.FloatField("City Noise Amplitude", cityNoiseAmplitude));
            mountainNoiseAmplitude = Mathf.Max(0f, EditorGUILayout.FloatField("Mountain Noise Amplitude", mountainNoiseAmplitude));
            noiseScale = Mathf.Max(0.0001f, EditorGUILayout.FloatField("Noise Scale", noiseScale));
            noiseOctaves = Mathf.Clamp(EditorGUILayout.IntField("Noise Octaves", noiseOctaves), 1, 8);
            noiseSeed = EditorGUILayout.IntField("Noise Seed", noiseSeed);
            smoothPasses = Mathf.Clamp(EditorGUILayout.IntField("Smooth Passes", smoothPasses), 0, 6);
            blendStrength = Mathf.Clamp01(EditorGUILayout.FloatField("Blend Strength", blendStrength));

            EditorGUILayout.Space(10f);
            if (GUILayout.Button("Apply Coastal Shape", GUILayout.Height(34f)))
            {
                ApplyCoastalShape();
            }
        }

        private void ApplyCoastalShape()
        {
            List<Terrain> terrains = CollectTerrains();
            if (terrains.Count == 0)
            {
                EditorUtility.DisplayDialog("Coastal Terrain", "Sekillendirilecek Terrain bulunamadi. Terrain secin veya Target Terrains'i ActiveTerrains yapin.", "OK");
                return;
            }

            Vector2 direction = mountainDirection;
            if (direction.sqrMagnitude < 0.0001f)
            {
                EditorUtility.DisplayDialog("Coastal Terrain", "Mountain Direction XZ sifir olamaz.", "OK");
                return;
            }

            direction.Normalize();
            Vector3 seaOrigin = seaReference != null ? seaReference.position : manualSeaOrigin;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Apply Coastal Terrain Shape");

            for (int i = 0; i < terrains.Count; i++)
            {
                ShapeTerrain(terrains[i], seaOrigin, direction);
            }

            SceneView.RepaintAll();
        }

        private List<Terrain> CollectTerrains()
        {
            List<Terrain> terrains = new List<Terrain>();
            if (targetMode == TerrainTargetMode.ActiveTerrains)
            {
                Terrain[] activeTerrains = Terrain.activeTerrains;
                for (int i = 0; i < activeTerrains.Length; i++)
                {
                    AddTerrain(activeTerrains[i], terrains);
                }

                return terrains;
            }

            Transform[] selectedTransforms = Selection.transforms;
            for (int i = 0; i < selectedTransforms.Length; i++)
            {
                Transform selected = selectedTransforms[i];
                if (selected == null)
                {
                    continue;
                }

                AddTerrain(selected.GetComponent<Terrain>(), terrains);
                Terrain[] childTerrains = selected.GetComponentsInChildren<Terrain>(true);
                for (int childIndex = 0; childIndex < childTerrains.Length; childIndex++)
                {
                    AddTerrain(childTerrains[childIndex], terrains);
                }
            }

            return terrains;
        }

        private void AddTerrain(Terrain terrain, List<Terrain> terrains)
        {
            if (terrain != null && terrain.terrainData != null && !terrains.Contains(terrain))
            {
                terrains.Add(terrain);
            }
        }

        private void ShapeTerrain(Terrain terrain, Vector3 seaOrigin, Vector2 direction)
        {
            TerrainData terrainData = terrain.terrainData;
            int resolution = terrainData.heightmapResolution;
            float[,] existingHeights = terrainData.GetHeights(0, 0, resolution, resolution);
            float[,] shapedHeights = new float[resolution, resolution];

            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = terrainData.size;

            for (int z = 0; z < resolution; z++)
            {
                float normalizedZ = z / (float)(resolution - 1);
                float worldZ = terrainPosition.z + normalizedZ * terrainSize.z;

                for (int x = 0; x < resolution; x++)
                {
                    float normalizedX = x / (float)(resolution - 1);
                    float worldX = terrainPosition.x + normalizedX * terrainSize.x;
                    float desiredWorldHeight = CalculateWorldHeight(worldX, worldZ, seaOrigin, direction);
                    float desiredHeight = Mathf.Clamp01((desiredWorldHeight - terrainPosition.y) / terrainSize.y);
                    shapedHeights[z, x] = Mathf.Lerp(existingHeights[z, x], desiredHeight, blendStrength);
                }
            }

            for (int pass = 0; pass < smoothPasses; pass++)
            {
                SmoothHeights(shapedHeights);
            }

            Undo.RegisterCompleteObjectUndo(terrainData, "Shape Coastal Terrain Data");
            terrainData.SetHeights(0, 0, shapedHeights);
            EditorUtility.SetDirty(terrainData);
            EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        }

        private float CalculateWorldHeight(float worldX, float worldZ, Vector3 seaOrigin, Vector2 direction)
        {
            Vector2 delta = new Vector2(worldX - seaOrigin.x, worldZ - seaOrigin.z);
            float distanceFromSea = Vector2.Dot(delta, direction);

            float baseHeight;
            if (distanceFromSea < 0f)
            {
                float shorelineBlend = Mathf.Clamp01((distanceFromSea + shorelineWidth) / shorelineWidth);
                baseHeight = Mathf.Lerp(seaLevel - underwaterDepth, seaLevel + beachHeight, Smooth01(shorelineBlend));
            }
            else if (distanceFromSea < coastWidth)
            {
                float coastBlend = Smooth01(distanceFromSea / coastWidth);
                baseHeight = Mathf.Lerp(seaLevel + beachHeight, cityPlateauHeight, coastBlend);
            }
            else if (distanceFromSea < coastWidth + cityPlateauWidth)
            {
                baseHeight = cityPlateauHeight;
            }
            else
            {
                float mountainDistance = distanceFromSea - coastWidth - cityPlateauWidth;
                float mountainBlend = Smooth01(Mathf.Clamp01(mountainDistance / mountainRampWidth));
                baseHeight = Mathf.Lerp(cityPlateauHeight, mountainHeight, mountainBlend);
            }

            float mountainStart = coastWidth + cityPlateauWidth;
            float mountainAmount = Smooth01(Mathf.Clamp01((distanceFromSea - mountainStart) / mountainRampWidth));
            float cityAmount = 1f - mountainAmount;
            float noise = FractalNoise(worldX, worldZ);
            float noiseAmplitude = cityNoiseAmplitude * cityAmount + mountainNoiseAmplitude * mountainAmount;

            return baseHeight + noise * noiseAmplitude;
        }

        private float FractalNoise(float worldX, float worldZ)
        {
            float amplitude = 1f;
            float frequency = noiseScale;
            float total = 0f;
            float normalization = 0f;

            for (int octave = 0; octave < noiseOctaves; octave++)
            {
                float sampleX = worldX * frequency + noiseSeed * 0.137f + octave * 37.1f;
                float sampleZ = worldZ * frequency + noiseSeed * 0.271f + octave * 19.7f;
                total += (Mathf.PerlinNoise(sampleX, sampleZ) * 2f - 1f) * amplitude;
                normalization += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }

            return normalization > 0f ? total / normalization : 0f;
        }

        private void SmoothHeights(float[,] heights)
        {
            int zLength = heights.GetLength(0);
            int xLength = heights.GetLength(1);
            float[,] copy = (float[,])heights.Clone();

            for (int z = 1; z < zLength - 1; z++)
            {
                for (int x = 1; x < xLength - 1; x++)
                {
                    float total =
                        copy[z, x] * 4f +
                        copy[z - 1, x] +
                        copy[z + 1, x] +
                        copy[z, x - 1] +
                        copy[z, x + 1];

                    heights[z, x] = total / 8f;
                }
            }
        }

        private float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
