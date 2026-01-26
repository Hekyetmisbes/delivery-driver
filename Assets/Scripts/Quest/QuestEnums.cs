namespace DeliveryDriver.Quest
{
    /// <summary>
    /// Defines the type of quest/delivery mission
    /// </summary>
    public enum QuestType
    {
        /// <summary>
        /// Basic delivery with normal time limit
        /// </summary>
        StandardDelivery,

        /// <summary>
        /// Tight time limit, higher reward
        /// </summary>
        ExpressDelivery,

        /// <summary>
        /// Damage penalty for collisions
        /// </summary>
        FragileDelivery,

        /// <summary>
        /// Multiple pickup/delivery locations
        /// </summary>
        MultiStopDelivery,

        /// <summary>
        /// Fastest delivery wins bonus
        /// </summary>
        TimeTrial
    }

    /// <summary>
    /// Defines the current status of a quest
    /// </summary>
    public enum QuestStatus
    {
        /// <summary>
        /// Quest available but not accepted
        /// </summary>
        NotStarted,

        /// <summary>
        /// Quest accepted and in progress
        /// </summary>
        Active,

        /// <summary>
        /// Successfully completed
        /// </summary>
        Completed,

        /// <summary>
        /// Time ran out or cargo destroyed
        /// </summary>
        Failed,

        /// <summary>
        /// Quest no longer available
        /// </summary>
        Expired
    }

    /// <summary>
    /// Defines the difficulty level of a quest
    /// </summary>
    public enum QuestDifficulty
    {
        /// <summary>
        /// Short distance, generous time (1-2 km, 3-5 min)
        /// </summary>
        Easy,

        /// <summary>
        /// Medium distance, moderate time (2-4 km, 4-7 min)
        /// </summary>
        Medium,

        /// <summary>
        /// Long distance, tight time (4-6 km, 6-10 min)
        /// </summary>
        Hard,

        /// <summary>
        /// Very long, very tight (6+ km, 8-12 min)
        /// </summary>
        Expert
    }
}
