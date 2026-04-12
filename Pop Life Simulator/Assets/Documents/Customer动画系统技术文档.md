# Customer 动画系统技术文档

## 架构概览

顾客动画系统使用**代码驱动程序化动画 + PrimeTween**，完全脱离 Mecanim Animator。角色由 6 个独立部件组成，通过 Sin 波数学计算驱动行走/待机，PrimeTween 驱动特殊动画，独立 EmojiController 管理表情气泡。

---

## 文件清单

| 文件 | 路径 | 职责 |
|------|------|------|
| `CustomerAnimationController.cs` | `Scripts/Customers/Runtime/` | 核心动画控制器：程序化动画、状态管理、部件管理、sorting layer |
| `CustomerEmojiController.cs` | `Scripts/Customers/Runtime/` | emoji 表情独立管理（循环帧切换 + PrimeTween 单次动画） |
| `CustomerPartLoader.cs` | `Scripts/Customers/Runtime/` | 静态工具类：从 sprite sheet 加载 6 部件精灵 |

---

## Prefab 层级结构

```
customer (root)                          ← transform.position = A* 寻路位置（脚底）
  ├── VisualAnchor (容器)                ← localPosition.y 向上偏移，使角色视觉居中
  │   ├── BodyParts (容器)               ← 翻转时整体 scale.x = ±1
  │   │   ├── Body                       ← SpriteRenderer (sortingOrder ≥ 1)
  │   │   ├── Head                       ← SpriteRenderer (sortingOrder ≥ 1)
  │   │   ├── LeftArm                    ← SpriteRenderer (sortingOrder ≥ 1)
  │   │   │   └── Hold                   ← SpriteRenderer (篮子，sortingOrder > Body，sprite 初始 null)
  │   │   ├── RightArm                   ← SpriteRenderer (sortingOrder ≥ 1)
  │   │   ├── LeftFoot                   ← SpriteRenderer (sortingOrder ≥ 1)
  │   │   └── RightFoot                  ← SpriteRenderer (sortingOrder ≥ 1)
  │   └── emoji                          ← SpriteRenderer (sortingOrder ≥ 1)
  │       └── name                       ← TextMeshPro
  └── [逻辑组件挂在 root 上]
```

### 关键设计
- **VisualAnchor 容器**：所有视觉元素的父对象。`localPosition.y` 向上偏移，使 root 的 transform.position 作为脚底锚点（对齐 A* walkable 节点中心），sprite 身体在上方。电梯穿越时 `LinkTraversalDetector` 通过 Tween VisualAnchor 的 localPosition 实现进出电梯的微小位移动画，不影响 root transform（避免 AILerp 状态分叉）。
- **BodyParts 容器**负责方向翻转：`localScale.x = ±1`，一次翻转所有部位
- **emoji 在 VisualAnchor 下、BodyParts 外**：不随身体翻转，始终朝向玩家
- **所有部件 sortingOrder ≥ 1**：确保在电梯 ElevatorBackgroundLayer 中时，顾客渲染在电梯 background sprite（sortingOrder=0）前面，但被门和货架所在的更高 sorting layer 遮挡
- **部件间 sortingOrder 的相对差值**固定在 Prefab 上，不受 sorting layer 切换影响

---

## 精灵加载系统 (`CustomerPartLoader`)

### 资源路径约定
```
Assets/Resources/CustomerParts/{customerId}_sheet.png
```
- 文件名小写：`c001_sheet.png`, `c006_sheet.png`
- Unity Sprite Mode: Multiple，切成 6 片

### 部件索引约定（`PartIndex` 枚举）

| 索引 | 枚举值 | 部位 |
|------|--------|------|
| 0 | `Head` | 头部 |
| 1 | `LeftFoot` | 左脚 |
| 2 | `RightFoot` | 右脚 |
| 3 | `Body` | 躯干 |
| 4 | `LeftArm` | 左臂 |
| 5 | `RightArm` | 右臂 |

