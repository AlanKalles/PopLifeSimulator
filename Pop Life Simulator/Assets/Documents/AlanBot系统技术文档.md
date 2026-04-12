# AlanBot 系统技术文档

## 概述

AlanBot 是一个玩家辅助NPC机器人，常驻商店内。不是顾客，不消费。核心功能：

- **巡逻**：在A*图形上水平随机移动（spritesheet帧动画）
- **空闲表情**：随机切换表情sprite + 跳跃动画
- **点击交互**：任何时刻点击AlanBot打断行为，弹出功能面板
- **拖放重定位**：建造阶段可拖放到 InteriorGrid 的 walkable cell，吸附到格子中心
- **Editor Authoring**：通过 WorldGrid Authoring 工具在编辑器中放置

---

## 文件结构

```
Assets/Scripts/AlanBot/
├── AlanBotController.cs              -- 核心控制器（单例）
├── AlanBotAnimator.cs                -- 单sprite动画控制器
├── AlanBotClickHandler.cs            -- 点击检测 + 交互调度
├── AlanBotPlacementHandler.cs        -- 建造阶段拖放（InteriorGrid walkable验证）
├── AlanBotSelectionPanel.cs          -- 选择面板UI
├── NodeCanvas/
│   ├── AlanBotIdleAction.cs          -- BT节点：空闲等待+表情切换
│   └── AlanBotHorizontalWalkAction.cs -- BT节点：水平随机移动
└── UI/
    ├── ItemCodexPanel.cs             -- Item Codex 面板
    ├── CustomerCodexPanel.cs         -- Customer Codex 面板
    └── CalendarPanel.cs              -- Calendar 面板
```

**命名空间**：`PopLife.AlanBot`（主系统）、`PopLife.AlanBot.UI`（子面板）

---

## 组件架构

### 核心组件关系

```
AlanBotController（单例，核心状态管理）
    ├── 引用 AlanBotAnimator（动画控制）
    ├── 获取 AILerp（A*移动）
    ├── 获取 Seeker（A*寻路请求）
    ├── 获取 BehaviourTreeOwner（NodeCanvas行为树）
    ├── 追踪 hostFloorTile（所属 FloorTileInstance）
    ├── 追踪 hostLocalPos（InteriorGrid 本地坐标）
    └── 订阅 WorldGrid.OnStructureChanged（结构变化响应）

AlanBotAnimator（动画状态机）
    ├── 管理 SpriteRenderer（单sprite替换）
    ├── 获取 AILerp（方向翻转依据）
    └── 使用 PrimeTween（跳跃动画）

AlanBotClickHandler（点击交互）
    ├── 引用 AlanBotController
    ├── 引用 AlanBotAnimator
    ├── 自动查找 AlanBotSelectionPanel（FindAnyObjectByType）
    └── 获取 AILerp

AlanBotPlacementHandler（拖放）
    ├── 引用 AlanBotController
    ├── 查找 ConstructionManager（模式检测）
    └── 创建 FloorDetectionService（检测鼠标所在 FloorTile）
```

---

## 核心坐标策略

**AlanBot 根节点始终位于 InteriorGrid walkable cell 中心**：

```
cell中心 = Interior.LocalToWorld(localPos) + (cellSize * 0.5, cellSize * 0.5, 0)
```

- A* `GetNearest()` 直接命中正确寻路节点
- host 归属用 `hostFloorTile` + `hostLocalPos` 表达
- 视觉脚底偏移由 sprite 子物体的 `localPosition.y` 处理（不影响根节点）
- 不再使用 `groundYOffset`

---

## Editor Authoring 放置

AlanBot 通过 WorldGrid Authoring 工具在编辑器中放置，不直接拖入场景。

### 放置流程

1. 打开菜单 **PopLife → WorldGrid Authoring**
2. 在 **AlanBot Mode** 区域，将 `Prefab/AlanBot.prefab` 拖入 **AlanBot Prefab** 字段
3. 点击 **Place AlanBot** 进入放置模式
4. Scene 视图中鼠标移到目标 FloorTile 的 walkable 区域
   - 绿色 = 可放置（walkable + 未被建筑占用）
   - 红色 = 不可放置
5. 左键点击放置

### 放置逻辑

- 自动探测鼠标下方的 FloorTile（通过 WorldGrid 坐标转换）
- 验证 InteriorGrid：`InBounds && IsWalkable && !IsOccupied`
- AlanBot 是单例：若场景中已有 AlanBot 实例，放置时会先删除旧实例
- 实例化到 `WorldGrid.buildingContainer` 下
- 位置吸附到 cell 中心

### 相关编辑器文件

