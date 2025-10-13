# Customer Level Up System - Implementation Plan

**创建日期**: 2025-10-13
**版本**: v1.0
**状态**: Planning

> ⚠️ **重要提示**: 每次读取本计划文件进行更新时，必须同步更新 `CustomerLevelUpSystem_Log.md` 日志文件，记录修改时间、修改内容和修改原因。

---

## 📋 项目概述

实现完整的顾客等级系统，包括经验值获取、升级机制、数据持久化和每日结算时的升级提示。

### 核心目标
- ✅ 顾客通过消费获得经验值（XP）
- ✅ 经验累积到阈值自动升级（不清零，累积式）
- ✅ 支持通过 Trait 调整经验获取倍率
- ✅ 每日结算时展示当日升级的顾客列表
- ✅ 线程安全的数据持久化

---

## 🎯 需求详细说明

### 1. 经验值获取时机

```
顾客生成 → 创建 CustomerSession
    ↓
取货成功 (TryPurchase) → 记录到 session.visitedShelves
    ↓ (如果排队时被打断/关店，直接销毁，不记录经验)
结账成功 (TryCheckout) → 记录 session.moneySpent = pendingPayment
    ↓
离店销毁 (DestroyAgentAction) → 计算 XP/Trust 增量 → 应用到 CustomerRecord
    ↓
保存到 persistentDataPath/Customers.json
```

**关键规则**:
- ✅ 取货后才记录（`TryPurchase()` 成功）
- ❌ 排队中被打断不记录（未结账 = 未完成购买）
- ❌ 关店时强制离店不算有效访问（`moneySpent == 0` → XP 乘数 = 0）

---

### 2. 经验值计算公式

```
基础XP = customerArchetype.baseXpGain
特质乘数 = Π (所有 trait.xpMultiplier)
消费乘数 = 根据消费金额查阈值表

最终XP增量 = 基础XP × 特质乘数 × 消费乘数

特殊规则:
  if (消费金额 == 0) { 消费乘数 = 0 }  // 没消费 = 没经验
  else { 消费乘数 ∈ [1.2, 1.8] }  // 消费越多，乘数越高
```

#### 消费金额阈值配置（默认值）

| 消费金额 | 经验乘数 | 说明 |
|---------|---------|------|
| 0 | 0 | 没消费就没经验 |
| 1-15 | 1.2 | 小额消费 |
| 16-25 | 1.4 | 中额消费 |
| 26-45 | 1.6 | 大额消费 |
| 46+ | 1.8 | 超大额消费（封顶）|

**可在 Inspector 中调整**

---

### 3. 升级机制（累积式）

```csharp
// 示例配置
customerArchetype.levelUpThresholds = [100, 250, 500, 1000]

// 升级判断（累积式，不清零）
当前 XP = 260
  → Level 0: XP >= 100 ✅ → level = 1
  → Level 1: XP >= 250 ✅ → level = 2
  → Level 2: XP >= 500 ❌ → 停止
  → 最终 loyaltyLevel = 2
```

**特点**:
- 经验值永不清零，只增不减
- 支持一次性跨多级升级
- 每个等级的阈值独立配置

---

### 4. 升级记录与结算展示

```
Day 5 营业中:
  ├─ Alice 离店 → 升级 0→1 → 记录到 DayLoopManager.todayLevelUps
  ├─ Bob 离店   → 未升级
  ├─ Charlie 离店 → 升级 1→2 → 记录到 todayLevelUps
  └─ Dave 离店  → 升级 2→3 → 记录到 todayLevelUps

23:00 关店:
  └─ 触发 OnDailySettlement 事件
      └─ 传递 DailySettlementData {
            sales: 450,
            levelUps: [Alice 0→1, Charlie 1→2, Dave 2→3]
         }

结算面板:
  ├─ 显示销售数据
  └─ 遍历 levelUps 数组 → 显示升级列表

Day 6 开始 (BuildPhase):
  └─ 清空 todayLevelUps 列表
```

---

### 5. 并发保存问题解决

