# 商店闭店机制 - Unity 编辑器操作指南

## 📋 总览

本指南将带你完成闭店机制的 Unity 编辑器配置，包括：
1. NodeCanvas 行为树重构
2. 黑板变量配置
3. 预制体更新
4. 场景设置

预计完成时间：**30-45分钟**

---

## 🎯 第一步：备份原行为树

### 1.1 在 Unity Project 窗口中
1. 导航到 `Assets/`
2. 找到 `CustomerBehaviorTree.asset`
3. **右键 → Duplicate**
4. 重命名为 `CustomerBehaviorTree_Backup.asset`

> ⚠️ **重要**：保留备份，方便回滚！

---

## 🌳 第二步：重构行为树结构

### 2.1 打开 NodeCanvas 编辑器
1. **Window → NodeCanvas → BehaviourTree Editor**
2. 在 Project 窗口选中 `CustomerBehaviorTree.asset`
3. 编辑器会显示当前行为树

### 2.2 添加黑板变量 `isClosingTime`
在 **Blackboard** 面板中（通常在编辑器底部）：

1. 点击 **[+] Add Variable**
2. 选择类型：**Boolean**
3. 变量名：`isClosingTime`
4. 默认值：`false`

> 📝 **验证**：确认黑板中现在有 `isClosingTime` 变量

---

### 2.3 创建新的根节点结构

#### 当前结构（需要修改）
```
Root: StepIterator (节点0)
├─ Repeater (节点1) - 购物循环
└─ Sequencer (节点9) - 结账离店
```

#### 目标结构（新的）
```
Root: PrioritySelector (新建)
├─ [Priority 1] Emergency Exit (新建)
└─ [Priority 2] Original Flow (原节点0)
```

---

### 2.4 具体操作步骤

#### Step 1: 创建新根节点
1. **右键空白处 → Add Node → Composites → PrioritySelector**
2. 将新节点拖到画布左上角
3. 右键节点 → **Set as Root**

#### Step 2: 连接原有根节点
1. 从 **PrioritySelector** 拖线到**原节点0**（StepIterator）
2. 此时原节点0成为 PrioritySelector 的第二个子节点

#### Step 3: 创建 Emergency Exit 分支
1. **右键空白处 → Add Node → Decorators → ConditionalEvaluator**
2. 配置 ConditionalEvaluator：
   - **isDynamic**: ✅ 勾选
   - **Condition**: 点击选择
     - 类型：选择 **PopLife/Store/CheckStoreClosingCondition**
3. 从 **PrioritySelector** 拖线到 **ConditionalEvaluator**
   - ⚠️ 确保这是 PrioritySelector 的**第一个子节点**（优先级最高）

---

### 2.5 构建 Emergency Exit 子树

在 **ConditionalEvaluator** 下方构建以下结构：

```
ConditionalEvaluator (CheckStoreClosingCondition)
└─ Sequencer "Emergency Exit Sequence"
   ├─ Action: ForceExitShoppingLoopAction
   ├─ PrioritySelector "Exit Strategy"
   │  ├─ ConditionalEvaluator (CheckPendingPaymentCondition)
   │  │  └─ Sequencer "Rush Checkout"
   │  │     ├─ Action: SetUrgentMoveSpeedAction
   │  │     ├─ Action: SelectCashierAction
   │  │     ├─ Action: AcquireQueueSlotAction
   │  │     ├─ Action: MoveToTargetAction
   │  │     ├─ Action: SkipWaitIfClosingAction
   │  │     ├─ Action: ExecuteCheckoutAction
   │  │     └─ Action: ReleaseQueueSlotAction
   │  └─ Sequencer "Direct Exit" (fallback)
   └─ Sequencer "Go To Exit"
      ├─ Action: SelectExitPointAction
      ├─ Action: SetVariable (HasReachedTarget = false)
      ├─ Action: SetUrgentMoveSpeedAction
      ├─ Action: MoveToTargetAction
      └─ Action: DestroyAgentAction
```

---

### 2.6 详细节点配置

