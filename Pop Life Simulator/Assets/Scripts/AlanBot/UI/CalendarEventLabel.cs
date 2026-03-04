using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace PopLife.AlanBot.UI
{
    /// <summary>
    /// 日历格子内的事件标签
    /// DueDate：使用 dueDateSprite，hover 时颜色变化
    /// Season/Buffer：使用 unselected/selected Sprite 切换
    /// 点击：仅 DueDate 类型响应
    /// </summary>
    public class CalendarEventLabel : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private Image background;

        [Header("Season / Buffer")]
        [SerializeField] private Sprite unselectedSprite;
        [SerializeField] private Sprite selectedSprite;

        [Header("Due Date")]
        [SerializeField] private Sprite dueDateSprite;
        [SerializeField] private Color dueDateNormalColor = Color.white;
        [SerializeField] private Color dueDateHoverColor = new Color(1f, 0.8f, 0.8f, 1f);

        private CalendarEventEntry entry;
        private Action<CalendarEventEntry> onClick;
        private Action<CalendarEventEntry, Vector2> onHoverStart;
        private Action onHoverEnd;

        /// <summary>
        /// 初始化标签
        /// </summary>
        public void Setup(CalendarEventEntry entry,
                          Action<CalendarEventEntry> onClick,
                          Action<CalendarEventEntry, Vector2> onHoverStart,
                          Action onHoverEnd)
        {
            this.entry = entry;
            this.onClick = onClick;
            this.onHoverStart = onHoverStart;
            this.onHoverEnd = onHoverEnd;

            // 设置显示文本
            if (labelText != null)
            {
                labelText.text = entry.type == CalendarEventType.DueDate
                    ? "!!! due date"
                    : entry.displayName;
            }

            // 初始 Sprite 和颜色
            if (background != null)
            {
                if (entry.type == CalendarEventType.DueDate)
                {
                    if (dueDateSprite != null)
                        background.sprite = dueDateSprite;
                    background.color = dueDateNormalColor;
                }
                else
                {
                    if (unselectedSprite != null)
                        background.sprite = unselectedSprite;
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (background != null)
            {
                if (entry.type == CalendarEventType.DueDate)
                {
                    // DueDate：颜色变化
                    background.color = dueDateHoverColor;
                }
                else
                {
                    // Season/Buffer：Sprite 切换
                    if (selectedSprite != null)
                        background.sprite = selectedSprite;
                }
            }

            // 触发 Tooltip
            onHoverStart?.Invoke(entry, eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (background != null)
            {
                if (entry.type == CalendarEventType.DueDate)
                {
                    // DueDate：恢复颜色
                    background.color = dueDateNormalColor;
                }
                else
                {
                    // Season/Buffer：恢复 Sprite
                    if (unselectedSprite != null)
                        background.sprite = unselectedSprite;
                }
            }

            // 隐藏 Tooltip
            onHoverEnd?.Invoke();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // 仅 DueDate 类型可点击
            if (entry.type == CalendarEventType.DueDate)
            {
                onClick?.Invoke(entry);
            }
        }
    }
}
