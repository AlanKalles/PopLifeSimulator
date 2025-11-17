# Stats 面板功能说明

## 概述
Stats 面板是一个从屏幕左侧滑出的侧边栏面板，用于实时显示游戏统计数据，包括经济状况、货架销售情况和顾客消费记录。

---

## 一、面板结构

### 1. 触发方式
- **Stats 按钮**: 场景中的固定UI按钮（需在Unity编辑器中设置）
- **打开/关闭**: 点击按钮触发滑动动画（从左往右展开，再次点击从右往左收起）
- **ESC 关闭**: 面板打开时按 ESC 键可关闭
- **自动关闭**: 每日结算界面弹出时自动关闭

### 2. 面板布局
```
┌─────────────────────────────────┐
│  Stats Panel (20% 屏幕宽度)     │
├─────────────────────────────────┤
│ [ Economy ] [ Products ] [ CC ] │ ← Tab 按钮
├─────────────────────────────────┤
│                                 │
│     当前激活的子面板内容         │
│                                 │
│                                 │
└─────────────────────────────────┘
```

### 3. 三个子面板
- **Economy (E)**: 经济统计面板（默认）
- **Products (P)**: 货架销售统计面板
- **Current Customers (CC)**: 当日顾客消费面板

---

## 二、Economy 面板

### 显示内容
1. **Today's Revenue (今日收入)**
   - 数据来源: `DayLoopManager.dailyTotalSale`
   - 更新时机: 每秒刷新
   - 建造阶段: 显示 $0.00
   - 营业阶段: 实时显示顾客结账金额

2. **Maintenance Fee (维护费总额)**
   - 数据来源: 遍历所有建筑计算 `CurrentMaintenanceFee`
   - 更新时机: 每秒刷新
   - 建造阶段: 实时更新（建造/升级/销毁货架时变化）
   - 营业阶段: 保持不变

### 技术实现
- 文件: `EconomyPanelView.cs`
- 刷新间隔: 1秒
- UI组件: 2个 `TextMeshProUGUI`

---

## 三、Products 面板

### 显示内容
每个货架显示一个横幅列表项，包含：
- **Icon**: 货架图标 (Sprite)
- **Name**: 货架名称
- **Category**: 商品类别 (Lingerie, Condom, Vibrator, etc.)
- **Level**: 当前等级 (Lv.1, Lv.2, etc.)
- **Revenue**: 今日创造的收入 (todaySales × currentPrice)
- **Price**: 当前单价

### 筛选与排序

#### Category 过滤器 (Dropdown)
- **All**: 显示所有货架
- **Lingerie / Condom / Vibrator / Fleshlight / Lubricant / BDSM**: 按类别筛选

#### 排序方式 (Dropdown)
- **Revenue (默认)**: 按今日收入降序排列
- **Level**: 按等级降序排列
- **Unit Price**: 按单价降序排列

### 特殊逻辑
#### 建造阶段排序
- **条件**: 所有货架 Revenue = 0
- **排序**: 按加载顺序排列（不排序）
- **新建货架**: 显示在列表最上方

#### 营业阶段排序
- **实时更新**: 顾客从货架取货后立即更新并重新排序
- **动态调整**: Revenue 变化会导致列表项位置变化

### 更新时机
1. **建造阶段**:
   - 新建货架 → 立即添加到列表
   - 销毁货架 → 立即从列表移除
   - 升级货架 → 更新等级显示
   - 检测方式: 定时轮询建筑数量变化 (1秒间隔)

2. **营业阶段**:
   - 顾客购买 → 监听 `CustomerEventBus.OnPurchased` 事件
   - 立即刷新并重新排序

### 技术实现
- 文件: `ProductsPanelView.cs`, `ShelfStatsItemUI.cs`
- 预制体: `ShelfStatsItem.prefab`
- 数据来源: `StatsDataManager.GetAllShelfStats()`
- 列表容器: ScrollView + VerticalLayoutGroup

---

## 四、Current Customers 面板

