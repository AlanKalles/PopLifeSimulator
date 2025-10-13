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
        [SerializeField] private TextMeshProUGUI levelUpText;
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

            // 显示升级信息
            if (levelUpText != null)
            {
                if (data.levelUps != null && data.levelUps.Length > 0)
                {
                    // 构建升级列表文本
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    sb.AppendLine($"<b>Leveled Up Customers ({data.levelUps.Length}):</b>");
                    sb.AppendLine();

                    foreach (var levelUp in data.levelUps)
                    {
                        // 格式：- Name: Lv X → Y (+Z XP)
                        sb.Append($"- <b>{levelUp.customerName}</b>: ");
                        sb.Append($"Lv {levelUp.oldLevel} → {levelUp.newLevel}");
                        sb.Append($" <color=#FFD700>(+{levelUp.xpGained} XP)</color>");

                        // 如果跨级升级，添加特殊标记
                        if (levelUp.newLevel - levelUp.oldLevel > 1)
                        {
                            int skippedLevels = levelUp.newLevel - levelUp.oldLevel - 1;
                            sb.Append($" <color=#FF6347>🔥 Skipped {skippedLevels} level(s)!</color>");
                        }

                        sb.AppendLine();
                    }

                    levelUpText.text = sb.ToString();
                }
                else
                {
                    // 没有顾客升级
                    levelUpText.text = "<b>Leveled Up Customers:</b>\n\n<i>None today</i>";
                }
            }

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

            // 通知 DayLoopManager 进入下一天的建造阶段
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.AdvanceToNextDay();
            }
        }
    }
}
