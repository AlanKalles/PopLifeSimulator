# 商店闭店机制 - 行为树设计方案

## 设计理念

**核心原则**：将闭店逻辑完全封装在行为树节点内，保持顾客行为的可视化和可配置性

### 优势
✅ **行为可视化** - 所有逻辑在行为树编辑器中可见
✅ **易于调试** - 通过 NodeCanvas 的 Graph Console 实时监控
✅ **配置灵活** - 无需修改代码即可调整闭店行为
✅ **模块化** - 新节点可复用于其他紧急情况（火灾、抢劫等）

---

## 新增节点清单

### 1. CheckStoreClosingCondition (条件节点)
**路径**: `Assets/Scripts/Customers/NodeCanvas/Conditions/CheckStoreClosingCondition.cs`

**功能**: 检查商店是否闭店

**黑板变量**:
- 输入: `isClosingTime` (bool)

**返回值**:
- True: 商店已闭店
- False: 正常营业

**用途**: 作为高优先级分支的条件判断

```csharp
namespace PopLife.Customers.NodeCanvas.Conditions
{
    [Category("PopLife/Store")]
    [Description("检查商店是否闭店")]
    public class CheckStoreClosingCondition : ConditionTask
    {
        [BlackboardOnly]
        public BBParameter<bool> isClosingTime;

        protected override string info => "Is Store Closing?";

        protected override bool OnCheck()
        {
            return isClosingTime.value;
        }
    }
}
```

---

### 2. CheckPendingPaymentCondition (条件节点)
**路径**: `Assets/Scripts/Customers/NodeCanvas/Conditions/CheckPendingPaymentCondition.cs`

**功能**: 检查顾客是否有待结账金额

**黑板变量**:
- 输入: `pendingPayment` (int)

**返回值**:
- True: pendingPayment > 0
- False: pendingPayment <= 0

**用途**: 决定闭店时顾客应该结账还是直接离店

```csharp
namespace PopLife.Customers.NodeCanvas.Conditions
{
    [Category("PopLife/Customer")]
    [Description("检查是否有待结账金额")]
    public class CheckPendingPaymentCondition : ConditionTask
    {
        [BlackboardOnly]
        public BBParameter<int> pendingPayment;

        protected override string info => "Has Pending Payment?";

        protected override bool OnCheck()
        {
            return pendingPayment.value > 0;
        }
    }
}
```

---

### 3. ForceExitShoppingLoopAction (动作节点)
**路径**: `Assets/Scripts/Customers/NodeCanvas/Actions/ForceExitShoppingLoopAction.cs`

**功能**: 强制退出购物循环（清理当前状态）

**黑板变量**:
- 输入/输出: `targetShelfId`, `assignedQueueSlot`, `purchaseQuantity`, `goalCell`

**执行逻辑**:
1. 检查是否有队列位置 (assignedQueueSlot)
2. 如果有，释放货架队列
3. 停止当前移动 (FollowerEntity.isStopped = true)
4. 清空所有货架相关变量
5. 返回 Success

**用途**: 在闭店时清理购物状态，为结账/离店做准备

```csharp
namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Store")]
    [Description("强制退出购物循环并清理状态")]
    public class ForceExitShoppingLoopAction : ActionTask
    {
        [BlackboardOnly]
        public BBParameter<string> targetShelfId;

        [BlackboardOnly]
        public BBParameter<Transform> assignedQueueSlot;

        [BlackboardOnly]
        public BBParameter<int> purchaseQuantity;

        [BlackboardOnly]
        public BBParameter<Vector2Int> goalCell;

        protected override string info => "Force Exit Shopping";

        protected override void OnExecute()
        {
            var blackboard = agent.GetComponent<CustomerBlackboardAdapter>();

            // 1. 停止移动
            var follower = agent.GetComponent<FollowerEntity>();
            if (follower != null)
            {
                follower.isStopped = true;
            }

            // 2. 释放队列位置
            if (!string.IsNullOrEmpty(targetShelfId.value) && assignedQueueSlot.value != null)
            {
                ReleaseShelfQueue(targetShelfId.value, blackboard.customerId);
            }

            // 3. 清空变量
            targetShelfId.value = string.Empty;
            assignedQueueSlot.value = null;
            purchaseQuantity.value = 0;
            goalCell.value = Vector2Int.zero;

            blackboard.targetShelfId = string.Empty;
            blackboard.assignedQueueSlot = null;
            blackboard.purchaseQuantity = 0;

            Debug.Log($"[ForceExitShoppingLoop] Customer {blackboard.customerId} exited shopping loop");
            EndAction(true);
        }

        private void ReleaseShelfQueue(string shelfId, string customerId)
        {
            // 查找货架并释放队列
            // (实现逻辑同 ReleaseQueueSlotAction)
        }
    }
}
```

