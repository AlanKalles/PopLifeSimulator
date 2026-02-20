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
    /// 2. 使用Archetype的spawnWeight作为基础权重
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
            return archetype.spawnWeight;
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
    }
}
