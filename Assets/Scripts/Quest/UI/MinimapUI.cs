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

        [Header("Route Preview")]
        [SerializeField] private bool showRoutePreview = true;
        [SerializeField] private bool includePlayerInRoute = true;
        [SerializeField] private Color routeLineColor = new Color(0.2f, 0.8f, 1f, 0.8f);
        [SerializeField] private float routeLineWidth = 2f;
        [SerializeField] private float routeRefreshInterval = 0.25f;
        [SerializeField] private RouteLineGraphic routeLine;

        [Header("Settings")]
        [SerializeField] private bool showMinimap = true;
        [SerializeField] private Vector2 minimapSize = new Vector2(200f, 200f);

        private GameObject currentPickupMarker;
        private List<GameObject> currentDeliveryMarkers = new List<GameObject>();
        private Vector3 currentPickupWorldPosition;
        private bool hasPickupMarkerWorldPosition;
        private readonly List<Vector3> currentDeliveryWorldPositions = new List<Vector3>();
        private GameObject playerMarker;
        private Transform playerTransform;
        private QuestData currentQuest;
        private float routeRefreshTimer;
        private bool subscribedToQuestEvents;

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
            if (!subscribedToQuestEvents)
            {
                SubscribeToQuestEvents();
            }

            if (playerMarker != null && playerTransform != null)
            {
                UpdatePlayerMarkerRotation();
            }

            UpdateObjectiveMarkerPositions();

            if (showRoutePreview && includePlayerInRoute && currentQuest != null)
            {
                routeRefreshTimer += Time.deltaTime;
                if (routeRefreshTimer >= routeRefreshInterval)
                {
                    UpdateRoutePreview(currentQuest);
                    routeRefreshTimer = 0f;
                }
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

            SetupRoutePreview();
            SetMinimapVisible(showMinimap);
        }

        private void SetupRoutePreview()
        {
            if (!showRoutePreview || minimapContainer == null)
            {
                return;
            }

            if (routeLine == null)
            {
                GameObject routeObject = new GameObject("RoutePreview");
                routeObject.transform.SetParent(minimapContainer, false);
                routeLine = routeObject.AddComponent<RouteLineGraphic>();
            }

            routeLine.color = routeLineColor;
            routeLine.SetLineWidth(routeLineWidth);

            RectTransform rectTransform = routeLine.rectTransform;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private void SubscribeToQuestEvents()
        {
            if (subscribedToQuestEvents || QuestManager.Instance == null)
            {
                return;
            }

            QuestManager.Instance.OnQuestStarted.AddListener(HandleQuestStarted);
            QuestManager.Instance.OnQuestUpdated.AddListener(HandleQuestUpdated);
            QuestManager.Instance.OnQuestCompleted.AddListener(HandleQuestCompleted);
            QuestManager.Instance.OnQuestFailed.AddListener(HandleQuestFailed);
            subscribedToQuestEvents = true;
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
            currentQuest = quest;

            if (quest == null)
            {
                ClearRoutePreview();
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

            UpdateRoutePreview(quest);
        }

        private void HandleQuestUpdated(QuestData quest)
        {
            if (quest == null)
            {
                currentQuest = null;
                ClearAllMarkers();
                ClearRoutePreview();
                return;
            }

            currentQuest = quest;

            // Update markers based on quest progress
            if (quest.HasPickedUpCargo && currentPickupMarker != null)
            {
                // Cargo picked up, remove pickup marker and show delivery markers
                Destroy(currentPickupMarker);
                currentPickupMarker = null;
                ShowDeliveryMarkers(quest);
            }
            else if (!quest.HasPickedUpCargo)
            {
                if (quest.PickupLocation != null && currentPickupMarker == null)
                {
                    ShowPickupMarker(quest.PickupLocation.Position);
                }
            }
            else if (quest.HasPickedUpCargo && currentDeliveryMarkers.Count == 0)
            {
                ShowDeliveryMarkers(quest);
            }

            UpdateRoutePreview(quest);
        }

        private void HandleQuestCompleted(QuestData quest)
        {
            ClearAllMarkers();
            currentQuest = null;
            ClearRoutePreview();
        }

        private void HandleQuestFailed(QuestData quest)
        {
            ClearAllMarkers();
            currentQuest = null;
            ClearRoutePreview();
        }

        private void ShowPickupMarker(Vector3 worldPosition)
        {
            if (markerContainer == null)
            {
                return;
            }

            if (currentPickupMarker != null)
            {
                Destroy(currentPickupMarker);
            }

            if (pickupMarkerPrefab != null)
            {
                currentPickupMarker = Instantiate(pickupMarkerPrefab, markerContainer);
            }
            else
            {
                currentPickupMarker = CreateDefaultMarkerUI(new Color(0.1f, 1f, 1f, 1f));
                currentPickupMarker.transform.SetParent(markerContainer, false);
            }
            currentPickupMarker.name = "PickupMarker";
            currentPickupWorldPosition = worldPosition;
            hasPickupMarkerWorldPosition = true;
            UpdateMarkerPosition(currentPickupMarker, worldPosition);
        }

        private void ShowDeliveryMarkers(QuestData quest)
        {
            if (markerContainer == null || quest.DeliveryLocations == null)
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
            currentDeliveryWorldPositions.Clear();

            // Show only the current delivery location
            if (quest.CurrentDeliveryIndex < quest.DeliveryLocations.Count)
            {
                QuestLocation currentDelivery = quest.DeliveryLocations[quest.CurrentDeliveryIndex];
                GameObject marker;
                if (deliveryMarkerPrefab != null)
                {
                    marker = Instantiate(deliveryMarkerPrefab, markerContainer);
                }
                else
                {
                    marker = CreateDefaultMarkerUI(new Color(1f, 0.9f, 0.05f, 1f));
                    marker.transform.SetParent(markerContainer, false);
                }
                marker.name = $"DeliveryMarker_{quest.CurrentDeliveryIndex}";
                currentDeliveryMarkers.Add(marker);
                Vector3 worldPosition = currentDelivery != null ? currentDelivery.Position : Vector3.zero;
                currentDeliveryWorldPositions.Add(worldPosition);
                UpdateMarkerPosition(marker, worldPosition);
            }
        }

        private void ClearAllMarkers()
        {
            if (currentPickupMarker != null)
            {
                Destroy(currentPickupMarker);
                currentPickupMarker = null;
            }
            hasPickupMarkerWorldPosition = false;

            foreach (var marker in currentDeliveryMarkers)
            {
                if (marker != null)
                {
                    Destroy(marker);
                }
            }
            currentDeliveryMarkers.Clear();
            currentDeliveryWorldPositions.Clear();
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

        private void UpdateRoutePreview(QuestData quest)
        {
            if (!showRoutePreview || routeLine == null || cameraComponent == null || minimapContainer == null)
            {
                return;
            }

            if (quest == null)
            {
                ClearRoutePreview();
                return;
            }

            List<Vector3> worldPoints = BuildRoutePoints(quest);
            if (includePlayerInRoute && playerTransform != null)
            {
                worldPoints.Insert(0, playerTransform.position);
            }

            if (worldPoints.Count < 2)
            {
                ClearRoutePreview();
                return;
            }

            Rect rect = minimapContainer.rect;
            List<Vector2> localPoints = new List<Vector2>(worldPoints.Count);
            for (int i = 0; i < worldPoints.Count; i++)
            {
                Vector3 viewport = cameraComponent.WorldToViewportPoint(worldPoints[i]);
                Vector2 local = new Vector2(
                    (viewport.x - 0.5f) * rect.width,
                    (viewport.y - 0.5f) * rect.height
                );
                localPoints.Add(local);
            }

            routeLine.color = routeLineColor;
            routeLine.SetLineWidth(routeLineWidth);
            routeLine.SetPoints(localPoints);
        }

        private void UpdateObjectiveMarkerPositions()
        {
            if (cameraComponent == null || minimapContainer == null)
            {
                return;
            }

            if (currentPickupMarker != null && hasPickupMarkerWorldPosition)
            {
                UpdateMarkerPosition(currentPickupMarker, currentPickupWorldPosition);
            }

            int count = Mathf.Min(currentDeliveryMarkers.Count, currentDeliveryWorldPositions.Count);
            for (int i = 0; i < count; i++)
            {
                GameObject marker = currentDeliveryMarkers[i];
                if (marker == null)
                {
                    continue;
                }

                UpdateMarkerPosition(marker, currentDeliveryWorldPositions[i]);
            }
        }

        private void UpdateMarkerPosition(GameObject markerObject, Vector3 worldPosition)
        {
            if (markerObject == null || cameraComponent == null || minimapContainer == null)
            {
                return;
            }

            Vector3 viewport = cameraComponent.WorldToViewportPoint(worldPosition);
            if (viewport.z <= 0f)
            {
                markerObject.SetActive(false);
                return;
            }

            markerObject.SetActive(true);

            // Clamp to minimap edge with padding so off-screen markers stay visible at the border
            const float edgePadding = 0.05f;
            float clampedX = Mathf.Clamp(viewport.x, edgePadding, 1f - edgePadding);
            float clampedY = Mathf.Clamp(viewport.y, edgePadding, 1f - edgePadding);

            Rect rect = minimapContainer.rect;
            Vector2 local = new Vector2(
                (clampedX - 0.5f) * rect.width,
                (clampedY - 0.5f) * rect.height
            );

            RectTransform markerRect = markerObject.GetComponent<RectTransform>();
            if (markerRect != null)
            {
                markerRect.anchoredPosition = local;
                return;
            }

            markerObject.transform.localPosition = new Vector3(local.x, local.y, 0f);
        }

        private GameObject CreateDefaultMarkerUI(Color color)
        {
            GameObject markerObj = new GameObject("DefaultMinimapMarker");
            RectTransform rect = markerObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(16f, 16f);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            Image img = markerObj.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return markerObj;
        }

        private List<Vector3> BuildRoutePoints(QuestData quest)
        {
            List<Vector3> points = new List<Vector3>();
            if (quest == null)
            {
                return points;
            }

            if (!quest.HasPickedUpCargo && quest.PickupLocation != null)
            {
                points.Add(quest.PickupLocation.Position);
            }

            if (quest.DeliveryLocations != null)
            {
                int startIndex = quest.HasPickedUpCargo ? quest.CurrentDeliveryIndex : 0;
                for (int i = startIndex; i < quest.DeliveryLocations.Count; i++)
                {
                    QuestLocation location = quest.DeliveryLocations[i];
                    if (location != null)
                    {
                        points.Add(location.Position);
                    }
                }
            }

            return points;
        }

        private void ClearRoutePreview()
        {
            if (routeLine != null)
            {
                routeLine.Clear();
            }
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