### API
```csharp
// 加载全部 6 部件（内部缓存，不重复加载）
Sprite[] parts = CustomerPartLoader.LoadParts("C006");

// 获取单个部件
Sprite head = CustomerPartLoader.GetPart("C006", PartIndex.Head);

// 清空缓存（场景卸载时）
CustomerPartLoader.ClearCache();
```

### 排序保证
`Resources.LoadAll<Sprite>()` 不保证顺序，加载后按名称后缀数字排序（`c006_sheet_0` → 0, `c006_sheet_3` → 3）。

---

## 动画状态机 (`CustomerAnimationController`)

### 状态枚举
```
AnimState: Idle, Walk, Think, PickProduct, Checkout, Upset, Interaction
```

### 状态转换逻辑
```
┌──────────────────────────────────────────────────────┐
│  正常模式（Update 每帧执行）                           │
│  ai.velocity.magnitude > idleThreshold → Walk        │
│  ai.velocity.magnitude ≤ idleThreshold → Idle        │
│  ai.velocity.x > 0.01 → BodyParts.scale.x = 1       │
│  ai.velocity.x < -0.01 → BodyParts.scale.x = -1     │
└──────────┬───────────────────────────────────────────┘
           │
     外部调用 PlayXxx()
           │
           ▼
┌──────────────────────────────────────────────────────┐
│  特殊动画模式（isPlayingOneShot 或 isPlayingLoop）     │
│  Update 中跳过自动状态切换                              │
│  PrimeTween 或 emojiController 驱动                   │
│  完成后自动恢复 / 手动 StopLoop()                      │
└──────────────────────────────────────────────────────┘
```

### 公共 API

| 方法 | 类型 | 时长 | 行为 |
|------|------|------|------|
| `PlayPickProduct()` | 单次 | ~2s | 身体+头部下蹲 → 回位 + emoji 上升淡出 |
| `PlayCheckout()` | 单次 | 0.67s | emoji 淡入上升 |
| `PlayUpset()` | 单次 | 0.85s | 身体水平衰减抖动 + emoji 不满循环 |
| `PlayThink()` | 循环 | - | emoji 思考帧循环 |
| `PlayInteraction()` | 循环 | - | emoji 交互帧循环 |
| `StopLoop()` | - | - | 停止循环动画，恢复自动控制 |
| `StopCurrentAnimation()` | - | - | 停止所有动画+Tween，重置部件位置 |
| `SetCustomerID(string)` | - | - | 保留接口，当前空实现 |
| `SetupParts(Sprite[])` | - | - | 赋值 6 部件精灵 |
| `SetAllSortingLayer(string)` | - | - | 批量设置所有渲染器+emoji 的 sorting layer |

---

## 程序化动画数学

### Walk 动画

所有计算基于 `sin = Sin(Time.time * footMoveSpeed)`：

```
右脚 Y = originY + Max(0, sin) × footMoveRange       ← 正半波抬起
左脚 Y = originY + Max(0, -sin) × footMoveRange      ← 负半波抬起（180° 相位差）
身体 Y = originY + Abs(sin) × bodyBobAmount           ← 双倍频率上下晃动
头部 Y = originY + Abs(sin) × bodyBobAmount           ← 跟随身体
左臂 Z旋转 = sin × armSwingAngle                       ← 前后摆动
右臂 Z旋转 = -sin × armSwingAngle                      ← 反向摆动
```

### Idle 动画

```
breathe = Sin(Time.time * breatheSpeed) × breatheAmount
身体 Y = originY + breathe
头部 Y = originY + breathe
脚/手臂 = Lerp(当前位置, 原始位置, deltaTime × returnToIdleSpeed)   ← 平滑回位
```

### 参数默认值

