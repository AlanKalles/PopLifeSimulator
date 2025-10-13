using UnityEngine;
using PopLife;
using PopLife.Runtime;
using PopLife.Customers.Services;
using PopLife.Data;
using PopLife.UI;

namespace PopLife.Customers.Runtime
{
    /// <summary>
    /// 处理顾客与货架、设施的交互（基于寻路到达 + ID直接查找，不依赖碰撞体）
    /// </summary>
    [RequireComponent(typeof(CustomerAgent))]
    public class CustomerInteraction : MonoBehaviour
    {
        private CustomerAgent customerAgent;
        private CustomerBlackboardAdapter blackboard;

        void Awake()
        {
            customerAgent = GetComponent<CustomerAgent>();
            blackboard = GetComponent<CustomerBlackboardAdapter>();
        }

        /// <summary>
        /// 执行购买（由行为树调用）- 基于 targetShelfId 查找货架
        /// </summary>
        public bool TryPurchase()
        {
            // 通过 targetShelfId 查找货架
            var targetShelf = FindShelfById(blackboard.targetShelfId);

            if (targetShelf == null)
            {
                string msg = $"Cannot find shelf {blackboard.targetShelfId}, cannot purchase";
                Debug.LogWarning($"[CustomerInteraction] Customer {blackboard.customerId} {msg}");
                ScreenLogger.LogWarning(blackboard.customerId, msg);
                return false;
            }

            // 检查库存和金钱
            if (targetShelf.currentStock <= 0)
            {
                string msg = $"Shelf {targetShelf.instanceId} out of stock";
                Debug.Log($"[CustomerInteraction] {msg}");
                ScreenLogger.LogWarning(blackboard.customerId, msg);
                return false;
            }

            if (blackboard.moneyBag < targetShelf.currentPrice)
            {
                string msg = $"Insufficient money (need ${targetShelf.currentPrice}, have ${blackboard.moneyBag})";
                Debug.Log($"[CustomerInteraction] Customer {blackboard.customerId} {msg}");
                ScreenLogger.LogWarning(blackboard.customerId, msg);
                return false;
            }

            // 执行取货 - 只扣库存，不增加玩家金钱
            if (targetShelf.TryTakeOne())
            {
                // 扣除顾客钱包
                blackboard.moneyBag -= targetShelf.currentPrice;
                // 累加待结账金额
                blackboard.pendingPayment += targetShelf.currentPrice;

                string msg = $"Took item from {targetShelf.instanceId}, pending payment: ${blackboard.pendingPayment}, remaining money: ${blackboard.moneyBag}";
                Debug.Log($"[CustomerInteraction] Customer {blackboard.customerId} {msg}");
                ScreenLogger.LogPurchase(blackboard.customerId, msg);

                // 记录到当前会话
                if (customerAgent.currentSession != null)
                {
                    var shelfArchetype = targetShelf.archetype as ShelfArchetype;
                    customerAgent.currentSession.visitedShelves.Add(new ShelfVisit
                    {
                        shelfId = targetShelf.instanceId,
                        categoryIndex = shelfArchetype != null ? (int)shelfArchetype.category : 0,
                        boughtQty = 1
                    });
                }

                // 触发购买事件（此时只是拿货，未结账）
                CustomerEventBus.RaisePurchased(customerAgent, targetShelf, 1, targetShelf.currentPrice);

                return true;
            }

            return false;
        }

        /// <summary>
        /// 执行结账（由行为树调用）- 基于 targetCashierId 查找收银台
        /// </summary>
        public bool TryCheckout()
        {
            // 通过 targetCashierId 查找收银台
            var targetCashier = FindCashierById(blackboard.targetCashierId);

            if (targetCashier == null)
            {
                string msg = $"Cannot find cashier {blackboard.targetCashierId}, cannot checkout";
                Debug.LogWarning($"[CustomerInteraction] Customer {blackboard.customerId} {msg}");
                ScreenLogger.LogWarning(blackboard.customerId, msg);
                return false;
            }

            // 检查是否有待结账金额
            if (blackboard.pendingPayment <= 0)
            {
                string msg = "No pending payment, checkout skipped";
                Debug.LogWarning($"[CustomerInteraction] Customer {blackboard.customerId} {msg}");
                ScreenLogger.LogWarning(blackboard.customerId, msg);
                return false;
            }

            // 记录销售额到 DayLoopManager
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.RecordSale(blackboard.pendingPayment);
            }

            // 结账 - 增加玩家金钱
            PopLife.ResourceManager.Instance.AddMoney(blackboard.pendingPayment);

            string logMsg = $"Checkout completed at {targetCashier.instanceId}, paid ${blackboard.pendingPayment}";
            Debug.Log($"[CustomerInteraction] Customer {blackboard.customerId} {logMsg}");
            ScreenLogger.LogPurchase(blackboard.customerId, logMsg);

            // 记录到当前会话
            if (customerAgent.currentSession != null)
            {
                customerAgent.currentSession.moneySpent += blackboard.pendingPayment;
            }

            // 清空待结账金额
            blackboard.pendingPayment = 0;

            // 触发结账事件
            CustomerEventBus.RaiseCheckedOut(customerAgent);

            return true;
        }

        /// <summary>
        /// 根据ID查找货架
        /// </summary>
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

        /// <summary>
        /// 根据ID查找收银台
        /// </summary>
        private FacilityInstance FindCashierById(string cashierId)
        {
            if (string.IsNullOrEmpty(cashierId))
                return null;

            var allFacilities = Object.FindObjectsByType<FacilityInstance>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var facility in allFacilities)
            {
                if (facility.instanceId == cashierId)
                {
                    var facilityArchetype = facility.archetype as FacilityArchetype;
                    if (facilityArchetype != null && facilityArchetype.facilityType == FacilityType.Cashier)
                    {
                        return facility;
                    }
                }
            }

            return null;
        }
    }
}
