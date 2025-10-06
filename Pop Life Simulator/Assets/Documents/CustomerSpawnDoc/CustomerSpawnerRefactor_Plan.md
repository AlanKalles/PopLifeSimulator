# Customer Spawner System Refactor - 实现计划

## 项目概述

重构顾客生成系统，实现多点生成、时间驱动、流量控制的智能化spawner。

**创建日期**: 2025-10-05
**最后更新**: 2025-10-05 (v1.2 - 优化编辑器工具分工，简化路径管理)
**状态**: 待实现

每次按照该计划进行实现后更新SpawnerRefactorLog.md于Documents文件夹，记录当次实现了计划中的哪些内容

---

## 一、设计目标

### 核心需求
1. ✅ 支持多个生成点随机出生
2. ✅ 基于时间的智能生成（Trait时间倾向 + Archetype时间窗口）
3. ✅ 流量控制（间隔时间随机 + 场上人数上限 + 抖动）
4. ✅ SpawnerProfile运行时配置（JSON存储解锁customer ID，支持跨平台存档）
5. ✅ 与DayLoopManager自动集成（正确的事件生命周期管理）
6. ✅ 预热机制（开店后延迟生成，避免瞬间涌入）

### 技术特性
- 数据驱动配置（ScriptableObject + JSON）
- 事件驱动架构（监听开店/关店，OnEnable订阅/OnDisable退订）
- 加权随机算法（时间倾向影响权重）
- 对象池管理（每日刷新可生成列表）
- 跨平台存档路径（StreamingAssets初始数据 + persistentDataPath运行时存档）

---

## 二、系统架构

```
┌───────────────────────────────────────────────────────┐
│          StreamingAssets (初始数据)                    │
│  SpawnerProfile.json / Customers.json                 │
│  (开发者预设，打包时只读)                               │
└───────────────────────────────────────────────────────┘
                        ↓ 首次启动复制
┌───────────────────────────────────────────────────────┐
│        persistentDataPath (运行时存档)                 │
│  SpawnerProfile.json / Customers.json                 │
│  (玩家可修改，跨平台支持)                               │
└───────────────────────────────────────────────────────┘
                        ↓
┌───────────────────────────────────────────────────────┐
│            SavePathManager (路径管理器)                │
│  • 编辑器模式: StreamingAssets                         │
│  • 运行时模式: persistentDataPath                      │
│  • 首次复制逻辑                                        │
└───────────────────────────────────────────────────────┘
                        ↓
DayLoopManager (时间控制)
    ↓ OnStoreOpen
CustomerSpawner.OnEnable (订阅事件 + 热加入检测)
    ↓ InitializeDailyPool
SpawnerProfile.json (加载解锁列表)
    ↓
CustomerRepository (加载customer records)
    ↓ 构建对象池
Update循环 (Time.time >= nextSpawnTime)
    ↓ TrySpawnCustomer
TimeBasedSpawnFilter (时间筛选器)
    ↓ 返回加权列表
WeightedRandom (加权随机)
    ↓ 选择customer
Instantiate Customer (多生成点随机)
    ↓
ScheduleNextSpawn (基础间隔 + 随机抖动)
```

---

## 三、文件结构

### 新增文件

#### 1. `Assets/Scripts/Utility/SavePathManager.cs` ⭐ 新增
**作用**: 统一管理跨平台存档路径
**核心功能**:
- 编辑器模式读写 `StreamingAssets`
- 运行时读写 `persistentDataPath`
- 首次启动自动复制初始数据
- 提供 `GetReadPath()` 和 `GetWritePath()` 接口

