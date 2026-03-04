using UnityEngine;

namespace PopLife.Data
{
    /// <summary>
    /// Buffer 触发方式
    /// </summary>
    public enum BufferTriggerType
    {
        Auto,   // 每天自动概率触发
        Manual  // 玩家主动花钱触发
    }

    /// <summary>
    /// Buffer ScriptableObject
    /// Auto-Buffer: 每天开始时检查触发概率
    /// Manual-Buffer: 玩家花费 hostCost 主动触发
    /// </summary>
    [CreateAssetMenu(menuName = "PopLife/Calendar/Buffer")]
    public class BufferData : ScriptableObject
    {
        [Header("基础信息")]
        [Tooltip("自动同步为文件名")]
        [HideInInspector] public string bufferId;

        [Tooltip("英文显示名（面向玩家）")]
        public string displayName;

        [TextArea(2, 4)]
        [Tooltip("英文描述")]
        public string description;

        [Tooltip("Buffer图标")]
        public Sprite icon;

        [Header("触发方式")]
        public BufferTriggerType triggerType;

        [Header("Auto-Buffer 配置")]
        [Tooltip("每天开始时触发概率 (0-1)")]
        [Range(0f, 1f)]
        public float occurProbability = 0.1f;

        [Header("Manual-Buffer 配置")]
        [Tooltip("玩家主动触发的费用")]
        public int hostCost = 0;

        [Header("持续时间")]
        [Tooltip("0 = 无限持续（靠终止概率结束）, >0 = 固定天数")]
        public int durationDays = 0;

        [Tooltip("每天开始时自动终止概率（仅 durationDays==0 时生效）")]
        [Range(0f, 1f)]
        public float terminateProbability = 0.2f;

#if UNITY_EDITOR
        private void OnValidate()
        {
            bufferId = name;
        }
#endif
    }
}
