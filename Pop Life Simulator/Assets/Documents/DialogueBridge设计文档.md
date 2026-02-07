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
│   ├── SpotlightManager.cs      # Spotlight 管理器
│   ├── SpotlightPanel.cs        # Spotlight 遮罩面板
│   ├── SpotlightTooltip.cs      # Spotlight 提示框组件
│   └── TooltipPosition.cs       # 提示框位置枚举
└── Sequencer/
    ├── SequencerCommandSpotlight.cs           # Spotlight(uiName)
    ├── SequencerCommandSpotlightWorld.cs      # SpotlightWorld(objectName)
    ├── SequencerCommandSpotlightRect.cs       # SpotlightRect(x,y,w,h) 像素坐标
    ├── SequencerCommandSpotlightNormalized.cs # SpotlightNormalized(x,y,w,h) 百分比坐标
    ├── SequencerCommandSpotlightOff.cs        # SpotlightOff()
    ├── SequencerCommandSpotlightTooltip.cs    # SpotlightTooltip(position) 显示提示框
    ├── SequencerCommandSpotlightTooltipOff.cs # SpotlightTooltipOff() 隐藏提示框
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

## 4. Spotlight 系统

### 功能
创建聚焦效果，高亮显示 UI 元素或场景物体。

### 支持的目标类型
- **UI 元素**：任何带有 RectTransform 的 UI 对象
- **场景物体**：任何带有 Renderer 的 2D/3D 对象

### 支持的形状
- `Rectangle` - 矩形
- `Circle` - 圆形
- `RoundedRectangle` - 圆角矩形（默认）

### 在 Dialogue Editor 中使用 Sequencer 命令

#### 按 GameObject 名称聚焦
```
// 显示 UI 元素聚焦
Spotlight(BuildButton)
Spotlight(MoneyDisplay, Circle)

// 显示场景物体聚焦
SpotlightWorld(Shelf_Lingerie)
SpotlightWorld(Cashier, RoundedRectangle)
```

#### 按屏幕坐标聚焦（像素）
```
// SpotlightRect(x, y, width, height[, shape])
// 坐标系：左下角为 (0,0)
SpotlightRect(100, 200, 300, 150)
SpotlightRect(100, 200, 300, 150, Circle)
```

#### 按屏幕百分比聚焦（推荐，适配不同分辨率）
```
// SpotlightNormalized(x, y, width, height[, shape])
// 坐标范围 0-1：x(0=左,1=右), y(0=底,1=顶)
SpotlightNormalized(0.4, 0.4, 0.2, 0.2)              // 屏幕中心
SpotlightNormalized(0, 0.85, 0.2, 0.1)               // 左上角
SpotlightNormalized(0.35, 0.35, 0.3, 0.3, Circle)    // 中心圆形
```

**常用位置参考（SpotlightNormalized）：**
| 位置 | 参数 |
|------|------|
| 屏幕中心 | `0.4, 0.4, 0.2, 0.2` |
| 左上角 | `0, 0.85, 0.2, 0.1` |
| 右上角 | `0.8, 0.85, 0.2, 0.1` |
| 左下角 | `0, 0.05, 0.2, 0.1` |
| 右下角 | `0.8, 0.05, 0.2, 0.1` |

#### 隐藏聚焦
```
SpotlightOff()
```

#### 组合使用
```
Spotlight(BuildButton); Delay(3); SpotlightOff()
SpotlightNormalized(0.4, 0.4, 0.2, 0.2); Delay(2); SpotlightOff()
```

### 在代码中使用

```csharp
// 显示 UI 聚焦
SpotlightManager.Instance.ShowSpotlight(myRectTransform, SpotlightShape.RoundedRectangle);

// 显示场景物体聚焦
SpotlightManager.Instance.ShowSpotlightOnWorldObject(myGameObject);

// 按名称显示
SpotlightManager.Instance.ShowSpotlightByName("BuildButton");
SpotlightManager.Instance.ShowSpotlightOnWorldObjectByName("Shelf_Lingerie");

// 按像素坐标显示
SpotlightManager.Instance.ShowSpotlightRect(100, 200, 300, 150);
SpotlightManager.Instance.ShowSpotlightRect(new Rect(100, 200, 300, 150), SpotlightShape.Circle);

// 隐藏
SpotlightManager.Instance.HideSpotlight();
```

