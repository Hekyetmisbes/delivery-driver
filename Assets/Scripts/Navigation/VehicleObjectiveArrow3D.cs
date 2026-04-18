using System.Collections.Generic;
using DeliveryDriver.Company;
using UnityEngine;

namespace DeliveryDriver.Navigation
{
    public class VehicleObjectiveArrow3D : MonoBehaviour
    {
        [Header("Anchor")]
        [SerializeField] private float minHeightOffset = 3.6f;
        [SerializeField] private float extraHeightOffset = 1.85f;
        [SerializeField] private float followSmoothTime = 0.08f;
        [SerializeField] private float bobAmplitude = 0.2f;
        [SerializeField] private float bobSpeed = 3f;

        [Header("Arrow Model")]
        [SerializeField] private bool preferLowPolyArrowPrefab = true;
        [SerializeField] private GameObject lowPolyArrowPrefab;
        [SerializeField] private string lowPolyArrowResourcePath = "Navigation/Arrow_3D_Icon_01";
        [SerializeField] private float lowPolyArrowBaseScale = 0.92f;
        [SerializeField] private Vector3 lowPolyArrowRotationOffset = Vector3.zero;

        [Header("Arrow Shape (Fallback)")]
        [SerializeField] private float shaftLength = 0.82f;
        [SerializeField] private float shaftThickness = 0.2f;
        [SerializeField] private float headLength = 0.56f;
        [SerializeField] private float headThickness = 0.14f;
        [SerializeField] private float headAngle = 52f;
        [SerializeField] private float headWidth = 0.68f;
        [SerializeField] private float rotationLerpSpeed = 10f;
        [SerializeField] private float minTargetDistance = 0.25f;

        [Header("Arrow Feel")]
        [SerializeField] private float minArrowScale = 0.82f;
        [SerializeField] private float maxArrowScale = 1.18f;
        [SerializeField] private float distanceScaleFactor = 0.0055f;
        [SerializeField] private float pulseScaleAmount = 0.035f;
        [SerializeField] private float pulseScaleSpeed = 3.2f;
        [SerializeField] private float visualPitch = 9f;
        [SerializeField] private float swayAmount = 1.8f;
        [SerializeField] private float swaySpeed = 2.3f;
        [SerializeField] private Color outlineColor = new Color(0.02f, 0.05f, 0.08f, 0.74f);
        [SerializeField] private float outlineScaleMultiplier = 1.08f;
        [SerializeField] private Color pickupColor = new Color(0.1f, 1f, 1f, 1f);
        [SerializeField] private Color deliveryColor = new Color(1f, 0.9f, 0.05f, 1f);
        [SerializeField] private Color nearTargetColor = new Color(0.25f, 1f, 0.35f, 1f);
        [SerializeField] private float nearTargetBlendDistance = 28f;
        [SerializeField] private float farEmissionIntensity = 2.3f;
        [SerializeField] private float nearEmissionIntensity = 4.2f;

        private static readonly string[] DefaultLowPolyArrowPrefabPaths =
        {
            "Assets/HQP Studios/Low Poly 3D Icons - Pack Lite/Prefabs/Arrow_3D_Icon_01.prefab",
            "Assets/HQP Studios/Low Poly 3D Icons - Pack Lite/Prefabs/Arrow_3D_Icon_02.prefab",
            "Assets/HQP Studios/Low Poly 3D Icons - Pack Lite/Prefabs/Arrow_3D_Icon_03.prefab",
            "Assets/HQP Studios/Low Poly 3D Icons - Pack Lite/Prefabs/Arrow_3D_Icon_04.prefab",
            "Assets/HQP Studios/Low Poly 3D Icons - Pack Lite/Prefabs/Arrow_3D_Icon_05.prefab"
        };

        private static VehicleObjectiveArrow3D instance;

        private NavigationService navigationService;
        private NavigationObjective currentObjective;
        private Transform playerTransform;
        private Transform arrowRoot;
        private Transform arrowVisual;
        private Transform arrowOutlineVisual;
        private Mesh arrowMesh;
        private Material arrowMaterial;
        private Material arrowOutlineMaterial;
        private Vector3 followVelocity;
        private float vehicleHeightOffset;
        private float nextPlayerResolveTime;
        private float arrowVisualBaseScale = 1f;
        private bool usingPrefabVisual;
        private readonly List<Material> arrowRuntimeMaterials = new List<Material>();

