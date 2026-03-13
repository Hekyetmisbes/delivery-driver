using UnityEngine;
using DeliveryDriver.Company;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DeliveryDriver.Quest.UI
{
    /// <summary>
    /// Controls the minimap camera that follows the player from above
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class MinimapCamera : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform playerTransform;

        [Header("Camera Settings")]
        [SerializeField] private float height = 65f;
        [SerializeField] private float orthographicSize = 40f;
        [SerializeField] private bool rotateWithPlayer = false;

        [Header("Overlay")]
        [SerializeField] private bool useStandaloneOverlay = true;
        [SerializeField] private bool allowToggleKey = true;
        [SerializeField] private float viewportSize = 0.22f;
        [SerializeField] private Vector2 viewportMargin = new Vector2(0.02f, 0.02f);
        [SerializeField] private LayerMask cullingMask = ~0;
        [SerializeField] private Color backgroundColor = new Color(0.18f, 0.22f, 0.24f, 1f);
        [SerializeField] private float nearClipPlane = 0.1f;
        [SerializeField] private float farClipPlane = 500f;
        [SerializeField] private float depth = 10f;

        [Header("Smoothing")]
        [SerializeField] private bool smoothFollow = true;
        [SerializeField] private float smoothSpeed = 10f;
        [SerializeField] private float playerResolveRetryInterval = 0.5f;

        private Camera minimapCamera;
        private bool isVisible = true;
        private float nextPlayerResolveTime;
        private PlayerVehicleManager cachedVehicleManager;

        public Camera CameraComponent => minimapCamera;

        private void Awake()
        {
            minimapCamera = GetComponent<Camera>();
            SetupCamera();
            SetVisible(isVisible);
        }

        private void Start()
        {
            ResolvePlayerTransform();
        }

        private void OnEnable()
        {
            PlayerVehicleManager.ActiveVehicleChanged += HandleActiveVehicleChanged;
        }

        private void OnDisable()
        {
            PlayerVehicleManager.ActiveVehicleChanged -= HandleActiveVehicleChanged;
        }

        private void LateUpdate()
        {
            HandleToggleInput();

            if (!IsUsablePlayerTransform(playerTransform))
            {
                ResolvePlayerTransform();
            }

            if (playerTransform == null)
            {
                return;
            }

            UpdateCameraPosition();

            if (rotateWithPlayer)
            {
                UpdateCameraRotation();
            }
        }

        private void SetupCamera()
        {
            if (minimapCamera == null)
            {
                return;
            }

            minimapCamera.orthographic = true;
            minimapCamera.orthographicSize = orthographicSize;
            minimapCamera.clearFlags = CameraClearFlags.SolidColor;
            minimapCamera.backgroundColor = backgroundColor;
            minimapCamera.cullingMask = cullingMask;
            minimapCamera.nearClipPlane = nearClipPlane;
            minimapCamera.farClipPlane = farClipPlane;
            minimapCamera.depth = depth;

            bool rendersToOverlay = useStandaloneOverlay && minimapCamera.targetTexture == null;
            if (rendersToOverlay)
            {
                minimapCamera.rect = BuildViewportRect();
            }
            else if (minimapCamera.targetTexture != null)
            {
                minimapCamera.rect = new Rect(0f, 0f, 1f, 1f);
            }
            else
            {
                minimapCamera.rect = new Rect(0f, 0f, 0f, 0f);
            }

            if (!rotateWithPlayer)
            {
                transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }

            minimapCamera.enabled = isVisible;
        }

        private void UpdateCameraPosition()
        {
            Vector3 targetPosition = new Vector3(
                playerTransform.position.x,
                playerTransform.position.y + height,
                playerTransform.position.z
            );

            if (smoothFollow)
            {
                transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);
            }
            else
            {
                transform.position = targetPosition;
            }
        }

        private void UpdateCameraRotation()
        {
            float targetYRotation = playerTransform.eulerAngles.y;
            Quaternion targetRotation = Quaternion.Euler(90f, targetYRotation, 0f);

            if (smoothFollow)
            {
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, smoothSpeed * Time.deltaTime);
            }
            else
            {
                transform.rotation = targetRotation;
            }
        }

        private void HandleToggleInput()
        {
            if (!allowToggleKey || !useStandaloneOverlay || (minimapCamera != null && minimapCamera.targetTexture != null))
            {
                return;
            }

#if ENABLE_INPUT_SYSTEM
            bool togglePressed = Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame;
#if ENABLE_LEGACY_INPUT_MANAGER
            togglePressed = togglePressed || Input.GetKeyDown(KeyCode.M);
#endif
#else
            bool togglePressed = Input.GetKeyDown(KeyCode.M);
#endif
            if (togglePressed)
            {
                SetVisible(!isVisible);
            }
        }

        private Rect BuildViewportRect()
        {
            float size = Mathf.Clamp(Mathf.Max(viewportSize, 0.18f), 0.18f, 0.45f);
            return new Rect(
                Mathf.Clamp01(viewportMargin.x),
                Mathf.Clamp01(viewportMargin.y),
                size,
                size);
        }

        private void ResolvePlayerTransform()
        {
            if (IsUsablePlayerTransform(playerTransform))
            {
                return;
            }

            if (Time.unscaledTime < nextPlayerResolveTime)
            {
                return;
            }

            nextPlayerResolveTime = Time.unscaledTime + Mathf.Max(0.1f, playerResolveRetryInterval);
            if (TryResolveAuthoritativePlayerTransform(out Transform resolvedPlayerTransform))
            {
                playerTransform = resolvedPlayerTransform;
                return;
            }

            playerTransform = null;
        }

        private static bool IsUsablePlayerTransform(Transform candidate)
        {
            return candidate != null &&
                   candidate.gameObject != null &&
                   candidate.gameObject.activeInHierarchy;
        }

        private bool TryResolveAuthoritativePlayerTransform(out Transform resolvedPlayerTransform)
        {
            resolvedPlayerTransform = null;

            if (QuestManager.Instance != null && IsUsablePlayerTransform(QuestManager.Instance.PlayerTransform))
            {
                resolvedPlayerTransform = QuestManager.Instance.PlayerTransform;
                return true;
            }

            PlayerVehicleManager vehicleManager = TryGetVehicleManager();
            if (vehicleManager != null &&
                vehicleManager.ActiveVehicleController != null &&
                IsUsablePlayerTransform(vehicleManager.ActiveVehicleController.transform))
            {
                resolvedPlayerTransform = vehicleManager.ActiveVehicleController.transform;
                return true;
            }

            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null && IsUsablePlayerTransform(taggedPlayer.transform))
            {
                resolvedPlayerTransform = taggedPlayer.transform;
                return true;
            }

            CarController[] controllers = FindObjectsByType<CarController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Transform inactiveFallback = null;
            for (int i = 0; i < controllers.Length; i++)
            {
                CarController controller = controllers[i];
                if (controller == null || controller.transform == null)
                {
                    continue;
                }

                if (IsUsablePlayerTransform(controller.transform))
                {
                    resolvedPlayerTransform = controller.transform;
                    return true;
                }

                if (inactiveFallback == null)
                {
                    inactiveFallback = controller.transform;
                }
            }

            resolvedPlayerTransform = inactiveFallback;
            return resolvedPlayerTransform != null;
        }

        public void SetPlayer(Transform player)
        {
            playerTransform = player;
            nextPlayerResolveTime = 0f;
        }

        private PlayerVehicleManager TryGetVehicleManager()
        {
            if (cachedVehicleManager == null)
            {
                cachedVehicleManager = PlayerVehicleManager.Instance ?? FindFirstObjectByType<PlayerVehicleManager>();
            }

            return cachedVehicleManager;
        }

        private void HandleActiveVehicleChanged(CarController controller)
        {
            SetPlayer(controller != null ? controller.transform : null);
        }

        public void ConfigureStandalone(
            float followHeight,
            float zoom,
            bool rotate,
            bool allowToggle,
            float overlayViewportSize,
            Vector2 overlayViewportMargin,
            LayerMask overlayCullingMask,
            Color overlayBackgroundColor)
        {
            height = followHeight;
            orthographicSize = zoom;
            rotateWithPlayer = rotate;
            allowToggleKey = allowToggle;
            viewportSize = overlayViewportSize;
            viewportMargin = overlayViewportMargin;
            cullingMask = overlayCullingMask;
            backgroundColor = overlayBackgroundColor;
            SetupCamera();
        }

        public void ConfigureRuntime(
            float followHeight,
            float zoom,
            bool rotate,
            bool allowToggle,
            bool useOverlay,
            float overlayViewportSize,
            Vector2 overlayViewportMargin,
            LayerMask overlayCullingMask,
            Color overlayBackgroundColor)
        {
            height = followHeight;
            orthographicSize = zoom;
            rotateWithPlayer = rotate;
            allowToggleKey = allowToggle;
            useStandaloneOverlay = useOverlay;
            viewportSize = overlayViewportSize;
            viewportMargin = overlayViewportMargin;
            cullingMask = overlayCullingMask;
            backgroundColor = overlayBackgroundColor;
            SetupCamera();
        }

        public void SetUseStandaloneOverlay(bool useOverlay)
        {
            useStandaloneOverlay = useOverlay;
            SetupCamera();
        }

        public void SetVisible(bool visible)
        {
            isVisible = visible;
            if (minimapCamera != null)
            {
                minimapCamera.enabled = visible;
            }
        }

        public void SetZoom(float zoom)
        {
            orthographicSize = zoom;
            if (minimapCamera != null)
            {
                minimapCamera.orthographicSize = orthographicSize;
            }
        }
    }
}