**问题**: 多个顾客同时离开 → 多次写入 JSON → 数据丢失/覆盖

**解决方案**: 在 `CustomerRepository` 添加线程安全的保存方法

```csharp
private static readonly object saveLock = new object();

public void SaveSingleRecord(CustomerRecord record) {
    lock(saveLock) {
        // 1. 读取最新的所有记录
        var allRecords = LoadAll();

        // 2. 更新目标记录
        int index = allRecords.FindIndex(r => r.customerId == record.customerId);
        if (index >= 0) {
            allRecords[index] = record;
        } else {
            allRecords.Add(record);
        }

        // 3. 保存所有记录
        SaveAll(allRecords);
    }
}
```

---

## 📊 数据结构设计

### CustomerArchetype.cs 新增字段

```csharp
[Header("经验值系统")]
[Tooltip("基础经验值增量")]
public float baseXpGain = 10f;

[Tooltip("消费金额对应的经验乘数阈值")]
public SpendingThreshold[] spendingThresholds = new SpendingThreshold[] {
    new() { minSpent = 0,  maxSpent = 0,   multiplier = 0f },
    new() { minSpent = 1,  maxSpent = 15,  multiplier = 1.2f },
    new() { minSpent = 16, maxSpent = 25,  multiplier = 1.4f },
    new() { minSpent = 26, maxSpent = 45,  multiplier = 1.6f },
    new() { minSpent = 46, maxSpent = -1,  multiplier = 1.8f }
};

[Header("等级系统")]
[Tooltip("累积经验阈值，达到阈值[i]时升到等级i+1")]
public int[] levelUpThresholds = new int[] { 100, 250, 500, 1000 };

// 工具方法
public float GetSpendingMultiplier(int moneySpent) {
    foreach (var threshold in spendingThresholds) {
        if (moneySpent >= threshold.minSpent &&
            (threshold.maxSpent == -1 || moneySpent <= threshold.maxSpent)) {
            return threshold.multiplier;
        }
    }
    return 1.0f;
}

[Serializable]
public class SpendingThreshold {
    public int minSpent;
    public int maxSpent;  // -1 表示无上限
    public float multiplier;
}
```

### Trait.cs 新增字段

```csharp
[Header("经验影响")]
[Tooltip("经验获取倍率，1.0为正常，大于1增加经验，小于1减少经验")]
public float xpMultiplier = 1.0f;
```

### TraitResolver.EffectiveStats 新增字段

```csharp
public float xpMul = 1f;  // 累乘所有 trait 的 xpMultiplier
```

### CustomerLevelUpInfo.cs (新建)

```csharp
namespace PopLife.Customers.Runtime
{
    [Serializable]
    public class CustomerLevelUpInfo
    {
        public string customerId;
        public string customerName;
        public int oldLevel;         // 升级前等级
        public int newLevel;         // 升级后等级
        public int totalXp;          // 当前总经验
        public int xpGained;         // 本次获得的经验
        public string appearanceId;  // 外貌ID（用于UI显示头像）
    }
}
```

### DayLoopManager 新增字段和方法

```csharp
[Header("顾客升级追踪")]
private List<CustomerLevelUpInfo> todayLevelUps = new List<CustomerLevelUpInfo>();

public IReadOnlyList<CustomerLevelUpInfo> TodayLevelUps => todayLevelUps;

public void RecordCustomerLevelUp(CustomerLevelUpInfo info)
{
    todayLevelUps.Add(info);
}

private void OnNewDayStart()
{
    todayLevelUps.Clear();
}
```

### DailySettlementData 新增字段

```csharp
public CustomerLevelUpInfo[] levelUps;  // 当日升级的顾客列表
```

---

## 🔧 实施计划

### Phase 1: 数据结构扩展
**目标**: 添加经验系统所需的所有数据字段

