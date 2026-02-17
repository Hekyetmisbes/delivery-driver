using UnityEngine;
using TrafficSystem;

namespace DeliveryDriver.Quest.UI
{
    public class QuestUIManager : MonoBehaviour
    {
        public static QuestUIManager Instance { get; private set; }

        [Header("UI Panels")]
        [SerializeField] private QuestListUI questListUI;
        [SerializeField] private ActiveQuestUI activeQuestUI;
        [SerializeField] private QuestCompleteUI questCompleteUI;

        [Header("Player")]
        [SerializeField] private Transform playerTransform;

        private QuestManager questManager;
        private bool isSubscribed;
        private QuestLocation lastObjective;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            questManager = QuestManager.Instance;
            SubscribeToQuestEvents();
        }

        private void Start()
        {
            if (questManager == null)
            {
                questManager = QuestManager.Instance;
                SubscribeToQuestEvents();
            }

            ResolvePlayerTransform();

            if (questCompleteUI != null)
            {
                questCompleteUI.SetContinueAction(HandleContinue);
                questCompleteUI.Hide();
            }

            if (activeQuestUI != null)
            {
                activeQuestUI.Hide();
            }

            RefreshQuestList();

            if (questManager != null && questManager.CurrentQuest != null)
            {
                HandleQuestStarted(questManager.CurrentQuest);
                HandleQuestUpdated(questManager.CurrentQuest);
            }
        }

        private void OnDisable()
        {
            UnsubscribeFromQuestEvents();
        }

        private void Update()
        {
            if (questManager == null || questManager.CurrentQuest == null)
            {
                return;
            }

            if (playerTransform == null)
            {
                ResolvePlayerTransform();
            }

            QuestData currentQuest = questManager.CurrentQuest;

            if (activeQuestUI != null)
            {
                activeQuestUI.UpdateTimer(currentQuest.TimeRemaining, currentQuest.TimeLimit);
            }

            UpdateActiveQuestDistance(currentQuest);
            UpdateCargoHealth(currentQuest);
        }

        private void SubscribeToQuestEvents()
        {
            if (questManager == null)
            {
                return;
            }

            if (isSubscribed)
            {
                return;
            }

            questManager.OnQuestStarted.AddListener(HandleQuestStarted);
            questManager.OnQuestCompleted.AddListener(HandleQuestCompleted);
            questManager.OnQuestFailed.AddListener(HandleQuestFailed);
            questManager.OnQuestUpdated.AddListener(HandleQuestUpdated);
            questManager.OnDrivingFeedback.AddListener(HandleDrivingFeedback);
            isSubscribed = true;
        }

        private void UnsubscribeFromQuestEvents()
        {
            if (questManager == null)
            {
                return;
            }

            questManager.OnQuestStarted.RemoveListener(HandleQuestStarted);
            questManager.OnQuestCompleted.RemoveListener(HandleQuestCompleted);
            questManager.OnQuestFailed.RemoveListener(HandleQuestFailed);
            questManager.OnQuestUpdated.RemoveListener(HandleQuestUpdated);
            questManager.OnDrivingFeedback.RemoveListener(HandleDrivingFeedback);
            isSubscribed = false;
        }

        public void RefreshQuestList()
        {
            if (questListUI == null || questManager == null)
            {
                return;
            }

            questListUI.PopulateQuestList(new System.Collections.Generic.List<QuestData>(questManager.AvailableQuests));
        }

        private void HandleQuestStarted(QuestData quest)
        {
            if (questListUI != null)
            {
                questListUI.SetOpen(false);
            }

            if (activeQuestUI != null)
            {
                activeQuestUI.Show();
                activeQuestUI.UpdateQuestDisplay(quest);
                lastObjective = null;
                UpdateActiveQuestDistance(quest);
                UpdateCargoHealth(quest);
            }
        }

        private void HandleQuestCompleted(QuestData quest)
        {
            if (activeQuestUI != null)
            {
                activeQuestUI.Hide();
            }

            if (questCompleteUI != null)
            {
                int reward = questManager != null ? questManager.LastCompletionReward : quest.CalculateFinalReward();
                QuestManager.RewardPenaltyBreakdown? breakdown = questManager != null
                    ? questManager.LastQuestBreakdown
                    : null;
                questCompleteUI.ShowCompleteScreen(quest, reward, breakdown);
            }

            RefreshQuestList();
        }

        private void HandleQuestFailed(QuestData quest)
        {
            if (activeQuestUI != null)
            {
                activeQuestUI.Hide();
            }

            if (questCompleteUI != null)
            {
                string reason = questManager != null ? questManager.LastFailureReason : string.Empty;
                int penalty = questManager != null ? questManager.LastFailurePenalty : 0;
                QuestManager.RewardPenaltyBreakdown? breakdown = questManager != null
                    ? questManager.LastQuestBreakdown
                    : null;
                questCompleteUI.ShowFailedScreen(quest, reason, penalty, breakdown);
            }

            RefreshQuestList();
        }

        private void HandleQuestUpdated(QuestData quest)
        {
            if (activeQuestUI == null)
            {
                return;
            }

            activeQuestUI.UpdateQuestDisplay(quest);
        }

        private void HandleDrivingFeedback(string message, int scoreDelta)
        {
            if (activeQuestUI == null)
            {
                return;
            }

            activeQuestUI.ShowDrivingFeedback(message, scoreDelta);
        }

        private void HandleContinue()
        {
            RefreshQuestList();
        }

        private void ResolvePlayerTransform()
        {
            if (playerTransform != null)
            {
                return;
            }

            CarController controller = FindAnyObjectByType<CarController>();
            if (controller != null)
            {
                playerTransform = controller.transform;
                return;
            }

            if (questManager != null && questManager.PlayerTransform != null)
            {
                playerTransform = questManager.PlayerTransform;
                return;
            }

            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
            {
                playerTransform = taggedPlayer.transform;
                return;
            }

            if (Camera.main != null)
            {
                playerTransform = Camera.main.transform.root;
            }
        }

        private void UpdateActiveQuestDistance(QuestData quest)
        {
            if (quest == null || activeQuestUI == null)
            {
                return;
            }

            if (playerTransform == null)
            {
                ResolvePlayerTransform();
            }

            if (playerTransform == null)
            {
                return;
            }

            QuestLocation target = GetCurrentObjective(quest);
            if (target == null)
            {
                activeQuestUI.UpdateDistance(0f);
                lastObjective = null;
                return;
            }

            Vector3 delta = playerTransform.position - target.Position;
            float distanceSqr = delta.sqrMagnitude;
            float distance = Mathf.Sqrt(distanceSqr);
            activeQuestUI.UpdateDistance(distance);
            lastObjective = target;
        }

        private void UpdateCargoHealth(QuestData quest)
        {
            if (activeQuestUI == null || quest == null)
            {
                return;
            }

            if (quest.Cargo != null && quest.Cargo.IsFragile)
            {
                activeQuestUI.UpdateCargoHealth(quest.Cargo.CargoHealth);
            }
        }

        private QuestLocation GetCurrentObjective(QuestData quest)
        {
            if (quest == null)
            {
                return null;
            }

            if (!quest.HasPickedUpCargo)
            {
                return quest.PickupLocation;
            }

            if (quest.DeliveryLocations == null || quest.DeliveryLocations.Count == 0)
            {
                return null;
            }

            int index = Mathf.Clamp(quest.CurrentDeliveryIndex, 0, quest.DeliveryLocations.Count - 1);
            return quest.DeliveryLocations[index];
        }
    }
}
