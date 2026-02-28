# Item Codex 系统文档

## 概述

Item Codex（物品图鉴）是一个供玩家浏览所有货架物品的百科全书式 UI 面板。从 AlanBot 菜单打开，展示物品的详细信息（描述、使用说明、数值），并追踪玩家是否查看过每个条目。

---

## 文件结构

| 文件 | 命名空间 | 说明 |
|------|----------|------|
| `Scripts/Data/ShelfArchetypes.cs` | `PopLife.Data` | 修改 - 新增 `usageInstruction` 字段 |
| `Scripts/Data/CodexMasterList.cs` | `PopLife.Data` | 新建 - Codex 排序主表 ScriptableObject |
| `Scripts/Manager/CodexStateManager.cs` | `PopLife` | 新建 - "已查看"状态管理单例 |
| `Scripts/AlanBot/UI/ItemCodexPanel.cs` | `PopLife.AlanBot.UI` | 重写 - 完整图鉴面板逻辑 |
| `Scripts/AlanBot/UI/CodexShelfEntry.cs` | `PopLife.AlanBot.UI` | 新建 - 左侧列表条目组件 |
| `Scripts/UI/FilterToggleButton.cs` | `PopLife.UI` | 修改 - 新增可选 sprite 切换支持 |
| `Scripts/Manager/GameStateManager.cs` | 现有 | 修改 - 清档列表新增 `ItemCodex.es3` |

---

## 系统架构

```
┌─────────────────────────────────────────────────────┐
│           AlanBotSelectionPanel                      │
│  点击 Item Codex 按钮 → Hide 自己 → Show Codex     │
│  关闭回调 savedCallback 传递给 Codex                │
└──────────────────────┬──────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────┐
│              ItemCodexPanel                          │
│  ┌─────────────────────────────────────────────┐    │
│  │ Header: 标题 + 类别横向滚动标签              │    │
│  │ (FilterToggleButton + FilterToggleGroup)     │    │
│  ├─────────────────┬───────────────────────────┤    │
│  │ Left Panel      │ Right Panel               │    │
│  │ - 搜索框        │ - 货架图片                 │    │
│  │ - 排序按钮      │ - 名称/品牌/类别           │    │
│  │ - 条目列表      │ - 描述/使用说明             │    │
│  │ - Mark All Seen │ - 数值统计 (Lv1)           │    │
│  └─────────────────┴───────────────────────────┘    │
│  关闭按钮 → Hide → 触发 onCloseCallback            │
└──────────────────────┬──────────────────────────────┘
                       │
          ┌────────────┼────────────┐
          ▼            ▼            ▼
  CodexStateManager  BlueprintMgr  CodexMasterList
  (已查看状态/ES3)   (解锁状态)    (排序主表/SO)
```

---

## 数据层

### ShelfArchetype 新增字段

```csharp
[Header("使用说明")]
[TextArea(3, 10)]
public string usageInstruction;
```

在 `description` 之后，用于在右侧详情面板显示 "USAGE INSTRUCTIONS" 区域的文本。

### CodexMasterList (ScriptableObject)

- **菜单路径**: Create → PopLife → Codex → CodexMasterList
- **字段**: `List<ShelfArchetype> shelves` — 设计师在编辑器中手动排列的所有货架引用
- **用途**: 定义图鉴的 "Codex" 默认排序顺序
- **API**:
  - `IReadOnlyList<ShelfArchetype> Shelves` — 只读列表
  - `int GetSortIndex(ShelfArchetype)` — 返回排序索引（未找到返回 `int.MaxValue`）
- **编辑器辅助**: 右键 ContextMenu → "Auto-fill from Resources"，自动扫描 `Resources/ScriptableObjects/BuildingArchetype/Shelf/` 添加缺失的 SO

**资产创建步骤**:
1. Project 窗口右键 → Create → PopLife → Codex → CodexMasterList
2. 放到 `Assets/Resources/ScriptableObjects/` 下
3. Inspector 中右键 → Auto-fill from Resources
4. 手动调整排序顺序

---

## CodexStateManager（状态管理器）

