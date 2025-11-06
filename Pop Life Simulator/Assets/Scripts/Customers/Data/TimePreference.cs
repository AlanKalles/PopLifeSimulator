using System;
using UnityEngine;

namespace PopLife.Customers.Data
{
    /// <summary>
    /// 定义时间范围数据结构
    /// 用于Trait的时间倾向和Archetype的生成时间窗口
    /// 支持跨日营业：时间范围可扩展到0-36小时（如27.0表示次日03:00）
    /// </summary>
    [Serializable]
    public class TimePreference
    {
        [Tooltip("起始时间 (扩展时间制，0-36小时，如12.0表示12:00，27.0表示次日03:00)")]
        [Range(0f, 36f)]
        public float startHour = 12f;  // 12:00

        [Tooltip("结束时间 (扩展时间制，0-36小时，如22.5表示22:30，27.0表示次日03:00)")]
        [Range(0f, 36f)]
        public float endHour = 22.5f;  // 22:30

        /// <summary>
        /// 判断给定时间是否在该时间范围内
        /// </summary>
        /// <param name="currentHour">当前时间 (扩展时间制，0-36小时)</param>
        /// <returns>是否在范围内</returns>
        public bool IsInRange(float currentHour)
        {
            return currentHour >= startHour && currentHour <= endHour;
        }

        /// <summary>
        /// 获取该时间范围的中心点
        /// </summary>
        public float GetCenter()
        {
            return (startHour + endHour) / 2f;
        }

        /// <summary>
        /// 获取该时间范围的持续时长（小时）
        /// </summary>
        public float GetDuration()
        {
            return endHour - startHour;
        }
    }
}
