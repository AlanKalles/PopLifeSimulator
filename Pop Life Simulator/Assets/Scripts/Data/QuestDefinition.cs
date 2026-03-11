using System;
using UnityEngine;
using PopLife.Quest;
using PopLife.Manager;

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

        [Header("完成条件")]
        [Tooltip("每个条件对应一个 Quest Entry（索引0=Entry1）。Manual类型由外部标记完成。为空则完全由外部控制。")]
        [SerializeField] private QuestCondition[] conditions;

        [Header("自动激活")]
        [Tooltip("当此 Marker 触发时自动激活此任务（None = 不自动激活，需通过 Lua 或其他方式激活）")]
        [SerializeField] private TutorialMarker activationMarker = TutorialMarker.None;

        [Header("完成时触发")]
        [Tooltip("任务完成时触发的 Marker（None = 不触发）")]
        [SerializeField] private TutorialMarker completionMarker = TutorialMarker.None;
        [Tooltip("勾选后，Marker 在完成通知 Toast 消失后才触发；否则任务完成时立即触发")]
        [SerializeField] private bool triggerMarkerAfterToast = false;

        [Header("UI 显示")]
        [SerializeField] private Sprite questIcon;
        [Tooltip("排序优先级，值越大越靠前")]
        [Range(0, 100)]
        [SerializeField] private int sortPriority = 50;

        // 公共属性
        public QuestCondition[] Conditions => conditions;
        public string QuestName => questName;
        public QuestType QuestType => questType;
        public int DeadlineDays => deadlineDays;
        public string GiverName => giverName;
        public Sprite GiverPortrait => giverPortrait;
        public QuestReward[] Rewards => rewards;
        public TutorialMarker ActivationMarker => activationMarker;
        public TutorialMarker CompletionMarker => completionMarker;
        public bool TriggerMarkerAfterToast => triggerMarkerAfterToast;
        public Sprite QuestIcon => questIcon;
        public int SortPriority => sortPriority;
    }
}
