using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Standalone component that manages the minimap objective marker and edge indicator.
/// Extracted from DeliveryManager to reduce file size and isolate UI concerns.
/// </summary>
public class MiniMapObjectiveMarker : MonoBehaviour
{
    [Header("MiniMap Objective Marker")]
    [SerializeField] private bool enableMiniMapObjectiveMarker = true;
    [SerializeField] private float miniMapMarkerHeight = 24f;
    [SerializeField] private Vector3 miniMapMarkerScale = new Vector3(4f, 10f, 4f);
    [SerializeField] private float miniMapMarkerSpinSpeed = 120f;
    [SerializeField] private float miniMapMarkerPulseSpeed = 3f;
    [SerializeField] private float miniMapMarkerPulseAmount = 0.2f;
    [SerializeField] private Color miniMapPickupMarkerColor = new Color(0.1f, 1f, 1f, 1f);
    [SerializeField] private Color miniMapDeliveryMarkerColor = new Color(1f, 0.9f, 0.05f, 1f);
    [SerializeField] private string miniMapMarkerLayerName = "MiniMapMarker";
    [SerializeField] private bool clampMiniMapMarkerToEdgeWhenOffscreen = true;
    [SerializeField, Range(0f, 0.45f)] private float miniMapMarkerEdgePadding = 0.08f;
    [SerializeField] private bool showMiniMapEdgeIndicator = true;
    [SerializeField] private float miniMapEdgeIndicatorSize = 22f;
    [SerializeField] private float miniMapEdgeIndicatorOffset = 0f;
    [SerializeField] private float miniMapEdgeIndicatorPulseSpeed = 4f;
    [SerializeField, Range(0f, 0.6f)] private float miniMapEdgeIndicatorPulseAmount = 0.2f;
    [SerializeField] private float miniMapMarkerFollowSmoothTime = 0.08f;
    [SerializeField] private float miniMapEdgeFollowSmoothTime = 0.12f;
    [SerializeField] private Sprite miniMapEdgeIndicatorSprite;
    [SerializeField] private int updateEveryNFrames = 3;

    private GameObject markerObject;
    private Material markerMaterial;
    private int cachedMarkerLayer = int.MinValue;
    private Camera cachedMiniMapCamera;
    private Canvas edgeCanvas;
    private RectTransform edgeIndicatorRect;
    private Image edgeIndicatorImage;
    private Vector3 markerVelocity;
    private Vector2 edgeIndicatorVelocity;
    private bool hasMarkerPosition;
    private bool hasEdgeIndicatorPosition;

    private enum TargetMode { None, Pickup, Delivery }
    private TargetMode currentMode = TargetMode.None;
    private Vector3 currentTargetPoint;
    private int frameCounter;

    public void SetPickupTarget(Vector3 point)
    {
        currentMode = TargetMode.Pickup;
        currentTargetPoint = point;
        UpdateMarker();
    }

    public void SetDeliveryTarget(Vector3 point)
    {
        currentMode = TargetMode.Delivery;
        currentTargetPoint = point;
        UpdateMarker();
    }

    public void ClearTarget()
    {
        currentMode = TargetMode.None;
        RemoveMarker();
    }

    private void Update()
    {
        int safeFrameInterval = Mathf.Max(1, updateEveryNFrames);
        frameCounter++;
        if (frameCounter % safeFrameInterval == 0)
        {
            UpdateMarker();
        }
    }

    private void OnDestroy()
    {
        RemoveMarker();
        RemoveEdgeIndicator();
    }

    private void UpdateMarker()
    {
        if (!enableMiniMapObjectiveMarker)
        {
            RemoveMarker();
            return;
        }

        if (currentMode == TargetMode.None)
        {
            HideEdgeIndicator();
            if (markerObject != null)
            {
                markerObject.SetActive(false);
            }
            hasMarkerPosition = false;
            return;
        }

        EnsureMarker();
        if (markerObject == null)
        {
            return;
        }

        bool targetIsOffscreen = false;
        Vector3 markerPoint = GetMarkerTargetPoint(currentTargetPoint, out targetIsOffscreen);
        Color markerColor = currentMode == TargetMode.Delivery ? miniMapDeliveryMarkerColor : miniMapPickupMarkerColor;

        if (targetIsOffscreen)
        {
            markerObject.SetActive(false);
            hasMarkerPosition = false;
            if (showMiniMapEdgeIndicator)
            {
                UpdateEdgeIndicator(currentTargetPoint, markerColor);
            }
            else
            {
                HideEdgeIndicator();
            }
        }
        else
        {
            markerObject.SetActive(true);
            Vector3 desiredPosition = markerPoint + Vector3.up * miniMapMarkerHeight;
            if (!hasMarkerPosition)
            {
                markerObject.transform.position = desiredPosition;
                markerVelocity = Vector3.zero;
                hasMarkerPosition = true;
            }
            else
            {
                float smoothTime = Mathf.Max(0.01f, miniMapMarkerFollowSmoothTime);
                markerObject.transform.position = Vector3.SmoothDamp(
                    markerObject.transform.position,
                    desiredPosition,
                    ref markerVelocity,
                    smoothTime);
            }

            float pulse = 1f + Mathf.Sin(Time.time * miniMapMarkerPulseSpeed) * miniMapMarkerPulseAmount;
            markerObject.transform.localScale = miniMapMarkerScale * pulse;
            markerObject.transform.Rotate(Vector3.up, miniMapMarkerSpinSpeed * Time.deltaTime, Space.World);
            HideEdgeIndicator();
        }

        if (markerMaterial != null)
        {
            markerMaterial.color = markerColor;
        }
    }