**内容**:
```csharp
public static class SavePathManager
{
    public static string GetReadPath(string fileName)
    {
        #if UNITY_EDITOR
        return Path.Combine(Application.streamingAssetsPath, fileName);
        #else
        string runtimePath = Path.Combine(Application.persistentDataPath, fileName);
        // 首次运行从StreamingAssets复制
        if (!File.Exists(runtimePath))
        {
            string streamingPath = Path.Combine(Application.streamingAssetsPath, fileName);
            if (File.Exists(streamingPath))
                File.Copy(streamingPath, runtimePath);
        }
        return runtimePath;
        #endif
    }

    public static string GetWritePath(string fileName)
    {
        #if UNITY_EDITOR
        return Path.Combine(Application.streamingAssetsPath, fileName);
        #else
        return Path.Combine(Application.persistentDataPath, fileName);
        #endif
    }
}
```

#### 2. `Assets/Editor/SpawnerProfileEditor.cs` ⭐ 新增（v1.2 重命名并简化）
**作用**: 专注于 SpawnerProfile.json 的可视化编辑工具
**功能**:
- 读取/写入 `StreamingAssets/SpawnerProfile.json`
- 提供GUI界面管理解锁顾客ID列表（添加/移除customer ID）
- 一键同步到 `persistentDataPath`（测试用）
- 集成"跳转到 Customer Records Editor"按钮

**注**: 原计划的 `CustomerDataEditorWindow` 简化为仅管理 SpawnerProfile，顾客记录编辑功能由现有的 `CustomerRecordsEditor.cs` 负责

#### 3. `Assets/Scripts/Customers/Data/TimePreference.cs`
**作用**: 定义时间范围数据结构
**内容**:
```csharp
[Serializable]
public class TimePreference
{
    public float startHour = 12f;  // 12:00
    public float endHour = 22.5f;  // 22:30

    public bool IsInRange(float currentHour)
    {
        return currentHour >= startHour && currentHour <= endHour;
    }
}
```

#### 4. `Assets/Scripts/Customers/Data/SpawnerProfile.cs`
**作用**: 运行时可编辑的解锁customer配置
**存储位置**:
- **编辑器**: `Assets/StreamingAssets/SpawnerProfile.json`
- **运行时**: `persistentDataPath/SpawnerProfile.json`

**内容**:
```csharp
[Serializable]
public class SpawnerProfile
{
    public List<string> unlockedCustomerIds;

    public static SpawnerProfile Load()
    {
        string path = SavePathManager.GetReadPath("SpawnerProfile.json");
        if (!File.Exists(path)) return new SpawnerProfile();

        string json = File.ReadAllText(path);
        return JsonUtility.FromJson<SpawnerProfile>(json);
    }

    public void Save()
    {
        string path = SavePathManager.GetWritePath("SpawnerProfile.json");
        string json = JsonUtility.ToJson(this, true);
        File.WriteAllText(path, json);
    }

    public void UnlockCustomer(string customerId)
    {
        if (!unlockedCustomerIds.Contains(customerId))
            unlockedCustomerIds.Add(customerId);
    }

    public void LockCustomer(string customerId)
    {
        unlockedCustomerIds.Remove(customerId);
    }
}
```

#### 5. `Assets/Scripts/Customers/Services/TimeBasedSpawnFilter.cs`
**作用**: 根据当前时间筛选并计算customer生成权重
**核心逻辑**:
1. 检查Archetype的`spawnTimeWindow`（硬性过滤）
2. 检查Trait的`preferredTimeRanges`（加权加成）
3. 返回`List<WeightedCustomer>`

```csharp
public class TimeBasedSpawnFilter
{
    public List<WeightedCustomer> GetEligibleCustomers(
        List<CustomerRecord> candidates,
        float currentHour,
        CustomerRepository repository
    );
}

public struct WeightedCustomer
{
    public CustomerRecord record;
    public float finalWeight;
}
```

---

### 修改文件

#### 1. `Assets/Scripts/Customers/Editor/CustomerRecordsEditor.cs` ⭐ 路径修改（v1.2 新增）
**修改内容**: 使用 `SavePathManager` 统一路径管理

**原代码**:
```csharp
private static string SaveFolderPath => Path.Combine(Application.dataPath, "Documents", "Save");
private static string DefaultFilePath => Path.Combine(SaveFolderPath, "Customers.json");
```

