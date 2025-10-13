# CLAUDE.md

此文件为 Claude Code (claude.ai/code) 提供项目指导和代码规范。

## 重要说明
**所有对话请使用中文进行**
**游戏设计师为中文母语，英文流利；玩家群体用户为英文母语用户，给玩家看的文字都应该是英文**
**产出任何文件后，都不要为其创建.meta文件，该步骤unity会自己处理，不需要你来处理**

## 项目概述
Unity 2D 成人商店模拟经营游戏（"Pop Life Simulator"）- 一款以经营成人用品商店为主题的策略模拟游戏。

### 核心玩法
- **商店建造**：在网格化楼层上放置货架和设施
- **顾客AI**：基于NodeCanvas行为树的智能顾客系统
- **经营管理**：每日营业→结算→扩张的游戏循环
- **资源系统**：金钱（Money）和声望（Fame）双货币

## Unity 环境配置
- Unity 项目位置：`Pop Life Simulator/`
- Unity 版本：6000.0.58f2 (Unity 6)
- 构建方式：Unity 编辑器 → File → Build Settings → Build
- 运行模式：Unity 编辑器 Play 按钮或 Ctrl+P
- 第三方框架：
  - **NodeCanvas**：行为树AI系统（位于 `Assets/ThirdParty/ParadoxNotion/NodeCanvas/`）
  - **A* Pathfinding**：自动寻路系统

## 项目架构

### 架构分层总览
```
┌─────────────────────────────────────────────────┐
│             Manager Layer (管理器层)             │
│    DayLoopManager | FloorManager | UIManager    │
│         ResourceManager | EffectManager          │
└───────────────────┬─────────────────────────────┘
                    │
┌───────────────────▼─────────────────────────────┐
│           Service Layer (服务层)                │
│  CommerceService | TraitResolver | QueueService │
│  CustomerRepository | NavigationService         │
└───────────────────┬─────────────────────────────┘
                    │
┌───────────────────▼─────────────────────────────┐
│          Runtime Layer (运行时层)                │
│  BuildingInstance | ShelfInstance | CustomerAgent│
│  FloorGrid | ConstructionManager                │
└───────────────────┬─────────────────────────────┘
                    │
┌───────────────────▼─────────────────────────────┐
│           Data Layer (数据层)                    │
│  BuildingArchetype | CustomerArchetype | Trait  │
│  Policy ScriptableObjects                       │
└─────────────────────────────────────────────────┘
```

### 1. 数据层 (Data Layer) - `Assets/Scripts/Data/`
**职责**：定义所有ScriptableObject数据模板，驱动游戏逻辑

#### 建筑系统
- **`BuildingArchetypes.cs`** (81行)
  - 建筑基类 `BuildingArchetype`（抽象ScriptableObject）
  - 多级升级系统（`BuildingLevelData[]`）
  - 占地模板（`footprintPattern`：支持不规则形状）
  - 旋转逻辑（4方向：0/90/180/270度）
  - 关键属性：buildCost, maintenanceFee, blueprintCost

- **`ShelfArchetypes.cs`** (27行)
  - 继承自 `BuildingArchetype`
  - 商品货架数据：价格、库存上限、吸引力
  - 按 `ProductCategory` 区分（6种类别）

- **`FacilityArchetype.cs`** (34行)
  - 设施原型（收银台、空调、ATM等）
  - 效果系统 `FacilityEffect[]`：
    - 类型：`EffectType` 枚举
    - 范围：全楼层/指定半径
    - 数值：减少尴尬值、增加吸引力等
  - 自定义放置验证（如收银台需要靠墙）

#### 核心枚举定义
```csharp
// 6种商品类别
ProductCategory { Lingerie, Condom, Vibrator, Fleshlight, Lubricant, BDSM }

// 设施类型
FacilityType { Cashier, AirConditioner, ATM, SecurityCamera, MusicPlayer }

// 效果类型
EffectType {
    ReduceEmbarrassment,      // 减少尴尬值
    IncreaseAttractiveness,   // 增加吸引力
    IncreaseCustomerSpeed,    // 加快顾客速度
    RestoreMoney              // 恢复金钱（ATM）
}
```

### 2. 运行时层 (Runtime Layer) - `Assets/Scripts/Runtime/`
**职责**：处理游戏运行时的实例管理和核心逻辑

