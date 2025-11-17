using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PopLife.Manager;

namespace PopLife.UI.Stats
{
    /// <summary>
    /// 货架统计列表项 UI 组件
    /// 显示格式: [Icon | Name | Category | Lv.X | Revenue: $XXX | Price: $XX]
    /// </summary>
    public class ShelfStatsItemUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI categoryText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI revenueText;
        [SerializeField] private TextMeshProUGUI priceText;

        private ShelfStatsData currentData;

        /// <summary>
        /// 设置数据并更新显示
        /// </summary>
        public void SetData(ShelfStatsData data)
        {
            currentData = data;
            UpdateDisplay();
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

            // 类别
            if (categoryText != null)
            {
                categoryText.text = currentData.category;
            }

            // 等级
            if (levelText != null)
            {
                levelText.text = $"{currentData.level}";
            }

            // 今日收入
            if (revenueText != null)
            {
                revenueText.text = $"${currentData.todayRevenue}";
            }

            // 单价
            if (priceText != null)
            {
                priceText.text = $"${currentData.unitPrice}";
            }
        }
    }
}
