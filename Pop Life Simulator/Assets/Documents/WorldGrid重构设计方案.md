# 世界网格 + Interior Grid + 电梯系统 设计方案

## 分层架构

### 第一层：世界网格（结构层）
```
WorldGrid (单例, cellSize=0.5, gridSize 可配置)
├── 管 FloorTile 放置位置
├── 管 支撑/堆叠规则（底部必须有 FloorTile，一楼除外）
├── 管 悬空保护
├── 管 FloorTile 之间的结构关系（IsSameFloorNeighbor / IsUpperFloorNeighbor）
├── 管 楼层层级计算（BFS，default=0，上方+1，下方-1）
├── 持有 ElevatorLinkManager 和 ElevatorArchetype 引用
└── 不管 shelf/电梯内部逻辑/顾客寻路（这些在 interior 层）
```

### 第二层：每个 FloorTileInstance 的 interior grid
```
FloorTileInstance.InteriorGrid
├── cellSize = 0.5（与世界网格相同）
├── 有自己的 origin offset（相对 FloorTile 锚点的偏移）
├── 有自己的 gridSize（interior 的格子数）
├── 三层布尔模型：placeable / occupied / walkable
├── 管 shelf / facility / elevator door 放置
├── 管 customer walkable（底部 walkableRows 行）
├── 有自己的 A* GridGraph（运行时生成）
└── 通过 NodeLink2 连接到其他 FloorTile 的 interior
```

### Tile 间连接
```
电梯: ElevatorLinkManager 自动创建 NodeLink2，连接同一结构分支内的电梯门
Portal: NavigationService 自动创建 NodeLink2，连接左右紧贴的 FloorTile 的 portal cells
```

---

## FloorTileArchetype 数据（SO 配置）

```
FloorTileArchetype:
  tileSize (Vector2Int)              — 外部占地格子数（世界网格）

  [Header("Interior")]
  interiorOffset (Vector2)           — interior 左下角相对 tile 锚点的世界空间偏移
  interiorGridSize (Vector2Int)      — interior 的格子数
  interiorCellSize (float = 0.5)     — interior 格子大小

  [Header("Portals")]
  leftPortalCells (List<int>)        — 从左起的通道列号（0=最左列，1=第二列…）。通道列不可放置建筑；含 0 时最左列亦为通道，可与紧贴左侧的 tile 建立 NodeLink2 连接
  rightPortalCells (List<int>)       — 从右起的通道列号（0=最右列，1=倒数第二列…）。通道列不可放置建筑；含 0 时最右列亦为通道，可与紧贴右侧的 tile 建立 NodeLink2 连接

  [Header("Customer")]
  walkableRows (int = 1)             — 底部几行可行走
```

---

## InteriorGrid 数据结构

```
InteriorGrid:
  Vector2Int gridSize
  float cellSize
  Vector3 originWorld

  bool[,] placeable         — 可放置建筑的格子（portal 列为 false）
  bool[,] occupied          — 已被建筑占用的格子
  bool[,] walkable          — 底部 walkableRows 行 + portal 列
  string[,] occupantId      — 占用者 instanceId

  Dictionary<string, BuildingInstance> buildings
```

---

## 电梯系统

### 架构

| 组件 | 文件 | 职责 |
|------|------|------|
| `ElevatorArchetype` | `Scripts/Data/ElevatorArchetype.cs` | SO 数据：3×4 footprint，费用公式，底行 walkable 校验 |
| `ElevatorDoorInstance` | `Scripts/Runtime/ElevatorDoorInstance.cs` | 门实例：PrimeTween 开合动画，并发保护（activeTraversals 计数），楼层标签 |
| `ElevatorLinkManager` | `Scripts/Runtime/ElevatorLinkManager.cs` | 自动连接管理器：按结构分支分组，全量重建 NodeLink2 |
| `AILerpLinkTeleporter` | `Scripts/Runtime/AILerpLinkTeleporter.cs` | 挂在 NodeLink2 上：门引用存储、注册表、方向判断 |
| `LinkTraversalDetector` | `Scripts/Runtime/LinkTraversalDetector.cs` | 挂在 customer 上：检测电梯 link，接管 AILerp 执行 Teleport 穿越协程 |