#### 网格系统（核心）
- **`FloorGrid.cs`** (421行) ⭐
  - **2D网格建筑系统**
  - 数据结构：`Cell[,] grid` 记录占用状态
  - 放置规则：
    - 建筑必须至少一格在第一层（y=0）
    - 第一层被占用的列，整列禁止建造
    - 支持不规则占地模板和4方向旋转
  - 功能：世界坐标↔网格坐标转换、碰撞检测、尴尬值热图计算
  - 关键方法：
    - `PlaceBuildingTransactional()` - 事务式放置
    - `IsCellAvailable()` - 检查格子可用性
    - `RegisterExistingBuilding()` - 注册已有建筑（用于移动）

- **`ConstructionManager.cs`** (484行) ⭐
  - **建造交互管理器**
  - 三种模式：`None / Place / Move`
  - 实时预览系统（绿色=可建造，红色=不可）
  - 楼层切换（Tab键循环/数字键直达）
  - 跨楼层移动支持（成本×2）
  - 流程：
    ```
    Place模式：选择建筑 → 预览 → 验证资源 → 放置 → 扣费
    Move模式：选择建筑 → 预览新位置 → 同/跨楼层移动 → 扣费
    ```

#### 建筑实例
- **`BuildingInstances.cs`** (104行)
  - 建筑实例基类
  - 序列化系统（`BuildingSaveData`）
  - 升级逻辑（消耗声望解锁下一等级）
  - 维护费用计算

- **`ShelfInstance.cs`** (128行)
  - 货架运行时状态：库存、销量、价格
  - `TryTakeOne()` - 取货（仅扣库存，不增加金钱）
  - 集成排队系统（`ShelfQueueController`）
  - 吸引力计算：`等级 × 品类系数`

- **`FacilityInstance.cs`** (55行)
  - 设施实例（收银台、空调等）
  - 效果注册/注销到 `EffectManager`
  - 收银台队列管理（`CashierQueueController`）

#### 其他
- **`ExitPoint.cs`** (49行) - 商店出口标记点
- **`CameraController.cs`** - 相机控制

### 3. 顾客系统 (Customer System) - `Assets/Scripts/Customers/`
**职责**：完整的顾客AI、数据持久化、行为策略系统

#### 数据层 (`Customers/Data/`)
- **`CustomerArchetype.cs`** (88行) ⭐
  - **顾客原型模板**（ScriptableObject）
  - 基础兴趣数组（6种商品类别）
  - 移动速度、排队容忍度
  - 曲线系统（随忠诚度变化）：
    - `walletCapCurve` - 钱包容量曲线
    - `patienceCurve` - 耐心曲线
    - `embarrassmentCapCurve` - 尴尬上限曲线
  - 时间窗口：`spawnTimeWindow`（如上班族12-22点生成）
  - 默认策略集：`defaultPolicies`（引用BehaviorPolicySet）

- **`Trait.cs`** (29行)
  - **特质修饰系统**（ScriptableObject）
  - 属性修正：
    - 兴趣加成：`interestAdditive[6]`（如 Gay: Condom+1, Lubricant+2）
    - 兴趣倍率：`interestMultipliers[6]`（如 Asexual: Condom×0）
    - 钱包/耐心/价格敏感度倍率
  - 时间倾向：`preferredTimeRanges[]`（如 Night Owl: 20-23点）
  - 特质权重：`timePreferenceWeight`（时间匹配时增加生成权重）
  - **已实现40+种特质**：性别、性向、职业、性格、关系状态等

- **`CustomerPolicies.cs`** (68行) ⭐ **策略模式核心**
  - 定义6种抽象策略基类：
    ```csharp
    TargetSelectorPolicy    // 选择目标货架
    PurchasePolicy          // 决定购买数量
    QueuePolicy             // 排队换线策略
    PathPolicy              // 重新寻路策略
    EmbarrassmentPolicy     // 尴尬值计算
    CheckoutPolicy          // 选择收银台
    ```
  - 快照数据结构：
    - `CustomerContext` - 顾客当前状态
    - `ShelfSnapshot` - 货架快照（含队列长度）
    - `CashierSnapshot` - 收银台快照

- **`Policies/WeightedRandomSelector.cs`** (198行) ⭐
  - **加权随机货架选择策略**（实现TargetSelectorPolicy）
  - 得分计算公式：
    ```
    货架得分 = 兴趣匹配 × interestWeight
             + 吸引力 × attractivenessWeight
             - 队列惩罚(曲线) × queuePenaltyWeight
             - 距离惩罚 × distanceWeight
    ```
  - 筛选条件：库存>0、队列<上限、兴趣>阈值、**未购买过该archetype**
  - 队列惩罚曲线：AnimationCurve（可自定义衰减）