#### 节点 A: ForceExitShoppingLoopAction
1. **右键 → Add Node → Actions → Action**
2. 选择 **PopLife/Store/ForceExitShoppingLoopAction**
3. 无需配置参数（自动读取黑板变量）

---

#### 节点 B: PrioritySelector "Exit Strategy"
1. **右键 → Add Node → Composites → PrioritySelector**

##### 子节点 B1: ConditionalEvaluator (CheckPendingPaymentCondition)
1. **Add Node → Decorators → ConditionalEvaluator**
2. 配置：
   - **isDynamic**: ✅ 勾选
   - **Condition**: 选择 **PopLife/Customer/CheckPendingPaymentCondition**

##### 子节点 B1.1: Sequencer "Rush Checkout"
1. **Add Node → Composites → Sequencer**
2. 在此 Sequencer 下添加以下 Action 节点（按顺序）：

**Action 1: SetUrgentMoveSpeedAction**
- 类型：`PopLife/Store/SetUrgentMoveSpeedAction`
- 参数：
  - `urgentSpeedMultiplier`: `2.0`

**Action 2: SelectCashierAction**
- 类型：`PopLife/Customer/SelectCashierAction`
- 黑板变量：
  - `policies`: 连接到黑板的 `policies`
  - `targetCashierId`: 连接到黑板的 `targetCashierId`
  - `goalCell`: 连接到黑板的 `goalCell`

**Action 3: AcquireQueueSlotAction**
- 类型：`PopLife/Customer/Queue/AcquireQueueSlotAction`
- 黑板变量：
  - `targetShelfId`: 连接到黑板的 `targetShelfId`（留空）
  - `targetCashierId`: 连接到黑板的 `targetCashierId`
  - `assignedQueueSlot`: 连接到黑板的 `assignedQueueSlot`

**Action 4: MoveToTargetAction**
- 类型：`PopLife/Customer/MoveToTargetAction`
- 黑板变量：
  - `assignedQueueSlot`: 连接到黑板的 `assignedQueueSlot`
  - `moveSpeed`: 连接到黑板的 `moveSpeed`
  - `hasReachedTarget`: 连接到黑板的 `Has Reached Target`
- 参数：
  - `stoppingDistance`: `0.5`

**Action 5: SkipWaitIfClosingAction**
- 类型：`PopLife/Store/SkipWaitIfClosingAction`
- 参数：
  - `normalWaitTime`: `1.0`
  - `urgentWaitTime`: `0.1`

**Action 6: ExecuteCheckoutAction**
- 类型：`PopLife/Customer/ExecuteCheckoutAction`
- 无需配置（自动读取黑板）

**Action 7: ReleaseQueueSlotAction**
- 类型：`PopLife/Customer/Queue/ReleaseQueueSlotAction`
- 黑板变量：
  - `targetShelfId`: 连接到黑板的 `targetShelfId`（留空）
  - `targetCashierId`: 连接到黑板的 `targetCashierId`

---

##### 子节点 B2: Sequencer "Direct Exit" (fallback)
1. **Add Node → Composites → Sequencer**
2. 这是 PrioritySelector 的第二个子节点（当没有待结账时执行）
3. 暂时留空（或添加日志节点用于调试）

---

#### 节点 C: Sequencer "Go To Exit"
1. **Add Node → Composites → Sequencer**
2. 在此 Sequencer 下添加以下 Action 节点：

**Action 1: SelectExitPointAction**
- 类型：`PopLife/Customer/SelectExitPointAction`
- 黑板变量：
  - `targetExitPoint`: 连接到黑板的 `targetExitPoint`
  - `targetExitId`: 连接到黑板的 `targetExitId`

**Action 2: SetVariable (HasReachedTarget = false)**
- 类型：`NodeCanvas/Tasks/Actions/SetVariable<Boolean>`
- 配置：
  - `valueA`: 连接到黑板的 `Has Reached Target`
  - `valueB`: `false`

**Action 3: SetUrgentMoveSpeedAction**
- 类型：`PopLife/Store/SetUrgentMoveSpeedAction`
- 参数：
  - `urgentSpeedMultiplier`: `2.0`