    private void EnsureMarker()
    {
        if (markerObject != null)
        {
            return;
        }

        markerObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        markerObject.name = "MiniMapObjectiveMarker";
        markerObject.transform.localScale = miniMapMarkerScale;
        int layer = ResolveMarkerLayer();
        if (layer >= 0)
        {
            markerObject.layer = layer;
        }

        Collider markerCollider = markerObject.GetComponent<Collider>();
        if (markerCollider != null)
        {
            Destroy(markerCollider);
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader != null)
        {
            markerMaterial = new Material(shader);
            markerObject.GetComponent<MeshRenderer>().material = markerMaterial;
        }
    }

    private int ResolveMarkerLayer()
    {
        if (cachedMarkerLayer == int.MinValue)
        {
            cachedMarkerLayer = LayerMask.NameToLayer(miniMapMarkerLayerName);
        }
        return cachedMarkerLayer;
    }

    private Vector3 GetMarkerTargetPoint(Vector3 worldTargetPoint, out bool targetIsOffscreen)
    {
        targetIsOffscreen = false;

        if (!clampMiniMapMarkerToEdgeWhenOffscreen)
        {
            return worldTargetPoint;
        }

        if (!TryGetMiniMapCamera(out Camera miniMapCamera))
        {
            return worldTargetPoint;
        }

        Vector3 viewportPoint = miniMapCamera.WorldToViewportPoint(worldTargetPoint);
        if (viewportPoint.z <= 0f)
        {
            return worldTargetPoint;
        }

        bool isOutsideViewport =
            viewportPoint.x < 0f || viewportPoint.x > 1f ||
            viewportPoint.y < 0f || viewportPoint.y > 1f;
        targetIsOffscreen = isOutsideViewport;

        if (!isOutsideViewport)
        {
            return worldTargetPoint;
        }

        float padding = Mathf.Clamp(miniMapMarkerEdgePadding, 0f, 0.45f);
        viewportPoint.x = Mathf.Clamp(viewportPoint.x, padding, 1f - padding);
        viewportPoint.y = Mathf.Clamp(viewportPoint.y, padding, 1f - padding);

        Vector3 edgePoint = miniMapCamera.ViewportToWorldPoint(viewportPoint);
        edgePoint.y = worldTargetPoint.y;
        return edgePoint;
    }

    private bool TryGetMiniMapCamera(out Camera miniMapCamera)
    {
        if (cachedMiniMapCamera == null)
        {
            GameObject miniMapCameraObject = GameObject.Find("MiniMapCamera");
            if (miniMapCameraObject != null)
            {
                cachedMiniMapCamera = miniMapCameraObject.GetComponent<Camera>();
            }
        }

        miniMapCamera = cachedMiniMapCamera;
        return miniMapCamera != null && miniMapCamera.gameObject.activeInHierarchy && miniMapCamera.enabled;
    }

    private void UpdateEdgeIndicator(Vector3 worldTargetPoint, Color indicatorColor)
    {
        if (!TryGetMiniMapCamera(out Camera miniMapCamera))
        {
            HideEdgeIndicator();
            return;
        }

        EnsureEdgeIndicator();
        if (edgeIndicatorRect == null || edgeCanvas == null)
        {
            return;
        }

        Vector3 viewportPoint = miniMapCamera.WorldToViewportPoint(worldTargetPoint);
        if (viewportPoint.z < 0f)
        {
            viewportPoint.x = 1f - viewportPoint.x;
            viewportPoint.y = 1f - viewportPoint.y;
            viewportPoint.z = -viewportPoint.z;
        }

        Vector2 direction = new Vector2(viewportPoint.x - 0.5f, viewportPoint.y - 0.5f);
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector2.up;
        }
        direction.Normalize();

