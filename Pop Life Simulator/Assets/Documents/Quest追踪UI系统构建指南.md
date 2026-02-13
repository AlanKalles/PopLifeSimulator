# Quest 追踪 UI 系统构建指南

## 概述
Quest 追踪 UI 系统为玩家提供任务进度的可视化追踪。包含三个核心 UI 组件：右上角追踪面板、鼠标悬停 Tooltip（含图标和类型标签）、点击打开的任务日志面板（左侧列表 + 右侧详情）。

系统仅负责 **UI 展示层**，任务数据来自两个数据源：
- **Dialogue System QuestLog**：任务状态（active/success/failure）、Entry 进度、标题、描述
- **QuestDefinition ScriptableObject**：DDL 天数、颁布者、奖励、图标等元数据

---

## 一、架构总览

```
QuestLog (状态/进度/文本)  +  QuestDefinition SO (DDL/颁布者/奖励)
                    ↓
            QuestDataService (合并数据 → ViewModel)
                    ↓
   ┌────────────────┼────────────────┐
   ▼                ▼                ▼
QuestTrackerPanel  QuestTooltip   QuestLogPanel
(右上角,可折叠)    (鼠标跟随,      (左右分栏:
   │                含icon+type)     列表+详情)
   └── QuestTrackerEntry × N
         hover → QuestTooltip
         click → QuestLogPanel(focusQuest)
```

---

## 二、文件清单

| 文件路径 | 类名 | 职责 |
|---------|------|------|
| `Scripts/Data/QuestDefinition.cs` | QuestDefinition | 任务元数据 SO 定义 |
| `Scripts/UI/Quest/QuestDataService.cs` | QuestDataService | 数据桥接服务（单例） |
| `Scripts/UI/Quest/QuestTrackerPanel.cs` | QuestTrackerPanel | 右上角追踪面板 |
| `Scripts/UI/Quest/QuestTrackerEntry.cs` | QuestTrackerEntry | 追踪面板中的单条任务 |
| `Scripts/UI/Quest/QuestTooltip.cs` | QuestTooltip | 鼠标悬停提示（含icon+type） |
| `Scripts/UI/Quest/QuestTooltipEntryItem.cs` | QuestTooltipEntryItem | Tooltip 中的条目行 |
| `Scripts/UI/Quest/QuestLogPanel.cs` | QuestLogPanel | 任务日志面板（左列表+右详情） |
| `Scripts/UI/Quest/QuestLogListEntry.cs` | QuestLogListEntry | 日志面板左侧列表条目 |
| `Scripts/UI/Quest/QuestRewardItem.cs` | QuestRewardItem | 详情面板中的奖励条目 |

修改的现有文件：
- `Scripts/Manager/UIManager.cs` — 添加了 Quest UI 面板引用
- `Scripts/Data/AudioKeys.cs` — 添加了 Quest 音效常量

---

## 三、Unity Editor 构建步骤

### 步骤 1：创建 QuestDefinition SO 资产

1. 在 `Assets/Resources/ScriptableObjects/` 下创建 `Quests` 文件夹
2. 右键 → Create → PopLife → Quest → QuestDefinition
3. 填写字段：

| 字段 | 说明 | 示例 |
|------|------|------|
| Quest Name | **必须**与 Dialogue Database 中 Quest Item 的 Name 一致 | `TUT_BuildBasics` |
| Quest Type | Main（主线）或 Side（支线） | Side |
| Deadline Days | 从激活日起的有效天数，0 = 永不过期 | 3 |
| Giver Name | 颁布者名称（英文，面向玩家） | `Mayor Johnson` |
| Giver Portrait | 颁布者头像 Sprite | 拖入 Sprite |
| Rewards | 奖励列表（Money/Fame/Blueprint/Customer） | Money: 500 |
| Quest Icon | 任务图标 Sprite | 拖入 Sprite |
| Sort Priority | 排序优先级（0-100，越大越靠前） | 80 |

### 步骤 2：在 Dialogue Database 中创建对应 Quest

