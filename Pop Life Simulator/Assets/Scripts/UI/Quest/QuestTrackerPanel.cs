using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PopLife.Data;

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

        [Header("引用")]
        [SerializeField] private QuestTooltip questTooltip;
        [SerializeField] private QuestLogPanel questLogPanel;

        [Header("Settings")]
        [SerializeField] private int maxDisplayCount = 5;

        private bool isCollapsed = true; // 默认折叠
        private readonly List<QuestTrackerEntry> activeEntries = new();

        private void OnEnable()
        {
            if (QuestDataService.Instance != null)
                QuestDataService.Instance.OnTrackedQuestsChanged += RefreshQuests;

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

            // 默认折叠状态：显示 expandIcon，隐藏 collapseIcon
            if (collapseIcon != null) collapseIcon.SetActive(false);
            if (expandIcon != null) expandIcon.SetActive(true);

            // 初始刷新
            RefreshQuests();
        }

        private void OnDisable()
        {
            if (QuestDataService.Instance != null)
                QuestDataService.Instance.OnTrackedQuestsChanged -= RefreshQuests;

            if (collapseButton != null)
                collapseButton.onClick.RemoveListener(Toggle);
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
        }

        /// <summary>
        /// 切换折叠/展开
        /// </summary>
        public void Toggle()
        {
            isCollapsed = !isCollapsed;

            if (collapseIcon != null) collapseIcon.SetActive(!isCollapsed);
            if (expandIcon != null) expandIcon.SetActive(isCollapsed);

            // 刷新列表（折叠时只显示1条，展开时显示全部）
            RefreshQuests();

            // 播放音效
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound(AudioKeys.UI_CLICK);
        }

        #region 内部方法

        private void UpdateHeader(int totalCount)
        {
            if (headerText != null)
                headerText.text = totalCount > 0 ? $"QUESTS ({totalCount})" : "QUESTS";
        }

        #endregion
    }
}