### 显示内容
每个顾客显示一个横幅列表项，包含：
- **Icon**: 顾客头像 (Sprite)
- **Name**: 顾客姓名
- **Loyalty Level**: 忠诚度等级 (Loyalty Lv.0, Lv.1, etc.)
- **Spent**: 今日消费金额

### 底部统计
- **Customers in Store**: 当前店内顾客数量
  - 数据来源: 实时统计场景中 `CustomerAgent` 数量
  - 更新间隔: 1秒

### 顾客状态
1. **在店顾客**: 正常显示（不透明）
2. **离店顾客**: 置灰显示（透明度 50%，文字颜色变灰）
   - 离店后不会从列表移除
   - 保留在原位置

### 消费金额锁定机制
**问题**: `pendingPayment` 在收银台结账后会清零

**解决方案**:
- 监听 `CustomerEventBus.OnReachedCashier` 事件
- 在顾客到达收银台时（结账前）锁定 `pendingPayment` 值
- 锁定后的金额作为最终消费金额显示

### 列表顺序
- **最新进店的顾客在最上方**（倒序）
- 新顾客生成时插入到 ScrollView Content 的第一个位置

### 更新时机
1. **顾客进店**:
   - 监听 `CustomerEventBus.OnSpawned` 事件
   - 立即添加到列表顶部

2. **顾客消费**:
   - 定时刷新所有顾客的消费金额 (1秒间隔)
   - 从 `StatsDataManager` 读取最新数据

3. **顾客离店**:
   - 监听 `CustomerEventBus.OnCustomerDestroyed` 事件
   - 将对应列表项置灰

### 建造阶段行为
- **列表清空**: 新的一天建造阶段开始时清空所有顾客记录
- **显示状态**: Content 为空，显示空列表

### 技术实现
- 文件: `CurrentCustomersPanelView.cs`, `CustomerStatsItemUI.cs`
- 预制体: `CustomerStatsItem.prefab`
- 数据来源: `StatsDataManager.GetAllCustomerStats()`
- 列表容器: ScrollView + VerticalLayoutGroup

---

## 五、数据管理系统

### StatsDataManager (核心数据管理器)
**文件**: `StatsDataManager.cs`

#### 追踪数据
1. **货架收入追踪**
   - 数据结构: `Dictionary<string, int>` (shelfId → 今日收入)
   - 监听事件: `CustomerEventBus.OnPurchased`
   - 累加逻辑: `revenue += quantity × price`

2. **顾客消费追踪**
   - 数据结构: `List<CustomerStatsData>`
   - 监听事件:
     - `OnSpawned` → 添加记录
     - `OnReachedCashier` → 锁定消费金额
     - `OnCustomerDestroyed` → 标记已离店

#### 数据重置
- 触发时机: `DayLoopManager.OnBuildPhaseStart`
- 重置内容:
  - 清空 `shelfRevenueTracker`
  - 清空 `customerStatsTracker`

#### 查询接口
```csharp
List<ShelfStatsData> GetAllShelfStats()      // 所有货架统计
List<CustomerStatsData> GetAllCustomerStats() // 所有顾客统计（倒序）
int GetCurrentCustomerCount()                 // 当前店内人数
float GetTotalMaintenanceFee()                // 维护费总额
```

---

## 六、事件系统集成

### 新增事件监听
1. **`CustomerEventBus.OnReachedCashier`**
   - 触发位置: `ExecuteCheckoutAction.OnExecute()` (结账前)
   - 触发时机: 顾客到达收银台准备结账时
   - 用途: 锁定消费金额（防止结账后 `pendingPayment` 归零）

2. **`CustomerEventBus.OnPurchased`**
   - 用途: 实时更新货架收入并重新排序

3. **`CustomerEventBus.OnSpawned`**
   - 用途: 添加顾客到 CC 面板顶部

4. **`CustomerEventBus.OnCustomerDestroyed`**
   - 用途: 置灰离店顾客

5. **`DayLoopManager.OnBuildPhaseStart`**
   - 用途: 清空所有统计数据

6. **`DayLoopManager.OnDailySettlement`**
   - 用途: 自动关闭 Stats 面板

---

## 七、UI 动画

