using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryDriver.Quest.UI
{
    /// <summary>
    /// Lightweight bar chart for statistics panels.
    /// </summary>
    public class SimpleBarChart : MonoBehaviour
    {
        [Serializable]
        public struct ChartPoint
        {
            public string Label;
            public float Value;

            public ChartPoint(string label, float value)
            {
                Label = label;
                Value = value;
            }
        }

        [Header("References")]
        [SerializeField] private RectTransform container;
        [SerializeField] private Image barPrefab;
        [SerializeField] private TextMeshProUGUI labelPrefab;

        [Header("Layout")]
        [SerializeField] private float maxBarHeight = 120f;
        [SerializeField] private float barWidth = 22f;
        [SerializeField] private float barSpacing = 10f;
        [SerializeField] private float labelOffset = 6f;

        [Header("Style")]
        [SerializeField] private Color barColor = new Color(0.2f, 0.8f, 1f, 0.9f);
        [SerializeField] private bool showValues = false;

        private readonly List<GameObject> spawned = new List<GameObject>();

        public void Render(IReadOnlyList<ChartPoint> points)
        {
            if (container == null || barPrefab == null)
            {
                return;
            }

            Clear();

            if (points == null || points.Count == 0)
            {
                return;
            }

            float maxValue = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                if (points[i].Value > maxValue)
                {
                    maxValue = points[i].Value;
                }
            }

            if (maxValue <= 0f)
            {
                maxValue = 1f;
            }

            for (int i = 0; i < points.Count; i++)
            {
                float height = Mathf.Clamp(points[i].Value / maxValue, 0f, 1f) * maxBarHeight;
                float x = (barWidth + barSpacing) * i;

                Image bar = Instantiate(barPrefab, container);
                bar.color = barColor;
                RectTransform barRect = bar.rectTransform;
                barRect.anchorMin = Vector2.zero;
                barRect.anchorMax = Vector2.zero;
                barRect.pivot = new Vector2(0.5f, 0f);
                barRect.anchoredPosition = new Vector2(x, 0f);
                barRect.sizeDelta = new Vector2(barWidth, height);
                spawned.Add(bar.gameObject);

                if (labelPrefab != null)
                {
                    TextMeshProUGUI label = Instantiate(labelPrefab, container);
                    RectTransform labelRect = label.rectTransform;
                    labelRect.anchorMin = Vector2.zero;
                    labelRect.anchorMax = Vector2.zero;
                    labelRect.pivot = new Vector2(0.5f, 1f);
                    labelRect.anchoredPosition = new Vector2(x, height + labelOffset);
                    label.text = showValues
                        ? $"{points[i].Label}\n{points[i].Value:0}"
                        : points[i].Label;
                    spawned.Add(label.gameObject);
                }
            }

            float totalWidth = (barWidth + barSpacing) * Mathf.Max(points.Count - 1, 0);
            container.sizeDelta = new Vector2(totalWidth, Mathf.Max(container.sizeDelta.y, maxBarHeight + labelOffset * 2f));
        }

        public void Clear()
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null)
                {
                    Destroy(spawned[i]);
                }
            }
            spawned.Clear();
        }
    }
}
