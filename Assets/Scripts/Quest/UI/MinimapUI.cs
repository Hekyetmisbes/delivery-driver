using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using DeliveryDriver.Navigation;
using DeliveryDriver.Company;
using DeliveryDriver.UI;
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
        // ────────────────────────────────────────────────────────────────────
        // Nested types
        // ────────────────────────────────────────────────────────────────────

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
                    Root.gameObject.SetActive(visible);
                if (Shadow != null && Shadow.gameObject.activeSelf != visible)
                    Shadow.gameObject.SetActive(visible);
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Constants
        // ────────────────────────────────────────────────────────────────────

        private const string RuntimeRootName = "MinimapUI";
        private const string QuestCanvasName = "Quest UI Canvas";
        private const string SpriteRegistryResourcePath = "Minimap/MinimapSpriteRegistry";
        private const float MinZoom = 12f;
        private const float MaxZoom = 220f;
        private const float RetryInterval = 0.5f;
        private const float RoadRetryInterval = 1f;
        private static readonly Vector2 ObjectiveMarkerShadowOffset = new Vector2(3f, -3f);
        private static readonly Vector2 PlayerMarkerShadowOffset = new Vector2(2f, -2f);

        // ────────────────────────────────────────────────────────────────────
        // Static sprite caches
        // ────────────────────────────────────────────────────────────────────

        private static Sprite circleMaskSprite;
        private static Texture2D circleMaskTexture;
        private static Sprite solidSprite;

        // ────────────────────────────────────────────────────────────────────
        // Inspector fields
        // ────────────────────────────────────────────────────────────────────

        [Header("Settings")]
        [SerializeField] private bool showMinimap = true;
        [SerializeField] private Vector2 minimapSize = new Vector2(218f, 218f);
        [SerializeField] private Vector2 minimumMinimapSize = new Vector2(192f, 192f);
        [SerializeField] private Vector2 maximumMinimapSize = new Vector2(236f, 236f);
        [SerializeField] private Vector2 anchorOffset = new Vector2(24f, 24f);
        [SerializeField] private float responsiveReferenceShortSide = 1080f;
        [SerializeField] private float framePadding = 6f;
        [SerializeField] private float viewportPadding = 12f;
        [SerializeField, Range(0f, 0.3f)] private float panelAlpha = 0.10f;
        [SerializeField, Range(0.78f, 1f)] private float mapAlpha = 0.96f;

        [Header("Colors")]
        [SerializeField] private Color panelColor = new Color(0.02f, 0.03f, 0.05f, 1f);
        [SerializeField] private Color panelOutlineColor = new Color(0.80f, 0.88f, 0.96f, 0.76f);
        [SerializeField] private Color mapBackgroundColor = new Color(0.17f, 0.21f, 0.24f, 0.96f);
        [SerializeField] private Color frameColor = new Color(0.10f, 0.12f, 0.15f, 0.94f);

        [Header("Scale")]
        [SerializeField] private float fixedZoom = 126f;
        [SerializeField] private bool alignMapToPlayerHeading = true;

        [Header("Route Preview")]
        [SerializeField] private bool showRoutePreview = true;
        [SerializeField] private Color routeLineColor = new Color(1f, 0.81f, 0.34f, 0.96f);
        [SerializeField] private Color fallbackRouteLineColor = new Color(1f, 0.88f, 0.56f, 0.72f);
        [SerializeField] private float routeLineWidth = 4.8f;

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
        [SerializeField] private float baseRoadLineWidth = 4.2f;
        [SerializeField] private float routeRefreshDistance = 2f;
        [SerializeField] private float roadRefreshDistance = 4f;

        // ────────────────────────────────────────────────────────────────────
        // Runtime state
        // ────────────────────────────────────────────────────────────────────

        // UI hierarchy
        private RectTransform minimapContainer;
        private RectTransform viewportRect;
        private RectTransform viewportFrameRect;
        private RawImage roadOverlayImage;
        private MinimapRoadGraphic roadGraphic;
        private RouteLineGraphic routeLine;
        private Transform markerContainer;
        private CanvasGroup minimapCanvasGroup;

        // Camera
        private MinimapCamera minimapCamera;
        private Camera cameraComponent;

        // Player
        private Transform playerTransform;
        private PlayerVehicleManager cachedVehicleManager;

        // Navigation
        private NavigationService subscribedNavService;
        private NavigationObjective currentObjective = NavigationObjective.Empty;
        private RouteResult currentRoute = RouteResult.Unavailable;

        // Markers
        private MarkerView pickupMarker;
        private MarkerView deliveryMarker;
        private MarkerView playerMarkerView;
        private MinimapSpriteRegistry spriteRegistry;

        // Road overlay
        private RoadGraphBuilder roadGraphBuilder;
        private RoadGraph cachedRoadGraph;
        private readonly List<List<Vector3>> worldRoadPolylines = new List<List<Vector3>>();
        private readonly List<List<Vector2>> localRoadPolylines = new List<List<Vector2>>();
        private Texture2D roadOverlayTexture;
        private Bounds roadBounds;
        private bool hasRoadBounds;
        private bool roadOverlayReady;

        // Route
        private readonly List<Vector2> routeLocalPoints = new List<Vector2>();

        // Zoom
        private float currentZoom;

        // Map state
        private Vector3 mapCenter;

        // Dirty tracking (simplified)
        private Vector3 lastUpdatePosition = new Vector3(float.NaN, float.NaN, float.NaN);
        private float lastUpdateZoom = -1f;
        private float lastUpdateHeading = float.NaN;
        private bool routeDirty = true;
        private bool roadsDirty = true;
        private int lastRoadGraphSegmentCount = -1;
        private Vector2 lastScreenSize = Vector2.negativeInfinity;

        // Retry timers
        private float nextPlayerResolveTime;
        private float nextNavBindTime;
        private float nextRoadResolveTime;

        // Initialization
        private bool initialized;

        // Logging (one-shot)
        private bool loggedNoNavService;
        private bool loggedNoPlayer;
        private bool loggedNoRoadGraph;
        private bool loggedNavBound;
        private bool loggedRoadOverlayReady;
        private bool loggedSmallViewportRect;

        // ────────────────────────────────────────────────────────────────────
        // Public API
        // ────────────────────────────────────────────────────────────────────

        public MinimapCamera CameraController => minimapCamera;

        public static MinimapUI EnsureSceneInstance()
        {
            MinimapUI existing = FindFirstObjectByType<MinimapUI>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.Initialize();
                return existing;
            }

            Transform parent = ResolvePreferredParent();
            GameObject root = new GameObject(RuntimeRootName, typeof(RectTransform));
            if (parent != null)
                root.transform.SetParent(parent, false);

            return root.AddComponent<MinimapUI>();
        }

        public void SetPlayerTransform(Transform player)
        {
            playerTransform = player;
            nextPlayerResolveTime = 0f;
            loggedNoPlayer = player == null;

            if (minimapCamera != null)
                minimapCamera.SetPlayer(player);

            if (playerMarkerView != null)
            {
                playerMarkerView.SetVisible(player != null);
                SetMarkerAnchoredPosition(playerMarkerView, Vector2.zero, true);
            }

            MarkAllDirty();
        }

        public void SetMinimapVisible(bool visible)
        {
            showMinimap = visible;

            if (minimapCanvasGroup != null)
            {
                minimapCanvasGroup.alpha = visible ? 1f : 0f;
                minimapCanvasGroup.interactable = false;
                minimapCanvasGroup.blocksRaycasts = false;
            }

            if (minimapCamera != null)
                minimapCamera.SetVisible(visible);
            else if (cameraComponent != null)
                cameraComponent.enabled = visible;
        }

        public void ToggleMinimap()
        {
            SetMinimapVisible(!showMinimap);
        }

        // ────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            currentZoom = Mathf.Clamp(fixedZoom, MinZoom, MaxZoom);
            Initialize();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            PlayerVehicleManager.ActiveVehicleChanged += HandleActiveVehicleChanged;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            PlayerVehicleManager.ActiveVehicleChanged -= HandleActiveVehicleChanged;
        }

        private void OnDestroy()
        {
            UnbindNavigation();

            if (roadOverlayTexture != null)
            {
                Destroy(roadOverlayTexture);
                roadOverlayTexture = null;
            }
        }

        private void Start()
        {
            ResolvePlayer(true);
            Initialize();
            BindNavigation();
        }

        private void Update()
        {
            HandleToggleInput();
            SyncZoom();

            if (!IsUsableTransform(playerTransform))
                ResolvePlayer();

            if (!initialized)
                Initialize();

            if (playerMarkerView == null && markerContainer != null)
                CreatePlayerMarker();

            if (subscribedNavService == null && Time.unscaledTime >= nextNavBindTime)
                BindNavigation();

            ResolveRoads();
            UpdateResponsiveLayout();

            if (playerTransform == null)
            {
                playerMarkerView?.SetVisible(false);
                SetObjectiveMarkersVisible(false);
                ClearRoute();
                return;
            }

            playerMarkerView?.SetVisible(true);
            mapCenter = playerTransform.position;

            UpdateRoadOverlay();
            UpdatePlayerMarkerRotation();
            UpdateObjectiveMarkerPositions();
            UpdateRoutePreview();
            UpdateMarkerPulse();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Reset all scene-dependent state
            roadGraphBuilder = null;
            cachedRoadGraph = null;
            roadOverlayReady = false;
            hasRoadBounds = false;
            lastRoadGraphSegmentCount = -1;
            cachedVehicleManager = null;
            worldRoadPolylines.Clear();
            localRoadPolylines.Clear();
            nextRoadResolveTime = 0f;
            nextNavBindTime = 0f;
            nextPlayerResolveTime = 0f;
            loggedNoPlayer = false;
            loggedNoNavService = false;
            loggedNoRoadGraph = false;
            loggedNavBound = false;
            loggedRoadOverlayReady = false;
            loggedSmallViewportRect = false;
            loggedRoadOverlayUpdate = false;

            roadGraphic?.Clear();
            MarkAllDirty();
            ResolveCameraReferences();
            SyncZoom();
            ResolvePlayer(true);
            Initialize();
            BindNavigation();
        }

        // ────────────────────────────────────────────────────────────────────
        // Initialization & UI hierarchy
        // ────────────────────────────────────────────────────────────────────

        private void Initialize()
        {
            ResolveCameraReferences();

            minimapContainer = GetComponent<RectTransform>();
            if (minimapContainer == null)
                return;

            // Reparent if needed
            Transform preferredParent = ResolvePreferredParent();
            if (preferredParent != null && minimapContainer.parent != preferredParent)
                minimapContainer.SetParent(preferredParent, false);

            // Anchor to bottom-left
            minimapContainer.anchorMin = Vector2.zero;
            minimapContainer.anchorMax = Vector2.zero;
            minimapContainer.pivot = Vector2.zero;
            minimapContainer.anchoredPosition = new Vector2(Mathf.Abs(anchorOffset.x), Mathf.Abs(anchorOffset.y));
            minimapContainer.sizeDelta = minimapSize;

            // CanvasGroup for visibility
            minimapCanvasGroup = minimapContainer.GetComponent<CanvasGroup>();
            if (minimapCanvasGroup == null)
                minimapCanvasGroup = minimapContainer.gameObject.AddComponent<CanvasGroup>();

            // Panel background (disabled, keeps inspector-configurable styling)
            Image panelImage = minimapContainer.GetComponent<Image>();
            if (panelImage == null)
                panelImage = minimapContainer.gameObject.AddComponent<Image>();
            panelImage.sprite = GetSolidSprite();
            panelImage.color = new Color(panelColor.r, panelColor.g, panelColor.b, panelAlpha);
            panelImage.raycastTarget = false;
            panelImage.enabled = false;

            Outline panelOutline = minimapContainer.GetComponent<Outline>();
            if (panelOutline == null)
                panelOutline = minimapContainer.gameObject.AddComponent<Outline>();
            panelOutline.effectColor = panelOutlineColor;
            panelOutline.effectDistance = new Vector2(1f, -1f);
            panelOutline.useGraphicAlpha = false;
            panelOutline.enabled = false;

            // Build hierarchy
            BuildViewportFrame();
            BuildViewport();
            BuildRoadOverlayImage();
            BuildRoadGraphic();
            BuildRoutePreview();
            BuildMarkerContainer();

            // Order
            viewportFrameRect.SetAsFirstSibling();
            viewportRect.transform.SetAsLastSibling();

            // Sprite registry
            if (spriteRegistry == null)
                spriteRegistry = Resources.Load<MinimapSpriteRegistry>(SpriteRegistryResourcePath);

            // Camera
            if (minimapCamera != null)
            {
                minimapCamera.SetUseStandaloneOverlay(false);
                minimapCamera.SetVisible(showMinimap);
            }

            // Force layout so viewportRect.rect has valid dimensions
            // on the same frame (Canvas layout normally runs after Update).
            Canvas.ForceUpdateCanvases();

            // Player marker
            CreatePlayerMarker();
            ApplyMarkerSizing();
            SetMinimapVisible(showMinimap);

            initialized = true;
        }

        private void BuildViewportFrame()
        {
            Transform existing = minimapContainer.Find("ViewportFrame");
            if (existing == null)
            {
                GameObject go = new GameObject("ViewportFrame", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(minimapContainer, false);
                existing = go.transform;
            }

            viewportFrameRect = existing as RectTransform;
            viewportFrameRect.anchorMin = Vector2.zero;
            viewportFrameRect.anchorMax = Vector2.one;
            viewportFrameRect.offsetMin = new Vector2(framePadding, framePadding);
            viewportFrameRect.offsetMax = new Vector2(-framePadding, -framePadding);

            Image img = existing.GetComponent<Image>();
            img.sprite = GetCircleMaskSprite();
            img.color = frameColor;
            img.raycastTarget = false;
        }

        private void BuildViewport()
        {
            Transform existing = minimapContainer.Find("Viewport");
            if (existing == null)
            {
                GameObject go = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
                go.transform.SetParent(minimapContainer, false);
                existing = go.transform;
            }

            viewportRect = existing as RectTransform;
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(viewportPadding, viewportPadding);
            viewportRect.offsetMax = new Vector2(-viewportPadding, -viewportPadding);

            Image viewportImage = existing.GetComponent<Image>();
            viewportImage.sprite = GetCircleMaskSprite();
            viewportImage.color = new Color(mapBackgroundColor.r, mapBackgroundColor.g, mapBackgroundColor.b, mapAlpha);
            viewportImage.raycastTarget = false;
            viewportImage.type = Image.Type.Simple;

            Mask mask = existing.GetComponent<Mask>();
            if (mask == null)
                mask = existing.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            // Remove RectMask2D if present (conflicts with Mask)
            RectMask2D rectMask = existing.GetComponent<RectMask2D>();
            if (rectMask != null)
            {
                rectMask.enabled = false;
                Destroy(rectMask);
            }
        }

        private void BuildRoadOverlayImage()
        {
            if (viewportRect == null) return;

            Transform existing = viewportRect.Find("RoadOverlay");
            if (existing == null)
            {
                GameObject go = new GameObject("RoadOverlay", typeof(RectTransform), typeof(RawImage));
                go.transform.SetParent(viewportRect, false);
                existing = go.transform;
            }

            roadOverlayImage = existing.GetComponent<RawImage>();
            RectTransform rt = roadOverlayImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            roadOverlayImage.texture = null;
            roadOverlayImage.color = new Color(1f, 1f, 1f, 0f);
            roadOverlayImage.raycastTarget = false;
        }

        private void BuildRoadGraphic()
        {
            if (viewportRect == null) return;

            if (roadGraphic == null)
            {
                Transform existing = viewportRect.Find("RoadNetwork");
                if (existing != null)
                    roadGraphic = existing.GetComponent<MinimapRoadGraphic>();
            }

            if (roadGraphic == null)
            {
                GameObject go = new GameObject("RoadNetwork", typeof(RectTransform), typeof(CanvasRenderer));
                go.transform.SetParent(viewportRect, false);
                roadGraphic = go.AddComponent<MinimapRoadGraphic>();
            }

            RectTransform rt = roadGraphic.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            roadGraphic.color = roadColor;
            roadGraphic.SetOutlineColor(roadOutlineColor);
            roadGraphic.SetLineWidth(baseRoadLineWidth);
            roadGraphic.raycastTarget = false;
            roadGraphic.enabled = true;
            roadGraphic.transform.SetAsFirstSibling();
        }

        private void BuildRoutePreview()
        {
            if (!showRoutePreview || viewportRect == null) return;

            if (routeLine == null)
            {
                Transform existing = viewportRect.Find("RoutePreview");
                if (existing != null)
                    routeLine = existing.GetComponent<RouteLineGraphic>();
            }

            if (routeLine == null)
            {
                GameObject go = new GameObject("RoutePreview", typeof(RectTransform), typeof(CanvasRenderer));
                go.transform.SetParent(viewportRect, false);
                routeLine = go.AddComponent<RouteLineGraphic>();
            }
            else if (routeLine.transform.parent != viewportRect)
            {
                routeLine.transform.SetParent(viewportRect, false);
            }

            routeLine.color = routeLineColor;
            routeLine.raycastTarget = false;
            routeLine.SetLineWidth(routeLineWidth);

            RectTransform rt = routeLine.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.SetAsLastSibling();
        }

        private void BuildMarkerContainer()
        {
            if (viewportRect == null) return;

            if (markerContainer == null)
            {
                Transform existing = viewportRect.Find("MarkerContainer");
                if (existing == null)
                {
                    GameObject go = new GameObject("MarkerContainer", typeof(RectTransform));
                    go.transform.SetParent(viewportRect, false);
                    existing = go.transform;
                }
                markerContainer = existing;
            }
            else if (markerContainer.parent != viewportRect)
            {
                markerContainer.SetParent(viewportRect, false);
            }

            RectTransform rt = markerContainer as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
            }

            // Ensure markers render above routes
            if (routeLine != null) routeLine.transform.SetAsLastSibling();
            markerContainer.SetAsLastSibling();
        }

        // ────────────────────────────────────────────────────────────────────
        // Navigation binding
        // ────────────────────────────────────────────────────────────────────

        private void BindNavigation()
        {
            NavigationService navService = NavigationService.EnsureInstance();
            if (navService == null)
            {
                if (!loggedNoNavService)
                {
                    Debug.LogWarning("[MinimapUI] NavigationService olusturulamadi.");
                    loggedNoNavService = true;
                }
                nextNavBindTime = Time.unscaledTime + RetryInterval;
                return;
            }

            loggedNoNavService = false;
            if (subscribedNavService == navService)
                return;

            UnbindNavigation();

            subscribedNavService = navService;
            subscribedNavService.OnObjectiveChanged += OnObjectiveChanged;
            subscribedNavService.OnRouteChanged += OnRouteChanged;
            subscribedNavService.OnNavigationCleared += OnNavigationCleared;

            if (!loggedNavBound)
            {
                Debug.Log("[MinimapUI] NavigationService basariyla baglandi.");
                loggedNavBound = true;
            }

            SyncNavigationState();
        }

        private void UnbindNavigation()
        {
            if (subscribedNavService == null) return;

            subscribedNavService.OnObjectiveChanged -= OnObjectiveChanged;
            subscribedNavService.OnRouteChanged -= OnRouteChanged;
            subscribedNavService.OnNavigationCleared -= OnNavigationCleared;
            subscribedNavService = null;
        }

        private void SyncNavigationState()
        {
            if (subscribedNavService == null) return;

            NavigationObjective objective = subscribedNavService.CurrentObjective;
            if (objective.IsValid)
                OnObjectiveChanged(objective);
            else
                OnNavigationCleared();

            OnRouteChanged(subscribedNavService.CurrentRoute);
        }

        // ────────────────────────────────────────────────────────────────────
        // Navigation event handlers
        // ────────────────────────────────────────────────────────────────────

        private void OnObjectiveChanged(NavigationObjective objective)
        {
            currentObjective = objective;
            routeDirty = true;

            if (!objective.IsValid)
            {
                SetObjectiveMarkersVisible(false);
                return;
            }

            if (!initialized) return;
            RefreshObjectiveMarkers();
        }

        private void OnRouteChanged(RouteResult route)
        {
            currentRoute = route ?? RouteResult.Unavailable;
            routeDirty = true;
        }

        private void OnNavigationCleared()
        {
            currentObjective = NavigationObjective.Empty;
            currentRoute = RouteResult.Unavailable;
            routeDirty = true;
            SetObjectiveMarkersVisible(false);
            ClearRoute();
        }

        // ────────────────────────────────────────────────────────────────────
        // Player resolution
        // ────────────────────────────────────────────────────────────────────

        private void ResolvePlayer(bool force = false)
        {
            if (!force && IsUsableTransform(playerTransform))
                return;

            if (!force && Time.unscaledTime < nextPlayerResolveTime)
                return;

            nextPlayerResolveTime = Time.unscaledTime + RetryInterval;

            if (!TryResolvePlayer(out Transform resolved))
            {
                if (!loggedNoPlayer)
                {
                    Debug.LogWarning("[MinimapUI] Oyuncu transform'u bulunamadi, bekleniyor.");
                    loggedNoPlayer = true;
                }
                playerTransform = null;
                minimapCamera?.SetPlayer(null);
                return;
            }

            loggedNoPlayer = false;
            if (playerTransform == resolved && !force)
                return;

            playerTransform = resolved;
            minimapCamera?.SetPlayer(playerTransform);
            MarkAllDirty();
        }

        private bool TryResolvePlayer(out Transform player)
        {
            player = null;

            // 1. QuestManager
            if (QuestManager.Instance != null && IsUsableTransform(QuestManager.Instance.PlayerTransform))
            {
                player = QuestManager.Instance.PlayerTransform;
                return true;
            }

            // 2. PlayerVehicleManager
            PlayerVehicleManager vm = GetVehicleManager();
            if (vm != null && vm.ActiveVehicleController != null &&
                IsUsableTransform(vm.ActiveVehicleController.transform))
            {
                player = vm.ActiveVehicleController.transform;
                return true;
            }

            // 3. Player tag
            GameObject tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null && IsUsableTransform(tagged.transform))
            {
                player = tagged.transform;
                return true;
            }

            // 4. Any CarController
            CarController[] controllers = FindObjectsByType<CarController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Transform inactiveFallback = null;
            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] == null) continue;
                if (IsUsableTransform(controllers[i].transform))
                {
                    player = controllers[i].transform;
                    return true;
                }
                if (inactiveFallback == null)
                    inactiveFallback = controllers[i].transform;
            }

            player = inactiveFallback;
            return player != null;
        }

        private void HandleActiveVehicleChanged(CarController controller)
        {
            SetPlayerTransform(controller != null ? controller.transform : null);
        }

        private PlayerVehicleManager GetVehicleManager()
        {
            if (cachedVehicleManager == null)
                cachedVehicleManager = PlayerVehicleManager.Instance ?? FindFirstObjectByType<PlayerVehicleManager>();
            return cachedVehicleManager;
        }

        // ────────────────────────────────────────────────────────────────────
        // Camera & zoom
        // ────────────────────────────────────────────────────────────────────

        private void ResolveCameraReferences()
        {
            if (minimapCamera == null)
                minimapCamera = FindFirstObjectByType<MinimapCamera>();
            if (cameraComponent == null && minimapCamera != null)
                cameraComponent = minimapCamera.GetComponent<Camera>();
        }

        private void SyncZoom()
        {
            ResolveCameraReferences();

            float resolved = Mathf.Clamp(fixedZoom, MinZoom, MaxZoom);

            if (minimapCamera != null && minimapCamera.CameraComponent != null)
            {
                float camZoom = minimapCamera.CameraComponent.orthographicSize;
                if (camZoom > 0.01f)
                    resolved = Mathf.Clamp(camZoom, MinZoom, MaxZoom);
            }

            if (Mathf.Abs(currentZoom - resolved) > 0.01f)
            {
                currentZoom = resolved;
                MarkAllDirty();
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Toggle input
        // ────────────────────────────────────────────────────────────────────

        private void HandleToggleInput()
        {
#if ENABLE_INPUT_SYSTEM
            bool pressed = Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame;
#if ENABLE_LEGACY_INPUT_MANAGER
            pressed = pressed || Input.GetKeyDown(KeyCode.M);
#endif
#else
            bool pressed = Input.GetKeyDown(KeyCode.M);
#endif
            if (pressed)
                ToggleMinimap();
        }

        // ────────────────────────────────────────────────────────────────────
        // Responsive layout
        // ────────────────────────────────────────────────────────────────────

        private void UpdateResponsiveLayout()
        {
            if (minimapContainer == null) return;

            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            if (screenSize == lastScreenSize) return;
            lastScreenSize = screenSize;

            float scale = Mathf.Clamp(
                Mathf.Min(Screen.width, Screen.height) / Mathf.Max(1f, responsiveReferenceShortSide),
                0.88f, 1.08f);

            Vector2 size = minimapSize * scale;
            size.x = Mathf.Clamp(size.x, minimumMinimapSize.x, maximumMinimapSize.x);
            size.y = Mathf.Clamp(size.y, minimumMinimapSize.y, maximumMinimapSize.y);
            minimapContainer.sizeDelta = size;
            minimapContainer.anchoredPosition = new Vector2(Mathf.Abs(anchorOffset.x), Mathf.Abs(anchorOffset.y));

            if (viewportRect != null)
            {
                float sp = Mathf.Clamp(viewportPadding * scale, 10f, 16f);
                viewportRect.offsetMin = new Vector2(sp, sp);
                viewportRect.offsetMax = new Vector2(-sp, -sp);
            }

            if (viewportFrameRect != null)
            {
                float fp = Mathf.Clamp(framePadding * scale, 5f, 9f);
                viewportFrameRect.offsetMin = new Vector2(fp, fp);
                viewportFrameRect.offsetMax = new Vector2(-fp, -fp);
            }

            MarkAllDirty();
            ApplyMarkerSizing();
        }

        // ────────────────────────────────────────────────────────────────────
        // Road overlay
        // ────────────────────────────────────────────────────────────────────

        private void ResolveRoads()
        {
            if (Time.unscaledTime < nextRoadResolveTime) return;

            if (!TryResolveRoadGraph(out RoadGraph graph))
            {
                if (roadOverlayReady)
                {
                    roadOverlayReady = false;
                    hasRoadBounds = false;
                    worldRoadPolylines.Clear();
                    localRoadPolylines.Clear();
                    cachedRoadGraph = null;
                    lastRoadGraphSegmentCount = -1;
                    roadGraphic?.Clear();
                    ClearRoadTexture();
                }
                nextRoadResolveTime = Time.unscaledTime + RoadRetryInterval;
                return;
            }

            bool graphChanged =
                !ReferenceEquals(cachedRoadGraph, graph) ||
                lastRoadGraphSegmentCount != graph.roadSegments.Count;

            if (roadOverlayReady && !graphChanged)
                return;

            nextRoadResolveTime = Time.unscaledTime + RoadRetryInterval;

            // Build polylines from graph
            worldRoadPolylines.Clear();
            List<List<Vector3>> built = MinimapRoadTextureBuilder.BuildRoadPolylines(graph);
            for (int i = 0; i < built.Count; i++)
            {
                if (built[i] != null && built[i].Count >= 2)
                    worldRoadPolylines.Add(built[i]);
            }

            if (worldRoadPolylines.Count == 0)
            {
                Debug.LogWarning("[MinimapUI] Road graph mevcut ama cizilebilir polyline uretilmedi.");
                return;
            }

            // Build bounds
            if (!TryBuildRoadBounds(graph, out roadBounds))
                return;

            roadOverlayReady = true;
            hasRoadBounds = true;
            cachedRoadGraph = graph;

            if (!loggedRoadOverlayReady)
            {
                Debug.Log($"[MinimapUI] Road overlay hazir: {worldRoadPolylines.Count} polyline, {graph.roadSegments.Count} segment.");
                loggedRoadOverlayReady = true;
            }
            lastRoadGraphSegmentCount = graph.roadSegments.Count;
            roadsDirty = true;
            routeDirty = true;
        }

        private bool TryResolveRoadGraph(out RoadGraph graph)
        {
            graph = null;

            if (roadGraphBuilder == null && Time.unscaledTime >= nextRoadResolveTime)
            {
                roadGraphBuilder = FindFirstObjectByType<RoadGraphBuilder>();
                nextRoadResolveTime = Time.unscaledTime + RoadRetryInterval;
            }

            if (roadGraphBuilder == null)
            {
                if (!loggedNoRoadGraph)
                {
                    Debug.LogWarning("[MinimapUI] RoadGraphBuilder bulunamadi.");
                    loggedNoRoadGraph = true;
                }
                return false;
            }

            loggedNoRoadGraph = false;

            if (!roadGraphBuilder.HasBuiltRoadGraph)
            {
                if (!roadGraphBuilder.HasPendingBuild)
                    roadGraphBuilder.BeginBuildWithDelay(0f);
                return false;
            }

            graph = roadGraphBuilder.RoadGraph;
            return graph != null && graph.roadSegments != null && graph.roadSegments.Count > 0;
        }

        private bool loggedRoadOverlayGuardFail;
        private bool loggedRoadOverlayUpdate;

        private void UpdateRoadOverlay()
        {
            if (!roadOverlayReady || roadGraphic == null || !hasRoadBounds || minimapContainer == null)
            {
                if (roadOverlayReady && !loggedRoadOverlayGuardFail)
                {
                    Debug.LogWarning($"[MinimapUI] UpdateRoadOverlay guard fail: roadGraphic={roadGraphic != null}, " +
                                     $"hasRoadBounds={hasRoadBounds}, minimapContainer={minimapContainer != null}");
                    loggedRoadOverlayGuardFail = true;
                }
                return;
            }

            if (!NeedsUpdate(roadRefreshDistance))
                return;

            Rect rect = GetViewportRect();
            if (rect.width < 1f || rect.height < 1f)
            {
                if (!loggedSmallViewportRect)
                {
                    Debug.LogWarning($"[MinimapUI] Viewport rect cok kucuk ({rect.width:F0}x{rect.height:F0}), yol overlay'i ertelendi.");
                    loggedSmallViewportRect = true;
                }
                return; // Layout henuz hesaplanmadi; dirty flag korunur
            }

            float clipExpansion = 1f + Mathf.Max(0.02f, edgePaddingNormalized);

            // Convert world polylines to local viewport space
            localRoadPolylines.Clear();
            for (int lineIdx = 0; lineIdx < worldRoadPolylines.Count; lineIdx++)
            {
                List<Vector3> worldLine = worldRoadPolylines[lineIdx];
                List<Vector2> localLine = null;

                for (int ptIdx = 1; ptIdx < worldLine.Count; ptIdx++)
                {
                    Vector2 start = WorldToLocal(worldLine[ptIdx - 1], rect);
                    Vector2 end = WorldToLocal(worldLine[ptIdx], rect);

                    if (!ClipLine(start, end, rect, clipExpansion, out Vector2 cs, out Vector2 ce))
                    {
                        localLine = null;
                        continue;
                    }

                    if (localLine == null)
                    {
                        localLine = new List<Vector2>(worldLine.Count);
                        localRoadPolylines.Add(localLine);
                        AddUniquePoint(localLine, cs);
                    }
                    AddUniquePoint(localLine, ce);
                }
            }

            // Apply to graphic
            roadGraphic.color = new Color(roadColor.r, roadColor.g, roadColor.b, mapAlpha);
            roadGraphic.SetOutlineColor(roadOutlineColor);
            roadGraphic.SetLineWidth(GetResolvedRoadLineWidth(rect));

            if (!loggedRoadOverlayUpdate)
            {
                loggedRoadOverlayUpdate = true;
                Debug.Log($"[MinimapUI] UpdateRoadOverlay: rect={rect.width:F0}x{rect.height:F0}, zoom={currentZoom:F1}, " +
                          $"worldPolylines={worldRoadPolylines.Count}, localPolylines={localRoadPolylines.Count}, " +
                          $"mapCenter={mapCenter}, graphic={roadGraphic != null}, graphicEnabled={roadGraphic?.enabled}, " +
                          $"canvas={roadGraphic?.canvas != null}, parent={roadGraphic?.transform.parent?.name}, " +
                          $"canvasRenderer={roadGraphic?.canvasRenderer != null}");
            }

            if (localRoadPolylines.Count > 0)
            {
                roadGraphic.SetPolylines(localRoadPolylines);
                // Hide texture fallback when vector roads work
                if (roadOverlayImage != null)
                    roadOverlayImage.color = new Color(1f, 1f, 1f, 0f);
            }
            else if (worldRoadPolylines.Count == 0)
            {
                roadGraphic.Clear();
            }
            else
            {
                // World polylines exist but none converted to local coords.
                // Use texture fallback for this frame.
                roadGraphic.Clear();
                RefreshRoadTextureFallback(rect);
            }

            // Only clear dirty when we produced renderable output or
            // there is genuinely no source data left to render.
            if (localRoadPolylines.Count > 0 || worldRoadPolylines.Count == 0)
            {
                roadsDirty = false;
                SaveUpdateState();
            }
        }

        private void RefreshRoadTextureFallback(Rect rect)
        {
            if (roadOverlayImage == null || cachedRoadGraph == null || !hasRoadBounds)
                return;

            if (roadOverlayTexture != null)
            {
                Destroy(roadOverlayTexture);
                roadOverlayTexture = null;
            }

            float visibleHeight = currentZoom * 2f;
            float visibleWidth = visibleHeight * (rect.width / Mathf.Max(1f, rect.height));
            float extent = Mathf.Max(visibleWidth, visibleHeight) * 0.82f;

            Bounds localBounds = new Bounds(
                new Vector3(mapCenter.x, 0f, mapCenter.z),
                new Vector3(extent * 2f, 1f, extent * 2f));

            int resolution = cachedRoadGraph.roadSegments.Count > 900 ? 1024 : 1536;
            int roadWidthPx = Mathf.Max(3, Mathf.RoundToInt(baseRoadLineWidth * 1.75f));

            roadOverlayTexture = MinimapRoadTextureBuilder.Build(
                cachedRoadGraph, localBounds, resolution,
                new Color(0f, 0f, 0f, 0f),
                new Color(roadColor.r, roadColor.g, roadColor.b, 1f),
                new Color(roadOutlineColor.r, roadOutlineColor.g, roadOutlineColor.b, 1f),
                roadWidthPx);

            roadOverlayImage.texture = roadOverlayTexture;
            roadOverlayImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            roadOverlayImage.color = new Color(1f, 1f, 1f, mapAlpha);

            float rotation = alignMapToPlayerHeading && playerTransform != null ? -playerTransform.eulerAngles.y : 0f;
            roadOverlayImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private void ClearRoadTexture()
        {
            if (roadOverlayImage != null)
            {
                roadOverlayImage.texture = null;
                roadOverlayImage.color = new Color(1f, 1f, 1f, 0f);
            }
            if (roadOverlayTexture != null)
            {
                Destroy(roadOverlayTexture);
                roadOverlayTexture = null;
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Route preview
        // ────────────────────────────────────────────────────────────────────

        private void UpdateRoutePreview()
        {
            if (!showRoutePreview || routeLine == null || minimapContainer == null)
                return;

            if (currentRoute == null || !currentRoute.IsRenderable)
            {
                ClearRoute();
                routeDirty = false;
                return;
            }

            if (!routeDirty && !NeedsUpdate(routeRefreshDistance))
                return;

            Rect rect = GetViewportRect();
            if (rect.width < 1f || rect.height < 1f)
                return; // Layout henuz hesaplanmadi; routeDirty korunur

            BuildRouteLocalPoints(currentRoute.Points, rect);

            if (routeLocalPoints.Count < 2)
            {
                ClearRoute();
                // Keep routeDirty if we have valid route data that just
                // failed to convert (e.g. all points off viewport).
                if (currentRoute.Points != null && currentRoute.Points.Count >= 2)
                    return;
            }
            else
            {
                Color color = currentRoute.IsFallback ? fallbackRouteLineColor : routeLineColor;
                if (currentRoute.IsStale)
                    color.a *= 0.68f;

                routeLine.color = color;
                routeLine.SetLineWidth(currentRoute.IsFallback ? Mathf.Max(3.8f, routeLineWidth - 0.6f) : routeLineWidth);
                routeLine.SetPoints(routeLocalPoints);
            }

            routeDirty = false;
            SaveUpdateState();
        }

        private void BuildRouteLocalPoints(IReadOnlyList<Vector3> worldPoints, Rect rect)
        {
            routeLocalPoints.Clear();
            if (worldPoints == null || worldPoints.Count < 2) return;

            float clipExpansion = 1f + Mathf.Max(0.04f, routeViewportExpansion * 0.25f);
            bool entered = false;

            for (int i = 1; i < worldPoints.Count; i++)
            {
                Vector2 prev = WorldToLocal(worldPoints[i - 1], rect);
                Vector2 curr = WorldToLocal(worldPoints[i], rect);

                if (!ClipLine(prev, curr, rect, clipExpansion, out Vector2 cs, out Vector2 ce))
                {
                    if (entered) break;
                    continue;
                }

                if (!entered)
                {
                    AddUniquePoint(routeLocalPoints, cs);
                    entered = true;
                }
                AddUniquePoint(routeLocalPoints, ce);
            }
        }

        private void ClearRoute()
        {
            routeLine?.Clear();
        }

        // ────────────────────────────────────────────────────────────────────
        // Markers
        // ────────────────────────────────────────────────────────────────────

        private void CreatePlayerMarker()
        {
            if (markerContainer == null || playerMarkerView != null) return;

            Sprite arrow = spriteRegistry != null
                ? spriteRegistry.GetPlayerArrowSprite()
                : MinimapSpriteRegistry.GetFallbackPlayerArrowSprite();

            if (arrow == null)
            {
                Debug.LogError("[MinimapUI] Player arrow sprite olusturulamadi.");
                return;
            }

            playerMarkerView = new MarkerView();

            // Shadow
            playerMarkerView.Shadow = CreateImageObject("PlayerMarkerShadow", markerContainer, playerMarkerSize);
            Image shadowImg = playerMarkerView.Shadow.GetComponent<Image>();
            shadowImg.sprite = arrow;
            shadowImg.preserveAspect = true;
            shadowImg.color = new Color(0f, 0f, 0f, 0.40f);
            shadowImg.raycastTarget = false;

            // Root (plate)
            playerMarkerView.Root = CreateImageObject("PlayerMarker", markerContainer, playerMarkerSize);
            Image plateImg = playerMarkerView.Root.GetComponent<Image>();
            plateImg.sprite = GetCircleMaskSprite();
            plateImg.color = new Color(0.06f, 0.08f, 0.11f, 0.88f);
            plateImg.raycastTarget = false;

            // Arrow icon
            RectTransform arrowRect = CreateImageObject("Arrow", playerMarkerView.Root, Mathf.Max(18f, playerMarkerSize - 6f));
            playerMarkerView.Icon = arrowRect.GetComponent<Image>();
            playerMarkerView.Icon.sprite = arrow;
            playerMarkerView.Icon.preserveAspect = true;
            playerMarkerView.Icon.color = playerMarkerColor;
            playerMarkerView.Icon.raycastTarget = false;

            playerMarkerView.BaseScale = Vector3.one;
            SetMarkerAnchoredPosition(playerMarkerView, Vector2.zero, true);
            playerMarkerView.Root.SetAsLastSibling();
            ApplyPlayerMarkerSizing(GetResolvedPlayerMarkerSize());
        }

        private MarkerView CreateObjectiveMarker(string name, string label, Color color, bool diamondIcon)
        {
            MarkerView marker = new MarkerView();
            float size = markerSize;

            // Shadow
            marker.Shadow = CreateImageObject($"{name}Shadow", markerContainer, size + 6f);
            Image shadowImg = marker.Shadow.GetComponent<Image>();
            shadowImg.sprite = GetSolidSprite();
            shadowImg.color = new Color(0f, 0f, 0f, 0.30f);
            shadowImg.raycastTarget = false;

            // Root (frame)
            marker.Root = CreateImageObject(name, markerContainer, size + 2f);
            Image rootImg = marker.Root.GetComponent<Image>();
            rootImg.sprite = GetSolidSprite();
            rootImg.color = markerFrameColor;
            rootImg.raycastTarget = false;

            // Icon
            RectTransform iconRect = CreateImageObject("Icon", marker.Root, Mathf.Max(12f, size * 0.68f));
            if (diamondIcon) iconRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
            marker.Icon = iconRect.GetComponent<Image>();
            marker.Icon.sprite = diamondIcon ? GetCircleMaskSprite() : GetSolidSprite();
            marker.Icon.color = color;
            marker.Icon.raycastTarget = false;

            // Label
            RectTransform labelRect = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<RectTransform>();
            labelRect.SetParent(marker.Root, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            marker.Label = labelRect.GetComponent<TextMeshProUGUI>();
            marker.Label.text = label;
            marker.Label.alignment = TextAlignmentOptions.Center;
            marker.Label.fontSize = 11f;
            marker.Label.fontStyle = FontStyles.Bold;
            marker.Label.color = Color.white;
            marker.Label.outlineWidth = 0.16f;
            marker.Label.outlineColor = new Color(0f, 0f, 0f, 0.95f);
            marker.Label.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null)
                marker.Label.font = TMP_Settings.defaultFontAsset;

            marker.BaseScale = Vector3.one;
            marker.SetVisible(false);
            return marker;
        }

        private void RefreshObjectiveMarkers()
        {
            HideAllObjectiveMarkers();

            if (!currentObjective.IsValid || markerContainer == null) return;

            if (currentObjective.Type == ObjectiveType.Pickup)
            {
                if (pickupMarker == null)
                    pickupMarker = CreateObjectiveMarker("PickupMarker", "P", pickupMarkerColor, true);
                pickupMarker.Icon.color = pickupMarkerColor;
                pickupMarker.Label.text = "P";
                pickupMarker.SetVisible(true);
                UpdateMarkerWorldPosition(pickupMarker, currentObjective.WorldPosition);
            }
            else if (currentObjective.Type == ObjectiveType.Delivery)
            {
                if (deliveryMarker == null)
                    deliveryMarker = CreateObjectiveMarker("DeliveryMarker", "D", deliveryMarkerColor, false);
                deliveryMarker.Icon.color = deliveryMarkerColor;
                deliveryMarker.Label.text = currentObjective.DeliveryIndex > 0
                    ? currentObjective.DeliveryIndex.ToString()
                    : "D";
                deliveryMarker.SetVisible(true);
                UpdateMarkerWorldPosition(deliveryMarker, currentObjective.WorldPosition);
            }
        }

        private void HideAllObjectiveMarkers()
        {
            pickupMarker?.SetVisible(false);
            deliveryMarker?.SetVisible(false);
        }

        private void SetObjectiveMarkersVisible(bool visible)
        {
            if (pickupMarker != null)
                pickupMarker.SetVisible(visible && currentObjective.Type == ObjectiveType.Pickup);
            if (deliveryMarker != null)
                deliveryMarker.SetVisible(visible && currentObjective.Type == ObjectiveType.Delivery);
        }

        private void UpdateObjectiveMarkerPositions()
        {
            if (!initialized || minimapContainer == null || !currentObjective.IsValid) return;

            Vector3 worldPos = currentObjective.WorldPosition;

            if (pickupMarker != null && pickupMarker.Root.gameObject.activeSelf)
                UpdateMarkerWorldPosition(pickupMarker, worldPos);
            if (deliveryMarker != null && deliveryMarker.Root.gameObject.activeSelf)
                UpdateMarkerWorldPosition(deliveryMarker, worldPos);
        }

        private void UpdateMarkerWorldPosition(MarkerView marker, Vector3 worldPosition)
        {
            if (marker?.Root == null || minimapContainer == null) return;

            Rect rect = GetViewportRect();
            Vector2 local = WorldToLocal(worldPosition, rect);

            if (clampObjectivesToEdge)
            {
                local = ClampToViewport(local, rect);
                marker.SetVisible(true);
            }
            else
            {
                bool inBounds = Mathf.Abs(local.x) <= rect.width * 0.65f &&
                                Mathf.Abs(local.y) <= rect.height * 0.65f;
                marker.SetVisible(inBounds);
                if (!inBounds) return;
            }

            SetMarkerAnchoredPosition(marker, local, false);
        }

        private void UpdatePlayerMarkerRotation()
        {
            if (playerMarkerView == null || playerTransform == null) return;

            float yRotation = alignMapToPlayerHeading ? 0f : -playerTransform.eulerAngles.y;
            playerMarkerView.Root.localRotation = Quaternion.Euler(0f, 0f, yRotation);
            if (playerMarkerView.Shadow != null)
                playerMarkerView.Shadow.localRotation = playerMarkerView.Root.localRotation;
        }

        private void UpdateMarkerPulse()
        {
            float pulse = 1f + (Mathf.Sin(Time.time * markerPulseSpeed * Mathf.PI) * 0.5f + 0.5f) * (markerPulseScale - 1f);
            Vector3 scale = new Vector3(pulse, pulse, 1f);
            PulseMarker(pickupMarker, scale);
            PulseMarker(deliveryMarker, scale);
        }

        private static void PulseMarker(MarkerView marker, Vector3 pulse)
        {
            if (marker?.Root == null || !marker.Root.gameObject.activeSelf) return;
            marker.Root.localScale = Vector3.Scale(marker.BaseScale, pulse);
            if (marker.Shadow != null)
                marker.Shadow.localScale = Vector3.Scale(marker.BaseScale, pulse);
        }

        private static void SetMarkerAnchoredPosition(MarkerView marker, Vector2 pos, bool isPlayer)
        {
            if (marker == null) return;
            if (marker.Root != null)
                marker.Root.anchoredPosition = pos;
            if (marker.Shadow != null)
            {
                Vector2 offset = isPlayer ? PlayerMarkerShadowOffset : ObjectiveMarkerShadowOffset;
                marker.Shadow.anchoredPosition = pos + offset;
            }
        }

        private void ApplyMarkerSizing()
        {
            float objSize = GetResolvedObjectiveMarkerSize();
            ApplyObjectiveMarkerSizing(pickupMarker, objSize);
            ApplyObjectiveMarkerSizing(deliveryMarker, objSize);
            ApplyPlayerMarkerSizing(GetResolvedPlayerMarkerSize());
        }

        private float GetResolvedObjectiveMarkerSize()
        {
            Rect rect = GetViewportRect();
            float vp = Mathf.Min(rect.width, rect.height);
            return vp > 0.01f ? Mathf.Max(markerSize, vp * 0.12f) : markerSize;
        }

        private float GetResolvedPlayerMarkerSize()
        {
            Rect rect = GetViewportRect();
            float vp = Mathf.Min(rect.width, rect.height);
            return vp > 0.01f ? Mathf.Max(playerMarkerSize, vp * 0.16f) : playerMarkerSize;
        }

        private static void ApplyObjectiveMarkerSizing(MarkerView marker, float size)
        {
            if (marker == null) return;
            if (marker.Shadow != null) marker.Shadow.sizeDelta = Vector2.one * (size + 6f);
            if (marker.Root != null) marker.Root.sizeDelta = Vector2.one * (size + 2f);
            if (marker.Icon != null) marker.Icon.rectTransform.sizeDelta = Vector2.one * Mathf.Max(12f, size * 0.68f);
            if (marker.Label != null) marker.Label.fontSize = Mathf.Clamp(size * 0.5f, 11f, 16f);
        }

        private void ApplyPlayerMarkerSizing(float size)
        {
            if (playerMarkerView == null) return;
            if (playerMarkerView.Shadow != null) playerMarkerView.Shadow.sizeDelta = Vector2.one * (size + 3f);
            if (playerMarkerView.Root != null) playerMarkerView.Root.sizeDelta = Vector2.one * size;
            if (playerMarkerView.Icon != null) playerMarkerView.Icon.rectTransform.sizeDelta = Vector2.one * Mathf.Max(18f, size * 0.78f);
        }

        // ────────────────────────────────────────────────────────────────────
        // Coordinate conversion
        // ────────────────────────────────────────────────────────────────────

        private Vector2 WorldToLocal(Vector3 worldPos, Rect rect)
        {
            if (rect.width <= 0.01f || rect.height <= 0.01f)
                return Vector2.zero;

            float visibleHeight = currentZoom * 2f;
            float visibleWidth = visibleHeight * (rect.width / Mathf.Max(1f, rect.height));

            Vector3 delta = worldPos - mapCenter;
            if (alignMapToPlayerHeading && playerTransform != null)
                delta = Quaternion.Euler(0f, -playerTransform.eulerAngles.y, 0f) * delta;

            return new Vector2(
                (delta.x / visibleWidth) * rect.width,
                (delta.z / visibleHeight) * rect.height);
        }

        private Vector2 ClampToViewport(Vector2 local, Rect rect)
        {
            float px = Mathf.Max(10f, rect.width * edgePaddingNormalized);
            float py = Mathf.Max(10f, rect.height * edgePaddingNormalized);
            return new Vector2(
                Mathf.Clamp(local.x, -rect.width * 0.5f + px, rect.width * 0.5f - px),
                Mathf.Clamp(local.y, -rect.height * 0.5f + py, rect.height * 0.5f - py));
        }

        private static bool ClipLine(Vector2 start, Vector2 end, Rect rect, float expansion,
            out Vector2 clippedStart, out Vector2 clippedEnd)
        {
            float hw = rect.width * 0.5f * Mathf.Max(1f, expansion);
            float hh = rect.height * 0.5f * Mathf.Max(1f, expansion);
            float minX = -hw, maxX = hw, minY = -hh, maxY = hh;

            clippedStart = start;
            clippedEnd = end;

            int sc = OutCode(clippedStart, minX, maxX, minY, maxY);
            int ec = OutCode(clippedEnd, minX, maxX, minY, maxY);

            while (true)
            {
                if ((sc | ec) == 0) return true;
                if ((sc & ec) != 0) return false;

                int code = sc != 0 ? sc : ec;
                float x = 0f, y = 0f;

                if ((code & 8) != 0)
                {
                    x = clippedStart.x + (clippedEnd.x - clippedStart.x) * (maxY - clippedStart.y) / (clippedEnd.y - clippedStart.y);
                    y = maxY;
                }
                else if ((code & 4) != 0)
                {
                    x = clippedStart.x + (clippedEnd.x - clippedStart.x) * (minY - clippedStart.y) / (clippedEnd.y - clippedStart.y);
                    y = minY;
                }
                else if ((code & 2) != 0)
                {
                    y = clippedStart.y + (clippedEnd.y - clippedStart.y) * (maxX - clippedStart.x) / (clippedEnd.x - clippedStart.x);
                    x = maxX;
                }
                else if ((code & 1) != 0)
                {
                    y = clippedStart.y + (clippedEnd.y - clippedStart.y) * (minX - clippedStart.x) / (clippedEnd.x - clippedStart.x);
                    x = minX;
                }

                if (code == sc)
                {
                    clippedStart = new Vector2(x, y);
                    sc = OutCode(clippedStart, minX, maxX, minY, maxY);
                }
                else
                {
                    clippedEnd = new Vector2(x, y);
                    ec = OutCode(clippedEnd, minX, maxX, minY, maxY);
                }
            }
        }

        private static int OutCode(Vector2 p, float minX, float maxX, float minY, float maxY)
        {
            int code = 0;
            if (p.x < minX) code |= 1;
            else if (p.x > maxX) code |= 2;
            if (p.y < minY) code |= 4;
            else if (p.y > maxY) code |= 8;
            return code;
        }

        // ────────────────────────────────────────────────────────────────────
        // Dirty tracking
        // ────────────────────────────────────────────────────────────────────

        private bool NeedsUpdate(float distanceThreshold)
        {
            if (routeDirty || roadsDirty) return true;
            if (float.IsNaN(lastUpdatePosition.x)) return true;
            if ((lastUpdatePosition - mapCenter).sqrMagnitude >= distanceThreshold * distanceThreshold) return true;
            if (Mathf.Abs(lastUpdateZoom - currentZoom) > 0.25f) return true;

            float heading = GetHeading();
            if (Mathf.Abs(Mathf.DeltaAngle(lastUpdateHeading, heading)) > 1f) return true;

            return false;
        }

        private void SaveUpdateState()
        {
            lastUpdatePosition = mapCenter;
            lastUpdateZoom = currentZoom;
            lastUpdateHeading = GetHeading();
        }

        private void MarkAllDirty()
        {
            routeDirty = true;
            roadsDirty = true;
            lastUpdatePosition = new Vector3(float.NaN, float.NaN, float.NaN);
            lastUpdateZoom = -1f;
            lastUpdateHeading = float.NaN;
        }

        private float GetHeading()
        {
            if (!alignMapToPlayerHeading || playerTransform == null)
                return 0f;
            return playerTransform.eulerAngles.y;
        }

        // ────────────────────────────────────────────────────────────────────
        // Utility
        // ────────────────────────────────────────────────────────────────────

        private Rect GetViewportRect()
        {
            if (viewportRect == null && minimapContainer != null)
                viewportRect = minimapContainer.Find("Viewport") as RectTransform;
            if (viewportRect != null)
                return viewportRect.rect;
            return minimapContainer != null ? minimapContainer.rect : new Rect(0f, 0f, 1f, 1f);
        }

        private static bool IsUsableTransform(Transform t)
        {
            return t != null && t.gameObject != null && t.gameObject.activeInHierarchy;
        }

        private static void AddUniquePoint(List<Vector2> points, Vector2 point)
        {
            if (points.Count > 0 && (points[points.Count - 1] - point).sqrMagnitude <= 0.25f)
            {
                points[points.Count - 1] = point;
                return;
            }
            points.Add(point);
        }

        private float GetResolvedRoadLineWidth(Rect rect)
        {
            float vp = Mathf.Min(rect.width, rect.height);
            return vp > 0.01f ? Mathf.Max(baseRoadLineWidth, vp * 0.024f) : baseRoadLineWidth;
        }

        private bool TryBuildRoadBounds(RoadGraph graph, out Bounds bounds)
        {
            bounds = default;
            if (graph?.roadSegments == null) return false;

            bool init = false;
            for (int i = 0; i < graph.roadSegments.Count; i++)
            {
                RoadSegment seg = graph.roadSegments[i];
                if (seg?.waypoints == null) continue;

                for (int j = 0; j < seg.waypoints.Count; j++)
                {
                    Vector3 pos = seg.waypoints[j].position;
                    Vector3 flat = new Vector3(pos.x, 0f, pos.z);
                    if (!init) { bounds = new Bounds(flat, Vector3.one); init = true; }
                    else bounds.Encapsulate(flat);
                }
            }

            if (!init) return false;
            bounds.Expand(new Vector3(roadBoundsPadding * 2f, 1f, roadBoundsPadding * 2f));
            return true;
        }

        private static Transform ResolvePreferredParent()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null && canvases[i].name == QuestCanvasName)
                    return canvases[i].transform;
            }

            Canvas globalCanvas = GlobalUiCoordinator.PrimaryCanvas;
            if (globalCanvas != null) return globalCanvas.transform;

            return canvases.Length > 0 ? canvases[0].transform : null;
        }

        private static RectTransform CreateImageObject(string name, Transform parent, float size)
        {
            RectTransform rt = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.one * size;
            return rt;
        }

        // ────────────────────────────────────────────────────────────────────
        // Static sprite generation
        // ────────────────────────────────────────────────────────────────────

        private static Sprite GetCircleMaskSprite()
        {
            if (circleMaskSprite != null) return circleMaskSprite;

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
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01((radius - dist) / feather);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            circleMaskTexture.SetPixels32(pixels);
            circleMaskTexture.Apply(false, true);
            circleMaskSprite = Sprite.Create(circleMaskTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            circleMaskSprite.name = "RuntimeMinimapCircle";
            return circleMaskSprite;
        }

        private static Sprite GetSolidSprite()
        {
            if (solidSprite != null) return solidSprite;

            Texture2D tex = new Texture2D(2, 2, TextureFormat.ARGB32, false, true);
            tex.name = "RuntimeMinimapSolid";
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply(false, true);
            solidSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
            solidSprite.name = "RuntimeMinimapSolid";
            return solidSprite;
        }
    }
}
