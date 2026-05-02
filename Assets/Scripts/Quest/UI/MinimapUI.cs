using System;
using System.Collections.Generic;
using DeliveryDriver.Company;
using DeliveryDriver.Navigation;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TrafficSystem;

namespace DeliveryDriver.Quest.UI
{
    public class MinimapUI : MonoBehaviour
    {
        private const string GameSceneName = "Game";
        private const string PanelName = "MinimapPanel";
        private const string ViewportName = "MinimapViewport";
        private const string OverlayName = "MinimapOverlay";
        private const string PlayerMarkerName = "PlayerMarker";
        private const string ObjectiveMarkerName = "ObjectiveMarker";
        private const string CameraName = "MinimapCamera";
        private const string RenderTextureName = "MinimapRenderTexture";
        private const string BorderObjectName = "Border";

        [Header("Layout")]
        [SerializeField] private bool showMinimap = true;
        [SerializeField] private Vector2 anchoredPosition = new Vector2(48f, 48f);
        [SerializeField] private Vector2 panelSize = new Vector2(332f, 332f);
        [SerializeField] private float cornerRadiusPadding = 12f;

        [Header("Camera")]
        [SerializeField] private float cameraHeight = 110f;
        [SerializeField] private float orthographicSize = 90f;
        [SerializeField] private float cameraFollowSpeed = 10f;
        [SerializeField] private float worldEdgePadding = 6f;
        [SerializeField] private int renderTextureSize = 512;
        [SerializeField] private LayerMask minimapCullingMask = ~0;
        [SerializeField] private bool hideNpcVehiclesOnMinimap = true;
        [SerializeField] private float npcRendererRefreshInterval = 1f;

        [Header("Route")]
        [SerializeField] private bool showRoutePreview = true;
        [SerializeField] private float routeThickness = 4f;
        [SerializeField] private float routeSimplifyTolerance = 9f;
        [SerializeField] private float routeMinSegmentScreenLength = 1.5f;
        [SerializeField] private int routeVisualSmoothingPasses = 2;

        [Header("Style")]
        [SerializeField] private Color frameColor = new Color(0.05f, 0.08f, 0.13f, 0.92f);
        [SerializeField] private Color viewportTint = new Color(0.98f, 1f, 1f, 0.98f);
        [SerializeField] private Color badgeColor = new Color(0.08f, 0.13f, 0.2f, 0.92f);
        [SerializeField] private Color playerMarkerColor = new Color(1f, 0.97f, 0.92f, 1f);
        [SerializeField] private Color pickupMarkerColor = new Color(0.22f, 0.72f, 1f, 1f);
        [SerializeField] private Color deliveryMarkerColor = new Color(0.25f, 0.98f, 0.48f, 1f);
        [SerializeField] private Color routeColor = new Color(1f, 0.86f, 0.22f, 0.9f);
        [SerializeField] private Color pickupRouteColor = new Color(0.2f, 0.62f, 1f, 0.95f);
        [SerializeField] private Color labelColor = new Color(0.92f, 0.97f, 1f, 0.92f);

        private RectTransform panelRect;
        private RectTransform viewportRect;
        private RectTransform overlayRect;
        private RectTransform titleBadgeRect;
        private RectTransform playerMarkerRect;
        private RectTransform objectiveMarkerRect;
        private RawImage minimapImage;
        private TextMeshProUGUI titleText;
        private Image objectiveMarkerImage;

        private Camera minimapCamera;
        private RenderTexture minimapTexture;
        private Collider mapBoundsCollider;

        private Transform playerTransform;
        private NavigationService subscribedNavigationService;
        private PlayerVehicleManager cachedVehicleManager;
        private NavigationObjective currentObjective;
        private RouteResult currentRoute = RouteResult.Unavailable;

        private readonly List<Image> routeSegmentImages = new List<Image>();
        private readonly List<Vector2> projectedRoutePoints = new List<Vector2>();
        private readonly List<Vector2> simplifiedRoutePoints = new List<Vector2>();
        private readonly List<Renderer> cachedNpcRenderers = new List<Renderer>();
        private readonly List<Renderer> temporarilyHiddenNpcRenderers = new List<Renderer>();
        [SerializeField] private int activeRouteSegmentCount;
        [SerializeField] private int peakRouteSegmentCount;

        private float nextNavigationBindTime;
        private float nextPlayerResolveTime;
        private bool hasObjective;
        private bool overlayDirty = true;
        private bool objectiveMarkerDirty = true;
        private Vector3 lastOverlayCameraPosition = Vector3.positiveInfinity;
        private Vector3 lastOverlayObjectiveWorldPosition = Vector3.positiveInfinity;
        private const float OverlayRefreshDistanceThreshold = 0.75f;
        private float nextNpcRendererRefreshTime;
        private bool npcRenderersHiddenForMinimap;

