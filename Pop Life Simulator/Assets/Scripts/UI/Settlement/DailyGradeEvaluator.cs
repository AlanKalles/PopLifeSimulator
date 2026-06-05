using UnityEngine;

namespace PopLife.UI.Settlement
{
    /// <summary>
    /// 每日评级期望基线 — 按天数自动分 4 档
    /// Tier 1: Day 1-8   Tier 2: Day 9-16   Tier 3: Day 17-24   Tier 4: Day 25+
    /// 占位数值，后续根据玩家实际数据调优
    /// </summary>
    public struct GradeTier
    {
        public int tierNum;        // 1-4
        public int revTarget;      // Revenue 期望基线
        public int fameTarget;     // Fame 期望基线
        public int customerTarget; // Customer 期望基线（按真实入店数）

        public static GradeTier From(int day)
        {
            if (day <= 8)  return new GradeTier { tierNum = 1, revTarget = 200,  fameTarget = 8,  customerTarget = 10 };
            if (day <= 16) return new GradeTier { tierNum = 2, revTarget = 600,  fameTarget = 18, customerTarget = 25 };
            if (day <= 24) return new GradeTier { tierNum = 3, revTarget = 1500, fameTarget = 35, customerTarget = 50 };
            return                new GradeTier { tierNum = 4, revTarget = 3500, fameTarget = 60, customerTarget = 90 };
        }
    }

    /// <summary>
    /// 每日评级评分器（100 分制，3 维度）
    ///   盈利能力 (35): Profit Margin + Net Profit 绝对值
    ///   成长性  (35): Revenue 达成 + Customer 达成
    ///   Fame    (30): 自适应目标
    /// 阈值: S+ ≥95 / S ≥85 / A ≥70 / B ≥55 / C ≥35 / D <35
    /// </summary>
    public static class DailyGradeEvaluator
    {
        /// <summary>
        /// 评估当日 grade，返回评级字母 + 分数（0-100）
        /// </summary>
        public static (string grade, float score) Evaluate(DailySettlementData data, GradeTier tier)
        {
            float score = 0f;

            // ── 1. 盈利能力 (35 分) ──
            // Profit Margin (20): 净利润 / Revenue，40% 利润率封顶满分
            float totalRevenue = data.totalSale + data.lotteryWinnings + data.sponsorshipAmount
                               + data.questRewardIncome + data.refundIncome;
            float netProfit = totalRevenue - data.dailyExpenses;

            if (totalRevenue > 0)
            {
                float margin = netProfit / totalRevenue;
                score += Mathf.Clamp(margin, 0f, 0.4f) * 50f; // 0.4 × 50 = 20
            }

            // Net Profit 绝对值 (15): 与 tier.revTarget 对比
            if (tier.revTarget > 0)
            {
                float absScore = Mathf.Clamp(netProfit / tier.revTarget, 0f, 0.5f);
                score += absScore * 30f; // 0.5 × 30 = 15
            }

            // ── 2. 成长性 (35 分) ──
            // Revenue 达成 (20)
            if (tier.revTarget > 0)
            {
                float revRatio = data.totalSale / tier.revTarget;
                score += Mathf.Clamp(revRatio, 0f, 1.5f) * 13.33f; // 1.5 × 13.33 = ~20
            }

            // Customer 达成 (15): 用真实入店数（customersEntered）
            int customerCount = data.customersEntered > 0 ? data.customersEntered : data.totalCustomers;
            if (tier.customerTarget > 0)
            {
                float custRatio = (float)customerCount / tier.customerTarget;
                score += Mathf.Clamp(custRatio, 0f, 1.5f) * 10f; // 1.5 × 10 = 15
            }

            // ── 3. Fame 收获 (30 分) ──
            if (tier.fameTarget > 0)
            {
                float fameRatio = (float)data.fameEarned / tier.fameTarget;
                score += Mathf.Clamp(fameRatio, 0f, 1.5f) * 20f; // 1.5 × 20 = 30
            }

            // ── 阈值映射 ──
            string grade;
            if (score >= 95f) grade = "S+";
            else if (score >= 85f) grade = "S";
            else if (score >= 70f) grade = "A";
            else if (score >= 55f) grade = "B";
            else if (score >= 35f) grade = "C";
            else grade = "D";

            return (grade, score);
        }
    }
}
