# 如何配置新的引导 (Guidance Configuration Guide)

基于现有代码架构，配置一个新的引导序列（Guidance Sequence）主要涉及修改 `GuidanceSequenceFactory.cs`。以下是详细步骤：

## 1. 定义引导序列 ID

首先确定你的引导序列的唯一 ID，例如 `"GUIDE_NEW_FEATURE"`。

## 2. 修改 `GuidanceSequenceFactory.cs`

打开 `Assets/Scripts/GuidanceSystem/Utility/GuidanceSequenceFactory.cs`，你需要做两件事：

### 2.1 在 `CreateSequence` 方法中注册 ID

在 `switch (sequenceID)` 语句中添加新的 case：

```csharp
public static GuidanceSequence CreateSequence(string sequenceID)
{
    switch (sequenceID)
    {
        // ... 现有的 case ...
        
        case "GUIDE_NEW_FEATURE": // [NEW] 你的新引导 ID
            return CreateNewFeatureGuidance();
            
        default:
            Debug.LogWarning($"[GuidanceFactory] Unknown sequence ID: {sequenceID}");
            return null;
    }
}
```

### 2.2 实现创建序列的方法

在类中添加一个新的私有方法来构建序列和步骤：

```csharp
private static GuidanceSequence CreateNewFeatureGuidance()
{
    // 1. 创建序列对象
    var sequence = new GuidanceSequence("GUIDE_NEW_FEATURE", "New Feature Tutorial");

    // 2. 添加步骤 (Step 1)
    var step1 = new GuidanceStep(
        "STEP_1",                               // 步骤 ID
        "点击这里开始体验新功能",                 // 指引文本
        ActionType.ClickTarget                  // 动作类型 (ClickTarget, ClickAnywhere, Wait 等)
    );
    // 注意：如果需要指向特定的动态 UI 元素，可能需要在运行时通过 SetRuntimeTargets 设置
    // 或者使用 Find 查找（如果对象名称固定）
    sequence.AddStep(step1);

    // 3. 添加更多步骤 (Step 2)
    var step2 = new GuidanceStep(
        "STEP_2",
        "很好！现在点击任意位置继续",
        ActionType.ClickAnywhere
    );
    sequence.AddStep(step2);

    return sequence;
}
```

## 3. 触发引导

配置好后，有两种方式触发这个引导：

### 方式 A：通过代码直接触发

在任何脚本中调用 `GuidanceManager`：

```csharp
GuidanceManager.Instance.StartGuidance("GUIDE_NEW_FEATURE");
```

### 方式 B：通过教程系统自动触发 (TutorialContentDispatcher)

如果你希望它作为教程流程的一部分（例如在某个对话结束后触发）：

1.  找到场景中的 `TutorialContentDispatcher` 物体。
2.  在 Inspector 中展开 **Content Mappings**。
3.  添加一个新的 Element：
    *   **Marker**: 选择触发该引导的标记（例如 `Custom03`，需要在 `TutorialMarker` 枚举中定义，并在前一个事件中触发）。
    *   **Content Type**: 选择 `Guidance`。
    *   **Content ID**: 输入 `"GUIDE_NEW_FEATURE"`。

## 4. 关键类参考

*   **`GuidanceSequenceFactory.cs`**: 定义所有引导内容的地方。
*   **`GuidanceStep.cs`**: 定义单个步骤的属性（文本、高亮类型、完成条件等）。
    *   `ActionType`: `ClickTarget` (点击目标), `ClickAnywhere` (点击任意处), `Wait` (等待时间) 等。
    *   `HighlightShape`: `Rectangle` (矩形), `Circle` (圆形) 等。
*   **`GuidanceManager.cs`**: 运行时管理引导的播放、暂停游戏等。