- **`SpawnerProfile.cs`** (138行)
  - **运行时可编辑的顾客解锁配置**
  - 保存位置：`StreamingAssets/SpawnerProfile.json` 或 `persistentDataPath`
  - 数据：解锁顾客ID列表（`unlockedCustomerIDs`）
  - 支持批量解锁/锁定

#### 运行时层 (`Customers/Runtime/`)
- **`CustomerRecord.cs`** (105行) ⭐
  - **顾客持久化数据**（JSON序列化）
  - 身份信息：ID、姓名、外貌ID、性取向
  - 原型与特质引用：`archetypeId`, `traitIds[]`
  - 个体化兴趣偏移：`interestPersonalDelta[6]`（使每个顾客独特）
  - 长期属性：信任值、忠诚度、经验值
  - 统计数据：访问次数、消费总额、最后访问原因
  - **最终兴趣计算公式**：
    ```csharp
    最终兴趣 = (原型基础 + 个体偏移 + Σ特质加成) × Π特质倍率
    ```

- **`CustomerSession.cs`** (34行)
  - **单次访问会话数据**
  - 时间戳：进店时间、离店时间、离店原因
  - 消费明细：货架访问列表（`ShelfVisit`）
  - 统计：总消费、排队时间、路径长度

- **`CustomerAgent.cs`** (70行) ⭐
  - **Unity组件**（挂载在顾客GameObject上）
  - `Initialize(CustomerRecord)` - 初始化入口
  - 计算最终属性（原型+特质+个体偏移）：
    - 采样钱袋金额：`Random(walletCap/2, walletCap)`
    - 采样尴尬上限：`embarrassmentCapCurve.Evaluate(loyaltyLevel)`
  - 设置外貌Sprite（从AppearanceDatabase）
  - 触发生成事件：`CustomerEventBus.RaiseCustomerSpawned()`

- **`CustomerBlackboardAdapter.cs`** (73行) ⭐ **NodeCanvas桥接器**
  - **桥接运行时数据到行为树黑板**
  - 会话状态变量：
    - `moneyBag` - 当前钱包余额
    - `embarrassmentLevel` - 当前尴尬值
    - `targetShelfId` - 目标货架ID
    - `targetCashierId` - 目标收银台ID
    - `goalCell` - 目标网格坐标（A*寻路）
    - `purchaseQuantity` - 购买数量
    - `pendingPayment` - 待结账金额
    - `purchasedArchetypes` - 已购买的archetype列表
    - `assignedQueueSlot` - 队列位置引用

- **`CustomerInteraction.cs`** (169行) ⭐ **交互逻辑核心**
  - `TryPurchase()` - 在货架购买
    ```csharp
    1. 查找targetShelfId对应的ShelfInstance
    2. 调用shelf.TryTakeOne() // 扣减库存
    3. 扣减顾客钱包: moneyBag -= price
    4. 累加待结账金额: pendingPayment += price
    5. ⚠️ 玩家金钱不变（延迟到收银台）
    ```
  - `TryCheckout()` - 在收银台结算
    ```csharp
    1. 检查pendingPayment > 0
    2. 记录销售额: DayLoopManager.RecordSale()
    3. 增加玩家金钱: ResourceManager.AddMoney()
    4. 清空待结账: pendingPayment = 0
    ```

#### 服务层 (`Customers/Services/`)
- **`CustomerRepository.cs`** (74行)
  - **数据持久化服务**
  - 保存/加载 `StreamingAssets/Customers.json`
  - 使用 `SavePathManager` 管理路径

- **`CommerceService.cs`** (51行)
  - **软预留机制**（不锁库存，仅返回可买量）
  - `GetAvailableStock()` - 查询可用库存
  - `CommitPurchase()` - 真正扣减库存（在收银台）

- **`TraitResolver.cs`** (36行)
  - **特质效果计算**
  - `Compute(traits)` → 返回 `EffectiveStats`
  - 累乘所有特质的倍率效果

- **`CustomerEventBus.cs`** (32行) ⭐ **事件总线**
  - 事件类型：
    - `OnCustomerSpawned` - 顾客生成
    - `OnCustomerPurchased` - 购买商品
    - `OnCustomerCheckedOut` - 结账完成
    - `OnCustomerReachedShelf` - 到达货架
    - `OnCustomerReachedCashier` - 到达收银台