---

### 4. SetUrgentMoveSpeedAction (动作节点)
**路径**: `Assets/Scripts/Customers/NodeCanvas/Actions/SetUrgentMoveSpeedAction.cs`

**功能**: 设置紧急移动速度（加速）

**黑板变量**:
- 输入: `moveSpeed` (float), `isClosingTime` (bool)
- 参数: `urgentSpeedMultiplier` (float, default: 2.0)

**执行逻辑**:
1. 检查 isClosingTime
2. 如果是闭店时间，移动速度 × urgentSpeedMultiplier
3. 更新 FollowerEntity.maxSpeed
4. 返回 Success

**用途**: 在闭店时加速顾客移动

```csharp
namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Store")]
    [Description("设置紧急移动速度")]
    public class SetUrgentMoveSpeedAction : ActionTask
    {
        [BlackboardOnly]
        public BBParameter<float> moveSpeed;

        [BlackboardOnly]
        public BBParameter<bool> isClosingTime;

        [Tooltip("闭店时的速度倍率")]
        public float urgentSpeedMultiplier = 2.0f;

        protected override string info => $"Set Speed (×{urgentSpeedMultiplier} if closing)";

        protected override void OnExecute()
        {
            var follower = agent.GetComponent<FollowerEntity>();
            if (follower == null)
            {
                EndAction(false);
                return;
            }

            float finalSpeed = moveSpeed.value;

            if (isClosingTime.value)
            {
                finalSpeed *= urgentSpeedMultiplier;
                Debug.Log($"[SetUrgentMoveSpeed] Urgent mode: speed = {finalSpeed}");
            }

            follower.maxSpeed = finalSpeed;
            EndAction(true);
        }
    }
}
```

---

### 5. SkipWaitIfClosingAction (动作节点)
**路径**: `Assets/Scripts/Customers/NodeCanvas/Actions/SkipWaitIfClosingAction.cs`

**功能**: 条件等待（闭店时缩短等待时间）

**黑板变量**:
- 输入: `isClosingTime` (bool)
- 参数: `normalWaitTime`, `urgentWaitTime`

**执行逻辑**:
1. 检查 isClosingTime
2. 选择等待时长
3. 执行等待
4. 返回 Success

**用途**: 替代原有的 Wait 节点

```csharp
namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Store")]
    [Description("条件等待（闭店时缩短）")]
    public class SkipWaitIfClosingAction : ActionTask
    {
        [BlackboardOnly]
        public BBParameter<bool> isClosingTime;

        public float normalWaitTime = 1.0f;
        public float urgentWaitTime = 0.1f;

        private float elapsedTime;
        private float targetWaitTime;

        protected override string info => $"Wait ({normalWaitTime}s / {urgentWaitTime}s)";

        protected override void OnExecute()
        {
            elapsedTime = 0f;
            targetWaitTime = isClosingTime.value ? urgentWaitTime : normalWaitTime;
        }

        protected override void OnUpdate()
        {
            elapsedTime += Time.deltaTime;
            if (elapsedTime >= targetWaitTime)
            {
                EndAction(true);
            }
        }
    }
}
```

---

### 6. EmergencyCheckoutAction (动作节点)
**路径**: `Assets/Scripts/Customers/NodeCanvas/Actions/EmergencyCheckoutAction.cs`

**功能**: 紧急结账（无需收银台）

**黑板变量**:
- 输入: `pendingPayment` (int)
- 输入/输出: `moneyBag` (int)

**执行逻辑**:
1. 检查 pendingPayment > 0
2. 直接调用 ResourceManager.AddMoney(pendingPayment)
3. 记录销售额到 DayLoopManager
4. 清空 pendingPayment
5. 返回 Success

