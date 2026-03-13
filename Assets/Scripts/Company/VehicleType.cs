using System;

namespace DeliveryDriver.Company
{
    public enum VehicleType
    {
        Van = 0,
        Truck = 1
    }

    public static class VehicleTypeExtensions
    {
        public static string ToDatabaseValue(VehicleType vehicleType)
        {
            return vehicleType == VehicleType.Truck ? "Truck" : "Van";
        }

        public static string ToDisplayLabel(VehicleType vehicleType)
        {
            return vehicleType == VehicleType.Truck ? "Tir" : "Kamyonet";
        }

        public static int ToDropdownIndex(VehicleType vehicleType)
        {
            return vehicleType == VehicleType.Truck ? 1 : 0;
        }

        public static VehicleType FromDropdownIndex(int index)
        {
            return index == 1 ? VehicleType.Truck : VehicleType.Van;
        }

        public static bool TryParseDatabaseValue(string value, out VehicleType vehicleType)
        {
            if (string.Equals(value, "Truck", StringComparison.OrdinalIgnoreCase))
            {
                vehicleType = VehicleType.Truck;
                return true;
            }

            if (string.Equals(value, "Van", StringComparison.OrdinalIgnoreCase))
            {
                vehicleType = VehicleType.Van;
                return true;
            }

            vehicleType = VehicleType.Van;
            return false;
        }
    }
}
