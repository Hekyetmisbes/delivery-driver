using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryDriver.Quest.UI
{
    public class QuestListUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject questEntryPrefab;
        [SerializeField] private Transform questEntriesContainer;
        [SerializeField] private Button closeButton;
        [SerializeField] private Animator panelAnimator;

        private readonly List<GameObject> spawnedEntries = new List<GameObject>();
        private bool isOpen = false; // Start closed, toggle with Tab
        private Image backgroundImage;

        private static readonly int IsOpenHash = Animator.StringToHash("IsOpen");

        private void Awake()
        {
            backgroundImage = GetComponent<Image>();

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(TogglePanel);
            }
        }

        private void OnDestroy()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(TogglePanel);
            }
        }

        private void Start()
        {
            // Apply initial closed state
            ApplyPanelState();
        }

        public bool IsOpen => isOpen;

        public void PopulateQuestList(List<QuestData> quests)
        {
            ClearQuestList();

            if (questEntriesContainer == null || questEntryPrefab == null || quests == null)
            {
                return;
            }

            foreach (QuestData quest in quests)
            {
                if (quest == null)
                {
                    continue;
                }

                GameObject entry = Instantiate(questEntryPrefab, questEntriesContainer);
                spawnedEntries.Add(entry);

                QuestEntryUI entryUI = entry.GetComponent<QuestEntryUI>();
                if (entryUI != null)
                {
                    entryUI.Initialize(quest);
                }
                else
                {
                    entry.SendMessage("Initialize", quest, SendMessageOptions.DontRequireReceiver);

                    Button[] buttons = entry.GetComponentsInChildren<Button>(true);
                    foreach (Button button in buttons)
                    {
                        button.onClick.AddListener(() => OnQuestEntryClicked(quest));
                    }
                }
            }
        }

        public void ClearQuestList()
        {
            for (int i = spawnedEntries.Count - 1; i >= 0; i--)
            {
                if (spawnedEntries[i] != null)
                {
                    Destroy(spawnedEntries[i]);
                }
            }

            spawnedEntries.Clear();

            if (questEntriesContainer == null)
            {
                return;
            }

            for (int i = questEntriesContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = questEntriesContainer.GetChild(i);
                Destroy(child.gameObject);
            }
        }

        public void TogglePanel()
        {
            isOpen = !isOpen;
            ApplyPanelState();
        }

        public void SetOpen(bool open)
        {
            isOpen = open;
            ApplyPanelState();
        }

        public void OnQuestEntryClicked(QuestData quest)
        {
            if (quest == null)
            {
                return;
            }

            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.AcceptQuest(quest.QuestID);
            }
        }

        private void ApplyPanelState()
        {
            if (panelAnimator != null)
            {
                panelAnimator.SetBool(IsOpenHash, isOpen);
                return;
            }

            if (backgroundImage != null)
            {
                backgroundImage.enabled = isOpen;
            }

            for (int i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetActive(isOpen);
            }
        }
    }
}
