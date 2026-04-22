# Quest 条件 - 指定 Shelf Archetype 过滤设计

**日期**：2026-04-22
**状态**：设计确认中

## 背景

Quest 系统的 `PlaceShelves` 条件目前只支持两种粒度：

1. **无过滤**：统计所有货架数量
2. **按 `ProductCategory` 过滤**：如"放置 2 个 Dildo 类别的货架"（`useFilterCategory = true` + `filterCategory`）

设计师需要更精确的过滤：**指定具体的若干 `ShelfArchetype` SO**，以 OR 关系任一满足即可计数。
典型场景："放置 2 个 Cucci 品牌的 Dildo 货架（以下 3 款任选）"。

## 目标 & 非目标

### 目标

- `QuestCondition` 支持通过拖入 `ShelfArchetype` SO 列表来精确过滤
- 在 Quest SO Inspector 配置时，拖入即用，无需写代码
- 现有 Quest SO 零迁移，现有"按类别过滤"逻辑完全保留
- 仅作用于 `PlaceShelves` 条件类型

### 非目标

- 不扩展到 `PlaceBuildings`（facility 过滤目前无需求）
- 不扩展到 `SellItems`（卖出条件语义上按类别足够）
- 不支持"必须全部放置（AND）"语义 —— 该语义可以通过多个 Entry 拆分实现
- 不做 UI Inspector 美化（Odin ShowIf 控制是可选加强）

## 前置澄清：PlaceShelves 的 scope 语义

**当前实现上，`PlaceShelves`（以及 `PlaceBuildings`）是"快照型"条件，不是事件累加型。**

