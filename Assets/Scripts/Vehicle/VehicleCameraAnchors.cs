using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDriver.Vehicle
{
    public class VehicleCameraAnchors : MonoBehaviour
    {
        [SerializeField] private Transform reverseCameraAnchor;
        [SerializeField] private bool autoCreateReverseCameraAnchor = true;
        [SerializeField] private string preferredBoundsRootName = "LorryTrailer";

        [Header("Van (Kamyonet) Kamera Ayarları")]
        [SerializeField] private float vanHeightRatio = 0.45f;
        [SerializeField] private float vanRearPadding = 0.02f;
        [SerializeField] private float vanMinHeight = 0.55f;

        [Header("Truck (Tır/Kamyon) Kamera Ayarları")]
        [SerializeField] private float truckHeightRatio = 0.55f;
        [SerializeField] private float truckRearPadding = 0.03f;
        [SerializeField] private float truckMinHeight = 0.9f;

        private Transform runtimeReverseCameraAnchor;
        private bool detectedAsTruck;

        public Transform ReverseCameraAnchor
        {
            get
            {
                if (reverseCameraAnchor != null)
                {
                    return reverseCameraAnchor;
                }

                if (!autoCreateReverseCameraAnchor)
                {
                    return null;
                }

                if (runtimeReverseCameraAnchor == null)
                {
                    CreateRuntimeReverseCameraAnchor();
                }

                return runtimeReverseCameraAnchor;
            }
        }

        /// <summary>
        /// Araç tipi tır/kamyon olarak algılandıysa true döner.
        /// Anchor oluşturulduktan sonra geçerlidir.
        /// </summary>
        public bool IsTruck => detectedAsTruck;

        private void CreateRuntimeReverseCameraAnchor()
        {
            Transform trailerRoot = FindPreferredBoundsRoot();
            detectedAsTruck = trailerRoot != null && trailerRoot != transform;

            Transform boundsRoot = detectedAsTruck ? trailerRoot : transform;

            if (!TryBuildLocalBounds(boundsRoot, out Bounds localBounds))
            {
                Debug.LogWarning($"[VehicleCameraAnchors] Reverse camera anchor could not be created for '{name}'.");
                return;
            }

            GameObject anchorObject = new GameObject("ReverseCameraAnchor");
            anchorObject.transform.SetParent(transform, false);
            anchorObject.transform.localRotation = Quaternion.identity;

            float heightRatio = detectedAsTruck ? truckHeightRatio : vanHeightRatio;
            float rearPadding = detectedAsTruck ? truckRearPadding : vanRearPadding;
            float minHeight = detectedAsTruck ? truckMinHeight : vanMinHeight;

            float targetHeight = Mathf.Lerp(localBounds.min.y, localBounds.max.y, heightRatio);
            float anchorHeight = Mathf.Max(targetHeight, minHeight);

            Vector3 localPosition = new Vector3(
                0f,
                anchorHeight,
                localBounds.min.z - rearPadding);
            anchorObject.transform.localPosition = localPosition;
            runtimeReverseCameraAnchor = anchorObject.transform;

            Debug.Log($"[VehicleCameraAnchors] Reverse camera anchor created for '{name}': " +
                      $"type={(detectedAsTruck ? "Truck" : "Van")}, " +
                      $"pos={localPosition}, bounds.min.z={localBounds.min.z:F2}, bounds.max.y={localBounds.max.y:F2}");
        }

        private Transform FindPreferredBoundsRoot()
        {
            if (string.IsNullOrWhiteSpace(preferredBoundsRootName))
            {
                return transform;
            }

            Queue<Transform> queue = new Queue<Transform>();
            queue.Enqueue(transform);

            while (queue.Count > 0)
            {
                Transform current = queue.Dequeue();
                if (current.name == preferredBoundsRootName)
                {
                    return current;
                }

                for (int i = 0; i < current.childCount; i++)
                {
                    queue.Enqueue(current.GetChild(i));
                }
            }

            return transform;
        }

        private bool TryBuildLocalBounds(Transform boundsRoot, out Bounds localBounds)
        {
            Renderer[] renderers = boundsRoot.GetComponentsInChildren<Renderer>(true);
            bool hasPoint = false;
            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer.transform == null)
                {
                    continue;
                }

                if (renderer.transform.name.Contains("Wheel"))
                {
                    continue;
                }

                Bounds rendererBounds = renderer.bounds;
                Vector3[] corners = GetWorldCorners(rendererBounds);
                for (int j = 0; j < corners.Length; j++)
                {
                    Vector3 localPoint = transform.InverseTransformPoint(corners[j]);
                    if (!hasPoint)
                    {
                        min = localPoint;
                        max = localPoint;
                        hasPoint = true;
                        continue;
                    }

                    min = Vector3.Min(min, localPoint);
                    max = Vector3.Max(max, localPoint);
                }
            }

            if (!hasPoint)
            {
                localBounds = default;
                return false;
            }

            localBounds = new Bounds((min + max) * 0.5f, max - min);
            return true;
        }

        private static Vector3[] GetWorldCorners(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            return new[]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };
        }
    }
}
