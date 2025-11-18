# 教程序列配置指南
# Tutorial Sequence Configuration Guide

本文档说明如何配置教程序列，让 D001 在游戏启动时显示，D001 结束后自动触发 D002，D002 结束后触发指引。

## 系统架构概览

```
游戏启动
   ↓
GameStateManager 触发 TutorialMarker.GameStarted
   ↓
TutorialContentDispatcher 查找映射
   ↓
触发 D001 (Midori 欢迎对话)
   ↓
D001 完成 → NarrativeManager 触发 TutorialMarker.Custom01
   ↓
触发 D002 (Midori 第二段对话)
   ↓
D002 完成 → NarrativeManager 触发 TutorialMarker.Custom02
   ↓
触发 GUIDE_BUILD_FIRST_SHELF (建造货架指引)
```

## 步骤 1：场景配置

### 1.1 添加必要的 Manager GameObject

在你的主场景（MainScene.unity）中，确保有以下 GameObject：

```
场景层级结构:
├─ GameStateManager
│  └─ GameStateManager.cs (已存在，会在 Start 时触发 GameStarted)
│
├─ NarrativeManager
│  └─ NarrativeManager.cs (已更新，支持完成后触发标记)
│
├─ GuidanceManager
│  └─ GuidanceManager.cs (已更新，支持通过 ID 创建指引)
│
├─ TutorialContentDispatcher
│  └─ TutorialContentDispatcher.cs (内容分发器)
│
└─ DayLoopManager
   └─ DayLoopManager.cs (游戏循环管理)
```

### 1.2 创建 GameObject 的步骤

1. **创建 NarrativeManager**（如果不存在）
   - 在 Hierarchy 中右键 → Create Empty
   - 重命名为 "NarrativeManager"
   - Add Component → 搜索 "NarrativeManager" → 添加脚本

2. **创建 GuidanceManager**（如果不存在）
   - 在 Hierarchy 中右键 → Create Empty
   - 重命名为 "GuidanceManager"
   - Add Component → 搜索 "GuidanceManager" → 添加脚本

3. **创建 TutorialContentDispatcher**（如果不存在）
   - 在 Hierarchy 中右键 → Create Empty
   - 重命名为 "TutorialContentDispatcher"
   - Add Component → 搜索 "TutorialContentDispatcher" → 添加脚本

## 步骤 2：配置 TutorialContentDispatcher

在 Unity Inspector 中配置 TutorialContentDispatcher：

### 2.1 基础设置
```
TutorialContentDispatcher (Inspector)
├─ Enable Dispatcher: ✓ (勾选)
├─ Debug Mode: ✓ (调试时勾选，可以看到详细日志)
└─ Auto Use Default Mappings: ✓ (使用默认映射)
```

### 2.2 配置内容映射 (Content Mappings)

点击 "Content Mappings" 旁边的箭头展开，设置 Size = 3，然后配置：

**映射 1：游戏启动 → D001**
```
Element 0
├─ Marker: GameStarted
├─ Content Type: Narrative
├─ Content ID: D001
├─ Priority: 0
└─ Description: Midori welcomes player (D001)
```

**映射 2：D001 完成 → D002**
```
Element 1
├─ Marker: Custom01
├─ Content Type: Narrative
├─ Content ID: D002
├─ Priority: 0
└─ Description: Midori continues tutorial (D002)
```

**映射 3：D002 完成 → 指引**
```
Element 2
├─ Marker: Custom02
├─ Content Type: Guidance
├─ Content ID: GUIDE_BUILD_FIRST_SHELF
├─ Priority: 0
└─ Description: Guide player to build first shelf
```

## 步骤 3：配置 NarrativeManager

在 NarrativeManager 的 Inspector 中：

```
NarrativeManager (Inspector)
├─ Enable Narratives: ✓ (勾选)
├─ Default Segment Delay: 0.5
└─ Conversation Panel: [自动创建或拖入 FanConversationPanel 预制体]
```

## 步骤 4：配置 GuidanceManager

在 GuidanceManager 的 Inspector 中：

```
GuidanceManager (Inspector)
├─ Enable Guidance: ✓ (勾选)
├─ Pause Game During Guidance: ✓ (可选，指引时暂停游戏)
└─ Debug Mode: ✓ (调试时勾选)
```

## 步骤 5：确认资源文件

确保以下资源文件存在：

1. **Narrative ScriptableObjects**
   - `Assets/Resources/NarrativeData/D001.asset` (已填充内容)
   - `Assets/Resources/NarrativeData/D002.asset` (已填充内容)

2. **检查 D001 和 D002 的 narrativeID**
   - D001.asset 的 narrativeID 必须是 "D001"
   - D002.asset 的 narrativeID 必须是 "D002"

## 步骤 6：测试流程

### 6.1 运行测试

1. 保存场景
2. 点击 Unity 编辑器的 Play 按钮
3. 观察 Console 窗口的日志

### 6.2 预期行为

1. **游戏启动时**：
   - Console 显示: `[GameState] Game started, triggering tutorial`
   - Console 显示: `[TutorialEventBus] Marker raised: GameStarted`
   - D001 对话显示（Midori 欢迎）