| 任务 | 文件 | 修改内容 | 状态 |
|-----|------|---------|------|
| 1.1 | CustomerArchetype.cs | 添加 baseXpGain, spendingThresholds, levelUpThresholds | ⏳ Pending |
| 1.2 | Trait.cs | 添加 xpMultiplier 字段 | ⏳ Pending |
| 1.3 | TraitResolver.cs | 修改 EffectiveStats，累乘 xpMul | ⏳ Pending |
| 1.4 | CustomerLevelUpInfo.cs | 新建升级信息记录类 | ⏳ Pending |

---

### Phase 2: Session 生命周期管理
**目标**: 在顾客生成和交互过程中记录 Session 数据

| 任务 | 文件 | 修改内容 | 状态 |
|-----|------|---------|------|
| 2.1 | CustomerAgent.cs | 添加 currentSession 字段，Initialize 时创建 | ⏳ Pending |
| 2.2 | CustomerInteraction.cs | TryPurchase 成功后记录到 visitedShelves | ⏳ Pending |
| 2.3 | CustomerInteraction.cs | TryCheckout 成功后记录 moneySpent | ⏳ Pending |

**关键代码**:
```csharp
// CustomerAgent.Initialize()
currentSession = new CustomerSession {
    customerId = record.customerId,
    dayId = DayLoopManager.Instance.currentDay.ToString(),
    sessionId = Guid.NewGuid().ToString(),
    moneyBagStart = bb.moneyBag,
    moneySpent = 0,
    trustDelta = 0,
    visitedShelves = new List<ShelfVisit>()
};

// CustomerInteraction.TryPurchase()
customerAgent.currentSession.visitedShelves.Add(new ShelfVisit {
    shelfId = targetShelf.instanceId,
    categoryIndex = (int)targetShelf.archetype.category,
    boughtQty = 1
});

// CustomerInteraction.TryCheckout()
customerAgent.currentSession.moneySpent += blackboard.pendingPayment;
```

---

### Phase 3: 经验计算与升级服务
**目标**: 创建独立服务类处理经验计算和升级逻辑

| 任务 | 文件 | 修改内容 | 状态 |
|-----|------|---------|------|
| 3.1 | CustomerProgressService.cs | 新建静态服务类 | ⏳ Pending |
| 3.2 | CustomerProgressService.cs | 实现 CalculateXpGain() | ⏳ Pending |
| 3.3 | CustomerProgressService.cs | 实现 CalculateLevel() | ⏳ Pending |
| 3.4 | CustomerProgressService.cs | 实现 ApplySessionRewards() | ⏳ Pending |

**核心方法签名**:
```csharp
public static class CustomerProgressService
{
    /// <summary>
    /// 计算经验增量
    /// </summary>
    public static int CalculateXpGain(
        CustomerSession session,
        CustomerArchetype archetype,
        Trait[] traits
    );

    /// <summary>
    /// 计算累积式等级
    /// </summary>
    public static int CalculateLevel(int currentXp, int[] thresholds);

    /// <summary>
    /// 应用经验和升级，记录到 DayLoopManager
    /// </summary>
    public static void ApplySessionRewards(
        CustomerRecord record,
        CustomerSession session,
        CustomerArchetype archetype,
        Trait[] traits
    );
}
```

---

### Phase 4: DestroyAgent 集成
**目标**: 在顾客销毁前应用经验并保存数据

| 任务 | 文件 | 修改内容 | 状态 |
|-----|------|---------|------|
| 4.1 | DestroyAgentAction.cs | 在 OnExecute 开头调用经验服务 | ⏳ Pending |
| 4.2 | DestroyAgentAction.cs | 加载 Archetype 和 Traits 引用 | ⏳ Pending |
| 4.3 | DestroyAgentAction.cs | 调用线程安全保存方法 | ⏳ Pending |