### 设置
1. 创建 Spotlight UI 预制体：
   - Canvas (Screen Space - Overlay)
     - SpotlightPanel (带 SpotlightPanel 组件)
       - MaskImage (全屏遮罩)
       - HighlightBorder (高亮边框)
2. 在 `DialogueBridgeManager` 上添加 `SpotlightManager` 组件
3. 将 SpotlightPanel 引用拖到 Inspector

---

## 4.1 SpotlightTooltip - 提示框系统

### 功能
在 Spotlight 高亮区域旁边显示提示文字，自动读取当前对话节点的 Dialogue Text。

### 特性
- **位置控制**：支持 Auto/Left/Right/Top/Bottom/Custom 六种定位方式
- **自动隐藏原对话框**：显示 Tooltip 时自动隐藏 Dialogue System 的对话框 UI
- **自动清理**：
  - 节点切换时自动关闭 Tooltip（需重新调用 SpotlightTooltip 显示）
  - 对话结束时自动关闭 Tooltip 和 Spotlight
- **样式自定义**：支持自定义背景图片和箭头图片

### 位置说明

| 位置 | 说明 |
|------|------|
| `Auto` | 自动选择空间最大的方向（推荐） |
| `Left` | Spotlight 左侧，箭头指向右 |
| `Right` | Spotlight 右侧，箭头指向左 |
| `Top` | Spotlight 上方，箭头指向下 |
| `Bottom` | Spotlight 下方，箭头指向上 |
| `Custom` | 自定义屏幕位置（归一化坐标 0-1），不显示箭头 |

### 在 Dialogue Editor 中使用 Sequencer 命令

**参数格式：**
```
SpotlightTooltip(position[, triggerMode][, customX, customY])
```

| 参数 | 值 | 说明 |
|------|-----|------|
| `position` | `Auto` `Left` `Right` `Top` `Bottom` `Custom` | Tooltip 位置 |
| `triggerMode` | `ClickAnywhere` `ClickSpotlight` `ClickButton` | 继续对话的触发方式（可选，默认 `ClickAnywhere`） |
| `customX, customY` | 0-1 浮点数 | 仅 `Custom` 位置时使用 |

**基本用法：**
```
// 先显示 Spotlight，再显示 Tooltip（默认点击任意位置继续）
Spotlight(BuildButton); SpotlightTooltip(Right)
SpotlightNormalized(0.4, 0.4, 0.2, 0.2); SpotlightTooltip(Auto)

// 指定位置
SpotlightTooltip(Left)     // 左侧
SpotlightTooltip(Right)    // 右侧
SpotlightTooltip(Top)      // 上方
SpotlightTooltip(Bottom)   // 下方
SpotlightTooltip(Auto)     // 自动选择

// 自定义位置（归一化坐标）
SpotlightTooltip(Custom, 0.7, 0.5)  // 屏幕 70% 宽度, 50% 高度处
```

**指定继续对话的触发方式（ContinueTriggerMode）：**
```
// ClickAnywhere - 点击屏幕任意位置继续（默认）
Spotlight(BuildButton); SpotlightTooltip(Right)
Spotlight(BuildButton); SpotlightTooltip(Right, ClickAnywhere)

// ClickSpotlight - 仅点击高亮区域才继续
Spotlight(MoneyDisplay); SpotlightTooltip(Right, ClickSpotlight)

// ClickButton - 仅点击目标 Button 才继续（目标须为带 Button 组件的 UI）
Spotlight(BuildButton); SpotlightTooltip(Right, ClickButton)

// Custom 位置 + 触发方式
SpotlightTooltip(Custom, ClickSpotlight, 0.7, 0.5)
```

**手动关闭 / 完整示例：**
```
// 手动关闭 Tooltip
SpotlightTooltipOff()

// 完整示例：显示→等待→关闭
Spotlight(ShelfButton); SpotlightTooltip(Right); Delay(3); SpotlightTooltipOff(); SpotlightOff()
```

### ContinueTriggerMode 详细说明

Tooltip 显示时会隐藏原 Dialogue UI（含 Continue Button），需要替代方式让玩家继续对话：

