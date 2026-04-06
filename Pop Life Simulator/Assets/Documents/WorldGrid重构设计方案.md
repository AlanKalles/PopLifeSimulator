# 新架构设计方案：世界网格 + 独立 Interior Grid

## 动机
FloorTile 的 interior（内部可建造/可行走空间）与世界网格不对齐。内外 cell size 相同（0.5），但 interior 起点有偏移。强行用统一网格会导致格子切到墙壁里，顾客飘在空中。

## 分层架构

### 第一层：世界网格（结构层）
```
WorldGrid (单例, cellSize=0.5, gridSize 可配置)
├── 管 FloorTile 放置位置
├── 管 支撑/堆叠规则（底部必须有 FloorTile，一楼除外）
├── 管 悬空保护
├── 管 FloorTile 之间的结构关系
└── 不管 shelf/电梯/顾客寻路（这些在 interior 层）
```

### 第二层：每个 FloorTileInstance 的 interior grid
```
FloorTileInstance.InteriorGrid
├── cellSize = 0.5（与世界网格相同，默认值）
├── 有自己的 origin offset（相对 FloorTile 锚点的偏移）
├── 有自己的 gridSize（interior 的格子数）
├── 管 shelf 放置
├── 管 电梯锚点放置
├── 管 customer walkable
├── 有自己的 A* GridGraph（运行时生成）
└── 通过 NodeLink2 连接到其他 FloorTile 的 interior
```

### Tile 间连接
```
电梯: NodeLink2 连接两个 FloorTileInstance 的 interior graph
侧边通道: NodeLink2 连接左右紧贴的 FloorTileInstance 的 portal cells
```

---

## 关键设计

### FloorTileArchetype 数据（SO 配置）
```
FloorTileArchetype:
  tileSize (Vector2Int)              — 外部占地格子数（世界网格）

  [Header("Interior")]
  interiorOffset (Vector2)           — interior 左下角相对 tile 锚点的世界空间偏移
  interiorGridSize (Vector2Int)      — interior 的格子数
  interiorCellSize (float = 0.5)     — interior 格子大小

  [Header("Portals")]
  leftPortalCells (List<int>)        — 左侧通道的 local Y 索引列表
  rightPortalCells (List<int>)       — 右侧通道的 local Y 索引列表

  [Header("Customer")]
  walkableRows (int = 1)             — 底部几行可行走
```

### FloorTileInstance 运行时
```
FloorTileInstance:
  InteriorGrid interiorGrid          — 内部网格数据
  GridGraph astarGraph               — 运行时 A* graph

  InitializeInterior()               — 创建 interior grid + A* graph
  PlaceShelfInInterior(arch, pos)    — 在 interior 中放 shelf
  CanPlaceInInterior(fp, pos)        — 检查可放性
  GetInteriorWorldPos(localPos)      — 本地坐标 → 世界坐标
  WorldToInterior(worldPos)          — 世界坐标 → 本地坐标
```

### InteriorGrid 数据结构
```
InteriorGrid:
  Vector2Int gridSize
  float cellSize
  Vector3 originWorld

  bool[,] occupied
  string[,] occupantId
  bool[,] walkable                   — 底部 N 行可走

  Dictionary<string, BuildingInstance> buildings
```

### Shelf 归属建模
```
Shelf 必须记录:
  hostFloorTileInstanceId (string)   — 所属 FloorTileInstance
  localInteriorPosition (Vector2Int) — interior 中的本地坐标

运行时通过 hostFloorTileInstanceId 查到 FloorTileInstance → InteriorGrid
存档时序列化这两个字段
```

### A* GridGraph（每个 FloorTileInstance 一个）
```
运行时创建:
  graph = AstarPath.active.data.AddGraph(typeof(GridGraph))
  SetDimensions(interiorGridSize.x, interiorGridSize.y, interiorCellSize)
  center = interiorWorldOrigin + gridSize/2 偏移
  rotation = (-90, 0, 0)
  collision 关闭
  Scan(graph)
  程序化设置 walkability
```

