using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryDriver.Quest.UI
{
    public class MinimapRoadGraphic : MaskableGraphic
    {
        [SerializeField] private float lineWidth = 3.5f;
        [SerializeField] private bool showOutline = true;
        [SerializeField] private float outlineWidthMultiplier = 1.9f;
        [SerializeField] private Color outlineColor = new Color(0.05f, 0.07f, 0.10f, 0.88f);
        [SerializeField] private bool showGlow = true;
        [SerializeField] private float glowWidthMultiplier = 1.8f;
        [SerializeField] private float glowAlpha = 0.18f;
        [SerializeField] private bool smoothCorners = true;
        [SerializeField] private int smoothingIterations = 1;

        private readonly List<List<Vector2>> sourcePolylines = new List<List<Vector2>>();
        private readonly List<List<Vector2>> drawablePolylines = new List<List<Vector2>>();
        private bool loggedPopulateMesh;
        private bool loggedSetPolylines;

        public void SetPolylines(List<List<Vector2>> polylines)
        {
            sourcePolylines.Clear();
            if (polylines != null)
            {
                for (int i = 0; i < polylines.Count; i++)
                {
                    List<Vector2> line = polylines[i];
                    if (line == null || line.Count < 2)
                    {
                        continue;
                    }

                    sourcePolylines.Add(new List<Vector2>(line));
                }
            }

            RebuildDrawablePolylines();

            if (!loggedSetPolylines)
            {
                loggedSetPolylines = true;
                int totalSrcPts = 0;
                for (int i = 0; i < sourcePolylines.Count; i++)
                    totalSrcPts += sourcePolylines[i].Count;
                Debug.Log($"[MinimapRoadGraphic] SetPolylines: source={sourcePolylines.Count}, " +
                          $"totalSrcPts={totalSrcPts}, drawable={drawablePolylines.Count}, " +
                          $"enabled={enabled}, gameObject.active={gameObject.activeInHierarchy}, " +
                          $"canvas={canvas != null}, canvasRenderer={canvasRenderer != null}");
            }

            SetVerticesDirty();
        }

        public void Clear()
        {
            sourcePolylines.Clear();
            drawablePolylines.Clear();
            SetVerticesDirty();
        }

        public void SetLineWidth(float width)
        {
            lineWidth = Mathf.Max(0.5f, width);
            SetVerticesDirty();
        }

        public void SetOutlineColor(Color color)
        {
            outlineColor = color;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if (!loggedPopulateMesh && drawablePolylines.Count > 0)
            {
                loggedPopulateMesh = true;
                int totalPts = 0;
                for (int i = 0; i < drawablePolylines.Count; i++)
                    totalPts += drawablePolylines[i].Count;
                Vector2 samplePos = drawablePolylines[0].Count > 0 ? drawablePolylines[0][0] : Vector2.zero;
                Debug.Log($"[MinimapRoadGraphic] OnPopulateMesh: drawablePolylines={drawablePolylines.Count}, " +
                          $"totalPoints={totalPts}, lineWidth={lineWidth:F1}, " +
                          $"color={color}, rectSize={rectTransform.rect.width:F0}x{rectTransform.rect.height:F0}, " +
                          $"sampleVertex={samplePos}, maskable={maskable}, " +
                          $"canvasRenderer.cull={canvasRenderer.cull}");
            }

            if (drawablePolylines.Count == 0)
            {
                return;
            }

            if (showOutline)
            {
                float outlineHalfWidth = lineWidth * outlineWidthMultiplier * 0.5f;
                DrawPolylines(vh, outlineHalfWidth, outlineColor);
            }

            if (showGlow)
            {
                float glowHalfWidth = lineWidth * glowWidthMultiplier * 0.5f;
                Color glowColor = new Color(color.r, color.g, color.b, color.a * glowAlpha);
                DrawPolylines(vh, glowHalfWidth, glowColor);
            }

            DrawPolylines(vh, lineWidth * 0.5f, color);
        }

        private void DrawPolylines(VertexHelper vh, float halfWidth, Color lineColor)
        {
            for (int lineIndex = 0; lineIndex < drawablePolylines.Count; lineIndex++)
            {
                List<Vector2> line = drawablePolylines[lineIndex];
                for (int pointIndex = 0; pointIndex < line.Count - 1; pointIndex++)
                {
                    Vector2 start = line[pointIndex];
                    Vector2 end = line[pointIndex + 1];
                    Vector2 direction = (end - start).normalized;
                    if (direction.sqrMagnitude <= Mathf.Epsilon)
                    {
                        continue;
                    }

                    Vector2 normal = new Vector2(-direction.y, direction.x) * halfWidth;
                    int baseIndex = vh.currentVertCount;
                    AddVertex(vh, start - normal, lineColor);
                    AddVertex(vh, start + normal, lineColor);
                    AddVertex(vh, end + normal, lineColor);
                    AddVertex(vh, end - normal, lineColor);
                    vh.AddTriangle(baseIndex + 0, baseIndex + 1, baseIndex + 2);
                    vh.AddTriangle(baseIndex + 2, baseIndex + 3, baseIndex + 0);
                }
            }
        }

        private static void AddVertex(VertexHelper vh, Vector2 position, Color vertexColor)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = vertexColor;
            vh.AddVert(vertex);
        }

        private void RebuildDrawablePolylines()
        {
            drawablePolylines.Clear();
            for (int i = 0; i < sourcePolylines.Count; i++)
            {
                List<Vector2> source = sourcePolylines[i];
                if (source == null || source.Count < 2)
                {
                    continue;
                }

                List<Vector2> line = new List<Vector2>(source);
                if (smoothCorners && line.Count >= 3)
                {
                    int iterations = Mathf.Clamp(smoothingIterations, 0, 3);
                    for (int iteration = 0; iteration < iterations; iteration++)
                    {
                        line = ApplyChaikinSmoothing(line);
                        if (line.Count < 3)
                        {
                            break;
                        }
                    }
                }

                drawablePolylines.Add(line);
            }
        }

        private static List<Vector2> ApplyChaikinSmoothing(List<Vector2> source)
        {
            if (source == null || source.Count < 3)
            {
                return source;
            }

            List<Vector2> smoothed = new List<Vector2>(source.Count * 2);
            smoothed.Add(source[0]);
            for (int i = 0; i < source.Count - 1; i++)
            {
                Vector2 p0 = source[i];
                Vector2 p1 = source[i + 1];
                smoothed.Add(Vector2.Lerp(p0, p1, 0.25f));
                smoothed.Add(Vector2.Lerp(p0, p1, 0.75f));
            }

            smoothed.Add(source[source.Count - 1]);
            return smoothed;
        }
    }
}
