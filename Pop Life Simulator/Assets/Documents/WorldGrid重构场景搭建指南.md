# WorldGrid 重构 — 场景搭建完整指南

本文档说明如何将旧的 FloorGrid/FloorManager 场景迁移到新的 WorldGrid + InteriorGrid + Editor Authoring 架构。

---

## 第一步：清理旧架构 GameObject

### 删除
| 对象 | 原因 |
|------|------|
| `FloorGrid2` | 多楼层概念已移除，新架构用 WorldGrid 单例 |
| `FloorGrid3` | 同上 |
| `FloorGrid4` | 同上 |
| `FloorManager` GameObject | 整个类已删除 |
| `Floor2` / `Floor3` / `Floor4`（如果是独立容器） | 旧楼层容器 |

### 保留但修改
| 对象 | 操作 |
|------|------|
| `FloorGrid1` | 改名为 `WorldGrid`，替换组件（见下方） |
| `Floor1`（FloorGrid1 的子容器） | 改名为 `BuildingContainer`，保留为 WorldGrid 的子物体 |
| `MainEntrance` | 保留，StoreEntrancePoint + NodeLink2 不受影响 |
| `EntranceExit` | 保留，作为 NodeLink2 的终点锚点 |
| `ConsrtuctionManager` | 保留，清理 Missing 引用 |
| `Service`（NavigationService） | 保留，清理 Missing 引用 |
| `A*`（AstarPath） | 保留，清空 Graphs 列表 |
| 所有 Cashier/Shelf prefab 实例 | 暂时移到场景根目录，后续重新摆放 |

### 完全不动
| 对象 | 原因 |
|------|------|
| `EntranceManager` | 不依赖 FloorGrid/FloorManager |
| 所有 UI Canvas | 不受影响 |
| `DayLoopManager` | 代码已适配 WorldGrid.Instance |
| `ResourceManager` | 代码已适配 |
| `CustomerSpawner` | 不依赖楼层系统 |
| 相机、灯光、背景 | 不受影响 |

---

## 第二步：搭建 WorldGrid

### 2.1 改造 FloorGrid1 → WorldGrid

1. 选中 `FloorGrid1`
2. 改名为 `WorldGrid`
3. 移除旧 `FloorGrid` 组件（会显示 Missing Script）
4. 添加 `WorldGrid` 组件（`PopLife.Runtime.WorldGrid`）
5. 配置 Inspector：

| 字段 | 值 | 说明 |
|------|-----|------|
| `Grid Size` | `80 x 20`（根据世界范围调整） | 世界网格尺寸（cellSize=0.5，所以 80×20 = 40×10 世界单位） |
| `Cell Size` | `0.5` | 每格世界单位大小 |
| `Building Container` | 拖入子物体 `BuildingContainer` | 所有 FloorTile 的父容器 |
| `Origin` | 同 `BuildingContainer` | 网格左下角原点 |
| `Grid Color` | 随意 | Gizmo 颜色 |
| `Preset Floors` | 留空 | 我们用 Editor Authoring 工具放置，不用预设列表 |

6. WorldGrid **不需要** BoxCollider2D 和 FloorDetection Layer（检测走 FloorTileInstance 自身的 collider）

### 2.2 BuildingContainer 层级

最终结构：
```
WorldGrid
└─��� BuildingContainer
    └── （空，后续用 Editor Authoring 工具放 FloorTile）
```

---

## 第三步：配置 FloorTileArchetype SO

路径：`Resources/ScriptableObjects/BuildingArchetype/FloorTile/BaseFloor.asset`

选中 BaseFloor SO，在 Inspector 中配置：

