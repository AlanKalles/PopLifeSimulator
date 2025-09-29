# 🎯 顾客策略系统设计文档

## 一、策略系统总体架构

### 核心设计理念
- **决策解耦**：将AI决策的每个关键点抽象为独立策略
- **数据驱动**：通过ScriptableObject配置不同行为模式
- **特质整合**：策略执行时自动考虑特质修正
- **行为树集成**：策略作为行为树节点的决策核心

### 顾客行为主流程
```
进店 → 目标选择 → 移动排队 → 购买决策 → 收银结账 → 循环/离店
```

## 二、各策略详细设计

### 1️⃣ **TargetSelectorPolicy（目标选择策略）**

**职责**：根据顾客兴趣和货架吸引力选择下一个目标货架

**调用时机**：
- 顾客进店后
- 完成一次购买后
- 目标货架无法到达时（重选）

**输入数据**：
- 顾客兴趣数组（经特质修正）
- 所有可用货架快照（位置、类别、吸引力、库存、队列）
- 顾客当前位置
- 已购买货架列表（shelf instance id）（避免重复）

**决策逻辑**：
```
1. 筛选阶段：
   - 过滤库存为0的货架
   - 过滤已购买的货架(查重instance id)
   - 过滤无法到达的货架（寻路检查）

2. 评分阶段：
   - 兴趣匹配分 = interest[category] * 兴趣权重
   - 吸引力分 = attractiveness * 吸引力权重
   - 队列惩罚 = queuePenaltyCurve(queueLength) * 队列权重

3. 加权随机选择：
   - 使用吸引力作为权重进行随机选择（符合设计文档要求）
```

**策略变体**：
- `WeightedRandomSelector`：按吸引力权重随机（默认）
- `GreedySelector`：总是选择得分最高的
- `CategoryFocusedSelector`：优先清空某一类别
- `ImpulseSelector`：忽略队列和距离，只看吸引力

---

### 2️⃣ **PurchasePolicy（购买数量策略）**

**职责**：决定在当前货架购买的商品数量

**调用时机**：
- 到达货架前排队列首位时

**输入数据**：
- 顾客钱包余额
- 商品单价
- 货架库存
- 顾客购买范围（min-buy, max-buy）
- 顾客loyalty level

**决策逻辑**：
```
1. 基础数量 = Random(min-buy, max-buy)
2. 预算限制 = floor(钱包余额 / 商品单价)
3. 库存限制 = 当前库存
4. 最终数量 = min(基础数量, 预算限制, 库存限制)
5. 特殊处理：
   - 如果最终数量 <= 0，触发离店
   - loyalty等级高则购买量增加
```

**策略变体**：
- `RandomPurchase`：在范围内随机（默认）
- `ConservativePurchase`：倾向购买较少
- `BulkPurchase`：倾向购买较多
- `BudgetAwarePurchase`：为后续购买预留资金

---

### 3️⃣ **QueuePolicy（排队换线策略）**

**职责**：决定是否切换到更短的队列

**调用时机**：
- 在队列中等待时（每隔X秒检查）

**输入数据**：
- 当前队列位置
- 当前队列预计等待时间
- 其他可用队列信息
- 顾客耐心值（queueToleranceSeconds）
- 已等待时间

**决策逻辑**：
```
1. 计算换线成本：
   - 时间成本 = 移动时间 + 新队列等待时间
   - 当前成本 = 剩余等待时间

2. 换线条件：
   - 时间成本 < 当前成本 * 换线阈值
   - 已等待时间 < 耐心值的一半（避免频繁换线）

3. 特殊情况：
   - 超过耐心值直接放弃购买
```

**策略变体**：
- `PatientQueue`：很少换线
- `ImpatientQueue`：积极换线
- `NoSwitchQueue`：从不换线
- `OptimalQueue`：精确计算最优选择

---

### 4️⃣ **PathPolicy（重新寻路策略）**

**职责**：决定是否需要重新计算路径

**调用时机**：
- 移动过程中定期检查
- 遇到障碍物时

**输入数据**：
- 上次寻路时间
- 剩余路径长度
- 当前位置偏离度
- 环境变化标记

**决策逻辑**：
```
1. 强制重寻路条件：
   - 路径被阻断
   - 偏离路径超过阈值

2. 优化重寻路条件：
   - 距离上次寻路超过X秒
   - 发现更短路径（启发式判断）

3. 避免过度寻路：
   - 设置最小间隔时间
   - 剩余距离很短时不重寻路
```

**策略变体**：
- `LazyPath`：很少重新寻路
- `AdaptivePath`：根据环境变化调整
- `DirectPath`：总是直线前进
- `OptimalPath`：频繁优化路径