**修改为**:
```csharp
private static string DefaultReadPath => SavePathManager.GetReadPath("Customers.json");
private static string DefaultWritePath => SavePathManager.GetWritePath("Customers.json");
```

**路径行为**:
- **编辑器模式**: 读写 `StreamingAssets/Customers.json`（开发者编辑初始数据）
- **运行时模式**: 读写 `persistentDataPath/Customers.json`（玩家存档）
- **首次运行**: 自动从 StreamingAssets 复制到 persistentDataPath

**注**: 保留该编辑器的所有现有功能（搜索、排序、CSV导入导出、可视化编辑等），仅更新路径逻辑

#### 2. `Assets/Scripts/Customers/Data/Trait.cs`
**新增字段**:
```csharp
[Header("时间倾向")]
[Tooltip("该特质偏好的时间段（可多个）")]
public TimePreference[] preferredTimeRanges;

[Tooltip("在偏好时间段内的权重倍率")]
[Range(0f, 3f)]
public float timePreferenceWeight = 1f;
```

**作用**: 如"夜猫子"trait可配置20:00-23:00权重×1.8

#### 3. `Assets/Scripts/Customers/Data/CustomerArchetype.cs`
**新增字段**:
```csharp
[Header("生成时间控制")]
[Tooltip("该原型可被生成的时间窗口")]
public TimePreference spawnTimeWindow = new TimePreference
{
    startHour = 12f,   // 默认开店时间
    endHour = 22.5f    // 默认闭店前半小时
};
```

**作用**: 如"办公族"原型可限制仅18:00-23:00出现

#### 4. `Assets/Scripts/Customers/Spawner/CustomerSpawner.cs` ⭐ 重大重构
**完全重构** - 核心变更:

**新增字段**:
```csharp
[Header("生成点配置")]
public Transform[] spawnPoints; // 替代单一spawnPoint

[Header("流量控制")]
public float[] spawnIntervalOptions = { 3f, 5f, 8f, 10f }; // 随机间隔（秒）
public int maxCustomersOnFloor = 10; // 场上人数上限

[Header("节奏控制")] ⭐ 新增
[Tooltip("开店后延迟多久开始生成第一个customer（秒）")]
public float initialSpawnDelay = 5f;
[Tooltip("在间隔基础上的随机抖动范围（秒）")]
public Vector2 spawnJitter = new Vector2(-1f, 1f);

[Header("调试")]
[SerializeField] private int currentCustomerCount;
[SerializeField] private float nextSpawnTime;
```

**核心方法**:
```csharp
private void OnEnable() ⭐ 修改
{
    if (DayLoopManager.Instance != null)
    {
        // 订阅事件
        DayLoopManager.Instance.OnStoreOpen += InitializeDailyPool;
        DayLoopManager.Instance.OnStoreClose += StopSpawning;

        // 热加入：如果已经开门，立即初始化
        if (DayLoopManager.Instance.isStoreOpen)
        {
            InitializeDailyPool();
        }
    }
}

private void OnDisable() ⭐ 新增
{
    if (DayLoopManager.Instance != null)
    {
        // 退订事件，防止内存泄漏
        DayLoopManager.Instance.OnStoreOpen -= InitializeDailyPool;
        DayLoopManager.Instance.OnStoreClose -= StopSpawning;
    }
}

private void InitializeDailyPool()
{
    // 1. 加载SpawnerProfile.json (使用SavePathManager)
    // 2. 根据unlockedCustomerIds从Repository获取records
    // 3. 构建customerPool列表
    // 4. 重置nextSpawnTime (加上 initialSpawnDelay) ⭐ 修改
    nextSpawnTime = Time.time + initialSpawnDelay;
}

private void Update()
{
    // 1. 检查是否到达nextSpawnTime
    // 2. 检查是否超过maxCustomersOnFloor
    // 3. 调用TrySpawnCustomer()
}

private void TrySpawnCustomer()
{
    // 1. 获取当前时间
    // 2. 调用TimeBasedSpawnFilter筛选
    // 3. 加权随机选择customer
    // 4. 随机选择spawnPoint
    // 5. 实例化customer
    // 6. 调用ScheduleNextSpawn()
}

private void ScheduleNextSpawn() ⭐ 修改
{
    // 从spawnIntervalOptions随机选择基础间隔
    float baseInterval = spawnIntervalOptions[Random.Range(0, spawnIntervalOptions.Length)];

    // 加上随机抖动
    float jitter = Random.Range(spawnJitter.x, spawnJitter.y);
    float finalInterval = Mathf.Max(0.1f, baseInterval + jitter);

    nextSpawnTime = Time.time + finalInterval;
}

private int GetCurrentCustomerCount()
{
    // 查询场景中当前CustomerAgent数量
    return FindObjectsOfType<CustomerAgent>().Length;
}
```

