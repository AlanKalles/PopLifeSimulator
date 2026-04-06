# Quest 逻辑系统构建指南

## 概述
Quest 逻辑系统处理任务的自动进度追踪、DDL 过期检测、奖励自动发放和数据持久化。与已有的 Quest UI 系统配合使用。

**QuestDefinition SO 是任务系统的唯一数据源**，无需在 Dialogue Database 中配置任何内容。

系统架构：
```
游戏事件（CustomerEventBus / DayLoopManager / ResourceManager / ConstructionManager）
                    ↓
          QuestProgressTracker（计数器更新）
                    ↓
          QuestStateStore（标记 Entry 完成，override delegate 拦截所有状态读写）
                    ↓
          QuestDataService（ViewModel 刷新 → UI 更新）
                    ↓
          QuestLogicManager（所有 Entry 完成 → CompleteQuest）
                    ↓
    ┌───────────────┼───────────────┐
    ↓               ↓               ↓
QuestReward    QuestState      QuestNotification
Distributor    Store           Toast
(发放奖励)    ("success")    (弹出通知)
```

---

## 一、文件清单

### 核心文件

| 文件 | 职责 |
|------|------|
| `Scripts/Data/QuestDefinition.cs` | 任务唯一数据源（元数据 + 文本 + 条件 + 奖励） |
| `Scripts/Quest/QuestStateStore.cs` | 状态存储后端（替代 Dialogue System QuestLog） |
| `Scripts/Quest/QuestCondition.cs` | 条件数据模型和枚举定义 |
| `Scripts/Quest/QuestLogicManager.cs` | 核心管理器（单例） |
| `Scripts/Quest/QuestProgressTracker.cs` | 进度追踪器（事件订阅 + 计数器） |
| `Scripts/Quest/QuestRewardDistributor.cs` | 奖励发放（静态工具类） |
| `Scripts/UI/Quest/QuestDataService.cs` | ViewModel 桥接服务（供 UI 消费） |
| `Scripts/UI/Quest/QuestNotificationToast.cs` | Toast 弹出通知 |

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

### 步骤 2：配置 QuestDefinition SO

1. 选中已有的 QuestDefinition SO（或通过 `Create > PopLife > Quest > QuestDefinition` 创建新的）
2. 填写 **显示文本** 区域：`title`（任务标题）、`description`（任务描述）、`successDescription`（完成描述）
3. 在 **完成条件** 区域添加条件数组
4. 在 **显示文本** 区域的 `entryTexts` 数组中填写每个条目的文本

**重要**：`conditions[i]` 对应 `entryTexts[i]`，两个数组长度必须一致（OnValidate 会自动校验）。

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
QuestDefinition SO:
  Quest Name: "StockUpYourStore"
  Title: "Stock Up Your Store"
  Description: "Build up your inventory and serve customers."
  Success Description: "Great job! Your store is thriving!"
  Deadline Days: 5
  Rewards: [{ Money, 500 }, { Fame, 10 }]
  Conditions:
    [0] SellItems, Cumulative, target=50, useFilter=false
    [1] ServeCustomers, Cumulative, target=20
  Entry Texts:
    [0] "Sell 50 items"
    [1] "Serve 20 customers"
```

**任务 "Build Empire"**（放置5个货架 + Appeal达200）：

```
QuestDefinition SO:
  Quest Name: "BuildEmpire"
  Title: "Build Your Empire"
  Description: "Expand your store and boost its appeal."
  Conditions:
    [0] PlaceBuildings, Current, target=5, useFilter=false
    [1] ReachStoreAppeal, Current, target=200
  Entry Texts:
    [0] "Place 5 buildings"
    [1] "Reach 200 Store Appeal"
```

**混合任务**（手动步骤 + 自动步骤）：

```
QuestDefinition SO:
  Quest Name: "TutorialQuest"
  Title: "Getting Started"
  Description: "Learn the basics of running your store."
  Conditions:
    [0] Manual         ← 由 TutorialMarkerBridge 标记完成
    [1] SellItems, Cumulative, target=3
    [2] Manual         ← 由对话完成
  Entry Texts:
    [0] "Talk to the guide"
    [1] "Sell 3 items"
    [2] "Return to the guide"
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
   - 创建 QuestDefinition SO，配置标识、文本、条件、奖励
   - 无需在 Dialogue Database 中做任何操作

2. 任务激活
   方式 A: QuestDefinition.activationMarker 触发时自动激活（推荐）
   方式 B: 对话中调用 SetQuestStateAfter("QuestName", "active")
   方式 C: 代码调用 QuestLogicManager.Instance.ActivateQuest("QuestName")
   → QuestStateStore 记录状态 → BroadcastMessage 通知
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

### QuestStateStore（状态后端）

QuestStateStore 通过 QuestLog 的 override delegate 拦截所有状态读写：
- 所有 `QuestLog.SetQuestState()` / `GetQuestState()` 调用自动路由到 store
- Lua 中的 `CurrentQuestState()` / `SetQuestState()` 同样自动路由
- 状态不再存入 Dialogue Database，而是由 ES3 持久化

### TutorialMarkerBridge（不变）

现有的 TutorialMarkerBridge 仍然可以正常工作：
- TutorialMarker → 设置 Entry 状态（Manual 类型条件，通过 override delegate 路由到 store）
- QuestLogicManager 会自动检测 Entry 状态变化并处理完成逻辑

### PopLifeLuaFunctions（不变）

对话中仍可使用（通过 override delegate 自动路由到 QuestStateStore）：
```lua
SetQuestStateAfter("QuestName", "active")         -- 激活任务（对话结束后执行）
SetQuestEntryStateAfter("QuestName", 1, "success") -- 完成手动步骤（对话结束后执行）
CurrentQuestState("QuestName")                      -- 查询状态（用于对话条件分支）
```

### 数据持久化

- ES3 文件：`QuestProgress.es3`
- 保存内容：任务状态数据、计数器状态、激活日映射、已发放奖励列表
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
- [ ] QuestDefinition SO 配置了 conditions 和 entryTexts 数组（长度一致）
- [ ] QuestDefinition SO 填写了 title、description 文本
- [ ] QuestNotificationToast 已搭建并引用配置完毕
- [ ] 激活任务后 QuestProgressTracker 开始追踪（Debug 日志确认）
- [ ] 卖出商品时 SellItems 条件计数增加
- [ ] 顾客结账时 ServeCustomers 条件计数增加
- [ ] 所有条件满足后任务自动完成
- [ ] 完成后奖励正确发放（金钱/声望/蓝图/顾客）
- [ ] DDL 到期后任务自动失败
- [ ] Toast 通知正确显示（新任务/完成/失败）
- [ ] 进度中途退出并重启后状态和计数器正确恢复
- [ ] Manual 类型条件可通过 TutorialMarkerBridge 手动标记完成
- [ ] 对话中 `CurrentQuestState()` 条件分支正常工作