1. 打开 Dialogue Editor（Tools → Pixel Crushers → Dialogue System → Dialogue Editor）
2. 切换到 Quests/Items 标签页
3. 点击 "+" 创建新 Quest Item
4. 设置字段：
   - **Name**: 与 QuestDefinition 的 Quest Name 一致（如 `TUT_BuildBasics`）
   - **Display Name**: UI 显示的标题（如 `Build Your First Shelf`）
   - **Description**: 任务描述文本
   - **Group**: `Main` 或 `Side`（与 QuestDefinition.QuestType 对应）
   - **Is Item**: 取消勾选（标记为 Quest 而非 Item）
   - **Trackable**: 勾选
   - **Track**: 勾选（默认追踪）
5. 添加 Quest Entry（子目标）：
   - Entry 1: `Place a shelf in your store`
   - Entry 2: `Open the store for business`
   - ...

### 步骤 3：搭建 QuestDataService

1. 在场景中找到 **Dialogue Manager** GameObject
2. 在其下创建空子物体，命名为 `[QuestDataService]`
   - **必须**是 DialogueManager 的子物体（接收 `OnQuestStateChange` BroadcastMessage）
3. 添加 `QuestDataService` 组件
4. 将所有 QuestDefinition SO 拖入 `Quest Definitions` 数组

```
Hierarchy:
├── Dialogue Manager
│   ├── [QuestDataService]          ← 挂载 QuestDataService
│   └── ... (其他 Dialogue System 子物体)
```

### 步骤 4：搭建 QuestTrackerPanel（右上角面板）

在主 UI Canvas 下创建以下 Hierarchy：

```
QuestTrackerPanel                          ← 挂载 QuestTrackerPanel.cs
├── Background (Image, 半透明黑底)
├── Header
│   ├── HeaderText (TMP)                   ← "QUESTS (3)"
│   ├── CollapseButton (Button)            ← 折叠按钮
│   │   ├── CollapseIcon (Image/TMP ▼)     ← 展开时显示
│   │   └── ExpandIcon (Image/TMP ►)       ← 折叠时显示
├── ContentContainer (VerticalLayoutGroup) ← 条目容器，需要 CanvasGroup
│   └── (运行时动态生成 QuestTrackerEntry)
```

**RectTransform 配置：**
- Anchor: Top-Right (1, 1) - (1, 1)
- Pivot: (1, 1)
- Anchored Position: (-20, -80)（在 ResourceDisplay 下方留出空间）
- Size: (300, 适配内容)

**QuestTrackerPanel Inspector 配置：**

| 字段 | 拖入 |
|------|------|
| Content Container | ContentContainer 的 RectTransform |
| Quest Entry Prefab | QuestTrackerEntry 预制体 |
| Collapse Button | CollapseButton |
| Collapse Icon | CollapseIcon GameObject |
| Expand Icon | ExpandIcon GameObject |
| Content Canvas Group | ContentContainer 上的 CanvasGroup |
| Header Text | HeaderText (TMP) |
| Quest Tooltip | QuestTooltip 实例（见步骤 5） |
| Quest Log Panel | QuestLogPanel 实例（见步骤 7） |

### 步骤 5：创建 QuestTrackerEntry 预制体

创建预制体保存到 `Assets/Prefab/UIs/` 或 `Assets/Prefab/Quest/`：

```
QuestTrackerEntry                          ← 挂载 QuestTrackerEntry.cs
├── Background (Image, Raycast Target=true) ← 悬停变色用
├── TitleText (TMP)                        ← 任务名称
├── DeadlineText (TMP)                     ← "3d left"
├── ProgressBar (Slider)                   ← 进度条
│   ├── Background
│   └── Fill Area
│       └── Fill
└── ProgressText (TMP)                     ← "2/5"
```

**布局建议：**
- 使用 HorizontalLayoutGroup 或手动定位
- TitleText 左对齐，DeadlineText 右对齐
- ProgressBar 在标题下方，ProgressText 在进度条右侧

### 步骤 6：搭建 QuestTooltip

在主 UI Canvas 下创建（需要在最上层，确保不被其他 UI 遮挡）：

```
QuestTooltip                               ← 挂载 QuestTooltip.cs + CanvasGroup
├── Background (Image, 深色半透明)
├── HeaderRow (HorizontalLayoutGroup)
│   ├── QuestIcon (Image, 24x24)           ← 任务图标（无图标时隐藏）
│   ├── TitleText (TMP, 粗体)              ← 任务名称
│   └── QuestTypeLabel (TMP, 右对齐)       ← "MAIN QUEST" / "SIDE QUEST"
├── DescriptionText (TMP)                  ← 任务描述
├── DeadlineText (TMP)                     ← "Deadline: 3 days left"
├── Separator (Image, 1px 高分隔线)
└── EntriesContainer (VerticalLayoutGroup) ← 条目容器
    └── (运行时动态生成 QuestTooltipEntryItem)
```

