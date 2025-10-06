# 商店闭店机制 - 实现总结

## 📦 已完成的工作

### 1. 新增行为树节点（6个）

#### ✅ Conditions（2个）
- **`CheckStoreClosingCondition.cs`** - 检查商店是否闭店
  - 位置: `Assets/Scripts/Customers/NodeCanvas/Conditions/`
  - 功能: 读取 `isClosingTime` 黑板变量

- **`CheckPendingPaymentCondition.cs`** - 检查是否有待结账金额
  - 位置: `Assets/Scripts/Customers/NodeCanvas/Conditions/`
  - 功能: 检查 `pendingPayment > 0`

#### ✅ Actions（4个）
- **`ForceExitShoppingLoopAction.cs`** - 强制退出购物循环
  - 位置: `Assets/Scripts/Customers/NodeCanvas/Actions/`
  - 功能: 停止移动、释放队列、清空货架相关变量

- **`SetUrgentMoveSpeedAction.cs`** - 设置紧急移动速度
  - 位置: `Assets/Scripts/Customers/NodeCanvas/Actions/`
  - 功能: 闭店时速度×2（可配置）
  - 参数: `urgentSpeedMultiplier = 2.0f`

- **`SkipWaitIfClosingAction.cs`** - 条件等待
  - 位置: `Assets/Scripts/Customers/NodeCanvas/Actions/`
  - 功能: 闭店时缩短等待（1.0s → 0.1s）
  - 参数: `normalWaitTime = 1.0f`, `urgentWaitTime = 0.1f`

- **`EmergencyCheckoutAction.cs`** - 紧急结账
  - 位置: `Assets/Scripts/Customers/NodeCanvas/Actions/`
  - 功能: 无收银台时原地结账（兜底方案）

---

### 2. 修改现有节点（2个）

#### ✅ SelectTargetShelfAction.cs
**修改位置**: `OnExecute()` 开头

**新增逻辑**:
```csharp
// 【闭店检查】如果商店闭店且顾客有待结账金额，跳过货架选择
if (adapter.isClosingTime)
{
    if (adapter.pendingPayment > 0)
    {
        // 返回失败，触发 Repeater 退出
        EndAction(false);
        return;
    }
    // 无待结账金额 → 继续正常选货架
}
```

**效果**: 闭店时有待结账金额的顾客跳过购物，打破 Repeater 循环

---

#### ✅ SelectCashierAction.cs
**修改位置**: 策略调用之前

**新增逻辑**:
```csharp
// 【闭店逻辑】如果商店闭店，选择最近的收银台（忽略队列长度）
if (adapter.isClosingTime)
{
    selectedIndex = FindNearestCashierIndex(cashierSnapshots, agent.transform.position);
}
```

**新增方法**: `FindNearestCashierIndex()` - 计算最近收银台

**效果**: 闭店时忽略队列长度，选择最近收银台

---

### 3. 核心系统修改（4个文件）

#### ✅ CustomerBlackboardAdapter.cs
**新增字段**:
```csharp
[Header("商店状态")]
public bool isClosingTime = false; // 商店是否闭店
```

**同步到 NodeCanvas 黑板**:
```csharp
ncBlackboard.SetVariableValue("isClosingTime", isClosingTime);
```

---

#### ✅ CustomerEventBus.cs
**新增事件**:
```csharp
public static event Action<CustomerAgent> OnCustomerDestroyed;
public static void RaiseCustomerDestroyed(CustomerAgent a) => OnCustomerDestroyed?.Invoke(a);
```

**用途**: DayLoopManager 监听顾客销毁事件（未来扩展）

---

#### ✅ DestroyAgentAction.cs
**新增功能**:
1. **队列清理**: 销毁前强制释放货架和收银台队列
2. **事件触发**: 调用 `CustomerEventBus.RaiseCustomerDestroyed()`

**新增方法**:
- `ReleaseShelfQueue()` - 释放货架队列
- `ReleaseCashierQueue()` - 释放收银台队列
- `FindShelfById()` - 查找货架
- `FindFacilityById()` - 查找设施

**效果**: 防止队列泄漏

---

#### ✅ CustomerSpawner.cs - StopSpawning()
**新增逻辑**:
```csharp
// 设置所有在场顾客的 isClosingTime = true
var allCustomers = FindObjectsByType<CustomerAgent>(FindObjectsSortMode.None);
foreach (var customer in allCustomers)
{
    var bb = customer.GetComponent<CustomerBlackboardAdapter>();
    bb.isClosingTime = true;

    // 同步到 NodeCanvas 黑板
    bb.ncBlackboard.SetVariableValue("isClosingTime", true);
}
```

**效果**: 闭店时触发所有顾客进入紧急模式

---

