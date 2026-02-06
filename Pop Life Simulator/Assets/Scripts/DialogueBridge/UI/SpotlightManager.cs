using System.Collections;
using UnityEngine;
using PrimeTween;
using Sirenix.OdinInspector;
using PixelCrushers.DialogueSystem;

namespace PopLife.DialogueBridge.UI
{
    /// <summary>
    /// Spotlight/Coach Mark effect manager
    /// Highlights specific UI elements or world objects during tutorials
    ///
    /// Supports:
    /// - UI elements (RectTransform)
    /// - World objects (GameObject with Renderer)
    /// - Custom shapes (Rectangle, Circle, RoundedRectangle)
    /// - Pulse animation
    /// - Automatic position tracking
    /// </summary>
    public class SpotlightManager : MonoBehaviour
    {
        #region Singleton

        public static SpotlightManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        #endregion

        #region Serialized Fields

        [Title("References")]
        [Required]
        [SerializeField] private SpotlightPanel spotlightPanel;

        [SerializeField] private Canvas spotlightCanvas;

        [Title("Animation Settings")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.2f;
        [SerializeField] private float pulseScale = 1.08f;
        [SerializeField] private float pulseDuration = 0.8f;
        [SerializeField] private Ease pulseEase = Ease.InOutSine;

        [Title("Target Tracking")]
        [Tooltip("If true, spotlight will follow moving targets")]
        [SerializeField] private bool trackTarget = true;

        [Tooltip("How often to update tracking (seconds)")]
        [SerializeField] private float trackingInterval = 0.1f;

        [Title("Tooltip")]
        [SerializeField]
        private SpotlightTooltip tooltip;

        [SerializeField, Tooltip("Automatically hide dialogue UI when showing tooltip")]
        private bool autoHideDialogueUI = true;

        [SerializeField, Tooltip("Automatically close tooltip when conversation node changes")]
        private bool autoCloseOnNodeChange = true;

        [Title("Debug")]
        [SerializeField] private bool debugMode = false;

        #endregion

        #region Private Fields

        private RectTransform currentUITarget;
        private GameObject currentWorldTarget;
        private Camera mainCamera;
        private Coroutine pulseCoroutine;
        private Coroutine trackingCoroutine;
        private bool isActive = false;

        // Tooltip state
        private bool isTooltipActive = false;
        private GameObject cachedDialogueUI;
        private Rect currentSpotlightRect;

        // Continue trigger
        private ContinueTriggerMode currentTriggerMode = ContinueTriggerMode.ClickAnywhere;
        private UnityEngine.UI.Button targetButton;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            // Subscribe to Dialogue System events
            if (DialogueManager.instance != null)
            {
                DialogueManager.instance.conversationEnded += OnConversationEnded;
                DialogueManager.instance.conversationLinePrepared += OnConversationLinePrepared;
            }
        }

        private void OnDisable()
        {
            // Unsubscribe from Dialogue System events
            if (DialogueManager.instance != null)
            {
                DialogueManager.instance.conversationEnded -= OnConversationEnded;
                DialogueManager.instance.conversationLinePrepared -= OnConversationLinePrepared;
            }
        }

