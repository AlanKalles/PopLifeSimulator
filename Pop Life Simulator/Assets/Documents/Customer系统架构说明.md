# Customer系统架构说明

## 一、系统概述

Customer系统是一个基于数据驱动的顾客行为模拟系统，采用**原型（Archetype）+ 特质（Trait）+ 个体记录（Record）**的组合模式，实现了顾客的多样性和个性化行为。

## 二、核心架构

### 系统分层

```
Customer系统
├── Data层（配置与策略）
│   ├── CustomerArchetype - 顾客原型模板
│   ├── Trait - 特质修饰器
│   ├── CustomerPolicies - 行为策略抽象
│   └── SpawnerProfile - 生成配置
├── Runtime层（运行时实例）
│   ├── CustomerRecord - 持久化数据
│   ├── CustomerSession - 会话记录
│   ├── CustomerAgent - Unity组件
│   └── CustomerBlackboardAdapter - 行为树适配器
└── Services层（业务服务）
    ├── CustomerRepository - 数据存储
    ├── CommerceService - 商业交易
    ├── NavigationService - 寻路服务
    └── TraitResolver - 特质计算
```

## 三、核心组件详解

### 1. Data层组件

#### CustomerArchetype（顾客原型）
- **作用**：定义顾客的基础属性模板
- **主要属性**：
  - `archetypeId`：唯一标识
  - `baseInterest[]`：对各商品类别的基础兴趣（0-100）
  - `moveSpeed`：移动速度
  - `queueToleranceSeconds`：排队容忍时间
  - `walletCapCurve`：钱包上限曲线（随忠诚度变化）
  - `embarrassmentCapCurve`：尴尬值上限曲线
  - `defaultPolicies`：默认行为策略集

#### Trait（特质）
- **作用**：修饰原型属性，实现个性化
- **修饰方式**：
  - `interestAdd[]`：兴趣值加法修正
  - `interestMul`：兴趣值乘法修正
  - `walletCapMul`：钱包上限倍率
  - `patienceMul`：耐心倍率
  - `priceSensitivityMul`：价格敏感度倍率

#### CustomerPolicies（行为策略）
- **策略类型**：
  - `TargetSelectorPolicy`：选择目标货架
  - `PurchasePolicy`：决定购买数量
  - `QueuePolicy`：排队换线策略
  - `PathPolicy`：重新寻路策略
  - `EmbarrassmentPolicy`：尴尬值计算
  - `CheckoutPolicy`：选择收银台

### 2. Runtime层组件

#### CustomerRecord（顾客记录）
- **持久化数据**：
  - 身份信息（ID、姓名、性取向、外观）
  - 行为基线（原型ID、特质ID列表）
  - 个体兴趣偏移
  - 长期属性（信任值、忠诚度、经验值）
  - 统计数据（访问次数、消费总额）

#### CustomerSession（会话记录）
- **单次访问数据**：
  - 进店时间、离店原因
  - 消费金额、信任值变化
  - 访问的货架列表
  - 路径长度、排队时间

#### CustomerAgent（Unity组件）
- **核心功能**：
  - 初始化顾客实例
  - 计算最终属性（原型+特质+个体偏移）
  - 注入黑板数据
  - 触发事件总线

#### CustomerBlackboardAdapter（黑板适配器）
- **数据桥接**：
  - 存储运行时数据
  - 同步到NodeCanvas黑板（可选）
  - 提供行为树访问接口

### 3. Services层组件

#### CustomerRepository
- **数据管理**：
  - 保存/加载顾客记录
  - 内存缓存（Dictionary）
  - JSON序列化存储

#### CommerceService
- **交易处理**：
  - `SoftReserve()`：软预留商品（不锁库存）
  - `CommitAtCashier()`：结账时扣减库存

#### NavigationService
- **寻路服务**：
  - A*寻路封装
  - 路径请求接口

## 四、Customer与Shelf的交互机制

### 数据关联
1. **兴趣匹配**：Customer的`interestFinal[]`数组索引与`ProductCategory`枚举对齐
2. **吸引力计算**：基于货架等级、商品类别倍率计算
3. **库存交互**：通过`ShelfInstance`的库存管理接口

### 交互流程
```
顾客进店
    ↓
获取所有货架快照（ShelfSnapshot）
    ↓
策略选择目标货架（基于兴趣、价格、吸引力）
    ↓
移动到货架 → 软预留商品
    ↓
前往收银台 → 提交购买（扣库存、扣款）
    ↓
更新会话记录 → 离店
```

## 五、属性计算公式

### 最终兴趣值
```
最终兴趣[i] = (原型基础兴趣[i] + 个体偏移[i] + Σ特质加成[i]) × Π特质倍率
```

### 钱包金额
```
钱包上限 = 个体基线 × 忠诚度曲线值 × Π特质倍率
实际金额 = Random(上限/2, 上限)
```

### 尴尬值上限
```
尴尬上限 = 原型曲线值(忠诚度) × Π特质倍率
```

## 六、创建Customer的完整流程

### 配置阶段
1. **创建CustomerArchetype资产**
   - 菜单：Create → PopLife → Customers → Archetype
   - 配置基础属性和兴趣数组

2. **创建Trait资产**（可选）
   - 菜单：Create → PopLife → Customers → Trait
   - 设置属性修饰值

3. **创建SpawnerProfile资产**
   - 配置原型和特质的生成权重

4. **创建策略资产**（可选）
   - 继承对应的Policy基类
   - 实现自定义决策逻辑

### 运行时流程
```csharp
// 1. 生成器调用
CustomerSpawner.SpawnOne(record, archetype, traits, daySeed)
    ↓
// 2. 实例化预制体
Instantiate(customerPrefab)
    ↓
// 3. 初始化Agent
CustomerAgent.Initialize()
    ├─ 计算最终兴趣（ComposeFinalInterest）
    ├─ 计算特质效果（TraitResolver.Compute）
    ├─ 采样钱包金额
    └─ 注入黑板数据
    ↓
// 4. 行为树开始执行
BehaviourTree.StartBehaviour()
```

## 七、数据配置要求

### 必需配置
- **CustomerPrefab**：包含`CustomerAgent`和`CustomerBlackboardAdapter`组件
- **商品类别数量**：确保兴趣数组长度与`ProductCategory`枚举长度一致
- **生成点位置**：设置`SpawnPoint`Transform

### 可选配置
- **NodeCanvas行为树**：定义AI决策流程
- **自定义策略**：覆盖默认行为策略
- **外观预设**：配置不同的视觉外观

## 八、关键设计优势

1. **数据驱动**：通过ScriptableObject配置，无需修改代码
2. **高度模块化**：策略模式允许灵活替换行为逻辑
3. **性能优化**：软预留机制避免频繁锁定资源
4. **可扩展性**：轻松添加新的原型、特质和策略
5. **持久化支持**：顾客数据可保存/加载，支持长期游戏

## 九、扩展建议

1. **添加更多特质类型**：如"夜猫子"、"打折狂"等
2. **实现动态策略切换**：基于环境条件切换行为模式
3. **增加社交互动**：顾客之间的影响和互动
4. **优化寻路系统**：集成更高级的寻路算法
5. **添加情绪系统**：影响购买决策和行为表现