**Action 4: MoveToTargetAction**
- 类型：`PopLife/Customer/MoveToTargetAction`
- 黑板变量：
  - `assignedQueueSlot`: 连接到黑板的 `targetExitPoint`
  - `moveSpeed`: 连接到黑板的 `moveSpeed`
  - `hasReachedTarget`: 连接到黑板的 `Has Reached Target`

**Action 5: DestroyAgentAction**
- 类型：`PopLife/Customer/DestroyAgentAction`
- 参数：
  - `delay`: `0`

---

## 🎨 第三步：可视化检查

### 3.1 行为树完整结构预览
保存后，你的行为树应该看起来像这样：

```
[Root] PrioritySelector
│
├─ [Priority 1] ConditionalEvaluator (CheckStoreClosingCondition)
│  └─ Sequencer "Emergency Exit"
│     ├─ ForceExitShoppingLoopAction
│     ├─ PrioritySelector "Exit Strategy"
│     │  ├─ ConditionalEvaluator (CheckPendingPaymentCondition)
│     │  │  └─ Sequencer "Rush Checkout" (7 actions)
│     │  └─ Sequencer "Direct Exit"
│     └─ Sequencer "Go To Exit" (5 actions)
│
└─ [Priority 2] StepIterator (原节点0)
   ├─ Repeater (原节点1)
   └─ Sequencer (原节点9)
```

### 3.2 验证清单
✅ PrioritySelector 是根节点
✅ Emergency Exit 是第一个子节点（优先级最高）
✅ 原 StepIterator (节点0) 是第二个子节点
✅ 所有黑板变量正确连接
✅ 所有 Action 节点参数已配置

---

## 🎮 第四步：更新顾客预制体

### 4.1 打开顾客预制体
1. **Project 窗口**：导航到 `Assets/Prefab/`
2. 找到 **Customer.prefab**
3. 双击打开预制体编辑模式

### 4.2 检查组件
确认预制体包含以下组件：
- ✅ `CustomerAgent`
- ✅ `CustomerBlackboardAdapter`
- ✅ `BehaviourTree` (NodeCanvas)
- ✅ `Blackboard` (NodeCanvas)
- ✅ `FollowerEntity` (A* Pathfinding)
- ✅ `AIDestinationSetter` (A* Pathfinding)

### 4.3 配置 BehaviourTree 组件
选中 Customer GameObject，在 Inspector 中：

1. **BehaviourTree 组件**：
   - **Behaviour Tree**: 拖入 `CustomerBehaviorTree.asset`
   - **Blackboard**: 指向同GameObject上的 `Blackboard` 组件
   - **Update Mode**: `Normal Update`

2. **Blackboard 组件**：
   - 点击 **Edit Blackboard Variables**
   - 确认包含所有必需变量：
     - `customerId` (String)
     - `loyaltyLevel` (Int)
     - `moneyBag` (Int)
     - `moveSpeed` (Float)
     - `targetShelfId` (String)
     - `targetCashierId` (String)
     - `targetExitPoint` (Transform)
     - `assignedQueueSlot` (Transform)
     - `pendingPayment` (Int)
     - `purchaseQuantity` (Int)
     - `goalCell` (Vector2Int)
     - `policies` (BehaviorPolicySet)
     - `Has Reached Target` (Boolean)
     - **`isClosingTime` (Boolean)** ← 新增

3. **CustomerBlackboardAdapter 组件**：
   - **Nc Blackboard**: 拖入同GameObject上的 `Blackboard` 组件

### 4.4 保存预制体
- **Ctrl + S** 或 **File → Save**

---

## 🏢 第五步：场景配置

### 5.1 检查 DayLoopManager
1. **Hierarchy** 中找到 `DayLoopManager` GameObject
2. 在 Inspector 中检查：
   - **Store Open Hour**: `12`
   - **Store Close Hour**: `23`
   - **Settlement Hour**: `23`
