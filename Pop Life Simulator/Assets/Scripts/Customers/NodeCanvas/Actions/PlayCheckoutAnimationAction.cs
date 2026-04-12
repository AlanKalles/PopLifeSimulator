using System.Collections;
using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer/Animation")]
    [Description("播放结账动画（有篮子时~1.4秒，无篮子时0.67秒）")]
    public class PlayCheckoutAnimationAction : ActionTask
    {
        private CustomerAnimationController animController;
        private Coroutine waitCoroutine;
        private bool completedNormally;

        protected override string info
        {
            get { return "播放结账动画"; }
        }

        protected override void OnExecute()
        {
            completedNormally = false;

            animController = agent.GetComponent<CustomerAnimationController>();
            if (animController == null)
            {
                Debug.LogError($"[PlayCheckoutAnimationAction] {agent.name} 缺少 CustomerAnimationController 组件！");
                EndAction(false);
                return;
            }

            // 播放结账动画（有篮子时包含篮子抬起/消失序列）
            animController.PlayCheckout();
            Debug.Log($"[PlayCheckoutAnimationAction] {agent.name} 播放结账动画");

            waitCoroutine = StartCoroutine(WaitForAnimationComplete());
        }

        private IEnumerator WaitForAnimationComplete()
        {
            // 等一帧确保 isPlayingOneShot 已被置 true
            yield return null;

            while (animController != null && animController.IsPlayingOneShot)
            {
                yield return null;
            }

            completedNormally = true;
            EndAction(true);
        }

        protected override void OnStop()
        {
            if (waitCoroutine != null)
            {
                StopCoroutine(waitCoroutine);
                waitCoroutine = null;
            }

            // 只在被中断时清理；正常完成时 PlayCheckout 的 Tween 回调已处理
            if (!completedNormally && animController != null)
            {
                animController.StopCurrentAnimation();
                animController.HideBasket();
            }

            base.OnStop();
        }
    }
}
