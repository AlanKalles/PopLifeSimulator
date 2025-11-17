using UnityEngine;
using TMPro;
using PopLife.Manager;

namespace PopLife.UI.Stats
{
    /// <summary>
    /// Economy 面板 - 显示每日经济统计
    /// </summary>
    public class EconomyPanelView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI todayRevenueText;
        [SerializeField] private TextMeshProUGUI maintenanceFeeText;

        [Header("Update Settings")]
        [SerializeField] private float updateInterval = 1f; // 更新间隔（秒）

        private float lastUpdateTime;

        void OnEnable()
        {
            // 监听资源变化
            if (ResourceManager.Instance != null)
            {
                // 注意：ResourceManager 可能没有 OnMoneyChanged 事件，这里使用定时刷新
            }

            // 立即刷新一次
            RefreshData();
            lastUpdateTime = Time.time;
        }

        void Update()
        {
            // 定时刷新
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                RefreshData();
                lastUpdateTime = Time.time;
            }
        }

        /// <summary>
        /// 刷新所有数据
        /// </summary>
        private void RefreshData()
        {
            UpdateRevenue();
            UpdateMaintenanceFee();
        }

        /// <summary>
        /// 更新今日收入
        /// 格式: Revenue           +$12(123)
        /// 其中 +$12 为绿色，123 为玩家总金钱
        /// </summary>
        private void UpdateRevenue()
        {
            if (todayRevenueText == null) return;

            float todayRevenue = 0f;
            int totalMoney = 0;

            // 从 DayLoopManager 获取今日销售总额
            if (DayLoopManager.Instance != null)
            {
                todayRevenue = DayLoopManager.Instance.dailyTotalSale;
            }

            // 从 ResourceManager 获取玩家总金钱
            if (ResourceManager.Instance != null)
            {
                totalMoney = ResourceManager.Instance.money;
            }

            // 使用 TextMeshPro 富文本标签
            // <color=#RRGGBB> 用于颜色，#00B11D 为绿色
            todayRevenueText.text = $"Revenue           <color=#00B11D>+${todayRevenue:F0}</color> (${totalMoney})";
        }

        /// <summary>
        /// 更新维护费总额
        /// 格式: Maintenance           -$50
        /// 其中 -$50 为红色
        /// </summary>
        private void UpdateMaintenanceFee()
        {
            if (maintenanceFeeText == null) return;

            float maintenanceFee = 0f;

            // 从 StatsDataManager 获取维护费
            if (StatsDataManager.Instance != null)
            {
                maintenanceFee = StatsDataManager.Instance.GetTotalMaintenanceFee();
            }

            // 使用 TextMeshPro 富文本标签
            // <color=#FF0000> 为红色
            maintenanceFeeText.text = $"Maintenance           <color=#FF0000>-${maintenanceFee:F0}</color>";
        }
    }
}
