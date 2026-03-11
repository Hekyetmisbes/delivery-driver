namespace DeliveryDriver.Company
{
    public sealed class CompanyProfileData
    {
        public string CompanyId { get; set; }
        public string PlayerId { get; set; }
        public string CompanyName { get; set; }
        public string PlayerDisplayName { get; set; }
        public int Balance { get; set; }
        public VehicleType SelectedVehicleType { get; set; }
    }
}