| 字段 | 值 | 说明 |
|------|-----|------|
| `Display Name` | `Base Floor` | |
| `Icon` | 可选 | |
| `Prefab` | `Prefab/FloorTiles/BaseFloor.prefab` | |
| `Build Cost` | `100`（或你的设计值） | |
| `Move Cost` | `50` | |
| `Destroy Refund Rate` | `0.8` | |
| `Tile Size` | `6 x 4`（或你的尺寸） | 世界网格中的占地格子数 |
| **Interior 配置** | | |
| `Interior Offset` | 根据美术量出 | interior 左下角相对 tile 锚点的偏移 |
| `Interior Grid Size` | `8 x 4`（示例） | interior 格子数 |
| `Interior Cell Size` | `0.5` | |
| `Walkable Rows` | `2` | 底部 2 行可行走 |
| `Left Portal Cells` | `[0, 1]` | 左侧通道 Y 索引 |
| `Right Portal Cells` | `[0, 1]` | 右侧通道 Y 索引 |

### Interior 布局图解

以 `Interior Grid Size = 8 x 4, Walkable Rows = 2, Portal = [0,1]` 为例：
```
Row 3: |     | place | place | place | place | place | place |     |
Row 2: |     | place | place | place | place | place | place |     |
Row 1: | walk | PW   |  PW  |  PW  |  PW  |  PW  |  PW  | walk |
Row 0: | walk | walk | walk | walk | walk | walk | walk | walk |
         x=0                                             x=7

walk = 顾客可走   place = 可放建筑   PW = 两者重叠
x=0, x=7 = portal 列（walkable 但不可放建筑）
```

### Interior Offset 如何量

1. 在 Scene 中放一个 BaseFloor prefab
2. 观察 tile 的锚点位置（transform.position）
3. 观察 tile sprite 内部可建造区域的左下角位置
4. `Interior Offset = 内部左下角世界坐标 - tile锚点世界坐标`

---

## 第四步：确保 Prefab 正确

### BaseFloor.prefab (`Prefab/FloorTiles/`)
- 必须有 `FloorTileInstance` 组件
- `Archetype` 字段指向 `BaseFloor` SO
- **不需要 BoxCollider2D**（FloorTile 检测走逻辑查表，不走 Raycast）
- Layer 可保留 `FloorTile`（不碍事，但不用于物理交互）

### Shelf Prefabs (`Prefab/Shelfs/`)
- 每个 shelf prefab 上必须有 `ShelfInstance` 组件
- `Archetype` 字段指向对应的 ShelfArchetype SO
- 有 `Collider2D`（用于交互检测），Layer = `InteractableShelf`

### Cashier.prefab (`Prefab/`)
- 必须有 `FacilityInstance` 组件
- `Archetype` 字段指向 `Cashier` FacilityArchetype SO
- 有 `CashierQueueController` 组件（配置队列锚点）
- 有 `Collider2D`

---

## 第五步：用 Editor Authoring 工具布局

### 5.1 打开工具

`PopLife > WorldGrid Authoring`

### 5.2 放置一楼 FloorTile

1. 工具面板选择 **PlaceFloorTile** 模式
2. `Archetype` 选 `BaseFloor`
3. `Rotation` = 0
4. 勾选 **Mark as Default Floor**（一楼不可移动/拆除）
5. 在 Scene 视图中看到白色网格线
6. 鼠标悬停显示绿色预览 → 左键点击放置
7. 一楼 FloorTile 出现在 `BuildingContainer` 下

放置完成后，Hierarchy 变为：
```
WorldGrid
└── BuildingContainer
    └── BaseFloor(Clone)    ← FloorTileInstance, IsDefault=true
```

### 5.3 放置一楼内的 Cashier

1. 在 Hierarchy 中**选中**刚放的 `BaseFloor(Clone)`
2. 工具面板下半部分激活（Interior 模式）
3. 选择 **PlaceInteriorBuilding** 模式
4. `Archetype` 选 `Cashier`
5. 勾选 **Lock Move** 和 **Lock Destroy**（锁定 Cashier）
6. Scene 视图显示 interior 网格（橙色=可放，绿色=可走）
7. 点击 interior 格子放置 Cashier

放置完成后，Hierarchy 变为：
```
WorldGrid
└── BuildingContainer
    └── BaseFloor(Clone)         ← FloorTileInstance
        └── Cashier(Clone)       ← FacilityInstance, locked
```

### 5.4 放置一楼内的 Shelf