#### 5. `Assets/Scripts/Customers/Services/CustomerRepository.cs` ⭐ 路径修改
**修改内容**:
```csharp
// 原: private string FilePath => Path.Combine(Application.dataPath, "Documents", "Save", fileName);
// 新: 使用SavePathManager
private string FilePath => SavePathManager.GetReadPath(fileName);
private string SavePath => SavePathManager.GetWritePath(fileName);

public void Load()
{
    string path = FilePath; // 自动处理首次复制
    // ... 原有逻辑
}

public void Save()
{
    string path = SavePath; // 写入正确路径
    // ... 原有逻辑
}
```

---

## 四、实现步骤

### Phase 0: 基础设施 ⭐ 新增
- [ ] **步骤0.1**: 创建`SavePathManager.cs` (路径管理工具)
- [ ] **步骤0.2**: 创建`Assets/StreamingAssets`文件夹（如不存在）
- [ ] **步骤0.3**: 创建编辑器工具`SpawnerProfileEditor.cs`（v1.2 简化为仅管理SpawnerProfile）
- [ ] **步骤0.4**: 修改`CustomerRecordsEditor.cs`使用`SavePathManager` ⭐ v1.2 新增
- [ ] **步骤0.5**: 修改`CustomerRepository.cs`使用`SavePathManager`

### Phase 1: 数据层基础
- [ ] **步骤1.1**: 创建`TimePreference.cs`
- [ ] **步骤1.2**: 创建`SpawnerProfile.cs`及JSON序列化逻辑（使用SavePathManager）
- [ ] **步骤1.3**: 扩展`Trait.cs`添加时间倾向字段
  - [ ] 添加`preferredTimeRanges`数组
  - [ ] 添加`timePreferenceWeight`倍率
- [ ] **步骤1.4**: 扩展`CustomerArchetype.cs`添加时间窗口字段
  - [ ] 添加`spawnTimeWindow`字段

### Phase 2: 服务层逻辑
- [ ] **步骤2.1**: 实现`TimeBasedSpawnFilter.cs`
  - [ ] 实现Archetype时间窗口过滤（硬性）
  - [ ] 实现Trait时间倾向权重计算（软性）
  - [ ] 实现加权列表生成
  - [ ] 实现`WeightedCustomer`结构体
- [ ] **步骤2.2**: 实现加权随机算法（在Spawner中）

### Phase 3: Spawner重构
- [ ] **步骤3.1**: 重构`CustomerSpawner.cs`
  - [ ] 添加多生成点支持 (`Transform[] spawnPoints`)
  - [ ] 添加流量控制字段 (`spawnIntervalOptions`, `maxCustomersOnFloor`)
  - [ ] 添加节奏控制字段 (`initialSpawnDelay`, `spawnJitter`) ⭐ 新增
  - [ ] 修改`OnEnable`添加热加入检测 ⭐ 新增
  - [ ] 添加`OnDisable`退订事件 ⭐ 新增
  - [ ] 实现每日池初始化（使用SpawnerProfile）
  - [ ] 实现自动生成Update循环
  - [ ] 修改`ScheduleNextSpawn`添加抖动逻辑 ⭐ 新增
  - [ ] 保留原有手动生成功能（兼容性）

