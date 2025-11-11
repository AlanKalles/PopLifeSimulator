# 自定义对话UI系统使用指南
# Custom Dialogue UI System Guide

## 概述

本系统允许不同的对话在不同位置显示，并支持通过Blackboard配置UI类型和位置。

---

## 🎯 核心功能

### 1. UI类型过滤
- 每个UI组件可以指定处理的对话类型（如"Tutorial", "VIP"）
- 同一场景中可以有多个UI组件，各自处理不同类型的对话

### 2. 动态位置配置
- 通过`DialogueUIConfig`配置每个对话的显示位置
- 支持锚点（anchor）和偏移（position）
- 支持缩放（scale）

### 3. Blackboard传递
- 对话配置通过NodeCanvas Blackboard传递给UI
- UI在运行时读取Blackboard并应用配置

---

## 📦 系统组件

### 1. DialogueUIConfig（配置类）

```csharp
public class DialogueUIConfig
{
    public string uiType = "Default";              // UI类型
    public Vector2 panelPosition = Vector2.zero;   // 位置偏移
    public Vector2 panelAnchor = new Vector2(0.5f, 0.5f); // 锚点
    public float panelScale = 1.0f;                // 缩放
}
```

**构造函数**：
```csharp
// 默认配置
new DialogueUIConfig();

// 指定UI类型
new DialogueUIConfig("Tutorial");

// 完整配置
new DialogueUIConfig(
    uiType: "Tutorial",
    position: new Vector2(100, 100),   // 从锚点偏移100像素
    anchor: new Vector2(0f, 0f),       // 左下角锚点
    scale: 1.2f                        // 120%缩放
);
```

### 2. DialogueEvent（对话事件）

已扩展支持UI配置：
```csharp
var dialogueEvent = new DialogueEvent(
    dialogueTree,
    "D001",
    "Meet Midori",
    rewards,
    new DialogueUIConfig("Tutorial", Vector2.zero, new Vector2(0.5f, 0.5f))
);
```

### 3. CustomDialogueUI（UI组件）

可配置的对话UI组件，放在Canvas下：

**Inspector配置**：
- **Target UI Type**: 过滤对话类型（如"Tutorial"）
- **Dialogue Panel**: 对话框RectTransform
- **Actor Name**: 角色名Text
- **Dialogue Text**: 对话文本Text
- **Actor Portrait**: 立绘Image
- **Next Button**: 继续按钮（可选）
- **Wait Input Indicator**: 等待输入指示器（可选）

---

## 🛠️ 使用步骤

### 步骤1：创建DialogueUI预制体

#### 方式A：复制NodeCanvas默认UI

1. 复制 `@DialogueUGUI.prefab`
2. 重命名为 `TutorialDialogueUI`
3. 移除原有的 `DialogueUGUI` 组件
4. 添加 `CustomDialogueUI` 组件
5. 配置Inspector字段：
   ```
   Target UI Type: Tutorial
   Dialogue Panel: [拖入对话框RectTransform]
   Actor Name: [拖入Text组件]
   Dialogue Text: [拖入Text组件]
   Actor Portrait: [拖入Image组件]
   ```

#### 方式B：从零创建

1. 创建Canvas（如果没有）
2. 在Canvas下创建Panel作为对话框
3. 添加Text组件：
   - ActorName
   - DialogueText
4. 添加Image组件：
   - ActorPortrait
5. 在对话框上添加`CustomDialogueUI`脚本
6. 添加CanvasGroup组件（用于淡入淡出）

### 步骤2：在场景中放置UI

```
LatestUpdate场景：
├── Canvas
│   └── TutorialDialogueUI (Panel)
│       ├── CustomDialogueUI (Component)
│       ├── CanvasGroup (Component)
│       ├── ActorName (Text)
│       ├── DialogueText (Text)
│       └── ActorPortrait (Image)
```

### 步骤3：配置对话位置

在`TutorialDialogueManager.cs`的`RegisterTutorialDialogues()`中：