        private static Sprite whiteSprite;
        public int ActiveRouteSegmentCount => activeRouteSegmentCount;
        public int PeakRouteSegmentCount => peakRouteSegmentCount;

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            PlayerVehicleManager.ActiveVehicleChanged += HandleActiveVehicleChanged;
            RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += HandleEndCameraRendering;

            EnsureMinimap();
            ResolveMapBounds();
            ResolvePlayerTransform();
            TryBindNavigationService();
            SetMinimapVisible(ShouldDisplayMinimap());
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            PlayerVehicleManager.ActiveVehicleChanged -= HandleActiveVehicleChanged;
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= HandleEndCameraRendering;
            UnbindNavigationService();
            SetMinimapVisible(false);
            DisableCamera();
            RestoreNpcRenderersForMinimap();
        }

        private void OnDestroy()
        {
            ReleaseRenderTexture();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            bool shouldDisplay = ShouldDisplayMinimap();
            if (minimapCamera == null || panelRect == null || viewportRect == null || overlayRect == null)
            {
                EnsureMinimap();
            }
            SetMinimapVisible(shouldDisplay);
            if (!shouldDisplay)
            {
                DisableCamera();
                return;
            }

            if (subscribedNavigationService == null && Time.unscaledTime >= nextNavigationBindTime)
            {
                TryBindNavigationService();
            }

            if (playerTransform == null && Time.unscaledTime >= nextPlayerResolveTime)
            {
                ResolvePlayerTransform();
            }

            UpdateCamera();
            UpdatePlayerMarker();
            RefreshOverlayIfNeeded();
        }

        private void RefreshOverlayIfNeeded()
        {
            if (minimapCamera == null)
            {
                return;
            }

            Vector3 cameraPosition = minimapCamera.transform.position;
            bool cameraMovedEnough = lastOverlayCameraPosition.x == float.PositiveInfinity ||
                (cameraPosition - lastOverlayCameraPosition).sqrMagnitude >= OverlayRefreshDistanceThreshold * OverlayRefreshDistanceThreshold;

            bool objectiveMoved = hasObjective &&
                (lastOverlayObjectiveWorldPosition.x == float.PositiveInfinity ||
                 (currentObjective.WorldPosition - lastOverlayObjectiveWorldPosition).sqrMagnitude >= 0.01f);

            if (!cameraMovedEnough && !overlayDirty && !objectiveMarkerDirty && !objectiveMoved)
            {
                return;
            }

            UpdateObjectiveMarker();
            UpdateRouteOverlay();
            lastOverlayCameraPosition = cameraPosition;
            lastOverlayObjectiveWorldPosition = hasObjective ? currentObjective.WorldPosition : Vector3.positiveInfinity;
            overlayDirty = false;
            objectiveMarkerDirty = false;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureMinimap();
            ResolveMapBounds();
            ResolvePlayerTransform(forceRefresh: true);
            TryBindNavigationService();
            SetMinimapVisible(ShouldDisplayMinimap());
        }

        private void EnsureMinimap()
        {
            if (!showMinimap)
            {
                return;
            }

            EnsureCamera();
            EnsureHud();
        }

        private void EnsureCamera()
        {
            if (minimapCamera == null)
            {
                Transform existing = transform.Find(CameraName);
                if (existing != null)
                {
                    minimapCamera = existing.GetComponent<Camera>();
                }
            }

            if (minimapCamera == null)
            {
                GameObject cameraObject = new GameObject(CameraName, typeof(Camera));
                cameraObject.transform.SetParent(transform, false);
                minimapCamera = cameraObject.GetComponent<Camera>();
            }

            EnsureRenderTexture();

            minimapCamera.orthographic = true;
            minimapCamera.orthographicSize = Mathf.Max(20f, orthographicSize);
            minimapCamera.clearFlags = CameraClearFlags.SolidColor;
            minimapCamera.backgroundColor = new Color(0.08f, 0.11f, 0.14f, 1f);
            minimapCamera.nearClipPlane = 0.3f;
            minimapCamera.farClipPlane = Mathf.Max(250f, cameraHeight + 200f);
            minimapCamera.allowHDR = false;
            minimapCamera.allowMSAA = false;
            minimapCamera.cullingMask = minimapCullingMask.value == 0 ? ~0 : minimapCullingMask;
            minimapCamera.targetTexture = minimapTexture;
        }

