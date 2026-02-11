using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PopLife.UI.Quest
{
    /// <summary>
    /// Tooltip 中的任务条目行
    /// 显示条目文本 + 勾选状态
    /// </summary>
    public class QuestTooltipEntryItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI entryText;
        [SerializeField] private Image checkmarkIcon;

        [Header("Colors")]
        [SerializeField] private Color completedColor = new Color(0.4f, 1f, 0.4f);
        [SerializeField] private Color pendingColor = new Color(0.6f, 0.6f, 0.6f);
        [SerializeField] private Color completedTextColor = new Color(0.6f, 0.6f, 0.6f);
        [SerializeField] private Color pendingTextColor = Color.white;

        public void SetData(QuestEntryInfo entry)
        {
            if (entryText != null)
            {
                entryText.text = entry.text;
                entryText.fontStyle = entry.isCompleted ? FontStyles.Strikethrough : FontStyles.Normal;
                entryText.color = entry.isCompleted ? completedTextColor : pendingTextColor;
            }

            if (checkmarkIcon != null)
            {
                checkmarkIcon.color = entry.isCompleted ? completedColor : pendingColor;
            }
        }
    }
}
