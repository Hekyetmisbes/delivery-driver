namespace TrafficSystem
{
    public static class NpcTimeOfDayPolicy
    {
        public static float GetSpeedMultiplier(int hour, bool aggressive)
        {
            if ((hour >= 7 && hour <= 9) || (hour >= 17 && hour <= 19))
                return aggressive ? 1.15f : 1.05f;
            return hour >= 22 || hour <= 6 ? 0.85f : 1f;
        }
    }
}
