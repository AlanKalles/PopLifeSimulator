# DialogueBridge 系统设计文档

## 概述

DialogueBridge 是 Pop Life Simulator 与 Pixel Crushers Dialogue System 的整合层，提供：
- 教程触发机制（TutorialMarker → Quest 同步）
- 游戏状态同步到 Lua 变量
- Spotlight/Coach Mark 聚焦效果
- 顾客对话触发系统
- 自定义 Sequencer 命令

## 目录结构

```
Assets/Scripts/DialogueBridge/
├── PopLifeLuaFunctions.cs       # 注册自定义 Lua 函数
├── TutorialMarkerBridge.cs      # TutorialMarker ↔ Quest 双向同步
├── GameStateLuaSync.cs          # 游戏状态同步到 Lua 变量
├── CustomerDialogueTrigger.cs   # 顾客对话触发组件
├── CustomerClickHandler.cs      # 玩家点击顾客检测
├── DialogueTriggerHelper.cs     # 静态工具类
├── UI/
│   ├── SpotlightManager.cs              # Spotlight + Tooltip 统一控制器（零 DS 依赖）
│   ├── SpotlightPanel.cs                # Spotlight 遮罩面板（mask + raycast filter + blocker）
│   ├── SpotlightTooltip.cs              # Spotlight 提示框（参数注入，独立定位算法）
│   ├── SpotlightRequest.cs              # spotlight 显示请求结构 + NullTextSentinel
│   ├── SpotlightCutoutRaycastFilter.cs  # shader 模式下 cutout 区域 raycast 穿透
│   ├── SpotlightBlockerInputHandler.cs  # Blocking 模式全屏点击监听
│   ├── DialogueUIVisibility.cs          # 对话 UI 视觉隐藏/恢复（CanvasGroup 三件套）
│   ├── TooltipPosition.cs               # 提示框位置枚举
│   └── UIFreezeManager.cs               # 游戏 UI 面板冻结管理器
├── Targeting/
│   ├── SpotlightTarget.cs            # 注册组件（spotlightId/priority/ensureRaycastTarget）
│   ├── SpotlightTargetRegistry.cs    # 多实例安全的字符串 ID 注册表
│   ├── SpotlightTargetSpec.cs        # 不可变规格 + ResolvedTarget
│   ├── SpotlightTargetClickProxy.cs  # Passthrough 非 Button 目标的临时点击监听
│   └── SpotlightTypes.cs             # InteractionMode + CloseReason 枚举
└── Sequencer/
    ├── SpotlightSequencerHelpers.cs                       # 共享参数解析 + TryShow
    ├── SequencerCommandShowSpotlight.cs                   # ShowSpotlight(...) fire-and-forget
    ├── SequencerCommandShowSpotlightAndWait.cs            # ShowSpotlightAndWait(...) 等关闭再 Stop
    ├── SequencerCommandSpotlightOff.cs                    # SpotlightOff() 关闭 spotlight
    ├── SequencerCommandHideDialogueUI.cs                  # HideDialogueUI()
    ├── SequencerCommandShowDialogueUI.cs                  # ShowDialogueUI()
    ├── SequencerCommandShowSpotlightAndContinue.cs        # 场景 1 合成（隐藏UI+spotlight+恢复UI+OnContinue）
    ├── SequencerCommandShowSpotlightThenConversation.cs   # 场景 2 合成（spotlight+QueueConversation）
    ├── SequencerCommandSpotlight.cs           # [Obsolete] 旧 Spotlight(uiName)
    ├── SequencerCommandSpotlightWorld.cs      # [Obsolete] 旧 SpotlightWorld(objectName)
    ├── SequencerCommandSpotlightRect.cs       # [Obsolete] 旧 SpotlightRect(x,y,w,h)
    ├── SequencerCommandSpotlightNormalized.cs # [Obsolete] 旧 SpotlightNormalized(x,y,w,h)
    ├── SequencerCommandSpotlightTooltip.cs    # [Obsolete] 旧 SpotlightTooltip(position)
    ├── SequencerCommandSpotlightTooltipOff.cs # [Obsolete] 旧 SpotlightTooltipOff()
    ├── SequencerCommandFreezeUI.cs            # FreezeUI(panelName) 冻结游戏 UI 面板
    ├── SequencerCommandUnfreezeUI.cs          # UnfreezeUI(panelName) 解冻游戏 UI 面板
    ├── SequencerCommandGiveReward.cs          # GiveReward(type, value)
    └── SequencerCommandRaiseMarker.cs         # RaiseMarker(markerName)
```

