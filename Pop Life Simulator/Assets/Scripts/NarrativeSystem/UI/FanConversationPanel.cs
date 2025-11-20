using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using PrimeTween;

namespace PopLife.NarrativeSystem.UI
{
    /// <summary>
    /// 扇形对话面板，管理三层对话框的显示和交互
    /// Fan conversation panel managing three-tier conversation box display and interaction
    /// </summary>
    public class FanConversationPanel : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Panel References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Conversation Box Prefab")]
        [SerializeField] private ConversationBox conversationBoxPrefab;  // 通用对话框预制体

        [Header("Fan Layout Settings")]
        [SerializeField] private float fanAngle = 25f;       // 扇形角度
        [SerializeField] private float fanRadius = 350f;     // 扇形半径（现在会正确使用）
        [SerializeField] private Vector2 fanPivot;           // 扇形圆心位置

        [Header("Box Sizes")]
        [SerializeField] private Vector2 centerBoxSize = new Vector2(600f, 150f);
        [SerializeField] private Vector2 edgeBoxSize = new Vector2(450f, 100f);
        [SerializeField] private float edgeBoxScale = 0.8f;  // 边缘框缩放

        [Header("Character Portrait")]
        [SerializeField] private Image characterPortrait;
        [SerializeField] private TextMeshProUGUI characterName;

        [Header("Finish Button")]
        [SerializeField] private Button finishButton;
        [SerializeField] private TextMeshProUGUI finishButtonText;

        [Header("Choice Buttons")]
        [SerializeField] private Button choiceButton1;
        [SerializeField] private Button choiceButton2;
        [SerializeField] private Button choiceButton3;
        [SerializeField] private TextMeshProUGUI choiceButtonText1;
        [SerializeField] private TextMeshProUGUI choiceButtonText2;
        [SerializeField] private TextMeshProUGUI choiceButtonText3;

        [Header("Animation")]
        [SerializeField] private float transitionDuration = 0.5f;
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.3f;

        #endregion

        #region Private Fields

        // 动态标记系统
        private Dictionary<BoxPosition, ConversationBox> boxes = new Dictionary<BoxPosition, ConversationBox>();
        private Dictionary<BoxPosition, BoxPositionConfig> positionConfigs;
        private Stack<ConversationBox> boxPool = new Stack<ConversationBox>();
        private Dictionary<ConversationBox, System.Action> boxEventHandlers = new Dictionary<ConversationBox, System.Action>();

        // State
        private NarrativeSO currentNarrative;
        private bool isTransitioning;
        private bool isPanelVisible;
        private Coroutine transitionCoroutine;

        // Events
        public event Action<int> OnChoiceSelected;
        public event Action OnFinishClicked;

        #endregion

        #region Structs and Enums

        /// <summary>
        /// 对话框位置枚举
        /// </summary>
        public enum BoxPosition
        {
            Top,
            Center,
            Bottom
        }

        private enum TransitionDirection
        {
            Up,
            Down
        }

        /// <summary>
        /// 位置配置数据
        /// </summary>
        [System.Serializable]
        private struct BoxPositionConfig
        {
            public Vector2 anchoredPosition;
            public Vector3 rotation;  // Euler angles
            public Vector3 scale;
            public Vector2 size;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeComponents();
            InitializePositionConfigs();
            InitializeBoxes();
            HidePanel(true); // Start hidden
        }

        private void Update()
        {
            if (isPanelVisible && !isTransitioning)
            {
                // Handle scroll wheel input
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (scroll > 0.01f)
                {
                    NavigateToBox(BoxPosition.Top);
                }
                else if (scroll < -0.01f)
                {
                    NavigateToBox(BoxPosition.Bottom);
                }
            }
        }

