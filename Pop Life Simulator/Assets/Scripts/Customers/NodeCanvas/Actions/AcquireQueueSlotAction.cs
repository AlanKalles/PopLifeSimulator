using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Runtime;
using PopLife.Customers.Services;
using PopLife.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer/Queue")]
    [Description("申请队列位置（货架或收银台）")]
    public class AcquireQueueSlotAction : ActionTask
    {
        [BlackboardOnly]
        [Tooltip("目标货架ID（如果是前往货架）")]
        public BBParameter<string> targetShelfId;

        [BlackboardOnly]
        [Tooltip("目标收银台ID（如果是前往收银台）")]
        public BBParameter<string> targetCashierId;

        [BlackboardOnly]
        [Tooltip("分配的队列位置（输出）")]
        public BBParameter<Transform> assignedQueueSlot;

        private CustomerBlackboardAdapter blackboard;

        protected override string info
        {
            get { return "申请队列位置"; }
        }

        protected override void OnExecute()
        {
            blackboard = agent.GetComponent<CustomerBlackboardAdapter>();
            if (blackboard == null)
            {
                Debug.LogError("[AcquireQueueSlotAction] 找不到 CustomerBlackboardAdapter");
                EndAction(false);
                return;
            }

            Transform queueSlot = null;

            // 优先处理货架队列
            if (!string.IsNullOrEmpty(targetShelfId.value))
            {
                queueSlot = AcquireShelfQueueSlot(targetShelfId.value);
            }
            // 其次处理收银台队列
            else if (!string.IsNullOrEmpty(targetCashierId.value))
            {
                queueSlot = AcquireCashierQueueSlot(targetCashierId.value);
            }
            else
            {
                Debug.LogError($"[AcquireQueueSlotAction] 顾客 {blackboard.customerId} 没有指定目标");
                EndAction(false);
                return;
            }

            if (queueSlot == null)
            {
                Debug.LogError($"[AcquireQueueSlotAction] 顾客 {blackboard.customerId} 申请队位失败");
                EndAction(false);
                return;
            }

            // 保存到黑板
            assignedQueueSlot.value = queueSlot;
            blackboard.assignedQueueSlot = queueSlot;

            Debug.Log($"[AcquireQueueSlotAction] 顾客 {blackboard.customerId} 成功申请队位: {queueSlot.position}");
            EndAction(true);
        }

        /// <summary>
        /// 申请货架队列位置
        /// </summary>
        private Transform AcquireShelfQueueSlot(string shelfId)
        {
            var shelf = FindShelfById(shelfId);
            if (shelf == null)
            {
                Debug.LogWarning($"[AcquireQueueSlotAction] 找不到货架 {shelfId}");
                return null;
            }

            var queueController = shelf.GetComponent<ShelfQueueController>();
            if (queueController == null)
            {
                Debug.LogWarning($"[AcquireQueueSlotAction] 货架 {shelfId} 没有 ShelfQueueController，使用降级方案");
                // 降级：直接返回货架的交互点
                return shelf.GetInteractionPoint();
            }

            return queueController.AcquireSlot(blackboard.customerId);
        }

        /// <summary>
        /// 申请收银台队列位置
        /// </summary>
        private Transform AcquireCashierQueueSlot(string cashierId)
        {
            var cashier = FindFacilityById(cashierId);
            if (cashier == null)
            {
                Debug.LogWarning($"[AcquireQueueSlotAction] 找不到收银台 {cashierId}");
                return null;
            }

            var queueController = cashier.GetComponent<CashierQueueController>();
            if (queueController == null)
            {
                Debug.LogWarning($"[AcquireQueueSlotAction] 收银台 {cashierId} 没有 CashierQueueController，使用降级方案");
                // 降级：直接返回收银台的交互点
                return cashier.GetInteractionPoint();
            }

            return queueController.AcquireSlot(blackboard.customerId);
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
