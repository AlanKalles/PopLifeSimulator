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
        public NarrativeSegment RootSegment
        {
            get => rootSegment;
            set => rootSegment = value;  // 允许设置（仅在创建序列时使用）
        }
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
        /// Initialize sequence and set root segment
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

            // 如果根节点是选择节点，立即触发选择事件
            // If root segment is a choice node, immediately trigger choice presentation
            if (currentSegment.IsChoiceNode && currentSegment.HasMultipleChoices())
            {
                Debug.Log($"[NarrativeSequence] Root is choice node, triggering OnChoicesPresented with {currentSegment.NextSegments.Count} choices");
                OnChoicesPresented?.Invoke(currentSegment.NextSegments);
            }
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

            // 如果当前节点是选择节点且有多个选择，需要等待玩家选择
            // 只有当 choiceIndex 由玩家选择按钮触发时才继续
            // If current segment is a choice node with multiple choices, wait for player selection
            if (currentSegment.IsChoiceNode && currentSegment.HasMultipleChoices())
            {
                // 触发选择展示事件，让UI显示选择按钮
                Debug.Log($"[NarrativeSequence] Current is choice node, triggering OnChoicesPresented with {currentSegment.NextSegments.Count} choices");
                OnChoicesPresented?.Invoke(currentSegment.NextSegments);
                return false; // 阻止自动导航，等待玩家选择
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

            // 检查是否完成
            if (currentSegment.IsEndSegment)
            {
                OnSequenceCompleted?.Invoke();
            }

            return true;
        }

        /// <summary>
        /// 处理玩家选择（从选择按钮触发）
        /// Handle player choice selection (triggered from choice buttons)
        /// </summary>
        public bool SelectChoice(int choiceIndex)
        {
            if (currentSegment == null)
            {
                Debug.LogWarning("[NarrativeSequence] No current segment for choice selection");
                return false;
            }

            if (!currentSegment.IsChoiceNode || !currentSegment.HasMultipleChoices())
            {
                Debug.LogWarning("[NarrativeSequence] Current segment is not a choice node");
                return false;
            }

            var nextSegments = currentSegment.NextSegments;
            if (choiceIndex >= nextSegments.Count)
            {
                Debug.LogError($"[NarrativeSequence] Invalid choice index {choiceIndex}, max is {nextSegments.Count - 1}");
                return false;
            }

            // 获取玩家选择的分支节点（玩家回答文本）
            // Get the player choice branch segment (player response text)
            var playerResponseSegment = nextSegments[choiceIndex];

            // 跳过玩家回答节点，直接导航到 customer 回应节点
            // Skip player response segment, navigate directly to customer response segment
            var customerResponseSegment = playerResponseSegment.GetDefaultNext();

            if (customerResponseSegment == null)
            {
                Debug.LogError($"[NarrativeSequence] Choice branch {choiceIndex} has no customer response segment!");
                return false;
            }

            // 设置为 customer 回应节点
            currentSegment = customerResponseSegment;

            // 断开与之前节点的链接（这是新对话的起点）
            // Break the link to previous segments (this is the start of a new conversation)
            currentSegment.PreviousSegment = null;

            // 清除导航历史，开始新的对话分支（选择后不能往回翻阅）
            // Clear navigation history to start a new conversation branch (cannot go back after choice)
            navigationHistory.Clear();
            navigationHistory.Add(currentSegment);

            OnSegmentChanged?.Invoke(currentSegment);

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

            // 如果回退到选择节点且有多个选择，重新触发选择事件显示选择按钮
            // If navigating back to a choice node with multiple choices, re-trigger choice presentation
            if (currentSegment.IsChoiceNode && currentSegment.HasMultipleChoices())
            {
                OnChoicesPresented?.Invoke(currentSegment.NextSegments);
            }

            return true;
        }

        /// <summary>
        /// 获取用于扇形显示的三个片段（上、中、下）
        /// Get three segments for fan display (top, center, bottom)
        /// </summary>
        public (NarrativeSegment previous, NarrativeSegment current, NarrativeSegment next) GetVisibleSegments()
        {
            if (currentSegment == null)
                return (null, null, null);

            var previous = currentSegment.PreviousSegment;

            // 如果当前节点是选择节点，不显示下一个节点（显示选择按钮代替）
            // If current segment is a choice node, don't show next segment (choice buttons will be shown instead)
            var next = currentSegment.IsChoiceNode ? null : currentSegment.GetDefaultNext();

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