- `Scripts/Editor/WorldGridAuthoringState.cs` — `PlaceAlanBot` 枚举 + `alanBotPrefab` 字段
- `Scripts/Editor/WorldGridSceneAuthoring.cs` — `HandlePlaceAlanBot` + `PlaceAlanBotInScene`
- `Scripts/Editor/WorldGridAuthoringWindow.cs` — AlanBot Mode UI 区域

---

## Prefab 配置

### 1. 创建 AlanBot 层

**路径**：Edit > Project Settings > Tags and Layers

在 Layers 列表中找到一个空位（如 Layer 10），命名为 `AlanBot`。

### 2. AlanBot Prefab 结构

```
AlanBot (Root GameObject)
├── Layer: AlanBot
├── Tag: Untagged
│
├── 组件:
│   ├── Transform
│   ├── SpriteRenderer
│   │     ├── Sorting Layer: InsideStoreLayer
│   │     ├── Order in Layer: 1
│   │     └── Sprite: (拖入 baseModelSprite)
│   │
│   ├── Rigidbody2D
│   │     ├── Body Type: Kinematic
│   │     └── Gravity Scale: 0
│   │
│   ├── BoxCollider2D
│   │     ├── Is Trigger: ✓
│   │     └── Size: (根据sprite大小调整，如 0.5 x 0.8)
│   │
│   ├── AILerp (A* Pathfinding)
│   │     ├── Speed: 2 (或自定义)
│   │     ├── Enable Rotation: ✗ (关闭，方向由sprite flipX控制)
│   │     ├── Orientation: YAxisForward
│   │     └── When Close To Destination: Stop
│   │
│   ├── Seeker (A* Pathfinding)
│   │     └── (保持默认即可，graphMask由代码设置)
│   │
│   ├── BehaviourTreeOwner (NodeCanvas)
│   │     ├── Behaviour Tree: (拖入AlanBot行为树资产)
│   │     ├── Update Mode: Normal Update
│   │     └── (Blackboard保持默认)
│   │
│   ├── AlanBotController
│   │     └── Animator: (拖入自身的AlanBotAnimator组件)
│   │
│   ├── AlanBotAnimator
│   │     ├── Sprite Renderer: (拖入自身的SpriteRenderer)
│   │     ├── Walk Frames: (拖入行走帧sprite数组)
│   │     ├── Walk Frame Interval: 0.15
│   │     ├── Base Model Sprite: (拖入基础表情sprite)
│   │     ├── Expressions: (配置表情列表，见下方)
│   │     ├── Interaction Expression Name: "Speechless"
│   │     ├── Bounce Height: 0.05
│   │     └── Bounce Duration: 0.15
│   │
│   ├── AlanBotClickHandler
│   │     ├── Alan Bot Mask: AlanBot (勾选AlanBot层)
│   │     └── Panel Show Delay: 0.3
│   │     （selectionPanel 由代码自动查找，无需手动拖入）
│   │
│   └── AlanBotPlacementHandler
│         ├── Alan Bot Mask: AlanBot (勾选AlanBot层)
│         ├── Ghost Alpha: 0.5 (拖动时虚影透明度)
│         └── Invalid Tint: (255, 128, 128, 255) 无效位置染色
```

### 3. Expressions 数组配置

在 AlanBotAnimator 的 `Expressions` 数组中添加3个元素：

| Index | Name | Sprite | Skip Bounce |
|-------|------|--------|-------------|
| 0 | Happy | (Happy表情sprite) | ✗ false |
| 1 | Speechless | (Speechless表情sprite) | ✓ true |
| 2 | Bruh | (Bruh表情sprite) | ✗ false |

**扩展**：直接在Inspector中增加数组元素即可添加新表情，无需修改代码。

### 4. 需要的美术资源

| 资源 | 说明 | 格式 |
|------|------|------|
| baseModelSprite | 基础空闲表情 | 单张sprite |
| walkFrames[] | 移动动画帧序列（默认朝左） | sprite数组（建议4-8帧） |
| Happy sprite | 开心表情 | 单张sprite |
| Speechless sprite | 无语表情（也用作交互表情） | 单张sprite |
| Bruh sprite | 无奈表情 | 单张sprite |

所有sprite尺寸应保持一致，确保替换时不会跳动。

---

## 行为树配置

### 创建行为树资产

1. 在 Project 窗口右键 → Create → NodeCanvas → BehaviourTree
2. 命名为 `AlanBotBehaviorTree`
3. 双击打开 NodeCanvas 编辑器

### 行为树结构