---

### 5️⃣ **EmbarrassmentPolicy（尴尬值管理策略）**

**职责**：计算每秒的尴尬值变化

**调用时机**：
- 每秒更新一次
- 进入特殊区域时

**输入数据**：
- 当前格子的环境尴尬值（EEB）
- 顾客当前尴尬值
- 顾客尴尬上限

**决策逻辑**：
```
1. 基础增量 = 环境尴尬值

3. 上限检查：
   - 如果当前值 + 增量 > 上限，触发逃离

4. 特殊区域：
   - 空调覆盖区：额外减少X点
   - 收银台附近：增加压力
```

**策略变体**：
- `StandardEmbarrassment`：标准计算
- `ThickSkinned`：减缓增长
- `Sensitive`：加速增长
- `AdaptiveEmbarrassment`：随时间降低敏感度

---

### 6️⃣ **CheckoutPolicy（收银台选择策略）**

**职责**：选择最优的收银台进行结账

**调用时机**：
- 完成商品选取后

**输入数据**：
- 所有收银台位置和队列长度
- 顾客当前位置
- 顾客购物袋中的商品数量

**决策逻辑**：
```
1. 计算每个收银台的得分：
   - 距离分 = 1 / (距离 + 1)
   - 队列分 = 1 / (队列长度 + 1)
   - 预计等待 = 队列长度 * 平均结账时间

2. 综合评分：
   - 总分 = 距离分 * 距离权重 + 队列分 * 队列权重

3. 特殊处理：
   - VIP通道（如果有）
   - 楼层限制（优先同层收银台）
```

**策略变体**：
- `NearestCheckout`：选择最近的
- `ShortestQueueCheckout`：选择队列最短的
- `BalancedCheckout`：平衡距离和队列
- `RandomCheckout`：随机选择

---

## 三、策略与NodeCanvas集成

### 行为树节点设计

```csharp
// 示例：选择目标货架的行为树Action节点
public class SelectTargetShelfAction : ActionTask<CustomerBlackboardAdapter>
{
    protected override void OnExecute()
    {
        var ctx = BuildContext(agent);
        var candidates = GatherCandidates();
        var policy = agent.policies.targetSelector;

        int selectedIndex = policy.SelectTargetShelf(ctx, candidates);

        if (selectedIndex >= 0)
        {
            agent.targetShelfId = candidates[selectedIndex].shelfId;
            EndAction(true);
        }
        else
        {
            EndAction(false); // 没有可选目标
        }
    }
}
```

### 数据流向
```
CustomerRecord（持久数据）
    ↓
CustomerAgent（运行时实例）
    ↓
CustomerBlackboardAdapter（黑板适配器）
    ↓
BehaviorPolicySet（策略集合）
    ↓
NodeCanvas行为树节点（执行策略）
```

---

## 四、策略配置示例

### 不同顾客原型的策略组合

**害羞顾客**：
- TargetSelector: 优先选择人少的货架
- Purchase: 保守购买
- Queue: 很少换线
- Embarrassment: 敏感型

**冲动顾客**：
- TargetSelector: 只看吸引力
- Purchase: 大量购买
- Queue: 频繁换线
- Embarrassment: 迟钝型

**精打细算顾客**：
- TargetSelector: 考虑性价比
- Purchase: 预算感知型
- Queue: 优化型
- Path: 最短路径

---

## 五、扩展性考虑

1. **新策略添加**：继承对应的抽象策略类即可
2. **策略组合**：通过BehaviorPolicySet灵活组合
3. **运行时切换**：支持根据条件动态切换策略
4. **数据收集**：策略执行时可记录决策数据用于分析

---

## 六、实现优先级

### 第一阶段（核心功能）
1. TargetSelectorPolicy - WeightedRandomSelector
2. PurchasePolicy - RandomPurchase
3. CheckoutPolicy - BalancedCheckout
4. EmbarrassmentPolicy - StandardEmbarrassment

### 第二阶段（优化体验）
5. QueuePolicy - PatientQueue
6. PathPolicy - LazyPath
7. 更多策略变体实现

### 第三阶段（高级功能）
8. 动态策略切换
9. 数据分析和策略优化
10. AI学习和自适应

---

## 七、测试要点

1. **单元测试**：每个策略独立测试其决策逻辑
2. **集成测试**：测试策略与行为树的配合
3. **性能测试**：大量顾客同时决策的性能
4. **边界测试**：异常输入和极端情况处理
5. **平衡测试**：不同策略组合的游戏平衡性

这个设计确保了高度的灵活性和可扩展性，同时保持了与原设计文档的一致性。每个策略都有明确的职责边界，便于独立开发和测试。