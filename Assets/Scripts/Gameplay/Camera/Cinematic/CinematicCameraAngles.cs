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
        public Vector3 localRotationEuler;
        public Vector3 localLookAtOffset;
        public float fov16x9;
        public float fov9x16;
        public float positionSmoothTime;
        public float rotationSmoothTime;
        public bool isChassisMounted;      // Araca kilitli (GoPro/onboard) kamera: Sıfır titreme!
        public bool isFixedWorldPosition;  // Yol kenarı geçiş kamerası
        public bool lookAtTarget;

        public static CameraPresetSettings CreateDefault(CinematicCameraAngle angle)
        {
            switch (angle)
            {
                case CinematicCameraAngle.GameplayChase:
                    return new CameraPresetSettings
                    {
                        displayName = "Cinematic Chase",
                        localOffset = new Vector3(0f, 2.0f, -4.8f),
                        localRotationEuler = Vector3.zero,
                        localLookAtOffset = new Vector3(0f, 1.0f, 0.5f),
                        fov16x9 = 58f,
                        fov9x16 = 68f,
                        positionSmoothTime = 0.08f,
                        rotationSmoothTime = 0.08f,
                        isChassisMounted = false,
                        isFixedWorldPosition = false,
                        lookAtTarget = true
                    };

                case CinematicCameraAngle.HoodBumper:
                    return new CameraPresetSettings
                    {
                        displayName = "Kaput & Tampon (Onboard)",
                        localOffset = new Vector3(0f, 0.85f, 1.8f),
                        localRotationEuler = new Vector3(3f, 0f, 0f),
                        localLookAtOffset = Vector3.zero,
                        fov16x9 = 75f,
                        fov9x16 = 85f,
                        positionSmoothTime = 0f,
                        rotationSmoothTime = 0f,
                        isChassisMounted = true,
                        isFixedWorldPosition = false,
                        lookAtTarget = false
                    };

                case CinematicCameraAngle.WheelDrift:
                    return new CameraPresetSettings
                    {
                        displayName = "Tekerlek & Lastik Dumanı",
                        localOffset = new Vector3(-1.65f, 0.45f, -1.0f),
                        localRotationEuler = new Vector3(5f, 162f, 0f),
                        localLookAtOffset = Vector3.zero,
                        fov16x9 = 65f,
                        fov9x16 = 75f,
                        positionSmoothTime = 0f,
                        rotationSmoothTime = 0f,
                        isChassisMounted = true,
                        isFixedWorldPosition = false,
                        lookAtTarget = false
                    };

                case CinematicCameraAngle.DoorMirrorProfile:
                    return new CameraPresetSettings
                    {
                        displayName = "Kapı Profili & Yan Ayna",
                        localOffset = new Vector3(1.65f, 1.25f, 0.15f),
                        localRotationEuler = new Vector3(2f, 192f, 0f),
                        localLookAtOffset = Vector3.zero,
                        fov16x9 = 62f,
                        fov9x16 = 72f,
                        positionSmoothTime = 0f,
                        rotationSmoothTime = 0f,
                        isChassisMounted = true,
                        isFixedWorldPosition = false,
                        lookAtTarget = false
                    };

                case CinematicCameraAngle.LowRearExhaust:
                    // Kullanıcı isteği: "alçak egzoz da geri görüş kamerası gibi olsun"
                    // Aracın arkasında yere yakın, 180 derece geriye ve akan yola bakan geri görüş açısı
                    return new CameraPresetSettings
                    {
                        displayName = "Geri Görüş & Egzoz (Rear Bumper)",
                        localOffset = new Vector3(0f, 0.65f, -2.1f),
                        localRotationEuler = new Vector3(10f, 180f, 0f),
                        localLookAtOffset = Vector3.zero,
                        fov16x9 = 78f,
                        fov9x16 = 88f,
                        positionSmoothTime = 0f,
                        rotationSmoothTime = 0f,
                        isChassisMounted = true,
                        isFixedWorldPosition = false,
                        lookAtTarget = false
                    };

                case CinematicCameraAngle.HelicopterDrone:
                    // Kullanıcı isteği: "Ve drone çekim polis helikopteri misali bir çekim olsun."
                    // Havadan takip: Aracı net gösterecek yükseklik (7.5m), stabil takip açısı ve aksiyon kadrajı
                    return new CameraPresetSettings
                    {
                        displayName = "Polis Helikopteri Takibi (Air Unit)",
                        localOffset = new Vector3(3.8f, 7.5f, -10.5f),
                        localRotationEuler = Vector3.zero,
                        localLookAtOffset = new Vector3(0f, 0.8f, 0f),
                        fov16x9 = 42f,
                        fov9x16 = 52f,
                        positionSmoothTime = 0.35f,
                        rotationSmoothTime = 0.15f,
                        isChassisMounted = false,
                        isFixedWorldPosition = false,
                        lookAtTarget = true
                    };

                case CinematicCameraAngle.RoadsideFlyby:
                    return new CameraPresetSettings
                    {
                        displayName = "Yol Kenarı Geçiş (Roadside Flyby)",
                        localOffset = new Vector3(3.5f, 1.1f, 14.0f),
                        localRotationEuler = Vector3.zero,
                        localLookAtOffset = new Vector3(0f, 0.7f, 0f),
                        fov16x9 = 32f,
                        fov9x16 = 40f,
                        positionSmoothTime = 0f,
                        rotationSmoothTime = 0.05f,
                        isChassisMounted = false,
                        isFixedWorldPosition = true,
                        lookAtTarget = true
                    };

                default:
                    return default;
            }
        }
    }
}
