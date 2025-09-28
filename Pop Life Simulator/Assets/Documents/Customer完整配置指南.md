# Customer完整配置指南

## 一、概述

本指南详细说明如何从零开始配置一个完整的Customer系统，包括所有必需的文件创建、配置步骤以及文件间的关系。

## 二、文件结构与组织

### 推荐的文件夹结构

```
Assets/
├── Scripts/
│   ├── Customers/
│   │   ├── AI/                    # 自定义行为树节点
│   │   │   ├── Actions/           # 行为节点
│   │   │   └── Conditions/        # 条件节点
│   │   ├── Data/                  # 数据类（已存在）
│   │   └── Runtime/               # 运行时脚本（已存在）
│   │
├── ScriptableObjects/
│   ├── CustomerArchetypes/        # 顾客原型
│   │   ├── NormalCustomer.asset
│   │   ├── RichCustomer.asset
│   │   └── ShyCustomer.asset
│   │
│   ├── Traits/                    # 特征
│   │   ├── Personality/
│   │   │   ├── Shy.asset
│   │   │   ├── Bold.asset
│   │   │   └── Neutral.asset
│   │   └── Wealth/
│   │       ├── Poor.asset
│   │       ├── Middle.asset
│   │       └── Rich.asset
│   │
│   └── BehaviorPolicies/          # 行为策略
│       ├── DefaultPolicies.asset
│       ├── AggressiveShopper.asset
│       └── CautiousShopper.asset
│
├── Prefabs/
│   └── Customers/
│       ├── Customer1.prefab       # 具体顾客预制体
│       └── CustomerBase.prefab    # 基础顾客模板（可选）
│
└── NodeCanvas/
    └── BehaviorTrees/
        ├── CustomerBehaviorTree.asset    # 通用行为树资产
        └── SpecialCustomerBT.asset       # 特殊行为树（可选）
```

## 三、需要创建的文件及其关系

### 3.1 ScriptableObject文件

#### A. CustomerArchetype（顾客原型）- **通用文件**

**文件位置**：`Assets/ScriptableObjects/CustomerArchetypes/`

**创建方法**：
1. 右键点击文件夹
2. Create → ScriptableObject → 搜索"CustomerArchetype"
3. 命名为 "NormalCustomer.asset"

**配置内容**（在Inspector中）：
```
Name: 普通顾客
Description: 标准的顾客类型

Base Attributes:
├── Loyalty Level: 5
├── Trust: 50
├── Interest (Size: 5)
│   ├── [0] Lingerie: 30
│   ├── [1] Condom: 50
│   ├── [2] Vibrator: 20
│   ├── [3] Fleshlights: 10
│   └── [4] Lubricant: 40
├── Embarrassment Cap: 75
├── Move Speed: 3.0
├── Queue Tolerance Sec: 60
└── Money Bag: 500

Allowed Traits: (拖入创建好的Trait资产)
├── [0] Shy
├── [1] Bold
└── [2] Middle

Behavior Policy Set: DefaultPolicies（拖入策略资产）
```

#### B. Trait（特征）- **通用文件**

**文件位置**：`Assets/ScriptableObjects/Traits/`

**创建方法**：
1. Create → ScriptableObject → 搜索"Trait"
2. 创建多个特征文件

**示例配置 - Shy.asset**：
```
Trait Name: 害羞
Category: Personality

Attribute Modifiers:
├── Loyalty Level Mod: 1
├── Trust Mod: -10
├── Interest Modifiers (Size: 5)
│   ├── [0]: -5  (Lingerie)
│   ├── [1]: 0   (Condom)
│   ├── [2]: -10 (Vibrator)
│   ├── [3]: -15 (Fleshlights)
│   └── [4]: 5   (Lubricant)
├── Embarrassment Cap Mod: -20
├── Move Speed Mod: -0.5
├── Queue Tolerance Sec Mod: -15
└── Money Bag Mod: 0
```

#### C. BehaviorPolicySet（行为策略集）- **通用文件**

**文件位置**：`Assets/ScriptableObjects/BehaviorPolicies/`

