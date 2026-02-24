using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using Pathfinding;

namespace PopLife.AlanBot
{
    /// <summary>
    /// AlanBot行为树节点：空闲等待+随机表情sprite切换
    /// 在空闲期间随机切换表情，表情切换时跳两下
    /// </summary>
    [Category("PopLife/AlanBot")]
    [Description("AlanBot空闲：等待+随机表情切换+跳跃")]
    public class AlanBotIdleAction : ActionTask
    {
        [Tooltip("空闲时间范围（秒）")]
        public Vector2 idleDurationRange = new Vector2(4f, 6f);
        [Tooltip("表情切换间隔范围（秒）")]
        public Vector2 expressionIntervalRange = new Vector2(3f, 8f);
        [Tooltip("表情持续时间，超时后切回BaseModel（秒）")]
        public float expressionHoldDuration = 2f;

        private AlanBotController controller;
        private AlanBotAnimator animator;
        private AILerp aiLerp;

        // 计时器
        private float idleDuration;
        private float idleTimer;
        private float expressionInterval;
        private float expressionTimer;
        private float holdTimer;

        // 表情状态
        private bool isShowingExpression;
        private string[] availableExpressions;

        protected override string info => "AlanBot空闲等待";

        protected override void OnExecute()
        {
            controller = agent.GetComponent<AlanBotController>();
            animator = agent.GetComponent<AlanBotAnimator>();
            aiLerp = agent.GetComponent<AILerp>();

            if (controller == null || animator == null)
            {
                EndAction(false);
                return;
            }

            // 停止移动
            if (aiLerp != null)
                aiLerp.isStopped = true;

            // 回到基础表情
            animator.ReturnToBaseModel();

            // 初始化计时器
            idleDuration = Random.Range(idleDurationRange.x, idleDurationRange.y);
            idleTimer = 0f;
            expressionInterval = Random.Range(expressionIntervalRange.x, expressionIntervalRange.y);
            expressionTimer = 0f;
            holdTimer = 0f;
            isShowingExpression = false;

            // 获取可用表情列表
            availableExpressions = animator.GetRandomExpressionNames();
        }

        protected override void OnUpdate()
        {
            if (controller == null || animator == null)
            {
                EndAction(false);
                return;
            }

            // 如果正在交互，暂停所有计时
            if (controller.isInteracting) return;

            float dt = Time.deltaTime;

            // 主计时器
            idleTimer += dt;

            // 表情逻辑
            if (isShowingExpression)
            {
                // 等待跳跃完成 + 表情持续时间
                if (!animator.IsBouncing)
                {
                    holdTimer += dt;
                    if (holdTimer >= expressionHoldDuration)
                    {
                        // 切回BaseModel（如果表情skipBounce=false，会跳两下）
                        animator.ReturnToBaseModel();
                        isShowingExpression = false;
                        // 重置表情计时器
                        expressionInterval = Random.Range(expressionIntervalRange.x, expressionIntervalRange.y);
                        expressionTimer = 0f;
                    }
                }
            }
            else
            {
                // 等待下次表情切换
                expressionTimer += dt;
                if (expressionTimer >= expressionInterval && !animator.IsBouncing)
                {
                    TryShowRandomExpression();
                }
            }

            // 检查空闲是否结束
            if (idleTimer >= idleDuration)
            {
                // 确保当前是BaseModel状态且不在跳跃/表情中
                if (!isShowingExpression && !animator.IsBouncing)
                {
                    EndAction(true);
                }
            }
        }

        private void TryShowRandomExpression()
        {
            if (availableExpressions == null || availableExpressions.Length == 0) return;

            string randomExpr = availableExpressions[Random.Range(0, availableExpressions.Length)];
            animator.ShowExpression(randomExpr);
            isShowingExpression = true;
            holdTimer = 0f;
        }

        protected override void OnStop()
        {
            // 清理状态
            isShowingExpression = false;
        }
    }
}