### Phase 4: 数据迁移与配置
- [ ] **步骤4.1**: 迁移现有JSON文件到StreamingAssets
  - [ ] 将`Documents/Save/Customers.json` 移动到 `StreamingAssets/Customers.json`
  - [ ] 创建初始`StreamingAssets/SpawnerProfile.json`
- [ ] **步骤4.2**: 配置现有ScriptableObject资产
  - [ ] 为常用Trait配置`preferredTimeRanges`（夜猫子、早鸟、办公族等）
  - [ ] 为所有Archetype配置`spawnTimeWindow`（默认12:00-22:30）
- [ ] **步骤4.3**: 在场景中配置多个spawn point

### Phase 5: 测试与调优
- [ ] **步骤5.1**: 测试存储路径逻辑
  - [ ] 编辑器模式读写StreamingAssets
  - [ ] 构建后首次运行复制逻辑
  - [ ] 运行时persistentDataPath读写
- [ ] **步骤5.2**: 测试事件生命周期
  - [ ] 正常开店/关店流程
  - [ ] 热加入场景（Spawner启用时店已开门）
  - [ ] 对象禁用/销毁时事件退订
- [ ] **步骤5.3**: 测试时间过滤逻辑
  - [ ] 不同时间段生成对应customer
  - [ ] Trait时间倾向权重生效
- [ ] **步骤5.4**: 测试流量控制
  - [ ] 间隔时间随机选择
  - [ ] 抖动生效（非规律生成） ⭐ 新增
  - [ ] 场上人数上限生效
  - [ ] 初始延迟生效 ⭐ 新增
- [ ] **步骤5.5**: 调优参数
  - [ ] 调整间隔选项数值
  - [ ] 调整抖动范围
  - [ ] 调整初始延迟时间
  - [ ] 调整权重倍率

---

## 五、数据示例

### SpawnerProfile.json
```json
{
  "unlockedCustomerIds": [
    "C001",
    "C002",
    "C003",
    "V001"
  ]
}
```

### Trait配置示例（夜猫子）
```
traitId: "NightOwl"
preferredTimeRanges:
  - startHour: 20.0
    endHour: 23.0
timePreferenceWeight: 1.8
```

### Archetype配置示例（办公族）
```
archetypeId: "OfficeworkerArchetype"
spawnTimeWindow:
  startHour: 18.0
  endHour: 23.0
```

---

## 六、预期效果

### 时间分布示例
| 时间段 | 可能出现的customer类型 |
|--------|----------------------|
| 12:00-14:00 | 午休族、自由职业者、学生 |
| 14:00-18:00 | 自由职业者、待业者、早退族 |
| 18:00-20:00 | 办公族、学生、普通顾客 |
| 20:00-23:00 | 夜猫子、派对动物、失眠者 |

### 流量控制效果
- 初始延迟：开店后5秒开始生成第一个customer ⭐ 新增
- 间隔：每3-10秒随机生成一个customer
- 抖动：基础间隔±1秒随机抖动 ⭐ 新增
- 上限：场上同时存在≤10个customer
- 自然波动：避免规律性，模拟真实客流

### 存储路径效果 ⭐ 新增
| 环境 | 读取路径 | 写入路径 | 首次运行行为 |
|------|---------|---------|-------------|
| Unity编辑器 | StreamingAssets | StreamingAssets | 直接读写 |
| 构建版本（首次） | StreamingAssets → persistentDataPath | persistentDataPath | 自动复制初始数据 |
| 构建版本（后续） | persistentDataPath | persistentDataPath | 读写玩家存档 |

---

## 七、扩展性考虑

