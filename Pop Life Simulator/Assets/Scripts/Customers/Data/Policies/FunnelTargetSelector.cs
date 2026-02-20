using System.Collections.Generic;
using UnityEngine;
using PopLife.Data;
using Sirenix.OdinInspector;

namespace PopLife.Customers.Data
{
    /// <summary>
    /// 四层降级漏斗货架选择策略
    /// 根据 ctx.currentPhase 在指定阶段内选择货架，找不到返回 -1
    /// </summary>
    [CreateAssetMenu(menuName = "PopLife/Policies/FunnelTargetSelector", fileName = "FunnelTargetSelector")]
    public class FunnelTargetSelector : TargetSelectorPolicy
    {
        [Title("Appeal 加权")]
        [SerializeField, Range(0.5f, 3f)] private float appealExponent = 1f;
        // 加权公式: weight = Mathf.Pow(shelf.appeal, appealExponent)

        [Title("每阶段最大选择数")]
        [BoxGroup("Phase Limits")]
        [LabelText("Favorite（固定）"), ReadOnly]
        [SerializeField] private int favoriteMax = 4;

        [BoxGroup("Phase Limits")]
        [LabelText("Category（基础）")]
        [SerializeField] private int baseCategoryMax = 2;

        [BoxGroup("Phase Limits")]
        [LabelText("Brand（基础）")]
        [SerializeField] private int baseBrandMax = 2;

        [BoxGroup("Phase Limits")]
        [LabelText("PriceBased（基础）")]
        [SerializeField] private int basePriceMax = 1;

        [Title("Loyalty 对选择上限的加成曲线")]
        [FoldoutGroup("Loyalty Curves")]
        [SerializeField] private AnimationCurve categoryMaxByLoyalty = AnimationCurve.Linear(0, 0, 4, 2);
        [FoldoutGroup("Loyalty Curves")]
        [SerializeField] private AnimationCurve brandMaxByLoyalty = AnimationCurve.Linear(0, 0, 4, 1);
        [FoldoutGroup("Loyalty Curves")]
        [SerializeField] private AnimationCurve priceMaxByLoyalty = AnimationCurve.Linear(0, 0, 4, 1);

        /// <summary>
        /// 供行为树节点调用，获取当前阶段的循环上限
        /// </summary>
        public int GetMaxLoops(FunnelPhase phase, int loyaltyLevel)
        {
            return phase switch
            {
                FunnelPhase.Favorite => favoriteMax,
                FunnelPhase.Category => baseCategoryMax + Mathf.RoundToInt(categoryMaxByLoyalty.Evaluate(loyaltyLevel)),
                FunnelPhase.Brand => baseBrandMax + Mathf.RoundToInt(brandMaxByLoyalty.Evaluate(loyaltyLevel)),
                FunnelPhase.PriceBased => basePriceMax + Mathf.RoundToInt(priceMaxByLoyalty.Evaluate(loyaltyLevel)),
                _ => 1
            };
        }

        /// <summary>
        /// 根据 ctx.currentPhase 在指定阶段内选择货架
        /// 找不到返回 -1，行为树负责推进到下一阶段
        /// </summary>
        public override int SelectTargetShelf(in CustomerContext ctx, List<ShelfSnapshot> candidates)
        {
            return ctx.currentPhase switch
            {
                FunnelPhase.Favorite => SelectFavorite(ctx, candidates),
                FunnelPhase.Category => SelectByCategory(ctx, candidates),
                FunnelPhase.Brand => SelectByBrand(ctx, candidates),
                FunnelPhase.PriceBased => SelectPriceBased(ctx, candidates),
                _ => -1
            };
        }

        /// <summary>
        /// 通用可用性检查（所有层共享）：stock > 0, 未购买过, 买得起
        /// </summary>
        private bool IsAvailable(in CustomerContext ctx, in ShelfSnapshot shelf)
        {
            if (shelf.stock <= 0) return false;
            if (shelf.price > ctx.walletAmount) return false;
            if (ctx.purchasedArchetypes != null && shelf.archetype != null
                && ctx.purchasedArchetypes.Contains(shelf.archetype))
                return false;
            return true;
        }

