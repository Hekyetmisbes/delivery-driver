using System;
using DeliveryDriver.Quest;
using DeliveryDriver.UI;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages vehicle fuel consumption, refueling, and out-of-fuel state.
/// Attaches to the same GameObject as CarController.
/// </summary>
public class FuelSystem : MonoBehaviour
{
    public static FuelSystem Instance { get; private set; }
    private const string FuelPrefsKey = "DeliveryDriver.Vehicle.CurrentFuel";
    private const string FuelPrefsVersionKey = "DeliveryDriver.Vehicle.FuelPersisted";
    private const float FuelSaveIntervalSeconds = 2f;

    [Header("--- FUEL SETTINGS ---")]
    [Tooltip("Maksimum yakıt kapasitesi (litre).")]
    [SerializeField] private float maxFuel = 100f;
    [Tooltip("Mevcut yakıt (litre).")]
    [SerializeField] private float currentFuel = 100f;
    [Tooltip("Durma halinde saatte tüketilen yakıt (litre/saat).")]
    [SerializeField] private float idleConsumptionPerHour = 0.5f;
    [Tooltip("Tam gazda saatte tüketilen yakıt (litre/saat).")]
    [SerializeField] private float fullThrottleConsumptionPerHour = 25f;
    [Tooltip("Tüketim hız çarpanı. Test için 1'in üstüne çıkarabilirsiniz.")]
    [Min(0.1f)]
    [SerializeField] private float consumptionRateMultiplier = 2f;
    [Tooltip("Düşük yakıt uyarısı eşiği (yüzde).")]
    [Range(0.05f, 0.4f)]
    [SerializeField] private float lowFuelWarningThreshold = 0.15f;
    [Tooltip("Litre başına yakıt fiyatı ($).")]
    [SerializeField] private int fuelPricePerLiter = 2;
    [Tooltip("Eve dönüş ücretinin bakiyeye oranı (maks). 0.5 = bakiyenin yarısını geçmez.")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float returnHomeCostFraction = 0.3f;
    [Tooltip("Eve dönüş için temel ücret ($).")]
    [SerializeField] private int returnHomeBaseCost = 50;
    [Tooltip("Eve dönüş ücreti minimum ($). Bu değerden az olmaz.")]
    [SerializeField] private int returnHomeMinCost = 10;

    [Header("--- FUEL STATION ---")]
    [Tooltip("Benzin istasyonu konumu (world space). Boşsa aracın başlangıç pozisyonu kullanılır.")]
    [SerializeField] private Vector3 fuelStationPosition = new Vector3(33.5f, 0f, 20f);
    [Tooltip("Benzin alma noktaları (world space). Boşsa fuelStationPosition kullanılır.")]
    [SerializeField] private Vector3[] fuelStationPositions = Array.Empty<Vector3>();
    [Tooltip("Benzin istasyonu algılama yarıçapı.")]
    [SerializeField] private float fuelStationRadius = 4f;
    [Tooltip("Etkileşim tuşu.")]
    // Interaction key is hardcoded to E via Input System Keyboard

    // Events
    public event Action<float> OnFuelChanged; // normalized 0-1
    public event Action OnFuelEmpty;
    public event Action OnLowFuelWarning;
    public event Action OnFuelRefilled;
    public event Action<bool> OnNearFuelStation; // true = entered, false = left

    // State
    private Rigidbody vehicleRigidbody;
    private bool isOutOfFuel;
    private bool lowFuelWarningTriggered;
    private bool isNearStation;
    private bool fuelStationPositionSet;
    private Vector3 vehicleSpawnPosition;
    private Quaternion vehicleSpawnRotation;
    private bool spawnPoseCaptured;
    private float nextFuelSaveTime;

    // Public properties
    public float CurrentFuel => currentFuel;
    public float MaxFuel => maxFuel;
    public float FuelNormalized => maxFuel > 0f ? Mathf.Clamp01(currentFuel / maxFuel) : 0f;
    public bool IsOutOfFuel => isOutOfFuel;
    public bool IsLowFuel => FuelNormalized <= lowFuelWarningThreshold && !isOutOfFuel;
    public bool IsNearFuelStation => isNearStation;
    public int FuelPricePerLiter => fuelPricePerLiter;
    public Vector3 FuelStationWorldPosition => GetNearestFuelStationPosition(transform.position);
    public Vector3[] FuelStationWorldPositions => GetFuelStationPositions();
    public float FuelStationRadius => fuelStationRadius;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Allow replacement if vehicle changed
            if (Instance.gameObject != gameObject)
            {
                Instance = this;
            }
            else
            {
                Destroy(this);
                return;
            }
        }
        Instance = this;