同 5.3 操作，但：
- `Archetype` 选具体的 ShelfArchetype
- **不勾选** Lock Move / Lock Destroy（玩家可以移动/拆除）
- 可放多个 shelf

### 5.5 放置更多 FloorTile（可选）

回到 WorldGrid 模式（点击 Scene 空处取消选中 FloorTileInstance）：
- 选择 PlaceFloorTile
- 不勾选 Mark as Default（二楼及以上可移动）
- 新 tile 必须有支撑（底部紧贴一楼或横向紧贴已有 tile）

---

## 第六步：处理 Entrance 和 Exit

**Entrance/Exit 系统不受 WorldGrid 重构影响。** 保留原有设置：

### MainEntrance（保留不动）
- `StoreEntrancePoint` 组件 + `NodeLink2` 组件
- NodeLink2 连接 `MainEntrance` → `EntranceExit`
- 顾客通过 NodeLink2 在店外/店内之间穿越

### EntranceExit（保留不动）
- 纯 Transform，作为 NodeLink2 的终点
- 位于店内（一楼 FloorTile 的 walkable 区域附近）

### 位置调整
如果一楼 FloorTile 的位置改变了，需要调整：
- `EntranceExit` 的 Transform 位置，确保在一楼 interior 的 walkable 区域范围内
- `MainEntrance` 位置不太需要改（它在店外）

### 顾客进出流程（不变）
```
进店：MoveToEntranceAction
  Phase 1: 走到 MainEntrance (outside anchor)
  Phase 2: 通过 NodeLink2 穿越到 EntranceExit (inside anchor)

出店：MoveToExitAction
  Phase 1: 走到 EntranceExit (inside anchor)
  Phase 2: 通过 NodeLink2 穿越到 MainEntrance (outside anchor)
```

---

## 第七步：清理旧引用

### ConstructionManager Inspector
- `Blueprint Manager` / `Resource Manager` → 确认仍指向正确对象
- `Building Highlighter` → 确认仍指向正确对象
- （代码中已无 floorManager 字段，Inspector 不会有 Missing 引用）

### NavigationService Inspector
- `Astar Path` → 确认指向 `A*` GameObject
- （代码中已无 floorManager 字段，Inspector 不会有 Missing 引用）
- NavigationService 现在自动从 `WorldGrid.Instance` 获取数据

### A* (AstarPath) Inspector
- **清空 Graphs 列表**（运行时 NavigationService 会动态创建 per-tile graph）
- 保持 `Scan On Startup = true`

### 清理 Missing Script
场景中 Service 对象上如有 Missing Script 组件（如旧 `FloorNavigationService`）→ 直接移除

---

## 第八步：配置运行时 Move/Destroy UI（子模式切换）

运行时 Move 和 Destroy 已拆分为两组操作：

| 模式 | 检测方式 | 操作对象 | 入口方法 |
|------|---------|---------|---------|
| Move（默认） | Raycast `InteractableShelf` layer | Shelf / Facility | `BeginMove()` |
| MoveFloorTile | 逻辑查表 `DetectFloorTileAtWorld()` | FloorTile | `BeginMoveFloorTile()` |
| Destroy（默认） | Raycast `InteractableShelf` layer | Shelf / Facility | `BeginDestroy()` |
| DestroyFloorTile | 逻辑查表 `DetectFloorTileAtWorld()` | FloorTile | `BeginDestroyFloorTile()` |

**UI 配置（方式 B：子模式 tab）**：
1. 在现有 Move/Destroy 面板上加一组 tab 或 toggle：`Building` / `FloorTile`
2. Building tab（默认）→ 按钮 OnClick 调 `BeginMove()` / `BeginDestroy()`
3. FloorTile tab → 按钮 OnClick 调 `BeginMoveFloorTile()` / `BeginDestroyFloorTile()`
4. 在 ConstructionManager Inspector 中无需额外配置

**为什么分开**：FloorTile 不加 BoxCollider2D（避免与 Shelf Collider 重叠），Move/Destroy 的 Raycast 只打 `InteractableShelf` layer，点不到 FloorTile。FloorTile 操作走逻辑网格查表。

---