```
Root
└── Repeater (repeat forever = true)
    └── Sequencer
        ├── AlanBotIdleAction
        │     ├── Idle Duration Range: (4, 6)
        │     ├── Expression Interval Range: (3, 8)
        │     └── Expression Hold Duration: 2
        │
        └── AlanBotHorizontalWalkAction
              ├── Max Horizontal Distance: 8
              ├── Stopping Distance: 0.5
              ├── Timeout: 10
              └── Y Tolerance: 0.3
```

### 配置步骤

1. 添加 **Repeater** 装饰器节点（设置 `repeatForever = true`）
2. 在 Repeater 下添加 **Sequencer** 组合节点
3. 在 Sequencer 下依次添加：
   - **AlanBotIdleAction**（路径：PopLife/AlanBot）
   - **AlanBotHorizontalWalkAction**（路径：PopLife/AlanBot）
4. 配置各节点参数（Inspector面板可调）
5. 将行为树资产拖到 AlanBot prefab 的 BehaviourTreeOwner 组件的 Behaviour Tree 字段

### 行为循环

```
空闲等待(4-6秒，期间随机切换表情+跳跃)
    → 水平随机移动(到达目标或超时)
    → 空闲等待
    → 水平随机移动
    → ... 无限循环
```

---

## UI 面板配置

### SelectionPanel（选择面板）

在 Canvas 下创建 UI 面板：

```
Canvas (Screen Space - Overlay)
└── AlanBotSelectionPanel
    ├── CanvasGroup (挂载)
    ├── AlanBotSelectionPanel.cs (挂载)
    │
    ├── PanelRoot (空GameObject，用于整体显示/隐藏)
    │   ├── Background (Image, 半透明黑色遮罩)
    │   ├── Panel (Image, 面板背景)
    │   │   ├── Title (TMP_Text: "ALAN BOT")
    │   │   ├── ItemCodexButton (Button)
    │   │   │   └── Text: "Item Codex"
    │   │   ├── CustomerCodexButton (Button)
    │   │   │   └── Text: "Customer Codex"
    │   │   ├── CalendarButton (Button)
    │   │   │   └── Text: "Calendar"
    │   │   └── CloseButton (Button)
    │   │       └── Text: "X"
```

**Inspector配置**：
- Panel Root → 拖入 PanelRoot 对象
- Canvas Group → 拖入 CanvasGroup 组件
- Item Codex Button / Customer Codex Button / Calendar Button → 拖入对应按钮
- Close Button → 拖入关闭按钮
- Item Codex Panel / Customer Codex Panel / Calendar Panel → 拖入子面板

**注意**：`AlanBotClickHandler` 会在 Awake 中自动查找场景中的 `AlanBotSelectionPanel`，不需要在 Inspector 中手动拖入引用。

---

## 核心机制详解

### Host FloorTile 追踪

AlanBot 追踪自己所属的 FloorTileInstance 和在其 InteriorGrid 中的本地坐标：

```csharp
private FloorTileInstance hostFloorTile;  // 当前所在的 FloorTile
private Vector2Int hostLocalPos;           // InteriorGrid 中的本地坐标
private Vector3 lastHostInteriorOrigin;    // 缓存 origin，用于检测 tile 移动
```

**解析优先级**（启动和加载时）：
1. 从 ES3 存档的 `hostTileId` 通过 `WorldGrid.GetFloorTileById()` 解析
2. 从当前世界坐标通过 `WorldGrid.WorldToGrid()` + `GetFloorTileAt()` 推断
3. Fallback：找 `IsDefault == true` 的 FloorTile

**初始化时序**：`Start()` 中用 coroutine 延迟一帧（`yield return null`），确保 `WorldGrid.Start()` 和 `NavigationService.RebuildAllGraphs()` 先完成。

### 结构变化响应

AlanBot 订阅 `WorldGrid.OnStructureChanged`，处理三种情况：

| 事件 | 检测方式 | 响应 |
|------|---------|------|
| Host tile 被拆除 | `hostFloorTile == null`（Unity fake-null） | `RelocateToDefaultTile()` 移到 default tile |
| Host tile 被移动 | `Interior.OriginWorld != lastHostInteriorOrigin` | 用 `hostLocalPos` 在新位置重定位 |
| A* graph 重建 | 任何结构变化 | 延迟一帧 `BindToNearestGraph()`（防重复 coroutine） |

### 动画状态机