---

## 1. PopLifeLuaFunctions - Lua 函数注册

### 功能
注册自定义 Lua 函数，允许在 Dialogue Editor 的 Script 字段中直接调用游戏系统。

### 在 Dialogue Editor 中使用

**奖励函数：**
```lua
GiveMoney(100)                         -- 给予金钱
GiveFame(50)                           -- 给予声望
UnlockBlueprint("ShelfVibrator")       -- 解锁蓝图
UnlockCustomer("Customer_001")         -- 解锁顾客
GiveReward("Money", "100")             -- 通用奖励函数
```

**教程标记函数：**
```lua
RaiseTutorialMarker("FirstShelfPlaced")  -- 触发教程标记
if IsMarkerTriggered("StoreOpened") then -- 检查标记是否已触发
    ...
end
```

**查询函数：**
```lua
Variable["Money"] = GetMoney()           -- 获取当前金钱
Variable["Fame"] = GetFame()             -- 获取当前声望
Variable["CurrentDay"] = GetCurrentDay() -- 获取当前天数
if IsStoreOpen() then ...                -- 检查商店是否营业
Variable["Phase"] = GetCurrentPhase()    -- 获取当前阶段
Variable["Hour"] = GetCurrentHour()      -- 获取当前小时
```

**游戏控制函数：**
```lua
PauseGame()   -- 暂停游戏
ResumeGame()  -- 恢复游戏
```

**Spotlight & Dialogue UI 函数（详见第 4 节）：**
```lua
ShowSpotlight("BuildButton", "Click here", "Right", "RoundedRectangle", "passthrough")
HideSpotlight()
HideDialogueUI()  -- CanvasGroup 视觉隐藏对话 UI（保存原状态）
ShowDialogueUI()  -- 恢复对话 UI 到 Hide 之前的状态
```

### 设置
1. 创建空 GameObject，命名为 `DialogueBridgeManager`
2. 添加 `PopLifeLuaFunctions` 组件
3. 组件会自动在 OnEnable 时注册所有函数

---

## 2. TutorialMarkerBridge - 教程标记桥接

### 功能
当 TutorialMarker 触发时，自动执行：
- 更新对应 Quest 的状态
- 启动指定对话
- 执行自定义 Lua 脚本

### Inspector 配置

```
Mappings:
├── Marker: FirstShelfPlaced
│   ├── Quest Name: TUT_BuildBasics
│   ├── Quest Entry Number: 2
│   ├── Target State: success
│   ├── Conversation To Start: Tutorial/PlaceShelf
│   └── Lua Script: (optional)
└── ...
```

### 设置
1. 在 `DialogueBridgeManager` 上添加 `TutorialMarkerBridge` 组件
2. 在 Inspector 中配置 Marker → Quest 映射
3. 组件会自动订阅 `TutorialEventBus.OnMarkerTriggered` 事件

---

## 3. GameStateLuaSync - 游戏状态同步

### 功能
将游戏状态同步到 Dialogue System 的 Lua 变量，允许在对话条件中使用。

### 同步的变量

| 变量名 | 类型 | 描述 |
|--------|------|------|
| `Money` | int | 当前金钱 |
| `Fame` | int | 当前声望 |
| `TotalIncome` | int | 总收入 |
| `TotalExpenses` | int | 总开支 |
| `CurrentDay` | int | 当前天数 |
| `CurrentHour` | float | 当前小时（0-24） |
| `IsStoreOpen` | bool | 商店是否营业 |
| `GamePhase` | string | "BuildPhase" 或 "OpenPhase" |
| `TotalShelvesPlaced` | int | 已放置货架数 |
| `TotalCustomersServed` | int | 已服务顾客数 |
| `HasPlacedFirstShelf` | bool | 是否已放置第一个货架 |
| `HasOpenedStore` | bool | 是否已开店 |
| `HasServedFirstCustomer` | bool | 是否已服务第一个顾客 |
| `HasEarnedFirstFame` | bool | 是否已获得第一份声望 |

### 在 Dialogue Editor 中使用条件

