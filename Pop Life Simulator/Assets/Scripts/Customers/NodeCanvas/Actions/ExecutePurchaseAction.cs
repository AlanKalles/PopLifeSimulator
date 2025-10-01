using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer")]
    [Description("执行购买交互 - 调用CustomerInteraction.TryPurchase()")]
    public class ExecutePurchaseAction : ActionTask
    {
        [BlackboardOnly]
        [Tooltip("要购买的数量（由DecidePurchaseAction设置）")]
        public BBParameter<int> purchaseQuantity;

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
                Debug.LogError("[ExecutePurchaseAction] 找不到 CustomerInteraction 组件");
                EndAction(false);
                return;
            }

            if (blackboard == null)
            {
                Debug.LogError("[ExecutePurchaseAction] 找不到 CustomerBlackboardAdapter 组件");
                EndAction(false);
                return;
            }

            int targetQty = purchaseQuantity.value;

            if (targetQty <= 0)
            {
                Debug.Log($"[ExecutePurchaseAction] 顾客 {blackboard.customerId} 购买数量为0，跳过购买");
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
                        Debug.LogWarning($"[ExecutePurchaseAction] 顾客 {blackboard.customerId} 在第 {i + 1} 件时购买失败");
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
                Debug.Log($"[ExecutePurchaseAction] 顾客 {blackboard.customerId} 成功购买了 {successCount}/{targetQty} 件商品，剩余金钱: {blackboard.moneyBag}");
                EndAction(true);
            }
            else
            {
                Debug.LogWarning($"[ExecutePurchaseAction] 顾客 {blackboard.customerId} 购买失败，没有成功购买任何商品");
                EndAction(false);
            }
        }
    }
}