**用途**: 闭店时无收银台的兜底方案

```csharp
namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Store")]
    [Description("紧急结账（无需收银台）")]
    public class EmergencyCheckoutAction : ActionTask
    {
        [BlackboardOnly]
        public BBParameter<int> pendingPayment;

        protected override string info => "Emergency Checkout";

        protected override void OnExecute()
        {
            var blackboard = agent.GetComponent<CustomerBlackboardAdapter>();

            if (pendingPayment.value <= 0)
            {
                Debug.Log($"[EmergencyCheckout] No pending payment, skip");
                EndAction(true);
                return;
            }

            // 记录销售额
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.RecordSale(pendingPayment.value);
            }

            // 增加玩家金钱
            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.AddMoney(pendingPayment.value);
            }

            Debug.Log($"[EmergencyCheckout] Customer {blackboard.customerId} paid ${pendingPayment.value}");

            // 清空待结账金额
            pendingPayment.value = 0;
            blackboard.pendingPayment = 0;

            EndAction(true);
        }
    }
}
```

---

## 修改现有节点

### 1. SelectTargetShelfAction 修改
**目的**: 闭店时如果有待结账金额，跳过货架选择

**修改位置**: `OnExecute()` 开头

```csharp
protected override void OnExecute()
{
    var adapter = agent.GetComponent<CustomerBlackboardAdapter>();

    // 【新增】闭店检查
    if (adapter != null && adapter.isClosingTime)
    {
        if (adapter.pendingPayment > 0)
        {
            // 有待结账金额 → 跳过购物，返回失败触发 Repeater 退出
            Debug.Log($"[SelectTargetShelf] Store closing, customer {adapter.customerId} skip shopping (pending: ${adapter.pendingPayment})");
            EndAction(false);
            return;
        }
        // 无待结账金额 → 继续正常选货架（可能需要买东西）
    }

    // 原有逻辑...
}
```

---

### 2. SelectCashierAction 修改
**目的**: 闭店时忽略队列长度限制，选择最近收银台

**修改位置**: 策略调用之前

```csharp
protected override void OnExecute()
{
    var adapter = agent.GetComponent<CustomerBlackboardAdapter>();

    // 【新增】闭店时的收银台选择逻辑
    if (adapter != null && adapter.isClosingTime)
    {
        // 寻找最近的收银台（忽略队列）
        var nearestCashier = FindNearestCashier();
        if (nearestCashier != null)
        {
            targetCashierId.value = nearestCashier.instanceId;
            // ... 设置 goalCell
            Debug.Log($"[SelectCashier] Store closing, selected nearest cashier: {nearestCashier.instanceId}");
            EndAction(true);
            return;
        }

        // 如果没有收银台，返回失败（触发紧急结账）
        Debug.LogWarning($"[SelectCashier] No cashier available during closing time");
        EndAction(false);
        return;
    }

    // 原有策略逻辑...
}
```

---

### 3. CustomerBlackboardAdapter 添加变量
**修改位置**: 类定义

```csharp
[Header("闭店状态")]
public bool isClosingTime = false; // 商店是否闭店
```

---

## 新行为树结构设计

### 完整树结构