- **`CustomerContextBuilder.cs`** (304行) ⭐ **快照构建器**
  - 将运行时实例转换为策略所需的快照
  - `BuildCustomerContext(adapter)` → `CustomerContext`
  - `BuildShelfSnapshot(shelf)` → `ShelfSnapshot`（含队列长度）
  - 支持多楼层坐标转换

- **`TimeBasedSpawnFilter.cs`** (187行) ⭐ **时间过滤器**
  - 根据游戏时间筛选符合条件的顾客
  - 硬过滤：检查Archetype的生成时间窗口
  - 软加成：应用Trait的时间倾向（如夜猫子晚间权重×2）
  - 返回加权顾客列表（`WeightedCustomer[]`）

- **`ShelfQueueController.cs`** (345行) ⭐ **货架排队系统**
  - 管理单个货架的顾客队列
  - 队列位置分配：
    - 位置0 = 队首（`interactionAnchor`）
    - 位置1+ = 预设队位（`queueSlots[]`）或动态计算
  - 队列前移通知（自动更新A*目标）
  - Gizmo可视化调试

- **`CashierQueueController.cs`** (283行)
  - 收银台排队系统（逻辑同ShelfQueueController）
  - 预测结账等待时间

- **`QueueService.cs`**, **`HeatmapService.cs`**, **`NavigationService.cs`** - 工具服务

#### 生成器 (`Customers/Spawner/`)
- **`CustomerSpawner.cs`** (625行) ⭐⭐ **顾客生成核心**
  - **手动生成**：通过ID生成指定顾客（调试）
  - **自动生成**：营业时段自动生成
  - 流程：
    ```
    1. 开店时初始化顾客池（从SpawnerProfile加载解锁ID）
    2. 使用TimeBasedSpawnFilter筛选符合时间条件的顾客
    3. 过滤已在场顾客（避免重复）
    4. 加权随机选择
    5. 实例化CustomerAgent并初始化
    6. 安排下次生成时间（基础间隔 + 随机抖动）
    ```
  - 流量控制：场上人数上限、随机间隔
  - 资源加载：动态加载Archetype和Trait ScriptableObject

#### NodeCanvas集成 (`Customers/NodeCanvas/`)
**自定义行为树节点**（10个Actions + 1个Condition）

- **Actions**:
  - `SelectTargetShelfAction` - 使用策略选择目标货架
  - `ExecutePurchaseAction` - 逐件购买（循环调用TryPurchase）
  - `ExecuteCheckoutAction` - 结账
  - `AcquireQueueSlotAction` - 获取队列位置
  - `ReleaseQueueSlotAction` - 释放队列位置
  - `MoveToTargetAction` - 移动到目标（配合A*）
  - `SelectCashierAction` - 选择收银台
  - `SelectExitPointAction` - 选择出口
  - `DecidePurchaseAction` - 决定购买数量
  - `DestroyAgentAction` - 销毁顾客

- **Conditions**:
  - `IsAtFrontOfQueueCondition` - 判断是否在队首

### 4. 管理器层 (Manager Layer) - `Assets/Scripts/Manager/`
**职责**：全局单例管理器，控制游戏核心系统

- **`DayLoopManager.cs`** (265行) ⭐⭐ **时间与游戏循环核心**
  - **两个阶段**：
    - `BuildPhase`（6:00）：建造阶段，时间暂停
    - `OpenPhase`（12:00-23:00）：营业阶段，时间流动
  - 时间流速：现实30秒 = 游戏1天（可配置）
  - **每日结算**：
    - 计算销售额、维护费用、净收入
    - 计算声望奖励
    - 检查破产条件（Money < 0）
  - **事件系统**：
    ```csharp
    OnBuildPhaseStart    // 建造阶段开始
    OnStoreOpen          // 开店（触发CustomerSpawner）
    OnStoreClose         // 关店
    OnDailySettlement    // 每日结算
    OnBankruptcy         // 破产
    ```
  - 统计记录：每日销售额、访问人数

- **`FloorManager.cs`** (403行) ⭐ **楼层管理**
  - 管理多个FloorGrid实例
  - 楼层激活/停用
  - 楼层切换（Tab键循环/数字键直达）
  - 自动分配楼层ID（避免冲突）
  - 楼层数据校验与清理

- **`ResourceManager.cs`** (17行)
  - 金钱和声望管理
  - `AddMoney()`, `SpendMoney()`, `AddFame()`

- **`BlueprintManager.cs`** (14行)
  - 蓝图解锁系统（原型期简化实现）

- **`CategoryManager.cs`** (13行)
  - 商品类别升级管理（原型期简化）

- **`EffectManager.cs`** (16行)
  - 全局效果管理（设施效果注册/注销）

