using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PopLife.Data;
using PopLife.UI.Quest;

namespace PopLife.AlanBot.UI
{
    /// <summary>
    /// 日历面板 — 集成到 AlanBot 面板流程中
    /// Header: 季节名/图标 + 年份 + 导航按钮
    /// Grid: 12个 DayBlock 显示当前浏览季节的每一天
    /// 支持季节事件、Buffer、Due Date 的标签显示和交互
    /// </summary>
    public class CalendarPanel : MonoBehaviour
    {
        [Header("面板控制")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Button closeButton;

        [Header("Header - 左侧")]
        [SerializeField] private TMP_Text seasonText;
        [SerializeField] private Image seasonIcon;

        [Header("Header - 右侧")]
        [SerializeField] private TMP_Text yearText;

        [Header("Header - 导航")]
        [SerializeField] private Button prevSeasonButton;
        [SerializeField] private Button todayButton;
        [SerializeField] private Button nextSeasonButton;

        [Header("Season Icons")]
        [SerializeField] private Sprite springIcon;
        [SerializeField] private Sprite summerIcon;
        [SerializeField] private Sprite autumnIcon;
        [SerializeField] private Sprite winterIcon;

        [Header("Grid")]
        [SerializeField] private Transform gridContainer; // 带 GridLayoutGroup 的父级
        [SerializeField] private CalendarDayBlock dayBlockPrefab;

        [Header("Tooltip")]
        [SerializeField] private CalendarTooltip tooltip;

        [Header("路由引用")]
        [Tooltip("Due Date 点击时打开的任务日志面板")]
        [SerializeField] private QuestLogPanel questLogPanel;

        [Header("动画")]
        [SerializeField] private float fadeInDuration = 0.2f;
        [SerializeField] private float fadeOutDuration = 0.15f;

        // 关闭回调（AlanBot callback chain）
        private Action onCloseCallback;
        private Coroutine fadeCoroutine;

        // 当前浏览的季节第一天的 absoluteDay
        private int viewingSeasonStartDay;

        // 缓存的 12 个 DayBlock 实例
        private CalendarDayBlock[] dayBlocks;
        private bool isInitialized;

        #region Unity 生命周期

        private void Awake()
        {
            // 初始隐藏
            if (panelRoot != null)
                panelRoot.SetActive(false);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            // 绑定按钮
            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);
            if (prevSeasonButton != null)
                prevSeasonButton.onClick.AddListener(OnPrevSeason);
            if (todayButton != null)
                todayButton.onClick.AddListener(OnToday);
            if (nextSeasonButton != null)
                nextSeasonButton.onClick.AddListener(OnNextSeason);
        }

        private void Update()
        {
            // Esc 关闭
            if (panelRoot != null && panelRoot.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            {
                Hide();
            }
        }

        #endregion

        #region 显示/隐藏

        /// <summary>
        /// 显示日历面板
        /// </summary>
        public void Show(Action closeCallback = null)
        {
            onCloseCallback = closeCallback;

            // 初始化网格（仅一次）
            if (!isInitialized)
                InitializeGrid();

            // 初始化到当前季节
            int today = DayLoopManager.Instance != null ? DayLoopManager.Instance.currentDay : 1;
            viewingSeasonStartDay = CalendarUtils.GetSeasonStartDay(today);

            // 刷新显示
            RefreshDisplay();

            // 显示面板
            if (panelRoot != null)
                panelRoot.SetActive(true);

            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeCanvasGroup(0f, 1f, fadeInDuration, true, true));

            // 播放音效
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound(AudioKeys.UI_CLICK);
        }

        /// <summary>
        /// 隐藏面板并触发回调
        /// </summary>
        public void Hide()
        {
            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeAndHide());

            // 立即隐藏 Tooltip（面板在淡出，不需要 tooltip 还缓缓消失）
            if (tooltip != null)
                tooltip.HideImmediate();
        }

