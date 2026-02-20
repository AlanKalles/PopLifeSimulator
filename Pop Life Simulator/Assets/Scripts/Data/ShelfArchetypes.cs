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

        // 等级数据结构（不再序列化存储，由公式动态生成）
        [Serializable]
        public class ShelfLevelData : BuildingLevelData
        {
            public int price = 100;
            public int maxStock = 10;
            public float appeal = 1f;
        }

        // 等级范围：1-5
        public override int MaxLevel => 5;

        // ── 公式计算方法（基于 buildCost 和 level）──

        /// <summary>升级所需声望 = Floor(buildCost * 0.5 + level² * 5)</summary>
        public int GetUpgradeFameCost(int level)
            => Mathf.FloorToInt(buildCost * 0.5f + Mathf.Pow(level, 2) * 5);

        /// <summary>商品单价 = Floor(buildCost * 0.1 + level³ * 0.2)</summary>
        public int GetPrice(int level)
            => Mathf.FloorToInt(buildCost * 0.1f + Mathf.Pow(level, 3) * 0.2f);

        /// <summary>每日维护费 = Floor(buildCost * 0.1 + level² * 0.5 - 5)</summary>
        public int GetMaintenanceFee(int level)
            => Mathf.FloorToInt(buildCost * 0.1f + Mathf.Pow(level, 2) * 0.5f - 5);

        /// <summary>最大库存 = Floor(buildCost * 0.01 + level * 0.9 + 5)</summary>
        public int GetStock(int level)
            => Mathf.FloorToInt(buildCost * 0.01f + level * 0.9f + 5);

        /// <summary>吸引力 = Floor(buildCost * 0.36 + 4 * level^1.5 + 5)</summary>
        public float GetAppeal(int level)
            => Mathf.Floor(buildCost * 0.09f * 4f + 4f * Mathf.Pow(level, 1.5f) + 5);

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
                maxStock = GetStock(lvl),
                appeal = GetAppeal(lvl)
            };
        }
    }
}
