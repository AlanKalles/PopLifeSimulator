using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife;
using PopLife.Customers.Runtime;
using PopLife.Customers.Services;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Store")]
    [Description("紧急结账（无需收银台，用于闭店时无收银台的情况）")]
    public class EmergencyCheckoutAction : ActionTask
    {
        protected override string info
        {
            get { return "Emergency Checkout (No Cashier Needed)"; }
        }

        protected override void OnExecute()
        {
            var blackboard = agent.GetComponent<CustomerBlackboardAdapter>();
            if (blackboard == null)
            {
                Debug.LogError("[EmergencyCheckout] 找不到 CustomerBlackboardAdapter");
                EndAction(false);
                return;
            }

            if (blackboard.pendingPayment <= 0)
            {
                Debug.Log($"[EmergencyCheckout] Customer {blackboard.customerId} 没有待结账金额，跳过");
                EndAction(true);
                return;
            }

            Debug.Log($"[EmergencyCheckout] Customer {blackboard.customerId} 紧急结账: ${blackboard.pendingPayment}");

            // 记录销售额到 DayLoopManager
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.RecordSale(blackboard.pendingPayment);
            }

            // 增加玩家金钱
            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.AddMoney(blackboard.pendingPayment);
            }

            // 触发结账事件
            var customerAgent = agent.GetComponent<CustomerAgent>();
            if (customerAgent != null)
            {
                CustomerEventBus.RaiseCheckedOut(customerAgent);
            }

            // 清空待结账金额
            blackboard.pendingPayment = 0;

            // 同步到 NodeCanvas 黑板
#if NODECANVAS
            if (blackboard.ncBlackboard != null)
            {
                blackboard.ncBlackboard.SetVariableValue("pendingPayment", 0);
            }
#endif

            Debug.Log($"[EmergencyCheckout] Customer {blackboard.customerId} 紧急结账完成");
            EndAction(true);
        }
    }
}