        public static VehicleObjectiveArrow3D Instance => instance;

        public static VehicleObjectiveArrow3D EnsureInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindFirstObjectByType<VehicleObjectiveArrow3D>();
            if (instance != null)
            {
                return instance;
            }

            GameObject go = new GameObject("VehicleObjectiveArrow3D");
            return go.AddComponent<VehicleObjectiveArrow3D>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
        }

        private void OnEnable()
        {
            PlayerVehicleManager.ActiveVehicleChanged += HandleActiveVehicleChanged;
            TryResolvePlayerTransform();
            TryBindNavigationService();
        }

        private void OnDisable()
        {
            PlayerVehicleManager.ActiveVehicleChanged -= HandleActiveVehicleChanged;
            BindNavigationService(null);
        }

        private void OnDestroy()
        {
            if (arrowRoot != null)
            {
                Destroy(arrowRoot.gameObject);
                arrowRoot = null;
            }

            if (arrowMaterial != null)
            {
                Destroy(arrowMaterial);
                arrowMaterial = null;
            }

            if (arrowOutlineMaterial != null)
            {
                Destroy(arrowOutlineMaterial);
                arrowOutlineMaterial = null;
            }

            if (arrowMesh != null)
            {
                Destroy(arrowMesh);
                arrowMesh = null;
            }

            for (int i = 0; i < arrowRuntimeMaterials.Count; i++)
            {
                if (arrowRuntimeMaterials[i] != null)
                {
                    Destroy(arrowRuntimeMaterials[i]);
                }
            }
            arrowRuntimeMaterials.Clear();

            if (instance == this)
            {
                instance = null;
            }
        }

        private void Update()
        {
            TryBindNavigationService();

            if (playerTransform == null && Time.time >= nextPlayerResolveTime)
            {
                nextPlayerResolveTime = Time.time + 1f;
                TryResolvePlayerTransform();
            }

            bool canDisplay = currentObjective.IsValid && playerTransform != null;
            if (!canDisplay)
            {
                SetArrowVisible(false);
                return;
            }

            EnsureArrowVisual();
            SetArrowVisible(true);

            Vector3 playerPosition = playerTransform.position;
            float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            Vector3 desiredPosition = playerPosition + Vector3.up * (vehicleHeightOffset + bobOffset);

            float smoothTime = Mathf.Max(0.01f, followSmoothTime);
            arrowRoot.position = Vector3.SmoothDamp(arrowRoot.position, desiredPosition, ref followVelocity, smoothTime);

            Vector3 targetDirection = currentObjective.WorldPosition - playerPosition;
            targetDirection.y = 0f;
            if (targetDirection.sqrMagnitude < minTargetDistance * minTargetDistance)
            {
                targetDirection = playerTransform.forward;
                targetDirection.y = 0f;
            }

            if (targetDirection.sqrMagnitude > 0.001f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(targetDirection.normalized, Vector3.up);
                float lerpFactor = 1f - Mathf.Exp(-rotationLerpSpeed * Time.deltaTime);
                arrowRoot.rotation = Quaternion.Slerp(arrowRoot.rotation, desiredRotation, lerpFactor);
            }

            UpdateVisualStyle(playerPosition);
        }

        public void BindNavigationService(NavigationService service)
        {
            if (navigationService == service)
            {
                return;
            }

            if (navigationService != null)
            {
                navigationService.OnObjectiveChanged -= HandleObjectiveChanged;
                navigationService.OnNavigationCleared -= HandleNavigationCleared;
            }

            navigationService = service;
            currentObjective = service != null ? service.CurrentObjective : NavigationObjective.Empty;

            if (navigationService != null)
            {
                navigationService.OnObjectiveChanged += HandleObjectiveChanged;
                navigationService.OnNavigationCleared += HandleNavigationCleared;
            }

            UpdateArrowColor(1f);
        }

        public void SetPlayerTransform(Transform player)
        {
            playerTransform = player;
            nextPlayerResolveTime = 0f;
            vehicleHeightOffset = CalculateVehicleHeightOffset(playerTransform);
            followVelocity = Vector3.zero;

            if (arrowRoot != null && playerTransform != null)
            {
                arrowRoot.position = playerTransform.position + Vector3.up * vehicleHeightOffset;
            }
        }

