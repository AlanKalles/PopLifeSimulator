using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using PopLife.Data;

namespace PopLife.UI.Quest
{
    /// <summary>
    /// QuestLogPanel 左侧列表中的任务条目
    /// 显示类型色条、标题、进度，支持选中高亮
    /// </summary>
    public class QuestLogListEntry : MonoBehaviour, IPointerClickHandler
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private Image typeIndicator;
        [SerializeField] private Image backgroundImage;

        [Header("Type Indicator Sprites")]
        [SerializeField] private Sprite mainQuestSprite;
        [SerializeField] private Sprite sideQuestSprite;

        [Header("Colors")]
        [SerializeField] private Color mainQuestColor = new Color(1f, 0.84f, 0f);
        [SerializeField] private Color sideQuestColor = Color.white;
        [SerializeField] private Color selectedBgColor = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] private Color normalBgColor = new Color(0f, 0f, 0f, 0f);
        [SerializeField] private Color completedTextColor = new Color(0.5f, 0.5f, 0.5f);

        private QuestViewModel data;
        private Action<QuestLogListEntry> onClicked;
        private bool isCompleted;

        public string QuestName => data.questName;

        /// <summary>
        /// 初始化条目
        /// </summary>
        /// <param name="viewModel">任务数据</param>
        /// <param name="clickCallback">点击回调</param>
        /// <param name="dimmed">是否灰化显示（已完成/失败）</param>
        public void Initialize(QuestViewModel viewModel, Action<QuestLogListEntry> clickCallback, bool dimmed = false)
        {
            data = viewModel;
            onClicked = clickCallback;
            isCompleted = dimmed;

            if (titleText != null)
            {
                titleText.text = viewModel.displayTitle;
                if (dimmed)
                {
                    titleText.color = completedTextColor;
                }
                else
                {
                    titleText.color = viewModel.questType == QuestType.Main ? mainQuestColor : sideQuestColor;
                }
            }

            if (progressText != null)
            {
                if (viewModel.totalEntries > 0)
                {
                    progressText.gameObject.SetActive(true);
                    progressText.text = $"{viewModel.completedEntries}/{viewModel.totalEntries}";
                    if (dimmed) progressText.color = completedTextColor;
                }
                else
                {
                    progressText.gameObject.SetActive(false);
                }
            }

            if (typeIndicator != null)
            {
                // Sprite 切换
                Sprite targetSprite = viewModel.questType == QuestType.Main ? mainQuestSprite : sideQuestSprite;
                if (targetSprite != null)
                    typeIndicator.sprite = targetSprite;

                typeIndicator.color = dimmed ? completedTextColor : Color.white;
            }

            SetSelected(false);
        }

        /// <summary>
        /// 设置选中/未选中高亮状态
        /// </summary>
        public void SetSelected(bool selected)
        {
            if (backgroundImage != null)
                backgroundImage.color = selected ? selectedBgColor : normalBgColor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            onClicked?.Invoke(this);
        }
    }
}
