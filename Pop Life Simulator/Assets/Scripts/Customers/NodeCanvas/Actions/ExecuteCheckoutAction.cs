using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Runtime;
using PopLife.Customers.Services;
using PopLife.Runtime;
using PopLife.UI;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer")]
    [Description("Execute checkout at cashier - calls CustomerInteraction.TryCheckout()")]
    public class ExecuteCheckoutAction : ActionTask
    {
        private CustomerInteraction interaction;
        private CustomerBlackboardAdapter customerBlackboard;

        protected override string info
        {
            get { return "Execute checkout at cashier"; }
        }

        protected override void OnExecute()
        {
            // Get components
            interaction = agent.GetComponent<CustomerInteraction>();
            customerBlackboard = agent.GetComponent<CustomerBlackboardAdapter>();

            if (interaction == null)
            {
                Debug.LogError("[ExecuteCheckoutAction] CustomerInteraction component not found");
                EndAction(false);
                return;
            }

            if (customerBlackboard == null)
            {
                Debug.LogError("[ExecuteCheckoutAction] CustomerBlackboardAdapter component not found");
                EndAction(false);
                return;
            }

            // 触发到达收银台事件（在结账前锁定消费金额）
            var customerAgent = agent.GetComponent<CustomerAgent>();
            if (customerAgent != null)
            {
                // 查找收银台实例
                FacilityInstance cashier = FindCashierById(customerBlackboard.targetCashierId);
                CustomerEventBus.RaiseReachedCashier(customerAgent, cashier);
            }

            // Try to checkout
            bool success = interaction.TryCheckout();

            if (success)
            {
                string msg = "Checkout completed successfully";
                Debug.Log($"[ExecuteCheckoutAction] Customer {customerBlackboard.customerId} {msg}");
                ScreenLogger.LogPurchase(customerBlackboard.customerId, msg);

                // Sync to NodeCanvas blackboard
#if NODECANVAS
                if (customerBlackboard.ncBlackboard != null)
                {
                    customerBlackboard.ncBlackboard.SetVariableValue("pendingPayment", customerBlackboard.pendingPayment);
                }
#endif

                EndAction(true);
            }
            else
            {
                string msg = "Checkout failed";
                Debug.LogWarning($"[ExecuteCheckoutAction] Customer {customerBlackboard.customerId} {msg}");
                ScreenLogger.LogWarning(customerBlackboard.customerId, msg);
                EndAction(false);
            }
        }

        /// <summary>
        /// 根据 ID 查找收银台实例
        /// </summary>
        private FacilityInstance FindCashierById(string cashierId)
        {
            if (string.IsNullOrEmpty(cashierId))
                return null;

            var floors = Object.FindObjectsByType<FloorGrid>(FindObjectsSortMode.None);
            foreach (var floor in floors)
            {
                foreach (var building in floor.AllBuildings())
                {
                    if (building is FacilityInstance facility && facility.instanceId == cashierId)
                    {
                        return facility;
                    }
                }
            }

            return null;
        }
    }
}
