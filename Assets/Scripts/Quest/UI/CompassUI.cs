using UnityEngine;
using UnityEngine.UI;
using DeliveryDriver.Navigation;
using TMPro;

namespace DeliveryDriver.Quest.UI
{
    public class CompassUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private RectTransform compassNeedle;
        [SerializeField] private TextMeshProUGUI distanceText;
        [SerializeField] private TextMeshProUGUI directionText;
        [SerializeField] private Image compassBackground;

        [Header("Settings")]
        [SerializeField] private bool showCompass = true;
        [SerializeField] private bool smoothRotation = true;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private bool showDistance = true;
        [SerializeField] private bool showCardinalDirection = true;
        [SerializeField] private float distanceDisplayMultiplier = 10f;

        [Header("Colors")]
        [SerializeField] private Color pickupColor = new Color(0.2f, 0.5f, 1f, 1f);
        [SerializeField] private Color deliveryColor = new Color(0.2f, 1f, 0.2f, 1f);

        private Transform playerTransform;
        private Vector3 currentObjectivePosition;
        private bool hasObjective = false;
        private float targetRotation = 0f;
        private Image cachedNeedleImage;
        private int frameCounter;
        private NavigationService subscribedNavigationService;

        private void Start()
        {
            if (compassNeedle != null)
            {
                compassNeedle.TryGetComponent(out cachedNeedleImage);
            }

            ResolvePlayerTransform();
            TryBindNavigationService();
            SetCompassVisible(showCompass);
        }

        private void OnDestroy()
        {
            UnbindNavigationService();
        }

        private void Update()
        {
            if (!hasObjective || playerTransform == null)
            {
                TryBindNavigationService();
                return;
            }

            TryBindNavigationService();
            UpdateCompassDirection();

            frameCounter++;
            if (frameCounter % 3 != 0) return;

            if (showDistance)
            {
                UpdateDistanceDisplay();
            }

            if (showCardinalDirection)
            {
                UpdateCardinalDirection();
            }
        }

        private void TryBindNavigationService()
        {
            NavigationService navigationService = NavigationService.Instance;
            if (navigationService == null || subscribedNavigationService == navigationService)
            {
                return;
            }

            UnbindNavigationService();

            subscribedNavigationService = navigationService;
            subscribedNavigationService.OnObjectiveChanged += HandleObjectiveChanged;
            subscribedNavigationService.OnNavigationCleared += HandleNavigationCleared;

            NavigationObjective objective = subscribedNavigationService.CurrentObjective;
            if (objective.IsValid)
            {
                HandleObjectiveChanged(objective);
            }
            else
            {
                HandleNavigationCleared();
            }
        }

        private void UnbindNavigationService()
        {
            if (subscribedNavigationService == null)
            {
                return;
            }

            subscribedNavigationService.OnObjectiveChanged -= HandleObjectiveChanged;
            subscribedNavigationService.OnNavigationCleared -= HandleNavigationCleared;
            subscribedNavigationService = null;
        }

        private void HandleObjectiveChanged(NavigationObjective objective)
        {
            if (!objective.IsValid)
            {
                hasObjective = false;
                SetCompassVisible(false);
                return;
            }

            currentObjectivePosition = objective.WorldPosition;
            hasObjective = true;

            if (cachedNeedleImage != null)
            {
                cachedNeedleImage.color = objective.Type == ObjectiveType.Delivery ? deliveryColor : pickupColor;
            }

            SetCompassVisible(true);
        }

        private void HandleNavigationCleared()
        {
            hasObjective = false;
            SetCompassVisible(false);
        }

        private void UpdateCompassDirection()
        {
            if (compassNeedle == null || playerTransform == null)
            {
                return;
            }

            Vector3 directionToObjective = currentObjectivePosition - playerTransform.position;
            directionToObjective.y = 0;

            if (directionToObjective.magnitude < 0.1f)
            {
                return;
            }

            float angleToObjective = Vector3.SignedAngle(playerTransform.forward, directionToObjective, Vector3.up);
            targetRotation = -angleToObjective;

            if (smoothRotation)
            {
                float currentZ = compassNeedle.localEulerAngles.z;
                if (Mathf.Abs(targetRotation - currentZ) > 180f)
                {
                    if (targetRotation > currentZ)
                    {
                        currentZ += 360f;
                    }
                    else
                    {
                        targetRotation += 360f;
                    }
                }

                float smoothedRotation = Mathf.LerpAngle(currentZ, targetRotation, rotationSpeed * Time.deltaTime);
                compassNeedle.localRotation = Quaternion.Euler(0f, 0f, smoothedRotation);
            }
            else
            {
                compassNeedle.localRotation = Quaternion.Euler(0f, 0f, targetRotation);
            }
        }

        private void UpdateDistanceDisplay()
        {
            if (distanceText == null || playerTransform == null)
            {
                return;
            }

            float distance = Vector3.Distance(playerTransform.position, currentObjectivePosition);
            float displayedDistance = distance * distanceDisplayMultiplier;

            if (displayedDistance < 1000f)
            {
                distanceText.text = $"{displayedDistance:F0}m";
            }
            else
            {
                distanceText.text = $"{displayedDistance / 1000f:F1}km";
            }
        }

        private void UpdateCardinalDirection()
        {
            if (directionText == null || playerTransform == null)
            {
                return;
            }

            Vector3 directionToObjective = currentObjectivePosition - playerTransform.position;
            directionToObjective.y = 0;

            float angle = Vector3.SignedAngle(playerTransform.forward, directionToObjective, Vector3.up);

            string direction = GetCardinalDirection(angle);
            directionText.text = direction;
        }

        private string GetCardinalDirection(float angle)
        {
            if (angle < 0)
            {
                angle += 360f;
            }

            if (angle >= 337.5f || angle < 22.5f)
                return "N";
            else if (angle >= 22.5f && angle < 67.5f)
                return "NE";
            else if (angle >= 67.5f && angle < 112.5f)
                return "E";
            else if (angle >= 112.5f && angle < 157.5f)
                return "SE";
            else if (angle >= 157.5f && angle < 202.5f)
                return "S";
            else if (angle >= 202.5f && angle < 247.5f)
                return "SW";
            else if (angle >= 247.5f && angle < 292.5f)
                return "W";
            else
                return "NW";
        }

        private void ResolvePlayerTransform()
        {
            if (playerTransform != null)
            {
                return;
            }

            if (QuestManager.Instance != null && QuestManager.Instance.PlayerTransform != null)
            {
                playerTransform = QuestManager.Instance.PlayerTransform;
                return;
            }

            CarController controller = FindAnyObjectByType<CarController>();
            if (controller != null)
            {
                playerTransform = controller.transform;
            }
        }

        public void SetPlayerTransform(Transform player)
        {
            playerTransform = player;
        }

        public void SetCompassVisible(bool visible)
        {
            showCompass = visible;
            gameObject.SetActive(visible && hasObjective);
        }

        public void ToggleCompass()
        {
            SetCompassVisible(!showCompass);
        }
    }
}
