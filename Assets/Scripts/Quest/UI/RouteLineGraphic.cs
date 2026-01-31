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
        [SerializeField] private float lineWidth = 2f;

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

            float halfWidth = lineWidth * 0.5f;
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
                AddVertex(vh, v0, color);
                AddVertex(vh, v1, color);
                AddVertex(vh, v2, color);
                AddVertex(vh, v3, color);

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
