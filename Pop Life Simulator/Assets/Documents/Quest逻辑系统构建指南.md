# Quest 逻辑系统构建指南

## 概述
Quest 逻辑系统处理任务的自动进度追踪、DDL 过期检测、奖励自动发放和数据持久化。与已有的 Quest UI 系统配合使用。

系统架构：
```
游戏事件（CustomerEventBus / DayLoopManager / ResourceManager / ConstructionManager）
                    ↓
          QuestProgressTracker（计数器更新）
                    ↓
          QuestLog.SetQuestEntryState（标记 Entry 完成）
                    ↓
          QuestDataService（ViewModel 刷新 → UI 更新）
                    ↓
          QuestLogicManager（所有 Entry 完成 → CompleteQuest）
                    ↓
    ┌───────────────┼───────────────┐
    ↓               ↓               ↓
QuestReward    QuestLog        QuestNotification
Distributor    SetState        Toast
(发放奖励)    ("success")    (弹出通知)
```

---

## 一、文件清单

### 新建文件

| 文件 | 职责 |
|------|------|
| `Scripts/Quest/QuestCondition.cs` | 条件数据模型和枚举定义 |
| `Scripts/Quest/QuestLogicManager.cs` | 核心管理器（单例） |
| `Scripts/Quest/QuestProgressTracker.cs` | 进度追踪器（事件订阅 + 计数器） |
| `Scripts/Quest/QuestRewardDistributor.cs` | 奖励发放（静态工具类） |
| `Scripts/UI/Quest/QuestNotificationToast.cs` | Toast 弹出通知 |

### 修改的文件

| 文件 | 修改 |
|------|------|
| `Scripts/Data/QuestDefinition.cs` | 添加 `QuestCondition[] conditions` 字段 |
| `Scripts/UI/Quest/QuestDataService.cs` | 添加 `GetDefinition()`, `GetAllDefinitions()`, 激活日持久化 API |
| `Scripts/Data/AudioKeys.cs` | 添加 `QUEST_NEW`, `QUEST_FAILED` 常量 |

---

## 二、Unity Editor 配置步骤

### 步骤 1：添加 QuestLogicManager 到场景

1. 在场景 Hierarchy 中找到管理器所在的 GameObject（或创建新的空物体 `[QuestLogicManager]`）
2. 添加 `QuestLogicManager` 组件
3. 可选勾选 `Debug Mode` 查看日志

```
Hierarchy:
├── Managers
│   ├── DayLoopManager
│   ├── ResourceManager
│   ├── [QuestLogicManager]     ← 新增
│   └── ...
```

### 步骤 2：配置 QuestDefinition SO 的条件

1. 选中已有的 QuestDefinition SO（或创建新的）
2. 在 Inspector 中找到 **完成条件** 区域
3. 添加条件数组元素，**每个条件对应 Dialogue Database 中的一个 Quest Entry**

**重要**：`conditions[0]` 对应 Entry 1, `conditions[1]` 对应 Entry 2, 以此类推。

#### 条件类型说明

| 类型 | 说明 | 典型 Scope |
|------|------|-----------|
| Manual | 不自动追踪，由对话/TutorialMarker 标记 | 无 |
| SellItems | 卖出 N 件商品 | Cumulative |
| EarnMoney | 赚取 N 金钱 | Cumulative/Daily |
| EarnFame | 赚取 N 声望 | Cumulative |
| ServeCustomers | 服务 N 位顾客 | Cumulative |
| PlaceBuildings | 放置 N 个建筑 | Current |
| ReachStoreAppeal | 店铺 Appeal 达到 N | Current |
| SurviveDays | 经营 N 天 | Cumulative |
| ReachMoneyThreshold | 金钱达到 N | Current |

#### CountScope 说明

| Scope | 行为 |
|-------|------|
| Cumulative | 从任务激活起持续累计 |
| Daily | 每日重置，当天完成即可 |
| Current | 基于当前快照值（如当前金钱、当前 Appeal） |

#### 示例配置

**任务 "Stock Up Your Store"**（卖出50件 + 服务20位顾客）：

```
QuestDefinition:
  Quest Name: "StockUpYourStore"
  Deadline Days: 5
  Rewards: [{ Money, 500 }, { Fame, 10 }]
  Conditions:
    [0] SellItems, Cumulative, target=50, useFilter=false
    [1] ServeCustomers, Cumulative, target=20

Dialogue Database:
  Quest "StockUpYourStore":
    Entry 1: "Sell 50 items"
    Entry 2: "Serve 20 customers"
```

**任务 "Build Empire"**（放置5个货架 + Appeal达200）：

```
QuestDefinition:
  Quest Name: "BuildEmpire"
  Conditions:
    [0] PlaceBuildings, Current, target=5, useFilter=false
    [1] ReachStoreAppeal, Current, target=200
```

**混合任务**（手动步骤 + 自动步骤）：

```
QuestDefinition:
  Quest Name: "TutorialQuest"
  Conditions:
    [0] Manual         ← 由 TutorialMarkerBridge 标记完成
    [1] SellItems, Cumulative, target=3
    [2] Manual         ← 由对话完成
```

