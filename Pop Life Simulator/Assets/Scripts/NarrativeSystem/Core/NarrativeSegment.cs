using System;
using System.Collections.Generic;
using UnityEngine;

namespace PopLife.NarrativeSystem
{
    /// <summary>
    /// 单个叙事片段，代表对话中的一个节点
    /// A single narrative segment representing one node in the conversation
    /// </summary>
    [Serializable]
    public class NarrativeSegment
    {
        [Header("Content")]
        [SerializeField] private string segmentID;           // 片段唯一标识
        [SerializeField] private string speakerName;         // 说话者名称
        [TextArea(3, 5)]
        [SerializeField] private string textContent;         // 文本内容
        [SerializeField] private Sprite speakerPortrait;     // 说话者肖像（可选）

        [Header("Navigation")]
        [SerializeField] private List<NarrativeSegment> nextSegments;    // 后续片段列表
        [SerializeField] private NarrativeSegment previousSegment;       // 前一片段引用

        [Header("Properties")]
        [SerializeField] private bool isEndSegment;          // 是否为结束片段
        [SerializeField] private float displayDuration;      // 显示持续时间（0表示需要点击继续）

        [Header("Choice Node")]
        [SerializeField] private bool isChoiceNode;          // 是否为选择节点（显示3个选择按钮）

        [Header("End Segment Rewards")]
        [Tooltip("仅当 Is End Segment 勾选时有效")]
        [SerializeField] private RewardData[] endRewards;    // 结束节点的奖励

        // Properties
        public RewardData[] EndRewards
        {
            get => endRewards;
            set => endRewards = value;
        }
        public string SegmentID => segmentID;
        public string SpeakerName => speakerName;
        public string TextContent => textContent;
        public Sprite SpeakerPortrait
        {
            get => speakerPortrait;
            set => speakerPortrait = value;
        }
        public bool IsEndSegment
        {
            get => isEndSegment;
            set => isEndSegment = value;
        }
        public float DisplayDuration
        {
            get => displayDuration;
            set => displayDuration = value;
        }
        public NarrativeSegment PreviousSegment
        {
            get => previousSegment;
            internal set => previousSegment = value;  // 仅内部设置（用于选择后断开链接）
        }
        public List<NarrativeSegment> NextSegments => nextSegments ?? new List<NarrativeSegment>();
        public bool IsChoiceNode
        {
            get => isChoiceNode;
            set => isChoiceNode = value;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public NarrativeSegment(string id, string speaker, string content)
        {
            segmentID = id;
            speakerName = speaker;
            textContent = content;
            nextSegments = new List<NarrativeSegment>();
            displayDuration = 0f; // 默认需要点击继续
            isEndSegment = false;
        }

        /// <summary>
        /// 添加后续片段
        /// </summary>
        public void AddNextSegment(NarrativeSegment segment)
        {
            if (segment == null) return;

            if (nextSegments == null)
                nextSegments = new List<NarrativeSegment>();

            if (!nextSegments.Contains(segment))
            {
                nextSegments.Add(segment);
                segment.previousSegment = this;
            }
        }

        /// <summary>
        /// 移除后续片段
        /// </summary>
        public void RemoveNextSegment(NarrativeSegment segment)
        {
            if (segment == null || nextSegments == null) return;

            if (nextSegments.Remove(segment))
            {
                if (segment.previousSegment == this)
                    segment.previousSegment = null;
            }
        }

        /// <summary>
        /// 获取默认的下一个片段（第一个子片段）
        /// </summary>
        public NarrativeSegment GetDefaultNext()
        {
            if (nextSegments == null || nextSegments.Count == 0)
                return null;

            return nextSegments[0];
        }

        /// <summary>
        /// 检查是否有多个选择分支
        /// </summary>
        public bool HasMultipleChoices()
        {
            return nextSegments != null && nextSegments.Count > 1;
        }

        /// <summary>
        /// 设置片段数据（用于编辑器）
        /// </summary>
        public void SetSegmentData(string id, string speaker, string text)
        {
            segmentID = id;
            speakerName = speaker;
            textContent = text;
        }

        /// <summary>
        /// 标记为结束片段
        /// </summary>
        public void MarkAsEnd()
        {
            isEndSegment = true;
            nextSegments?.Clear();
        }

        /// <summary>
        /// 生成下一个可用的Segment ID
        /// Generate next available segment ID (SEG01, SEG02, etc.)
        /// </summary>
        public static string GenerateSegmentID(int index)
        {
            return $"SEG{index:D2}";
        }

        /// <summary>
        /// 从已有ID中提取序号
        /// Extract index from existing segment ID
        /// </summary>
        public static int ExtractSegmentIndex(string segmentID)
        {
            if (string.IsNullOrEmpty(segmentID)) return 0;

            // Try to extract number from format like "SEG01"
            if (segmentID.StartsWith("SEG") && segmentID.Length >= 5)
            {
                string numberPart = segmentID.Substring(3);
                if (int.TryParse(numberPart, out int index))
                    return index;
            }

            return 0;
        }
    }
}