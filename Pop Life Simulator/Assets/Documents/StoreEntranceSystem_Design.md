# 商店入口/出口系统设计文档

## 概述

本文档描述了商店入口/出口系统的设计与实现，该系统通过 A* Pathfinding 的 NodeLink2 连接外部街道和商店内部两个 Grid Graph，实现顾客真实的入店和离店流程。

---

## 核心概念

### 1. 三个关键位置

```
外部街道 (Graph 0)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

  🚶 SpawnPoint (生成点 & 最终撤离点)

              🚪 entranceOutsideAnchor
              ║ (NodeLink2 连接)
━━━━━━━━━━━━━━╬━━━━━━━━━━━━━━━━━━━━
商店一楼        ║  (Graph 1)
              🚪 entranceInsideAnchor

              🛒  🛒  💰
           (货架) (货架) (收银台)
```

- **SpawnPoint（生成点）**: 顾客在外部街道生成的位置，同时也是最终撤离点
- **entranceOutsideAnchor（入口外侧）**: 商店门口外侧锚点（在 Graph 0 上）
- **entranceInsideAnchor（入口内侧）**: 商店门口内侧锚点（在 Graph 1 上）

### 2. 两个 Grid Graph

- **Graph 0（外部街道）**:
  - 覆盖商店外部区域
  - 包含所有 SpawnPoint
  - 包含所有 ExitPoint（最终撤离点）
  - 精度可以较低（节省性能）

- **Graph 1（商店内部）**:
  - 覆盖商店一楼及以上楼层
  - 现有建筑系统所在图形
  - 需要较高精度（支持建筑碰撞）

### 3. NodeLink2 连接

- 连接 `entranceOutsideAnchor` ↔ `entranceInsideAnchor`
- 双向通行（`OneWay = false`）
- A* 会自动选择最优路径穿过 Link

---

## 系统架构

### 组件层次

```
StoreEntrancePoint (MonoBehaviour)
  ├─ entranceId: string
  ├─ outsideAnchor: Transform
  ├─ insideAnchor: Transform
  ├─ nodeLink: NodeLink2
  └─ 自动配置 NodeLink2

EntranceManager (Singleton)
  ├─ entrances: List<StoreEntrancePoint>
  ├─ GetMainEntrance()
  ├─ GetNearestEntrance(position)
  └─ GetOutsideAnchor() / GetInsideAnchor()

CustomerBlackboardAdapter
  ├─ spawnPoint: Transform (最终撤离点)
  ├─ entranceOutsideAnchor: Transform
  ├─ entranceInsideAnchor: Transform
  └─ hasEnteredStore: bool
```

---

## 完整流程

### 顾客生命周期

```
【1. 生成阶段】
CustomerSpawner.SpawnCustomer()
  ↓
在外部街道 SpawnPoint 生成顾客
  ↓
设置 blackboard.spawnPoint = 生成点 ✅
设置 blackboard.entranceOutsideAnchor = 主入口外侧
设置 blackboard.entranceInsideAnchor = 主入口内侧
  ↓
行为树启动

【2. 入店阶段】
MoveToEntranceAction
  ↓
graphMask = 1 << 0 (Graph 0: 外部)
target = entranceOutsideAnchor
  ↓
MoveToTargetAction 寻路到外部锚点
  ↓
到达后，自动切换：
  - target = entranceInsideAnchor
  - graphMask = 1 << 1 (Graph 1: 内部)
  - hasEnteredStore = true
  ↓
A* 自动通过 NodeLink2 进入商店

【3. 购物阶段】
（在 Graph 1 上移动）
SelectTargetShelfAction → 移动 → 购买 → ...

【4. 结账阶段】
SelectCashierAction → 移动 → ExecuteCheckoutAction

【5. 离店阶段】
MoveToExitAction
  ↓
graphMask = 1 << 1 (Graph 1: 内部)
target = entranceInsideAnchor
  ↓
MoveToTargetAction 寻路到内部锚点
  ↓
到达后，自动切换：
  - target = entranceOutsideAnchor
  - graphMask = 1 << 0 (Graph 0: 外部)
  - hasEnteredStore = false
  ↓
A* 自动通过 NodeLink2 离开商店

【6. 回到生成点】
SelectExitPointAction
  ↓
targetExitPoint = spawnPoint ✅
  ↓
MoveToTargetAction 寻路回到生成点
  ↓
到达

【7. 撤离阶段】
DestroyAgentAction → 销毁 GameObject
```