### 步骤 3：搭建 QuestNotificationToast

在主 UI Canvas 下创建 Toast UI：

```
QuestNotificationToast                    ← 挂载 QuestNotificationToast.cs
├── ToastRoot                            ← toastRoot (初始隐藏)
│   ├── CanvasGroup                      ← canvasGroup (淡入淡出用)
│   ├── Background (Image)
│   ├── AccentBar (Image, 4px 宽色条)    ← accentBar
│   ├── QuestIcon (Image, 32x32)         ← questIconImage
│   ├── TitleText (TMP, 粗体)            ← titleText ("NEW QUEST")
│   └── MessageText (TMP)               ← messageText (任务名)
```

**定位建议**：
- Anchor: Top-Center
- 在屏幕上方弹出

**Inspector 配置**：

| 字段 | 拖入 |
|------|------|
| Toast Root | ToastRoot GameObject |
| Canvas Group | ToastRoot 上的 CanvasGroup |
| Title Text | TitleText (TMP) |
| Message Text | MessageText (TMP) |
| Quest Icon Image | QuestIcon (Image) |
| Accent Bar | AccentBar (Image) |

---

## 三、任务生命周期

```
1. 任务创建
   - QuestDefinition SO 配置完成条件和奖励
   - Dialogue Database 中创建对应 Quest（Entry 数量需一致）

2. 任务激活
   方式 A: TutorialMarkerBridge → QuestLog.SetQuestState("active")
   方式 B: 对话中调用 SetQuestState("QuestName", "active")
   方式 C: 代码调用 QuestLogicManager.Instance.ActivateQuest("QuestName")
   → QuestDataService 记录激活日
   → QuestLogicManager 检测到新任务 → 开始追踪
   → Toast 显示 "NEW QUEST"

3. 进度追踪
   - 自动条件：QuestProgressTracker 订阅事件，更新计数器
   - 手动条件：TutorialMarkerBridge 或对话直接设置 Entry 状态
   - 每次 Entry 完成 → UI 自动刷新（通过 QuestDataService 事件链）

4. 任务完成
   - 所有 Entry 标记为 success → QuestLogicManager 自动完成
   - 发放 QuestDefinition.rewards 中配置的奖励
   - Toast 显示 "QUEST COMPLETE"
   - 音效 "Quest_Complete"

5. 任务失败
   - DDL 到期 → QuestLogicManager 在 OnDayChanged 中自动检查
   - Toast 显示 "QUEST FAILED"
   - 音效 "Quest_Failed"
```

---

## 四、与现有系统的集成

### TutorialMarkerBridge（不变）

现有的 TutorialMarkerBridge 仍然可以正常工作：
- TutorialMarker → 设置 QuestLog Entry 状态（Manual 类型条件）
- QuestLogicManager 会自动检测 Entry 状态变化并处理完成逻辑

### PopLifeLuaFunctions（不变）

对话中仍可使用：
```lua
SetQuestState("QuestName", "active")         -- 激活任务
SetQuestEntryState("QuestName", 1, "success") -- 完成手动步骤
```

### 数据持久化

- ES3 文件：`QuestProgress.es3`
- 保存内容：计数器状态、激活日映射、已发放奖励列表
- 自动保存时机：Entry 完成时、每日新天时、退出时

---

## 五、调试

### Debug Mode

在 QuestLogicManager Inspector 中勾选 `Debug Mode`，控制台会输出：
- 任务追踪开始/停止
- 条目计数更新
- 任务完成/失败

### Lua Console 测试

```lua
-- 激活任务
SetQuestState("TestQuest", "active")

-- 手动完成一个 Entry（用于 Manual 类型条件）
SetQuestEntryState("TestQuest", 1, "success")

-- 查看状态
print(CurrentQuestState("TestQuest"))
print(CurrentQuestEntryState("TestQuest", 1))
```

### C# 代码测试

```csharp
// 激活任务
QuestLogicManager.Instance.ActivateQuest("TestQuest");

// 监听事件
QuestLogicManager.OnQuestCompleted += (name) => Debug.Log($"Quest completed: {name}");
QuestLogicManager.OnQuestFailed += (name) => Debug.Log($"Quest failed: {name}");
```

---

## 六、验证清单

- [ ] QuestLogicManager 已添加到场景中
- [ ] QuestDefinition SO 配置了 conditions 数组
- [ ] conditions 数量 == Dialogue Database 中 Quest 的 Entry 数量
- [ ] QuestNotificationToast 已搭建并引用配置完毕
- [ ] 激活任务后 QuestProgressTracker 开始追踪（Debug 日志确认）
- [ ] 卖出商品时 SellItems 条件计数增加
- [ ] 顾客结账时 ServeCustomers 条件计数增加
- [ ] 所有条件满足后任务自动完成
- [ ] 完成后奖励正确发放（金钱/声望/蓝图/顾客）
- [ ] DDL 到期后任务自动失败
- [ ] Toast 通知正确显示（新任务/完成/失败）
- [ ] 进度中途退出并重启后计数器正确恢复
- [ ] Manual 类型条件可通过 TutorialMarkerBridge 手动标记完成