        private void HandleObjectiveChanged(NavigationObjective objective)
        {
            currentObjective = objective;
            UpdateArrowColor(1f);
        }

        private void HandleNavigationCleared()
        {
            currentObjective = NavigationObjective.Empty;
            SetArrowVisible(false);
        }

        private void HandleActiveVehicleChanged(CarController controller)
        {
            SetPlayerTransform(controller != null ? controller.transform : null);
        }

        private void TryBindNavigationService()
        {
            if (navigationService != null)
            {
                return;
            }

            NavigationService service = NavigationService.Instance;
            if (service != null)
            {
                BindNavigationService(service);
            }
        }

        private void TryResolvePlayerTransform()
        {
            Transform resolvedPlayer = null;

            if (PlayerVehicleManager.Instance != null && PlayerVehicleManager.Instance.ActiveVehicleController != null)
            {
                resolvedPlayer = PlayerVehicleManager.Instance.ActiveVehicleController.transform;
            }

            if (resolvedPlayer == null)
            {
                CarController controller = FindFirstObjectByType<CarController>();
                if (controller != null)
                {
                    resolvedPlayer = controller.transform;
                }
            }

            if (resolvedPlayer == null)
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject != null)
                {
                    resolvedPlayer = playerObject.transform;
                }
            }

            if (resolvedPlayer != playerTransform)
            {
                SetPlayerTransform(resolvedPlayer);
            }
        }

        private void EnsureArrowVisual()
        {
            if (arrowRoot != null)
            {
                return;
            }

            GameObject root = new GameObject("VehicleObjectiveArrowVisual");
            arrowRoot = root.transform;
            arrowRoot.SetParent(transform, false);

            if (TryCreateLowPolyArrowVisual())
            {
                UpdateArrowColor(1f);
                arrowRoot.gameObject.SetActive(false);
                return;
            }

            usingPrefabVisual = false;
            arrowVisualBaseScale = 1f;

            GameObject visual = new GameObject("Visual");
            arrowVisual = visual.transform;
            arrowVisual.SetParent(arrowRoot, false);

            MeshFilter meshFilter = visual.AddComponent<MeshFilter>();
            arrowMesh = CreateArrowMesh();
            meshFilter.sharedMesh = arrowMesh;

            MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            arrowMaterial = RuntimeColorMaterialHelper.CreateColorMaterial(pickupColor, renderer);
            if (arrowMaterial != null)
            {
                renderer.sharedMaterial = arrowMaterial;
            }

            GameObject outline = new GameObject("Outline");
            arrowOutlineVisual = outline.transform;
            arrowOutlineVisual.SetParent(arrowRoot, false);
            arrowOutlineVisual.localPosition = new Vector3(0f, -0.01f, -0.015f);

            MeshFilter outlineMeshFilter = outline.AddComponent<MeshFilter>();
            outlineMeshFilter.sharedMesh = arrowMesh;

            MeshRenderer outlineRenderer = outline.AddComponent<MeshRenderer>();
            outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;
            arrowOutlineMaterial = RuntimeColorMaterialHelper.CreateColorMaterial(outlineColor, outlineRenderer);
            if (arrowOutlineMaterial != null)
            {
                outlineRenderer.sharedMaterial = arrowOutlineMaterial;
            }

            UpdateArrowColor(1f);

            arrowRoot.gameObject.SetActive(false);
        }

        private bool TryCreateLowPolyArrowVisual()
        {
            if (!preferLowPolyArrowPrefab)
            {
                return false;
            }

            GameObject prefab = ResolveLowPolyArrowPrefab();
            if (prefab == null)
            {
                return false;
            }

            GameObject visualInstance = Instantiate(prefab, arrowRoot);
            visualInstance.name = "Visual";

            arrowVisual = visualInstance.transform;
            arrowVisual.localPosition = Vector3.zero;
            arrowVisual.localRotation = Quaternion.Euler(lowPolyArrowRotationOffset);

            usingPrefabVisual = true;
            arrowVisualBaseScale = Mathf.Max(0.01f, lowPolyArrowBaseScale);
            arrowVisual.localScale = Vector3.one * arrowVisualBaseScale;

            MeshRenderer[] renderers = visualInstance.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                RegisterRuntimeMaterials(renderers[i]);
            }

            return true;
        }

        private GameObject ResolveLowPolyArrowPrefab()
        {
            if (lowPolyArrowPrefab != null)
            {
                return lowPolyArrowPrefab;
            }

            if (!string.IsNullOrWhiteSpace(lowPolyArrowResourcePath))
            {
                GameObject resourcePrefab = Resources.Load<GameObject>(lowPolyArrowResourcePath);
                if (resourcePrefab != null)
                {
                    return resourcePrefab;
                }
            }

#if UNITY_EDITOR
            return LoadDefaultLowPolyArrowPrefabFromEditorAssets();
#else
            return null;
#endif
        }

