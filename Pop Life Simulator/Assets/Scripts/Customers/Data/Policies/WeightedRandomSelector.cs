using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace PopLife.Customers.Data
{
    /// <summary>
    /// 加权随机目标选择策略
    /// 根据兴趣、吸引力、队列长度等因素计算权重，然后进行加权随机选择
    /// </summary>
    [CreateAssetMenu(menuName = "PopLife/Policies/TargetSelector/WeightedRandomSelector", fileName = "WeightedRandomSelector")]
    public class WeightedRandomSelector : TargetSelectorPolicy
    {
        [Header("权重配置")]
        [Tooltip("兴趣值对选择的影响权重")]
        [Range(0, 2f)] public float interestWeight = 1.0f;

        [Tooltip("货架吸引力对选择的影响权重")]
        [Range(0, 2f)] public float attractivenessWeight = 0.8f;

        [Tooltip("队列长度惩罚的影响权重")]
        [Range(0, 2f)] public float queuePenaltyWeight = 0.5f;

        [Tooltip("距离对选择的影响权重")]
        [Range(0, 2f)] public float distanceWeight = 0.3f;

        [Header("队列惩罚曲线")]
        [Tooltip("X轴=队列长度(0-10), Y轴=惩罚系数(0-1)，1表示无惩罚，0表示完全惩罚")]
        public AnimationCurve queuePenaltyCurve = AnimationCurve.EaseInOut(0, 1f, 10, 0.1f);

        [Header("筛选条件")]
        [Tooltip("是否过滤库存为0的货架")]
        public bool filterZeroStock = true;

        [Tooltip("是否过滤已购买的货架")]
        public bool filterPurchased = true;

        [Tooltip("最大可接受的队列长度")]
        public int maxQueueLength = 10;

        [Tooltip("最小兴趣阈值，低于此值的类别不考虑")]
        [Range(0, 5)] public int minInterestThreshold = 1;

        [Header("调试选项")]
        public bool enableDebugLog = false;

        /// <summary>
        /// 选择目标货架
        /// </summary>
        /// <param name="ctx">顾客上下文信息</param>
        /// <param name="candidates">所有候选货架</param>
        /// <returns>选中货架的索引，-1表示无可选目标</returns>
        public override int SelectTargetShelf(in CustomerContext ctx, List<ShelfSnapshot> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                if (enableDebugLog) Debug.Log($"[WeightedRandomSelector] 顾客 {ctx.customerId}: 没有候选货架");
                return -1;
            }

            // 第一步：筛选有效候选
            var validCandidates = new List<(int index, float score)>();

            for (int i = 0; i < candidates.Count; i++)
            {
                var shelf = candidates[i];

                // 硬性筛选条件
                if (filterZeroStock && shelf.stock <= 0)
                {
                    continue; // 跳过无库存货架
                }

                if (shelf.queueLength > maxQueueLength)
                {
                    continue; // 跳过队列过长的货架
                }

                // 检查兴趣阈值
                float interest = GetInterestForCategory(ctx.interest, shelf.categoryIndex);
                if (interest < minInterestThreshold)
                {
                    continue; // 跳过兴趣过低的类别
                }

                // 过滤已购买的货架archetype
                if (filterPurchased && ctx.purchasedArchetypes != null &&
                    ctx.purchasedArchetypes.Contains(shelf.archetypeId))
                {
                    continue; // 跳过已购买过的archetype
                }

                // 第二步：计算得分
                float score = CalculateShelfScore(ctx, shelf, interest);

                if (score > 0)
                {
                    validCandidates.Add((i, score));
                }
            }

            // 如果没有有效候选
            if (validCandidates.Count == 0)
            {
                if (enableDebugLog) Debug.Log($"[WeightedRandomSelector] 顾客 {ctx.customerId}: 筛选后无有效货架");
                return -1;
            }

            // 第三步：加权随机选择
            int selectedIndex = PerformWeightedRandomSelection(validCandidates);

            if (enableDebugLog && selectedIndex >= 0)
            {
                var selected = candidates[selectedIndex];
                Debug.Log($"[WeightedRandomSelector] 顾客 {ctx.customerId} 选择了货架 {selected.shelfId} " +
                         $"(类别:{selected.categoryIndex}, 库存:{selected.stock}, 队列:{selected.queueLength})");
            }

            return selectedIndex;
        }

        /// <summary>
        /// 计算单个货架的得分
        /// </summary>
        private float CalculateShelfScore(in CustomerContext ctx, in ShelfSnapshot shelf, float interest)
        {
            float score = 0;

            // 1. 兴趣匹配分
            float interestScore = interest * interestWeight;
            score += interestScore;

            // 2. 吸引力分
            float attractivenessScore = shelf.attractiveness * attractivenessWeight;
            score += attractivenessScore;

            // 3. 队列惩罚（使用曲线）
            float queuePenalty = (1f - queuePenaltyCurve.Evaluate(shelf.queueLength)) * queuePenaltyWeight;
            score -= queuePenalty;

            // 4. 距离惩罚（简化计算，使用曼哈顿距离）
            // TODO: 获取顾客当前位置后计算实际距离
            // float distance = Vector2Int.Distance(currentPos, shelf.gridCell);
            // float distancePenalty = (distance / 20f) * distanceWeight; // 归一化距离
            // score -= distancePenalty;

            // 确保得分非负
            return Mathf.Max(0, score);
        }

        /// <summary>
        /// 获取对应类别的兴趣值
        /// </summary>
        private float GetInterestForCategory(float[] interests, int categoryIndex)
        {
            if (interests == null || categoryIndex < 0 || categoryIndex >= interests.Length)
            {
                return 0f;
            }
            return interests[categoryIndex];
        }

        /// <summary>
        /// 执行加权随机选择
        /// </summary>
        private int PerformWeightedRandomSelection(List<(int index, float score)> candidates)
        {
            if (candidates.Count == 0) return -1;
            if (candidates.Count == 1) return candidates[0].index;

            // 计算总权重
            float totalWeight = candidates.Sum(c => c.score);

            if (totalWeight <= 0)
            {
                // 如果所有权重都是0，则均匀随机
                return candidates[Random.Range(0, candidates.Count)].index;
            }

            // 生成随机值
            float randomValue = Random.Range(0f, totalWeight);

            // 累积权重选择
            float accumulated = 0;
            foreach (var (index, score) in candidates)
            {
                accumulated += score;
                if (randomValue <= accumulated)
                {
                    return index;
                }
            }

            // 理论上不应该到这里，但作为保险返回最后一个
            return candidates[candidates.Count - 1].index;
        }
    }
}