| 模式 | 行为 | 适用场景 |
|------|------|---------|
| `ClickAnywhere` | 点击屏幕任意位置继续对话 | 通用提示，快速跳过 |
| `ClickSpotlight` | 仅点击 Spotlight 高亮区域内才继续 | 引导玩家注意特定区域 |
| `ClickButton` | 仅点击目标 Button 才继续 | 要求玩家点击指定按钮（如"开始建造"按钮） |

**ClickButton 注意事项：**
- Spotlight 目标必须是通过 `Spotlight(uiName)` 指定的 UI 元素
- 该 UI 元素必须有 `UnityEngine.UI.Button` 组件
- 如果目标无 Button 组件，会在 Debug 模式下输出警告

### 自动清理机制

| 场景 | Tooltip 行为 | 对话框 UI 行为 | Spotlight 行为 | Button 订阅 |
|------|-------------|---------------|---------------|------------|
| 手动 `SpotlightTooltipOff()` | 隐藏 | 恢复显示 | 保持不变 | 自动清理 |
| **节点切换** | **自动隐藏** | **自动恢复** | **自动隐藏** | 自动清理 |
| 对话结束 (End) | 自动隐藏 | 自然消失 | 自动隐藏 | 自动清理 |

**典型使用流程：**
```
节点1: Spotlight(A); SpotlightTooltip(Right)  → 显示高亮A + Tooltip
       [用户点击继续]
节点2: (Tooltip 自动关闭, 对话框恢复)           → 正常显示对话框
       Spotlight(B); SpotlightTooltip(Left)   → 显示高亮B + 新Tooltip
       [用户点击继续]
节点3: SpotlightOff()                          → 关闭所有效果
```

### 在代码中使用

```csharp
// 显示 Tooltip（从当前对话节点读取文本，默认 ClickAnywhere）
SpotlightManager.Instance.ShowTooltipFromDialogue(TooltipPosition.Right);

// 指定触发方式
SpotlightManager.Instance.ShowTooltipFromDialogue(
    TooltipPosition.Right, null, ContinueTriggerMode.ClickSpotlight);

// ClickButton 模式（需先调用 ShowSpotlight 指定 UI 目标）
SpotlightManager.Instance.ShowSpotlightByName("BuildButton");
SpotlightManager.Instance.ShowTooltipFromDialogue(
    TooltipPosition.Right, null, ContinueTriggerMode.ClickButton);

// 自定义位置 + 触发方式
SpotlightManager.Instance.ShowTooltipFromDialogue(
    TooltipPosition.Custom, new Vector2(0.7f, 0.5f), ContinueTriggerMode.ClickAnywhere);

// 显示 Tooltip（自定义文本）
SpotlightManager.Instance.ShowTooltip("This is a hint!", TooltipPosition.Auto);

// 隐藏 Tooltip
SpotlightManager.Instance.HideTooltip();

// 检查状态
bool isActive = SpotlightManager.Instance.IsTooltipActive;
```

### 设置

1. 创建 SpotlightTooltip UI 预制体：
   ```
   SpotlightTooltip (RectTransform + CanvasGroup)
   ├── Background (Image)        ← 自定义 9-slice 背景图
   ├── Arrow (Image)             ← 箭头图片（可选）
   └── Content
       └── Text (TextMeshProUGUI)
   ```

2. 添加 `SpotlightTooltip` 组件到预制体

3. 在 `SpotlightManager` 的 Inspector 中：
   - 将 SpotlightTooltip 预制体拖到 `Tooltip` 字段
   - 配置 `Auto Hide Dialogue UI`（默认开启）
   - 配置 `Auto Close On Node Change`（默认开启）

4. 配置 SpotlightTooltip 组件：
   - 设置背景图片（建议 9-slice 格式）
   - 设置箭头图片
   - 调整间距（margin）和最大宽度（maxWidth）

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

---

## 快速设置指南

### 1. 创建 DialogueBridgeManager

```
Hierarchy:
└── DialogueBridgeManager (DontDestroyOnLoad)
    ├── PopLifeLuaFunctions
    ├── TutorialMarkerBridge
    ├── GameStateLuaSync
    └── CustomerClickHandler
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
