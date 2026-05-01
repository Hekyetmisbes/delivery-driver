using System;
using DeliveryDriver.Navigation;
using DeliveryDriver.Quest;
using DeliveryDriver.Quest.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeliveryDriver.Vehicle;

namespace DeliveryDriver.Company
{
    [DefaultExecutionOrder(-560)]
    public class PlayerVehicleManager : MonoBehaviour
    {
        private readonly struct SpawnSourceContext
        {
            public SpawnSourceContext(GameObject root, CarController controller, Rigidbody rigidbody, Pose pose, string sourceLabel)
            {
                Root = root;
                Controller = controller;
                Rigidbody = rigidbody;
                Pose = pose;
                SourceLabel = sourceLabel;
            }

            public GameObject Root { get; }
            public CarController Controller { get; }
            public Rigidbody Rigidbody { get; }
            public Pose Pose { get; }
            public string SourceLabel { get; }
        }

        private const string GameSceneName = "Game";
        private const string VanVehicleName = "Minivan";
        private const string TruckVehicleName = "LorryCargo";

        private GameObject vanPrefab;
        private GameObject truckPrefab;
        private GameObject activeVehicleRoot;
        private CarController activeVehicleController;
        private GameObject initialSceneVehicleRoot;
        private CarController initialSceneVehicleController;
        private VehicleType activeVehicleType = VehicleType.Van;
        private bool prefabsConfigured;
        private CameraFollow cachedCameraFollow;
        private ReverseCameraHUD cachedReverseCameraHud;
        private DeliveryManager cachedDeliveryManager;
        private DeliveryUI cachedDeliveryUi;
        private CompassUI cachedCompassUi;
        private NavigationService cachedNavigationService;

        public static PlayerVehicleManager Instance { get; private set; }
        public static event Action<CarController> ActiveVehicleChanged;

        public CarController ActiveVehicleController => activeVehicleController;
        public VehicleType ActiveVehicleType => activeVehicleType;
        public bool IsConfigured => prefabsConfigured;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (!IsGameSceneActive())
            {
                Destroy(gameObject);
                return;
            }

            CacheInitialSceneVehicleReference();
            SynchronizeSceneVehicleState();
            EnsureRuntimeVehicleReady();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void SetVehiclePrefabs(GameObject vanVehiclePrefab, GameObject truckVehiclePrefab)
        {
            vanPrefab = ValidateVehiclePrefab(vanVehiclePrefab, "Van");
            truckPrefab = ValidateVehiclePrefab(truckVehiclePrefab, "Truck");
            prefabsConfigured = vanPrefab != null && truckPrefab != null;

            SynchronizeSceneVehicleState();
            EnsureRuntimeVehicleReady();
        }

        private static GameObject ValidateVehiclePrefab(GameObject prefab, string label)
        {
            if (prefab == null)
            {
                Debug.LogError($"[PlayerVehicleManager] {label} prefab reference is missing.");
                return null;
            }

            try
            {
                CarController controller = prefab.GetComponent<CarController>();
                if (controller == null)
                {
                    Debug.LogError($"[PlayerVehicleManager] {label} prefab '{prefab.name}' is missing CarController.");
                    return null;
                }

                return prefab;
            }
            catch (MissingReferenceException)
            {
                Debug.LogError($"[PlayerVehicleManager] {label} prefab reference points to a missing asset. Reassign the prefab in VehiclePrefabCatalog.");
                return null;
            }
        }

        public bool ApplyVehicleType(VehicleType vehicleType)
        {
            return ApplyVehicleTypeInternal(vehicleType, false);
        }

        public bool EnsureVehicleType(VehicleType vehicleType)
        {
            return ApplyVehicleTypeInternal(vehicleType, true);
        }

        public bool IsVehicleTypeAvailable(VehicleType vehicleType)
        {
            SynchronizeSceneVehicleState();

            if (prefabsConfigured)
            {
                GameObject targetTemplate = GetTemplateRoot(vehicleType);
                return targetTemplate != null && targetTemplate.GetComponent<CarController>() != null;
            }

            return CanUseCurrentVehicleAsFallback(vehicleType);
        }