3. 确认脚本已更新（应该包含 `WaitForCustomersToLeave()` 方法）

### 5.2 检查 CustomerSpawner
1. **Hierarchy** 中找到 `CustomerSpawner` GameObject
2. 在 Inspector 中检查：
   - **Customer Prefab**: 拖入更新后的 `Customer.prefab`
   - **Spawn Points**: 配置生成点（数组）
   - **Max Customers On Floor**: 设置上限（如 `10`）
3. 确认脚本已更新（`StopSpawning()` 方法包含闭店逻辑）

### 5.3 检查 ExitPoint
确保场景中有 `ExitPoint` GameObject：
- 组件：`ExitPoint` 脚本
- Transform：放置在商店出口位置

---

## 🧪 第六步：测试准备

### 6.1 创建测试场景
1. **复制主场景**：`Scenes/Main.unity` → `Scenes/Main_ClosingTest.unity`
2. 在测试场景中：
   - 放置 1-2 个货架
   - 放置 1 个收银台
   - 配置 `DayLoopManager`:
     - `realSecondsPerDay`: `60`（加速测试：1分钟=1天）

### 6.2 调试工具启用
1. **打开 Console 窗口**：`Window → General → Console`
2. **打开 NodeCanvas Graph Console**：
   - Play 模式下
   - 选中任意顾客GameObject
   - Inspector 中 BehaviourTree 组件
   - 点击 **Open Graph**

---

## ✅ 第七步：验证测试

### 7.1 测试场景 1：基础闭店
1. **Play** 游戏
2. 点击"开店"按钮
3. 等待顾客生成
4. 手动调整 `DayLoopManager.currentHour` 到 `22.9`（Inspector 中）
5. **预期结果**：
   - Console 输出：`[CustomerSpawner] 关店，停止自动生成，设置所有顾客闭店状态`
   - Console 输出：`[CustomerSpawner] 设置顾客 XXX 闭店状态`
   - 顾客加速移动
   - 所有顾客离开后显示结算界面

### 7.2 测试场景 2：有待结账金额
1. Play 游戏
2. 等待顾客购买商品（`pendingPayment > 0`）
3. 触发闭店（`currentHour = 23`）
4. **预期结果**：
   - Console 输出：`[SelectTargetShelfAction] 商店闭店，顾客 XXX 跳过购物`
   - 顾客直接前往收银台
   - 加速结账

### 7.3 测试场景 3：无收银台
1. 删除场景中的所有收银台
2. 让顾客购买商品
3. 触发闭店
4. **预期结果**：
   - Console 输出：`[EmergencyCheckout] Customer XXX 紧急结账`
   - 顾客原地结账
   - 直接离店

### 7.4 测试场景 4：超时保护
1. 故意让顾客卡在某个位置（如删除收银台和出口）
2. 触发闭店
3. 等待 30 秒
4. **预期结果**：
   - Console 输出：`[DayLoopManager] 顾客清场超时，强制结算`
   - 强制销毁顾客
   - 显示结算界面

---

## 📊 节点配置速查表

| 节点类型 | 所在命名空间 | 关键参数 |
|---------|-------------|---------|
| **CheckStoreClosingCondition** | PopLife/Store | 无参数 |
| **CheckPendingPaymentCondition** | PopLife/Customer | 无参数 |
| **ForceExitShoppingLoopAction** | PopLife/Store | 无参数 |
| **SetUrgentMoveSpeedAction** | PopLife/Store | `urgentSpeedMultiplier: 2.0` |
| **SkipWaitIfClosingAction** | PopLife/Store | `normalWaitTime: 1.0`, `urgentWaitTime: 0.1` |
| **EmergencyCheckoutAction** | PopLife/Store | 无参数 |

---

## 🐛 常见问题排查

### 问题 1: 找不到新节点类型
**症状**：NodeCanvas 编辑器中搜索不到 `CheckStoreClosingCondition` 等节点

**解决方案**：
1. 回到 Unity 编辑器主界面
2. 等待编译完成（右下角进度条）
3. **Assets → Reimport All**
4. 重新打开 NodeCanvas 编辑器