- **`UIManager.cs`** (57行)
  - UI面板管理
  - 监听结算事件，显示结算面板

- **`ExitPointManager.cs`**, **`AudioManager.cs`** - 其他管理器

### 5. UI层与工具 - `Assets/Scripts/UI/` & `Assets/Scripts/Utility/`
- **UI组件**：
  - `ResourceDisplay.cs` - 资源显示（金钱/声望）
  - `DailySettlementPanel.cs` - 每日结算面板
  - `BankruptcyPanel.cs` - 破产面板
  - `DayCounterUI.cs` - 天数计数器
  - `ScreenLogger.cs` - 屏幕日志

- **工具类**：
  - `SavePathManager.cs` - 保存路径管理（StreamingAssets/persistentDataPath）

### 6. 编辑器扩展 - `Assets/Scripts/Editor/`
- `FloorGridDebugger.cs` - 网格调试器
- `FloorEntryDrawer.cs` - 楼层条目属性绘制
- `InterestArrayPropertyDrawer.cs` - 兴趣数组编辑器
- `TraitInterestPropertyDrawer.cs` - 特质兴趣编辑器
- `CustomerRecordsEditor.cs` - 顾客记录编辑器

---

## 核心游戏机制

### 顾客AI决策流程（完整）
```
【生成阶段】
1. DayLoopManager触发OnStoreOpen事件
2. CustomerSpawner开始自动生成
   - TimeBasedSpawnFilter筛选符合时间条件的顾客
   - 加权随机选择 → 实例化CustomerAgent

【行为树执行】
3. SelectTargetShelfAction（策略选择货架）
   - 使用WeightedRandomSelector计算得分
   - 选择最优货架 → 更新targetShelfId和goalCell

4. MoveToTargetAction（移动到货架）
   - 配合A* Pathfinding寻路

5. AcquireQueueSlotAction（获取队列位置）
   - 调用ShelfQueueController.RequestSlot()

6. IsAtFrontOfQueueCondition（等待队首）
   - 循环检查assignedQueueSlot.Position == 0

7. ExecutePurchaseAction（购买商品）
   - 调用CustomerInteraction.TryPurchase()
   - 扣减库存、钱包，累加pendingPayment
   - ⚠️ 玩家金钱不变

8. ReleaseQueueSlotAction（释放队列）

9. SelectCashierAction（策略选择收银台）

10. MoveToTargetAction（移动到收银台）

11. AcquireQueueSlotAction（收银台排队）

12. ExecuteCheckoutAction（结账）
    - 调用CustomerInteraction.TryCheckout()
    - ✅ 玩家金钱增加、记录销售额

13. SelectExitPointAction（选择出口）

14. MoveToTargetAction（移动到出口）

15. DestroyAgentAction（离店）
    - 更新CustomerRecord统计
    - 销毁GameObject
```

### 属性计算公式
- **最终兴趣**：`(原型基础 + 个体偏移 + Σ特质加成) × Π特质倍率`
  - 示例：Gay顾客对Condom的兴趣 = (原型2 + 个体0.5 + Gay特质+1) × 1.0 = 3.5
- **钱包金额**：`walletCapBase × 原型曲线(忠诚度) × Π特质倍率`
  - 实际采样范围：`[钱包容量/2, 钱包容量]`
- **尴尬上限**：`原型曲线(忠诚度) × Π特质倍率`
  - 示例：Shy特质会 × 0.8（更容易尴尬）
- **移动速度**：`原型速度 × Π特质倍率`
- **排队容忍**：`原型容忍秒数 × Π特质倍率`

### 时间系统
- **游戏时间**：现实30秒 = 游戏1天（可配置 `dayDurationInSeconds`）
- **营业时间**：12:00 - 23:00（`storeOpenHour` - `storeCloseHour`）
- **建造阶段**：6:00（`buildPhaseStartHour`）
- **每日结算**：23:00关店后自动触发
  - 计算公式：
    ```
    dailyIncome = dailySales - totalMaintenanceFee
    fameEarned = f(dailyIncome, customerCount)
    ```
  - 破产检测：`if (money < 0) → OnBankruptcy`

### 建筑放置规则
1. **网格约束**：
   - 建筑必须至少一格在第一层（y=0）
   - 第一层被占用的列，整列禁止建造（模拟重力）
   - 支持不规则占地模板（`footprintPattern`）

