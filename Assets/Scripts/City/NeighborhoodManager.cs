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
        private static bool warnedMissingInstance;

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

            if (!warnedMissingInstance)
            {
                Debug.LogWarning("[NeighborhoodManager] Missing in scene. Add a NeighborhoodManager to the scene or bootstrap before NeighborhoodZone triggers can report neighborhood changes.");
                warnedMissingInstance = true;
            }

            return null;
        }

        [Header("Runtime")]
        [SerializeField] private NeighborhoodZone currentNeighborhood;
        [SerializeField] private List<Neighborhood> neighborhoods = new List<Neighborhood>();

        private NeighborhoodUI neighborhoodUI;
        private bool initialized = false;
        private bool warnedMissingUI;

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
            if (neighborhoodUI == null)
            {
                WarnMissingUI("[NeighborhoodManager] NeighborhoodUI missing in scene. Neighborhood names will not be displayed until a configured NeighborhoodUI is added.");
                initialized = true;
                return;
            }

            if (!neighborhoodUI.HasValidReferences)
            {
                WarnMissingUI("[NeighborhoodManager] NeighborhoodUI found, but its panel/text references are missing. Neighborhood names will not be displayed until the UI is configured.");
                neighborhoodUI = null;
            }

            initialized = true;
        }

        private void WarnMissingUI(string message)
        {
            if (warnedMissingUI)
            {
                return;
            }

            Debug.LogWarning(message);
            warnedMissingUI = true;
        }

    }
}
