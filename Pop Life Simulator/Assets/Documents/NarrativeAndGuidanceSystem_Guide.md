# 叙事与指引系统集成指南
# Narrative and Guidance System Integration Guide

## 📋 概述

本项目实现了两套独立但协同工作的教程系统：

1. **叙事系统（Narrative System）** - 扇形对话面板，用于NPC与玩家对话
2. **指引系统（Guidance System）** - 全屏遮罩教程，用于操作指引

两套系统通过 `TutorialContentDispatcher` 统一调度，根据教程标记自动选择合适的展示方式。

### 动画引擎
系统使用 **PrimeTween** 作为动画库，提供高性能、流畅的UI动画效果。

---

## 🏗️ 系统架构

```
TutorialEventBus (标记触发)
        ↓
TutorialContentDispatcher (内容分发)
        ↓
    ┌───┴───┐
    ↓       ↓
NarrativeManager  GuidanceManager
    ↓              ↓
FanConversationPanel  GuidanceMask
```

---

## 🎭 叙事系统（Narrative System）

### 核心组件

#### 1. NarrativeSegment.cs
- 单个对话片段
- 支持树形结构（多个后续片段）
- 包含文本、说话者、肖像等数据

#### 2. NarrativeSequence.cs
- 管理整个对话流程
- 支持前进/后退导航
- 事件系统（OnSegmentChanged, OnSequenceCompleted等）

#### 3. NarrativeSO.cs (ScriptableObject)
- 存储对话数据
- 配置触发条件（VIP限定、天数要求、声望要求）
- 完成奖励配置

#### 4. NarrativeManager.cs
- 全局单例管理器
- 控制对话播放
- 处理奖励发放

### UI组件

#### FanConversationPanel.cs
**扇形对话面板特性：**
- 三层对话框（上、中、下）
- 中间框横向最大，上下框扇形斜置
- 滚轮/点击切换对话
- 平滑过渡动画
- 历史翻阅功能

**使用示例：**
```csharp
// 启动对话
NarrativeManager.Instance.StartNarrative("NARR_WELCOME");

// 导航控制
NarrativeManager.Instance.NavigateForward();  // 下一句
NarrativeManager.Instance.NavigateBackward(); // 上一句
```

### 创建新对话

#### 步骤1：创建NarrativeSO资产
```
1. Project窗口右键
2. Create → PopLife → Narrative → Narrative Data
3. 配置对话数据：
   - Narrative ID: 唯一标识
   - Character Portrait: 角色肖像
   - Narrative Sequence: 对话序列
```

#### 步骤2：构建对话树
```csharp
// 代码方式创建
var segment1 = new NarrativeSegment("SEG_001", "Midori", "欢迎来到商店！");
var segment2 = new NarrativeSegment("SEG_002", "Midori", "让我教你如何经营。");
segment1.AddNextSegment(segment2);

var sequence = new NarrativeSequence("SEQ_WELCOME", "Welcome Tutorial");
sequence.Initialize(segment1);
```

---

## 🎯 指引系统（Guidance System）

### 核心组件

#### 1. GuidanceStep.cs
- 单个指引步骤
- 支持多种动作类型（点击目标、点击任意、等待等）
- 高亮形状配置（矩形、圆形）
- 完成条件设置

#### 2. GuidanceSequence.cs
- 管理指引步骤序列
- 支持跳过功能
- 暂停游戏选项

#### 3. GuidanceManager.cs
- 全局单例管理器
- 控制指引播放
- 管理UI组件

### UI组件

#### GuidanceMask.cs
**遮罩特性：**
- 全屏半透明遮罩
- 动态镂空区域（允许特定区域点击）
- 支持UI元素和世界对象
- 平滑过渡动画

**镂空实现：**
```csharp
// UI元素镂空
guidanceMask.CreateCutoutForUI(targetButton.GetComponent<RectTransform>());

// 世界对象镂空
guidanceMask.CreateCutoutForWorldObject(shelfTransform, new Vector2(200, 100));

// 屏幕位置镂空
guidanceMask.CreateCutout(new Rect(100, 100, 200, 150));
```

#### InstructionBox.cs
**文本框特性：**
- 自适应大小
- 9种锚点位置
- 淡入淡出动画
- 强调效果（PrimeTween缩放动画）

#### HighlightZone.cs
**高亮特性：**
- 边框高亮
- 箭头指示
- 脉冲动画
- 自动方向计算

### 创建新指引

#### 步骤1：构建指引序列
```csharp
var sequence = new GuidanceSequence("GUIDE_001", "Build Tutorial");

// 步骤1：点击按钮
var step1 = new GuidanceStep(
    "STEP_001",
    "点击货架按钮选择要放置的货架",
    ActionType.ClickTarget
);
step1.SetTargetUI(shelfButton);

// 步骤2：放置货架
var step2 = new GuidanceStep(
    "STEP_002",
    "点击空地放置货架",
    ActionType.ClickTarget
);

sequence.AddStep(step1);
sequence.AddStep(step2);
```

