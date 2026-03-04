using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using PopLife.Data;

namespace PopLife.AlanBot.UI
{
    /// <summary>
    /// 日历网格中的单个日期格子
    /// 包含日期编号、今日指示器、事件标签列表
    /// </summary>
    public class CalendarDayBlock : MonoBehaviour
    {
        [SerializeField] private TMP_Text dayNumberText;
        [SerializeField] private GameObject todayIndicator;
        [SerializeField] private Transform eventLabelContainer; // 带 VerticalLayoutGroup
        [SerializeField] private CalendarEventLabel eventLabelPrefab;

        private readonly List<CalendarEventLabel> spawnedLabels = new();

        /// <summary>
        /// 设置日期格子内容
        /// </summary>
        public void Setup(int absoluteDay, bool isToday,
                          List<CalendarEventEntry> entries,
                          Action<CalendarEventEntry> onLabelClick,
                          Action<CalendarEventEntry, Vector2> onLabelHoverStart,
                          Action onLabelHoverEnd)
        {
            int startingYear = DayLoopManager.Instance != null
                ? DayLoopManager.Instance.StartingYear : 2126;
            var date = CalendarUtils.AbsoluteDayToDate(absoluteDay, startingYear);

            // 日期编号
            if (dayNumberText != null)
                dayNumberText.text = date.dayInSeason.ToString();

            // 今日指示器
            if (todayIndicator != null)
                todayIndicator.SetActive(isToday);

            // 清除旧标签
            ClearLabels();

            // 创建新标签
            if (eventLabelPrefab == null || eventLabelContainer == null) return;

            foreach (var entry in entries)
            {
                var label = Instantiate(eventLabelPrefab, eventLabelContainer);
                label.Setup(entry, onLabelClick, onLabelHoverStart, onLabelHoverEnd);
                spawnedLabels.Add(label);
            }
        }

        /// <summary>
        /// 清除所有事件标签
        /// </summary>
        private void ClearLabels()
        {
            foreach (var label in spawnedLabels)
            {
                if (label != null)
                    Destroy(label.gameObject);
            }
            spawnedLabels.Clear();
        }
    }
}