```
                     SetWalking(true)
         ┌──────────────────────────────┐
         │                              ▼
       Idle ◄──── FinishingWalk ◄── Walking
         │              ▲
         │              │ SetWalking(false)
         │
         │ ShowExpression()
         ▼
    Expression ──(skipBounce=false)──► Bouncing ──(完成)──► Expression
         │                                                     │
         │ ReturnToBaseModel()                                 │
         ▼                                                     │
       Idle ◄──────────────────────────────────────────────────┘
         │
         │ ShowInteractionExpression()
         ▼
    Interaction ──(面板关闭, ReturnToBaseModel())──► Idle
```

### 跳跃动画（PrimeTween）

跳两下的 Y 轴位移序列：

```
原始Y → +bounceHeight (EaseOutQuad, 0.15s)
      → 原始Y          (EaseInQuad,  0.15s)
      → +bounceHeight (EaseOutQuad, 0.15s)
      → 原始Y          (EaseInQuad,  0.15s)
总时长 = bounceDuration × 4 = 0.6s
```

### 移动动画停止逻辑（FinishingWalk）

当 `SetWalking(false)` 调用时：
1. 不立即停止帧动画
2. 继续播放当前帧循环
3. 到达第0帧后停止，切换到 baseModelSprite
4. 期间 `IsFinishingWalk = true`，通知调用方

### 点击交互状态过渡

玩家随时可点击AlanBot，但根据当前动画状态决定过渡方式：

| 当前状态 | 处理方式 |
|---------|---------|
| Walking（移动中） | 立即停止寻路 → 移动动画播回第一帧 → Speechless → 面板 |
| Bouncing（跳跃中） | 等跳完 → Speechless → 面板 |
| Expression（表情中，未跳） | 直接 → Speechless → 面板 |
| Idle（空闲中） | 直接 → Speechless → 面板 |

面板关闭后恢复流程：
```
关闭面板 → ReturnToBaseModel()（从Speechless回来，skipBounce=true所以不跳）
         → ResumeBehavior()（仅营业阶段恢复BT）
```

### A*图形绑定

AlanBot使用与顾客相同的A*寻路系统，但通过 `GraphMask` 限制在当前图形上移动：

```csharp
// 绑定到最近的A*图形
var nearestInfo = AstarPath.active.GetNearest(transform.position);
boundGraphIndex = nearestInfo.node.GraphIndex;
seeker.graphMask = GraphMask.FromGraphIndex(boundGraphIndex);
```

**重绑定时机**：
- 放置完成后（`OnPlacementComplete`）
- WorldGrid 结构变化后（延迟一帧，确保 NavigationService 先重建 graph）
- 启动时（延迟一帧初始化）

水平移动节点使用 `ConstantPath` 获取范围内节点，并过滤同高度节点：

```csharp
var path = ConstantPath.Construct(position, maxHorizontalDistance * 1000);
// 过滤条件：
// 1. node.GraphIndex == boundGraphIndex（同图形）
// 2. Mathf.Abs(nodeY - currentY) < yTolerance（同高度）
// 3. node.Walkable（可行走）
```

### 建造阶段拖放

激活条件：`DayLoopManager.currentPhase == BuildPhase` 且 `ConstructionManager.mode == Move`

拖动规则：
- **虚影状态**：拖动期间AlanBot变为半透明（`ghostAlpha=0.5`）
- 使用 `FloorDetectionService` 检测鼠标所在 FloorTile
- 将鼠标坐标转换为 FloorTile 的 InteriorGrid 本地坐标
- 验证 `InBounds && IsWalkable && !IsOccupied`

虚影颜色反馈：
- 半透明白色 = 有效位置（walkable cell 且未被建筑占用）→ 吸附到格子中心
- 半透明浅红色 = 无效位置 → 自由跟随鼠标

确认放置（左键，仅 walkable 且未占用时生效）：
1. 根节点吸附到 InteriorGrid cell 中心
2. 更新 `hostFloorTile` 和 `hostLocalPos`
3. 恢复正常不透明显示
4. 重置 `FloorDetectionService` 缓存
5. 自动重新绑定最近的A*图形（`BindToNearestGraph`）
6. ES3保存新位置和 hostTileId

### ES3 持久化

保存内容：

```
文件: AlanBot.es3
键名:
  - alanBotPosition (Vector3) — 世界坐标
  - alanBotHostTileId (string) — 所属 FloorTile 的 instanceId
```

保存时机：放置完成时、host tile 移动后重定位时、应用退出时、对象销毁时
加载时机：`Awake` 中加载原始数据，`Start` 延迟一帧后解析 host tile 引用

**fake-null 安全**：保存 hostTileId 时使用 `(hostFloorTile != null) ? hostFloorTile.instanceId : ""`，避免访问已 Destroy 的 Unity 对象。

### DayLoop 事件订阅

