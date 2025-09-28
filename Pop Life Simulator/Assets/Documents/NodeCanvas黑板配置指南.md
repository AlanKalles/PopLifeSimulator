# NodeCanvas黑板配置指南

## 一、概述

本文档详细说明如何配置NodeCanvas的Blackboard系统，使其与Customer系统的`CustomerBlackboardAdapter`进行数据同步，实现行为树驱动的顾客AI。

## 二、核心概念

### 双黑板架构

```
Customer GameObject
├── CustomerBlackboardAdapter（C#数据存储）
│   ├─ 存储实际运行时数据
│   └─ 可被任何C#脚本访问
└── NodeCanvas.Framework.Blackboard（NC黑板）
    ├─ NodeCanvas的变量系统
    └─ 行为树节点通过此访问数据
```

### 数据同步机制

```csharp
// 单向同步：C# → NodeCanvas
CustomerBlackboardAdapter.InjectFromRecord()
    ↓
设置C#公共字段
    ↓
#if NODECANVAS
ncBlackboard.SetVariableValue("key", value)
#endif
```

## 三、Unity编辑器配置步骤

### Step 1: 启用NodeCanvas支持

1. 打开 **Edit → Project Settings → Player → Other Settings**
2. 找到 **Scripting Define Symbols**
3. 添加预编译指令：`NODECANVAS`
4. 点击 **Apply**，等待重新编译

### Step 2: 配置Customer预制体

#### 2.1 添加必需组件（按顺序）

1. **CustomerAgent**
   - 负责初始化顾客数据

2. **CustomerBlackboardAdapter**
   - 存储和管理运行时数据
   - Inspector中会显示`ncBlackboard`字段

3. **Blackboard** (NodeCanvas组件)
   - 位置：Add Component → NodeCanvas → Blackboard
   - NodeCanvas的变量存储系统

4. **BehaviourTreeOwner** (NodeCanvas组件)
   - 位置：Add Component → NodeCanvas → BehaviourTreeOwner
   - 控制行为树的执行

#### 2.2 组件关联设置

1. **CustomerBlackboardAdapter设置**
   - `ncBlackboard`字段会自动检测同GameObject上的Blackboard组件
   - 如未自动关联，手动拖拽Blackboard组件到此字段

2. **BehaviourTreeOwner设置**
   - 在Inspector中找到`Blackboard`字段
   - 选择"Use Self Blackboard"或拖拽同GameObject的Blackboard组件

### Step 3: 配置Blackboard变量

#### 3.1 打开黑板编辑器

1. 选中Blackboard组件
2. 点击Inspector中的 **"Edit Blackboard"** 按钮
3. 或者点击 **"Open Editor"** 打开可视化编辑器

#### 3.2 添加必需变量

在黑板编辑器中添加以下变量（名称和类型必须完全匹配）：

| 变量名 | 类型 | 访问权限 | 说明 |
|--------|------|---------|------|
| **customerId** | string | 只读 | 顾客唯一标识 |
| **loyaltyLevel** | int | 只读 | 忠诚度等级(1-10) |
| **trust** | int | 只读 | 信任值(0-100) |
| **interestFinal** | int[] | 只读 | 各商品类别兴趣值数组 |
| **embarrassmentCap** | int | 只读 | 尴尬值上限 |
| **moveSpeed** | float | 只读 | 移动速度 |
| **queueToleranceSec** | int | 只读 | 排队容忍秒数 |
| **moneyBag** | int | 读写 | 当前钱包金额 |
| **embarrassment** | int | 读写 | 当前尴尬值 |
| **goalCell** | Vector2Int | 读写 | 目标网格位置 |
| **targetShelfId** | string | 读写 | 目标货架ID |
| **targetCashierId** | string | 读写 | 目标收银台ID |

#### 3.3 变量添加方法

1. 点击黑板编辑器中的 **"+ Add Variable"**
2. 选择变量类型
3. 输入变量名（必须与上表完全一致）
4. 设置默认值（可选）

### Step 4: 创建并配置行为树

#### 4.1 创建行为树资产

1. 在BehaviourTreeOwner组件中
2. 点击 **"Create New"** 创建新行为树
3. 或选择 **"Select Existing"** 使用已有行为树

#### 4.2 配置行为树黑板源

1. 打开行为树编辑器
2. 在Graph Inspector中
3. 设置Blackboard Source为 **"Use Component Blackboard"**
4. 这确保行为树使用GameObject上的Blackboard组件

## 四、在行为树节点中访问数据

### 方法1：使用BBParameter（推荐）

```csharp
using NodeCanvas.Framework;
using ParadoxNotion.Design;

[Category("Customer")]
public class SelectShelfAction : ActionTask
{
    // BBParameter自动绑定到黑板变量
    public BBParameter<int[]> interestFinal;
    public BBParameter<int> moneyBag;
    public BBParameter<string> targetShelfId;

    protected override void OnExecute()
    {
        // 读取值
        var interests = interestFinal.value;
        var money = moneyBag.value;

        // 执行逻辑
        var bestShelf = SelectBestShelf(interests, money);

        // 写入值
        targetShelfId.value = bestShelf.id;

        EndAction(true);
    }
}
```

### 方法2：直接访问黑板

