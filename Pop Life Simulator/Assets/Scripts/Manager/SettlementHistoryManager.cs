using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PopLife
{
    /// <summary>
    /// 结算历史管理器 — 使用 Easy Save 3 持久化最近7天的每日结算数据
    /// 提供历史查询API，供结算面板折线图、对比箭头等功能使用
    /// </summary>
    public class SettlementHistoryManager : MonoBehaviour
    {
        public static SettlementHistoryManager Instance { get; private set; }

        private const string ES3_KEY = "settlementHistory";
        private const string ES3_FILE = "SettlementHistory.es3";
        private const int MAX_HISTORY = 7;

        private List<SettlementHistoryEntry> history = new List<SettlementHistoryEntry>();

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            Load();
        }

        /// <summary>
        /// 记录今日结算数据（结算时由 NewDailySettlementPanel 调用）
        /// </summary>
        public void RecordDay(SettlementHistoryEntry entry)
        {
            history.Add(entry);

            // 保持最多7天记录
            while (history.Count > MAX_HISTORY)
                history.RemoveAt(0);

            Save();
        }

        /// <summary>
        /// 获取最近N天历史（最旧在前，最新在后）
        /// </summary>
        public SettlementHistoryEntry[] GetHistory(int count = 7)
        {
            int start = Mathf.Max(0, history.Count - count);
            return history.GetRange(start, history.Count - start).ToArray();
        }

        /// <summary>
        /// 获取昨天的数据（当前最后一条是今天，倒数第二条是昨天）
        /// 注意：需要在 RecordDay 之后调用才能获取正确的昨天数据
        /// </summary>
        public SettlementHistoryEntry GetYesterday()
        {
            if (history.Count < 2) return null;
            return history[history.Count - 2];
        }

        /// <summary>
        /// 获取今天的数据（最后录入的一条）
        /// </summary>
        public SettlementHistoryEntry GetToday()
        {
            if (history.Count == 0) return null;
            return history[history.Count - 1];
        }

        /// <summary>
        /// 获取当前历史记录数量
        /// </summary>
        public int HistoryCount => history.Count;

        private void Save()
        {
            try
            {
                ES3.Save<List<SettlementHistoryEntry>>(ES3_KEY, history, ES3_FILE);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SettlementHistoryManager] 保存失败: {e.Message}");
            }
        }

        private void Load()
        {
            try
            {
                history = ES3.Load<List<SettlementHistoryEntry>>(
                    ES3_KEY, ES3_FILE, new List<SettlementHistoryEntry>());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SettlementHistoryManager] 加载失败（首次运行属正常情况）: {e.Message}");
                history = new List<SettlementHistoryEntry>();
            }
        }
    }

    /// <summary>
    /// 每日结算历史条目
    /// </summary>
    [Serializable]
    public class SettlementHistoryEntry
    {
        public int day;
        public float totalSale;          // 今日销售额
        public float dailyExpenses;      // 今日费用（含维护费）
        public float dailyIncome;        // 净收入 (totalSale - dailyExpenses)
        public int totalCustomers;       // 今日顾客数
        public int fameEarned;           // 今日Fame
        public float[] categoryRevenue;  // 各品类收入（索引对应ProductCategory枚举）
        public HotSellerEntry[] hotSellers; // 热销前3
    }

    /// <summary>
    /// 热销商品条目
    /// </summary>
    [Serializable]
    public class HotSellerEntry
    {
        public string shelfName;         // 货架显示名
        public string category;          // 商品类别
        public int revenue;              // 今日收入
        public string archetypeId;       // 用于运行时加载icon
    }
}
