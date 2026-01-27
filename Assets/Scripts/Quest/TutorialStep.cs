using UnityEngine;

namespace DeliveryDriver.Quest
{
    /// <summary>
    /// Represents a single step in the tutorial sequence
    /// </summary>
    [System.Serializable]
    public class TutorialStep
    {
        [Header("Content")]
        [TextArea(3, 6)]
        public string message;
        public string title;

        [Header("UI Highlight")]
        public RectTransform highlightTarget; // UI element to highlight (optional)
        public Vector2 arrowDirection; // Direction for arrow pointer

        [Header("Trigger")]
        public TutorialTriggerType triggerType = TutorialTriggerType.ManualAdvance;
        public string triggerKey; // For specific triggers (e.g., key press, quest accepted)

        [Header("Settings")]
        public float autoAdvanceDelay = 0f; // If > 0, auto-advance after delay
        public bool pauseGame = false;
        public bool canSkip = true;

        [Header("Audio")]
        public AudioClip voiceOverClip; // Optional voice-over

        public TutorialStep()
        {
            triggerType = TutorialTriggerType.ManualAdvance;
            canSkip = true;
        }

        public TutorialStep(string message, string title = "")
        {
            this.message = message;
            this.title = title;
            triggerType = TutorialTriggerType.ManualAdvance;
            canSkip = true;
        }
    }

    /// <summary>
    /// Types of triggers that can advance a tutorial step
    /// </summary>
    public enum TutorialTriggerType
    {
        ManualAdvance,      // Player clicks "Next" button
        AutoAdvance,        // Auto-advance after delay
        KeyPress,           // Specific key pressed (e.g., "Q")
        QuestOpened,        // Quest menu opened
        QuestAccepted,      // Quest accepted
        CargoPickedUp,      // Cargo picked up
        CargoDelivered,     // Cargo delivered
        QuestCompleted      // Quest completed
    }
}
