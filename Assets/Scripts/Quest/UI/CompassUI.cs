using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DeliveryDriver.Quest.UI
{
    /// <summary>
    /// Displays a compass HUD element showing direction to the next quest objective
    /// </summary>
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

        [Header("Colors")]
        [SerializeField] private Color pickupColor = new Color(0.2f, 0.5f, 1f, 1f); // Blue
        [SerializeField] private Color deliveryColor = new Color(0.2f, 1f, 0.2f, 1f); // Green

        private Transform playerTransform;
        private Vector3 currentObjectivePosition;
        private bool hasObjective = false;
        private float targetRotation = 0f;

        private void Start()
        {
            ResolvePlayerTransform();
            SubscribeToQuestEvents();
            SetCompassVisible(showCompass);
        }

        private void OnDestroy()
        {
            UnsubscribeFromQuestEvents();
        }

        private void Update()
        {
            if (!hasObjective || playerTransform == null)
            {
                return;
            }

            UpdateCompassDirection();

            if (showDistance)
            {
                UpdateDistanceDisplay();
            }

            if (showCardinalDirection)
            {
                UpdateCardinalDirection();
            }
        }

        private void SubscribeToQuestEvents()
        {
            if (QuestManager.Instance == null)
            {
                return;
            }

            QuestManager.Instance.OnQuestStarted.AddListener(HandleQuestStarted);
            QuestManager.Instance.OnQuestUpdated.AddListener(HandleQuestUpdated);
            QuestManager.Instance.OnQuestCompleted.AddListener(HandleQuestCompleted);
            QuestManager.Instance.OnQuestFailed.AddListener(HandleQuestFailed);
        }

        private void UnsubscribeFromQuestEvents()
        {
            if (QuestManager.Instance == null)
            {
                return;
            }

            QuestManager.Instance.OnQuestStarted.RemoveListener(HandleQuestStarted);
            QuestManager.Instance.OnQuestUpdated.RemoveListener(HandleQuestUpdated);
            QuestManager.Instance.OnQuestCompleted.RemoveListener(HandleQuestCompleted);
            QuestManager.Instance.OnQuestFailed.RemoveListener(HandleQuestFailed);
        }

        private void HandleQuestStarted(QuestData quest)
        {
            if (quest == null)
            {
                return;
            }

            UpdateObjective(quest);
            SetCompassVisible(true);
        }

        private void HandleQuestUpdated(QuestData quest)
        {
            if (quest == null)
            {
                return;
            }

            UpdateObjective(quest);
        }

        private void HandleQuestCompleted(QuestData quest)
        {
            hasObjective = false;
            SetCompassVisible(false);
        }

        private void HandleQuestFailed(QuestData quest)
        {
            hasObjective = false;
            SetCompassVisible(false);
        }

        private void UpdateObjective(QuestData quest)
        {
            QuestLocation objective = GetCurrentObjective(quest);

            if (objective == null)
            {
                hasObjective = false;
                return;
            }

            currentObjectivePosition = objective.Position;
            hasObjective = true;

            // Update compass color based on objective type
            if (compassNeedle != null)
            {
                Image needleImage = compassNeedle.GetComponent<Image>();
                if (needleImage != null)
                {
                    needleImage.color = quest.HasPickedUpCargo ? deliveryColor : pickupColor;
                }
            }
        }

        private void UpdateCompassDirection()
        {
            if (compassNeedle == null || playerTransform == null)
            {
                return;
            }

            // Calculate direction to objective
            Vector3 directionToObjective = currentObjectivePosition - playerTransform.position;
            directionToObjective.y = 0; // Flatten to horizontal plane

            if (directionToObjective.magnitude < 0.1f)
            {
                return;
            }

            // Calculate angle relative to player's forward direction
            float angleToObjective = Vector3.SignedAngle(playerTransform.forward, directionToObjective, Vector3.up);
            targetRotation = -angleToObjective; // Negative because UI rotates opposite

            // Apply rotation
            if (smoothRotation)
            {
                float currentZ = compassNeedle.localEulerAngles.z;
                // Handle 360-degree wrap
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

            if (distance < 1000f)
            {
                distanceText.text = $"{distance:F0}m";
            }
            else
            {
                distanceText.text = $"{distance / 1000f:F1}km";
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

            // Convert to compass directions
            string direction = GetCardinalDirection(angle);
            directionText.text = direction;
        }

        private string GetCardinalDirection(float angle)
        {
            // Normalize angle to 0-360
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

        private QuestLocation GetCurrentObjective(QuestData quest)
        {
            if (quest == null)
            {
                return null;
            }

            if (!quest.HasPickedUpCargo)
            {
                return quest.PickupLocation;
            }

            if (quest.DeliveryLocations == null || quest.DeliveryLocations.Count == 0)
            {
                return null;
            }

            int index = Mathf.Clamp(quest.CurrentDeliveryIndex, 0, quest.DeliveryLocations.Count - 1);
            return quest.DeliveryLocations[index];
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
