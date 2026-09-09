using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace DeliveryDriver.City
{
    /// <summary>
    /// Sahnedeki tum yol modellerine (Asphalt, Smooth Road, Road Lane, vb.) runtime ve editorde
    /// otomatik olarak MeshCollider ekler ve "Road" physics katmanina atar.
    /// Araclarin yol mesh'ini delip asagidaki zemine/terrain'e batmasini onler.
    /// </summary>
    [DefaultExecutionOrder(-360)]
    public class RoadCollisionBootstrap : MonoBehaviour
    {
        private static readonly string[] RoadNameKeywords =
        {
            "asphalt",
            "road",
            "lane",
            "intersection",
            "corner",
            "crosswalk",
            "sidewalk"
        };

        private static readonly string[] RoadParentKeywords =
        {
            "placed roads",
            "generated spline roads",
            "roads",
            "roadtiles",
            "roadnetwork",
            "splineroad"
        };

        private static bool sceneHookRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            if (sceneHookRegistered) return;

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            sceneHookRegistered = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureForActiveScene()
        {
            TryCreateBootstrap(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryCreateBootstrap(scene);
        }

        private static void TryCreateBootstrap(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            if (!IsGameplayScene(scene.name)) return;

            if (FindFirstObjectByType<RoadCollisionBootstrap>() != null) return;

            GameObject bootstrapObj = new GameObject("RoadCollisionBootstrap");
            SceneManager.MoveGameObjectToScene(bootstrapObj, scene);
            bootstrapObj.AddComponent<RoadCollisionBootstrap>();
        }

        private static bool IsGameplayScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) return false;
            return sceneName.Equals("Game", StringComparison.OrdinalIgnoreCase) ||
                   sceneName.Equals("GameScene", StringComparison.OrdinalIgnoreCase);
        }

        private void Awake()
        {
            EnsureRoadColliders();
        }

        private void Start()
        {
            // Ikinci bir kontrol: Runtime'da gecikmeli yuklenen veya olusturulan yol objeleri icin
            EnsureRoadColliders();
        }

        public static int EnsureRoadColliders()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid()) return 0;

            int roadLayer = LayerMask.NameToLayer("Road");
            if (roadLayer < 0) roadLayer = 3; // Fallback to Layer 3 if TagManager not matched

            int collidersAdded = 0;
            GameObject[] rootObjects = activeScene.GetRootGameObjects();

            for (int i = 0; i < rootObjects.Length; i++)
            {
                collidersAdded += ProcessGameObjectRecursive(rootObjects[i], roadLayer, false);
            }

            if (collidersAdded > 0)
            {
                Debug.Log($"[RoadCollisionBootstrap] {collidersAdded} road mesh(es) were equipped with MeshColliders on layer '{LayerMask.LayerToName(roadLayer)}'.");
            }

            return collidersAdded;
        }

        private static int ProcessGameObjectRecursive(GameObject go, int roadLayer, bool isInsideRoadParent)
        {
            if (go == null) return 0;

            int added = 0;
            string goName = go.name.ToLowerInvariant();

            bool isRoadParent = isInsideRoadParent;
            if (!isRoadParent)
            {
                for (int i = 0; i < RoadParentKeywords.Length; i++)
                {
                    if (goName.Contains(RoadParentKeywords[i]))
                    {
                        isRoadParent = true;
                        break;
                    }
                }
            }

            bool isRoadName = false;
            for (int i = 0; i < RoadNameKeywords.Length; i++)
            {
                if (goName.Contains(RoadNameKeywords[i]))
                {
                    isRoadName = true;
                    break;
                }
            }

            // Eger yol objesi veya yol hiyerarsisi altindaysa
            if (isRoadParent || isRoadName)
            {
                MeshFilter mf = go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    // Cizgi ve kucuk sus objelerini haric tut
                    if (!goName.Contains("marking") && !goName.Contains("stripe") && !goName.Contains("sign"))
                    {
                        Collider existingCollider = go.GetComponent<Collider>();
                        if (existingCollider == null)
                        {
                            MeshCollider mc = go.AddComponent<MeshCollider>();
                            mc.sharedMesh = mf.sharedMesh;
                            mc.convex = false;
                            added++;
                        }

                        // Katmani Road katmanina ayarla
                        if (go.layer != roadLayer)
                        {
                            go.layer = roadLayer;
                        }
                    }
                }
            }

            Transform t = go.transform;
            int childCount = t.childCount;
            for (int i = 0; i < childCount; i++)
            {
                added += ProcessGameObjectRecursive(t.GetChild(i).gameObject, roadLayer, isRoadParent);
            }

            return added;
        }

#if UNITY_EDITOR
        [MenuItem("Delivery Driver/City/Ensure Road Colliders In Scene", false, 320)]
        public static void EnsureRoadCollidersInSceneEditor()
        {
            int added = EnsureRoadColliders();
            Scene activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorUtility.DisplayDialog(
                "Road Colliders",
                $"Tum yol mesh'leri kontrol edildi.\n\nYeni eklenen MeshCollider sayisi: {added}\nYol katmani 'Road' olarak ayarlandi.",
                "Tamam");
        }
#endif
    }
}