---

## 行为树结构

### 修改后的行为树

```
【根节点 - PrioritySelector】
├─ 【分支1】检查关店条件
│   └─ 应急离店流程（保持现有）
│
└─ 【分支2】正常流程 (Sequencer)
    │
    ├─ 【新增】入店流程 (Sequencer)
    │   └─ MoveToEntranceAction
    │       ├─ 移动到 entranceOutsideAnchor (Graph 0)
    │       ├─ 自动穿过 NodeLink2
    │       ├─ 到达 entranceInsideAnchor (Graph 1)
    │       └─ 设置 hasEnteredStore = true
    │
    ├─ 【保持】购物循环 (Repeater)
    │   └─ 选择货架 → 移动 → 购买 → ...
    │
    └─ 【修改】离店流程 (Sequencer)
        ├─ 选择收银台
        ├─ 结账
        │
        ├─ 【新增】MoveToExitAction
        │   ├─ 移动到 entranceInsideAnchor (Graph 1)
        │   ├─ 自动穿过 NodeLink2
        │   ├─ 到达 entranceOutsideAnchor (Graph 0)
        │   └─ 设置 hasEnteredStore = false
        │
        ├─ 【保持】SelectExitPointAction
        │   └─ 设置 targetExitPoint = spawnPoint ✅
        │
        ├─ 【保持】MoveToTargetAction
        │   └─ 移动回到 spawnPoint (Graph 0)
        │
        └─ 【保持】DestroyAgentAction
```

---

## NodeCanvas Actions 详解

### MoveToEntranceAction

**功能**: 移动到商店入口并进入（外部 → 内部）

**执行流程**:
1. 验证 `entranceOutsideAnchor` 和 `entranceInsideAnchor` 已设置
2. 设置 `graphMask = 1 << 0`（只使用外部图形）
3. 设置目标为 `entranceOutsideAnchor`
4. 开始移动
5. 到达外侧锚点后：
   - 切换目标为 `entranceInsideAnchor`
   - 切换 `graphMask = 1 << 1`（切换到内部图形）
   - A* 自动通过 NodeLink2
   - 设置 `hasEnteredStore = true`
6. 完成

**关键参数**:
- `stoppingDistance`: 0.8f（到达距离）
- `timeoutSeconds`: 30f（超时时间）

### MoveToExitAction

**功能**: 移动到商店出口并离开（内部 → 外部）

**执行流程**:
1. 验证 `entranceInsideAnchor` 和 `entranceOutsideAnchor` 已设置
2. 设置 `graphMask = 1 << 1`（使用内部图形）
3. 设置目标为 `entranceInsideAnchor`
4. 开始移动
5. 到达内侧锚点后：
   - 切换目标为 `entranceOutsideAnchor`
   - 切换 `graphMask = 1 << 0`（切换到外部图形）
   - A* 自动通过 NodeLink2
   - 设置 `hasEnteredStore = false`
6. 完成

**两阶段检测**:
- **阶段1**: 检测到达内侧锚点
- **阶段2**: 检测到达外侧锚点（穿过 Link2 后）

---

## Graph Mask 管理

### 关键设计

为了避免重叠区域的寻路冲突，使用 `graphMask` 严格控制顾客在不同阶段使用的图形：

```csharp
// 外部街道阶段（入店前、回到生成点）
followerEntity.pathfindingSettings.graphMask = 1 << 0;  // 只使用 Graph 0

// 商店内部阶段（购物、结账）
followerEntity.pathfindingSettings.graphMask = 1 << 1;  // 只使用 Graph 1
```

### 状态转换表