**关键配置：**
- CanvasGroup 组件（Awake 时自动添加，但建议手动添加）
- RectTransform: 使用 Pivot (0, 1)（左上角），这样偏移定位更自然
- 建议宽度 250-350px，高度由 ContentSizeFitter (Vertical Fit: Preferred) 自适应

**QuestTooltipEntryItem 预制体：**

```
QuestTooltipEntryItem                      ← 挂载 QuestTooltipEntryItem.cs
├── CheckmarkIcon (Image, 16x16)           ← ✓ 图标
└── EntryText (TMP)                        ← 条目文本
```

### 步骤 7：搭建 QuestLogPanel

在主 UI Canvas 下创建（居中大面板，左右分栏）：

```
QuestLogPanel                              ← 挂载 QuestLogPanel.cs + CanvasGroup
├── PanelRoot                              ← 控制显隐的根物体
│   ├── Overlay (Image, 全屏半透明黑底)    ← 可选
│   ├── PanelFrame (Image, 面板背景, ~800x500)
│   │   ├── Header
│   │   │   ├── HeaderTitle (TMP, "QUEST LOG")
│   │   │   └── CloseButton (Button)       ← 右上角 X 按钮
│   │   └── Content (HorizontalLayoutGroup)
│   │       ├── LeftPanel (宽35%)
│   │       │   ├── ListScrollView (ScrollRect)
│   │       │   │   └── ListContent (VLG + ContentSizeFitter) ← listContainer
│   │       │   │       ├── [GroupHeader 动态] "ACTIVE (3)"
│   │       │   │       ├── [QuestLogListEntry 动态] × N
│   │       │   │       ├── [GroupHeader 动态] "COMPLETED (2)"
│   │       │   │       └── [QuestLogListEntry 动态] × N
│   │       │   └── EmptyState (TMP, "No quests available")
│   │       ├── Divider (Image, 2px 竖线)
│   │       └── RightPanel (宽65%)
│   │           ├── DetailContent          ← detailContent 根物体
│   │           │   ├── DetailHeader
│   │           │   │   ├── QuestIcon (Image)
│   │           │   │   ├── TitleText (TMP, 大号粗体)
│   │           │   │   ├── QuestTypeLabel (TMP)
│   │           │   │   └── QuestStateLabel (TMP) ← "IN PROGRESS"/"COMPLETED"/"FAILED"
│   │           │   ├── DescriptionText (TMP)
│   │           │   ├── EntriesSection
│   │           │   │   ├── SectionTitle (TMP, "Requirements")
│   │           │   │   └── EntriesContainer (VLG)
│   │           │   ├── DeadlineSection
│   │           │   │   ├── SectionTitle (TMP, "Deadline")
│   │           │   │   └── DeadlineText (TMP)
│   │           │   ├── GiverSection
│   │           │   │   ├── SectionTitle (TMP, "Given by")
│   │           │   │   ├── GiverPortrait (Image)
│   │           │   │   └── GiverNameText (TMP)
│   │           │   └── RewardsSection
│   │           │       ├── SectionTitle (TMP, "Rewards")
│   │           │       └── RewardsContainer (HLG/VLG)
│   │           └── DetailEmptyState (TMP, "Select a quest to view details")
```

**QuestLogListEntry 预制体：**

```
QuestLogListEntry                          ← 挂载 QuestLogListEntry.cs
├── BackgroundImage (Image, Raycast=true)  ← 选中高亮用
├── TypeIndicator (Image, 4px 宽色条)      ← 金色=主线
├── TitleText (TMP)                        ← 任务名称
└── ProgressText (TMP, 右对齐)             ← "2/5"
```

**GroupHeader 预制体：**

```
GroupHeader (LayoutElement, MinHeight=30)
└── GroupLabel (TMP, bold, uppercase)      ← "ACTIVE (3)"
```

**QuestRewardItem 预制体：**

```
QuestRewardItem                            ← 挂载 QuestRewardItem.cs
├── RewardIcon (Image, 24x24)              ← 奖励类型图标
└── RewardText (TMP)                       ← "$500" / "50 Fame"
```

**QuestLogPanel Inspector 配置：**