        /// <summary>
        /// 纯淡出，不触发 onCloseCallback（Due Date 路由专用）
        /// </summary>
        private void HideWithoutCallback()
        {
            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeAndHideNoCallback());

            if (tooltip != null)
                tooltip.HideImmediate();
        }

        private IEnumerator FadeAndHide()
        {
            yield return FadeCanvasGroup(1f, 0f, fadeOutDuration, false, false);

            if (panelRoot != null)
                panelRoot.SetActive(false);

            // 触发回调
            var cb = onCloseCallback;
            onCloseCallback = null;
            cb?.Invoke();
        }

        private IEnumerator FadeAndHideNoCallback()
        {
            yield return FadeCanvasGroup(1f, 0f, fadeOutDuration, false, false);

            if (panelRoot != null)
                panelRoot.SetActive(false);

            // 故意不触发 onCloseCallback
        }

        private IEnumerator FadeCanvasGroup(float startAlpha, float endAlpha, float duration,
            bool interactable, bool blocksRaycasts)
        {
            if (canvasGroup == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
                yield return null;
            }

            canvasGroup.alpha = endAlpha;
            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = blocksRaycasts;
        }

        #endregion

        #region 网格初始化与刷新

        /// <summary>
        /// 创建 12 个 DayBlock（仅调用一次）
        /// </summary>
        private void InitializeGrid()
        {
            if (dayBlockPrefab == null || gridContainer == null)
            {
                Debug.LogWarning("[CalendarPanel] dayBlockPrefab 或 gridContainer 未设置");
                isInitialized = true;
                return;
            }

            dayBlocks = new CalendarDayBlock[CalendarUtils.DaysPerSeason];
            for (int i = 0; i < CalendarUtils.DaysPerSeason; i++)
            {
                var block = Instantiate(dayBlockPrefab, gridContainer);
                dayBlocks[i] = block;
            }

            isInitialized = true;
        }

        /// <summary>
        /// 刷新 Header 和所有 12 个 DayBlock 的内容
        /// </summary>
        private void RefreshDisplay()
        {
            int startingYear = DayLoopManager.Instance != null
                ? DayLoopManager.Instance.StartingYear : 2126;
            int today = DayLoopManager.Instance != null
                ? DayLoopManager.Instance.currentDay : 1;

            // Header
            var seasonDate = CalendarUtils.AbsoluteDayToDate(viewingSeasonStartDay, startingYear);

            if (seasonText != null)
                seasonText.text = CalendarUtils.GetSeasonDisplayName(seasonDate.season);

            if (seasonIcon != null)
            {
                seasonIcon.sprite = GetSeasonSprite(seasonDate.season);
                seasonIcon.enabled = seasonIcon.sprite != null;
            }

            if (yearText != null)
                yearText.text = $"YR {seasonDate.year}";

            // 预取 Due Date 数据（避免在循环中重复查询）
            var questDeadlines = QuestDataService.Instance != null
                ? QuestDataService.Instance.GetActiveQuestDeadlines()
                : new List<(string questName, string displayTitle, int dueAbsoluteDay, string giverName)>();

            // 刷新 12 个 DayBlock
            if (dayBlocks == null) return;

            for (int i = 0; i < CalendarUtils.DaysPerSeason; i++)
            {
                if (dayBlocks[i] == null) continue;

                int absoluteDay = viewingSeasonStartDay + i;
                bool isToday = absoluteDay == today;

                // 收集该日的所有事件条目
                var entries = BuildEntriesForDay(absoluteDay, questDeadlines);

                dayBlocks[i].Setup(absoluteDay, isToday, entries,
                    onLabelClick: OnLabelClicked,
                    onLabelHoverStart: OnLabelHoverStart,
                    onLabelHoverEnd: OnLabelHoverEnd);
            }
        }

        /// <summary>
        /// 构建指定日期的所有事件条目
        /// </summary>
        private List<CalendarEventEntry> BuildEntriesForDay(int absoluteDay,
            List<(string questName, string displayTitle, int dueAbsoluteDay, string giverName)> questDeadlines)
        {
            var entries = new List<CalendarEventEntry>();

            // 1. 季节事件
            if (CalendarManager.Instance != null)
            {
                var seasonEvents = CalendarManager.Instance.GetEventsForDay(absoluteDay);
                foreach (var evt in seasonEvents)
                {
                    entries.Add(new CalendarEventEntry
                    {
                        type = CalendarEventType.SeasonEvent,
                        displayName = evt.displayName,
                        description = evt.description
                    });
                }

                // 2. 活跃的 Buffer
                var buffers = CalendarManager.Instance.GetBuffersForDay(absoluteDay);
                foreach (var buf in buffers)
                {
                    if (buf.data == null) continue;
                    entries.Add(new CalendarEventEntry
                    {
                        type = CalendarEventType.Buffer,
                        displayName = buf.data.displayName,
                        description = buf.data.description
                    });
                }
            }

            // 3. Due Date（任务截止日）
            foreach (var deadline in questDeadlines)
            {
                if (deadline.dueAbsoluteDay == absoluteDay)
                {
                    entries.Add(new CalendarEventEntry
                    {
                        type = CalendarEventType.DueDate,
                        displayName = deadline.displayTitle,
                        questName = deadline.questName,
                        giverName = deadline.giverName
                    });
                }
            }

            return entries;
        }

        #endregion

        #region 导航

        private void OnPrevSeason()
        {
            viewingSeasonStartDay -= CalendarUtils.DaysPerSeason;
            if (viewingSeasonStartDay < 1)
                viewingSeasonStartDay = 1;
            RefreshDisplay();
        }

        private void OnNextSeason()
        {
            viewingSeasonStartDay += CalendarUtils.DaysPerSeason;
            RefreshDisplay();
        }

        private void OnToday()
        {
            int today = DayLoopManager.Instance != null ? DayLoopManager.Instance.currentDay : 1;
            viewingSeasonStartDay = CalendarUtils.GetSeasonStartDay(today);
            RefreshDisplay();
        }

        #endregion

        #region 事件标签交互

        private void OnLabelClicked(CalendarEventEntry entry)
        {
            // 仅 DueDate 响应点击
            if (entry.type == CalendarEventType.DueDate)
            {
                OnDueDateClicked(entry);
            }
        }

        private void OnLabelHoverStart(CalendarEventEntry entry, Vector2 screenPos)
        {
            if (tooltip != null)
                tooltip.Show(entry, screenPos);
        }

        private void OnLabelHoverEnd()
        {
            if (tooltip != null)
                tooltip.Hide();
        }

        /// <summary>
        /// Due Date 标签点击路由：
        /// Calendar → QuestLogPanel → AlanBotSelectionPanel
        /// </summary>
        private void OnDueDateClicked(CalendarEventEntry entry)
        {
            if (string.IsNullOrEmpty(entry.questName)) return;

            // 1. 保存原始关闭回调（正常情况下会 re-show AlanBotSelectionPanel）
            var originalCloseCallback = onCloseCallback;
            onCloseCallback = null; // 防止 Hide 触发它

            // 2. 纯淡出，不触发任何回调
            HideWithoutCallback();

            // 3. 打开 QuestLogPanel，传入关闭回调恢复 AlanBot
            if (questLogPanel != null)
            {
                questLogPanel.Show(
                    focusQuestName: entry.questName,
                    closeCallback: () => originalCloseCallback?.Invoke()
                );
            }
            else
            {
                // 找不到 QuestLogPanel 时走原始回调链
                Debug.LogWarning("[CalendarPanel] questLogPanel 引用为空，无法打开任务日志");
                originalCloseCallback?.Invoke();
            }
        }

        #endregion

        #region 工具方法

        private Sprite GetSeasonSprite(Season season) => season switch
        {
            Season.Spring => springIcon,
            Season.Summer => summerIcon,
            Season.Autumn => autumnIcon,
            Season.Winter => winterIcon,
            _ => null
        };

        #endregion
    }
}