#### 步骤2：启动指引
```csharp
GuidanceManager.Instance.StartGuidance("GUIDE_001");
```

---

## 🔄 系统集成

### TutorialContentDispatcher

**功能：**
- 监听教程标记（TutorialMarker）
- 根据映射决定触发内容类型
- 管理内容队列
- 处理混合内容

### 配置映射

```csharp
// 在Inspector中配置
Content Mappings:
├── GameStarted → Narrative: NARR_WELCOME
├── FirstBuildPhaseEntered → Guidance: GUIDE_BUILD
├── TwoShelvesPlaced → Narrative: NARR_READY_OPEN
└── StoreOpened → Mixed: MIXED_CELEBRATION

// 或代码配置
dispatcher.AddOrUpdateMapping(
    TutorialMarker.FirstShelfPlaced,
    ContentType.Guidance,
    "GUIDE_SHELF_PLACED"
);
```

### 触发流程

```csharp
// 1. 在游戏逻辑中触发标记
TutorialEventBus.RaiseMarker(TutorialMarker.FirstShelfPlaced);

// 2. Dispatcher自动处理
// TutorialContentDispatcher.HandleMarkerTriggered()
//   → 查找映射
//   → 判断类型
//   → 调用对应Manager

// 3. 显示内容
// NarrativeManager或GuidanceManager接管控制
```

---

## 📦 场景配置

### 必需的GameObject

```
场景层级:
├── TutorialContentDispatcher
│   └── TutorialContentDispatcher.cs
├── NarrativeManager
│   ├── NarrativeManager.cs
│   └── FanConversationPanel (预制体)
└── GuidanceManager
    ├── GuidanceManager.cs
    ├── GuidanceMask
    ├── InstructionBox
    └── HighlightZone
```

### Canvas配置

#### 叙事Canvas
```
NarrativeCanvas
├── Render Mode: Screen Space - Overlay
├── Sort Order: 100
└── Canvas Scaler: Scale With Screen Size
```

#### 指引Canvas
```
GuidanceCanvas
├── Render Mode: Screen Space - Overlay
├── Sort Order: 999 (最顶层)
└── Canvas Scaler: Scale With Screen Size
```

---

## 🎨 UI预制体结构

### FanConversationPanel预制体
```
FanConversationPanel
├── CharacterPortraitContainer
│   ├── PortraitImage
│   └── NameLabel
├── ConversationBoxes
│   ├── TopBox (ConversationBox)
│   ├── CenterBox (ConversationBox)
│   └── BottomBox (ConversationBox)
└── FinishButton
```

### GuidanceMask预制体
```
GuidanceMask
├── FullscreenMask (Image)
│   └── Material: CutoutMaterial (自定义Shader)
├── ClickBlocker (透明Image)
└── Settings
    ├── Mask Color: (0,0,0,0.7)
    └── Block Outside Clicks: true
```

---

## 🛠️ 常见用法

### 1. 简单信息展示
```csharp
// 叙事方式（角色对话）
NarrativeManager.Instance.StartNarrative("NARR_INFO");

// 指引方式（提示框）
var step = new GuidanceStep("INFO", "这是商店的货架区域", ActionType.ClickAnywhere);
var sequence = new GuidanceSequence("GUIDE_INFO", "Information");
sequence.AddStep(step);
GuidanceManager.Instance.StartGuidance(sequence);
```

### 2. 操作教学
```csharp
// 创建指引序列
var buildGuide = new GuidanceSequence("GUIDE_BUILD", "Build Tutorial");

// 高亮按钮
var step1 = new GuidanceStep("STEP_1", "点击这里打开建造菜单", ActionType.ClickTarget);
step1.SetTargetUI(buildButton.GetComponent<RectTransform>());
buildGuide.AddStep(step1);

// 启动
GuidanceManager.Instance.StartGuidance(buildGuide);
```

### 3. 混合内容
```csharp
// 先对话后指引
TutorialContentDispatcher.Instance.AddOrUpdateMapping(
    TutorialMarker.StoreOpened,
    ContentType.Mixed,
    "MIXED_STORE_OPEN"
);

// 触发时会按顺序播放
TutorialEventBus.RaiseMarker(TutorialMarker.StoreOpened);
```

---

## 🐛 调试功能

### NarrativeManager调试
```csharp
// Inspector右键菜单
[ContextMenu("Test Start First Narrative")]

// 运行时查看
NarrativeManager.Instance.GetActiveNarrative();
NarrativeManager.Instance.IsNarrativeActive();
```

### GuidanceManager调试
```csharp
// Inspector右键菜单
[ContextMenu("Test Start First Guidance")]

// 跳过当前指引
GuidanceManager.Instance.SkipCurrentGuidance();

// 启用调试模式
[SerializeField] private bool debugMode = true;
```