| 阶段 | graphMask | 当前位置 | 目标位置 | 备注 |
|------|-----------|----------|----------|------|
| 生成后 | `1 << 0` | SpawnPoint | entranceOutsideAnchor | 外部街道 |
| 入店中 | `1 << 0` → `1 << 1` | entranceOutsideAnchor | entranceInsideAnchor | 穿过 Link2 时切换 |
| 购物中 | `1 << 1` | 商店内 | 货架/收银台 | 商店内部 |
| 离店中 | `1 << 1` → `1 << 0` | entranceInsideAnchor | entranceOutsideAnchor | 穿过 Link2 时切换 |
| 回生成点 | `1 << 0` | entranceOutsideAnchor | SpawnPoint | 外部街道 |

---

## 场景配置步骤

### 1. 创建外部 Grid Graph

在 A* Inspector 中：
1. 添加新的 Grid Graph（Graph 0）
2. 设置 `Center` 和 `Size` 覆盖外部街道区域
3. 设置 `Node Size`（可以比内部图粗糙一些，如 1.5f）
4. 设置 `Collision Testing`：
   - Type: Circle
   - Diameter: 0.5f
   - Layer Mask: 障碍物层
5. 扫描图形

### 2. 创建商店入口点

**步骤 A：创建 NodeLink2**
1. 在场景中创建空 GameObject，命名为 "MainEntranceLink"
2. 位置：放在门外（外部街道上，Graph 0 可行走区域）
3. 添加组件：`NodeLink2`
4. 配置 NodeLink2：
   - `end`：拖拽一个门内的 Transform（商店一楼内，Graph 1 可行走区域）
   - `oneWay`: ✗（取消勾选，双向通行）
   - `Graph Mask`: -1（所有图形都可使用）
   - `Cost Factor`: 1000

**步骤 B：创建 StoreEntrancePoint**
1. 创建另一个空 GameObject，命名为 "MainEntrance"
2. 添加组件：`StoreEntrancePoint`
3. 配置：
   - `entranceId`: "MainEntrance"
   - `isMainEntrance`: ✓
   - `nodeLink`: 拖拽上面创建的 NodeLink2 组件
4. 运行时自动设置：
   - `outsideAnchor` = `nodeLink.StartTransform`（NodeLink2 自身位置）
   - `insideAnchor` = `nodeLink.EndTransform`（nodeLink.end 设置的位置）

**关键理解**：
- NodeLink2 的 `start` = 其自身的 transform 位置（门外）
- NodeLink2 的 `end` = Inspector 中手动设置的目标 Transform（门内）
- `StartTransform` 和 `EndTransform` 是只读属性，用于运行时获取

### 3. 创建 EntranceManager

在场景中：
1. 创建空 GameObject，命名为 "EntranceManager"
2. 添加 `EntranceManager` 组件
3. 配置：
   - `autoDiscoverEntrances`: true

### 4. 调整生成点

将 `CustomerSpawner.spawnPoints` 移动到外部街道（Graph 0 上）

### 5. 调整出口点

将 `ExitPoint` GameObject 移动到外部街道远端（远离入口的位置）

---

## 调试技巧

### Gizmos 可视化

**StoreEntrancePoint**:
- 外部锚点：蓝色球体
- 内部锚点：绿色球体
- 连接线：黄色虚线
- 主入口：橙色圆环

### 日志跟踪

关键日志输出：
```
[MoveToEntranceAction] 顾客 C001 开始移动到入口外侧
[MoveToEntranceAction] 顾客 C001 到达入口外侧，准备进入商店
[MoveToEntranceAction] 顾客 C001 已进入商店，graphMask 切换到内部图形

[MoveToExitAction] 顾客 C001 开始移动到出口内侧
[MoveToExitAction] 顾客 C001 到达出口内侧，准备离开商店
[MoveToExitAction] 顾客 C001 开始穿过出口到外部，graphMask 切换到外部图形
[MoveToExitAction] 顾客 C001 已离开商店到达外部街道
```

### Scene 视图调试

1. 选中顾客查看 `CustomerBlackboardAdapter`：
   - `hasEnteredStore`：当前是否在店内
   - `entranceOutsideAnchor`：入口外侧引用
   - `entranceInsideAnchor`：入口内侧引用