| 参数 | 默认值 | 单位 | 说明 |
|------|--------|------|------|
| `footMoveRange` | 0.08 | Unity 世界单位 | 脚步上下幅度 |
| `footMoveSpeed` | 8.0 | 频率系数 | 脚步频率 |
| `bodyBobAmount` | 0.02 | Unity 世界单位 | 身体上下晃动幅度 |
| `armSwingAngle` | 10.0 | 度 | 手臂 Z 轴摆动角度 |
| `breatheSpeed` | 2.0 | 频率系数 | 呼吸频率 |
| `breatheAmount` | 0.015 | Unity 世界单位 | 呼吸幅度 |
| `returnToIdleSpeed` | 5.0 | Lerp 速度 | Walk → Idle 过渡速度 |
| `idleThreshold` | 0.1 | Unity 单位/秒 | 低于此速度视为静止 |

> **重要**：以上距离参数为**绝对值**（Unity 世界单位），不会随 sprite 大小自动缩放。如果角色 sprite 尺寸与默认值不匹配，需要在 Inspector 中手动调节，或后续添加基于 sprite 尺寸的自适应比例逻辑。

---

## 特殊动画实现（PrimeTween）

### PickProduct（~2 秒）
```
1. StopCurrentAnimation() 清理旧状态
2. isPlayingOneShot = true
3. PrimeTween Sequence:
   ├── Group: body.Y → originY - 0.05 (0.4s, OutQuad)    ← 下蹲
   ├── Group: head.Y → originY - 0.05 (0.4s, OutQuad)
   ├── Chain: body.Y → originY (0.3s, InOutQuad)          ← 回位
   ├── Group: head.Y → originY (0.3s, InOutQuad)
   ├── ChainDelay(1.3s)                                    ← 等待
   └── ChainCallback: isPlayingOneShot = false, StopEmoji()
4. emojiController.PlayPickProduct()                        ← emoji 上升+淡出
```

### Checkout（0.67 秒）
```
1. StopCurrentAnimation()
2. isPlayingOneShot = true
3. PrimeTween Sequence:
   ├── ChainDelay(0.67s)
   └── ChainCallback: isPlayingOneShot = false, StopEmoji()
4. emojiController.PlayCheckout()                            ← emoji 淡入+上升
```

### Upset（0.85 秒）
```
1. StopCurrentAnimation()
2. isPlayingOneShot = true
3. PrimeTween Sequence:
   ├── Tween.Custom(0→1, 0.85s):                           ← 衰减水平抖动
   │     shake = Sin(t × PI × 8) × (1-t) × 0.03
   │     bodyPartsContainer.localPosition.x += shake
   └── ChainCallback: 容器恢复到 bodyPartsContainerOriginPos, isPlayingOneShot = false, StopEmoji()
4. emojiController.PlayUpset()                               ← emoji 不满帧循环
```

> **注意**：Upset 抖动结束后恢复到 `bodyPartsContainerOriginPos`（Awake 时缓存的初始位置），而非 `Vector3.zero`。这样 BodyParts 容器的 Y 偏移不会被重置。`ResetAllParts()` 同理。

### Think / Interaction（循环）
```
1. StopCurrentAnimation()
2. isPlayingLoop = true
3. emojiController.PlayThink() 或 PlayInteraction()          ← Update 计时器帧循环
4. 外部调用 StopLoop() 恢复自动控制
```

---

## Emoji 系统 (`CustomerEmojiController`)

### Inspector 配置

| 字段 | 类型 | 说明 |
|------|------|------|
| `emojiRenderer` | SpriteRenderer | emoji 子物体的渲染器 |
| `thinkSprites` | Sprite[] | 思考表情帧数组（循环播放） |
| `upsetSprites` | Sprite[] | 不满表情帧数组（循环播放） |
| `interactionSprites` | Sprite[] | 交互表情帧数组（循环播放） |
| `checkoutSprite` | Sprite | 结账表情（单帧） |
| `pickProductSprite` | Sprite | 拿货表情（单帧） |
| `frameInterval` | float | 循环帧切换间隔（默认 0.3s） |
| `riseDistance` | float | 上升距离（默认 0.3 世界单位） |
| `riseDuration` | float | 上升时长（默认 0.5s） |
| `fadeDuration` | float | 淡入淡出时长（默认 0.3s） |

