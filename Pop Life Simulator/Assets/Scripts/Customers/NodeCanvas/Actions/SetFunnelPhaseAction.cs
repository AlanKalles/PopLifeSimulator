using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Data;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer")]
    [Description("设置当前漏斗阶段到黑板")]
    public class SetFunnelPhaseAction : ActionTask
    {
        [Tooltip("要设置的漏斗阶段")]
        public FunnelPhase phase;

        protected override string info
        {
            get { return $"设置漏斗阶段: {phase}"; }
        }

        protected override void OnExecute()
        {
            var adapter = agent.GetComponent<CustomerBlackboardAdapter>();
            if (adapter == null)
            {
                Debug.LogError("[SetFunnelPhaseAction] 找不到 CustomerBlackboardAdapter");
                EndAction(false);
                return;
            }

            adapter.currentFunnelPhase = phase;

            Debug.Log($"[SetFunnelPhaseAction] 顾客 {adapter.customerId} 漏斗阶段设置为 {phase}");
            EndAction(true);
        }
    }
}