2. **D001 完成后**：
   - 点击 Finish 按钮或完成所有对话段
   - Console 显示: `[NarrativeManager] D001 completed, triggering Custom01 for D002`
   - D002 对话自动开始

3. **D002 完成后**：
   - 点击 Finish 按钮或完成所有对话段
   - Console 显示: `[NarrativeManager] D002 completed, triggering Custom02 for guidance`
   - 指引遮罩出现，显示建造货架的步骤

### 6.3 调试日志

如果启用了 Debug Mode，你会看到详细的流程日志：

```
[TutorialEventBus] Marker raised: GameStarted
[TutorialDispatcher] Processing marker: GameStarted -> Narrative
[NarrativeManager] Started narrative: D001
[NarrativeManager] Ended narrative: D001
[TutorialEventBus] Marker raised: Custom01
[TutorialDispatcher] Processing marker: Custom01 -> Narrative
[NarrativeManager] Started narrative: D002
[NarrativeManager] Ended narrative: D002
[TutorialEventBus] Marker raised: Custom02
[TutorialDispatcher] Processing marker: Custom02 -> Guidance
[GuidanceManager] Started guidance: GUIDE_BUILD_FIRST_SHELF
```

## 故障排除

### 问题 1：D001 没有在游戏启动时显示

**可能原因**：
- GameStateManager 不在场景中
- TutorialContentDispatcher 没有正确配置映射
- NarrativeManager 没有启用

**解决方法**：
1. 确认 GameStateManager GameObject 存在并有脚本
2. 检查 TutorialContentDispatcher 的 Content Mappings
3. 确认 NarrativeManager 的 Enable Narratives = true

### 问题 2：D001 结束后 D002 没有自动开始

**可能原因**：
- NarrativeManager.cs 没有更新（缺少 TriggerFollowUpContent 方法）
- Custom01 标记没有映射到 D002

**解决方法**：
1. 确认 NarrativeManager.cs 包含最新的代码更新
2. 检查 TutorialContentDispatcher 中 Custom01 → D002 的映射

### 问题 3：D002 结束后指引没有出现

**可能原因**：
- GuidanceManager 没有启用
- GUIDE_BUILD_FIRST_SHELF 序列创建失败
- Custom02 标记没有映射

**解决方法**：
1. 确认 GuidanceManager 的 Enable Guidance = true
2. 检查 Console 是否有错误信息
3. 验证 GuidanceSequenceFactory.cs 文件存在

### 问题 4：找不到 Narrative 资源

**错误信息**：`[NarrativeManager] Narrative not found: D001`

**解决方法**：
1. 确认文件路径：`Assets/Resources/NarrativeData/D001.asset`
2. 在 Inspector 中打开 D001.asset，确认 narrativeID = "D001"
3. 确保文件在 Resources 文件夹下（不是其他位置）

## 扩展配置

### 添加更多序列链接

如果你想添加更多的教程序列，可以：

1. **在 NarrativeManager.cs 中扩展 TriggerFollowUpContent 方法**：
```csharp
case "D003":
    TutorialEventBus.RaiseMarker(TutorialMarker.Custom03);
    break;
```

2. **在 TutorialContentDispatcher 中添加新映射**：
```
Element 3
├─ Marker: Custom03
├─ Content Type: Narrative/Guidance
├─ Content ID: [下一个内容的ID]
└─ Description: [描述]
```

3. **在 GuidanceSequenceFactory 中添加新的指引序列**：
```csharp
case "GUIDE_YOUR_NEW_GUIDE":
    return CreateYourNewGuidance();
```

### 使用条件触发

你也可以基于游戏状态条件触发不同的内容：

```csharp
// 在 NarrativeManager.TriggerFollowUpContent 中
case "D001":
    if (GameStateManager.Instance.totalShelvesPlaced > 0)
    {
        // 如果已经放置了货架，跳过建造教程
        TutorialEventBus.RaiseMarker(TutorialMarker.StoreOpened);
    }
    else
    {
        // 否则继续教程序列
        TutorialEventBus.RaiseMarker(TutorialMarker.Custom01);
    }
    break;
```

## 最佳实践

1. **使用有意义的 ID**：
   - Narrative: D001, D002, NARR_WELCOME
   - Guidance: GUIDE_BUILD_SHELF, GUIDE_OPEN_STORE

2. **保持映射简单**：
   - 每个标记映射到一个内容
   - 使用 Custom 标记连接序列

3. **添加日志**：
   - 在开发时启用 Debug Mode
   - 在关键点添加 Debug.Log

4. **测试每个环节**：
   - 单独测试每个 Narrative
   - 单独测试每个 Guidance
   - 最后测试完整序列

## 总结

通过以上配置，你的教程系统将按以下流程运行：

1. 游戏启动 → 自动显示 D001（Midori 欢迎）
2. D001 完成 → 自动触发 D002（继续教程）
3. D002 完成 → 自动触发建造货架指引
4. 玩家完成指引 → 继续游戏

这个系统是完全可扩展的，你可以继续添加更多的 Narrative 和 Guidance 序列，通过 TutorialMarker 和 TutorialContentDispatcher 的映射来控制流程。