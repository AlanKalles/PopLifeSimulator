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

        [Header("引用")]
        [SerializeField] private QuestTooltip questTooltip;
        [SerializeField] private QuestLogPanel questLogPanel;

        [Header("Settings")]
        [SerializeField] private int maxDisplayCount = 5;

        private bool isCollapsed = false; // 默认展开
        private readonly List<QuestTrackerEntry> activeEntries = new();

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
        }

        private void OnQuestTerminalState(string questName)
        {
            RefreshQuests();
        }

        /// <summary>
        /// 刷新追踪列表
        /// </summary>
        public void RefreshQuests()
        {
            // 清空旧条目
            foreach (var entry in activeEntries)
            {
                if (entry != null) Destroy(entry.gameObject);
            }
            activeEntries.Clear();

            // 获取数据
            var quests = QuestDataService.Instance?.GetTrackedQuests();
            if (quests == null || quests.Count == 0)
            {
                UpdateHeader(0);
                // 即使列表为空也强制重建：上面的 Destroy 是延迟到帧末，layout
                // 不主动收缩会让已销毁条目的位置看起来还占着 panel
                if (contentContainer != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer);
                return;
            }

            // 折叠时只显示第一条，展开时显示全部（最多 maxDisplayCount 条）
            int count = isCollapsed ? 1 : Mathf.Min(quests.Count, maxDisplayCount);
            for (int i = 0; i < count; i++)
            {
                if (questEntryPrefab == null || contentContainer == null) break;

                var obj = Instantiate(questEntryPrefab, contentContainer);
                var entry = obj.GetComponent<QuestTrackerEntry>();
                if (entry != null)
                {
                    entry.Initialize(quests[i], questTooltip, questLogPanel);
                    activeEntries.Add(entry);
                }
            }

            UpdateHeader(quests.Count);

            // 强制重建布局，避免新条目与 header 重叠
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer);
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

            // 刷新列表（折叠时只显示1条，展开时显示全部）
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