#### ✅ DayLoopManager.cs - TriggerDailySettlement()
**重构逻辑**:
```csharp
private void TriggerDailySettlement()
{
    isStoreOpen = false;
    OnStoreClose?.Invoke(); // 触发 CustomerSpawner.StopSpawning()
    StartCoroutine(WaitForCustomersToLeave()); // 等待顾客离开
}

private IEnumerator WaitForCustomersToLeave()
{
    float timeout = 30f;

    while (true)
    {
        var remaining = FindObjectsByType<CustomerAgent>(...);
        if (remaining.Length == 0) break; // 全部离开

        if (elapsed >= timeout)
        {
            // 超时强制销毁
            foreach (var agent in remaining)
                Destroy(agent.gameObject);
            break;
        }

        yield return new WaitForSeconds(0.5f);
    }

    ShowSettlementUI(); // 显示结算界面
}
```

**效果**:
- 等待所有顾客自然离开
- 30秒超时保护
- 所有顾客离开后才显示结算界面

---

## 🔄 执行流程

### 完整闭店流程

```
1. DayLoopManager.Update()
   └─ currentHour >= settlementHour
      └─ TriggerDailySettlement()

2. TriggerDailySettlement()
   ├─ isStoreOpen = false
   ├─ OnStoreClose?.Invoke()
   └─ StartCoroutine(WaitForCustomersToLeave())

3. OnStoreClose 事件触发
   └─ CustomerSpawner.StopSpawning()
      ├─ isSpawning = false (停止生成新顾客)
      └─ foreach (顾客)
         └─ blackboard.isClosingTime = true

4. 顾客行为树检测到 isClosingTime = true
   ├─ 在 Repeater 循环中
   │  └─ SelectTargetShelfAction 检查
   │     ├─ pendingPayment > 0 → 返回失败（退出循环）
   │     └─ pendingPayment = 0 → 继续购物（可能需要买东西）
   │
   └─ Repeater 退出后进入结账流程
      └─ SelectCashierAction (选择最近收银台)
         └─ 加速移动 → 结账 → 离店

5. WaitForCustomersToLeave()
   ├─ 每0.5秒检查场上顾客数量
   ├─ 数量 = 0 → ShowSettlementUI()
   └─ 超时30秒 → 强制销毁 → ShowSettlementUI()

6. ShowSettlementUI()
   ├─ 计算每日数据
   ├─ OnDailySettlement?.Invoke(data)
   └─ PauseTime()
```

---

## 🎯 设计特点

### ✅ 完全行为树驱动
- 所有闭店逻辑通过行为树节点实现
- 无硬编码状态检查
- NodeCanvas Graph Console 实时可视化

### ✅ 分层防护机制
1. **第一层**: SelectTargetShelfAction - 打破购物循环
2. **第二层**: SetUrgentMoveSpeedAction - 加速移动
3. **第三层**: DestroyAgentAction - 强制队列清理
4. **第四层**: DayLoopManager - 超时强制销毁

### ✅ 智能分流
```
闭店时检查 pendingPayment:
├─ > 0 → 跳过购物 → 选最近收银台 → 加速结账 → 离店
└─ = 0 → 直接离店（或继续购物买东西）
```

### ✅ 容错处理
- **无收银台**: EmergencyCheckoutAction 原地结账
- **队列满**: 闭店时忽略队列限制
- **超时保护**: 30秒后强制清场

---

## 📋 下一步：行为树搭建指南

### 新行为树结构（推荐）

```
Root: Priority Selector
│
├─── [Branch 1 - 最高优先级] Emergency Exit
│    Condition: CheckStoreClosingCondition
│    │
│    └─── Sequencer "Emergency Exit Sequence"
│         ├─── ForceExitShoppingLoopAction (清理购物状态)
│         │
│         ├─── Priority Selector "Exit Strategy"
│         │    ├─── [A] CheckPendingPaymentCondition → Rush Checkout
│         │    │    └─── Sequencer
│         │    │         ├─── SetUrgentMoveSpeedAction
│         │    │         ├─── SelectCashierAction (修改版)
│         │    │         ├─── AcquireQueueSlotAction
│         │    │         ├─── MoveToTargetAction
│         │    │         ├─── SkipWaitIfClosingAction (0.1s)
│         │    │         ├─── Fallback Selector
│         │    │         │    ├─── ExecuteCheckoutAction
│         │    │         │    └─── EmergencyCheckoutAction
│         │    │         └─── ReleaseQueueSlotAction
│         │    │
│         │    └─── [B] No Payment → Direct Exit
│         │
│         └─── Sequencer "Go To Exit"
│              ├─── SelectExitPointAction
│              ├─── SetVariable (HasReachedTarget = false)
│              ├─── SetUrgentMoveSpeedAction
│              ├─── MoveToTargetAction
│              └─── DestroyAgentAction
│
└─── [Branch 2 - 正常优先级] Normal Shopping & Checkout (原有流程)
```

