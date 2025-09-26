using System;
using System.Collections.Generic;
using UnityEngine;
using PopLife.Customers.Data;


namespace PopLife.Customers.Runtime
{
    [Serializable]
    public class AppearanceParts
    {
        public string hairId;
        public string eyesId;
        public string outfitId;
        public string accessoryId;
        public string presetId; // 若使用预设，优先读取
    }
    [Serializable]
    public class CustomerRecord
    {
// —— 主键与身份 ——
        public string customerId; // 稳定 GUID
        public string name;
        [TextArea] public string bio;
        public SexualOrientation orientation;
        public AppearanceParts appearance = new();


// —— 行为基线来源 ——
        public string archetypeId;
        public string[] traitIds = Array.Empty<string>();


// —— 个体化兴趣偏移 ——
        public int[] interestPersonalDelta = Array.Empty<int>();


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
                if (interestPersonalDelta == null) interestPersonalDelta = Array.Empty<int>();
                if (interestPersonalDelta.Length == size) return;
                var arr = new int[size];
                for (int i = 0; i < size; i++) arr[i] = (i < interestPersonalDelta.Length) ? interestPersonalDelta[i] : 0;
                interestPersonalDelta = arr;
        }
        public int[] ComposeFinalInterest(CustomerArchetype archetype, int categories, IReadOnlyList<Trait> traits)
        {
                var baseArr = archetype.GetBaseInterestClamped(categories);
                EnsureInterestSize(categories);
                var res = new int[categories];
                for (int i = 0; i < categories; i++) res[i] = Mathf.Clamp(baseArr[i] + interestPersonalDelta[i], 0, 100);
// Trait 加成
                if (traits != null)
                {
                        for (int t = 0; t < traits.Count; t++)
                        {
                                var tr = traits[t];
                                if (tr == null) continue;
                                if (tr.interestAdd != null && tr.interestAdd.Length == res.Length)
                                {
                                        for (int i = 0; i < res.Length; i++) res[i] += tr.interestAdd[i];
                                }
                                for (int i = 0; i < res.Length; i++) res[i] = Mathf.RoundToInt(res[i] * tr.interestMul);
                        }
                        for (int i = 0; i < res.Length; i++) res[i] = Mathf.Clamp(res[i], 0, 100);
                }
                return res;
        }
    }
}