### 滑动动画
- **动画类型**: RectTransform.anchoredPosition 插值
- **缓动函数**:
  - 滑入: EaseOutCubic (`1 - (1-t)³`)
  - 滑出: EaseInCubic (`t³`)
- **动画时长**: 0.3秒
- **隐藏位置**: X = -panelWidth
- **显示位置**: X = 0

### Tab 按钮高亮
- **激活状态**: 浅黄色 (1f, 1f, 0.5f)
- **未激活状态**: 白色 (Color.white)

---

## 八、Unity 编辑器设置

### 必须在场景中配置

#### 1. UIManager 引用
在 `UIManager` 组件中分配：
- `Stats Panel` → 拖入 `StatsPanelController` GameObject

#### 2. StatsDataManager GameObject
- 在场景中创建一个空 GameObject
- 添加 `StatsDataManager` 组件
- 确保在 DontDestroyOnLoad 或始终存在

#### 3. StatsPanelController 配置
分配以下引用：
- `Panel Root` → Stats 面板的 RectTransform
- `Canvas Group` → 用于动画的 CanvasGroup
- `Economy Panel` → E 面板 GameObject
- `Products Panel` → P 面板 GameObject
- `Current Customers Panel` → CC 面板 GameObject
- `Economy Button` → E 按钮
- `Products Button` → P 按钮
- `Current Customers Button` → CC 按钮

#### 4. EconomyPanelView 配置
- `Today Revenue Text` → TextMeshProUGUI
- `Maintenance Fee Text` → TextMeshProUGUI

#### 5. ProductsPanelView 配置
- `Scroll Rect` → ScrollRect 组件
- `Content Container` → ScrollView 的 Content Transform
- `Shelf Item Prefab` → ShelfStatsItem 预制体
- `Category Dropdown` → TMP_Dropdown (筛选器)
- `Sort Dropdown` → TMP_Dropdown (排序器)

#### 6. CurrentCustomersPanelView 配置
- `Scroll Rect` → ScrollRect 组件
- `Content Container` → ScrollView 的 Content Transform
- `Customer Item Prefab` → CustomerStatsItem 预制体
- `Customer Count Text` → TextMeshProUGUI (底部人数显示)

#### 7. Stats 触发按钮
- 创建一个 UI Button
- OnClick 事件绑定: `UIManager.ToggleStatsPanel()`

### 预制体结构

#### ShelfStatsItem.prefab
```
ShelfStatsItem (GameObject)
├─ ShelfStatsItemUI (Component)
├─ Icon (Image)
├─ Name (TextMeshProUGUI)
├─ Category (TextMeshProUGUI)
├─ Level (TextMeshProUGUI)
├─ Revenue (TextMeshProUGUI)
└─ Price (TextMeshProUGUI)
```

#### CustomerStatsItem.prefab
```
CustomerStatsItem (GameObject)
├─ CustomerStatsItemUI (Component)
├─ CanvasGroup (Component) - 用于置灰
├─ Icon (Image)
├─ Name (TextMeshProUGUI)
├─ Loyalty (TextMeshProUGUI)
└─ Spent (TextMeshProUGUI)
```

---

## 九、文件清单

### 新增文件 (9个)
1. `Assets/Scripts/Manager/StatsDataManager.cs` - 数据管理器
2. `Assets/Scripts/UI/Stats/StatsPanelController.cs` - 主控制器
3. `Assets/Scripts/UI/Stats/EconomyPanelView.cs` - E 面板
4. `Assets/Scripts/UI/Stats/ProductsPanelView.cs` - P 面板
5. `Assets/Scripts/UI/Stats/CurrentCustomersPanelView.cs` - CC 面板
6. `Assets/Scripts/UI/Stats/ShelfStatsItemUI.cs` - 货架列表项
7. `Assets/Scripts/UI/Stats/CustomerStatsItemUI.cs` - 顾客列表项
8. `Assets/Prefab/UIs/ShelfStatsItem.prefab` - 货架预制体（需手动创建）
9. `Assets/Prefab/UIs/CustomerStatsItem.prefab` - 顾客预制体（需手动创建）

