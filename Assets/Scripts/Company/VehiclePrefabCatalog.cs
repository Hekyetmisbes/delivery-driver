using UnityEngine;

namespace DeliveryDriver.Company
{
    [CreateAssetMenu(fileName = "VehiclePrefabCatalog", menuName = "Delivery Driver/Company/Vehicle Prefab Catalog")]
    public sealed class VehiclePrefabCatalog : ScriptableObject
    {
        [SerializeField] private GameObject vanPrefab;
        [SerializeField] private GameObject truckPrefab;

        public GameObject VanPrefab => vanPrefab;
        public GameObject TruckPrefab => truckPrefab;

        public bool TryGetPrefab(string label, out GameObject prefab)
        {
            prefab = string.Equals(label, "Truck", System.StringComparison.OrdinalIgnoreCase)
                ? truckPrefab
                : vanPrefab;
            return prefab != null;
        }
    }
}
