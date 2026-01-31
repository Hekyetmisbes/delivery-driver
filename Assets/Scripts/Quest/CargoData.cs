using UnityEngine;

namespace DeliveryDriver.Quest
{
    /// <summary>
    /// Represents cargo data for delivery quests
    /// </summary>
    [System.Serializable]
    public class CargoData
    {
        /// <summary>
        /// Display name of the cargo (e.g., "Medical Supplies", "Electronics", "Heavy Machinery")
        /// </summary>
        public string CargoName;

        /// <summary>
        /// Weight of the cargo in kilograms (0-500 kg). Affects vehicle handling and performance.
        /// </summary>
        [Range(0f, 500f)]
        public float Weight;

        /// <summary>
        /// If true, collisions will reduce cargo health
        /// </summary>
        public bool IsFragile;

        /// <summary>
        /// Current health of the cargo (0-100). Only relevant for fragile cargo.
        /// </summary>
        [Range(0f, 100f)]
        public float CargoHealth;

        /// <summary>
        /// UI icon displayed for this cargo type
        /// </summary>
        public Sprite CargoIcon;

        /// <summary>
        /// Flavor text description of the cargo
        /// </summary>
        [TextArea(2, 4)]
        public string Description;

        /// <summary>
        /// Default constructor initializing cargo with full health
        /// </summary>
        public CargoData()
        {
            CargoHealth = 100f;
            Weight = 100f;
            IsFragile = false;
            CargoName = "Standard Cargo";
            Description = "";
        }

        /// <summary>
        /// Creates a new cargo data instance with specified parameters
        /// </summary>
        /// <param name="name">Display name of the cargo</param>
        /// <param name="weight">Weight in kilograms</param>
        /// <param name="isFragile">Whether the cargo is fragile</param>
        /// <param name="description">Flavor text description</param>
        public CargoData(string name, float weight, bool isFragile, string description = "")
        {
            CargoName = name;
            Weight = Mathf.Clamp(weight, 0f, 500f);
            IsFragile = isFragile;
            CargoHealth = 100f;
            Description = description;
        }

        /// <summary>
        /// Reduces cargo health by the specified amount
        /// </summary>
        /// <param name="amount">Amount of damage to apply (0-100)</param>
        public void TakeDamage(float amount)
        {
            if (!IsFragile)
            {
                // Non-fragile cargo doesn't take damage
                return;
            }

            CargoHealth -= amount;
            CargoHealth = Mathf.Clamp(CargoHealth, 0f, 100f);

            if (IsDestroyed())
            {
                Debug.Log($"Cargo '{CargoName}' has been destroyed!");
            }
            else
            {
                Debug.Log($"Cargo '{CargoName}' took {amount} damage. Health: {CargoHealth:F1}%");
            }
        }

        /// <summary>
        /// Checks if the cargo has been destroyed
        /// </summary>
        /// <returns>True if cargo health is zero or below, false otherwise</returns>
        public bool IsDestroyed()
        {
            return CargoHealth <= 0f;
        }

        /// <summary>
        /// Restores cargo to full health
        /// </summary>
        public void RestoreHealth()
        {
            CargoHealth = 100f;
        }

        /// <summary>
        /// Gets the cargo health as a normalized value (0-1)
        /// </summary>
        /// <returns>Health percentage as a value between 0 and 1</returns>
        public float GetHealthNormalized()
        {
            return CargoHealth / 100f;
        }
    }
}
