using System;
using System.Collections.Generic;
using UnityEngine;

namespace PopLife.NarrativeSystem
{
    /// <summary>
    /// 叙事序列，管理整个对话流程
    /// Narrative sequence managing the entire conversation flow
    /// </summary>
    [Serializable]
    public class NarrativeSequence
    {
        [Header("Sequence Data")]
        [SerializeField] private string sequenceID;          // 序列唯一标识
        [SerializeField] private string sequenceName;        // 序列名称
        [SerializeField] private NarrativeSegment rootSegment;     // 根片段

        [Header("Navigation State")]
        [SerializeField] private NarrativeSegment currentSegment;  // 当前片段
        [SerializeField] private List<NarrativeSegment> navigationHistory;  // 导航历史

        // Events
        public event Action<NarrativeSegment> OnSegmentChanged;
        public event Action OnSequenceCompleted;
        public event Action<List<NarrativeSegment>> OnChoicesPresented;

        // Properties
        public string SequenceID => sequenceID;
        public string SequenceName => sequenceName;
        public NarrativeSegment CurrentSegment => currentSegment;
        public NarrativeSegment RootSegment => rootSegment;
        public bool IsCompleted => currentSegment != null && currentSegment.IsEndSegment;

        /// <summary>
        /// 构造函数
        /// </summary>
        public NarrativeSequence(string id, string name)
        {
            sequenceID = id;
            sequenceName = name;
            navigationHistory = new List<NarrativeSegment>();
        }

        /// <summary>
        /// 初始化序列，设置根片段
        /// </summary>
        public void Initialize(NarrativeSegment root)
        {
            if (root == null)
            {
                Debug.LogError("[NarrativeSequence] Cannot initialize with null root segment");
                return;
            }

            rootSegment = root;
            currentSegment = root;
            navigationHistory.Clear();
            navigationHistory.Add(root);

            OnSegmentChanged?.Invoke(currentSegment);
        }

        /// <summary>
        /// 导航到下一个片段
        /// </summary>
        public bool NavigateForward(int choiceIndex = 0)
        {
            if (currentSegment == null)
            {
                Debug.LogWarning("[NarrativeSequence] No current segment to navigate from");
                return false;
            }

            if (currentSegment.IsEndSegment)
            {
                OnSequenceCompleted?.Invoke();
                return false;
            }

            var nextSegments = currentSegment.NextSegments;
            if (nextSegments == null || nextSegments.Count == 0)
            {
                Debug.Log("[NarrativeSequence] No next segments available");
                return false;
            }

            // 如果有多个选择，验证索引
            if (choiceIndex >= nextSegments.Count)
            {
                Debug.LogError($"[NarrativeSequence] Invalid choice index {choiceIndex}, max is {nextSegments.Count - 1}");
                return false;
            }

            // 导航到选择的片段
            currentSegment = nextSegments[choiceIndex];
            navigationHistory.Add(currentSegment);

            OnSegmentChanged?.Invoke(currentSegment);

            // 如果有多个选择，触发选择事件
            if (currentSegment.HasMultipleChoices())
            {
                OnChoicesPresented?.Invoke(currentSegment.NextSegments);
            }

            // 检查是否完成
            if (currentSegment.IsEndSegment)
            {
                OnSequenceCompleted?.Invoke();
            }

            return true;
        }

        /// <summary>
        /// 导航到前一个片段
        /// </summary>
        public bool NavigateBackward()
        {
            if (navigationHistory.Count <= 1)
            {
                Debug.Log("[NarrativeSequence] Already at the beginning");
                return false;
            }

            // 移除当前片段
            navigationHistory.RemoveAt(navigationHistory.Count - 1);

            // 设置为前一个片段
            currentSegment = navigationHistory[navigationHistory.Count - 1];

            OnSegmentChanged?.Invoke(currentSegment);

            // 如果有多个选择，触发选择事件
            if (currentSegment.HasMultipleChoices())
            {
                OnChoicesPresented?.Invoke(currentSegment.NextSegments);
            }

            return true;
        }

        /// <summary>
        /// 获取用于扇形显示的三个片段（上、中、下）
        /// </summary>
        public (NarrativeSegment previous, NarrativeSegment current, NarrativeSegment next) GetVisibleSegments()
        {
            if (currentSegment == null)
                return (null, null, null);

            var previous = currentSegment.PreviousSegment;
            var next = currentSegment.GetDefaultNext();

            return (previous, currentSegment, next);
        }

        /// <summary>
        /// 跳转到特定片段（用于调试或特殊需求）
        /// </summary>
        public void JumpToSegment(NarrativeSegment segment)
        {
            if (segment == null) return;

            currentSegment = segment;

            // 重建历史路径
            navigationHistory.Clear();
            var path = new List<NarrativeSegment>();
            var temp = segment;

            while (temp != null)
            {
                path.Insert(0, temp);
                temp = temp.PreviousSegment;
            }

            navigationHistory.AddRange(path);

            OnSegmentChanged?.Invoke(currentSegment);
        }

        /// <summary>
        /// 重置序列到开始状态
        /// </summary>
        public void Reset()
        {
            if (rootSegment != null)
            {
                Initialize(rootSegment);
            }
        }

        /// <summary>
        /// 获取当前进度（0-1）
        /// </summary>
        public float GetProgress()
        {
            if (navigationHistory == null || navigationHistory.Count == 0)
                return 0f;

            // 简单估算：基于导航历史深度
            // 实际项目中可能需要更复杂的计算
            const float maxExpectedDepth = 20f;
            return Mathf.Clamp01(navigationHistory.Count / maxExpectedDepth);
        }

        /// <summary>
        /// 检查是否可以后退
        /// </summary>
        public bool CanNavigateBackward()
        {
            return navigationHistory != null && navigationHistory.Count > 1;
        }

        /// <summary>
        /// 检查是否可以前进
        /// </summary>
        public bool CanNavigateForward()
        {
            return currentSegment != null &&
                   !currentSegment.IsEndSegment &&
                   currentSegment.NextSegments.Count > 0;
        }

        /// <summary>
        /// 生成下一个可用的Sequence ID
        /// Generate next available sequence ID (SEQ01, SEQ02, etc.)
        /// </summary>
        public static string GenerateSequenceID(int index)
        {
            return $"SEQ{index:D2}";
        }

        /// <summary>
        /// 从已有ID中提取序号
        /// Extract index from existing sequence ID
        /// </summary>
        public static int ExtractSequenceIndex(string sequenceID)
        {
            if (string.IsNullOrEmpty(sequenceID)) return 0;

            // Try to extract number from format like "SEQ01"
            if (sequenceID.StartsWith("SEQ") && sequenceID.Length >= 5)
            {
                string numberPart = sequenceID.Substring(3);
                if (int.TryParse(numberPart, out int index))
                    return index;
            }

            return 0;
        }
    }
}