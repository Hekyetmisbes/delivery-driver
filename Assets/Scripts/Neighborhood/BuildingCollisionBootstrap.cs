using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryDriver.City
{
    [DefaultExecutionOrder(-350)]
    public class BuildingCollisionBootstrap : MonoBehaviour
    {
        private const string GameSceneName = "Game";
        private static readonly string[] RemovableRuntimeRootNames =
        {
            "SimplePoly_Natures",
            "SimplePoly_Props"
        };

        private static readonly string[] BuildingNamePrefixes =
        {
            "building_",
            "building ",
            "house",
            "shop",
            "restaurant",
            "factory",
            "apartment",
            "office",
            "market",
            "store",
            "bar",
            "tower",
            "skyscraper"
        };

        private static readonly string[] ExcludedNameTokens =
        {
            "buildings",
            "manager",
            "trigger",
            "zone",
            "road",
            "street",
            "sidewalk",
            "curb",
            "lamp",
            "light",
            "tree",
            "bush",
            "fence",
            "sign",
            "bench",
            "mailbox"
        };

        [SerializeField] private Vector3 colliderPadding = new Vector3(0.35f, 0.2f, 0.35f);
        [SerializeField] private Vector3 minimumColliderSize = new Vector3(1.6f, 2f, 1.6f);

        private static bool sceneHookRegistered;
        private int cachedBuildingLayer = int.MinValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            if (sceneHookRegistered)
            {
                return;
            }

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
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            if (!scene.name.Equals(GameSceneName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (FindFirstObjectByType<BuildingCollisionBootstrap>() != null)
            {
                return;
            }

            GameObject bootstrapObject = new GameObject("BuildingCollisionBootstrap");
            bootstrapObject.AddComponent<BuildingCollisionBootstrap>();
        }

        private void Start()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded ||
                !activeScene.name.Equals(GameSceneName, StringComparison.OrdinalIgnoreCase))
            {
                Destroy(gameObject);
                return;
            }

            RemoveUnusedSimplePolyRoots(activeScene);
            EnsureBuildingColliders(activeScene);
            Destroy(gameObject);
        }

        private void RemoveUnusedSimplePolyRoots(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            int removed = 0;
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (!IsRemovableSimplePolyRoot(root))
                {
                    continue;
                }

                Destroy(root);
                removed++;
            }

            if (removed > 0)
            {
                Debug.Log($"[BuildingCollisionBootstrap] Removed {removed} unused SimplePoly runtime root object(s).");
            }
        }

        private static bool IsRemovableSimplePolyRoot(GameObject candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            bool matchesCleanupName = false;
            for (int i = 0; i < RemovableRuntimeRootNames.Length; i++)
            {
                if (candidate.name.Equals(RemovableRuntimeRootNames[i], StringComparison.OrdinalIgnoreCase))
                {
                    matchesCleanupName = true;
                    break;
                }
            }

            if (!matchesCleanupName)
            {
                return false;
            }

            if (candidate.transform.childCount > 0)
            {
                return false;
            }

            Component[] components = candidate.GetComponents<Component>();
            return components == null || components.Length <= 1;
        }

        private void EnsureBuildingColliders(Scene scene)
        {
            cachedBuildingLayer = LayerMask.NameToLayer("Building");
            GameObject[] roots = scene.GetRootGameObjects();
            int collidersAdded = 0;

            for (int i = 0; i < roots.Length; i++)
            {
                collidersAdded += EnsureBuildingCollidersRecursive(roots[i].transform);
            }

            Debug.Log($"[BuildingCollisionBootstrap] Added {collidersAdded} missing building colliders in scene '{scene.name}'.");
        }

        private int EnsureBuildingCollidersRecursive(Transform current)
        {
            if (current == null || !current.gameObject.activeInHierarchy)
            {
                return 0;
            }

            if (TryAddColliderToBuildingRoot(current.gameObject))
            {
                return 1;
            }

            int added = 0;
            for (int i = 0; i < current.childCount; i++)
            {
                added += EnsureBuildingCollidersRecursive(current.GetChild(i));
            }

            return added;
        }

        private bool TryAddColliderToBuildingRoot(GameObject candidate)
        {
            if (!IsLikelyBuildingRoot(candidate))
            {
                return false;
            }

            if (HasSolidColliderInHierarchy(candidate.transform))
            {
                return false;
            }

            if (!TryCalculateLocalRendererBounds(candidate.transform, out Bounds localBounds))
            {
                return false;
            }

            Vector3 paddedSize = Vector3.Max(localBounds.size + colliderPadding, minimumColliderSize);
            if (paddedSize.y < minimumColliderSize.y)
            {
                return false;
            }

            BoxCollider collider = candidate.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = candidate.AddComponent<BoxCollider>();
            }

            collider.isTrigger = false;
            collider.center = localBounds.center;
            collider.size = paddedSize;
            return true;
        }

        private bool IsLikelyBuildingRoot(GameObject candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            if (candidate.TryGetComponent<Rigidbody>(out _))
            {
                return false;
            }

            if (cachedBuildingLayer >= 0 && candidate.layer == cachedBuildingLayer)
            {
                return true;
            }

            string lowerName = candidate.name.ToLowerInvariant();
            for (int i = 0; i < ExcludedNameTokens.Length; i++)
            {
                if (lowerName.Contains(ExcludedNameTokens[i]))
                {
                    return false;
                }
            }

            bool hasPrefixMatch = false;
            for (int i = 0; i < BuildingNamePrefixes.Length; i++)
            {
                if (lowerName.StartsWith(BuildingNamePrefixes[i], StringComparison.Ordinal))
                {
                    hasPrefixMatch = true;
                    break;
                }
            }

            if (!hasPrefixMatch && !lowerName.Contains("auto service"))
            {
                return false;
            }

            return candidate.GetComponentInChildren<Renderer>(true) != null;
        }

        private static bool HasSolidColliderInHierarchy(Transform root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider != null && collider.enabled && !collider.isTrigger)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryCalculateLocalRendererBounds(Transform root, out Bounds localBounds)
        {
            localBounds = default;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Matrix4x4 worldToLocal = root.worldToLocalMatrix;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null ||
                    renderer is ParticleSystemRenderer ||
                    renderer is TrailRenderer ||
                    renderer is LineRenderer)
                {
                    continue;
                }

                Bounds rendererBounds = renderer.bounds;
                if (rendererBounds.size.sqrMagnitude < 0.01f)
                {
                    continue;
                }

                Vector3 min = rendererBounds.min;
                Vector3 max = rendererBounds.max;
                Vector3[] corners =
                {
                    new Vector3(min.x, min.y, min.z),
                    new Vector3(min.x, min.y, max.z),
                    new Vector3(min.x, max.y, min.z),
                    new Vector3(min.x, max.y, max.z),
                    new Vector3(max.x, min.y, min.z),
                    new Vector3(max.x, min.y, max.z),
                    new Vector3(max.x, max.y, min.z),
                    new Vector3(max.x, max.y, max.z)
                };

                for (int c = 0; c < corners.Length; c++)
                {
                    Vector3 localPoint = worldToLocal.MultiplyPoint3x4(corners[c]);
                    if (!hasBounds)
                    {
                        localBounds = new Bounds(localPoint, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(localPoint);
                    }
                }
            }

            return hasBounds;
        }
    }
}
