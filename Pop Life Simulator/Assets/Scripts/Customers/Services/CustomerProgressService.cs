using UnityEngine;
using PopLife.Customers.Data;
using PopLife.Customers.Runtime;
using Sirenix.OdinInspector;

namespace PopLife.Customers.Services
{
    /// <summary>
    /// 顾客经验和升级服务
    /// MonoBehaviour 单例，Inspector 可配置漏斗层级 XP 倍率
    /// </summary>
    public class CustomerProgressService : MonoBehaviour
    {
        public static CustomerProgressService Instance { get; private set; }

        [Title("漏斗层级 XP 倍率")]
        [BoxGroup("Funnel XP")]
        [LabelText("Favorite"), SerializeField] private float favoriteXpMul = 1.5f;
        [BoxGroup("Funnel XP")]
        [LabelText("Category"), SerializeField] private float categoryXpMul = 1.2f;
        [BoxGroup("Funnel XP")]
        [LabelText("Brand"), SerializeField] private float brandXpMul = 1.0f;
        [BoxGroup("Funnel XP")]
        [LabelText("PriceBased"), SerializeField] private float priceXpMul = 0.8f;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(this);
            }
        }

        /// <summary>
        /// 获取漏斗层级对应的 XP 倍率
        /// </summary>
        public float GetFunnelXpMultiplier(FunnelPhase phase)
        {
            return phase switch
            {
                FunnelPhase.Favorite => favoriteXpMul,
                FunnelPhase.Category => categoryXpMul,
                FunnelPhase.Brand => brandXpMul,
                FunnelPhase.PriceBased => priceXpMul,
                _ => 1f
            };
        }

        /// <summary>
        /// 计算经验增量
        /// 公式：基础XP × 漏斗层级加权平均倍率 × 消费乘数
        /// </summary>
        public int CalculateXpGain(
            CustomerSession session,
            CustomerArchetype archetype)
        {
            if (session == null || archetype == null)
                return 0;

            // 1. 基础经验值
            float baseXp = archetype.baseXpGain;

            // 2. 漏斗层级 XP 加成（所有购买的层级倍率的加权平均）
            float funnelXpBonus = 1f;
            if (session.purchaseFunnelTiers != null && session.purchaseFunnelTiers.Count > 0)
            {
                float sum = 0f;
                foreach (var tier in session.purchaseFunnelTiers)
                {
                    sum += GetFunnelXpMultiplier(tier);
                }
                funnelXpBonus = sum / session.purchaseFunnelTiers.Count;
            }

            // 3. 消费乘数（根据消费金额）
            float spendingMultiplier = archetype.GetSpendingMultiplier(session.moneySpent);

            // 4. 最终经验 = 基础 × 漏斗加成 × 消费
            float finalXp = baseXp * funnelXpBonus * spendingMultiplier;

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
        public void ApplySessionRewards(
            CustomerRecord record,
            CustomerSession session,
            CustomerArchetype archetype)
        {
            if (record == null || session == null || archetype == null)
            {
                Debug.LogWarning("[CustomerProgressService] ApplySessionRewards: null parameter detected");
                return;
            }

            // 1. 计算经验增量
            int xpGained = CalculateXpGain(session, archetype);

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

        /// <summary>
        /// 静态便捷方法（使用Instance调用，兼容旧代码过渡）
        /// </summary>
        public static void ApplySessionRewardsStatic(
            CustomerRecord record,
            CustomerSession session,
            CustomerArchetype archetype)
        {
            if (Instance != null)
            {
                Instance.ApplySessionRewards(record, session, archetype);
            }
            else
            {
                Debug.LogWarning("[CustomerProgressService] Instance not found, skipping session rewards");
            }
        }
    }
}
