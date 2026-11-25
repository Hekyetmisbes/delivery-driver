using System;

namespace DeliveryDriver.Quest
{
    /// <summary>
    /// Legacy-compatible read order: a successfully read SQLite balance,
    /// then the explicit PlayerPrefs balance, then the JSON snapshot.
    /// An explicit pending local write takes precedence until committed to SQLite.
    /// Does not infer recency from balance size.
    /// </summary>
    public static class PlayerBalanceReadPolicy
    {
        public static int Resolve(int? database, int? preferences, int snapshot, bool pending = false)
        {
            return Math.Max(0, pending && preferences.HasValue
                ? preferences.Value : database ?? preferences ?? snapshot);
        }
    }
}