## 第九步：Play 测试验证

### 启动验证
1. 按 Play
2. Console 检查：
   - 无 `FloorGrid` / `FloorManager` 相关报错
   - `WorldGrid` 初始化成功
   - `NavigationService` 创建了 GridGraph（日志：`[NavigationService] 导航网格已重新扫描`）

### 功能验证
| 验证项 | 预期结果 |
|--------|----------|
| 一楼 FloorTile 显示正确 | 位置、大小、sprite 正确 |
| Cashier 在一楼内 | 正确位置，运行时自动注册到 InteriorGrid |
| 预设 Shelf 在一楼内 | 同上 |
| Build Phase 能放新 Shelf | 选 shelf → 点击 interior 区域 → 放置成功 |
| Move 模式点击 Shelf | 选中 shelf（不会误选 FloorTile） |
| Move 模式点击空白区域 | 无反应（FloorTile 不被 Raycast 检测） |
| MoveFloorTile 模式 | 悬停 tile 高亮 → 点击选中空 tile → 拖动 |
| DestroyFloorTile 模式 | 悬停 tile 高亮 → D3.6 检查 → 确认拆除 |
| Build Phase 不能移动一楼 | IsDefault = true → 显示 "Cannot move" |
| Build Phase 不能拆 Cashier | EditorLockedDestroy = true → 显示 "locked" |
| 顾客生成并进店 | 通过 MainEntrance NodeLink2 进入 |
| 顾客走到 Shelf | 在 walkable 行上行走，到达 queue slot |
| 顾客走到 Cashier 结账 | 寻路到 Cashier 的 interaction anchor |
| 顾客出店 | 通过 EntranceExit → MainEntrance NodeLink2 离开 |
| 每日结算正确 | 维护费、销售额、补货正常 |

---

## 常见问题

### Q: Interior 格子对不上美术？
A: 调整 FloorTileArchetype 的 `Interior Offset`。在 Scene 中选中 FloorTileInstance，用 Editor Authoring 工具看 interior 网格可视化，调整到与美术对齐。

### Q: 顾客不走路？
A: 检查：
1. `Walkable Rows` 是否 > 0
2. Interior Offset 是否正确（walkable 行是否覆盖了正确的世界区域）
3. A* GridGraph 是否正确创建（Console 日志）
4. EntranceExit 位置是否在 walkable 区域范围内

### Q: 放不了建筑？
A: 检查：
1. 是否在 placeable 区域（非 portal 列）
2. 是否已被其他建筑占用
3. FloorTileArchetype 的 Interior Grid Size 是否足够大

### Q: Portal 通道不连通？
A: 检查：
1. 两个 FloorTile 在世界网格中是否左右紧贴（A 的右边 x+1 = B 的左边 x）
2. 两边的 Portal Cells Y 索引是否有重叠
3. NavigationService 是否正确创建了 NodeLink2（Console 日志）

### Q: 旧场景中的 Shelf 怎么迁移？
A: 旧场景中手动摆的 Shelf 实例需要重新用 Editor Authoring 工具放置：
1. 记下旧 Shelf 的类型和大概位置
2. 删除旧 Shelf 实例
3. 用 Editor Authoring → PlaceInteriorBuilding 重新放

---

## 总结：操作清单

- [ ] 删除 FloorGrid2/3/4、FloorManager
- [ ] FloorGrid1 → WorldGrid（添加 WorldGrid 组件）
- [ ] 配置 WorldGrid Inspector（gridSize, cellSize, container, origin）
- [ ] 配置 BaseFloor SO（interior 字段）
- [ ] 确认 prefab 组件正确（FloorTileInstance, ShelfInstance, FacilityInstance）
- [ ] Editor Authoring → 放一楼（Default, 含 Cashier + Shelf）
- [ ] 确认 MainEntrance / EntranceExit 位置合理
- [ ] 清理 ConstructionManager / NavigationService 的 Missing 引用
- [ ] 清空 A* Graphs
- [ ] 配置 Move/Destroy UI 子模式切换（Building / FloorTile tab）
- [ ] Play 测试全流程