### Dispatcher调试
```csharp
// 打印所有映射
[ContextMenu("Print All Mappings")]

// 查看队列状态
TutorialContentDispatcher.Instance.ClearQueues();
```

---

## ⚠️ 注意事项

### 1. 命名规范
- **避免使用"Dialogue"命名**（与NodeCanvas冲突）
- 使用"Narrative"代替对话
- 使用"Guidance"代替教程

### 2. 资源路径
```
Resources/
├── NarrativeData/        # 叙事数据
│   ├── Characters/       # 角色对话
│   └── Templates/        # 模板
├── GuidanceData/         # 指引数据
│   ├── Tutorials/        # 教程序列
│   └── Templates/        # 模板
└── Sprites/
    ├── Portraits/        # 角色肖像
    └── UI/              # UI图标
```

### 3. 性能优化
- 使用对象池管理对话框
- 遮罩使用Shader实现镂空
- 避免频繁创建/销毁UI
- 使用PrimeTween动画库优化性能

### 4. 动画系统
本系统使用 **PrimeTween** 作为动画引擎，提供流畅的UI动画效果：

#### 已实现的动画效果
- **InstructionBox**: 强调动画（缩放ping-pong效果）
- **CharacterPortrait**:
  - 说话动画（弹跳效果）
  - 强调动画（震动效果）
  - 情绪变化（Alpha闪烁）
  - 出现动画（缩放过渡）
- **ConversationBox**: 点击反馈（缩放punch效果）
- **FanConversationPanel**: 完成按钮动画（Back缓动出现）

#### PrimeTween使用示例
```csharp
// 缩放动画
Tween.Scale(transform, Vector3.one * 1.05f, 0.2f, Ease.InOutQuad);

// 位置动画
Tween.LocalPosition(transform, targetPos, 0.3f, Ease.OutBack);

// Alpha动画
Tween.Alpha(canvasGroup, 1f, 0.5f, Ease.Linear);
```

### 5. 扩展建议
- 添加语音支持
- 实现存档系统
- 多语言支持
- 添加打字机效果

---

## 📚 示例工程

### 完整示例：首次建造教程

```csharp
public class TutorialExample : MonoBehaviour
{
    void Start()
    {
        // 1. 配置映射
        var dispatcher = TutorialContentDispatcher.Instance;

        // 游戏开始 - 叙事
        dispatcher.AddOrUpdateMapping(
            TutorialMarker.GameStarted,
            ContentType.Narrative,
            "NARR_WELCOME"
        );

        // 进入建造模式 - 指引
        dispatcher.AddOrUpdateMapping(
            TutorialMarker.FirstBuildPhaseEntered,
            ContentType.Guidance,
            "GUIDE_BUILD_MODE"
        );
    }

    void OnGameStart()
    {
        // 2. 触发标记
        TutorialEventBus.RaiseMarker(TutorialMarker.GameStarted);
        // 自动显示Midori欢迎对话
    }

    void OnEnterBuildMode()
    {
        // 3. 触发建造指引
        TutorialEventBus.RaiseMarker(TutorialMarker.FirstBuildPhaseEntered);
        // 自动显示建造操作指引
    }
}
```

---

## 🔧 故障排除

### 问题1：对话/指引没有显示
- 检查Manager是否在场景中
- 确认EnableNarratives/EnableGuidance已勾选
- 查看Console是否有错误日志
- 验证资源路径是否正确

### 问题2：镂空区域无法点击
- 检查GuidanceMask的allowCutoutClick设置
- 确认blockOutsideClicks配置
- 验证RaycastTarget设置

### 问题3：扇形对话框位置错误
- 检查Canvas Scaler配置
- 验证锚点设置
- 调整fanPivot位置

### 问题4：内容触发顺序错误
- 检查Priority设置
- 验证队列处理逻辑
- 使用debugMode查看执行流程

---

## 📈 未来扩展

### 计划功能
1. **对话分支选择** - 玩家选择影响剧情
2. **条件触发** - 基于游戏状态的复杂条件
3. **动态内容** - 根据玩家数据生成个性化内容
4. **成就系统集成** - 完成教程解锁成就
5. **数据分析** - 追踪玩家教程完成率

### 架构优化
1. **资源异步加载** - 减少初始加载时间
2. **内容热更新** - 支持运行时更新教程内容
3. **编辑器工具** - 可视化对话树编辑器
4. **本地化支持** - 多语言教程系统

---

## 📝 总结

本系统提供了灵活的教程展示方案：
- **叙事系统** - 适合剧情对话、角色介绍
- **指引系统** - 适合操作教学、功能说明
- **统一调度** - 自动选择最合适的展示方式

通过合理配置和使用，可以创建流畅、直观的新手引导体验。

---

*文档版本: 1.1*
*更新日期: 2024*
*作者: Claude Assistant*

### 更新记录
- v1.1: 添加PrimeTween动画库集成说明
- v1.0: 初始版本