# CLAUDE.md

此文件为 Claude Code (claude.ai/code) 提供项目指导和代码规范。

## 重要说明
**所有对话请使用中文进行**
**游戏设计师为中文母语，英文流利；玩家群体用户为英文母语用户，给玩家看的文字都应该是英文**

## 项目概述
Unity 2D 成人商店模拟经营游戏（"Pop Life Simulator"）- 一款以经营成人用品商店为主题的策略模拟游戏。

## Unity 环境配置
- Unity 项目位置：`Pop Life Simulator/`
- Unity 版本：6000.0.46f1
- 构建方式：Unity 编辑器 → File → Build Settings → Build
- 运行模式：Unity 编辑器 Play 按钮或 Ctrl+P
- 第三方框架：NodeCanvas 行为树系统, A* Pathfinding 自动寻路系统

## 项目架构

### 1. 数据层 (Data Layer) - `Assets/Scripts/Data/`
负责定义游戏中所有的数据模板和配置。

#### 建筑系统
- **`BuildingArchetypes.cs`**：所有建筑的基础原型系统
  - 支持多级建筑进阶
  - 定义建筑占地面积、旋转规则
  - 包含升级路径配置

- **`ShelfArchetypes.cs`**：商品货架原型
  - 按商品类别区分（内衣、避孕套、振动器、飞机杯、润滑剂）
  - 定义价格、库存、吸引力等属性
  - 支持等级提升系统

- **`FacilityArchetype.cs`**：设施原型
  - 特殊功能设施（收银台、空调、ATM、监控、音乐播放器）
  - 效果系统（减少尴尬、增加吸引力、加快顾客速度、恢复金钱）

#### 枚举定义
```csharp
ProductCategory { Lingerie, Condom, Vibrator, Fleshlight, Lubricant, BDSM }
FacilityType { Cashier, AirConditioner, ATM, SecurityCamera, MusicPlayer }
EffectType { ReduceEmbarrassment, IncreaseAttractiveness, IncreaseCustomerSpeed, RestoreMoney }
```

### 2. 运行时层 (Runtime Layer) - `Assets/Scripts/Runtime/`
处理游戏运行时的所有实例和逻辑。

#### 核心系统
- **`FloorGrid.cs`**：网格建筑系统
  - 基于2D网格的建筑放置
  - 碰撞检测与验证
  - 支持自定义原点偏移
  - 尴尬值热图计算

- **`ConstructionManager.cs`**：建造管理器
  - 放置/移动/拆除模式切换
  - 建筑预览与验证
  - 事务式操作（失败自动回滚）

- **`BuildingInstances.cs`**：建筑实例管理
- **`ShelfInstance.cs`**：货架实例（库存管理）
- **`FacilityInstance.cs`**：设施实例（效果应用）

### 3. 顾客系统 (Customer System) - `Assets/Scripts/Customers/`
完整的顾客AI和行为模拟系统。

#### 数据层 (`Customers/Data/`)
- **`CustomerArchetype.cs`**：顾客原型模板
  - 基础兴趣数组（对应5种商品类别）
  - 移动速度、排队容忍度
  - 钱包/尴尬值曲线（随忠诚度变化）
  - 默认行为策略集

- **`Trait.cs`**：特质系统
  - 修饰原型属性（兴趣加成、倍率修正）
  - 影响钱包容量、耐心、价格敏感度
  - 支持40+种特质（性别、性向、职业、性格等）

- **`CustomerPolicies.cs`**：行为策略抽象
  - `TargetSelectorPolicy`：选择目标货架
  - `PurchasePolicy`：决定购买数量
  - `QueuePolicy`：排队换线策略
  - `PathPolicy`：重新寻路策略
  - `EmbarrassmentPolicy`：尴尬值计算
  - `CheckoutPolicy`：选择收银台

- **`SpawnerProfile.cs`**：生成配置
  - 原型和特质的生成权重
  - 每日生成规则

#### 运行时层 (`Customers/Runtime/`)
- **`CustomerRecord.cs`**：顾客持久化数据
  - 身份信息（ID、姓名、性取向、外观）
  - 长期属性（信任值、忠诚度、经验）
  - 访问统计（次数、消费总额）

- **`CustomerSession.cs`**：单次访问会话
  - 进店/离店时间和原因
  - 消费记录、路径数据
  - 排队时间统计

- **`CustomerAgent.cs`**：Unity组件
  - 初始化顾客实例
  - 计算最终属性（原型+特质+个体偏移）
  - 触发事件总线

