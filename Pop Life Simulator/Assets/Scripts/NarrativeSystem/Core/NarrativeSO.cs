using System;
using UnityEngine;
using PopLife.Manager;

namespace PopLife.NarrativeSystem
{
    /// <summary>
    /// 叙事ScriptableObject，存储对话数据
    /// Narrative ScriptableObject storing conversation data
    /// </summary>
    [CreateAssetMenu(fileName = "New Narrative", menuName = "PopLife/Narrative/Narrative Data")]
    public class NarrativeSO : ScriptableObject
    {
        [Header("Basic Info")]
        [SerializeField] private string narrativeID;         // 唯一标识
        [SerializeField] private string narrativeName;       // 叙事名称
        [TextArea(2, 3)]
        [SerializeField] private string description;         // 描述

        [Header("Character Info")]
        [SerializeField] private Sprite characterPortrait;   // 角色肖像
        [SerializeField] private string characterName;       // 角色名称
        [SerializeField] private ConversationType conversationType;  // 对话类型

        [Header("Narrative Data")]
        [SerializeField] private NarrativeSequence narrativeSequence; // 叙事序列

        [Header("Trigger Conditions")]
        [SerializeField] private bool isVIPOnly;             // 是否仅限VIP
        [SerializeField] private int minimumDayRequired;     // 最小天数要求
        [SerializeField] private int minimumFameRequired;    // 最小声望要求

        [Header("Rewards")]
        [SerializeField] private RewardData[] completionRewards;  // 完成奖励

        // Properties
        public string NarrativeID => narrativeID;
        public string NarrativeName => narrativeName;
        public string Description => description;
        public Sprite CharacterPortrait => characterPortrait;
        public string CharacterName => characterName;
        public ConversationType ConversationType => conversationType;
        public NarrativeSequence NarrativeSequence => narrativeSequence;
        public bool IsVIPOnly => isVIPOnly;
        public RewardData[] CompletionRewards => completionRewards;

        /// <summary>
        /// 检查是否满足触发条件
        /// </summary>
        public bool CheckTriggerConditions(int currentDay, int currentFame, bool isVIP)
        {
            if (isVIPOnly && !isVIP)
                return false;

            if (currentDay < minimumDayRequired)
                return false;

            if (currentFame < minimumFameRequired)
                return false;

            return true;
        }

        /// <summary>
        /// 创建新的叙事序列实例
        /// </summary>
        public NarrativeSequence CreateSequenceInstance()
        {
            if (narrativeSequence == null)
            {
                Debug.LogError($"[NarrativeSO] {narrativeID} has no narrative sequence data!");
                return null;
            }

            // 创建序列的深拷贝，避免修改原始数据
            var instance = new NarrativeSequence(narrativeSequence.SequenceID, narrativeSequence.SequenceName);

            if (narrativeSequence.RootSegment != null)
            {
                // 创建段落的深拷贝并重建链接
                var segmentCopies = new System.Collections.Generic.Dictionary<NarrativeSegment, NarrativeSegment>();
                var rootCopy = DeepCopySegment(narrativeSequence.RootSegment, segmentCopies);

                // 重建段落之间的链接关系
                RebuildSegmentLinks(narrativeSequence.RootSegment, segmentCopies);

                // 不在这里调用 Initialize，让 NarrativeManager 在订阅事件后调用
                // Don't call Initialize here, let NarrativeManager call it after subscribing to events
                instance.RootSegment = rootCopy;
            }

            return instance;
        }

        /// <summary>
        /// 深拷贝段落（递归）
        /// </summary>
        private NarrativeSegment DeepCopySegment(NarrativeSegment original,
            System.Collections.Generic.Dictionary<NarrativeSegment, NarrativeSegment> copies)
        {
            if (original == null) return null;

            // 如果已经拷贝过，返回已有的拷贝
            if (copies.ContainsKey(original))
                return copies[original];

            // 创建新的段落实例
            var copy = new NarrativeSegment(
                original.SegmentID,
                original.SpeakerName,
                original.TextContent
            );

            // 复制属性
            copy.IsEndSegment = original.IsEndSegment;
            copy.DisplayDuration = original.DisplayDuration;
            copy.SpeakerPortrait = original.SpeakerPortrait;
            copy.EndRewards = original.EndRewards;  // 复制结束节点奖励
            copy.IsChoiceNode = original.IsChoiceNode;  // 复制选择节点标记

            // 记录拷贝
            copies[original] = copy;

            // 递归拷贝所有子段落
            foreach (var nextSegment in original.NextSegments)
            {
                DeepCopySegment(nextSegment, copies);
            }

            return copy;
        }

        /// <summary>
        /// 重建段落之间的链接关系
        /// </summary>
        private void RebuildSegmentLinks(NarrativeSegment original,
            System.Collections.Generic.Dictionary<NarrativeSegment, NarrativeSegment> copies)
        {
            if (original == null || !copies.ContainsKey(original))
                return;

            var copy = copies[original];

            // 重建与子段落的链接
            foreach (var originalNext in original.NextSegments)
            {
                if (copies.ContainsKey(originalNext))
                {
                    var nextCopy = copies[originalNext];
                    copy.AddNextSegment(nextCopy); // 这会自动设置 previousSegment
                }
            }

            // 递归处理所有子段落
            foreach (var nextSegment in original.NextSegments)
            {
                RebuildSegmentLinks(nextSegment, copies);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// 在编辑器中验证数据
        /// </summary>
        private void OnValidate()
        {
            // 自动生成ID如果为空 - 使用简化的NARR01格式
            if (string.IsNullOrEmpty(narrativeID))
            {
                narrativeID = GenerateNarrativeID();
            }

            // 确保名称不为空
            if (string.IsNullOrEmpty(narrativeName))
            {
                narrativeName = name; // 使用资产名称
            }
        }

        /// <summary>
        /// 生成叙事ID (NARR01, NARR02, etc.)
        /// </summary>
        private string GenerateNarrativeID()
        {
            // In a real implementation, you might want to scan existing assets
            // For now, generate based on current timestamp
            int index = (System.DateTime.Now.Millisecond % 99) + 1;
            return $"NARR{index:D2}";
        }
#endif
    }

    /// <summary>
    /// 对话类型枚举
    /// </summary>
    public enum ConversationType
    {
        Regular,        // 普通对话
        VIP,           // VIP对话
        Special,       // 特殊NPC对话
        Tutorial,      // 教程对话
        Random         // 随机对话
    }

    /// <summary>
    /// 奖励数据
    /// </summary>
    [Serializable]
    public class RewardData
    {
        public enum RewardType
        {
            Money,
            Fame,
            Blueprint,
            Customer,
            Item,
            Marker
        }

        [SerializeField] private RewardType type;
        [SerializeField] private string rewardID;    // 奖励ID（如蓝图ID、顾客ID等）
        [SerializeField] private int amount;         // 数量（金钱或声望）
        [SerializeField] private TutorialMarker marker;  // Marker（仅RewardType.Marker时使用）

        public RewardType Type => type;
        public string RewardID => rewardID;
        public int Amount => amount;
        public TutorialMarker Marker => marker;

        public RewardData(RewardType rewardType, string id, int value)
        {
            type = rewardType;
            rewardID = id;
            amount = value;
        }
    }
}