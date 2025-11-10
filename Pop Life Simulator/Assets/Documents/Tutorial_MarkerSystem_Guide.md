# 教程标记系统集成指南
# Tutorial Marker System Integration Guide

## 概述 (Overview)

本指南介绍基于事件驱动的教程对话标记系统。

### 核心概念
- **Midori只有立绘**，不会出现在场景中
- **标记触发**：在代码中埋入`TutorialMarker`触发教程对话
- **事件驱动**：无需Update()轮询，性能更好
- **解耦设计**：触发点与对话系统完全分离

---

## 系统架构

```
代码埋点                 标记系统                对话系统
┌──────────────┐       ┌──────────────┐       ┌──────────────┐
│GameStateMgr  │──标记→│TutorialEvent │──触发→│TutorialDialo │
│DayLoopMgr    │       │Bus           │       │gueManager    │
│Construction  │       │              │       │              │
└──────────────┘       └──────────────┘       └──────────────┘
        │                      │                      │
     埋入标记              分发事件              触发对话
```

---

## 快速开始

### 步骤1：创建Midori的DialogueActorAsset

**Unity操作**：
1. Project窗口右键 → `Create → ParadoxNotion → NodeCanvas → Dialogue Actor`
2. 命名：`Midori`
3. 配置：
   - **Name**: `Midori`
   - **Portrait**: 拖入Midori立绘（Texture2D或Sprite）
   - **Dialogue Color**: 自定义颜色
4. 保存到：`Assets/Resources/DialogueActors/Midori.asset`

### 步骤2：配置DialogueTree的Actor参数

**在Unity编辑器中配置（推荐）**：
1. 打开D001.asset（双击DialogueTree资产）
2. 在Graph Inspector中找到 **Actor Parameters** 区域
3. 点击 `+` 添加参数：
   - **Key Name**: `Midori`
   - **Actor**: 拖入 `Resources/DialogueActors/Midori.asset`
4. 对D002-D007重复此操作

**或通过代码动态设置**：
```csharp
// 在DialogueEvent.ForceTrigger()中已预留代码位置
// 取消注释即可启用运行时Actor设置
```

### 步骤3：在场景中添加Manager

#### 3.1 添加GameStateManager
1. LatestUpdate场景中创建空GameObject：`GameStateManager`
2. 添加组件：`GameStateManager.cs`

#### 3.2 添加TutorialDialogueManager
1. 创建空GameObject：`TutorialDialogueManager`
2. 添加组件：`TutorialDialogueManager.cs`
3. Inspector配置：
   - **Enable Tutorial**: ✅ 勾选

---

## 标记系统详解

### 已定义的标记 (TutorialMarker Enum)

| 标记 | 说明 | 触发位置 |
|------|------|---------|
| `GameStarted` | 游戏启动 | GameStateManager.Start() |
| `FirstBuildPhaseEntered` | 首次进入建造模式 | GameStateManager.OnBuildPhaseStarted() |
| `FirstShelfPlaced` | 首次放置货架 | GameStateManager.NotifyShelfPlaced() |
| `TwoShelvesPlaced` | 放置2个货架 | GameStateManager.NotifyShelfPlaced() |
| `StoreOpened` | 商店开张 | GameStateManager.OnStoreOpenedEvent() |
| `FirstCustomerCheckedOut` | 首位顾客结账 | GameStateManager.NotifyCustomerServed() |
| `FirstFameEarned` | 首次获得声望 | GameStateManager.NotifyFameEarned() |
| `FirstDayCompleted` | 首日结束 | （待实现） |
| `PlacedFacility` | 放置设施 | （待实现） |
| `FirstBuildingUpgraded` | 首次升级建筑 | （待实现） |

### 教程对话映射表

