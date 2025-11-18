using System;
using UnityEngine;

namespace PopLife.GuidanceSystem
{
    /// <summary>
    /// 指引步骤，代表教程中的单个指引
    /// Guidance step representing a single tutorial instruction
    /// </summary>
    [Serializable]
    public class GuidanceStep
    {
        [Header("Basic Info")]
        [SerializeField] private string stepID;              // 步骤唯一标识
        [SerializeField] private string stepName;            // 步骤名称
        [TextArea(3, 5)]
        [SerializeField] private string instructionText;     // 指示文本

        [Header("Target Settings")]
        [SerializeField] private ActionType actionType;      // 动作类型
        [SerializeField] private string targetElementID;     // 目标UI元素ID
        [SerializeField] private RectTransform targetRect;   // 目标区域（UI空间）
        [SerializeField] private Transform targetObject;     // 目标对象（世界空间）
        [SerializeField] private Vector2 targetScreenPosition; // 屏幕位置（像素）

        [Header("Highlight Settings")]
        [SerializeField] private HighlightShape highlightShape = HighlightShape.Rectangle;
        [SerializeField] private Vector2 highlightSize = new Vector2(200, 100);
        [SerializeField] private float highlightPadding = 10f;
        [SerializeField] private bool showArrow = true;
        [SerializeField] private ArrowDirection arrowDirection = ArrowDirection.Auto;

        [Header("Completion Settings")]
        [SerializeField] private CompletionType completionType;
        [SerializeField] private float autoCompleteDelay = 3f;
        [SerializeField] private KeyCode completionKey = KeyCode.None;

        [Header("Visual Settings")]
        [SerializeField] private TextAnchor textBoxPosition = TextAnchor.UpperCenter;
        [SerializeField] private bool dimBackground = true;
        [SerializeField] private float dimOpacity = 0.7f;

        // Properties
        public string StepID => stepID;
        public string StepName => stepName;
        public string InstructionText => instructionText;
        public ActionType ActionType => actionType;
        public HighlightShape HighlightShape => highlightShape;
        public Vector2 HighlightSize => highlightSize;
        public float HighlightPadding => highlightPadding;
        public CompletionType CompletionType => completionType;
        public float AutoCompleteDelay => autoCompleteDelay;
        public TextAnchor TextBoxPosition => textBoxPosition;
        public bool DimBackground => dimBackground;
        public float DimOpacity => dimOpacity;
        public bool ShowArrow => showArrow;
        public ArrowDirection ArrowDirection => arrowDirection;

        // Events
        public event Action OnStepStarted;
        public event Action OnStepCompleted;

        // Runtime state
        private bool isActive;
        private float startTime;

        /// <summary>
        /// 构造函数
        /// </summary>
        public GuidanceStep(string id, string instruction, ActionType action)
        {
            stepID = id;
            instructionText = instruction;
            actionType = action;
            highlightShape = HighlightShape.Rectangle;
            completionType = CompletionType.ClickTarget;
        }

        /// <summary>
        /// 开始步骤
        /// </summary>
        public void StartStep()
        {
            if (isActive) return;

            isActive = true;
            startTime = Time.time;
            OnStepStarted?.Invoke();

            Debug.Log($"[GuidanceStep] Started: {stepName ?? stepID}");
        }

        /// <summary>
        /// 完成步骤
        /// </summary>
        public void CompleteStep()
        {
            if (!isActive) return;

            isActive = false;
            OnStepCompleted?.Invoke();

            Debug.Log($"[GuidanceStep] Completed: {stepName ?? stepID}");
        }

        /// <summary>
        /// 检查是否满足完成条件
        /// </summary>
        public bool CheckCompletion()
        {
            if (!isActive) return false;

            switch (completionType)
            {
                case CompletionType.Automatic:
                    // Check if auto-complete delay has passed
                    return Time.time - startTime >= autoCompleteDelay;

                case CompletionType.ClickAnywhere:
                    // Check for any mouse click
                    return Input.GetMouseButtonDown(0);

                case CompletionType.ClickTarget:
                    // This will be handled by the mask system
                    return false;

                case CompletionType.KeyPress:
                    // Check for specific key press
                    return completionKey != KeyCode.None && Input.GetKeyDown(completionKey);

                case CompletionType.Custom:
                    // Custom completion logic (handled externally)
                    return false;

                default:
                    return false;
            }
        }