**创建方法**：
1. Create → ScriptableObject → 搜索"BehaviorPolicySet"
2. 创建DefaultPolicies.asset

**配置内容**：
```
Policy Name: 默认策略

Policies:
├── Navigation: DefaultNavigationPolicy
├── Purchase: DefaultPurchasePolicy
├── Queue: DefaultQueuePolicy
└── Social: DefaultSocialPolicy

(每个Policy需要创建对应的ScriptableObject)
```

### 3.2 行为树文件

#### A. BehaviorTree Asset - **建议使用Asset模式（通用）**

**为什么选择Asset模式**：
- ✅ **可重用性**：一个行为树可被多个顾客使用
- ✅ **易于管理**：集中管理所有行为树逻辑
- ✅ **性能优化**：减少内存占用
- ✅ **版本控制**：更容易追踪修改

**创建方法**：
1. 在Project窗口创建文件夹：`Assets/NodeCanvas/BehaviorTrees/`
2. 右键 → Create → NodeCanvas → Behavior Tree
3. 命名为 "CustomerBehaviorTree.asset"

**配置行为树**：
1. 双击打开行为树编辑器
2. 设置Blackboard Source: "From Prefab" 或 "Use Component Blackboard"
3. 构建行为树结构（见下文）

### 3.3 预制体配置

#### Customer1.prefab - **特定顾客实例**

**组件配置顺序**：

1. **Transform**（已有）
   - Position: (0, 0, 0)
   - Scale: (1, 1, 1)

2. **CustomerAgent**（已有）
   - Archetype: 拖入 NormalCustomer.asset
   - Assigned Traits:
     - Size: 2
     - [0]: Shy.asset
     - [1]: Middle.asset

3. **CustomerBlackboardAdapter**（已有）
   - NC Blackboard: （自动关联或手动拖入Blackboard组件）

4. **Blackboard**（已有）
   - 按之前指南配置所有变量

5. **BehaviourTreeOwner**（已有）
   - **Behaviour**: 拖入 CustomerBehaviorTree.asset（使用Asset模式）
   - **Blackboard**: Use Self Blackboard
   - **Update Mode**: Every Frame
   - **Start When Enabled**: ✓

6. **需要添加的额外组件**：

   a. **Rigidbody2D**（用于物理移动）
   ```
   Body Type: Dynamic
   Mass: 1
   Gravity Scale: 0
   Freeze Rotation: ✓
   ```

   b. **Collider2D**（用于碰撞检测）
   ```
   选择: Circle Collider 2D 或 Capsule Collider 2D
   Is Trigger: ✗
   Radius/Size: 根据角色大小调整
   ```

   c. **SpriteRenderer** 或 **其他渲染组件**
   ```
   Sprite: 选择顾客精灵图
   Sorting Layer: Characters
   Order in Layer: 0
   ```

   d. **NavMeshAgent**（如果使用Unity导航）或 **自定义移动组件**

## 四、文件关系图

```
Customer1 (GameObject/Prefab) [特定实例]
├── CustomerAgent [组件]
│   ├── 引用 → CustomerArchetype.asset [通用]
│   └── 引用 → Trait.asset (多个) [通用]
│
├── CustomerBlackboardAdapter [组件]
│   └── 引用 → Blackboard组件 [同GameObject]
│
├── Blackboard [组件]
│   └── 存储运行时变量
│
└── BehaviourTreeOwner [组件]
    ├── 引用 → CustomerBehaviorTree.asset [通用]
    └── 引用 → Blackboard组件 [同GameObject]

CustomerArchetype.asset [通用]
├── 定义基础属性
├── 引用 → Trait.asset列表 [通用]
└── 引用 → BehaviorPolicySet.asset [通用]

CustomerBehaviorTree.asset [通用]
└── 包含所有行为逻辑节点
```

## 五、创建步骤流程

### Step 1: 创建基础ScriptableObject资产

1. **创建Traits**（5-10个不同特征）
2. **创建BehaviorPolicies**（至少1个默认策略集）
3. **创建CustomerArchetypes**（2-3个不同类型）

### Step 2: 创建行为树资产

