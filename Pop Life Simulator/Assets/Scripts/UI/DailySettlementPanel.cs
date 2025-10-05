using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PopLife
{
    public class DailySettlementPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI dayText;
        [SerializeField] private TextMeshProUGUI totalSaleText;
        [SerializeField] private TextMeshProUGUI totalExpensesText;
        [SerializeField] private TextMeshProUGUI dailyIncomeText;
        [SerializeField] private TextMeshProUGUI totalCustomersText;
        [SerializeField] private TextMeshProUGUI fameEarnedText;
        [SerializeField] private Button continueButton;

        private void Awake()
        {
            if (continueButton != null)
            {
                continueButton.onClick.AddListener(OnContinueClicked);
            }
        }

        public void ShowSettlement(DailySettlementData data)
        {
            // 更新UI文本
            if (dayText != null)
                dayText.text = $"Day {data.day}";

            if (totalSaleText != null)
                totalSaleText.text = $"Daily Total Sale: ${data.totalSale:F2}";

            if (totalExpensesText != null)
                totalExpensesText.text = $"Daily Total Expenses: ${data.totalExpenses:F2}";

            if (dailyIncomeText != null)
            {
                dailyIncomeText.text = $"Daily Income: ${data.dailyIncome:F2}";
                // 根据收支情况改变颜色
                dailyIncomeText.color = data.dailyIncome >= 0 ? Color.green : Color.red;
            }

            if (totalCustomersText != null)
                totalCustomersText.text = $"Daily Total Customers: {data.totalCustomers}";

            if (fameEarnedText != null)
                fameEarnedText.text = $"Fame Earned: +{data.fameEarned}";

            // 应用声誉奖励到 ResourceManager
            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.fame += data.fameEarned;
            }
        }

        private void OnContinueClicked()
        {
            // 隐藏面板
            gameObject.SetActive(false);

            // 通知 DayLoopManager 进入下一天
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.AdvanceToNextDay();
            }
        }
    }
}
