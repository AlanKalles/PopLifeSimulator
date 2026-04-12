using System.Collections;
using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer/Animation")]
    [Description("播放拾取商品动画（仅在购买成功时播放，自动等待动画结束）")]
    public class PlayPickProductAnimationAction : ActionTask
    {
        [BlackboardOnly]
        [Tooltip("上一次购买是否成功的标志（由 ExecutePurchaseAction 设置）")]
        public BBParameter<bool> lastPurchaseSuccessful;

        private CustomerAnimationController animController;
        private Coroutine waitCoroutine;

        protected override string info
        {
            get { return "播放拾取商品动画 (auto wait)"; }
        }

        protected override void OnExecute()
        {
            animController = agent.GetComponent<CustomerAnimationController>();
            if (animController == null)
            {
                Debug.LogError($"[PlayPickProductAnimationAction] {agent.name} 缺少 CustomerAnimationController 组件！");
                EndAction(false);
                return;
            }

            bool purchaseSuccess = lastPurchaseSuccessful != null && lastPurchaseSuccessful.value;

            if (purchaseSuccess)
            {
                animController.PlayPickProduct();

                // 首次成功购买 → 篮子切满
                if (!animController.IsBasketFull)
                    animController.SetBasketFull();

                // 轮询等待动画系统自行结束，无需手动指定时长
                waitCoroutine = StartCoroutine(WaitForAnimationComplete());
            }
            else
            {
                EndAction(true);
            }
        }

        private IEnumerator WaitForAnimationComplete()
        {
            // 等一帧确保 isPlayingOneShot 已被置 true
            yield return null;

            while (animController != null && animController.IsPlayingOneShot)
            {
                yield return null;
            }

            EndAction(true);
        }

        protected override void OnStop()
        {
            if (waitCoroutine != null)
            {
                StopCoroutine(waitCoroutine);
                waitCoroutine = null;
            }

            if (animController != null)
            {
                animController.StopCurrentAnimation();
            }

            base.OnStop();
        }
    }
}