        /// <summary>
        /// 获取目标区域的屏幕矩形
        /// </summary>
        public Rect GetTargetScreenRect()
        {
            if (targetRect != null)
            {
                // Convert RectTransform to screen space
                Vector3[] corners = new Vector3[4];
                targetRect.GetWorldCorners(corners);

                if (Camera.main != null)
                {
                    Vector2 min = RectTransformUtility.WorldToScreenPoint(Camera.main, corners[0]);
                    Vector2 max = RectTransformUtility.WorldToScreenPoint(Camera.main, corners[2]);

                    return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
                }
            }
            else if (targetObject != null)
            {
                // Convert world object to screen space
                if (Camera.main != null)
                {
                    Vector3 screenPos = Camera.main.WorldToScreenPoint(targetObject.position);
                    return new Rect(screenPos.x - highlightSize.x / 2,
                                   screenPos.y - highlightSize.y / 2,
                                   highlightSize.x,
                                   highlightSize.y);
                }
            }
            else if (targetScreenPosition != Vector2.zero)
            {
                // Use direct screen position
                return new Rect(targetScreenPosition.x - highlightSize.x / 2,
                               targetScreenPosition.y - highlightSize.y / 2,
                               highlightSize.x,
                               highlightSize.y);
            }

            return Rect.zero;
        }

        /// <summary>
        /// 设置目标UI元素
        /// </summary>
        public void SetTargetUI(RectTransform target)
        {
            targetRect = target;
            targetObject = null;
            targetScreenPosition = Vector2.zero;
        }

        /// <summary>
        /// 设置目标世界对象
        /// </summary>
        public void SetTargetObject(Transform target)
        {
            targetObject = target;
            targetRect = null;
            targetScreenPosition = Vector2.zero;
        }

        /// <summary>
        /// 设置目标屏幕位置
        /// </summary>
        public void SetTargetScreenPosition(Vector2 position)
        {
            targetScreenPosition = position;
            targetRect = null;
            targetObject = null;
        }

        /// <summary>
        /// 验证步骤配置
        /// </summary>
        public bool Validate()
        {
            // Check if has valid instruction
            if (string.IsNullOrEmpty(instructionText))
            {
                Debug.LogWarning($"[GuidanceStep] {stepID} has no instruction text");
                return false;
            }

            // Check if has valid target for click actions
            if (actionType == ActionType.ClickTarget)
            {
                if (targetRect == null && targetObject == null && targetScreenPosition == Vector2.zero)
                {
                    Debug.LogWarning($"[GuidanceStep] {stepID} requires a target but none is set");
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// 动作类型
    /// </summary>
    public enum ActionType
    {
        Information,      // 仅显示信息
        ClickTarget,      // 点击特定目标
        ClickAnywhere,    // 点击任意位置
        DragTarget,       // 拖动目标
        KeyPress,         // 按键
        Wait,            // 等待
        Custom           // 自定义动作
    }

    /// <summary>
    /// 高亮形状
    /// </summary>
    public enum HighlightShape
    {
        Rectangle,       // 矩形
        Circle,         // 圆形
        RoundedRect,    // 圆角矩形
        Custom          // 自定义形状
    }

    /// <summary>
    /// 完成类型
    /// </summary>
    public enum CompletionType
    {
        ClickTarget,     // 点击目标完成
        ClickAnywhere,   // 点击任意位置完成
        Automatic,       // 自动完成（延迟）
        KeyPress,        // 按键完成
        Custom          // 自定义完成条件
    }

    /// <summary>
    /// 箭头方向
    /// </summary>
    public enum ArrowDirection
    {
        Auto,           // 自动计算
        Up,
        Down,
        Left,
        Right,
        UpLeft,
        UpRight,
        DownLeft,
        DownRight
    }
}