### ElevatorDoorInstance

- 继承 `BuildingInstance`，接入 InteriorGrid 占位体系
- 占 3×4 interior 格子（横3竖4），pivot 左下角
- `editorLockedMove = true`（不可移动，拆除后重新放置）
- `MaxLevel = 0`（不可升级），`GetMaintenanceFee() = 0`
- Prefab 结构：
  ```
  ElevatorDoor (ElevatorDoorInstance)
    ├── Background (SpriteRenderer, ElevatorBackgroundLayer, sortingOrder=0)
    ├── Frame (SpriteRenderer)
    ├── DoorLeft (SpriteRenderer)
    ├── DoorRight (SpriteRenderer)
    ├── FloorLabel (TextMeshPro, world-space)
    └── Anchor (空 GameObject, 门口中央底部, 用作 NodeLink2 锚点)
  ```
- 门动画：Awake 缓存左右门初始 localPosition.x，开/关门始终从初始位置计算目标（防累积偏移）
- 并发保护：`activeTraversals` 计数，`OpenDoor()` 时 ++，`NotifyTraversalComplete()` 时 --，归零才关门

### 放置费用

```
cost = ElevatorArchetype.GetPlacementCost(floorLevel)
     = baseCost + |floorLevel| × costPerLevel
```
运行时收费，Editor 不收费。

### 放置校验

`ElevatorArchetype.ValidateInteriorPlacement()`：
1. 标准 `CanPlace(footprint, localPos)` — 占位 + 可放置
2. 底行 3 格必须在 walkable 行 — `IsWalkable(localPos + (x, 0))` for x=0,1,2

### 连接机制：结构分支（branch）自动互通

不依赖坐标对齐。连接完全基于 FloorTile 的结构邻接关系。

**分支定义**：从 root (default floor, 1F) 出发，沿结构树向上遍历。每次遇到分叉（一个超级节点有 2+ 个上层超级节点），每个子超级节点开新 branch。

```
例：
      E     F
       \   /
        A     B
         \   /
          1F

branch 1: {1F, A, E}
branch 2: {1F, A, F}
branch 3: {1F, B}

1F 上电梯 → 全互通（属于所有 branch）
A 上电梯 → 连 branch1 + branch2 内的所有
E 上电梯 → 只连 branch1 内的
B 上电梯 → 只连 branch3 内的
```

**BuildBranchMap 算法**（ElevatorLinkManager）：
1. Step 1 — 同层连通组件：`IsSameFloorNeighbor` 合并为超级节点
2. Step 2 — 超级节点间上下邻接图：`IsUpperFloorNeighbor`
3. Step 3 — 从 root 超级节点 DFS 枚举所有 root-to-leaf 路径
   - 分叉时：新 branchId + 祖先路径所有超级节点也加入新 branch
   - 不分叉：继承当前 branch

**连接规则**：
- 同一 branch 内的门两两直连（直达模型）
- 同一 FloorTile 上的门不互连（防止同层横穿）
- NodeLink2 锚点 = 门的 Anchor Transform 世界坐标

### 电梯穿越（LinkTraversalDetector 协程）

AILerp 不原生支持 NodeLink2 穿越。由 LinkTraversalDetector 检测路径中的电梯 link，到达入口时接管 AILerp 控制权。

**完整流程**：
```
1.  暂停 AILerp (simulateMovement=false)
2.  入口门开门
3.  VisualAnchor 向上 Tween enterOffset（不动 transform）
4.  切换 sorting layer → ElevatorBackgroundLayer
5.  入口门关门
6.  隐藏 sprite (alpha=0)
7.  AILerp.Teleport(exitAnchor, clearPath: true)
8.  等待 |楼层差| × waitPerFloor 秒
9.  恢复 alpha=1
10. 出口门开门
11. 恢复原始 sorting layer
12. VisualAnchor 恢复到初始位置
13. isTraversing = false
14. 恢复 AILerp + SearchPath
15. 延迟后出口门关门
```

