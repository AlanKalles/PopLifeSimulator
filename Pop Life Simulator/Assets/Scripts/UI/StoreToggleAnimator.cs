using UnityEngine;
using PrimeTween;
using PopLife.Manager;

namespace PopLife.UI
{
    /// <summary>
    /// OpenStoreButton ↔ TimeControlUI 切换动画控制器
    /// 开店时：OpenStoreButton 按下→上飘 → TimeControlUI 坠入
    /// 闭店时：TimeControlUI 上飘 → OpenStoreButton 坠入
    /// </summary>
    public class StoreToggleAnimator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform openStoreButton;
        [SerializeField] private RectTransform timeControlUI;
        [SerializeField] private CanvasGroup openStoreCanvasGroup;
        [SerializeField] private CanvasGroup timeControlCanvasGroup;

        [Header("Press Animation")]
        [SerializeField] private float pressDuration = 0.1f;
        [SerializeField] private float pressDistance = 20f;
        [SerializeField] private float pressScale = 0.95f;

        [Header("Fly Out / Drop In")]
        [SerializeField] private float flyOutDuration = 0.4f;
        [SerializeField] private float dropInDuration = 0.5f;
        [SerializeField] private float sequenceDelay = 0.1f;

        // 缓存
        private Vector2 openStoreOriginalPos;
        private Vector2 timeControlOriginalPos;
        private float canvasHeight;

        // 动画状态
        private Sequence currentSequence;
        private bool isAnimating;

        // 锁定状态：marker触发前，OpenStoreButton和TimeControlUI都不显示
        private bool isUnlocked = false;

        private void Awake()
        {
            // 缓存原始位置
            if (openStoreButton != null)
                openStoreOriginalPos = openStoreButton.anchoredPosition;
            if (timeControlUI != null)
                timeControlOriginalPos = timeControlUI.anchoredPosition;

            // 获取 Canvas 坐标空间高度（适配不同分辨率）
            var canvas = openStoreButton != null
                ? openStoreButton.GetComponentInParent<Canvas>()
                : null;
            if (canvas != null)
                canvasHeight = ((RectTransform)canvas.transform).rect.height;
            else
                canvasHeight = 1080f; // 兜底值

            // 检查marker是否已经触发过（非首次游戏）
            if (TutorialEventBus.IsMarkerTriggered(TutorialMarker.EnableStoreToggle))
            {
                isUnlocked = true;
                SnapToInitState();
            }
            else
            {
                // 首次游戏，全部隐藏，等待marker
                SnapToHidden();
            }
        }

        private void OnEnable()
        {
            if (!isUnlocked)
                TutorialEventBus.OnMarkerTriggered += OnMarkerTriggered;
        }

        private void OnDisable()
        {
            TutorialEventBus.OnMarkerTriggered -= OnMarkerTriggered;
        }

        private void OnMarkerTriggered(TutorialMarker marker)
        {
            if (marker != TutorialMarker.EnableStoreToggle) return;

            isUnlocked = true;
            TutorialEventBus.OnMarkerTriggered -= OnMarkerTriggered;

            // 解锁时播放OpenStoreButton坠入动画
            PlayUnlockSequence();
        }

        /// <summary>
        /// 全部隐藏（marker触发前的锁定状态）
        /// </summary>
        private void SnapToHidden()
        {
            KillCurrentSequence();

            SetCanvasGroupVisible(openStoreCanvasGroup, false);
            SetCanvasGroupVisible(timeControlCanvasGroup, false);

            // 把两个UI都移到屏幕外，防止闪烁
            if (openStoreButton != null)
                openStoreButton.anchoredPosition = new Vector2(
                    openStoreOriginalPos.x, openStoreOriginalPos.y + canvasHeight);
            if (timeControlUI != null)
                timeControlUI.anchoredPosition = new Vector2(
                    timeControlOriginalPos.x, timeControlOriginalPos.y + canvasHeight);

            isAnimating = false;
        }

