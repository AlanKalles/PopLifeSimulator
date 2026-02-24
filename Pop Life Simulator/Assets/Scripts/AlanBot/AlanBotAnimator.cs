using UnityEngine;
using Pathfinding;
using PrimeTween;

namespace PopLife.AlanBot
{
    /// <summary>
    /// AlanBot表情系统数据（Inspector可扩展）
    /// </summary>
    [System.Serializable]
    public class AlanBotExpression
    {
        public string name;       // 表情名称（如 "Happy", "Bruh"）
        public Sprite sprite;     // 表情sprite
        public bool skipBounce;   // 是否跳过跳跃（Speechless=true）
    }

    /// <summary>
    /// AlanBot单Sprite动画控制器
    /// 所有动画通过替换单个SpriteRenderer的sprite实现
    /// 包含：移动帧动画、表情切换、跳跃、方向翻转
    /// </summary>
    public class AlanBotAnimator : MonoBehaviour
    {
        private enum AnimState
        {
            Idle,
            Walking,
            Expression,
            Bouncing,
            Interaction,
            FinishingWalk
        }

        [Header("组件")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("移动动画")]
        [SerializeField] private Sprite[] walkFrames;
        [SerializeField] private float walkFrameInterval = 0.15f;

        [Header("基础表情")]
        [SerializeField] private Sprite baseModelSprite;

        [Header("可用表情列表（可扩展）")]
        [SerializeField] private AlanBotExpression[] expressions;
        // 初始：Happy(skipBounce=false), Speechless(skipBounce=true), Bruh(skipBounce=false)

        [Header("交互表情")]
        [SerializeField] private string interactionExpressionName = "Speechless";

        [Header("跳跃参数")]
        [SerializeField] private float bounceHeight = 0.05f;
        [SerializeField] private float bounceDuration = 0.15f;

        // 内部状态
        private AnimState currentState = AnimState.Idle;
        private int currentFrameIndex;
        private float frameTimer;
        private AlanBotExpression currentExpression;
        private Sequence bounceSequence;
        private Vector3 originalLocalPosition;

        // AILerp引用（用于方向检测）
        private AILerp aiLerp;

        // 公开属性
        public bool IsBouncing => currentState == AnimState.Bouncing;
        public bool IsFinishingWalk => currentState == AnimState.FinishingWalk;

        private void Awake()
        {
            aiLerp = GetComponent<AILerp>();
            originalLocalPosition = spriteRenderer.transform.localPosition;
        }

        private void Update()
        {
            UpdateDirectionFlip();
            UpdateWalkAnimation();
            UpdateFinishingWalk();
        }

        // ─── 方向翻转 ───

        /// <summary>
        /// 根据AILerp速度方向翻转sprite
        /// 默认朝左（flipX=false），往右时flipX=true
        /// </summary>
        private void UpdateDirectionFlip()
        {
            if (aiLerp == null || currentState != AnimState.Walking) return;

            float vx = aiLerp.velocity.x;
            if (Mathf.Abs(vx) > 0.01f)
            {
                spriteRenderer.flipX = vx > 0;
            }
        }

        // ─── 移动帧动画 ───

        /// <summary>
        /// 设置行走状态
        /// true: 开始播放walkFrames循环
        /// false: 将动画播放回第一帧后停止（FinishingWalk）
        /// </summary>
        public void SetWalking(bool walking)
        {
            if (walking)
            {
                // 开始行走
                StopBounceIfActive();
                currentState = AnimState.Walking;
                currentFrameIndex = 0;
                frameTimer = 0f;
                if (walkFrames != null && walkFrames.Length > 0)
                    spriteRenderer.sprite = walkFrames[0];
            }
            else
            {
                if (currentState == AnimState.Walking)
                {
                    // 进入FinishingWalk：继续播放到第0帧再停
                    currentState = AnimState.FinishingWalk;
                }
            }
        }

        private void UpdateWalkAnimation()
        {
            if (currentState != AnimState.Walking) return;
            if (walkFrames == null || walkFrames.Length == 0) return;

            frameTimer += Time.deltaTime;
            if (frameTimer >= walkFrameInterval)
            {
                frameTimer -= walkFrameInterval;
                currentFrameIndex = (currentFrameIndex + 1) % walkFrames.Length;
                spriteRenderer.sprite = walkFrames[currentFrameIndex];
            }
        }