2. 选中 `FollowerEntity` 查看：
   - `graphMask`：当前使用的图形掩码
   - `destination`：当前寻路目标

3. A* 路径可视化：
   - 启用 A* Inspector 中的 "Show Paths" 选项

---

## 已知问题与解决方案

### 问题 1: 顾客卡在入口

**症状**: 顾客到达 `entranceOutsideAnchor` 后无法进入

**可能原因**:
- NodeLink2 未正确配置
- 两个锚点距离过近或过远
- graphMask 设置错误

**解决方案**:
1. 检查 NodeLink2.Start 和 End 是否正确设置
2. 调整锚点距离（建议 1-3 米）
3. 验证 `autoConfigureNodeLink = true`
4. 检查日志中的 graphMask 切换

### 问题 2: 重叠区域寻路错误

**症状**: 顾客在重叠区域选择错误的图形

**解决方案**:
- 严格使用 graphMask 限制
- 确保 MoveToEntranceAction 和 MoveToExitAction 正确切换 graphMask
- 减少两个图形的重叠范围

### 问题 3: 顾客无法离开商店

**症状**: 顾客结账后卡在商店内

**可能原因**:
- 行为树中缺少 MoveToExitAction 节点
- `entranceInsideAnchor` 未设置

**解决方案**:
1. 检查行为树结构
2. 验证 `CustomerSpawner` 正确设置了入口锚点
3. 检查 EntranceManager 是否存在且有效

---

## 性能优化建议

1. **外部 Grid Graph 精度**：
   - 使用较大的 `Node Size`（如 1.5-2.0）
   - 减少不必要的碰撞检测层

2. **NodeLink2 数量**：
   - 避免过多的 Link（每个入口只需一个）
   - 合理设置 `costFactor`

3. **图形扫描**：
   - 仅在必要时重新扫描（建筑变化时）
   - 使用局部更新而非全局扫描

---

## 未来扩展

### 多入口支持

当前系统已支持多入口：
1. 创建多个 `StoreEntrancePoint`
2. 设置不同的 `entranceId`
3. 可以通过 `EntranceManager.GetNearestEntrance(position)` 智能选择

### 入口拥堵控制

可以添加：
- 入口队列系统（类似收银台）
- 入口流量限制
- 智能分流到其他入口

### VIP 入口

可以添加：
- 根据顾客忠诚度选择不同入口
- VIP 入口跳过排队

---

## 文件清单

### 新增文件

- `Assets/Scripts/Runtime/StoreEntrancePoint.cs`
- `Assets/Scripts/Manager/EntranceManager.cs`
- `Assets/Scripts/Customers/NodeCanvas/Actions/MoveToEntranceAction.cs`
- `Assets/Scripts/Customers/NodeCanvas/Actions/MoveToExitAction.cs`
- `Assets/Documents/StoreEntranceSystem_Design.md`

### 修改文件

- `Assets/Scripts/Customers/Runtime/CustomerBlackboardAdapter.cs`
  - 新增：`entranceOutsideAnchor`, `entranceInsideAnchor`, `hasEnteredStore`
- `Assets/Scripts/Customers/Spawner/CustomerSpawner.cs`
  - 修改：`SpawnCustomer()` 方法，添加入口锚点设置
- `Assets/CustomerBehaviorTree.asset`（需要在 Unity 编辑器中修改）
  - 新增：入店流程节点（MoveToEntranceAction）
  - 修改：离店流程，添加 MoveToExitAction

---

## 总结

商店入口/出口系统通过以下设计实现了真实的顾客进出店流程：

✅ **清晰的概念分离**：生成点 ≠ 入口点 ≠ 出口点
✅ **Graph Mask 管理**：严格控制不同阶段的图形使用
✅ **NodeLink2 自动穿越**：无需手动编写穿越逻辑
✅ **保持向后兼容**：保留 `spawnPoint` 作为最终撤离点
✅ **支持多入口扩展**：架构支持未来添加多个入口
✅ **完善的调试工具**：Gizmos 和日志支持开发调试

该系统为后续扩展（多楼层、电梯、VIP 入口等）打下了坚实的基础。