**关键设计**：
- `Teleport(pos, clearPath: true)` 清旧路径，防止恢复后沿旧路径飞行
- VisualAnchor Tween 不动 transform，避免 AILerp 状态分叉
- `visualAnchorOriginLocalPos` 缓存初始位置，AbortTraversal 时恢复，防累积偏移
- `isTraversing` 期间 `OnPathComplete` 直接 return，防 autoRepath 触发闪现
- `entryDoorPendingClose` / `exitDoorPendingClose` 独立标志，OnDestroy 按标志回收门计数
- MoveToTargetAction 等行为树 action 检查 `IsTraversingElevator`，穿越中跳过到达判断但不阻止 timeout

### RebuildAllLinks 调用时序

- **运行时放置/拆除**：ConstructionManager → RegisterBuilding/UnregisterBuilding → `ElevatorLinkManager.RebuildAllLinks()`（不需要 RebuildAllGraphs，电梯门不改 walkable 层）
- **场景加载**：`ElevatorLinkManager.Start()` 兜底调用一次 + 刷新所有门楼层标签
- **拆除电梯门**：`ExecuteDestroyBuilding` 中检测 `bi is ElevatorDoorInstance` 后调用 `RebuildAllLinks()`

---

## Portal（侧边通道）设计

### 数据
```
leftPortalCells: List<int>   — 从左起的通道列号（0=最左列）
rightPortalCells: List<int>  — 从右起的通道列号（0=最右列，actualX = w-1-col）
```

### 双重职责
1. **InteriorGrid 层（不可放置）**：列表中的每个列号都把对应整列标记为 `placeable = false`。中间值（不含 0）用于"挖空"中段几列禁止放建筑。walkable 仍仅由 `walkableRows` 决定，与 portal 列无关。
2. **NavigationService 层（跨 tile 连接）**：仅当列表**包含 0** 时，对应边界列才被视为 portal 边界，可与紧贴的相邻 tile 建立 NodeLink2。

### 连接规则
左右紧贴时（`leftMaxX + 1 == rightMinX`）：
1. 左 tile 的 `rightPortalCells.Contains(0)` 且右 tile 的 `leftPortalCells.Contains(0)` → 两侧边界列均为通道列
2. 在两侧 `walkableRows` 内的 local Y 上，按世界 Y 坐标重合（容差 0.01）配对 → NodeLink2 + NormalTraversalLink 标记
3. 任一侧不含 0 → 该侧不开放，无连接
4. 任一侧 `walkableRows <= 0` → 无可走格子，无连接

Portal NodeLink2 没有 AILerpLinkTeleporter，LinkTraversalDetector 跳过不处理。AILerp 自行直线走过。

---

## 职责分离

| 职责 | WorldGrid | InteriorGrid | ElevatorLinkManager |
|------|-----------|--------------|---------------------|
| FloorTile 放置位置 | ✅ | | |
| 堆叠/支撑/悬空 | ✅ | | |
| 楼层层级计算 | ✅ | | |
| Shelf/Facility 放置 | | ✅ | |
| 电梯门放置（占位） | | ✅ | |
| 电梯连接（NodeLink2） | | | ✅ |
| 分支计算 | | | ✅ |
| 顾客寻路 | | ✅ | |
| A* GridGraph | | ✅ | |
| Portal NodeLink2 | | ✅（NavigationService） | |

---

## 交互分离：建筑操作 vs 结构操作

### ConstructionManager Mode
```
Mode { None, Place, Move, Destroy, PlaceElevator, MoveFloorTile, DestroyFloorTile }
```

| 模式 | 检测方式 | 操作对象 |
|------|----------|----------|
| Place | 逻辑查表 → FloorTile → InteriorGrid | Shelf / Facility |
| PlaceElevator | 逻辑查表 → FloorTile → InteriorGrid | ElevatorDoorInstance |
| Move / Destroy | Raycast `InteractableShelf` layer | Shelf / Facility（电梯门 editorLockedMove=true 跳过） |
| MoveFloorTile / DestroyFloorTile | `WorldGrid.WorldToGrid → GetFloorTileAt` | FloorTile |

