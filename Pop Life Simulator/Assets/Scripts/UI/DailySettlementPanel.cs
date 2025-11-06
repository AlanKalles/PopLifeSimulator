using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PopLife.Customers.Runtime;

namespace PopLife
{
    public class DailySettlementPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI dayText;

        [Header("Income Section")]
        [SerializeField] private TextMeshProUGUI todaySalesText;           // 今日开店收益
        [SerializeField] private TextMeshProUGUI todayBonusIncomeText;     // 今日开店额外收益
        [SerializeField] private TextMeshProUGUI todayMaintenanceText;     // 今日维护费
        [SerializeField] private TextMeshProUGUI todayMoneyChangeText;     // 今日金钱变化

        [Header("Lifetime Statistics")]
        [SerializeField] private TextMeshProUGUI totalIncomeText;          // 总收入
        [SerializeField] private TextMeshProUGUI totalExpensesText;        // 总开支

        [Header("Other Stats")]
        [SerializeField] private TextMeshProUGUI todayFameEarnedText;      // 今日获得的Fame
        [SerializeField] private TextMeshProUGUI totalCustomersText;       // 今日访客数

        [Header("Level Up Section")]
        [SerializeField] private GameObject levelUpContainer;              // ScrollView的Content容器
        [SerializeField] private GameObject levelUpItemPrefab;             // 升级条目预制体
        [SerializeField] private TextMeshProUGUI noLevelUpText;           // "没有顾客升级"的提示文本

        [Header("Control")]
        [SerializeField] private Button continueButton;
        
        [Header("Color")]
        [SerializeField] private Color greenColor;
        [SerializeField] private Color redColor;

        private void Awake()
        {
            if (continueButton != null)
            {
                continueButton.onClick.AddListener(OnContinueClicked);
            }
        }

        public void ShowSettlement(DailySettlementData data)
        {
            // 标题：天数
            if (dayText != null)
                dayText.text = $"Day {data.day} Settlement";

            // === 收入部分 ===
            if (todaySalesText != null)
                todaySalesText.text = $"Today's Sale: ${data.totalSale:F2}";

            if (todayBonusIncomeText != null)
                todayBonusIncomeText.text = $"Bonus Income: ${data.bonusIncome:F2}";

            if (todayMaintenanceText != null)
                todayMaintenanceText.text = $"Maintenance Fee: ${data.dailyExpenses:F2}";

            if (todayMoneyChangeText != null)
            {
                // 今日金钱变化（正数=亏损，负数=盈利）
                int change = data.dailyMoneyChange;
                todayMoneyChangeText.text = change > 0
                    ? $"Money Change: -${change:F0}"
                    : $"Money Change: +${Mathf.Abs(change):F0}";
                todayMoneyChangeText.color = change > 0 ? redColor : greenColor;
            }

            // === 累计统计 ===
            if (totalIncomeText != null)
                totalIncomeText.text = $"Lifetime Income: ${data.lifetimeIncome:F0}";

            if (totalExpensesText != null)
                totalExpensesText.text = $"Lifetime Expense: ${data.lifetimeExpenses:F0}";

            // === 其他统计 ===
            if (todayFameEarnedText != null)
                todayFameEarnedText.text = $"Fame Earned: +{data.fameEarned}";

            if (totalCustomersText != null)
                totalCustomersText.text = $"Customers Entered Today: {data.totalCustomers}";

            // === 升级列表 ===
            PopulateLevelUpList(data.levelUps);

            // === 应用声誉奖励 ===
            ResourceManager.Instance?.AddFame(data.fameEarned);
        }

        /// <summary>
        /// 填充升级列表（使用ScrollView）
        /// </summary>
        private void PopulateLevelUpList(CustomerLevelUpInfo[] levelUps)
        {
            // 清空旧的升级条目
            if (levelUpContainer != null)
            {
                foreach (Transform child in levelUpContainer.transform)
                {
                    Destroy(child.gameObject);
                }
            }

            // 如果没有升级
            if (levelUps == null || levelUps.Length == 0)
            {
                if (noLevelUpText != null)
                {
                    noLevelUpText.gameObject.SetActive(true);
                }
                return;
            }

            // 有升级，隐藏提示文本
            if (noLevelUpText != null)
            {
                noLevelUpText.gameObject.SetActive(false);
            }

            // 创建升级条目
            if (levelUpItemPrefab != null && levelUpContainer != null)
            {
                foreach (var levelUp in levelUps)
                {
                    GameObject item = Instantiate(levelUpItemPrefab, levelUpContainer.transform);

                    // 假设预制体中有一个TextMeshProUGUI组件
                    TextMeshProUGUI text = item.GetComponent<TextMeshProUGUI>();
                    if (text != null)
                    {
                        // 格式：Name: Lv X → Y (+Z XP)
                        text.text = $"<b>{levelUp.customerName}</b>: " +
                                   $"Lv {levelUp.oldLevel} → {levelUp.newLevel} " +
                                   $"<color=#FFD700>(+{levelUp.xpGained} XP)</color>";

                        // 如果跨级升级，添加特殊标记
                        if (levelUp.newLevel - levelUp.oldLevel > 1)
                        {
                            int skippedLevels = levelUp.newLevel - levelUp.oldLevel - 1;
                            text.text += $" <color=#FF6347> Skipped {skippedLevels} level(s)!</color>";
                        }
                    }
                }
            }
        }

        private void OnContinueClicked()
        {
            // 隐藏面板
            gameObject.SetActive(false);

            // 重新加载蓝图配置（用户可能在外部修改了蓝图文件）
            BlueprintManager.Instance?.ReloadProfile();

            // 通知 DayLoopManager 进入下一天的建造阶段
            DayLoopManager.Instance?.AdvanceToNextDay();
        }
    }
}
