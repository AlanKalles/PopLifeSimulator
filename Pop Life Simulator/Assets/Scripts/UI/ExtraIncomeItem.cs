using UnityEngine;
using TMPro;

namespace PopLife.UI
{
    /// <summary>
    /// 结算面板 Extra Income 区域中的一个词条（如 "Lottery Win: $100"）。
    /// 由 NewDailySettlementPanel.PopulateExtraIncome 动态实例化。
    /// </summary>
    public class ExtraIncomeItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI labelText;
        [SerializeField] private TextMeshProUGUI amountText;

        public void SetData(string label, int amount)
        {
            if (labelText != null) labelText.text = label;
            if (amountText != null) amountText.text = $"+${amount}";
        }
    }
}
