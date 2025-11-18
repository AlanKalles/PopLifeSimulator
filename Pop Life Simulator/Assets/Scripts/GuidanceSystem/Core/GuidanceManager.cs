using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PopLife.Manager;

namespace PopLife.GuidanceSystem
{
    /// <summary>
    /// 指引管理器，控制所有教程指引流程
    /// Guidance manager controlling all tutorial guidance flows
    /// </summary>
    public class GuidanceManager : MonoBehaviour
    {
        public static GuidanceManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private bool enableGuidance = true;
        [SerializeField] private bool debugMode = false;
        [SerializeField] private float defaultTransitionDelay = 0.3f;

        [Header("UI References")]
        [SerializeField] private UI.GuidanceMask guidanceMask;         // 遮罩组件
        [SerializeField] private UI.InstructionBox instructionBox;     // 指示文本框
        [SerializeField] private UI.HighlightZone highlightZone;       // 高亮区域

        [Header("Guidance Database")]
        [SerializeField] private List<GuidanceSequence> availableSequences;  // 可用序列

        // Runtime state
        private GuidanceSequence activeSequence;
        private Queue<GuidanceSequence> sequenceQueue;
        private bool isGuidanceActive;
        private Coroutine transitionCoroutine;

        // Events
        public event Action<GuidanceSequence> OnGuidanceStarted;
        public event Action<GuidanceSequence> OnGuidanceCompleted;
        public event Action<GuidanceStep> OnStepChanged;

        private void Awake()
        {
            // Singleton setup
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Initialize()
        {
            sequenceQueue = new Queue<GuidanceSequence>();
            LoadAvailableSequences();

            // Create UI components if not assigned
            if (guidanceMask == null)
            {
                CreateGuidanceUI();
            }
        }

        private void OnEnable()
        {
            // Subscribe to tutorial markers
            TutorialEventBus.OnMarkerTriggered += OnTutorialMarkerTriggered;
        }

        private void OnDisable()
        {
            TutorialEventBus.OnMarkerTriggered -= OnTutorialMarkerTriggered;
        }

        private void Update()
        {
            if (!isGuidanceActive || activeSequence == null) return;

            // Check current step completion
            var currentStep = activeSequence.CurrentStep;
            if (currentStep != null && currentStep.CheckCompletion())
            {
                AdvanceStep();
            }

            // Handle skip input
            if (activeSequence.AllowSkip && Input.GetKeyDown(KeyCode.Escape))
            {
                SkipCurrentGuidance();
            }
        }

        /// <summary>
        /// 加载所有可用的指引序列
        /// </summary>
        private void LoadAvailableSequences()
        {
            // In a real implementation, load from ScriptableObjects or JSON
            availableSequences = new List<GuidanceSequence>();

            // Create sample sequences for testing
            CreateSampleSequences();

            Debug.Log($"[GuidanceManager] Loaded {availableSequences.Count} sequences");
        }

        /// <summary>
        /// 创建示例序列（测试用）
        /// </summary>
        private void CreateSampleSequences()
        {
            // Build mode tutorial
            var buildSequence = new GuidanceSequence("GUIDE_001", "Build Mode Tutorial");
            buildSequence.AddStep(new GuidanceStep(
                "STEP_001",
                "Welcome to the build mode! Click the shelf button to select a shelf.",
                ActionType.ClickTarget
            ));
            buildSequence.AddStep(new GuidanceStep(
                "STEP_002",
                "Now click on an empty space to place the shelf.",
                ActionType.ClickTarget
            ));
            buildSequence.AddStep(new GuidanceStep(
                "STEP_003",
                "Great! You've placed your first shelf. Click anywhere to continue.",
                ActionType.ClickAnywhere
            ));

            availableSequences.Add(buildSequence);
        }

        /// <summary>
        /// 开始指引序列（通过ID）
        /// Start guidance sequence by ID
        /// </summary>
        public bool StartGuidanceByID(string sequenceID)
        {
            if (!enableGuidance)
            {
                Debug.Log("[GuidanceManager] Guidance is disabled");
                return false;
            }

            // 尝试从工厂创建序列
            // Try to create sequence from factory
            var sequence = GuidanceSystem.Utility.GuidanceSequenceFactory.CreateSequence(sequenceID);
            if (sequence != null)
            {
                return StartGuidance(sequence);
            }

            // 如果工厂没有，尝试从已加载的序列中查找
            // If not in factory, try to find in loaded sequences
            var loadedSequence = availableSequences.Find(s => s.SequenceID == sequenceID);
            if (loadedSequence != null)
            {
                return StartGuidance(loadedSequence);
            }

            Debug.LogWarning($"[GuidanceManager] Sequence not found: {sequenceID}");
            return false;
        }

        /// <summary>
        /// 开始指引序列
        /// </summary>
        public bool StartGuidance(string sequenceID)
        {
            if (!enableGuidance)
            {
                Debug.Log("[GuidanceManager] Guidance is disabled");
                return false;
            }

            if (isGuidanceActive)
            {
                Debug.Log("[GuidanceManager] Another guidance is already active");
                QueueGuidance(sequenceID);
                return false;
            }

            // Find sequence by ID
            var sequence = availableSequences.Find(s => s.SequenceID == sequenceID);
            if (sequence == null)
            {
                Debug.LogError($"[GuidanceManager] Sequence not found: {sequenceID}");
                return false;
            }

            return StartGuidance(sequence);
        }

        /// <summary>
        /// 开始指引序列（使用序列对象）
        /// </summary>
        public bool StartGuidance(GuidanceSequence sequence)
        {
            if (sequence == null)
            {
                Debug.LogError("[GuidanceManager] Cannot start null sequence");
                return false;
            }

            if (!enableGuidance)
            {
                Debug.Log("[GuidanceManager] Guidance is disabled");
                return false;
            }

            if (isGuidanceActive)
            {
                sequenceQueue.Enqueue(sequence);
                Debug.Log($"[GuidanceManager] Queued sequence: {sequence.SequenceName}");
                return false;
            }

            // Validate sequence
            if (!sequence.ValidateSequence())
            {
                Debug.LogError($"[GuidanceManager] Sequence validation failed: {sequence.SequenceName}");
                return false;
            }

            // Setup
            activeSequence = sequence;
            isGuidanceActive = true;

            // Subscribe to sequence events
            sequence.OnStepStarted += HandleStepStarted;
            sequence.OnStepCompleted += HandleStepCompleted;
            sequence.OnSequenceCompleted += HandleSequenceCompleted;
            sequence.OnSequenceSkipped += HandleSequenceSkipped;

            // Pause game if needed
            if (sequence.PauseGameDuringGuidance)
            {
                PauseGame(true);
            }

            // Start sequence
            if (sequence.StartSequence())
            {
                OnGuidanceStarted?.Invoke(sequence);
                Debug.Log($"[GuidanceManager] Started guidance: {sequence.SequenceName}");
                return true;
            }
            else
            {
                // Failed to start
                CleanupCurrentGuidance();
                return false;
            }
        }

        /// <summary>
        /// 结束当前指引
        /// </summary>
        public void EndCurrentGuidance()
        {
            if (!isGuidanceActive || activeSequence == null)
            {
                Debug.Log("[GuidanceManager] No active guidance to end");
                return;
            }

            var completedSequence = activeSequence;

            // Cleanup
            CleanupCurrentGuidance();

            // Notify
            OnGuidanceCompleted?.Invoke(completedSequence);

            Debug.Log($"[GuidanceManager] Ended guidance: {completedSequence.SequenceName}");

            // Process queue
            ProcessSequenceQueue();
        }

        /// <summary>
        /// 前进到下一步
        /// </summary>
        public void AdvanceStep()
        {
            if (!isGuidanceActive || activeSequence == null) return;

            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }

            transitionCoroutine = StartCoroutine(TransitionToNextStep());
        }

