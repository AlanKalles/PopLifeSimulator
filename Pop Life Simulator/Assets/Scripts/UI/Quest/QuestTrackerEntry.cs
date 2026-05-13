using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using PopLife.Data;

namespace PopLife.UI.Quest
{
    /// <summary>
    /// 追踪面板中的单个任务条目
    /// 支持悬停显示 Tooltip、点击打开详情面板
    /// </summary>
    public class QuestTrackerEntry : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI deadlineText;
        [SerializeField] private Slider progressBar;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image typeIcon;

        [Header("Type Icon Sprites")]
        [SerializeField] private Sprite mainQuestSprite;
        [SerializeField] private Sprite sideQuestSprite;

        [Header("Colors")]
        [SerializeField] private Color mainQuestColor = new Color(1f, 0.84f, 0f);   // 金色
        [SerializeField] private Color sideQuestColor = Color.white;
        [SerializeField] private Color normalDeadlineColor = new Color(0.8f, 0.8f, 0.8f);
        [SerializeField] private Color hoverBgColor = new Color(1f, 1f, 1f, 0.1f);
        [SerializeField] private Color normalBgColor = new Color(0f, 0f, 0f, 0f);

        private QuestViewModel data;
        private QuestTooltip tooltip;
        private QuestLogPanel logPanel;

        /// <summary>
        /// 初始化条目数据和引用
        /// </summary>
        public void Initialize(QuestViewModel viewModel, QuestTooltip tooltip, QuestLogPanel logPanel)
        {
            this.data = viewModel;
            this.tooltip = tooltip;
            this.logPanel = logPanel;

            // 标题
            if (titleText != null)
            {
                titleText.text = viewModel.displayTitle;
                titleText.color = viewModel.questType == QuestType.Main ? mainQuestColor : sideQuestColor;
            }

            // DDL：无截止显示 "Unlimited time"，有截止显示绝对日期 "Spring Day 11"
            if (deadlineText != null)
            {
                deadlineText.gameObject.SetActive(true);
                deadlineText.color = normalDeadlineColor;
                if (viewModel.deadlineDay < 0)
                {
                    deadlineText.text = "Unlimited time";
                }
                else
                {
                    int startingYear = DayLoopManager.Instance != null
                        ? DayLoopManager.Instance.StartingYear
                        : 2126;
                    deadlineText.text = CalendarUtils.FormatShortDate(viewModel.deadlineDay, startingYear);
                }
            }

            // 进度条
            if (progressBar != null)
            {
                if (viewModel.totalEntries > 0)
                {
                    progressBar.gameObject.SetActive(true);
                    progressBar.maxValue = viewModel.totalEntries;
                    progressBar.value = viewModel.completedEntries;
                }
                else
                {
                    progressBar.gameObject.SetActive(false);
                }
            }

            // 进度文本
            if (progressText != null)
            {
                if (viewModel.totalEntries > 0)
                {
                    progressText.gameObject.SetActive(true);
                    progressText.text = $"{viewModel.completedEntries}/{viewModel.totalEntries}";
                }
                else
                {
                    progressText.gameObject.SetActive(false);
                }
            }

            // 任务类型图标
            if (typeIcon != null)
            {
                Sprite targetSprite = viewModel.questType == QuestType.Main ? mainQuestSprite : sideQuestSprite;
                if (targetSprite != null)
                {
                    typeIcon.sprite = targetSprite;
                    typeIcon.enabled = true;
                }
                else
                {
                    typeIcon.enabled = false;
                }
            }

            // 背景默认状态
            if (backgroundImage != null)
                backgroundImage.color = normalBgColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (backgroundImage != null)
                backgroundImage.color = hoverBgColor;

            tooltip?.Show(data.questName, eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (backgroundImage != null)
                backgroundImage.color = normalBgColor;

            tooltip?.Hide();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            tooltip?.HideImmediate();
            logPanel?.Show(data.questName);

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound(AudioKeys.UI_CLICK);
        }
    }
}
