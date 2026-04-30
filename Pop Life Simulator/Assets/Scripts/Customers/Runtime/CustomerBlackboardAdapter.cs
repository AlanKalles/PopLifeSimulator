using System;
using System.Collections.Generic;
using UnityEngine;
using PopLife.Customers.Data;
using PopLife.Data;


namespace PopLife.Customers.Runtime
{
// 将持久层与策略输入注入到行为树可读的黑板
    public class CustomerBlackboardAdapter : MonoBehaviour
    {
        [Header("只读基线（注入）")]
        public string customerId;
        public string customerName;
        public int loyaltyLevel;
        public int trust;
        public int embarrassmentCap;
        public float moveSpeed;
        public int queueToleranceSec;

        [Header("偏好数据（Initialize 时预计算）")]
        public string[] favoriteShelfIds;           // 从 CustomerRecord 加载
        public int[] favoriteCategoryIndices;       // 偏好货架的去重品类索引
        public BrandData[] favoriteBrands;          // 偏好货架的去重品牌

        [Header("漏斗状态（行为树读写）")]
        public FunnelPhase currentFunnelPhase;      // 由行为树设置
        public FunnelPhase lastSelectionTier;       // 最近一次选择命中的层级（策略写入）

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
        public Transform spawnPoint; // 顾客生成点（也是最终撤离点）
        public HashSet<ShelfArchetype> purchasedArchetypes = new HashSet<ShelfArchetype>(); // 本次访问已购买的货架archetype（SO引用）

        [Header("入口/出口状态")]
        public Transform entranceOutsideAnchor; // 商店入口外部锚点（街道侧）
        public Transform entranceInsideAnchor;  // 商店入口内部锚点（商店一楼侧）
        public bool hasEnteredStore = false;    // 是否已进入商店（用于区分入店/离店状态）

        [Header("商店状态")]
        public bool isClosingTime = false; // 商店是否闭店

        [Header("情绪状态 - Upset 机制")]
        public bool isUpset = false;       // 是否处于 upset 状态（找不到商品/购买失败）
        public int consecutivePurchaseFailures = 0;    // 连续到达货架但购买失败的次数（到达货架库存为0）

        [Header("交互事件状态")]
        public InteractionEventData assignedInteraction;    // 分配的交互事件（null = 无交互）
        public bool interactionUsedToday = false;           // 今日是否已触发过交互
        public bool interactionBubbleActive = false;        // 交互气泡是否正在显示（供点击检测使用）
        public bool interactionClickAllowed = false;        // 是否允许点击气泡进入交互对话
        public bool interactionDialogueStarted = false;     // 对话是否已开始（由 CustomerDialogueTrigger 设置）

        [Header("策略集合（只读引用）")]
        public BehaviorPolicySet policies;


// NodeCanvas 黑板写入（可选）
#if NODECANVAS
        public global::NodeCanvas.Framework.Blackboard ncBlackboard;
        void Reset(){ ncBlackboard = GetComponent<global::NodeCanvas.Framework.Blackboard>(); }
#endif
        public void InjectFromRecord(CustomerRecord record, CustomerArchetype archetype, int embarrassmentCapVal, float finalMoveSpeed)
        {
            customerId = record.customerId;
            customerName = record.name;
            loyaltyLevel = record.loyaltyLevel;
            trust = record.trust;
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
                ncBlackboard.SetVariableValue("embarrassmentCap", embarrassmentCap);
                ncBlackboard.SetVariableValue("moveSpeed", moveSpeed);
                ncBlackboard.SetVariableValue("queueToleranceSec", queueToleranceSec);
                ncBlackboard.SetVariableValue("policies", policies);
                ncBlackboard.SetVariableValue("moneyBag", moneyBag);
                ncBlackboard.SetVariableValue("purchaseQuantity", 0); // 初始化为0
                ncBlackboard.SetVariableValue("isClosingTime", isClosingTime); // 初始化闭店状态
            }
#endif
        }
    }
}
