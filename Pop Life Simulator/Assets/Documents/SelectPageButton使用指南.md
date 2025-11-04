# SelectPageButton 使用指南

## 概述

`SelectPageButton` 是一个基于传统 Unity `Button` 组件的筛选按钮，支持 **Alpha Hit Test** 功能，可以实现不规则形状（如人体部位）的精确点击检测。

## 核心特性

✅ **使用传统 Button 组件** - 完全兼容 Unity 的 Button 事件系统
✅ **Alpha Hit Test 支持** - 只响应不透明区域的点击
✅ **自动引用管理** - 自动查找 Button 和 Image 组件
✅ **使用 Button 的 ColorBlock** - 直接使用 Button 组件的颜色配置系统
✅ **IFilterButton 接口** - 与 `FilterToggleGroup` 无缝集成
✅ **零额外配置** - 无需手动设置颜色，完全依赖 Button 的设置

---

## 使用步骤

### 1. 在 Unity 中创建按钮

1. 创建一个空的 GameObject
2. 添加以下组件：
   - **Image** 组件（主按钮图像）
   - **Button** 组件（Unity 内置）
   - **SelectPageButton** 脚本
3. （可选）创建子对象 `HighlightOverlay`：
   - 添加 **Image** 组件用于高亮效果

### 2. 配置 Image 的 Sprite

1. 选择你想要的 Sprite（如人体部位图）
2. **重要**：在 Sprite 的贴图导入设置中：
   - 勾选 **Read/Write Enabled**
   - 点击 **Apply**

### 3. 配置 Button 组件的 ColorBlock

在 Inspector 中找到 **Button** 组件，设置颜色：

1. **Normal Color**: 按钮正常状态颜色（如：白色）
2. **Highlighted Color**: 鼠标悬停时颜色（如：浅灰）
3. **Pressed Color**: 按下时颜色（如：深灰）
4. **Selected Color**: 选中状态颜色（如：蓝色）✨ **重要**
5. **Disabled Color**: 禁用时颜色（如：灰色）

**SelectPageButton 会自动使用**：
- `originalColors.normalColor` → 未选中状态
- `originalColors.selectedColor` → 选中状态

### 4. 配置 SelectPageButton 组件

在 Inspector 中设置以下参数：

#### **Filter Configuration（筛选配置）**
- `isAllButton`: 是否为 "All" 按钮（显示所有货架）
- `filterPage`: 筛选范围（SelectPage 枚举值）

#### **UI References（UI 引用）**
- `button`: Button 组件（自动查找，也可手动拖入）
- `buttonImage`: 主按钮 Image（自动查找，也可手动拖入）

---

## 工作原理

### Alpha Hit Test 机制

```csharp
// 在 Awake() 中自动配置
buttonImage.alphaHitTestMinimumThreshold = 0.1f;
```

- **0.1f 阈值**：只有透明度 > 10% 的像素区域才会响应点击
- **效果**：不规则 Sprite 的透明区域不会触发按钮点击
- **要求**：贴图必须启用 **Read/Write Enabled**

### Button 组件集成

```csharp
// 自动设置 targetGraphic
if (button != null && button.targetGraphic == null)
{
    button.targetGraphic = buttonImage;
}

// 缓存原始 ColorBlock
originalColors = button.colors;

// 注册点击事件
button.onClick.AddListener(OnButtonClick);
```

- Button 的 `targetGraphic` 会自动设置为 `buttonImage`
- Alpha Hit Test 会应用到 Button 的点击检测上
- 保留 Button 的所有原生功能（Transition、Navigation 等）
- **ColorBlock 会被缓存**，用于状态切换

### 选中状态管理

```csharp
private void UpdateVisual()
{
    if (isSelected)
    {
        // 选中状态：使用 Button 的 selectedColor
        buttonImage.color = originalColors.selectedColor;
        button.interactable = false; // 禁用交互
    }
    else
    {
        // 未选中状态：使用 Button 的 normalColor
        buttonImage.color = originalColors.normalColor;
        button.interactable = true;  // 启用交互
    }
}
```

- 选中状态下按钮显示 `selectedColor`，并变为 **不可交互**
- 未选中状态下按钮显示 `normalColor`，可正常交互
- 避免重复点击已选中的按钮
- **完全使用 Button 的 ColorBlock 系统，无额外配置**

---

## 与 ShelfListPanel 的集成

### 1. 在 ShelfListPanel 中注册按钮

```csharp
[SerializeField] private SelectPageButton[] selectPageButtons;

private void InitializeSelectPageButtons()
{
    foreach (var button in selectPageButtons)
    {
        // 初始化按钮
        button.Initialize((clickedToggle) =>
        {
            selectPageToggleGroup.OnToggleClicked(clickedToggle);
        });

        // 注册到切换组
        selectPageToggleGroup.RegisterToggle(button);
    }
}
```

### 2. 按钮数组配置

在 Inspector 中：
1. 展开 `ShelfListPanel` 组件
2. 找到 `selectPageButtons` 数组
3. 设置 Size（如 5）
4. 将创建好的 SelectPageButton 拖入数组槽位

---

## 常见问题

### Q1: 报错 "alphaHitTestMinimumThreshold should not be modified"

