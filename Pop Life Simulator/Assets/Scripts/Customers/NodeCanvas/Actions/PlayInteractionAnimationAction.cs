using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer/Animation")]
    [Description("播放交互动画（占位节点，未来用于社交互动）")]
    public class PlayInteractionAnimationAction : ActionTask
    {
        [Tooltip("交互动画持续时间（秒）")]
        public float interactionDuration = 2.0f;

        private CustomerAnimationController animController;
        private float timer;
        private bool isPlaying;

        protected override string info
        {
            get { return "播放交互动画 (占位)"; }
        }

        protected override void OnExecute()
        {
            // 获取动画控制器
            animController = agent.GetComponent<CustomerAnimationController>();
            if (animController == null)
            {
                Debug.LogError($"[PlayInteractionAnimationAction] {agent.name} 缺少 CustomerAnimationController 组件！");
                EndAction(false);
                return;
            }

            // 播放交互动画（循环）
            animController.PlayInteraction();
            isPlaying = true;
            timer = 0f;

            Debug.Log($"[PlayInteractionAnimationAction] {agent.name} 播放交互动画（占位实现）");
        }

        protected override void OnUpdate()
        {
            if (!isPlaying)
            {
                EndAction(false);
                return;
            }

            // 计时
            timer += Time.deltaTime;

            if (timer >= interactionDuration)
            {
                // 停止交互动画
                if (animController != null)
                {
                    animController.StopLoop();
                }

                Debug.Log($"[PlayInteractionAnimationAction] {agent.name} 交互动画结束");
                EndAction(true);
            }
        }

        protected override void OnStop()
        {
            // 清理：停止动画
            if (animController != null)
            {
                animController.StopLoop();
            }
        }
    }
}