在 [QuestProgressTracker.HandleBuildingChanged()](../Scripts/Quest/QuestProgressTracker.cs#L198) 中，每次建造/拆除/移动/升级事件触发时，直接调用 `SetCounter(questName, i, CountShelves(cond), cond)` 用**当前场上货架数量**覆盖写，不做增量累加。

这意味着：

- `QuestCondition.scope` 字段在 `PlaceShelves` / `PlaceBuildings` 下**实际上被忽略**
- 设计师配 `Cumulative` 或 `Daily` 不会报错，但运行时行为仍然是快照覆盖
- 拆除货架时，计数会**回退**（快照语义决定）。但已标记为 `Success` 的 Entry 不会被重置（`CheckAndUpdateEntry` 只做 `>=` 判定，不做反向）

**统一约定**：`PlaceShelves` / `PlaceBuildings` 的 scope 推荐一律配 `Current`。本次改动**不改变**这个运行时语义 —— 只在文档和 `OnValidate` 警告中说明。

在 `QuestDefinition.OnValidate()` 中对非 `Current` 的 `PlaceShelves` / `PlaceBuildings` 条件打一条 `Debug.LogWarning`，提示设计师该字段对这两种类型无效。

## 设计

### 数据层改动：`QuestCondition.cs`

新增两个字段：

```csharp
[Tooltip("是否启用具体 ShelfArchetype 过滤（仅 PlaceShelves 使用）。与 useFilterCategory 互斥，优先级更高")]
public bool useFilterArchetypes = false;

[Tooltip("指定的 ShelfArchetype 列表（拖入 SO），OR 关系。启用 useFilterArchetypes 时有效")]
public ShelfArchetype[] filterArchetypes;
```

### 过滤优先级

统计 `PlaceShelves` 计数时的判定顺序（互斥，只走一个分支；`useFilterArchetypes` 一旦为 true 就**锁死**在 archetype 分支，不会 fall through 到 category）：

1. `useFilterArchetypes == true`：
   - 列表有效（`HasAnyValidArchetype` 返回 true）→ 按 archetype 列表过滤
   - 列表无效（null / 长度 0 / 全空槽）→ **直接返回 0**（不回退到 category），由 `OnValidate` 警告提醒
2. `useFilterArchetypes == false` 且 `useFilterCategory == true` → 按 category 过滤（现有逻辑）
3. 两个 bool 都 false → 不过滤，统计所有货架

**空列表保护**：若 `useFilterArchetypes == true` 但 `filterArchetypes` 无任何有效元素，计数永远为 0（任务永远无法完成）。

判定"无效"的三种情况（需同时检测，不能只看 `Length`）：

1. `filterArchetypes == null`
2. `filterArchetypes.Length == 0`
3. `filterArchetypes.Length > 0` 但**每一个元素都是 null**（Inspector 里常见：size 设成 3 但三个槽都是空的）

三种情况在 `OnValidate` 中都要 `Debug.LogWarning` 并在运行时统计中跳过（`CountShelves` 返回 0）。统一实现一个 helper：

```csharp
private static bool HasAnyValidArchetype(ShelfArchetype[] list)
{
    if (list == null || list.Length == 0) return false;
    for (int i = 0; i < list.Length; i++)
        if (list[i] != null) return true;
    return false;
}
```

`CountShelves` 里遍历匹配时也要跳过 null 元素，避免 `sa == null` 的误匹配。

### 运行时层改动：`QuestProgressTracker.CountShelves()`

伪代码：

```csharp
private int CountShelves(QuestCondition cond)
{
    var wg = WorldGrid.Instance;
    if (wg == null) return 0;

    // 优先级 1: archetype 列表过滤（空列表保护：无有效元素时计数为 0）
    bool useArchetypeFilter = cond.useFilterArchetypes && HasAnyValidArchetype(cond.filterArchetypes);
    if (cond.useFilterArchetypes && !useArchetypeFilter)
        return 0; // 勾选了但没有有效元素：永远 0

    int count = 0;
    foreach (var shelf in wg.AllShelves())
    {
        var sa = shelf.archetype as ShelfArchetype;
        if (sa == null) continue;

        if (useArchetypeFilter)
        {
            for (int i = 0; i < cond.filterArchetypes.Length; i++)
            {
                if (cond.filterArchetypes[i] == null) continue; // 跳过空槽
                if (cond.filterArchetypes[i] == sa) { count++; break; }
            }
        }
        // 优先级 2: category 过滤（现有）
        else if (cond.useFilterCategory)
        {
            if (sa.category == cond.filterCategory) count++;
        }
        // 优先级 3: 无过滤
        else
        {
            count++;
        }
    }
    return count;
}
```

比较用 SO 引用相等（`==`），不走 `archetypeId` 字符串，符合 CLAUDE.md 中"纯运行时场景用 SO 引用直接比较"的约定。

### Inspector 显示（可选加强）

使用 Odin `[ShowIf]` / `[HideIf]` 控制字段显示，避免用户同时勾选两个 bool：

- `filterArchetypes` 只在 `useFilterArchetypes == true` 时显示
- `filterCategory` 只在 `useFilterCategory == true` 时显示
- `useFilterArchetypes` 只在 `conditionType == PlaceShelves` 时显示

这部分是纯 UI 改进，不影响数据逻辑，可以后续按需加。

### 持久化

无影响。`QuestProgressTracker` 的计数器是整数数组，过滤逻辑只在统计当前快照时用，不改变存档格式。

## 使用示例（设计师视角）

在 Quest SO 中配一条 Entry："放置 2 个 Cucci Dildo 货架"：

1. Conditions 数组新增一项
2. `conditionType` = `PlaceShelves`
3. `scope` = `Current`（强约定：`PlaceShelves` 运行时恒为快照语义，其他值无效但不报错）
4. `targetValue` = 2
5. `useFilterArchetypes` = `true`
6. `filterArchetypes` 拖入 3 个 SO：`Shelf_Cucci_Dildo_L1`, `Shelf_Cucci_Dildo_L2`, `Shelf_Cucci_Dildo_L3`
7. `entryTexts[i]` 对应文本："Place 2 Cucci Dildo shelves"

玩家放置其中任一 archetype 的货架都计入，累计到 2 时该 Entry 完成。

## 测试计划

在 Unity Editor 中手动验证：

1. **基础过滤**：配置一个只含 `Shelf_A` 的列表，放置 2 个 `Shelf_A` → Entry 完成
2. **OR 关系**：配置 `[Shelf_A, Shelf_B]`，放置 1 A + 1 B → Entry 完成
3. **负向**：列表为 `[Shelf_A]`，放置 `Shelf_B` 2 个 → Entry 不完成
4. **互斥**：同时勾选两个 bool，验证走 archetype 分支（category 分支被旁路）
5. **空列表保护 - null 数组**：勾选 `useFilterArchetypes`，`filterArchetypes` 未初始化 → OnValidate 警告，计数恒 0
6. **空列表保护 - 零长度**：勾选 `useFilterArchetypes`，`filterArchetypes.Length == 0` → OnValidate 警告，计数恒 0
7. **空列表保护 - 全空槽**：勾选 `useFilterArchetypes`，`filterArchetypes = [null, null, null]` → OnValidate 警告，计数恒 0
8. **部分空槽**：`filterArchetypes = [Shelf_A, null, Shelf_B]` → 跳过 null，A/B 任一仍能计入
9. **拆除快照回退**：放置达标前先 1 个 → 计数 1；再拆除 → 计数回退到 0（快照语义）。已标记 Success 的 Entry 不回退
10. **scope 告警**：配置 `PlaceShelves` + `scope = Cumulative` → OnValidate 打 warning 提示 scope 被忽略

## 改动清单

| 文件 | 改动 | 预计行数 |
|---|---|---|
| `Scripts/Quest/QuestCondition.cs` | 加 2 个字段 + Tooltip | ~5 |
| `Scripts/Quest/QuestProgressTracker.cs` | `CountShelves()` 加 archetype 分支 + `HasAnyValidArchetype` helper | ~15 |
| `Scripts/Data/QuestDefinition.cs` | `OnValidate` 加空列表警告 + scope 警告 | ~15 |
| **合计** | | **~35 行** |

Odin ShowIf UI 优化属于可选加强，不计入核心改动。

## 风险

- **空列表遗忘**：设计师勾选了 `useFilterArchetypes` 但忘记拖 SO → 任务无法完成。**缓解**：`OnValidate` 检测三种"无效列表"情况并 `Debug.LogWarning`
- **scope 字段误配**：设计师可能误以为 `Cumulative` 可用于"累计放置过的货架"。**缓解**：`OnValidate` 对 `PlaceShelves`/`PlaceBuildings` 非 `Current` 的 scope 打 warning。本次不改变运行时行为（无反向兼容风险），只文档 + 警告提醒
- **未来扩展到 SellItems / PlaceBuildings**：若后续需要，将同样两个字段（及其 ShowIf 条件）推广过去；当前 YAGNI 不做