```
Root: Priority Selector
│
├─── [Branch 1 - 最高优先级] Emergency Exit (闭店处理)
│    Condition: CheckStoreClosingCondition
│    │
│    └─── Sequencer "Emergency Exit Sequence"
│         │
│         ├─── ForceExitShoppingLoopAction (清理购物状态)
│         │
│         ├─── Priority Selector "Exit Strategy"
│         │    │
│         │    ├─── [Sub-Branch A] Has Pending Payment → Rush Checkout
│         │    │    Condition: CheckPendingPaymentCondition
│         │    │    │
│         │    │    └─── Sequencer "Rush Checkout"
│         │    │         ├─── SetUrgentMoveSpeedAction (加速)
│         │    │         ├─── SelectCashierAction (修改版 - 忽略队列)
│         │    │         ├─── Fallback Selector
│         │    │         │    ├─── AcquireQueueSlotAction (尝试获取队列)
│         │    │         │    └─── Success (兜底：无队列也继续)
│         │    │         ├─── MoveToTargetAction (移动到收银台)
│         │    │         ├─── SkipWaitIfClosingAction (0.1s等待)
│         │    │         ├─── Fallback Selector
│         │    │         │    ├─── ExecuteCheckoutAction (正常结账)
│         │    │         │    └─── EmergencyCheckoutAction (紧急结账)
│         │    │         └─── ReleaseQueueSlotAction (收银台)
│         │    │
│         │    └─── [Sub-Branch B] No Payment → Direct Exit
│         │         Sequencer "Direct Exit"
│         │         └─── (直接跳到出口)
│         │
│         └─── Sequencer "Go To Exit"
│              ├─── SelectExitPointAction
│              ├─── SetVariable (HasReachedTarget = false)
│              ├─── SetUrgentMoveSpeedAction (加速)
│              ├─── MoveToTargetAction
│              └─── DestroyAgentAction
│
└─── [Branch 2 - 正常优先级] Normal Shopping & Checkout (原有流程)
     StepIterator (节点0)
     │
     ├─── Repeater (节点1 - 修改：添加闭店检查)
     │    └─── ConditionalEvaluator (moneyBag > 0)
     │         └─── StepIterator (节点3)
     │              ├─── Sequencer "check shelf" (节点4-5)
     │              ├─── ConditionalEvaluator (purchaseQuantity > 0) (节点6-7)
     │              └─── ReleaseQueueSlotAction (节点8)
     │
     └─── Sequencer "checkout & exit" (节点9-10)
          └─── (原有结账离店逻辑)
```

---

## 实现步骤

### Phase 1: 创建新节点 (1-2小时)
1. ✅ CheckStoreClosingCondition
2. ✅ CheckPendingPaymentCondition
3. ✅ ForceExitShoppingLoopAction
4. ✅ SetUrgentMoveSpeedAction
5. ✅ SkipWaitIfClosingAction
6. ✅ EmergencyCheckoutAction

### Phase 2: 修改现有节点 (30分钟)
1. ✅ SelectTargetShelfAction - 添加闭店检查
2. ✅ SelectCashierAction - 添加闭店逻辑
3. ✅ CustomerBlackboardAdapter - 添加 isClosingTime 变量

### Phase 3: 重构行为树 (1小时)
1. 创建新的行为树资产（备份原有）
2. 构建 Priority Selector 根节点
3. 构建 Emergency Exit 分支
4. 连接原有 Normal Shopping 分支
5. 测试连接关系

### Phase 4: 集成 DayLoopManager (30分钟)
1. 修改 `CustomerSpawner.StopSpawning()`
   ```csharp
   private void StopSpawning()
   {
       Debug.Log("[CustomerSpawner] 关店，停止自动生成，设置所有顾客闭店状态");
       isSpawning = false;

       // 设置所有在场顾客的 isClosingTime = true
       var allCustomers = FindObjectsByType<CustomerAgent>(FindObjectsSortMode.None);
       foreach (var customer in allCustomers)
       {
           var bb = customer.GetComponent<CustomerBlackboardAdapter>();
           if (bb != null)
           {
               bb.isClosingTime = true;

               // 同步到 NodeCanvas 黑板
               #if NODECANVAS
               if (bb.ncBlackboard != null)
               {
                   bb.ncBlackboard.SetVariableValue("isClosingTime", true);
               }
               #endif
           }
       }
   }
   ```

2. 修改 `DayLoopManager.TriggerDailySettlement()`
   ```csharp
   private void TriggerDailySettlement()
   {
       isStoreOpen = false;
       OnStoreClose?.Invoke(); // 触发 CustomerSpawner.StopSpawning()

       // 等待所有顾客离开
       StartCoroutine(WaitForCustomersToLeave());
   }

   private IEnumerator WaitForCustomersToLeave()
   {
       float timeout = 30f; // 超时保护
       float elapsed = 0f;

       while (FindObjectsByType<CustomerAgent>(FindObjectsSortMode.None).Length > 0)
       {
           elapsed += Time.deltaTime;

           if (elapsed >= timeout)
           {
               Debug.LogWarning("[DayLoopManager] Customer cleanup timeout, forcing settlement");
               // 强制销毁剩余顾客
               var remaining = FindObjectsByType<CustomerAgent>(FindObjectsSortMode.None);
               foreach (var agent in remaining)
               {
                   Destroy(agent.gameObject);
               }
               break;
           }

           yield return new WaitForSeconds(0.5f);
       }

       // 所有顾客离开，显示结算界面
       ShowSettlementUI();
   }

   private void ShowSettlementUI()
   {
       // 计算每日数据
       DailySettlementData data = CalculateDailySettlement();

       // 触发结算事件
       OnDailySettlement?.Invoke(data);

       // 暂停时间
       PauseTime();
   }
   ```

