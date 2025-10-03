using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Runtime;
using PopLife.UI;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer")]
    [Description("Execute checkout at cashier - calls CustomerInteraction.TryCheckout()")]
    public class ExecuteCheckoutAction : ActionTask
    {
        private CustomerInteraction interaction;
        private CustomerBlackboardAdapter blackboard;

        protected override string info
        {
            get { return "Execute checkout at cashier"; }
        }

        protected override void OnExecute()
        {
            // Get components
            interaction = agent.GetComponent<CustomerInteraction>();
            blackboard = agent.GetComponent<CustomerBlackboardAdapter>();

            if (interaction == null)
            {
                Debug.LogError("[ExecuteCheckoutAction] CustomerInteraction component not found");
                EndAction(false);
                return;
            }

            if (blackboard == null)
            {
                Debug.LogError("[ExecuteCheckoutAction] CustomerBlackboardAdapter component not found");
                EndAction(false);
                return;
            }

            // Try to checkout
            bool success = interaction.TryCheckout();

            if (success)
            {
                string msg = "Checkout completed successfully";
                Debug.Log($"[ExecuteCheckoutAction] Customer {blackboard.customerId} {msg}");
                ScreenLogger.LogPurchase(blackboard.customerId, msg);

                // Sync to NodeCanvas blackboard
#if NODECANVAS
                if (blackboard.ncBlackboard != null)
                {
                    blackboard.ncBlackboard.SetVariableValue("pendingPayment", blackboard.pendingPayment);
                }
#endif

                EndAction(true);
            }
            else
            {
                string msg = "Checkout failed";
                Debug.LogWarning($"[ExecuteCheckoutAction] Customer {blackboard.customerId} {msg}");
                ScreenLogger.LogWarning(blackboard.customerId, msg);
                EndAction(false);
            }
        }
    }
}
