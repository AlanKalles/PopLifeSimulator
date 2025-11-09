using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer/Animation")]
    [Description("播放结账动画（0.67秒）")]
    public class PlayCheckoutAnimationAction : ActionTask
    {
        private CustomerAnimationController animController;

        protected override string info
        {
            get { return "播放结账动画"; }
        }

        protected override void OnExecute()
        {
            // 获取动画控制器
            animController = agent.GetComponent<CustomerAnimationController>();
            if (animController == null)
            {
                Debug.LogError($"[PlayCheckoutAnimationAction] {agent.name} 缺少 CustomerAnimationController 组件！");
                EndAction(false);
                return;
            }

            // 播放结账动画（0.67秒）
            animController.PlayCheckout();
            Debug.Log($"[PlayCheckoutAnimationAction] {agent.name} 播放结账动画");

            // 立即完成（动画异步播放）
            EndAction(true);
        }
    }
}