### Phase 5: 测试与调优 (1-2小时)
1. ✅ 测试场景1：顾客在货架队列中
2. ✅ 测试场景2：顾客正在购买
3. ✅ 测试场景3：顾客在收银台队列中
4. ✅ 测试场景4：顾客正在结账
5. ✅ 测试场景5：无收银台情况
6. ✅ 测试超时机制
7. ✅ 性能测试（大量顾客同时清场）

---

## 调试工具

### 1. 行为树可视化
- 在 Unity 编辑器中打开 NodeCanvas Graph Console
- 运行时实时监控顾客行为树执行状态
- 观察节点执行顺序和返回值

### 2. 黑板变量监控
- 在 Graph Console 中查看 Blackboard 面板
- 实时监控 `isClosingTime`, `pendingPayment`, `targetShelfId` 等变量

### 3. 日志输出
所有新节点都包含详细日志，方便调试：
```
[CheckStoreClosing] Closing time: true
[ForceExitShoppingLoop] Customer C001 exited shopping loop
[SetUrgentMoveSpeed] Urgent mode: speed = 6.0
[EmergencyCheckout] Customer C001 paid $150
```

---

## 性能考虑

### 1. Priority Selector 性能
- Priority Selector 每帧检查条件（CheckStoreClosingCondition）
- 但条件检查非常轻量（仅读取 bool 变量）
- 预计性能影响 < 0.01ms per customer

### 2. 行为树复杂度
- 新分支增加约 10 个节点
- 但只有在闭店时才会执行
- 正常营业时，第一个条件失败，直接跳过

### 3. 大量顾客清场
- 如果场上有 50+ 顾客同时触发闭店逻辑
- 可能导致瞬时寻路请求过多
- **缓解措施**: A* 寻路系统已有队列机制

---

## 扩展性

### 其他紧急情况复用
该架构可轻松扩展到其他紧急情况：

#### 火灾疏散
```
Condition: CheckFireAlarm
└─ EmergencyEvacuate (复用 ForceExitShoppingLoopAction)
   └─ PanicMovement (更高速度倍率)
      └─ ExitBuilding
```

#### 抢劫事件
```
Condition: CheckRobberyInProgress
└─ HideOrFlee Selector
   ├─ FindHidingSpot
   └─ RunToExit
```

#### VIP 顾客优先服务
```
Condition: IsVIPCustomer
└─ SkipQueueAction
   └─ DirectServiceAction
```

---

## 总结

### 优势
✅ **完全可视化** - 所有逻辑在行为树中展现
✅ **易于调试** - NodeCanvas 提供实时监控
✅ **高度模块化** - 新节点可独立测试和复用
✅ **无代码耦合** - 不依赖硬编码的状态检查
✅ **灵活配置** - 可通过行为树编辑器调整参数

### 风险
⚠️ **行为树复杂度增加** - 需要团队熟悉 NodeCanvas
⚠️ **调试难度** - 需要同时查看行为树和代码
⚠️ **性能开销** - Priority Selector 每帧检查（但影响极小）

### 建议
1. **先实现 Phase 1-2**（新节点和修改），在代码层面测试
2. **再实现 Phase 3**（行为树重构），在编辑器中验证
3. **最后实现 Phase 4-5**（集成和测试），完整测试流程
4. **保留原行为树备份**，便于回滚

---

## 下一步行动

你觉得这个方案如何？如果认可，我可以开始：
1. 创建 6 个新节点的完整代码
2. 修改现有节点（SelectTargetShelfAction, SelectCashierAction）
3. 提供详细的行为树搭建指南（带截图说明）
4. 实现 DayLoopManager 的集成代码

需要我现在开始实现吗？
