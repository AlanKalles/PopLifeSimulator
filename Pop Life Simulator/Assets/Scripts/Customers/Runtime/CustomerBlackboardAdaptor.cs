using System;
using UnityEngine;
using PopLife.Customers.Data;


namespace PopLife.Customers.Runtime
{
// 将持久层与策略输入注入到行为树可读的黑板
    public class CustomerBlackboardAdapter : MonoBehaviour
    {
        [Header("只读基线（注入）")]
        public string customerId;
        public int loyaltyLevel;
        public int trust;
        public int[] interestFinal = Array.Empty<int>();
        public int embarrassmentCap;
        public float moveSpeed;
        public int queueToleranceSec;


        [Header("本次会话状态（行为树读写）")]
        public int moneyBag;
        public int embarrassment;
        public Vector2Int goalCell;
        public string targetShelfId;
        public string targetCashierId;
        public int purchaseQuantity; // 决定购买的数量

        [Header("策略集合（只读引用）")]
        public BehaviorPolicySet policies;


// NodeCanvas 黑板写入（可选）
#if NODECANVAS
        public global::NodeCanvas.Framework.Blackboard ncBlackboard;
        void Reset(){ ncBlackboard = GetComponent<global::NodeCanvas.Framework.Blackboard>(); }
#endif
        public void InjectFromRecord(CustomerRecord record, CustomerArchetype archetype, int[] finalInterest, int embarrassmentCapVal)
        {
            customerId = record.customerId;
            loyaltyLevel = record.loyaltyLevel;
            trust = record.trust;
            interestFinal = finalInterest;
            embarrassmentCap = embarrassmentCapVal;
            moveSpeed = archetype.moveSpeed;
            queueToleranceSec = archetype.queueToleranceSeconds;
            policies = archetype.defaultPolicies;


#if NODECANVAS
            if (ncBlackboard)
            {
                ncBlackboard.SetVariableValue("customerId", customerId);
                ncBlackboard.SetVariableValue("loyaltyLevel", loyaltyLevel);
                ncBlackboard.SetVariableValue("trust", trust);
                ncBlackboard.SetVariableValue("interestFinal", interestFinal);
                ncBlackboard.SetVariableValue("embarrassmentCap", embarrassmentCap);
                ncBlackboard.SetVariableValue("moveSpeed", moveSpeed);
                ncBlackboard.SetVariableValue("queueToleranceSec", queueToleranceSec);
                ncBlackboard.SetVariableValue("policies", policies);
                ncBlackboard.SetVariableValue("moneyBag", moneyBag);
                ncBlackboard.SetVariableValue("purchaseQuantity", 0); // 初始化为0
            }
#endif
        }
    }
}