### 未来可扩展功能
1. **星期系统**: 在`TimePreference`中添加`dayOfWeek`字段
2. **天气影响**: 添加`weatherModifier`影响生成率
3. **特殊事件**: 节假日（情人节）提升特定trait权重
4. **VIP预约**: 特定customer在特定时间必定出现
5. **区域生成**: 不同spawnPoint关联不同customer池

### 性能优化预留
- 对象池复用（避免频繁Instantiate）
- 权重计算缓存（相同时间段复用结果）
- 异步加载customer资源

---

## 八、技术依赖

### 现有系统依赖
- `DayLoopManager`: 时间控制与事件
- `CustomerRepository`: customer数据存储
- `CustomerAgent`: customer实例初始化
- `CustomerRecord`: customer持久化数据

### Unity组件
- `Transform[]`: 多生成点管理
- `Time.time`: 间隔计时
- `Random.Range`: 随机选择

---

## 九、风险与注意事项

### 潜在风险
1. **时间窗口冲突**: 所有customer的时间窗口都不在当前时间 → 无customer生成
   - **解决**: 确保至少有部分customer的窗口覆盖全营业时间
2. **权重计算开销**: 每次生成都遍历所有customer计算权重
   - **解决**: 缓存相同时间段的结果，或预计算时间段映射表
3. **场上人数统计**: `FindObjectsOfType`开销较大
   - **解决**: 改用手动计数（生成+1，离店-1）
4. **StreamingAssets在WebGL平台的限制** ⭐ 新增
   - WebGL无法直接File.Copy，需使用UnityWebRequest异步加载
   - **解决**: 如需支持WebGL，SavePathManager需增加平台判断
5. **事件订阅时序问题** ⭐ 新增
   - 如果DayLoopManager比Spawner晚初始化，会出现空引用
   - **解决**: 在OnEnable中加null检查，或使用ScriptExecutionOrder

### 兼容性
- 保留`SpawnCustomerById(string id)`手动生成接口
- 保留Inspector调试字段（targetCustomerId, spawnTargetCustomer）
- 确保不破坏现有CustomerAgent初始化流程
- 向后兼容旧的存储路径（首次运行自动迁移） ⭐ 新增

---

## 十、验收标准

### 功能验收
- [ ] 可从多个生成点随机生成customer
- [ ] customer按时间倾向自然分布
- [ ] 流量控制生效（间隔+上限+抖动） ⭐ 更新
- [ ] 初始延迟生效（开店后延迟生成） ⭐ 新增
- [ ] SpawnerProfile.json可运行时读写
- [ ] 开店自动开始生成，关店自动停止
- [ ] 热加入场景时正确初始化 ⭐ 新增
- [ ] 存储路径在编辑器和构建版本均正常工作 ⭐ 新增
- [ ] 首次运行自动复制初始数据 ⭐ 新增

### 质量验收
- [ ] 无重大性能问题（FPS稳定）
- [ ] 无内存泄漏（事件正确退订） ⭐ 更新
- [ ] 代码符合项目规范（命名空间、注释）
- [ ] 可在Inspector中直观调试
- [ ] 兼容旧版手动生成接口 ⭐ 新增

---

## 附录：关键代码片段

### 加权随机算法
```csharp
private CustomerRecord WeightedRandom(List<WeightedCustomer> weighted)
{
    float totalWeight = 0f;
    foreach (var wc in weighted) totalWeight += wc.finalWeight;

    float rand = Random.Range(0f, totalWeight);
    float cumulative = 0f;

    foreach (var wc in weighted)
    {
        cumulative += wc.finalWeight;
        if (rand <= cumulative) return wc.record;
    }

    return weighted[weighted.Count - 1].record; // fallback
}
```

