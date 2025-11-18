using System;
using System.Collections.Generic;
using UnityEngine;

namespace PopLife.GuidanceSystem
{
    /// <summary>
    /// 指引序列，管理一系列指引步骤
    /// Guidance sequence managing a series of guidance steps
    /// </summary>
    [Serializable]
    public class GuidanceSequence
    {
        [Header("Sequence Info")]
        [SerializeField] private string sequenceID;          // 序列唯一标识
        [SerializeField] private string sequenceName;        // 序列名称
        [TextArea(2, 3)]
        [SerializeField] private string description;         // 描述

        [Header("Steps")]
        [SerializeField] private List<GuidanceStep> steps;   // 步骤列表
        [SerializeField] private int currentStepIndex = -1;  // 当前步骤索引

        [Header("Settings")]
        [SerializeField] private bool allowSkip = false;     // 是否允许跳过
        [SerializeField] private bool pauseGameDuringGuidance = true; // 指引期间暂停游戏
        [SerializeField] private float stepTransitionDelay = 0.3f;     // 步骤间过渡延迟

        // Properties
        public string SequenceID => sequenceID;
        public string SequenceName => sequenceName;
        public string Description => description;
        public List<GuidanceStep> Steps => steps ?? new List<GuidanceStep>();
        public int CurrentStepIndex => currentStepIndex;
        public GuidanceStep CurrentStep => GetCurrentStep();
        public bool AllowSkip => allowSkip;
        public bool PauseGameDuringGuidance => pauseGameDuringGuidance;
        public float StepTransitionDelay => stepTransitionDelay;
        public bool IsActive => currentStepIndex >= 0 && currentStepIndex < Steps.Count;
        public bool IsCompleted => currentStepIndex >= Steps.Count;
        public float Progress => Steps.Count > 0 ? (float)(currentStepIndex + 1) / Steps.Count : 0;

        // Events
        public event Action<GuidanceStep> OnStepStarted;
        public event Action<GuidanceStep> OnStepCompleted;
        public event Action OnSequenceStarted;
        public event Action OnSequenceCompleted;
        public event Action OnSequenceSkipped;

        /// <summary>
        /// 构造函数
        /// </summary>
        public GuidanceSequence(string id, string name)
        {
            sequenceID = id;
            sequenceName = name;
            steps = new List<GuidanceStep>();
            currentStepIndex = -1;
        }

        /// <summary>
        /// 添加步骤
        /// </summary>
        public void AddStep(GuidanceStep step)
        {
            if (step == null) return;

            if (steps == null)
                steps = new List<GuidanceStep>();

            steps.Add(step);

            // Subscribe to step events
            step.OnStepCompleted += HandleStepCompleted;
        }

        /// <summary>
        /// 移除步骤
        /// </summary>
        public void RemoveStep(GuidanceStep step)
        {
            if (step == null || steps == null) return;

            if (steps.Remove(step))
            {
                step.OnStepCompleted -= HandleStepCompleted;
            }
        }

        /// <summary>
        /// 开始序列
        /// </summary>
        public bool StartSequence()
        {
            if (steps == null || steps.Count == 0)
            {
                Debug.LogWarning($"[GuidanceSequence] Cannot start {sequenceName}: no steps defined");
                return false;
            }

            // Validate all steps
            foreach (var step in steps)
            {
                if (!step.Validate())
                {
                    Debug.LogError($"[GuidanceSequence] Step validation failed in {sequenceName}");
                    return false;
                }
            }

            currentStepIndex = 0;
            OnSequenceStarted?.Invoke();

            // Start first step
            StartCurrentStep();

            Debug.Log($"[GuidanceSequence] Started: {sequenceName}");
            return true;
        }

        /// <summary>
        /// 进入下一步
        /// </summary>
        public bool AdvanceStep()
        {
            if (!IsActive)
            {
                Debug.LogWarning("[GuidanceSequence] Cannot advance: sequence not active");
                return false;
            }

            // Complete current step if not already completed
            var current = GetCurrentStep();
            if (current != null)
            {
                current.CompleteStep();
            }

            // Move to next step
            currentStepIndex++;

            if (currentStepIndex >= steps.Count)
            {
                // Sequence completed
                CompleteSequence();
                return false;
            }

            // Start next step
            StartCurrentStep();
            return true;
        }

        /// <summary>
        /// 跳到指定步骤
        /// </summary>
        public void JumpToStep(int stepIndex)
        {
            if (stepIndex < 0 || stepIndex >= steps.Count)
            {
                Debug.LogError($"[GuidanceSequence] Invalid step index: {stepIndex}");
                return;
            }

            // Complete current step
            var current = GetCurrentStep();
            if (current != null)
            {
                current.CompleteStep();
            }

            // Jump to target step
            currentStepIndex = stepIndex;
            StartCurrentStep();
        }