        Rect miniMapRect = miniMapCamera.rect;
        float rectCenterX = (miniMapRect.x + (miniMapRect.width * 0.5f)) * Screen.width;
        float rectCenterY = (miniMapRect.y + (miniMapRect.height * 0.5f)) * Screen.height;
        float rectHalfWidth = miniMapRect.width * Screen.width * 0.5f;
        float rectHalfHeight = miniMapRect.height * Screen.height * 0.5f;

        float inset = Mathf.Max(0f, miniMapEdgeIndicatorOffset);
        float usableHalfWidth = Mathf.Max(1f, rectHalfWidth - inset);
        float usableHalfHeight = Mathf.Max(1f, rectHalfHeight - inset);
        float tx = Mathf.Approximately(direction.x, 0f) ? float.PositiveInfinity : usableHalfWidth / Mathf.Abs(direction.x);
        float ty = Mathf.Approximately(direction.y, 0f) ? float.PositiveInfinity : usableHalfHeight / Mathf.Abs(direction.y);
        float t = Mathf.Min(tx, ty);
        Vector2 screenPoint = new Vector2(rectCenterX, rectCenterY) + direction * t;

        RectTransform canvasRect = edgeCanvas.transform as RectTransform;
        if (canvasRect != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out Vector2 localPoint))
        {
            if (!hasEdgeIndicatorPosition)
            {
                edgeIndicatorRect.anchoredPosition = localPoint;
                edgeIndicatorVelocity = Vector2.zero;
                hasEdgeIndicatorPosition = true;
            }
            else
            {
                float smoothTime = Mathf.Max(0.01f, miniMapEdgeFollowSmoothTime);
                edgeIndicatorRect.anchoredPosition = Vector2.SmoothDamp(
                    edgeIndicatorRect.anchoredPosition,
                    localPoint,
                    ref edgeIndicatorVelocity,
                    smoothTime);
            }
        }

        float edgePulse = 1f + Mathf.Sin(Time.time * miniMapEdgeIndicatorPulseSpeed) * Mathf.Clamp(miniMapEdgeIndicatorPulseAmount, 0f, 0.6f);
        float size = Mathf.Max(8f, miniMapEdgeIndicatorSize) * edgePulse;
        edgeIndicatorRect.sizeDelta = new Vector2(size, size);
        edgeIndicatorRect.localRotation = Quaternion.identity;

        if (edgeIndicatorImage != null)
        {
            edgeIndicatorImage.color = indicatorColor;
            edgeIndicatorImage.enabled = true;
        }
    }

    private void EnsureEdgeIndicator()
    {
        if (edgeCanvas == null)
        {
            GameObject canvasObject = new GameObject("MiniMapEdgeIndicatorCanvas");
            edgeCanvas = canvasObject.AddComponent<Canvas>();
            edgeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            edgeCanvas.sortingOrder = 1000;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        if (edgeIndicatorRect == null)
        {
            GameObject indicatorObject = new GameObject("MiniMapEdgeIndicator");
            indicatorObject.transform.SetParent(edgeCanvas.transform, false);
            edgeIndicatorRect = indicatorObject.AddComponent<RectTransform>();
            edgeIndicatorRect.anchorMin = new Vector2(0.5f, 0.5f);
            edgeIndicatorRect.anchorMax = new Vector2(0.5f, 0.5f);
            edgeIndicatorRect.pivot = new Vector2(0.5f, 0.5f);

            edgeIndicatorImage = indicatorObject.AddComponent<Image>();
            edgeIndicatorImage.raycastTarget = false;
            edgeIndicatorImage.sprite = miniMapEdgeIndicatorSprite != null
                ? miniMapEdgeIndicatorSprite
                : DeliveryUiSpriteHelper.GetFallbackSprite();
            edgeIndicatorImage.preserveAspect = true;
        }
    }

    private void HideEdgeIndicator()
    {
        if (edgeIndicatorImage != null)
        {
            edgeIndicatorImage.enabled = false;
        }
        hasEdgeIndicatorPosition = false;
    }

    private void RemoveEdgeIndicator()
    {
        if (edgeIndicatorRect != null)
        {
            Destroy(edgeIndicatorRect.gameObject);
            edgeIndicatorRect = null;
            edgeIndicatorImage = null;
        }

        if (edgeCanvas != null)
        {
            Destroy(edgeCanvas.gameObject);
            edgeCanvas = null;
        }
    }

    private void RemoveMarker()
    {
        HideEdgeIndicator();

        if (markerObject != null)
        {
            Destroy(markerObject);
            markerObject = null;
            hasMarkerPosition = false;
        }

        if (markerMaterial != null)
        {
            Destroy(markerMaterial);
            markerMaterial = null;
        }
    }
}
