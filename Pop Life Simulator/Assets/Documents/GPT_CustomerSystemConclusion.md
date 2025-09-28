# 顾客系统概览

## 核心架构
- `Customers/Spawner/CustomerSpawner.cs`: 控制顾客预制体实例化，按 `SpawnerProfile` 权重挑选原型与 Trait 后调用 `CustomerAgent.Initialize`。
- `Customers/Runtime/CustomerAgent.cs`: 负责将 `CustomerRecord` 与 `CustomerArchetype`、Trait 数据融合，注入黑板并通过事件总线广播生成事件。
- `Customers/Runtime/CustomerBlackboardAdapter.cs`: 保存顾客的基线数据与会话状态，行为树/节点系统从这里读写目标货架、排队点等信息。
- 策略接口定义在 `Customers/Data/CustomerPolicies.cs`，`BehaviorPolicySet`（见 `CustomerArchetype`）聚合策略实现以便替换不同顾客行为。

## 数据与服务
- `CustomerRecord`（`Customers/Runtime/CustomerRecord.cs`）保存顾客档案、兴趣偏移与忠诚统计，并提供 `ComposeFinalInterest` 生成最终兴趣分布。
- `CustomerArchetype`（`Customers/Data/CustomerArchetype.cs`）定义外观、移动与各类能力曲线，指定默认策略集合。
- `Trait` 与可选 `TraitHook`（`Customers/Data/Trait.cs`, `Customers/Data/TraitHooks.cs`）提供数值修饰与事件回调；`TraitRegistry`/`TraitResolver` 负责解析与折算 Trait 效果。
- `CustomerRepository`（`Customers/Services/CustomerRepository.cs`）使用 JSON 持久化顾客档案；`CustomerSession` 记录单次来访统计，为后续分析服务。
- `SpawnerProfile`（`Customers/Data/SpawnerProfile.cs`）配置每日访问量范围与顾客/特质权重，为排班或随机生成提供参数。

## 与货架系统的连接
- 策略上下文中的 `ShelfSnapshot`/`CashierSnapshot`（`Customers/Data/CustomerPolicies.cs`）需要货架/收银系统提供库存、品类、排队长度等即时数据。
- `CommerceService`（`Customers/Services/CommerceService.cs`）通过 `SoftReserve` 与 `CommitAtCashier` 与 `ShelfInstance` 交互，完成库存预留与扣减。
- `CustomerEventBus`（`Customers/Services/CustomerEventBus.cs`）广播顾客生成、货架售罄等事件，供货架补货或布局调整监听。
- `QueueService`（`Customers/Services/QueueService.cs`）追踪排队点长度并提供等待时间预测，行为策略可据此决定换队。

## 新增顾客流程
1. 在 `CustomerArchetype` ScriptableObject 中配置新原型：设定 `archetypeId`、外观 preset、曲线与策略集合。
2. 若顾客需要特质，先创建对应 `Trait` 并将资源引用进 `TraitRegistry` 的列表。
3. 创建或更新 `CustomerRecord`：填写唯一 `customerId`、个人兴趣偏移、忠诚/信任等信息；通过 `CustomerRepository.Put`/`Save` 保存。
4. 在运行时由 `CustomerSpawner` 调用 `SpawnOne`，传入档案、原型和 Trait 集合，让 `CustomerAgent` 完成初始化并接入场景行为树。
