# Customer 外观与动画系统大重制方案

## Context
当前顾客使用 Mecanim Animator + 整张 Sprite 的方案。美术团队已将角色 Sprite 拆分为6部件（Head/Body/LeftArm/RightArm/LeftFoot/RightFoot）的 sprite sheet。需要将动画系统从 Animator 驱动全面转向**代码驱动程序化动画 + PrimeTween**，包括行走、待机、emoji表情等所有动画状态。

---

## 1. 精灵加载策略

### 推荐：`Resources.LoadAll<Sprite>()` 自动加载

**资源存放路径**：`Assets/Resources/CustomerParts/{customerId}_sheet.png`
- 例如 `c006_sheet.png`（小写），在 Unity 中设为 Sprite Mode: Multiple，切成6片
- Unity 自动命名：`c006_sheet_0` ~ `c006_sheet_5`

**索引约定**（与现有 c006_sheet 一致）：
| 索引 | 部位 | 说明 |
|------|------|------|
| 0 | Head | 头部 |
| 1 | LeftFoot | 左脚 |
| 2 | RightFoot | 右脚 |
| 3 | Body | 躯干 |
| 4 | LeftArm | 左臂 |
| 5 | RightArm | 右臂 |

**文件名约定**：`c001_sheet.png`, `c002_sheet.png`, ... 小写字母 + 编号。

**优势**：添加新顾客只需放入 sprite sheet + 切片，零代码改动。

**回退方案**：如果某顾客没有 parts 资源，回退到 AppearanceDatabase 的整张 Sprite（向后兼容过渡期用）。

---

## 2. 新 Prefab 层级结构

```
customer (root)                           -- 保留所有逻辑组件
  ├── BodyParts (空容器)                   -- 翻转时整体 scale.x = -1
  │   ├── Body                            -- SpriteRenderer (sortingOrder: 0, 基准层)
  │   ├── Head                            -- SpriteRenderer (sortingOrder: 1, 在身体上方)
  │   ├── LeftArm                         -- SpriteRenderer (sortingOrder: -1, 在身体后方)
  │   ├── RightArm                        -- SpriteRenderer (sortingOrder: -1, 在身体后方)
  │   ├── LeftFoot                        -- SpriteRenderer (sortingOrder: -2, 最底层)
  │   └── RightFoot                       -- SpriteRenderer (sortingOrder: -2, 最底层)
  ├── emoji                               -- SpriteRenderer (sortingOrder: 10, 不在 BodyParts 下)
  └── name                                -- TMP (保持不变)
```

### Sorting Layer 配合说明
- 顾客进店时 `MoveToEntranceAction` 将所有 SpriteRenderer 的 sortingLayerName 切为 `"InsideStoreLayer"`
- 顾客出店时 `MoveToExitAction` 切为 `"OutsideStoreLayer"`
- `SetAllSortingLayer()` 遍历6个部件 + emoji，统一切换 sortingLayerName，**不改变各部件的相对 sortingOrder**
- 部件间的相对 sortingOrder 固定在 Prefab 上（-2, -1, 0, 1, 10），不受 layer 切换影响

### 关键设计
- **BodyParts 容器**负责翻转：`localScale.x = ±1`，一次翻转所有部位
- **emoji 不在 BodyParts 下**：emoji 不随身体翻转，始终朝玩家
- Root 上**移除** SpriteRenderer 和 Animator 组件
- Root 上**新增** CustomerEmojiController 组件

---

## 3. 新增文件

### 3.1 `CustomerPartLoader.cs`（新建）
**路径**：`Assets/Scripts/Customers/Runtime/CustomerPartLoader.cs`
**职责**：静态工具类，从 sprite sheet 自动加载6个部件

```
索引枚举：Head=0, LeftFoot=1, RightFoot=2, Body=3, LeftArm=4, RightArm=5

核心API：
- static Sprite[] LoadParts(string customerId)
  → 将 customerId（如 "C006"）转为小写路径 "CustomerParts/c006_sheet"
  → Resources.LoadAll<Sprite>(path)
  → 按名称后缀数字排序确保索引一致
  → 静态 Dictionary 缓存，避免重复加载
- static Sprite GetPart(string customerId, PartIndex part)
- static void ClearCache()
```

