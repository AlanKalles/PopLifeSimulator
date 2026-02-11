using System.Collections;
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
        [SerializeField] private CanvasGroup contentCanvasGroup;
        [SerializeField] private TextMeshProUGUI headerText;

        [Header("引用")]
        [SerializeField] private QuestTooltip questTooltip;
        [SerializeField] private QuestDetailPanel questDetailPanel;

        [Header("Animation")]
        [SerializeField] private float collapseDuration = 0.25f;

        [Header("Settings")]
        [SerializeField] private int maxDisplayCount = 5;

        private bool isCollapsed = false;
        private readonly List<QuestTrackerEntry> activeEntries = new();
        private Coroutine collapseCoroutine;

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

            // 初始化折叠图标
            if (collapseIcon != null) collapseIcon.SetActive(true);
            if (expandIcon != null) expandIcon.SetActive(false);

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

            // 创建条目（最多 maxDisplayCount 条）
            int count = Mathf.Min(quests.Count, maxDisplayCount);
            for (int i = 0; i < count; i++)
            {
                if (questEntryPrefab == null || contentContainer == null) break;

                var obj = Instantiate(questEntryPrefab, contentContainer);
                var entry = obj.GetComponent<QuestTrackerEntry>();
                if (entry != null)
                {
                    entry.Initialize(quests[i], questTooltip, questDetailPanel);
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

            if (collapseCoroutine != null) StopCoroutine(collapseCoroutine);
            collapseCoroutine = StartCoroutine(AnimateCollapse(isCollapsed));

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

        /// <summary>
        /// 折叠/展开动画
        /// 使用 EaseOutCubic 缓动，Time.unscaledDeltaTime 不受游戏时间倍速影响
        /// </summary>
        private IEnumerator AnimateCollapse(bool collapse)
        {
            if (contentCanvasGroup == null) yield break;

            float elapsed = 0f;
            float startAlpha = contentCanvasGroup.alpha;
            float targetAlpha = collapse ? 0f : 1f;

            while (elapsed < collapseDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                // EaseOutCubic 缓动
                float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / collapseDuration), 3f);
                contentCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            contentCanvasGroup.alpha = targetAlpha;
            contentCanvasGroup.interactable = !collapse;
            contentCanvasGroup.blocksRaycasts = !collapse;

            // 折叠时隐藏内容容器（节省性能）
            if (contentContainer != null)
                contentContainer.gameObject.SetActive(!collapse);

            collapseCoroutine = null;
        }

        #endregion
    }
}
