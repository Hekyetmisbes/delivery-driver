using UnityEngine;

namespace DeliveryDriver.Quest
{
    /// <summary>
    /// Centralized logging helper for quest system diagnostics.
    /// </summary>
    public static class QuestLogger
    {
        public static bool EnableLogs = true;

        public static void Log(string message)
        {
            if (!EnableLogs)
            {
                return;
            }

            Debug.Log(message);
        }

        public static void Warn(string message)
        {
            if (!EnableLogs)
            {
                return;
            }

            Debug.LogWarning(message);
        }

        public static void Error(string message)
        {
            if (!EnableLogs)
            {
                return;
            }

            Debug.LogError(message);
        }
    }
}
