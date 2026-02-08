using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PopLife.UI
{
    /// <summary>
    /// 热销商品条目组件 - 管理单个热销商品UI元素的引用
    /// 挂载在 HotSellerItem 预制体的根对象上
    /// </summary>
    public class HotSellerItem : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI rankText;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI revenueText;

        /// <summary>
        /// 设置热销商品数据
        /// </summary>
        /// <param name="rank">排名（1-3）</param>
        /// <param name="productName">商品名称</param>
        /// <param name="sprite">商品图标</param>
        /// <param name="revenue">收入金额</param>
        public void SetData(int rank, string productName, Sprite sprite, float revenue)
        {
            if (rankText != null)
                rankText.text = $"#{rank}";

            if (nameText != null)
                nameText.text = productName;

            if (iconImage != null && sprite != null)
                iconImage.sprite = sprite;

            if (revenueText != null)
                revenueText.text = $"${revenue:F0}";
        }
    }
}
