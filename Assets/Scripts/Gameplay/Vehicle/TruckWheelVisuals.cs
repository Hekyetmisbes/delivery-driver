using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDriver.Vehicle
{
    public class TruckWheelVisuals : MonoBehaviour
    {
        [Serializable]
        private sealed class WheelBinding
        {
            public WheelCollider Collider;
            public Transform Visual;
        }

        [SerializeField] private List<WheelBinding> trailerWheelBindings = new List<WheelBinding>();

        private readonly HashSet<WheelCollider> excludedColliders = new HashSet<WheelCollider>();
        private readonly HashSet<Transform> excludedVisuals = new HashSet<Transform>();
        private bool initialized;

        public void Initialize(CarController controller)
        {
            trailerWheelBindings.Clear();
            excludedColliders.Clear();
            excludedVisuals.Clear();

            if (controller == null)
            {
                Debug.LogError("[TruckWheelVisuals] CarController is required to initialize trailer wheel visuals.");
                initialized = false;
                return;
            }

            ExcludePrimaryWheel(controller.frontLeftCollider, controller.frontLeftMesh);
            ExcludePrimaryWheel(controller.frontRightCollider, controller.frontRightMesh);
            ExcludePrimaryWheel(controller.rearLeftCollider, controller.rearLeftMesh);
            ExcludePrimaryWheel(controller.rearRightCollider, controller.rearRightMesh);

            List<WheelCollider> extraColliders = CollectExtraWheelColliders();
            List<Transform> candidateVisuals = CollectExtraWheelVisuals();

            for (int i = 0; i < extraColliders.Count; i++)
            {
                WheelCollider wheelCollider = extraColliders[i];
                Transform bestVisual = FindBestVisual(wheelCollider, candidateVisuals);
                if (bestVisual == null)
                {
                    Debug.LogWarning($"[TruckWheelVisuals] No matching visual wheel mesh found for extra collider '{wheelCollider.name}'.");
                    continue;
                }

                candidateVisuals.Remove(bestVisual);
                trailerWheelBindings.Add(new WheelBinding
                {
                    Collider = wheelCollider,
                    Visual = bestVisual
                });
            }

            initialized = trailerWheelBindings.Count > 0;
            if (!initialized)
            {
                Debug.LogWarning("[TruckWheelVisuals] No trailer wheel bindings were created for this truck.");
            }
        }

        private void LateUpdate()
        {
            if (!initialized)
            {
                return;
            }

            for (int i = 0; i < trailerWheelBindings.Count; i++)
            {
                WheelBinding binding = trailerWheelBindings[i];
                if (binding == null || binding.Collider == null || binding.Visual == null)
                {
                    continue;
                }

                Vector3 position;
                Quaternion rotation;
                binding.Collider.GetWorldPose(out position, out rotation);
                binding.Visual.position = position;
                binding.Visual.rotation = rotation;
            }
        }

        private void ExcludePrimaryWheel(WheelCollider wheelCollider, Transform wheelVisual)
        {
            if (wheelCollider != null)
            {
                excludedColliders.Add(wheelCollider);
            }

            if (wheelVisual != null)
            {
                excludedVisuals.Add(wheelVisual);
            }
        }

        private List<WheelCollider> CollectExtraWheelColliders()
        {
            WheelCollider[] allColliders = GetComponentsInChildren<WheelCollider>(true);
            List<WheelCollider> extraColliders = new List<WheelCollider>();
            for (int i = 0; i < allColliders.Length; i++)
            {
                WheelCollider wheelCollider = allColliders[i];
                if (wheelCollider == null || excludedColliders.Contains(wheelCollider))
                {
                    continue;
                }

                extraColliders.Add(wheelCollider);
            }

            return extraColliders;
        }

        private List<Transform> CollectExtraWheelVisuals()
        {
            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
            List<Transform> visuals = new List<Transform>();
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (renderer == null || renderer.transform == null)
                {
                    continue;
                }

                Transform candidate = renderer.transform;
                if (excludedVisuals.Contains(candidate))
                {
                    continue;
                }

                if (!candidate.name.Contains("Wheel", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                visuals.Add(candidate);
            }

            return visuals;
        }

        private Transform FindBestVisual(WheelCollider wheelCollider, List<Transform> candidates)
        {
            Transform best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                Transform candidate = candidates[i];
                if (candidate == null)
                {
                    continue;
                }

                float distance = (wheelCollider.transform.position - candidate.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }
    }
}