        private void EnsureRenderTexture()
        {
            int safeSize = Mathf.Clamp(renderTextureSize, 128, 2048);
            if (minimapTexture != null && minimapTexture.width == safeSize && minimapTexture.height == safeSize)
            {
                if (minimapImage != null && minimapImage.texture != minimapTexture)
                {
                    minimapImage.texture = minimapTexture;
                }

                return;
            }

            ReleaseRenderTexture();

            minimapTexture = new RenderTexture(safeSize, safeSize, 16, RenderTextureFormat.ARGB32)
            {
                name = RenderTextureName,
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false
            };
            minimapTexture.Create();

            if (minimapImage != null)
            {
                minimapImage.texture = minimapTexture;
            }

            if (minimapCamera != null)
            {
                minimapCamera.targetTexture = minimapTexture;
            }
        }

        private void ReleaseRenderTexture()
        {
            if (minimapTexture == null)
            {
                return;
            }

            if (minimapCamera != null && minimapCamera.targetTexture == minimapTexture)
            {
                minimapCamera.targetTexture = null;
            }

            minimapTexture.Release();
            Destroy(minimapTexture);
            minimapTexture = null;
        }

        private void EnsureHud()
        {
            Transform hudParent = ResolveHudParent();
            if (hudParent == null)
            {
                return;
            }

            if (panelRect == null)
            {
                Transform existing = hudParent.Find(PanelName);
                if (existing is RectTransform existingRect)
                {
                    panelRect = existingRect;
                    viewportRect = existingRect.Find(ViewportName) as RectTransform;
                    overlayRect = existingRect.Find(OverlayName) as RectTransform;
                    titleBadgeRect = existingRect.Find("LabelBadge") as RectTransform;
                    playerMarkerRect = overlayRect != null ? overlayRect.Find(PlayerMarkerName) as RectTransform : null;
                    objectiveMarkerRect = overlayRect != null ? overlayRect.Find(ObjectiveMarkerName) as RectTransform : null;
                    minimapImage = viewportRect != null ? viewportRect.GetComponentInChildren<RawImage>(true) : null;
                    titleText = titleBadgeRect != null
                        ? titleBadgeRect.GetComponentInChildren<TextMeshProUGUI>(true)
                        : existingRect.GetComponentInChildren<TextMeshProUGUI>(true);
                }
            }

            if (panelRect == null)
            {
                BuildHud(hudParent);
            }

            ConfigureHud();
        }