#if UNITY_EDITOR
        private static GameObject LoadDefaultLowPolyArrowPrefabFromEditorAssets()
        {
            for (int i = 0; i < DefaultLowPolyArrowPrefabPaths.Length; i++)
            {
                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DefaultLowPolyArrowPrefabPaths[i]);
                if (prefab != null)
                {
                    return prefab;
                }
            }

            return null;
        }
#endif

        private void RegisterRuntimeMaterials(MeshRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Material[] sharedMaterials = renderer.sharedMaterials;
            if (sharedMaterials == null || sharedMaterials.Length == 0)
            {
                return;
            }

            Material[] runtimeMaterials = new Material[sharedMaterials.Length];
            for (int i = 0; i < sharedMaterials.Length; i++)
            {
                Material sourceMaterial = sharedMaterials[i];
                Material runtimeMaterial = sourceMaterial != null
                    ? new Material(sourceMaterial)
                    : RuntimeColorMaterialHelper.CreateColorMaterial(pickupColor, renderer);

                runtimeMaterials[i] = runtimeMaterial;
                if (runtimeMaterial != null)
                {
                    arrowRuntimeMaterials.Add(runtimeMaterial);
                }
            }

            renderer.sharedMaterials = runtimeMaterials;
        }

        private Mesh CreateArrowMesh()
        {
            float bodyDepth = Mathf.Max(0.08f, headThickness);
            float shaftHalfWidth = Mathf.Max(0.07f, shaftThickness * 0.5f);
            float shaftLengthLocal = Mathf.Max(0.5f, shaftLength);
            float headLengthLocal = Mathf.Max(0.34f, headLength);
            float tailY = -shaftLengthLocal * 0.5f;
            float headBaseY = tailY + shaftLengthLocal;
            float tipY = headBaseY + headLengthLocal;
            float angleHalfWidth = Mathf.Tan(Mathf.Clamp(headAngle, 18f, 75f) * 0.5f * Mathf.Deg2Rad) * headLengthLocal;
            float headHalfWidth = Mathf.Max(shaftHalfWidth * 1.7f, Mathf.Max(headWidth * 0.5f, angleHalfWidth));

            Vector2[] outline =
            {
                new Vector2(-shaftHalfWidth, tailY),
                new Vector2(-shaftHalfWidth, headBaseY),
                new Vector2(-headHalfWidth, headBaseY),
                new Vector2(0f, tipY),
                new Vector2(headHalfWidth, headBaseY),
                new Vector2(shaftHalfWidth, headBaseY),
                new Vector2(shaftHalfWidth, tailY)
            };

            List<Vector3> vertices = new List<Vector3>(outline.Length * 2);
            List<int> triangles = new List<int>((outline.Length - 2) * 6 + outline.Length * 6);

            float frontZ = bodyDepth * 0.5f;
            float backZ = -frontZ;

            for (int i = 0; i < outline.Length; i++)
            {
                vertices.Add(new Vector3(outline[i].x, outline[i].y, frontZ));
            }

            for (int i = 0; i < outline.Length; i++)
            {
                vertices.Add(new Vector3(outline[i].x, outline[i].y, backZ));
            }

            for (int i = 1; i < outline.Length - 1; i++)
            {
                triangles.Add(0);
                triangles.Add(i);
                triangles.Add(i + 1);
            }

            int bottomOffset = outline.Length;
            for (int i = 1; i < outline.Length - 1; i++)
            {
                triangles.Add(bottomOffset);
                triangles.Add(bottomOffset + i + 1);
                triangles.Add(bottomOffset + i);
            }

            for (int i = 0; i < outline.Length; i++)
            {
                int next = (i + 1) % outline.Length;
                AddQuad(triangles, i, next, bottomOffset + next, bottomOffset + i);
            }

            Mesh mesh = new Mesh
            {
                name = "VehicleObjectiveArrowMesh"
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            RotateMeshVertices(mesh, Quaternion.Euler(90f, 0f, 0f));
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void RotateMeshVertices(Mesh mesh, Quaternion rotation)
        {
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = rotation * vertices[i];
            }

            mesh.vertices = vertices;
        }

        private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);

            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(d);
        }

        private void UpdateArrowColor(float distanceToTarget)
        {
            Color baseColor = currentObjective.Type == ObjectiveType.Delivery ? deliveryColor : pickupColor;
            float normalizedDistance = nearTargetBlendDistance <= 0.01f
                ? 1f
                : Mathf.Clamp01(distanceToTarget / nearTargetBlendDistance);
            float highlightFactor = 1f - normalizedDistance;
            Color color = Color.Lerp(baseColor, nearTargetColor, highlightFactor);
            float emissionIntensity = Mathf.Lerp(farEmissionIntensity, nearEmissionIntensity, highlightFactor);

            if (arrowMaterial != null)
            {
                ApplyMaterialColor(arrowMaterial, color, emissionIntensity);
            }

            for (int i = 0; i < arrowRuntimeMaterials.Count; i++)
            {
                Material material = arrowRuntimeMaterials[i];
                if (material != null)
                {
                    ApplyMaterialColor(material, color, emissionIntensity);
                }
            }
        }

        private static void ApplyMaterialColor(Material material, Color color, float emissionIntensity)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emissionIntensity);
            }
        }

        private void SetArrowVisible(bool visible)
        {
            if (arrowRoot == null || arrowRoot.gameObject.activeSelf == visible)
            {
                return;
            }

            if (visible && playerTransform != null)
            {
                arrowRoot.position = playerTransform.position + Vector3.up * vehicleHeightOffset;
                followVelocity = Vector3.zero;
            }

            if (arrowRoot != null)
            {
                arrowRoot.gameObject.SetActive(visible);
            }
        }

        private float CalculateVehicleHeightOffset(Transform player)
        {
            if (player == null)
            {
                return minHeightOffset;
            }

            Bounds vehicleBounds = new Bounds(player.position, Vector3.zero);
            bool hasBounds = false;

            Renderer[] renderers = player.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    vehicleBounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    vehicleBounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
            {
                Collider[] colliders = player.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    Collider collider = colliders[i];
                    if (collider == null || collider.isTrigger)
                    {
                        continue;
                    }

                    if (!hasBounds)
                    {
                        vehicleBounds = collider.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        vehicleBounds.Encapsulate(collider.bounds);
                    }
                }
            }

            if (!hasBounds)
            {
                return minHeightOffset;
            }

            return Mathf.Max(minHeightOffset, vehicleBounds.extents.y + extraHeightOffset);
        }

        private void UpdateVisualStyle(Vector3 playerPosition)
        {
            if (arrowVisual == null)
            {
                return;
            }

            float distanceToTarget = Vector3.Distance(playerPosition, currentObjective.WorldPosition);
            UpdateArrowColor(distanceToTarget);

            float distanceScale = Mathf.Clamp(minArrowScale + distanceToTarget * distanceScaleFactor, minArrowScale, maxArrowScale);
            float pulse = 1f + Mathf.Sin(Time.time * pulseScaleSpeed) * pulseScaleAmount;
            float finalScale = distanceScale * pulse;
            arrowVisual.localScale = Vector3.one * (arrowVisualBaseScale * finalScale);
            if (arrowOutlineVisual != null)
            {
                float outlineScale = finalScale * outlineScaleMultiplier;
                float outlineHeightScale = finalScale * Mathf.Lerp(1f, outlineScaleMultiplier, 0.25f);
                arrowOutlineVisual.localScale = new Vector3(outlineScale, outlineHeightScale, outlineScale);
            }

            float sway = Mathf.Sin(Time.time * swaySpeed) * swayAmount;
            Vector3 localEuler = new Vector3(visualPitch, 0f, sway);
            if (usingPrefabVisual)
            {
                localEuler += lowPolyArrowRotationOffset;
            }

            arrowVisual.localRotation = Quaternion.Euler(localEuler);
            if (arrowOutlineVisual != null)
            {
                arrowOutlineVisual.localRotation = arrowVisual.localRotation;
            }
        }
    }
}