        private void Start()
        {
            mainCamera = Camera.main;

            if (spotlightPanel != null)
            {
                spotlightPanel.gameObject.SetActive(false);
            }

            if (tooltip != null)
            {
                tooltip.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }

        private void Update()
        {
            if (!isTooltipActive) return;

            switch (currentTriggerMode)
            {
                case ContinueTriggerMode.ClickAnywhere:
                    if (Input.GetMouseButtonDown(0))
                    {
                        ContinueDialogue();
                    }
                    break;

                case ContinueTriggerMode.ClickSpotlight:
                    if (Input.GetMouseButtonDown(0) && IsClickInsideSpotlight())
                    {
                        ContinueDialogue();
                    }
                    break;

                case ContinueTriggerMode.ClickButton:
                    // Button 点击由事件处理，这里不需要额外逻辑
                    break;
            }
        }

        #endregion

        #region Public API - UI Elements

        /// <summary>
        /// Show spotlight on a UI element
        /// </summary>
        public void ShowSpotlight(RectTransform target, SpotlightShape shape = SpotlightShape.RoundedRectangle)
        {
            if (target == null)
            {
                Debug.LogWarning("[SpotlightManager] ShowSpotlight called with null target");
                return;
            }

            currentUITarget = target;
            currentWorldTarget = null;

            ShowSpotlightInternal(shape);

            if (trackTarget)
            {
                StartTracking();
            }

            if (debugMode)
            {
                Debug.Log($"[SpotlightManager] Showing spotlight on UI: {target.name}");
            }
        }

        /// <summary>
        /// Show spotlight on a UI element by name
        /// Searches entire hierarchy
        /// </summary>
        public void ShowSpotlightByName(string targetName, SpotlightShape shape = SpotlightShape.RoundedRectangle)
        {
            if (string.IsNullOrEmpty(targetName))
            {
                Debug.LogWarning("[SpotlightManager] ShowSpotlightByName called with empty name");
                return;
            }

            var target = GameObject.Find(targetName);
            if (target != null)
            {
                var rectTransform = target.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    ShowSpotlight(rectTransform, shape);
                    return;
                }
            }

            Debug.LogWarning($"[SpotlightManager] UI target not found: {targetName}");
        }

        #endregion

        #region Public API - World Objects

        /// <summary>
        /// Show spotlight on a world object (3D/2D)
        /// </summary>
        public void ShowSpotlightOnWorldObject(GameObject target, SpotlightShape shape = SpotlightShape.RoundedRectangle)
        {
            if (target == null)
            {
                Debug.LogWarning("[SpotlightManager] ShowSpotlightOnWorldObject called with null target");
                return;
            }

            currentUITarget = null;
            currentWorldTarget = target;

            ShowSpotlightInternal(shape);

            if (trackTarget)
            {
                StartTracking();
            }

            if (debugMode)
            {
                Debug.Log($"[SpotlightManager] Showing spotlight on world object: {target.name}");
            }
        }

        /// <summary>
        /// Show spotlight on a world object by name
        /// </summary>
        public void ShowSpotlightOnWorldObjectByName(string targetName, SpotlightShape shape = SpotlightShape.RoundedRectangle)
        {
            if (string.IsNullOrEmpty(targetName))
            {
                Debug.LogWarning("[SpotlightManager] ShowSpotlightOnWorldObjectByName called with empty name");
                return;
            }

            var target = GameObject.Find(targetName);
            if (target != null)
            {
                ShowSpotlightOnWorldObject(target, shape);
                return;
            }

            Debug.LogWarning($"[SpotlightManager] World object not found: {targetName}");
        }

        #endregion

        #region Public API - Direct Rect

        /// <summary>
        /// Show spotlight on a specific screen rect
        /// </summary>
        /// <param name="screenRect">Target rect in screen coordinates (pixels)</param>
        /// <param name="shape">Shape of the spotlight cutout</param>
        public void ShowSpotlightRect(Rect screenRect, SpotlightShape shape = SpotlightShape.RoundedRectangle)
        {
            currentUITarget = null;
            currentWorldTarget = null;

            if (spotlightPanel == null)
            {
                Debug.LogError("[SpotlightManager] SpotlightPanel is not assigned!");
                return;
            }

            isActive = true;
            spotlightPanel.gameObject.SetActive(true);

            // Save current rect for tooltip positioning
            currentSpotlightRect = screenRect;

            // Set target directly
            spotlightPanel.SetTarget(screenRect, shape);

            // Fade in
            var cg = spotlightPanel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0f;
                Tween.Alpha(cg, 1f, fadeInDuration, useUnscaledTime: true);
            }

            // Start pulse animation
            StartPulse();

            if (debugMode)
            {
                Debug.Log($"[SpotlightManager] Showing spotlight on rect: {screenRect}");
            }
        }

        /// <summary>
        /// Show spotlight on a specific screen rect (convenience overload)
        /// </summary>
        public void ShowSpotlightRect(float x, float y, float width, float height, SpotlightShape shape = SpotlightShape.RoundedRectangle)
        {
            ShowSpotlightRect(new Rect(x, y, width, height), shape);
        }