```lua
-- 在 Conditions 字段中使用
Variable["CurrentDay"] >= 2
Variable["Money"] >= 1000
Variable["IsStoreOpen"] == true
Variable["GamePhase"] == "BuildPhase"
```

### 设置
1. 在 `DialogueBridgeManager` 上添加 `GameStateLuaSync` 组件
2. 配置同步时机（对话开始时、商店状态变化时、天数变化时）

---

## 4. Spotlight 系统（重制版）

### 功能
高亮 UI 元素或场景物体，配套显示 tooltip 文本。**Spotlight 与 Tooltip 一体化**——一次调用必同时存在。

### 核心特性
- **零 Dialogue System 耦合**：核心代码独立，对话仅通过 Lua/Sequencer 桥接调用
- **多种目标类型**：注册表 ID、RectTransform 直传、World GameObject、屏幕像素 Rect、normalized Rect
- **两种交互模式**：Passthrough（必须点目标） / Blocking（点屏幕任意位置关闭）
- **分辨率自适应**：rect diff 检测，屏幕变化自动跟随
- **目标失效自动 Hide**：注册表移除或 GameObject 销毁触发 `CloseReason.TargetLost`

### 支持的形状
- `Rectangle` - 矩形
- `Circle` - 圆形
- `RoundedRectangle` - 圆角矩形（默认）

### 交互模式

| 模式 | 行为 | 适用 |
|------|------|------|
| `Passthrough`（默认）| 高亮区域可点击穿透到目标按钮；外部 mask 拦截但**不**关闭 spotlight | 强制教学（"必须点这个按钮"）|
| `Blocking` | 屏幕任意位置点击都关闭；下方 UI 完全无法交互 | 概念展示（"看一眼就行"）|

### 目标识别

**注册表路径**（推荐）：在需要被字符串 ID 调用的 prefab/GameObject 挂 `SpotlightTarget` 组件，填 `spotlightId`。运行时 `OnEnable` 自动注册，多实例同 ID 按 `priority` 选择。

**直传路径**（C# 代码）：直接传 RectTransform / GameObject / Rect，绕过注册表。

**智能字符串解析**（Sequencer/Lua）：
- `"BuildButton"` 或 `"id:BuildButton"` → 注册表查找
- `"rect:100,200,300,150"` → 屏幕像素坐标
- `"norm:0.4,0.4,0.2,0.2"` → normalized 坐标 (0..1)

### Lua 函数（4 个）

| 函数 | 含义 |
|------|------|
| `ShowSpotlight(targetId, text, position, shape, mode)` | 异步显示 spotlight，不等待关闭 |
| `HideSpotlight()` | 关闭 spotlight |
| `HideDialogueUI()` | 视觉隐藏 Dialogue UI（CanvasGroup alpha=0 + raycast off）|
| `ShowDialogueUI()` | 恢复 Dialogue UI 到 Hide 之前的原状态 |

**用法示例**：
```lua
ShowSpotlight("BuildButton", "Click here to build", "Right", "RoundedRectangle", "passthrough")
ShowSpotlight("rect:760,440,400,200", "Center area", "Auto", "Circle", "blocking")
ShowSpotlight("BuildButton", "NULL", "Right", "RoundedRectangle", "passthrough")  -- 仅高亮无 tooltip
HideSpotlight()
```

text 取值：
- 普通字符串 → 显示 tooltip
- `"NULL"` → 仅 spotlight 不显示 tooltip
- 空字符串 `""` → 报 warning 不显示

### Sequencer 命令（7 个）

#### 基础 spotlight（3 个）

```
ShowSpotlight(target, text, position, shape, mode)
```
显示后**立即结束**（fire-and-forget），对话照常推进。Spotlight 异步存在直到玩家关闭或调 `SpotlightOff()`。

```
required ShowSpotlightAndWait(target, text, position, shape, mode); Continue()
```
显示后**等 spotlight 关闭才结束**。配合 `Continue()` 让对话节点关闭瞬间自动推进。节点保留 dialogue UI 可见。

```
SpotlightOff()
```
关闭 spotlight。等同 Lua `HideSpotlight()`。

#### Dialogue UI 控制（2 个）

```
HideDialogueUI()
ShowDialogueUI()
```
独立细粒度命令。**注意 Sequencer 命令是并行执行**，不能简单写
`HideDialogueUI(); ShowSpotlightAndWait(...); ShowDialogueUI()` —— 三个会同时启动，
ShowDialogueUI 不会等 spotlight 关。如需此时序请用合成命令 `ShowSpotlightAndContinue`。

