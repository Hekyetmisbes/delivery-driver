#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using DeliveryDriver.Optimization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DeliveryDriver.EditorTools
{
    /// <summary>
    /// City Optimization & LOD Studio.
    /// Comprehensive tool for:
    /// 1. Automatic LODGroup generation with distant shadow culling (Roads & Terrain protected).
    /// 2. Spatial 2D World Chunking & dynamic streaming setup.
    /// 3. Chunk-based HLOD proxy mesh generation and draw call reduction.
    /// 4. Camera layer culling configuration.
    /// 5. Scene performance auditing, 1-click mobile/PC optimization presets, and Road Rescue.
    /// </summary>
    public class CityOptimizationStudio : EditorWindow
    {
        private const string GeneratedMeshesFolder = "Assets/Art/Meshes/GeneratedHLOD";
        private const string WorldChunksRootName = "WorldChunks";

        private enum Tab
        {
            LodSetup,
            WorldChunking,
            HlodCombiner,
            LayerCulling,
            OcclusionCulling,
            AuditAndPresets
        }

        private Tab currentTab = Tab.LodSetup;
        private Vector2 scrollPosition;

        // Common Settings
        [SerializeField] private Transform targetCityRoot;
        [SerializeField] private bool autoFindCityRoot = true;

        // --- LOD Setup Settings ---
        [Header("LOD: Buildings")]
        [SerializeField] private bool processBuildings = true;
        [SerializeField] private float buildingLod0ScreenHeight = 0.30f;
        [SerializeField] private float buildingLod1ScreenHeight = 0.015f;
        [SerializeField] private bool buildingDisableShadowsInLod1 = true;
        [SerializeField] private bool buildingExcludeSmallDetailsInLod1 = true;

        [Header("LOD: Trees & Foliage")]
        [SerializeField] private bool processTrees = true;
        [SerializeField] private float treeLod0ScreenHeight = 0.25f;
        [SerializeField] private float treeLod1ScreenHeight = 0.025f;
        [SerializeField] private bool treeDisableShadowsInLod1 = true;

        [Header("LOD: Props & Small Items")]
        [SerializeField] private bool processProps = true;
        [SerializeField] private float propLod0ScreenHeight = 0.15f;
        [SerializeField] private float propLod1ScreenHeight = 0.040f;
        [SerializeField] private bool propDisableShadowsInLod1 = true;

        [Header("LOD: Landmarks & Mega Structures")]
        [SerializeField] private bool processLandmarks = true;
        [SerializeField] private float landmarkLod0ScreenHeight = 0.40f;
        [SerializeField] private float landmarkLod1ScreenHeight = 0.005f;
        [SerializeField] private bool landmarkDisableShadowsInLod1 = true;

        // --- World Chunking Settings ---
        [Header("Chunk Grid Configuration")]
        [SerializeField] private float chunkSize = 64f;
        [SerializeField] private float nearRingDistance = 180f;
        [SerializeField] private float midRingDistance = 320f;
        [SerializeField] private float farRingDistance = 450f;
        [SerializeField] private bool enableHardRadiusCulling = true;
        [SerializeField] private float rendererCullingPadding = 25f;
        [SerializeField] private Transform chunkContainerParent;

        // --- HLOD Combiner Settings ---
        [Header("HLOD Mesh Combining")]
        [SerializeField] private bool combineByMaterial = true;
        [SerializeField] private bool removeCollidersOnProxy = true;

        // --- Camera & Layer Culling Settings ---
        [Header("Layer Culling Distances")]
        [SerializeField] private float propsCullingDistance = 120f;
        [SerializeField] private float natureCullingDistance = 250f;
        [SerializeField] private float buildingsCullingDistance = 600f;
        [SerializeField] private float defaultCullingDistance = 1000f;

        // --- Occlusion & Frustum Culling Settings ---
        [Header("Occlusion & Frustum Culling Settings")]
        [SerializeField] private float smallestOccluder = 8.0f;
        [SerializeField] private float smallestHole = 2.5f;
        [SerializeField] private float backfaceThreshold = 100f;
        [SerializeField] private bool enableWorldChunkFrustumCulling = true;
        [SerializeField] private float frustumSafetyRadius = 100f;
        [SerializeField] private float recommendedShadowDistance = 80f;

        // --- Audit Cache ---
        private int auditTotalRenderers;
        private int auditWithLodCount;
        private int auditWithoutLodCount;
        private int auditTotalChunks;
        private int auditTotalTriangles;
        private bool auditCompleted;

        // Filter keywords for categorization
        private static readonly string[] BuildingKeywords = { "building", "house", "shop", "store", "residential", "station", "apart", "factory", "stadium", "bakery", "bar", "cafe", "fast food", "garage" };
        private static readonly string[] TreeKeywords = { "tree", "nature", "bush", "plant", "palm", "birch", "oak", "flower", "grass", "foliage" };
        private static readonly string[] PropKeywords = { "prop", "bench", "trash", "light", "lamp", "hydrant", "sign", "fence", "barrier", "post", "barrel", "box", "crate", "traffic light" };
        private static readonly string[] LandmarkKeywords = { "landmark", "tower", "atakule", "castle", "crane", "port", "monument", "anit" };

        [MenuItem("Tools/Delivery Driver/Optimization/City Optimization & LOD Studio", false, 10)]
        public static void ShowWindow()
        {
            CityOptimizationStudio window = GetWindow<CityOptimizationStudio>("Optimization Studio");
            window.minSize = new Vector2(520, 700);
            window.Show();
        }

        [MenuItem("Tools/Delivery Driver/Optimization/Restore and Fix Scene Roads", false, 11)]
        public static void RestoreRoadsMenuCommand()
        {
            RestoreAndFixRoads();
        }

        private void OnEnable()
        {
            if (autoFindCityRoot && targetCityRoot == null)
            {
                TryAutoFindCityRoot();
            }
        }

        private void TryAutoFindCityRoot()
        {
            string[] candidateNames = { "Ankara", "City", "Districts", "Environment", "Buildings" };
            foreach (string name in candidateNames)
            {
                GameObject go = GameObject.Find(name);
                if (go != null)
                {
                    targetCityRoot = go.transform;
                    break;
                }
            }
        }

        private void OnGUI()
        {
            DrawHeader();

            GUILayout.Space(6);
            DrawTabBar();

            GUILayout.Space(8);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            switch (currentTab)
            {
                case Tab.LodSetup:
                    DrawLodSetupTab();
                    break;
                case Tab.WorldChunking:
                    DrawWorldChunkingTab();
                    break;
                case Tab.HlodCombiner:
                    DrawHlodCombinerTab();
                    break;
                case Tab.LayerCulling:
                    DrawLayerCullingTab();
                    break;
                case Tab.OcclusionCulling:
                    DrawOcclusionCullingTab();
                    break;
                case Tab.AuditAndPresets:
                    DrawAuditAndPresetsTab();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(4);
            EditorGUILayout.LabelField("City Optimization & LOD Studio", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Mobile 60 FPS & PC Optimization Suite (LODs, Chunking, HLOD, Culling)", EditorStyles.miniLabel);
            GUILayout.Space(4);

            // Emergency / Quick Action Road Rescue Bar
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(1f, 0.65f, 0.15f);
            if (GUILayout.Button("🛣️ Yolları Kurtar ve Geri Yükle (Restore & Fix Roads)", GUILayout.Height(30)))
            {
                RestoreAndFixRoads();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            int roadLods = CountRoadLodsInScene();
            if (roadLods > 0)
            {
                GUILayout.Space(4);
                EditorGUILayout.HelpBox($"⚠️ DİKKAT: Sahnede {roadLods} adet yol/kavşak objesine hatalı LODGroup eklenmiş durumda! Bu durum yolların oyunda görünmemesine (cull edilmesine) sebep olur. Lütfen yukarıdaki 'Yolları Kurtar' butonuna tıklayın.", MessageType.Error);
            }

            GUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            targetCityRoot = (Transform)EditorGUILayout.ObjectField("Target Root:", targetCityRoot, typeof(Transform), true);
            if (GUILayout.Button("Auto-Find", GUILayout.Width(75)))
            {
                TryAutoFindCityRoot();
            }
            EditorGUILayout.EndHorizontal();

            if (targetCityRoot == null)
            {
                EditorGUILayout.HelpBox("Hedef kök obje seçilmediğinde, işlemler sahnedeki binalar ve proplar üzerinde çalıştırılır (Yollar ve zemin otomatik korunur).", MessageType.Info);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawTabBar()
        {
            EditorGUILayout.BeginHorizontal();
            string[] tabNames = { "LOD Kurulumu", "Mekansal Izgara", "HLOD Combine", "Kamera Culling", "Görünmeyenleri Gizle (Occlusion)", "Analiz & Şablonlar" };
            for (int i = 0; i < tabNames.Length; i++)
            {
                bool isSelected = (int)currentTab == i;
                GUIStyle style = isSelected ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
                if (isSelected)
                {
                    GUI.backgroundColor = new Color(0.4f, 0.8f, 1f, 1f);
                }
                if (GUILayout.Button(tabNames[i], style, GUILayout.Height(26)))
                {
                    currentTab = (Tab)i;
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();
        }

        // =========================================================================
        // 1. LOD SETUP TAB
        // =========================================================================
        private void DrawLodSetupTab()
        {
            EditorGUILayout.LabelField("1. Otomatik LODGroup ve Gölge Optimizasyonu", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Bu modül, sahnedeki binalara, ağaçlara ve proplara akıllı kurallarla LODGroup bileşeni ekler.\n" +
                "🛡️ Yollar, kaldırımlar, kavşaklar ve zeminler kesinlikle LOD işleminden MUAF tutulur.", MessageType.Info);

            GUILayout.Space(8);

            // Buildings Group
            processBuildings = EditorGUILayout.BeginToggleGroup("Binalar (Buildings)", processBuildings);
            buildingLod0ScreenHeight = EditorGUILayout.Slider("LOD 0 Geçiş Oranı:", buildingLod0ScreenHeight, 0.05f, 0.8f);
            buildingLod1ScreenHeight = EditorGUILayout.Slider("Culling (Gizlenme) Oranı:", buildingLod1ScreenHeight, 0.005f, 0.1f);
            buildingDisableShadowsInLod1 = EditorGUILayout.Toggle("LOD1'de Gölgeleri Kapat", buildingDisableShadowsInLod1);
            buildingExcludeSmallDetailsInLod1 = EditorGUILayout.Toggle("LOD1'de Küçük Detayları Kapat", buildingExcludeSmallDetailsInLod1);
            EditorGUILayout.EndToggleGroup();

            GUILayout.Space(6);

            // Trees Group
            processTrees = EditorGUILayout.BeginToggleGroup("Ağaçlar ve Doğa (Trees & Nature)", processTrees);
            treeLod0ScreenHeight = EditorGUILayout.Slider("LOD 0 Geçiş Oranı:", treeLod0ScreenHeight, 0.05f, 0.8f);
            treeLod1ScreenHeight = EditorGUILayout.Slider("Culling (Gizlenme) Oranı:", treeLod1ScreenHeight, 0.005f, 0.1f);
            treeDisableShadowsInLod1 = EditorGUILayout.Toggle("LOD1'de Gölgeleri Kapat", treeDisableShadowsInLod1);
            EditorGUILayout.EndToggleGroup();

            GUILayout.Space(6);

            // Props Group
            processProps = EditorGUILayout.BeginToggleGroup("Küçük Proplar ve Sokak Mobilyaları (Props)", processProps);
            propLod0ScreenHeight = EditorGUILayout.Slider("LOD 0 Geçiş Oranı:", propLod0ScreenHeight, 0.05f, 0.6f);
            propLod1ScreenHeight = EditorGUILayout.Slider("Culling (Gizlenme) Oranı:", propLod1ScreenHeight, 0.01f, 0.15f);
            propDisableShadowsInLod1 = EditorGUILayout.Toggle("LOD1'de Gölgeleri Kapat", propDisableShadowsInLod1);
            EditorGUILayout.EndToggleGroup();

            GUILayout.Space(6);

            // Landmarks Group
            processLandmarks = EditorGUILayout.BeginToggleGroup("Simgesel Yapılar (Landmarks & Towers)", processLandmarks);
            landmarkLod0ScreenHeight = EditorGUILayout.Slider("LOD 0 Geçiş Oranı:", landmarkLod0ScreenHeight, 0.1f, 0.9f);
            landmarkLod1ScreenHeight = EditorGUILayout.Slider("Culling (Gizlenme) Oranı:", landmarkLod1ScreenHeight, 0.001f, 0.03f);
            landmarkDisableShadowsInLod1 = EditorGUILayout.Toggle("LOD1'de Gölgeleri Kapat", landmarkDisableShadowsInLod1);
            EditorGUILayout.EndToggleGroup();

            GUILayout.Space(14);
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.3f);
            if (GUILayout.Button("🚀 LODGroup'ları Otomatik Oluştur", GUILayout.Height(36)))
            {
                SetupLODGroupsOnTarget();
            }

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("🗑️ Tüm LODGroup'ları Kaldır", GUILayout.Height(36), GUILayout.Width(170)))
            {
                RemoveAllLODGroupsOnTarget();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        private void SetupLODGroupsOnTarget()
        {
            List<GameObject> candidates = CollectCandidateRoots();
            if (candidates.Count == 0)
            {
                EditorUtility.DisplayDialog("LOD Kurulumu", "Hedef hiyerarşide işlenecek uygun bina/obje bulunamadı!", "Tamam");
                return;
            }

            int buildingCount = 0;
            int treeCount = 0;
            int propCount = 0;
            int landmarkCount = 0;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Setup City LODGroups");
            int undoGroup = Undo.GetCurrentGroup();

            try
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    GameObject obj = candidates[i];
                    if (obj == null) continue;

                    // Strictly skip roads, terrain, water, or ground
                    if (IsExcludedFromOptimization(obj.transform))
                    {
                        continue;
                    }

                    EditorUtility.DisplayProgressBar("LOD Kurulumu", $"İşleniyor: {obj.name} ({i + 1}/{candidates.Count})", (float)i / candidates.Count);

                    Category cat = CategorizeGameObject(obj);
                    if (cat == Category.Excluded) continue;
                    if (cat == Category.Building && !processBuildings) continue;
                    if (cat == Category.Tree && !processTrees) continue;
                    if (cat == Category.Prop && !processProps) continue;
                    if (cat == Category.Landmark && !processLandmarks) continue;

                    float lod0 = cat switch
                    {
                        Category.Building => buildingLod0ScreenHeight,
                        Category.Tree => treeLod0ScreenHeight,
                        Category.Prop => propLod0ScreenHeight,
                        Category.Landmark => landmarkLod0ScreenHeight,
                        _ => 0.25f
                    };

                    float cull = cat switch
                    {
                        Category.Building => buildingLod1ScreenHeight,
                        Category.Tree => treeLod1ScreenHeight,
                        Category.Prop => propLod1ScreenHeight,
                        Category.Landmark => landmarkLod1ScreenHeight,
                        _ => 0.02f
                    };

                    bool disableShadows = cat switch
                    {
                        Category.Building => buildingDisableShadowsInLod1,
                        Category.Tree => treeDisableShadowsInLod1,
                        Category.Prop => propDisableShadowsInLod1,
                        Category.Landmark => landmarkDisableShadowsInLod1,
                        _ => true
                    };

                    bool excludeDetails = (cat == Category.Building) && buildingExcludeSmallDetailsInLod1;

                    ApplyLODToGameObject(obj, lod0, cull, disableShadows, excludeDetails);

                    switch (cat)
                    {
                        case Category.Building: buildingCount++; break;
                        case Category.Tree: treeCount++; break;
                        case Category.Prop: propCount++; break;
                        case Category.Landmark: landmarkCount++; break;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Undo.CollapseUndoOperations(undoGroup);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }

            int total = buildingCount + treeCount + propCount + landmarkCount;
            EditorUtility.DisplayDialog("LOD Kurulumu Tamamlandı",
                $"Toplam {total} objeye güvenli LODGroup entegre edildi:\n\n" +
                $"• Binalar: {buildingCount}\n" +
                $"• Ağaçlar: {treeCount}\n" +
                $"• Proplar: {propCount}\n" +
                $"• Simgesel Yapılar: {landmarkCount}\n\n" +
                $"🛡️ Yollar ve zeminler korunarak LOD dışı bırakıldı.", "Harika");
        }

        private void ApplyLODToGameObject(GameObject rootObj, float lod0Transition, float cullingThreshold, bool disableShadowsInLod1, bool excludeDetailsInLod1)
        {
            Renderer[] allRenderers = rootObj.GetComponentsInChildren<Renderer>(true);
            if (allRenderers.Length == 0) return;

            LODGroup lodGroup = rootObj.GetComponent<LODGroup>();
            if (lodGroup == null)
            {
                lodGroup = Undo.AddComponent<LODGroup>(rootObj);
            }
            else
            {
                Undo.RecordObject(lodGroup, "Update LODGroup");
            }

            List<Renderer> lod0Renderers = new List<Renderer>();
            List<Renderer> lod1Renderers = new List<Renderer>();

            foreach (Renderer r in allRenderers)
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                if (IsExcludedFromOptimization(r.transform)) continue;

                lod0Renderers.Add(r);

                if (excludeDetailsInLod1)
                {
                    if (r.bounds.size.magnitude < 1.4f && r.gameObject != rootObj)
                    {
                        continue;
                    }
                }

                lod1Renderers.Add(r);
            }

            LOD[] lods;
            if (disableShadowsInLod1)
            {
                lods = new LOD[2];
                lods[0] = new LOD(lod0Transition, lod0Renderers.ToArray());
                lods[1] = new LOD(cullingThreshold, lod1Renderers.ToArray());
            }
            else
            {
                lods = new LOD[1];
                lods[0] = new LOD(cullingThreshold, lod0Renderers.ToArray());
            }

            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();
        }

        private void RemoveAllLODGroupsOnTarget()
        {
            if (!EditorUtility.DisplayDialog("LODGroup Temizleme", "Hedefteki tüm LODGroup bileşenleri kaldırılacak. Devam edilsin mi?", "Evet, Kaldır", "İptal"))
            {
                return;
            }

            Transform root = targetCityRoot != null ? targetCityRoot : null;
            LODGroup[] groups = root != null ? root.GetComponentsInChildren<LODGroup>(true) : FindObjectsByType<LODGroup>(FindObjectsSortMode.None);

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Remove All LODGroups");
            int undoGroup = Undo.GetCurrentGroup();

            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] != null)
                {
                    Undo.DestroyObjectImmediate(groups[i]);
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Temizleme Tamamlandı", $"{groups.Length} adet LODGroup bileşeni kaldırıldı.", "Tamam");
        }

        // =========================================================================
        // 2. WORLD CHUNKING TAB
        // =========================================================================
        private void DrawWorldChunkingTab()
        {
            EditorGUILayout.LabelField("2. Mekansal Izgara (World Chunking) ve Akış", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Binaları ve çevre detaylarını 2D grid ızgaralarına (WorldChunk) böler.\n" +
                "🛡️ Yollar, zeminler ve ana altyapı chunk'lara dahil edilmez, daima sahnede sabit kalır.", MessageType.Info);

            GUILayout.Space(8);

            chunkSize = EditorGUILayout.Slider("Chunk Boyutu (Metre):", chunkSize, 32f, 128f);
            nearRingDistance = EditorGUILayout.FloatField("Near Ring Mesafesi (m):", nearRingDistance);
            midRingDistance = EditorGUILayout.FloatField("Mid Ring Mesafesi (m):", midRingDistance);
            farRingDistance = EditorGUILayout.FloatField("Far Ring Mesafesi (m):", farRingDistance);
            enableHardRadiusCulling = EditorGUILayout.Toggle("Hard Radius Culling Açık", enableHardRadiusCulling);
            rendererCullingPadding = EditorGUILayout.FloatField("Radius Padding (m):", rendererCullingPadding);

            chunkContainerParent = (Transform)EditorGUILayout.ObjectField("Chunk Kök Objesi:", chunkContainerParent, typeof(Transform), true);

            GUILayout.Space(14);
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.3f);
            if (GUILayout.Button("📦 Binaları Chunk'lara Böl (Auto-Partition)", GUILayout.Height(38)))
            {
                PartitionCityIntoChunks();
            }

            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
            if (GUILayout.Button("🔄 WorldChunkManager'ı Güncelle / Ekle", GUILayout.Height(38)))
            {
                EnsureAndConfigureWorldChunkManager();
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);
            if (GUILayout.Button("⚠️ Chunk Hiyerarşisini Geri Çöz (Flatten Chunks)", GUILayout.Height(28)))
            {
                FlattenChunks();
            }
        }

        private void PartitionCityIntoChunks()
        {
            List<GameObject> objectsToChunk = CollectCandidateRoots();
            if (objectsToChunk.Count == 0)
            {
                EditorUtility.DisplayDialog("Chunking Hatası", "Chunk'lanacak bina/obje bulunamadı!", "Tamam");
                return;
            }

            if (!EditorUtility.DisplayDialog("Şehri Chunk'lara Bölme",
                $"Toplam {objectsToChunk.Count} obje {chunkSize}x{chunkSize}m boyutunda ızgaralara bölünecek.\n" +
                "Yollar ve zeminler korunarak bu işlemden muaf tutulacaktır. Devam edilsin mi?", "Evet, Böl", "İptal"))
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Partition City Into Chunks");
            int undoGroup = Undo.GetCurrentGroup();

            Transform rootChunksTransform = chunkContainerParent;
            if (rootChunksTransform == null)
            {
                GameObject rootGo = GameObject.Find(WorldChunksRootName);
                if (rootGo == null)
                {
                    rootGo = new GameObject(WorldChunksRootName);
                    Undo.RegisterCreatedObjectUndo(rootGo, "Create WorldChunks Root");
                }
                rootChunksTransform = rootGo.transform;
            }

            Dictionary<Vector2Int, WorldChunk> createdChunks = new Dictionary<Vector2Int, WorldChunk>();

            try
            {
                for (int i = 0; i < objectsToChunk.Count; i++)
                {
                    GameObject obj = objectsToChunk[i];
                    if (obj == null) continue;
                    if (IsExcludedFromOptimization(obj.transform)) continue;

                    EditorUtility.DisplayProgressBar("Chunking İşlemi", $"Objeler yerleştiriliyor ({i + 1}/{objectsToChunk.Count})", (float)i / objectsToChunk.Count);

                    Vector3 pos = obj.transform.position;
                    Vector2Int coord = new Vector2Int(
                        Mathf.FloorToInt(pos.x / chunkSize),
                        Mathf.FloorToInt(pos.z / chunkSize)
                    );

                    if (!createdChunks.TryGetValue(coord, out WorldChunk chunk))
                    {
                        string chunkName = $"Chunk_{coord.x}_{coord.y}";
                        Transform existingChunkTr = rootChunksTransform.Find(chunkName);
                        GameObject chunkGo;

                        if (existingChunkTr != null)
                        {
                            chunkGo = existingChunkTr.gameObject;
                            chunk = chunkGo.GetComponent<WorldChunk>();
                        }
                        else
                        {
                            chunkGo = new GameObject(chunkName);
                            chunkGo.transform.parent = rootChunksTransform;
                            chunkGo.transform.position = new Vector3(coord.x * chunkSize, 0f, coord.y * chunkSize);
                            Undo.RegisterCreatedObjectUndo(chunkGo, "Create Chunk Object");
                            chunk = chunkGo.AddComponent<WorldChunk>();
                        }

                        chunk.gridPosition = coord;
                        chunk.chunkSize = chunkSize;
                        chunk.currentState = WorldChunk.ChunkState.LoadedFull;

                        Transform fullDetailTr = chunkGo.transform.Find("FullDetail");
                        if (fullDetailTr == null)
                        {
                            GameObject fullDetailGo = new GameObject("FullDetail");
                            fullDetailGo.transform.parent = chunkGo.transform;
                            fullDetailGo.transform.localPosition = Vector3.zero;
                            Undo.RegisterCreatedObjectUndo(fullDetailGo, "Create FullDetail");
                            fullDetailTr = fullDetailGo.transform;
                        }
                        chunk.fullDetailContent = fullDetailTr.gameObject;

                        createdChunks[coord] = chunk;
                    }

                    Undo.SetTransformParent(obj.transform, chunk.fullDetailContent.transform, "Move Object to Chunk FullDetail");
                }

                EnsureAndConfigureWorldChunkManager();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Undo.CollapseUndoOperations(undoGroup);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }

            EditorUtility.DisplayDialog("Chunking Başarılı",
                $"Toplam {objectsToChunk.Count} obje, {createdChunks.Count} adet mekansal chunk içerisine yerleştirildi!\n" +
                $"WorldChunkManager optimize mesafelerle güncellendi.", "Tamam");
        }

        private void EnsureAndConfigureWorldChunkManager()
        {
            WorldChunkManager manager = FindAnyObjectByType<WorldChunkManager>();
            if (manager == null)
            {
                GameObject managerGo = new GameObject("[Optimization] WorldChunkManager");
                Undo.RegisterCreatedObjectUndo(managerGo, "Create WorldChunkManager");
                manager = managerGo.AddComponent<WorldChunkManager>();
            }
            else
            {
                Undo.RecordObject(manager, "Configure WorldChunkManager");
            }

            manager.chunkSize = chunkSize;
            manager.nearRingDistance = nearRingDistance;
            manager.midRingDistance = midRingDistance;
            manager.farRingDistance = farRingDistance;
            manager.enableHardRadiusCulling = enableHardRadiusCulling;
            manager.rendererCullingPadding = rendererCullingPadding;
            manager.autoDetectChunks = true;
            manager.autoFindPlayer = true;
            manager.DiscoverChunks();

            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[CityOptimizationStudio] WorldChunkManager configured with Near:{nearRingDistance}m, Mid:{midRingDistance}m, Far:{farRingDistance}m");
        }

        private void FlattenChunks()
        {
            GameObject rootGo = GameObject.Find(WorldChunksRootName);
            if (rootGo == null)
            {
                EditorUtility.DisplayDialog("Bilgi", "Sahnede 'WorldChunks' kök objesi bulunamadı.", "Tamam");
                return;
            }

            if (!EditorUtility.DisplayDialog("Geri Çözme", "WorldChunks altındaki tüm objeler kök seviyeye taşınacak ve chunk klasörleri silinecek. Emin misiniz?", "Evet, Çöz", "İptal"))
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Flatten World Chunks");
            int undoGroup = Undo.GetCurrentGroup();

            Transform defaultParent = targetCityRoot != null ? targetCityRoot : null;
            WorldChunk[] allChunks = rootGo.GetComponentsInChildren<WorldChunk>(true);

            foreach (WorldChunk chunk in allChunks)
            {
                if (chunk.fullDetailContent != null)
                {
                    Transform fullDetailTr = chunk.fullDetailContent.transform;
                    while (fullDetailTr.childCount > 0)
                    {
                        Transform child = fullDetailTr.GetChild(0);
                        Undo.SetTransformParent(child, defaultParent, "Unparent From Chunk");
                    }
                }
            }

            Undo.DestroyObjectImmediate(rootGo);
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("İşlem Tamam", "Chunk hiyerarşisi çözüldü ve objeler taşındı.", "Tamam");
        }

        // =========================================================================
        // 3. HLOD & MESH COMBINER TAB
        // =========================================================================
        private void DrawHlodCombinerTab()
        {
            EditorGUILayout.LabelField("3. Chunk HLOD & Mesh Birleştirme (Draw Call Reducer)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Orta mesafedeki (Mid Ring, 85-160m) chunk'lar için onlarca binayı tek tek çizmek yerine, her chunk'ın binalarını tek bir hafif Proxy Mesh'te birleştirir.\n" +
                "Bu sayede bir chunk'taki 40 draw call, SADECE 1 DRAW CALL'a iner!", MessageType.Info);

            GUILayout.Space(8);
            combineByMaterial = EditorGUILayout.Toggle("Materyale Göre Birleştir (Önerilen)", combineByMaterial);
            removeCollidersOnProxy = EditorGUILayout.Toggle("Proxy Mesh'te Collider'ları Kaldır", removeCollidersOnProxy);

            GUILayout.Space(14);
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.3f);
            if (GUILayout.Button("✨ Tüm Chunk'lar İçin HLOD Proxy Üret", GUILayout.Height(38)))
            {
                GenerateAllChunkProxies();
            }

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("🗑️ HLOD Proxy'leri Temizle", GUILayout.Height(38), GUILayout.Width(170)))
            {
                ClearAllChunkProxies();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        private void GenerateAllChunkProxies()
        {
            WorldChunk[] chunks = FindObjectsByType<WorldChunk>(FindObjectsSortMode.None);
            if (chunks.Length == 0)
            {
                EditorUtility.DisplayDialog("HLOD Hatası", "Sahnede WorldChunk bulunamadı! Önce 'Mekansal Izgara' sekmesinden şehri chunk'lara bölün.", "Tamam");
                return;
            }

            if (!Directory.Exists(GeneratedMeshesFolder))
            {
                Directory.CreateDirectory(GeneratedMeshesFolder);
                AssetDatabase.Refresh();
            }

            int generatedCount = 0;
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Generate Chunk HLOD Proxies");
            int undoGroup = Undo.GetCurrentGroup();

            try
            {
                for (int i = 0; i < chunks.Length; i++)
                {
                    WorldChunk chunk = chunks[i];
                    if (chunk == null || chunk.fullDetailContent == null) continue;

                    EditorUtility.DisplayProgressBar("HLOD Üretimi", $"Proxy üretiliyor: {chunk.gameObject.name} ({i + 1}/{chunks.Length})", (float)i / chunks.Length);

                    MeshFilter[] meshFilters = chunk.fullDetailContent.GetComponentsInChildren<MeshFilter>(true);
                    if (meshFilters.Length == 0) continue;

                    GameObject proxyGo = CombineChunkMeshes(chunk, meshFilters);
                    if (proxyGo != null)
                    {
                        chunk.proxyContent = proxyGo;
                        proxyGo.SetActive(false);
                        EditorUtility.SetDirty(chunk);
                        generatedCount++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(undoGroup);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }

            EditorUtility.DisplayDialog("HLOD Üretimi Tamamlandı", $"{generatedCount} adet chunk için birleşik HLOD proxy mesh'i üretildi ve bağlandı!", "Harika");
        }

        private GameObject CombineChunkMeshes(WorldChunk chunk, MeshFilter[] meshFilters)
        {
            Dictionary<Material, List<CombineInstance>> materialToCombines = new Dictionary<Material, List<CombineInstance>>();
            Transform chunkTransform = chunk.transform;

            foreach (MeshFilter mf in meshFilters)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                if (IsExcludedFromOptimization(mf.transform)) continue;

                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled) continue;

                Material mat = mr.sharedMaterial;
                if (mat == null) continue;

                if (!materialToCombines.ContainsKey(mat))
                {
                    materialToCombines[mat] = new List<CombineInstance>();
                }

                CombineInstance ci = new CombineInstance
                {
                    mesh = mf.sharedMesh,
                    transform = chunkTransform.worldToLocalMatrix * mf.transform.localToWorldMatrix
                };
                materialToCombines[mat].Add(ci);
            }

            if (materialToCombines.Count == 0) return null;

            Transform existingProxyTr = chunk.transform.Find("Proxy");
            GameObject proxyGo;
            if (existingProxyTr != null)
            {
                proxyGo = existingProxyTr.gameObject;
            }
            else
            {
                proxyGo = new GameObject("Proxy");
                proxyGo.transform.parent = chunk.transform;
                proxyGo.transform.localPosition = Vector3.zero;
                proxyGo.transform.localRotation = Quaternion.identity;
                proxyGo.transform.localScale = Vector3.one;
                Undo.RegisterCreatedObjectUndo(proxyGo, "Create Proxy GameObject");
            }

            List<CombineInstance> submeshCombines = new List<CombineInstance>();
            List<Material> finalMaterials = new List<Material>();

            foreach (var kvp in materialToCombines)
            {
                Mesh subMesh = new Mesh();
                int vCount = 0;
                foreach (var ci in kvp.Value)
                {
                    if (ci.mesh != null) vCount += ci.mesh.vertexCount;
                }
                if (vCount > 65535)
                {
                    subMesh.indexFormat = IndexFormat.UInt32;
                }

                subMesh.CombineMeshes(kvp.Value.ToArray(), true, true);

                CombineInstance subCi = new CombineInstance
                {
                    mesh = subMesh,
                    transform = Matrix4x4.identity
                };
                submeshCombines.Add(subCi);
                finalMaterials.Add(kvp.Key);
            }

            Mesh finalCombinedMesh = new Mesh
            {
                name = $"Mesh_HLOD_{chunk.gameObject.name}"
            };

            int totalVertices = 0;
            foreach (var sc in submeshCombines)
            {
                if (sc.mesh != null) totalVertices += sc.mesh.vertexCount;
            }
            if (totalVertices > 65535)
            {
                finalCombinedMesh.indexFormat = IndexFormat.UInt32;
            }

            finalCombinedMesh.CombineMeshes(submeshCombines.ToArray(), false, false);
            finalCombinedMesh.RecalculateBounds();
            finalCombinedMesh.RecalculateNormals();
            finalCombinedMesh.Optimize();

            string assetPath = $"{GeneratedMeshesFolder}/{finalCombinedMesh.name}_{chunk.gridPosition.x}_{chunk.gridPosition.y}.asset";
            if (File.Exists(assetPath))
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
            AssetDatabase.CreateAsset(finalCombinedMesh, assetPath);

            MeshFilter proxyMf = proxyGo.GetComponent<MeshFilter>();
            if (proxyMf == null) proxyMf = proxyGo.AddComponent<MeshFilter>();
            proxyMf.sharedMesh = finalCombinedMesh;

            MeshRenderer proxyMr = proxyGo.GetComponent<MeshRenderer>();
            if (proxyMr == null) proxyMr = proxyGo.AddComponent<MeshRenderer>();
            proxyMr.sharedMaterials = finalMaterials.ToArray();
            proxyMr.shadowCastingMode = ShadowCastingMode.Off;

            return proxyGo;
        }

        private void ClearAllChunkProxies()
        {
            WorldChunk[] chunks = FindObjectsByType<WorldChunk>(FindObjectsSortMode.None);
            int clearedCount = 0;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Clear Chunk Proxies");
            int undoGroup = Undo.GetCurrentGroup();

            foreach (WorldChunk chunk in chunks)
            {
                if (chunk == null) continue;
                Transform proxyTr = chunk.transform.Find("Proxy");
                if (proxyTr != null)
                {
                    Undo.DestroyObjectImmediate(proxyTr.gameObject);
                    chunk.proxyContent = null;
                    EditorUtility.SetDirty(chunk);
                    clearedCount++;
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Temizlendi", $"{clearedCount} adet proxy objesi silindi.", "Tamam");
        }

        // =========================================================================
        // 4. LAYER & CAMERA CULLING TAB
        // =========================================================================
        private void DrawLayerCullingTab()
        {
            EditorGUILayout.LabelField("4. Kamera Katman (Layer) Culling Ayarları", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Unity'nin Camera.layerCullDistances özelliği, belirli katmanlardaki objeleri tanımlanan mesafeden sonra C++ seviyesinde sıfır CPU maliyetiyle çizimden kaldırır.\n" +
                "🛡️ Road katmanı her zaman tam mesafede tutulur ve asla gizlenmez.", MessageType.Info);

            GUILayout.Space(8);
            propsCullingDistance = EditorGUILayout.Slider("Props / Küçük Eşyalar (m):", propsCullingDistance, 30f, 120f);
            natureCullingDistance = EditorGUILayout.Slider("Ağaçlar ve Doğa (m):", natureCullingDistance, 60f, 250f);
            buildingsCullingDistance = EditorGUILayout.Slider("Normal Binalar (m):", buildingsCullingDistance, 150f, 600f);
            defaultCullingDistance = EditorGUILayout.Slider("Varsayılan / Gökyüzü / Yollar (m):", defaultCullingDistance, 500f, 2000f);

            GUILayout.Space(14);
            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.3f);
            if (GUILayout.Button("📷 Kamera Culling Mesafelerini Sahneye Uygula", GUILayout.Height(38)))
            {
                ApplyLayerCullingToSceneCameras();
            }
            GUI.backgroundColor = Color.white;
        }

        private void ApplyLayerCullingToSceneCameras()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                mainCam = FindAnyObjectByType<Camera>();
            }

            if (mainCam == null)
            {
                EditorUtility.DisplayDialog("Hata", "Sahnede kamera bulunamadı!", "Tamam");
                return;
            }

            Undo.RecordObject(mainCam, "Apply Layer Cull Distances");

            float[] distances = new float[32];
            for (int i = 0; i < 32; i++)
            {
                distances[i] = defaultCullingDistance;
            }

            SetLayerDistance(distances, "Props", propsCullingDistance);
            SetLayerDistance(distances, "Nature", natureCullingDistance);
            SetLayerDistance(distances, "Trees", natureCullingDistance);
            SetLayerDistance(distances, "Buildings", buildingsCullingDistance);
            SetLayerDistance(distances, "MiniMapBuilding", buildingsCullingDistance);
            SetLayerDistance(distances, "Road", defaultCullingDistance); // Always full distance for roads!

            mainCam.layerCullDistances = distances;
            EditorUtility.SetDirty(mainCam);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog("Başarılı",
                $"Kamera Layer Culling mesafeleri uygulandı!\n\n" +
                $"• Props: {propsCullingDistance}m\n" +
                $"• Doğa: {natureCullingDistance}m\n" +
                $"• Binalar: {buildingsCullingDistance}m\n" +
                $"• Yollar & Varsayılan: {defaultCullingDistance}m", "Tamam");
        }

        private static void SetLayerDistance(float[] distances, string layerName, float distance)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0 && layer < 32)
            {
                distances[layer] = distance;
            }
        }

        // =========================================================================
        // 5. OCCLUSION & FRUSTUM CULLING TAB
        // =========================================================================
        private void DrawOcclusionCullingTab()
        {
            EditorGUILayout.LabelField("5. Kameranın Görmediği Yerleri Gizleme (Occlusion & Frustum Culling)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Bu modül, kameranın görmediği alanların render edilmesini donanım ve kod seviyesinde engeller:\n\n" +
                "1. Occlusion Culling: Kameranın baktığı yönde olmasına rağmen önündeki binaların ARKASINDA kalan tüm objeleri (binalar, ağaçlar, proplar) çizmez.\n" +
                "2. Frustum Culling: Kameranın bakış yönünün DIŞINDA veya ARKASINDA kalan chunk'ları ve NPC araçlarını çizimden kaldırır.\n" +
                "3. Gölge Mesafesi: Uzakta ve arkada kalan nesnelerin gereksiz gölge hesaplamasını önler.", MessageType.Info);

            GUILayout.Space(8);

            // Section 1: Occlusion Culling
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🏢 1. Occlusion Culling (Binaların Arkasını Gizleme)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Öndeki büyük binalar, arkalarındaki onlarca binayı kapatır. Bu sistem arkada kalanları Unity'nin Umbra motoruyla gizler.", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(4);

            if (StaticOcclusionCulling.isRunning)
            {
                EditorGUILayout.HelpBox("⚠️ Occlusion Culling şu anda hesaplanıyor... Lütfen bekleyin veya gerekirse aşağıdaki butonla iptal edin.", MessageType.Warning);
                GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
                if (GUILayout.Button("❌ Devam Eden Hesaplamayı İptal Et (Cancel Bake)", GUILayout.Height(30)))
                {
                    StaticOcclusionCulling.Cancel();
                }
                GUI.backgroundColor = Color.white;
                GUILayout.Space(6);
            }

            smallestOccluder = EditorGUILayout.Slider("En Küçük Engel (Smallest Occluder, m):", smallestOccluder, 2f, 20f);
            smallestHole = EditorGUILayout.Slider("En Küçük Delik (Smallest Hole, m):", smallestHole, 1f, 6f);
            backfaceThreshold = EditorGUILayout.Slider("Backface Eşiği (%):", backfaceThreshold, 50f, 100f);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("⚡ Hızlı Şehir Şablonu (8m / 2.5m - Önerilen)", EditorStyles.miniButton))
            {
                smallestOccluder = 8.0f;
                smallestHole = 2.5f;
            }
            if (GUILayout.Button("🔍 Dengeli Şablon (6m / 1.8m)", EditorStyles.miniButton))
            {
                smallestOccluder = 6.0f;
                smallestHole = 1.8f;
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
            if (GUILayout.Button("1. Binaları Statik İşaretle (Mark Occluders)", GUILayout.Height(34)))
            {
                MarkObjectsForOcclusion();
            }

            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.3f);
            if (GUILayout.Button("2. Occlusion Culling Bake Et (Hesapla)", GUILayout.Height(34)))
            {
                BakeOcclusionCulling();
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);
            if (GUILayout.Button("Occlusion Verisini Temizle (Clear Occlusion Data)", GUILayout.Height(24)))
            {
                ClearOcclusionCulling();
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(8);

            // Section 2: Frustum Culling
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📐 2. Kamera Frustum Culling (Kameranın Arkasını & Açısını Kapatma)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Kameranın görüş açısının dışında ve arkasında kalan mekanları ve NPC araçlarını anında devre dışı bırakır.", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(4);

            enableWorldChunkFrustumCulling = EditorGUILayout.Toggle("Frustum Culling Açık", enableWorldChunkFrustumCulling);
            frustumSafetyRadius = EditorGUILayout.Slider("Araba Çevresi Güvenlik Mesafesi (m):", frustumSafetyRadius, 15f, 60f);

            GUILayout.Space(6);
            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.3f);
            if (GUILayout.Button("Frustum Culling Ayarlarını WorldChunkManager'a Uygula", GUILayout.Height(32)))
            {
                ApplyFrustumCullingToManager();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();

            GUILayout.Space(8);

            // Section 3: Shadow Distance
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🌑 3. Optimize Gölge Mesafesi (Shadow Distance)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Şu anki Gölge Mesafesi: {QualitySettings.shadowDistance:F0} metre", EditorStyles.miniLabel);
            recommendedShadowDistance = EditorGUILayout.Slider("Yeni Gölge Mesafesi (m):", recommendedShadowDistance, 20f, 120f);

            GUILayout.Space(6);
            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.3f);
            if (GUILayout.Button("Gölge Mesafesini Uygula", GUILayout.Height(32)))
            {
                QualitySettings.shadowDistance = recommendedShadowDistance;
                EditorUtility.DisplayDialog("Gölge Ayarı", $"Gölge mesafesi {recommendedShadowDistance}m olarak ayarlandı. Uzaktaki veya görüş dışındaki alanların gereksiz gölge çizim yükü kaldırıldı.", "Tamam");
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();
        }

        private void MarkObjectsForOcclusion()
        {
            MeshRenderer[] allRenderers = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (allRenderers.Length == 0)
            {
                EditorUtility.DisplayDialog("Hata", "Sahnede hiçbir MeshRenderer bulunamadı!", "Tamam");
                return;
            }

            int occluderCount = 0;
            int occludeeCount = 0;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Mark Objects For Occlusion");
            int undoGroup = Undo.GetCurrentGroup();

            HashSet<GameObject> processedObjects = new HashSet<GameObject>();

            try
            {
                for (int i = 0; i < allRenderers.Length; i++)
                {
                    MeshRenderer mr = allRenderers[i];
                    if (mr == null) continue;
                    Transform tr = mr.transform;

                    if (IsExcludedFromOptimization(tr)) continue;

                    GameObject go = mr.gameObject;
                    if (!processedObjects.Add(go)) continue;

                    EditorUtility.DisplayProgressBar("Statik İşaretleme", $"İşleniyor: {go.name} ({i + 1}/{allRenderers.Length})", (float)i / allRenderers.Length);

                    Category cat = CategorizeGameObject(go);
                    StaticEditorFlags currentFlags = GameObjectUtility.GetStaticEditorFlags(go);

                    if (cat == Category.Building || cat == Category.Landmark)
                    {
                        StaticEditorFlags newFlags = currentFlags | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
                        Undo.RecordObject(go, "Set Static Flags");
                        GameObjectUtility.SetStaticEditorFlags(go, newFlags);
                        occluderCount++;
                    }
                    else if (cat == Category.Tree || cat == Category.Prop)
                    {
                        StaticEditorFlags newFlags = currentFlags | StaticEditorFlags.OccludeeStatic;
                        Undo.RecordObject(go, "Set Static Flags");
                        GameObjectUtility.SetStaticEditorFlags(go, newFlags);
                        occludeeCount++;
                    }
                    else
                    {
                        StaticEditorFlags newFlags = currentFlags | StaticEditorFlags.OccludeeStatic;
                        if (mr.bounds.size.magnitude > 4.5f)
                        {
                            newFlags |= StaticEditorFlags.OccluderStatic;
                            occluderCount++;
                        }
                        else
                        {
                            occludeeCount++;
                        }
                        Undo.RecordObject(go, "Set Static Flags");
                        GameObjectUtility.SetStaticEditorFlags(go, newFlags);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Undo.CollapseUndoOperations(undoGroup);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }

            EditorUtility.DisplayDialog("Statik İşaretleme Tamamlandı",
                $"Objeler Occlusion Culling için başarıyla hazırlandı:\n\n" +
                $"• Occluder (Görüş Kesen Bina/Yapı): {occluderCount}\n" +
                $"• Occludee (Gizlenebilen Ağaç/Prop): {occludeeCount}\n\n" +
                $"Şimdi '2. Occlusion Culling Bake Et' butonuna basarak hesaplamayı başlatabilirsiniz!", "Harika");
        }

        private static void SetStaticFlagsRecursive(GameObject go, StaticEditorFlags flags)
        {
            Undo.RecordObject(go, "Set Static Flags");
            GameObjectUtility.SetStaticEditorFlags(go, flags);
            for (int i = 0; i < go.transform.childCount; i++)
            {
                SetStaticFlagsRecursive(go.transform.GetChild(i).gameObject, flags);
            }
        }

        private void BakeOcclusionCulling()
        {
            if (StaticOcclusionCulling.isRunning)
            {
                if (EditorUtility.DisplayDialog("Bake Zaten Çalışıyor", "Occlusion Culling hesaplaması şu anda devam ediyor. İptal etmek ister misiniz?", "İptal Et", "Beklemeye Devam Et"))
                {
                    StaticOcclusionCulling.Cancel();
                }
                return;
            }

            if (!EditorUtility.DisplayDialog("Occlusion Bake",
                $"Occlusion Culling şu optimize parametrelerle hesaplanacak:\n\n" +
                $"• En Küçük Engel (Binalar): {smallestOccluder}m\n" +
                $"• En Küçük Delik (Sokaklar): {smallestHole}m\n\n" +
                $"Otomatik OcclusionArea şehre göre sınırlandırılacaktır.\n" +
                $"Unity işlem sırasında 10-30 saniye meşgul görünebilir. Başlatılsın mı?", "Evet, Başlat", "İptal"))
            {
                return;
            }

            EnsureCityOcclusionArea();

            StaticOcclusionCulling.smallestOccluder = smallestOccluder;
            StaticOcclusionCulling.smallestHole = smallestHole;
            StaticOcclusionCulling.backfaceThreshold = backfaceThreshold;

            StaticOcclusionCulling.Compute();
        }

        private void EnsureCityOcclusionArea()
        {
            OcclusionArea area = FindAnyObjectByType<OcclusionArea>();
            MeshRenderer[] renderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            if (renderers.Length == 0) return;

            Bounds cityBounds = new Bounds();
            bool initialized = false;

            foreach (MeshRenderer mr in renderers)
            {
                if (mr == null || !mr.enabled) continue;
                if (IsExcludedFromOptimization(mr.transform)) continue;

                if (!initialized)
                {
                    cityBounds = mr.bounds;
                    initialized = true;
                }
                else
                {
                    cityBounds.Encapsulate(mr.bounds);
                }
            }

            if (!initialized) return;

            if (area == null)
            {
                GameObject areaGo = new GameObject("[Optimization] City_OcclusionArea");
                Undo.RegisterCreatedObjectUndo(areaGo, "Create Occlusion Area");
                area = areaGo.AddComponent<OcclusionArea>();
            }
            else
            {
                Undo.RecordObject(area, "Update Occlusion Area");
            }

            area.transform.position = cityBounds.center;
            area.size = cityBounds.size + new Vector3(40f, 40f, 40f);
            EditorUtility.SetDirty(area);
            Debug.Log($"[CityOptimizationStudio] OcclusionArea fitted to city bounds: Center {area.transform.position}, Size {area.size}");
        }

        private void ClearOcclusionCulling()
        {
            if (EditorUtility.DisplayDialog("Occlusion Temizleme", "Mevcut Occlusion Culling verisi silinecek. Emin misiniz?", "Evet, Sil", "İptal"))
            {
                StaticOcclusionCulling.Clear();
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorUtility.DisplayDialog("Temizlendi", "Occlusion Culling verisi silindi.", "Tamam");
            }
        }

        private void ApplyFrustumCullingToManager()
        {
            WorldChunkManager manager = FindAnyObjectByType<WorldChunkManager>();
            if (manager == null)
            {
                EnsureAndConfigureWorldChunkManager();
                manager = FindAnyObjectByType<WorldChunkManager>();
            }

            if (manager != null)
            {
                Undo.RecordObject(manager, "Configure Frustum Culling");
                manager.enableFrustumCulling = enableWorldChunkFrustumCulling;
                manager.frustumSafetyDistance = frustumSafetyRadius;
                EditorUtility.SetDirty(manager);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

                EditorUtility.DisplayDialog("Başarılı",
                    $"Frustum Culling ayarları WorldChunkManager'a uygulandı!\n\n" +
                    $"• Frustum Culling: {(enableWorldChunkFrustumCulling ? "AÇIK" : "KAPALI")}\n" +
                    $"• Güvenlik Yarıçapı: {frustumSafetyRadius}m\n\n" +
                    $"Artık kameranın arkasında veya bakış açısı dışında kalan uzak bölgeler ve araçlar render edilmeyecektir.", "Harika");
            }
        }

        // =========================================================================
        // 6. AUDIT & PRESETS TAB
        // =========================================================================
        private void DrawAuditAndPresetsTab()
        {
            EditorGUILayout.LabelField("6. Sahne Analizi ve 1-Tık Hazır Şablonlar", EditorStyles.boldLabel);

            GUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📊 Canlı Sahne Analizi", EditorStyles.boldLabel);

            if (GUILayout.Button("Sahneyi Tara ve Analiz Et", GUILayout.Height(28)))
            {
                RunSceneAudit();
            }

            if (auditCompleted)
            {
                GUILayout.Space(6);
                EditorGUILayout.LabelField($"• Toplam MeshRenderer Sayısı: {auditTotalRenderers:N0}");
                EditorGUILayout.LabelField($"• LODGroup Olan Obje: {auditWithLodCount:N0}");
                EditorGUILayout.LabelField($"• LODGroup Olmayan Obje: {auditWithoutLodCount:N0}");
                EditorGUILayout.LabelField($"• Toplam WorldChunk Sayısı: {auditTotalChunks:N0}");
                EditorGUILayout.LabelField($"• Yaklaşık Toplam Üçgen (Triangles): {auditTotalTriangles:N0}");

                if (auditWithoutLodCount > 50)
                {
                    EditorGUILayout.HelpBox($"Uyarı: Sahnede {auditWithoutLodCount} adet LODGroup bulunmayan bina/obje var.", MessageType.Warning);
                }
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(14);
            EditorGUILayout.LabelField("⚡ Hazır Optimizasyon Şablonları", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.6f);
            if (GUILayout.Button("📱 Mobil 60 FPS Şablonu\n(Agresif Culling, 64m Izgara)", GUILayout.Height(46)))
            {
                ApplyPresetMobile();
            }

            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
            if (GUILayout.Button("💻 Dengeli PC/Mobil Şablonu\n(Orta Mesafe, 96m Izgara)", GUILayout.Height(46)))
            {
                ApplyPresetBalanced();
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(12);
            GUI.backgroundColor = new Color(1f, 0.75f, 0.2f);
            if (GUILayout.Button("🚀 1-Tıkla Tüm Şehri Optimize Et (Master Optimization)", GUILayout.Height(44)))
            {
                ExecuteMasterOptimization();
            }
            GUI.backgroundColor = Color.white;
        }

        private void RunSceneAudit()
        {
            MeshRenderer[] renderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            auditTotalRenderers = renderers.Length;
            auditWithLodCount = 0;
            auditWithoutLodCount = 0;
            auditTotalTriangles = 0;

            HashSet<GameObject> countedRoots = new HashSet<GameObject>();

            foreach (MeshRenderer mr in renderers)
            {
                if (mr == null) continue;
                if (IsExcludedFromOptimization(mr.transform)) continue;

                LODGroup lod = mr.GetComponentInParent<LODGroup>();
                if (lod != null)
                {
                    if (countedRoots.Add(lod.gameObject))
                    {
                        auditWithLodCount++;
                    }
                }
                else
                {
                    auditWithoutLodCount++;
                }

                MeshFilter mf = mr.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    auditTotalTriangles += mf.sharedMesh.triangles.Length / 3;
                }
            }

            WorldChunk[] chunks = FindObjectsByType<WorldChunk>(FindObjectsSortMode.None);
            auditTotalChunks = chunks.Length;
            auditCompleted = true;
        }

        private void ApplyPresetMobile()
        {
            chunkSize = 64f;
            nearRingDistance = 75f;
            midRingDistance = 140f;
            farRingDistance = 180f;
            enableHardRadiusCulling = true;

            buildingLod0ScreenHeight = 0.28f;
            buildingLod1ScreenHeight = 0.020f;
            treeLod0ScreenHeight = 0.22f;
            treeLod1ScreenHeight = 0.030f;
            propLod0ScreenHeight = 0.12f;
            propLod1ScreenHeight = 0.050f;

            propsCullingDistance = 50f;
            natureCullingDistance = 110f;
            buildingsCullingDistance = 280f;

            EditorUtility.DisplayDialog("Şablon Uygulandı", "Mobil 60 FPS şablon parametreleri yüklendi.", "Tamam");
        }

        private void ApplyPresetBalanced()
        {
            chunkSize = 96f;
            nearRingDistance = 95f;
            midRingDistance = 180f;
            farRingDistance = 250f;
            enableHardRadiusCulling = true;

            buildingLod0ScreenHeight = 0.32f;
            buildingLod1ScreenHeight = 0.015f;
            treeLod0ScreenHeight = 0.26f;
            treeLod1ScreenHeight = 0.025f;
            propLod0ScreenHeight = 0.16f;
            propLod1ScreenHeight = 0.040f;

            propsCullingDistance = 75f;
            natureCullingDistance = 140f;
            buildingsCullingDistance = 380f;

            EditorUtility.DisplayDialog("Şablon Uygulandı", "Dengeli PC/Mobil şablon parametreleri yüklendi.", "Tamam");
        }

        private void ExecuteMasterOptimization()
        {
            if (!EditorUtility.DisplayDialog("Master Şehir Optimizasyonu",
                "Bu işlem sırasıyla:\n" +
                "1. Binalara, ağaçlara ve proplara güvenli LODGroup ekleyecek (Yollar ve zeminler KORUNACAK).\n" +
                "2. Binaları mekansal WorldChunk ızgaralarına bölecek.\n" +
                "3. WorldChunkManager'ı kurup optimize halka mesafelerini uygulayacak.\n" +
                "4. Kamera culling mesafelerini atayacak.\n\n" +
                "Devam etmek istiyor musunuz?", "Evet, Başlat", "İptal"))
            {
                return;
            }

            SetupLODGroupsOnTarget();
            PartitionCityIntoChunks();
            ApplyLayerCullingToSceneCameras();

            EditorUtility.DisplayDialog("Tebrikler!", "Tüm şehir optimizasyonu başarıyla tamamlandı! Yollar ve zeminler korunurken uzaktaki binalar optimize edildi.", "Harika");
        }

        // =========================================================================
        // ROAD RESCUE & RESTORATION
        // =========================================================================
        public static void RestoreAndFixRoads()
        {
            MeshRenderer[] allRenderers = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int lodRemovedCount = 0;
            int unparentedCount = 0;
            int enabledCount = 0;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Restore City Roads");
            int undoGroup = Undo.GetCurrentGroup();

            try
            {
                // Remove LODGroups directly attached to road objects
                LODGroup[] allLods = FindObjectsByType<LODGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (LODGroup lod in allLods)
                {
                    if (lod == null) continue;
                    if (IsExcludedFromOptimization(lod.transform))
                    {
                        Undo.DestroyObjectImmediate(lod);
                        lodRemovedCount++;
                    }
                }

                foreach (MeshRenderer mr in allRenderers)
                {
                    if (mr == null) continue;
                    Transform tr = mr.transform;

                    if (IsExcludedFromOptimization(tr))
                    {
                        // 1. Re-enable renderer
                        if (!mr.enabled)
                        {
                            Undo.RecordObject(mr, "Enable Road Renderer");
                            mr.enabled = true;
                            enabledCount++;
                        }

                        // 2. Ensure GameObject is active
                        if (!tr.gameObject.activeSelf)
                        {
                            Undo.RecordObject(tr.gameObject, "Activate Road Object");
                            tr.gameObject.SetActive(true);
                        }

                        // 3. Ensure parent hierarchy is active
                        Transform p = tr.parent;
                        while (p != null)
                        {
                            if (!p.gameObject.activeSelf)
                            {
                                Undo.RecordObject(p.gameObject, "Activate Road Parent");
                                p.gameObject.SetActive(true);
                            }
                            p = p.parent;
                        }

                        // 4. Remove any LODGroup on tr
                        LODGroup lod = tr.GetComponent<LODGroup>();
                        if (lod != null)
                        {
                            Undo.DestroyObjectImmediate(lod);
                            lodRemovedCount++;
                        }

                        // 5. If moved inside WorldChunks, rescue it back to a safe parent
                        WorldChunk chunk = tr.GetComponentInParent<WorldChunk>();
                        if (chunk != null)
                        {
                            Transform safeParent = FindAppropriateRoadParent(tr);
                            Undo.SetTransformParent(tr, safeParent, "Rescue Road from WorldChunk");
                            unparentedCount++;
                        }
                    }
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }

            EditorUtility.DisplayDialog("Yollar Başarıyla Kurtarıldı!",
                $"Yol restorasyon özeti:\n\n" +
                $"• Yollardan kaldırılan hatalı LODGroup: {lodRemovedCount}\n" +
                $"• Chunk'lardan kurtarılan yol objesi: {unparentedCount}\n" +
                $"• Yeniden aktif edilen renderer: {enabledCount}\n\n" +
                $"Yollar artık görünür ve kalıcı olarak korunuyor.", "Harika!");
        }

        private static Transform FindAppropriateRoadParent(Transform roadTr)
        {
            GameObject placedRoads = GameObject.Find("Placed Roads");
            if (placedRoads != null) return placedRoads.transform;

            GameObject roadsRoot = GameObject.Find("Roads");
            if (roadsRoot != null) return roadsRoot.transform;

            GameObject ankara = GameObject.Find("Ankara");
            if (ankara != null)
            {
                Transform roadsFolder = ankara.transform.Find("Roads");
                if (roadsFolder != null) return roadsFolder;
                return ankara.transform;
            }

            return null;
        }

        private static int CountRoadLodsInScene()
        {
            int count = 0;
            LODGroup[] allLods = FindObjectsByType<LODGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (LODGroup lod in allLods)
            {
                if (lod != null && IsExcludedFromOptimization(lod.transform))
                {
                    count++;
                }
            }
            return count;
        }

        // =========================================================================
        // HELPER FUNCTIONS & EXCLUSION RULES
        // =========================================================================
        private enum Category
        {
            Building,
            Tree,
            Prop,
            Landmark,
            Excluded
        }

        public static bool IsExcludedFromOptimization(Transform tr)
        {
            if (tr == null) return true;

            int roadLayer = LayerMask.NameToLayer("Road");
            int waterLayer = LayerMask.NameToLayer("Water");
            if (roadLayer != -1 && tr.gameObject.layer == roadLayer) return true;
            if (waterLayer != -1 && tr.gameObject.layer == waterLayer) return true;

            if (tr.GetComponent<Terrain>() != null || tr.GetComponentInParent<Terrain>() != null) return true;
            if (tr.GetComponent<TrafficSystem.Spline.SplineRoad>() != null || tr.GetComponentInParent<TrafficSystem.Spline.SplineRoad>() != null) return true;
            if (tr.GetComponent<TrafficSystem.Spline.RoadIntersection>() != null || tr.GetComponentInParent<TrafficSystem.Spline.RoadIntersection>() != null) return true;
            if (tr.GetComponent<CarController>() != null || tr.GetComponentInParent<CarController>() != null) return true;
            if (tr.GetComponent<TrafficSystem.NpcCarAgent>() != null || tr.GetComponentInParent<TrafficSystem.NpcCarAgent>() != null) return true;

            // If object itself is named like building, prop or nature -> NEVER exclude!
            string selfName = tr.name.ToLowerInvariant();
            if (selfName.Contains("building") ||
                selfName.Contains("house") ||
                selfName.Contains("shop") ||
                selfName.Contains("apart") ||
                selfName.Contains("store") ||
                selfName.Contains("tower") ||
                selfName.Contains("tree") ||
                selfName.Contains("nature") ||
                selfName.Contains("bush") ||
                selfName.Contains("plant") ||
                selfName.Contains("prop") ||
                selfName.Contains("bench") ||
                selfName.Contains("lamp") ||
                selfName.Contains("light") ||
                selfName.Contains("fence") ||
                selfName.Contains("sign") ||
                selfName.Contains("roof") ||
                selfName.Contains("facade") ||
                selfName.Contains("wall"))
            {
                return false;
            }

            Transform current = tr;
            while (current != null)
            {
                string nameLower = current.name.ToLowerInvariant();

                // "roadside" or "roadside buildings" is a building collection, NOT a road!
                if (nameLower.Contains("roadside"))
                {
                    current = current.parent;
                    continue;
                }

                // If under dedicated content folders, it is NOT a road!
                if (nameLower == "buildings" || nameLower == "trees" || nameLower == "props" || nameLower == "fulldetail")
                {
                    return false;
                }

                // Strict road and terrain indicators
                if (nameLower == "roads" ||
                    nameLower == "placed roads" ||
                    nameLower.StartsWith("road_") ||
                    nameLower.EndsWith("_road") ||
                    nameLower.Contains("asphalt") ||
                    nameLower.Contains("intersection") ||
                    nameLower.Contains("splineroad") ||
                    nameLower.Contains("sidewalk") ||
                    nameLower.Contains("lanemarking") ||
                    nameLower.Contains("lane marking") ||
                    nameLower.Contains("crosswalk") ||
                    nameLower == "terrain" ||
                    nameLower == "ground" ||
                    nameLower == "water" ||
                    nameLower.Contains("bridge_deck") ||
                    nameLower.Contains("curb"))
                {
                    return true;
                }

                current = current.parent;
            }

            Renderer r = tr.GetComponent<Renderer>();
            if (r != null && r.sharedMaterials != null)
            {
                foreach (Material mat in r.sharedMaterials)
                {
                    if (mat == null) continue;
                    string matName = mat.name.ToLowerInvariant();
                    if (matName.Contains("asphalt") ||
                        matName.Contains("lanemarking") ||
                        matName.Contains("lane marking") ||
                        matName == "road concrete tile" ||
                        matName == "terrain")
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private List<GameObject> CollectCandidateRoots()
        {
            List<GameObject> result = new List<GameObject>();
            HashSet<GameObject> visited = new HashSet<GameObject>();

            if (targetCityRoot != null)
            {
                CollectCandidatesRecursive(targetCityRoot, result, visited);
            }
            else
            {
                // Auto-collect from standard roots: "Generated Roadside Buildings", "WorldChunks", "Ankara", "Districts", "City", "Buildings"
                string[] commonRootNames = { "Generated Roadside Buildings", "WorldChunks", "Ankara", "Districts", "City", "Buildings" };
                bool foundAnyRoot = false;

                foreach (string rootName in commonRootNames)
                {
                    GameObject go = GameObject.Find(rootName);
                    if (go != null)
                    {
                        foundAnyRoot = true;
                        CollectCandidatesRecursive(go.transform, result, visited);
                    }
                }

                if (!foundAnyRoot || result.Count == 0)
                {
                    MeshRenderer[] renderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
                    foreach (MeshRenderer mr in renderers)
                    {
                        if (mr == null) continue;
                        if (IsExcludedFromOptimization(mr.transform)) continue;

                        Transform itemTr = mr.transform;
                        while (itemTr.parent != null &&
                               itemTr.parent.parent != null &&
                               itemTr.parent.name != "FullDetail" &&
                               itemTr.parent.name != "Buildings" &&
                               itemTr.parent.name != "Generated Roadside Buildings" &&
                               itemTr.parent.name != "Trees" &&
                               itemTr.parent.name != "Props")
                        {
                            itemTr = itemTr.parent;
                        }

                        if (visited.Add(itemTr.gameObject))
                        {
                            result.Add(itemTr.gameObject);
                        }
                    }
                }
            }

            return result;
        }

        private void CollectCandidatesRecursive(Transform tr, List<GameObject> result, HashSet<GameObject> visited)
        {
            if (tr == null) return;
            if (IsExcludedFromOptimization(tr)) return;

            MeshRenderer mr = tr.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                if (visited.Add(tr.gameObject))
                {
                    result.Add(tr.gameObject);
                }
                return;
            }

            for (int i = 0; i < tr.childCount; i++)
            {
                CollectCandidatesRecursive(tr.GetChild(i), result, visited);
            }
        }

        private Category CategorizeGameObject(GameObject go)
        {
            if (go == null || IsExcludedFromOptimization(go.transform))
            {
                return Category.Excluded;
            }

            string nameLower = go.name.ToLowerInvariant();

            foreach (string kw in LandmarkKeywords)
            {
                if (nameLower.Contains(kw)) return Category.Landmark;
            }

            foreach (string kw in TreeKeywords)
            {
                if (nameLower.Contains(kw)) return Category.Tree;
            }

            foreach (string kw in PropKeywords)
            {
                if (nameLower.Contains(kw)) return Category.Prop;
            }

            foreach (string kw in BuildingKeywords)
            {
                if (nameLower.Contains(kw)) return Category.Building;
            }

            Renderer r = go.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                float size = r.bounds.size.magnitude;
                if (size > 35f) return Category.Landmark;
                if (size < 3f) return Category.Prop;
            }

            return Category.Building;
        }
    }
}
#endif