- **`CustomerBlackboardAdapter.cs`**：NodeCanvas适配器
  - 桥接运行时数据到行为树黑板
  - 支持动态数据同步

#### 服务层 (`Customers/Services/`)
- **`CustomerRepository.cs`**：数据存储服务
- **`CommerceService.cs`**：商业交易服务（软预留/提交购买）
- **`NavigationService.cs`**：A*寻路封装
- **`TraitResolver.cs`**：特质效果计算
- **`CustomerEventBus.cs`**：事件总线系统
- **`QueueService.cs`**：排队管理
- **`HeatmapService.cs`**：尴尬值热图服务

#### 生成器 (`Customers/Spawner/`)
- **`CustomerSpawner.cs`**：顾客生成控制器

### 4. 管理器层 (Manager Layer) - `Assets/Scripts/Manager/`
游戏全局管理器（单例模式）。

- **`FloorManager.cs`**：楼层管理（当前单层原型）
- **`BlueprintManager.cs`**：蓝图解锁系统
- **`CategoryManager.cs`**：商品类别升级管理
- **`ResourceManager.cs`**：资源管理（金钱、名声）
- **`EffectManager.cs`**：全局效果管理
- **`AudioManager.cs`**：音效管理
- **`UIManager.cs`**：UI界面管理

## 核心游戏机制

### 顾客AI决策流程
```
1. 进店 → 基于兴趣和吸引力选择目标货架
2. 移动到货架 → 排队等待 → 软预留商品
3. 移动到收银台 → 排队结账 → 扣减库存和金钱
4. 更新信任值 → 离店
```

### 属性计算公式
- **最终兴趣**：`(原型基础 + 个体偏移 + Σ特质加成) × Π特质倍率`
- **钱包金额**：`基线 × 忠诚度曲线值 × Π特质倍率`
- **尴尬上限**：`原型曲线值(忠诚度) × Π特质倍率`

### 时间系统
- 游戏内一天 = 现实30秒
- 营业时间：12:00 - 23:00
- 每日结算：23:00自动结算收支

## 第三方组件

### NodeCanvas 行为树系统
位置：`Assets/ThirdParty/ParadoxNotion/`

- **BehaviourTree**：行为树组件，控制顾客AI决策
- **Blackboard**：黑板系统，存储运行时变量
- **FSM**：状态机支持（可选）
- **DialogueTree**：对话树系统（未来扩展）

使用方式：
1. 顾客预制体包含 BehaviourTree 组件
2. CustomerBlackboardAdapter 负责数据同步
3. 自定义 Action/Condition 节点扩展行为

## 开发规范

### 命名空间
- 核心系统：`PopLife`、`PopLife.Data`、`PopLife.Runtime`
- 顾客系统：`PopLife.Customers.Data`、`PopLife.Customers.Runtime`、`PopLife.Customers.Services`
- 编辑器：`PopLife.Editor`

### 文件组织
- ScriptableObject 资产放在 `Assets/ScriptableObjects/`
- 预制体放在 `Assets/Prefab/`
- 场景文件放在 `Assets/Scenes/`

### 编码约定
- 代码文件使用英文命名
- 注释可使用中文
- 遵循 C# 命名规范（PascalCase/camelCase）
- 使用 ScriptableObject 进行数据配置

### 忽略文件
- `.meta` 文件：Unity 自动生成的元数据
- `Assets/ThirdParty/`：第三方包，不需要修改

## 关键设计模式

1. **原型-实例模式**：ScriptableObject 定义模板，运行时创建实例
2. **策略模式**：可替换的行为策略，灵活控制AI决策
3. **事件总线**：解耦系统间通信
4. **软预留机制**：避免频繁锁定资源，提升性能
5. **数据驱动**：通过配置而非代码控制游戏行为

## 扩展建议

1. **新增商品类别**：扩展 ProductCategory 枚举，添加对应货架原型
2. **新增特质**：创建 Trait ScriptableObject，配置属性修饰
3. **自定义策略**：继承 Policy 基类，实现特殊行为逻辑
4. **增加楼层**：扩展 FloorManager 支持多层管理
5. **社交系统**：利用事件总线实现顾客间互动

## 调试技巧

1. 使用 HeatmapService 可视化尴尬值分布
2. CustomerSession 记录详细的顾客行为日志
3. 通过 CustomerEventBus 监听关键事件
4. NodeCanvas 的 Graph Console 查看行为树执行状态

## 性能优化要点

1. 使用对象池管理顾客实例
2. 批量处理寻路请求
3. 缓存常用的计算结果
4. 合理设置 LOD 和剔除距离