### 循环播放机制
Update 中使用 `frameTimer` 计时，按 `frameInterval` 间隔循环切换 `currentLoopSprites` 数组中的帧。

### 单次播放机制
使用 `PrimeTween.Sequence` 组合位移（`Tween.LocalPositionY`）和透明度（`Tween.Custom` 驱动 `color.a`）动画。

### 并发清理
`StopEmoji()` 中：
1. `isLooping = false` 停止帧循环
2. `currentSequence.Stop()` 停止 PrimeTween
3. `ResetEmoji()` 重置 sprite/color/position

---

## Sorting Layer 管理

| 时机 | 操作 | 调用链 |
|------|------|--------|
| 进店 | `MoveToEntranceAction.OnReachedInside()` | → `animController.SetAllSortingLayer("InsideStoreLayer")` |
| 出店 | `MoveToExitAction.OnReachedOutsideAnchor()` | → `animController.SetAllSortingLayer("OutsideStoreLayer")` |
| 电梯进入 | `LinkTraversalDetector` 协程步骤 4 | → 所有渲染器 `sortingLayerName = "ElevatorBackgroundLayer"` |
| 电梯出口 | `LinkTraversalDetector` 协程步骤 11 | → 恢复原始 `sortingLayerName` |

`SetAllSortingLayer()` 遍历 6 个部件渲染器 + `emojiController.SetSortingLayer()`，统一切换 `sortingLayerName`，不改变各部件的相对 `sortingOrder`。

### ElevatorBackgroundLayer
- 专用 sorting layer，仅电梯 background sprite 在此层（sortingOrder=0）
- 顾客进入电梯时切换到此 layer：sortingOrder ≥ 1 在 background 前面，但整个 layer 被更高层（shelf layer 等）遮挡
- 效果：门关闭后顾客被门遮挡（视觉上"消失"在电梯内）

---

## 电梯穿越系统

### 组件

| 组件 | 位置 | 职责 |
|------|------|------|
| `LinkTraversalDetector` | customer prefab (root) | 监听路径回调，检测电梯 NodeLink2，接管 AILerp 执行 Teleport 穿越协程 |
| `AILerpLinkTeleporter` | 电梯 NodeLink2 物体 | 门引用存储（doorA/doorB）、静态注册表、方向判断（ResolveDoors） |
| `ElevatorDoorInstance` | 电梯门 prefab | 门动画（PrimeTween 开合）+ 并发保护（activeTraversals 计数） |

### 穿越流程（LinkTraversalDetector 协程）

```
1.  暂停 AILerp (simulateMovement=false)
2.  入口门开门 + 标记 entryDoorPendingClose
3.  VisualAnchor 向上 Tween enterOffset（走进电梯，不动 transform）
4.  切换 sorting layer → ElevatorBackgroundLayer
5.  入口门关门 + 清除 entryDoorPendingClose
6.  隐藏 sprite (alpha=0)（此时被门遮挡，视觉无感知）
7.  AILerp.Teleport(exitAnchor, clearPath: true)
8.  等待 |楼层差| × waitPerFloor 秒（受 Time.timeScale 影响）
9.  恢复 alpha=1（出口门关着，被遮挡）
10. 出口门开门 + 标记 exitDoorPendingClose
11. 恢复原始 sorting layer
12. VisualAnchor 恢复到初始 localPosition（走出电梯）
13. isTraversing = false
14. 恢复 AILerp (simulateMovement=true, isStopped=false) + SearchPath
15. 延迟后出口门关门 + 清除 exitDoorPendingClose
```

