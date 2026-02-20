using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Data;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer")]
    [Description("从 FunnelTargetSelector 获取当前阶段的循环上限")]
    public class GetFunnelLoopCountAction : ActionTask
    {
        [Tooltip("要查询的漏斗阶段")]
        public FunnelPhase phase;

        [BlackboardOnly]
        public BBParameter<BehaviorPolicySet> policies;

        [BlackboardOnly]
        [Tooltip("输出：当前阶段的最大循环次数")]
        public BBParameter<int> maxLoops;

        protected override string info
        {
            get { return $"获取 {phase} 阶段循环上限"; }
        }

        protected override void OnExecute()
        {
            var adapter = agent.GetComponent<CustomerBlackboardAdapter>();
            if (adapter == null)
            {
                Debug.LogError("[GetFunnelLoopCountAction] 找不到 CustomerBlackboardAdapter");
                maxLoops.value = 1;
                EndAction(false);
                return;
            }

            var policySet = policies.value;
            if (policySet == null || policySet.targetSelector == null)
            {
                Debug.LogWarning("[GetFunnelLoopCountAction] 策略集或目标选择策略为空，使用默认值 1");
                maxLoops.value = 1;
                EndAction(true);
                return;
            }

            // 尝试转换为 FunnelTargetSelector
            var funnelSelector = policySet.targetSelector as FunnelTargetSelector;
            if (funnelSelector == null)
            {
                Debug.LogWarning("[GetFunnelLoopCountAction] targetSelector 不是 FunnelTargetSelector，使用默认值 1");
                maxLoops.value = 1;
                EndAction(true);
                return;
            }

            int loops = funnelSelector.GetMaxLoops(phase, adapter.loyaltyLevel);
            maxLoops.value = loops;

            Debug.Log($"[GetFunnelLoopCountAction] 顾客 {adapter.customerId} 阶段 {phase} 循环上限: {loops} (忠诚度: {adapter.loyaltyLevel})");
            EndAction(true);
        }
    }
}
