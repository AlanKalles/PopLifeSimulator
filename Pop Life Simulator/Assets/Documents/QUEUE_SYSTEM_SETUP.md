# 队列系统 + 真实坐标寻路 配置指南

本文档说明如何配置新的队列系统和真实坐标寻路功能。

---

## 📋 系统概述

新架构使用以下核心组件:

1. **ShelfQueueController** - 货架队列管理器
2. **CashierQueueController** - 收银台队列管理器
3. **真实坐标寻路** - 顾客直接寻路到 Transform 位置
4. **RVO 兼容** - 支持局部避障,允许自然排队

---

## 🔧 配置步骤

### 1. 货架配置

对于每个货架 Prefab (`ShelfInstance`):

#### 1.1 添加 ShelfQueueController 组件

```
1. 选中货架 Prefab
2. Add Component → ShelfQueueController
3. 配置以下字段:
```

| 字段 | 说明 | 推荐值 |
|------|------|--------|
| **Interaction Anchor** | 交互点(顾客购买时站立位置) | 创建空 GameObject 放在货架前沿 |
| **Queue Anchor** | 队列起点(第一个排队位置) | 创建空 GameObject 放在交互点后方1米 |
| **Queue Direction** | 队列方向 | (0, -1, 0) 向下排队 |
| **Slot Spacing** | 队位间距 | 1.0 米 |
| **Queue Slots** (可选) | 预设队位数组 | 留空使用自动计算 |
| **Seconds Per Customer** | 每位顾客服务时间 | 10 秒 |
| **Arrival Distance** | 到达容忍距离(RVO兼容) | 0.8 米 |

#### 1.2 创建交互锚点层级结构

推荐在货架下创建以下子对象:

```
ShelfPrefab
├── Model (货架模型)
├── InteractionAnchor (空GameObject)  ← 放在货架前方中央
└── QueueAnchor (空GameObject)       ← 放在 InteractionAnchor 后方1米
```

**InteractionAnchor 位置示例**:
```
货架模型在 (0, 0, 0)
InteractionAnchor 在 (0, -0.5, 0) ← 顾客站在货架前方 0.5 米
QueueAnchor 在 (0, -1.5, 0)      ← 队列从货架前方 1.5 米开始
```

---

### 2. 收银台配置

对于每个收银台 Prefab (`FacilityInstance`, `FacilityType.Cashier`):

#### 2.1 添加 CashierQueueController 组件

```
1. 选中收银台 Prefab
2. Add Component → CashierQueueController
3. 配置字段(同货架配置)
```

| 字段 | 推荐值 |
|------|--------|
| **Interaction Anchor** | 收银台前方 0.5 米 |
| **Queue Anchor** | 收银台前方 1.5 米 |
| **Queue Direction** | (0, -1, 0) 或 (-1, 0, 0) |
| **Slot Spacing** | 1.0 米 |
| **Seconds Per Customer** | 15 秒 (结账通常比浏览慢) |
| **Arrival Distance** | 0.8 米 |

---

### 3. 顾客 Prefab 配置

#### 3.1 确保顾客有以下组件

```
customer1.prefab
├── CustomerAgent
├── CustomerBlackboardAdapter
├── FollowerEntity (A* Pathfinding)
├── AIDestinationSetter (A* Pathfinding)
├── RVOController (A* Pathfinding, 可选但推荐)
├── BehaviourTree (NodeCanvas)
└── Blackboard (NodeCanvas)
```

#### 3.2 RVOController 推荐设置

| 字段 | 推荐值 | 说明 |
|------|--------|------|
| **Max Speed** | 与 moveSpeed 一致 | 自动从 CustomerAgent 同步 |
| **Radius** | 0.3 - 0.5 | 顾客碰撞半径 |
| **Priority** | 0.5 | 越小越优先 |
| **Movement Plane** | XY | 2D 游戏使用 XY 平面 |
| **Locked** | false | 允许 RVO 调整位置 |

---

### 4. 行为树配置

#### 4.1 新的节点流程

**旧流程** (已废弃):
```
SelectAndMoveToShelfAction
```

**新流程**:
```
1. SelectTargetShelfAction       // 策略选择货架
   ↓
2. AcquireQueueSlotAction        // 申请队列位置
   ↓
3. MoveToTargetAction            // 移动到分配的队位
   ↓
4. (等待队首/购买逻辑)
   ↓
5. ReleaseQueueSlotAction        // 释放队位(触发后续顾客前移)
```

#### 4.2 行为树示例配置

```
Sequence
├── SelectTargetShelfAction
│   └── [输出] targetShelfId → blackboard
├── AcquireQueueSlotAction
│   ├── [输入] targetShelfId
│   └── [输出] assignedQueueSlot → blackboard
├── MoveToTargetAction
│   ├── [输入] assignedQueueSlot
│   ├── [输入] moveSpeed
│   └── [输出] hasReachedTarget
├── (购买逻辑)
└── ReleaseQueueSlotAction
    └── [输入] targetShelfId
```

#### 4.3 黑板变量配置

确保 NodeCanvas Blackboard 包含以下变量:

| 变量名 | 类型 | 说明 |
|--------|------|------|
| `targetShelfId` | String | 目标货架 ID |
| `targetCashierId` | String | 目标收银台 ID |
| `assignedQueueSlot` | Transform | 分配的队列位置 |
| `moveSpeed` | Float | 移动速度 |
| `hasReachedTarget` | Bool | 是否到达目标 |
| `policies` | BehaviorPolicySet | 策略集合 |

---

## 🎨 可视化调试

