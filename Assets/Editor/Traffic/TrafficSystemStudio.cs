#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TrafficSystem;
using TrafficSystem.Spline;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryDriver.EditorTools
{
    /// <summary>
    /// Master Editor Studio for NPC Traffic System:
    /// 1. One-Click automatic setup of NpcSpawner, RoadNetworkManager, and all 9 NPC vehicle prefabs.
    /// 2. Profile configuration (Mobile 60 FPS vs PC Dense).
    /// 3. Traffic & Road Network diagnostic audit.
    /// 4. Live simulation controls and debug inspection.
    /// </summary>
    public class TrafficSystemStudio : EditorWindow
    {
        private const string PrefabsFolder = "Assets/Prefabs/NPCs";
        private const string SystemsRootName = "Systems";
        private const string TrafficSystemGameObjectName = "TrafficSystem";

        private enum StudioTab
        {
            SetupAndProfiles,
            DiagnosticsAndAudit,
            LiveSimulation
        }

        private StudioTab currentTab = StudioTab.SetupAndProfiles;
        private Vector2 scrollPos;

        [SerializeField] private NpcSpawner.TrafficDensityProfile selectedProfile = NpcSpawner.TrafficDensityProfile.MobileBalanced60Fps;

        [MenuItem("Tools/Delivery Driver/Traffic/Traffic Setup & Simulation Studio", false, 1)]
        public static void ShowWindow()
        {
            TrafficSystemStudio win = GetWindow<TrafficSystemStudio>("Traffic Studio");
            win.minSize = new Vector2(450f, 580f);
            win.Show();
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("🚦 Traffic Setup & Simulation Studio", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Bu araç NPC Trafik Sistemini yönetir, yapılandırır ve test eder:\n" +
                "• Şerit ve kavşak takibi (Pure Pursuit + Intersection Yielding)\n" +
                "• Öndeki aracı virajlarda dahi algılayan çoklu sensör ve ACC mesafe koruma\n" +
                "• Kamera dışı akıllı spawn/despawn (Streaming Ring Spawner)\n" +
                "• Mobil 60 FPS bütçesine uygun aktif araç yönetimi",
                MessageType.Info);

            EditorGUILayout.Space(10f);

            // Tab Bar
            currentTab = (StudioTab)GUILayout.Toolbar((int)currentTab, new[]
            {
                "⚙️ Kurulum & Profiller",
                "🔍 Teşhis & Denetim",
                "🎮 Canlı Simülasyon"
            }, GUILayout.Height(30f));

            EditorGUILayout.Space(12f);

            switch (currentTab)
            {
                case StudioTab.SetupAndProfiles:
                    DrawSetupAndProfilesTab();
                    break;
                case StudioTab.DiagnosticsAndAudit:
                    DrawDiagnosticsTab();
                    break;
                case StudioTab.LiveSimulation:
                    DrawLiveSimulationTab();
                    break;
            }

            EditorGUILayout.Space(12f);
            EditorGUILayout.EndScrollView();
        }

        #region Tab 1: Setup & Profiles

        private void DrawSetupAndProfilesTab()
        {
            EditorGUILayout.LabelField("1. Tek Tıkla Otomatik Kurulum", EditorStyles.boldLabel);

            GUI.backgroundColor = new Color(0.25f, 0.85f, 0.45f, 1f);
            if (GUILayout.Button("⚡ SETUP TRAFFIC SYSTEM NOW (TEK TIKLA KURULUM)", GUILayout.Height(44f)))
            {
                SetupTrafficSystemNow();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(14f);
            EditorGUILayout.LabelField("2. Trafik Yoğunluğu ve Performans Profili", EditorStyles.boldLabel);

            selectedProfile = (NpcSpawner.TrafficDensityProfile)EditorGUILayout.EnumPopup("Seçili Profil", selectedProfile);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            switch (selectedProfile)
            {
                case NpcSpawner.TrafficDensityProfile.MobileBalanced60Fps:
                    EditorGUILayout.LabelField("📱 Mobil Dengeli (60 FPS Hedefi)", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("• Aktif Araç: 18 araç");
                    EditorGUILayout.LabelField("• Spawn Halkası: 45m - 120m (Kamera Dışı)");
                    EditorGUILayout.LabelField("• Despawn Mesafesi: 160m (Arkadakiler sürmeye devam eder)");
                    EditorGUILayout.LabelField("• Fizik CPU Bütçesi: ~1.8 ms (60 FPS garantisi)");
                    break;
                case NpcSpawner.TrafficDensityProfile.MobileDense:
                    EditorGUILayout.LabelField("📱 Mobil Yoğun Trafik", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("• Aktif Araç: 22 araç");
                    EditorGUILayout.LabelField("• Spawn Halkası: 40m - 130m");
                    EditorGUILayout.LabelField("• Despawn Mesafesi: 170m");
                    break;
                case NpcSpawner.TrafficDensityProfile.PcStandard:
                    EditorGUILayout.LabelField("🖥️ PC Standart Yoğunluk", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("• Aktif Araç: 32 araç");
                    EditorGUILayout.LabelField("• Spawn Halkası: 45m - 160m");
                    EditorGUILayout.LabelField("• Despawn Mesafesi: 210m");
                    break;
                case NpcSpawner.TrafficDensityProfile.PcUltraDense:
                    EditorGUILayout.LabelField("🖥️ PC Ultra Yoğun Trafik", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("• Aktif Araç: 48 araç");
                    EditorGUILayout.LabelField("• Spawn Halkası: 40m - 200m");
                    EditorGUILayout.LabelField("• Despawn Mesafesi: 250m");
                    break;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("🎯 Bu Profili Sahnedeki Spawner'a Uygula", GUILayout.Height(30f)))
            {
                ApplyProfileToSceneSpawner();
            }

            EditorGUILayout.Space(14f);
            EditorGUILayout.LabelField("3. Prefab Yönetimi", EditorStyles.boldLabel);

            List<GameObject> foundPrefabs = LoadNpcPrefabsFromDisk();
            EditorGUILayout.LabelField($"• {PrefabsFolder} altında tespit edilen araç sayısı: {foundPrefabs.Count}");

            if (GUILayout.Button("🔄 Sahnedeki Spawner'a Prefabları Yeniden Bağla", GUILayout.Height(28f)))
            {
                BindPrefabsToSpawner(foundPrefabs);
            }
        }

        private void SetupTrafficSystemNow()
        {
            Scene scene = SceneManager.GetActiveScene();
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Setup Traffic System");

            // 1. Ensure RoadNetworkManager exists
            RoadNetworkManager rnm = Object.FindFirstObjectByType<RoadNetworkManager>();
            Transform systemsRoot = EnsureSceneRoot(scene, SystemsRootName);

            if (rnm == null)
            {
                GameObject rnmGo = new GameObject("RoadNetworkManager");
                Undo.RegisterCreatedObjectUndo(rnmGo, "Create RoadNetworkManager");
                rnmGo.transform.SetParent(systemsRoot, false);
                rnm = Undo.AddComponent<RoadNetworkManager>(rnmGo);
                rnm.RefreshNetwork();
                Debug.Log("[TrafficStudio] RoadNetworkManager oluşturuldu ve yol ağı yenilendi.");
            }
            else
            {
                if (rnm.transform.parent != systemsRoot)
                {
                    Undo.SetTransformParent(rnm.transform, systemsRoot, "Move RoadNetworkManager to Systems");
                }
                rnm.RefreshNetwork();
            }

            // 2. Ensure TrafficSystem GameObject and NpcSpawner
            Transform trafficSystemTr = systemsRoot.Find(TrafficSystemGameObjectName);
            if (trafficSystemTr == null)
            {
                GameObject tsGo = new GameObject(TrafficSystemGameObjectName);
                Undo.RegisterCreatedObjectUndo(tsGo, "Create TrafficSystem");
                tsGo.transform.SetParent(systemsRoot, false);
                trafficSystemTr = tsGo.transform;
            }

            NpcSpawner spawner = trafficSystemTr.GetComponent<NpcSpawner>();
            if (spawner == null)
            {
                spawner = Undo.AddComponent<NpcSpawner>(trafficSystemTr.gameObject);
                Debug.Log("[TrafficStudio] NpcSpawner bileşeni TrafficSystem altına eklendi.");
            }

            // 3. Load & Assign All 9 NPC Prefabs
            List<GameObject> prefabs = LoadNpcPrefabsFromDisk();
            if (prefabs.Count > 0)
            {
                Undo.RecordObject(spawner, "Assign NPC Prefabs");
                spawner.SetNpcPrefabs(prefabs.ToArray());
                Debug.Log($"[TrafficStudio] {prefabs.Count} adet NPC prefabı NpcSpawner'a başarıyla atandı.");
            }
            else
            {
                Debug.LogWarning($"[TrafficStudio] {PrefabsFolder} altında prefab bulunamadı!");
            }

            // 4. Apply Profile
            Undo.RecordObject(spawner, "Apply Density Profile");
            spawner.ApplyDensityProfile(selectedProfile);

            EditorSceneManager.MarkSceneDirty(scene);

            string msg = $"[TrafficStudio] NPC Trafik Sistemi Başarıyla Kuruldu!\n" +
                         $"• Yol Ağı: {rnm.roads.Count} yol, {rnm.intersections.Count} kavşak.\n" +
                         $"• Prefablar: {prefabs.Count} adet araç bağlandı.\n" +
                         $"• Profil: {selectedProfile} uygulandı.";
            Debug.Log(msg);
            EditorUtility.DisplayDialog("Trafik Sistemi Kuruldu", msg, "Harika");
        }

        private void ApplyProfileToSceneSpawner()
        {
            NpcSpawner spawner = Object.FindFirstObjectByType<NpcSpawner>();
            if (spawner == null)
            {
                EditorUtility.DisplayDialog("Spawner Bulunamadı", "Sahnede NpcSpawner bulunamadı! Önce 'Setup Traffic System Now' butonuna basın.", "Tamam");
                return;
            }

            Undo.RecordObject(spawner, "Apply Density Profile");
            spawner.ApplyDensityProfile(selectedProfile);
            EditorUtility.SetDirty(spawner);
            EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);

            Debug.Log($"[TrafficStudio] {selectedProfile} profili başarıyla uygulandı.");
            EditorUtility.DisplayDialog("Profil Uygulandı", $"{selectedProfile} profili sahnedeki NpcSpawner'a uygulandı.", "Tamam");
        }

        private void BindPrefabsToSpawner(List<GameObject> prefabs)
        {
            NpcSpawner spawner = Object.FindFirstObjectByType<NpcSpawner>();
            if (spawner == null)
            {
                EditorUtility.DisplayDialog("Spawner Bulunamadı", "Sahnede NpcSpawner bulunamadı!", "Tamam");
                return;
            }

            Undo.RecordObject(spawner, "Bind NPC Prefabs");
            spawner.SetNpcPrefabs(prefabs.ToArray());
            EditorUtility.SetDirty(spawner);
            EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);

            Debug.Log($"[TrafficStudio] {prefabs.Count} adet prefab NpcSpawner'a bağlandı.");
            EditorUtility.DisplayDialog("Prefablar Bağlandı", $"{prefabs.Count} adet araç prefabı NpcSpawner'a bağlandı.", "Tamam");
        }

        #endregion

        #region Tab 2: Diagnostics & Audit

        private void DrawDiagnosticsTab()
        {
            EditorGUILayout.LabelField("🔍 Trafik & Yol Ağı Teşhis Raporu", EditorStyles.boldLabel);

            RoadNetworkManager rnm = Object.FindFirstObjectByType<RoadNetworkManager>();
            NpcSpawner spawner = Object.FindFirstObjectByType<NpcSpawner>();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("1. Yol Ağı (RoadNetworkManager):", EditorStyles.boldLabel);
            if (rnm != null)
            {
                EditorGUILayout.LabelField($"• Durum: ✅ Mevcut ({rnm.name})");
                EditorGUILayout.LabelField($"• Kayıtlı Yol Sayısı: {rnm.roads.Count}");
                EditorGUILayout.LabelField($"• Kayıtlı Kavşak Sayısı: {rnm.intersections.Count}");
            }
            else
            {
                EditorGUILayout.LabelField("• Durum: ❌ Eksik! (RoadNetworkManager sahnede yok)");
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6f);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("2. Araç Üretici (NpcSpawner):", EditorStyles.boldLabel);
            if (spawner != null)
            {
                EditorGUILayout.LabelField($"• Durum: ✅ Mevcut ({spawner.name})");
                EditorGUILayout.LabelField($"• Aktif Profil: {spawner.CurrentProfile}");
                GameObject[] assignedPrefabs = spawner.GetNpcPrefabs();
                int pCount = assignedPrefabs != null ? assignedPrefabs.Length : 0;
                EditorGUILayout.LabelField($"• Atanmış Prefab Sayısı: {pCount}");
                if (pCount == 0)
                {
                    EditorGUILayout.HelpBox("Uyarı: NpcSpawner üzerinde hiç prefab tanımlı değil!", MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.LabelField("• Durum: ❌ Eksik! (NpcSpawner sahnede yok)");
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6f);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("3. NPC Prefab Bütünlüğü:", EditorStyles.boldLabel);
            List<GameObject> prefabs = LoadNpcPrefabsFromDisk();
            foreach (var p in prefabs)
            {
                if (p == null) continue;
                bool hasAgent = p.GetComponent<NpcCarAgent>() != null;
                bool hasRecovery = p.GetComponent<NpcRecovery>() != null;
                bool hasRb = p.GetComponent<Rigidbody>() != null;
                WheelCollider[] wheels = p.GetComponentsInChildren<WheelCollider>();

                string status = (hasAgent && hasRecovery && hasRb && wheels.Length >= 4) ? "✅ Sağlam" : "⚠️ Eksik Bileşen";
                EditorGUILayout.LabelField($"• {p.name}: {status} (Tekerlek: {wheels.Length}, Rigidbody: {(hasRb ? "Var" : "Yok")})");
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10f);
            if (GUILayout.Button("🔄 Yol Ağını ve Bağlantıları Şimdi Yenile (Refresh Network)", GUILayout.Height(32f)))
            {
                if (rnm != null)
                {
                    rnm.RefreshNetwork();
                    Debug.Log($"[TrafficStudio] Yol ağı yenilendi: {rnm.roads.Count} yol, {rnm.intersections.Count} kavşak.");
                }
            }
        }

        #endregion

        #region Tab 3: Live Simulation

        private void DrawLiveSimulationTab()
        {
            EditorGUILayout.LabelField("🎮 Canlı Trafik Simülasyonu", EditorStyles.boldLabel);

            NpcSpawner spawner = Object.FindFirstObjectByType<NpcSpawner>();

            if (spawner == null)
            {
                EditorGUILayout.HelpBox("Sahnede NpcSpawner bulunamadı! Önce Kurulum sekmesinden sistemi kurun.", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📊 Canlı Metrikler:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"• Aktif NPC Sayısı: {spawner.ActiveNpcCount}");
            EditorGUILayout.LabelField($"• Havuzdaki (Pooled) NPC Sayısı: {spawner.PooledNpcCount}");
            EditorGUILayout.LabelField($"• Oyun Durumu: {(Application.isPlaying ? "▶️ Oynatılıyor (Play Mode)" : "⏸️ Editör Modu")}");
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10f);

            if (Application.isPlaying)
            {
                GUI.backgroundColor = new Color(0.2f, 0.85f, 0.35f, 1f);
                if (GUILayout.Button("🚗 Trafiği Şimdi Doğur (Spawn NPCs Now)", GUILayout.Height(36f)))
                {
                    spawner.SpawnNpcs();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.Space(6f);

                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f, 1f);
                if (GUILayout.Button("🧹 Tüm Aktif Trafiği Temizle (Clear Active Traffic)", GUILayout.Height(32f)))
                {
                    spawner.ClearAllNpcs(true);
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Canlı trafik doğurma ve interaktif testler için Unity'de 'Play' tuşuna basınız.\n" +
                    "Oyun başladığında NpcSpawner otomatik olarak araçları oyuncunun etrafında doğuracaktır.",
                    MessageType.Info);
            }
        }

        #endregion

        #region Helpers

        private static List<GameObject> LoadNpcPrefabsFromDisk()
        {
            List<GameObject> list = new List<GameObject>();
            if (!Directory.Exists(PrefabsFolder)) return list;

            string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { PrefabsFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null && go.GetComponent<NpcCarAgent>() != null)
                {
                    list.Add(go);
                }
            }
            return list;
        }

        private static Transform EnsureSceneRoot(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == name) return go.transform;
            }

            GameObject newGo = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(newGo, $"Create {name}");
            newGo.transform.position = Vector3.zero;
            newGo.transform.rotation = Quaternion.identity;
            return newGo.transform;
        }

        #endregion
    }
}
#endif
