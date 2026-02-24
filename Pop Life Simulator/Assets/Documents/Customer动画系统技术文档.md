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
customer (root)
  ├── BodyParts (空容器)                 ← 翻转时整体 scale.x = ±1
  │   ├── Body                          ← SpriteRenderer (sortingOrder: 0)
  │   ├── Head                          ← SpriteRenderer (sortingOrder: 1)
  │   ├── LeftArm                       ← SpriteRenderer (sortingOrder: -1)
  │   ├── RightArm                      ← SpriteRenderer (sortingOrder: -1)
  │   ├── LeftFoot                      ← SpriteRenderer (sortingOrder: -2)
  │   └── RightFoot                     ← SpriteRenderer (sortingOrder: -2)
  ├── emoji                             ← SpriteRenderer (sortingOrder: 10)
  │   └── name                          ← TextMeshPro
  └── [逻辑组件挂在 root 上]
```

### 关键设计
- **BodyParts 容器**负责方向翻转：`localScale.x = ±1`，一次翻转所有部位
- **emoji 不在 BodyParts 下**：不随身体翻转，始终朝向玩家
- **部件间 sortingOrder 固定**在 Prefab 上，不受 sorting layer 切换影响

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
   └── ChainCallback: 容器归零, isPlayingOneShot = false, StopEmoji()
4. emojiController.PlayUpset()                               ← emoji 不满帧循环
```

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

`SetAllSortingLayer()` 遍历 6 个部件渲染器 + `emojiController.SetSortingLayer()`，统一切换 `sortingLayerName`，不改变各部件的相对 `sortingOrder`。

---

## NodeLink2 穿越隐身

| 组件 | 位置 | 职责 |
|------|------|------|
| `LinkTraversalDetector` | customer prefab (root) | `GetComponentsInChildren<SpriteRenderer>(true)` 收集所有渲染器，传递给 teleporter |
| `AILerpLinkTeleporter` | NodeLink2 物体 | 跟踪 agent 位置，到达起点时 alpha→0 隐藏，到达终点时 alpha→1 恢复 |

穿越时所有 6 个部件 + emoji 同时隐藏/显示。

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
| `PlayPickProductAnimationAction` | `PlayPickProduct()`, `StopCurrentAnimation()` |
| `PlayCheckoutAnimationAction` | `PlayCheckout()` |
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

## 已知限制与待改进

- **动画幅度为绝对值**：距离参数（footMoveRange, bodyBobAmount 等）是 Unity 世界单位，不会随 sprite 大小自动缩放。不同尺寸的角色可能需要在 Inspector 中单独调节，或后续实现基于 sprite bounds 的自适应比例
- **翻转时手臂层级**：初始方案固定 sortingOrder（左右手臂均为 -1）。如果翻转后前后手臂层级不对，需在 `UpdateFacing()` 中动态交换 sortingOrder
- **PrimeTween SpriteRenderer Alpha**：PrimeTween 无直接 `Tween.Alpha(SpriteRenderer)` API，使用 `Tween.Custom` 手动驱动 `color.a`