| 对话 | 触发标记 | 奖励 | 说明 |
|------|---------|------|------|
| **D001** | GameStarted | 无 | Midori介绍商店 |
| **D002** | FirstBuildPhaseEntered | Blueprint:B001, B002 | 建造教程 |
| **D003** | TwoShelvesPlaced | Customer:C001-C003 | 开店引导 |
| **D004** | StoreOpened | Fame:500 | 首位顾客 |
| **D005** | FirstFameEarned | Blueprint:R004, R005 | 声望系统 |

---

## 如何埋入标记

### 方法1：直接触发（推荐）

在需要触发教程的位置，直接调用：

```csharp
using PopLife.Manager;

// 在任意代码位置，一行代码触发教程
TutorialEventBus.RaiseMarker(TutorialMarker.FirstShelfPlaced);
```

### 方法2：通过GameStateManager

调用GameStateManager的通知方法（已埋好标记）：

```csharp
// 在ConstructionManager放置建筑时
if (buildingInstance is ShelfInstance)
{
    GameStateManager.Instance?.NotifyShelfPlaced();
    // 会自动触发 FirstShelfPlaced 和 TwoShelvesPlaced 标记
}
```

### 示例：在不同脚本中埋入标记

#### 示例1：ConstructionManager（放置建筑）

```csharp
// Assets/Scripts/Runtime/ConstructionManager.cs
using PopLife.Manager;

private void PlaceBuilding()
{
    // 原有放置逻辑...

    // 通知GameStateManager（已包含标记）
    if (buildingInstance is ShelfInstance)
    {
        GameStateManager.Instance?.NotifyShelfPlaced();
    }
    else if (buildingInstance is FacilityInstance)
    {
        // 直接触发标记
        TutorialEventBus.RaiseMarker(TutorialMarker.PlacedFacility);
    }
}

public void UpgradeBuilding()
{
    // 升级逻辑...

    TutorialEventBus.RaiseMarker(TutorialMarker.FirstBuildingUpgraded);
}
```

#### 示例2：DayLoopManager（每日结算）

```csharp
// Assets/Scripts/Manager/DayLoopManager.cs
using PopLife.Manager;

private void PerformDailySettlement()
{
    // 结算逻辑...

    // 通知声望获得（已包含标记）
    int fameEarned = CalculateFameReward(dailySales, customerCount);
    GameStateManager.Instance?.NotifyFameEarned(fameEarned);

    // 首日完成标记
    if (currentDay == 1)
    {
        TutorialEventBus.RaiseMarker(TutorialMarker.FirstDayCompleted);
    }
}
```

#### 示例3：CustomerInteraction（顾客结账）

```csharp
// Assets/Scripts/Customers/Runtime/CustomerInteraction.cs
using PopLife.Manager;

public bool TryCheckout()
{
    // 结账逻辑...

    // 通知顾客服务（已包含标记）
    GameStateManager.Instance?.NotifyCustomerServed();

    return true;
}
```

---

## 高级功能

### 1. 防止重复触发

标记系统默认防止重复触发：

```csharp
// 第一次触发 - 成功
TutorialEventBus.RaiseMarker(TutorialMarker.FirstShelfPlaced);

// 第二次触发 - 被忽略（不会重复触发教程）
TutorialEventBus.RaiseMarker(TutorialMarker.FirstShelfPlaced);
```

如需允许重复触发：

```csharp
TutorialEventBus.RaiseMarker(TutorialMarker.Custom01, allowRetrigger: true);
```

### 2. 检查标记状态

```csharp
// 检查某个标记是否已触发
if (TutorialEventBus.IsMarkerTriggered(TutorialMarker.FirstShelfPlaced))
{
    Debug.Log("玩家已经放置过货架");
}
```

### 3. 重置标记（测试用）

```csharp
// 重置单个标记
TutorialEventBus.ResetMarker(TutorialMarker.FirstShelfPlaced);

// 重置所有标记
TutorialEventBus.ResetAllMarkers();
```

### 4. 添加自定义标记

编辑 `TutorialMarker.cs`：

```csharp
public enum TutorialMarker
{
    // ... existing markers

    // 添加你的自定义标记
    MyCustomEvent,
    AnotherCustomEvent,
}
```