2. **事务式操作**：
   ```
   PlaceBuildingTransactional():
   1. 验证位置可用
   2. 检查蓝图和资源
   3. 扣减资源
   4. try { 实例化建筑 → 标记占用 }
      catch { 回滚资源（返还金钱和蓝图）}
   ```

3. **移动规则**：
   - 同楼层移动：收取移动费
   - 跨楼层移动：移动费 × 2

### 排队系统
- **队列位置**：
  - 位置0 = 队首（`interactionAnchor`）
  - 位置1+ = 预设队位或动态计算
- **队列前移**：
  ```
  顾客离开时 → 重新分配所有队位 → 更新A*寻路目标
  ```
- **RVO兼容**：设置到达距离0.8f，允许局部避障偏移

---

## 第三方组件

### NodeCanvas 行为树系统
位置：`Assets/ThirdParty/ParadoxNotion/NodeCanvas/`

- **核心组件**：
  - `BehaviourTree` - 行为树组件（挂载在顾客GameObject上）
  - `Blackboard` - 黑板系统（存储运行时变量）
  - `FSM` - 状态机支持（可选）
  - `DialogueTree` - 对话树系统（未来扩展）

- **使用方式**：
  1. 顾客预制体包含 `BehaviourTree` 组件
  2. `CustomerBlackboardAdapter` 桥接运行时数据到黑板
  3. 自定义 Action/Condition 节点扩展行为
  4. 行为树资产：`Assets/CustomerBehaviorTree.asset`

- **自定义节点位置**：`Assets/Scripts/Customers/NodeCanvas/`

### A* Pathfinding 自动寻路
- **集成方式**：通过 `NavigationService` 封装
- **关键组件**：
  - `AIDestinationSetter` - 设置寻路目标
  - `AIPath` - 执行移动
- **队列集成**：队列位置变化时自动更新目标点

---

## 开发规范

### 命名空间规范
```csharp
PopLife                           // 核心系统
PopLife.Data                      // 数据层
PopLife.Runtime                   // 运行时层
PopLife.Customers.Data            // 顾客数据
PopLife.Customers.Runtime         // 顾客运行时
PopLife.Customers.Services        // 顾客服务
PopLife.Customers.Policies        // 顾客策略
PopLife.Editor                    // 编辑器扩展
```

### 文件组织
```
Assets/
├── Resources/
│   └── ScriptableObjects/        # SO资产
│       ├── BuildingArchetype/
│       ├── CustomerArchetypes/
│       ├── Traits/
│       └── BehaviorPolicies/
├── Prefab/                       # 预制体
│   ├── Customer.prefab
│   └── Buildings/
├── Scenes/                       # 场景文件
├── StreamingAssets/              # 运行时可编辑数据
│   ├── Customers.json
│   └── SpawnerProfile.json
├── Scripts/                      # 所有C#脚本
└── ThirdParty/                   # 第三方包（不修改）
```

### 编码约定
- **命名规范**：
  - 类名/方法名：PascalCase
  - 字段/变量：camelCase
  - 私有字段：camelCase（不使用下划线前缀）
  - 常量：PascalCase
- **注释**：
  - 代码文件使用英文命名
  - 注释可使用中文（便于团队沟通）
  - 复杂逻辑必须添加注释
- **数据配置**：优先使用 ScriptableObject
- **序列化**：使用 `[SerializeField]` 而非 public 字段

### Git忽略规则
- `.meta` 文件：Unity 自动生成的元数据（已在.gitignore）
- `Assets/ThirdParty/`：第三方包（不需要修改）
- `Library/`, `Temp/`, `Logs/` - Unity临时文件

---

## 关键设计模式

### 1. 原型-实例模式 (Prototype-Instance)
- **原型**：ScriptableObject（`BuildingArchetype`, `CustomerArchetype`, `Trait`）
- **实例**：MonoBehaviour（`BuildingInstance`, `CustomerAgent`）
- **优势**：数据与逻辑分离，支持运行时修改原型影响所有实例

### 2. 策略模式 (Strategy)
- **应用**：顾客AI决策系统（6种可替换策略）
- **实现**：
  ```csharp
  抽象基类：TargetSelectorPolicy, PurchasePolicy, CheckoutPolicy...
  具体实现：WeightedRandomSelector, RandomPurchasePolicy...
  ```
- **优势**：AI行为高度可配置，支持热插拔

### 3. 事件总线模式 (Event Bus)
- **实现**：`CustomerEventBus`, `DayLoopManager` 事件系统
- **事件类型**：`OnCustomerSpawned`, `OnStoreOpen`, `OnDailySettlement`...
- **优势**：解耦系统间通信，避免硬编码依赖