### 3.2 `CustomerEmojiController.cs`（新建）
**路径**：`Assets/Scripts/Customers/Runtime/CustomerEmojiController.cs`
**职责**：独立管理 emoji 显示，完全脱离 Animator

**Inspector 字段**：
- `[SerializeField] SpriteRenderer emojiRenderer` - emoji 渲染器引用
- `[SerializeField] Sprite[] thinkSprites` - 思考表情帧（3帧）
- `[SerializeField] Sprite[] upsetSprites` - 不满表情帧（4帧）
- `[SerializeField] Sprite[] interactionSprites` - 交互表情帧（2帧）
- `[SerializeField] Sprite checkoutSprite` - 结账表情
- `[SerializeField] Sprite pickProductSprite` - 拿货表情

**公共 API**：
| 方法 | 行为 |
|------|------|
| `PlayThink()` | 循环切换 thinkSprites（Update 计时器驱动） |
| `PlayUpset()` | 循环切换 upsetSprites |
| `PlayInteraction()` | 循环切换 interactionSprites |
| `PlayPickProduct()` | 显示 pickProductSprite + PrimeTween 上升 + 淡出 |
| `PlayCheckout()` | 显示 checkoutSprite + PrimeTween 上升 + 淡入 |
| `StopEmoji()` | 停止所有 Tween/循环，sprite 设为 null，位置重置 |
| `SetSortingLayer(string)` | 设置 emoji 的 sortingLayerName |

**PrimeTween 用法示例**：
```csharp
// PlayCheckout: emoji 上升 + 淡入
emojiRenderer.color = new Color(1, 1, 1, 0);
emojiRenderer.sprite = checkoutSprite;
currentSequence = Sequence.Create()
    .Group(Tween.LocalPositionY(emojiTransform, originY + riseDistance, 0.5f, Ease.OutQuad))
    .Group(Tween.Color(emojiRenderer, Color.white, 0.3f, Ease.Linear));

// PlayPickProduct: emoji 上升 + 淡出
emojiRenderer.sprite = pickProductSprite;
emojiRenderer.color = Color.white;
currentSequence = Sequence.Create()
    .Chain(Tween.LocalPositionY(emojiTransform, originY + riseDistance, 0.5f, Ease.OutQuad))
    .Chain(Tween.Color(emojiRenderer, new Color(1,1,1,0), 0.3f, Ease.InQuad));
```

---

## 4. 重写文件

### 4.1 `CustomerAnimationController.cs`（完全重写）
**路径**：`Assets/Scripts/Customers/Runtime/CustomerAnimationController.cs`

**移除**：
- `[RequireComponent(typeof(Animator))]`
- 所有 Animator 引用、Hash 常量
- Mecanim PlayState/PlayOneShot/PlayLoop 逻辑

**新增**：
- `AnimState` 枚举：`Idle, Walk, Think, PickProduct, Checkout, Upset, Interaction`
- 6个部位 `[SerializeField] SpriteRenderer` 引用
- `[SerializeField] Transform bodyPartsContainer` - BodyParts 容器
- `[SerializeField] CustomerEmojiController emojiController` 引用
- 行走/待机参数（footMoveRange, footMoveSpeed, bodyBobAmount, armSwingAngle, breatheAmount 等）

**保持不变的公共 API**（NodeCanvas actions 零改动）：
```csharp
public void PlayPickProduct();
public void PlayCheckout();
public void PlayUpset();
public void PlayThink();
public void PlayInteraction();
public void StopLoop();
public void StopCurrentAnimation();
public void SetCustomerID(string id);  // 保留空实现
```

**新增 API**：
```csharp
public void SetupParts(Sprite[] parts);       // 赋值6个部件精灵
public void SetAllSortingLayer(string layer);  // 设置所有渲染器 sorting layer
```

**动画实现**：

