using System.Collections.Generic;
using TrafficSystem.Spline;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryDriver.EditorTools
{
    public class CityHierarchyOrganizer : EditorWindow
    {
        private const string WorldRootName = "World";
        private const string SystemsRootName = "Systems";
        private const string AnkaraName = "Ankara";
        private const string IstanbulName = "İstanbul";
        private const string HighwaysName = "Highways & Bridges";
        private const string PlacedRoadsName = "Placed Roads";
        private const string RoadsideBuildingsName = "Generated Roadside Buildings";

        [MenuItem("Tools/Delivery Driver/City/Organize World Hierarchy", false, 5)]
        public static void ShowWindow()
        {
            CityHierarchyOrganizer window = GetWindow<CityHierarchyOrganizer>("Hierarchy Organizer");
            window.minSize = new Vector2(380f, 320f);
            window.Show();
        }

        [MenuItem("Tools/Delivery Driver/City/Quick Auto-Organize Hierarchy", false, 6)]
        public static void QuickOrganize()
        {
            OrganizeHierarchyInScene();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("World & City Hierarchy Organizer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Bu araç sahnedeki Ankara, İstanbul ve Köprüleri temiz bir mimariye taşır:\n\n" +
                "• 'World' ve 'Systems' kök objelerini oluşturur.\n" +
                "• 'RoadNetworkManager'ı şehir içinden çıkarıp 'Systems' altına taşır (Singleton çakışmasını önler).\n" +
                "• Şehirlerarası köprüler için 'Highways & Bridges' grubunu açar.\n" +
                "• Tüm yollara ait sektör etiketlerini (sectorTag) günceller.",
                MessageType.Info);

            EditorGUILayout.Space(15f);

            if (GUILayout.Button("🏗️ Hiyerarşiyi Otomatik Düzenle (Organize Now)", GUILayout.Height(38)))
            {
                OrganizeHierarchyInScene();
            }

            EditorGUILayout.Space(10f);
            if (GUILayout.Button("🏷️ Yolların ve Kavşakların Sektörlerini Güncelle", GUILayout.Height(28)))
            {
                int count = TagAllRoadSectors();
                Debug.Log($"[CityHierarchyOrganizer] {count} adet yol ve kavşak sektör etiketiyle güncellendi.");
            }
        }

        public static void OrganizeHierarchyInScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Organize World & City Hierarchy");

            // 1. Ensure World and Systems root objects at (0,0,0)
            Transform worldRoot = EnsureRootObject(scene, WorldRootName);
            Transform systemsRoot = EnsureRootObject(scene, SystemsRootName);

            // 2. Ensure Highways & Bridges under World
            Transform highwaysRoot = EnsureChildObject(worldRoot, HighwaysName);
            EnsureChildObject(highwaysRoot, PlacedRoadsName);
            EnsureChildObject(highwaysRoot, RoadsideBuildingsName);

            // 3. Find and adopt Ankara
            Transform ankara = FindObjectInScene(AnkaraName, scene);
            if (ankara != null)
            {
                if (ankara.parent != worldRoot)
                {
                    Undo.SetTransformParent(ankara, worldRoot, "Move Ankara to World");
                }
                EnsureChildObject(ankara, PlacedRoadsName);
                EnsureChildObject(ankara, RoadsideBuildingsName);
            }

            // 4. Find and adopt Istanbul (checking both "İstanbul" and "Istanbul")
            Transform istanbul = FindObjectInScene(IstanbulName, scene) ?? FindObjectInScene("Istanbul", scene);
            if (istanbul != null)
            {
                if (istanbul.parent != worldRoot)
                {
                    Undo.SetTransformParent(istanbul, worldRoot, "Move Istanbul to World");
                }
                EnsureChildObject(istanbul, PlacedRoadsName);
                EnsureChildObject(istanbul, RoadsideBuildingsName);
            }

            // 5. Critical: Move RoadNetworkManager to Systems (out of Ankara/Istanbul)
            RoadNetworkManager rnm = Object.FindFirstObjectByType<RoadNetworkManager>();
            if (rnm != null)
            {
                if (rnm.transform.parent != systemsRoot)
                {
                    Undo.SetTransformParent(rnm.transform, systemsRoot, "Move RoadNetworkManager to Systems");
                    Debug.Log("[CityHierarchyOrganizer] RoadNetworkManager güvenli şekilde 'Systems' altına taşındı.");
                }
            }

            // 5b. Ensure CityStreamingManager under Systems
            DeliveryDriver.City.CityStreamingManager csm = Object.FindFirstObjectByType<DeliveryDriver.City.CityStreamingManager>();
            if (csm == null)
            {
                GameObject csmGo = new GameObject("CityStreamingManager");
                Undo.RegisterCreatedObjectUndo(csmGo, "Create CityStreamingManager");
                csmGo.transform.SetParent(systemsRoot, false);
                csm = Undo.AddComponent<DeliveryDriver.City.CityStreamingManager>(csmGo);
                csm.AutoFindRootsIfNull();
                Debug.Log("[CityHierarchyOrganizer] CityStreamingManager 'Systems' altına eklendi ve bağlandı.");
            }
            else if (csm.transform.parent != systemsRoot)
            {
                Undo.SetTransformParent(csm.transform, systemsRoot, "Move CityStreamingManager to Systems");
            }

            // 6. Find any loose Placed Roads at scene root and move to appropriate parent
            AdoptLooseRoots(scene, worldRoot, ankara, highwaysRoot);

            // 7. Update sector tags on all SplineRoad and RoadIntersection objects
            int tagged = TagAllRoadSectors();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[CityHierarchyOrganizer] Hiyerarşi organizasyonu başarıyla tamamlandı! {tagged} adet yol/kavşak sektöre bağlandı.");
        }

        private static void AdoptLooseRoots(Scene scene, Transform worldRoot, Transform ankara, Transform highways)
        {
            foreach (GameObject rootGo in scene.GetRootGameObjects())
            {
                if (rootGo == null) continue;
                string nameLower = rootGo.name.ToLowerInvariant();

                // If loose Placed Roads exists at scene root
                if (nameLower == "placed roads" || nameLower == "roads")
                {
                    Transform targetParent = ankara != null ? ankara : highways;
                    Transform targetFolder = EnsureChildObject(targetParent, PlacedRoadsName);

                    // Move children of rootGo into targetFolder
                    List<Transform> children = new List<Transform>();
                    for (int i = 0; i < rootGo.transform.childCount; i++)
                    {
                        children.Add(rootGo.transform.GetChild(i));
                    }

                    foreach (Transform child in children)
                    {
                        Undo.SetTransformParent(child, targetFolder, "Move road to target folder");
                    }

                    // If empty, delete rootGo
                    if (rootGo.transform.childCount == 0)
                    {
                        Undo.DestroyObjectImmediate(rootGo);
                    }
                }
            }
        }

        public static int TagAllRoadSectors()
        {
            int count = 0;
            SplineRoad[] roads = Object.FindObjectsByType<SplineRoad>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < roads.Length; i++)
            {
                SplineRoad road = roads[i];
                if (road == null) continue;

                string sector = ResolveSectorFromTransform(road.transform);
                if (road.sectorTag != sector)
                {
                    Undo.RecordObject(road, "Update Road Sector Tag");
                    road.sectorTag = sector;
                    EditorUtility.SetDirty(road);
                    count++;
                }
            }

            RoadIntersection[] intersections = Object.FindObjectsByType<RoadIntersection>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < intersections.Length; i++)
            {
                RoadIntersection inter = intersections[i];
                if (inter == null) continue;

                string sector = ResolveSectorFromTransform(inter.transform);
                if (inter.sectorTag != sector)
                {
                    Undo.RecordObject(inter, "Update Intersection Sector Tag");
                    inter.sectorTag = sector;
                    EditorUtility.SetDirty(inter);
                    count++;
                }
            }

            return count;
        }

        public static string ResolveSectorFromTransform(Transform tr)
        {
            Transform curr = tr;
            while (curr != null)
            {
                string name = curr.name.ToLowerInvariant();
                if (name.Contains("ankara")) return "Ankara";
                if (name.Contains("istanbul") || name.Contains("i̇stanbul")) return "İstanbul";
                if (name.Contains("highway") || name.Contains("bridge") || name.Contains("köprü") || name.Contains("otoyol")) return "Highways";
                curr = curr.parent;
            }
            return "Ankara"; // Default fallback
        }

        private static Transform EnsureRootObject(Scene scene, string name)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == name)
                {
                    NormalizeTransform(go.transform);
                    return go.transform;
                }
            }

            GameObject newGo = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(newGo, $"Create {name} Root");
            SceneManager.MoveGameObjectToScene(newGo, scene);
            NormalizeTransform(newGo.transform);
            return newGo.transform;
        }

        private static Transform EnsureChildObject(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                return child;
            }

            GameObject newChild = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(newChild, $"Create {name} under {parent.name}");
            newChild.transform.SetParent(parent, false);
            NormalizeTransform(newChild.transform);
            return newChild.transform;
        }

        private static Transform FindObjectInScene(string name, Scene scene)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == name) return go.transform;
                Transform found = go.transform.Find(name);
                if (found != null) return found;
            }
            return null;
        }

        private static void NormalizeTransform(Transform t)
        {
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }
    }
}