### 4. 适配器模式 (Adapter)
- **应用**：`CustomerBlackboardAdapter`
- **功能**：桥接运行时数据到NodeCanvas黑板系统
- **优势**：分离业务逻辑与第三方框架

### 5. 快照模式 (Snapshot)
- **应用**：`CustomerContext`, `ShelfSnapshot`, `CashierSnapshot`
- **功能**：创建不可变数据快照供策略使用
- **优势**：确保决策时数据一致性

### 6. 工厂模式 (Factory)
- **应用**：`CustomerSpawner`
- **功能**：根据配置生成不同类型的顾客实例

### 7. 单例模式 (Singleton)
- **应用**：所有Manager（`DayLoopManager`, `ResourceManager`, etc.）
- **实现**：`public static XxxManager Instance`

### 8. 状态模式 (State)
- **应用**：`DayLoopManager` 的游戏阶段（BuildPhase / OpenPhase）
- **实现**：枚举+状态切换逻辑

### 9. 软预留机制 (Soft Reservation)
- **应用**：`CommerceService`
- **功能**：查询库存时不锁定，提交时才扣减
- **优势**：避免频繁锁定资源，提升性能

### 10. 数据驱动设计 (Data-Driven)
- **核心**：所有配置使用ScriptableObject
- **优势**：通过数据而非代码控制游戏行为，支持热更新

---

## 扩展指南

### 1. 新增商品类别
```csharp
// 1. 扩展枚举
public enum ProductCategory {
    Lingerie, Condom, Vibrator, Fleshlight, Lubricant, BDSM,
    NewCategory  // 新类别
}

// 2. 创建ShelfArchetype ScriptableObject
// 3. 调整所有InterestArray大小（确保与枚举长度一致）
// 4. 更新CategoryManager解锁逻辑
```

### 2. 新增特质
```csharp
// 1. 创建 Trait ScriptableObject
// 2. 配置属性修饰（兴趣加成/倍率）
// 3. 可选：设置时间倾向（preferredTimeRanges）
// 4. 添加到顾客的 traitIds 数组
```

### 3. 自定义策略
```csharp
// 1. 继承对应的 XxxPolicy 基类
public class MyTargetSelector : TargetSelectorPolicy {
    public override int SelectTargetShelf(
        CustomerContext ctx, ShelfSnapshot[] candidates) {
        // 实现自定义决策逻辑
        return bestShelfIndex;
    }
}

// 2. 创建 ScriptableObject 实例
// 3. 分配到 CustomerArchetype.defaultPolicies
```

### 4. 增加楼层
```csharp
// 1. 创建新的 FloorGrid GameObject
// 2. 设置唯一 floorId
// 3. 添加到 FloorManager.floors 列表
// 4. 启用 autoAssignFloorIds（自动分配ID）
```

### 5. 新增设施效果
```csharp
// 1. 扩展 EffectType 枚举
public enum EffectType {
    ReduceEmbarrassment,
    // ... existing
    NewEffect  // 新效果
}

// 2. 在 FacilityArchetype 中配置新效果
// 3. 在 EffectManager 中实现效果逻辑
```

### 6. 社交系统（建议）
- 利用 `CustomerEventBus` 实现顾客间互动
- 创建 `SocialInteraction` 服务
- 定义交互策略（如朋友一起购物、情侣同行）

---

## 调试技巧

### 1. 可视化工具
- **HeatmapService**：可视化尴尬值分布（网格叠加层）
- **FloorGridDebugger**：显示网格占用状态和建筑ID
- **ShelfQueueController Gizmo**：显示队列位置（Scene视图）

### 2. 日志系统
- **CustomerSession**：记录详细行为日志（进店时间、购买明细、离店原因）
- **ScreenLogger**：屏幕日志显示（运行时调试）
- **CustomerEventBus**：监听关键事件（生成、购买、结账）

### 3. 行为树调试
- **NodeCanvas Graph Console**：查看行为树执行状态
- **Blackboard Inspector**：实时查看黑板变量
- **断点调试**：在自定义Action节点设置断点

### 4. 性能分析
- **Profiler**：分析CPU/内存占用
- **Frame Debugger**：分析渲染调用
- **Physics Debugger**：检查碰撞器和寻路

---

## 性能优化要点

### 已实现优化
1. **对象池思想**：队列位置Transform缓存（`cachedSlotTransforms`）
2. **缓存机制**：
   - FloorManager的活跃楼层列表
   - 货架快照批量构建
