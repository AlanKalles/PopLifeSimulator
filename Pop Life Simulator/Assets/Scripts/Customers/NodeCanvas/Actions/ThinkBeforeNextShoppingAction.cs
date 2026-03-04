using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using Pathfinding;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer/Animation")]
    [Description("概率性原地思考：停下 → 播放Think表情 → 恢复。有每日次数上限")]
    public class ThinkBeforeNextShoppingAction : ActionTask
    {
        [UnityEngine.Header("概率控制")]
        [Tooltip("每次购物循环结束后触发思考的概率（0~1）")]
        [Range(0f, 1f)]
        public float thinkProbability = 0.4f;

        [Tooltip("单个顾客每天最多触发的思考次数")]
        public int maxThinkCountPerDay = 3;

        [UnityEngine.Header("思考时长")]
        [Tooltip("思考持续时间范围（秒），随机取值")]
        public Vector2 thinkDurationRange = new Vector2(1.0f, 2.0f);

        // 组件缓存
        private CustomerAnimationController animController;
        private IAstarAI ai;

        // 每日计数（顾客每天新生成，Action 实例随之重建，自动重置）
        private int thinkCount;

        // 思考计时
        private float thinkDuration;
        private float thinkTimer;

        protected override string info
        {
            get
            {
                return $"Think? ({thinkProbability * 100:F0}%, max {maxThinkCountPerDay}/day)";
            }
        }

        protected override void OnExecute()
        {
            // 已达每日上限，跳过
            if (thinkCount >= maxThinkCountPerDay)
            {
                EndAction(true);
                return;
            }

            // 概率判定，未触发则跳过
            if (Random.value > thinkProbability)
            {
                EndAction(true);
                return;
            }

            // 获取组件
            animController = agent.GetComponent<CustomerAnimationController>();
            ai = agent.GetComponent<IAstarAI>();

            if (animController == null)
            {
                EndAction(true);
                return;
            }

            // 停下脚步
            if (ai != null)
                ai.isStopped = true;

            // 播放思考表情（循环动画）
            animController.PlayThink();

            // 随机思考时长
            thinkDuration = Random.Range(thinkDurationRange.x, thinkDurationRange.y);
            thinkTimer = 0f;
            thinkCount++;
        }

        protected override void OnUpdate()
        {
            thinkTimer += Time.deltaTime;

            if (thinkTimer >= thinkDuration)
            {
                // 思考结束，停止动画，恢复移动
                animController.StopLoop();

                if (ai != null)
                    ai.isStopped = false;

                EndAction(true);
            }
        }

        protected override void OnStop()
        {
            if (animController != null)
                animController.StopLoop();

            if (ai != null)
                ai.isStopped = false;

            base.OnStop();
        }
    }
}
