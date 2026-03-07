using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryDriver.Quest.UI
{
    /// <summary>
    /// Simple UI polyline renderer for route previews.
    /// </summary>
    public class RouteLineGraphic : Graphic
    {
        [SerializeField] private float lineWidth = 3.5f;
        [SerializeField] private bool showGlow = true;
        [SerializeField] private float glowWidthMultiplier = 2.5f;
        [SerializeField] private float glowAlpha = 0.25f;

        private readonly List<Vector2> points = new List<Vector2>();

        public void SetPoints(IReadOnlyList<Vector2> newPoints)
        {
            points.Clear();
            if (newPoints != null)
            {
                points.AddRange(newPoints);
            }
            SetVerticesDirty();
        }

        public void Clear()
        {
            points.Clear();
            SetVerticesDirty();
        }

        public void SetLineWidth(float width)
        {
            lineWidth = Mathf.Max(0.5f, width);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if (points.Count < 2)
            {
                return;
            }

            // Draw glow layer first (wider, semi-transparent)
            if (showGlow)
            {
                float glowHalfWidth = lineWidth * glowWidthMultiplier * 0.5f;
                Color glowColor = new Color(color.r, color.g, color.b, color.a * glowAlpha);
                DrawLineSegments(vh, glowHalfWidth, glowColor);
            }

            // Draw main line on top
            float halfWidth = lineWidth * 0.5f;
            DrawLineSegments(vh, halfWidth, color);
        }

        private void DrawLineSegments(VertexHelper vh, float halfWidth, Color lineColor)
        {
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 start = points[i];
                Vector2 end = points[i + 1];
                Vector2 direction = (end - start).normalized;
                if (direction.sqrMagnitude <= Mathf.Epsilon)
                {
                    continue;
                }

                Vector2 normal = new Vector2(-direction.y, direction.x) * halfWidth;
                Vector2 v0 = start - normal;
                Vector2 v1 = start + normal;
                Vector2 v2 = end + normal;
                Vector2 v3 = end - normal;

                int index = vh.currentVertCount;
                AddVertex(vh, v0, lineColor);
                AddVertex(vh, v1, lineColor);
                AddVertex(vh, v2, lineColor);
                AddVertex(vh, v3, lineColor);

                vh.AddTriangle(index + 0, index + 1, index + 2);
                vh.AddTriangle(index + 2, index + 3, index + 0);
            }
        }

        private void AddVertex(VertexHelper vh, Vector2 position, Color vertexColor)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = vertexColor;
            vertex.position = position;
            vh.AddVert(vertex);
        }
    }
}
