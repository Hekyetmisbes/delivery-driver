using UnityEngine;

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
        [SerializeField] private float height = 200f;
        [SerializeField] private float orthographicSize = 100f;
        [SerializeField] private bool rotateWithPlayer = false;

        [Header("Smoothing")]
        [SerializeField] private bool smoothFollow = true;
        [SerializeField] private float smoothSpeed = 10f;

        private Camera minimapCamera;

        private void Awake()
        {
            minimapCamera = GetComponent<Camera>();
            SetupCamera();
        }

        private void Start()
        {
            ResolvePlayerTransform();
        }

        private void LateUpdate()
        {
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
            minimapCamera.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f);

            // Set the camera to look down
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
            // Match player's Y rotation plus 90 degrees to look down
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
            TrafficSystem.CarController controller = FindAnyObjectByType<TrafficSystem.CarController>();
            if (controller != null)
            {
                playerTransform = controller.transform;
            }
        }

        public void SetPlayer(Transform player)
        {
            playerTransform = player;
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
