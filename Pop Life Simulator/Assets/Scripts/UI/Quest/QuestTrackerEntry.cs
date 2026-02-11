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

        [Header("Colors")]
        [SerializeField] private Color mainQuestColor = new Color(1f, 0.84f, 0f);   // 金色
        [SerializeField] private Color sideQuestColor = Color.white;
        [SerializeField] private Color urgentColor = new Color(1f, 0.27f, 0.27f);    // 红色
        [SerializeField] private Color normalDeadlineColor = new Color(0.8f, 0.8f, 0.8f);
        [SerializeField] private Color hoverBgColor = new Color(1f, 1f, 1f, 0.1f);
        [SerializeField] private Color normalBgColor = new Color(0f, 0f, 0f, 0f);

        private QuestViewModel data;
        private QuestTooltip tooltip;
        private QuestDetailPanel detailPanel;

        /// <summary>
        /// 初始化条目数据和引用
        /// </summary>
        public void Initialize(QuestViewModel viewModel, QuestTooltip tooltip, QuestDetailPanel detailPanel)
        {
            this.data = viewModel;
            this.tooltip = tooltip;
            this.detailPanel = detailPanel;

            // 标题
            if (titleText != null)
            {
                titleText.text = viewModel.displayTitle;
                titleText.color = viewModel.questType == QuestType.Main ? mainQuestColor : sideQuestColor;
            }

            // DDL
            if (deadlineText != null)
            {
                if (viewModel.remainingDays < 0)
                {
                    deadlineText.gameObject.SetActive(false);
                }
                else
                {
                    deadlineText.gameObject.SetActive(true);
                    deadlineText.text = viewModel.remainingDays <= 1
                        ? $"{viewModel.remainingDays}d left!"
                        : $"{viewModel.remainingDays}d left";
                    deadlineText.color = viewModel.remainingDays <= 1 ? urgentColor : normalDeadlineColor;
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
            detailPanel?.Show(data.questName);

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound(AudioKeys.UI_CLICK);
        }
    }
}
