using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using DeliveryDriver.Navigation;
using DeliveryDriver.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DeliveryDriver.Quest.UI
{
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
        [SerializeField] private Color routeLineColor = new Color(0.2f, 0.8f, 1f, 0.8f);
        [SerializeField] private float routeLineWidth = 2f;
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
        private Vector3 currentObjectiveWorldPosition;
        private bool hasObjectiveWorldPosition;
        private GameObject playerMarker;
        private Transform playerTransform;
        private float targetZoom;
        private float currentZoom;
        private bool zoomControlsBuilt;
        private NavigationService subscribedNavigationService;
        private readonly List<RectTransform> markerRects = new List<RectTransform>();
        private CanvasGroup minimapCanvasGroup;

        private void Awake()
        {
            InitializeZoom();
            SetupMinimap();
            BuildZoomControls();
        }

        private void Start()
        {
            ResolvePlayerTransform();

            if (playerMarkerPrefab != null && markerContainer != null)
            {
                playerMarker = Instantiate(playerMarkerPrefab, markerContainer);
                playerMarker.name = "PlayerMarker";
            }

            TryBindNavigationService();
        }

        private void OnDestroy()
        {
            UnbindNavigationService();
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

            if (playerTransform == null)
            {
                ResolvePlayerTransform();
            }

            TryBindNavigationService();

            if (playerMarker != null && playerTransform != null)
            {
                UpdatePlayerMarkerRotation();
            }

            UpdateObjectiveMarkerPosition();
            UpdateZoomLerp();
            UpdateMarkerPulse();
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
            subscribedNavigationService.OnRouteChanged += HandleRouteChanged;
            subscribedNavigationService.OnNavigationCleared += HandleNavigationCleared;

            SyncNavigationState();
        }

        private void UnbindNavigationService()
        {
            if (subscribedNavigationService == null)
            {
                return;
            }

            subscribedNavigationService.OnObjectiveChanged -= HandleObjectiveChanged;
            subscribedNavigationService.OnRouteChanged -= HandleRouteChanged;
            subscribedNavigationService.OnNavigationCleared -= HandleNavigationCleared;
            subscribedNavigationService = null;
        }

        private void SyncNavigationState()
        {
            if (subscribedNavigationService == null)
            {
                return;
            }

            NavigationObjective objective = subscribedNavigationService.CurrentObjective;
            if (objective.IsValid)
            {
                HandleObjectiveChanged(objective);
            }
            else
            {
                HandleNavigationCleared();
            }

            HandleRouteChanged(subscribedNavigationService.CurrentRoute);
        }

        private void HandleObjectiveChanged(NavigationObjective objective)
        {
            ClearAllMarkers();

            if (!objective.IsValid)
            {
                hasObjectiveWorldPosition = false;
                ClearRoutePreview();
                return;
            }

            currentObjectiveWorldPosition = objective.WorldPosition;
            hasObjectiveWorldPosition = true;

            if (objective.Type == ObjectiveType.Pickup)
            {
                ShowPickupMarker(objective.WorldPosition);
            }
            else if (objective.Type == ObjectiveType.Delivery)
            {
                ShowDeliveryMarker(objective.WorldPosition, objective.DeliveryIndex);
            }
        }

        private void HandleRouteChanged(RouteResult route)
        {
            if (!showRoutePreview || routeLine == null || cameraComponent == null || minimapContainer == null)
            {
                return;
            }

            if (route == null || !route.IsValid)
            {
                ClearRoutePreview();
                return;
            }

            Rect rect = minimapContainer.rect;
            List<Vector2> localPoints = new List<Vector2>(route.Points.Count);
            for (int i = 0; i < route.Points.Count; i++)
            {
                Vector3 viewport = cameraComponent.WorldToViewportPoint(route.Points[i]);
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

        private void HandleNavigationCleared()
        {
            ClearAllMarkers();
            hasObjectiveWorldPosition = false;
            ClearRoutePreview();
        }

        private void SetupMinimap()
        {
            if (minimapImage == null || cameraComponent == null)
            {
                return;
            }

            if (minimapRenderTexture == null)
            {
                minimapRenderTexture = new RenderTexture(512, 512, 16);
                minimapRenderTexture.name = "MinimapRT";
            }

            cameraComponent.targetTexture = minimapRenderTexture;
            minimapImage.texture = minimapRenderTexture;

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
            UpdateMarkerPosition(currentPickupMarker, worldPosition);
        }

        private void ShowDeliveryMarker(Vector3 worldPosition, int deliveryIndex)
        {
            if (markerContainer == null)
            {
                return;
            }

            foreach (var marker in currentDeliveryMarkers)
            {
                if (marker != null)
                {
                    Destroy(marker);
                }
            }
            currentDeliveryMarkers.Clear();

            GameObject markerObj;
            if (deliveryMarkerPrefab != null)
            {
                markerObj = Instantiate(deliveryMarkerPrefab, markerContainer);
            }
            else
            {
                markerObj = CreateDefaultMarkerUI(deliveryMarkerColor);
                markerObj.transform.SetParent(markerContainer, false);
            }
            markerObj.name = $"DeliveryMarker_{deliveryIndex}";
            currentDeliveryMarkers.Add(markerObj);
            UpdateMarkerPosition(markerObj, worldPosition);
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
            markerRects.Clear();
        }

        private void UpdatePlayerMarkerRotation()
        {
            if (playerMarker == null || playerTransform == null)
            {
                return;
            }

            float yRotation = playerTransform.eulerAngles.y;
            playerMarker.transform.rotation = Quaternion.Euler(0f, 0f, -yRotation);
        }

        private void UpdateObjectiveMarkerPosition()
        {
            if (cameraComponent == null || minimapContainer == null || !hasObjectiveWorldPosition)
            {
                return;
            }

            if (currentPickupMarker != null)
            {
                UpdateMarkerPosition(currentPickupMarker, currentObjectiveWorldPosition);
            }

            for (int i = 0; i < currentDeliveryMarkers.Count; i++)
            {
                GameObject marker = currentDeliveryMarkers[i];
                if (marker != null)
                {
                    UpdateMarkerPosition(marker, currentObjectiveWorldPosition);
                }
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
