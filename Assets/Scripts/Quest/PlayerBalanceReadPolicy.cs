using System;

namespace DeliveryDriver.Quest
{
    /// <summary>
    /// Legacy-compatible read order: a successfully read SQLite balance,
    /// then the explicit PlayerPrefs balance, then the JSON snapshot.
    /// Does not migrate storage or infer recency from balance size.
    /// </summary>
    public static class PlayerBalanceReadPolicy
    {
        public static int Resolve(int? database, int? preferences, int snapshot)
        {
            return Math.Max(0, database ?? preferences ?? snapshot);
        }
    }
}