| 字段 | 拖入 |
|------|------|
| Panel Root | PanelRoot GameObject |
| Canvas Group | QuestLogPanel 上的 CanvasGroup |
| Close Button | CloseButton |
| List Container | ListContent Transform |
| List Entry Prefab | QuestLogListEntry 预制体 |
| Group Header Prefab | GroupHeader 预制体 |
| Title Text | TitleText |
| Quest Icon | QuestIcon Image |
| Quest Type Label | QuestTypeLabel |
| Quest State Label | QuestStateLabel |
| Description Text | DescriptionText |
| Entries Container | EntriesContainer Transform |
| Entry Item Prefab | QuestTooltipEntryItem 预制体（复用） |
| Deadline Section | DeadlineSection GameObject |
| Deadline Text | DeadlineText |
| Giver Section | GiverSection GameObject |
| Giver Portrait | GiverPortrait Image |
| Giver Name Text | GiverNameText |
| Rewards Container | RewardsContainer Transform |
| Reward Item Prefab | QuestRewardItem 预制体 |
| Empty State Object | LeftPanel/EmptyState GameObject |
| Detail Empty State | DetailEmptyState GameObject |
| Detail Content | DetailContent GameObject |

### 步骤 8：配置 UIManager

1. 选中场景中的 UIManager GameObject
2. 在 Inspector 中找到 **Quest UI** 分组
3. 拖入引用：
   - Quest Tracker Panel → QuestTrackerPanel 实例
   - Quest Log Panel → QuestLogPanel 实例

---

## 四、数据流说明

### 任务状态变化时的刷新链路

```
1. 外部代码调用 QuestLog.SetQuestState("QuestName", QuestState.Active)
   ↓
2. Dialogue System 内部调用 BroadcastMessage("OnQuestStateChange", "QuestName")
   ↓
3. QuestDataService.OnQuestStateChange(string) 被触发
   - 如果是新激活的任务 → 记录 activationDayMap[questName] = currentDay
   - 触发 OnTrackedQuestsChanged 事件（QuestTrackerPanel 订阅）
   - 触发 OnQuestStateChanged 事件（QuestLogPanel 订阅）
   ↓
4. QuestTrackerPanel.RefreshQuests() 被调用
   QuestLogPanel（如果正在显示）也会自动刷新列表并保持选中
   - 调用 QuestDataService.GetTrackedQuests() 获取 ViewModel 列表
   - 清空旧条目，实例化新的 QuestTrackerEntry
   ↓
5. UI 更新完成
```

### DDL 计算公式

```
剩余天数 = (激活日 + DeadlineDays) - 当前天数

示例：
- 任务在 Day 3 激活，DeadlineDays = 5
- Day 3: 剩余 5 天
- Day 6: 剩余 2 天
- Day 8: 剩余 0 天（到期！）
```

### 排序规则

追踪面板中的任务按以下优先级排序：
1. **主线 (Main)** 排在 **支线 (Side)** 前面
2. 同类型内，**sortPriority** 高的排前面
3. 同优先级内，**DDL 更紧迫**的排前面（永不过期排最后）

---

## 五、ViewModel 结构

### QuestViewModel（追踪面板用）

| 字段 | 类型 | 来源 |
|------|------|------|
| questName | string | QuestLog |
| displayTitle | string | QuestLog.GetQuestTitle() |
| questType | QuestType | QuestDefinition SO / QuestLog Group |
| remainingDays | int | QuestDefinition.DeadlineDays + activationDay 计算 |
| completedEntries | int | QuestLog Entry 状态统计 |
| totalEntries | int | QuestLog.GetQuestEntryCount() |
| sortPriority | int | QuestDefinition SO |

### QuestDetailViewModel（详情面板用）

在 QuestViewModel 基础上增加：

| 字段 | 类型 | 来源 |
|------|------|------|
| description | string | QuestLog.GetQuestDescription() |
| giverName | string | QuestDefinition SO |
| giverPortrait | Sprite | QuestDefinition SO |
| questIcon | Sprite | QuestDefinition SO |
| rewards | QuestReward[] | QuestDefinition SO |
| entries | QuestEntryInfo[] | QuestLog Entry 文本 + 状态 |
| questState | QuestState | QuestLog.GetQuestState() |

---

## 六、交互行为

