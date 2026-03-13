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
        [SerializeField] private float lineWidth = 4f;
        [SerializeField] private bool showOutline = true;
        [SerializeField] private float outlineWidthMultiplier = 1.7f;
        [SerializeField] private float outlineAlpha = 0.92f;
        [SerializeField] private bool showGlow = true;
        [SerializeField] private float glowWidthMultiplier = 2.1f;
        [SerializeField] private float glowAlpha = 0.18f;
        [SerializeField] private bool smoothCorners = true;
        [SerializeField] private int smoothingIterations = 2;

        private readonly List<Vector2> points = new List<Vector2>();
        private readonly List<Vector2> drawablePoints = new List<Vector2>();

        public void SetPoints(IReadOnlyList<Vector2> newPoints)
        {
            points.Clear();
            if (newPoints != null)
            {
                points.AddRange(newPoints);
            }

            RebuildDrawablePoints();
            SetVerticesDirty();
        }

        public void Clear()
        {
            points.Clear();
            drawablePoints.Clear();
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

            if (drawablePoints.Count < 2)
            {
                return;
            }

            if (showOutline)
            {
                float outlineHalfWidth = lineWidth * outlineWidthMultiplier * 0.5f;
                Color outlineColor = new Color(0.06f, 0.08f, 0.12f, color.a * outlineAlpha);
                DrawLineSegments(vh, outlineHalfWidth, outlineColor);
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
            for (int i = 0; i < drawablePoints.Count - 1; i++)
            {
                Vector2 start = drawablePoints[i];
                Vector2 end = drawablePoints[i + 1];
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

        private void RebuildDrawablePoints()
        {
            drawablePoints.Clear();
            if (points.Count == 0)
            {
                return;
            }

            drawablePoints.AddRange(points);
            if (!smoothCorners || drawablePoints.Count < 3)
            {
                return;
            }

            int iterations = Mathf.Clamp(smoothingIterations, 0, 3);
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                ApplyChaikinSmoothing();
                if (drawablePoints.Count < 3)
                {
                    break;
                }
            }
        }

        private void ApplyChaikinSmoothing()
        {
            if (drawablePoints.Count < 3)
            {
                return;
            }

            List<Vector2> source = new List<Vector2>(drawablePoints);
            drawablePoints.Clear();
            drawablePoints.Add(source[0]);

            for (int i = 0; i < source.Count - 1; i++)
            {
                Vector2 p0 = source[i];
                Vector2 p1 = source[i + 1];
                Vector2 q = Vector2.Lerp(p0, p1, 0.25f);
                Vector2 r = Vector2.Lerp(p0, p1, 0.75f);

                drawablePoints.Add(q);
                drawablePoints.Add(r);
            }

            drawablePoints.Add(source[source.Count - 1]);
        }
    }
}