在TutorialDialogueManager中注册对话：

```csharp
private void RegisterTutorialDialogues()
{
    // ... existing dialogues

    // 添加自定义对话
    AddEvent("D008", "Custom Tutorial",
        TutorialMarker.MyCustomEvent,
        new List<string> { "Money:1000" });
}
```

---

## 测试方法

### 方法1：正常游戏流程
1. 运行LatestUpdate场景
2. 游戏启动后，D001应该自动弹出
3. 进入建造模式 → D002触发
4. 放置2个货架 → D003触发
5. 商店开张 → D004触发

### 方法2：手动触发（调试）
在TutorialDialogueManager上：
- 右键 → `Trigger D001` - 手动触发D001对话
- 右键 → `Reset Tutorial` - 重置所有教程进度

### 方法3：代码触发
```csharp
// 直接触发特定标记
TutorialEventBus.RaiseMarker(TutorialMarker.FirstShelfPlaced);

// 或手动触发对话
FindObjectOfType<TutorialDialogueManager>().TriggerDialogueByCode("D001");
```

### 方法4：使用GameStateManager重置
在GameStateManager上：
- 右键 → `Reset Tutorial States` - 重置游戏状态

---

## 故障排除

### 问题1：对话没有自动弹出

**检查**：
- TutorialDialogueManager的 `Enable Tutorial` 是否勾选？
- GameStateManager是否在场景中？
- Console是否有 `[TutorialEventBus] Marker raised: XXX` 日志？
- DialogueTree资产是否在 `Resources/DialogueTreeControllers/` ？

**解决**：
- 检查标记是否正确触发（查看Console日志）
- 使用右键菜单手动触发测试

### 问题2：对话显示了但没有立绘

**检查**：
- DialogueActorAsset是否创建并配置了Portrait？
- DialogueTree的Actor Parameters是否添加了"Midori"？
- Midori.asset的Portrait字段是否填写？

**解决**：
1. 确认 `Resources/DialogueActors/Midori.asset` 存在
2. 打开D001.asset检查Actor Parameters
3. 如使用代码动态设置，检查Resources.Load路径

### 问题3：标记触发了但对话没有触发

**检查Console日志**：
```
[TutorialEventBus] Marker raised: FirstShelfPlaced  ✅ 标记成功触发
[Tutorial] Marker triggered: FirstShelfPlaced       ✅ TutorialDialogueManager收到
[DialogueEvent] Triggering dialogue: D002           ✅ 对话开始触发
```

如果只有第一行日志：
- 检查TutorialDialogueManager是否订阅了事件
- 检查对话是否已经触发过（不会重复触发）

### 问题4：GameStateManager的通知方法没有被调用

**需要在现有脚本中添加调用**：
- ConstructionManager → `GameStateManager.Instance.NotifyShelfPlaced()`
- CustomerInteraction → `GameStateManager.Instance.NotifyCustomerServed()`
- DayLoopManager → `GameStateManager.Instance.NotifyFameEarned(amount)`

---

## 性能优势

### 对比旧系统（轮询）

❌ **旧方式**：Update()每帧检查条件
```csharp
void Update() {
    for (int i = 0; i < dialogues.Count; i++) {
        if (dialogues[i].CheckCondition()) {  // 每帧检查
            dialogues[i].Trigger();
        }
    }
}
```

✅ **新方式**：事件驱动，仅在触发时执行
```csharp
// 在需要时触发一次
TutorialEventBus.RaiseMarker(TutorialMarker.FirstShelfPlaced);

// TutorialDialogueManager自动响应，无需轮询
```

### 性能提升
- **CPU开销**：从每帧O(n)降至O(1)（仅触发时）
- **内存**：无需存储lambda表达式和UI预制体引用
- **可维护性**：触发点一目了然，易于调试

---

## 扩展示例

### 示例1：条件组合触发

