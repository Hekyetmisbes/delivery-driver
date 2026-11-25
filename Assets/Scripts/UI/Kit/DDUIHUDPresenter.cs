using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryDriver.UIKit
{
    /// <summary>
    /// UI-only sample presenter. Connect to existing SpeedometerUI, FuelHudUI, QuestManager,
    /// etc. using your project's event system. Does not control the vehicle or game state.
    /// </summary>
    public sealed class DDUIHUDPresenter : MonoBehaviour
    {
        [Header("Vehicle")]
        [SerializeField] private TextMeshProUGUI speedLabel;
        [SerializeField] private TextMeshProUGUI speedUnitLabel;
        [SerializeField] private TextMeshProUGUI gearLabel;
        [SerializeField] private Image fuelFill;
        [SerializeField] private TextMeshProUGUI fuelPercentLabel;
        [SerializeField] private Image vehicleHealthFill;

        [Header("Company and mission")]
        [SerializeField] private TextMeshProUGUI balanceLabel;
        [SerializeField] private TextMeshProUGUI missionTitleLabel;
        [SerializeField] private TextMeshProUGUI destinationLabel;
        [SerializeField] private TextMeshProUGUI remainingTimeLabel;
        [SerializeField] private Image cargoHealthFill;

        public void ShowSpeed(float kilometersPerHour, bool useMph)
        {
            float shown = useMph ? kilometersPerHour * 0.621371f : kilometersPerHour;
            if (speedLabel) speedLabel.text = Mathf.RoundToInt(Mathf.Abs(shown)).ToString();
            if (speedUnitLabel) speedUnitLabel.text = useMph ? "MPH" : "KM/H";
        }

        public void ShowGear(string gear)
        {
            if (gearLabel) gearLabel.text = string.IsNullOrEmpty(gear) ? "N" : gear;
        }

        public void ShowFuel(float normalized)
        {
            normalized = Mathf.Clamp01(normalized);
            if (fuelFill)
            {
                fuelFill.fillAmount = normalized;
                fuelFill.color = normalized < 0.15f ? DDUITheme.Danger :
                                 normalized < 0.30f ? DDUITheme.Warning : DDUITheme.Success;
            }
            if (fuelPercentLabel) fuelPercentLabel.text = Mathf.RoundToInt(normalized * 100f) + "%";
        }

        public void ShowVehicleHealth(float normalized)
        {
            if (vehicleHealthFill) vehicleHealthFill.fillAmount = Mathf.Clamp01(normalized);
        }

        public void ShowCompanyBalance(decimal amount, string currencySymbol = "$")
        {
            if (balanceLabel) balanceLabel.text = currencySymbol + amount.ToString("N0");
        }

        public void ShowMission(string title, string destination, int remainingSeconds, float cargoHealth01)
        {
            if (missionTitleLabel) missionTitleLabel.text = title ?? "";
            if (destinationLabel) destinationLabel.text = destination ?? "";
            if (remainingTimeLabel) remainingTimeLabel.text =
                string.Format("{0:00}:{1:00}", Mathf.Max(0, remainingSeconds) / 60,
                    Mathf.Max(0, remainingSeconds) % 60);
            if (cargoHealthFill) cargoHealthFill.fillAmount = Mathf.Clamp01(cargoHealth01);
        }
    }
}