### 顾客行走模型
```
单 FloorTile 内:
  顾客走 interior 底部 walkableRows 行
  shelf 不阻挡（顾客要前往 shelf）

跨 FloorTile:
  电梯 / 侧边通道 → NodeLink2
  Agent 用 GraphMask.everything 搜索完整路径
  A* 自动通过 NodeLink2 跨 graph 寻路
```

### 电梯双层归属
```
电梯锚点: interior 层（占 interior grid 格子）
结构合法性: WorldGrid 层（相邻、楼层关系、不破坏结构）
放置流程:
  ① 检查 WorldGrid 结构合法性
  ② 检查两端 interior 格子可用性
  ③ 创建 NodeLink2 连接两个 interior graph
```

---

## Portal（侧边通道）设计

### 数据
```
leftPortalCells: List<int>   — 左侧通道 Y 索引
rightPortalCells: List<int>  — 右侧通道 Y 索引
```

### 连接规则
左右紧贴时:
1. 左 tile 的 rightPortalCells vs 右 tile 的 leftPortalCells
2. 世界 Y 坐标重合的格子 → NodeLink2
3. 不重合 → 不连接
4. 空列表 → 该侧封闭

---

## 职责分离

| 职责 | WorldGrid | Interior Grid |
|------|-----------|---------------|
| FloorTile 放置位置 | ✅ | |
| 堆叠/支撑/悬空 | ✅ | |
| 电梯结构合法性 | ✅ | |
| Shelf 放置 | | ✅ |
| 电梯锚点 | | ✅ |
| 顾客寻路 | | ✅ |
| A* GridGraph | | ✅ |
| NodeLink2 | | ✅ |

---

## 与当前架构的变化

### 保留
- WorldGrid 保留结构管理（放置、支撑、悬空）
- FloorTileArchetype / FloorTileInstance 保留
- 电梯 NodeLink2 模式保留
- ConstructionManager Place/Move/Destroy 保留

### 重大变更
- Shelf 从 WorldGrid.Cell[,] → FloorTileInstance.InteriorGrid
- 每个 FloorTileInstance 运行时创建 A* GridGraph
- ConstructionManager 放 shelf 需先确定目标 FloorTile
- BuildingInstance shelf 用 interior 本地坐标

### 移除
- FloorManager
- 统一的 floorTileLayer/interiorLayer
- 跨楼层移动特殊逻辑

### 改造
- FloorDetectionService → InteriorDetectionService（检测 FloorTileInstance + local cell）

---

## 受影响文件（24 个）

### 核心重写
| 文件 | 变更 |
|------|------|
| `FloorGrid.cs` → `WorldGrid.cs` | 单例 + 仅结构管理 |
| `FloorTileInstance.cs` | 新增 InteriorGrid + A* graph |
| `ConstructionManager.cs` | shelf 放置路由到 interior |
| `NavigationService.cs` | 管理多 graph |

### 适配修改
| 文件 | 变更 |
|------|------|
| `BuildingInstances.cs` | 新增 hostFloorTileInstanceId + localInteriorPosition |
| `BuildingArchetypes.cs` | ValidatePlacement 参数改 |
| `FloorTileArchetype.cs` | 新增 interiorOffset/gridSize/portals |
| `FloorDetectionService.cs` | → InteriorDetectionService |
| `CustomerContextBuilder.cs` | 坐标转换用 interior |
| `SortingOrderUtility.cs` | 改用 interior 坐标 |
| `BuildingHighlighter.cs` | 用 WorldGrid + interior |
| `AlanBotPlacementHandler.cs` | 同上 |
| `ShelfListPanel.cs` | 适配 |
| `ResourceManager.cs` | 遍历建筑方式改变 |
| `DayLoopManager.cs` | 同上 |
| `StatsDataManager.cs` | 同上 |
| `QuestProgressTracker.cs` | 同上 |
| `ExecuteCheckoutAction.cs` | 同上 |
| `FloorGridDebugger.cs` | 适配 WorldGrid |
| `FloorEntryDrawer.cs` | 移除 |
| `FloorTileArchetypeEditor.cs` | 适配新 interior 模型 |

---

## 交互分离：建筑操作 vs 结构操作

### 问题
FloorTile 和 Shelf 的 Collider 重叠导致 Raycast 命中不确定。

