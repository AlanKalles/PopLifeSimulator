using System;
using System.Collections.Generic;
using UnityEngine;

namespace PopLife.Data
{
    /// <summary>
    /// 品类乘数修饰条目（Inspector序列化用）
    /// 运行时由 GlobalModifierManager 扁平化到 float[] 数组
    /// </summary>
    [Serializable]
    public struct CategoryModifier
    {
        [Tooltip("目标商品品类")]
        public ProductCategory category;

        [Tooltip("乘数值（1.0=无影响, 1.3=+30%, 0.8=-20%）")]
        public float multiplier;
    }

    /// <summary>
    /// 货架Appeal加值修饰条目（Inspector序列化用）
    /// targetShelf 为 null 时表示全局生效
    /// </summary>
    [Serializable]
    public struct ShelfAppealModifier
    {
        [Tooltip("目标货架原型（留空=全局生效）")]
        public ShelfArchetype targetShelf;

        [Tooltip("Appeal 平坦加值（整数）")]
        public int addend;
    }

    /// <summary>
    /// 全局修饰器数据载荷
    /// 挂载在 SeasonEventData / BufferData 上，由设计师在 Inspector 中配置
    /// 运行时由 GlobalModifierManager 聚合烘焙
    /// </summary>
    [Serializable]
    public class GlobalModifierPayload
    {
        [Header("A. 全局乘数（默认1.0，无影响）")]
        [Tooltip("建造成本乘数")]
        public float constructionCostMultiplier = 1f;

        [Tooltip("升级成本（Fame）乘数")]
        public float upgradeCostMultiplier = 1f;

        [Tooltip("顾客生成速率乘数（>1=更快, <1=更慢）")]
        public float customerSpawnRateMultiplier = 1f;

        [Tooltip("顾客钱包容量乘数")]
        public float customerMoneyBagMultiplier = 1f;

        [Tooltip("全局Fame获取乘数")]
        public float globalFameGainMultiplier = 1f;

        [Header("B. 品类定向乘数")]
        [Tooltip("品类售价乘数列表")]
        public List<CategoryModifier> categoryPriceModifiers = new();

        [Tooltip("品类Fame乘数列表")]
        public List<CategoryModifier> categoryFameModifiers = new();

        [Tooltip("品类购买数量乘数列表")]
        public List<CategoryModifier> categoryQuantityModifiers = new();

        [Header("C. 货架Appeal加值")]
        [Tooltip("货架Appeal平坦加值列表（targetShelf留空=全局）")]
        public List<ShelfAppealModifier> shelfAppealModifiers = new();

        [Header("D. AI行为修饰")]
        [Tooltip("奖励兴趣品类（顾客生成时临时加入购物偏好）")]
        public List<ProductCategory> bonusInterestCategories = new();
    }
}
