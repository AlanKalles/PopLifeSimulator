using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Data;
using PopLife.Customers.Services;
using PopLife.Customers.Runtime;
using PopLife.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer")]
    [Description("使用购买策略决定购买数量")]
    public class DecidePurchaseAction : ActionTask
    {
        [BlackboardOnly]
        public BBParameter<BehaviorPolicySet> policies;

        [BlackboardOnly]
        public BBParameter<string> targetShelfId;

        [BlackboardOnly]
        public BBParameter<int> moneyBag;

        [BlackboardOnly]
        public BBParameter<int> purchaseQuantity;

        protected override string info
        {
            get { return "决定购买数量"; }
        }

        protected override void OnExecute()
        {
            // 获取组件
            var adapter = agent.GetComponent<CustomerBlackboardAdapter>();
            if (adapter == null)
            {
                Debug.LogError("[DecidePurchaseAction] 找不到 CustomerBlackboardAdapter");
                EndAction(false);
                return;
            }

            // 获取策略
            var policySet = policies.value;
            if (policySet == null || policySet.purchase == null)
            {
                Debug.LogError("[DecidePurchaseAction] 策略集或购买策略为空");
                EndAction(false);
                return;
            }

            // 查找目标货架
            var shelf = FindShelfById(targetShelfId.value);
            if (shelf == null)
            {
                Debug.LogError($"[DecidePurchaseAction] 找不到货架 {targetShelfId.value}");
                EndAction(false);
                return;
            }

            // 构建上下文
            var customerContext = CustomerContextBuilder.BuildCustomerContext(adapter);
            var shelfSnapshot = CustomerContextBuilder.BuildShelfSnapshot(shelf);

            // 使用策略决定购买数量
            int qty = policySet.purchase.DecidePurchaseQty(
                customerContext,
                shelfSnapshot,
                moneyBag.value,
                shelf.currentPrice
            );

            if (qty <= 0)
            {
                Debug.Log($"[DecidePurchaseAction] 顾客 {adapter.customerId} 决定不购买");
                purchaseQuantity.value = 0;
                EndAction(false);
                return;
            }

            // 软预留商品
            var ticket = CommerceService.Instance.SoftReserve(shelf, qty);
            purchaseQuantity.value = ticket.qty;

            Debug.Log($"[DecidePurchaseAction] 顾客 {adapter.customerId} 决定购买 {ticket.qty} 个商品");

            EndAction(true);
        }

        private ShelfInstance FindShelfById(string shelfId)
        {
            if (string.IsNullOrEmpty(shelfId))
                return null;

            var allShelves = Object.FindObjectsByType<ShelfInstance>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var shelf in allShelves)
            {
                if (shelf.instanceId == shelfId)
                    return shelf;
            }

            return null;
        }
    }
}
