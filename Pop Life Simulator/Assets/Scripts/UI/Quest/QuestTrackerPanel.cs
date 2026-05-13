using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PopLife.Data;
using PopLife.Quest;

namespace PopLife.UI.Quest
{
    /// <summary>
    /// 任务追踪面板 - 固定在右上角，始终可见，可折叠
    /// 订阅 QuestDataService.OnTrackedQuestsChanged 事件自动刷新
    /// </summary>
    public class QuestTrackerPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private RectTransform contentContainer;
        [SerializeField] private GameObject questEntryPrefab;
        [SerializeField] private Button collapseButton;
        [SerializeField] private GameObject collapseIcon;   // ▼ 展开状态显示
        [SerializeField] private GameObject expandIcon;     // ► 折叠状态显示
        [SerializeField] private TextMeshProUGUI headerText;
        [SerializeField] private TextMeshProUGUI toggleLabel; // 折叠/展开文字
        [SerializeField] private string expandedText = "Collapse";
        [SerializeField] private string collapsedText = "Expand";
        [SerializeField] private Button moreButton; // "..." 按钮，点击打开 QuestLogPanel；应放在 contentContainer 之后的同级位置

        [Header("引用")]
        [SerializeField] private QuestTooltip questTooltip;
        [SerializeField] private QuestLogPanel questLogPanel;

        [Header("Settings")]
        [SerializeField] private int maxDisplayCount = 3;

        private bool isCollapsed = false; // 默认展开
        // 对象池：复用 entry GameObject，避免每次刷新都 Destroy/Instantiate 引起的 GC
        private readonly List<QuestTrackerEntry> entryPool = new();

        private void OnEnable()
        {
            if (QuestDataService.Instance != null)
            {
                QuestDataService.Instance.OnTrackedQuestsChanged += RefreshQuests;
                // 重新启用时立即刷新一次：补上隐藏期间错过的事件
                // （ConstructionModeOverlay 在 Place/Move/Destroy 模式会临时 SetActive(false)）
                RefreshQuests();
            }

            // 触发即完成的任务（CurrentScope 条件已满足、AutoCompleteOnActivation 等）
            // 仅靠 OnTrackedQuestsChanged 在嵌套广播链中可能产生残留条目，这里直接监听
            // 终态事件，确保完成/失败时一定刷新。
            QuestLogicManager.OnQuestCompleted += OnQuestTerminalState;
            QuestLogicManager.OnQuestFailed += OnQuestTerminalState;

            if (collapseButton != null)
                collapseButton.onClick.AddListener(Toggle);

            if (moreButton != null)
                moreButton.onClick.AddListener(OnMoreClicked);
        }

        private void Start()
        {
            // 延迟订阅，确保 QuestDataService 已初始化
            if (QuestDataService.Instance != null)
            {
                QuestDataService.Instance.OnTrackedQuestsChanged -= RefreshQuests;
                QuestDataService.Instance.OnTrackedQuestsChanged += RefreshQuests;
            }

            // 默认展开状态：显示 collapseIcon，隐藏 expandIcon
            if (collapseIcon != null) collapseIcon.SetActive(true);
            if (expandIcon != null) expandIcon.SetActive(false);
            UpdateToggleLabel();

            // 初始刷新
            RefreshQuests();
        }

        private void OnDisable()
        {
            if (QuestDataService.Instance != null)
                QuestDataService.Instance.OnTrackedQuestsChanged -= RefreshQuests;

            QuestLogicManager.OnQuestCompleted -= OnQuestTerminalState;
            QuestLogicManager.OnQuestFailed -= OnQuestTerminalState;

            if (collapseButton != null)
                collapseButton.onClick.RemoveListener(Toggle);

            if (moreButton != null)
                moreButton.onClick.RemoveListener(OnMoreClicked);
        }

        private void OnMoreClicked()
        {
            questLogPanel?.Show();

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound(AudioKeys.UI_CLICK);
        }

        private void OnQuestTerminalState(string questName)
        {
            RefreshQuests();
        }

        /// <summary>
        /// 刷新追踪列表。
        /// 折叠状态下也会被调用——池中数据照常更新（不可见，但保持最新），
        /// 这样玩家展开时立刻看到最新的任务/进度，无需额外刷新。
        /// </summary>
        public void RefreshQuests()
        {
            var quests = QuestDataService.Instance?.GetTrackedQuests();
            int totalCount = quests?.Count ?? 0;
            UpdateHeader(totalCount);

            // 折叠时显示数为 0（条目都隐藏，但下面仍会用最新数据填充已激活的池条目，
            // 以及保持池容量与实际任务数同步——展开时无需 rebuild）
            int displayCount = isCollapsed ? 0 : Mathf.Min(totalCount, maxDisplayCount);

            EnsurePoolCapacity(displayCount);

            // 复用池中条目：前 displayCount 个激活并填充新数据，其余隐藏
            for (int i = 0; i < entryPool.Count; i++)
            {
                var entry = entryPool[i];
                if (entry == null) continue;

                if (i < displayCount)
                {
                    if (!entry.gameObject.activeSelf) entry.gameObject.SetActive(true);
                    entry.Initialize(quests[i], questTooltip, questLogPanel);
                }
                else
                {
                    if (entry.gameObject.activeSelf) entry.gameObject.SetActive(false);
                }
            }

            // 折叠态没有可见 UI 变化，跳过 immediate rebuild；展开时强制立即重建避免与 header 重叠
            if (!isCollapsed && contentContainer != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer);
        }

        /// <summary>
        /// 确保对象池至少有 desired 个 entry GameObject 可用。
        /// 池只增不减——maxDisplayCount=3 上限下，最多保留 3 个实例。
        /// </summary>
        private void EnsurePoolCapacity(int desired)
        {
            while (entryPool.Count < desired)
            {
                if (questEntryPrefab == null || contentContainer == null) return;

                var obj = Instantiate(questEntryPrefab, contentContainer);
                var entry = obj.GetComponent<QuestTrackerEntry>();
                if (entry == null)
                {
                    Destroy(obj);
                    return;
                }
                entryPool.Add(entry);
            }
        }

        /// <summary>
        /// 切换折叠/展开
        /// </summary>
        public void Toggle()
        {
            isCollapsed = !isCollapsed;

            if (collapseIcon != null) collapseIcon.SetActive(!isCollapsed);
            if (expandIcon != null) expandIcon.SetActive(isCollapsed);

            UpdateToggleLabel();

            // 折叠时把所有条目 SetActive(false)；展开时把池中前 maxDisplayCount 个用最新数据填充并显示
            RefreshQuests();

            // 播放音效
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound(AudioKeys.UI_CLICK);
        }

        #region 内部方法

        private void UpdateToggleLabel()
        {
            if (toggleLabel != null)
                toggleLabel.text = isCollapsed ? collapsedText : expandedText;
        }

        private void UpdateHeader(int totalCount)
        {
            if (headerText != null)
                headerText.text = totalCount > 0 ? $"QUESTS ({totalCount})" : "QUESTS";
        }

        #endregion
    }
}
