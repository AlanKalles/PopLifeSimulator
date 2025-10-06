using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Runtime;
using PopLife.Customers.Services;
using PopLife.Runtime;

namespace PopLife.Customers.NodeCanvas.Conditions
{
    [Category("PopLife/Customer/Queue")]
    [Description("检查顾客是否在队列的队首位置（位置0）")]
    public class IsAtFrontOfQueueCondition : ConditionTask
    {
        [BlackboardOnly]
        [Tooltip("目标货架ID（用于检查货架队列）")]
        public BBParameter<string> targetShelfId;

        [BlackboardOnly]
        [Tooltip("目标收银台ID（用于检查收银台队列）")]
        public BBParameter<string> targetCashierId;

        private CustomerBlackboardAdapter blackboard;

        protected override string info
        {
            get { return "检查是否在队首"; }
        }

        protected override bool OnCheck()
        {
            blackboard = agent.GetComponent<CustomerBlackboardAdapter>();
            if (blackboard == null)
            {
                Debug.LogError("[IsAtFrontOfQueueCondition] 找不到 CustomerBlackboardAdapter");
                return false;
            }

            // 优先检查货架队列
            if (!string.IsNullOrEmpty(targetShelfId.value))
            {
                return CheckShelfQueuePosition(targetShelfId.value);
            }
            // 其次检查收银台队列
            else if (!string.IsNullOrEmpty(targetCashierId.value))
            {
                return CheckCashierQueuePosition(targetCashierId.value);
            }

            Debug.LogWarning($"[IsAtFrontOfQueueCondition] 顾客 {blackboard.customerId} 没有指定目标");
            return false;
        }

        /// <summary>
        /// 检查货架队列位置
        /// </summary>
        private bool CheckShelfQueuePosition(string shelfId)
        {
            var shelf = FindShelfById(shelfId);
            if (shelf == null)
            {
                Debug.LogWarning($"[IsAtFrontOfQueueCondition] 找不到货架 {shelfId}");
                return false;
            }

            var queueController = shelf.GetComponent<ShelfQueueController>();
            if (queueController == null)
            {
                Debug.LogWarning($"[IsAtFrontOfQueueCondition] 货架 {shelfId} 没有 ShelfQueueController");
                return false;
            }

            int position = queueController.GetQueuePosition(blackboard.customerId);
            bool isAtFront = position == 0;

            if (!isAtFront)
            {
                Debug.Log($"[IsAtFrontOfQueueCondition] 顾客 {blackboard.customerId} 在货架队列位置 {position}，等待前移...");
            }
            else
            {
                Debug.Log($"[IsAtFrontOfQueueCondition] 顾客 {blackboard.customerId} 已到达货架队首");
            }

            return isAtFront;
        }

        /// <summary>
        /// 检查收银台队列位置
        /// </summary>
        private bool CheckCashierQueuePosition(string cashierId)
        {
            var cashier = FindFacilityById(cashierId);
            if (cashier == null)
            {
                Debug.LogWarning($"[IsAtFrontOfQueueCondition] 找不到收银台 {cashierId}");
                return false;
            }

            var queueController = cashier.GetComponent<CashierQueueController>();
            if (queueController == null)
            {
                Debug.LogWarning($"[IsAtFrontOfQueueCondition] 收银台 {cashierId} 没有 CashierQueueController");
                return false;
            }

            int position = queueController.GetQueuePosition(blackboard.customerId);
            bool isAtFront = position == 0;

            if (!isAtFront)
            {
                Debug.Log($"[IsAtFrontOfQueueCondition] 顾客 {blackboard.customerId} 在收银台队列位置 {position}，等待前移...");
            }
            else
            {
                Debug.Log($"[IsAtFrontOfQueueCondition] 顾客 {blackboard.customerId} 已到达收银台队首");
            }

            return isAtFront;
        }

        /// <summary>
        /// 根据ID查找货架
        /// </summary>
        private ShelfInstance FindShelfById(string shelfId)
        {
            if (string.IsNullOrEmpty(shelfId)) return null;

            var shelves = Object.FindObjectsByType<ShelfInstance>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var shelf in shelves)
            {
                if (shelf.instanceId == shelfId)
                    return shelf;
            }

            return null;
        }

        /// <summary>
        /// 根据ID查找设施
        /// </summary>
        private FacilityInstance FindFacilityById(string facilityId)
        {
            if (string.IsNullOrEmpty(facilityId)) return null;

            var facilities = Object.FindObjectsByType<FacilityInstance>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var facility in facilities)
            {
                if (facility.instanceId == facilityId)
                    return facility;
            }

            return null;
        }
    }
}
