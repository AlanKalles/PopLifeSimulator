using UnityEngine;
using PopLife.Customers.Data;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.Services
{
    /// <summary>
    /// 顾客经验和升级服务
    /// </summary>
    public static class CustomerProgressService
    {
        /// <summary>
        /// 计算经验增量
        /// 公式：基础XP × 特质乘数 × 消费乘数
        /// </summary>
        public static int CalculateXpGain(
            CustomerSession session,
            CustomerArchetype archetype,
            Trait[] traits)
        {
            if (session == null || archetype == null)
                return 0;

            // 1. 基础经验值
            float baseXp = archetype.baseXpGain;

            // 2. 特质乘数（累乘所有特质的xpMultiplier）
            float traitMultiplier = 1f;
            if (traits != null)
            {
                var stats = TraitResolver.Compute(traits);
                traitMultiplier = stats.xpMul;
            }

            // 3. 消费乘数（根据消费金额）
            float spendingMultiplier = archetype.GetSpendingMultiplier(session.moneySpent);

            // 4. 最终经验 = 基础 × 特质 × 消费
            float finalXp = baseXp * traitMultiplier * spendingMultiplier;

            return Mathf.RoundToInt(finalXp);
        }

        /// <summary>
        /// 计算累积式等级
        /// 根据当前总经验和阈值数组，计算应该达到的等级
        /// </summary>
        public static int CalculateLevel(int currentXp, int[] thresholds)
        {
            if (thresholds == null || thresholds.Length == 0)
                return 0;

            int level = 0;
            for (int i = 0; i < thresholds.Length; i++)
            {
                if (currentXp >= thresholds[i])
                {
                    level = i + 1;
                }
                else
                {
                    break;
                }
            }

            return level;
        }

        /// <summary>
        /// 应用会话奖励（经验、升级、记录）
        /// </summary>
        public static void ApplySessionRewards(
            CustomerRecord record,
            CustomerSession session,
            CustomerArchetype archetype,
            Trait[] traits)
        {
            if (record == null || session == null || archetype == null)
            {
                Debug.LogWarning("[CustomerProgressService] ApplySessionRewards: null parameter detected");
                return;
            }

            // 1. 计算经验增量
            int xpGained = CalculateXpGain(session, archetype, traits);

            // 2. 记录升级前等级
            int oldLevel = record.loyaltyLevel;

            // 3. 增加经验（累积式，不清零）
            record.xp += xpGained;

            // 4. 计算新等级
            int newLevel = CalculateLevel(record.xp, archetype.levelUpThresholds);
            record.loyaltyLevel = newLevel;

            // 5. 如果升级了，记录到 DayLoopManager
            if (newLevel > oldLevel && PopLife.DayLoopManager.Instance != null)
            {
                var levelUpInfo = new CustomerLevelUpInfo
                {
                    customerId = record.customerId,
                    customerName = record.name,
                    oldLevel = oldLevel,
                    newLevel = newLevel,
                    totalXp = record.xp,
                    xpGained = xpGained,
                    appearanceId = record.appearanceId
                };

                PopLife.DayLoopManager.Instance.RecordCustomerLevelUp(levelUpInfo);

                Debug.Log($"[CustomerProgressService] Customer {record.name} leveled up: {oldLevel} → {newLevel} (XP: {record.xp})");
            }

            // 6. 记录会话统计
            if (session.moneySpent > 0)
            {
                record.lifetimeSpent += session.moneySpent;
                record.visitCount++;
            }
        }
    }
}
