using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace DeliveryDriver.Quest.UI
{
    /// <summary>
    /// Controls the minimap UI display and quest markers on the minimap
    /// </summary>
    public class MinimapUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private RawImage minimapImage;
        [SerializeField] private RectTransform minimapContainer;
        [SerializeField] private Transform markerContainer;

        [Header("Camera")]
        [SerializeField] private MinimapCamera minimapCamera;
        [SerializeField] private Camera cameraComponent;
        [SerializeField] private RenderTexture minimapRenderTexture;

        [Header("Markers")]
        [SerializeField] private GameObject pickupMarkerPrefab;
        [SerializeField] private GameObject deliveryMarkerPrefab;
        [SerializeField] private GameObject playerMarkerPrefab;

        [Header("Settings")]
        [SerializeField] private bool showMinimap = true;
        [SerializeField] private Vector2 minimapSize = new Vector2(200f, 200f);

        private GameObject currentPickupMarker;
        private List<GameObject> currentDeliveryMarkers = new List<GameObject>();
        private GameObject playerMarker;
        private Transform playerTransform;

        private void Awake()
        {
            SetupMinimap();
        }

        private void Start()
        {
            ResolvePlayerTransform();
            SubscribeToQuestEvents();

            if (playerMarkerPrefab != null && markerContainer != null)
            {
                playerMarker = Instantiate(playerMarkerPrefab, markerContainer);
                playerMarker.name = "PlayerMarker";
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromQuestEvents();
        }

        private void Update()
        {
            if (playerMarker != null && playerTransform != null)
            {
                UpdatePlayerMarkerRotation();
            }
        }

        private void SetupMinimap()
        {
            if (minimapImage == null || cameraComponent == null)
            {
                return;
            }

            // Create render texture if not assigned
            if (minimapRenderTexture == null)
            {
                minimapRenderTexture = new RenderTexture(512, 512, 16);
                minimapRenderTexture.name = "MinimapRT";
            }

            cameraComponent.targetTexture = minimapRenderTexture;
            minimapImage.texture = minimapRenderTexture;

            // Set minimap size
            if (minimapContainer != null)
            {
                minimapContainer.sizeDelta = minimapSize;
            }

            SetMinimapVisible(showMinimap);
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
            ClearAllMarkers();

            if (quest == null)
            {
                return;
            }

            // Show pickup marker if cargo not picked up
            if (!quest.HasPickedUpCargo && quest.PickupLocation != null)
            {
                ShowPickupMarker(quest.PickupLocation.Position);
            }
            // Show delivery markers if cargo picked up
            else if (quest.HasPickedUpCargo && quest.DeliveryLocations != null)
            {
                ShowDeliveryMarkers(quest);
            }
        }

        private void HandleQuestUpdated(QuestData quest)
        {
            if (quest == null)
            {
                return;
            }

            // Update markers based on quest progress
            if (quest.HasPickedUpCargo && currentPickupMarker != null)
            {
                // Cargo picked up, remove pickup marker and show delivery markers
                Destroy(currentPickupMarker);
                currentPickupMarker = null;
                ShowDeliveryMarkers(quest);
            }
        }

        private void HandleQuestCompleted(QuestData quest)
        {
            ClearAllMarkers();
        }

        private void HandleQuestFailed(QuestData quest)
        {
            ClearAllMarkers();
        }

        private void ShowPickupMarker(Vector3 worldPosition)
        {
            if (pickupMarkerPrefab == null || markerContainer == null)
            {
                return;
            }

            currentPickupMarker = Instantiate(pickupMarkerPrefab, markerContainer);
            currentPickupMarker.name = "PickupMarker";
            // Position will be handled by the marker script or manually
        }

        private void ShowDeliveryMarkers(QuestData quest)
        {
            if (deliveryMarkerPrefab == null || markerContainer == null || quest.DeliveryLocations == null)
            {
                return;
            }

            // Clear existing delivery markers
            foreach (var marker in currentDeliveryMarkers)
            {
                if (marker != null)
                {
                    Destroy(marker);
                }
            }
            currentDeliveryMarkers.Clear();

            // Show only the current delivery location
            if (quest.CurrentDeliveryIndex < quest.DeliveryLocations.Count)
            {
                QuestLocation currentDelivery = quest.DeliveryLocations[quest.CurrentDeliveryIndex];
                GameObject marker = Instantiate(deliveryMarkerPrefab, markerContainer);
                marker.name = $"DeliveryMarker_{quest.CurrentDeliveryIndex}";
                currentDeliveryMarkers.Add(marker);
            }
        }

        private void ClearAllMarkers()
        {
            if (currentPickupMarker != null)
            {
                Destroy(currentPickupMarker);
                currentPickupMarker = null;
            }

            foreach (var marker in currentDeliveryMarkers)
            {
                if (marker != null)
                {
                    Destroy(marker);
                }
            }
            currentDeliveryMarkers.Clear();
        }

        private void UpdatePlayerMarkerRotation()
        {
            if (playerMarker == null || playerTransform == null)
            {
                return;
            }

            // Rotate player marker to match player's heading
            float yRotation = playerTransform.eulerAngles.y;
            playerMarker.transform.rotation = Quaternion.Euler(0f, 0f, -yRotation);
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

        public void SetMinimapVisible(bool visible)
        {
            showMinimap = visible;

            if (minimapContainer != null)
            {
                minimapContainer.gameObject.SetActive(visible);
            }

            if (cameraComponent != null)
            {
                cameraComponent.enabled = visible;
            }
        }

        public void ToggleMinimap()
        {
            SetMinimapVisible(!showMinimap);
        }
    }
}
