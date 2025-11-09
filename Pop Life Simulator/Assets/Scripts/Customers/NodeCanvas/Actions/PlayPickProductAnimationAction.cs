using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer/Animation")]
    [Description("播放拾取商品动画（仅在购买成功时播放）")]
    public class PlayPickProductAnimationAction : ActionTask
    {
        [BlackboardOnly]
        [Tooltip("上一次购买是否成功的标志（由 ExecutePurchaseAction 设置）")]
        public BBParameter<bool> lastPurchaseSuccessful;

        private CustomerAnimationController animController;

        protected override string info
        {
            get { return "播放拾取商品动画 (if purchase successful)"; }
        }

        protected override void OnExecute()
        {
            // 获取动画控制器
            animController = agent.GetComponent<CustomerAnimationController>();
            if (animController == null)
            {
                Debug.LogError($"[PlayPickProductAnimationAction] {agent.name} 缺少 CustomerAnimationController 组件！");
                EndAction(false);
                return;
            }

            // 检查是否购买成功
            bool purchaseSuccess = lastPurchaseSuccessful != null && lastPurchaseSuccessful.value;

            if (purchaseSuccess)
            {
                // 播放拾取商品动画（0.25秒）
                animController.PlayPickProduct();
                Debug.Log($"[PlayPickProductAnimationAction] {agent.name} 播放拾取商品动画");
            }
            else
            {
                Debug.Log($"[PlayPickProductAnimationAction] {agent.name} 购买失败，跳过拾取动画");
            }

            // 立即完成（动画异步播放）
            EndAction(true);
        }
    }
}
