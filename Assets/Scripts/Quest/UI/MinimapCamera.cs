using UnityEngine;
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

        private Camera minimapCamera;
        private bool isVisible = true;

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

        private void LateUpdate()
        {
            HandleToggleInput();

            if (playerTransform == null)
            {
                ResolvePlayerTransform();
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

            if (useStandaloneOverlay && minimapCamera.targetTexture == null)
            {
                minimapCamera.rect = BuildViewportRect();
            }

            if (!rotateWithPlayer)
            {
                transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
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
            if (playerTransform != null)
            {
                return;
            }

            // Try to get from QuestManager
            if (QuestManager.Instance != null && QuestManager.Instance.PlayerTransform != null)
            {
                playerTransform = QuestManager.Instance.PlayerTransform;
                return;
            }

            // Try to find CarController
            CarController controller = FindAnyObjectByType<CarController>();
            if (controller != null)
            {
                playerTransform = controller.transform;
            }
        }

        public void SetPlayer(Transform player)
        {
            playerTransform = player;
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
