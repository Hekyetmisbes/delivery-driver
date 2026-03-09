using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using DeliveryDriver.UI;
using TrafficSystem;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DeliveryDriver.Quest.UI
{
    /// <summary>
    /// Controls the minimap UI display and quest markers on the minimap
    /// </summary>
    public class MinimapUI : MonoBehaviour, IScrollHandler
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

        [Header("Zoom")]
        [SerializeField] private float minZoom = 100f;
        [SerializeField] private float maxZoom = 500f;
        [SerializeField] private float zoomStep = 50f;
        [SerializeField] private float zoomLerpSpeed = 8f;
        [SerializeField] private float scrollZoomStep = 30f;

        [Header("Marker Style")]
        [SerializeField] private Color pickupMarkerColor = new Color(0.2f, 0.6f, 1f, 1f);
        [SerializeField] private Color deliveryMarkerColor = new Color(0.2f, 0.9f, 0.4f, 1f);
        [SerializeField] private float markerSize = 22f;
        [SerializeField] private float markerPulseScale = 1.25f;
        [SerializeField] private float markerPulseSpeed = 2.5f;

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
        private float targetZoom;
        private float currentZoom;
        private bool zoomControlsBuilt;
        private readonly List<RectTransform> markerRects = new List<RectTransform>();
        private CanvasGroup minimapCanvasGroup;

        // Road graph pathfinding
        private RoadGraph roadGraph;
        private List<Vector3> cachedRoadPath;
        private Vector3 cachedPathPlayerPos;
        private QuestData cachedPathQuest;
        private bool cachedPathPickedUp;
        private int cachedPathDeliveryIndex;
        private bool cachedPathUsedRoadGraph;
        private const float PathRecalcDistanceThreshold = 5f;

        private void Awake()
        {
            InitializeZoom();
            SetupMinimap();
            BuildZoomControls();
        }

        private void Start()
        {
            ResolvePlayerTransform();
            ResolveRoadGraph();
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
#if ENABLE_INPUT_SYSTEM
            bool mPressed = Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame;
#if ENABLE_LEGACY_INPUT_MANAGER
            mPressed = mPressed || Input.GetKeyDown(KeyCode.M);
#endif
#else
            bool mPressed = Input.GetKeyDown(KeyCode.M);
#endif
            if (mPressed)
            {
                ToggleMinimap();
            }

            if (!subscribedToQuestEvents)
            {
                SubscribeToQuestEvents();
            }

            if (playerMarker != null && playerTransform != null)
            {
                UpdatePlayerMarkerRotation();
            }

            UpdateObjectiveMarkerPositions();
            UpdateZoomLerp();
            UpdateMarkerPulse();

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

            // Ensure road graph is available for route display
            ResolveRoadGraph();
            cachedRoadPath = null;

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
                // Force route recalculation on cargo state change
                cachedRoadPath = null;
                ResolveRoadGraph();
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
                currentPickupMarker = CreateDefaultMarkerUI(pickupMarkerColor);
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
                    marker = CreateDefaultMarkerUI(deliveryMarkerColor);
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
            markerRects.Clear();
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

            List<Vector3> worldPoints = BuildRoadRoutePoints(quest);

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

        private List<Vector3> BuildRoadRoutePoints(QuestData quest)
        {
            if (roadGraph == null)
            {
                ResolveRoadGraph();
            }

            Vector3 playerPos = (includePlayerInRoute && playerTransform != null)
                ? playerTransform.position
                : Vector3.zero;

            // Check if cached path is still valid
            bool needsRecalc = cachedRoadPath == null
                || cachedPathQuest != quest
                || cachedPathPickedUp != quest.HasPickedUpCargo
                || cachedPathDeliveryIndex != quest.CurrentDeliveryIndex
                || (!cachedPathUsedRoadGraph && roadGraph != null)
                || (includePlayerInRoute && playerTransform != null
                    && Vector3.Distance(playerPos, cachedPathPlayerPos) > PathRecalcDistanceThreshold);

            if (!needsRecalc)
            {
                return cachedRoadPath;
            }

            // Build straight-line waypoints as fallback
            List<Vector3> straightPoints = BuildRoutePoints(quest);
            if (includePlayerInRoute && playerTransform != null)
            {
                straightPoints.Insert(0, playerPos);
            }

            if (roadGraph == null || straightPoints.Count < 2)
            {
                cachedRoadPath = straightPoints;
                cachedPathPlayerPos = playerPos;
                cachedPathQuest = quest;
                cachedPathPickedUp = quest.HasPickedUpCargo;
                cachedPathDeliveryIndex = quest.CurrentDeliveryIndex;
                cachedPathUsedRoadGraph = false;
                return cachedRoadPath;
            }

            // Build road path between consecutive waypoints
            var roadPath = new List<Vector3>();
            roadPath.Add(straightPoints[0]);

            for (int i = 0; i < straightPoints.Count - 1; i++)
            {
                List<Vector3> segment = RoadGraphPathfinder.FindPath(
                    roadGraph, straightPoints[i], straightPoints[i + 1]);

                if (segment != null && segment.Count >= 2)
                {
                    // Skip first point to avoid duplicates (already added)
                    for (int j = 1; j < segment.Count; j++)
                    {
                        roadPath.Add(segment[j]);
                    }
                }
                else
                {
                    // Fallback: straight line
                    roadPath.Add(straightPoints[i + 1]);
                }
            }

            cachedRoadPath = roadPath;
            cachedPathPlayerPos = playerPos;
            cachedPathQuest = quest;
            cachedPathPickedUp = quest.HasPickedUpCargo;
            cachedPathDeliveryIndex = quest.CurrentDeliveryIndex;
            cachedPathUsedRoadGraph = true;
            return cachedRoadPath;
        }

        private void ResolveRoadGraph()
        {
            // Always re-check if the current graph is empty or stale
            if (roadGraph != null && roadGraph.roadSegments != null && roadGraph.roadSegments.Count > 0)
            {
                return;
            }

            RoadGraphBuilder builder = FindAnyObjectByType<RoadGraphBuilder>();
            if (builder == null)
            {
                return;
            }

            if (builder.HasBuiltRoadGraph)
            {
                roadGraph = builder.RoadGraph;
            }
            else if (!builder.HasPendingBuild)
            {
                // Graph not built yet and no build pending - trigger a build
                builder.BeginBuildWithDelay(0f);
            }
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
            rect.sizeDelta = new Vector2(markerSize, markerSize);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            Image img = markerObj.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;

            markerRects.Add(rect);
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
            cachedRoadPath = null;
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

        public void OnScroll(PointerEventData eventData)
        {
            float scroll = eventData.scrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                targetZoom -= scroll * scrollZoomStep;
                targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
            }
        }

        private void InitializeZoom()
        {
            if (GameSettings.Instance != null)
            {
                targetZoom = GameSettings.Instance.MinimapZoom;
            }
            else
            {
                targetZoom = Mathf.Clamp(250f, minZoom, maxZoom);
            }
            currentZoom = targetZoom;
        }

        private void UpdateZoomLerp()
        {
            if (Mathf.Abs(currentZoom - targetZoom) < 0.5f)
            {
                if (Mathf.Abs(currentZoom - targetZoom) > 0.01f)
                {
                    currentZoom = targetZoom;
                    ApplyZoom(currentZoom);
                }
                return;
            }

            currentZoom = Mathf.Lerp(currentZoom, targetZoom, zoomLerpSpeed * Time.deltaTime);
            ApplyZoom(currentZoom);
        }

        private void ApplyZoom(float zoom)
        {
            if (minimapCamera != null)
            {
                minimapCamera.SetZoom(zoom);
            }

            if (GameSettings.Instance != null)
            {
                GameSettings.Instance.SetMinimapZoom(zoom);
            }
        }

        private void ZoomIn()
        {
            targetZoom = Mathf.Clamp(targetZoom - zoomStep, minZoom, maxZoom);
        }

        private void ZoomOut()
        {
            targetZoom = Mathf.Clamp(targetZoom + zoomStep, minZoom, maxZoom);
        }

        private void BuildZoomControls()
        {
            if (zoomControlsBuilt || minimapContainer == null) return;
            zoomControlsBuilt = true;

            Sprite fallback = DeliveryUiSpriteHelper.GetFallbackSprite();

            // Zoom in button (+)
            GameObject zoomInObj = new GameObject("ZoomIn", typeof(RectTransform), typeof(Image), typeof(Button));
            zoomInObj.transform.SetParent(minimapContainer, false);
            RectTransform zoomInRect = zoomInObj.GetComponent<RectTransform>();
            zoomInRect.anchorMin = new Vector2(1f, 0f);
            zoomInRect.anchorMax = new Vector2(1f, 0f);
            zoomInRect.pivot = new Vector2(1f, 0f);
            zoomInRect.anchoredPosition = new Vector2(-4f, 30f);
            zoomInRect.sizeDelta = new Vector2(24f, 24f);

            Image zoomInImage = zoomInObj.GetComponent<Image>();
            zoomInImage.color = new Color(0.15f, 0.2f, 0.28f, 0.85f);
            if (fallback != null) zoomInImage.sprite = fallback;

            GameObject zoomInLabel = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            zoomInLabel.transform.SetParent(zoomInObj.transform, false);
            RectTransform zoomInLabelRect = zoomInLabel.GetComponent<RectTransform>();
            zoomInLabelRect.anchorMin = Vector2.zero;
            zoomInLabelRect.anchorMax = Vector2.one;
            zoomInLabelRect.offsetMin = Vector2.zero;
            zoomInLabelRect.offsetMax = Vector2.zero;
            TextMeshProUGUI zoomInText = zoomInLabel.GetComponent<TextMeshProUGUI>();
            zoomInText.text = "+";
            zoomInText.fontSize = 18f;
            zoomInText.alignment = TextAlignmentOptions.Center;
            zoomInText.color = Color.white;
            zoomInText.fontStyle = FontStyles.Bold;
            if (TMP_Settings.defaultFontAsset != null) zoomInText.font = TMP_Settings.defaultFontAsset;

            zoomInObj.GetComponent<Button>().onClick.AddListener(ZoomIn);

            // Zoom out button (-)
            GameObject zoomOutObj = new GameObject("ZoomOut", typeof(RectTransform), typeof(Image), typeof(Button));
            zoomOutObj.transform.SetParent(minimapContainer, false);
            RectTransform zoomOutRect = zoomOutObj.GetComponent<RectTransform>();
            zoomOutRect.anchorMin = new Vector2(1f, 0f);
            zoomOutRect.anchorMax = new Vector2(1f, 0f);
            zoomOutRect.pivot = new Vector2(1f, 0f);
            zoomOutRect.anchoredPosition = new Vector2(-4f, 4f);
            zoomOutRect.sizeDelta = new Vector2(24f, 24f);

            Image zoomOutImage = zoomOutObj.GetComponent<Image>();
            zoomOutImage.color = new Color(0.15f, 0.2f, 0.28f, 0.85f);
            if (fallback != null) zoomOutImage.sprite = fallback;

            GameObject zoomOutLabel = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            zoomOutLabel.transform.SetParent(zoomOutObj.transform, false);
            RectTransform zoomOutLabelRect = zoomOutLabel.GetComponent<RectTransform>();
            zoomOutLabelRect.anchorMin = Vector2.zero;
            zoomOutLabelRect.anchorMax = Vector2.one;
            zoomOutLabelRect.offsetMin = Vector2.zero;
            zoomOutLabelRect.offsetMax = Vector2.zero;
            TextMeshProUGUI zoomOutText = zoomOutLabel.GetComponent<TextMeshProUGUI>();
            zoomOutText.text = "-";
            zoomOutText.fontSize = 18f;
            zoomOutText.alignment = TextAlignmentOptions.Center;
            zoomOutText.color = Color.white;
            zoomOutText.fontStyle = FontStyles.Bold;
            if (TMP_Settings.defaultFontAsset != null) zoomOutText.font = TMP_Settings.defaultFontAsset;

            zoomOutObj.GetComponent<Button>().onClick.AddListener(ZoomOut);
        }

        private void UpdateMarkerPulse()
        {
            float pulse = 1f + (Mathf.Sin(Time.time * markerPulseSpeed * Mathf.PI) * 0.5f + 0.5f) * (markerPulseScale - 1f);
            Vector3 pulseScale = new Vector3(pulse, pulse, 1f);

            for (int i = 0; i < markerRects.Count; i++)
            {
                if (markerRects[i] != null)
                {
                    markerRects[i].localScale = pulseScale;
                }
            }
        }

        public void SetMinimapVisible(bool visible)
        {
            showMinimap = visible;

            if (minimapContainer != null)
            {
                // Use CanvasGroup instead of SetActive so the MinimapUI script
                // keeps running and the M key toggle always works.
                if (minimapCanvasGroup == null)
                {
                    minimapCanvasGroup = minimapContainer.GetComponent<CanvasGroup>();
                    if (minimapCanvasGroup == null)
                    {
                        minimapCanvasGroup = minimapContainer.gameObject.AddComponent<CanvasGroup>();
                    }
                }

                minimapCanvasGroup.alpha = visible ? 1f : 0f;
                minimapCanvasGroup.interactable = visible;
                minimapCanvasGroup.blocksRaycasts = visible;
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
