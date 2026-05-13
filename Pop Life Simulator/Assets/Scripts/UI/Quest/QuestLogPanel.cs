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
        [SerializeField] private GameObject entriesSection;
        [SerializeField] private Transform entriesContainer;
        [SerializeField] private GameObject entryItemPrefab;

        [Header("右侧详情区 - 截止日期")]
        [SerializeField] private TextMeshProUGUI deadlineText;

        [Header("右侧详情区 - 颁布者")]
        [SerializeField] private GameObject portraitSection;
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

            if (DayLoopManager.Instance != null)
                DayLoopManager.Instance.OnDailySettlement += OnDailySettlement;
        }

        private void OnDisable()
        {
            if (QuestDataService.Instance != null)
                QuestDataService.Instance.OnQuestStateChanged -= OnQuestStateChanged;

            if (DayLoopManager.Instance != null)
                DayLoopManager.Instance.OnDailySettlement -= OnDailySettlement;
        }

        // 结算时自动关闭面板
        private void OnDailySettlement(DailySettlementData data)
        {
            if (IsShowing())
                Hide();
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

            // 先激活面板根，保证后续 Instantiate 出来的 UI 子物体处于 active 容器内，
            // LayoutGroup / ContentSizeFitter / TMP 的 preferred size 计算才会正常发生；
            // 否则布局会推迟到 SetActive(true) 之后的下一帧才 settle，与 FadeIn 同步进行 → 视觉抖动。
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (panelRoot != null && !panelRoot.activeSelf) panelRoot.SetActive(true);

            RefreshList();

            if (!string.IsNullOrEmpty(focusQuestName))
                SelectQuest(focusQuestName);
            else
                SelectFirstQuest();

            // 同步强制一次完整布局重算，避免 fade-in 过程中布局还在 settle
            ForceLayoutRebuild();

            StopAllCoroutines();
            StartCoroutine(FadeIn());

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound(AudioKeys.UI_CLICK);
        }

        /// <summary>
        /// 强制完整刷新一次布局：Canvas 同步重绘 + 嵌套 LayoutGroup 按 leaf-first 顺序立即重算。
        /// 必要原因：嵌套 LayoutGroup + ContentSizeFitter + TMP 默认需要 1-2 帧才稳定。
        /// 内层 ContentSizeFitter 必须先确定自己的尺寸，外层 LayoutGroup 才能正确读到 preferred size，
        /// 否则会出现"有时坍缩有时正常"的随机抖动。
        /// </summary>
        private void ForceLayoutRebuild()
        {
            if (panelRoot == null) return;

            // 先驱动一次 Canvas 同步刷新，让新 Instantiate 的 TMP 立刻生成 mesh，
            // 否则后续 LayoutRebuilder 读到的 preferredHeight 可能是 0 → 父容器坍缩
            Canvas.ForceUpdateCanvases();

            // leaf-first：先重算右侧详情中的两个内层容器，再重算根
            RebuildIfRect(entriesContainer);
            RebuildIfRect(rewardsContainer);

            var rt = panelRoot.transform as RectTransform;
            if (rt != null)
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        private static void RebuildIfRect(Transform t)
        {
            if (t is RectTransform rt)
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
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
            if (entry == null) return;

            // 已经是当前选中项：仅播放音效，跳过整套 destroy + instantiate + 布局重算。
            // 否则嵌套 LayoutGroup + ContentSizeFitter + TMP 的多帧 settle 流程
            // 会在每次重复点击时随机出现"坍缩-恢复"抖动。
            if (entry.QuestName == selectedQuestName)
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlaySound(AudioKeys.UI_CLICK);
                return;
            }

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

                // 切换 quest 后立刻强制完整布局重算：
                // PopulateEntries/PopulateRewards 改了子物体集合 + TMP 文本，
                // 单靠 Unity 自带的 dirty 标记需要 1-2 帧 + 多次 pass 才能 settle 嵌套
                // ContentSizeFitter，期间玩家会看到坍缩或参差。
                ForceLayoutRebuild();
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

            // 截止日期：无截止显示 "Unlimited time"；有截止显示绝对日期，如 "Spring Day 11"
            if (deadlineText != null)
            {
                if (detail.deadlineDay < 0)
                {
                    deadlineText.text = "Unlimited time";
                }
                else
                {
                    int startingYear = DayLoopManager.Instance != null
                        ? DayLoopManager.Instance.StartingYear
                        : 2126;
                    deadlineText.text = CalendarUtils.FormatShortDate(detail.deadlineDay, startingYear);
                }
            }

            // 颁布者：portraitSection 和 giverSection 是两个独立块，没有 giver 时整体隐藏
            bool hasGiver = !string.IsNullOrEmpty(detail.giverName);
            if (portraitSection != null) portraitSection.SetActive(hasGiver);
            if (giverSection != null) giverSection.SetActive(hasGiver);

            if (hasGiver)
            {
                if (giverNameText != null) giverNameText.text = detail.giverName;
                if (giverPortrait != null)
                {
                    if (detail.giverPortrait != null)
                    {
                        giverPortrait.sprite = detail.giverPortrait;
                        giverPortrait.preserveAspect = true;
                        giverPortrait.enabled = true;
                    }
                    else
                    {
                        giverPortrait.enabled = false;
                    }
                }
            }

            // 条目列表
            PopulateEntries(detail.entries);

            // 奖励列表
            PopulateRewards(detail.rewards);
        }

        private void PopulateEntries(QuestEntryInfo[] entries)
        {
            // 关键：先 SetParent(null) 把旧物体立刻从 LayoutGroup 中脱钩再 Destroy。
            // Unity 的 Destroy 是延迟到帧末执行的，若直接 Destroy，本帧 LayoutGroup 仍把它当作子物体，
            // 与即将 Instantiate 的新条目"重叠计算"一次布局，造成 ContentSizeFitter 抖动/坍缩。
            foreach (var obj in spawnedEntries)
            {
                if (obj == null) continue;
                obj.transform.SetParent(null, false);
                Destroy(obj);
            }
            spawnedEntries.Clear();

            // 无 condition 的自动完成任务：整块隐藏（含 "Objectives:" 标题）
            bool hasEntries = entries != null && entries.Length > 0;
            if (entriesSection != null) entriesSection.SetActive(hasEntries);

            if (!hasEntries || entriesContainer == null || entryItemPrefab == null)
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
            // 同 PopulateEntries：先脱钩 LayoutGroup 再 Destroy，规避 deferred-destroy 引发的布局抖动
            foreach (var obj in spawnedRewards)
            {
                if (obj == null) continue;
                obj.transform.SetParent(null, false);
                Destroy(obj);
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
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            // 多等一帧，让 TMP 的 mesh 与嵌套 LayoutGroup 的 ContentSizeFitter 在 alpha 抬起前彻底 settle，
            // 否则同步强制布局后仍可能有一帧的残余抖动可见
            yield return null;

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