### 关键设计
- **AILerp.Teleport(pos, clearPath: true)**：清除旧路径防止恢复移动后沿旧路径飞行
- **VisualAnchor Tween 不动 transform**：避免 AILerp 内部状态与 transform.position 分叉
- **visualAnchorOriginLocalPos 缓存**：Awake 时记录，AbortTraversal/中断时恢复，防止累积偏移
- **isTraversing 期间 OnPathComplete 直接 return**：防止 autoRepath 触发 AbortTraversal 导致闪现
- **entryDoorPendingClose / exitDoorPendingClose 独立标志**：OnDestroy 时按标志回收门计数，不依赖 isTraversing
- **MoveToTargetAction / MoveToExitAction / MoveToEntranceAction** 中检查 `IsTraversingElevator`：穿越中跳过到达判断，但不阻止 timeout

### 可配置参数（LinkTraversalDetector Inspector）

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `enterOffset` | 0.1 | VisualAnchor 进出电梯的上下偏移量（Unity 世界单位） |
| `waitPerFloor` | 0.5 | 每层楼的等待时间（秒，受 timeScale 影响） |
| `arrivalThreshold` | 0.3 | 到达入口门触发穿越的距离阈值 |
| `visualAnchor` | — | Inspector 拖入 customer prefab 的 VisualAnchor 子对象 |

### Portal 穿越（非电梯）
Portal NodeLink2（水平相邻 FloorTile 间的连接）没有 `AILerpLinkTeleporter` 组件，LinkTraversalDetector 跳过不处理。AILerp 自行沿路径直线走过 portal。

---

## 初始化流程

```
CustomerSpawner 实例化 customer prefab
  │
  ▼
CustomerAgent.Initialize(record, archetype, daySeed)
  │
  ├── CustomerPartLoader.LoadParts(customerID)     ← 加载 sprite sheet 的 6 个切片
  │     └── Resources.LoadAll<Sprite>("CustomerParts/c001_sheet")
  │     └── 按后缀数字排序 → 缓存
  │
  ├── animationController.SetupParts(parts)        ← 赋值给 6 个 SpriteRenderer
  │
  ├── animationController.SetCustomerID(id)        ← 保留接口，空实现
  │
  └── CustomerEventBus.RaiseSpawned(this)
```

---

## 与 NodeCanvas 行为树的集成

以下 Action 调用动画 API，**无需任何代码改动**：

| NodeCanvas Action | 调用的 API |
|-------------------|-----------|
| `PlayBasketAppearAction` | `PlayBasketAppear()`, `HideBasket()`(中断时) |
| `PlayPickProductAnimationAction` | `PlayPickProduct()`, `SetBasketFull()`(首次), `StopCurrentAnimation()` |
| `PlayCheckoutAnimationAction` | `PlayCheckout()`, `HideBasket()`(中断时), `StopCurrentAnimation()` |
| `PlayUpsetAnimationAction` | `PlayUpset()`, `StopCurrentAnimation()` |
| `PlayInteractionAnimationAction` | `PlayInteraction()`, `StopLoop()` |
| `ThinkBeforeNextShoppingAction` | `PlayThink()`, `StopLoop()` |

---

## 添加新顾客（零代码改动）

1. 制作 sprite sheet（6 部件，按索引约定切片）
2. 放入 `Assets/Resources/CustomerParts/{id}_sheet.png`
3. Unity 中设为 Sprite Mode: Multiple，切成 6 片
4. 确保 Sprite 的 Pivot 对齐正确
5. 完成 — `CustomerPartLoader` 自动按 ID 加载

---

## 篮子系统 (`CustomerAnimationController` 内置)

### 概述
顾客进店后自动拿出购物篮子，购买商品后篮子变满，结账时篮子动画演出后消失。篮子作为 LeftArm 的子对象 Hold，跟随手臂摆动。