### 问题 2: 黑板变量未同步
**症状**：运行时 `isClosingTime` 始终为 `false`

**解决方案**：
1. 检查 `CustomerBlackboardAdapter.InjectFromRecord()` 中是否包含：
   ```csharp
   ncBlackboard.SetVariableValue("isClosingTime", isClosingTime);
   ```
2. 检查预制体的 `Blackboard` 组件是否包含 `isClosingTime` 变量

### 问题 3: 顾客不离开
**症状**：闭店后顾客继续购物

**排查步骤**：
1. 打开 NodeCanvas Graph Console
2. 观察行为树执行路径
3. 检查 `CheckStoreClosingCondition` 是否返回 `true`
4. 在 Inspector 中查看顾客的 `CustomerBlackboardAdapter.isClosingTime` 值

### 问题 4: 队列泄漏
**症状**：顾客离开后，队列位置仍被占用

**解决方案**：
1. 确认 `DestroyAgentAction` 包含队列清理逻辑
2. 检查 Console 输出：`[DestroyAgentAction] 释放货架/收银台的队列位置`

---

## 📝 配置检查清单

在测试前，使用此清单确认所有配置完成：

### 行为树配置
- [ ] 黑板中添加了 `isClosingTime` 变量
- [ ] PrioritySelector 设为根节点
- [ ] Emergency Exit 分支为第一个子节点
- [ ] 原 StepIterator 为第二个子节点
- [ ] 所有新节点已添加并配置
- [ ] 黑板变量正确连接

### 预制体配置
- [ ] Customer.prefab 包含所有必需组件
- [ ] BehaviourTree 组件指向 `CustomerBehaviorTree.asset`
- [ ] Blackboard 包含 `isClosingTime` 变量
- [ ] CustomerBlackboardAdapter 的 `ncBlackboard` 已赋值

### 场景配置
- [ ] DayLoopManager 脚本已更新
- [ ] CustomerSpawner 脚本已更新
- [ ] ExitPoint 已放置在场景中
- [ ] 至少有 1 个收银台和 1 个货架

### 代码验证
- [ ] 所有新节点文件存在于 `Assets/Scripts/Customers/NodeCanvas/`
- [ ] CustomerBlackboardAdapter 包含 `isClosingTime` 字段
- [ ] DayLoopManager 包含 `WaitForCustomersToLeave()` 方法
- [ ] CustomerSpawner.StopSpawning() 包含闭店逻辑

---

## 🎯 完成标志

当你完成以上所有步骤后，进行最终验证：

1. **编译无错误**：Console 无红色错误信息
2. **运行测试场景**：Play 模式下顾客正常生成
3. **触发闭店**：手动设置 `currentHour = 23`
4. **观察行为**：
   - 顾客停止生成
   - 在场顾客加速离开
   - 结算界面在所有顾客离开后显示

✅ 如果以上全部通过，恭喜你完成了闭店机制的实现！

---

## 📚 相关文档
- **设计方案**: `StoreClosing_BehaviorTree_Design.md`
- **实现总结**: `StoreClosing_Implementation_Summary.md`
- **行为树分析**: `BehaviorTree_Structure_Analysis.md`

---

## 💡 提示与技巧

### 调试技巧
1. **使用日志节点**：在关键分支添加 `Log` 节点标记执行路径
2. **减速测试**：设置 `Time.timeScale = 0.5f` 观察慢动作
3. **断点调试**：在新节点的 `OnExecute()` 中设置断点

### 性能优化
1. **生成数量控制**：测试时设置 `maxCustomersOnFloor = 3`
2. **寻路优化**：确保 A* Pathfinding 的 Grid Graph 正确配置

### 扩展建议
1. **添加音效**：在闭店时播放"关门"音效
2. **视觉反馈**：顾客头顶显示"匆忙"图标
3. **UI 提示**：屏幕顶部显示"Closing Time!"文字

---

需要我帮你解决任何具体步骤的问题吗？或者你在哪一步遇到了困难？
