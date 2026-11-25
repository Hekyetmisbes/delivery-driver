using DeliveryDriver.Company;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runtime bootstrap that ensures FuelSystem is attached to the active player vehicle
/// and creates the holographic fuel station zones at configured refuel points.
/// This runs after scene load and after vehicle changes.
/// </summary>
[DefaultExecutionOrder(-400)]
public class FuelSystemInstaller : MonoBehaviour
{
    private const string GameSceneName = "Game";
    private static readonly Vector3[] FuelStationPositions =
    {
        new Vector3(19.75f, 0f, 185.3f),
        new Vector3(19.75f, 0f, 189.5f),
        new Vector3(139.35f, 0f, 348.7f),
        new Vector3(135.2f, 0f, 348.7f),
    };
    private const float FuelStationRadius = 2f;
    private static bool sceneHookRegistered;
    private static FuelSystemInstaller instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstallerForActiveScene()
    {
        TryInstall(SceneManager.GetActiveScene());

        if (!sceneHookRegistered)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            PlayerVehicleManager.ActiveVehicleChanged -= OnVehicleChanged;
            PlayerVehicleManager.ActiveVehicleChanged += OnVehicleChanged;
            sceneHookRegistered = true;
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstall(scene);
    }

    private static void OnVehicleChanged(CarController controller)
    {
        if (controller == null) return;
        EnsureFuelSystemOnVehicle(controller);
    }

    private static void TryInstall(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        if (!scene.name.Equals(GameSceneName, System.StringComparison.OrdinalIgnoreCase)) return;

        // Find the active vehicle and attach FuelSystem
        PlayerVehicleManager vehicleManager = PlayerVehicleManager.Instance;
        CarController controller = null;

        if (vehicleManager != null && vehicleManager.ActiveVehicleController != null)
        {
            controller = vehicleManager.ActiveVehicleController;
        }
        else
        {
            controller = Object.FindFirstObjectByType<CarController>();
        }

        if (controller != null)
        {
            EnsureFuelSystemOnVehicle(controller);
        }
    }

    private static void EnsureFuelSystemOnVehicle(CarController controller)
    {
        if (controller == null) return;

        FuelSystem fuelSystem = controller.GetComponent<FuelSystem>();
        if (fuelSystem == null)
        {
            fuelSystem = controller.gameObject.AddComponent<FuelSystem>();
        }

        fuelSystem.CaptureSpawnPose();
        fuelSystem.SetFuelStationPositions(FuelStationPositions);
        fuelSystem.SetFuelStationRadius(FuelStationRadius);

        // Create the holographic fuel station zones
        FuelStationZone.EnsureInstances(fuelSystem.FuelStationWorldPositions, fuelSystem.FuelStationRadius);
    }
}
