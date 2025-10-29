# Dialogue System Analysis

## 系统概览

当前项目包含一个**基于 NodeCanvas DialogueTree 的对话系统**，用于实现教程对话和 VIP 故事任务。

**状态**：核心代码已完成，但**未集成到主场景（LatestUpdate）**。

---

## 文件结构

### 核心脚本 (`Scripts/Dialogue/`)

| 文件 | 功能 | 状态 |
|------|------|------|
| `DialogueManager.cs` | 对话事件管理器 | ✅ 已实现 |
| `DialogueEvent.cs` | 单个对话事件逻辑（触发条件、UI创建） | ✅ 已实现 |
| `DialogueTriggerClickable.cs` | 可点击的对话触发器组件 | ⚠️ 大部分代码被注释 |

### 预制体 (`Prefab/Dialogue/`)

- `DialogueManager.prefab` - 对话管理器
- `CanvasDialogueIndicatorUI.prefab` - World Space Canvas 对话指示器（NPC头顶黄色按钮）

### 对话树资产 (`Resources/DialogueTreeControllers/`)

- **D001** - "Meet Midori"：初次见到 Midori 的剧情（✅ 已完成）
- **D002-D007** - 教程/任务对话（⚠️ 仅占位，内容未填充）

### 测试场景

- `AnnaDialogueTreeTest.unity`
- `AnnaDialogueTutorialTest.unity`

---

## 工作原理

```
[1] DialogueManager 初始化
    └─ 注册对话事件（DialogueTree 资产 + 触发条件 + NPC Code）

[2] 每帧检查触发条件
    └─ 条件满足 → 在 NPC 头顶创建 World Space UI 按钮

[3] 玩家点击按钮
    └─ 动态创建 DialogueTreeController
    └─ 启动 NodeCanvas 对话树
    └─ 销毁 UI 按钮

[4] 对话完成 → 移入归档列表
```

---

## 已实现功能

✅ 基于条件触发的对话事件系统
✅ World Space UI 指示器（自动跟随 NPC）
✅ NodeCanvas DialogueTree 集成
✅ D001 完整剧情内容
✅ 独立测试场景

---

## 缺失实现

### 1. VIP 顾客数据

**问题**：
- `Customers.json` 仅包含普通顾客（C001-C009）
- 缺少 VIP 顾客（V001=Midori, V002）

**影响**：
- `FindNPCByCode()` 找不到 NPC，返回 null
- 对话 UI 无法定位到 NPC 位置

---

### 2. 主场景集成

**问题**：
- **LatestUpdate.unity 未放置 DialogueManager 预制体**

**影响**：
- 对话系统无法在正式游戏中运行

---

### 3. Request（任务）系统

**问题**：
- 完全未实现任务系统代码
- `DialogueEvent.rewards` 字段未使用

**影响**：
- 无法发放奖励（Money, Fame, Blueprint）
- 无法解锁新顾客或触发后续对话

---

### 4. 游戏状态联动

**问题**：
- 触发条件硬编码（如 `() => buildModeEntered`）
- 缺少全局状态管理器追踪：
  - 建造模式是否进入
  - 货架数量
  - 商店是否开张
  - 声望点数

**影响**：
- D002-D007 触发条件无法实现（代码已注释）

---

### 5. 对话完成回调

**问题**：
- 未监听 NodeCanvas 的对话完成事件

**影响**：
- 无法在对话结束后执行逻辑（发放奖励、更新状态）

---

## 预期整合方式

### 教程对话（Tutorial Dialogue）

```
游戏开始
  → D001: Midori 介绍商店（条件：游戏启动）
  → D002: 建造教程（条件：进入建造模式）
  → D003: 开店教程（条件：放置 ≥2 个货架）
  → D004: 首位顾客（条件：商店开张）
  → D005: 声望系统介绍（条件：获得首个声望点）
```

### VIP 故事任务（VIP Story Mission）

根据设计文档：
> VIP Story Mission 是主线剧情。需求不明确，玩家需通过叙事语境解谜。同一时间只存在一个活跃 VIP 任务。

```
VIP 顾客出现在店门口
  → 头顶显示对话指示器
  → 玩家点击触发对话树
  → 对话提供隐晦的任务线索
  → 玩家完成条件后获得奖励
  → 解锁下一个 VIP 任务
```

### 普通顾客请求（Customer Request）