**Walk（Update 中程序化）**：
```
sin = Sin(Time.time * footMoveSpeed)
右脚Y = origin + Max(0, sin) * footMoveRange     // 正半波
左脚Y = origin + Max(0, -sin) * footMoveRange    // 负半波（180°相位差）
身体Y = origin + Abs(sin) * bodyBobAmount         // 身体微晃
头部Y = origin + Abs(sin) * bodyBobAmount         // 跟随身体
左臂旋转Z = sin * armSwingAngle                    // 手臂摆动
右臂旋转Z = -sin * armSwingAngle                   // 反向
```

**Idle（Update 中程序化）**：
```
breathe = Sin(Time.time * breatheSpeed) * breatheAmount
身体Y = origin + breathe
头部Y = origin + breathe
脚/手臂 Lerp 回原位
```

**PickProduct（PrimeTween）**：
```
1. isPlayingOneShot = true
2. 身体+头部下蹲: Tween.LocalPositionY(body, origin - 0.05f, 0.4s) → 回原位(0.3s)
3. emojiController.PlayPickProduct()
4. 延迟2s后: isPlayingOneShot = false, emojiController.StopEmoji()
```

**Checkout（PrimeTween）**：
```
1. isPlayingOneShot = true
2. emojiController.PlayCheckout()
3. 延迟0.67s后: isPlayingOneShot = false, emojiController.StopEmoji()
```

**Upset（PrimeTween）**：
```
1. isPlayingOneShot = true
2. emojiController.PlayUpset()（表情循环）
3. 可选: Tween bodyPartsContainer 轻微水平抖动
4. 延迟0.85s后: isPlayingOneShot = false, emojiController.StopEmoji()
```

**Think / Interaction**：
```
isPlayingLoop = true
emojiController.PlayThink() 或 PlayInteraction()
StopLoop() → isPlayingLoop = false, emojiController.StopEmoji()
```

**方向翻转（Update 中）**：
```
velocity = ai.velocity
if |velocity.x| > 0.01:
    bodyPartsContainer.localScale.x = velocity.x > 0 ? 1 : -1
```

### 4.2 `CustomerAgent.cs`（修改）
**路径**：`Assets/Scripts/Customers/Runtime/CustomerAgent.cs`

| 改动项 | 具体操作 |
|--------|----------|
| 移除 `[RequireComponent(typeof(SpriteRenderer))]` | 根节点不再有 SpriteRenderer |
| 移除 `[RequireComponent(typeof(Animator))]` | 根节点不再有 Animator |
| 移除字段 `spriteRenderer`, `animator`, `baseOverrideController` | 不再需要 |
| 移除 `SetupAnimatorOverride()` 方法 | 不再需要 AnimatorOverrideController |
| 移除 Awake 中 `animator.enabled = false` | 不再需要 |
| 移除 Initialize 步骤8（SetupAnimatorOverride）| 不再需要 |
| 移除 Initialize 步骤10（animator.enabled = true）| 不再需要 |
| **改写步骤1：外貌设置** | 改为 `CustomerPartLoader.LoadParts()` + `animController.SetupParts()` |

**新的外貌加载逻辑**：
```csharp
// 步骤1：使用部件加载器设置外貌
// CustomerPartLoader 内部将 customerID "C006" 转为路径 "CustomerParts/c006_sheet"
Sprite[] parts = CustomerPartLoader.LoadParts(customerID);
if (parts != null && parts.Length >= 6)
{
    animationController.SetupParts(parts);
    // parts[0]=Head, [1]=LeftFoot, [2]=RightFoot, [3]=Body, [4]=LeftArm, [5]=RightArm
}
```

### 4.3 `MoveToEntranceAction.cs`（小改）
**改动**：将 `GetComponent<SpriteRenderer>()` + `transform.Find("emoji")` 替换为 `GetComponent<CustomerAnimationController>().SetAllSortingLayer("InsideStoreLayer")`

### 4.4 `MoveToExitAction.cs`（小改）
**改动**：同上，layer 改为 `"OutsideStoreLayer"`

---

## 5. 不需要改动的文件