        private void BuildHud(Transform hudParent)
        {
            GameObject panelObject = new GameObject(PanelName, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(hudParent, false);
            panelRect = panelObject.GetComponent<RectTransform>();

            GameObject titleBadgeObject = new GameObject("LabelBadge", typeof(RectTransform), typeof(Image));
            titleBadgeObject.transform.SetParent(panelObject.transform, false);
            titleBadgeRect = titleBadgeObject.GetComponent<RectTransform>();

            GameObject titleObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObject.transform.SetParent(titleBadgeObject.transform, false);
            titleText = titleObject.GetComponent<TextMeshProUGUI>();

            GameObject viewportObject = new GameObject(ViewportName, typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportObject.transform.SetParent(panelObject.transform, false);
            viewportRect = viewportObject.GetComponent<RectTransform>();

            GameObject mapObject = new GameObject("MapImage", typeof(RectTransform), typeof(RawImage));
            mapObject.transform.SetParent(viewportObject.transform, false);
            minimapImage = mapObject.GetComponent<RawImage>();

            GameObject overlayObject = new GameObject(OverlayName, typeof(RectTransform));
            overlayObject.transform.SetParent(viewportObject.transform, false);
            overlayRect = overlayObject.GetComponent<RectTransform>();

            GameObject playerMarkerObject = new GameObject(PlayerMarkerName, typeof(RectTransform), typeof(Image));
            playerMarkerObject.transform.SetParent(overlayObject.transform, false);
            playerMarkerRect = playerMarkerObject.GetComponent<RectTransform>();

            GameObject objectiveMarkerObject = new GameObject(ObjectiveMarkerName, typeof(RectTransform), typeof(Image));
            objectiveMarkerObject.transform.SetParent(overlayObject.transform, false);
            objectiveMarkerRect = objectiveMarkerObject.GetComponent<RectTransform>();
            objectiveMarkerImage = objectiveMarkerObject.GetComponent<Image>();
        }

        private void ConfigureHud()
        {
            if (panelRect == null || viewportRect == null || overlayRect == null || minimapImage == null)
            {
                return;
            }

            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(0f, 0f);
            panelRect.pivot = new Vector2(0f, 0f);
            panelRect.anchoredPosition = anchoredPosition;
            panelRect.sizeDelta = panelSize;

            Image panelImage = panelRect.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.sprite = GetWhiteSprite();
                panelImage.type = Image.Type.Simple;
                panelImage.color = frameColor;
                panelImage.raycastTarget = false;
                EnsureShadow(panelImage, new Color(0f, 0f, 0f, 0.38f), new Vector2(0f, -10f));
                EnsureOutline(panelImage, new Color(0.35f, 0.56f, 0.84f, 0.18f), new Vector2(1f, -1f));
            }

            EnsureTitleBadge();
            if (titleText != null)
            {
                RectTransform titleRect = titleText.rectTransform;
                titleRect.anchorMin = Vector2.zero;
                titleRect.anchorMax = Vector2.one;
                titleRect.offsetMin = new Vector2(10f, 2f);
                titleRect.offsetMax = new Vector2(-10f, -2f);
                titleText.text = "CITY MAP";
                titleText.fontSize = 14f;
                titleText.fontStyle = FontStyles.Bold;
                titleText.alignment = TextAlignmentOptions.Center;
                titleText.characterSpacing = 3f;
                titleText.color = labelColor;
                titleText.raycastTarget = false;
                EnsureTextShadow(titleText, new Color(0f, 0f, 0f, 0.5f), new Vector2(1f, -1f));
            }

            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(cornerRadiusPadding, cornerRadiusPadding);
            viewportRect.offsetMax = new Vector2(-cornerRadiusPadding, -cornerRadiusPadding);

            Image viewportImage = viewportRect.GetComponent<Image>();
            if (viewportImage != null)
            {
                viewportImage.sprite = GetWhiteSprite();
                viewportImage.type = Image.Type.Simple;
                viewportImage.color = new Color(0.02f, 0.04f, 0.08f, 0.86f);
                viewportImage.raycastTarget = false;
                EnsureOutline(viewportImage, new Color(0.72f, 0.85f, 1f, 0.08f), new Vector2(1f, -1f));
            }

            RectTransform mapRect = minimapImage.rectTransform;
            mapRect.anchorMin = Vector2.zero;
            mapRect.anchorMax = Vector2.one;
            mapRect.offsetMin = Vector2.zero;
            mapRect.offsetMax = Vector2.zero;
            minimapImage.color = viewportTint;
            minimapImage.raycastTarget = false;
            minimapImage.texture = minimapTexture;

            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            ConfigureMarker(playerMarkerRect, playerMarkerColor, new Vector2(14f, 14f));
            ConfigureMarker(objectiveMarkerRect, deliveryMarkerColor, new Vector2(12f, 12f));
        }

        private void EnsureTitleBadge()
        {
            if (panelRect == null)
            {
                return;
            }

            if (titleBadgeRect == null)
            {
                Transform existing = panelRect.Find("LabelBadge");
                if (existing is RectTransform existingRect)
                {
                    titleBadgeRect = existingRect;
                }
                else
                {
                    GameObject badgeObject = new GameObject("LabelBadge", typeof(RectTransform), typeof(Image));
                    badgeObject.transform.SetParent(panelRect, false);
                    titleBadgeRect = badgeObject.GetComponent<RectTransform>();
                }
            }

            if (titleBadgeRect == null)
            {
                return;
            }

            if (titleText != null && titleText.transform.parent != titleBadgeRect)
            {
                titleText.transform.SetParent(titleBadgeRect, false);
            }

            titleBadgeRect.anchorMin = new Vector2(0f, 1f);
            titleBadgeRect.anchorMax = new Vector2(0f, 1f);
            titleBadgeRect.pivot = new Vector2(0f, 1f);
            titleBadgeRect.anchoredPosition = new Vector2(14f, -14f);
            titleBadgeRect.sizeDelta = new Vector2(96f, 30f);

            Image badgeImage = titleBadgeRect.GetComponent<Image>();
            if (badgeImage != null)
            {
                badgeImage.sprite = GetWhiteSprite();
                badgeImage.type = Image.Type.Simple;
                badgeImage.color = badgeColor;
                badgeImage.raycastTarget = false;
                EnsureOutline(badgeImage, new Color(0.74f, 0.86f, 1f, 0.12f), new Vector2(1f, -1f));
            }
        }

        private void ConfigureMarker(RectTransform markerRect, Color color, Vector2 size)
        {
            if (markerRect == null)
            {
                return;
            }

            markerRect.anchorMin = new Vector2(0.5f, 0.5f);
            markerRect.anchorMax = new Vector2(0.5f, 0.5f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.sizeDelta = size;

            Image markerImage = markerRect.GetComponent<Image>();
            if (markerImage != null)
            {
                markerImage.sprite = GetWhiteSprite();
                markerImage.color = color;
                markerImage.raycastTarget = false;
                EnsureOutline(markerImage, new Color(0f, 0f, 0f, 0.35f), new Vector2(1f, -1f));
            }
        }

        private void UpdateCamera()
        {
            if (minimapCamera == null)
            {
                return;
            }

            if (playerTransform == null)
            {
                minimapCamera.enabled = false;
                return;
            }

            Vector3 desiredPosition = playerTransform.position + Vector3.up * Mathf.Max(30f, cameraHeight);
            desiredPosition = ClampCameraPositionToMap(desiredPosition);
            if (!minimapCamera.enabled)
            {
                minimapCamera.transform.position = desiredPosition;
                minimapCamera.enabled = true;
            }
            else
            {
                float lerpFactor = 1f - Mathf.Exp(-Mathf.Max(1f, cameraFollowSpeed) * Time.unscaledDeltaTime);
                minimapCamera.transform.position = Vector3.Lerp(minimapCamera.transform.position, desiredPosition, lerpFactor);
            }

            minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            minimapCamera.orthographicSize = Mathf.Max(20f, orthographicSize);
        }

        private void UpdatePlayerMarker()
        {
            if (playerMarkerRect == null)
            {
                return;
            }

            playerMarkerRect.anchoredPosition = WorldToMapPosition(playerTransform != null ? playerTransform.position : Vector3.zero, clampToViewport: true);
            if (playerTransform != null)
            {
                playerMarkerRect.localRotation = Quaternion.Euler(0f, 0f, -playerTransform.eulerAngles.y);
            }
        }

        private void UpdateObjectiveMarker()
        {
            if (objectiveMarkerRect == null)
            {
                return;
            }

            if (!hasObjective || playerTransform == null)
            {
                objectiveMarkerRect.gameObject.SetActive(false);
                return;
            }

            objectiveMarkerRect.gameObject.SetActive(true);
            objectiveMarkerRect.anchoredPosition = WorldToMapPosition(currentObjective.WorldPosition, clampToViewport: true);

            if (objectiveMarkerImage != null)
            {
                objectiveMarkerImage.color = currentObjective.Type == ObjectiveType.Delivery ? deliveryMarkerColor : pickupMarkerColor;
            }
        }

        private void UpdateRouteOverlay()
        {
            if (!showRoutePreview ||
                overlayRect == null ||
                playerTransform == null ||
                currentRoute == null ||
                !currentRoute.IsRenderable ||
                !currentRoute.IsGraphRoute)
            {
                SetRouteSegmentCount(0);
                UpdateRouteSegmentDiagnostics(0);
                return;
            }

            IReadOnlyList<Vector3> points = currentRoute.Points;
            BuildRoutePolyline(points);

            int segmentCount = Mathf.Max(0, simplifiedRoutePoints.Count - 1);
            SetRouteSegmentCount(segmentCount);
            int visibleSegmentCount = 0;
            Color activeRouteColor = ResolveRouteColor();

            for (int i = 0; i < segmentCount; i++)
            {
                Vector2 from = simplifiedRoutePoints[i];
                Vector2 to = simplifiedRoutePoints[i + 1];
                Vector2 delta = to - from;
                float length = delta.magnitude;

                Image segmentImage = routeSegmentImages[i];
                if (length < 1f)
                {
                    segmentImage.gameObject.SetActive(false);
                    continue;
                }

                segmentImage.gameObject.SetActive(true);
                visibleSegmentCount++;
                RectTransform segmentRect = segmentImage.rectTransform;
                segmentRect.anchorMin = new Vector2(0.5f, 0.5f);
                segmentRect.anchorMax = new Vector2(0.5f, 0.5f);
                segmentRect.pivot = new Vector2(0f, 0.5f);
                segmentRect.anchoredPosition = from;
                segmentRect.sizeDelta = new Vector2(length, Mathf.Max(1f, routeThickness));
                segmentRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                segmentImage.color = activeRouteColor;
            }

            UpdateRouteSegmentDiagnostics(visibleSegmentCount);
        }

        private Color ResolveRouteColor()
        {
            return currentObjective.Type == ObjectiveType.Pickup ? pickupRouteColor : routeColor;
        }

        private void BuildRoutePolyline(IReadOnlyList<Vector3> worldPoints)
        {
            projectedRoutePoints.Clear();
            simplifiedRoutePoints.Clear();

            if (worldPoints == null || worldPoints.Count == 0)
            {
                return;
            }

            float minSegmentLengthSqr = Mathf.Max(0.25f, routeMinSegmentScreenLength * routeMinSegmentScreenLength);
            for (int i = 0; i < worldPoints.Count; i++)
            {
                Vector2 projected = WorldToMapPosition(worldPoints[i], clampToViewport: false);
                if (projectedRoutePoints.Count > 0)
                {
                    Vector2 previous = projectedRoutePoints[projectedRoutePoints.Count - 1];
                    if ((projected - previous).sqrMagnitude <= minSegmentLengthSqr)
                    {
                        projectedRoutePoints[projectedRoutePoints.Count - 1] = projected;
                        continue;
                    }
                }

                projectedRoutePoints.Add(projected);
            }

            if (projectedRoutePoints.Count <= 2)
            {
                simplifiedRoutePoints.AddRange(projectedRoutePoints);
                return;
            }

            RemoveRouteJitter(minSegmentLengthSqr);

            if (projectedRoutePoints.Count <= 2)
            {
                simplifiedRoutePoints.AddRange(projectedRoutePoints);
                return;
            }

            float simplifyTolerance = Mathf.Max(0.5f, routeSimplifyTolerance);
            simplifiedRoutePoints.Add(projectedRoutePoints[0]);

            for (int i = 1; i < projectedRoutePoints.Count - 1; i++)
            {
                Vector2 previousKept = simplifiedRoutePoints[simplifiedRoutePoints.Count - 1];
                Vector2 current = projectedRoutePoints[i];
                Vector2 next = projectedRoutePoints[i + 1];

                float deviation = DistancePointToSegment(current, previousKept, next);
                bool shouldKeep = deviation > simplifyTolerance;

                if (!shouldKeep)
                {
                    continue;
                }

                simplifiedRoutePoints.Add(current);
            }

            Vector2 finalPoint = projectedRoutePoints[projectedRoutePoints.Count - 1];
            if (simplifiedRoutePoints.Count == 0 ||
                (finalPoint - simplifiedRoutePoints[simplifiedRoutePoints.Count - 1]).sqrMagnitude > minSegmentLengthSqr)
            {
                simplifiedRoutePoints.Add(finalPoint);
            }
            else
            {
                simplifiedRoutePoints[simplifiedRoutePoints.Count - 1] = finalPoint;
            }
        }

        private void RemoveRouteJitter(float minSegmentLengthSqr)
        {
            int passCount = Mathf.Clamp(routeVisualSmoothingPasses, 0, 4);
            if (passCount <= 0 || projectedRoutePoints.Count <= 3)
            {
                return;
            }

            float corridor = Mathf.Max(routeSimplifyTolerance * 1.45f, Mathf.Sqrt(minSegmentLengthSqr) * 4f);
            float corridorSqr = corridor * corridor;

            for (int pass = 0; pass < passCount; pass++)
            {
                bool removedAny = false;
                for (int i = 1; i < projectedRoutePoints.Count - 1; i++)
                {
                    Vector2 previous = projectedRoutePoints[i - 1];
                    Vector2 current = projectedRoutePoints[i];
                    Vector2 next = projectedRoutePoints[i + 1];

                    Vector2 incoming = current - previous;
                    Vector2 outgoing = next - current;
                    Vector2 chord = next - previous;

                    if (incoming.sqrMagnitude <= minSegmentLengthSqr ||
                        outgoing.sqrMagnitude <= minSegmentLengthSqr)
                    {
                        projectedRoutePoints.RemoveAt(i--);
                        removedAny = true;
                        continue;
                    }

                    if (chord.sqrMagnitude <= minSegmentLengthSqr)
                    {
                        continue;
                    }

                    float deviation = DistancePointToSegment(current, previous, next);
                    float detour = incoming.magnitude + outgoing.magnitude - chord.magnitude;
                    bool shallowDetour = deviation <= corridor && detour <= corridor * 2.2f;
                    bool tightSawtooth = deviation * deviation <= corridorSqr * 1.8f &&
                                         Mathf.Min(incoming.sqrMagnitude, outgoing.sqrMagnitude) <= corridorSqr * 1.1f;

                    if (shallowDetour || tightSawtooth)
                    {
                        projectedRoutePoints.RemoveAt(i--);
                        removedAny = true;
                    }
                }

                if (!removedAny)
                {
                    break;
                }
            }
        }

        private void SetRouteSegmentCount(int count)
        {
            if (overlayRect == null)
            {
                return;
            }

            while (routeSegmentImages.Count < count)
            {
                GameObject segmentObject = new GameObject($"RouteSegment_{routeSegmentImages.Count}", typeof(RectTransform), typeof(Image));
                segmentObject.transform.SetParent(overlayRect, false);
                Image segmentImage = segmentObject.GetComponent<Image>();
                segmentImage.sprite = GetWhiteSprite();
                segmentImage.type = Image.Type.Simple;
                segmentImage.raycastTarget = false;
                routeSegmentImages.Add(segmentImage);
            }

            for (int i = 0; i < routeSegmentImages.Count; i++)
            {
                routeSegmentImages[i].gameObject.SetActive(i < count);
            }

            if (objectiveMarkerRect != null)
            {
                objectiveMarkerRect.SetAsLastSibling();
            }

            if (playerMarkerRect != null)
            {
                playerMarkerRect.SetAsLastSibling();
            }
        }

        private void UpdateRouteSegmentDiagnostics(int visibleCount)
        {
            activeRouteSegmentCount = Mathf.Max(0, visibleCount);
            if (activeRouteSegmentCount > peakRouteSegmentCount)
            {
                peakRouteSegmentCount = activeRouteSegmentCount;
            }
        }

        private Vector2 WorldToMapPosition(Vector3 worldPosition, bool clampToViewport)
        {
            if (viewportRect == null)
            {
                return Vector2.zero;
            }

            Vector3 mapCenter = minimapCamera != null ? minimapCamera.transform.position : (playerTransform != null ? playerTransform.position : Vector3.zero);
            Vector3 delta = worldPosition - mapCenter;
            float halfExtent = Mathf.Max(20f, orthographicSize);
            float width = viewportRect.rect.width * 0.5f;
            float height = viewportRect.rect.height * 0.5f;

            float x = (delta.x / halfExtent) * width;
            float y = (delta.z / halfExtent) * height;

            if (clampToViewport)
            {
                x = Mathf.Clamp(x, -width + 8f, width - 8f);
                y = Mathf.Clamp(y, -height + 8f, height - 8f);
            }

            return new Vector2(x, y);
        }

        private static float DistancePointToSegment(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
        {
            Vector2 segment = segmentEnd - segmentStart;
            float segmentLengthSqr = segment.sqrMagnitude;
            if (segmentLengthSqr <= Mathf.Epsilon)
            {
                return Vector2.Distance(point, segmentStart);
            }

            float t = Mathf.Clamp01(Vector2.Dot(point - segmentStart, segment) / segmentLengthSqr);
            Vector2 projection = segmentStart + segment * t;
            return Vector2.Distance(point, projection);
        }

        private void TryBindNavigationService()
        {
            NavigationService navigationService = NavigationService.Instance;
            if (navigationService == null)
            {
                nextNavigationBindTime = Time.unscaledTime + 0.5f;
                return;
            }

            if (subscribedNavigationService == navigationService)
            {
                currentRoute = subscribedNavigationService.CurrentRoute;
                currentObjective = subscribedNavigationService.CurrentObjective;
                hasObjective = currentObjective.IsValid;
                return;
            }

            UnbindNavigationService();

            subscribedNavigationService = navigationService;
            subscribedNavigationService.OnObjectiveChanged += HandleObjectiveChanged;
            subscribedNavigationService.OnNavigationCleared += HandleNavigationCleared;
            subscribedNavigationService.OnRouteChanged += HandleRouteChanged;

            currentRoute = subscribedNavigationService.CurrentRoute;
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
            subscribedNavigationService.OnRouteChanged -= HandleRouteChanged;
            subscribedNavigationService = null;
            currentRoute = RouteResult.Unavailable;
        }

        private void HandleObjectiveChanged(NavigationObjective objective)
        {
            currentObjective = objective;
            hasObjective = objective.IsValid;
            objectiveMarkerDirty = true;
            overlayDirty = true;
        }

        private void HandleNavigationCleared()
        {
            currentObjective = NavigationObjective.Empty;
            currentRoute = RouteResult.Unavailable;
            hasObjective = false;
            objectiveMarkerDirty = true;
            overlayDirty = true;
        }

        private void HandleRouteChanged(RouteResult route)
        {
            currentRoute = route ?? RouteResult.Unavailable;
            overlayDirty = true;
        }

        private void ResolvePlayerTransform(bool forceRefresh = false)
        {
            if (!forceRefresh && playerTransform != null)
            {
                return;
            }

            playerTransform = null;

            PlayerVehicleManager vehicleManager = TryGetVehicleManager();
            if (vehicleManager != null && vehicleManager.ActiveVehicleController != null)
            {
                playerTransform = vehicleManager.ActiveVehicleController.transform;
            }

            if (playerTransform == null)
            {
                CarController controller = FindFirstObjectByType<CarController>();
                if (controller != null)
                {
                    playerTransform = controller.transform;
                }
            }

            nextPlayerResolveTime = playerTransform == null ? Time.unscaledTime + 0.5f : 0f;
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
            playerTransform = controller != null ? controller.transform : null;
        }

        private void ResolveMapBounds()
        {
            if (mapBoundsCollider != null)
            {
                return;
            }

            GameObject borderObject = GameObject.Find(BorderObjectName);
            mapBoundsCollider = borderObject != null ? borderObject.GetComponent<Collider>() : null;
        }

        private Vector3 ClampCameraPositionToMap(Vector3 desiredPosition)
        {
            ResolveMapBounds();
            if (mapBoundsCollider == null)
            {
                return desiredPosition;
            }

            Bounds bounds = mapBoundsCollider.bounds;
            float extent = Mathf.Max(20f, orthographicSize) + Mathf.Max(0f, worldEdgePadding);

            float minX = bounds.min.x + extent;
            float maxX = bounds.max.x - extent;
            float minZ = bounds.min.z + extent;
            float maxZ = bounds.max.z - extent;

            Vector3 clamped = desiredPosition;
            clamped.x = minX <= maxX ? Mathf.Clamp(desiredPosition.x, minX, maxX) : bounds.center.x;
            clamped.z = minZ <= maxZ ? Mathf.Clamp(desiredPosition.z, minZ, maxZ) : bounds.center.z;
            return clamped;
        }

        private bool ShouldDisplayMinimap()
        {
            return showMinimap && IsGameScene();
        }

        private static bool IsGameScene()
        {
            return SceneManager.GetActiveScene().name.Equals(GameSceneName, StringComparison.OrdinalIgnoreCase);
        }

        private static Transform ResolveHudParent()
        {
            Transform canvasGroupRoot = GlobalUiCoordinator.CanvasGroupRoot;
            if (canvasGroupRoot != null)
            {
                return canvasGroupRoot;
            }

            Canvas primaryCanvas = GlobalUiCoordinator.PrimaryCanvas;
            return primaryCanvas != null ? primaryCanvas.transform : null;
        }

        private void SetMinimapVisible(bool visible)
        {
            if (panelRect != null && panelRect.gameObject.activeSelf != visible)
            {
                panelRect.gameObject.SetActive(visible);
            }

            if (minimapCamera != null)
            {
                minimapCamera.enabled = visible && playerTransform != null;
            }
        }

        private void DisableCamera()
        {
            if (minimapCamera != null)
            {
                minimapCamera.enabled = false;
            }
        }

        private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!hideNpcVehiclesOnMinimap || minimapCamera == null || camera != minimapCamera)
            {
                return;
            }

            HideNpcRenderersForMinimap();
        }

        private void HandleEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (minimapCamera == null || camera != minimapCamera)
            {
                return;
            }

            RestoreNpcRenderersForMinimap();
        }

