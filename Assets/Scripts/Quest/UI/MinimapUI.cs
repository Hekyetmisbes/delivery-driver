using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using DeliveryDriver.Navigation;
using DeliveryDriver.Company;
using TMPro;
using TrafficSystem;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DeliveryDriver.Quest.UI
{
    public class MinimapUI : MonoBehaviour
    {
        private sealed class MarkerView
        {
            public RectTransform Root;
            public RectTransform Shadow;
            public Image Icon;
            public TextMeshProUGUI Label;
            public Vector3 BaseScale = Vector3.one;

            public void SetVisible(bool visible)
            {
                if (Root != null && Root.gameObject.activeSelf != visible)
                {
                    Root.gameObject.SetActive(visible);
                }

                if (Shadow != null && Shadow.gameObject.activeSelf != visible)
                {
                    Shadow.gameObject.SetActive(visible);
                }
            }
        }

        private const string RuntimeRootName = "MinimapUI";
        private const string QuestCanvasName = "Quest UI Canvas";
        private const string SpriteRegistryResourcePath = "Minimap/MinimapSpriteRegistry";
        private const float MinAllowedZoom = 12f;
        private const float MaxAllowedZoom = 220f;
        private static readonly Vector2 ObjectiveMarkerShadowOffset = new Vector2(3f, -3f);
        private static readonly Vector2 PlayerMarkerShadowOffset = new Vector2(2f, -2f);
        private static Sprite circleMaskSprite;
        private static Texture2D circleMaskTexture;
        private static Sprite solidSprite;

        [Header("UI References")]
        [SerializeField] private RawImage minimapImage;
        [SerializeField] private RectTransform minimapContainer;
        [SerializeField] private Transform markerContainer;

        [Header("Camera")]
        [SerializeField] private MinimapCamera minimapCamera;
        [SerializeField] private Camera cameraComponent;

        [Header("Markers")]
        [SerializeField] private GameObject pickupMarkerPrefab;
        [SerializeField] private GameObject deliveryMarkerPrefab;
        [SerializeField] private GameObject playerMarkerPrefab;

        [Header("Route Preview")]
        [SerializeField] private bool showRoutePreview = true;
        [SerializeField] private Color routeLineColor = new Color(1f, 0.81f, 0.34f, 0.96f);
        [SerializeField] private Color fallbackRouteLineColor = new Color(1f, 0.88f, 0.56f, 0.72f);
        [SerializeField] private float routeLineWidth = 4.8f;
        [SerializeField] private RouteLineGraphic routeLine;
        [SerializeField] private MinimapRoadGraphic roadGraphic;

        [Header("Settings")]
        [SerializeField] private bool showMinimap = true;
        [SerializeField] private Vector2 minimapSize = new Vector2(218f, 218f);
        [SerializeField] private Vector2 minimumMinimapSize = new Vector2(192f, 192f);
        [SerializeField] private Vector2 maximumMinimapSize = new Vector2(236f, 236f);
        [SerializeField] private Vector2 anchorOffset = new Vector2(24f, 24f);
        [SerializeField] private float responsiveReferenceShortSide = 1080f;
        [SerializeField] private float framePadding = 6f;
        [SerializeField] private float viewportPadding = 12f;
        [SerializeField, Range(0.0f, 0.30f)] private float panelAlpha = 0.10f;
        [SerializeField, Range(0.78f, 1f)] private float mapAlpha = 0.96f;
        [SerializeField] private Color panelColor = new Color(0.02f, 0.03f, 0.05f, 1f);
        [SerializeField] private Color panelOutlineColor = new Color(0.80f, 0.88f, 0.96f, 0.76f);
        [SerializeField] private Color panelShadowColor = new Color(0f, 0f, 0f, 0.22f);
        [SerializeField] private Color mapBackgroundColor = new Color(0.17f, 0.21f, 0.24f, 0.96f);
        [SerializeField] private Color frameColor = new Color(0.10f, 0.12f, 0.15f, 0.94f);

        [Header("Scale")]
        [SerializeField] private float fixedZoom = 126f;
        [SerializeField] private bool alignMapToPlayerHeading = true;

        [Header("Marker Style")]
        [SerializeField] private Color pickupMarkerColor = new Color(0.25f, 0.77f, 1f, 1f);
        [SerializeField] private Color deliveryMarkerColor = new Color(0.28f, 0.95f, 0.58f, 1f);
        [SerializeField] private Color playerMarkerColor = new Color(0.98f, 0.99f, 1f, 1f);
        [SerializeField] private Color markerFrameColor = new Color(0.08f, 0.10f, 0.12f, 0.96f);
        [SerializeField] private float markerSize = 22f;
        [SerializeField] private float playerMarkerSize = 26f;
        [SerializeField] private float markerPulseScale = 1.08f;
        [SerializeField] private float markerPulseSpeed = 2.2f;
        [SerializeField] private bool clampObjectivesToEdge = true;
        [SerializeField, Range(0.02f, 0.12f)] private float edgePaddingNormalized = 0.10f;
        [SerializeField, Range(0.5f, 0.8f)] private float routeViewportExpansion = 0.68f;

        [Header("Road Overlay")]
        [SerializeField] private Color roadColor = new Color(0.77f, 0.82f, 0.88f, 1f);
        [SerializeField] private Color roadOutlineColor = new Color(0.09f, 0.12f, 0.16f, 0.96f);
        [SerializeField] private float roadBoundsPadding = 40f;
        [SerializeField] private float roadRetryInterval = 1f;
        [SerializeField] private float routeRefreshDistance = 2f;
        [SerializeField] private float roadRefreshDistance = 4f;
        [SerializeField] private float baseRoadLineWidth = 4.2f;
        [SerializeField] private float navigationBindRetryInterval = 0.5f;
        [SerializeField] private float playerResolveRetryInterval = 0.5f;
        [SerializeField] private float roadGraphResolveRetryInterval = 1f;

        private GameObject currentPickupMarker;
        private List<GameObject> currentDeliveryMarkers = new List<GameObject>();
        private Vector3 currentObjectiveWorldPosition;
        private bool hasObjectiveWorldPosition;
        private GameObject playerMarker;
        private Transform playerTransform;
        private float currentZoom;
        private NavigationService subscribedNavigationService;
        private readonly List<Vector2> routeLocalPoints = new List<Vector2>();
        private CanvasGroup minimapCanvasGroup;
        private NavigationObjective currentObjective = NavigationObjective.Empty;
        private bool minimapRuntimeReady;
        private bool navigationStateDirty;
        private MarkerView pooledPickupMarker;
        private MarkerView pooledDeliveryMarker;
        private MarkerView pooledPlayerMarker;
        private MinimapSpriteRegistry spriteRegistry;
        private Bounds roadBounds;
        private bool hasRoadBounds;
        private bool roadOverlayReady;
        private float nextRoadAttemptTime;
        private float nextNavigationBindTime;
        private float nextPlayerResolveTime;
        private float nextRoadGraphResolveTime;
        private RoadGraphBuilder roadGraphBuilder;
        private PlayerVehicleManager cachedVehicleManager;
        private Vector3 mapCenter;
        private RouteResult currentRoute = RouteResult.Unavailable;
        private Vector3 lastRouteCenter = new Vector3(float.NaN, float.NaN, float.NaN);
        private float lastRouteZoom = -1f;
        private Vector2 lastScreenSize = Vector2.negativeInfinity;
        private bool routeDirty = true;
        private readonly List<List<Vector3>> worldRoadPolylines = new List<List<Vector3>>();
        private readonly List<List<Vector2>> localRoadPolylines = new List<List<Vector2>>();
        private Vector3 lastRoadCenter = new Vector3(float.NaN, float.NaN, float.NaN);
        private float lastRoadZoom = -1f;
        private float lastRouteHeading = float.NaN;
        private float lastRoadHeading = float.NaN;
        private RoadGraph lastRoadGraphSource;
        private int lastRoadGraphSegmentCount = -1;
        private RectTransform viewportRect;
        private RectTransform viewportFrameRect;
        private bool loggedMissingNavigationService;
        private bool loggedMissingSpriteRegistry;
        private bool loggedMissingRoadGraphBuilder;
        private bool loggedMissingRoadPolylines;
        private bool loggedMissingRoadGraph;
        private bool loggedMissingPlayerArrowSprite;
        private bool loggedMissingPlayerTransform;
        private bool loggedEmptyLocalRoadPolylines;

        public MinimapCamera CameraController => minimapCamera;

        public static MinimapUI EnsureSceneInstance()
        {
            MinimapUI existing = FindFirstObjectByType<MinimapUI>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.TryInitializeMinimapRuntime(true);
                return existing;
            }

            Transform parent = ResolvePreferredParent();
            GameObject root = new GameObject(RuntimeRootName, typeof(RectTransform));
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            return root.AddComponent<MinimapUI>();
        }

        private void Awake()
        {
            ApplyFixedZoom();
            TryInitializeMinimapRuntime(true);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            ResolvePlayerTransform(true);
            TryInitializeMinimapRuntime(true);
            TryBindNavigationService();
            PlayerVehicleManager.ActiveVehicleChanged += HandleActiveVehicleChanged;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            PlayerVehicleManager.ActiveVehicleChanged -= HandleActiveVehicleChanged;
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

            RefreshCurrentZoomFromSources();

            if (!IsUsablePlayerTransform(playerTransform))
            {
                ResolvePlayerTransform();
            }

            bool wasReady = minimapRuntimeReady;
            if (TryInitializeMinimapRuntime(false) && (!wasReady || navigationStateDirty))
            {
                navigationStateDirty = false;
                if (subscribedNavigationService != null)
                {
                    SyncNavigationState();
                }
            }

            if (pooledPlayerMarker == null && markerContainer != null)
            {
                EnsurePlayerMarker();
            }

            if (subscribedNavigationService == null && Time.unscaledTime >= nextNavigationBindTime)
            {
                TryBindNavigationService();
            }
            UpdateRoadOverlayState();
            UpdateResponsiveLayout();

            if (playerTransform == null)
            {
                if (pooledPlayerMarker != null)
                {
                    pooledPlayerMarker.SetVisible(false);
                }

                SetPooledObjectiveMarkersVisible(false);
                ClearRoutePreview();
                return;
            }

            if (pooledPlayerMarker != null)
            {
                pooledPlayerMarker.SetVisible(true);
            }

            UpdateMapCenter();
            UpdateRoadOverlayUv();

            if (pooledPlayerMarker != null)
            {
                UpdatePlayerMarkerRotation();
            }

            UpdateObjectiveMarkerPosition();
            if (currentRoute != null && currentRoute.IsRenderable)
            {
                RefreshRoutePreviewIfNeeded();
            }
            UpdateMarkerPulse();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            roadGraphBuilder = null;
            lastRoadGraphSource = null;
            lastRoadGraphSegmentCount = -1;
            roadOverlayReady = false;
            hasRoadBounds = false;
            nextRoadAttemptTime = 0f;
            nextNavigationBindTime = 0f;
            nextPlayerResolveTime = 0f;
            nextRoadGraphResolveTime = 0f;
            cachedVehicleManager = null;
            worldRoadPolylines.Clear();
            localRoadPolylines.Clear();
            lastRoadCenter = new Vector3(float.NaN, float.NaN, float.NaN);
            lastRouteCenter = new Vector3(float.NaN, float.NaN, float.NaN);
            lastRoadZoom = -1f;
            lastRouteZoom = -1f;
            lastRoadHeading = float.NaN;
            lastRouteHeading = float.NaN;
            routeDirty = true;
            navigationStateDirty = true;
            loggedMissingPlayerTransform = false;

            if (roadGraphic != null)
            {
                roadGraphic.Clear();
            }

            ResolveCameraReferences();
            RefreshCurrentZoomFromSources();
            ResolvePlayerTransform(true);
            TryInitializeMinimapRuntime(true);
            TryBindNavigationService();
        }

        private void TryBindNavigationService()
        {
            NavigationService navigationService = NavigationService.Instance;
            if (navigationService == null)
            {
                if (!loggedMissingNavigationService)
                {
                    Debug.LogError("[MinimapUI] NavigationService bulunamadi. Minimap authoritative navigation verisine baglanamiyor.");
                    loggedMissingNavigationService = true;
                }

                nextNavigationBindTime = Time.unscaledTime + Mathf.Max(0.1f, navigationBindRetryInterval);
                return;
            }

            loggedMissingNavigationService = false;
            if (subscribedNavigationService == navigationService)
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
            currentObjective = objective;
            routeDirty = true;

            if (!objective.IsValid)
            {
                hasObjectiveWorldPosition = false;
                SetPooledObjectiveMarkersVisible(false);
                return;
            }

            currentObjectiveWorldPosition = objective.WorldPosition;
            hasObjectiveWorldPosition = true;
            navigationStateDirty = !TryInitializeMinimapRuntime(false);
            if (navigationStateDirty)
            {
                return;
            }

            RefreshObjectiveMarkers();
        }

        private void HandleRouteChanged(RouteResult route)
        {
            currentRoute = route ?? RouteResult.Unavailable;
            routeDirty = true;

            if (!TryInitializeMinimapRuntime(false) || !showRoutePreview || routeLine == null || minimapContainer == null)
            {
                navigationStateDirty = true;
                return;
            }

            if (currentRoute == null || !currentRoute.IsRenderable)
            {
                ClearRoutePreview();
                return;
            }

            Rect rect = GetMinimapViewportRect();
            PopulateRouteLocalPoints(currentRoute.Points, rect);

            if (routeLocalPoints.Count < 2)
            {
                navigationStateDirty = true;
                ClearRoutePreview();
                return;
            }

            Color resolvedRouteColor = currentRoute.IsFallback ? fallbackRouteLineColor : routeLineColor;
            if (currentRoute.IsStale)
            {
                resolvedRouteColor.a *= 0.68f;
            }

            routeLine.color = resolvedRouteColor;
            routeLine.SetLineWidth(currentRoute.IsFallback ? Mathf.Max(3.8f, routeLineWidth - 0.6f) : routeLineWidth);
            routeLine.SetPoints(routeLocalPoints);
            lastRouteCenter = mapCenter;
            lastRouteZoom = currentZoom;
            lastRouteHeading = GetMapHeadingDegrees();
            routeDirty = false;
        }

        private void HandleNavigationCleared()
        {
            currentObjective = NavigationObjective.Empty;
            currentRoute = RouteResult.Unavailable;
            routeDirty = true;
            SetPooledObjectiveMarkersVisible(false);
            hasObjectiveWorldPosition = false;
            ClearRoutePreview();
        }

        private void SetupMinimap()
        {
            ResolveCameraReferences();
            RefreshCurrentZoomFromSources();
            if (!EnsureUiHierarchy())
            {
                return;
            }

            if (minimapContainer != null)
            {
                minimapContainer.anchorMin = new Vector2(0f, 0f);
                minimapContainer.anchorMax = new Vector2(0f, 0f);
                minimapContainer.pivot = new Vector2(0f, 0f);
                minimapContainer.anchoredPosition = new Vector2(Mathf.Abs(anchorOffset.x), Mathf.Abs(anchorOffset.y));
                minimapContainer.sizeDelta = minimapSize;
            }

            if (minimapCanvasGroup == null && minimapContainer != null)
            {
                minimapCanvasGroup = minimapContainer.GetComponent<CanvasGroup>();
                if (minimapCanvasGroup == null)
                {
                    minimapCanvasGroup = minimapContainer.gameObject.AddComponent<CanvasGroup>();
                }
            }

            Image panelImage = minimapContainer != null ? minimapContainer.GetComponent<Image>() : null;
            if (panelImage == null && minimapContainer != null)
            {
                panelImage = minimapContainer.gameObject.AddComponent<Image>();
            }

            if (panelImage != null)
            {
                panelImage.sprite = GetSolidSprite();
                panelImage.color = new Color(panelColor.r, panelColor.g, panelColor.b, panelAlpha);
                panelImage.raycastTarget = false;
                panelImage.enabled = false;
            }

            Outline outline = minimapContainer != null ? minimapContainer.GetComponent<Outline>() : null;
            if (outline == null && minimapContainer != null)
            {
                outline = minimapContainer.gameObject.AddComponent<Outline>();
            }

            if (outline != null)
            {
                outline.effectColor = panelOutlineColor;
                outline.effectDistance = new Vector2(1f, -1f);
                outline.useGraphicAlpha = false;
                outline.enabled = false;
            }

            Shadow shadow = minimapContainer != null ? minimapContainer.GetComponent<Shadow>() : null;
            if (shadow == null && minimapContainer != null)
            {
                shadow = minimapContainer.gameObject.AddComponent<Shadow>();
            }

            if (shadow != null)
            {
                shadow.effectColor = panelShadowColor;
                shadow.effectDistance = new Vector2(3f, -3f);
                shadow.useGraphicAlpha = false;
                shadow.enabled = false;
            }

            if (spriteRegistry == null)
            {
                spriteRegistry = Resources.Load<MinimapSpriteRegistry>(SpriteRegistryResourcePath);
                if (spriteRegistry == null && !loggedMissingSpriteRegistry)
                {
                    Debug.LogWarning($"[MinimapUI] Missing sprite registry resource at Resources/{SpriteRegistryResourcePath}. A runtime fallback player arrow sprite will be used.");
                    loggedMissingSpriteRegistry = true;
                }
                else if (spriteRegistry != null)
                {
                    loggedMissingSpriteRegistry = false;
                }
            }
            else
            {
                loggedMissingSpriteRegistry = false;
            }

            if (minimapImage != null)
            {
                minimapImage.texture = null;
                minimapImage.color = new Color(1f, 1f, 1f, 0f);
                minimapImage.raycastTarget = false;
            }

            Transform viewport = minimapContainer != null ? minimapContainer.Find("Viewport") : null;
            if (viewport != null)
            {
                if (roadGraphic == null)
                {
                    Transform existingRoadGraphic = viewport.Find("RoadNetwork");
                    if (existingRoadGraphic != null)
                    {
                        roadGraphic = existingRoadGraphic.GetComponent<MinimapRoadGraphic>();
                    }
                }

                if (roadGraphic == null)
                {
                    GameObject roadGraphicObject = new GameObject("RoadNetwork");
                    roadGraphicObject.transform.SetParent(viewport, false);
                    roadGraphic = roadGraphicObject.AddComponent<MinimapRoadGraphic>();
                }

                RectTransform roadGraphicRect = roadGraphic.rectTransform;
                roadGraphicRect.anchorMin = Vector2.zero;
                roadGraphicRect.anchorMax = Vector2.one;
                roadGraphicRect.offsetMin = Vector2.zero;
                roadGraphicRect.offsetMax = Vector2.zero;
                roadGraphic.color = roadColor;
                roadGraphic.SetOutlineColor(roadOutlineColor);
                roadGraphic.SetLineWidth(baseRoadLineWidth);
                roadGraphic.raycastTarget = false;
                roadGraphic.transform.SetAsFirstSibling();
            }

            if (minimapCamera != null)
            {
                minimapCamera.SetUseStandaloneOverlay(false);
                minimapCamera.SetVisible(showMinimap);
            }

            SetupRoutePreview();
            EnsureMarkerContainerOrder();
            EnsurePlayerMarker();
            ApplyMarkerSizing();
            SetMinimapVisible(showMinimap);
        }

        private void SetupRoutePreview()
        {
            if (!showRoutePreview || minimapContainer == null)
            {
                return;
            }

            Transform viewport = minimapContainer.Find("Viewport");
            Transform routeParent = viewport != null ? viewport : minimapContainer;

            if (routeLine == null)
            {
                Transform existingRoute = routeParent.Find("RoutePreview");
                if (existingRoute != null)
                {
                    routeLine = existingRoute.GetComponent<RouteLineGraphic>();
                }
            }

            if (routeLine == null)
            {
                GameObject routeObject = new GameObject("RoutePreview");
                routeObject.transform.SetParent(routeParent, false);
                routeLine = routeObject.AddComponent<RouteLineGraphic>();
            }
            else if (routeLine.transform.parent != routeParent)
            {
                routeLine.transform.SetParent(routeParent, false);
            }

            routeLine.color = routeLineColor;
            routeLine.raycastTarget = false;
            routeLine.SetLineWidth(routeLineWidth);

            RectTransform rectTransform = routeLine.rectTransform;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.SetAsLastSibling();
        }

        private void ShowPickupMarker(Vector3 worldPosition)
        {
            if (!EnsureUiHierarchy() || markerContainer == null)
            {
                return;
            }

            if (pooledPickupMarker == null)
            {
                pooledPickupMarker = CreatePooledMarker("PickupMarker", "P", pickupMarkerColor, markerSize, true);
            }

            pooledPickupMarker.Icon.color = pickupMarkerColor;
            pooledPickupMarker.Label.text = "P";
            pooledPickupMarker.SetVisible(true);
            UpdateMarkerPosition(pooledPickupMarker, worldPosition);
        }

        private void ShowDeliveryMarker(Vector3 worldPosition, int deliveryIndex)
        {
            if (!EnsureUiHierarchy() || markerContainer == null)
            {
                return;
            }

            if (pooledDeliveryMarker == null)
            {
                pooledDeliveryMarker = CreatePooledMarker("DeliveryMarker", "D", deliveryMarkerColor, markerSize, false);
            }

            pooledDeliveryMarker.Icon.color = deliveryMarkerColor;
            pooledDeliveryMarker.Label.text = deliveryIndex > 0 ? deliveryIndex.ToString() : "D";
            pooledDeliveryMarker.SetVisible(true);
            UpdateMarkerPosition(pooledDeliveryMarker, worldPosition);
        }

        private void ClearAllMarkers()
        {
            if (pooledPickupMarker != null)
            {
                pooledPickupMarker.SetVisible(false);
            }

            if (pooledDeliveryMarker != null)
            {
                pooledDeliveryMarker.SetVisible(false);
            }
        }

        private void UpdatePlayerMarkerRotation()
        {
            if (pooledPlayerMarker == null || playerTransform == null)
            {
                return;
            }

            float yRotation = alignMapToPlayerHeading ? 0f : -playerTransform.eulerAngles.y;
            pooledPlayerMarker.Root.localRotation = Quaternion.Euler(0f, 0f, yRotation);
            if (pooledPlayerMarker.Shadow != null)
            {
                pooledPlayerMarker.Shadow.localRotation = pooledPlayerMarker.Root.localRotation;
            }
        }

        private void SetMarkerAnchoredPosition(MarkerView marker, Vector2 anchoredPosition, bool isPlayerMarker)
        {
            if (marker == null)
            {
                return;
            }

            if (marker.Root != null)
            {
                marker.Root.anchoredPosition = anchoredPosition;
            }

            if (marker.Shadow != null)
            {
                Vector2 shadowOffset = isPlayerMarker ? PlayerMarkerShadowOffset : ObjectiveMarkerShadowOffset;
                marker.Shadow.anchoredPosition = anchoredPosition + shadowOffset;
            }
        }

        private void UpdateObjectiveMarkerPosition()
        {
            if (!TryInitializeMinimapRuntime() || minimapContainer == null || !hasObjectiveWorldPosition)
            {
                return;
            }

            if (pooledPickupMarker != null && pooledPickupMarker.Root.gameObject.activeSelf)
            {
                UpdateMarkerPosition(pooledPickupMarker, currentObjectiveWorldPosition);
            }

            if (pooledDeliveryMarker != null && pooledDeliveryMarker.Root.gameObject.activeSelf)
            {
                UpdateMarkerPosition(pooledDeliveryMarker, currentObjectiveWorldPosition);
            }
        }

        private void UpdateMarkerPosition(MarkerView marker, Vector3 worldPosition)
        {
            if (marker == null || marker.Root == null || minimapContainer == null)
            {
                return;
            }

            if (!TryConvertWorldToMinimapLocal(worldPosition, GetMinimapViewportRect(), clampObjectivesToEdge, out Vector2 local))
            {
                marker.SetVisible(false);
                return;
            }

            marker.SetVisible(true);
            SetMarkerAnchoredPosition(marker, local, false);
        }

        private MarkerView CreatePooledMarker(string name, string labelText, Color color, float size, bool diamondAccent)
        {
            MarkerView marker = new MarkerView();

            marker.Shadow = new GameObject($"{name}Shadow", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            marker.Shadow.SetParent(markerContainer, false);
            marker.Shadow.anchorMin = new Vector2(0.5f, 0.5f);
            marker.Shadow.anchorMax = new Vector2(0.5f, 0.5f);
            marker.Shadow.pivot = new Vector2(0.5f, 0.5f);
            marker.Shadow.sizeDelta = Vector2.one * (size + 6f);
            Image shadowImage = marker.Shadow.GetComponent<Image>();
            shadowImage.sprite = GetSolidSprite();
            shadowImage.color = new Color(0f, 0f, 0f, 0.30f);
            shadowImage.raycastTarget = false;

            marker.Root = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            marker.Root.SetParent(markerContainer, false);
            marker.Root.anchorMin = new Vector2(0.5f, 0.5f);
            marker.Root.anchorMax = new Vector2(0.5f, 0.5f);
            marker.Root.pivot = new Vector2(0.5f, 0.5f);
            marker.Root.sizeDelta = Vector2.one * (size + 2f);
            Image rootImage = marker.Root.GetComponent<Image>();
            rootImage.sprite = GetSolidSprite();
            rootImage.color = markerFrameColor;
            rootImage.raycastTarget = false;

            RectTransform iconRect = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            iconRect.SetParent(marker.Root, false);
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = Vector2.one * Mathf.Max(12f, size * 0.68f);
            iconRect.localRotation = diamondAccent ? Quaternion.Euler(0f, 0f, 45f) : Quaternion.identity;
            marker.Icon = iconRect.GetComponent<Image>();
            marker.Icon.sprite = diamondAccent ? GetCircleMaskSprite() : GetSolidSprite();
            marker.Icon.color = color;
            marker.Icon.raycastTarget = false;

            RectTransform labelRect = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<RectTransform>();
            labelRect.SetParent(marker.Root, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            marker.Label = labelRect.GetComponent<TextMeshProUGUI>();
            marker.Label.text = labelText;
            marker.Label.alignment = TextAlignmentOptions.Center;
            marker.Label.fontSize = 11f;
            marker.Label.fontStyle = FontStyles.Bold;
            marker.Label.color = Color.white;
            marker.Label.outlineWidth = 0.16f;
            marker.Label.outlineColor = new Color(0f, 0f, 0f, 0.95f);
            marker.Label.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null)
            {
                marker.Label.font = TMP_Settings.defaultFontAsset;
            }

            marker.BaseScale = Vector3.one;
            marker.SetVisible(false);
            return marker;
        }

        private void SetPooledObjectiveMarkersVisible(bool visible)
        {
            if (pooledPickupMarker != null)
            {
                pooledPickupMarker.SetVisible(visible && currentObjective.Type == ObjectiveType.Pickup);
            }

            if (pooledDeliveryMarker != null)
            {
                pooledDeliveryMarker.SetVisible(visible && currentObjective.Type == ObjectiveType.Delivery);
            }
        }

        private bool TryInitializeMinimapRuntime(bool forceReparent = false)
        {
            ResolveCameraReferences();
            RectTransform rootRect = transform as RectTransform;
            if (forceReparent && rootRect != null)
            {
                Transform preferredParent = ResolvePreferredParent();
                if (preferredParent != null && rootRect.parent != preferredParent)
                {
                    rootRect.SetParent(preferredParent, false);
                }
            }

            bool ready = EnsureUiHierarchy() && minimapImage != null;
            if (!ready)
            {
                minimapRuntimeReady = false;
                return false;
            }

            bool needsSetup =
                !minimapRuntimeReady ||
                routeLine == null ||
                markerContainer == null;

            if (needsSetup)
            {
                SetupMinimap();
                EnsurePlayerMarker();
                EnsureMarkerContainerOrder();
            }

            minimapRuntimeReady =
                minimapImage != null &&
                markerContainer != null;
            return minimapRuntimeReady;
        }

        private bool EnsureUiHierarchy()
        {
            if (minimapContainer == null)
            {
                minimapContainer = GetComponent<RectTransform>();
            }

            if (minimapContainer == null)
            {
                return false;
            }

            Transform frame = minimapContainer.Find("ViewportFrame");
            if (frame == null)
            {
                GameObject frameObject = new GameObject("ViewportFrame", typeof(RectTransform), typeof(Image));
                frame = frameObject.transform;
                frame.SetParent(minimapContainer, false);
            }

            viewportFrameRect = frame as RectTransform;
            viewportFrameRect.anchorMin = Vector2.zero;
            viewportFrameRect.anchorMax = Vector2.one;
            viewportFrameRect.offsetMin = new Vector2(framePadding, framePadding);
            viewportFrameRect.offsetMax = new Vector2(-framePadding, -framePadding);

            Image frameImage = frame.GetComponent<Image>();
            frameImage.sprite = GetCircleMaskSprite();
            frameImage.color = frameColor;
            frameImage.raycastTarget = false;

            Transform viewport = minimapContainer.Find("Viewport");
            if (viewport == null)
            {
                GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
                viewport = viewportObject.transform;
                viewport.SetParent(minimapContainer, false);
            }

            viewportRect = viewport as RectTransform;
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(viewportPadding, viewportPadding);
            viewportRect.offsetMax = new Vector2(-viewportPadding, -viewportPadding);

            Image viewportImage = viewport.GetComponent<Image>();
            viewportImage.sprite = GetCircleMaskSprite();
            viewportImage.color = new Color(mapBackgroundColor.r, mapBackgroundColor.g, mapBackgroundColor.b, mapAlpha);
            viewportImage.raycastTarget = false;
            viewportImage.type = Image.Type.Simple;

            Mask viewportMask = viewport.GetComponent<Mask>();
            if (viewportMask == null)
            {
                viewportMask = viewport.gameObject.AddComponent<Mask>();
            }

            viewportMask.showMaskGraphic = true;

            RectMask2D rectMask = viewport.GetComponent<RectMask2D>();
            if (rectMask != null)
            {
                rectMask.enabled = false;
                Destroy(rectMask);
            }

            Outline viewportOutline = viewport.GetComponent<Outline>();
            if (viewportOutline == null)
            {
                viewportOutline = viewport.gameObject.AddComponent<Outline>();
            }

            viewportOutline.effectColor = new Color(panelOutlineColor.r, panelOutlineColor.g, panelOutlineColor.b, 0.22f);
            viewportOutline.effectDistance = new Vector2(1f, -1f);
            viewportOutline.useGraphicAlpha = false;
            viewportOutline.enabled = false;

            if (minimapImage == null)
            {
                Transform existingRoad = viewport.Find("RoadOverlay");
                if (existingRoad != null)
                {
                    minimapImage = existingRoad.GetComponent<RawImage>();
                }
            }

            if (minimapImage == null)
            {
                GameObject roadObject = new GameObject("RoadOverlay", typeof(RectTransform), typeof(RawImage));
                roadObject.transform.SetParent(viewport, false);
                minimapImage = roadObject.GetComponent<RawImage>();
            }

            RectTransform roadRect = minimapImage.rectTransform;
            roadRect.anchorMin = Vector2.zero;
            roadRect.anchorMax = Vector2.one;
            roadRect.offsetMin = Vector2.zero;
            roadRect.offsetMax = Vector2.zero;
            minimapImage.raycastTarget = false;

            if (markerContainer == null)
            {
                Transform existingMarkerContainer = viewport.Find("MarkerContainer");
                if (existingMarkerContainer == null)
                {
                    GameObject markerContainerObject = new GameObject("MarkerContainer", typeof(RectTransform));
                    existingMarkerContainer = markerContainerObject.transform;
                    existingMarkerContainer.SetParent(viewport, false);
                }

                markerContainer = existingMarkerContainer;
            }
            else if (markerContainer.parent != viewport)
            {
                markerContainer.SetParent(viewport, false);
            }

            RectTransform markerRect = markerContainer as RectTransform;
            if (markerRect != null)
            {
                markerRect.anchorMin = Vector2.zero;
                markerRect.anchorMax = Vector2.one;
                markerRect.offsetMin = Vector2.zero;
                markerRect.offsetMax = Vector2.zero;
                markerRect.pivot = new Vector2(0.5f, 0.5f);
            }

            frame.SetAsFirstSibling();
            viewport.SetAsLastSibling();

            return true;
        }

        private void EnsurePlayerMarker()
        {
            if (markerContainer == null || pooledPlayerMarker != null)
            {
                return;
            }

            Sprite arrowSprite = spriteRegistry != null
                ? spriteRegistry.GetPlayerArrowSprite()
                : MinimapSpriteRegistry.GetFallbackPlayerArrowSprite();

            if (arrowSprite == null)
            {
                if (spriteRegistry == null && !loggedMissingSpriteRegistry)
                {
                    Debug.LogError("[MinimapUI] Missing MinimapSpriteRegistry resource and fallback player arrow sprite creation failed.");
                    loggedMissingSpriteRegistry = true;
                }
                if (!loggedMissingPlayerArrowSprite)
                {
                    Debug.LogError("[MinimapUI] Player arrow sprite could not be created, even after fallback generation.");
                    loggedMissingPlayerArrowSprite = true;
                }

                return;
            }

            if (spriteRegistry == null && !loggedMissingSpriteRegistry)
            {
                Debug.LogWarning("[MinimapUI] MinimapSpriteRegistry resource is missing. Using runtime fallback player arrow sprite.");
                loggedMissingSpriteRegistry = true;
            }
            else if (spriteRegistry != null)
            {
                loggedMissingSpriteRegistry = false;
            }
            loggedMissingPlayerArrowSprite = false;

            pooledPlayerMarker = new MarkerView();
            pooledPlayerMarker.Shadow = new GameObject("PlayerMarkerShadow", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            pooledPlayerMarker.Shadow.SetParent(markerContainer, false);
            pooledPlayerMarker.Shadow.anchorMin = new Vector2(0.5f, 0.5f);
            pooledPlayerMarker.Shadow.anchorMax = new Vector2(0.5f, 0.5f);
            pooledPlayerMarker.Shadow.pivot = new Vector2(0.5f, 0.5f);
            pooledPlayerMarker.Shadow.sizeDelta = Vector2.one * playerMarkerSize;
            Image shadowImage = pooledPlayerMarker.Shadow.GetComponent<Image>();
            shadowImage.sprite = arrowSprite;
            shadowImage.preserveAspect = true;
            shadowImage.color = new Color(0f, 0f, 0f, 0.40f);
            shadowImage.raycastTarget = false;

            pooledPlayerMarker.Root = new GameObject("PlayerMarker", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            pooledPlayerMarker.Root.SetParent(markerContainer, false);
            pooledPlayerMarker.Root.anchorMin = new Vector2(0.5f, 0.5f);
            pooledPlayerMarker.Root.anchorMax = new Vector2(0.5f, 0.5f);
            pooledPlayerMarker.Root.pivot = new Vector2(0.5f, 0.5f);
            pooledPlayerMarker.Root.sizeDelta = Vector2.one * playerMarkerSize;
            Image plateImage = pooledPlayerMarker.Root.GetComponent<Image>();
            plateImage.sprite = GetCircleMaskSprite();
            plateImage.color = new Color(0.06f, 0.08f, 0.11f, 0.88f);
            plateImage.raycastTarget = false;

            RectTransform arrowRect = new GameObject("Arrow", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            arrowRect.SetParent(pooledPlayerMarker.Root, false);
            arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
            arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
            arrowRect.pivot = new Vector2(0.5f, 0.5f);
            arrowRect.sizeDelta = Vector2.one * Mathf.Max(18f, playerMarkerSize - 6f);
            pooledPlayerMarker.Icon = arrowRect.GetComponent<Image>();
            pooledPlayerMarker.Icon.sprite = arrowSprite;
            pooledPlayerMarker.Icon.preserveAspect = true;
            pooledPlayerMarker.Icon.color = playerMarkerColor;
            pooledPlayerMarker.Icon.raycastTarget = false;
            pooledPlayerMarker.BaseScale = Vector3.one;
            SetMarkerAnchoredPosition(pooledPlayerMarker, Vector2.zero, true);
            pooledPlayerMarker.Root.SetAsLastSibling();
            ApplyPlayerMarkerSizing(GetResolvedPlayerMarkerSize());
        }

        private void EnsureMarkerContainerOrder()
        {
            if (routeLine != null)
            {
                routeLine.transform.SetAsLastSibling();
            }

            if (markerContainer != null)
            {
                markerContainer.SetAsLastSibling();
            }
        }

        private void RefreshObjectiveMarkers()
        {
            ClearAllMarkers();

            if (!currentObjective.IsValid)
            {
                return;
            }

            if (currentObjective.Type == ObjectiveType.Pickup)
            {
                ShowPickupMarker(currentObjective.WorldPosition);
            }
            else if (currentObjective.Type == ObjectiveType.Delivery)
            {
                ShowDeliveryMarker(currentObjective.WorldPosition, currentObjective.DeliveryIndex);
            }
        }

        private void MoveMarkerAboveRoute(GameObject markerObject)
        {
            if (markerObject == null)
            {
                return;
            }

            markerObject.transform.SetAsLastSibling();
            if (markerContainer != null)
            {
                markerContainer.SetAsLastSibling();
            }
        }

        private void PopulateRouteLocalPoints(IReadOnlyList<Vector3> worldPoints, Rect rect)
        {
            routeLocalPoints.Clear();
            if (worldPoints == null || worldPoints.Count < 2)
            {
                return;
            }

            bool enteredViewport = false;
            float clipExpansion = 1f + Mathf.Max(0.04f, routeViewportExpansion * 0.25f);
            for (int i = 1; i < worldPoints.Count; i++)
            {
                Vector2 previousLocal = ConvertWorldToMinimapLocal(worldPoints[i - 1], rect);
                Vector2 currentLocal = ConvertWorldToMinimapLocal(worldPoints[i], rect);
                if (!TryClipLineToExpandedRect(previousLocal, currentLocal, rect, clipExpansion, out Vector2 clippedStart, out Vector2 clippedEnd))
                {
                    if (enteredViewport)
                    {
                        break;
                    }

                    continue;
                }

                if (!enteredViewport)
                {
                    AddRoutePoint(clippedStart);
                    enteredViewport = true;
                }

                AddRoutePoint(clippedEnd);
            }
        }

        private void AddRoutePoint(Vector2 point)
        {
            if (routeLocalPoints.Count > 0)
            {
                Vector2 previous = routeLocalPoints[routeLocalPoints.Count - 1];
                if ((previous - point).sqrMagnitude <= 0.25f)
                {
                    routeLocalPoints[routeLocalPoints.Count - 1] = point;
                    return;
                }
            }

            routeLocalPoints.Add(point);
        }

        private Vector2 ClampLocalPointToRect(Vector2 local, Rect rect)
        {
            float paddingX = Mathf.Max(10f, rect.width * edgePaddingNormalized);
            float paddingY = Mathf.Max(10f, rect.height * edgePaddingNormalized);
            return new Vector2(
                Mathf.Clamp(local.x, -rect.width * 0.5f + paddingX, rect.width * 0.5f - paddingX),
                Mathf.Clamp(local.y, -rect.height * 0.5f + paddingY, rect.height * 0.5f - paddingY));
        }

        private bool TryConvertWorldToMinimapLocal(Vector3 worldPosition, Rect rect, bool clampToEdge, out Vector2 local)
        {
            local = ConvertWorldToMinimapLocal(worldPosition, rect);
            if (rect.width <= 0.01f || rect.height <= 0.01f)
            {
                return false;
            }

            if (clampToEdge)
            {
                local = ClampLocalPointToRect(local, rect);
            }
            else if (Mathf.Abs(local.x) > rect.width * 0.65f || Mathf.Abs(local.y) > rect.height * 0.65f)
            {
                return false;
            }

            return true;
        }

        private Vector2 ConvertWorldToMinimapLocal(Vector3 worldPosition, Rect rect)
        {
            if (rect.width <= 0.01f || rect.height <= 0.01f)
            {
                return Vector2.zero;
            }

            float visibleHeight = currentZoom * 2f;
            float visibleWidth = visibleHeight * (rect.width / Mathf.Max(1f, rect.height));
            Vector3 delta = worldPosition - mapCenter;
            if (alignMapToPlayerHeading && playerTransform != null)
            {
                delta = Quaternion.Euler(0f, -playerTransform.eulerAngles.y, 0f) * delta;
            }

            return new Vector2(
                (delta.x / visibleWidth) * rect.width,
                (delta.z / visibleHeight) * rect.height);
        }

        private static void AddRoadPoint(List<Vector2> points, Vector2 point)
        {
            if (points == null)
            {
                return;
            }

            if (points.Count > 0)
            {
                Vector2 previous = points[points.Count - 1];
                if ((previous - point).sqrMagnitude <= 0.25f)
                {
                    points[points.Count - 1] = point;
                    return;
                }
            }

            points.Add(point);
        }

        private static bool TryClipLineToExpandedRect(
            Vector2 start,
            Vector2 end,
            Rect rect,
            float expansionFactor,
            out Vector2 clippedStart,
            out Vector2 clippedEnd)
        {
            float halfWidth = rect.width * 0.5f * Mathf.Max(1f, expansionFactor);
            float halfHeight = rect.height * 0.5f * Mathf.Max(1f, expansionFactor);
            float minX = -halfWidth;
            float maxX = halfWidth;
            float minY = -halfHeight;
            float maxY = halfHeight;

            clippedStart = start;
            clippedEnd = end;

            int startCode = ComputeOutCode(clippedStart, minX, maxX, minY, maxY);
            int endCode = ComputeOutCode(clippedEnd, minX, maxX, minY, maxY);

            while (true)
            {
                if ((startCode | endCode) == 0)
                {
                    return true;
                }

                if ((startCode & endCode) != 0)
                {
                    return false;
                }

                int outCode = startCode != 0 ? startCode : endCode;
                float x = 0f;
                float y = 0f;

                if ((outCode & 8) != 0)
                {
                    x = clippedStart.x + ((clippedEnd.x - clippedStart.x) * (maxY - clippedStart.y) / (clippedEnd.y - clippedStart.y));
                    y = maxY;
                }
                else if ((outCode & 4) != 0)
                {
                    x = clippedStart.x + ((clippedEnd.x - clippedStart.x) * (minY - clippedStart.y) / (clippedEnd.y - clippedStart.y));
                    y = minY;
                }
                else if ((outCode & 2) != 0)
                {
                    y = clippedStart.y + ((clippedEnd.y - clippedStart.y) * (maxX - clippedStart.x) / (clippedEnd.x - clippedStart.x));
                    x = maxX;
                }
                else if ((outCode & 1) != 0)
                {
                    y = clippedStart.y + ((clippedEnd.y - clippedStart.y) * (minX - clippedStart.x) / (clippedEnd.x - clippedStart.x));
                    x = minX;
                }

                if (outCode == startCode)
                {
                    clippedStart = new Vector2(x, y);
                    startCode = ComputeOutCode(clippedStart, minX, maxX, minY, maxY);
                }
                else
                {
                    clippedEnd = new Vector2(x, y);
                    endCode = ComputeOutCode(clippedEnd, minX, maxX, minY, maxY);
                }
            }
        }

        private static int ComputeOutCode(Vector2 point, float minX, float maxX, float minY, float maxY)
        {
            int code = 0;
            if (point.x < minX)
            {
                code |= 1;
            }
            else if (point.x > maxX)
            {
                code |= 2;
            }

            if (point.y < minY)
            {
                code |= 4;
            }
            else if (point.y > maxY)
            {
                code |= 8;
            }

            return code;
        }

        private void ClearRoutePreview()
        {
            if (routeLine != null)
            {
                routeLine.Clear();
            }
        }

        private void RefreshRoutePreviewIfNeeded()
        {
            if (!showRoutePreview || routeLine == null || minimapContainer == null)
            {
                return;
            }

            bool needsRefresh =
                routeDirty ||
                float.IsNaN(lastRouteCenter.x) ||
                (lastRouteCenter - mapCenter).sqrMagnitude >= routeRefreshDistance * routeRefreshDistance ||
                Mathf.Abs(lastRouteZoom - currentZoom) > 0.25f ||
                Mathf.Abs(Mathf.DeltaAngle(lastRouteHeading, GetMapHeadingDegrees())) > 1f;

            if (!needsRefresh)
            {
                return;
            }

            HandleRouteChanged(currentRoute);
        }

        private void UpdateResponsiveLayout()
        {
            if (minimapContainer == null)
            {
                return;
            }

            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            if (screenSize == lastScreenSize)
            {
                return;
            }

            lastScreenSize = screenSize;
            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / Mathf.Max(1f, responsiveReferenceShortSide), 0.88f, 1.08f);
            Vector2 size = minimapSize * scale;
            size.x = Mathf.Clamp(size.x, minimumMinimapSize.x, maximumMinimapSize.x);
            size.y = Mathf.Clamp(size.y, minimumMinimapSize.y, maximumMinimapSize.y);
            minimapContainer.sizeDelta = size;
            minimapContainer.anchoredPosition = new Vector2(Mathf.Abs(anchorOffset.x), Mathf.Abs(anchorOffset.y));

            if (viewportRect == null)
            {
                viewportRect = minimapContainer.Find("Viewport") as RectTransform;
            }

            if (viewportRect != null)
            {
                float scaledPadding = Mathf.Clamp(viewportPadding * scale, 10f, 16f);
                viewportRect.offsetMin = new Vector2(scaledPadding, scaledPadding);
                viewportRect.offsetMax = new Vector2(-scaledPadding, -scaledPadding);
            }

            if (viewportFrameRect != null)
            {
                float scaledFramePadding = Mathf.Clamp(framePadding * scale, 5f, 9f);
                viewportFrameRect.offsetMin = new Vector2(scaledFramePadding, scaledFramePadding);
                viewportFrameRect.offsetMax = new Vector2(-scaledFramePadding, -scaledFramePadding);
            }

            routeDirty = true;
            lastRoadCenter = new Vector3(float.NaN, float.NaN, float.NaN);
            lastRoadZoom = -1f;
            lastRoadHeading = float.NaN;
            ApplyMarkerSizing();
        }

        private void UpdateRoadOverlayState()
        {
            if (Time.unscaledTime < nextRoadAttemptTime)
            {
                return;
            }

            if (!TryResolveRoadGraph(out RoadGraph graph) || !TryBuildRoadBounds(graph, out roadBounds))
            {
                if (roadOverlayReady)
                {
                    roadOverlayReady = false;
                    hasRoadBounds = false;
                    worldRoadPolylines.Clear();
                    localRoadPolylines.Clear();
                    lastRoadGraphSource = null;
                    lastRoadGraphSegmentCount = -1;
                    if (roadGraphic != null)
                    {
                        roadGraphic.Clear();
                    }
                }

                nextRoadAttemptTime = Time.unscaledTime + Mathf.Max(0.2f, roadRetryInterval);
                return;
            }

            bool graphChanged =
                !ReferenceEquals(lastRoadGraphSource, graph) ||
                lastRoadGraphSegmentCount != graph.roadSegments.Count;
            if (roadOverlayReady && !graphChanged)
            {
                return;
            }

            nextRoadAttemptTime = Time.unscaledTime + Mathf.Max(0.2f, roadRetryInterval);

            worldRoadPolylines.Clear();
            List<List<Vector3>> builtPolylines = MinimapRoadTextureBuilder.BuildRoadPolylines(graph);
            for (int i = 0; i < builtPolylines.Count; i++)
            {
                if (builtPolylines[i] != null && builtPolylines[i].Count >= 2)
                {
                    worldRoadPolylines.Add(builtPolylines[i]);
                }
            }

            if (worldRoadPolylines.Count == 0)
            {
                if (!loggedMissingRoadPolylines)
                {
                    Debug.LogWarning("[MinimapUI] RoadGraph olustu ancak cizilebilir minimap yol polylineleri uretilmedi.");
                    loggedMissingRoadPolylines = true;
                }

                return;
            }

            loggedMissingRoadPolylines = false;
            roadOverlayReady = true;
            hasRoadBounds = true;
            lastRoadGraphSource = graph;
            lastRoadGraphSegmentCount = graph.roadSegments.Count;
            lastRoadCenter = new Vector3(float.NaN, float.NaN, float.NaN);
            lastRoadZoom = -1f;
            routeDirty = true;
        }

        private void UpdateMapCenter()
        {
            mapCenter = playerTransform.position;
            if (!hasRoadBounds || minimapContainer == null)
            {
                return;
            }

            // Keep the player centered for a more standard driving-game minimap.
            mapCenter = playerTransform.position;
        }

        private void UpdateRoadOverlayUv()
        {
            if (!roadOverlayReady || roadGraphic == null || !hasRoadBounds || minimapContainer == null)
            {
                return;
            }

            bool needsRefresh =
                float.IsNaN(lastRoadCenter.x) ||
                (lastRoadCenter - mapCenter).sqrMagnitude >= roadRefreshDistance * roadRefreshDistance ||
                Mathf.Abs(lastRoadZoom - currentZoom) > 0.25f ||
                Mathf.Abs(Mathf.DeltaAngle(lastRoadHeading, GetMapHeadingDegrees())) > 1f;

            if (!needsRefresh)
            {
                return;
            }

            Rect rect = GetMinimapViewportRect();
            float clipExpansion = 1f + Mathf.Max(0.02f, edgePaddingNormalized);
            localRoadPolylines.Clear();
            for (int lineIndex = 0; lineIndex < worldRoadPolylines.Count; lineIndex++)
            {
                List<Vector3> worldLine = worldRoadPolylines[lineIndex];
                List<Vector2> localLine = null;

                for (int pointIndex = 1; pointIndex < worldLine.Count; pointIndex++)
                {
                    Vector2 start = ConvertWorldToMinimapLocal(worldLine[pointIndex - 1], rect);
                    Vector2 end = ConvertWorldToMinimapLocal(worldLine[pointIndex], rect);
                    if (!TryClipLineToExpandedRect(start, end, rect, clipExpansion, out Vector2 clippedStart, out Vector2 clippedEnd))
                    {
                        localLine = null;
                        continue;
                    }

                    if (localLine == null)
                    {
                        localLine = new List<Vector2>(worldLine.Count);
                        localRoadPolylines.Add(localLine);
                        AddRoadPoint(localLine, clippedStart);
                    }

                    AddRoadPoint(localLine, clippedEnd);
                }
            }

            roadGraphic.color = new Color(roadColor.r, roadColor.g, roadColor.b, mapAlpha);
            roadGraphic.SetOutlineColor(roadOutlineColor);
            roadGraphic.SetLineWidth(GetResolvedRoadLineWidth(rect));
            if (localRoadPolylines.Count == 0)
            {
                if (!loggedEmptyLocalRoadPolylines)
                {
                    Debug.LogWarning(
                        $"[MinimapUI] Road overlay resolved zero visible local polylines. worldPolylines={worldRoadPolylines.Count}, zoom={currentZoom:F1}, viewport={rect.width:F1}x{rect.height:F1}.");
                    loggedEmptyLocalRoadPolylines = true;
                }

                roadGraphic.Clear();
            }
            else
            {
                loggedEmptyLocalRoadPolylines = false;
                roadGraphic.SetPolylines(localRoadPolylines);
            }
            lastRoadCenter = mapCenter;
            lastRoadZoom = currentZoom;
            lastRoadHeading = GetMapHeadingDegrees();
        }

        private float GetMapHeadingDegrees()
        {
            if (!alignMapToPlayerHeading || playerTransform == null)
            {
                return 0f;
            }

            return playerTransform.eulerAngles.y;
        }

        private void ResolvePlayerTransform(bool forceRefresh = false)
        {
            Transform resolvedPlayerTransform = null;
            if (!forceRefresh && IsUsablePlayerTransform(playerTransform))
            {
                return;
            }

            if (!forceRefresh && Time.unscaledTime < nextPlayerResolveTime)
            {
                return;
            }

            nextPlayerResolveTime = Time.unscaledTime + Mathf.Max(0.1f, playerResolveRetryInterval);
            if (!TryResolveAuthoritativePlayerTransform(out resolvedPlayerTransform))
            {
                if (!loggedMissingPlayerTransform)
                {
                    Debug.LogWarning("[MinimapUI] Could not resolve an active player vehicle transform. Waiting for QuestManager or PlayerVehicleManager to publish the authoritative target.");
                    loggedMissingPlayerTransform = true;
                }

                playerTransform = null;
                if (minimapCamera != null)
                {
                    minimapCamera.SetPlayer(null);
                }

                return;
            }

            loggedMissingPlayerTransform = false;
            if (playerTransform == resolvedPlayerTransform && !forceRefresh)
            {
                return;
            }

            playerTransform = resolvedPlayerTransform;
            if (minimapCamera != null)
            {
                minimapCamera.SetPlayer(playerTransform);
            }

            routeDirty = true;
            lastRouteCenter = new Vector3(float.NaN, float.NaN, float.NaN);
            lastRoadCenter = new Vector3(float.NaN, float.NaN, float.NaN);
            lastRoadHeading = float.NaN;
            lastRouteHeading = float.NaN;
        }

        private bool TryResolveRoadGraph(out RoadGraph graph)
        {
            if (roadGraphBuilder == null && Time.unscaledTime >= nextRoadGraphResolveTime)
            {
                roadGraphBuilder = FindFirstObjectByType<RoadGraphBuilder>();
                nextRoadGraphResolveTime = Time.unscaledTime + Mathf.Max(0.25f, roadGraphResolveRetryInterval);
            }

            if (roadGraphBuilder == null)
            {
                if (!loggedMissingRoadGraphBuilder)
                {
                    Debug.LogError("[MinimapUI] RoadGraphBuilder could not be found. The minimap road overlay cannot bind to the authoritative road graph source.");
                    loggedMissingRoadGraphBuilder = true;
                }

                graph = null;
                return false;
            }

            loggedMissingRoadGraphBuilder = false;
            if (!roadGraphBuilder.HasBuiltRoadGraph)
            {
                if (!roadGraphBuilder.HasPendingBuild)
                {
                    roadGraphBuilder.BeginBuildWithDelay(0f);
                }

                if (!loggedMissingRoadGraph)
                {
                    Debug.LogWarning("[MinimapUI] Road graph is not built yet. Waiting for RoadGraphBuilder before drawing roads and graph routes.");
                    loggedMissingRoadGraph = true;
                }

                graph = null;
                return false;
            }

            graph = roadGraphBuilder.RoadGraph;
            bool hasGraph = graph != null && graph.roadSegments != null && graph.roadSegments.Count > 0;
            if (!hasGraph && !loggedMissingRoadGraph)
            {
                Debug.LogError("[MinimapUI] RoadGraphBuilder finished without producing any road segments. The minimap road overlay cannot render.");
                loggedMissingRoadGraph = true;
            }
            else if (hasGraph)
            {
                loggedMissingRoadGraph = false;
            }

            return hasGraph;
        }

        private bool TryBuildRoadBounds(RoadGraph graph, out Bounds bounds)
        {
            bounds = default;
            bool initialized = false;
            if (graph == null || graph.roadSegments == null)
            {
                return false;
            }

            for (int segmentIndex = 0; segmentIndex < graph.roadSegments.Count; segmentIndex++)
            {
                RoadSegment segment = graph.roadSegments[segmentIndex];
                if (segment == null || segment.waypoints == null)
                {
                    continue;
                }

                for (int waypointIndex = 0; waypointIndex < segment.waypoints.Count; waypointIndex++)
                {
                    Vector3 position = segment.waypoints[waypointIndex].position;
                    Vector3 flatPosition = new Vector3(position.x, 0f, position.z);
                    if (!initialized)
                    {
                        bounds = new Bounds(flatPosition, Vector3.one);
                        initialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(flatPosition);
                    }
                }
            }

            if (!initialized)
            {
                return false;
            }

            bounds.Expand(new Vector3(roadBoundsPadding * 2f, 1f, roadBoundsPadding * 2f));
            return true;
        }

        private void ResolveCameraReferences()
        {
            if (minimapCamera == null)
            {
                minimapCamera = FindFirstObjectByType<MinimapCamera>();
            }

            if (cameraComponent == null && minimapCamera != null)
            {
                cameraComponent = minimapCamera.GetComponent<Camera>();
            }

            if (minimapCamera == null && cameraComponent != null)
            {
                minimapCamera = cameraComponent.GetComponent<MinimapCamera>();
            }
        }

        private static Sprite GetCircleMaskSprite()
        {
            if (circleMaskSprite != null)
            {
                return circleMaskSprite;
            }

            const int size = 128;
            circleMaskTexture = new Texture2D(size, size, TextureFormat.ARGB32, false, false);
            circleMaskTexture.name = "RuntimeMinimapCircle";
            circleMaskTexture.wrapMode = TextureWrapMode.Clamp;
            circleMaskTexture.filterMode = FilterMode.Bilinear;

            Color32[] pixels = new Color32[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.5f - 2f;
            float feather = 2.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01((radius - distance) / feather);
                    pixels[(y * size) + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            circleMaskTexture.SetPixels32(pixels);
            circleMaskTexture.Apply(false, true);
            circleMaskSprite = Sprite.Create(
                circleMaskTexture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size);
            circleMaskSprite.name = "RuntimeMinimapCircle";
            return circleMaskSprite;
        }

        private static Transform ResolvePreferredParent()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null && canvases[i].name == QuestCanvasName)
                {
                    return canvases[i].transform;
                }
            }

            Canvas globalCanvas = GlobalUiCoordinator.PrimaryCanvas;
            if (globalCanvas != null)
            {
                return globalCanvas.transform;
            }

            return canvases.Length > 0 ? canvases[0].transform : null;
        }

        public void SetPlayerTransform(Transform player)
        {
            playerTransform = player;
            nextPlayerResolveTime = 0f;
            TryInitializeMinimapRuntime(false);
            if (minimapCamera != null)
            {
                minimapCamera.SetPlayer(player);
            }

            routeDirty = true;
            loggedMissingPlayerTransform = player == null;
            lastRouteCenter = new Vector3(float.NaN, float.NaN, float.NaN);
            lastRoadCenter = new Vector3(float.NaN, float.NaN, float.NaN);
            lastRoadHeading = float.NaN;
            lastRouteHeading = float.NaN;
            if (pooledPlayerMarker != null)
            {
                pooledPlayerMarker.SetVisible(player != null);
                SetMarkerAnchoredPosition(pooledPlayerMarker, Vector2.zero, true);
            }
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
            SetPlayerTransform(controller != null ? controller.transform : null);
        }

        private void ApplyFixedZoom()
        {
            currentZoom = Mathf.Clamp(fixedZoom, MinAllowedZoom, MaxAllowedZoom);
            RefreshCurrentZoomFromSources();
        }

        private Rect GetMinimapViewportRect()
        {
            if (viewportRect == null && minimapContainer != null)
            {
                viewportRect = minimapContainer.Find("Viewport") as RectTransform;
            }

            if (viewportRect != null)
            {
                return viewportRect.rect;
            }

            return minimapContainer != null ? minimapContainer.rect : new Rect(0f, 0f, 1f, 1f);
        }

        private void RefreshCurrentZoomFromSources()
        {
            ResolveCameraReferences();

            float resolvedZoom = Mathf.Clamp(fixedZoom, MinAllowedZoom, MaxAllowedZoom);
            bool usedAuthoritativeCameraZoom = false;
            if (minimapCamera != null && minimapCamera.CameraComponent != null)
            {
                float cameraZoom = minimapCamera.CameraComponent.orthographicSize;
                if (cameraZoom > 0.01f)
                {
                    resolvedZoom = Mathf.Clamp(cameraZoom, MinAllowedZoom, MaxAllowedZoom);
                    usedAuthoritativeCameraZoom = true;
                }
            }

            if (Mathf.Abs(currentZoom - resolvedZoom) <= 0.01f)
            {
                return;
            }

            currentZoom = resolvedZoom;
            routeDirty = true;
            lastRouteCenter = new Vector3(float.NaN, float.NaN, float.NaN);
            lastRoadCenter = new Vector3(float.NaN, float.NaN, float.NaN);
            lastRoadZoom = -1f;
            lastRouteZoom = -1f;

            if (!usedAuthoritativeCameraZoom && minimapCamera != null)
            {
                minimapCamera.SetZoom(currentZoom);
            }
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

        private static Sprite GetSolidSprite()
        {
            if (solidSprite != null)
            {
                return solidSprite;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false, true);
            texture.name = "RuntimeMinimapSolid";
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            texture.Apply(false, true);

            solidSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            solidSprite.name = "RuntimeMinimapSolid";
            return solidSprite;
        }

        private void UpdateMarkerPulse()
        {
            float pulse = 1f + (Mathf.Sin(Time.time * markerPulseSpeed * Mathf.PI) * 0.5f + 0.5f) * (markerPulseScale - 1f);
            Vector3 pulseScale = new Vector3(pulse, pulse, 1f);

            ApplyMarkerPulse(pooledPickupMarker, pulseScale);
            ApplyMarkerPulse(pooledDeliveryMarker, pulseScale);
        }

        private static void ApplyMarkerPulse(MarkerView marker, Vector3 pulseScale)
        {
            if (marker == null || marker.Root == null || !marker.Root.gameObject.activeSelf)
            {
                return;
            }

            marker.Root.localScale = Vector3.Scale(marker.BaseScale, pulseScale);
            if (marker.Shadow != null)
            {
                marker.Shadow.localScale = Vector3.Scale(marker.BaseScale, pulseScale);
            }
        }

        private void ApplyMarkerSizing()
        {
            float objectiveMarkerResolvedSize = GetResolvedObjectiveMarkerSize();
            ApplyObjectiveMarkerSizing(pooledPickupMarker, objectiveMarkerResolvedSize);
            ApplyObjectiveMarkerSizing(pooledDeliveryMarker, objectiveMarkerResolvedSize);
            ApplyPlayerMarkerSizing(GetResolvedPlayerMarkerSize());
        }

        private float GetResolvedObjectiveMarkerSize()
        {
            Rect rect = GetMinimapViewportRect();
            float viewportSize = Mathf.Min(rect.width, rect.height);
            if (viewportSize <= 0.01f)
            {
                return markerSize;
            }

            return Mathf.Max(markerSize, viewportSize * 0.12f);
        }

        private float GetResolvedPlayerMarkerSize()
        {
            Rect rect = GetMinimapViewportRect();
            float viewportSize = Mathf.Min(rect.width, rect.height);
            if (viewportSize <= 0.01f)
            {
                return playerMarkerSize;
            }

            return Mathf.Max(playerMarkerSize, viewportSize * 0.16f);
        }

        private static void ApplyObjectiveMarkerSizing(MarkerView marker, float size)
        {
            if (marker == null)
            {
                return;
            }

            if (marker.Shadow != null)
            {
                marker.Shadow.sizeDelta = Vector2.one * (size + 6f);
            }

            if (marker.Root != null)
            {
                marker.Root.sizeDelta = Vector2.one * (size + 2f);
            }

            if (marker.Icon != null)
            {
                marker.Icon.rectTransform.sizeDelta = Vector2.one * Mathf.Max(12f, size * 0.68f);
            }

            if (marker.Label != null)
            {
                marker.Label.fontSize = Mathf.Clamp(size * 0.5f, 11f, 16f);
            }
        }

        private void ApplyPlayerMarkerSizing(float size)
        {
            if (pooledPlayerMarker == null)
            {
                return;
            }

            if (pooledPlayerMarker.Shadow != null)
            {
                pooledPlayerMarker.Shadow.sizeDelta = Vector2.one * (size + 3f);
            }

            if (pooledPlayerMarker.Root != null)
            {
                pooledPlayerMarker.Root.sizeDelta = Vector2.one * size;
            }

            if (pooledPlayerMarker.Icon != null)
            {
                pooledPlayerMarker.Icon.rectTransform.sizeDelta = Vector2.one * Mathf.Max(18f, size * 0.78f);
            }
        }

        private float GetResolvedRoadLineWidth(Rect rect)
        {
            float viewportSize = Mathf.Min(rect.width, rect.height);
            if (viewportSize <= 0.01f)
            {
                return baseRoadLineWidth;
            }

            return Mathf.Max(baseRoadLineWidth, viewportSize * 0.024f);
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
                minimapCanvasGroup.interactable = false;
                minimapCanvasGroup.blocksRaycasts = false;
            }

            if (minimapCamera != null)
            {
                minimapCamera.SetVisible(visible);
            }
            else if (cameraComponent != null)
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
