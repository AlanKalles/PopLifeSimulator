using System;
using System.Collections.Generic;
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
        public float[] interestFinal = Array.Empty<float>();
        public int embarrassmentCap;
        public float moveSpeed;
        public int queueToleranceSec;


        [Header("本次会话状态（行为树读写）")]
        public int moneyBag;
        public int embarrassment;
        public Vector2Int goalCell;
        public string targetShelfId;
        public string targetCashierId;
        public string targetExitId;
        public int purchaseQuantity; // 决定购买的数量
        public int pendingPayment; // 待结账金额（在收银台结算）
        public Transform assignedQueueSlot; // 分配的队列位置（由 QueueController 分配）
        public Transform targetExitPoint; // 目标离店点的 Transform
        public HashSet<string> purchasedArchetypes = new HashSet<string>(); // 本次访问已购买的货架archetype ID

        [Header("策略集合（只读引用）")]
        public BehaviorPolicySet policies;


// NodeCanvas 黑板写入（可选）
#if NODECANVAS
        public global::NodeCanvas.Framework.Blackboard ncBlackboard;
        void Reset(){ ncBlackboard = GetComponent<global::NodeCanvas.Framework.Blackboard>(); }
#endif
        public void InjectFromRecord(CustomerRecord record, CustomerArchetype archetype, float[] finalInterest, int embarrassmentCapVal, float finalMoveSpeed)
        {
            customerId = record.customerId;
            loyaltyLevel = record.loyaltyLevel;
            trust = record.trust;
            interestFinal = finalInterest;
            embarrassmentCap = embarrassmentCapVal;
            moveSpeed = finalMoveSpeed;
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