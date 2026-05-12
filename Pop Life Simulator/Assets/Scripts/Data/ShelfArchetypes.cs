using System;
using UnityEngine;

namespace PopLife.Data
{
    // 货架原型
    [CreateAssetMenu(menuName = "PopLife/Buildings/ShelfArchetype")]
    public class ShelfArchetype : BuildingArchetype
    {
        [Header("货架属性")]
        public ProductCategory category;

        [Header("品牌")]
        public BrandData brand;

        [Header("货架描述")]
        [TextArea(3, 10)]
        public string description;

        [Header("使用说明")]
        [TextArea(3, 10)]
        public string usageInstruction;

        // 等级数据结构（不再序列化存储，由公式动态生成）
        [Serializable]
        public class ShelfLevelData : BuildingLevelData
        {
            public int price = 100;
            public int stockFee = 0;   // 玩家每件补货成本
            public int profit = 0;     // 每件利润（price = stockFee + profit）
            public int maxStock = 10;
            public float appeal = 1f;
        }

        // 等级范围：1-5
        public override int MaxLevel => 5;

        // ── 公式计算方法（基于 buildCost 和 level）──

        /// <summary>升级所需声望 = Floor(buildCost * 0.5 + level² * 5)</summary>
        public int GetUpgradeFameCost(int level)
            => Mathf.FloorToInt(buildCost * 0.5f + Mathf.Pow(level, 2) * 5f);

        /// <summary>每件补货成本 = Floor(buildCost * 0.05 - 2 + level² * 0.5) + 2</summary>
        public int GetStockFee(int level)
            => Mathf.FloorToInt(buildCost * 0.05f - 2f + Mathf.Pow(level, 2) * 0.5f) + 2;

        /// <summary>每件利润 = Floor(buildCost * 0.03 + level² * 0.25)</summary>
        public int GetProfit(int level)
            => Mathf.Max(Mathf.FloorToInt(buildCost * 0.03f + Mathf.Pow(level, 2) * 0.25f),1);

        /// <summary>商品售价 = StockFee + Profit（顾客支付的单价）</summary>
        public int GetPrice(int level)
            => GetStockFee(level) + GetProfit(level);

        /// <summary>每日维护费 = Floor(0.9 × Profit + 3 × √Profit)</summary>
        public int GetMaintenanceFee(int level)
        {
            int p = GetProfit(level);
            return Mathf.Max(Mathf.FloorToInt(0.9f * p + 3f * Mathf.Sqrt(p)) - 1,1);
        }

        /// <summary>最大库存 = Floor(Max(0, 800 - buildCost) × 0.01 + 5 + level² × 0.5)</summary>
        public int GetStock(int level)
            => Mathf.FloorToInt(Mathf.Max(0, 800 - buildCost) * 0.01f + 5f + Mathf.Pow(level, 2) * 0.5f);

        /// <summary>吸引力 = Floor(buildCost × 0.018 × level + 4 × level^1.5)</summary>
        public float GetAppeal(int level)
            => Mathf.Floor(buildCost * 0.018f * level + 4f * Mathf.Pow(level, 1.5f));

        // ── 兼容接口（供 BuildingInstance.TryUpgrade / ShelfInstance 使用）──

        public override BuildingLevelData GetLevel(int lvl) => BuildComputedLevel(lvl);
        public ShelfLevelData GetShelfLevel(int lvl) => BuildComputedLevel(lvl);

        private ShelfLevelData BuildComputedLevel(int lvl)
        {
            lvl = Mathf.Clamp(lvl, 1, MaxLevel);
            return new ShelfLevelData
            {
                level = lvl,
                upgradeFameCost = GetUpgradeFameCost(lvl),
                maintenanceFee = GetMaintenanceFee(lvl),
                price = GetPrice(lvl),
                stockFee = GetStockFee(lvl),
                profit = GetProfit(lvl),
                maxStock = GetStock(lvl),
                appeal = GetAppeal(lvl)
            };
        }
    }
}
