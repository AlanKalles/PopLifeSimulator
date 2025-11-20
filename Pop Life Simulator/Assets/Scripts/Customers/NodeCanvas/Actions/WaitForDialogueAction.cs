using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer/Interaction")]
    [Description("等待对话状态结束（被点击触发对话/超时）")]
    public class WaitForDialogueAction : ActionTask
    {
        private CustomerInteractionState interactionState;

        protected override string info
        {
            get { return "Wait For Dialogue State End"; }
        }

        protected override void OnExecute()
        {
            // 获取交互状态组件
            interactionState = agent.GetComponent<CustomerInteractionState>();
            if (interactionState == null)
            {
                Debug.LogError($"[WaitForDialogueAction] {agent.name} 缺少 CustomerInteractionState 组件！");
                EndAction(false);
                return;
            }

            // 如果不在对话状态，直接结束
            if (!interactionState.IsInDialogueState)
            {
                Debug.Log($"[WaitForDialogueAction] {agent.name} 不在对话状态，立即结束");
                EndAction(true);
            }
        }

        protected override void OnUpdate()
        {
            // 检查对话状态是否结束
            if (interactionState == null || !interactionState.IsInDialogueState)
            {
                Debug.Log($"[WaitForDialogueAction] {agent.name} 对话状态结束");
                EndAction(true);
            }
        }

        protected override void OnStop()
        {
            // 清理：如果行为树被中断，确保顾客退出对话状态
            if (interactionState != null && interactionState.IsInDialogueState)
            {
                interactionState.ExitDialogueState();
                Debug.Log($"[WaitForDialogueAction] {agent.name} 行为树中断，强制退出对话状态");
            }
        }
    }
}
