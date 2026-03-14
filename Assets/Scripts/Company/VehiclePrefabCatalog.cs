using UnityEngine;

namespace DeliveryDriver.Company
{
    [CreateAssetMenu(fileName = "VehiclePrefabCatalog", menuName = "Delivery Driver/Company/Vehicle Prefab Catalog")]
    public sealed class VehiclePrefabCatalog : ScriptableObject
    {
        [SerializeField] private GameObject vanPrefab;
        [SerializeField] private GameObject truckPrefab;

        public GameObject VanPrefab => GetValidatedPrefab(vanPrefab);
        public GameObject TruckPrefab => GetValidatedPrefab(truckPrefab);

        public bool TryGetPrefab(string label, out GameObject prefab)
        {
            prefab = string.Equals(label, "Truck", System.StringComparison.OrdinalIgnoreCase)
                ? TruckPrefab
                : VanPrefab;
            return prefab != null;
        }

        private static GameObject GetValidatedPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                return null;
            }

            try
            {
                _ = prefab.transform;
                return prefab;
            }
            catch (MissingReferenceException)
            {
                return null;
            }
        }
    }
}
