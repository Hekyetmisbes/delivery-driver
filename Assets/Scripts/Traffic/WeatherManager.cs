using UnityEngine;

namespace TrafficSystem
{
    /// <summary>
    /// Global weather manager that affects NPC driving behavior
    /// Controls weather conditions and their impact on traffic
    /// Priority 3: Environmental Awareness - Weather Effects
    /// </summary>
    public class WeatherManager : MonoBehaviour
    {
        public static WeatherManager Instance { get; private set; }

        [Header("Current Weather")]
        [Tooltip("Current weather condition")]
        [SerializeField] private WeatherCondition currentWeather = WeatherCondition.Clear;

        [Header("Weather Effects")]
        [Tooltip("Speed reduction in rain (multiplier)")]
        [SerializeField] private float rainSpeedReduction = 0.85f;

        [Tooltip("Speed reduction in snow (multiplier)")]
        [SerializeField] private float snowSpeedReduction = 0.7f;

        [Tooltip("Speed reduction in fog (multiplier)")]
        [SerializeField] private float fogSpeedReduction = 0.75f;

        [Tooltip("Visibility range in clear weather (meters)")]
        [SerializeField] private float clearVisibility = 100f;

        [Tooltip("Visibility range in rain (meters)")]
        [SerializeField] private float rainVisibility = 60f;

        [Tooltip("Visibility range in snow (meters)")]
        [SerializeField] private float snowVisibility = 50f;

        [Tooltip("Visibility range in fog (meters)")]
        [SerializeField] private float fogVisibility = 30f;

        [Tooltip("Traction in clear weather")]
        [SerializeField] private float clearTraction = 1.0f;

        [Tooltip("Traction in rain (0-1)")]
        [SerializeField] private float rainTraction = 0.75f;

        [Tooltip("Traction in snow (0-1)")]
        [SerializeField] private float snowTraction = 0.5f;

        [Tooltip("Traction in fog (same as clear)")]
        [SerializeField] private float fogTraction = 1.0f;

        [Header("Weather Transition")]
        [Tooltip("Enable automatic weather changes")]
        [SerializeField] private bool autoChangeWeather = false;

        [Tooltip("Time between weather changes (seconds)")]
        [SerializeField] private float weatherChangeInterval = 300f; // 5 minutes

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        private float nextWeatherChangeTime;

        private void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            nextWeatherChangeTime = Time.time + weatherChangeInterval;
        }

        private void Update()
        {
            if (autoChangeWeather && Time.time >= nextWeatherChangeTime)
            {
                ChangeWeatherRandomly();
                nextWeatherChangeTime = Time.time + weatherChangeInterval;
            }
        }

        /// <summary>
        /// Get speed reduction multiplier for current weather
        /// </summary>
        public float GetSpeedReduction()
        {
            switch (currentWeather)
            {
                case WeatherCondition.Rain:
                    return rainSpeedReduction;
                case WeatherCondition.Snow:
                    return snowSpeedReduction;
                case WeatherCondition.Fog:
                    return fogSpeedReduction;
                case WeatherCondition.Clear:
                default:
                    return 1.0f;
            }
        }

        /// <summary>
        /// Get visibility range for current weather
        /// </summary>
        public float GetVisibilityRange()
        {
            switch (currentWeather)
            {
                case WeatherCondition.Rain:
                    return rainVisibility;
                case WeatherCondition.Snow:
                    return snowVisibility;
                case WeatherCondition.Fog:
                    return fogVisibility;
                case WeatherCondition.Clear:
                default:
                    return clearVisibility;
            }
        }

        /// <summary>
        /// Get traction multiplier for current weather
        /// </summary>
        public float GetTractionMultiplier()
        {
            switch (currentWeather)
            {
                case WeatherCondition.Rain:
                    return rainTraction;
                case WeatherCondition.Snow:
                    return snowTraction;
                case WeatherCondition.Fog:
                    return fogTraction;
                case WeatherCondition.Clear:
                default:
                    return clearTraction;
            }
        }

        /// <summary>
        /// Get following distance multiplier for current weather
        /// </summary>
        public float GetFollowingDistanceMultiplier()
        {
            switch (currentWeather)
            {
                case WeatherCondition.Rain:
                    return 1.3f; // 30% more distance
                case WeatherCondition.Snow:
                    return 1.5f; // 50% more distance
                case WeatherCondition.Fog:
                    return 1.4f; // 40% more distance
                case WeatherCondition.Clear:
                default:
                    return 1.0f;
            }
        }

        /// <summary>
        /// Get steering smoothing multiplier for current weather
        /// </summary>
        public float GetSteeringSmoothingMultiplier()
        {
            switch (currentWeather)
            {
                case WeatherCondition.Rain:
                case WeatherCondition.Snow:
                case WeatherCondition.Fog:
                    return 1.5f; // Gentler steering
                case WeatherCondition.Clear:
                default:
                    return 1.0f;
            }
        }

        /// <summary>
        /// Check if weather is bad (not clear)
        /// </summary>
        public bool IsBadWeather()
        {
            return currentWeather != WeatherCondition.Clear;
        }

        /// <summary>
        /// Set weather condition
        /// </summary>
        public void SetWeather(WeatherCondition weather)
        {
            if (currentWeather != weather)
            {
                currentWeather = weather;
                Debug.Log($"[WeatherManager] Weather changed to: {weather}");
            }
        }

        /// <summary>
        /// Change weather randomly
        /// </summary>
        public void ChangeWeatherRandomly()
        {
            WeatherCondition[] conditions = System.Enum.GetValues(typeof(WeatherCondition)) as WeatherCondition[];
            WeatherCondition newWeather = conditions[Random.Range(0, conditions.Length)];
            SetWeather(newWeather);
        }

        /// <summary>
        /// Get current weather condition
        /// </summary>
        public WeatherCondition GetCurrentWeather()
        {
            return currentWeather;
        }

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, 420, 300, 120));
            GUI.color = Color.yellow;
            GUILayout.Label("<b>WEATHER SYSTEM</b>");
            GUILayout.Label($"Condition: {currentWeather}");
            GUILayout.Label($"Speed Reduction: {GetSpeedReduction():P0}");
            GUILayout.Label($"Visibility: {GetVisibilityRange():F0}m");
            GUILayout.Label($"Traction: {GetTractionMultiplier():P0}");

            if (GUILayout.Button("Change Weather"))
            {
                ChangeWeatherRandomly();
            }
            GUILayout.EndArea();
        }
    }

    /// <summary>
    /// Weather condition types
    /// </summary>
    public enum WeatherCondition
    {
        Clear,
        Rain,
        Snow,
        Fog
    }
}