### 系统总开关
`Awake()` 时检查 4 个引用：`basketRenderer`、`leftArmRenderer`、`basketEmptySprite`、`basketFullSprite`。任一缺失则 `basketSystemEnabled = false`，整套篮子逻辑静默禁用。无论是否启用，都会在 `Awake()` 中清掉 `basketRenderer` 的 prefab 残值。

### 3 个 Helper 方法（收口所有篮子显示变更）

| 方法 | 可见性 | 行为 |
|------|--------|------|
| `ShowBasketEmpty()` | private | sprite = empty, alpha = 1, basketVisible = true, basketIsFull = false |
| `SetBasketFull()` | public | sprite = full, basketIsFull = true |
| `HideBasket()` | public | sprite = null, alpha = 1, basketVisible = false, basketIsFull = false |

所有方法前置检查 `basketSystemEnabled`，禁用时直接 return。

### 篮子生命周期

```
进店 MoveToEntranceAction 完成
  ↓
PlayBasketAppearAction（停AI → PlayBasketAppear()）
  LeftArm Z旋转到-10° (0.2s) → ShowBasketEmpty() → LeftArm Z回0° (0.2s)
  ↓
Walk/Idle（篮子跟随 LeftArm ±10° 摆动，翻转时随 BodyParts 镜像）
  ↓
首次 PlayPickProductAnimationAction 成功 → SetBasketFull()（瞬时切换）
  ↓
PlayCheckoutAnimationAction → PlayCheckout()
  LeftArm上移0.035 (0.25s) → 停顿 → ShowBasketEmpty() →
  LeftArm回位 (0.25s) → HideBasket() + emoji → emoji播放 (0.67s) → 完成
```

### 中断安全

`StopCurrentAnimation()` / `ResetAllParts()` **不碰篮子**。中断清理由各 Action 的 `OnStop()` 负责：

| Action | OnStop 中断时 | OnStop 正常完成时 |
|--------|-------------|-----------------|
| `PlayBasketAppearAction` | `StopCurrentAnimation()` + `HideBasket()` | 不碰（篮子保留） |
| `PlayCheckoutAnimationAction` | `StopCurrentAnimation()` + `HideBasket()` | 不碰（Tween 已处理） |
| 其他 Action | 不碰篮子 | 不碰篮子 |

区分机制：`bool completedNormally` 标记，`EndAction(true)` 前置 true，`OnStop()` 中仅 `!completedNormally` 时清理。

### Sorting Layer 同步
`basketRenderer` 在 `basketSystemEnabled` 时加入 `allPartRenderers` 数组，`SetAllSortingLayer()` 自动同步。

### Inspector 配置

| 字段 | 类型 | 说明 |
|------|------|------|
| `basketRenderer` | SpriteRenderer | Hold 子对象的 SpriteRenderer |
| `basketEmptySprite` | Sprite | 空篮子精灵 (`shopping_basket.png`) |
| `basketFullSprite` | Sprite | 满篮子精灵 (`shopping_basket_2.png`) |

---

## 已知限制与待改进

- **动画幅度为绝对值**：距离参数（footMoveRange, bodyBobAmount 等）是 Unity 世界单位，不会随 sprite 大小自动缩放。不同尺寸的角色可能需要在 Inspector 中单独调节，或后续实现基于 sprite bounds 的自适应比例
- **翻转时手臂层级**：初始方案固定 sortingOrder（左右手臂均为 -1）。如果翻转后前后手臂层级不对，需在 `UpdateFacing()` 中动态交换 sortingOrder
- **PrimeTween SpriteRenderer Alpha**：PrimeTween 无直接 `Tween.Alpha(SpriteRenderer)` API，使用 `Tween.Custom` 手动驱动 `color.a`
- **AILerp 不原生支持 NodeLink2**：电梯穿越完全由 `LinkTraversalDetector` 协程手动管理（暂停 AILerp → Teleport → 恢复）。Portal 穿越因两端距离近由 AILerp 直线走过，视觉可接受
