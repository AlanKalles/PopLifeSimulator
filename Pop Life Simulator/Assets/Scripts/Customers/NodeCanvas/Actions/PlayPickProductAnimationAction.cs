using System.Collections;
using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer/Animation")]
    [Description("播放拾取商品动画（整合了思考动作，仅在购买成功时播放）")]
    public class PlayPickProductAnimationAction : ActionTask
    {
        [BlackboardOnly]
        [Tooltip("上一次购买是否成功的标志（由 ExecutePurchaseAction 设置）")]
        public BBParameter<bool> lastPurchaseSuccessful;

        [Tooltip("动画持续时间（秒），需要根据实际动画长度调整")]
        public float animationDuration = 0.8f;

        private CustomerAnimationController animController;
        private Coroutine animationCoroutine;

        protected override string info
        {
            get { return "播放拾取商品动画并等待 (if purchase successful)"; }
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
                // 播放拾取商品动画（整合了think动画）
                animController.PlayPickProduct();
                Debug.Log($"[PlayPickProductAnimationAction] {agent.name} 播放拾取商品动画，等待 {animationDuration} 秒");

                // 启动协程等待动画播放完成（保存引用以便中断时停止）
                animationCoroutine = StartCoroutine(WaitForAnimation());
            }
            else
            {
                Debug.Log($"[PlayPickProductAnimationAction] {agent.name} 购买失败，跳过拾取动画");
                EndAction(true);
            }
        }

        /// <summary>
        /// 等待动画播放完成
        /// </summary>
        private IEnumerator WaitForAnimation()
        {
            yield return new WaitForSeconds(animationDuration);

            // 动画播放完成
            Debug.Log($"[PlayPickProductAnimationAction] {agent.name} 拾取商品动画播放完成");
            EndAction(true);
        }

        protected override void OnStop()
        {
            // 节点被中断时，停止协程和动画
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }

            if (animController != null)
            {
                animController.StopCurrentAnimation();
            }

            base.OnStop();
        }
    }
}