#### 合成教程命令（2 个，**自包含完整时序**）

```
ShowSpotlightAndContinue(target, text, position, shape, mode)
```
**场景 1**：隐藏 Dialogue UI → 显示 spotlight → 等关闭 → 推进对话 → 恢复 Dialogue UI。

**关键时序**（避免视觉闪烁）：spotlight 关闭后**先**调 OnContinue 推进，**等一帧**让 dialogue system 切到下一节点并渲染 subtitle，**再**恢复 dialogue UI alpha。否则 alpha 恢复时 dialogue UI 仍显示旧节点状态，玩家会看到一帧旧内容闪现。

⚠️ **设计师必读**：
- **不要追加 `Continue()`** — 命令内部已自包含 OnContinue 调用
- **节点必须无 auto-continue** — 否则节点 subtitle 渲染完会自动推进，spotlight 等关闭逻辑失效
- **节点应是空节点**（DialogueText 留空）—— 命令进入即 Hide UI，节点对话文本会被吞。推荐用法：`node2 → [空节点放此命令] → node3`

```
ShowSpotlightThenConversation(target, text, position, shape, mode, nextConversation)
```
**场景 2**：spotlight 异步显示 → 玩家正常 Continue 推进当前对话至结束 → spotlight 关闭后 `QueueConversation(nextConv)` 启动新对话。

边界保障：玩家先关 spotlight 再点 Continue → `QueueConversation` 协程等当前对话结束后才启动新对话（不会抢启动）。

### 推荐用法速查

| 教程场景 | 推荐 Sequence 写法 |
|---------|-------------------|
| 节点中显示 spotlight、对话照常推进 | `ShowSpotlight(target, text, position)` |
| 节点中等 spotlight 关再推进（保留对话 UI 可见） | `required ShowSpotlightAndWait(target, text, position); Continue()` |
| 节点中隐藏对话 UI + spotlight + 关后恢复 + 推进 | `ShowSpotlightAndContinue(target, text, position)` *（不要外接 Continue()）* |
| 最后 node：spotlight 关后启动新对话 | `ShowSpotlightThenConversation(target, text, position, shape, mode, NextConversation)` |
| 关闭 spotlight | `SpotlightOff()` |
| 独立隐藏/恢复对话 UI（高级用法） | `HideDialogueUI()` / `ShowDialogueUI()` |

### 常用位置（normalized 坐标参考）

| 位置 | 参数 |
|------|------|
| 屏幕中心 | `norm:0.4,0.4,0.2,0.2` |
| 左上角 | `norm:0,0.85,0.2,0.1` |
| 右上角 | `norm:0.8,0.85,0.2,0.1` |
| 左下角 | `norm:0,0.05,0.2,0.1` |
| 右下角 | `norm:0.8,0.05,0.2,0.1` |

### 在代码中使用（C#）

```csharp
using PopLife.DialogueBridge.UI;

// 直传 RectTransform
SpotlightManager.Instance.Show(myRectTransform, "Click here", TooltipPosition.Right);

// 注册表 ID
SpotlightManager.Instance.Show("BuildButton", "Click to build", TooltipPosition.Auto);

// SpotlightRequest 完整控制
SpotlightManager.Instance.Show(new SpotlightRequest {
    target = SpotlightTargetSpec.ById("BuildButton"),
    text = "Click to build",
    position = TooltipPosition.Right,
    shape = SpotlightShape.RoundedRectangle,
    mode = InteractionMode.Passthrough,
    onTargetClicked = () => Debug.Log("Player clicked!"),
    onClosed = reason => Debug.Log($"Closed: {reason}"),
});

// 关闭
SpotlightManager.Instance.Hide();
```

### 设置（场景搭建）

1. **SpotlightManager 物体**：在场景里有 SpotlightManager 单例，Inspector 配置：
   - SpotlightPanel 引用
   - SpotlightTooltip 引用
   - Spotlight Canvas（Screen Space Overlay，sortingOrder ≥ 1000）
   - 动画参数（fadeIn/Out duration、pulse scale 等）