        /// <summary>
        /// 跳过当前指引
        /// </summary>
        public void SkipCurrentGuidance()
        {
            if (!isGuidanceActive || activeSequence == null) return;

            activeSequence.SkipSequence();
        }

        /// <summary>
        /// 过渡到下一步
        /// </summary>
        private IEnumerator TransitionToNextStep()
        {
            // Hide current UI
            if (guidanceMask != null)
            {
                guidanceMask.FadeOut(0.2f);
            }

            if (instructionBox != null)
            {
                instructionBox.Hide();
            }

            // Wait for transition
            yield return new WaitForSeconds(activeSequence?.StepTransitionDelay ?? defaultTransitionDelay);

            // Advance to next step
            if (activeSequence != null && activeSequence.AdvanceStep())
            {
                // Continue with next step (UI will be updated in HandleStepStarted)
            }
            else
            {
                // Sequence completed or failed to advance
                EndCurrentGuidance();
            }

            transitionCoroutine = null;
        }

        /// <summary>
        /// 处理步骤开始
        /// </summary>
        private void HandleStepStarted(GuidanceStep step)
        {
            if (step == null) return;

            OnStepChanged?.Invoke(step);

            // Update mask
            if (guidanceMask != null)
            {
                var targetRect = step.GetTargetScreenRect();
                if (targetRect != Rect.zero)
                {
                    guidanceMask.CreateCutout(targetRect, step.HighlightShape);
                }
                else if (step.DimBackground)
                {
                    guidanceMask.ShowFullMask(step.DimOpacity);
                }

                guidanceMask.FadeIn(0.3f);
            }

            // Update instruction box
            if (instructionBox != null)
            {
                instructionBox.SetText(step.InstructionText);
                instructionBox.SetPosition(step.TextBoxPosition);
                instructionBox.Show();
            }

            // Update highlight zone
            if (highlightZone != null && step.ShowArrow)
            {
                var targetRect = step.GetTargetScreenRect();
                if (targetRect != Rect.zero)
                {
                    highlightZone.ShowHighlight(targetRect, step.ArrowDirection);
                }
            }
        }