### 方案
- **FloorTile 不依赖 Collider**，检测走逻辑网格查表（`WorldGrid.WorldToGrid → GetFloorTileAt`）
- **Shelf/Facility 继续走 Collider Raycast**（`InteractableShelf` layer）
- **Move/Destroy 拆分**为建筑操作（默认）和结构操作（显式模式）

### ConstructionManager Mode 扩展
```
Mode { None, Place, Move, Destroy, PlaceElevator, MoveFloorTile, DestroyFloorTile }
```

- `Move` / `Destroy`：默认只检测 `InteractableShelf` layer → 操作 shelf/facility
- `MoveFloorTile` / `DestroyFloorTile`：用 `DetectFloorTileAtWorld()` 逻辑查表 → 操作 FloorTile
- UI 上用子模式切换（Building / FloorTile tab）提供入口
- 公共方法：`BeginMoveFloorTile()`, `BeginDestroyFloorTile()`

### FloorDetectionService 改为逻辑查表
不再依赖 FloorTile 物理 Collider，改为 `WorldGrid.WorldToGrid() → GetFloorTileAt()`。

### FloorTile Prefab
- **不需要 BoxCollider2D**（不再被 Raycast 检测）
- Layer 可保留 FloorTile（不碍事），但不用于物理交互

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
    │       └── FacilityInstance_Cashier
    └── FloorTileInstance_B
        └── InteriorContainer
            └── ...
```

### 设计原则
1. Editor 和 Runtime 彻底分开——不在 Editor 里跑运行时建造逻辑
2. Editor 校验复用运行时规则（IWorldPlaceable / IInteriorPlaceable），但用纯快照（EditorGridSnapshot），不污染真实 WorldGrid
3. 运行时只做注册，不生成布局

### Editor Authoring 文件
| 文件 | 说明 |
|------|------|
| `Scripts/Editor/EditorGridSnapshot.cs` | Editor 侧 WorldGrid 纯快照，含 HasSupport 逻辑副本 + BuildEditorInterior() |
| `Scripts/Editor/WorldGridAuthoringState.cs` | ScriptableSingleton，工具状态持久化 |
| `Scripts/Editor/WorldGridAuthoringWindow.cs` | EditorWindow（菜单：PopLife/WorldGrid Authoring） |
| `Scripts/Editor/WorldGridSceneAuthoring.cs` | SceneView GUI 交互 + GL 批量绘制 |

### 模式
- **PlaceFloorTile**：世界网格可视化 + 点击放 tile（勾选 Mark Default 跳过支撑检查）
- **EraseFloorTile**：悬停高亮 → 点击删除 tile
- **PlaceInteriorBuilding**：选中 FloorTileInstance 后显示 interior 网格 + 点击放建筑
- **EraseInteriorBuilding**：悬停高亮建筑 → 点击删除

### 实例级锁定
`BuildingInstance` 新增 `editorLockedMove` / `editorLockedDestroy` 字段，Editor 中可勾选 Lock Move / Lock Destroy。运行时 ConstructionManager 在 Move/Destroy 入口检查。

### 运行时注册流程
```
WorldGrid.Start()
  → PlacePresetFloors()（如有配置）
  → RegisterAllChildBuildings()
      → Phase 1: 注册 FloorTile（标记 floorTileLayer，调 InitializeInterior）
      → Phase 2: 对每个 tile 调 RegisterExistingBuildingsInInterior()
          → 扫描 tile 子物体（InteriorContainer 下），注册到 InteriorGrid
  → NavigationService.RebuildAllGraphs()
```

---

## 已确认事项
1. WorldGrid gridSize 可配置
2. interior cellSize 默认等于世界 cellSize（0.5）
3. shelf 不阻碍顾客寻路（顾客走底部行，要前往 shelf）
4. 电梯锚点在 interior，结构合法性在 WorldGrid
5. FloorTile 不需要 BoxCollider2D（交互走逻辑查表）
6. Move/Destroy 分为建筑模式和结构模式
7. Interior 建筑放在 FloorTile 的 InteriorContainer 子物体下
8. 所有建筑 canRotate = false
