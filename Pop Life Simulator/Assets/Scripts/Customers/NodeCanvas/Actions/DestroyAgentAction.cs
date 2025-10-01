using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer")]
    [Description("销毁顾客GameObject - 用于离店")]
    public class DestroyAgentAction : ActionTask
    {
        [Tooltip("延迟销毁时间（秒）")]
        public float delay = 0f;

        protected override string info
        {
            get { return "销毁顾客"; }
        }

        protected override void OnExecute()
        {
            var blackboard = agent.GetComponent<CustomerBlackboardAdapter>();

            if (blackboard != null)
            {
                Debug.Log($"[DestroyAgentAction] 顾客 {blackboard.customerId} 离店");
            }

            if (delay > 0)
            {
                GameObject.Destroy(agent.gameObject, delay);
            }
            else
            {
                GameObject.Destroy(agent.gameObject);
            }

            EndAction(true);
        }
    }
}
