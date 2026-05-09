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

        [Header("经济修正")]
        [Tooltip("该Buffer的全局修饰器载荷（由 GlobalModifierManager 聚合）")]
        public GlobalModifierPayload modifierPayload;

        [Header("延迟激活 / 动态品类（高级）")]
        [Tooltip("激活后从次日开始第一天生效（Robot Strike: 滚到当天不影响补货，从明天开始禁补）")]
        public bool delayActivationByOneDay = false;

        [Tooltip("激活时随机选一个 ProductCategory 写入 ActiveBuffer.rolledBlockedCategory（Robot Strike 用）")]
        public bool rollRandomBlockedCategory = false;

        [Header("激活时启动对话（可选）")]
        [Tooltip("对话标题（如 Auditor/BufferHappy）。空字符串 = 不触发对话。已有对话进行中会自动排队等待")]
        public string activationConversationTitle = "";

#if UNITY_EDITOR
        private void OnValidate()
        {
            bufferId = name;
        }
#endif
    }
}