**原因**：贴图未启用 Read/Write
**解决**：
1. 选中 Sprite 的贴图资源
2. 在 Inspector 中勾选 **Read/Write Enabled**
3. 点击 **Apply**

### Q2: 透明区域仍然会触发点击

**原因**：`alphaHitTestMinimumThreshold` 未生效
**解决**：
1. 确保贴图启用 Read/Write
2. 检查 `buttonImage` 引用是否正确
3. 在运行时查看 Console 是否有警告信息

### Q3: Button 的 Transition 动画不工作

**原因**：选中状态下 `button.interactable = false`
**解决**：这是预期行为，选中状态不应响应交互

### Q4: 如何自定义按钮的颜色？

**解决**：在 Unity Inspector 中配置 Button 组件的 ColorBlock：
- Normal Color → 未选中状态
- Selected Color → 选中状态
- Highlighted/Pressed/Disabled → 悬停/按下/禁用状态

---

## 最佳实践

### 1. 贴图设置

```
✅ Read/Write Enabled: 勾选
✅ Format: RGBA32 或 ARGB32
✅ Compression: None 或 High Quality
```

### 2. 性能优化

- 如果不需要 Alpha Hit Test，使用 `FilterToggleButton` 代替
- 避免过大的贴图尺寸（建议 256x256 以内）

### 3. 层级命名规范

```
BodyButton
HeadButton
FrontButton
BackButton
AllButton (isAllButton = true)
```

### 4. Button ColorBlock 配置建议

在 Inspector 的 Button 组件中设置：

```
Normal Color: (255, 255, 255, 255)        // 白色（保持原图）
Highlighted Color: (230, 230, 230, 255)   // 浅灰（悬停）
Pressed Color: (200, 200, 200, 255)       // 深灰（按下）
Selected Color: (128, 179, 230, 255)      // 蓝色（选中）✨
Disabled Color: (150, 150, 150, 128)      // 灰色半透明（禁用）

Color Multiplier: 1.0
Fade Duration: 0.1
```

---

## 与 FilterToggleButton 的对比

| 特性 | SelectPageButton | FilterToggleButton |
|------|-----------------|-------------------|
| **Button 组件** | ✅ 使用 | ✅ 使用 |
| **Alpha Hit Test** | ✅ 支持 | ❌ 不支持 |
| **适用场景** | 不规则形状（人体部位） | 矩形按钮（文字标签） |
| **贴图要求** | 需要 Read/Write | 无要求 |
| **性能** | 稍低 | 更高 |
| **配置复杂度** | 中等 | 简单 |

---

## 调试技巧

### 1. 查看 Alpha Hit Test 是否生效

在运行时，通过 Hierarchy 选中按钮，查看 Inspector：
```
Image Component
  ├─ Sprite: xxxxx
  └─ alphaHitTestMinimumThreshold: 0.1
```

### 2. 测试透明区域

使用 Scene 视图的 **Alpha 通道预览**：
- 选中 Sprite
- 在 Scene 视图工具栏选择 **Alpha**
- 黑色区域 = 透明（不响应点击）

### 3. 检查 Button 引用

```csharp
// 在 Awake() 后添加日志
Debug.Log($"Button: {button}, TargetGraphic: {button.targetGraphic}");
```

---

## 示例代码

### 手动创建 SelectPageButton

```csharp
// 创建按钮对象
GameObject buttonObj = new GameObject("BodyButton");
Image buttonImage = buttonObj.AddComponent<Image>();
Button button = buttonObj.AddComponent<Button>();
SelectPageButton selectButton = buttonObj.AddComponent<SelectPageButton>();

// 设置 Sprite（需要已启用 Read/Write）
buttonImage.sprite = Resources.Load<Sprite>("UI/BodySprite");

// 配置 SelectPageButton
selectButton.Initialize((clickedButton) =>
{
    Debug.Log($"Clicked: {clickedButton.FilterValue}");
});
```

---

## 版本历史

### v3.0 (当前版本) - 2025-11-04
- ✅ **完全使用 Button 的 ColorBlock 系统**
- ✅ 移除所有自定义颜色配置
- ✅ 移除 HighlightOverlay 叠加层系统
- ✅ 简化代码，仅保留核心功能
- ✅ 缓存 `originalColors` 用于状态切换

### v2.0
- ✅ 改用传统 Button 组件
- ✅ 移除手动 IPointerXXXHandler 实现
- ✅ 保留 Alpha Hit Test 功能
- ✅ 添加自动贴图可读性检查
- ❌ 仍有自定义颜色配置

### v1.0 (已废弃)
- ❌ 使用手动点击检测（IPointerClickHandler）
- ❌ 不使用 Button 组件

---

## 相关文档

- [IFilterButton 接口说明](./IFilterButton接口说明.md)
- [货架列表面板设置指南](./货架列表面板设置指南_v3.md)
- [FilterToggleGroup 使用指南](./FilterToggleGroup使用指南.md)

---

## 技术支持

如遇问题，请检查：
1. Console 中的警告信息
2. 贴图 Read/Write 设置
3. Button 和 Image 组件引用
4. SelectPage 枚举值配置

**最后更新时间**: 2025-11-04