        #endregion

        #region Public API - General

        /// <summary>
        /// Hide the spotlight
        /// </summary>
        [Button("Hide Spotlight")]
        public void HideSpotlight()
        {
            if (!isActive) return;

            StopTracking();
            StopPulse();

            isActive = false;
            currentUITarget = null;
            currentWorldTarget = null;

            // Fade out
            var cg = spotlightPanel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                Tween.Alpha(cg, 0f, fadeOutDuration, useUnscaledTime: true)
                    .OnComplete(() => {
                        spotlightPanel.gameObject.SetActive(false);
                    });
            }
            else
            {
                spotlightPanel.gameObject.SetActive(false);
            }

            if (debugMode)
            {
                Debug.Log("[SpotlightManager] Spotlight hidden");
            }
        }

        /// <summary>
        /// Check if spotlight is currently active
        /// </summary>
        public bool IsActive => isActive;

        /// <summary>
        /// Check if tooltip is currently active
        /// </summary>
        public bool IsTooltipActive => isTooltipActive;

        #endregion

        #region Public API - Tooltip

        /// <summary>
        /// Show tooltip with content from current dialogue node
        /// </summary>
        /// <param name="position">Position relative to spotlight</param>
        /// <param name="customOffset">Custom offset for Custom position mode (normalized 0-1)</param>
        /// <param name="triggerMode">How to trigger continue dialogue</param>
        public void ShowTooltipFromDialogue(TooltipPosition position, Vector2? customOffset = null,
            ContinueTriggerMode triggerMode = ContinueTriggerMode.ClickAnywhere)
        {
            if (tooltip == null)
            {
                Debug.LogError("[SpotlightManager] SpotlightTooltip is not assigned!");
                return;
            }

            // Get current dialogue text
            var state = DialogueManager.currentConversationState;
            string text = state?.subtitle?.formattedText?.text ?? "";

            if (string.IsNullOrEmpty(text))
            {
                Debug.LogWarning("[SpotlightManager] No dialogue text available for tooltip");
                return;
            }

            // Auto-hide dialogue UI if enabled and conversation is active
            if (autoHideDialogueUI && DialogueManager.isConversationActive)
            {
                HideDialogueUI();
            }

            // Ensure tooltip is under the spotlight canvas
            EnsureTooltipUnderSpotlightCanvas();

            // Set trigger mode
            currentTriggerMode = triggerMode;

            // Setup button trigger if needed
            if (triggerMode == ContinueTriggerMode.ClickButton)
            {
                SetupButtonTrigger();
            }

            // Show tooltip
            isTooltipActive = true;
            tooltip.Show(text, currentSpotlightRect, position, customOffset);

            if (debugMode)
            {
                Debug.Log($"[SpotlightManager] Showing tooltip: {position}, trigger: {triggerMode}, text: {text.Substring(0, Mathf.Min(50, text.Length))}...");
            }
        }

        /// <summary>
        /// Show tooltip with custom text
        /// </summary>
        /// <param name="text">Text content to display</param>
        /// <param name="position">Position relative to spotlight</param>
        /// <param name="customOffset">Custom offset for Custom position mode (normalized 0-1)</param>
        /// <param name="triggerMode">How to trigger continue dialogue</param>
        public void ShowTooltip(string text, TooltipPosition position, Vector2? customOffset = null,
            ContinueTriggerMode triggerMode = ContinueTriggerMode.ClickAnywhere)
        {
            if (tooltip == null)
            {
                Debug.LogError("[SpotlightManager] SpotlightTooltip is not assigned!");
                return;
            }

            if (string.IsNullOrEmpty(text))
            {
                Debug.LogWarning("[SpotlightManager] ShowTooltip called with empty text");
                return;
            }

            // Auto-hide dialogue UI if enabled and conversation is active
            if (autoHideDialogueUI && DialogueManager.isConversationActive)
            {
                HideDialogueUI();
            }

            // Ensure tooltip is under the spotlight canvas
            EnsureTooltipUnderSpotlightCanvas();

            // Set trigger mode
            currentTriggerMode = triggerMode;

            // Setup button trigger if needed
            if (triggerMode == ContinueTriggerMode.ClickButton)
            {
                SetupButtonTrigger();
            }

            // Show tooltip
            isTooltipActive = true;
            tooltip.Show(text, currentSpotlightRect, position, customOffset);

            if (debugMode)
            {
                Debug.Log($"[SpotlightManager] Showing tooltip: {position}, trigger: {triggerMode}");
            }
        }

        /// <summary>
        /// Hide the tooltip and restore dialogue UI
        /// </summary>
        [Button("Hide Tooltip")]
        public void HideTooltip()
        {
            if (!isTooltipActive) return;

            isTooltipActive = false;

            // Cleanup button trigger
            CleanupButtonTrigger();

            // Reset trigger mode
            currentTriggerMode = ContinueTriggerMode.ClickAnywhere;

            if (tooltip != null)
            {
                tooltip.Hide();
            }

            // Restore dialogue UI only if conversation is still active
            if (autoHideDialogueUI && DialogueManager.isConversationActive)
            {
                ShowDialogueUI();
            }

            if (debugMode)
            {
                Debug.Log("[SpotlightManager] Tooltip hidden");
            }
        }

        /// <summary>
        /// Get the current spotlight screen rect (for tooltip positioning)
        /// </summary>
        public Rect GetCurrentSpotlightRect()
        {
            return currentSpotlightRect;
        }

        /// <summary>
        /// Ensure tooltip is parented under the spotlight canvas for correct rendering
        /// </summary>
        private void EnsureTooltipUnderSpotlightCanvas()
        {
            if (tooltip == null || spotlightCanvas == null) return;

            // Check if tooltip is already under spotlight canvas
            if (tooltip.transform.parent == spotlightCanvas.transform) return;

            // Reparent tooltip to spotlight canvas
            tooltip.transform.SetParent(spotlightCanvas.transform, false);

            // Set as last sibling to render on top
            tooltip.transform.SetAsLastSibling();

            if (debugMode)
            {
                Debug.Log("[SpotlightManager] Tooltip reparented to spotlight canvas");
            }
        }

        #endregion

        #region Dialogue UI Control

        private void HideDialogueUI()
        {
            // Find and cache the Dialogue System UI
            if (cachedDialogueUI == null)
            {
                // displaySettings.dialogueUI is a GameObject reference
                cachedDialogueUI = DialogueManager.displaySettings?.dialogueUI;
            }

            if (cachedDialogueUI != null)
            {
                cachedDialogueUI.SetActive(false);

                if (debugMode)
                {
                    Debug.Log("[SpotlightManager] Dialogue UI hidden");
                }
            }
        }

        private void ShowDialogueUI()
        {
            if (cachedDialogueUI != null)
            {
                cachedDialogueUI.SetActive(true);

                if (debugMode)
                {
                    Debug.Log("[SpotlightManager] Dialogue UI restored");
                }
            }
        }

        #endregion

        #region Dialogue Continue

        /// <summary>
        /// 继续对话 - 模拟点击 continue button
        /// </summary>
        private void ContinueDialogue()
        {
            if (!DialogueManager.isConversationActive) return;

            // 必须先恢复 DialogueUI，否则 OnContinue 内部的 Coroutine 无法启动
            if (autoHideDialogueUI)
            {
                ShowDialogueUI();
            }

            // 调用 Dialogue System 的 OnContinue 来继续对话
            if (DialogueManager.standardDialogueUI != null)
            {
                DialogueManager.standardDialogueUI.OnContinue();

                if (debugMode)
                {
                    Debug.Log("[SpotlightManager] Dialogue continued via click");
                }
            }
        }

        /// <summary>
        /// 检测点击是否在 spotlight 区域内
        /// </summary>
        private bool IsClickInsideSpotlight()
        {
            Vector2 mousePos = Input.mousePosition;
            return currentSpotlightRect.Contains(mousePos);
        }

        /// <summary>
        /// 设置 Button 触发器
        /// </summary>
        private void SetupButtonTrigger()
        {
            // 清理旧的订阅
            CleanupButtonTrigger();

            // 从 currentUITarget 获取 Button 组件
            if (currentUITarget != null)
            {
                targetButton = currentUITarget.GetComponent<UnityEngine.UI.Button>();
                if (targetButton != null)
                {
                    targetButton.onClick.AddListener(OnTargetButtonClicked);

                    if (debugMode)
                    {
                        Debug.Log($"[SpotlightManager] Button trigger setup on: {targetButton.name}");
                    }
                }
                else if (debugMode)
                {
                    Debug.LogWarning("[SpotlightManager] ClickButton mode but target has no Button component");
                }
            }
        }

        /// <summary>
        /// 清理 Button 触发器
        /// </summary>
        private void CleanupButtonTrigger()
        {
            if (targetButton != null)
            {
                targetButton.onClick.RemoveListener(OnTargetButtonClicked);
                targetButton = null;
            }
        }

        /// <summary>
        /// Button 点击事件处理（用于 ClickButton 模式）
        /// </summary>
        private void OnTargetButtonClicked()
        {
            if (isTooltipActive && currentTriggerMode == ContinueTriggerMode.ClickButton)
            {
                ContinueDialogue();
            }
        }

        #endregion

        #region Dialogue System Event Handlers

        /// <summary>
        /// Called when conversation node changes - auto close tooltip and spotlight
        /// </summary>
        private void OnConversationLinePrepared(Subtitle subtitle)
        {
            if (autoCloseOnNodeChange && (isTooltipActive || isActive))
            {
                HideTooltip();
                HideSpotlight();

                if (debugMode)
                {
                    Debug.Log("[SpotlightManager] Tooltip and spotlight auto-closed on node change");
                }
            }
        }

        /// <summary>
        /// Called when conversation ends - cleanup spotlight and tooltip
        /// </summary>
        private void OnConversationEnded(Transform actor)
        {
            if (isTooltipActive || isActive)
            {
                HideTooltip();
                HideSpotlight();

                if (debugMode)
                {
                    Debug.Log("[SpotlightManager] Spotlight and tooltip auto-closed on conversation end");
                }
            }
        }

        #endregion

        #region Internal Methods

        private void ShowSpotlightInternal(SpotlightShape shape)
        {
            if (spotlightPanel == null)
            {
                Debug.LogError("[SpotlightManager] SpotlightPanel is not assigned!");
                return;
            }

            isActive = true;
            spotlightPanel.gameObject.SetActive(true);

            // Update spotlight position
            UpdateSpotlightPosition(shape);

            // Fade in
            var cg = spotlightPanel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0f;
                Tween.Alpha(cg, 1f, fadeInDuration, useUnscaledTime: true);
            }

            // Start pulse animation
            StartPulse();
        }

        private void UpdateSpotlightPosition(SpotlightShape shape)
        {
            if (spotlightPanel == null) return;

            Rect screenRect;

            if (currentUITarget != null)
            {
                screenRect = GetUIElementScreenRect(currentUITarget);
            }
            else if (currentWorldTarget != null)
            {
                screenRect = GetWorldObjectScreenRect(currentWorldTarget);
            }
            else
            {
                return;
            }

            // Save current rect for tooltip positioning
            currentSpotlightRect = screenRect;

            spotlightPanel.SetTarget(screenRect, shape);
        }

        /// <summary>
        /// Get screen rect for UI element
        /// </summary>
        private Rect GetUIElementScreenRect(RectTransform rectTransform)
        {
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);

            // Convert to screen coordinates
            Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
            Camera cam = canvas?.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas?.worldCamera ?? mainCamera;

            Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);

            return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        }

        /// <summary>
        /// Get screen rect for world object
        /// </summary>
        private Rect GetWorldObjectScreenRect(GameObject target)
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            // Try to get bounds from renderer
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                Bounds bounds = renderer.bounds;

                // Get all 8 corners of the bounding box
                Vector3[] worldCorners = new Vector3[8];
                worldCorners[0] = new Vector3(bounds.min.x, bounds.min.y, bounds.min.z);
                worldCorners[1] = new Vector3(bounds.max.x, bounds.min.y, bounds.min.z);
                worldCorners[2] = new Vector3(bounds.min.x, bounds.max.y, bounds.min.z);
                worldCorners[3] = new Vector3(bounds.max.x, bounds.max.y, bounds.min.z);
                worldCorners[4] = new Vector3(bounds.min.x, bounds.min.y, bounds.max.z);
                worldCorners[5] = new Vector3(bounds.max.x, bounds.min.y, bounds.max.z);
                worldCorners[6] = new Vector3(bounds.min.x, bounds.max.y, bounds.max.z);
                worldCorners[7] = new Vector3(bounds.max.x, bounds.max.y, bounds.max.z);

                // Convert to screen space and find min/max
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
                Vector2 max = new Vector2(float.MinValue, float.MinValue);

                foreach (var corner in worldCorners)
                {
                    Vector2 screenPoint = mainCamera.WorldToScreenPoint(corner);
                    min = Vector2.Min(min, screenPoint);
                    max = Vector2.Max(max, screenPoint);
                }

                return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
            }

            // Fallback: use transform position
            Vector2 center = mainCamera.WorldToScreenPoint(target.transform.position);
            float size = 100f; // Default size
            return new Rect(center.x - size / 2, center.y - size / 2, size, size);
        }

        #endregion

        #region Animation

        private void StartPulse()
        {
            if (pulseCoroutine != null)
            {
                StopCoroutine(pulseCoroutine);
            }
            pulseCoroutine = StartCoroutine(PulseAnimation());
        }

        private void StopPulse()
        {
            if (pulseCoroutine != null)
            {
                StopCoroutine(pulseCoroutine);
                pulseCoroutine = null;
            }
        }

        private IEnumerator PulseAnimation()
        {
            if (spotlightPanel.HighlightBorder == null) yield break;

            var border = spotlightPanel.HighlightBorder;

            while (isActive)
            {
                // Scale up
                yield return Tween.Scale(border, Vector3.one * pulseScale, pulseDuration / 2, pulseEase, useUnscaledTime: true)
                    .ToYieldInstruction();

                // Scale down
                yield return Tween.Scale(border, Vector3.one, pulseDuration / 2, pulseEase, useUnscaledTime: true)
                    .ToYieldInstruction();
            }
        }

        #endregion

        #region Target Tracking

        private void StartTracking()
        {
            if (trackingCoroutine != null)
            {
                StopCoroutine(trackingCoroutine);
            }
            trackingCoroutine = StartCoroutine(TrackTargetCoroutine());
        }

        private void StopTracking()
        {
            if (trackingCoroutine != null)
            {
                StopCoroutine(trackingCoroutine);
                trackingCoroutine = null;
            }
        }

        private IEnumerator TrackTargetCoroutine()
        {
            var wait = new WaitForSecondsRealtime(trackingInterval);

            while (isActive && (currentUITarget != null || currentWorldTarget != null))
            {
                UpdateSpotlightPosition(spotlightPanel.CurrentShape);
                yield return wait;
            }
        }

        #endregion

        #region Editor Helpers

#if UNITY_EDITOR
        [Button("Test UI Spotlight")]
        private void TestUISpotlight()
        {
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                var firstButton = canvas.GetComponentInChildren<UnityEngine.UI.Button>();
                if (firstButton != null)
                {
                    ShowSpotlight(firstButton.GetComponent<RectTransform>());
                }
            }
        }
#endif

        #endregion
    }

    /// <summary>
    /// Spotlight shape type
    /// </summary>
    public enum SpotlightShape
    {
        Rectangle,
        Circle,
        RoundedRectangle
    }

    /// <summary>
    /// Tooltip 模式下继续对话的触发方式
    /// </summary>
    public enum ContinueTriggerMode
    {
        ClickAnywhere,    // 点击屏幕任意位置
        ClickSpotlight,   // 点击 spotlight 高亮区域
        ClickButton       // 点击目标 Button
    }
}
