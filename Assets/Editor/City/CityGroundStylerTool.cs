#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryDriver.EditorTools
{
    /// <summary>
    /// Master Editor Tool for Hybrid City Ground Styling:
    /// 1. Fixes all Scene Terrains by assigning optimized TerrainLayers (removing the grey prototype checkerboard).
    /// 2. Generates seamless 3D urban parcel foundation slabs (City Block Slabs) under roadside buildings.
    /// 3. Provides 1-Click "Apply Full Hybrid Model" and "Clear Slabs" with full Undo support.
    /// </summary>
    public class CityGroundStylerTool : EditorWindow
    {
        private const string ConcreteMaterialPath = "Assets/ThirdParty/LowPolyCity/Materials/Road Concrete Tile.mat";
        private const string LayerGrassPath = "Assets/ThirdParty/TerrainSamples/TerrainLayers/Grass_A_TerrainLayer.terrainlayer";
        private const string LayerSoilRocksPath = "Assets/ThirdParty/TerrainSamples/TerrainLayers/Soil_Rocks_TerrainLayer.terrainlayer";
        private const string LayerPebblesPath = "Assets/ThirdParty/TerrainSamples/TerrainLayers/Pebbles_A_TerrainLayer.terrainlayer";
        private const string LayerConcretePath = "Assets/ThirdParty/TerrainSamples/TerrainLayers/Urban_Concrete_TerrainLayer.terrainlayer";
        private const string LayerRockPath = "Assets/ThirdParty/TerrainSamples/TerrainLayers/Rock_TerrainLayer.terrainlayer";

        private const string ConcreteTexturePath = "Assets/ThirdParty/LowPolyCity/Textures/Road Concrete Tile.png";

        private enum TargetCitySelection
        {
            BothCities,
            AnkaraOnly,
            IstanbulOnly,
            CustomParent
        }

        [Header("Scope")]
        [SerializeField] private TargetCitySelection cityScope = TargetCitySelection.BothCities;
        [SerializeField] private Transform customBuildingParent;

        [Header("Foundation Slab Settings")]
        [SerializeField] private float slabMargin = 1.8f;
        [SerializeField] private float slabThickness = 0.5f;
        [SerializeField] private float heightOffset = 0.06f;
        [SerializeField] private float uvScale = 0.25f;
        [SerializeField] private bool addBoxColliders = true;
        [SerializeField] private bool markBatchingStatic = true;

        [Header("Terrain Layer Palette")]
        [SerializeField] private TerrainLayer customGrassLayer;
        [SerializeField] private TerrainLayer customSoilLayer;
        [SerializeField] private TerrainLayer customPebblesLayer;
        [SerializeField] private TerrainLayer customConcreteLayer;

        private Vector2 scrollPos;

        [MenuItem("Tools/Delivery Driver/City/City Ground Styler (Hybrid)", false, 10)]
        public static void ShowWindow()
        {
            CityGroundStylerTool win = GetWindow<CityGroundStylerTool>("City Ground Styler");
            win.minSize = new Vector2(420f, 540f);
            win.Show();
        }

        private void OnEnable()
        {
            LoadDefaultLayers();
        }

        private void LoadDefaultLayers()
        {
            if (customGrassLayer == null)
                customGrassLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerGrassPath);
            if (customSoilLayer == null)
                customSoilLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerSoilRocksPath);
            if (customPebblesLayer == null)
                customPebblesLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerPebblesPath);
            if (customConcreteLayer == null)
                customConcreteLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerConcretePath);
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("🏙️ Hybrid City Ground Styler", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Bu araç kentsel zemin için Hibrit Modeli uygular:\n" +
                "1. Sahnede gri kareli (checkerboard) görünen tüm Terrain'lere çim/toprak katmanlarını atar.\n" +
                "2. Şehir merkezindeki binaların altına 'Road Concrete Tile' materyaliyle kentsel ada plakaları (Slabs) döşer.\n" +
                "3. Plakalar World-Space UV ile kesintisiz bağlanır ve mobilde 0-1 draw call ile batch edilir.",
                MessageType.Info);

            // --- LIVE SCENE STATUS ---
            DrawSceneStatusSummary();

            EditorGUILayout.Space(10f);

            // --- 1-CLICK HYBRID MASTER BUTTON ---
            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f, 1f);
            if (GUILayout.Button("⚡ APPLY FULL HYBRID MODEL (TEK TIKLA UYGULA)", GUILayout.Height(44f)))
            {
                ApplyFullHybridModel();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(14f);

            // --- SECTION 1: TERRAIN LAYERS ---
            EditorGUILayout.LabelField("1. Global Terrain Katmanları (Checkerboard Düzeltici)", EditorStyles.boldLabel);
            customGrassLayer = (TerrainLayer)EditorGUILayout.ObjectField("Base Grass (Katman 0)", customGrassLayer, typeof(TerrainLayer), false);
            customSoilLayer = (TerrainLayer)EditorGUILayout.ObjectField("Soil / Dirt (Katman 1)", customSoilLayer, typeof(TerrainLayer), false);
            customPebblesLayer = (TerrainLayer)EditorGUILayout.ObjectField("Pebbles (Katman 2)", customPebblesLayer, typeof(TerrainLayer), false);
            customConcreteLayer = (TerrainLayer)EditorGUILayout.ObjectField("Urban Concrete (Katman 3)", customConcreteLayer, typeof(TerrainLayer), false);

            if (GUILayout.Button("🌿 Sadece Terrain Katmanlarını Uygula (Fix All Terrains)", GUILayout.Height(30f)))
            {
                ApplyTerrainLayers();
            }

            EditorGUILayout.Space(14f);

            // --- SECTION 2: URBAN SLABS ---
            EditorGUILayout.LabelField("2. Kentsel Ada Tabanları (City Block Slabs)", EditorStyles.boldLabel);
            cityScope = (TargetCitySelection)EditorGUILayout.EnumPopup("Hedef Şehir / Kapsam", cityScope);
            if (cityScope == TargetCitySelection.CustomParent)
            {
                customBuildingParent = (Transform)EditorGUILayout.ObjectField("Bina Kök Objesi", customBuildingParent, typeof(Transform), true);
            }

            slabMargin = EditorGUILayout.Slider("Kenar Boşluğu / Margin (m)", slabMargin, 0.5f, 5.0f);
            slabThickness = EditorGUILayout.Slider("Plaka Kalınlığı / Depth (m)", slabThickness, 0.2f, 2.0f);
            heightOffset = EditorGUILayout.Slider("Zemin Yükseklik Ofseti (m)", heightOffset, -0.1f, 0.3f);
            uvScale = EditorGUILayout.Slider("UV Dokusu Tekrarı (Scale)", uvScale, 0.1f, 1.0f);
            addBoxColliders = EditorGUILayout.Toggle("Fizik BoxCollider Ekle", addBoxColliders);
            markBatchingStatic = EditorGUILayout.Toggle("Batching Static Olarak İşaretle", markBatchingStatic);

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("🏙️ Sadece Kentsel Ada Plakalarını Üret (Generate Slabs)", GUILayout.Height(30f)))
            {
                GenerateFoundationSlabs();
            }

            EditorGUILayout.Space(14f);

            // --- SECTION 3: CLEANUP ---
            EditorGUILayout.LabelField("3. Temizleme ve Yönetim", EditorStyles.boldLabel);
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f, 1f);
            if (GUILayout.Button("🧹 Üretilen Ada Plakalarını Temizle (Clear Generated Slabs)", GUILayout.Height(30f)))
            {
                ClearAllGeneratedSlabs();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(12f);
            EditorGUILayout.EndScrollView();
        }

        private void DrawSceneStatusSummary()
        {
            int terrainCount = Terrain.activeTerrains.Length;
            if (terrainCount == 0)
            {
                terrainCount = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            }

            Scene scene = SceneManager.GetActiveScene();
            Transform worldRoot = FindRootTransform(scene, "World");
            bool ankaraFound = (FindChildRecursive(worldRoot, "Ankara") ?? FindSceneRootTransform(scene, "Ankara")) != null;
            bool istanbulFound = (FindChildRecursive(worldRoot, "İstanbul") ??
                                  FindChildRecursive(worldRoot, "Istanbul") ??
                                  FindSceneRootTransform(scene, "İstanbul") ??
                                  FindSceneRootTransform(scene, "Istanbul")) != null;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📊 Sahne Durumu (Aktif Sahne: " + scene.name + ")", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"• Tespit Edilen Terrain Sayısı: {terrainCount}");
            EditorGUILayout.LabelField($"• Ankara Kökü: {(ankaraFound ? "✅ Mevcut" : "❌ Bulunamadı")}");
            EditorGUILayout.LabelField($"• İstanbul Kökü: {(istanbulFound ? "✅ Mevcut" : "❌ Bulunamadı")}");
            EditorGUILayout.EndVertical();
        }

        #region Hybrid Master Execution

        private void ApplyFullHybridModel()
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Apply Full Hybrid City Ground Model");

            int terrainCount = ApplyTerrainLayersInternal();
            int slabCount = GenerateFoundationSlabsInternal();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            string message = $"[CityGroundStyler] Hibrit Model Başarıyla Uygulandı!\n" +
                             $"• {terrainCount} adet Terrain'e zemin katmanları atandı (Gri dama giderildi).\n" +
                             $"• {slabCount} adet bina altına 'Road Concrete Tile' beton kentsel ada plakası yerleştirildi.";
            Debug.Log(message);
            EditorUtility.DisplayDialog("Hibrit Model Uygulandı", message, "Tamam");
        }

        #endregion

        #region Terrain Layer Management

        public void ApplyTerrainLayers()
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Apply Terrain Layers");

            int count = ApplyTerrainLayersInternal();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            string msg = $"[CityGroundStyler] Toplam {count} adet Terrain güncellendi ve gri damalı zemin düzeltildi!";
            Debug.Log(msg);
            EditorUtility.DisplayDialog("Terrain Katmanları Güncellendi", msg, "Harika");
        }

        private int ApplyTerrainLayersInternal()
        {
            EnsureConcreteTerrainLayerExists();
            LoadDefaultLayers();

            List<TerrainLayer> layerList = new List<TerrainLayer>();
            if (customGrassLayer != null) layerList.Add(customGrassLayer);
            if (customSoilLayer != null) layerList.Add(customSoilLayer);
            if (customPebblesLayer != null) layerList.Add(customPebblesLayer);
            if (customConcreteLayer != null) layerList.Add(customConcreteLayer);

            if (layerList.Count == 0)
            {
                Debug.LogWarning("[CityGroundStyler] Hiçbir TerrainLayer bulunamadı!");
                return 0;
            }

            TerrainLayer[] finalLayers = layerList.ToArray();

            // Find all Terrains in the active scene
            Terrain[] sceneTerrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            HashSet<TerrainData> processedTerrainData = new HashSet<TerrainData>();
            int count = 0;

            foreach (Terrain t in sceneTerrains)
            {
                if (t == null || t.terrainData == null) continue;
                if (processedTerrainData.Contains(t.terrainData)) continue;

                processedTerrainData.Add(t.terrainData);
                Undo.RegisterCompleteObjectUndo(t.terrainData, "Update Terrain Layers");

                t.terrainData.terrainLayers = finalLayers;
                EditorUtility.SetDirty(t.terrainData);
                count++;
            }

            AssetDatabase.SaveAssets();
            return count;
        }

        private void EnsureConcreteTerrainLayerExists()
        {
            if (customConcreteLayer != null) return;

            customConcreteLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerConcretePath);
            if (customConcreteLayer != null) return;

            // Create programmatically if not on disk
            Texture2D concreteTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ConcreteTexturePath);
            if (concreteTex == null) return;

            TerrainLayer newLayer = new TerrainLayer
            {
                name = "Urban_Concrete_TerrainLayer",
                diffuseTexture = concreteTex,
                tileSize = new Vector2(4f, 4f),
                smoothness = 0.2f
            };

            string dir = Path.GetDirectoryName(LayerConcretePath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            AssetDatabase.CreateAsset(newLayer, LayerConcretePath);
            AssetDatabase.SaveAssets();
            customConcreteLayer = newLayer;
            Debug.Log("[CityGroundStyler] Yeni Urban_Concrete_TerrainLayer oluşturuldu.");
        }

        #endregion

        #region Foundation Slabs Generation

        public void GenerateFoundationSlabs()
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Generate City Block Foundation Slabs");

            int count = GenerateFoundationSlabsInternal();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            string msg = $"[CityGroundStyler] {count} adet bina altına kentsel ada plakası (Slab) başarıyla yerleştirildi!";
            Debug.Log(msg);
            EditorUtility.DisplayDialog("Ada Plakaları Üretildi", msg, "Tamam");
        }

        private int GenerateFoundationSlabsInternal()
        {
            Material concreteMat = AssetDatabase.LoadAssetAtPath<Material>(ConcreteMaterialPath);
            if (concreteMat == null)
            {
                Debug.LogError($"[CityGroundStyler] Beton materyali bulunamadı: {ConcreteMaterialPath}");
                return 0;
            }

            List<Transform> targetRoots = ResolveTargetCityRoots();
            if (targetRoots.Count == 0)
            {
                Debug.LogWarning("[CityGroundStyler] Hedef şehir kökü veya binalar bulunamadı!");
                return 0;
            }

            int totalCreated = 0;

            foreach (Transform cityRoot in targetRoots)
            {
                List<GameObject> buildings = CollectBuildingsUnder(cityRoot);
                if (buildings.Count == 0) continue;

                Transform groundFolder = EnsureChild(cityRoot, "Ground");
                Transform slabsContainer = EnsureChild(groundFolder, "City_Block_Slabs");

                // Clear previous slabs under this container to prevent stacking duplicates
                ClearChildrenImmediate(slabsContainer);

                foreach (GameObject building in buildings)
                {
                    if (building == null) continue;

                    GameObject slab = CreateOrientedSlabForBuilding(building, slabsContainer, concreteMat);
                    if (slab != null)
                    {
                        totalCreated++;
                    }
                }
            }

            return totalCreated;
        }

        private GameObject CreateOrientedSlabForBuilding(GameObject building, Transform parent, Material mat)
        {
            Renderer[] renderers = building.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return null;

            // Filter for valid mesh renderers (avoid light halos, particle systems)
            List<Renderer> validRenderers = new List<Renderer>();
            foreach (var r in renderers)
            {
                if (r is MeshRenderer || r is SkinnedMeshRenderer)
                {
                    // Ignore road and existing slabs if accidentally nested
                    if (r.name.ToLower().Contains("slab") || r.name.ToLower().Contains("road")) continue;
                    validRenderers.Add(r);
                }
            }

            if (validRenderers.Count == 0) return null;

            // Compute Yaw-aligned bounding box around the building
            Quaternion buildingYaw = Quaternion.Euler(0f, building.transform.eulerAngles.y, 0f);
            Quaternion invYaw = Quaternion.Inverse(buildingYaw);

            Vector3 minLocal = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 maxLocal = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            Vector3 pivot = building.transform.position;

            foreach (var r in validRenderers)
            {
                Bounds b = r.bounds;
                Vector3[] corners = GetBoundsCorners(b);
                foreach (var c in corners)
                {
                    Vector3 localCorner = invYaw * (c - pivot);
                    minLocal = Vector3.Min(minLocal, localCorner);
                    maxLocal = Vector3.Max(maxLocal, localCorner);
                }
            }

            // Expand by margin
            minLocal.x -= slabMargin;
            maxLocal.x += slabMargin;
            minLocal.z -= slabMargin;
            maxLocal.z += slabMargin;

            float width = maxLocal.x - minLocal.x;
            float length = maxLocal.z - minLocal.z;

            // Reject unreasonably tiny or gigantic bounds
            if (width < 1f || length < 1f || width > 300f || length > 300f) return null;

            Vector3 localCenter = new Vector3((minLocal.x + maxLocal.x) * 0.5f, 0f, (minLocal.z + maxLocal.z) * 0.5f);
            Vector3 worldCenter = pivot + buildingYaw * localCenter;

            // Sample ground height under world center
            float groundY = GetGroundHeight(worldCenter);
            worldCenter.y = groundY + heightOffset - (slabThickness * 0.5f);

            // Create Slab GameObject
            GameObject slabGo = new GameObject($"Slab_{building.name}");
            Undo.RegisterCreatedObjectUndo(slabGo, "Create Building Slab");

            slabGo.transform.SetParent(parent, false);
            slabGo.transform.position = worldCenter;
            slabGo.transform.rotation = buildingYaw;

            MeshFilter mf = slabGo.AddComponent<MeshFilter>();
            MeshRenderer mr = slabGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

            // Build 3D box mesh with World-Space UVs for seamless tiling
            mf.sharedMesh = BuildBoxMesh(width, slabThickness, length, worldCenter, buildingYaw, uvScale);

            if (addBoxColliders)
            {
                BoxCollider col = slabGo.AddComponent<BoxCollider>();
                col.size = new Vector3(width, slabThickness, length);
            }

            if (markBatchingStatic)
            {
                GameObjectUtility.SetStaticEditorFlags(slabGo,
                    StaticEditorFlags.BatchingStatic |
                    StaticEditorFlags.OccludeeStatic |
                    StaticEditorFlags.ReflectionProbeStatic);
            }

            return slabGo;
        }

        private Mesh BuildBoxMesh(float width, float height, float length, Vector3 worldPos, Quaternion worldRot, float uvFactor)
        {
            Mesh mesh = new Mesh { name = "Procedural_City_Slab" };

            float hx = width * 0.5f;
            float hy = height * 0.5f;
            float hz = length * 0.5f;

            // 8 Box Corners in Local Space
            Vector3 p0 = new Vector3(-hx, -hy, -hz);
            Vector3 p1 = new Vector3( hx, -hy, -hz);
            Vector3 p2 = new Vector3( hx, -hy,  hz);
            Vector3 p3 = new Vector3(-hx, -hy,  hz);
            Vector3 p4 = new Vector3(-hx,  hy, -hz);
            Vector3 p5 = new Vector3( hx,  hy, -hz);
            Vector3 p6 = new Vector3( hx,  hy,  hz);
            Vector3 p7 = new Vector3(-hx,  hy,  hz);

            List<Vector3> verts = new List<Vector3>(24);
            List<Vector3> norms = new List<Vector3>(24);
            List<Vector2> uvs = new List<Vector2>(24);
            List<int> tris = new List<int>(36);

            // Top face (+Y)
            AddQuad(verts, norms, uvs, tris, p4, p7, p6, p5, Vector3.up, worldPos, worldRot, uvFactor, isTop: true);

            // Bottom face (-Y)
            AddQuad(verts, norms, uvs, tris, p1, p2, p3, p0, Vector3.down, worldPos, worldRot, uvFactor, isTop: false);

            // Front face (-Z)
            AddQuad(verts, norms, uvs, tris, p0, p4, p5, p1, Vector3.back, worldPos, worldRot, uvFactor, isTop: false);

            // Back face (+Z)
            AddQuad(verts, norms, uvs, tris, p2, p6, p7, p3, Vector3.forward, worldPos, worldRot, uvFactor, isTop: false);

            // Left face (-X)
            AddQuad(verts, norms, uvs, tris, p3, p7, p4, p0, Vector3.left, worldPos, worldRot, uvFactor, isTop: false);

            // Right face (+X)
            AddQuad(verts, norms, uvs, tris, p1, p5, p6, p2, Vector3.right, worldPos, worldRot, uvFactor, isTop: false);

            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);

            mesh.RecalculateBounds();
            return mesh;
        }

        private void AddQuad(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 norm, Vector3 worldPos, Quaternion worldRot, float uvFactor, bool isTop)
        {
            int startIndex = verts.Count;

            verts.Add(a);
            verts.Add(b);
            verts.Add(c);
            verts.Add(d);

            norms.Add(norm);
            norms.Add(norm);
            norms.Add(norm);
            norms.Add(norm);

            if (isTop)
            {
                // Seamless World-Space XZ mapping for top faces
                uvs.Add(ToWorldUV(a, worldPos, worldRot, uvFactor));
                uvs.Add(ToWorldUV(b, worldPos, worldRot, uvFactor));
                uvs.Add(ToWorldUV(c, worldPos, worldRot, uvFactor));
                uvs.Add(ToWorldUV(d, worldPos, worldRot, uvFactor));
            }
            else
            {
                // Local curb edge mapping for sides
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(0f, 1f));
                uvs.Add(new Vector2(1f, 1f));
                uvs.Add(new Vector2(1f, 0f));
            }

            tris.Add(startIndex);
            tris.Add(startIndex + 1);
            tris.Add(startIndex + 2);

            tris.Add(startIndex);
            tris.Add(startIndex + 2);
            tris.Add(startIndex + 3);
        }

        private Vector2 ToWorldUV(Vector3 localVertex, Vector3 worldPos, Quaternion worldRot, float uvFactor)
        {
            Vector3 worldV = worldPos + worldRot * localVertex;
            return new Vector2(worldV.x * uvFactor, worldV.z * uvFactor);
        }

        private Vector3[] GetBoundsCorners(Bounds b)
        {
            return new[]
            {
                new Vector3(b.min.x, b.min.y, b.min.z),
                new Vector3(b.max.x, b.min.y, b.min.z),
                new Vector3(b.max.x, b.min.y, b.max.z),
                new Vector3(b.min.x, b.min.y, b.max.z),
                new Vector3(b.min.x, b.max.y, b.min.z),
                new Vector3(b.max.x, b.max.y, b.min.z),
                new Vector3(b.max.x, b.max.y, b.max.z),
                new Vector3(b.min.x, b.max.y, b.max.z)
            };
        }

        private float GetGroundHeight(Vector3 pos)
        {
            // Raycast terrain and ground colliders
            if (Physics.Raycast(new Vector3(pos.x, pos.y + 100f, pos.z), Vector3.down, out RaycastHit hit, 250f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider is TerrainCollider || hit.collider.name.ToLower().Contains("terrain"))
                {
                    return hit.point.y;
                }
            }

            // Fallback to active terrain sample
            Terrain activeTerrain = Terrain.activeTerrain;
            if (activeTerrain != null)
            {
                return activeTerrain.SampleHeight(pos) + activeTerrain.transform.position.y;
            }

            return pos.y;
        }

        #endregion

        #region City & Building Collection Helpers

        private List<Transform> ResolveTargetCityRoots()
        {
            List<Transform> roots = new List<Transform>();
            Scene scene = SceneManager.GetActiveScene();

            if (cityScope == TargetCitySelection.CustomParent && customBuildingParent != null)
            {
                roots.Add(customBuildingParent);
                return roots;
            }

            Transform worldRoot = FindRootTransform(scene, "World");

            if (cityScope == TargetCitySelection.AnkaraOnly || cityScope == TargetCitySelection.BothCities)
            {
                Transform ankara = FindChildRecursive(worldRoot, "Ankara") ?? FindSceneRootTransform(scene, "Ankara");
                if (ankara != null) roots.Add(ankara);
            }

            if (cityScope == TargetCitySelection.IstanbulOnly || cityScope == TargetCitySelection.BothCities)
            {
                Transform istanbul = FindChildRecursive(worldRoot, "İstanbul") ??
                                     FindChildRecursive(worldRoot, "Istanbul") ??
                                     FindSceneRootTransform(scene, "İstanbul") ??
                                     FindSceneRootTransform(scene, "Istanbul");
                if (istanbul != null) roots.Add(istanbul);
            }

            return roots;
        }

        private List<GameObject> CollectBuildingsUnder(Transform root)
        {
            List<GameObject> buildings = new List<GameObject>();
            if (root == null) return buildings;

            HashSet<GameObject> seen = new HashSet<GameObject>();

            void TryAdd(GameObject go)
            {
                if (go != null && seen.Add(go))
                {
                    buildings.Add(go);
                }
            }

            // Search in typical folders: "Generated Roadside Buildings", "Buildings"
            Transform roadside = FindChildRecursive(root, "Generated Roadside Buildings");
            if (roadside != null)
            {
                for (int i = 0; i < roadside.childCount; i++)
                {
                    TryAdd(roadside.GetChild(i).gameObject);
                }
            }

            // Search in district folders: Districts/*/Buildings
            Transform districts = FindChildRecursive(root, "Districts");
            if (districts != null)
            {
                for (int d = 0; d < districts.childCount; d++)
                {
                    Transform district = districts.GetChild(d);
                    Transform bFolder = FindChildRecursive(district, "Buildings");
                    if (bFolder != null)
                    {
                        for (int b = 0; b < bFolder.childCount; b++)
                        {
                            TryAdd(bFolder.GetChild(b).gameObject);
                        }
                    }
                }
            }

            // Fallback: If no roadside or districts folder, collect direct children or GameObjects with "Building" in name
            if (buildings.Count == 0)
            {
                Transform generalBuildings = FindChildRecursive(root, "Buildings");
                if (generalBuildings != null)
                {
                    for (int i = 0; i < generalBuildings.childCount; i++)
                    {
                        TryAdd(generalBuildings.GetChild(i).gameObject);
                    }
                }
                else
                {
                    CollectBuildingsByNameRecursive(root, TryAdd);
                }
            }

            return buildings;
        }

        private void CollectBuildingsByNameRecursive(Transform current, System.Action<GameObject> onFound)
        {
            if (current.name.ToLower().Contains("building") && current.GetComponent<Renderer>() != null)
            {
                onFound(current.gameObject);
                return;
            }

            for (int i = 0; i < current.childCount; i++)
            {
                CollectBuildingsByNameRecursive(current.GetChild(i), onFound);
            }
        }

        #endregion

        #region Cleanup

        public void ClearAllGeneratedSlabs()
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Clear Generated City Block Slabs");

            int destroyedCount = 0;
            Scene scene = SceneManager.GetActiveScene();

            List<GameObject> slabsToDestroy = new List<GameObject>();
            foreach (GameObject rootGo in scene.GetRootGameObjects())
            {
                FindSlabContainersRecursive(rootGo.transform, slabsToDestroy);
            }

            foreach (var go in slabsToDestroy)
            {
                if (go != null)
                {
                    destroyedCount++;
                    Undo.DestroyObjectImmediate(go);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            string msg = $"[CityGroundStyler] {destroyedCount} adet kentsel ada plakası grubu temizlendi.";
            Debug.Log(msg);
            EditorUtility.DisplayDialog("Plakalar Temizlendi", msg, "Tamam");
        }

        private void FindSlabContainersRecursive(Transform current, List<GameObject> list)
        {
            if (current.name == "City_Block_Slabs")
            {
                list.Add(current.gameObject);
                return;
            }

            for (int i = 0; i < current.childCount; i++)
            {
                FindSlabContainersRecursive(current.GetChild(i), list);
            }
        }

        #endregion

        #region Transform & Hierarchy Helpers

        private static Transform EnsureChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null) return child;

            GameObject newGo = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(newGo, $"Create {childName}");
            newGo.transform.SetParent(parent, false);
            newGo.transform.localPosition = Vector3.zero;
            newGo.transform.localRotation = Quaternion.identity;
            newGo.transform.localScale = Vector3.one;
            return newGo.transform;
        }

        private static void ClearChildrenImmediate(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
            }
        }

        private static Transform FindRootTransform(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == name) return go.transform;
            }
            return null;
        }

        private static Transform FindSceneRootTransform(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == name) return go.transform;
            }
            return null;
        }

        private static Transform FindChildRecursive(Transform parent, string targetName)
        {
            if (parent == null) return null;
            if (parent.name == targetName) return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindChildRecursive(parent.GetChild(i), targetName);
                if (found != null) return found;
            }

            return null;
        }

        #endregion
    }
}
#endif
