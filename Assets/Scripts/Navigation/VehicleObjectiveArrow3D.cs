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

        [Header("Arrow Shape")]
        [SerializeField] private float shaftLength = 0.9f;
        [SerializeField] private float shaftThickness = 0.12f;
        [SerializeField] private float headLength = 0.55f;
        [SerializeField] private float headThickness = 0.12f;
        [SerializeField] private float headAngle = 36f;
        [SerializeField] private float headWidth = 0.62f;
        [SerializeField] private float rotationLerpSpeed = 10f;
        [SerializeField] private float minTargetDistance = 0.25f;

        [Header("Arrow Feel")]
        [SerializeField] private float minArrowScale = 0.95f;
        [SerializeField] private float maxArrowScale = 1.45f;
        [SerializeField] private float distanceScaleFactor = 0.009f;
        [SerializeField] private float pulseScaleAmount = 0.05f;
        [SerializeField] private float pulseScaleSpeed = 3.2f;
        [SerializeField] private float baseTilt = 6f;
        [SerializeField] private float cameraTiltInfluence = 5f;
        [SerializeField] private float swayAmount = 2.2f;
        [SerializeField] private float swaySpeed = 2.1f;
        [SerializeField] private Color pickupColor = new Color(0.1f, 1f, 1f, 1f);
        [SerializeField] private Color deliveryColor = new Color(1f, 0.9f, 0.05f, 1f);
        [SerializeField] private Color nearTargetColor = new Color(0.25f, 1f, 0.35f, 1f);
        [SerializeField] private float nearTargetBlendDistance = 28f;
        [SerializeField] private float farEmissionIntensity = 2.3f;
        [SerializeField] private float nearEmissionIntensity = 4.2f;

        private static VehicleObjectiveArrow3D instance;

        private NavigationService navigationService;
        private NavigationObjective currentObjective;
        private Transform playerTransform;
        private Transform arrowRoot;
        private Transform arrowVisual;
        private Material arrowMaterial;
        private Vector3 followVelocity;
        private float vehicleHeightOffset;
        private float nextPlayerResolveTime;
        private Camera cachedCamera;
        private float nextCameraResolveTime;

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

            GameObject visual = new GameObject("Visual");
            arrowVisual = visual.transform;
            arrowVisual.SetParent(arrowRoot, false);

            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shaft.name = "Shaft";
            shaft.transform.SetParent(arrowVisual, false);
            shaft.transform.localPosition = new Vector3(0f, 0f, -shaftLength * 0.12f);
            shaft.transform.localScale = new Vector3(shaftThickness, shaftThickness, shaftLength);
            RemoveCollider(shaft);

            GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tip.name = "Tip";
            tip.transform.SetParent(arrowVisual, false);
            tip.transform.localPosition = new Vector3(0f, 0f, shaftLength * 0.5f + headLength * 0.12f);
            tip.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            tip.transform.localScale = new Vector3(headWidth * 0.42f, headThickness, headWidth * 0.42f);
            RemoveCollider(tip);

            GameObject leftHead = CreateHeadArm("LeftHead", -headAngle, -headWidth * 0.18f);
            leftHead.transform.SetParent(arrowVisual, false);

            GameObject rightHead = CreateHeadArm("RightHead", headAngle, headWidth * 0.18f);
            rightHead.transform.SetParent(arrowVisual, false);

            MeshRenderer referenceRenderer = shaft.GetComponent<MeshRenderer>();
            arrowMaterial = RuntimeColorMaterialHelper.CreateColorMaterial(pickupColor, referenceRenderer);

            ApplyMaterial(shaft.GetComponent<MeshRenderer>());
            ApplyMaterial(tip.GetComponent<MeshRenderer>());
            ApplyMaterialToChildren(leftHead.transform);
            ApplyMaterialToChildren(rightHead.transform);

            UpdateArrowColor(1f);

            arrowRoot.gameObject.SetActive(false);
        }

        private GameObject CreateHeadArm(string name, float yRotation, float xOffset)
        {
            GameObject armRoot = new GameObject(name);
            armRoot.transform.localPosition = new Vector3(xOffset, 0f, shaftLength * 0.34f);
            armRoot.transform.localRotation = Quaternion.identity;
            armRoot.transform.localScale = Vector3.one;

            GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = "Arm";
            arm.transform.SetParent(armRoot.transform, false);
            arm.transform.localPosition = new Vector3(0f, 0f, headLength * 0.24f);
            arm.transform.localRotation = Quaternion.Euler(0f, yRotation, 0f);
            arm.transform.localScale = new Vector3(headThickness, headThickness, headLength);
            RemoveCollider(arm);

            return armRoot;
        }

        private void ApplyMaterial(MeshRenderer renderer)
        {
            if (renderer != null && arrowMaterial != null)
            {
                renderer.material = arrowMaterial;
            }
        }

        private void ApplyMaterialToChildren(Transform root)
        {
            if (root == null)
            {
                return;
            }

            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                ApplyMaterial(renderers[i]);
            }
        }

        private void RemoveCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }

        private void UpdateArrowColor(float distanceToTarget)
        {
            if (arrowMaterial == null)
            {
                return;
            }

            Color baseColor = currentObjective.Type == ObjectiveType.Delivery ? deliveryColor : pickupColor;
            float normalizedDistance = nearTargetBlendDistance <= 0.01f
                ? 1f
                : Mathf.Clamp01(distanceToTarget / nearTargetBlendDistance);
            float highlightFactor = 1f - normalizedDistance;
            Color color = Color.Lerp(baseColor, nearTargetColor, highlightFactor);
            arrowMaterial.color = color;

            if (arrowMaterial.HasProperty("_EmissionColor"))
            {
                arrowMaterial.EnableKeyword("_EMISSION");
                float emissionIntensity = Mathf.Lerp(farEmissionIntensity, nearEmissionIntensity, highlightFactor);
                arrowMaterial.SetColor("_EmissionColor", color * emissionIntensity);
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

            Camera camera = ResolveCamera();
            float distanceToTarget = Vector3.Distance(playerPosition, currentObjective.WorldPosition);
            UpdateArrowColor(distanceToTarget);

            float distanceScale = Mathf.Clamp(minArrowScale + distanceToTarget * distanceScaleFactor, minArrowScale, maxArrowScale);
            float pulse = 1f + Mathf.Sin(Time.time * pulseScaleSpeed) * pulseScaleAmount;
            arrowVisual.localScale = Vector3.one * (distanceScale * pulse);

            float cameraTilt = 0f;
            if (camera != null)
            {
                cameraTilt = -camera.transform.forward.y * cameraTiltInfluence;
            }

            float sway = Mathf.Sin(Time.time * swaySpeed) * swayAmount;
            arrowVisual.localRotation = Quaternion.Euler(baseTilt + cameraTilt, 0f, sway);
        }

        private Camera ResolveCamera()
        {
            if (cachedCamera != null)
            {
                return cachedCamera;
            }

            if (Time.time < nextCameraResolveTime)
            {
                return null;
            }

            nextCameraResolveTime = Time.time + 1f;
            cachedCamera = Camera.main;
            if (cachedCamera == null)
            {
                cachedCamera = FindFirstObjectByType<Camera>();
            }

            return cachedCamera;
        }
    }
}
