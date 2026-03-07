using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace DeliveryDriver.Quest
{
    /// <summary>
    /// Manages the tutorial system and progression through tutorial steps
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        [Header("Tutorial Steps")]
        [SerializeField] private List<TutorialStep> tutorialSteps = new List<TutorialStep>();

        [Header("UI")]
        [SerializeField] private UI.TutorialUI tutorialUI;

        [Header("Settings")]
        [SerializeField] private bool startTutorialOnFirstPlay = true;
        [SerializeField] private string tutorialCompletedKey = "TutorialCompleted";

        [Header("Audio")]
        [SerializeField] private AudioSource tutorialAudioSource;

        private int currentStepIndex = -1;
        private bool isTutorialActive = false;
        private bool isWaitingForTrigger = false;
        private Coroutine autoAdvanceCoroutine;

        public bool IsTutorialActive => isTutorialActive;
        public int CurrentStepIndex => currentStepIndex;
        public int TotalSteps => tutorialSteps != null ? tutorialSteps.Count : 0;
        public bool IsTutorialCompleted => PlayerPrefs.GetInt(tutorialCompletedKey, 0) == 1;

        public UnityEvent OnTutorialStarted = new UnityEvent();
        public UnityEvent OnTutorialCompleted = new UnityEvent();
        public UnityEvent<int> OnStepChanged = new UnityEvent<int>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            SetupDefaultTutorialSteps();

            if (startTutorialOnFirstPlay && !IsTutorialCompleted)
            {
                // Delay tutorial start slightly to let the scene initialize
                StartCoroutine(DelayedTutorialStart());
            }

            SubscribeToQuestEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromQuestEvents();
        }

        private IEnumerator DelayedTutorialStart()
        {
            yield return new WaitForSeconds(1f);
            StartTutorial();
        }

        private void SetupDefaultTutorialSteps()
        {
            // Only set up default steps if none are configured
            if (tutorialSteps.Count > 0)
            {
                return;
            }

            tutorialSteps = new List<TutorialStep>
            {
                new TutorialStep(
                    "Welcome to Delivery Driver! Complete deliveries to earn money and level up.",
                    "Welcome!"
                )
                {
                    autoAdvanceDelay = 0f,
                    canSkip = true
                },

                new TutorialStep(
                    "Press Q to open the quest menu and see available deliveries.",
                    "Open Quest Menu"
                )
                {
                    triggerType = TutorialTriggerType.QuestOpened,
                    triggerKey = "Q"
                },

                new TutorialStep(
                    "Select a delivery quest to begin. Each quest shows the distance, time limit, and reward.",
                    "Accept a Quest"
                )
                {
                    triggerType = TutorialTriggerType.QuestAccepted
                },

                new TutorialStep(
                    "Drive to the blue marker to pick up the cargo. Follow the compass at the top of the screen.",
                    "Pick Up Cargo"
                )
                {
                    triggerType = TutorialTriggerType.CargoPickedUp
                },

                new TutorialStep(
                    "Now deliver the cargo to the green marker before time runs out! Drive carefully if the cargo is fragile.",
                    "Deliver Cargo"
                )
                {
                    triggerType = TutorialTriggerType.CargoDelivered
                },

                new TutorialStep(
                    "Great job! Complete more deliveries to earn money, gain experience, and unlock new challenges. Good luck!",
                    "Tutorial Complete"
                )
                {
                    autoAdvanceDelay = 5f,
                    canSkip = true
                }
            };
        }

        private void SubscribeToQuestEvents()
        {
            if (QuestManager.Instance == null)
            {
                return;
            }

            QuestManager.Instance.OnQuestStarted.AddListener(HandleQuestStarted);
            QuestManager.Instance.OnQuestCompleted.AddListener(HandleQuestCompleted);

            // Subscribe to UI manager for menu opened event
            if (UI.QuestUIManager.Instance != null)
            {
                // We'll check for quest menu opened in Update
            }
        }

        private void UnsubscribeFromQuestEvents()
        {
            if (QuestManager.Instance == null)
            {
                return;
            }

            QuestManager.Instance.OnQuestStarted.RemoveListener(HandleQuestStarted);
            QuestManager.Instance.OnQuestCompleted.RemoveListener(HandleQuestCompleted);
        }

        private void Update()
        {
            if (!isTutorialActive || !isWaitingForTrigger)
            {
                return;
            }

            CheckTriggerConditions();
        }

        public void StartTutorial()
        {
            if (isTutorialActive)
            {
                return;
            }

            isTutorialActive = true;
            currentStepIndex = -1;

            if (tutorialUI != null)
            {
                tutorialUI.Show();
            }

            OnTutorialStarted?.Invoke();

            AdvanceToNextStep();
        }

        public void RestartTutorial()
        {
            EndTutorial();
            PlayerPrefs.SetInt(tutorialCompletedKey, 0);
            PlayerPrefs.Save();
            StartTutorial();
        }

        public void EndTutorial()
        {
            if (!isTutorialActive)
            {
                return;
            }

            isTutorialActive = false;
            isWaitingForTrigger = false;
            currentStepIndex = -1;

            if (autoAdvanceCoroutine != null)
            {
                StopCoroutine(autoAdvanceCoroutine);
                autoAdvanceCoroutine = null;
            }

            if (tutorialUI != null)
            {
                tutorialUI.Hide();
            }
        }

        public void CompleteTutorial()
        {
            PlayerPrefs.SetInt(tutorialCompletedKey, 1);
            PlayerPrefs.Save();

            OnTutorialCompleted?.Invoke();
            EndTutorial();

            Debug.Log("Tutorial completed!");
        }

        public void SkipTutorial()
        {
            CompleteTutorial();
        }

        public void AdvanceToNextStep()
        {
            currentStepIndex++;

            if (currentStepIndex >= tutorialSteps.Count)
            {
                CompleteTutorial();
                return;
            }

            ShowCurrentStep();
        }

        public void AdvanceToPreviousStep()
        {
            if (currentStepIndex <= 0)
            {
                return;
            }

            currentStepIndex--;
            ShowCurrentStep();
        }

        private void ShowCurrentStep()
        {
            if (currentStepIndex < 0 || currentStepIndex >= tutorialSteps.Count)
            {
                return;
            }

            TutorialStep step = tutorialSteps[currentStepIndex];

            if (tutorialUI != null)
            {
                tutorialUI.DisplayStep(step);
            }

            OnStepChanged?.Invoke(currentStepIndex);

            // Play voice-over if available
            if (step.voiceOverClip != null && tutorialAudioSource != null)
            {
                tutorialAudioSource.PlayOneShot(step.voiceOverClip);
            }

            // Handle pause
            if (step.pauseGame)
            {
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = 1f;
            }

            // Handle triggers
            if (step.triggerType == TutorialTriggerType.ManualAdvance)
            {
                isWaitingForTrigger = false;
            }
            else if (step.triggerType == TutorialTriggerType.AutoAdvance && step.autoAdvanceDelay > 0f)
            {
                if (autoAdvanceCoroutine != null)
                {
                    StopCoroutine(autoAdvanceCoroutine);
                }
                autoAdvanceCoroutine = StartCoroutine(AutoAdvanceAfterDelay(step.autoAdvanceDelay));
                isWaitingForTrigger = false;
            }
            else
            {
                isWaitingForTrigger = true;
            }
        }

        private IEnumerator AutoAdvanceAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            AdvanceToNextStep();
        }

        private void CheckTriggerConditions()
        {
            if (currentStepIndex < 0 || currentStepIndex >= tutorialSteps.Count)
            {
                return;
            }

            TutorialStep step = tutorialSteps[currentStepIndex];

            switch (step.triggerType)
            {
                case TutorialTriggerType.KeyPress:
                    if (!string.IsNullOrEmpty(step.triggerKey) && Input.GetKeyDown(step.triggerKey))
                    {
                        OnTriggerMet();
                    }
                    break;

                case TutorialTriggerType.QuestOpened:
                    // Check if quest list is open
                    if (UI.QuestUIManager.Instance != null)
                    {
                        // This would need to be implemented in QuestListUI
                        // For now, we'll trigger on Q key press
                        if (Input.GetKeyDown(KeyCode.Q))
                        {
                            OnTriggerMet();
                        }
                    }
                    break;
            }
        }

        private void HandleQuestStarted(QuestData quest)
        {
            if (!isTutorialActive || !isWaitingForTrigger)
            {
                return;
            }

            if (currentStepIndex >= 0 && currentStepIndex < tutorialSteps.Count)
            {
                TutorialStep step = tutorialSteps[currentStepIndex];

                if (step.triggerType == TutorialTriggerType.QuestAccepted)
                {
                    OnTriggerMet();
                }
            }

            // Subscribe to cargo events
            if (quest != null)
            {
                // We'll check these in QuestManager callbacks
            }
        }

        private void HandleQuestCompleted(QuestData quest)
        {
            if (!isTutorialActive || !isWaitingForTrigger)
            {
                return;
            }

            if (currentStepIndex >= 0 && currentStepIndex < tutorialSteps.Count)
            {
                TutorialStep step = tutorialSteps[currentStepIndex];

                if (step.triggerType == TutorialTriggerType.QuestCompleted)
                {
                    OnTriggerMet();
                }
            }
        }

        public void OnCargoPickedUp()
        {
            if (!isTutorialActive || !isWaitingForTrigger)
            {
                return;
            }

            if (currentStepIndex >= 0 && currentStepIndex < tutorialSteps.Count)
            {
                TutorialStep step = tutorialSteps[currentStepIndex];

                if (step.triggerType == TutorialTriggerType.CargoPickedUp)
                {
                    OnTriggerMet();
                }
            }
        }

        public void OnCargoDelivered()
        {
            if (!isTutorialActive || !isWaitingForTrigger)
            {
                return;
            }

            if (currentStepIndex >= 0 && currentStepIndex < tutorialSteps.Count)
            {
                TutorialStep step = tutorialSteps[currentStepIndex];

                if (step.triggerType == TutorialTriggerType.CargoDelivered)
                {
                    OnTriggerMet();
                }
            }
        }

        private void OnTriggerMet()
        {
            isWaitingForTrigger = false;
            AdvanceToNextStep();
        }

        public TutorialStep GetCurrentStep()
        {
            if (currentStepIndex >= 0 && currentStepIndex < tutorialSteps.Count)
            {
                return tutorialSteps[currentStepIndex];
            }

            return null;
        }
    }
}
