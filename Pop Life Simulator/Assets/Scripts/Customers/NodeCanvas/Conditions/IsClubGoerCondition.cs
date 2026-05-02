using NodeCanvas.Framework;
using ParadoxNotion.Design;
using PopLife.Customers.Data;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Conditions
{
    [Category("PopLife/Store")]
    [Description("Checks whether this customer is routed to the club.")]
    public class IsClubGoerCondition : ConditionTask
    {
        protected override string info => "Is Club Goer?";

        protected override bool OnCheck()
        {
            var blackboard = agent.GetComponent<CustomerBlackboardAdapter>();
            return blackboard != null && blackboard.visitPurpose == CustomerVisitPurpose.Club;
        }
    }
}