        private bool ApplyVehicleTypeInternal(VehicleType vehicleType, bool allowDuringActiveQuest)
        {
            SynchronizeSceneVehicleState();

            if (!prefabsConfigured)
            {
                if (CanUseCurrentVehicleAsFallback(vehicleType))
                {
                    Debug.LogWarning($"[PlayerVehicleManager] Vehicle prefabs are not configured. Keeping the currently active scene vehicle for '{vehicleType}'.");
                    ConfigureVehicleSpecializedBindings(activeVehicleController, activeVehicleType);
                    EnsureSingleActivePlayerVehicle(activeVehicleController.gameObject);
                    RebindSystems(activeVehicleController);
                    return true;
                }

                Debug.LogError("[PlayerVehicleManager] Vehicle prefab references are not configured.");
                return false;
            }

            GameObject targetTemplate = GetTemplateRoot(vehicleType);
            if (targetTemplate == null)
            {
                Debug.LogError($"[PlayerVehicleManager] Vehicle prefab load failed for '{vehicleType}'.");
                return false;
            }

            CarController templateController = targetTemplate.GetComponent<CarController>();
            if (templateController == null)
            {
                Debug.LogError($"[PlayerVehicleManager] Prefab '{targetTemplate.name}' is missing CarController.");
                return false;
            }
            if (activeVehicleController != null &&
                activeVehicleController.gameObject != null &&
                activeVehicleType == vehicleType)
            {
                ConfigureVehicleSpecializedBindings(activeVehicleController, vehicleType);
                EnsureSingleActivePlayerVehicle(activeVehicleController.gameObject);
                RebindSystems(activeVehicleController);
                return true;
            }

            if (!allowDuringActiveQuest && HasActiveQuest())
            {
                Debug.LogWarning("[PlayerVehicleManager] Vehicle switching is blocked while an active quest is running.");
                return false;
            }

            if (!TryResolveSpawnSource(out SpawnSourceContext spawnSource))
            {
                return false;
            }

            Vector3 linearVelocity = CaptureLinearVelocity(spawnSource.Rigidbody);
            Vector3 angularVelocity = CaptureAngularVelocity(spawnSource.Rigidbody);

            if (activeVehicleRoot != null && activeVehicleRoot != spawnSource.Root)
            {
                Destroy(activeVehicleRoot);
            }

            if (spawnSource.Root != null)
            {
                Destroy(spawnSource.Root);
            }

            GameObject newVehicleRoot = Instantiate(targetTemplate, spawnSource.Pose.position, spawnSource.Pose.rotation);
            newVehicleRoot.name = targetTemplate.name;
            newVehicleRoot.tag = "Player";
            newVehicleRoot.SetActive(true);

            CarController controller = newVehicleRoot.GetComponent<CarController>();
            if (controller == null)
            {
                Debug.LogError($"[PlayerVehicleManager] Instantiated vehicle '{newVehicleRoot.name}' does not contain CarController.");
                Destroy(newVehicleRoot);
                return false;
            }

            Rigidbody rb = controller.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = linearVelocity;
                rb.angularVelocity = angularVelocity;
            }

            activeVehicleRoot = newVehicleRoot;
            activeVehicleController = controller;
            activeVehicleType = vehicleType;

            ConfigureVehicleSpecializedBindings(controller, vehicleType);
            EnsureSingleActivePlayerVehicle(newVehicleRoot);
            RebindSystems(controller);

            Debug.Log($"[PlayerVehicleManager] Active player vehicle switched to '{newVehicleRoot.name}' ({vehicleType}).");
            return true;
        }

        private void ResolveActiveSceneVehicle()
        {
            SynchronizeSceneVehicleState();
        }