        private void HideNpcRenderersForMinimap()
        {
            if (npcRenderersHiddenForMinimap)
            {
                return;
            }

            RefreshNpcRendererCacheIfNeeded();
            temporarilyHiddenNpcRenderers.Clear();

            for (int i = 0; i < cachedNpcRenderers.Count; i++)
            {
                Renderer renderer = cachedNpcRenderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                renderer.enabled = false;
                temporarilyHiddenNpcRenderers.Add(renderer);
            }

            npcRenderersHiddenForMinimap = temporarilyHiddenNpcRenderers.Count > 0;
        }

        private void RestoreNpcRenderersForMinimap()
        {
            if (!npcRenderersHiddenForMinimap)
            {
                return;
            }

            for (int i = 0; i < temporarilyHiddenNpcRenderers.Count; i++)
            {
                Renderer renderer = temporarilyHiddenNpcRenderers[i];
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }

            temporarilyHiddenNpcRenderers.Clear();
            npcRenderersHiddenForMinimap = false;
        }

        private void RefreshNpcRendererCacheIfNeeded()
        {
            if (Time.unscaledTime < nextNpcRendererRefreshTime && cachedNpcRenderers.Count > 0)
            {
                return;
            }

            cachedNpcRenderers.Clear();
            NpcCarAgent[] npcCars = FindObjectsByType<NpcCarAgent>(FindObjectsSortMode.None);
            for (int i = 0; i < npcCars.Length; i++)
            {
                NpcCarAgent npcCar = npcCars[i];
                if (npcCar == null)
                {
                    continue;
                }

                Renderer[] renderers = npcCar.GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    Renderer renderer = renderers[rendererIndex];
                    if (renderer != null)
                    {
                        cachedNpcRenderers.Add(renderer);
                    }
                }
            }

            nextNpcRendererRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, npcRendererRefreshInterval);
        }

        private static Sprite GetWhiteSprite()
        {
            if (whiteSprite != null)
            {
                return whiteSprite;
            }

            Texture2D texture = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            return whiteSprite;
        }

        private static void EnsureShadow(Graphic graphic, Color color, Vector2 offset)
        {
            if (graphic == null)
            {
                return;
            }

            Shadow shadow = graphic.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = graphic.gameObject.AddComponent<Shadow>();
            }

            shadow.effectColor = color;
            shadow.effectDistance = offset;
            shadow.useGraphicAlpha = true;
        }

        private static void EnsureTextShadow(TextMeshProUGUI text, Color color, Vector2 offset)
        {
            if (text == null)
            {
                return;
            }

            Shadow shadow = text.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = text.gameObject.AddComponent<Shadow>();
            }

            shadow.effectColor = color;
            shadow.effectDistance = offset;
            shadow.useGraphicAlpha = true;
        }

        private static void EnsureOutline(Graphic graphic, Color color, Vector2 offset)
        {
            if (graphic == null)
            {
                return;
            }

            Outline outline = graphic.GetComponent<Outline>();
            if (outline == null)
            {
                outline = graphic.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = color;
            outline.effectDistance = offset;
            outline.useGraphicAlpha = true;
        }
    }
}