2. **SpotlightTarget 组件**：在需要被 ID 调用的 UI prefab 上挂载
   - 填 `spotlightId`
   - 多实例同 ID 时设 `priority`
   - 非 Button 目标可勾选 `ensureRaycastTarget`

---

## 4.1 兼容期 [Obsolete] 命令（迁移期保留）

### 旧 → 新对话节点 Sequence 迁移

| 旧写法 | 新写法 |
|--------|--------|
| `Spotlight(BuildButton)` | `ShowSpotlight(BuildButton, "你的提示文字", Right)` |
| `Spotlight(BuildButton); SpotlightTooltip(Right, ClickButton)` | `required ShowSpotlightAndWait(BuildButton, "提示", Right); Continue()` |
| `SpotlightWorld(Cashier)` | `ShowSpotlight(Cashier, "提示", Right)`（先在 Cashier 物体挂 SpotlightTarget）|
| `SpotlightRect(100, 200, 300, 150)` | `ShowSpotlight("rect:100,200,300,150", "提示", Auto)` |
| `SpotlightNormalized(0.4, 0.4, 0.2, 0.2)` | `ShowSpotlight("norm:0.4,0.4,0.2,0.2", "提示", Auto)` |
| `SpotlightTooltipOff()` | `SpotlightOff()` |
| `SpotlightOff()` | 不变 |

### 兼容期行为
旧命令仍可执行，但会输出 `[DEPRECATED]` warning。占位 tooltip 文本为 `"NULL"`（不显示 tooltip 仅高亮），提示设计师补全实际文本。资产全部迁移完成后将删除 obsolete wrapper。

---

## 4.2 UIFreezeManager - UI 面板冻结

### 功能
在对话/教程期间临时冻结 UI 面板的可交互性，防止玩家在教程引导时误操作。节点切换或对话结束时自动恢复。

### 机制
使用 `CanvasGroup` 实现冻结：
- `interactable = false` → 所有子 Button/Toggle 变灰不可点击
- `blocksRaycasts = false` → 整个面板穿透点击

恢复时精确还原原始值。如果 CanvasGroup 是由 UIFreezeManager 添加的，恢复后自动移除，不干扰其他系统。

### 自动恢复时机

| 事件 | 行为 |
|------|------|
| 对话节点切换 | 自动解冻所有面板（可在 Inspector 关闭 `autoUnfreezeOnNodeChange`） |
| 对话结束 | 自动解冻所有面板 |
| 手动调用 `UnfreezeUI()` | 立即解冻 |

### 在 Dialogue Editor 中使用 Sequencer 命令

```
// 冻结指定面板
FreezeUI(ShelfListPanelRoot)

// 配合 Spotlight + Tooltip 使用
Spotlight(ShelfListPanelRoot); SpotlightTooltip(Auto); FreezeUI(ShelfListPanelRoot)

// 手动解冻（通常不需要，节点切换时自动恢复）
UnfreezeUI(ShelfListPanelRoot)   // 解冻指定面板
UnfreezeUI()                     // 解冻全部
```

### 在代码中使用

```csharp
// 按名称冻结（内部使用 GameObject.Find）
UIFreezeManager.Instance.Freeze("ShelfListPanelRoot");

// 按引用冻结（适用于多个同名 clone 需要精确指定的情况）
UIFreezeManager.Instance.Freeze("myKey", someGameObject);

// 解冻
UIFreezeManager.Instance.Unfreeze("ShelfListPanelRoot");
UIFreezeManager.Instance.UnfreezeAll();

// 检查状态
bool hasFrozen = UIFreezeManager.Instance.HasFrozenPanels;
```

### 设置
1. 在 `DialogueBridgeManager` 上添加 `UIFreezeManager` 组件
2. 配置 `Auto Unfreeze On Node Change`（默认开启）

---

## 5. 顾客对话系统

### 功能
玩家点击顾客时触发对话。

### 对话选择优先级
1. 顾客专属对话：`Customer/{customerId}`
2. 特质对话：`Customer/Trait_{traitId}`
3. 忠诚度对话：`Customer/Loyalty_{loyaltyLevel}`
4. 默认对话：`Customer/Generic`

### 同步的顾客数据

