# Customer Behavior Tree 结构分析

基于 `CustomerBehaviorTree.asset` 的实际节点结构

## 节点列表

### 节点 0: StepIterator (根节点)
- 位置: (47.79, -381.18)
- **作用**: 按步骤迭代子节点

### 节点 1: Repeater
- 位置: (-17.49, -285.91)
- repeaterMode: 1
- **作用**: 循环执行购物逻辑

### 节点 2: ConditionalEvaluator
- 位置: (14.53, -161.42)
- 条件: CheckInt (检查 moneyBag > 0)
- **作用**: 如果钱包有钱，继续购物

### 节点 3: StepIterator (购物子步骤)
- 位置: (44.26, -40.93)
- **作用**: 货架购物流程的步骤控制器

### 节点 4: Sequencer (标签: "check shelf")
- 位置: (-109.6, 26.2)
- **作用**: 货架交互序列

### 节点 5: ActionNode (货架交互动作组)
包含的动作:
1. **SelectTargetShelfAction** - 选择目标货架
2. **AcquireQueueSlotAction** - 获取货架队列位置
3. **MoveToTargetAction** - 移动到货架队列位置
4. **Wait(0.5s)** - 等待0.5秒
5. **DecidePurchaseAction** - 决定购买数量

### 节点 6: ConditionalEvaluator
- 条件: CheckInt (检查 purchaseQuantity > 0)
- **作用**: 如果决定购买数量 > 0，执行购买

### 节点 7: ActionNode (购买动作组)
包含的动作:
1. **ExecutePurchaseAction** - 执行购买
2. **Wait(0.2s)** - 等待0.2秒

### 节点 8: ActionNode (释放货架队列)
包含的动作:
1. **ReleaseQueueSlotAction** (targetShelfId)

### 节点 9: Sequencer (结账离店序列)
- 位置: (137.05, -280.17)
- **作用**: 收银台结账和离店流程

### 节点 10: ActionNode (结账离店动作组)
包含的动作:
1. **SelectCashierAction** - 选择收银台
2. **AcquireQueueSlotAction** - 获取收银台队列位置
3. **MoveToTargetAction** - 移动到收银台
4. **Wait(1.0s)** - 等待1秒
5. **ExecuteCheckoutAction** - 执行结账
6. **ReleaseQueueSlotAction** (targetCashierId)
7. **SelectExitPointAction** - 选择出口
8. **SetVariable (HasReachedTarget = false)** - 重置到达标志
9. **MoveToTargetAction** - 移动到出口
10. **DestroyAgentAction** - 销毁顾客

## 连接关系 (流程图)

```
0 (StepIterator 根节点)
├─→ 1 (Repeater - 购物循环)
│   └─→ 2 (CheckInt: moneyBag > 0?)
│       └─→ 3 (StepIterator - 购物子步骤)
│           ├─→ 4 (Sequencer "check shelf")
│           │   └─→ 5 (ActionList: 选货架 → 获取队列 → 移动 → 等待 → 决定购买)
│           ├─→ 6 (CheckInt: purchaseQuantity > 0?)
│           │   └─→ 7 (ActionList: 执行购买 → 等待)
│           └─→ 8 (ReleaseQueueSlot - 货架)
│
└─→ 9 (Sequencer - 结账离店)
    └─→ 10 (ActionList: 选收银台 → 获取队列 → 移动 → 等待 → 结账 → 释放队列 → 选出口 → 移动 → 销毁)
```

## 实际执行流程

### 阶段1: 购物循环 (节点 1-8)
```
Repeater循环开始
  ↓
检查钱包 > 0? (节点2)
  ↓ Yes
StepIterator执行:
  ├─ Sequencer "check shelf" (节点4-5)
  │   1. SelectTargetShelfAction
  │   2. AcquireQueueSlotAction (货架)
  │   3. MoveToTargetAction (到货架)
  │   4. Wait 0.5s
  │   5. DecidePurchaseAction
  ├─ 检查 purchaseQuantity > 0? (节点6)
  │   ↓ Yes
  │   ExecutePurchaseAction (节点7)
  │   Wait 0.2s
  └─ ReleaseQueueSlotAction (节点8)
  ↓
返回 Repeater 循环
```

