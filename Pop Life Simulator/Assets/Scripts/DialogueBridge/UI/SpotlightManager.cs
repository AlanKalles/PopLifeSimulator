using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using PrimeTween;
using Sirenix.OdinInspector;

namespace PopLife.DialogueBridge.UI
{
    /// <summary>
    /// Spotlight + Tooltip 统一控制器。零 Dialogue System 依赖。
    ///
    /// 核心契约：
    /// <list type="bullet">
    /// <item>Spotlight 与 Tooltip 一体：Show 必传 text，二者同时显示同时隐藏</item>
    /// <item>Show 入口若已 active，强制 Hide(Manual) 清理旧状态</item>
    /// <item>所有关闭路径（Manual / TargetClicked / BackgroundClicked / TargetLost）必经 Hide(reason)</item>
    /// <item>LateUpdate 检测 rect diff（4 corner 投影）+ 失效检测，自动跟随 + 自动 Hide</item>
    /// </list>
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
                return;
            }

            if (spotlightCanvas != null && spotlightCanvas.sortingOrder < 1000)
            {
                Debug.LogWarning(
                    $"[SpotlightManager] spotlightCanvas.sortingOrder={spotlightCanvas.sortingOrder}，" +
                    "建议 ≥ 1000 以确保覆盖游戏 UI");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        #endregion

        #region Serialized Fields

        [Title("References")]
        [Required, SerializeField] private SpotlightPanel spotlightPanel;
        [Required, SerializeField] private SpotlightTooltip tooltip;
        [SerializeField] private Canvas spotlightCanvas;

        [Title("Animation Settings")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.2f;
        [SerializeField] private float pulseScale = 1.08f;
        [SerializeField] private float pulseDuration = 0.8f;
        [SerializeField] private Ease pulseEase = Ease.InOutSine;

        [Title("Layout")]
        [SerializeField, Tooltip("Spotlight 高亮区域的额外内边距（像素）")]
        private float defaultPadding = 15f;

        [Title("Debug")]
        [SerializeField] private bool debugMode = false;

        #endregion

        #region Runtime State

        private CanvasGroup panelCanvasGroup;
        private bool isActive;
        private SpotlightRequest currentRequest;
        private ResolvedTarget currentTarget;
        private Rect lastComputedRect;
        private int lastScreenW, lastScreenH;

        // Passthrough 模式下的临时订阅（统一由 Hide 清理）
        private Button subscribedButton;
        private UnityAction subscribedButtonHandler;
        private SpotlightTargetClickProxy attachedProxy;
        private RectTransform raycastModifiedRT;
        private bool raycastSavedState;
        private bool didModifyRaycast;
        private bool targetClickHandled;

        // 动画 tween 引用（Hide 时 Stop）
        private Tween fadeTween;
        private Sequence pulseSequence;

        #endregion

        #region Public API

        /// <summary>
        /// 显示 spotlight + tooltip。此为唯一主入口。
        /// </summary>
        /// <exception cref="System.ArgumentException">text 为空或 target 无效时抛出</exception>
        public void Show(SpotlightRequest request)
        {
            request.Validate();

            // 入口防御：彻底清理任何旧状态
            if (isActive) Hide(CloseReason.Manual);

            // 解析目标（Camera fallback 一次性完成）
            var resolved = request.target.Resolve(spotlightCanvas);
            if (!resolved.IsValid)
            {
                // Resolve 失败（注册表查不到 / 无 Camera 等）已 logwarning，静默不显示
                return;
            }

            currentRequest = request;
            currentTarget = resolved;
            isActive = true;
            targetClickHandled = false;

            EnsurePanelCanvasGroup();

            // 计算初始 rect 并应用
            var rect = resolved.GetScreenRect();
            ApplyRect(rect, request.shape);
            lastComputedRect = rect;
            lastScreenW = Screen.width;
            lastScreenH = Screen.height;

            // 模式切换
            spotlightPanel.SetInteractionMode(request.mode);
            if (request.mode == InteractionMode.Blocking && spotlightPanel.BlockerHandler != null)
            {
                spotlightPanel.BlockerHandler.onClicked = OnBackgroundClicked;
            }
            else if (request.mode == InteractionMode.Passthrough)
            {
                AttachPassthroughListener(resolved);
            }

            // 显示 tooltip（IsTextSuppressed 时仅显示 spotlight，tooltip 保持隐藏）
            if (request.IsTextSuppressed)
            {
                if (tooltip != null) tooltip.Hide();
            }
            else
            {
                // Passthrough 模式下走 ClickButton 智能定位（tooltip 远离按钮，避免遮挡）
                tooltip.Show(request.text, rect, request.position, request.customOffset,
                    isClickButton: request.mode == InteractionMode.Passthrough);
            }

            // Spotlight panel 淡入
            spotlightPanel.gameObject.SetActive(true);
            fadeTween.Stop();
            panelCanvasGroup.alpha = 0f;
            fadeTween = Tween.Alpha(panelCanvasGroup, 1f, fadeInDuration, useUnscaledTime: true);

            // 边框脉动动画
            StartPulseAnimation();

            if (debugMode) Debug.Log($"[SpotlightManager] Show: target={resolved.kind}, mode={request.mode}, rect={rect}");
        }

        /// <summary>
        /// 关闭 spotlight + tooltip，统一清理所有监听与动画。
        /// </summary>
        public void Hide(CloseReason reason = CloseReason.Manual)
        {
            if (!isActive) return;

            if (debugMode) Debug.Log($"[SpotlightManager] Hide: reason={reason}");

            // 1. 解除 Passthrough 监听（无论何种关闭路径都走这里）
            DetachPassthroughListener();

            // 2. 解除 Blocking 监听
            if (spotlightPanel != null && spotlightPanel.BlockerHandler != null)
            {
                spotlightPanel.BlockerHandler.onClicked = null;
            }

            // 3. 停止脉动并淡出
            if (pulseSequence.isAlive) pulseSequence.Stop();
            if (spotlightPanel != null && spotlightPanel.HighlightBorder != null)
            {
                spotlightPanel.HighlightBorder.localScale = Vector3.one;
            }

            fadeTween.Stop();
            if (panelCanvasGroup != null)
            {
                fadeTween = Tween.Alpha(panelCanvasGroup, 0f, fadeOutDuration, useUnscaledTime: true)
                    .OnComplete(() =>
                    {
                        if (spotlightPanel != null) spotlightPanel.gameObject.SetActive(false);
                    });
            }

            // 4. tooltip 淡出
            if (tooltip != null) tooltip.Hide();

            // 5. 触发回调（捕获本地副本，回调中可能再次 Show）
            var cb = currentRequest.onClosed;

            // 6. 重置状态
            isActive = false;
            currentTarget = ResolvedTarget.Empty;
            currentRequest = default;
            targetClickHandled = false;

            // 7. 调用回调（最后，确保 isActive=false 已生效）
            cb?.Invoke(reason);
        }

        /// <summary>当前是否有活跃的 spotlight</summary>
        public bool IsActive => isActive;

        /// <summary>Spotlight 视觉淡出耗时。Sequencer 合成命令用它避免在遮罩淡出中途恢复 Dialogue UI。</summary>
        public float FadeOutDuration => fadeOutDuration;

        #endregion

        #region Convenience Overloads

        public void Show(string targetId, string text,
            TooltipPosition position = TooltipPosition.Auto,
            SpotlightShape shape = SpotlightShape.RoundedRectangle,
            InteractionMode mode = InteractionMode.Passthrough)
        {
            Show(new SpotlightRequest
            {
                target = SpotlightTargetSpec.ById(targetId),
                text = text, position = position, shape = shape, mode = mode,
            });
        }

        public void Show(RectTransform target, string text,
            TooltipPosition position = TooltipPosition.Auto,
            SpotlightShape shape = SpotlightShape.RoundedRectangle,
            InteractionMode mode = InteractionMode.Passthrough)
        {
            Show(new SpotlightRequest
            {
                target = SpotlightTargetSpec.ForRect(target),
                text = text, position = position, shape = shape, mode = mode,
            });
        }

        public void Show(Rect screenRect, string text,
            TooltipPosition position = TooltipPosition.Auto,
            SpotlightShape shape = SpotlightShape.RoundedRectangle,
            InteractionMode mode = InteractionMode.Passthrough)
        {
            Show(new SpotlightRequest
            {
                target = SpotlightTargetSpec.ForScreenRect(screenRect),
                text = text, position = position, shape = shape, mode = mode,
            });
        }

        #endregion

        #region LateUpdate Tracking

        private void LateUpdate()
        {
            if (!isActive) return;

            // 失效检测优先
            if (currentTarget.IsLost())
            {
                Hide(CloseReason.TargetLost);
                return;
            }

            var newRect = currentTarget.GetScreenRect();
            bool screenChanged = Screen.width != lastScreenW || Screen.height != lastScreenH;
            bool rectChanged = !ApproxEquals(newRect, lastComputedRect, epsilon: 0.5f);

            if (screenChanged || rectChanged)
            {
                ApplyRect(newRect, currentRequest.shape);
                // 与初次 Show 保持一致：text 被抑制时不更新 tooltip；否则走 ClickButton 智能定位
                if (!currentRequest.IsTextSuppressed)
                {
                    tooltip.Show(currentRequest.text, newRect, currentRequest.position, currentRequest.customOffset,
                        isClickButton: currentRequest.mode == InteractionMode.Passthrough);
                }
                lastComputedRect = newRect;
                lastScreenW = Screen.width;
                lastScreenH = Screen.height;
            }
        }

        private static bool ApproxEquals(Rect a, Rect b, float epsilon)
        {
            return Mathf.Abs(a.x - b.x) < epsilon
                && Mathf.Abs(a.y - b.y) < epsilon
                && Mathf.Abs(a.width - b.width) < epsilon
                && Mathf.Abs(a.height - b.height) < epsilon;
        }

        #endregion

        #region Rendering Helpers

        private void EnsurePanelCanvasGroup()
        {
            if (panelCanvasGroup == null)
            {
                panelCanvasGroup = spotlightPanel.GetComponent<CanvasGroup>();
                if (panelCanvasGroup == null)
                {
                    panelCanvasGroup = spotlightPanel.gameObject.AddComponent<CanvasGroup>();
                }
            }
        }

        private void ApplyRect(Rect rect, SpotlightShape shape)
        {
            // padding 来自 SpotlightPanel 内部，不在此处重复加
            spotlightPanel.SetTarget(rect, shape);
        }

        private void StartPulseAnimation()
        {
            if (spotlightPanel == null || spotlightPanel.HighlightBorder == null) return;

            if (pulseSequence.isAlive) pulseSequence.Stop();
            var border = spotlightPanel.HighlightBorder;
            border.localScale = Vector3.one;

            // 无限循环 yoyo：放大 → 缩回。
            // 注意：PrimeTween 中 Sequence 控制所有子 tween 的 useUnscaledTime，
            // 必须把 useUnscaledTime 设到 Sequence.Create 上，子 Tween 不能再传（否则报错）
            float halfDuration = pulseDuration * 0.5f;
            pulseSequence = Sequence.Create(cycles: -1, useUnscaledTime: true)
                .Chain(Tween.Scale(border, Vector3.one * pulseScale, halfDuration, pulseEase))
                .Chain(Tween.Scale(border, Vector3.one, halfDuration, pulseEase));
        }

        #endregion

        #region Passthrough Listener Management

        private void AttachPassthroughListener(ResolvedTarget resolved)
        {
            // 仅 UI 路径（World 物体目标的 Passthrough 暂不支持点击穿透）
            if (resolved.kind != ResolvedTarget.Kind.UI) return;
            var rt = resolved.rectTransform;
            if (rt == null) return;

            // 路径 A：目标是 Button — 仅订阅 onClick，最稳
            var btn = rt.GetComponent<Button>();
            if (btn != null)
            {
                subscribedButton = btn;
                subscribedButtonHandler = OnTargetClicked;
                btn.onClick.AddListener(subscribedButtonHandler);
                return;
            }

            // 路径 B：非 Button — 临时挂 ClickProxy
            // 是否需要打开 raycastTarget：尊重 SpotlightTarget.ensureRaycastTarget 配置
            bool ensureRaycast = true;
            if (resolved.targetComponent != null)
            {
                ensureRaycast = resolved.targetComponent.EnsureRaycastTarget;
            }
            if (ensureRaycast)
            {
                EnsureRaycastTarget(rt);
            }

            attachedProxy = rt.gameObject.AddComponent<SpotlightTargetClickProxy>();
            attachedProxy.onClicked = OnTargetClicked;
        }

        private void DetachPassthroughListener()
        {
            if (subscribedButton != null && subscribedButtonHandler != null)
            {
                subscribedButton.onClick.RemoveListener(subscribedButtonHandler);
                subscribedButton = null;
                subscribedButtonHandler = null;
            }
            if (attachedProxy != null)
            {
                Destroy(attachedProxy);
                attachedProxy = null;
            }
            if (didModifyRaycast && raycastModifiedRT != null)
            {
                var graphic = raycastModifiedRT.GetComponent<Graphic>();
                if (graphic != null) graphic.raycastTarget = raycastSavedState;
            }
            raycastModifiedRT = null;
            didModifyRaycast = false;
        }

        private void EnsureRaycastTarget(RectTransform rt)
        {
            var graphic = rt.GetComponent<Graphic>();
            if (graphic == null) return;
            if (graphic.raycastTarget) return;   // 已是 true，无需修改

            raycastSavedState = graphic.raycastTarget;
            raycastModifiedRT = rt;
            didModifyRaycast = true;
            graphic.raycastTarget = true;
        }

        #endregion

        #region Click Callbacks

        private void OnTargetClicked()
        {
            if (targetClickHandled) return;
            targetClickHandled = true;

            // 先触发用户回调（在 Hide 之前，玩家逻辑可能依赖 currentRequest）
            currentRequest.onTargetClicked?.Invoke();
            Hide(CloseReason.TargetClicked);
        }

        private void OnBackgroundClicked()
        {
            Hide(CloseReason.BackgroundClicked);
        }

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
}
