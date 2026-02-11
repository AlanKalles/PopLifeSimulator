using System;
using UnityEngine;

namespace PopLife.Data
{
    /// <summary>
    /// 任务类型枚举
    /// </summary>
    public enum QuestType
    {
        Main, // 主线任务
        Side  // 支线任务
    }

    /// <summary>
    /// 奖励类型枚举
    /// </summary>
    public enum RewardType
    {
        Money,
        Fame,
        Blueprint,
        Customer
    }

    /// <summary>
    /// 任务奖励数据
    /// </summary>
    [Serializable]
    public class QuestReward
    {
        public RewardType type;
        public int amount;
        [Tooltip("Blueprint/Customer 类型时使用的物品ID")]
        public string itemId;
    }

    /// <summary>
    /// 任务元数据 ScriptableObject
    /// 与 Dialogue System 的 QuestLog 通过 questName 关联
    /// </summary>
    [CreateAssetMenu(menuName = "PopLife/Quest/QuestDefinition")]
    public class QuestDefinition : ScriptableObject
    {
        [Header("关联标识")]
        [Tooltip("必须与 Dialogue Database 中 Quest 的 Name 完全一致")]
        [SerializeField] private string questName;

        [Header("分类")]
        [SerializeField] private QuestType questType = QuestType.Side;

        [Header("截止日期")]
        [Tooltip("从任务激活日起的有效天数，0 表示永不过期")]
        [SerializeField] private int deadlineDays = 0;

        [Header("颁布者")]
        [SerializeField] private string giverName;
        [SerializeField] private Sprite giverPortrait;

        [Header("奖励")]
        [SerializeField] private QuestReward[] rewards;

        [Header("UI 显示")]
        [SerializeField] private Sprite questIcon;
        [Tooltip("排序优先级，值越大越靠前")]
        [Range(0, 100)]
        [SerializeField] private int sortPriority = 50;

        // 公共属性
        public string QuestName => questName;
        public QuestType QuestType => questType;
        public int DeadlineDays => deadlineDays;
        public string GiverName => giverName;
        public Sprite GiverPortrait => giverPortrait;
        public QuestReward[] Rewards => rewards;
        public Sprite QuestIcon => questIcon;
        public int SortPriority => sortPriority;
    }
}
