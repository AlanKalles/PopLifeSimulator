# ProductCategory 扩展完全指南

## 📋 目录
- [系统概览](#系统概览)
- [核心数据流](#核心数据流)
- [扩展步骤](#扩展步骤)
- [自动同步机制](#自动同步机制)
- [手动同步清单](#手动同步清单)
- [测试验证](#测试验证)
- [常见问题](#常见问题)

---

## 系统概览

### ProductCategory 枚举定义
**位置**: `Assets/Scripts/Data/BuildingArchetypes.cs:16`

```csharp
public enum ProductCategory {
    Lingerie,   // 0
    Condom,     // 1
    Vibrator,   // 2
    Fleshlight, // 3
    Lubricant,  // 4
    BDSM        // 5
}
```

### 影响范围统计
- **直接引用文件**: 12个
- **ScriptableObject资产**: 所有CustomerArchetype、Trait、ShelfArchetype
- **运行时数据**: Customers.json 中所有顾客记录
- **编辑器UI**: InterestArrayPropertyDrawer、TraitInterestPropertyDrawer

---

## 核心数据流

### 兴趣值计算流程
```
ProductCategory枚举（6个值）
    ↓
CustomerArchetype.baseInterest[6]  ← 原型基础兴趣
    ↓
CustomerRecord.interestPersonalDelta[6]  ← 个体偏移（随机生成）
    ↓
Trait.interestAdd[6]  ← 特质加成
Trait.interestMul[6]  ← 特质倍率
    ↓
CustomerRecord.ComposeFinalInterest()  ← 最终计算
    ↓
CustomerAgent初始化 → CustomerBlackboardAdapter.interestFinal[6]
    ↓
策略使用（WeightedRandomSelector、RandomPurchasePolicy等）
```

### 完整计算公式
```
最终兴趣[i] = (baseInterest[i] + personalDelta[i] + Σ(trait.interestAdd[i]))
              × Π(trait.interestMul[i])

where i ∈ [0, categoryCount-1]
```

**代码位置**: `CustomerRecord.cs:57-103`

---

## 扩展步骤

### 步骤1: 修改枚举定义 ⚠️ **唯一必须的手动步骤**

**文件**: `Assets/Scripts/Data/BuildingArchetypes.cs`

```csharp
// 原始定义（6个类别）
public enum ProductCategory { Lingerie, Condom, Vibrator, Fleshlight, Lubricant, BDSM }

// 扩展后（7个类别）- 示例添加 Toys
public enum ProductCategory {
    Lingerie,   // 0
    Condom,     // 1
    Vibrator,   // 2
    Fleshlight, // 3
    Lubricant,  // 4
    BDSM,       // 5
    Toys        // 6 ← 新增
}
```

**注意事项**:
- ✅ 必须在枚举末尾添加（保持现有索引不变）
- ✅ 命名使用PascalCase（首字母大写）
- ❌ 不要插入中间（会导致现有数据错位）
- ❌ 不要删除现有类别（会破坏数据兼容性）

---

### 步骤2: 创建新的货架原型

**操作路径**:
1. 在Project窗口右键点击 `Assets/Resources/ScriptableObjects/BuildingArchetype/Shelf/`
2. 选择 `Create → PopLife/Buildings/ShelfArchetype`
3. 命名新资产（例如：`Toys Shelf.asset`）

**Inspector配置**:
```
archetypeId: shelf_toys_001
displayName: Toys Shelf
category: Toys  ← 选择新增的类别
icon: [分配对应Sprite]
prefab: [分配对应Prefab]
buildCost: 100
levels[0]:
  - level: 1
  - upgradeFameCost: 50
  - maintenanceFee: 10
  - basePrice: 20
  - stockCapacity: 30
  - attractiveness: 8
```

---

### 步骤3: 等待自动同步完成 ✅

保存修改后，Unity会自动触发以下操作：

#### 3.1 编辑器UI自动扩展
**文件**: `InterestArrayPropertyDrawer.cs:22-37`

```csharp
// 自动读取枚举长度
string[] categoryNames = System.Enum.GetNames(typeof(ProductCategory));
int categoryCount = categoryNames.Length;  // 自动从6变为7

// 自动扩展数组大小
if (valuesProperty.arraySize != categoryCount)
{
    valuesProperty.arraySize = categoryCount;  // 调整为7
    for (int i = 0; i < categoryCount; i++)
    {
        if (valuesProperty.GetArrayElementAtIndex(i).floatValue == 0)
        {
            valuesProperty.GetArrayElementAtIndex(i).floatValue = DEFAULT_INTEREST;  // 默认2.0
        }
    }
}
```

**效果**:
- CustomerArchetype的Inspector会显示7个兴趣输入框
- Trait的Inspector会显示7个加成/倍率输入框
- 新增类别的默认值：加成=0，倍率=1.0，基础兴趣=2.0

#### 3.2 运行时数据自动扩展
**文件**: `CustomerArchetype.cs:97-103`

```csharp
public float[] GetBaseInterest(int categories)
{
    baseInterest.EnsureSize(categories, 2f);  // 自动扩展到7个，默认值2.0
    // ...
}
```

**文件**: `CustomerRecord.cs:49-56`

```csharp
public void EnsureInterestSize(int size)
{
    if (interestPersonalDelta.Length == size) return;
    var arr = new float[size];
    for (int i = 0; i < size; i++)
        arr[i] = (i < interestPersonalDelta.Length) ? interestPersonalDelta[i] : 0f;
    interestPersonalDelta = arr;  // 自动扩展到7个，默认值0.0
}
```

**触发时机**:
- CustomerSpawner生成顾客时
- ComposeFinalInterest()计算时
- Unity序列化系统加载SO时

---

### 步骤4: 验证自动同步结果

#### 4.1 检查编辑器UI
1. 打开任意 CustomerArchetype 资产
2. 找到 `Base Interest` 折叠栏
3. **预期**: 看到7个输入框（Lingerie到Toys）
4. 新增的Toys默认值应为 `2.0`

![Expected UI](https://via.placeholder.com/600x200?text=7+Interest+Fields)

#### 4.2 检查Trait资产
1. 打开任意 Trait 资产
2. 找到 `Interest Add` 和 `Interest Mul` 数组
3. **预期**:
   - Interest Add: 7个元素，新增值为 `0`
   - Interest Mul: 7个元素，新增值为 `1.0`

#### 4.3 运行时验证
```csharp
// 在CustomerSpawner或CustomerAgent中添加调试代码
Debug.Log($"Category Count: {System.Enum.GetValues(typeof(ProductCategory)).Length}");
Debug.Log($"Interest Array Length: {adapter.interestFinal.Length}");
// 预期输出: Category Count: 7, Interest Array Length: 7
```

---

## 自动同步机制

### ✅ 完全自动的部分

| 系统 | 自动同步内容 | 触发机制 |
|------|------------|---------|
| **编辑器UI** | InterestArray字段显示7个输入框 | PropertyDrawer.OnGUI() |
| **ScriptableObject** | 所有CustomerArchetype/Trait数组自动扩展 | Unity序列化系统 |
| **运行时数据** | CustomerRecord.interestPersonalDelta扩展 | EnsureInterestSize()调用 |
| **策略系统** | CustomerContext.interest[]自动适配 | ComposeFinalInterest()传递 |
| **黑板系统** | NodeCanvas黑板变量数组长度 | CustomerBlackboardAdapter初始化 |

### 核心自动化代码

#### 编辑器层面
```csharp
// InterestArrayPropertyDrawer.cs:23-24
string[] categoryNames = System.Enum.GetNames(typeof(ProductCategory));
int categoryCount = categoryNames.Length;  // 🔄 动态获取，无需硬编码
```

#### 运行时层面
```csharp
// CustomerSpawner.cs 调用链
CustomerAgent.Initialize(record)
  → record.ComposeFinalInterest(archetype, categories, traits)
    → archetype.GetBaseInterest(categories)  // 🔄 自动扩展baseInterest
      → baseInterest.EnsureSize(categories, 2f)
    → record.EnsureInterestSize(categories)  // 🔄 自动扩展personalDelta
```

---

## 手动同步清单

### ⚠️ 可能需要手动调整的地方

#### 1. RandomPurchasePolicy 配置（可选）

**位置**: `Assets/Resources/ScriptableObjects/BehaviorPolicies/`

**场景**: 如果需要为新类别设置特定的购买数量范围

**操作**:
1. 打开对应的 RandomPurchasePolicy 资产
2. 在 `Category Overrides` 数组中添加新元素
3. 设置:
   ```
   categoryIndex: 6  ← Toys的索引
   minBuy: 1
   maxBuy: 3
   ```

**代码参考**: `RandomPurchasePolicy.cs:30-50`

---

#### 2. CategoryManager 倍率系统（未来扩展）

**位置**: `Assets/Scripts/Manager/CategoryManager.cs`

**当前状态**: 原型期简化实现，恒返回1.0倍率

```csharp
public float GetCategoryMultiplier(ProductCategory c) => 1f; // 原型：恒为1
```

**未来扩展**: 如需实现类别升级系统
```csharp
// 示例扩展代码
[SerializeField] private float[] categoryMultipliers = new float[7]; // 手动同步

public float GetCategoryMultiplier(ProductCategory c)
{
    int index = (int)c;
    return (index >= 0 && index < categoryMultipliers.Length)
        ? categoryMultipliers[index]
        : 1f;
}
```

**手动同步**: 需要手动调整数组大小为新的类别数量

---

#### 3. JSON数据文件（运行时自动兼容）

**位置**: `Assets/StreamingAssets/Customers.json`

**自动兼容机制**:
```csharp
// CustomerRecord.EnsureInterestSize() 会自动处理旧数据
// 旧数据 interestPersonalDelta=[6个值] → 自动扩展为[7个值]，新值填充0
```

**无需手动操作**: 旧的JSON文件会在加载时自动升级

**验证方式**:
```csharp
// CustomerRepository.LoadAll() 加载后
foreach (var record in records)
{
    Debug.Assert(record.interestPersonalDelta.Length == 7, "Interest array not expanded!");
}
```

---

#### 4. 特质配置（批量调整）

**场景**: 想让某些特质影响新类别

**示例**: 为 "Gay" 特质添加对 Toys 的兴趣加成

**操作**:
1. 打开 `Assets/Resources/ScriptableObjects/Traits/Gay.asset`
2. 找到 `Interest Add` 数组（现在有7个元素）
3. 设置 `Element 6 (Toys) = 1.0`  ← 增加1点兴趣

**批量修改**: 如果有很多特质需要调整，建议写编辑器脚本批处理

---

#### 5. UI显示层（根据需要）

**BuildingDetailPanel** (`Assets/Scripts/UI/BuildingInteraction/BuildingDetailPanel.cs`)

**当前**: 自动显示 `shelf.Category` 字段（枚举ToString()）

**如需本地化**:
```csharp
// 添加类别名称映射表
private static readonly Dictionary<ProductCategory, string> categoryDisplayNames = new()
{
    { ProductCategory.Lingerie, "Lingerie" },
    { ProductCategory.Condom, "Condoms" },
    // ...
    { ProductCategory.Toys, "Adult Toys" }  ← 添加新映射
};
```

---

## 测试验证

### 完整测试清单

#### ✅ 编辑器测试

1. **枚举验证**
   ```csharp
   // Unity编辑器控制台执行
   Debug.Log(System.Enum.GetValues(typeof(ProductCategory)).Length);
   // 预期输出: 7
   ```

2. **Inspector验证**
   - [ ] CustomerArchetype显示7个兴趣字段
   - [ ] Trait显示7个Add和7个Mul字段
   - [ ] 新字段默认值正确（Add=0, Mul=1.0, Interest=2.0）

3. **SO资产测试**
   ```csharp
   // 编辑器脚本测试
   var archetype = Resources.Load<CustomerArchetype>("CustomerArchetypes/OfficeWorker");
   var interests = archetype.GetBaseInterest(7);
   Debug.Assert(interests.Length == 7, "Base interest not expanded!");
   ```

---

#### ✅ 运行时测试

1. **生成顾客测试**
   ```csharp
   // 在CustomerSpawner.SpawnCustomer()后添加
   Debug.Log($"Generated customer interest length: {record.interestPersonalDelta.Length}");
   // 预期: 7
   ```

2. **策略选择测试**
   - 在场景中放置新的 Toys Shelf
   - 运行游戏，观察顾客是否会选择新货架
   - 检查日志：`WeightedRandomSelector` 的得分计算

3. **兴趣计算测试**
   ```csharp
   // CustomerAgent.Initialize()中添加
   for (int i = 0; i < adapter.interestFinal.Length; i++)
   {
       Debug.Log($"Category {(ProductCategory)i}: Interest={adapter.interestFinal[i]}");
   }
   ```

---

#### ✅ 数据兼容性测试

1. **旧数据加载**
   - 备份 `StreamingAssets/Customers.json`
   - 修改枚举并保存
   - 运行游戏，检查旧顾客数据是否正常加载
   - 预期：旧的6元素数组自动扩展为7元素

2. **混合版本测试**
   - 场景中同时有旧货架（6类别）和新货架（7类别）
   - 验证顾客能正确访问两种货架

---

## 常见问题

### Q1: 修改枚举后编辑器显示错误？

**症状**: Inspector显示 "Array size mismatch"

**原因**: Unity缓存未刷新

**解决**:
1. 关闭Unity编辑器
2. 删除 `Library/` 文件夹
3. 重新打开项目

---

### Q2: 旧的CustomerRecord数据丢失？

**症状**: 旧顾客的兴趣值变为全2.0

**原因**: 数据迁移问题

**解决**:
```csharp
// 修改 CustomerRecord.EnsureInterestSize()
public void EnsureInterestSize(int size)
{
    if (interestPersonalDelta == null) interestPersonalDelta = Array.Empty<float>();
    if (interestPersonalDelta.Length == size) return;

    var arr = new float[size];
    for (int i = 0; i < size; i++)
    {
        // 保留旧值，新值填充0（个体偏移默认为0）
        arr[i] = (i < interestPersonalDelta.Length) ? interestPersonalDelta[i] : 0f;
    }
    interestPersonalDelta = arr;
}
```

---

### Q3: 策略不选择新类别的货架？

**症状**: 顾客始终忽略新货架

**排查步骤**:

1. **检查库存**
   ```csharp
   // ShelfInstance.cs
   Debug.Log($"Shelf {archetypeId} stock: {currentStock}");
   ```

2. **检查兴趣值**
   ```csharp
   // WeightedRandomSelector.cs
   float interest = GetInterestForCategory(ctx.interest, shelf.categoryIndex);
   Debug.Log($"Interest for category {shelf.categoryIndex}: {interest}");
   ```

3. **检查阈值**
   ```csharp
   // WeightedRandomSelector.cs:interestThreshold
   // 默认值: 0.1
   // 如果新类别兴趣 < 0.1，会被过滤
   ```

**解决**: 调整CustomerArchetype的baseInterest或Trait的interestAdd

---

### Q4: 如何批量更新所有Trait？

**场景**: 50+个Trait需要为新类别设置默认值

**方案1: 编辑器脚本**
```csharp
[MenuItem("PopLife/Update Traits for New Category")]
static void UpdateAllTraits()
{
    var guids = AssetDatabase.FindAssets("t:Trait");
    foreach (var guid in guids)
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        var trait = AssetDatabase.LoadAssetAtPath<Trait>(path);

        // 扩展数组
        if (trait.interestAdd == null || trait.interestAdd.Length < 7)
        {
            var newAdd = new float[7];
            if (trait.interestAdd != null)
                Array.Copy(trait.interestAdd, newAdd, trait.interestAdd.Length);
            trait.interestAdd = newAdd;
        }

        if (trait.interestMul == null || trait.interestMul.Length < 7)
        {
            var newMul = new float[7];
            if (trait.interestMul != null)
                Array.Copy(trait.interestMul, newMul, trait.interestMul.Length);
            // 新增元素默认为1.0
            for (int i = trait.interestMul.Length; i < 7; i++)
                newMul[i] = 1.0f;
            trait.interestMul = newMul;
        }

        EditorUtility.SetDirty(trait);
    }
    AssetDatabase.SaveAssets();
    Debug.Log("All traits updated!");
}
```

**方案2: 自动触发**
- PropertyDrawer已实现自动扩展
- 打开每个Trait资产即可触发自动更新

---

### Q5: 能否一键同步所有？

**答案**: 已实现！

**自动同步覆盖范围**:
- ✅ 编辑器UI（PropertyDrawer自动）
- ✅ ScriptableObject数组（序列化系统自动）
- ✅ 运行时数据（EnsureSize()自动）
- ✅ 策略系统（动态数组自动）

**唯一需要手动的**:
1. 修改枚举定义（1行代码）
2. 创建新货架原型SO（右键菜单）
3. 可选：调整RandomPurchasePolicy配置

**总工作量**: 约2-5分钟

---

## 快速参考

### 关键文件位置
```
枚举定义:
  Assets/Scripts/Data/BuildingArchetypes.cs:16

编辑器UI:
  Assets/Scripts/Customers/Editor/InterestArrayPropertyDrawer.cs
  Assets/Scripts/Customers/Editor/TraitInterestPropertyDrawer.cs

运行时计算:
  Assets/Scripts/Customers/Runtime/CustomerRecord.cs:57 (ComposeFinalInterest)
  Assets/Scripts/Customers/Data/CustomerArchetype.cs:97 (GetBaseInterest)

策略系统:
  Assets/Scripts/Customers/Data/Policies/WeightedRandomSelector.cs
  Assets/Scripts/Customers/Data/Policies/RandomPurchasePolicy.cs

数据持久化:
  Assets/StreamingAssets/Customers.json
```

### 扩展模板代码

```csharp
// 1. 修改枚举
public enum ProductCategory {
    Lingerie, Condom, Vibrator, Fleshlight, Lubricant, BDSM,
    NewCategory  // ← 添加新类别
}

// 2. 验证代码
int categoryCount = System.Enum.GetValues(typeof(ProductCategory)).Length;
Debug.Log($"Total categories: {categoryCount}");  // 预期: 7

// 3. 测试兴趣计算
var interests = record.ComposeFinalInterest(archetype, categoryCount, traits);
Debug.Assert(interests.Length == categoryCount);
```

---

## 总结

### 系统优势
1. **数据驱动**: 枚举为唯一真实来源
2. **自动扩展**: PropertyDrawer + EnsureSize()机制
3. **向后兼容**: 旧数据自动迁移
4. **类型安全**: 编译期检查枚举索引

### 最佳实践
- ✅ 始终在枚举末尾添加新值
- ✅ 使用枚举索引而非硬编码数字
- ✅ 扩展后进行完整测试（编辑器+运行时）
- ✅ 备份JSON数据文件
- ❌ 不要删除或重排现有枚举值

### 维护建议
1. 每次扩展后更新本文档
2. 记录新类别的设计意图（游戏性、平衡性）
3. 建立类别测试场景（快速验证新类别）
4. 定期检查数据一致性（CategoryCount == InterestArrayLength）

---

**文档版本**: 1.0
**最后更新**: 2025-10-29
**当前类别数量**: 6
**负责人**: AI Assistant (Claude Code)