需要满足多个条件才触发对话：

```csharp
private void OnMarkerTriggered(TutorialMarker marker)
{
    // 特殊对话：需要商店开张 AND 放置2个货架
    if (marker == TutorialMarker.StoreOpened)
    {
        if (TutorialEventBus.IsMarkerTriggered(TutorialMarker.TwoShelvesPlaced))
        {
            // 触发特殊教程
            var specialDialogue = new DialogueEvent(...);
            specialDialogue.ForceTrigger();
        }
    }
}
```

### 示例2：延迟触发

标记触发后延迟几秒再显示对话：

```csharp
private void OnMarkerTriggered(TutorialMarker marker)
{
    StartCoroutine(TriggerDialogueDelayed(marker, 2f)); // 延迟2秒
}

private IEnumerator TriggerDialogueDelayed(TutorialMarker marker, float delay)
{
    yield return new WaitForSeconds(delay);

    if (markerDialogueMap.ContainsKey(marker))
    {
        foreach (var dialogue in markerDialogueMap[marker])
        {
            dialogue.ForceTrigger();
        }
    }
}
```

### 示例3：对话序列（连续播放）

一个标记触发多个对话，按顺序播放：

```csharp
// 注册时添加多个对话到同一标记
AddEvent("D001", "Part 1", TutorialMarker.GameStarted, ...);
AddEvent("D001_Part2", "Part 2", TutorialMarker.GameStarted, ...);

// 播放序列
private IEnumerator PlayDialogueSequence(List<DialogueEvent> dialogues)
{
    foreach (var dialogue in dialogues)
    {
        dialogue.ForceTrigger();
        yield return new WaitUntil(() => dialogue.IsCompleted);
        yield return new WaitForSeconds(0.5f); // 对话间隔
    }
}
```

---

## 文件清单

### 新增文件
- ✅ `Scripts/Manager/TutorialMarker.cs` - 标记枚举定义
- ✅ `Scripts/Manager/TutorialEventBus.cs` - 事件总线
- ✅ `Scripts/Manager/GameStateManager.cs` - 游戏状态管理（已添加标记）
- ✅ `Scripts/Dialogue/TutorialDialogueManager.cs` - 教程对话管理（标记版）
- ✅ `Scripts/Dialogue/DialogueEvent.cs` - 对话事件（简化版）

### 需要创建的资产
- ⚠️ `Resources/DialogueActors/Midori.asset` - Midori立绘配置（需手动创建）
- ⚠️ D001-D007的Actor Parameters配置（需在Unity编辑器中操作）

### 已删除的文件
- ❌ `NPCManager.cs` - 不需要
- ❌ `NPCArchetype.cs` - 不需要
- ❌ 原DialogueManager.cs中的UI按钮逻辑 - 已移除

---

## 下一步

### 立即完成
1. ✅ 创建Midori的DialogueActorAsset
2. ✅ 配置D001-D007的Actor Parameters
3. ✅ 在场景中添加GameStateManager和TutorialDialogueManager
4. ✅ 测试D001是否自动弹出

### 未来扩展
1. 在其他脚本中添加更多标记埋点
2. 完善D002-D007的对话内容
3. 实现Blueprint/Customer解锁系统
4. 添加对话期间暂停游戏功能

---

## 参考资料

- NodeCanvas文档：`ThirdParty/ParadoxNotion/NodeCanvas/`
- 对话系统分析：`Documents/DialogueSystem_Analysis.md`
- 设计文档：`Documents/PopLifeDesignDoc.md`

---

## 总结

✅ **标记系统优势**：
- 事件驱动，性能优秀
- 解耦设计，易于维护
- 一行代码触发教程
- 防止重复触发
- 完整的调试支持

🎯 **使用方式**：
```csharp
// 在任意需要触发教程的地方
using PopLife.Manager;
TutorialEventBus.RaiseMarker(TutorialMarker.YourMarkerHere);
```

祝你顺利集成教程系统！🎮