        /// <summary>
        /// 解锁动画：OpenStoreButton从屏幕上方坠入原位
        /// </summary>
        private void PlayUnlockSequence()
        {
            if (openStoreButton == null) return;
            if (isAnimating) return;

            isAnimating = true;

            // 将按钮放到屏幕上方，设为可见但暂不可交互
            openStoreButton.anchoredPosition = new Vector2(
                openStoreOriginalPos.x, openStoreOriginalPos.y + canvasHeight);
            openStoreButton.localScale = Vector3.one;
            SetCanvasGroupVisible(openStoreCanvasGroup, true);
            SetCanvasGroupInteractable(openStoreCanvasGroup, false);

            // timeControlUI 保持隐藏
            SetCanvasGroupVisible(timeControlCanvasGroup, false);

            currentSequence = Sequence.Create()
                .Chain(Tween.Custom(this,
                    openStoreOriginalPos.y + canvasHeight,
                    openStoreOriginalPos.y,
                    dropInDuration,
                    (target, val) => target.openStoreButton.anchoredPosition = new Vector2(
                        target.openStoreOriginalPos.x, val),
                    Ease.OutBounce))
                .ChainCallback(this, target =>
                {
                    SetCanvasGroupInteractable(target.openStoreCanvasGroup, true);
                    target.isAnimating = false;
                });
        }

        /// <summary>
        /// 瞬间归位（无动画）。场景加载第一天、中断恢复时调用
        /// </summary>
        public void SnapToInitState()
        {
            if (!isUnlocked)
            {
                SnapToHidden();
                return;
            }

            KillCurrentSequence();

            // openStoreButton：可见，原位，原大小
            if (openStoreButton != null)
            {
                openStoreButton.anchoredPosition = openStoreOriginalPos;
                openStoreButton.localScale = Vector3.one;
            }
            SetCanvasGroupVisible(openStoreCanvasGroup, true);

            // timeControlUI：不可见，移到屏幕外上方
            if (timeControlUI != null)
            {
                timeControlUI.anchoredPosition = new Vector2(
                    timeControlOriginalPos.x,
                    timeControlOriginalPos.y + canvasHeight);
                timeControlUI.localScale = Vector3.one;
            }
            SetCanvasGroupVisible(timeControlCanvasGroup, false);

            isAnimating = false;
        }

        /// <summary>
        /// 开店动画：OpenStoreButton 按下→上飘 → TimeControlUI 坠入
        /// </summary>
        public void PlayOpenStoreSequence()
        {
            if (!isUnlocked) return;
            if (isAnimating) return;
            if (openStoreButton == null || timeControlUI == null) return;

            isAnimating = true;

            // 禁用交互，防止动画期间点击
            SetCanvasGroupInteractable(openStoreCanvasGroup, false);
            SetCanvasGroupInteractable(timeControlCanvasGroup, false);

            float startY = openStoreOriginalPos.y;

            currentSequence = Sequence.Create()
                // 按下：Y向下 + 缩放0.95（同步，增强肉感）
                .Group(Tween.Custom(this, startY, startY - pressDistance, pressDuration,
                    (target, val) => target.openStoreButton.anchoredPosition = new Vector2(
                        target.openStoreOriginalPos.x, val),
                    Ease.InQuad))
                .Group(Tween.Scale(openStoreButton, pressScale, pressDuration, Ease.InQuad))
                // 上浮飘出屏幕
                .Chain(Tween.Custom(this, startY - pressDistance, startY + canvasHeight, flyOutDuration,
                    (target, val) => target.openStoreButton.anchoredPosition = new Vector2(
                        target.openStoreOriginalPos.x, val),
                    Ease.InBack))
                // 回调：隐藏按钮，准备 timeControlUI
                .ChainCallback(this, target =>
                {
                    // 隐藏 openStoreButton 并复位
                    SetCanvasGroupVisible(target.openStoreCanvasGroup, false);
                    target.openStoreButton.anchoredPosition = target.openStoreOriginalPos;
                    target.openStoreButton.localScale = Vector3.one;

                    // 将 timeControlUI 放到屏幕上方，显示但暂不可交互
                    target.timeControlUI.anchoredPosition = new Vector2(
                        target.timeControlOriginalPos.x,
                        target.timeControlOriginalPos.y + target.canvasHeight);
                    SetCanvasGroupVisible(target.timeControlCanvasGroup, true);
                    SetCanvasGroupInteractable(target.timeControlCanvasGroup, false);
                })
                // 短延迟
                .ChainDelay(sequenceDelay)
                // timeControlUI 坠入（OutBounce = 惯性拉力弹跳）
                .Chain(Tween.Custom(this,
                    timeControlOriginalPos.y + canvasHeight,
                    timeControlOriginalPos.y,
                    dropInDuration,
                    (target, val) => target.timeControlUI.anchoredPosition = new Vector2(
                        target.timeControlOriginalPos.x, val),
                    Ease.OutBounce))
                // 完成：启用交互
                .ChainCallback(this, target =>
                {
                    SetCanvasGroupInteractable(target.timeControlCanvasGroup, true);
                    target.isAnimating = false;
                });
        }