| 事件 | AlanBot响应 |
|------|------------|
| OnBuildPhaseStart | PauseBehavior()（暂停BT+停止移动） |
| OnStoreOpen | ResumeBehavior()（恢复BT） |

---

## 边界情况处理

| 场景 | 处理方式 |
|------|---------|
| 首次运行无存档 | ResolveHostTile fallback → 从位置推断 → default tile |
| 加载时 tile 不存在 | GetFloorTileById 返回 null → fallback 同上 |
| Host tile 被拆除 | OnWorldStructureChanged → RelocateToDefaultTile |
| Host tile 被移动 | OnWorldStructureChanged → 用 hostLocalPos 重定位到新位置 |
| A* graph 重建 | 延迟一帧 BindToNearestGraph（防重复 coroutine） |
| 放到被占用的 walkable cell | IsOccupied 检查阻止放置 |
| Default tile 无可用 cell | 扩大搜索到所有 tile → 若仍无则 warning 保持原位 |
| OnStructureChanged 连续触发 | StopCoroutine + 重启，只执行最后一次 rebind |

---

## 参数调优指南

### AlanBotAnimator

| 参数 | 默认值 | 说明 | 调优建议 |
|------|--------|------|---------|
| walkFrameInterval | 0.15s | 每帧持续时间 | 降低=走路更快，0.1-0.2之间 |
| bounceHeight | 0.05 | 跳跃高度（世界单位） | 根据sprite大小调整，太大会显得夸张 |
| bounceDuration | 0.15s | 单次跳跃时长 | 总跳跃时间 = 4 × bounceDuration |

### AlanBotIdleAction

| 参数 | 默认值 | 说明 | 调优建议 |
|------|--------|------|---------|
| idleDurationRange | (4, 6) | 空闲持续秒数范围 | 增大=更长时间站着不动 |
| expressionIntervalRange | (3, 8) | 表情切换间隔 | 减小=更频繁切换表情 |
| expressionHoldDuration | 2s | 表情持续时间 | 太短看不清表情变化 |

### AlanBotHorizontalWalkAction

| 参数 | 默认值 | 说明 | 调优建议 |
|------|--------|------|---------|
| maxHorizontalDistance | 8 | 最大移动距离（格数） | 太大会走很远，太小原地踏步 |
| stoppingDistance | 0.5 | 到达判定距离 | 与AILerp的endReachedDistance匹配 |
| timeout | 10s | 移动超时 | 防止卡死，应大于正常移动时间 |
| yTolerance | 0.3 | Y轴容差 | 确保只选同高度节点 |

---

## 与现有系统的交互

| 系统 | 交互方式 | 说明 |
|------|---------|------|
| DayLoopManager | 事件订阅 | BuildPhase暂停BT，OpenPhase恢复BT |
| ConstructionManager | 状态读取 | 检测Move模式，控制拖放激活 |
| WorldGrid | 事件订阅 + 查询 | OnStructureChanged 响应结构变化；GetFloorTileAt/GetFloorTileById 查询 tile |
| InteriorGrid | 坐标转换 + 验证 | WorldToLocal/LocalToWorld 坐标转换；IsWalkable/IsOccupied 放置验证 |
| FloorDetectionService | 逻辑检测 | 拖放时检测鼠标所在 FloorTile（通过 WorldGrid 坐标转换） |
| A* Pathfinding | AILerp + Seeker | 移动和寻路，graph 重建后自动重绑定 |
| NodeCanvas | BehaviourTreeOwner | 行为树控制 |
| PrimeTween | Sequence + Tween | 跳跃动画 |
| ES3 | Save/Load | 位置 + hostTileId 持久化 |

---

## 扩展指南

### 添加新表情

1. 准备新表情的sprite资源
2. 在AlanBot prefab的 AlanBotAnimator 组件中，展开 `Expressions` 数组
3. 增加一个元素，填写：
   - Name: 表情名（如 "Angry"）
   - Sprite: 拖入sprite
   - Skip Bounce: 是否跳过跳跃动画
4. 无需修改任何代码，IdleAction会自动将新表情纳入随机池

### 添加新功能面板

1. 创建新的面板脚本（参考 `ItemCodexPanel.cs`）
2. 在Canvas中创建对应UI
3. 在 `AlanBotSelectionPanel` 中添加新按钮和面板引用
4. 在 `AlanBotSelectionPanel.cs` 中添加按钮点击处理

### 添加新BT行为

1. 在 `Assets/Scripts/AlanBot/NodeCanvas/` 中创建新 ActionTask
2. 使用 `[Category("PopLife/AlanBot")]` 分类
3. 在NodeCanvas编辑器中将新节点添加到行为树
