# FloorManager 文档

## 概述
`FloorManager` 是游戏中管理多楼层系统的核心组件。它支持在 Unity Inspector 中直接管理多个楼层，控制楼层的激活状态，并自动管理楼层ID。

## 主要功能

### 1. Inspector 中管理多楼层
- 使用 `List<FloorEntry>` 数据结构存储所有楼层
- 可在 Inspector 中直接添加/删除楼层条目
- 每个楼层条目包含：楼层引用、激活状态、楼层ID

### 2. 楼层激活控制
- 每个楼层都有独立的 `isActive` 复选框
- 可在 Inspector 中直接勾选/取消勾选来激活/停用楼层
- 提供完整的代码 API 用于运行时控制激活状态
- 支持批量操作（激活所有、仅激活指定楼层）

### 3. 楼层ID管理
- 自动为每个楼层分配唯一的ID
- ID 在 Inspector 中显示为只读字段
- ID 会自动同步到对应的 `FloorGrid.floorId`
- 支持手动刷新ID分配

### 4. Inspector 配置选项
- **Auto Assign Floor Ids**：启用后自动分配楼层ID
- **Hide Inactive Floors**：启用后会隐藏未激活楼层的GameObject
- **Current Floor Index**：当前焦点楼层的索引

### 5. 右键菜单功能
- **刷新楼层ID**：手动重新分配所有楼层的ID
- **激活所有楼层**：一键激活所有楼层
- **检查楼层状态**：在控制台输出所有楼层的详细状态

## 使用方式

### 在 Unity 编辑器中设置

1. **添加 FloorManager 组件**
   - 在场景中创建一个GameObject
   - 添加 `FloorManager` 组件

2. **添加楼层**
   - 在 Inspector 中找到 `Floors` 列表
   - 点击 "+" 按钮添加新的楼层条目
   - 将场景中的 `FloorGrid` 对象拖拽到 `Floor` 字段

3. **配置楼层**
   - 勾选/取消勾选 `Is Active` 来控制楼层是否激活
   - `Floor Id` 会自动分配（显示为只读）

4. **调试选项**
   - 启用 `Auto Assign Floor Ids` 来自动管理ID
   - 启用 `Hide Inactive Floors` 来隐藏未激活的楼层对象

### 代码中使用

```csharp
// 获取 FloorManager 实例
FloorManager floorManager = GetComponent<FloorManager>();

// 获取当前焦点楼层
FloorGrid currentFloor = floorManager.GetActiveFloor();

// 获取所有激活的楼层
List<FloorGrid> activeFloors = floorManager.GetAllActiveFloors();

// 根据ID获取楼层（无论是否激活）
FloorGrid floor = floorManager.GetFloor(floorId);

// 根据ID获取激活的楼层
FloorGrid activeFloor = floorManager.GetActiveFloor(floorId);

// 设置楼层激活状态
floorManager.SetFloorActive(floorId, true);  // 激活
floorManager.SetFloorActive(floorId, false); // 停用

// 通过楼层引用设置激活状态
floorManager.SetFloorActive(floorGrid, true);

// 切换楼层激活状态
floorManager.ToggleFloorActive(floorId);

// 激活所有楼层
floorManager.ActivateAllFloors();

// 仅激活指定楼层（其他全部停用）
floorManager.ActivateOnlyFloor(floorId);

// 获取楼层信息
int totalFloors = floorManager.GetTotalFloorCount();
int activeCount = floorManager.GetActiveFloorCount();
bool isActive = floorManager.IsFloorActive(floorId);

// 设置当前焦点楼层
floorManager.SetActive(floorGrid);
```

## API 方法详解

### 查询方法
- `GetActiveFloor()` - 返回当前焦点楼层
- `GetAllActiveFloors()` - 返回所有激活的楼层列表
- `GetFloor(int id)` - 根据ID获取楼层（无论激活状态）
- `GetActiveFloor(int id)` - 根据ID获取激活的楼层
- `GetTotalFloorCount()` - 获取楼层总数
- `GetActiveFloorCount()` - 获取激活的楼层数量
- `IsFloorActive(int floorId)` - 检查楼层是否激活

### 控制方法
- `SetFloorActive(int floorId, bool active)` - 设置楼层激活状态
- `SetFloorActive(FloorGrid floor, bool active)` - 通过引用设置激活状态
- `ToggleFloorActive(int floorId)` - 切换楼层激活状态
- `SetActive(FloorGrid floor)` - 设置当前焦点楼层
- `ActivateAllFloors()` - 激活所有楼层
- `ActivateOnlyFloor(int floorId)` - 仅激活指定楼层

## 注意事项

1. **楼层ID管理**
   - 楼层ID是自动分配的，不建议手动修改
   - 删除楼层后，ID可能会重新分配
   - ID会自动同步到 `FloorGrid.floorId`

2. **激活状态**
   - 未激活的楼层不会参与游戏逻辑
   - 可通过 `hideInactiveFloors` 控制是否隐藏GameObject

3. **性能优化**
   - 使用缓存机制优化活跃楼层列表的访问
   - 批量操作时会自动刷新缓存

4. **兼容性**
   - 保持了对原有 `GetActiveFloor()` 和 `GetFloor(id)` 方法的兼容
   - 新增功能不会影响现有代码

## 扩展建议

1. **多楼层切换UI**
   - 可创建楼层切换按钮，调用 `SetActiveByIndex()`
   - 可显示楼层列表，使用 `GetAllFloorEntries()`

2. **楼层间传送**
   - 使用 `SetActive()` 切换玩家所在楼层
   - 可根据楼层ID实现特定传送点

3. **楼层独立管理**
   - 每个楼层可有独立的 `FloorGrid` 配置
   - 支持不同楼层有不同的网格大小和规则