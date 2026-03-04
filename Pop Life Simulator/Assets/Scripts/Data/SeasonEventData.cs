using UnityEngine;

namespace PopLife.Data
{
    /// <summary>
    /// 季节事件（节日）ScriptableObject
    /// 每年同一季节同一天重复触发
    /// </summary>
    [CreateAssetMenu(menuName = "PopLife/Calendar/SeasonEvent")]
    public class SeasonEventData : ScriptableObject
    {
        [Header("基础信息")]
        [Tooltip("自动同步为文件名")]
        [HideInInspector] public string eventId;

        [Tooltip("英文显示名（面向玩家）")]
        public string displayName;

        [TextArea(2, 4)]
        [Tooltip("英文描述")]
        public string description;

        [Tooltip("事件图标")]
        public Sprite icon;

        [Header("时间配置")]
        [Tooltip("所属季节")]
        public Season season;

        [Tooltip("季节中第几天 (1-12)")]
        [Range(1, 12)]
        public int dayInSeason = 1;

        [Tooltip("持续天数（1 = 仅当天）")]
        [Min(1)]
        public int durationDays = 1;

#if UNITY_EDITOR
        private void OnValidate()
        {
            eventId = name;
        }
#endif
    }
}