        /// <summary>
        /// 闭店动画：TimeControlUI 上飘 → OpenStoreButton 坠入
        /// </summary>
        public void PlayCloseStoreSequence()
        {
            if (!isUnlocked) return;
            if (isAnimating) return;
            if (openStoreButton == null || timeControlUI == null) return;

            isAnimating = true;

            // 禁用交互
            SetCanvasGroupInteractable(openStoreCanvasGroup, false);
            SetCanvasGroupInteractable(timeControlCanvasGroup, false);

            float tcStartY = timeControlOriginalPos.y;

            currentSequence = Sequence.Create()
                // timeControlUI 上飘飘出
                .Chain(Tween.Custom(this, tcStartY, tcStartY + canvasHeight, flyOutDuration,
                    (target, val) => target.timeControlUI.anchoredPosition = new Vector2(
                        target.timeControlOriginalPos.x, val),
                    Ease.InBack))
                // 回调：隐藏 timeControlUI，准备 openStoreButton
                .ChainCallback(this, target =>
                {
                    SetCanvasGroupVisible(target.timeControlCanvasGroup, false);
                    target.timeControlUI.anchoredPosition = target.timeControlOriginalPos;

                    // 将 openStoreButton 放到屏幕上方
                    target.openStoreButton.anchoredPosition = new Vector2(
                        target.openStoreOriginalPos.x,
                        target.openStoreOriginalPos.y + target.canvasHeight);
                    target.openStoreButton.localScale = Vector3.one;
                    SetCanvasGroupVisible(target.openStoreCanvasGroup, true);
                    SetCanvasGroupInteractable(target.openStoreCanvasGroup, false);
                })
                // 短延迟
                .ChainDelay(sequenceDelay)
                // openStoreButton 坠入
                .Chain(Tween.Custom(this,
                    openStoreOriginalPos.y + canvasHeight,
                    openStoreOriginalPos.y,
                    dropInDuration,
                    (target, val) => target.openStoreButton.anchoredPosition = new Vector2(
                        target.openStoreOriginalPos.x, val),
                    Ease.OutBounce))
                // 完成：启用交互
                .ChainCallback(this, target =>
                {
                    SetCanvasGroupInteractable(target.openStoreCanvasGroup, true);
                    target.isAnimating = false;
                });
        }

        private void KillCurrentSequence()
        {
            if (currentSequence.isAlive)
            {
                currentSequence.Stop();
                // 强制复位，防止残留状态
                if (openStoreButton != null)
                {
                    openStoreButton.anchoredPosition = openStoreOriginalPos;
                    openStoreButton.localScale = Vector3.one;
                }
                if (timeControlUI != null)
                {
                    timeControlUI.anchoredPosition = timeControlOriginalPos;
                    timeControlUI.localScale = Vector3.one;
                }
            }
        }

        private void OnDestroy()
        {
            // 场景切换/对象销毁时清理 tween，防止 MissingReferenceException
            KillCurrentSequence();
        }

        private static void SetCanvasGroupVisible(CanvasGroup cg, bool visible)
        {
            if (cg == null) return;
            cg.alpha = visible ? 1f : 0f;
            cg.blocksRaycasts = visible;
            cg.interactable = visible;
        }

        private static void SetCanvasGroupInteractable(CanvasGroup cg, bool interactable)
        {
            if (cg == null) return;
            cg.interactable = interactable;
            cg.blocksRaycasts = interactable;
        }
    }
}
