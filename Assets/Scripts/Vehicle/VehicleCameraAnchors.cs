using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDriver.Vehicle
{
    public class VehicleCameraAnchors : MonoBehaviour
    {
        [SerializeField] private Transform reverseCameraAnchor;
        [SerializeField] private bool autoCreateReverseCameraAnchor = true;
        [SerializeField] private string preferredBoundsRootName = "LorryTrailer";
        [SerializeField] private float reverseCameraHeightOffset = 0.35f;
        [SerializeField] private float reverseCameraRearPadding = 0.45f;
        [SerializeField] private float minimumAnchorHeight = 0.85f;

        private Transform runtimeReverseCameraAnchor;

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

        private void CreateRuntimeReverseCameraAnchor()
        {
            Transform boundsRoot = FindPreferredBoundsRoot();
            if (boundsRoot == null)
            {
                boundsRoot = transform;
            }

            if (!TryBuildLocalBounds(boundsRoot, out Bounds localBounds))
            {
                Debug.LogWarning($"[VehicleCameraAnchors] Reverse camera anchor could not be created for '{name}'.");
                return;
            }

            GameObject anchorObject = new GameObject("ReverseCameraAnchor");
            anchorObject.transform.SetParent(transform, false);
            anchorObject.transform.localRotation = Quaternion.identity;

            Vector3 localPosition = new Vector3(
                localBounds.center.x,
                Mathf.Max(localBounds.center.y + reverseCameraHeightOffset, minimumAnchorHeight),
                localBounds.min.z - reverseCameraRearPadding);
            anchorObject.transform.localPosition = localPosition;
            runtimeReverseCameraAnchor = anchorObject.transform;
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