```csharp
// D001: 屏幕中央
AddEvent("D001", "Meet Midori",
    TutorialMarker.GameStarted,
    new List<string>(),
    new DialogueUIConfig(
        uiType: "Tutorial",
        position: Vector2.zero,         // 中心点
        anchor: new Vector2(0.5f, 0.5f) // 屏幕中央锚点
    ));

// D002: 左下角
AddEvent("D002", "Build Tutorial",
    TutorialMarker.FirstBuildPhaseEntered,
    new List<string> { "Blueprint:B001" },
    new DialogueUIConfig(
        uiType: "Tutorial",
        position: new Vector2(100, 100), // 从左下角偏移100px
        anchor: new Vector2(0f, 0f)      // 左下角锚点
    ));

// D003: 右下角
AddEvent("D003", "Open Store",
    TutorialMarker.TwoShelvesPlaced,
    new List<string>(),
    new DialogueUIConfig(
        uiType: "Tutorial",
        position: new Vector2(-100, 100), // 从右下角偏移-100,100px
        anchor: new Vector2(1f, 0f)       // 右下角锚点
    ));

// D004: 屏幕顶部
AddEvent("D004", "First Customer",
    TutorialMarker.StoreOpened,
    new List<string>(),
    new DialogueUIConfig(
        uiType: "Tutorial",
        position: new Vector2(0, -100),  // 从顶部向下偏移100px
        anchor: new Vector2(0.5f, 1f)    // 顶部中央锚点
    ));

// D005: 屏幕中央（放大120%）
AddEvent("D005", "Fame System",
    TutorialMarker.FirstFameEarned,
    new List<string>(),
    new DialogueUIConfig(
        uiType: "Tutorial",
        position: Vector2.zero,
        anchor: new Vector2(0.5f, 0.5f),
        scale: 1.2f                      // 120%缩放
    ));
```

---

## 📐 位置配置参考

### 常用锚点位置

```
(0, 1) ───── (0.5, 1) ───── (1, 1)
左上角         顶部中央        右上角
  │                            │
  │                            │
(0, 0.5) ─── (0.5, 0.5) ─── (1, 0.5)
左侧中央      屏幕中央         右侧中央
  │                            │
  │                            │
(0, 0) ───── (0.5, 0) ───── (1, 0)
左下角        底部中央         右下角
```

### 配置示例

| 位置 | anchor | position | 说明 |
|------|--------|----------|------|
| 屏幕中央 | (0.5, 0.5) | (0, 0) | 居中显示 |
| 左下角 | (0, 0) | (100, 100) | 距左下角100px |
| 右上角 | (1, 1) | (-100, -100) | 距右上角100px |
| 顶部居中 | (0.5, 1) | (0, -50) | 距顶部50px |
| 底部居中 | (0.5, 0) | (0, 50) | 距底部50px |
| 左侧中央 | (0, 0.5) | (100, 0) | 距左侧100px |
| 右侧中央 | (1, 0.5) | (-100, 0) | 距右侧100px |

---

## 🎨 多UI类型支持

### 创建不同类型的UI

#### Tutorial UI（教程UI）
```
特点：
- UI类型：Tutorial
- 简洁明了
- 位置灵活（根据教程内容）
- 字体较大
```

#### VIP UI（剧情UI）
```
特点：
- UI类型：VIP
- 华丽装饰
- 固定在屏幕底部
- 支持选项分支
```

### 在场景中同时使用

```
Canvas
├── TutorialDialogueUI (targetUIType = "Tutorial")
└── VIPDialogueUI (targetUIType = "VIP")
```

两个UI会自动过滤对话：
- TutorialDialogueUI 只显示 uiType="Tutorial" 的对话
- VIPDialogueUI 只显示 uiType="VIP" 的对话

---

## 🔧 高级功能

### 1. 动画自定义

在`CustomDialogueUI.cs`中调整：
```csharp
[Header("Animation")]
public float fadeInDuration = 0.2f;   // 淡入时长
public float fadeOutDuration = 0.2f;  // 淡出时长
```