| 变量名 | 描述 |
|--------|------|
| `CurrentCustomer_ID` | 顾客 ID |
| `CurrentCustomer_Name` | 顾客名称 |
| `CurrentCustomer_LoyaltyLevel` | 忠诚度等级 |
| `CurrentCustomer_VisitCount` | 访问次数 |
| `CurrentCustomer_LifetimeSpent` | 累计消费 |
| `CurrentCustomer_Traits` | 特质 ID 列表（逗号分隔） |

### 设置

**顾客预制体：**
1. 添加 `Collider2D` 组件（建议 BoxCollider2D）
2. 设置 Collider 为 Trigger
3. 添加 `CustomerDialogueTrigger` 组件
4. （可选）设置 Layer 为 "Customer"

**场景管理器：**
1. 在 `DialogueBridgeManager` 上添加 `CustomerClickHandler` 组件
2. 配置 Layer Mask 为 "Customer"

---

## 6. DialogueTriggerHelper - 工具类

### 功能
提供静态方法，方便在代码中触发对话。

### 使用示例

```csharp
// 启动对话
DialogueTriggerHelper.StartConversation("Tutorial/Welcome");

// 条件启动
DialogueTriggerHelper.StartConversationIf("Tutorial/Day2", "Variable['CurrentDay'] >= 2");

// 队列对话（等待当前对话结束后启动）
DialogueTriggerHelper.QueueConversation("Tutorial/Next");

// 聚焦并启动对话
DialogueTriggerHelper.ShowSpotlightAndTalk("Tutorial/Build", buildButton.GetComponent<RectTransform>());

// Quest 操作
DialogueTriggerHelper.SetQuestState("TUT_BuildBasics", "success");
DialogueTriggerHelper.SetQuestEntryState("TUT_BuildBasics", 1, "success");

// Lua 变量操作
DialogueTriggerHelper.SetVariable("MyVar", 100);
int value = DialogueTriggerHelper.GetVariableInt("MyVar");
```

---

## 7. 其他 Sequencer 命令

### GiveReward
```
Sequence: GiveReward(Money, 100)
Sequence: GiveReward(Fame, 50)
Sequence: GiveReward(Blueprint, ShelfVibrator)
Sequence: GiveReward(Customer, Customer_001)
```

### RaiseMarker
```
Sequence: RaiseMarker(FirstShelfPlaced)
Sequence: RaiseMarker(StoreOpened)
```

### FreezeUI / UnfreezeUI
```
Sequence: FreezeUI(ShelfListPanelRoot)           // 冻结面板
Sequence: UnfreezeUI(ShelfListPanelRoot)          // 解冻指定面板
Sequence: UnfreezeUI()                            // 解冻全部
```

---

## 快速设置指南

### 1. 创建 DialogueBridgeManager

```
Hierarchy:
└── DialogueBridgeManager (DontDestroyOnLoad)
    ├── PopLifeLuaFunctions
    ├── TutorialMarkerBridge
    ├── GameStateLuaSync
    ├── CustomerClickHandler
    └── UIFreezeManager
```

### 2. 创建 Spotlight UI

```
Canvas (Screen Space - Overlay, Sort Order: 1000)
└── SpotlightPanel
    ├── MaskImage (全屏, Color: 黑色 70% 透明)
    └── HighlightBorder (边框图片, Color: 黄色)
```

### 3. 更新顾客预制体

```
Customer Prefab:
├── ... (原有组件)
├── BoxCollider2D (Is Trigger: true)
└── CustomerDialogueTrigger
```

### 4. 创建 Dialogue Database 结构

```
Conversations:
├── Tutorial/
│   ├── Welcome
│   ├── BuildBasics
│   └── FirstDay
├── Customer/
│   ├── Generic
│   ├── Trait_Gay
│   ├── Trait_Shy
│   └── Loyalty_1
└── Info/
    └── ...

Quests:
├── TUT_Intro (教程：介绍)
├── TUT_BuildBasics (教程：建造基础)
└── TUT_FirstDay (教程：第一天营业)
```

---

## 注意事项

1. **顺序依赖**：确保 DialogueManager 在 DialogueBridge 组件之前初始化
2. **单例模式**：所有 Bridge 组件使用单例，添加到 DontDestroyOnLoad 对象
3. **Odin Inspector**：某些 Inspector 特性需要 Odin Inspector 插件
4. **时间缩放**：Spotlight 动画使用 unscaledTime，在暂停时仍可播放
5. **Layer 设置**：建议为顾客创建专用 Layer，提高点击检测效率