### 在 Unity 编辑器中操作步骤

1. **备份原行为树**: 复制 `CustomerBehaviorTree.asset`
2. **打开 NodeCanvas 编辑器**: Window → NodeCanvas → BehaviourTree Editor
3. **创建根节点**: Priority Selector
4. **构建 Emergency Exit 分支**:
   - 添加 CheckStoreClosingCondition
   - 添加 Sequencer
   - 添加所有新节点
5. **连接原有流程**: 作为 Priority Selector 的第二个子节点
6. **测试**: 运行游戏，观察 Graph Console

---

## 🧪 测试建议

### 测试场景清单

#### ✅ 场景1: 顾客在货架队列中
- 预期: 释放队列 → 离店（无待结账）或加速结账（有待结账）

#### ✅ 场景2: 顾客正在购买
- 预期: 完成购买 → 释放队列 → 加速结账

#### ✅ 场景3: 顾客在收银台队列中
- 预期: 保持队列 → 加速移动 → 正常结账

#### ✅ 场景4: 顾客正在结账
- 预期: 完成结账 → 加速离店

#### ✅ 场景5: 无收银台
- 预期: EmergencyCheckoutAction 原地结账

#### ✅ 场景6: 大量顾客同时清场
- 预期: 所有顾客加速离开，无队列阻塞

#### ✅ 场景7: 超时测试
- 预期: 30秒后强制销毁，显示结算界面

---

## 📊 代码统计

### 新增文件
- 条件节点: 2个
- 动作节点: 4个
- **总计**: 6个新文件

### 修改文件
- 行为树节点: 2个
- 核心系统: 4个
- **总计**: 6个修改文件

### 新增代码行数
- 新节点: ~400行
- 修改逻辑: ~200行
- **总计**: ~600行

---

## 🚀 性能影响

### 预计性能开销
- **Priority Selector 检查**: < 0.01ms per customer per frame
- **isClosingTime 变量读取**: < 0.001ms
- **协程等待**: 每0.5秒检查一次，几乎无开销

### 优化建议
- 正常营业时，第一个条件失败，直接跳过紧急分支
- 闭店时才执行紧急逻辑，不影响正常性能

---

## ⚠️ 已知限制

1. **行为树未重构**: 需要在 Unity 编辑器中手动搭建新结构
2. **NodeCanvas 依赖**: 需要 NodeCanvas 插件支持
3. **测试覆盖**: 需要完整测试所有场景

---

## 🎓 扩展建议

### 其他紧急情况复用
该架构可轻松扩展到其他紧急情况：

#### 火灾疏散
```
Condition: CheckFireAlarm
└─ ForceExitShoppingLoopAction (复用)
   └─ PanicMovement (更高速度倍率 ×3)
      └─ ExitBuilding
```

#### 抢劫事件
```
Condition: CheckRobberyInProgress
└─ HideOrFlee Selector
   ├─ FindHidingSpot
   └─ RunToExit (复用 SetUrgentMoveSpeedAction)
```

#### VIP 优先服务
```
Condition: IsVIPCustomer
└─ SkipQueueAction
   └─ DirectServiceAction
```

---

## 📚 相关文档

- **设计方案**: `StoreClosing_BehaviorTree_Design.md`
- **行为树分析**: `BehaviorTree_Structure_Analysis.md`
- **项目架构**: `CLAUDE.md`

---

## ✅ 完成状态

| 任务 | 状态 | 备注 |
|------|------|------|
| 创建新节点 | ✅ 完成 | 6个节点 |
| 修改现有节点 | ✅ 完成 | 2个节点 |
| 修改黑板适配器 | ✅ 完成 | 添加 isClosingTime |
| 修改生成器 | ✅ 完成 | 设置闭店状态 |
| 修改时间管理器 | ✅ 完成 | 等待顾客离开 |
| 添加销毁事件 | ✅ 完成 | 事件总线扩展 |
| **行为树重构** | ⏳ 待完成 | 需在 Unity 编辑器操作 |
| **完整测试** | ⏳ 待完成 | 需运行游戏测试 |

---

## 🎉 总结

本次实现完全基于**行为树驱动**的设计理念，将闭店逻辑封装在可视化、可配置的节点中。所有代码修改遵循以下原则：

✅ **最小侵入**: 仅添加必要字段和逻辑
✅ **向后兼容**: 不影响原有购物流程
✅ **高度复用**: 节点可用于其他紧急情况
✅ **易于调试**: NodeCanvas Graph Console 实时监控

下一步建议在 Unity 编辑器中搭建新的行为树结构，并进行完整测试。
