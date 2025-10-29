# Building Detail Panel - Next Level Stats Display
## 建筑详情面板 - 下一级属性显示功能

### 概述
为 `BuildingDetailPanel` 添加了显示下一级属性的功能，以"当前值 → 下一级值"的格式展示升级后的属性变化。

### 修改内容

#### 1. 新增UI字段
在 `BuildingDetailPanel.cs` 中添加了5个新的TextMeshProUGUI字段：

```csharp
[Header("Next Level Stats")]
[SerializeField] private TextMeshProUGUI nextLevelPriceText;
[SerializeField] private TextMeshProUGUI nextLevelStockText;
[SerializeField] private TextMeshProUGUI nextLevelAttractivenessText;
[SerializeField] private TextMeshProUGUI nextLevelMaintenanceText;
[SerializeField] private TextMeshProUGUI nextLevelUpgradeCostText; // 升级所需Fame
```

#### 2. 显示逻辑
- **格式**: `当前值 → 下一级值`
- **最高等级**: 显示 "MAX"
- **属性分类**:
  - **Price** (价格): 仅货架显示，格式 `$100 → $150`
  - **Stock** (库存上限): 仅货架显示，格式 `10 → 15`
  - **Attractiveness** (吸引力): 仅货架显示，格式 `1.0 → 1.5`
  - **Maintenance** (维护费): 所有建筑显示，格式 `$50 → $75`
  - **Upgrade Cost** (升级费用): 所有建筑显示，格式 `Upgrade Cost: 100 Fame`
    - 绿色：Fame足够
    - 红色：Fame不足

#### 3. UpdateNextLevelInfo 方法
新增方法 `UpdateNextLevelInfo(BuildingInstance building)`，负责：
1. 检查是否达到最高等级
2. 获取当前等级和下一等级的数据
3. 分别更新每个属性的TextMeshPro组件
4. 根据建筑类型（货架/设施）显示/隐藏相应的UI

### Unity编辑器配置步骤

#### 在 BuildingDetailPanel 预制体中：
1. 创建5个新的TextMeshProUGUI对象（或复制现有的属性文本）：
   - `NextLevelPriceText`
   - `NextLevelStockText`
   - `NextLevelAttractivenessText`
   - `NextLevelMaintenanceText`
   - `NextLevelUpgradeCostText`

2. 将这些TextMeshProUGUI组件拖拽到BuildingDetailPanel组件的对应字段

3. 建议布局（参考现有属性布局）：
   ```
   Price: $100           [NextLevelPriceText: $100 → $150]
   Stock: 10/10          [NextLevelStockText: 10 → 15]
   Attractiveness: 1.0   [NextLevelAttractivenessText: 1.0 → 1.5]
   Maintenance: $50/day  [NextLevelMaintenanceText: $50 → $75]

   [NextLevelUpgradeCostText: Upgrade Cost: 100 Fame]
   [Upgrade Button]
   ```

### 示例显示效果

#### 非最高等级货架（Fame足够）：
```
Price: $100           → $150
Stock: 10/10          → 15
Attractiveness: 1.0   → 1.5
Maintenance: $50/day  → $75

Upgrade Cost: 100 Fame (绿色)
```

#### 非最高等级货架（Fame不足）：
```
Price: $100           → $150
Stock: 10/10          → 15
Attractiveness: 1.0   → 1.5
Maintenance: $50/day  → $75

Upgrade Cost: 100 Fame (红色)
```

#### 最高等级货架：
```
Price: $100           MAX
Stock: 10/10          MAX
Attractiveness: 1.0   MAX
Maintenance: $50/day  MAX

MAX
```

#### 设施（非货架）：
```
Maintenance: $50/day  → $75

Upgrade Cost: 50 Fame (绿色/红色)
```
（Price, Stock, Attractiveness自动隐藏）

### 技术细节

#### 货架专属属性检测
```csharp
if (building is ShelfInstance shelf && building.archetype is ShelfArchetype shelfArch)
{
    // 显示货架特定属性
}
else
{
    // 隐藏货架特定属性
}
```

#### 等级数据获取
```csharp
var currentShelfData = shelfArch.GetShelfLevel(building.currentLevel);
var nextShelfData = shelfArch.GetShelfLevel(building.currentLevel + 1);
```

### 代码位置
- 文件: `Assets/Scripts/UI/BuildingInteraction/BuildingDetailPanel.cs`
- 新增字段: 第27-32行
- 核心方法: `UpdateNextLevelInfo()` (第263-398行)

### 注意事项
1. TextMeshProUGUI组件必须在Unity编辑器中手动绑定
2. 货架专属属性（Price, Stock, Attractiveness）仅在建筑是货架时显示
3. Maintenance和Upgrade Cost属性适用于所有建筑类型
4. 达到最高等级时显示"MAX"而非数值对比
5. Upgrade Cost会根据玩家当前Fame实时显示颜色（绿色=足够，红色=不足）

### 测试建议
1. 测试不同等级的货架（Level 1, 2, 3...）
2. 测试最高等级货架
3. 测试设施（确认货架专属属性正确隐藏）
4. 测试升级后面板自动刷新

---
**修改日期**: 2025-10-29
**修改人**: Claude Code
