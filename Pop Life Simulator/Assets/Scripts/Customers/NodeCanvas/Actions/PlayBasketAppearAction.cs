using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using Pathfinding;
using PopLife.Customers.Data;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer/Animation")]
    [Description("播放篮子出现动画（进店后，停AI→手臂抬起→篮子出现→手臂回位→恢复AI）")]
    public class PlayBasketAppearAction : ActionTask
    {
        private CustomerAnimationController animController;
        private IAstarAI ai;
        private bool wasAIStopped;
        private bool completedNormally;

        protected override string info
        {
            get { return "播放篮子出现动画"; }
        }

        protected override void OnExecute()
        {
            completedNormally = false;

            // Club 顾客不购物也不结账，直接跳过篮子动画
            var blackboard = agent.GetComponent<CustomerBlackboardAdapter>();
            if (blackboard != null && blackboard.visitPurpose == CustomerVisitPurpose.Club)
            {
                completedNormally = true;
                EndAction(true);
                return;
            }

            animController = agent.GetComponent<CustomerAnimationController>();
            if (animController == null)
            {
                Debug.LogError($"[PlayBasketAppearAction] {agent.name} 缺少 CustomerAnimationController！");
                EndAction(false);
                return;
            }

            ai = agent.GetComponent<IAstarAI>();
            wasAIStopped = ai?.isStopped ?? false;
            if (ai != null) ai.isStopped = true;

            animController.PlayBasketAppear();

            // 篮子系统未启用时 PlayBasketAppear 立即返回，isPlayingOneShot 为 false
            // → 静默跳过，恢复 AI
            if (!animController.IsPlayingOneShot)
            {
                if (ai != null) ai.isStopped = wasAIStopped;
                completedNormally = true;
                EndAction(true);
                return;
            }
        }

        protected override void OnUpdate()
        {
            if (animController == null || !animController.IsPlayingOneShot)
            {
                // 动画播放完成
                if (ai != null) ai.isStopped = wasAIStopped;
                completedNormally = true;
                EndAction(true);
            }
        }

        protected override void OnStop()
        {
            // 只在被中断时清理篮子；正常完成后篮子应保留
            if (!completedNormally)
            {
                animController?.StopCurrentAnimation();
                animController?.HideBasket();
            }

            if (ai != null) ai.isStopped = wasAIStopped;
            base.OnStop();
        }
    }
}