        private void CacheInitialSceneVehicleReference()
        {
            if (initialSceneVehicleController != null && initialSceneVehicleController.gameObject != null)
            {
                return;
            }

            if (TryGetAnyCarController(out CarController sceneController))
            {
                initialSceneVehicleController = sceneController;
                initialSceneVehicleRoot = sceneController.gameObject;
            }
        }

        private void SynchronizeSceneVehicleState()
        {
            if (TryResolveActiveVehicleCandidate(out CarController resolvedController, out string sourceLabel))
            {
                bool changed = activeVehicleController != resolvedController || activeVehicleRoot != resolvedController.gameObject;
                activeVehicleController = resolvedController;
                activeVehicleRoot = resolvedController.gameObject;
                activeVehicleType = ResolveVehicleType(resolvedController.gameObject);

                if (changed)
                {
                    Debug.Log($"[PlayerVehicleManager] Active vehicle synchronized from {sourceLabel}: '{resolvedController.gameObject.name}'.");
                }
                return;
            }

            activeVehicleController = null;
            activeVehicleRoot = null;
        }

        private void EnsureRuntimeVehicleReady()
        {
            SynchronizeSceneVehicleState();

            if (activeVehicleController != null && activeVehicleController.gameObject != null)
            {
                if (!activeVehicleController.gameObject.activeSelf)
                {
                    activeVehicleController.gameObject.SetActive(true);
                }

                activeVehicleRoot = activeVehicleController.gameObject;
                activeVehicleType = ResolveVehicleType(activeVehicleRoot);
                ConfigureVehicleSpecializedBindings(activeVehicleController, activeVehicleType);
                EnsureSingleActivePlayerVehicle(activeVehicleRoot);
                RebindSystems(activeVehicleController);
                return;
            }

            if (!prefabsConfigured)
            {
                return;
            }

            if (!TryResolveDesiredVehicleType(out VehicleType desiredVehicleType))
            {
                return;
            }

            ApplyVehicleTypeInternal(desiredVehicleType, true);
        }

        private bool TryResolveDesiredVehicleType(out VehicleType desiredVehicleType)
        {
            desiredVehicleType = VehicleType.Van;
            QuestDatabaseService database = QuestDatabaseService.Instance;
            if (database == null || !database.IsReady)
            {
                Debug.LogWarning("[PlayerVehicleManager] Database-backed company profile is not ready yet. Runtime vehicle spawn will wait for SQLite initialization.");
                return false;
            }

            if (!database.EnsureDefaultCompanyProfile())
            {
                Debug.LogError("[PlayerVehicleManager] Failed to ensure the default company profile before resolving the active vehicle type.");
                return false;
            }

            CompanyProfileData profile = database.GetCompanyProfile(QuestDatabaseService.DefaultPlayerId);
            if (profile == null)
            {
                Debug.LogError("[PlayerVehicleManager] Company profile could not be loaded from the database.");
                return false;
            }

            desiredVehicleType = profile.SelectedVehicleType;
            return true;
        }

        private bool CanUseCurrentVehicleAsFallback(VehicleType requestedVehicleType)
        {
            return activeVehicleController != null &&
                   activeVehicleController.gameObject != null &&
                   activeVehicleType == requestedVehicleType;
        }