### Scene 视图中的 Gizmo 显示

选中带有 `ShelfQueueController` 或 `CashierQueueController` 的对象时,会显示:

- **绿色球体** - 交互点 (Interaction Anchor)
- **黄色球体** - 队列起点 (Queue Anchor)
- **青色线条** - 队列方向
- **蓝色方块** - 预设队位 (如果配置了 Queue Slots)
- **红色球体** (运行时) - 当前顾客位置和队列序号

---

## ⚙️ 高级配置选项

### 选项 1: 使用预设队位 (固定队列形状)

适用于 L 型队列或特殊形状:

```
1. 在货架下创建多个空 GameObject
   ShelfPrefab
   ├── QueueSlot1
   ├── QueueSlot2
   ├── QueueSlot3
   └── QueueSlot4

2. 在 ShelfQueueController 中:
   Queue Slots[0] = QueueSlot1
   Queue Slots[1] = QueueSlot2
   ...
```

队列会按顺序使用这些预设位置,超出数量后自动计算延伸位置。

---

### 选项 2: 动态队列方向自动计算

如果 `Queue Direction = (0, 0, 0)`,系统会自动计算:

```csharp
queueDirection = (queueAnchor.position - interactionAnchor.position).normalized
```

这样可以通过调整锚点位置自动确定队列方向。

---

## 🐛 常见问题排查

### 问题 1: 顾客无法移动

**检查清单**:
- [ ] 顾客 Prefab 有 `FollowerEntity` 组件
- [ ] 顾客 Prefab 有 `AIDestinationSetter` 组件
- [ ] A* 导航网格已扫描 (`AstarPath.Scan()`)
- [ ] `MoveToTargetAction` 的 `assignedQueueSlot` 参数已连接到黑板变量

---

### 问题 2: 顾客重叠在一起

**可能原因**:
- 未调用 `AcquireQueueSlotAction`(直接移动会重叠)
- `Slot Spacing` 设置过小
- RVO 未启用或 `Radius` 设置过小

**解决方案**:
```
1. 确保行为树流程包含 AcquireQueueSlotAction
2. 增大 Slot Spacing 到 1.0 米以上
3. 启用 RVOController 并设置 Radius = 0.3+
```

---

### 问题 3: 顾客"永远到不了"目标

**可能原因**:
- `Arrival Distance` 设置过小(RVO 会产生偏移)
- 目标点在障碍物内

**解决方案**:
```
1. 增大 MoveToTargetAction.stoppingDistance 到 0.8 米
2. 增大 ShelfQueueController.arrivalDistance 到 0.8 米
3. 确保 InteractionAnchor 不在货架碰撞体内
```

---

### 问题 4: 队列前移不工作

**检查清单**:
- [ ] 购买完成后调用了 `ReleaseQueueSlotAction`
- [ ] 顾客 GameObject 的名称是 `customerId` (用于查找)
- [ ] 顾客有 `CustomerBlackboardAdapter` 和 `AIDestinationSetter`

---

## 📊 性能优化建议

### 1. 缓存优化

ShelfQueueController 会自动缓存动态创建的队位 Transform,无需手动管理。

### 2. 对象池

建议为顾客和队位 GameObject 使用对象池,而不是频繁 `Destroy/Instantiate`。

### 3. 队列长度限制

可以在策略中过滤过长的队列:

```csharp
// WeightedRandomSelector.cs
public int maxQueueLength = 10; // 超过 10 人的队列不考虑
```

---

## 🔄 迁移旧行为树

如果你已有使用旧节点的行为树:

### 替换 SelectAndMoveToShelfAction

**旧**:
```
SelectAndMoveToShelfAction (一个节点)
```

**新**:
```
Sequence
├── SelectTargetShelfAction
├── AcquireQueueSlotAction
└── MoveToTargetAction
```

### 替换 MoveToTargetAction 参数

**旧**:
```
MoveToTargetAction
├── goalCell (Vector2Int)
└── ...
```

**新**:
```
MoveToTargetAction
├── assignedQueueSlot (Transform)  ← 新参数
└── ...
```

---

## 📞 支持

如遇到问题,请检查:

1. Console 中的日志输出(所有操作都有详细日志)
2. Scene 视图中的 Gizmo 显示(选中货架查看队列配置)
3. 运行时 Inspector 查看 `ShelfQueueController.queuedCustomers` 列表

关键日志标记:
- `[ShelfQueueController]` - 队列操作
- `[AcquireQueueSlotAction]` - 申请队位
- `[ReleaseQueueSlotAction]` - 释放队位
- `[MoveToTargetAction]` - 移动状态

---

## ✅ 配置完成检查清单

- [ ] 所有货架添加了 `ShelfQueueController`
- [ ] 所有收银台添加了 `CashierQueueController`
- [ ] 每个货架/收银台配置了 `InteractionAnchor` 和 `QueueAnchor`
- [ ] 顾客 Prefab 有 `RVOController` (推荐)
- [ ] 行为树更新为新的节点流程
- [ ] `CustomerBlackboardAdapter` 包含 `assignedQueueSlot` 字段
- [ ] 测试:顾客能正常排队且队列前移正常工作

---

## 🎯 下一步

配置完成后,建议测试以下场景:

1. **单顾客购买** - 验证基础流程
2. **多顾客排队** - 验证队列分配
3. **队列前移** - 第一个顾客离开后,第二个顾客自动前移
4. **RVO 避障** - 多个顾客靠近时自然避让
5. **队列策略** - 顾客选择最短队列的货架

祝配置顺利! 🎉
