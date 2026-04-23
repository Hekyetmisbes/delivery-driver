using UnityEngine;
using System.Collections.Generic;

namespace TrafficSystem
{
    internal readonly struct NpcCarWheelVisualSet
    {
        public NpcCarWheelVisualSet(
            Transform frontLeft,
            Transform frontRight,
            Transform rearLeft,
            Transform rearRight)
        {
            FrontLeft = frontLeft;
            FrontRight = frontRight;
            RearLeft = rearLeft;
            RearRight = rearRight;
        }

        public Transform FrontLeft { get; }
        public Transform FrontRight { get; }
        public Transform RearLeft { get; }
        public Transform RearRight { get; }
    }

    internal sealed class NpcCarWheelVisualService
    {
        private readonly struct WheelVisualOffset
        {
            public WheelVisualOffset(Vector3 localPositionOffset, Quaternion localRotationOffset)
            {
                LocalPositionOffset = localPositionOffset;
                LocalRotationOffset = localRotationOffset;
            }

            public Vector3 LocalPositionOffset { get; }
            public Quaternion LocalRotationOffset { get; }
        }

        private readonly bool autoSetupWheelVisuals;
        private readonly bool autoCreateWheelVisualsIfMissing;
        private readonly Dictionary<Transform, WheelVisualOffset> cachedOffsets = new Dictionary<Transform, WheelVisualOffset>();

        public NpcCarWheelVisualService(bool autoSetupWheelVisuals, bool autoCreateWheelVisualsIfMissing)
        {
            this.autoSetupWheelVisuals = autoSetupWheelVisuals;
            this.autoCreateWheelVisualsIfMissing = autoCreateWheelVisualsIfMissing;
        }

        public NpcCarWheelVisualSet ResolveWheelVisuals(
            WheelCollider frontLeftCollider,
            Transform frontLeftWheelVisual,
            WheelCollider frontRightCollider,
            Transform frontRightWheelVisual,
            WheelCollider rearLeftCollider,
            Transform rearLeftWheelVisual,
            WheelCollider rearRightCollider,
            Transform rearRightWheelVisual)
        {
            if (!autoSetupWheelVisuals)
            {
                CacheWheelVisualOffset(frontLeftCollider, frontLeftWheelVisual);
                CacheWheelVisualOffset(frontRightCollider, frontRightWheelVisual);
                CacheWheelVisualOffset(rearLeftCollider, rearLeftWheelVisual);
                CacheWheelVisualOffset(rearRightCollider, rearRightWheelVisual);

                return new NpcCarWheelVisualSet(
                    frontLeftWheelVisual,
                    frontRightWheelVisual,
                    rearLeftWheelVisual,
                    rearRightWheelVisual);
            }

            Transform resolvedFrontLeft = ResolveWheelVisual(frontLeftCollider, frontLeftWheelVisual);
            Transform resolvedFrontRight = ResolveWheelVisual(frontRightCollider, frontRightWheelVisual);
            Transform resolvedRearLeft = ResolveWheelVisual(rearLeftCollider, rearLeftWheelVisual);
            Transform resolvedRearRight = ResolveWheelVisual(rearRightCollider, rearRightWheelVisual);

            CacheWheelVisualOffset(frontLeftCollider, resolvedFrontLeft);
            CacheWheelVisualOffset(frontRightCollider, resolvedFrontRight);
            CacheWheelVisualOffset(rearLeftCollider, resolvedRearLeft);
            CacheWheelVisualOffset(rearRightCollider, resolvedRearRight);

            return new NpcCarWheelVisualSet(
                resolvedFrontLeft,
                resolvedFrontRight,
                resolvedRearLeft,
                resolvedRearRight);
        }

        public void UpdateWheelVisuals(
            WheelCollider frontLeftCollider,
            Transform frontLeftWheelVisual,
            WheelCollider frontRightCollider,
            Transform frontRightWheelVisual,
            WheelCollider rearLeftCollider,
            Transform rearLeftWheelVisual,
            WheelCollider rearRightCollider,
            Transform rearRightWheelVisual)
        {
            UpdateSingleWheelVisual(frontLeftCollider, frontLeftWheelVisual, true);
            UpdateSingleWheelVisual(frontRightCollider, frontRightWheelVisual, true);
            UpdateSingleWheelVisual(rearLeftCollider, rearLeftWheelVisual, true);
            UpdateSingleWheelVisual(rearRightCollider, rearRightWheelVisual, true);
        }

        private Transform ResolveWheelVisual(WheelCollider wheelCollider, Transform currentVisual)
        {
            if (currentVisual != null)
            {
                return currentVisual;
            }

            if (wheelCollider == null)
            {
                return null;
            }

            Transform wheelRoot = wheelCollider.transform;
            for (int i = 0; i < wheelRoot.childCount; i++)
            {
                Transform child = wheelRoot.GetChild(i);
                if (child.GetComponent<MeshRenderer>() != null)
                {
                    return child;
                }
            }

            if (!autoCreateWheelVisualsIfMissing)
            {
                return null;
            }

            MeshRenderer renderer = wheelRoot.GetComponent<MeshRenderer>();
            MeshFilter filter = wheelRoot.GetComponent<MeshFilter>();
            if (renderer == null || filter == null || filter.sharedMesh == null)
            {
                return null;
            }

            GameObject visual = new GameObject("WheelVisual");
            visual.transform.SetParent(wheelRoot, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            MeshFilter visualFilter = visual.AddComponent<MeshFilter>();
            MeshRenderer visualRenderer = visual.AddComponent<MeshRenderer>();
            visualFilter.sharedMesh = filter.sharedMesh;
            visualRenderer.sharedMaterials = renderer.sharedMaterials;
            renderer.enabled = false;

            return visual.transform;
        }

        private void CacheWheelVisualOffset(WheelCollider wheelCollider, Transform wheelVisual)
        {
            if (wheelCollider == null || wheelVisual == null)
            {
                return;
            }

            Transform wheelRoot = wheelCollider.transform;
            Vector3 localPositionOffset = Quaternion.Inverse(wheelRoot.rotation) * (wheelVisual.position - wheelRoot.position);
            Quaternion localRotationOffset = Quaternion.Inverse(wheelRoot.rotation) * wheelVisual.rotation;
            cachedOffsets[wheelVisual] = new WheelVisualOffset(localPositionOffset, localRotationOffset);
        }

        private void UpdateSingleWheelVisual(WheelCollider wheelCollider, Transform wheelVisual, bool useCachedOffset)
        {
            if (wheelCollider == null || wheelVisual == null)
            {
                return;
            }

            wheelCollider.GetWorldPose(out Vector3 position, out Quaternion rotation);

            if (useCachedOffset && cachedOffsets.TryGetValue(wheelVisual, out WheelVisualOffset offset))
            {
                Quaternion offsetRotation = GetWheelOffsetRotation(wheelCollider);
                wheelVisual.position = position + (offsetRotation * offset.LocalPositionOffset);
                wheelVisual.rotation = rotation * offset.LocalRotationOffset;
                return;
            }

            wheelVisual.position = position;
            wheelVisual.rotation = rotation;
        }

        private static Quaternion GetWheelOffsetRotation(WheelCollider wheelCollider)
        {
            Transform wheelRoot = wheelCollider.transform;
            return wheelRoot.rotation * Quaternion.Euler(0f, wheelCollider.steerAngle, 0f);
        }
    }
}