以下 NodeCanvas action 文件调用的 API 完全不变，**零改动**：
- `PlayPickProductAnimationAction.cs` → 调用 `PlayPickProduct()`, `StopCurrentAnimation()`
- `PlayCheckoutAnimationAction.cs` → 调用 `PlayCheckout()`
- `PlayUpsetAnimationAction.cs` → 调用 `PlayUpset()`, `StopCurrentAnimation()`
- `PlayInteractionAnimationAction.cs` → 调用 `PlayInteraction()`, `StopLoop()`
- `ThinkBeforeNextShoppingAction.cs` → 调用 `PlayThink()`, `StopLoop()`

---

## 6. 实施步骤

### Phase 1：基础设施（不破坏现有功能）
1. 创建 `CustomerPartLoader.cs`
2. 创建 `CustomerEmojiController.cs`
3. 创建文件夹 `Assets/Resources/CustomerParts/`
4. 放入至少一个测试 sprite sheet（复用 c006_sheet.png 重命名测试）
5. 从现有 .anim 文件中提取 emoji sprites，放入可引用的位置

### Phase 2：Prefab 重构
6. 备份 `customer.prefab`
7. 修改 prefab 层级：添加 BodyParts 容器 + 6个子物体
8. 移除根节点的 SpriteRenderer 和 Animator
9. 添加 CustomerEmojiController 组件，连线 Inspector 引用

### Phase 3：核心脚本重写
10. 重写 `CustomerAnimationController.cs`
11. 修改 `CustomerAgent.cs`（移除 Animator 相关代码，改用部件加载）
12. 在 prefab 上连线所有 `[SerializeField]` 引用

### Phase 4：辅助脚本适配
13. 修改 `MoveToEntranceAction.cs`（sorting layer 逻辑，约5行）
14. 修改 `MoveToExitAction.cs`（sorting layer 逻辑，约5行）

### Phase 5：测试验证
15. 在 Unity 中运行，验证所有7种动画状态
16. 验证方向翻转
17. 验证 sorting layer 切换（进店/出店）
18. 验证多顾客同时在场
19. 验证没有 parts 资源的顾客（回退行为）

### Phase 6：清理
20. 删除 `WalkAnimation/` 文件夹（原型已吸收）
21. 旧动画资源（.anim / .controller / .overrideController）可后续清理

---

## 7. 注意事项

- **Sprite Pivot 对齐**：6个部件的 pivot 必须设置正确才能在 prefab 中正确拼装。这是美术侧的关键工作
- **PrimeTween SpriteRenderer Alpha**：PrimeTween 没有直接的 `Tween.Alpha(SpriteRenderer)`，需用 `Tween.Color(renderer, targetColor, duration)` 改变 alpha 通道
- **并发 Tween 清理**：新动画触发前必须 `.Stop()` 旧的 Sequence/Tween，防止动画冲突
- **Sorting Order 层级**：初始方案用固定 sorting order，如果翻转时前后手臂层级不对，后续再加动态切换逻辑

---

## 8. 当前系统 vs 新系统对比

| 维度 | 当前方案 (Mecanim) | 新方案 (程序化) |
|------|-------------------|----------------|
| 角色结构 | 单 SpriteRenderer + Animator | 6部件子物体 + BodyParts容器 |
| 行走动画 | .anim clip 逐帧 / AnimatorOverrideController | Sin 波脚步 + 身体 bob + 手臂摆动 |
| 待机动画 | 空闲 Mecanim 状态 | 呼吸起伏（Sin 波） |
| 特殊动画 | .anim clip（sprite 帧切换 + 位置曲线） | PrimeTween（位置/颜色/缩放） |
| Emoji | Animator PPtrCurve 驱动 sprite 切换 | CustomerEmojiController 代码驱动 |
| 翻转 | spriteRenderer.flipX | bodyPartsContainer.localScale.x = -1 |
| Sorting | 根 SpriteRenderer + emoji 手动 Find | SetAllSortingLayer() 批量设置 |
| 添加新顾客 | 需制作 walk .anim + 注册 AnimatorOverride | 放入 sprite sheet 即可 |
| 性能 | Animator.Update 开销 + GC | 纯数学计算，零 GC |
