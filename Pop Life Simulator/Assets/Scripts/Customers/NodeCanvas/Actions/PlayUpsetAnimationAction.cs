using System.Collections;
using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer/Animation")]
    [Description("播放 upset 动画（0.85秒）- 用于购买失败或找不到商品时")]
    public class PlayUpsetAnimationAction : ActionTask
    {
        [Tooltip("是否等待动画播放完成（默认 true）")]
        public bool waitForCompletion = true;

        [Tooltip("动画持续时间（秒），默认 0.85秒")]
        public float animationDuration = 0.85f;

        private CustomerAnimationController animController;
        private Coroutine animationCoroutine;

        protected override string info
        {
            get { return waitForCompletion ? "播放 upset 动画并等待" : "播放 upset 动画"; }
        }

        protected override void OnExecute()
        {
            // 获取动画控制器
            animController = agent.GetComponent<CustomerAnimationController>();
            if (animController == null)
            {
                Debug.LogError($"[PlayUpsetAnimationAction] {agent.name} 缺少 CustomerAnimationController 组件！");
                EndAction(false);
                return;
            }

            // 播放 upset 动画
            animController.PlayUpset();
            Debug.Log($"[PlayUpsetAnimationAction] {agent.name} 播放 upset 动画，时长: {animationDuration} 秒");

            if (waitForCompletion)
            {
                // 启动协程等待动画播放完成
                animationCoroutine = StartCoroutine(WaitForAnimation());
            }
            else
            {
                // 不等待，直接完成
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
            Debug.Log($"[PlayUpsetAnimationAction] {agent.name} upset 动画播放完成");
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
