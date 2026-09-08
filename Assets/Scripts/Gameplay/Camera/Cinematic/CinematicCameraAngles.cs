using System;
using UnityEngine;

namespace DeliveryDriver.Cinematic
{
    public enum CinematicCameraAngle
    {
        GameplayChase = 0,
        HoodBumper = 1,
        WheelDrift = 2,
        DoorMirrorProfile = 3,
        LowRearExhaust = 4,
        HelicopterDrone = 5,
        RoadsideFlyby = 6
    }

    public enum CinematicVideoFormat
    {
        Trailer_16_9 = 0,
        Reels_9_16 = 1
    }

    [Serializable]
    public struct CameraPresetSettings
    {
        public string displayName;
        public Vector3 localOffset;
        public Vector3 localLookAtOffset;
        public float fov16x9;
        public float fov9x16;
        public float positionSmoothTime;
        public float rotationSmoothTime;
        public bool isFixedWorldPosition; // e.g. Roadside flyby
        public bool lookAtTarget;

        public static CameraPresetSettings CreateDefault(CinematicCameraAngle angle)
        {
            switch (angle)
            {
                case CinematicCameraAngle.GameplayChase:
                    return new CameraPresetSettings
                    {
                        displayName = "Cinematic Chase",
                        localOffset = new Vector3(0f, 2.8f, -6.2f),
                        localLookAtOffset = new Vector3(0f, 1.2f, 0.5f),
                        fov16x9 = 58f,
                        fov9x16 = 68f,
                        positionSmoothTime = 0.12f,
                        rotationSmoothTime = 0.14f,
                        isFixedWorldPosition = false,
                        lookAtTarget = true
                    };

                case CinematicCameraAngle.HoodBumper:
                    return new CameraPresetSettings
                    {
                        displayName = "Hood & Bumper",
                        localOffset = new Vector3(0f, 0.85f, 1.8f),
                        localLookAtOffset = new Vector3(0f, 0.7f, 12f),
                        fov16x9 = 75f,
                        fov9x16 = 85f,
                        positionSmoothTime = 0.02f,
                        rotationSmoothTime = 0.03f,
                        isFixedWorldPosition = false,
                        lookAtTarget = true
                    };

                case CinematicCameraAngle.WheelDrift:
                    return new CameraPresetSettings
                    {
                        displayName = "Wheel & Drift",
                        localOffset = new Vector3(-1.65f, 0.45f, -1.2f),
                        localLookAtOffset = new Vector3(-0.9f, 0.35f, -0.6f),
                        fov16x9 = 65f,
                        fov9x16 = 75f,
                        positionSmoothTime = 0.03f,
                        rotationSmoothTime = 0.04f,
                        isFixedWorldPosition = false,
                        lookAtTarget = true
                    };

                case CinematicCameraAngle.DoorMirrorProfile:
                    return new CameraPresetSettings
                    {
                        displayName = "Door Profile",
                        localOffset = new Vector3(1.7f, 1.3f, 0.2f),
                        localLookAtOffset = new Vector3(0.5f, 1.0f, 4.5f),
                        fov16x9 = 60f,
                        fov9x16 = 70f,
                        positionSmoothTime = 0.04f,
                        rotationSmoothTime = 0.05f,
                        isFixedWorldPosition = false,
                        lookAtTarget = true
                    };

                case CinematicCameraAngle.LowRearExhaust:
                    return new CameraPresetSettings
                    {
                        displayName = "Low Exhaust",
                        localOffset = new Vector3(0f, 0.5f, -2.6f),
                        localLookAtOffset = new Vector3(0f, 0.9f, 2.0f),
                        fov16x9 = 78f,
                        fov9x16 = 88f,
                        positionSmoothTime = 0.04f,
                        rotationSmoothTime = 0.05f,
                        isFixedWorldPosition = false,
                        lookAtTarget = true
                    };

                case CinematicCameraAngle.HelicopterDrone:
                    return new CameraPresetSettings
                    {
                        displayName = "Drone / Heli",
                        localOffset = new Vector3(0f, 12.0f, -14.0f),
                        localLookAtOffset = new Vector3(0f, 1.0f, 0f),
                        fov16x9 = 50f,
                        fov9x16 = 60f,
                        positionSmoothTime = 0.35f,
                        rotationSmoothTime = 0.30f,
                        isFixedWorldPosition = false,
                        lookAtTarget = true
                    };

                case CinematicCameraAngle.RoadsideFlyby:
                    return new CameraPresetSettings
                    {
                        displayName = "Roadside Flyby",
                        localOffset = new Vector3(4.5f, 1.2f, 25.0f),
                        localLookAtOffset = Vector3.zero,
                        fov16x9 = 42f,
                        fov9x16 = 50f,
                        positionSmoothTime = 0f,
                        rotationSmoothTime = 0.05f,
                        isFixedWorldPosition = true,
                        lookAtTarget = true
                    };

                default:
                    return default;
            }
        }
    }
}
