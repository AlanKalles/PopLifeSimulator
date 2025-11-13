using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Conditions
{
    [Category("PopLife/Customer")]
    [Description("检查顾客是否处于 upset 状态（找不到商品/购买失败）")]
    public class CheckIsUpsetCondition : ConditionTask
    {
        protected override string info
        {
            get { return "Is Customer Upset?"; }
        }

        protected override bool OnCheck()
        {
            var blackboard = agent.GetComponent<CustomerBlackboardAdapter>();
            if (blackboard == null)
            {
                Debug.LogError("[CheckIsUpsetCondition] CustomerBlackboardAdapter not found!");
                return false;
            }

            bool isUpset = blackboard.isUpset;
            Debug.Log($"[CheckIsUpsetCondition] 顾客 {blackboard.customerId} - isUpset={isUpset}, pendingPayment={blackboard.pendingPayment}");

            return isUpset;
        }
    }
}
