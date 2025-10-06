using NodeCanvas.Framework;
using ParadoxNotion.Design;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Conditions
{
    [Category("PopLife/Store")]
    [Description("检查商店是否闭店")]
    public class CheckStoreClosingCondition : ConditionTask
    {
        protected override string info
        {
            get { return "Is Store Closing?"; }
        }

        protected override bool OnCheck()
        {
            var blackboard = agent.GetComponent<CustomerBlackboardAdapter>();
            if (blackboard == null)
            {
                return false;
            }

            return blackboard.isClosingTime;
        }
    }
}