        private void OnDestroy()
        {
            // 清理事件处理器
            foreach (var kvp in boxEventHandlers)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.OnBoxClicked -= kvp.Value;
                }
            }
            boxEventHandlers.Clear();

            // 清理对象池
            while (boxPool.Count > 0)
            {
                var box = boxPool.Pop();
                if (box != null)
                {
                    Destroy(box.gameObject);
                }
            }

            // 清理活跃的框
            foreach (var kvp in boxes)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }
        }

        #endregion

        #region Initialization

        private void InitializeComponents()
        {
            // Ensure canvas group exists
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // Setup finish button
            if (finishButton != null)
            {
                finishButton.gameObject.SetActive(false);
                finishButton.onClick.RemoveAllListeners();
                finishButton.onClick.AddListener(HandleFinishClicked);

                if (finishButtonText != null)
                    finishButtonText.text = "Finish";
            }

            // Setup choice buttons (default hidden)
            HideChoiceButtons();
        }

        /// <summary>
        /// 初始化位置配置（使用 fanRadius）
        /// </summary>
        private void InitializePositionConfigs()
        {
            positionConfigs = new Dictionary<BoxPosition, BoxPositionConfig>
            {
                [BoxPosition.Top] = new BoxPositionConfig
                {
                    anchoredPosition = new Vector2(fanPivot.x, fanPivot.y + fanRadius * 0.5f), // 使用fanRadius
                    rotation = new Vector3(0, 0, -fanAngle),
                    scale = Vector3.one * edgeBoxScale,
                    size = edgeBoxSize
                },
                [BoxPosition.Center] = new BoxPositionConfig
                {
                    anchoredPosition = fanPivot,
                    rotation = Vector3.zero,
                    scale = Vector3.one,
                    size = centerBoxSize
                },
                [BoxPosition.Bottom] = new BoxPositionConfig
                {
                    anchoredPosition = new Vector2(fanPivot.x, fanPivot.y - fanRadius * 0.5f), // 使用fanRadius
                    rotation = new Vector3(0, 0, fanAngle),
                    scale = Vector3.one * edgeBoxScale,
                    size = edgeBoxSize
                }
            };
        }

        /// <summary>
        /// 初始化对话框（动态创建）
        /// </summary>
        private void InitializeBoxes()
        {
            // 确保有预制体
            if (conversationBoxPrefab == null)
            {
                Debug.LogError("[FanConversationPanel] ConversationBox prefab is not assigned!");
                return;
            }

            // 为每个位置创建一个框
            foreach (BoxPosition position in Enum.GetValues(typeof(BoxPosition)))
            {
                var box = CreateBox();
                boxes[position] = box;
                ApplyPositionConfig(box, position);
                SetupBoxEventHandlers(box);  // 添加事件处理
                box.gameObject.SetActive(true);
            }
        }

        #endregion

        #region Object Pool Management

        /// <summary>
        /// 创建或从池中获取一个对话框
        /// </summary>
        private ConversationBox CreateBox()
        {
            ConversationBox box;

            if (boxPool.Count > 0)
            {
                box = boxPool.Pop();
                box.gameObject.SetActive(true);
            }
            else
            {
                // 实例化新的对话框
                var go = Instantiate(conversationBoxPrefab.gameObject, panelRoot.transform);
                box = go.GetComponent<ConversationBox>();

                // 确保有CanvasGroup
                if (box.GetComponent<CanvasGroup>() == null)
                {
                    box.gameObject.AddComponent<CanvasGroup>();
                }
            }

            // 设置事件处理
            SetupBoxEventHandlers(box);

            return box;
        }

        /// <summary>
        /// 将对话框返回池中
        /// </summary>
        private void ReturnToPool(ConversationBox box)
        {
            if (box == null) return;

            // 清理状态和事件（使用字典记录的处理器来取消订阅）
            if (boxEventHandlers.TryGetValue(box, out var handler))
            {
                box.OnBoxClicked -= handler;
                boxEventHandlers.Remove(box);
            }

            box.Clear();
            box.Hide();
            box.gameObject.SetActive(false);

            // 添加到池中
            boxPool.Push(box);
        }

        /// <summary>
        /// 设置对话框事件处理
        /// </summary>
        private void SetupBoxEventHandlers(ConversationBox box)
        {
            // 先清除旧的处理器（如果存在）
            if (boxEventHandlers.TryGetValue(box, out var oldHandler))
            {
                box.OnBoxClicked -= oldHandler;
                boxEventHandlers.Remove(box);
            }

            // 创建新的处理器并保存引用
            System.Action handler = () => HandleBoxClicked(box);
            box.OnBoxClicked += handler;
            boxEventHandlers[box] = handler;
        }

        /// <summary>
        /// 处理对话框点击
        /// </summary>
        private void HandleBoxClicked(ConversationBox clickedBox)
        {
            if (isTransitioning) return;

            // 查找点击的框当前的位置
            var position = GetPositionForBox(clickedBox);
            if (position.HasValue)
            {
                NavigateToBox(position.Value);
            }
        }

        /// <summary>
        /// 获取对话框当前的位置标记
        /// </summary>
        private BoxPosition? GetPositionForBox(ConversationBox box)
        {
            foreach (var kvp in boxes)
            {
                if (kvp.Value == box)
                    return kvp.Key;
            }
            return null;
        }

        #endregion

        #region Position Management

        /// <summary>
        /// 应用位置配置到对话框
        /// </summary>
        private void ApplyPositionConfig(ConversationBox box, BoxPosition position)
        {
            if (box == null) return;

            var config = positionConfigs[position];
            var rect = box.GetComponent<RectTransform>();

            rect.anchoredPosition = config.anchoredPosition;
            rect.localRotation = Quaternion.Euler(config.rotation);
            rect.localScale = config.scale;
            rect.sizeDelta = config.size;

            // 设置基础缩放
            box.SetBaseScale(config.scale);

            // 重置透明度
            var cg = box.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
            }

            // 设置高亮状态（中间框高亮）
            box.SetHighlight(position == BoxPosition.Center);
        }

        #endregion

        #region Public API

        /// <summary>
        /// 显示面板
        /// </summary>
        public void ShowPanel(NarrativeSO narrative)
        {
            if (narrative == null) return;

            currentNarrative = narrative;
            isPanelVisible = true;

            // Setup character portrait
            if (characterPortrait != null && narrative.CharacterPortrait != null)
            {
                characterPortrait.sprite = narrative.CharacterPortrait;
                characterPortrait.enabled = true;
            }

            if (characterName != null)
            {
                characterName.text = narrative.CharacterName;
            }

            // Show panel with animation
            StartCoroutine(FadeInPanel());
        }

        /// <summary>
        /// 隐藏面板
        /// </summary>
        public void HidePanel(bool immediate = false)
        {
            isPanelVisible = false;

            // 隐藏选择按钮
            HideChoiceButtons();

            if (immediate)
            {
                if (panelRoot != null)
                    panelRoot.SetActive(false);
                if (canvasGroup != null)
                    canvasGroup.alpha = 0;
            }
            else
            {
                StartCoroutine(FadeOutPanel());
            }
        }

        /// <summary>
        /// 显示对话片段
        /// Display conversation segments
        /// </summary>
        public void DisplaySegments(NarrativeSegment previous, NarrativeSegment current, NarrativeSegment next)
        {
            if (isTransitioning) return;

            // Update top box
            var topBox = boxes.ContainsKey(BoxPosition.Top) ? boxes[BoxPosition.Top] : null;
            if (topBox != null)
            {
                if (previous != null)
                {
                    topBox.SetContent(previous.TextContent, previous.SpeakerName);
                    topBox.Show();
                }
                else
                {
                    topBox.Hide();
                }
            }

            // Update center box
            var centerBox = boxes.ContainsKey(BoxPosition.Center) ? boxes[BoxPosition.Center] : null;
            if (centerBox != null && current != null)
            {
                centerBox.SetContent(current.TextContent, current.SpeakerName);
                centerBox.Show();
                centerBox.SetHighlight(true); // Center is always highlighted
            }

            // Update bottom box
            // 如果当前节点是 choice node，强制隐藏 bottom box（显示选择按钮代替）
            // If current segment is a choice node, force hide bottom box (choice buttons will be shown instead)
            var bottomBox = boxes.ContainsKey(BoxPosition.Bottom) ? boxes[BoxPosition.Bottom] : null;
            if (bottomBox != null)
            {
                if (next != null && (current == null || !current.IsChoiceNode))
                {
                    bottomBox.SetContent(next.TextContent, next.SpeakerName);
                    bottomBox.Show();
                }
                else
                {
                    bottomBox.Hide();
                }
            }
        }

        /// <summary>
        /// 显示选择选项
        /// </summary>
        public void PresentChoices(List<NarrativeSegment> choices)
        {
            // TODO: Implement choice presentation UI
            if (choices != null && choices.Count > 0)
            {
                var bottomBox = boxes.ContainsKey(BoxPosition.Bottom) ? boxes[BoxPosition.Bottom] : null;
                if (bottomBox != null)
                {
                    var firstChoice = choices[0];
                    bottomBox.SetContent($"[Choice] {firstChoice.TextContent}", "");
                    bottomBox.Show();
                }
            }
        }

        /// <summary>
        /// 显示结束按钮
        /// </summary>
        public void ShowFinishButton()
        {
            if (finishButton != null)
            {
                finishButton.gameObject.SetActive(true);

                // Animate button appearance with PrimeTween
                var rectTransform = finishButton.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.localScale = Vector3.zero;
                    Tween.Scale(rectTransform, Vector3.one, 0.3f, Ease.OutBack, useUnscaledTime: true);
                }
            }
        }

        #endregion

        #region Navigation

        /// <summary>
        /// 导航到指定位置的对话框
        /// </summary>
        private void NavigateToBox(BoxPosition position)
        {
            if (isTransitioning) return;

            switch (position)
            {
                case BoxPosition.Top:
                    var topBox = boxes.ContainsKey(BoxPosition.Top) ? boxes[BoxPosition.Top] : null;
                    if (topBox != null && topBox.IsVisible)
                    {
                        AnimateTransition(TransitionDirection.Up);
                    }
                    break;

                case BoxPosition.Bottom:
                    var bottomBox = boxes.ContainsKey(BoxPosition.Bottom) ? boxes[BoxPosition.Bottom] : null;
                    if (bottomBox != null && bottomBox.IsVisible)
                    {
                        AnimateTransition(TransitionDirection.Down);
                    }
                    break;
            }
        }

        /// <summary>
        /// 动画过渡
        /// </summary>
        private void AnimateTransition(TransitionDirection direction)
        {
            if (transitionCoroutine != null)
                StopCoroutine(transitionCoroutine);

            transitionCoroutine = StartCoroutine(PerformTransition(direction));
        }

        private IEnumerator PerformTransition(TransitionDirection direction)
        {
            isTransitioning = true;

            if (direction == TransitionDirection.Down)
            {
                // 向下滑动：Bottom → Center, Center → Top, Top 消失
                yield return StartCoroutine(AnimateDownTransition());
            }
            else if (direction == TransitionDirection.Up)
            {
                // 向上滑动：Top → Center, Center → Bottom, Bottom 消失
                yield return StartCoroutine(AnimateUpTransition());
            }

            isTransitioning = false;
        }

        /// <summary>
        /// 向下滑动动画
        /// </summary>
        private IEnumerator AnimateDownTransition()
        {
            // 先导航
            NarrativeManager.Instance?.NavigateForward();

            // 获取当前的框
            var oldTop = boxes[BoxPosition.Top];
            var oldCenter = boxes[BoxPosition.Center];
            var oldBottom = boxes[BoxPosition.Bottom];

            // 获取位置配置
            var topConfig = positionConfigs[BoxPosition.Top];
            var centerConfig = positionConfigs[BoxPosition.Center];

            // Bottom 滑到 Center 位置
            AnimateToPosition(oldBottom, centerConfig);

            // Center 滑到 Top 位置
            AnimateToPosition(oldCenter, topConfig);

            // Top 淡出并移动到上方
            AnimateFadeOut(oldTop, true);

            // 等待动画完成 - 使用 WaitForSecondsRealtime 确保暂停时继续
            yield return new WaitForSecondsRealtime(transitionDuration);

            // 回收旧的 Top
            ReturnToPool(oldTop);

            // 重新分配标记
            boxes[BoxPosition.Top] = oldCenter;
            boxes[BoxPosition.Center] = oldBottom;

            // 创建新的 Bottom
            var newBottom = CreateBox();
            boxes[BoxPosition.Bottom] = newBottom;
            ApplyPositionConfig(newBottom, BoxPosition.Bottom);

            // 设置事件处理
            SetupBoxEventHandlers(newBottom);

            // 更新显示内容
            UpdateDisplay();

            // 确保新框可见（使用 NarrativeManager 获取内容）
            var narrativeManager = NarrativeManager.Instance;
            if (narrativeManager != null && narrativeManager.ActiveSequence != null)
            {
                var activeSequence = narrativeManager.ActiveSequence;
                var nextSegment = GetNextSegment(activeSequence.CurrentSegment);
                if (nextSegment != null)
                {
                    newBottom.SetContent(nextSegment.TextContent, nextSegment.SpeakerName);
                    newBottom.Show();

                    // 添加淡入动画
                    var cg = newBottom.GetComponent<CanvasGroup>();
                    if (cg != null)
                    {
                        cg.alpha = 0f;
                        Tween.Alpha(cg, 1f, fadeInDuration, useUnscaledTime: true);
                    }
                }
                else
                {
                    // 如果没有下一个段落，隐藏新底部框
                    newBottom.Hide();
                }
            }
            else
            {
                // 如果没有活跃序列，隐藏新底部框
                newBottom.Hide();
            }
        }

        /// <summary>
        /// 向上滑动动画
        /// </summary>
        private IEnumerator AnimateUpTransition()
        {
            // 先导航
            NarrativeManager.Instance?.NavigateBackward();

            // 获取当前的框
            var oldTop = boxes[BoxPosition.Top];
            var oldCenter = boxes[BoxPosition.Center];
            var oldBottom = boxes[BoxPosition.Bottom];

            // 获取位置配置
            var centerConfig = positionConfigs[BoxPosition.Center];
            var bottomConfig = positionConfigs[BoxPosition.Bottom];

            // Top 滑到 Center 位置
            AnimateToPosition(oldTop, centerConfig);

            // Center 滑到 Bottom 位置
            AnimateToPosition(oldCenter, bottomConfig);

            // Bottom 淡出并移动到下方
            AnimateFadeOut(oldBottom, false);

            // 等待动画完成 - 使用 WaitForSecondsRealtime 确保暂停时继续
            yield return new WaitForSecondsRealtime(transitionDuration);

            // 回收旧的 Bottom
            ReturnToPool(oldBottom);

            // 重新分配标记
            boxes[BoxPosition.Bottom] = oldCenter;
            boxes[BoxPosition.Center] = oldTop;

            // 创建新的 Top
            var newTop = CreateBox();
            boxes[BoxPosition.Top] = newTop;
            ApplyPositionConfig(newTop, BoxPosition.Top);

            // 设置事件处理
            SetupBoxEventHandlers(newTop);

            // 更新显示内容
            UpdateDisplay();

            // 确保新框可见（使用 NarrativeManager 获取内容）
            var narrativeManager = NarrativeManager.Instance;
            if (narrativeManager != null && narrativeManager.ActiveSequence != null)
            {
                var activeSequence = narrativeManager.ActiveSequence;
                var previousSegment = GetPreviousSegment(activeSequence.CurrentSegment);
                if (previousSegment != null)
                {
                    newTop.SetContent(previousSegment.TextContent, previousSegment.SpeakerName);
                    newTop.Show();

                    // 添加淡入动画
                    var cg = newTop.GetComponent<CanvasGroup>();
                    if (cg != null)
                    {
                        cg.alpha = 0f;
                        Tween.Alpha(cg, 1f, fadeInDuration, useUnscaledTime: true);
                    }
                }
                else
                {
                    // 如果没有上一个段落，隐藏新顶部框
                    newTop.Hide();
                }
            }
            else
            {
                // 如果没有活跃序列，隐藏新顶部框
                newTop.Hide();
            }
        }

        /// <summary>
        /// 动画移动到指定配置
        /// </summary>
        private void AnimateToPosition(ConversationBox box, BoxPositionConfig config)
        {
            if (box == null) return;

            var rect = box.GetComponent<RectTransform>();

            // 标记开始动画
            box.SetScaleAnimating(true);

            // 动画位置、旋转、缩放和尺寸 - 使用 useUnscaledTime 确保暂停时动画继续
            Tween.UIAnchoredPosition(rect, config.anchoredPosition, transitionDuration, transitionCurve, useUnscaledTime: true);
            Tween.LocalRotation(rect, config.rotation, transitionDuration, transitionCurve, useUnscaledTime: true);

            // 缩放动画，完成时更新基础缩放
            Tween.Scale(rect, config.scale, transitionDuration, transitionCurve, useUnscaledTime: true)
                .OnComplete(() => {
                    box.SetBaseScale(config.scale);
                    box.SetScaleAnimating(false);
                });

            // 也要动画尺寸变化！
            Tween.UISizeDelta(rect, config.size, transitionDuration, transitionCurve, useUnscaledTime: true);

            // 如果移动到中间位置，设置高亮
            bool isCenter = config.anchoredPosition == fanPivot;
            box.SetHighlight(isCenter);
        }

        /// <summary>
        /// 淡出动画
        /// </summary>
        private void AnimateFadeOut(ConversationBox box, bool moveUp)
        {
            if (box == null) return;

            var cg = box.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                Tween.Alpha(cg, 0f, transitionDuration * 0.5f, useUnscaledTime: true);
            }

            // 移动到屏幕外
            var rect = box.GetComponent<RectTransform>();
            var offscreenPos = moveUp
                ? new Vector2(fanPivot.x, fanPivot.y + fanRadius)  // 使用fanRadius
                : new Vector2(fanPivot.x, fanPivot.y - fanRadius); // 使用fanRadius

            Tween.UIAnchoredPosition(rect, offscreenPos, transitionDuration, transitionCurve, useUnscaledTime: true);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// 获取下一个段落
        /// </summary>
        private NarrativeSegment GetNextSegment(NarrativeSegment current)
        {
            if (current == null) return null;

            // 返回默认的下一个段落（第一个子段落）
            return current.GetDefaultNext();
        }

        /// <summary>
        /// 获取上一个段落
        /// </summary>
        private NarrativeSegment GetPreviousSegment(NarrativeSegment current)
        {
            if (current == null) return null;
            return current.PreviousSegment;
        }

        /// <summary>
        /// 更新显示内容（在动画后调用）
        /// </summary>
        private void UpdateDisplay()
        {
            if (NarrativeManager.Instance != null && NarrativeManager.Instance.ActiveSequence != null)
            {
                var segments = NarrativeManager.Instance.ActiveSequence.GetVisibleSegments();
                DisplaySegments(segments.previous, segments.current, segments.next);
            }
        }

        private IEnumerator FadeInPanel()
        {
            if (panelRoot != null)
                panelRoot.SetActive(true);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0;

                float elapsed = 0;
                while (elapsed < fadeInDuration)
                {
                    // 使用 unscaledDeltaTime 确保暂停时UI动画继续
                    elapsed += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Lerp(0, 1, elapsed / fadeInDuration);
                    yield return null;
                }

                canvasGroup.alpha = 1;
            }
        }

        private IEnumerator FadeOutPanel()
        {
            if (canvasGroup != null)
            {
                float elapsed = 0;
                while (elapsed < fadeOutDuration)
                {
                    // 使用 unscaledDeltaTime 确保暂停时UI动画继续
                    elapsed += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Lerp(1, 0, elapsed / fadeOutDuration);
                    yield return null;
                }

                canvasGroup.alpha = 0;
            }

            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        private void HandleFinishClicked()
        {
            OnFinishClicked?.Invoke();
            NarrativeManager.Instance?.EndCurrentNarrative();
        }

        #endregion

        #region Choice Buttons

        /// <summary>
        /// 显示选择按钮（类似结束节点，显示3个选择）
        /// Show choice buttons (similar to end node, display 3 choices)
        /// </summary>
        public void ShowChoiceButtons(List<NarrativeSegment> choices)
        {
            if (choices == null || choices.Count == 0)
            {
                Debug.LogWarning("[FanConversationPanel] No choices to display");
                return;
            }

            // 隐藏 Bottom box（不显示下一个节点）
            if (boxes.ContainsKey(BoxPosition.Bottom))
                boxes[BoxPosition.Bottom].Hide();

            // 隐藏 Finish button
            if (finishButton != null)
                finishButton.gameObject.SetActive(false);

            // 设置3个选择按钮
            SetupChoiceButton(choiceButton1, choiceButtonText1, choices, 0);
            SetupChoiceButton(choiceButton2, choiceButtonText2, choices, 1);
            SetupChoiceButton(choiceButton3, choiceButtonText3, choices, 2);

            Debug.Log($"[FanConversationPanel] Showing {choices.Count} choice buttons");
        }

        /// <summary>
        /// 设置单个选择按钮
        /// Setup a single choice button
        /// </summary>
        private void SetupChoiceButton(Button btn, TextMeshProUGUI text, List<NarrativeSegment> choices, int index)
        {
            if (btn == null) return;

            if (index < choices.Count)
            {
                btn.gameObject.SetActive(true);

                // 使用分支节点的文本作为按钮文字
                if (text != null)
                    text.text = choices[index].TextContent;

                // 动画显示（与 Finish Button 一致）
                var rect = btn.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.localScale = Vector3.zero;
                    Tween.Scale(rect, Vector3.one, 0.3f, Ease.OutBack, useUnscaledTime: true);
                }

                // 绑定点击事件
                btn.onClick.RemoveAllListeners();
                int choiceIndex = index; // 捕获当前索引
                btn.onClick.AddListener(() => OnChoiceButtonClicked(choiceIndex));
            }
            else
            {
                btn.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 选择按钮点击处理
        /// Choice button click handler
        /// </summary>
        private void OnChoiceButtonClicked(int choiceIndex)
        {
            Debug.Log($"[FanConversationPanel] Choice {choiceIndex} selected");

            // 隐藏所有选择按钮
            HideChoiceButtons();

            // 通知选择
            OnChoiceSelected?.Invoke(choiceIndex);
        }

        /// <summary>
        /// 隐藏所有选择按钮
        /// Hide all choice buttons
        /// </summary>
        private void HideChoiceButtons()
        {
            if (choiceButton1 != null) choiceButton1.gameObject.SetActive(false);
            if (choiceButton2 != null) choiceButton2.gameObject.SetActive(false);
            if (choiceButton3 != null) choiceButton3.gameObject.SetActive(false);
        }

        #endregion
    }
}