### 时间权重计算
```csharp
float CalculateTimeWeight(CustomerRecord record, float currentHour)
{
    // 1. 加载archetype和traits
    var archetype = LoadArchetype(record.archetypeId);
    var traits = LoadTraits(record.traitIds);

    // 2. 检查archetype窗口
    if (!archetype.spawnTimeWindow.IsInRange(currentHour))
        return 0f;

    // 3. 基础权重
    float weight = archetype.spawnWeight;

    // 4. trait时间加成
    foreach (var trait in traits)
    {
        if (trait.preferredTimeRanges != null)
        {
            foreach (var range in trait.preferredTimeRanges)
            {
                if (range.IsInRange(currentHour))
                {
                    weight *= trait.timePreferenceWeight;
                    break; // 每个trait只应用一次加成
                }
            }
        }
    }

    return weight;
}
```

---

## 十一、变更日志 ⭐ 新增

### v1.2 (2025-10-05)
**架构优化：编辑器工具分工优化**

#### 修改内容
1. **编辑器工具重新分工**
   - 原计划的 `CustomerDataEditorWindow.cs` 重命名为 `SpawnerProfileEditor.cs`
   - 简化功能：专注于 `SpawnerProfile.json` 的编辑（解锁/锁定顾客ID）
   - 保留现有的 `CustomerRecordsEditor.cs` 作为顾客记录专业编辑器
   - 避免功能重复，明确职责分工

2. **CustomerRecordsEditor 路径升级**
   - 修改为使用 `SavePathManager` 统一路径管理
   - 编辑器模式：读写 `StreamingAssets/Customers.json`
   - 运行时模式：读写 `persistentDataPath/Customers.json`
   - 保留所有现有功能（搜索、排序、CSV、可视化编辑）

#### 实现步骤更新
- Phase 0 步骤0.3：修改为创建 `SpawnerProfileEditor.cs`（简化版）
- Phase 0 步骤0.4：新增修改 `CustomerRecordsEditor.cs` 使用 SavePathManager
- Phase 0 步骤0.5：原步骤0.4（修改 CustomerRepository）

#### 设计理念
- **职责分离**: SpawnerProfileEditor 管理解锁列表，CustomerRecordsEditor 管理顾客详细数据
- **避免重复**: 不创建功能重叠的编辑器窗口
- **向后兼容**: 充分利用现有成熟的 CustomerRecordsEditor，仅升级路径逻辑

---

### v1.1 (2025-10-05)
**重大更新：存储路径修正 + 事件生命周期 + 节奏控制**

#### 新增内容
1. **存储路径系统重构**
   - 新增 `SavePathManager.cs` 统一路径管理
   - 编辑器模式使用 `StreamingAssets`
   - 运行时使用 `persistentDataPath`
   - 首次启动自动复制初始数据
   - 新增编辑器工具 `CustomerDataEditorWindow.cs`

2. **事件生命周期管理**
   - `OnEnable` 订阅事件 + 热加入检测
   - `OnDisable` 退订事件（防止内存泄漏）
   - 支持场景热加入（Spawner启用时店已开门）

3. **预热与节奏控制**
   - 新增 `initialSpawnDelay` 字段（开店后延迟）
   - 新增 `spawnJitter` 字段（间隔随机抖动）
   - 打破机械感，提升自然度

#### 修改内容
- `CustomerSpawner.cs` 重大重构（事件订阅/退订、节奏控制）
- `CustomerRepository.cs` 路径逻辑修改（使用SavePathManager）
- `SpawnerProfile.cs` 路径逻辑修改（使用SavePathManager）
- 实现步骤增加Phase 0（基础设施）
- 实现步骤增加Phase 5详细测试项
- 验收标准增加存储路径、热加入、内存泄漏检查

#### 风险更新
- 新增WebGL平台StreamingAssets限制说明
- 新增事件订阅时序问题说明

---

### v1.0 (2025-10-05)
**初始版本**
- 基础架构设计
- 多点生成系统
- 时间驱动筛选
- 流量控制机制
- SpawnerProfile配置

---

**文档版本**: 1.2
**最后更新**: 2025-10-05
**负责人**: Claude Code
**审核状态**: 已更新（优化编辑器工具分工，避免功能重复）