```csharp
protected override void OnExecute()
{
    // 获取黑板引用
    var bb = blackboard;

    // 读取变量
    var interests = bb.GetVariableValue<int[]>("interestFinal");
    var money = bb.GetVariableValue<int>("moneyBag");

    // 写入变量
    bb.SetVariableValue("targetShelfId", selectedId);
    bb.SetVariableValue("embarrassment", newEmbarrassment);

    EndAction(true);
}
```

### 方法3：使用条件节点

```csharp
[Category("Customer")]
public class CheckEmbarrassmentCondition : ConditionTask
{
    public BBParameter<int> embarrassment;
    public BBParameter<int> embarrassmentCap;
    public CompareMethod comparison = CompareMethod.LessThan;

    protected override bool OnCheck()
    {
        switch(comparison)
        {
            case CompareMethod.LessThan:
                return embarrassment.value < embarrassmentCap.value;
            case CompareMethod.GreaterThan:
                return embarrassment.value > embarrassmentCap.value;
            default:
                return false;
        }
    }
}
```

## 五、数据同步流程详解

### 初始化时序

```
1. GameObject实例化
    ↓
2. Awake()调用
    ├─ CustomerAgent.Awake()
    ├─ CustomerBlackboardAdapter.Reset() // 自动获取ncBlackboard引用
    └─ Blackboard.Awake()
    ↓
3. Start()调用
    ↓
4. CustomerSpawner.SpawnOne()
    ↓
5. CustomerAgent.Initialize()
    ├─ 计算最终属性
    └─ 调用InjectFromRecord()
        ├─ 设置C#字段
        └─ 同步到NC黑板（如果启用）
    ↓
6. BehaviourTreeOwner.StartBehaviour()
    └─ 行为树开始执行
```

### 运行时数据流

```
行为树节点
    ↓
读取黑板变量 → 执行逻辑 → 更新黑板变量
    ↓
其他节点读取更新后的值
    ↓
CustomerBlackboardAdapter的公共字段也可被其他C#脚本访问
```

## 六、调试技巧

### 6.1 运行时查看黑板变量

1. 运行游戏
2. 选中Customer GameObject
3. 在Blackboard组件的Inspector中查看所有变量实时值
4. 值会随着行为树执行自动更新

### 6.2 行为树可视化调试

1. 选中Customer GameObject
2. 在BehaviourTreeOwner组件中点击 **"Open Editor"**
3. 观察：
   - 当前执行的节点会高亮
   - 节点状态颜色（成功=绿色，失败=红色，运行中=黄色）
   - 黑板变量实时值

### 6.3 断点调试

在自定义节点中添加断点：
```csharp
protected override void OnExecute()
{
    Debug.Log($"[{agent.name}] 当前尴尬值: {embarrassment.value}/{embarrassmentCap.value}");

    // 条件断点
    if(embarrassment.value > 80)
    {
        Debug.Break(); // 暂停编辑器
    }
}
```

## 七、常见问题与解决方案

### Q1: 黑板变量未同步

**原因**：未启用NODECANVAS预编译指令
**解决**：Project Settings中添加NODECANVAS定义

### Q2: 变量类型不匹配错误

**原因**：黑板定义的类型与代码设置的类型不一致
**解决**：确保类型完全匹配（如int[]不能用List<int>）

### Q3: 找不到变量

**原因**：变量名拼写错误或大小写不匹配
**解决**：变量名必须完全一致，包括大小写

### Q4: 行为树不执行

**原因**：未正确关联黑板或未启动行为树
**解决**：
1. 确认BehaviourTreeOwner的Blackboard字段已设置
2. 确认调用了StartBehaviour()或设置了自动启动

### Q5: 数组变量无法显示

**原因**：NodeCanvas编辑器对数组显示有限制
**解决**：使用自定义Inspector或在运行时通过代码查看

## 八、最佳实践

### 1. 变量命名规范
- 使用驼峰命名法
- 避免使用下划线开头
- 名称要有描述性

### 2. 性能优化
- 避免每帧SetVariableValue
- 批量更新变量时集中处理
- 使用BBParameter缓存变量引用

### 3. 代码组织
- 将自定义节点放在专门的文件夹
- 使用Category特性分组
- 为复杂逻辑创建子图

### 4. 错误处理
```csharp
protected override void OnExecute()
{
    if(interestFinal.value == null || interestFinal.value.Length == 0)
    {
        Debug.LogError($"[{agent.name}] 兴趣数组为空！");
        EndAction(false);
        return;
    }
    // 正常逻辑...
}
```

## 九、扩展示例

### 创建自定义策略节点

```csharp
using NodeCanvas.Framework;
using PopLife.Customers.Data;

[Category("Customer/Policies")]
public class ExecutePurchasePolicy : ActionTask
{
    public BBParameter<BehaviorPolicySet> policies;
    public BBParameter<int> moneyBag;
    public BBParameter<string> targetShelfId;

    protected override void OnExecute()
    {
        if(policies.value?.purchase == null)
        {
            EndAction(false);
            return;
        }

        var ctx = BuildContext();
        var shelf = GetShelfSnapshot(targetShelfId.value);
        var qty = policies.value.purchase.DecidePurchaseQty(
            in ctx, in shelf,
            moneyBag.value,
            shelf.price
        );

        // 执行购买...
        EndAction(qty > 0);
    }
}
```

这样配置后，您的Customer系统就能完美地与NodeCanvas行为树系统协作了！