### 阶段2: 结账离店 (节点 9-10)
```
当钱包 <= 0 或 Repeater 退出后
  ↓
Sequencer "checkout & exit" (节点9-10)
  1. SelectCashierAction
  2. AcquireQueueSlotAction (收银台)
  3. MoveToTargetAction (到收银台)
  4. Wait 1.0s
  5. ExecuteCheckoutAction
  6. ReleaseQueueSlotAction (收银台)
  7. SelectExitPointAction
  8. SetVariable (HasReachedTarget = false)
  9. MoveToTargetAction (到出口)
  10. DestroyAgentAction
```

## 关键发现

### 1. Repeater 退出条件
- repeaterMode = 1 (RepeatUntilFailure - 直到子节点失败才退出)
- 退出条件:
  - moneyBag <= 0 (节点2条件失败)
  - SelectTargetShelfAction 失败 (没有可选货架)
  - DecidePurchaseAction 失败
  - ExecutePurchaseAction 失败

### 2. 没有 IsAtFrontOfQueueCondition
- **确认**: 该条件节点未被使用
- 顾客到达队列位置后，只等待固定0.5秒就决定购买
- 实际逻辑依赖:
  - ShelfQueueController 自动分配队首位置 (assignedQueueSlot)
  - MoveToTargetAction 移动到该位置
  - Wait 0.5s 给予队列前移时间

### 3. StepIterator 节点3的作用
- 控制货架交互的3个步骤按顺序执行:
  1. check shelf 序列 (节点4-5)
  2. 购买条件检查和执行 (节点6-7)
  3. 释放队列 (节点8)
- **关键**: StepIterator 会按顺序执行所有子节点，即使某些失败也会继续

### 4. 软预留实际上是"硬购买"
- DecidePurchaseAction 调用 `CommerceService.SoftReserve()` (但不锁库存)
- ExecutePurchaseAction 立即调用 `TryPurchase()` → 扣库存
- `pendingPayment` 在 TryPurchase 时累加
- **结论**: "软预留"只是决策阶段的概念，实际购买是立即扣库存

### 5. 无 IsClosingTime 检查
- **当前行为树没有任何闭店状态检查**
- 所有顾客会继续执行正常购物流程
- 需要添加高优先级分支来处理闭店情况

## Repeater 行为分析

根据 NodeCanvas 文档，repeaterMode = 1 对应:
- **RepeatUntilFailure**: 重复执行子节点，直到子节点返回 Failure

### 触发 Repeater 退出的场景:
1. **moneyBag <= 0** (节点2 ConditionalEvaluator 返回 Failure)
2. **SelectTargetShelfAction 失败**:
   - 没有可用货架 (库存为0、队列满、兴趣不匹配)
   - 所有货架的 archetype 都已购买过
3. **AcquireQueueSlotAction 失败**:
   - 货架队列已满
4. **MoveToTargetAction 失败**:
   - 寻路失败 (极端情况)
5. **DecidePurchaseAction 失败**:
   - 决定不购买 (purchaseQuantity = 0)
6. **ExecutePurchaseAction 失败**:
   - 库存不足
   - 钱不够

### Repeater 退出后:
- StepIterator (节点0) 执行下一个子节点
- 进入节点9 (Sequencer "checkout & exit")
- 执行结账离店流程

## 闭店时的潜在问题

### 问题1: Repeater 无法中断
- 如果顾客正在 Repeater 循环中，不会自动退出
- 即使 `isClosingTime = true`，行为树仍会继续执行
- **需要**: 添加外部中断机制

### 问题2: Wait 节点无法跳过
- 节点5中的 Wait(0.5s) 和节点10中的 Wait(1.0s) 会阻塞执行
- **需要**: 闭店时跳过或缩短等待时间

### 问题3: 无法优先执行结账
- 当前结构: Repeater → 购物循环 → 结账离店
- 闭店时: 无法让顾客跳过购物循环，直接进入结账
- **需要**: 添加优先级判断

## 建议的行为树修改方案

### 方案A: 添加根节点优先级选择器 (推荐)
```
Selector (根节点)
├─→ [Priority 1] EmergencyExit Sequence (isClosingTime == true)
│   ├─ CheckClosingTime Condition
│   └─ FastExitActions
└─→ [Priority 2] 原有流程 (StepIterator 节点0)
```

### 方案B: 在 Repeater 内部添加闭店检查
```
Repeater
└─→ Selector
    ├─ CheckClosingTime → BreakLoop (返回 Failure)
    └─ 原有购物逻辑 (节点2-3)
```

### 方案C: 使用事件中断
- DayLoopManager.OnStoreClose 触发时
- 调用 `BehaviourTree.SendEvent("OnStoreClose")`
- 行为树中添加事件监听节点
