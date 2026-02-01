using UnityEngine;

namespace TrafficSystem
{
    /// <summary>
    /// Predicts future positions and trajectories of vehicles
    /// Used for anticipatory collision avoidance
    /// Priority 2: Predictive Behavior - Trajectory Prediction
    /// </summary>
    public static class VehicleTrajectoryPredictor
    {
        /// <summary>
        /// Predict future position of a vehicle after a given time
        /// </summary>
        public static Vector3 PredictPosition(NpcCarAgent vehicle, float timeAhead)
        {
            if (vehicle == null) return Vector3.zero;

            Rigidbody rb = vehicle.GetComponent<Rigidbody>();
            if (rb == null) return vehicle.transform.position;

            // Simple linear prediction based on current velocity
            Vector3 currentPos = vehicle.transform.position;
            Vector3 velocity = rb.linearVelocity;

            // Account for steering angle if turning
            Vector3 predictedPos = currentPos + velocity * timeAhead;

            return predictedPos;
        }

        /// <summary>
        /// Predict future position with path following consideration
        /// </summary>
        public static Vector3 PredictPositionWithPath(NpcCarAgent vehicle, float timeAhead)
        {
            if (vehicle == null || vehicle.CurrentSegment == null)
                return PredictPosition(vehicle, timeAhead);

            // Get current speed in m/s
            float speedMs = vehicle.CurrentSpeed / 3.6f;

            // Calculate distance to travel
            float distanceToTravel = speedMs * timeAhead;

            // Follow waypoints to predict position
            Vector3 currentPos = vehicle.transform.position;
            int waypointIndex = vehicle.CurrentWaypointIndex;
            RoadSegment segment = vehicle.CurrentSegment;

            float remainingDistance = distanceToTravel;

            while (remainingDistance > 0 && waypointIndex < segment.waypoints.Count)
            {
                Waypoint wp = segment.waypoints[waypointIndex];
                float distToWaypoint = Vector3.Distance(currentPos, wp.position);

                if (distToWaypoint >= remainingDistance)
                {
                    // Interpolate between current position and waypoint
                    Vector3 direction = (wp.position - currentPos).normalized;
                    return currentPos + direction * remainingDistance;
                }

                remainingDistance -= distToWaypoint;
                currentPos = wp.position;
                waypointIndex++;
            }

            return currentPos;
        }

        /// <summary>
        /// Check if two vehicles will collide within a time window
        /// </summary>
        public static bool WillCollide(NpcCarAgent vehicleA, NpcCarAgent vehicleB, float timeWindow)
        {
            if (vehicleA == null || vehicleB == null) return false;

            // Sample trajectory at multiple time steps
            int samples = 5;
            float timeStep = timeWindow / samples;

            for (int i = 1; i <= samples; i++)
            {
                float t = timeStep * i;

                Vector3 posA = PredictPositionWithPath(vehicleA, t);
                Vector3 posB = PredictPositionWithPath(vehicleB, t);

                float distance = Vector3.Distance(posA, posB);

                // Collision threshold (sum of vehicle sizes)
                float collisionThreshold = 4f; // Approximate vehicle size

                if (distance < collisionThreshold)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Calculate time to collision if vehicles maintain current velocities
        /// </summary>
        public static float CalculateTimeToCollision(NpcCarAgent vehicleA, NpcCarAgent vehicleB)
        {
            if (vehicleA == null || vehicleB == null) return float.MaxValue;

            Rigidbody rbA = vehicleA.GetComponent<Rigidbody>();
            Rigidbody rbB = vehicleB.GetComponent<Rigidbody>();

            if (rbA == null || rbB == null) return float.MaxValue;

            Vector3 posA = vehicleA.transform.position;
            Vector3 posB = vehicleB.transform.position;
            Vector3 velA = rbA.linearVelocity;
            Vector3 velB = rbB.linearVelocity;

            // Relative position and velocity
            Vector3 relativePos = posB - posA;
            Vector3 relativeVel = velB - velA;

            // If moving apart, no collision
            if (Vector3.Dot(relativePos, relativeVel) >= 0)
            {
                return float.MaxValue;
            }

            // Calculate closest approach
            float relativeSpeed = relativeVel.magnitude;
            if (relativeSpeed < 0.1f) return float.MaxValue; // Essentially stationary

            // Project relative position onto relative velocity
            float timeToClosest = -Vector3.Dot(relativePos, relativeVel) / (relativeSpeed * relativeSpeed);

            if (timeToClosest < 0) return float.MaxValue;

            // Calculate distance at closest approach
            Vector3 posAtClosest = relativePos + relativeVel * timeToClosest;
            float closestDistance = posAtClosest.magnitude;

            // Collision threshold
            float collisionThreshold = 4f;

            if (closestDistance < collisionThreshold)
            {
                return timeToClosest;
            }

            return float.MaxValue;
        }

        /// <summary>
        /// Predict turn radius based on current steering and speed
        /// </summary>
        public static float PredictTurnRadius(NpcCarAgent vehicle, float steerAngle)
        {
            if (vehicle == null) return float.MaxValue;

            float wheelBase = 2.5f; // Average wheelbase

            if (Mathf.Abs(steerAngle) < 0.1f)
                return float.MaxValue; // Essentially straight

            float steerRad = Mathf.Deg2Rad * steerAngle;
            float turnRadius = wheelBase / Mathf.Tan(Mathf.Abs(steerRad));

            return turnRadius;
        }

        /// <summary>
        /// Check if vehicle will enter a specific area within time window
        /// </summary>
        public static bool WillEnterArea(NpcCarAgent vehicle, Vector3 areaCenter, float areaRadius, float timeWindow)
        {
            if (vehicle == null) return false;

            // Sample trajectory
            int samples = 5;
            float timeStep = timeWindow / samples;

            for (int i = 1; i <= samples; i++)
            {
                float t = timeStep * i;
                Vector3 predictedPos = PredictPositionWithPath(vehicle, t);

                float distance = Vector3.Distance(predictedPos, areaCenter);

                if (distance < areaRadius)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
