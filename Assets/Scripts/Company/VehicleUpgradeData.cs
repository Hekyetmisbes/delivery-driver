using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDriver.Company
{
    /// <summary>
    /// Veritabanında saklanacak araç geliştirme verileri.
    /// </summary>
    [Serializable]
    public class VehicleUpgradeData
    {
        public int EngineLevel;
        public int BrakeLevel;
        public int HandlingLevel;

        public static VehicleUpgradeData Default => new VehicleUpgradeData { EngineLevel = 0, BrakeLevel = 0, HandlingLevel = 0 };
    }
}
