using UnityEngine;

namespace TrafficSystem
{
    /// <summary>
    /// Defines comprehensive driving personality for NPC vehicles
    /// Controls speed preferences, aggressiveness, and rule-following behavior
    /// Priority 2: Personality & Variation
    /// </summary>
    [System.Serializable]
    public class DrivingPersonality
    {
        [Header("Speed Preferences")]
        [Tooltip("Speed multiplier (0.8 = 20% slower, 1.3 = 30% faster)")]
        [Range(0.8f, 1.3f)]
        public float speedMultiplier = 1.0f;

        [Tooltip("Following distance multiplier (0.5 = closer, 2.0 = farther)")]
        [Range(0.5f, 2.0f)]
        public float followingDistanceMultiplier = 1.0f;

        [Header("Aggressiveness")]
        [Tooltip("Lane change frequency multiplier (0.5 = less frequent, 2.0 = more frequent)")]
        [Range(0.5f, 2.0f)]
        public float laneChangeFrequency = 1.0f;

        [Tooltip("Risk tolerance (0.0 = very cautious, 1.0 = very aggressive)")]
        [Range(0.0f, 1.0f)]
        public float riskTolerance = 0.5f;

        [Tooltip("Gap acceptance threshold in seconds (2.0 = tight gaps, 5.0 = large gaps)")]
        [Range(2.0f, 5.0f)]
        public float gapAcceptanceThreshold = 3.0f;

        [Header("Response Characteristics")]
        [Tooltip("Reaction time in seconds (0.3 = quick, 1.0 = slow)")]
        [Range(0.3f, 1.0f)]
        public float reactionTime = 0.5f;

        [Tooltip("Acceleration aggressiveness (0.7 = gentle, 1.5 = aggressive)")]
        [Range(0.7f, 1.5f)]
        public float accelerationAggression = 1.0f;

        [Tooltip("Braking aggressiveness (0.7 = gentle, 1.5 = hard)")]
        [Range(0.7f, 1.5f)]
        public float brakingAggression = 1.0f;

        [Header("Rule Following")]
        [Tooltip("Speed limit compliance (0.9 = under limit, 1.3 = over limit)")]
        [Range(0.9f, 1.3f)]
        public float speedLimitCompliance = 1.0f;

        [Tooltip("Signal usage reliability (0.7 = sometimes forgets, 1.0 = always uses)")]
        [Range(0.7f, 1.0f)]
        public float signalUsageReliability = 1.0f;

        [Tooltip("Strictly follows all traffic rules")]
        public bool strictRuleFollowing = true;

        [Header("Visual Identification")]
        [Tooltip("Personality type name for debugging")]
        public string personalityName = "Normal";

        /// <summary>
        /// Create a cautious driver personality
        /// </summary>
        public static DrivingPersonality CreateCautious()
        {
            return new DrivingPersonality
            {
                personalityName = "Cautious",
                speedMultiplier = 0.85f,
                followingDistanceMultiplier = 1.5f,
                laneChangeFrequency = 0.6f,
                riskTolerance = 0.2f,
                gapAcceptanceThreshold = 4.5f,
                reactionTime = 0.4f,
                accelerationAggression = 0.75f,
                brakingAggression = 0.8f,
                speedLimitCompliance = 0.95f,
                signalUsageReliability = 1.0f,
                strictRuleFollowing = true
            };
        }

        /// <summary>
        /// Create an aggressive driver personality
        /// </summary>
        public static DrivingPersonality CreateAggressive()
        {
            return new DrivingPersonality
            {
                personalityName = "Aggressive",
                speedMultiplier = 1.2f,
                followingDistanceMultiplier = 0.7f,
                laneChangeFrequency = 1.8f,
                riskTolerance = 0.8f,
                gapAcceptanceThreshold = 2.5f,
                reactionTime = 0.35f,
                accelerationAggression = 1.4f,
                brakingAggression = 1.3f,
                speedLimitCompliance = 1.2f,
                signalUsageReliability = 0.8f,
                strictRuleFollowing = false
            };
        }

        /// <summary>
        /// Create a normal driver personality
        /// </summary>
        public static DrivingPersonality CreateNormal()
        {
            return new DrivingPersonality
            {
                personalityName = "Normal",
                speedMultiplier = 1.0f,
                followingDistanceMultiplier = 1.0f,
                laneChangeFrequency = 1.0f,
                riskTolerance = 0.5f,
                gapAcceptanceThreshold = 3.0f,
                reactionTime = 0.5f,
                accelerationAggression = 1.0f,
                brakingAggression = 1.0f,
                speedLimitCompliance = 1.05f,
                signalUsageReliability = 0.95f,
                strictRuleFollowing = true
            };
        }

        /// <summary>
        /// Create a professional driver personality (trucks, buses)
        /// </summary>
        public static DrivingPersonality CreateProfessional()
        {
            return new DrivingPersonality
            {
                personalityName = "Professional",
                speedMultiplier = 0.9f,
                followingDistanceMultiplier = 1.8f,
                laneChangeFrequency = 0.5f,
                riskTolerance = 0.3f,
                gapAcceptanceThreshold = 4.0f,
                reactionTime = 0.45f,
                accelerationAggression = 0.8f,
                brakingAggression = 0.85f,
                speedLimitCompliance = 1.0f,
                signalUsageReliability = 1.0f,
                strictRuleFollowing = true
            };
        }

        /// <summary>
        /// Create a random personality with variation
        /// </summary>
        public static DrivingPersonality CreateRandom()
        {
            float roll = Random.value;

            // 40% normal, 30% cautious, 20% aggressive, 10% professional
            if (roll < 0.4f)
            {
                return CreateNormal();
            }
            else if (roll < 0.7f)
            {
                return CreateCautious();
            }
            else if (roll < 0.9f)
            {
                return CreateAggressive();
            }
            else
            {
                return CreateProfessional();
            }
        }

        /// <summary>
        /// Create a random personality with slight variations to add uniqueness
        /// </summary>
        public static DrivingPersonality CreateRandomVaried()
        {
            DrivingPersonality personality = CreateRandom();

            // Add slight random variation (±10%) to make each vehicle more unique
            float variation = 0.1f;
            personality.speedMultiplier *= Random.Range(1f - variation, 1f + variation);
            personality.followingDistanceMultiplier *= Random.Range(1f - variation, 1f + variation);
            personality.laneChangeFrequency *= Random.Range(1f - variation, 1f + variation);

            // Clamp to valid ranges
            personality.speedMultiplier = Mathf.Clamp(personality.speedMultiplier, 0.8f, 1.3f);
            personality.followingDistanceMultiplier = Mathf.Clamp(personality.followingDistanceMultiplier, 0.5f, 2.0f);
            personality.laneChangeFrequency = Mathf.Clamp(personality.laneChangeFrequency, 0.5f, 2.0f);

            return personality;
        }

        /// <summary>
        /// Get a description of this personality for debugging
        /// </summary>
        public string GetDescription()
        {
            return $"{personalityName}: Speed {speedMultiplier:F2}x, Following {followingDistanceMultiplier:F2}x, " +
                   $"Risk {riskTolerance:F2}, Reaction {reactionTime:F2}s";
        }
    }
}
