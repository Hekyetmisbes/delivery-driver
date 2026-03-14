using UnityEngine;

namespace TrafficSystem
{
    /// <summary>
    /// Controls turn signal lights for NPC vehicles
    /// Provides visual indicators for lane changes and turns
    /// </summary>
    public class TurnSignalController : MonoBehaviour
    {
        [Header("Signal Lights")]
        [Tooltip("Left turn signal light")]
        [SerializeField] private Light leftSignalLight;
        [Tooltip("Right turn signal light")]
        [SerializeField] private Light rightSignalLight;
        [Tooltip("Auto-find signal lights in children")]
        [SerializeField] private bool autoFindLights = true;

        [Header("Signal Settings")]
        [Tooltip("Blink frequency (Hz)")]
        [SerializeField] private float blinkFrequency = 1.5f;
        [Tooltip("Signal light color")]
        [SerializeField] private Color signalColor = new Color(1f, 0.6f, 0f); // Orange
        [Tooltip("Signal light intensity")]
        [SerializeField] private float signalIntensity = 2f;
        [Tooltip("Signal light range (meters)")]
        [SerializeField] private float signalRange = 10f;

        // Runtime state
        private bool leftActive;
        private bool rightActive;
        private float blinkTimer;
        private bool blinkState;

        public bool IsLeftActive => leftActive;
        public bool IsRightActive => rightActive;

        private void Awake()
        {
            if (autoFindLights)
            {
                AutoSetupLights();
            }
        }

        private void Update()
        {
            if (!leftActive && !rightActive)
            {
                blinkTimer = 0f;
                blinkState = false;
                return;
            }

            // Update blink timer
            blinkTimer += Time.deltaTime;
            float blinkInterval = 1f / blinkFrequency;

            if (blinkTimer >= blinkInterval)
            {
                blinkTimer = 0f;
                blinkState = !blinkState;
            }

            // Apply blink state to lights
            if (leftActive && leftSignalLight != null)
            {
                leftSignalLight.enabled = blinkState;
            }

            if (rightActive && rightSignalLight != null)
            {
                rightSignalLight.enabled = blinkState;
            }
        }

        /// <summary>
        /// Activate left turn signal
        /// </summary>
        public void ActivateLeft()
        {
            leftActive = true;
            rightActive = false;
            blinkTimer = 0f;
            blinkState = true;

            if (leftSignalLight != null)
            {
                leftSignalLight.enabled = true;
            }
            if (rightSignalLight != null)
            {
                rightSignalLight.enabled = false;
            }
        }

        /// <summary>
        /// Activate right turn signal
        /// </summary>
        public void ActivateRight()
        {
            rightActive = true;
            leftActive = false;
            blinkTimer = 0f;
            blinkState = true;

            if (rightSignalLight != null)
            {
                rightSignalLight.enabled = true;
            }
            if (leftSignalLight != null)
            {
                leftSignalLight.enabled = false;
            }
        }

        /// <summary>
        /// Deactivate all signals
        /// </summary>
        public void DeactivateAll()
        {
            leftActive = false;
            rightActive = false;
            blinkTimer = 0f;
            blinkState = false;

            if (leftSignalLight != null)
            {
                leftSignalLight.enabled = false;
            }
            if (rightSignalLight != null)
            {
                rightSignalLight.enabled = false;
            }
        }

        /// <summary>
        /// Auto-setup: find or create signal lights
        /// </summary>
        private void AutoSetupLights()
        {
            // Try to find existing lights by name
            Transform[] children = GetComponentsInChildren<Transform>();
            foreach (Transform child in children)
            {
                string nameLower = child.name.ToLower();

                if (leftSignalLight == null && (nameLower.Contains("left") && nameLower.Contains("signal")))
                {
                    leftSignalLight = child.GetComponent<Light>();
                }

                if (rightSignalLight == null && (nameLower.Contains("right") && nameLower.Contains("signal")))
                {
                    rightSignalLight = child.GetComponent<Light>();
                }
            }

            // Create lights if not found (optional, commented out for now)
            // This would require knowing the vehicle's structure

            // Configure existing lights
            ConfigureLight(leftSignalLight);
            ConfigureLight(rightSignalLight);
        }

        /// <summary>
        /// Configure a signal light with default settings
        /// </summary>
        private void ConfigureLight(Light light)
        {
            if (light == null) return;

            light.type = LightType.Point;
            light.color = signalColor;
            light.intensity = signalIntensity;
            light.range = signalRange;
            light.enabled = false;
        }

        /// <summary>
        /// Manually set signal light references
        /// </summary>
        public void SetSignalLights(Light left, Light right)
        {
            leftSignalLight = left;
            rightSignalLight = right;

            ConfigureLight(leftSignalLight);
            ConfigureLight(rightSignalLight);
        }
    }
}
