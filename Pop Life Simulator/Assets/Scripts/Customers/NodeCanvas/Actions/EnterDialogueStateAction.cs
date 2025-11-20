using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer/Interaction")]
    [Description("让顾客进入对话等待状态")]
    public class EnterDialogueStateAction : ActionTask
    {
        private CustomerInteractionState interactionState;

        protected override string info
        {
            get { return "Enter Dialogue State"; }
        }

        protected override void OnExecute()
        {
            // 获取交互状态组件
            interactionState = agent.GetComponent<CustomerInteractionState>();
            if (interactionState == null)
            {
                Debug.LogError($"[EnterDialogueStateAction] {agent.name} 缺少 CustomerInteractionState 组件！");
                EndAction(false);
                return;
            }

            // 进入对话状态
            interactionState.EnterDialogueState();

            Debug.Log($"[EnterDialogueStateAction] {agent.name} 进入对话等待状态");
            EndAction(true);
        }
    }
}
