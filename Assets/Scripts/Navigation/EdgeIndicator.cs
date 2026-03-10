using UnityEngine;
using UnityEngine.UI;
using DeliveryDriver.UI;

namespace DeliveryDriver.Navigation
{
    public class EdgeIndicator : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool showEdgeIndicator = true;
        [SerializeField] private float indicatorSize = 22f;
        [SerializeField] private float indicatorOffset = 0f;
        [SerializeField] private float pulseSpeed = 4f;
        [SerializeField, Range(0f, 0.6f)] private float pulseAmount = 0.2f;
        [SerializeField] private float followSmoothTime = 0.12f;
        [SerializeField] private Sprite indicatorSprite;
        [SerializeField] private Color pickupColor = new Color(0.1f, 1f, 1f, 1f);
        [SerializeField] private Color deliveryColor = new Color(1f, 0.9f, 0.05f, 1f);
        [SerializeField] private int updateEveryNFrames = 3;

        private Camera cachedMiniMapCamera;
        private Canvas edgeCanvas;
        private RectTransform edgeIndicatorRect;
        private Image edgeIndicatorImage;
        private Vector2 edgeIndicatorVelocity;
        private bool hasEdgeIndicatorPosition;
        private NavigationObjective currentObjective;
        private int frameCounter;

        private void OnEnable()
        {
            if (NavigationService.Instance != null)
            {
                NavigationService.Instance.OnObjectiveChanged += HandleObjectiveChanged;
                NavigationService.Instance.OnNavigationCleared += HandleNavigationCleared;
            }
        }

        private void OnDisable()
        {
            if (NavigationService.Instance != null)
            {
                NavigationService.Instance.OnObjectiveChanged -= HandleObjectiveChanged;
                NavigationService.Instance.OnNavigationCleared -= HandleNavigationCleared;
            }
        }

        private void OnDestroy()
        {
            RemoveEdgeIndicator();
        }

        private void Update()
        {
            if (!showEdgeIndicator || !currentObjective.IsValid)
            {
                HideEdgeIndicator();
                return;
            }

            frameCounter++;
            if (frameCounter % Mathf.Max(1, updateEveryNFrames) != 0)
            {
                return;
            }

            if (!TryGetMiniMapCamera(out Camera miniMapCamera))
            {
                HideEdgeIndicator();
                return;
            }

            Vector3 viewportPoint = miniMapCamera.WorldToViewportPoint(currentObjective.WorldPosition);
            if (viewportPoint.z <= 0f)
            {
                viewportPoint.x = 1f - viewportPoint.x;
                viewportPoint.y = 1f - viewportPoint.y;
                viewportPoint.z = -viewportPoint.z;
            }

            bool isOutsideViewport =
                viewportPoint.x < 0f || viewportPoint.x > 1f ||
                viewportPoint.y < 0f || viewportPoint.y > 1f;

            if (!isOutsideViewport)
            {
                HideEdgeIndicator();
                return;
            }

            Color color = currentObjective.Type == ObjectiveType.Delivery ? deliveryColor : pickupColor;
            UpdateEdgeIndicatorPosition(miniMapCamera, color);
        }

        private void HandleObjectiveChanged(NavigationObjective objective)
        {
            currentObjective = objective;
            hasEdgeIndicatorPosition = false;

            if (!objective.IsValid)
            {
                HideEdgeIndicator();
            }
        }

        private void HandleNavigationCleared()
        {
            currentObjective = NavigationObjective.Empty;
            HideEdgeIndicator();
        }

        private void UpdateEdgeIndicatorPosition(Camera miniMapCamera, Color indicatorColor)
        {
            EnsureEdgeIndicator();
            if (edgeIndicatorRect == null || edgeCanvas == null)
            {
                return;
            }

            Vector3 viewportPoint = miniMapCamera.WorldToViewportPoint(currentObjective.WorldPosition);
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

            float inset = Mathf.Max(0f, indicatorOffset);
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
                    float smoothTime = Mathf.Max(0.01f, followSmoothTime);
                    edgeIndicatorRect.anchoredPosition = Vector2.SmoothDamp(
                        edgeIndicatorRect.anchoredPosition,
                        localPoint,
                        ref edgeIndicatorVelocity,
                        smoothTime);
                }
            }

            float edgePulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * Mathf.Clamp(pulseAmount, 0f, 0.6f);
            float size = Mathf.Max(8f, indicatorSize) * edgePulse;
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
                edgeIndicatorImage.sprite = indicatorSprite != null
                    ? indicatorSprite
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
    }
}