### 电梯放置（PlaceElevator 模式）
- **单击放门**，保持模式可继续放置
- 预览：3×4 footprint 绿/红色
- 费用：`baseCost + |floorLevel| × costPerLevel`
- 放置后自动触发 `ElevatorLinkManager.RebuildAllLinks()`

### 电梯拆除限制
- 有电梯门的 FloorTile 不能移动/删除（提示 "remove elevator first"）
- 电梯门不可移动（`editorLockedMove = true`）
- 电梯门可拆除（Destroy 模式，有退款）

---

## Editor Authoring 工具

### 层级结构
```
WorldGrid (单例)
└── buildingContainer
    ├── FloorTileInstance_A
    │   ├── (sprite 子物体...)
    │   └── InteriorContainer
    │       ├── ShelfInstance_1
    │       ├── FacilityInstance_Cashier
    │       └── ElevatorDoorInstance_1
    └── FloorTileInstance_B
        └── InteriorContainer
            └── ...
```

### Authoring 模式

| 模式 | 需要选中 FloorTile? | 说明 |
|------|---------------------|------|
| PlaceFloorTile | 否 | 世界网格放置 |
| EraseFloorTile | 否 | 世界网格擦除 |
| PlaceInteriorBuilding | 是（Hierarchy 选中） | 放 shelf/facility |
| EraseInteriorBuilding | 是 | 擦除 interior 建筑 |
| PlaceElevator | **否**（自动探测） | 鼠标悬停自动探测 FloorTile |
| PlaceAlanBot | 否（自动探测） | 放置 AlanBot |

### 运行时注册流程
```
WorldGrid.Start()
  → PlacePresetFloors()
  → RegisterAllChildBuildings()
      → Phase 1: 注册 FloorTile（标记 floorTileLayer，调 InitializeInterior）
      → Phase 2: 对每个 tile 调 RegisterExistingBuildingsInInterior()
  → NotifyStructureChanged()
      → NavigationService.RebuildAllGraphs()（创建 GridGraph + Portal NodeLink2）
      → ElevatorLinkManager.RebuildAllLinks()（创建电梯 NodeLink2）
      → ElevatorLinkManager.RefreshAllDoorLabels()（刷新楼层标签）
```

---

## A* 寻路配置

### GridGraph（每个 FloorTile 一个）
```
graph = AstarPath.data.AddGraph(typeof(GridGraph))
SetDimensions(interiorGridSize.x, interiorGridSize.y, interiorCellSize)
center = interior.CenterWorld
rotation = (-90, 0, 0)    // 2D 模式
collision 全部关闭（程序化 walkability）
```

### Walkability 同步
`SyncGraphWalkability()`：遍历 graph 节点，`node.Walkable = interior.IsWalkable(localPos)`，然后 `RecalculateAllConnections()`。

### GraphMask 管理

| 阶段 | graphMask | 说明 |
|------|-----------|------|
| 店外行走 | `outsideGraphMask` | 只用 outside graph |
| 穿越入口 | `GraphMask.everything` | 允许通过入口 NodeLink2 |
| 店内购物 | `~outsideGraphMask` | 所有 interior graph（含电梯 NodeLink2） |
| 穿越出口 | `GraphMask.everything` | 允许通过出口 NodeLink2 |

---

## 已确认事项
1. WorldGrid cellSize = 0.5，InteriorGrid cellSize = 0.5（两者相同）
2. Shelf 不阻碍顾客寻路（顾客走底部 walkable 行）
3. 电梯门在 InteriorGrid 中注册，连接由 ElevatorLinkManager 管理
4. FloorTile 不需要 BoxCollider2D（交互走逻辑查表）
5. Move/Destroy 分为建筑模式和结构模式
6. Interior 建筑放在 FloorTile 的 InteriorContainer 子物体下
7. AILerp 不原生支持 NodeLink2，电梯穿越由 LinkTraversalDetector 协程管理
8. Portal 穿越由 AILerp 直线走过（两端距离近，视觉可接受）
9. 电梯门 sortingOrder ≥ 1，background 在 ElevatorBackgroundLayer (order=0)
10. Customer VisualAnchor 容器使角色脚底对齐 A* 节点中心
