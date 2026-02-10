using UnityEngine;
using System.Collections.Generic;

namespace DeliveryDriver.City
{
    /// <summary>
    /// Manages neighborhoods in the city and handles player entering/exiting events.
    /// </summary>
    public class NeighborhoodManager : MonoBehaviour
    {
        private static NeighborhoodManager instance;
        public static NeighborhoodManager Instance => instance;

        [Header("Runtime")]
        [SerializeField] private NeighborhoodZone currentNeighborhood;
        [SerializeField] private List<Neighborhood> neighborhoods = new List<Neighborhood>();

        private NeighborhoodUI neighborhoodUI;

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
        }

        private void Start()
        {
            neighborhoodUI = FindFirstObjectByType<NeighborhoodUI>();
            if (neighborhoodUI == null)
            {
                Debug.LogWarning("[NeighborhoodManager] NeighborhoodUI not found in scene.");
            }
        }

        public void OnPlayerEnteredNeighborhood(NeighborhoodZone zone)
        {
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
    }
}
