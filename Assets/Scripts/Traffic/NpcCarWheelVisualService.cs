using UnityEngine;

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
        private readonly bool autoSetupWheelVisuals;
        private readonly bool autoCreateWheelVisualsIfMissing;

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
                return new NpcCarWheelVisualSet(
                    frontLeftWheelVisual,
                    frontRightWheelVisual,
                    rearLeftWheelVisual,
                    rearRightWheelVisual);
            }

            return new NpcCarWheelVisualSet(
                ResolveWheelVisual(frontLeftCollider, frontLeftWheelVisual),
                ResolveWheelVisual(frontRightCollider, frontRightWheelVisual),
                ResolveWheelVisual(rearLeftCollider, rearLeftWheelVisual),
                ResolveWheelVisual(rearRightCollider, rearRightWheelVisual));
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
            UpdateSingleWheelVisual(frontLeftCollider, frontLeftWheelVisual);
            UpdateSingleWheelVisual(frontRightCollider, frontRightWheelVisual);
            UpdateSingleWheelVisual(rearLeftCollider, rearLeftWheelVisual);
            UpdateSingleWheelVisual(rearRightCollider, rearRightWheelVisual);
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

        private static void UpdateSingleWheelVisual(WheelCollider wheelCollider, Transform wheelVisual)
        {
            if (wheelCollider == null || wheelVisual == null)
            {
                return;
            }

            wheelCollider.GetWorldPose(out Vector3 position, out Quaternion rotation);
            wheelVisual.position = position;
            wheelVisual.rotation = rotation;
        }
    }
}
