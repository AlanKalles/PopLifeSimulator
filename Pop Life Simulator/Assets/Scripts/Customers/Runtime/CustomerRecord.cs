using System;
using System.Collections.Generic;
using UnityEngine;
using PopLife.Customers.Data;


namespace PopLife.Customers.Runtime
{
    [Serializable]
    public class CustomerRecord
    {
// —— 主键与身份 ——
        public string customerId; // 自定义ID格式: C001(普通) 或 V001(VIP)
        public string name;
        [TextArea] public string bio;
        public string appearanceId; // 外貌预设ID，对应 AppearanceDatabase 中的条目


// —— 行为基线来源 ——
        public string archetypeId;
        public string[] traitIds = Array.Empty<string>();


// —— 个体化兴趣偏移 ——
        public float[] interestPersonalDelta = Array.Empty<float>();


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


// —— 版本 ——
        public int schemaVersion = 1;


// —— 工具：确保兴趣长度 ——
        public void EnsureInterestSize(int size)
        {
                if (interestPersonalDelta == null) interestPersonalDelta = Array.Empty<float>();
                if (interestPersonalDelta.Length == size) return;
                var arr = new float[size];
                for (int i = 0; i < size; i++) arr[i] = (i < interestPersonalDelta.Length) ? interestPersonalDelta[i] : 0f;
                interestPersonalDelta = arr;
        }
        public float[] ComposeFinalInterest(CustomerArchetype archetype, int categories, IReadOnlyList<Trait> traits)
        {
                var baseArr = archetype.GetBaseInterest(categories);
                EnsureInterestSize(categories);
                var res = new float[categories];

                // 1. 基础值 = 原型 + 个体偏移
                for (int i = 0; i < categories; i++) res[i] = baseArr[i] + interestPersonalDelta[i];

                // 2. 先汇总所有 Trait 的加法
                if (traits != null)
                {
                        for (int t = 0; t < traits.Count; t++)
                        {
                                var tr = traits[t];
                                if (tr == null) continue;
                                if (tr.interestAdd != null && tr.interestAdd.Length == categories)
                                {
                                        for (int i = 0; i < categories; i++) res[i] += tr.interestAdd[i];
                                }
                        }

                        // 3. 再对每个 category 应用所有 Trait 的乘法
                        for (int i = 0; i < categories; i++)
                        {
                                for (int t = 0; t < traits.Count; t++)
                                {
                                        var tr = traits[t];
                                        if (tr == null) continue;
                                        if (tr.interestMul != null && tr.interestMul.Length == categories)
                                        {
                                                res[i] *= tr.interestMul[i];
                                        }
                                }
                        }

                        // 4. 确保非负
                        for (int i = 0; i < res.Length; i++) res[i] = Mathf.Max(res[i], 0f);
                }
                else
                {
                        // 没有 traits 时也要确保非负
                        for (int i = 0; i < res.Length; i++) res[i] = Mathf.Max(res[i], 0f);
                }

                return res;
        }
    }
}