### 职责
追踪玩家是否查看过每个货架图鉴条目，持久化到 ES3。

### 单例模式
```csharp
CodexStateManager.Instance
```
需要挂载到场景中的 Manager GameObject 上。

### ES3 持久化
- **文件**: `ItemCodex.es3`
- **键**: `seenShelfIds` → `List<string>`（已查看的货架 archetypeId 集合）

### 事件
| 事件 | 触发时机 |
|------|---------|
| `OnShelfSeen(string shelfId)` | 玩家点击查看了某个条目 |
| `OnNewShelfAvailable(string shelfId)` | 新蓝图解锁且该条目尚未被查看过 |

### API
| 方法 | 说明 |
|------|------|
| `bool IsShelfSeen(string shelfId)` | 检查是否已查看 |
| `void MarkShelfAsSeen(string shelfId)` | 标记为已查看（自动保存） |
| `void MarkAllAsSeen()` | 将所有已解锁货架标记为已查看 |
| `int GetUnseenCount()` | 获取未查看的已解锁货架数量 |

### 蓝图解锁监听
在 `OnEnable` 中订阅 `BlueprintManager.OnShelfUnlocked`，即使 Codex UI 关闭也持续监听。当新蓝图解锁时，如果该条目尚未被查看，触发 `OnNewShelfAvailable` 事件。

---

## CodexShelfEntry（列表条目组件）

### 为何不使用 Unity 原生 Button/Selectable

Unity Button 支持 Normal/Highlighted/Pressed/Selected/Disabled 5 种状态，由引擎自动管理过渡。我们需要 4 种自定义业务状态（Unselected/Selected/New/Locked），其视觉和行为规则更复杂：
- New 状态点击后变 Selected，再取消选中后变 Unselected（永远不会回到 New）
- Locked 状态要自定义 tint 和替换文字

因此使用 `IPointerClickHandler` + 手动状态枚举管理。

### 4 种状态

| 状态 | 背景 Sprite | 文字 | 图标 | 可点击 |
|------|------------|------|------|--------|
| **Unselected** | `unselectedSprite` + 白色 | displayName + 正常色 | 显示 | 是 |
| **Selected** | `selectedSprite` + 白色 | displayName + 高亮色 | 显示 | 是 |
| **New** | `newSprite` + 白色（sprite 自带 New 角标） | displayName + 正常色 | 显示 | 是 |
| **Locked** | `selectedSprite` + lockedTintColor | "???" + 暗色 | 隐藏 | 否 |

### Inspector 配置

| 字段 | 说明 |
|------|------|
| `backgroundImage` | 背景 Image 组件 |
| `iconImage` | 货架图标 Image |
| `nameText` | 名称 TMP 文字 |
| `unselectedSprite` | 未选中状态背景 |
| `selectedSprite` | 选中状态背景 |
| `newSprite` | 新条目状态背景 |
| `normalTextColor` | 正常文字颜色 |
| `selectedTextColor` | 选中文字颜色 |
| `lockedTextColor` | 锁定文字颜色 |
| `lockedTintColor` | 锁定背景着色 |

### 预制体结构

```
CodexShelfEntry (GameObject)
├── Image (backgroundImage) + CodexShelfEntry 脚本
├── Icon (Image, iconImage)
└── Name (TextMeshProUGUI, nameText)
```

> **注**: New 状态的角标效果由 `newSprite` 自带，无需额外的角标 GameObject。

---

## ItemCodexPanel（主面板）

### Header 区域

- **标题**: "ITEM CODEX" + 图标
- **类别横向滚动**: 复用 `FilterToggleButton` + `FilterToggleGroup`
  - "ALL" + 11 个 ProductCategory 标签（字母排序）
  - 所有类别标签默认全部显示，不做锁定处理
  - 左右滚动按钮控制 `ScrollRect.horizontalNormalizedPosition`
  - 预制体: `Prefab/UIs/categoryContainerButton.prefab`

### 左侧面板

**搜索框**: `TMP_InputField`，placeholder "Search..."，实时过滤。

**排序按钮**: 点击展开下拉列表（6 种排序）：