        /// <summary>
        /// Layer 1 - Favorite: 按槽位顺序遍历偏好货架，第一个可用的即返回
        /// </summary>
        private int SelectFavorite(in CustomerContext ctx, List<ShelfSnapshot> candidates)
        {
            if (ctx.favoriteShelfIds == null) return -1;

            foreach (var favoriteId in ctx.favoriteShelfIds)
            {
                if (string.IsNullOrEmpty(favoriteId)) continue;

                for (int i = 0; i < candidates.Count; i++)
                {
                    var shelf = candidates[i];
                    if (shelf.archetype != null
                        && shelf.archetype.archetypeId == favoriteId
                        && IsAvailable(ctx, shelf))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Layer 2 - Category: 过滤偏好品类中的可用货架，appeal 加权随机
        /// </summary>
        private int SelectByCategory(in CustomerContext ctx, List<ShelfSnapshot> candidates)
        {
            if (ctx.favoriteCategoryIndices == null || ctx.favoriteCategoryIndices.Length == 0)
                return -1;

            // 收集符合条件的候选
            var validIndices = new List<int>();
            var weights = new List<float>();

            for (int i = 0; i < candidates.Count; i++)
            {
                var shelf = candidates[i];
                if (!IsAvailable(ctx, shelf)) continue;
                if (!ContainsInt(ctx.favoriteCategoryIndices, shelf.categoryIndex)) continue;

                validIndices.Add(i);
                weights.Add(Mathf.Pow(Mathf.Max(1f, shelf.appeal), appealExponent));
            }

            return WeightedRandomPick(validIndices, weights);
        }

        /// <summary>
        /// Layer 3 - Brand: 过滤偏好品牌中的可用货架，appeal 加权随机
        /// </summary>
        private int SelectByBrand(in CustomerContext ctx, List<ShelfSnapshot> candidates)
        {
            if (ctx.favoriteBrands == null || ctx.favoriteBrands.Length == 0)
                return -1;

            var validIndices = new List<int>();
            var weights = new List<float>();

            for (int i = 0; i < candidates.Count; i++)
            {
                var shelf = candidates[i];
                if (!IsAvailable(ctx, shelf)) continue;
                if (shelf.archetype == null || shelf.archetype.brand == null) continue;
                if (!ContainsBrand(ctx.favoriteBrands, shelf.archetype.brand)) continue;

                validIndices.Add(i);
                weights.Add(Mathf.Pow(Mathf.Max(1f, shelf.appeal), appealExponent));
            }

            return WeightedRandomPick(validIndices, weights);
        }

        /// <summary>
        /// Layer 4 - PriceBased: 所有剩余可用货架，appeal 加权随机
        /// </summary>
        private int SelectPriceBased(in CustomerContext ctx, List<ShelfSnapshot> candidates)
        {
            var validIndices = new List<int>();
            var weights = new List<float>();

            for (int i = 0; i < candidates.Count; i++)
            {
                var shelf = candidates[i];
                if (!IsAvailable(ctx, shelf)) continue;

                validIndices.Add(i);
                weights.Add(Mathf.Pow(Mathf.Max(1f, shelf.appeal), appealExponent));
            }

            return WeightedRandomPick(validIndices, weights);
        }

        /// <summary>
        /// 加权随机选择
        /// </summary>
        private int WeightedRandomPick(List<int> indices, List<float> weights)
        {
            if (indices.Count == 0) return -1;
            if (indices.Count == 1) return indices[0];

            float totalWeight = 0f;
            foreach (var w in weights) totalWeight += w;

            if (totalWeight <= 0f) return indices[0];

            float rand = Random.Range(0f, totalWeight);
            float cumulative = 0f;

            for (int i = 0; i < indices.Count; i++)
            {
                cumulative += weights[i];
                if (rand <= cumulative)
                    return indices[i];
            }

            return indices[indices.Count - 1];
        }

        private bool ContainsInt(int[] array, int value)
        {
            foreach (var v in array)
            {
                if (v == value) return true;
            }
            return false;
        }

        private bool ContainsBrand(BrandData[] array, BrandData value)
        {
            foreach (var v in array)
            {
                if (v == value) return true;
            }
            return false;
        }
    }
}
