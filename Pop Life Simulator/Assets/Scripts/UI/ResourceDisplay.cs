using UnityEngine;
using TMPro;
using PopLife.Runtime;

namespace PopLife
{
    /// <summary>
    /// 显示玩家资源（金钱、声望、Store Appeal）+ 当日预警 UI（维护费、当日销售、负债红字）。
    /// 完全事件驱动 — 不再使用 Update() 轮询，避免双源刷新结构混乱。
    /// </summary>
    public class ResourceDisplay : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI moneyText;
        [SerializeField] private TextMeshProUGUI fameText;
        [SerializeField] private TextMeshProUGUI storeAppealText;

        [Header("Day Stats Display")]
        [SerializeField] private TextMeshProUGUI maintenanceFeeText;
        [SerializeField] private TextMeshProUGUI todayIncomeText;
        [SerializeField] private TextMeshProUGUI profitText;

        [Header("Display Format")]
        [SerializeField] private string moneyPrefix = "$";
        [SerializeField] private string famePrefix = "Fame: ";

        [Header("Money Color")]
        [SerializeField] private Color debtColor = new Color(1f, 0.27f, 0.27f);
        [SerializeField] private Color normalColor = Color.white;

        [Header("Profit Color")]
        [SerializeField] private Color profitPositiveColor = new Color(0.35f, 1f, 0.45f);
        [SerializeField] private Color profitNegativeColor = new Color(1f, 0.27f, 0.27f);
        [SerializeField] private Color profitZeroColor = Color.white;

        // 当日实时数值缓存（用于 profit 计算）
        private float currentMaintenance;
        private float currentDailySales;

        // Start 而非 OnEnable，保证 ResourceManager / DayLoopManager 的 Awake 已完成，
        // 否则 OnEnable 比 manager Awake 先跑时 Instance == null，订阅会被静默跳过。
        private void Start()
        {
            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.OnMoneyChanged += UpdateMoneyDisplay;
                ResourceManager.Instance.OnFameChanged += UpdateFameDisplay;
                ResourceManager.Instance.OnStoreAppealChanged += UpdateStoreAppealDisplay;
            }
            ConstructionManager.OnBuildingPlacedOrDestroyed += RefreshMaintenance;
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBuildPhaseStart += HandleBuildPhaseStart;
                DayLoopManager.Instance.OnSaleRecorded += UpdateDailyIncome;
            }

            // 订阅后立即拉一次当前值同步初始 UI
            if (ResourceManager.Instance != null)
            {
                UpdateMoneyDisplay(ResourceManager.Instance.money);
                UpdateFameDisplay(ResourceManager.Instance.fame);
                UpdateStoreAppealDisplay(ResourceManager.Instance.GetStoreAppeal());
            }
            RefreshMaintenance();
            ResetDailyIncome();
        }

        private void OnDestroy()
        {
            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.OnMoneyChanged -= UpdateMoneyDisplay;
                ResourceManager.Instance.OnFameChanged -= UpdateFameDisplay;
                ResourceManager.Instance.OnStoreAppealChanged -= UpdateStoreAppealDisplay;
            }
            ConstructionManager.OnBuildingPlacedOrDestroyed -= RefreshMaintenance;
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBuildPhaseStart -= HandleBuildPhaseStart;
                DayLoopManager.Instance.OnSaleRecorded -= UpdateDailyIncome;
            }
        }

        private void UpdateMoneyDisplay(int money)
        {
            if (moneyText == null) return;
            if (money < 0)
            {
                moneyText.text = $"-{moneyPrefix}{(-money).ToString("N0")}";
                moneyText.color = debtColor;
            }
            else
            {
                moneyText.text = $"{moneyPrefix}{money.ToString("N0")}";
                moneyText.color = normalColor;
            }
        }

        private void UpdateFameDisplay(int fame)
        {
            if (fameText != null)
                fameText.text = famePrefix + fame.ToString();
        }

        private void UpdateStoreAppealDisplay(int appeal)
        {
            if (storeAppealText != null)
                storeAppealText.text = appeal.ToString("N0");
        }

        /// <summary>
        /// 实时计算预计维护费 = 所有玩家拥有建筑的基础维护费总和 × GlobalModifier 维护乘数。
        /// 与 DayLoopManager.CalculateAndDeductMaintenance 计算公式一致，确保 UI 显示与实际扣费一致。
        /// </summary>
        private void RefreshMaintenance()
        {
            float total = 0f;
            var wg = WorldGrid.Instance;
            if (wg != null)
            {
                foreach (var b in wg.AllBuildings())
                {
                    var hostTile = WorldGrid.ResolveHostTile(b);
                    if (hostTile == null || !ConstructionGuards.IsStoreOwnedByPlayer(hostTile.StoreId))
                        continue;
                    total += b.GetMaintenanceFee();
                }
            }

            // 必须应用与结算时相同的 GlobalModifier，否则预估值与实际扣费不一致
            if (GlobalModifierManager.Instance != null)
                total *= GlobalModifierManager.Instance.GetMaintenanceCostMultiplier();

            currentMaintenance = total;
            if (maintenanceFeeText != null)
                maintenanceFeeText.text = $"-{moneyPrefix}{Mathf.RoundToInt(total).ToString("N0")}";
            RefreshProfit();
        }

        private void HandleBuildPhaseStart()
        {
            ResetDailyIncome();
            // 跨日 buffer 当日激活时维护乘数可能变化，BuildPhase 切换时重算一次
            RefreshMaintenance();
        }

        private void ResetDailyIncome()
        {
            currentDailySales = 0f;
            if (todayIncomeText != null)
                todayIncomeText.text = $"+{moneyPrefix}0";
            RefreshProfit();
        }

        private void UpdateDailyIncome(float total)
        {
            currentDailySales = total;
            if (todayIncomeText != null)
                todayIncomeText.text = $"+{moneyPrefix}{Mathf.RoundToInt(total).ToString("N0")}";
            RefreshProfit();
        }

        /// <summary>
        /// 利润 = 当日销售（仅收银台结算的实际收入） - 当日维护费预估。
        /// 销售或维护任何一方变化时重新计算并染色。
        /// </summary>
        private void RefreshProfit()
        {
            if (profitText == null) return;

            int profit = Mathf.RoundToInt(currentDailySales - currentMaintenance);
            if (profit > 0)
            {
                profitText.text = $"+{moneyPrefix}{profit.ToString("N0")}";
                profitText.color = profitPositiveColor;
            }
            else if (profit < 0)
            {
                profitText.text = $"-{moneyPrefix}{(-profit).ToString("N0")}";
                profitText.color = profitNegativeColor;
            }
            else
            {
                profitText.text = $"{moneyPrefix}0";
                profitText.color = profitZeroColor;
            }
        }
    }
}