### 修改文件 (2个)
1. `Assets/Scripts/Manager/UIManager.cs` - 添加 Stats 面板引用和接口
2. `Assets/Scripts/Customers/NodeCanvas/Actions/ExecuteCheckoutAction.cs` - 触发 OnReachedCashier 事件

### 说明文档 (1个)
- `Assets/Documents/Stats面板功能说明.md` - 本文档

---

## 十、测试清单

### 功能测试
- [ ] Stats 按钮能正确打开/关闭面板
- [ ] 滑动动画流畅（从左往右展开，从右往左收起）
- [ ] ESC 键能关闭面板
- [ ] 结算界面弹出时面板自动关闭
- [ ] 三个 Tab 按钮能正确切换子面板
- [ ] Tab 按钮高亮状态正确显示

### Economy 面板测试
- [ ] 建造阶段收入显示 $0.00
- [ ] 营业阶段顾客结账后收入实时更新
- [ ] 维护费总额在建造/升级/销毁货架后正确更新
- [ ] 定时刷新（1秒间隔）正常工作

### Products 面板测试
- [ ] 建造阶段新建货架后列表立即更新
- [ ] 建造阶段销毁货架后列表立即更新
- [ ] 建造阶段升级货架后等级正确显示
- [ ] 营业阶段顾客购买后 Revenue 实时更新
- [ ] Revenue 变化后列表正确重新排序
- [ ] Category 过滤器正确筛选货架
- [ ] 排序方式切换（Revenue/Level/Price）正常工作
- [ ] 建造阶段 Revenue=0 时按加载顺序排列

### Current Customers 面板测试
- [ ] 顾客进店后立即显示在列表顶部
- [ ] 顾客消费金额实时更新
- [ ] 顾客到达收银台后消费金额锁定（不归零）
- [ ] 顾客离店后正确置灰
- [ ] 店内人数统计准确
- [ ] 建造阶段列表为空
- [ ] 结算后下一天建造阶段列表清空

### 性能测试
- [ ] 大量货架（50+）时 Products 面板刷新流畅
- [ ] 大量顾客（100+）时 CC 面板刷新流畅
- [ ] 定时刷新不会造成卡顿
- [ ] 列表滚动流畅

---

## 十一、已知限制与未来优化

### 当前限制
1. **Fame 收入显示**: 暂未实现（跳过，等待 Fame 结算逻辑修改）
2. **预制体需手动创建**: `ShelfStatsItem.prefab` 和 `CustomerStatsItem.prefab` 需在 Unity 编辑器中手动创建并配置
3. **BuildingChanged 事件缺失**: 建筑变化检测使用定时轮询（性能开销较小）

### 未来优化建议
1. **对象池**: 复用列表项 GameObject，避免频繁创建/销毁
2. **虚拟列表**: 大量顾客时使用虚拟滚动（只渲染可见项）
3. **增量更新**: 仅更新变化的列表项，而非整个列表重建
4. **事件驱动**: 为 ConstructionManager 添加建筑变化事件，替代轮询
5. **Fame 实时预估**: 根据当前收入计算预估 Fame 收入

---

## 十二、调试技巧

### 日志输出
所有脚本包含详细的 `Debug.Log` 输出：
- `[StatsDataManager]` - 数据追踪日志
- `[ProductsPanelView]` - 货架列表刷新日志
- `[CurrentCustomersPanelView]` - 顾客列表更新日志
- `[ExecuteCheckoutAction]` - 触发 OnReachedCashier 事件日志

### 常见问题
1. **面板不显示**: 检查 UIManager 中 `statsPanel` 引用是否分配
2. **列表项为空**: 检查预制体引用和 TextMeshProUGUI 组件分配
3. **消费金额归零**: 确认 `OnReachedCashier` 事件正确触发
4. **货架不更新**: 检查 `StatsDataManager` GameObject 是否存在于场景中
5. **滑动动画卡顿**: 检查 `panelWidth` 设置是否合理

---

**创建时间**: 2025-11-15
**版本**: 1.0
**作者**: Claude Code
**项目**: Pop Life Simulator
