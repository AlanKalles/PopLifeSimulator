using UnityEngine;
using System.Linq;

namespace PopLife.Customers.Data
{
    /// <summary>
    /// 随机购买数量策略
    /// 在配置的范围内随机决定购买数量，并考虑预算、库存、忠诚度等因素
    /// </summary>
    [CreateAssetMenu(menuName = "PopLife/Policies/Purchase/RandomPurchase", fileName = "RandomPurchasePolicy")]
    public class RandomPurchasePolicy : PurchasePolicy
    {
        [Header("基础购买范围")]
        [Tooltip("默认最小购买数量")]
        [Min(1)] public int defaultMinBuy = 1;

        [Tooltip("默认最大购买数量")]
        [Min(1)] public int defaultMaxBuy = 3;

        [Header("类别特殊配置")]
        [Tooltip("不同商品类别的特殊购买范围")]
        public CategoryPurchaseRange[] categoryOverrides;

        [Header("忠诚度影响")]
        [Tooltip("X轴=忠诚度(1-10), Y轴=购买量倍率(建议1-2)")]
        public AnimationCurve loyaltyMultiplierCurve = AnimationCurve.Linear(1, 1f, 10, 1.5f);

        [Header("兴趣影响")]
        [Tooltip("是否根据兴趣调整购买量")]
        public bool useInterestModifier = true;

        [Tooltip("兴趣对购买量的影响曲线, X=兴趣(0-10), Y=倍率(0.5-2)")]
        public AnimationCurve interestMultiplierCurve = AnimationCurve.Linear(0, 0.5f, 10, 1.5f);

        [Header("预算策略")]
        [Tooltip("预留资金比例，为后续购买保留部分预算")]
        [Range(0, 1f)] public float budgetReserveRatio = 0.2f;

        [Tooltip("是否允许花光所有钱")]
        public bool allowEmptyWallet = false;

        [Tooltip("当剩余资金低于此值时，更保守地购买")]
        [Min(0)] public int lowBudgetThreshold = 50;

        [Header("特殊行为")]
        [Tooltip("首次购买该类商品时是否更保守")]
        public bool conservativeFirstPurchase = true;

        [Tooltip("首次购买时的数量上限")]
        [Min(1)] public int firstPurchaseMaxQty = 2;

        [Header("调试选项")]
        public bool enableDebugLog = false;

        /// <summary>
        /// 决定购买数量
        /// </summary>
        /// <param name="ctx">顾客上下文</param>
        /// <param name="shelf">货架信息</param>
        /// <param name="wallet">钱包余额</param>
        /// <param name="price">商品单价</param>
        /// <returns>购买数量，0表示不购买</returns>
        public override int DecidePurchaseQty(in CustomerContext ctx, in ShelfSnapshot shelf, int wallet, int price)
        {
            // 基本验证
            if (wallet < price || shelf.stock <= 0)
            {
                if (enableDebugLog)
                {
                    Debug.Log($"[RandomPurchase] 顾客 {ctx.customerId} 无法购买: 钱包={wallet}, 价格={price}, 库存={shelf.stock}");
                }
                return 0;
            }

            // 1. 获取该类别的购买范围
            var range = GetPurchaseRange(shelf.categoryIndex);

            // 2. 生成基础随机数量
            int baseQty = Random.Range(range.minBuy, range.maxBuy + 1);

            // 3. 应用忠诚度倍率
            float loyaltyMult = loyaltyMultiplierCurve.Evaluate(ctx.loyaltyLevel);
            float adjustedQty = baseQty * loyaltyMult;

            // 4. 应用兴趣倍率（如果启用）
            if (useInterestModifier && ctx.interest != null)
            {
                int interest = GetInterestForCategory(ctx.interest, shelf.categoryIndex);
                float interestMult = interestMultiplierCurve.Evaluate(interest);
                adjustedQty *= interestMult;
            }

            // 5. 首次购买限制（如果启用）
            if (conservativeFirstPurchase)
            {
                // TODO: 需要追踪是否首次购买该类别
                // 暂时用低信任值模拟首次购买
                if (ctx.trust < 30)
                {
                    adjustedQty = Mathf.Min(adjustedQty, firstPurchaseMaxQty);
                }
            }

            // 6. 计算预算限制
            int budgetLimit = CalculateBudgetLimit(wallet, price);

            // 7. 应用所有限制
            int finalQty = Mathf.Min(
                Mathf.RoundToInt(adjustedQty),
                budgetLimit,
                shelf.stock
            );

            // 8. 确保至少买1个（如果有钱的话）
            if (finalQty < 1 && wallet >= price && shelf.stock > 0)
            {
                finalQty = 1;
            }

            if (enableDebugLog && finalQty > 0)
            {
                Debug.Log($"[RandomPurchase] 顾客 {ctx.customerId} 决定购买 {finalQty} 个 " +
                         $"(基础:{baseQty}, 忠诚倍率:{loyaltyMult:F2}, 预算限制:{budgetLimit}, 库存:{shelf.stock})");
            }

            return finalQty;
        }

        /// <summary>
        /// 获取指定类别的购买范围
        /// </summary>
        private CategoryPurchaseRange GetPurchaseRange(int categoryIndex)
        {
            // 查找类别特殊配置
            if (categoryOverrides != null)
            {
                var overrideRange = categoryOverrides.FirstOrDefault(c => c.categoryIndex == categoryIndex);
                if (overrideRange != null)
                {
                    return overrideRange;
                }
            }

            // 返回默认范围
            return new CategoryPurchaseRange
            {
                categoryIndex = categoryIndex,
                minBuy = defaultMinBuy,
                maxBuy = defaultMaxBuy
            };
        }

        /// <summary>
        /// 计算预算限制的购买数量
        /// </summary>
        private int CalculateBudgetLimit(int wallet, int price)
        {
            if (price <= 0) return 0;

            float availableBudget = wallet;

            // 应用预留资金策略
            if (!allowEmptyWallet && budgetReserveRatio > 0)
            {
                // 如果钱包余额已经很少，放宽预留限制
                if (wallet <= lowBudgetThreshold)
                {
                    availableBudget = wallet * 0.9f; // 低预算时只预留10%
                }
                else
                {
                    availableBudget = wallet * (1f - budgetReserveRatio);
                }
            }

            int maxAffordable = Mathf.FloorToInt(availableBudget / price);

            // 确保不会因为预留导致完全买不起
            if (maxAffordable < 1 && wallet >= price)
            {
                maxAffordable = 1;
            }

            return maxAffordable;
        }

        /// <summary>
        /// 获取对应类别的兴趣值
        /// </summary>
        private int GetInterestForCategory(int[] interests, int categoryIndex)
        {
            if (interests == null || categoryIndex < 0 || categoryIndex >= interests.Length)
            {
                return 2; // 默认中等兴趣
            }
            return interests[categoryIndex];
        }
    }

    /// <summary>
    /// 商品类别的购买范围配置
    /// </summary>
    [System.Serializable]
    public class CategoryPurchaseRange
    {
        [Tooltip("商品类别索引（对应ProductCategory）")]
        public int categoryIndex;

        [Tooltip("该类别的最小购买数量")]
        [Min(1)] public int minBuy = 1;

        [Tooltip("该类别的最大购买数量")]
        [Min(1)] public int maxBuy = 3;
    }
}