using NodeCanvas.Framework;
using ParadoxNotion.Design;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Conditions
{
    [Category("PopLife/Customer")]
    [Description("检查是否有待结账金额")]
    public class CheckPendingPaymentCondition : ConditionTask
    {
        protected override string info
        {
            get { return "Has Pending Payment?"; }
        }

        protected override bool OnCheck()
        {
            var blackboard = agent.GetComponent<CustomerBlackboardAdapter>();
            if (blackboard == null)
            {
                return false;
            }

            return blackboard.pendingPayment > 0;
        }
    }
}