**关键流程**:
```csharp
protected override void OnExecute()
{
    var customerAgent = agent.GetComponent<CustomerAgent>();

    if (customerAgent != null && customerAgent.currentSession != null)
    {
        // 1. 获取 CustomerRecord
        var record = CustomerRepository.Instance.GetRecord(customerAgent.customerID);

        // 2. 获取 Archetype 和 Traits
        var archetype = LoadArchetype(...);
        var traits = LoadTraits(...);

        // 3. 计算并应用经验
        CustomerProgressService.ApplySessionRewards(record, customerAgent.currentSession, archetype, traits);

        // 4. 线程安全保存
        CustomerRepository.Instance.SaveSingleRecord(record);
    }

    // ... 原有的队列释放和销毁逻辑 ...
}
```

---

### Phase 5: 线程安全持久化
**目标**: 确保多顾客同时离开时数据不丢失

| 任务 | 文件 | 修改内容 | 状态 |
|-----|------|---------|------|
| 5.1 | CustomerRepository.cs | 添加 saveLock 静态锁对象 | ⏳ Pending |
| 5.2 | CustomerRepository.cs | 实现 SaveSingleRecord() 方法 | ⏳ Pending |

---

### Phase 6: 升级记录系统
**目标**: 在 DayLoopManager 中记录每日升级，结算时展示

| 任务 | 文件 | 修改内容 | 状态 |
|-----|------|---------|------|
| 6.1 | DayLoopManager.cs | 添加 todayLevelUps 列表 | ⏳ Pending |
| 6.2 | DayLoopManager.cs | 添加 RecordCustomerLevelUp() 方法 | ⏳ Pending |
| 6.3 | DayLoopManager.cs | 在新一天开始时清空列表 | ⏳ Pending |
| 6.4 | DayLoopManager.cs | DailySettlementData 添加 levelUps 字段 | ⏳ Pending |
| 6.5 | DailySettlementPanel.cs | 接收并显示升级列表（UI） | 🔄 Later |
| 6.6 | LevelUpItemDisplay.cs | 新建单个升级项UI组件 | 🔄 Later |

**注**: 6.5 和 6.6 为 UI 部分，可延后实施

---

## 📈 计算示例

### 示例1: 正常消费升级

```
顾客信息:
- Archetype.baseXpGain = 10
- Trait: "Student" (xpMultiplier = 1.5)
- session.moneySpent = 30

计算过程:
1. 基础 XP = 10
2. 特质乘数 = 1.5
3. 消费乘数 = 1.6 (30 在 26-45 区间)
4. 最终 XP = 10 × 1.5 × 1.6 = 24

升级检查:
- 当前 XP: 80
- 新 XP: 80 + 24 = 104
- 检查阈值 [100, 250, 500, 1000]
  → 104 >= 100 ✅ → level = 1
  → 104 >= 250 ❌ → 停止
- loyaltyLevel: 0 → 1 (升级！)

记录到 DayLoopManager:
- CustomerLevelUpInfo { oldLevel=0, newLevel=1, xpGained=24, totalXp=104 }
```

### 示例2: 未消费不涨经验

```
顾客信息:
- Archetype.baseXpGain = 10
- Trait: "Default" (xpMultiplier = 1.0)
- session.moneySpent = 0  // 进店后没买东西就离开

计算过程:
1. 基础 XP = 10
2. 特质乘数 = 1.0
3. 消费乘数 = 0 (消费金额为0)
4. 最终 XP = 10 × 1.0 × 0 = 0

结果: 不涨经验，不记录升级
```

### 示例3: 跨级升级

```
顾客信息:
- 当前 XP: 90
- 本次获得: 180 XP
- 新 XP: 270

升级检查:
- 阈值 [100, 250, 500, 1000]
  → 270 >= 100 ✅ → level = 1
  → 270 >= 250 ✅ → level = 2
  → 270 >= 500 ❌ → 停止
- loyaltyLevel: 0 → 2 (跨级升级！)

记录: oldLevel=0, newLevel=2, xpGained=180, totalXp=270
```

---

## ⚠️ 注意事项

### 1. CustomerAgent 需要缓存 Archetype 和 Traits
**问题**: DestroyAgent 时需要访问这些数据计算经验
**解决方案**: 在 CustomerAgent 添加字段缓存
```csharp
public CustomerArchetype cachedArchetype;
public Trait[] cachedTraits;
```

