using UnityEngine;

namespace DeliveryDriver.Quest
{
    /// <summary>
    /// Runtime validation helper to confirm quest system dependencies are wired.
    /// </summary>
    public class QuestSystemValidator : MonoBehaviour
    {
        [ContextMenu("Run Quest System Validation")]
        private void RunQuestSystemValidation()
        {
            bool hasIssues = false;

            if (QuestManager.Instance == null)
            {
                QuestLogger.Warn("[QuestSystemValidator] QuestManager not found.");
                hasIssues = true;
            }

            if (PlayerProgressionManager.Instance == null)
            {
                QuestLogger.Warn("[QuestSystemValidator] PlayerProgressionManager not found.");
                hasIssues = true;
            }

            if (SaveManager.Instance == null)
            {
                QuestLogger.Warn("[QuestSystemValidator] SaveManager not found.");
                hasIssues = true;
            }

            if (QuestUIManager.Instance == null)
            {
                QuestLogger.Warn("[QuestSystemValidator] QuestUIManager not found.");
                hasIssues = true;
            }

            if (!hasIssues)
            {
                QuestLogger.Log("[QuestSystemValidator] Quest system validation passed.");
            }
        }
    }
}