| 排序模式 | 说明 |
|---------|------|
| Codex ↑ | 按 CodexMasterList 排序（默认） |
| Codex ↓ | 按 CodexMasterList 倒序 |
| Alphabetical ↑ | A-Z |
| Alphabetical ↓ | Z-A |
| Build Cost ↑ | 价格低→高 |
| Build Cost ↓ | 价格高→低 |

**条目列表**: ScrollRect，动态实例化 `CodexShelfEntry` 预制体。

**过滤+排序逻辑 (AND 链)**:
1. Category filter（类别标签选择）
2. Search filter（displayName 大小写不敏感子串匹配）
3. Sort（当前排序模式）

**Mark All As Seen 按钮**: 调用 `CodexStateManager.MarkAllAsSeen()` 后刷新列表。

### 右侧面板

右侧面板分为**始终显示区域**和**可切换区域**（Detail 页 / Stats 页），通过两个切换按钮控制。

**始终显示（不随页面切换变化）：**

| 区域 | 数据来源 |
|------|---------|
| 货架图片 | `shelf.icon`，`preserveAspect = true` |
| 名称 | `shelf.displayName` |
| 品牌 | `shelf.brand.displayName` |
| 类别 | `shelf.category.ToString()` |

**Detail 页（默认显示）：**

| 区域 | 数据来源 |
|------|---------|
| 描述 | `shelf.description` |
| 使用说明 | `shelf.usageInstruction` |

**Stats 页：**

| 区域 | 数据来源 |
|------|---------|
| Build Cost | `shelf.buildCost` |
| Maintenance Fee | `shelf.GetMaintenanceFee(1)` |
| Sell Price | `shelf.GetPrice(1)` |
| Stock | `shelf.GetStock(1)` |
| Appeal | `shelf.GetAppeal(1)` |

所有数值均为 Level 1 基础值。

**切换逻辑：**
- 点击 Detail 按钮 → `detailPage` 显示，`statsPage` 隐藏
- 点击 Stats 按钮 → `statsPage` 显示，`detailPage` 隐藏
- 切换条目时保持当前页面选择（不会自动跳回 Detail）
- 按钮视觉：激活页按钮正常颜色，非激活页按钮半透明

### 关闭按钮

右上角关闭按钮 → `Hide()` → 淡出 → `onCloseCallback?.Invoke()` → 恢复 AlanBot 状态。

### Inspector 配置清单

```
面板控制:
  - panelRoot (GameObject)
  - canvasGroup (CanvasGroup)
  - closeButton (Button)

Header:
  - titleText (TMP_Text)

Category Tabs:
  - categoryTabContainer (Transform) — 横向 Content 容器
  - categoryTabPrefab (GameObject) — categoryContainerButton.prefab
  - scrollLeftButton (Button)
  - scrollRightButton (Button)
  - categoryScrollRect (ScrollRect)

Left Panel - Search & Sort:
  - searchInput (TMP_InputField)
  - sortButton (Button)
  - sortDropdown (GameObject)
  - sortCodexAscButton / sortCodexDescButton (Button)
  - sortAlphaAscButton / sortAlphaDescButton (Button)
  - sortCostAscButton / sortCostDescButton (Button)

Left Panel - List:
  - listScrollRect (ScrollRect)
  - listContainer (Transform) — 垂直 Content 容器
  - entryPrefab (CodexShelfEntry)
  - markAllSeenButton (Button)

Right Panel:
  - detailContent (GameObject) — 有内容时显示
  - detailEmptyState (GameObject) — 无选中时显示
  - shelfImage (Image)
  - shelfNameText / brandText / categoryText (TMP_Text)

Right Panel - Page Toggle:
  - detailTabButton (Button) — "Detail" 切换按钮
  - statsTabButton (Button) — "Stats" 切换按钮
  - detailPage (GameObject) — 描述+使用说明容器
  - statsPage (GameObject) — 数值统计容器
  - activeTabColor (Color) — 激活按钮颜色（默认白色）
  - inactiveTabColor (Color) — 非激活按钮颜色（默认半透明）

Right Panel - Detail Page:
  - descriptionText / usageInstructionText (TMP_Text)

Right Panel - Stats Page:
  - buildCostText / maintenanceFeeText / sellPriceText (TMP_Text)
  - stockText / appealText (TMP_Text)

Data:
  - masterList (CodexMasterList) — 拖入 CodexMasterList.asset
```