### 2. 资源加载问题
**问题**: Archetype 和 Traits 可能从 Resources 动态加载
**解决方案**: 确保在 DestroyAgent 时能正确访问，或在 Initialize 时缓存

### 3. 关店强制离开处理
**问题**: 关店时顾客被强制销毁
**确认**: `session.moneySpent == 0` 时，消费乘数 = 0，不涨经验 ✅

### 4. 升级事件触发
**问题**: 是否需要在升级时触发事件（UI 提示、音效）？
**当前方案**: 仅记录到 DayLoopManager，结算时统一展示
**可扩展**: 在 CustomerProgressService 中添加事件系统

### 5. 多次升级显示
**问题**: 一个顾客一天升了2级，如何显示？
**方案**: 显示一条记录 "Lv.0 → Lv.2"（代码已按此设计）

---

## 🧪 测试清单

### 单元测试
- [ ] CustomerArchetype.GetSpendingMultiplier() 边界值测试
- [ ] CustomerProgressService.CalculateLevel() 累积式升级测试
- [ ] TraitResolver.Compute() xpMul 累乘测试

### 集成测试
- [ ] 顾客正常消费 → 获得经验 → 升级
- [ ] 顾客不消费 → 不获得经验
- [ ] 多个顾客同时离开 → 数据不丢失
- [ ] 跨级升级正确记录
- [ ] 每日结算显示升级列表

### 边界测试
- [ ] 消费金额 = 0
- [ ] 消费金额 = 1
- [ ] 消费金额 = 阈值边界（15, 16, 25, 26, 45, 46）
- [ ] XP 刚好等于阈值
- [ ] 一次获得大量 XP 跨多级

---

## 📦 文件清单

### 修改文件 (9个)
1. `Scripts/Customers/Data/CustomerArchetype.cs`
2. `Scripts/Customers/Data/Trait.cs`
3. `Scripts/Customers/Services/TraitResolver.cs`
4. `Scripts/Customers/Runtime/CustomerAgent.cs`
5. `Scripts/Customers/Runtime/CustomerInteraction.cs`
6. `Scripts/Customers/NodeCanvas/Actions/DestroyAgentAction.cs`
7. `Scripts/Customers/Services/CustomerRepository.cs`
8. `Scripts/Manager/DayLoopManager.cs`
9. `Scripts/UI/DailySettlementPanel.cs` (UI部分，可延后)

### 新建文件 (2个)
1. `Scripts/Customers/Runtime/CustomerLevelUpInfo.cs`
2. `Scripts/Customers/Services/CustomerProgressService.cs`
3. `Scripts/UI/LevelUpItemDisplay.cs` (UI组件，可延后)

---

## 📅 时间估算

| 阶段 | 预计时间 | 备注 |
|-----|---------|------|
| Phase 1: 数据结构 | 30 分钟 | 简单字段添加 |
| Phase 2: Session 管理 | 45 分钟 | 需要仔细测试记录时机 |
| Phase 3: 经验服务 | 1 小时 | 核心逻辑，需充分测试 |
| Phase 4: DestroyAgent | 30 分钟 | 集成调用 |
| Phase 5: 持久化 | 20 分钟 | 添加锁机制 |
| Phase 6: 升级记录 | 40 分钟 | DayLoopManager 集成 |
| 测试调试 | 1 小时 | 全流程测试 |
| **总计** | **约 4.5 小时** | 不含UI部分 |

---

## 🔄 版本历史

| 版本 | 日期 | 修改内容 | 修改人 |
|-----|------|---------|-------|
| v1.0 | 2025-10-13 | 初始版本，完整计划文档 | Claude |

---

## 📎 相关文档

- `PopLifeDesignDoc.md` - 游戏设计总文档
- `CustomerLevelUpSystem_Log.md` - 实施日志文件
- `CLAUDE.md` - 项目开发规范

---

**最后更新**: 2025-10-13
**下一步行动**: 开始 Phase 1 实施
