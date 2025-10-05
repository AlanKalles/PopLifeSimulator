using UnityEngine;
using PopLife;
using PopLife.Runtime;
using PopLife.Customers.Services;
using PopLife.Data;
using PopLife.UI;

namespace PopLife.Customers.Runtime
{
    /// <summary>
    /// 处理顾客与货架、设施的碰撞交互
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(CustomerAgent))]
    public class CustomerInteraction : MonoBehaviour
    {
        private CustomerAgent customerAgent;
        private CustomerBlackboardAdapter blackboard;

        // 交互范围
        [Header("交互设置")]
        [SerializeField] private float interactionRadius = 1f;
        [SerializeField] private LayerMask interactableLayer = -1;

        // 当前交互的对象
        private ShelfInstance currentShelf;
        private FacilityInstance currentFacility;

        // 交互状态
        private bool isInteracting = false;
        private float interactionStartTime;

        void Awake()
        {
            customerAgent = GetComponent<CustomerAgent>();
            blackboard = GetComponent<CustomerBlackboardAdapter>();

            // 确保有Collider2D且设置为触发器
            var collider = GetComponent<Collider2D>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            // 检查是否是目标货架
            if (!string.IsNullOrEmpty(blackboard.targetShelfId))
            {
                var shelf = other.GetComponent<ShelfInstance>();
                if (shelf != null && shelf.instanceId == blackboard.targetShelfId)
                {
                    OnReachTargetShelf(shelf);
                }
            }

            // 检查是否是目标收银台
            if (!string.IsNullOrEmpty(blackboard.targetCashierId))
            {
                var facility = other.GetComponent<FacilityInstance>();
                if (facility != null && facility.instanceId == blackboard.targetCashierId)
                {
                    var facilityArchetype = facility.archetype as FacilityArchetype;
                    if (facilityArchetype != null && facilityArchetype.facilityType == FacilityType.Cashier)
                    {
                        OnReachCashier(facility);
                    }
                }
            }
        }

        void OnTriggerExit2D(Collider2D other)
        {
            // 离开货架
            var shelf = other.GetComponent<ShelfInstance>();
            if (shelf != null && shelf == currentShelf)
            {
                OnLeaveShelf();
            }

            // 离开设施
            var facility = other.GetComponent<FacilityInstance>();
            if (facility != null && facility == currentFacility)
            {
                OnLeaveFacility();
            }
        }

        /// <summary>
        /// 到达目标货架
        /// </summary>
        private void OnReachTargetShelf(ShelfInstance shelf)
        {
            currentShelf = shelf;
            isInteracting = true;
            interactionStartTime = Time.time;

            string msg = $"Reached target shelf {shelf.instanceId}";
            Debug.Log($"[CustomerInteraction] Customer {blackboard.customerId} {msg}");
            ScreenLogger.LogCustomerAction(blackboard.customerId, msg);

            // 触发事件
            CustomerEventBus.RaiseReachedShelf(customerAgent, shelf);

        }

        /// <summary>
        /// 离开货架
        /// </summary>
        private void OnLeaveShelf()
        {
            if (currentShelf != null)
            {
                Debug.Log($"[CustomerInteraction] 顾客 {blackboard.customerId} 离开货架 {currentShelf.instanceId}");
                

                currentShelf = null;
                isInteracting = false;
            }
        }

        /// <summary>
        /// 到达收银台
        /// </summary>
        private void OnReachCashier(FacilityInstance cashier)
        {
            currentFacility = cashier;
            isInteracting = true;
            interactionStartTime = Time.time;

            Debug.Log($"[CustomerInteraction] 顾客 {blackboard.customerId} 到达收银台 {cashier.instanceId}");

            // 触发事件
            CustomerEventBus.RaiseReachedCashier(customerAgent, cashier);
            
        }

        /// <summary>
        /// 离开设施
        /// </summary>
        private void OnLeaveFacility()
        {
            if (currentFacility != null)
            {
                Debug.Log($"[CustomerInteraction] 顾客 {blackboard.customerId} 离开设施 {currentFacility.instanceId}");
                

                currentFacility = null;
                isInteracting = false;
            }
        }

        /// <summary>
        /// 执行购买（由行为树调用）- 只扣库存和记录待结账金额，不立即增加玩家金钱
        /// </summary>
        public bool TryPurchase()
        {
            if (currentShelf == null || !isInteracting)
            {
                string msg = "Not at shelf, cannot purchase";
                Debug.LogWarning($"[CustomerInteraction] Customer {blackboard.customerId} {msg}");
                ScreenLogger.LogWarning(blackboard.customerId, msg);
                return false;
            }

            // 检查库存和金钱
            if (currentShelf.currentStock <= 0)
            {
                string msg = $"Shelf {currentShelf.instanceId} out of stock";
                Debug.Log($"[CustomerInteraction] {msg}");
                ScreenLogger.LogWarning(blackboard.customerId, msg);
                return false;
            }

            if (blackboard.moneyBag < currentShelf.currentPrice)
            {
                string msg = $"Insufficient money (need ${currentShelf.currentPrice}, have ${blackboard.moneyBag})";
                Debug.Log($"[CustomerInteraction] Customer {blackboard.customerId} {msg}");
                ScreenLogger.LogWarning(blackboard.customerId, msg);
                return false;
            }

            // 执行取货 - 只扣库存，不增加玩家金钱
            if (currentShelf.TryTakeOne())
            {
                // 扣除顾客钱包
                blackboard.moneyBag -= currentShelf.currentPrice;
                // 累加待结账金额
                blackboard.pendingPayment += currentShelf.currentPrice;

                string msg = $"Took item, pending payment: ${blackboard.pendingPayment}, remaining money: ${blackboard.moneyBag}";
                Debug.Log($"[CustomerInteraction] Customer {blackboard.customerId} {msg}");
                ScreenLogger.LogPurchase(blackboard.customerId, msg);

                // 触发购买事件（此时只是拿货，未结账）
                CustomerEventBus.RaisePurchased(customerAgent, currentShelf, 1, currentShelf.currentPrice);

                return true;
            }

            return false;
        }

        /// <summary>
        /// 执行结账（由行为树调用）- 在收银台结算待结账金额
        /// </summary>
        public bool TryCheckout()
        {
            if (currentFacility == null || !isInteracting)
            {
                string msg = "Not at cashier, cannot checkout";
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

            string logMsg = $"Checkout completed, paid ${blackboard.pendingPayment}";
            Debug.Log($"[CustomerInteraction] Customer {blackboard.customerId} {logMsg}");
            ScreenLogger.LogPurchase(blackboard.customerId, logMsg);

            // 清空待结账金额
            blackboard.pendingPayment = 0;

            // 触发结账事件
            CustomerEventBus.RaiseCheckedOut(customerAgent);

            return true;
        }

        /// <summary>
        /// 手动检查交互范围内的对象
        /// </summary>
        public void CheckForInteractables()
        {
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, interactionRadius, interactableLayer);

            foreach (var collider in colliders)
            {
                // 检查货架
                var shelf = collider.GetComponent<ShelfInstance>();
                if (shelf != null && shelf.instanceId == blackboard.targetShelfId && currentShelf == null)
                {
                    OnReachTargetShelf(shelf);
                    return;
                }

                // 检查收银台
                var facility = collider.GetComponent<FacilityInstance>();
                if (facility != null && facility.instanceId == blackboard.targetCashierId && currentFacility == null)
                {
                    var facilityArchetype = facility.archetype as FacilityArchetype;
                    if (facilityArchetype != null && facilityArchetype.facilityType == FacilityType.Cashier)
                    {
                        OnReachCashier(facility);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 获取当前交互的货架
        /// </summary>
        public ShelfInstance GetCurrentShelf() => currentShelf;

        /// <summary>
        /// 获取当前交互的设施
        /// </summary>
        public FacilityInstance GetCurrentFacility() => currentFacility;

        /// <summary>
        /// 是否正在交互
        /// </summary>
        public bool IsInteracting => isInteracting;

        /// <summary>
        /// 交互持续时间
        /// </summary>
        public float InteractionDuration => isInteracting ? (Time.time - interactionStartTime) : 0f;

        void OnDrawGizmosSelected()
        {
            // 绘制交互范围
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }
    }
}