        private void EnsureSingleActivePlayerVehicle(GameObject activeRoot)
        {
            CarController[] vehicles = FindObjectsByType<CarController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < vehicles.Length; i++)
            {
                CarController vehicle = vehicles[i];
                if (vehicle == null || vehicle.gameObject == null)
                {
                    continue;
                }

                bool shouldStayActive = vehicle.gameObject == activeRoot;
                vehicle.gameObject.tag = shouldStayActive ? "Player" : "Untagged";

                if (!shouldStayActive)
                {
                    vehicle.gameObject.SetActive(false);
                }
            }
        }

        private void RebindSystems(CarController controller)
        {
            if (controller == null)
            {
                return;
            }

            CameraFollow cameraFollow = GetCameraFollow();
            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(controller.transform);
            }
            else
            {
                ReverseCameraHUD reverseCameraHud = GetReverseCameraHud();
                reverseCameraHud?.SetTarget(controller.transform);
            }

            QuestManager.Instance?.SetPlayerVehicle(controller);

            DeliveryManager deliveryManager = GetDeliveryManager();
            if (deliveryManager != null)
            {
                deliveryManager.SetPlayerVehicle(controller);
            }

            GetNavigationService()?.SetPlayerTransform(controller.transform);

            DeliveryUI deliveryUI = GetDeliveryUi();
            if (deliveryUI != null)
            {
                deliveryUI.SetPlayerTransform(controller.transform);
            }

            CompassUI compassUi = GetCompassUi();
            if (compassUi != null)
            {
                compassUi.SetPlayerTransform(controller.transform);
            }

            ActiveVehicleChanged?.Invoke(controller);
        }

        private static void ConfigureVehicleSpecializedBindings(CarController controller, VehicleType vehicleType)
        {
            if (controller == null)
            {
                return;
            }

            VehicleExhaustSmoke exhaustSmoke = controller.GetComponent<VehicleExhaustSmoke>();
            if (exhaustSmoke == null)
            {
                exhaustSmoke = controller.gameObject.AddComponent<VehicleExhaustSmoke>();
            }

            exhaustSmoke.ConfigurePreset(vehicleType == VehicleType.Truck
                ? VehicleExhaustSmokePreset.Truck
                : VehicleExhaustSmokePreset.Van);

            if (vehicleType == VehicleType.Truck)
            {
                TruckWheelVisuals truckWheelVisuals = controller.GetComponent<TruckWheelVisuals>();
                if (truckWheelVisuals == null)
                {
                    truckWheelVisuals = controller.gameObject.AddComponent<TruckWheelVisuals>();
                }

                truckWheelVisuals.Initialize(controller);

                if (controller.GetComponent<VehicleCameraAnchors>() == null)
                {
                    controller.gameObject.AddComponent<VehicleCameraAnchors>();
                }
            }
        }

        private bool TryResolveActiveVehicleCandidate(out CarController controller, out string sourceLabel)
        {
            if (activeVehicleController != null && activeVehicleController.gameObject != null)
            {
                controller = activeVehicleController;
                sourceLabel = "active vehicle cache";
                return true;
            }

            if (activeVehicleRoot != null &&
                activeVehicleRoot.transform != null &&
                TryResolveController(activeVehicleRoot.transform, out controller))
            {
                sourceLabel = "active vehicle root";
                return true;
            }

            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null && TryResolveController(taggedPlayer.transform, out controller))
            {
                sourceLabel = "Player tag";
                return true;
            }

            if (TryGetAnyCarController(out controller))
            {
                sourceLabel = "scene CarController";
                return true;
            }

            if (QuestManager.Instance != null &&
                QuestManager.Instance.PlayerTransform != null &&
                TryResolveController(QuestManager.Instance.PlayerTransform, out controller))
            {
                sourceLabel = "QuestManager.PlayerTransform";
                return true;
            }

            CameraFollow cameraFollow = GetCameraFollow();
            if (cameraFollow != null && cameraFollow.target != null && TryResolveController(cameraFollow.target, out controller))
            {
                sourceLabel = "CameraFollow.target";
                return true;
            }

            if (initialSceneVehicleController != null && initialSceneVehicleController.gameObject != null)
            {
                controller = initialSceneVehicleController;
                sourceLabel = "initial scene vehicle cache";
                return true;
            }

            if (initialSceneVehicleRoot != null &&
                initialSceneVehicleRoot.transform != null &&
                TryResolveController(initialSceneVehicleRoot.transform, out controller))
            {
                sourceLabel = "initial scene vehicle root";
                return true;
            }

            controller = null;
            sourceLabel = string.Empty;
            return false;
        }

        private bool TryResolveSpawnSource(out SpawnSourceContext spawnSource)
        {
            SynchronizeSceneVehicleState();
            CacheInitialSceneVehicleReference();

            if (TryBuildSpawnSourceFromController(activeVehicleController, "active vehicle cache", out spawnSource))
            {
                return true;
            }

            Debug.Log("[PlayerVehicleManager] Active vehicle cache unavailable for spawn pose; trying active vehicle root.");
            if (TryBuildSpawnSourceFromGameObject(activeVehicleRoot, "active vehicle root", out spawnSource))
            {
                return true;
            }

            Debug.Log("[PlayerVehicleManager] Active vehicle root unavailable for spawn pose; trying tagged player.");
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null && TryBuildSpawnSourceFromTransform(taggedPlayer.transform, "Player tag", out spawnSource))
            {
                return true;
            }

            Debug.Log("[PlayerVehicleManager] Tagged player unavailable for spawn pose; trying scene CarController.");
            if (TryGetAnyCarController(out CarController anyController) &&
                TryBuildSpawnSourceFromController(anyController, "scene CarController", out spawnSource))
            {
                return true;
            }

            Debug.Log("[PlayerVehicleManager] Scene CarController unavailable for spawn pose; trying QuestManager player transform.");
            if (QuestManager.Instance != null &&
                QuestManager.Instance.PlayerTransform != null &&
                TryBuildSpawnSourceFromTransform(QuestManager.Instance.PlayerTransform, "QuestManager.PlayerTransform", out spawnSource))
            {
                return true;
            }

            Debug.Log("[PlayerVehicleManager] QuestManager player transform unavailable for spawn pose; trying CameraFollow target.");
            CameraFollow cameraFollow = GetCameraFollow();
            if (cameraFollow != null &&
                cameraFollow.target != null &&
                TryBuildSpawnSourceFromTransform(cameraFollow.target, "CameraFollow.target", out spawnSource))
            {
                return true;
            }

            Debug.Log("[PlayerVehicleManager] Camera target unavailable for spawn pose; trying initial scene vehicle cache.");
            if (TryBuildSpawnSourceFromController(initialSceneVehicleController, "initial scene vehicle cache", out spawnSource))
            {
                return true;
            }

            Debug.Log("[PlayerVehicleManager] Initial scene vehicle cache unavailable; trying initial scene vehicle root.");
            if (TryBuildSpawnSourceFromGameObject(initialSceneVehicleRoot, "initial scene vehicle root", out spawnSource))
            {
                return true;
            }

            Debug.Log("[PlayerVehicleManager] Initial scene vehicle root unavailable; trying fallback default target.");
            Transform defaultTarget = VehicleCameraTargetResolver.ResolveDefaultTarget();
            if (defaultTarget != null && TryBuildSpawnSourceFromTransform(defaultTarget, "default gameplay target", out spawnSource))
            {
                return true;
            }

            Debug.LogError("[PlayerVehicleManager] No valid spawn source could be resolved from active cache, active root, tagged player, scene CarController, QuestManager, CameraFollow, or initial scene vehicle.");
            spawnSource = default;
            return false;
        }

        private bool TryBuildSpawnSourceFromController(CarController controller, string sourceLabel, out SpawnSourceContext spawnSource)
        {
            if (controller == null || controller.gameObject == null)
            {
                spawnSource = default;
                return false;
            }

            return TryBuildSpawnSourceFromTransform(controller.transform, sourceLabel, out spawnSource);
        }

        private bool TryBuildSpawnSourceFromGameObject(GameObject sourceRoot, string sourceLabel, out SpawnSourceContext spawnSource)
        {
            if (sourceRoot == null)
            {
                spawnSource = default;
                return false;
            }

            return TryBuildSpawnSourceFromTransform(sourceRoot.transform, sourceLabel, out spawnSource);
        }

        private bool TryBuildSpawnSourceFromTransform(Transform candidate, string sourceLabel, out SpawnSourceContext spawnSource)
        {
            VehicleCameraBinding binding = VehicleCameraTargetResolver.Resolve(candidate);
            Transform sourceTransform = binding.CarController != null ? binding.CarController.transform : binding.Target;
            if (sourceTransform == null)
            {
                spawnSource = default;
                return false;
            }

            GameObject root = binding.CarController != null
                ? binding.CarController.gameObject
                : sourceTransform.gameObject;
            Pose pose = new Pose(sourceTransform.position, sourceTransform.rotation);
            spawnSource = new SpawnSourceContext(root, binding.CarController, binding.Rigidbody, pose, sourceLabel);
            Debug.Log($"[PlayerVehicleManager] Spawn pose resolved from {sourceLabel}: '{root.name}' at {pose.position}.");
            return true;
        }

        private static bool TryResolveController(Transform source, out CarController controller)
        {
            controller = source != null
                ? source.GetComponent<CarController>() ??
                  source.GetComponentInParent<CarController>() ??
                  source.GetComponentInChildren<CarController>()
                : null;
            return controller != null;
        }

        private static bool TryGetAnyCarController(out CarController controller)
        {
            controller = UnityEngine.Object.FindFirstObjectByType<CarController>();
            if (controller != null)
            {
                return true;
            }

            CarController[] inactiveVehicles = UnityEngine.Object.FindObjectsByType<CarController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            controller = inactiveVehicles.Length > 0 ? inactiveVehicles[0] : null;
            return controller != null;
        }

        private static Vector3 CaptureLinearVelocity(Rigidbody rb)
        {
            return rb != null ? rb.linearVelocity : Vector3.zero;
        }

        private static Vector3 CaptureAngularVelocity(Rigidbody rb)
        {
            return rb != null ? rb.angularVelocity : Vector3.zero;
        }

        private GameObject GetTemplateRoot(VehicleType vehicleType)
        {
            return vehicleType == VehicleType.Truck ? truckPrefab : vanPrefab;
        }

        private static VehicleType ResolveVehicleType(GameObject vehicleRoot)
        {
            if (vehicleRoot == null)
            {
                return VehicleType.Van;
            }

            string normalizedName = NormalizeVehicleName(vehicleRoot.name);
            return normalizedName.Equals(TruckVehicleName, StringComparison.OrdinalIgnoreCase)
                ? VehicleType.Truck
                : VehicleType.Van;
        }

        private static string NormalizeVehicleName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Replace("(Clone)", string.Empty).Trim();
        }

        private static bool HasActiveQuest()
        {
            return QuestManager.Instance != null &&
                   QuestManager.Instance.CurrentQuest != null &&
                   QuestManager.Instance.CurrentQuest.Status == QuestStatus.Active;
        }

        private CameraFollow GetCameraFollow()
        {
            if (cachedCameraFollow == null)
            {
                cachedCameraFollow = FindFirstObjectByType<CameraFollow>();
            }

            return cachedCameraFollow;
        }

        private ReverseCameraHUD GetReverseCameraHud()
        {
            if (cachedReverseCameraHud == null)
            {
                cachedReverseCameraHud = FindFirstObjectByType<ReverseCameraHUD>();
            }

            return cachedReverseCameraHud;
        }

        private DeliveryManager GetDeliveryManager()
        {
            if (cachedDeliveryManager == null)
            {
                cachedDeliveryManager = FindFirstObjectByType<DeliveryManager>();
            }

            return cachedDeliveryManager;
        }

        private DeliveryUI GetDeliveryUi()
        {
            if (cachedDeliveryUi == null)
            {
                cachedDeliveryUi = FindFirstObjectByType<DeliveryUI>();
            }

            return cachedDeliveryUi;
        }

        private CompassUI GetCompassUi()
        {
            if (cachedCompassUi == null)
            {
                cachedCompassUi = FindFirstObjectByType<CompassUI>();
            }

            return cachedCompassUi;
        }

        private NavigationService GetNavigationService()
        {
            if (cachedNavigationService == null)
            {
                cachedNavigationService = NavigationService.Instance ?? NavigationService.EnsureInstance();
            }

            return cachedNavigationService;
        }

        private static bool IsGameSceneActive()
        {
            return SceneManager.GetActiveScene().name.Equals(GameSceneName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