        /// <summary>
        /// 跳过当前步骤
        /// </summary>
        public void SkipCurrentStep()
        {
            if (!allowSkip)
            {
                Debug.Log("[GuidanceSequence] Skip not allowed for this sequence");
                return;
            }

            AdvanceStep();
        }

        /// <summary>
        /// 跳过整个序列
        /// </summary>
        public void SkipSequence()
        {
            if (!allowSkip)
            {
                Debug.Log("[GuidanceSequence] Skip not allowed for this sequence");
                return;
            }

            OnSequenceSkipped?.Invoke();
            CompleteSequence();
        }

        /// <summary>
        /// 重置序列
        /// </summary>
        public void ResetSequence()
        {
            // Complete current step if active
            var current = GetCurrentStep();
            if (current != null)
            {
                current.CompleteStep();
            }

            currentStepIndex = -1;

            Debug.Log($"[GuidanceSequence] Reset: {sequenceName}");
        }

        /// <summary>
        /// 完成序列
        /// </summary>
        private void CompleteSequence()
        {
            currentStepIndex = steps.Count;
            OnSequenceCompleted?.Invoke();

            Debug.Log($"[GuidanceSequence] Completed: {sequenceName}");
        }

        /// <summary>
        /// 开始当前步骤
        /// </summary>
        private void StartCurrentStep()
        {
            var step = GetCurrentStep();
            if (step != null)
            {
                step.StartStep();
                OnStepStarted?.Invoke(step);

                Debug.Log($"[GuidanceSequence] Step {currentStepIndex + 1}/{steps.Count}: {step.InstructionText}");
            }
        }

        /// <summary>
        /// 获取当前步骤
        /// </summary>
        private GuidanceStep GetCurrentStep()
        {
            if (currentStepIndex >= 0 && currentStepIndex < steps.Count)
            {
                return steps[currentStepIndex];
            }
            return null;
        }

        /// <summary>
        /// 处理步骤完成
        /// </summary>
        private void HandleStepCompleted()
        {
            var completedStep = GetCurrentStep();
            if (completedStep != null)
            {
                OnStepCompleted?.Invoke(completedStep);
            }
        }

        /// <summary>
        /// 获取下一步骤（预览）
        /// </summary>
        public GuidanceStep GetNextStep()
        {
            int nextIndex = currentStepIndex + 1;
            if (nextIndex >= 0 && nextIndex < steps.Count)
            {
                return steps[nextIndex];
            }
            return null;
        }

        /// <summary>
        /// 获取前一步骤
        /// </summary>
        public GuidanceStep GetPreviousStep()
        {
            int prevIndex = currentStepIndex - 1;
            if (prevIndex >= 0 && prevIndex < steps.Count)
            {
                return steps[prevIndex];
            }
            return null;
        }

        /// <summary>
        /// 检查是否可以前进
        /// </summary>
        public bool CanAdvance()
        {
            return IsActive && currentStepIndex < steps.Count - 1;
        }

        /// <summary>
        /// 检查是否可以后退
        /// </summary>
        public bool CanGoBack()
        {
            return IsActive && currentStepIndex > 0;
        }

        /// <summary>
        /// 获取步骤数量
        /// </summary>
        public int GetStepCount()
        {
            return steps?.Count ?? 0;
        }

        /// <summary>
        /// 清空所有步骤
        /// </summary>
        public void ClearSteps()
        {
            if (steps != null)
            {
                foreach (var step in steps)
                {
                    step.OnStepCompleted -= HandleStepCompleted;
                }
                steps.Clear();
            }

            currentStepIndex = -1;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 验证序列配置
        /// </summary>
        public bool ValidateSequence()
        {
            if (string.IsNullOrEmpty(sequenceID))
            {
                Debug.LogWarning("[GuidanceSequence] Sequence has no ID");
                return false;
            }

            if (steps == null || steps.Count == 0)
            {
                Debug.LogWarning($"[GuidanceSequence] {sequenceName} has no steps");
                return false;
            }

            bool allValid = true;
            for (int i = 0; i < steps.Count; i++)
            {
                if (!steps[i].Validate())
                {
                    Debug.LogWarning($"[GuidanceSequence] Step {i + 1} validation failed in {sequenceName}");
                    allValid = false;
                }
            }

            return allValid;
        }
#endif
    }
}