using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Runtime;
using PopLife.Runtime;
using PopLife.Data;
using PopLife.UI;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer")]
    [Description("执行购买交互 - 调用CustomerInteraction.TryPurchase()")]
    public class ExecutePurchaseAction : ActionTask
    {
        [BlackboardOnly]
        [Tooltip("要购买的数量（由DecidePurchaseAction设置）")]
        public BBParameter<int> purchaseQuantity;

        [BlackboardOnly]
        [Tooltip("目标货架ID（用于记录已购买archetype）")]
        public BBParameter<string> targetShelfId;

        [Tooltip("是否每次只购买1件（推荐true，因为TryPurchase一次买一件）")]
        public bool purchaseOneByOne = true;

        private CustomerInteraction interaction;
        private CustomerBlackboardAdapter blackboard;
        private int successCount = 0;

        protected override string info
        {
            get { return $"执行购买 x{purchaseQuantity}"; }
        }

        protected override void OnExecute()
        {
            // 获取组件
            interaction = agent.GetComponent<CustomerInteraction>();
            blackboard = agent.GetComponent<CustomerBlackboardAdapter>();

            if (interaction == null)
            {
                Debug.LogError("[ExecutePurchaseAction] CustomerInteraction component not found");
                EndAction(false);
                return;
            }

            if (blackboard == null)
            {
                Debug.LogError("[ExecutePurchaseAction] CustomerBlackboardAdapter component not found");
                EndAction(false);
                return;
            }

            int targetQty = purchaseQuantity.value;

            if (targetQty <= 0)
            {
                string msg = $"Purchase quantity is 0, skipping purchase";
                Debug.Log($"[ExecutePurchaseAction] Customer {blackboard.customerId} {msg}");
                ScreenLogger.LogWarning(blackboard.customerId, msg);
                EndAction(false);
                return;
            }

            // 执行购买
            successCount = 0;

            if (purchaseOneByOne)
            {
                // 逐件购买（推荐方式，因为TryPurchase设计为单次购买）
                for (int i = 0; i < targetQty; i++)
                {
                    if (interaction.TryPurchase())
                    {
                        successCount++;
                    }
                    else
                    {
                        // 购买失败（库存不足或钱不够），停止
                        string msg = $"Purchase failed at item {i + 1} (out of stock or insufficient money)";
                        Debug.LogWarning($"[ExecutePurchaseAction] Customer {blackboard.customerId} {msg}");
                        ScreenLogger.LogWarning(blackboard.customerId, msg);
                        break;
                    }
                }
            }
            else
            {
                // 一次性购买（假设TryPurchase支持多件）
                if (interaction.TryPurchase())
                {
                    successCount = 1; // TryPurchase当前只支持单件
                }
            }

            // 同步purchaseQuantity回黑板（记录实际购买量）
            purchaseQuantity.value = successCount;

            // 同步到CustomerBlackboardAdapter
            blackboard.purchaseQuantity = successCount;

            // 同步到NodeCanvas黑板
#if NODECANVAS
            if (blackboard.ncBlackboard != null)
            {
                blackboard.ncBlackboard.SetVariableValue("moneyBag", blackboard.moneyBag);
                blackboard.ncBlackboard.SetVariableValue("purchaseQuantity", successCount);
            }
#endif

            if (successCount > 0)
            {
                // 记录已购买的货架archetype
                RecordPurchasedArchetype();

                string msg = $"Successfully purchased {successCount}/{targetQty} items, remaining money: ${blackboard.moneyBag}";
                Debug.Log($"[ExecutePurchaseAction] Customer {blackboard.customerId} {msg}");
                ScreenLogger.LogPurchase(blackboard.customerId, msg);
                EndAction(true);
            }
            else
            {
                string msg = "Purchase failed, no items purchased";
                Debug.LogWarning($"[ExecutePurchaseAction] Customer {blackboard.customerId} {msg}");
                ScreenLogger.LogWarning(blackboard.customerId, msg);
                EndAction(false);
            }
        }

        /// <summary>
        /// 记录已购买的货架archetype ID
        /// </summary>
        private void RecordPurchasedArchetype()
        {
            if (string.IsNullOrEmpty(targetShelfId.value))
            {
                Debug.LogWarning("[ExecutePurchaseAction] targetShelfId is null or empty, cannot record purchased archetype");
                return;
            }

            // 查找目标货架
            var shelf = FindShelfById(targetShelfId.value);
            if (shelf == null)
            {
                Debug.LogWarning($"[ExecutePurchaseAction] Cannot find shelf {targetShelfId.value} to record archetype");
                return;
            }

            // 获取archetype ID
            var shelfArchetype = shelf.archetype as ShelfArchetype;
            if (shelfArchetype == null)
            {
                Debug.LogWarning($"[ExecutePurchaseAction] Shelf {targetShelfId.value} has no ShelfArchetype");
                return;
            }

            // 添加到已购买列表
            if (blackboard.purchasedArchetypes == null)
            {
                blackboard.purchasedArchetypes = new System.Collections.Generic.HashSet<string>();
            }

            bool added = blackboard.purchasedArchetypes.Add(shelfArchetype.archetypeId);
            if (added)
            {
                Debug.Log($"[ExecutePurchaseAction] Customer {blackboard.customerId} marked archetype {shelfArchetype.archetypeId} as purchased");
            }
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
    }
}
