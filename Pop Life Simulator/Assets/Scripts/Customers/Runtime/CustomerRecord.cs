using System;
using System.Collections.Generic;
using UnityEngine;
namespace PopLife.Customers.Runtime
{
    [Serializable]
    public class CustomerRecord
    {
// —— 主键与身份 ——
        public string customerId; // 自定义ID格式: C001(普通) 或 V001(VIP)
        public string name;
        [TextArea] public string bio;


// —— 行为基线来源 ——
        public string archetypeId;

// —— 偏好货架（按优先级排序，最多4个，存储 archetypeId 字符串）——
        public string[] favoriteShelfIds = new string[4];


// —— 长期属性 ——
        public int trust; // 可随日增长
        public int loyaltyLevel; // 熟客等级
        public int xp; // 用于计算 loyaltyLevel 的经验


// 钱袋上限的个体基线（来店刷新时用）
        public int walletCapBase = 100;

        // —— 统计 ——
        public int visitCount;
        public string lastVisitDay;
        public string lastLeaveReason;
        public int lifetimeSpent;

// —— 对话交互 ——
        public string[] availableNarrativeIds = Array.Empty<string>(); // 该顾客可用的对话ID列表


// —— 版本 ——
        public int schemaVersion = 1;
    }
}