---

## FilterToggleButton 修改

新增两个可选字段：

```csharp
[Header("Sprite Settings (可选，设置后优先使用sprite切换)")]
[SerializeField] private Sprite normalSprite;
[SerializeField] private Sprite selectedSprite;
```

行为：
- 如果 `normalSprite` 和 `selectedSprite` 都设置了 → 切换 sprite + 切换颜色
- 如果未设置 → 仅切换颜色（原有行为，完全向后兼容）

现有使用 `FilterToggleButton` 的地方（如 ShelfListPanel）不受影响。

---

## 事件流

### 蓝图解锁 → 图鉴更新

```
BlueprintManager.AddBlueprint(archetypeId)
  └→ OnShelfUnlocked.Invoke(shelf)
       ├→ CodexStateManager.OnBlueprintShelfUnlocked(shelf)
       │    └→ if 未查看过: OnNewShelfAvailable.Invoke(archetypeId)
       │         └→ ItemCodexPanel.OnNewShelfAvailable() [面板可见时]
       │              └→ RefreshAllEntries() — 条目从 Locked → New
       └→ ShelfListPanel.OnShelfBlueprintUnlocked(shelf) [现有逻辑不变]
```

### 玩家查看条目

```
点击 CodexShelfEntry
  └→ ItemCodexPanel.OnEntryClicked(entry)
       ├→ 旧 selectedEntry.SetState(Unselected)
       ├→ if entry 是 New: CodexStateManager.MarkShelfAsSeen(id)
       │    └→ ES3 保存 → OnShelfSeen.Invoke(id)
       ├→ entry.SetState(Selected)
       └→ ShowDetailForShelf(entry.Shelf) — 右侧面板更新
```

### Mark All As Seen

```
点击 Mark All As Seen 按钮
  └→ CodexStateManager.MarkAllAsSeen()
       └→ 遍历所有已解锁 ID → seenShelfIds.Add → ES3 保存
  └→ RefreshAllEntries() — 所有 New → Unselected
```

### 面板打开/关闭

```
AlanBot 点击 → AlanBotSelectionPanel.Show(hideCallback)
  └→ 点击 Item Codex 按钮
       ├→ 保存 hideCallback
       ├→ Hide 选择面板（不触发回调）
       └→ itemCodexPanel.Show(closeCallback: () => hideCallback())

关闭 Codex:
  └→ ItemCodexPanel.Hide()
       └→ 淡出 → panelRoot.SetActive(false)
            └→ onCloseCallback?.Invoke()
                 └→ 恢复 AlanBot 状态（表情、行为树）
```

---

## 清档集成

`GameStateManager.ClearAllSaves()` 的 ES3 文件列表已包含 `"ItemCodex.es3"`，清档时会自动删除图鉴查看记录。

---

## Unity 编辑器操作清单

1. **创建 CodexMasterList 资产**
   - Project → Create → PopLife → Codex → CodexMasterList
   - 放到 `Resources/ScriptableObjects/`
   - ContextMenu → Auto-fill from Resources
   - 手动调整排序

2. **挂载 CodexStateManager**
   - 场景中 Manager GameObject → Add Component → CodexStateManager

3. **创建 CodexShelfEntry 预制体**
   - 结构: Image(bg) + Image(icon) + TMP(name)
   - 挂载 CodexShelfEntry 脚本
   - 配置 3 种 sprite（newSprite 自带 New 角标）+ 颜色

4. **搭建 ItemCodexPanel UI 层级**
   - 参照 Inspector 配置清单拖入引用
   - categoryTabPrefab 使用现有 `Prefab/UIs/categoryContainerButton.prefab`
   - masterList 拖入 CodexMasterList 资产

5. **填写 usageInstruction**
   - 每个 ShelfArchetype SO 的 Inspector 中填写使用说明文本
