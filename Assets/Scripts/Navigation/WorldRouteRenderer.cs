using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDriver.Navigation
{
    public class WorldRouteRenderer : MonoBehaviour
    {
        [SerializeField] private bool enableLegacyWorldRoute = false;

        [Header("Route Line Settings")]
        [SerializeField] private Color routeLineColor = new Color(0.2f, 0.85f, 1f, 0.95f);
        [SerializeField] private float routeLineWidth = 2.4f;
        [SerializeField] private float routeHeightOffset = 18f;
        [SerializeField] private string routeLineLayerName = "NavigationMarker";
        [SerializeField] private int routeCornerVertices = 6;
        [SerializeField] private int routeCapVertices = 8;

        private GameObject routeLineObject;
        private LineRenderer routeLineRenderer;
        private Material routeLineMaterial;

        private void OnEnable()
        {
            if (!enableLegacyWorldRoute)
            {
                ClearRouteLine();
                return;
            }

            if (NavigationService.Instance != null)
            {
                NavigationService.Instance.OnRouteChanged += HandleRouteChanged;
                NavigationService.Instance.OnNavigationCleared += HandleNavigationCleared;
            }
        }

        private void OnDisable()
        {
            if (NavigationService.Instance != null)
            {
                NavigationService.Instance.OnRouteChanged -= HandleRouteChanged;
                NavigationService.Instance.OnNavigationCleared -= HandleNavigationCleared;
            }
        }

        private void OnDestroy()
        {
            ClearRouteLine();
        }

        private void HandleRouteChanged(RouteResult route)
        {
            if (!enableLegacyWorldRoute || route == null || !route.IsRenderable)
            {
                ClearRouteLine();
                return;
            }

            EnsureRouteLine();
            if (routeLineRenderer == null)
            {
                return;
            }

            IReadOnlyList<Vector3> points = route.Points;
            routeLineRenderer.startWidth = routeLineWidth;
            routeLineRenderer.endWidth = routeLineWidth;
            routeLineRenderer.positionCount = points.Count;

            float routeY = Mathf.Max(routeHeightOffset, 23f);
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 p = points[i];
                p.y = routeY;
                routeLineRenderer.SetPosition(i, p);
            }

            if (routeLineMaterial != null)
            {
                routeLineMaterial.color = routeLineColor;
            }
        }

        private void HandleNavigationCleared()
        {
            ClearRouteLine();
        }

        private void EnsureRouteLine()
        {
            if (routeLineRenderer != null)
            {
                return;
            }

            routeLineObject = new GameObject("NavigationRouteLine");
            int routeLayer = LayerMask.NameToLayer(routeLineLayerName);
            if (routeLayer >= 0)
            {
                routeLineObject.layer = routeLayer;
            }

            routeLineRenderer = routeLineObject.AddComponent<LineRenderer>();
            routeLineRenderer.loop = false;
            routeLineRenderer.useWorldSpace = true;
            routeLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            routeLineRenderer.receiveShadows = false;
            routeLineRenderer.alignment = LineAlignment.View;
            routeLineRenderer.numCapVertices = Mathf.Max(0, routeCapVertices);
            routeLineRenderer.numCornerVertices = Mathf.Max(0, routeCornerVertices);
            routeLineRenderer.textureMode = LineTextureMode.Stretch;
            routeLineRenderer.sortingOrder = 120;
            routeLineRenderer.startColor = routeLineColor;
            routeLineRenderer.endColor = routeLineColor;

            routeLineMaterial = RuntimeColorMaterialHelper.CreateColorMaterial(routeLineColor, null);
            if (routeLineMaterial != null)
            {
                routeLineRenderer.material = routeLineMaterial;
            }
        }

        private void ClearRouteLine()
        {
            if (routeLineObject != null)
            {
                Destroy(routeLineObject);
                routeLineObject = null;
                routeLineRenderer = null;
            }

            if (routeLineMaterial != null)
            {
                Destroy(routeLineMaterial);
                routeLineMaterial = null;
            }
        }
    }
}
