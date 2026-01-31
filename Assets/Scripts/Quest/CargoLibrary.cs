using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDriver.Quest
{
    [CreateAssetMenu(menuName = "Quest System/Cargo Library", fileName = "CargoLibrary")]
    public class CargoLibrary : ScriptableObject
    {
        [SerializeField] private List<CargoData> cargoTypes = new List<CargoData>();

        public CargoData GetRandomCargo()
        {
            if (cargoTypes == null || cargoTypes.Count == 0)
            {
                return null;
            }

            int index = Random.Range(0, cargoTypes.Count);
            return cargoTypes[index];
        }

        public CargoData GetCargoByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || cargoTypes == null)
            {
                return null;
            }

            return cargoTypes.Find(cargo => cargo != null && cargo.CargoName == name);
        }

        public CargoData GetCargoByFragility(bool isFragile)
        {
            if (cargoTypes == null || cargoTypes.Count == 0)
            {
                return null;
            }

            List<CargoData> filtered = cargoTypes.FindAll(cargo => cargo != null && cargo.IsFragile == isFragile);

            if (filtered.Count == 0)
            {
                return null;
            }

            int index = Random.Range(0, filtered.Count);
            return filtered[index];
        }
    }
}
