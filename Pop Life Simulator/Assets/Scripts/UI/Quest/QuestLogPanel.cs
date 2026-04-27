using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PopLife.Data;
using PixelCrushers.DialogueSystem;

namespace PopLife.UI.Quest
{
    /// <summary>
    /// 任务日志面板 - 左侧任务列表 + 右侧任务详情
    /// 替代原 QuestDetailPanel，提供完整的任务浏览体验
    /// </summary>
    public class QuestLogPanel : MonoBehaviour
    {
        [Header("面板控制")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Button closeButton;

        [Header("左侧列表区")]
        [SerializeField] private Transform listContainer;
        [SerializeField] private GameObject listEntryPrefab;
        [SerializeField] private GameObject groupHeaderPrefab;

        [Header("右侧详情区 - 标题")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Image questIcon;
        [SerializeField] private TextMeshProUGUI questTypeLabel;
        [SerializeField] private Image questTypeIcon;
        [SerializeField] private TextMeshProUGUI questStateLabel;

        [Header("Type Icon Sprites")]
        [SerializeField] private Sprite mainQuestSprite;
        [SerializeField] private Sprite sideQuestSprite;

        [Header("Type Label Colors")]
        [SerializeField] private Color mainQuestLabelColor = new Color(1f, 0.84f, 0f);
        [SerializeField] private Color sideQuestLabelColor = Color.white;

        [Header("右侧详情区 - 描述")]
        [SerializeField] private TextMeshProUGUI descriptionText;

        [Header("右侧详情区 - 需求条目")]
        [SerializeField] private Transform entriesContainer;
        [SerializeField] private GameObject entryItemPrefab;

        [Header("右侧详情区 - 截止日期")]
        [SerializeField] private GameObject deadlineSection;
        [SerializeField] private TextMeshProUGUI deadlineText;

        [Header("右侧详情区 - 颁布者")]
        [SerializeField] private GameObject giverSection;
        [SerializeField] private Image giverPortrait;
        [SerializeField] private TextMeshProUGUI giverNameText;

        [Header("右侧详情区 - 奖励")]
        [SerializeField] private Transform rewardsContainer;
        [SerializeField] private GameObject rewardItemPrefab;

        [Header("空状态")]
        [SerializeField] private GameObject emptyStateObject;
        [SerializeField] private GameObject detailEmptyState;
        [SerializeField] private GameObject detailContent;

        [Header("Animation")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.2f;

        // 关闭回调（由 CalendarPanel Due Date 路由传入）
        private Action onCloseCallback;

        private string selectedQuestName;
        private readonly List<GameObject> spawnedListItems = new();
        private readonly List<GameObject> spawnedEntries = new();
        private readonly List<GameObject> spawnedRewards = new();
        private QuestLogListEntry selectedEntry;

        private void Awake()
        {
            canvasGroup = canvasGroup ?? GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);

            if (panelRoot != null)
                panelRoot.SetActive(false);

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void OnEnable()
        {
            if (QuestDataService.Instance != null)
                QuestDataService.Instance.OnQuestStateChanged += OnQuestStateChanged;
        }

        private void OnDisable()
        {
            if (QuestDataService.Instance != null)
                QuestDataService.Instance.OnQuestStateChanged -= OnQuestStateChanged;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && panelRoot != null && panelRoot.activeSelf)
            {
                Hide();
                return;
            }

            // 按 P 键切换任务日志面板
            if (Input.GetKeyDown(KeyCode.P))
            {
                if (IsShowing())
                    Hide();
                else
                    Show();
            }
        }

        /// <summary>
        /// 显示面板，可选聚焦到指定任务
        /// </summary>
        /// <param name="focusQuestName">聚焦到指定任务名（可选）</param>
        /// <param name="closeCallback">关闭时触发的回调（由 CalendarPanel Due Date 路由传入）</param>
        public void Show(string focusQuestName = null, Action closeCallback = null)
        {
            CloseShelfListPanelIfOpen();

            onCloseCallback = closeCallback;

            RefreshList();

            if (!string.IsNullOrEmpty(focusQuestName))
                SelectQuest(focusQuestName);
            else
                SelectFirstQuest();

            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (panelRoot != null) panelRoot.SetActive(true);

            StopAllCoroutines();
            StartCoroutine(FadeIn());

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound(AudioKeys.UI_CLICK);
        }

        /// <summary>
        /// 隐藏面板
        /// </summary>
        public void Hide()
        {
            StopAllCoroutines();
            StartCoroutine(FadeOut());
        }

        /// <summary>
        /// 检查面板是否正在显示
        /// </summary>
        public bool IsShowing()
        {
            return panelRoot != null && panelRoot.activeSelf;
        }

        private static void CloseShelfListPanelIfOpen()
        {
            var shelfListPanel = FindFirstObjectByType<PopLife.UI.ShelfListPanel>();
            if (shelfListPanel != null && shelfListPanel.IsOpen())
            {
                shelfListPanel.ClosePanel();
            }
        }

        #region 左侧列表

        private void RefreshList()
        {
            ClearListItems();

            var groups = QuestDataService.Instance?.GetAllQuestsGrouped();
            if (groups == null || groups.Count == 0)
            {
                if (emptyStateObject != null) emptyStateObject.SetActive(true);
                return;
            }

            if (emptyStateObject != null) emptyStateObject.SetActive(false);

            foreach (var group in groups)
            {
                // 创建分组标题
                if (groupHeaderPrefab != null)
                {
                    var headerObj = Instantiate(groupHeaderPrefab, listContainer);
                    var headerText = headerObj.GetComponent<TextMeshProUGUI>();
                    if (headerText != null)
                        headerText.text = $"{group.groupLabel} ({group.quests.Count})";
                    spawnedListItems.Add(headerObj);
                }

                // 判断是否为非 Active 组（灰化显示）
                bool dimmed = group.groupLabel != "Active";

                // 创建任务条目
                foreach (var quest in group.quests)
                {
                    if (listEntryPrefab == null) continue;
                    var obj = Instantiate(listEntryPrefab, listContainer);
                    var entry = obj.GetComponent<QuestLogListEntry>();
                    if (entry != null)
                    {
                        entry.Initialize(quest, OnListEntryClicked, dimmed);
                    }
                    spawnedListItems.Add(obj);
                }
            }
        }

        private void OnListEntryClicked(QuestLogListEntry entry)
        {
            SelectQuest(entry.QuestName);

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound(AudioKeys.UI_CLICK);
        }

        private void SelectQuest(string questName)
        {
            selectedQuestName = questName;

            // 更新列表项选中高亮
            selectedEntry = null;
            foreach (var obj in spawnedListItems)
            {
                if (obj == null) continue;
                var entry = obj.GetComponent<QuestLogListEntry>();
                if (entry == null) continue;

                bool isSelected = entry.QuestName == questName;
                entry.SetSelected(isSelected);
                if (isSelected) selectedEntry = entry;
            }

            // 更新右侧详情
            var detail = QuestDataService.Instance?.GetQuestDetail(questName);
            if (detail != null)
            {
                if (detailEmptyState != null) detailEmptyState.SetActive(false);
                if (detailContent != null) detailContent.SetActive(true);
                UpdateDetailContent(detail.Value);
            }
            else
            {
                ShowDetailEmpty();
            }
        }

        private void SelectFirstQuest()
        {
            foreach (var obj in spawnedListItems)
            {
                if (obj == null) continue;
                var entry = obj.GetComponent<QuestLogListEntry>();
                if (entry != null)
                {
                    SelectQuest(entry.QuestName);
                    return;
                }
            }

            // 没有任何任务
            ShowDetailEmpty();
        }

        private void ShowDetailEmpty()
        {
            if (detailEmptyState != null) detailEmptyState.SetActive(true);
            if (detailContent != null) detailContent.SetActive(false);
        }

        private void ClearListItems()
        {
            foreach (var obj in spawnedListItems)
            {
                if (obj != null) Destroy(obj);
            }
            spawnedListItems.Clear();
            selectedEntry = null;
        }

        #endregion

        #region 右侧详情

        private void UpdateDetailContent(QuestDetailViewModel detail)
        {
            // 标题
            if (titleText != null) titleText.text = detail.displayTitle;

            // 任务图标
            if (questIcon != null)
            {
                if (detail.questIcon != null)
                {
                    questIcon.sprite = detail.questIcon;
                    questIcon.enabled = true;
                }
                else
                {
                    questIcon.enabled = false;
                }
            }

            // 任务类型标签
            if (questTypeLabel != null)
            {
                questTypeLabel.text = detail.questType == QuestType.Main ? "MAIN QUEST" : "SIDE QUEST";
                questTypeLabel.color = detail.questType == QuestType.Main
                    ? mainQuestLabelColor
                    : sideQuestLabelColor;
            }

            // 任务类型图标（仅切换 Sprite，不变色）
            if (questTypeIcon != null)
            {
                Sprite targetSprite = detail.questType == QuestType.Main ? mainQuestSprite : sideQuestSprite;
                if (targetSprite != null)
                {
                    questTypeIcon.sprite = targetSprite;
                    questTypeIcon.enabled = true;
                }
                else
                {
                    questTypeIcon.enabled = false;
                }
            }

            // 任务状态标签
            if (questStateLabel != null)
            {
                questStateLabel.text = detail.questState switch
                {
                    QuestState.Active => "IN PROGRESS",
                    QuestState.Success => "COMPLETED",
                    QuestState.Failure => "FAILED",
                    _ => ""
                };
                questStateLabel.color = detail.questState switch
                {
                    QuestState.Active => new Color(0.4f, 0.8f, 1f),
                    QuestState.Success => new Color(0.4f, 1f, 0.4f),
                    QuestState.Failure => new Color(1f, 0.27f, 0.27f),
                    _ => Color.white
                };
            }

            // 描述
            if (descriptionText != null) descriptionText.text = detail.description;

            // 截止日期
            if (deadlineSection != null)
            {
                if (detail.remainingDays < 0)
                {
                    deadlineSection.SetActive(false);
                }
                else
                {
                    deadlineSection.SetActive(true);
                    if (deadlineText != null)
                    {
                        if (detail.remainingDays <= 1)
                            deadlineText.text = $"<color=#FF4444>{detail.remainingDays} day remaining!</color>";
                        else
                            deadlineText.text = $"{detail.remainingDays} days remaining";
                    }
                }
            }

            // 颁布者
            if (giverSection != null)
            {
                if (!string.IsNullOrEmpty(detail.giverName))
                {
                    giverSection.SetActive(true);
                    if (giverNameText != null) giverNameText.text = detail.giverName;
                    if (giverPortrait != null && detail.giverPortrait != null)
                    {
                        giverPortrait.sprite = detail.giverPortrait;
                        giverPortrait.preserveAspect = true;
                        giverPortrait.enabled = true;
                    }
                    else if (giverPortrait != null)
                    {
                        giverPortrait.enabled = false;
                    }
                }
                else
                {
                    giverSection.SetActive(false);
                }
            }

            // 条目列表
            PopulateEntries(detail.entries);

            // 奖励列表
            PopulateRewards(detail.rewards);
        }

        private void PopulateEntries(QuestEntryInfo[] entries)
        {
            foreach (var obj in spawnedEntries)
            {
                if (obj != null) Destroy(obj);
            }
            spawnedEntries.Clear();

            if (entries == null || entries.Length == 0 || entriesContainer == null || entryItemPrefab == null)
                return;

            foreach (var entry in entries)
            {
                var obj = Instantiate(entryItemPrefab, entriesContainer);
                var item = obj.GetComponent<QuestTooltipEntryItem>();
                if (item != null)
                {
                    item.SetData(entry);
                }
                spawnedEntries.Add(obj);
            }
        }

        private void PopulateRewards(QuestReward[] rewards)
        {
            foreach (var obj in spawnedRewards)
            {
                if (obj != null) Destroy(obj);
            }
            spawnedRewards.Clear();

            if (rewards == null || rewards.Length == 0 || rewardsContainer == null || rewardItemPrefab == null)
                return;

            foreach (var reward in rewards)
            {
                var obj = Instantiate(rewardItemPrefab, rewardsContainer);
                var item = obj.GetComponent<QuestRewardItem>();
                if (item != null)
                {
                    item.SetData(reward);
                }
                spawnedRewards.Add(obj);
            }
        }

        #endregion

        #region 事件处理

        private void OnQuestStateChanged()
        {
            if (!IsShowing()) return;

            string previousSelection = selectedQuestName;
            RefreshList();

            // 尝试重新选中之前的任务
            if (!string.IsNullOrEmpty(previousSelection))
                SelectQuest(previousSelection);
            else
                SelectFirstQuest();
        }

        #endregion

        #region 动画

        private IEnumerator FadeIn()
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }

        private IEnumerator FadeOut()
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
                yield return null;
            }

            canvasGroup.alpha = 0f;
            if (panelRoot != null) panelRoot.SetActive(false);

            // 触发并清空关闭回调
            var cb = onCloseCallback;
            onCloseCallback = null;
            cb?.Invoke();
        }

        #endregion
    }
}