        vehicleRigidbody = GetComponent<Rigidbody>();
        CaptureSpawnPose();
        LoadFuelFromPrefs();
    }

    private void Start()
    {
        if (!fuelStationPositionSet)
        {
            if (fuelStationPositions != null && fuelStationPositions.Length > 0)
            {
                fuelStationPositionSet = true;
                return;
            }

            if (fuelStationPosition != Vector3.zero)
            {
                fuelStationPositions = new[] { fuelStationPosition };
                fuelStationPositionSet = true;
                return;
            }

            // If fuel station position not explicitly set, use vehicle spawn position
            fuelStationPosition = vehicleSpawnPosition;
            fuelStationPositions = new[] { fuelStationPosition };
            fuelStationPositionSet = true;
        }
    }

    private void OnDestroy()
    {
        SaveFuelToPrefs();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            SaveFuelToPrefs();
        }
    }

    private void OnApplicationQuit()
    {
        SaveFuelToPrefs();
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        ConsumeFuel();
        CheckFuelStationProximity();
        HandleFuelStationInteraction();
    }

    /// <summary>
    /// Captures the initial spawn position of the vehicle for fuel station placement.
    /// </summary>
    public void CaptureSpawnPose()
    {
        if (spawnPoseCaptured) return;
        vehicleSpawnPosition = transform.position;
        vehicleSpawnRotation = transform.rotation;
        spawnPoseCaptured = true;
    }

    /// <summary>
    /// Sets the fuel station position explicitly. Use this from scene setup.
    /// </summary>
    public void SetFuelStationPosition(Vector3 position)
    {
        fuelStationPosition = position;
        fuelStationPositions = new[] { position };
        fuelStationPositionSet = true;
    }

    public void SetFuelStationPositions(params Vector3[] positions)
    {
        if (positions == null || positions.Length == 0)
        {
            return;
        }

        fuelStationPositions = (Vector3[])positions.Clone();
        fuelStationPosition = fuelStationPositions[0];
        fuelStationPositionSet = true;
    }

    public void SetFuelStationRadius(float radius)
    {
        fuelStationRadius = Mathf.Max(0.1f, radius);
    }

    /// <summary>
    /// Returns whether the engine should be blocked due to no fuel.
    /// Called by CarController.
    /// </summary>
    public bool ShouldBlockEngine()
    {
        return isOutOfFuel;
    }

    /// <summary>
    /// Calculates the refuel cost for a given number of liters.
    /// </summary>
    public int CalculateRefuelCost(float liters)
    {
        return Mathf.Max(0, Mathf.CeilToInt(liters * fuelPricePerLiter));
    }

    /// <summary>
    /// Calculates the cost to fill the tank completely.
    /// </summary>
    public int CalculateFullRefuelCost()
    {
        float litersNeeded = maxFuel - currentFuel;
        return CalculateRefuelCost(litersNeeded);
    }

    /// <summary>
    /// Calculates how many liters a player can buy with a given budget.
    /// </summary>
    public float CalculateAffordableLiters(int budget)
    {
        if (fuelPricePerLiter <= 0) return maxFuel - currentFuel;
        return Mathf.Min(maxFuel - currentFuel, (float)budget / fuelPricePerLiter);
    }

    /// <summary>
    /// Refuels the vehicle by a specific amount of liters.
    /// Returns the actual cost charged.
    /// </summary>
    public int Refuel(float liters)
    {
        if (liters <= 0f) return 0;

        float actualLiters = Mathf.Min(liters, maxFuel - currentFuel);
        int cost = CalculateRefuelCost(actualLiters);

        PlayerProgressionManager progression = PlayerProgressionManager.Instance;
        if (progression == null) return 0;

        if (cost > 0 && !progression.SpendMoney(cost))
        {
            // Can't afford it - buy what they can
            float affordable = CalculateAffordableLiters(progression.CurrentMoney);
            cost = CalculateRefuelCost(affordable);
            actualLiters = affordable;
            if (cost > 0 && !progression.SpendMoney(cost))
            {
                return 0;
            }
        }

        currentFuel = Mathf.Clamp(currentFuel + actualLiters, 0f, maxFuel);
        UpdateFuelStateFlags();
        SaveFuelToPrefs();

        OnFuelChanged?.Invoke(FuelNormalized);
        OnFuelRefilled?.Invoke();
        Debug.Log($"[FuelSystem] Refueled {actualLiters:F1}L for ${cost}. Fuel: {currentFuel:F1}/{maxFuel}L");
        return cost;
    }

    /// <summary>
    /// Refuels to full tank. Returns the cost charged.
    /// </summary>
    public int RefuelToFull()
    {
        float litersNeeded = maxFuel - currentFuel;
        return Refuel(litersNeeded);
    }

    /// <summary>
    /// Calculates the return home cost, capped to never exceed the player's balance.
    /// </summary>
    public int CalculateReturnHomeCost()
    {
        PlayerProgressionManager progression = PlayerProgressionManager.Instance;
        int playerBalance = progression != null ? progression.CurrentMoney : 0;

        int baseCost = Mathf.Max(returnHomeMinCost, returnHomeBaseCost);
        int maxFromBalance = Mathf.FloorToInt(playerBalance * returnHomeCostFraction);
        int cost = Mathf.Min(baseCost, maxFromBalance);

        // Never exceed player balance
        cost = Mathf.Min(cost, playerBalance);

        // At minimum 0 (free if player is broke)
        return Mathf.Max(0, cost);
    }

    /// <summary>
    /// Teleports vehicle back to spawn (fuel station) position and refuels partially.
    /// </summary>
    public void ReturnHome()
    {
        int cost = CalculateReturnHomeCost();
        PlayerProgressionManager progression = PlayerProgressionManager.Instance;
        if (cost > 0 && progression != null)
        {
            progression.SpendMoney(cost);
        }

        // Teleport vehicle
        if (vehicleRigidbody != null)
        {
            vehicleRigidbody.linearVelocity = Vector3.zero;
            vehicleRigidbody.angularVelocity = Vector3.zero;
            vehicleRigidbody.position = vehicleSpawnPosition;
            vehicleRigidbody.rotation = vehicleSpawnRotation;
            UnityEngine.Physics.SyncTransforms();
        }
        else
        {
            transform.SetPositionAndRotation(vehicleSpawnPosition, vehicleSpawnRotation);
            UnityEngine.Physics.SyncTransforms();
        }

        // Give a small amount of fuel to continue (25% tank)
        currentFuel = maxFuel * 0.25f;
        UpdateFuelStateFlags();
        SaveFuelToPrefs();
        OnFuelChanged?.Invoke(FuelNormalized);
        OnFuelRefilled?.Invoke();

        Debug.Log($"[FuelSystem] Returned home. Cost: ${cost}. Fuel: {currentFuel:F1}/{maxFuel}L");
    }

    private void ConsumeFuel()
    {
        if (isOutOfFuel || currentFuel <= 0f)
        {
            if (!isOutOfFuel)
            {
                isOutOfFuel = true;
                OnFuelEmpty?.Invoke();
                Debug.Log("[FuelSystem] Vehicle is out of fuel!");
            }
            return;
        }

        float speedMps = vehicleRigidbody != null ? vehicleRigidbody.linearVelocity.magnitude : 0f;
        float speedFactor = Mathf.Clamp01(speedMps / 30f); // normalize: 30 m/s ≈ 108 km/h as full throttle approx

        float consumptionPerHour = Mathf.Lerp(idleConsumptionPerHour, fullThrottleConsumptionPerHour, speedFactor);
        float consumptionThisFrame = consumptionPerHour * Mathf.Max(0.1f, consumptionRateMultiplier) * (Time.deltaTime / 3600f);

        currentFuel = Mathf.Max(0f, currentFuel - consumptionThisFrame);
        OnFuelChanged?.Invoke(FuelNormalized);
        SaveFuelToPrefsThrottled();

        // Check low fuel warning
        if (!lowFuelWarningTriggered && FuelNormalized <= lowFuelWarningThreshold)
        {
            lowFuelWarningTriggered = true;
            OnLowFuelWarning?.Invoke();
        }

        // Check empty
        if (currentFuel <= 0f)
        {
            currentFuel = 0f;
            isOutOfFuel = true;
            SaveFuelToPrefs();
            OnFuelEmpty?.Invoke();
            Debug.Log("[FuelSystem] Vehicle is out of fuel!");
        }
    }

    private void LoadFuelFromPrefs()
    {
        if (!PlayerPrefs.HasKey(FuelPrefsVersionKey))
        {
            currentFuel = Mathf.Clamp(currentFuel, 0f, maxFuel);
            UpdateFuelStateFlags();
            return;
        }

        currentFuel = Mathf.Clamp(PlayerPrefs.GetFloat(FuelPrefsKey, currentFuel), 0f, maxFuel);
        UpdateFuelStateFlags();
        OnFuelChanged?.Invoke(FuelNormalized);
    }

    private void SaveFuelToPrefsThrottled()
    {
        if (Time.unscaledTime < nextFuelSaveTime)
        {
            return;
        }

        nextFuelSaveTime = Time.unscaledTime + FuelSaveIntervalSeconds;
        SaveFuelToPrefs();
    }

    private void SaveFuelToPrefs()
    {
        PlayerPrefs.SetInt(FuelPrefsVersionKey, 1);
        PlayerPrefs.SetFloat(FuelPrefsKey, Mathf.Clamp(currentFuel, 0f, maxFuel));
        PlayerPrefs.Save();
    }

    private void UpdateFuelStateFlags()
    {
        currentFuel = Mathf.Clamp(currentFuel, 0f, maxFuel);
        isOutOfFuel = currentFuel <= 0.001f;
        lowFuelWarningTriggered = FuelNormalized <= lowFuelWarningThreshold;
    }

    private void CheckFuelStationProximity()
    {
        float closestDistance = float.PositiveInfinity;
        Vector3[] stationPositions = GetFuelStationPositions();

        for (int i = 0; i < stationPositions.Length; i++)
        {
            float distance = Vector3.Distance(transform.position, stationPositions[i]);
            if (distance < closestDistance)
            {
                closestDistance = distance;
            }
        }

        bool wasNear = isNearStation;
        isNearStation = closestDistance <= fuelStationRadius;

        if (isNearStation != wasNear)
        {
            OnNearFuelStation?.Invoke(isNearStation);
        }
    }

    private void HandleFuelStationInteraction()
    {
        if (!isNearStation) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
        {
            FuelStationUI.Show(this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3[] stationPositions = Application.isPlaying
            ? GetFuelStationPositions()
            : GetEditorFuelStationPositions();

        foreach (Vector3 stationPos in stationPositions)
        {
            Gizmos.color = new Color(0f, 1f, 0.4f, 0.35f);
            Gizmos.DrawWireSphere(stationPos, fuelStationRadius);
            Gizmos.color = new Color(0f, 1f, 0.4f, 0.12f);
            Gizmos.DrawSphere(stationPos, fuelStationRadius);
        }
    }

    private Vector3 GetNearestFuelStationPosition(Vector3 referencePosition)
    {
        Vector3[] stationPositions = GetFuelStationPositions();
        Vector3 nearestPosition = stationPositions[0];
        float closestDistance = Vector3.Distance(referencePosition, nearestPosition);

        for (int i = 1; i < stationPositions.Length; i++)
        {
            float distance = Vector3.Distance(referencePosition, stationPositions[i]);
            if (distance < closestDistance)
            {
                nearestPosition = stationPositions[i];
                closestDistance = distance;
            }
        }

        return nearestPosition;
    }

    private Vector3[] GetFuelStationPositions()
    {
        if (fuelStationPositionSet && fuelStationPositions != null && fuelStationPositions.Length > 0)
        {
            return fuelStationPositions;
        }

        if (fuelStationPosition != Vector3.zero)
        {
            return new[] { fuelStationPosition };
        }

        return new[] { vehicleSpawnPosition };
    }

    private Vector3[] GetEditorFuelStationPositions()
    {
        if (fuelStationPositions != null && fuelStationPositions.Length > 0)
        {
            return fuelStationPositions;
        }

        return new[] { fuelStationPosition != Vector3.zero ? fuelStationPosition : transform.position };
    }
}
