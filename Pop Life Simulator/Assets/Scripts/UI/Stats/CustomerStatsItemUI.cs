using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PopLife.Manager;

namespace PopLife.UI.Stats
{
    /// <summary>
    /// 顾客统计列表项 UI 组件
    /// 显示格式: [Icon | Name | Loyalty Lv.X | Spent: $XXX]
    /// </summary>
    public class CustomerStatsItemUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Image backgroundImage; // 背景图片，用于置灰
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI loyaltyText;
        [SerializeField] private TextMeshProUGUI spentText;

        [Header("Visual Settings")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color grayedOutColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);

        private CustomerStatsData currentData;
        private bool isGrayedOut = false;

        /// <summary>
        /// 设置数据并更新显示
        /// </summary>
        public void SetData(CustomerStatsData data)
        {
            currentData = data;
            UpdateDisplay();
        }

        /// <summary>
        /// 更新消费金额
        /// </summary>
        public void UpdateSpent(int newSpent)
        {
            if (currentData != null)
            {
                currentData.totalSpent = newSpent;
            }

            if (spentText != null)
            {
                spentText.text = $"Spent: ${newSpent}";
            }
        }

        /// <summary>
        /// 设置置灰状态（顾客离店）
        /// </summary>
        public void SetGrayedOut(bool grayed)
        {
            isGrayedOut = grayed;

            // 修改背景图片颜色
            if (backgroundImage != null)
            {
                backgroundImage.color = grayed ? grayedOutColor : normalColor;
            }
        }

        /// <summary>
        /// 更新显示
        /// </summary>
        private void UpdateDisplay()
        {
            if (currentData == null) return;

            // 图标
            if (iconImage != null && currentData.sprite != null)
            {
                iconImage.sprite = currentData.sprite;
                iconImage.enabled = true;
            }
            else if (iconImage != null)
            {
                iconImage.enabled = false;
            }

            // 名称
            if (nameText != null)
            {
                nameText.text = currentData.name;
            }

            // 忠诚度等级
            if (loyaltyText != null)
            {
                loyaltyText.text = $"Loyalty Lv.{currentData.loyaltyLevel}";
            }

            // 消费金额
            if (spentText != null)
            {
                spentText.text = $"Spent: ${currentData.totalSpent}";
            }

            // 应用置灰状态
            SetGrayedOut(currentData.hasLeft);
        }
    }
}
