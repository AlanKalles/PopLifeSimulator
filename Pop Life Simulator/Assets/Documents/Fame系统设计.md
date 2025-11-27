# Fame系统设计文档

## 概述
Fame（声望）是游戏中的重要资源，用于解锁货架升级。本文档记录当前的Fame获取机制设计。

## 获取方式
**顾客每次从货架取货成功时即时获得Fame**（不再是每日结算时批量获取）

## 计算公式
```
Fame = (price × priceWeight + attractiveness × attractivenessWeight) × traitFameMultiplier
```

### 默认参数
| 参数 | 默认值 | 说明 |
|------|--------|------|
| priceWeight | 0.05 | 商品价格权重 |
| attractivenessWeight | 0.08 | 货架吸引力权重 |
| traitFameMultiplier | 1.0 | 特质声望倍率（来自顾客Trait） |

### 参数可调
`FameCalculator` 组件挂载在场景中，Inspector可调整 `priceWeight` 和 `attractivenessWeight`。

## 升级曲线设计

### 目标
- **30-40次购买可升级到2级**（中高端货架）
- **16-27次购买可升级到2级**（低端货架）

### 实际计算数据

| 货架类型 | 升级Fame | price | attractiveness | 每次Fame | 需购买次数 |
|----------|----------|-------|----------------|----------|-----------|
| Vellesa Portable Vibrator | 8 | 6 | 2.6 | 0.51 | **16次** |
| Darex Condoms | 10 | 3 | 2.7 | 0.37 | **27次** |
| Darex Intense Condoms | 10 | 7 | 4.0 | 0.67 | **15次** |
| Arclight Textured Stroker | 12 | 4 | 3.9 | 0.51 | **24次** |
| Love Bunny Silicone Dildo | 12 | 4 | 3.7 | 0.50 | **24次** |
| Tenga Egg Fleshlight | 12 | 7 | 4.6 | 0.72 | **17次** |
| Vintage Lingeres | 15 | 3 | 5.0 | 0.55 | **27次** |
| Darex Natural Lubricant | 15 | 4 | 4.0 | 0.52 | **29次** |
| Lewd Lewb Glow Lubricant | 18 | 5 | 4.9 | 0.64 | **28次** |
| Love Bunny Vibrator | 20 | 7 | 5.0 | 0.75 | **27次** |
| Love Bunny Fleshlight | 20 | 6 | 4.9 | 0.69 | **29次** |
| Glacial Glass Dildo | 20 | 6 | 5.0 | 0.70 | **29次** |
| Bunny Girl Lingeres | 25 | 5 | 5.9 | 0.72 | **35次** |
| Lovehoney Strap on Dildo | 25 | 8 | 6.1 | 0.89 | **28次** |
| Love Bunny Strawberry Lubricant | 30 | 6 | 6.9 | 0.85 | **35次** |
| Vellesa Wireless Vibrator | 30 | 8 | 7.1 | 0.97 | **31次** |
| Anal beads | 30 | 8 | 7.1 | 0.97 | **31次** |
| Leather Queen Lingeres | 35 | 7 | 8.1 | 1.00 | **35次** |

### 分类总结
- **低端货架**（升级Fame 8-15）：15-29次购买可升级
- **中端货架**（升级Fame 18-25）：27-35次购买可升级
- **高端货架**（升级Fame 30-35）：31-35次购买可升级

## 技术实现

### 关键文件
1. `Scripts/Customers/Services/FameCalculator.cs` - Fame计算服务（MonoBehaviour单例）
2. `Scripts/Customers/Data/Trait.cs` - 特质声望倍率 `fameMultiplier`
3. `Scripts/Customers/Services/TraitResolver.cs` - 特质效果计算 `fameMul`
4. `Scripts/Customers/Runtime/CustomerInteraction.cs` - 取货时触发Fame计算
5. `Scripts/Customers/Runtime/CustomerAgent.cs` - `GetTraitFameMul()` 方法
6. `Scripts/Manager/ResourceManager.cs` - `AddFame(float)` 累积小数Fame
7. `Scripts/Manager/DayLoopManager.cs` - `RecordFame()` 追踪每日Fame

### 流程
```
顾客取货成功 (CustomerInteraction.TryPurchase)
    ↓
获取货架吸引力 (ShelfInstance.GetAttractiveness)
    ↓
获取特质Fame倍率 (CustomerAgent.GetTraitFameMul)
    ↓
计算Fame (FameCalculator.CalculateFame)
    ↓
增加玩家Fame (ResourceManager.AddFame)
    ↓
记录每日统计 (DayLoopManager.RecordFame)
```

### 小数累积机制
`ResourceManager` 使用 `fameAccumulator` 累积小数Fame，当累积满1时才增加整数Fame。

## 特质影响

### Trait.fameMultiplier
- 默认值：1.0
- 大于1：增加Fame贡献（如Rich顾客可设为1.2）
- 小于1：减少Fame贡献（如Shy顾客可设为0.8）

### 计算方式
多个特质的 `fameMultiplier` 相乘：
```csharp
e.fameMul *= t.fameMultiplier;
```

## 调参建议

### 如需加快Fame获取
- 增大 `priceWeight`（高价商品贡献更多）
- 增大 `attractivenessWeight`（高级货架贡献更多）

### 如需减慢Fame获取
- 减小上述权重值

### 当前设置平衡
- 价格贡献约 40%（低价商品price=3时贡献0.15）
- 吸引力贡献约 60%（低级货架attr=2.7时贡献0.22）

## 版本历史
- 2024-XX-XX：从每日结算改为实时获取，设计30-40次购买升级曲线