设计文档定义：
- 新顾客（Lv 0）：简单对话选择 → 奖励 Money/Fame/EXP
- 老顾客：建造/升级货架任务 → 奖励新蓝图

---

## 集成到 LatestUpdate 的步骤

### 最小可用集成（让 D001 运行）

**1. 添加 VIP 顾客数据**

在 `StreamingAssets/Customers.json` 添加：
```json
{
  "customerId": "V001",
  "name": "Midori",
  "archetypeId": "VIPCustomer",
  "appearanceId": "CSP_Midori",
  ...
}
```

**2. 放置 DialogueManager**

- 拖入 `Prefab/Dialogue/DialogueManager.prefab` 到 LatestUpdate 场景
- 确保 `dialogueUIPrefab` 引用正确

**3. 生成 Midori NPC**

方案 A：修改 `CustomerSpawner`，游戏开始时生成 Midori
方案 B：场景中手动放置 Midori GameObject（需要 `CustomerBlackboardAdapter` 组件）

**4. 测试**

运行游戏 → Midori 头顶出现黄色按钮 → 点击触发对话

---

### 完整集成（需要额外系统）

**1. 实现 GameStateManager**

```csharp
public class GameStateManager : MonoBehaviour {
    public static bool buildModeEntered;
    public static int shelfCount;
    public static bool storeOpened;
    public static int famePoints;
}
```

**2. 实现 RequestSystem**

```csharp
public class RequestSystem {
    public static void GrantRewards(List<string> rewards);
    public static void UnlockBlueprint(string blueprintId);
    public static void AddCustomerXP(string customerId, int xp);
}
```

**3. 监听对话完成事件**

```csharp
// 在 DialogueEvent.TryTrigger() 中
controllerInstance.onDialogueFinished += OnDialogueComplete;

void OnDialogueComplete(DialogueTree dialogue) {
    // 发放奖励
    RequestSystem.GrantRewards(rewards);
}
```

**4. 集成到 DayLoopManager**

- 商店开张时检查待触发对话
- 对话进行中暂停时间流动

---

## 完成度总结

| 组件 | 完成度 | 备注 |
|------|--------|------|
| 对话事件框架 | 80% | 核心逻辑已实现，缺少奖励系统 |
| UI 指示器 | 70% | 预制体完整，交互逻辑部分注释 |
| 对话树内容 | 15% | 仅 D001 完整，其余为占位 |
| VIP 顾客数据 | 0% | Customers.json 无 VIP 条目 |
| Request 系统 | 0% | 完全未实现 |
| 主场景集成 | 0% | LatestUpdate 未包含对话系统 |

---

## 关键问题

1. **VIP 顾客缺失** - 对话系统依赖的 NPC 不存在
2. **场景未集成** - 代码写好了但未放入主场景
3. **游戏状态系统缺失** - 无法追踪触发条件
4. **任务系统缺失** - 无法发放奖励和解锁内容

---

## 设计意图

对话系统设计用途：
- **教程引导**：通过 Midori 教玩家基础操作
- **剧情叙事**：VIP 任务推进主线故事
- **任务发布**：普通顾客通过对话发布建造任务

整合方式：
1. DialogueManager 作为常驻单例放在主场景
2. VIP 顾客在特定条件下生成在门口
3. 对话完成后通过事件总线通知 RequestSystem
4. 与 DayLoopManager 和 CustomerSpawner 协同工作

---

## 优先级建议

### P0（必须）
- 添加 VIP 顾客数据（V001, V002）
- 将 DialogueManager 放入 LatestUpdate 场景

### P1（高优先级）
- 实现 GameStateManager 追踪游戏状态
- 完善 D002-D007 对话内容

### P2（中优先级）
- 实现 RequestSystem 发放奖励
- 监听对话完成事件

### P3（低优先级）
- 为普通顾客添加对话功能
- 实现对话选择分支逻辑

---

## 相关文档

- 设计文档：`Documents/PopLifeDesignDoc.md`
  - 2.2 Customer Request
  - 2.3 Main Mission (VIP Story Mission)
  - 5.3 Storytelling Tools
- 对话内容表格：[Google Sheets - Tutorial Dialogue](https://docs.google.com/spreadsheets/d/1_qCvlhZ5sylgz-1XzBbcHplGseE1k1LZm2bWC_EidHc/edit?gid=1982605657#gid=1982605657)

---

**结论**：对话系统框架完整且设计良好，但缺少数据支持和场景集成。需要补充 VIP 顾客数据并放入主场景才能运行。
