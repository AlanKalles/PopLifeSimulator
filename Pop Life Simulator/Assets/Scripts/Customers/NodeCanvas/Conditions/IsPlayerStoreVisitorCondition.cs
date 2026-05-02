using NodeCanvas.Framework;
using ParadoxNotion.Design;
using PopLife.Customers.Data;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Conditions
{
    [Category("PopLife/Store")]
    [Description("Checks whether this customer is routed to the player store.")]
    public class IsPlayerStoreVisitorCondition : ConditionTask
    {
        protected override string info => "Is Player Store Visitor?";

        protected override bool OnCheck()
        {
            var blackboard = agent.GetComponent<CustomerBlackboardAdapter>();
            return blackboard != null && blackboard.visitPurpose == CustomerVisitPurpose.PlayerStore;
        }
    }
}