打字机效果速度：
```csharp
private IEnumerator TypeText(string text)
{
    for (int i = 0; i <= text.Length; i++)
    {
        dialogueText.text = text.Substring(0, i);
        yield return new WaitForSeconds(0.03f); // 修改这里调整速度
    }
}
```

### 2. 运行时修改位置

在对话触发前动态修改配置：
```csharp
var config = new DialogueUIConfig("Tutorial");
config.panelPosition = CalculateDynamicPosition(); // 动态计算位置
config.panelAnchor = new Vector2(0.5f, 0.5f);

var dialogueEvent = new DialogueEvent(tree, code, name, rewards, config);
```

### 3. 响应式位置

根据屏幕大小调整：
```csharp
Vector2 CalculateDynamicPosition()
{
    float screenWidth = Screen.width;
    float screenHeight = Screen.height;

    // 距离屏幕边缘10%的位置
    return new Vector2(screenWidth * 0.1f, screenHeight * 0.1f);
}
```

---

## 🐛 故障排除

### 问题1：对话显示了但位置不对

**检查**：
- DialoguePanel的Anchors是否被预设覆盖
- 确保RectTransform的Anchors Min/Max在代码中正确设置

**解决**：
在CustomDialogueUI中添加调试日志：
```csharp
Debug.Log($"Panel anchor: {dialoguePanel.anchorMin}, position: {dialoguePanel.anchoredPosition}");
```

### 问题2：多个UI同时显示

**原因**：
- 多个UI的targetUIType相同或为空

**解决**：
- 确保每个UI的targetUIType不同
- 或在RegisterDialogues时指定不同的uiType

### 问题3：UI不显示

**检查清单**：
1. ✅ CustomDialogueUI在场景中？
2. ✅ targetUIType与对话的uiType匹配？
3. ✅ DialoguePanel引用正确？
4. ✅ Canvas的RenderMode设置正确？
5. ✅ Console有"Handling dialogue"日志？

---

## 📊 完整示例

### 示例：创建教程系统UI

```csharp
// 在TutorialDialogueManager中
private void RegisterTutorialDialogues()
{
    // 欢迎对话 - 屏幕中央
    AddEvent("D001", "Welcome",
        TutorialMarker.GameStarted,
        new List<string>(),
        new DialogueUIConfig("Tutorial", Vector2.zero, new Vector2(0.5f, 0.5f)));

    // 建造提示 - 跟随鼠标附近（左下角作为备选）
    AddEvent("D002", "Build Hint",
        TutorialMarker.FirstBuildPhaseEntered,
        new List<string>(),
        new DialogueUIConfig("Tutorial", new Vector2(200, 200), new Vector2(0f, 0f)));

    // 成功提示 - 屏幕顶部（大字体）
    AddEvent("D003", "Success",
        TutorialMarker.TwoShelvesPlaced,
        new List<string>(),
        new DialogueUIConfig("Tutorial", new Vector2(0, -150), new Vector2(0.5f, 1f), 1.3f));
}
```

### 示例：在Unity中设置

1. **创建TutorialDialogueUI预制体**
2. **设置RectTransform**：
   - Anchors: Stretch (对于响应式布局)
   - Width: 600, Height: 200
3. **添加组件**：
   - CustomDialogueUI
   - CanvasGroup
4. **配置CustomDialogueUI**：
   - Target UI Type: Tutorial
   - Dialogue Panel: 自身的RectTransform
   - Actor Name, Dialogue Text, Actor Portrait: 拖入对应UI元素
5. **放入场景**

---

## 总结

✅ **核心特性**：
- 通过Blackboard传递UI配置
- 支持多UI类型过滤
- 灵活的位置和锚点系统
- 简单的API设计

🎯 **使用方式**：
```csharp
// 1. 创建配置
var config = new DialogueUIConfig(
    uiType: "Tutorial",
    position: new Vector2(100, 100),
    anchor: new Vector2(0f, 0f)
);

// 2. 添加对话事件
AddEvent("D001", "Name", marker, rewards, config);

// 3. UI自动读取并应用配置
```

祝你创建出优秀的对话系统！🎮
