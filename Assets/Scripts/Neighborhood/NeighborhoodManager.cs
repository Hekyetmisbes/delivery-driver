using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using DeliveryDriver.UI;

namespace DeliveryDriver.City
{
    /// <summary>
    /// Manages neighborhoods in the city and handles player entering/exiting events.
    /// </summary>
    public class NeighborhoodManager : MonoBehaviour
    {
        private static NeighborhoodManager instance;
        public static NeighborhoodManager Instance => instance;
        public static NeighborhoodManager EnsureInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindFirstObjectByType<NeighborhoodManager>();
            if (instance != null)
            {
                return instance;
            }

            GameObject managerObject = new GameObject("NeighborhoodManager");
            instance = managerObject.AddComponent<NeighborhoodManager>();
            return instance;
        }

        [Header("Runtime")]
        [SerializeField] private NeighborhoodZone currentNeighborhood;
        [SerializeField] private List<Neighborhood> neighborhoods = new List<Neighborhood>();

        private NeighborhoodUI neighborhoodUI;
        private bool initialized = false;

        public NeighborhoodZone CurrentNeighborhood => currentNeighborhood;
        public List<Neighborhood> Neighborhoods => neighborhoods;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            EnsureInitialized();
        }

        private void Start()
        {
            EnsureInitialized();
        }

        public void OnPlayerEnteredNeighborhood(NeighborhoodZone zone)
        {
            EnsureInitialized();

            if (zone == null || currentNeighborhood == zone)
            {
                return;
            }

            currentNeighborhood = zone;

            if (neighborhoodUI != null && !string.IsNullOrWhiteSpace(zone.NeighborhoodName))
            {
                neighborhoodUI.ShowNeighborhoodName(zone.NeighborhoodName);
            }

            Debug.Log($"[NeighborhoodManager] Player entered: {zone.NeighborhoodName}");
        }

        public void OnPlayerExitedNeighborhood(NeighborhoodZone zone)
        {
            if (zone == null)
            {
                return;
            }

            if (currentNeighborhood == zone)
            {
                currentNeighborhood = null;
            }

            Debug.Log($"[NeighborhoodManager] Player exited: {zone.NeighborhoodName}");
        }

        public void RegisterNeighborhood(Neighborhood neighborhood)
        {
            if (!neighborhoods.Contains(neighborhood))
            {
                neighborhoods.Add(neighborhood);
            }
        }

        public void ClearNeighborhoods()
        {
            neighborhoods.Clear();
            currentNeighborhood = null;
        }

        public Neighborhood GetNeighborhoodByName(string name)
        {
            return neighborhoods.Find(n => n.NeighborhoodName == name);
        }

        public Neighborhood GetNeighborhoodAtGridCell(Vector2Int cell)
        {
            return neighborhoods.Find(n => n.ContainsGridCell(cell));
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            neighborhoodUI = FindFirstObjectByType<NeighborhoodUI>();
            if (neighborhoodUI == null || !neighborhoodUI.HasValidReferences)
            {
                CreateDefaultUI();
            }

            initialized = true;
        }

        private void CreateDefaultUI()
        {
            Canvas canvas = GameObject.Find("NeighborhoodCanvas")?.GetComponent<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObject = new GameObject("NeighborhoodCanvas");
                canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 5000;
                canvasObject.AddComponent<CanvasScaler>();
                canvasObject.AddComponent<GraphicRaycaster>();
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 5000;
            }

            GameObject panelObject = new GameObject("NeighborhoodPanel");
            panelObject.transform.SetParent(canvas.transform, false);

            Image panelImage = panelObject.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.8f);

            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -50f);
            panelRect.sizeDelta = new Vector2(500f, 100f);

            GameObject textObject = new GameObject("NeighborhoodText");
            textObject.transform.SetParent(panelObject.transform, false);

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = LocalizationTable.Get("neighborhood_title");
            text.fontSize = 36f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            neighborhoodUI = FindFirstObjectByType<NeighborhoodUI>();
            if (neighborhoodUI == null)
            {
                neighborhoodUI = gameObject.AddComponent<NeighborhoodUI>();
            }

            neighborhoodUI.ConfigureReferences(panelObject, text);
            Debug.LogWarning("[NeighborhoodManager] NeighborhoodUI missing. Created runtime fallback UI.");
        }

    }
}