        /// <summary>
        /// 处理步骤完成
        /// </summary>
        private void HandleStepCompleted(GuidanceStep step)
        {
            Debug.Log($"[GuidanceManager] Step completed: {step.StepName ?? step.StepID}");

            // Hide highlight
            if (highlightZone != null)
            {
                highlightZone.HideHighlight();
            }
        }

        /// <summary>
        /// 处理序列完成
        /// </summary>
        private void HandleSequenceCompleted()
        {
            EndCurrentGuidance();
        }

        /// <summary>
        /// 处理序列跳过
        /// </summary>
        private void HandleSequenceSkipped()
        {
            Debug.Log($"[GuidanceManager] Sequence skipped: {activeSequence?.SequenceName}");
            EndCurrentGuidance();
        }

        /// <summary>
        /// 清理当前指引
        /// </summary>
        private void CleanupCurrentGuidance()
        {
            if (activeSequence != null)
            {
                // Unsubscribe events
                activeSequence.OnStepStarted -= HandleStepStarted;
                activeSequence.OnStepCompleted -= HandleStepCompleted;
                activeSequence.OnSequenceCompleted -= HandleSequenceCompleted;
                activeSequence.OnSequenceSkipped -= HandleSequenceSkipped;

                // Resume game if was paused
                if (activeSequence.PauseGameDuringGuidance)
                {
                    PauseGame(false);
                }
            }

            // Hide UI
            if (guidanceMask != null)
            {
                guidanceMask.HideImmediate();
            }

            if (instructionBox != null)
            {
                instructionBox.Hide();
            }

            if (highlightZone != null)
            {
                highlightZone.HideHighlight();
            }

            // Reset state
            activeSequence = null;
            isGuidanceActive = false;

            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
                transitionCoroutine = null;
            }
        }

        /// <summary>
        /// 将指引加入队列
        /// </summary>
        private void QueueGuidance(string sequenceID)
        {
            var sequence = availableSequences.Find(s => s.SequenceID == sequenceID);
            if (sequence != null)
            {
                sequenceQueue.Enqueue(sequence);
                Debug.Log($"[GuidanceManager] Queued sequence: {sequence.SequenceName}");
            }
        }

        /// <summary>
        /// 处理序列队列
        /// </summary>
        private void ProcessSequenceQueue()
        {
            if (sequenceQueue.Count > 0 && !isGuidanceActive)
            {
                var nextSequence = sequenceQueue.Dequeue();
                StartGuidance(nextSequence);
            }
        }

        /// <summary>
        /// 暂停/恢复游戏
        /// </summary>
        private void PauseGame(bool pause)
        {
            // Simple time scale pause
            // In production, might want more sophisticated pause system
            Time.timeScale = pause ? 0f : 1f;

            if (debugMode)
            {
                Debug.Log($"[GuidanceManager] Game {(pause ? "paused" : "resumed")}");
            }
        }

        /// <summary>
        /// 处理教程标记触发
        /// </summary>
        private void OnTutorialMarkerTriggered(TutorialMarker marker)
        {
            // Map markers to guidance sequences
            // This will be handled by TutorialContentDispatcher
        }

        /// <summary>
        /// 创建指引UI（如果未分配）
        /// </summary>
        private void CreateGuidanceUI()
        {
            // Create canvas if needed
            var canvas = GameObject.Find("GuidanceCanvas");
            if (canvas == null)
            {
                canvas = new GameObject("GuidanceCanvas");
                var canvasComp = canvas.AddComponent<Canvas>();
                canvasComp.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasComp.sortingOrder = 999; // On top of everything
                canvas.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            // Create mask
            if (guidanceMask == null)
            {
                var maskObj = new GameObject("GuidanceMask");
                maskObj.transform.SetParent(canvas.transform, false);
                guidanceMask = maskObj.AddComponent<UI.GuidanceMask>();
            }

            // Create instruction box
            if (instructionBox == null)
            {
                var boxObj = new GameObject("InstructionBox");
                boxObj.transform.SetParent(canvas.transform, false);
                instructionBox = boxObj.AddComponent<UI.InstructionBox>();
            }

            // Create highlight zone
            if (highlightZone == null)
            {
                var zoneObj = new GameObject("HighlightZone");
                zoneObj.transform.SetParent(canvas.transform, false);
                highlightZone = zoneObj.AddComponent<UI.HighlightZone>();
            }
        }

        /// <summary>
        /// 获取当前活跃的序列
        /// </summary>
        public GuidanceSequence GetActiveSequence()
        {
            return activeSequence;
        }

        /// <summary>
        /// 检查是否有活跃的指引
        /// </summary>
        public bool IsGuidanceActive()
        {
            return isGuidanceActive;
        }

        /// <summary>
        /// 启用/禁用指引系统
        /// </summary>
        public void SetGuidanceEnabled(bool enabled)
        {
            enableGuidance = enabled;

            if (!enabled && isGuidanceActive)
            {
                EndCurrentGuidance();
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Test Start First Guidance")]
        private void TestStartFirstGuidance()
        {
            if (availableSequences.Count > 0)
            {
                StartGuidance(availableSequences[0]);
            }
        }
#endif
    }
}