| 操作 | 行为 |
|------|------|
| 查看追踪面板 | 始终显示在右上角 |
| 点击折叠按钮 | EaseOutCubic 动画折叠/展开内容区域 |
| 鼠标悬停条目 | 条目背景变亮 + 显示 QuestTooltip（跟随鼠标） |
| 鼠标移出条目 | 背景恢复 + Tooltip 淡出 |
| 点击条目 | 隐藏 Tooltip + 打开 QuestLogPanel（聚焦到点击的任务） |
| 点击左侧列表条目 | 右侧详情切换到选中任务，左侧高亮更新 |
| 按 ESC | 关闭 QuestLogPanel |
| 点击日志面板 X 按钮 | 关闭 QuestLogPanel |

---

## 七、音效常量

定义在 `AudioKeys.cs` 中：

| 常量 | 键名 | 用途 |
|------|------|------|
| `QUEST_TRACKER_OPEN` | `Quest_Tracker_Open` | 展开追踪面板 |
| `QUEST_TRACKER_CLOSE` | `Quest_Tracker_Close` | 折叠追踪面板 |
| `QUEST_ENTRY_COMPLETE` | `Quest_Entry_Complete` | 子目标完成 |
| `QUEST_COMPLETE` | `Quest_Complete` | 整个任务完成 |

> 注意：需要在 AudioManager 中注册对应的 AudioClip 才能播放。

---

## 八、调试与测试

### 通过 Lua 命令快速测试

在 Dialogue System 的 Lua Console 中执行（或通过 `PopLifeLuaFunctions` 调用）：

```lua
-- 激活一个任务
SetQuestState("TUT_BuildBasics", "active")

-- 完成一个子目标
SetQuestEntryState("TUT_BuildBasics", 1, "success")

-- 完成整个任务
SetQuestState("TUT_BuildBasics", "success")

-- 查询任务状态
print(CurrentQuestState("TUT_BuildBasics"))
```

### 通过 C# 代码测试

```csharp
using PixelCrushers.DialogueSystem;

// 激活任务
QuestLog.SetQuestState("TestQuest", QuestState.Active);

// 启用追踪
QuestLog.SetQuestTracking("TestQuest", true);

// 完成子目标
QuestLog.SetQuestEntryState("TestQuest", 1, QuestState.Success);

// 手动刷新追踪面板
UIManager.Instance.RefreshQuestTracker();

// 打开任务日志面板并聚焦到指定任务
UIManager.Instance.ShowQuestLog("TestQuest");

// 打开任务日志面板（默认选中第一个Active任务）
UIManager.Instance.ShowQuestLog();
```

### 验证清单

- [ ] QuestDataService 挂在 DialogueManager 子物体上
- [ ] QuestDefinition SO 的 questName 与 Dialogue Database Quest Name 完全一致
- [ ] Dialogue Database 中 Quest 的 Trackable 和 Track 已勾选
- [ ] QuestTrackerPanel 的预制体引用已全部拖入
- [ ] UIManager 的 Quest UI 引用已配置
- [ ] 激活任务后追踪面板正确显示条目
- [ ] 悬停条目时 Tooltip 跟随鼠标并显示详情
- [ ] 点击条目时 QuestLogPanel 正确打开，左侧列表按 Active/Completed/Failed 分组
- [ ] 点击的任务在左侧高亮并在右侧显示详情
- [ ] 点击左侧不同条目可切换右侧详情
- [ ] 按 ESC 可关闭 QuestLogPanel
- [ ] 折叠/展开动画正常
- [ ] DDL 随天数递减（测试跨天）
- [ ] 主线任务排在支线前面

---

## 九、未来扩展点

本系统仅包含 UI 展示层，以下功能需要后续实现：

| 功能 | 说明 |
|------|------|
| 任务发放/触发逻辑 | 谁发任务、什么条件触发（可复用 TutorialMarkerBridge 架构） |
| 进度自动检测 | 如"卖出100件商品"的计数监听（需订阅 CustomerEventBus） |
| DDL 到期自动失败 | DayLoopManager.OnDayChanged 中检查并调用 QuestLog.FailQuest() |
| 奖励自动发放 | 任务完成时调用 PopLifeLuaFunctions.GiveReward() |
| 失败惩罚执行 | 任务失败时扣除资源 |
| 数据持久化 | activationDayMap 通过 ES3 存档/读档 |
| 任务通知动画 | 新任务激活/完成时的 toast 通知 |