        /// <summary>
        /// FinishingWalk：继续播放帧直到回到第0帧
        /// </summary>
        private void UpdateFinishingWalk()
        {
            if (currentState != AnimState.FinishingWalk) return;
            if (walkFrames == null || walkFrames.Length == 0)
            {
                GoToIdle();
                return;
            }

            // 如果已经在第0帧，直接转idle
            if (currentFrameIndex == 0)
            {
                GoToIdle();
                return;
            }

            // 继续播放帧动画直到回到第0帧
            frameTimer += Time.deltaTime;
            if (frameTimer >= walkFrameInterval)
            {
                frameTimer -= walkFrameInterval;
                currentFrameIndex = (currentFrameIndex + 1) % walkFrames.Length;
                spriteRenderer.sprite = walkFrames[currentFrameIndex];

                if (currentFrameIndex == 0)
                {
                    GoToIdle();
                }
            }
        }

        private void GoToIdle()
        {
            currentState = AnimState.Idle;
            spriteRenderer.sprite = baseModelSprite;
            currentExpression = null;
        }

        // ─── 表情系统 ───

        /// <summary>
        /// 切换到指定表情sprite
        /// 如果skipBounce=false → 切换后跳两下
        /// 如果skipBounce=true → 直接切换，不跳
        /// </summary>
        public void ShowExpression(string expressionName)
        {
            var expr = FindExpression(expressionName);
            if (expr == null)
            {
                Debug.LogWarning($"[AlanBotAnimator] 找不到表情: {expressionName}");
                return;
            }

            StopBounceIfActive();
            currentExpression = expr;
            spriteRenderer.sprite = expr.sprite;
            currentState = AnimState.Expression;

            if (!expr.skipBounce)
            {
                StartBounce();
            }
        }

        /// <summary>
        /// 回到baseModelSprite
        /// 根据当前表情的skipBounce决定是否跳
        /// </summary>
        public void ReturnToBaseModel()
        {
            bool shouldBounce = currentExpression != null && !currentExpression.skipBounce;

            StopBounceIfActive();
            spriteRenderer.sprite = baseModelSprite;
            currentState = AnimState.Idle;

            if (shouldBounce)
            {
                StartBounce();
            }

            currentExpression = null;
        }

        /// <summary>
        /// 显示交互表情（Speechless，skipBounce=true，不跳）
        /// </summary>
        public void ShowInteractionExpression()
        {
            var expr = FindExpression(interactionExpressionName);
            if (expr == null)
            {
                Debug.LogWarning($"[AlanBotAnimator] 找不到交互表情: {interactionExpressionName}");
                return;
            }

            StopBounceIfActive();
            currentExpression = expr;
            spriteRenderer.sprite = expr.sprite;
            currentState = AnimState.Interaction;
            // Speechless的skipBounce=true，不跳
        }

        /// <summary>
        /// 获取所有可用的非交互表情名（用于随机选择）
        /// </summary>
        public string[] GetRandomExpressionNames()
        {
            if (expressions == null || expressions.Length == 0) return System.Array.Empty<string>();

            var names = new System.Collections.Generic.List<string>();
            foreach (var expr in expressions)
            {
                // 排除交互表情
                if (expr.name != interactionExpressionName)
                    names.Add(expr.name);
            }
            return names.ToArray();
        }

        // ─── 跳跃（PrimeTween）───

        /// <summary>
        /// 跳两下序列：上→下→上→下
        /// </summary>
        private void StartBounce()
        {
            currentState = AnimState.Bouncing;
            float originY = originalLocalPosition.y;

            bounceSequence = Sequence.Create()
                .Chain(Tween.LocalPositionY(spriteRenderer.transform,
                    originY + bounceHeight, bounceDuration, Ease.OutQuad))
                .Chain(Tween.LocalPositionY(spriteRenderer.transform,
                    originY, bounceDuration, Ease.InQuad))
                .Chain(Tween.LocalPositionY(spriteRenderer.transform,
                    originY + bounceHeight, bounceDuration, Ease.OutQuad))
                .Chain(Tween.LocalPositionY(spriteRenderer.transform,
                    originY, bounceDuration, Ease.InQuad))
                .ChainCallback(this, target =>
                {
                    // 跳跃完成后恢复到之前的状态
                    if (target.currentState == AnimState.Bouncing)
                    {
                        // 如果有表情显示中，回到Expression状态
                        if (target.currentExpression != null)
                            target.currentState = AnimState.Expression;
                        else
                            target.currentState = AnimState.Idle;
                    }
                });
        }

        private void StopBounceIfActive()
        {
            if (bounceSequence.isAlive)
            {
                bounceSequence.Stop();
                // 恢复原始位置
                spriteRenderer.transform.localPosition = originalLocalPosition;
            }
        }

        private AlanBotExpression FindExpression(string expressionName)
        {
            if (expressions == null) return null;
            foreach (var expr in expressions)
            {
                if (expr.name == expressionName)
                    return expr;
            }
            return null;
        }
    }
}