3. **延迟结算**：软预留机制避免频繁锁定资源
4. **事件驱动**：避免轮询检查

### 建议优化
1. **顾客对象池**：复用CustomerAgent GameObject（减少GC）
2. **空间分割**：大量顾客时使用四叉树优化碰撞检测
3. **批量操作**：批量处理寻路请求（异步寻路）
4. **LOD系统**：
   - 远距离顾客降低更新频率
   - 屏幕外顾客暂停行为树
5. **内存优化**：
   - 避免频繁List/Array创建（使用对象池）
   - Resources.Load改为异步加载
6. **ECS架构**（大量顾客时）：考虑Unity DOTS重构

---

## 已知限制与未来计划

### 当前限制
1. **BlueprintManager/CategoryManager/EffectManager**：原型期简化实现，需补充完整逻辑
2. **多楼层寻路**：当前仅支持单层A*，跨楼层需手动切换
3. **声望系统**：计算公式待完善
4. **顾客升级**：自动升级逻辑未实现

### 未来计划
1. **完整的商店升级系统**（0-5星）
2. **请求系统**（Request）- 完成任务获取奖励
3. **动态定价**：根据供需调整价格
4. **员工系统**：雇佣收银员、理货员
5. **装饰系统**：影响尴尬值和吸引力
6. **多楼层电梯**：跨楼层顾客流动

---

## 项目统计

### 代码规模
- **C# 脚本**：72个文件
- **代码行数**：约10,000+行
- **ScriptableObject资产**：30+个
- **自定义NodeCanvas节点**：11个

### 系统分类
- **数据层**：6个核心文件
- **运行时层**：8个核心文件
- **顾客系统**：43个文件（数据11 + 运行时5 + 服务12 + 生成1 + NodeCanvas11 + 编辑器3）
- **管理器层**：9个管理器
- **UI层**：6个组件
- **编辑器扩展**：4个工具

### 技术栈
- **Unity 6000.0.46f1** (Unity 6)
- **NodeCanvas** 行为树系统
- **A* Pathfinding** 自动寻路
- **ScriptableObject** 数据驱动
- **C# 9.0+** 特性（switch表达式、record等）

---

## 快速参考

### 重要文件路径
```
核心配置：
- 顾客数据：StreamingAssets/Customers.json
- 解锁配置：StreamingAssets/SpawnerProfile.json
- 行为树：Assets/CustomerBehaviorTree.asset

ScriptableObject：
- 建筑原型：Resources/ScriptableObjects/BuildingArchetype/
- 顾客原型：Resources/ScriptableObjects/CustomerArchetypes/
- 特质：Resources/ScriptableObjects/Traits/
- 策略：Resources/ScriptableObjects/BehaviorPolicies/

关键脚本：
- 时间管理：Scripts/Manager/DayLoopManager.cs
- 顾客生成：Scripts/Customers/Spawner/CustomerSpawner.cs
- 网格系统：Scripts/Runtime/FloorGrid.cs
- 建造管理：Scripts/Runtime/ConstructionManager.cs:

说明文档：
- 一切产出的说明文档全部存储在：PopLifeSimulator\Pop Life Simulator\Assets\Documents文件夹中
```

### 常用Manager引用
```csharp
DayLoopManager.Instance       // 时间和游戏循环
ResourceManager.Instance      // 金钱和声望
FloorManager.Instance         // 楼层管理
UIManager.Instance            // UI面板
CustomerEventBus              // 事件总线（静态）
```

### 调试快捷键
- **Tab**：切换楼层
- **数字键1-9**：直接切换到指定楼层
- **Ctrl+P**：Unity编辑器Play/暂停

---

## 总结

Pop Life Simulator 是一个架构设计优秀、扩展性强的模拟经营游戏项目。核心亮点包括：

✅ **清晰的分层架构**：数据层、运行时层、服务层、管理层分离明确
✅ **高度可配置**：ScriptableObject驱动的数据设计
✅ **策略模式应用**：AI决策系统灵活可扩展
✅ **完整的顾客系统**：从生成→行为→交易→离店的闭环
✅ **时间系统设计**：双阶段游戏循环（建造/营业）
✅ **事务式操作**：确保资源和状态的一致性
✅ **排队系统**：真实模拟商店排队体验
✅ **数据持久化**：JSON序列化顾客记录
✅ **行为树AI**：NodeCanvas深度集成
✅ **多设计模式**：原型、策略、事件总线、适配器、快照等

项目展现了良好的软件工程实践，代码质量整体较高，适合作为Unity商业项目的参考案例。