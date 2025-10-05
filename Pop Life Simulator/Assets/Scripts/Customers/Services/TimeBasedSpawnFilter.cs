using System.Collections.Generic;
using UnityEngine;
using PopLife.Customers.Data;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.Services
{
    /// <summary>
    /// 加权顾客结构体
    /// </summary>
    public struct WeightedCustomer
    {
        public CustomerRecord record;
        public float finalWeight;
    }

    /// <summary>
    /// 根据当前时间筛选并计算customer生成权重
    /// 核心逻辑:
    /// 1. 检查Archetype的spawnTimeWindow (硬性过滤)
    /// 2. 检查Trait的preferredTimeRanges (加权加成)
    /// 3. 返回加权列表
    /// </summary>
    public class TimeBasedSpawnFilter
    {
        private readonly CustomerRepository repository;

        public TimeBasedSpawnFilter(CustomerRepository repository)
        {
            this.repository = repository;
        }

        /// <summary>
        /// 获取在当前时间段符合条件的顾客列表（带权重）
        /// </summary>
        /// <param name="candidates">候选顾客列表</param>
        /// <param name="currentHour">当前游戏时间（24小时制）</param>
        /// <returns>加权顾客列表</returns>
        public List<WeightedCustomer> GetEligibleCustomers(
            List<CustomerRecord> candidates,
            float currentHour)
        {
            var result = new List<WeightedCustomer>();

            foreach (var record in candidates)
            {
                float weight = CalculateTimeWeight(record, currentHour);

                // 权重为0表示不符合时间窗口，跳过
                if (weight <= 0f)
                    continue;

                result.Add(new WeightedCustomer
                {
                    record = record,
                    finalWeight = weight
                });
            }

            return result;
        }

        /// <summary>
        /// 计算单个顾客在当前时间的生成权重
        /// </summary>
        private float CalculateTimeWeight(CustomerRecord record, float currentHour)
        {
            // 1. 加载archetype
            var archetype = LoadArchetype(record.archetypeId);
            if (archetype == null)
            {
                Debug.LogWarning($"[TimeBasedSpawnFilter] Archetype not found: {record.archetypeId}");
                return 0f;
            }

            // 2. 检查archetype时间窗口 (硬性过滤)
            if (archetype.spawnTimeWindow == null || !archetype.spawnTimeWindow.IsInRange(currentHour))
            {
                return 0f;
            }

            // 3. 基础权重来自archetype
            float weight = archetype.spawnWeight;

            // 4. 加载traits并应用时间倾向加成
            var traits = LoadTraits(record.traitIds);
            foreach (var trait in traits)
            {
                if (trait.preferredTimeRanges != null && trait.preferredTimeRanges.Length > 0)
                {
                    // 检查当前时间是否在该trait的任意偏好时间段内
                    bool inPreferredTime = false;
                    foreach (var timeRange in trait.preferredTimeRanges)
                    {
                        if (timeRange != null && timeRange.IsInRange(currentHour))
                        {
                            inPreferredTime = true;
                            break;
                        }
                    }

                    // 如果在偏好时间段内，应用权重倍率
                    if (inPreferredTime)
                    {
                        weight *= trait.timePreferenceWeight;
                    }
                }
            }

            return weight;
        }

        /// <summary>
        /// 加载Archetype ScriptableObject
        /// </summary>
        private CustomerArchetype LoadArchetype(string archetypeId)
        {
            if (string.IsNullOrEmpty(archetypeId))
                return null;

            // 从Resources加载
            var archetype = Resources.Load<CustomerArchetype>($"ScriptableObjects/Archetypes/{archetypeId}");

            // 如果Resources中没找到，尝试直接用名字搜索所有资源
            if (archetype == null)
            {
                var allArchetypes = Resources.LoadAll<CustomerArchetype>("ScriptableObjects");
                foreach (var a in allArchetypes)
                {
                    if (a.archetypeId == archetypeId || a.name == archetypeId)
                    {
                        archetype = a;
                        break;
                    }
                }
            }

            return archetype;
        }

        /// <summary>
        /// 加载Trait ScriptableObject列表（支持数组）
        /// </summary>
        private List<Trait> LoadTraits(string[] traitIds)
        {
            var traits = new List<Trait>();

            if (traitIds == null || traitIds.Length == 0)
                return traits;

            // 缓存所有traits以提高性能
            var allTraits = Resources.LoadAll<Trait>("ScriptableObjects/Traits");

            foreach (var traitId in traitIds)
            {
                if (string.IsNullOrEmpty(traitId))
                    continue;

                Trait trait = null;

                // 先尝试直接加载
                trait = Resources.Load<Trait>($"ScriptableObjects/Traits/{traitId}");

                // 如果失败，从缓存中搜索
                if (trait == null)
                {
                    foreach (var t in allTraits)
                    {
                        if (t.traitId == traitId || t.name == traitId)
                        {
                            trait = t;
                            break;
                        }
                    }
                }

                if (trait != null)
                {
                    traits.Add(trait);
                }
            }

            return traits;
        }
    }
}