1. 创建CustomerBehaviorTree.asset
2. 打开编辑器构建基础行为流程
3. 保存行为树

### Step 3: 配置Customer预制体

1. 设置CustomerAgent的Archetype引用
2. 分配Traits
3. 将行为树资产拖入BehaviourTreeOwner
4. 添加必要的物理和渲染组件

### Step 4: 创建自定义行为树节点（可选）

在 `Assets/Scripts/Customers/AI/` 下创建自定义Action和Condition

## 六、通用vs特定文件说明

### 通用文件（多个顾客共享）：
- **CustomerArchetype.asset** - 定义顾客类型模板
- **Trait.asset** - 可组合的特征修饰符
- **BehaviorPolicySet.asset** - 行为决策策略
- **CustomerBehaviorTree.asset** - AI行为逻辑
- **自定义行为树节点脚本** - 行为实现

### 特定文件（每个顾客独有）：
- **Customer1.prefab** - 具体的顾客实例
- **特定的精灵/模型资源** - 视觉表现（如果需要区分）

### 运行时生成（不需要预创建）：
- **CustomerRecord** - 运行时数据记录
- **黑板变量值** - 运行时状态

## 七、行为树基础结构示例

```
Root
└── Selector (主循环)
    ├── Sequence (购物流程)
    │   ├── Condition: HasShoppingIntent
    │   ├── Action: SelectTargetShelf
    │   ├── Sequence (移动到货架)
    │   │   ├── Action: PathfindToShelf
    │   │   └── Action: MoveAlongPath
    │   ├── Action: BrowseProducts
    │   ├── Condition: DecideToPurchase
    │   ├── Action: TakeProduct
    │   ├── Sequence (结账)
    │   │   ├── Action: FindCashier
    │   │   ├── Action: MoveToQueue
    │   │   └── Action: PayAndLeave
    │   └── Action: ExitStore
    │
    ├── Sequence (尴尬逃离)
    │   ├── Condition: EmbarrassmentOverLimit
    │   └── Action: FleeStore
    │
    └── Action: IdleWander (默认行为)
```

## 八、Asset模式 vs Bound模式对比

### Asset模式（推荐）✅
**优点**：
- 行为树作为独立资产存在
- 可被多个预制体引用
- 便于版本管理和团队协作
- 修改一处，所有引用自动更新

**适用场景**：
- 多个顾客使用相同AI逻辑
- 需要标准化行为模式
- 团队协作开发

### Bound模式
**优点**：
- 行为树绑定到特定GameObject
- 可为单个实例定制独特行为

**缺点**：
- 不可重用
- 难以管理多个顾客
- 占用更多内存

**结论**：强烈建议使用Asset模式

## 九、测试与验证

### 测试步骤：

1. **单元测试**：
   - 将Customer1拖入场景
   - 运行游戏
   - 打开行为树编辑器观察执行

2. **检查要点**：
   - 黑板变量是否正确初始化
   - 行为树节点是否按预期执行
   - 移动和碰撞是否正常

3. **调试技巧**：
   - 在行为树编辑器中实时观察节点状态
   - 使用Debug.Log在关键节点输出信息
   - 检查Inspector中的黑板变量值

## 十、常见问题

### Q: 如何复制创建多个不同的顾客？
A:
1. 复制Customer1.prefab → Customer2.prefab
2. 修改CustomerAgent中的Archetype和Traits组合
3. 可选：使用不同的精灵图
4. 行为树可以共用同一个Asset

### Q: 如何创建特殊行为的顾客？
A:
1. 创建新的BehaviorTree.asset
2. 或在现有行为树中使用条件分支
3. 基于Archetype或Trait进行行为区分

### Q: 行为树修改后需要重新配置预制体吗？
A: 不需要。使用Asset模式时，修改行为树会自动应用到所有引用它的预制体。

## 十一、下一步

1. 先创建基础的ScriptableObject资产
2. 构建一个简单的行为树（移动→购买→离开）
3. 测试单个顾客的完整流程
4. 逐步添加复杂行为和决策
5. 创建多样化的顾客类型

记住：从简单开始，逐步迭代完善！