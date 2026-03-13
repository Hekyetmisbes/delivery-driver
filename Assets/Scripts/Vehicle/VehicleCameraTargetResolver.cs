using DeliveryDriver.Company;
using DeliveryDriver.Quest;
using UnityEngine;

namespace DeliveryDriver.Vehicle
{
    public readonly struct VehicleCameraBinding
    {
        public VehicleCameraBinding(Transform target, Rigidbody rigidbody, CarController carController, VehicleCameraAnchors cameraAnchors)
        {
            Target = target;
            Rigidbody = rigidbody;
            CarController = carController;
            CameraAnchors = cameraAnchors;
        }

        public Transform Target { get; }
        public Rigidbody Rigidbody { get; }
        public CarController CarController { get; }
        public VehicleCameraAnchors CameraAnchors { get; }
        public bool IsValid => Target != null;
    }

    public static class VehicleCameraTargetResolver
    {
        public static VehicleCameraBinding Resolve(Transform candidate)
        {
            if (candidate == null)
            {
                return default;
            }

            CarController controller = candidate.GetComponent<CarController>()
                ?? candidate.GetComponentInParent<CarController>()
                ?? candidate.GetComponentInChildren<CarController>();

            Transform resolvedTarget = controller != null ? controller.transform : candidate;
            Rigidbody rigidbody = resolvedTarget.GetComponent<Rigidbody>()
                ?? resolvedTarget.GetComponentInParent<Rigidbody>()
                ?? resolvedTarget.GetComponentInChildren<Rigidbody>();

            VehicleCameraAnchors cameraAnchors = resolvedTarget.GetComponent<VehicleCameraAnchors>()
                ?? resolvedTarget.GetComponentInParent<VehicleCameraAnchors>()
                ?? resolvedTarget.GetComponentInChildren<VehicleCameraAnchors>();

            return new VehicleCameraBinding(resolvedTarget, rigidbody, controller, cameraAnchors);
        }

        public static Transform ResolveDefaultTarget()
        {
            PlayerVehicleManager vehicleManager = Object.FindFirstObjectByType<PlayerVehicleManager>();
            if (vehicleManager != null && vehicleManager.ActiveVehicleController != null)
            {
                return vehicleManager.ActiveVehicleController.transform;
            }

            if (QuestManager.Instance != null && QuestManager.Instance.PlayerTransform != null)
            {
                return QuestManager.Instance.PlayerTransform;
            }

            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
            {
                VehicleCameraBinding taggedBinding = Resolve(taggedPlayer.transform);
                if (taggedBinding.IsValid)
                {
                    return taggedBinding.Target;
                }
            }

            CarController controller = Object.FindFirstObjectByType<CarController>();
            return controller != null ? controller.transform : null;
        }
    }
}
