# 叙事与引导系统完整指南 (Narrative and Guidance System Guide)

## 系统概览

叙事与引导系统是Pop Life Simulator的核心交互系统，用于处理NPC对话、教程引导和剧情推进。系统采用数据驱动设计，支持分支对话、动态UI展示和深度序列管理。

### 核心特性
- **数据驱动叙事**: 基于ScriptableObject的叙事数据结构
- **动态标签系统**: 对话框位置动态分配，而非静态引用（v2.0重大改进）
- **扇形对话布局**: 独特的三层扇形UI设计
- **分支对话**: 支持多选项分支和条件判断
- **深拷贝机制**: 确保每个对话会话独立运行
- **对象池优化**: 减少GC压力，提升性能

## 架构设计

### 系统分层

```
┌─────────────────────────────────────────────────┐
│              UI Layer (UI层)                     │
│  FanConversationPanel | ConversationBox          │
│      VIPConversationPanel | TutorialPanel        │
└───────────────────┬─────────────────────────────┘
                    │
┌───────────────────▼─────────────────────────────┐
│         Manager Layer (管理器层)                 │
│     NarrativeManager | GuidanceManager           │
│         TutorialManager | NPCManager             │
└───────────────────┬─────────────────────────────┘
                    │
┌───────────────────▼─────────────────────────────┐
│           Core Layer (核心层)                    │
│   NarrativeSequence | NarrativeSegment           │
│     NarrativeChoice | NPCConversationData        │
└───────────────────┬─────────────────────────────┘
                    │
┌───────────────────▼─────────────────────────────┐
│           Data Layer (数据层)                    │
│        NarrativeSO (ScriptableObject)            │
│         Tutorial Configurations                  │
└─────────────────────────────────────────────────┘
```

## 核心组件详解

### 1. 数据层 (Data Layer)

#### NarrativeSO (ScriptableObject)
**位置**: `Scripts/NarrativeSystem/Core/NarrativeSO.cs`

核心叙事数据容器，存储完整的对话序列：

```csharp
[CreateAssetMenu(fileName = "New Narrative", menuName = "PopLife/Narrative/Narrative Data")]
public class NarrativeSO : ScriptableObject
{
    // 基础信息
    [SerializeField] private string narrativeID;         // 唯一标识
    [SerializeField] private string narrativeName;       // 叙事名称
    [SerializeField] private string description;         // 描述

    // 角色信息
    [SerializeField] private Sprite characterPortrait;   // 角色肖像
    [SerializeField] private string characterName;       // 角色名称
    [SerializeField] private ConversationType conversationType;  // 对话类型

    // 叙事数据
    [SerializeField] private NarrativeSequence narrativeSequence; // 叙事序列

    // 触发条件
    [SerializeField] private bool isVIPOnly;             // 是否仅限VIP
    [SerializeField] private int minimumDayRequired;     // 最小天数要求
    [SerializeField] private int minimumFameRequired;    // 最小声望要求

    // 奖励
    [SerializeField] private RewardData[] completionRewards;  // 完成奖励
}
```

**深拷贝机制** (v2.0重要更新):
```csharp
public NarrativeSequence CreateSequenceInstance()
{
    // 创建序列的深拷贝，避免修改原始数据
    var instance = new NarrativeSequence(narrativeSequence.SequenceID, narrativeSequence.SequenceName);

    if (narrativeSequence.RootSegment != null)
    {
        // 创建段落的深拷贝并重建链接
        var segmentCopies = new Dictionary<NarrativeSegment, NarrativeSegment>();
        var rootCopy = DeepCopySegment(narrativeSequence.RootSegment, segmentCopies);

        // 重建段落之间的链接关系
        RebuildSegmentLinks(narrativeSequence.RootSegment, segmentCopies);

        instance.Initialize(rootCopy);
    }

    return instance;
}

private NarrativeSegment DeepCopySegment(NarrativeSegment original,
    Dictionary<NarrativeSegment, NarrativeSegment> copies)
{
    if (original == null) return null;

    // 如果已经拷贝过，返回已有的拷贝
    if (copies.ContainsKey(original))
        return copies[original];

    // 创建新的段落实例
    var copy = new NarrativeSegment(
        original.SegmentID,
        original.SpeakerName,
        original.TextContent
    );

    // 复制属性
    copy.IsEndSegment = original.IsEndSegment;
    copy.DisplayDuration = original.DisplayDuration;
    copy.SpeakerPortrait = original.SpeakerPortrait;

    // 记录拷贝
    copies[original] = copy;

    // 递归拷贝所有子段落
    foreach (var nextSegment in original.NextSegments)
    {
        DeepCopySegment(nextSegment, copies);
    }

    return copy;
}

private void RebuildSegmentLinks(NarrativeSegment original,
    Dictionary<NarrativeSegment, NarrativeSegment> copies)
{
    if (original == null || !copies.ContainsKey(original))
        return;

    var copy = copies[original];

    // 重建与子段落的链接
    foreach (var originalNext in original.NextSegments)
    {
        if (copies.ContainsKey(originalNext))
        {
            var nextCopy = copies[originalNext];
            copy.AddNextSegment(nextCopy); // 这会自动设置 previousSegment
        }
    }

    // 递归处理所有子段落
    foreach (var nextSegment in original.NextSegments)
    {
        RebuildSegmentLinks(nextSegment, copies);
    }
}
```

### 2. 核心层 (Core Layer)

#### NarrativeSequence
**位置**: `Scripts/NarrativeSystem/Core/NarrativeSequence.cs`

管理整个叙事流程的核心类：

```csharp
[Serializable]
public class NarrativeSequence
{
    private NarrativeSegment rootSegment;      // 根段落
    private NarrativeSegment currentSegment;   // 当前段落
    private Stack<NarrativeSegment> history;   // 历史记录（用于回退）

    public void MoveNext(int choiceIndex = 0)
    {
        // 保存到历史
        if (currentSegment != null)
            history.Push(currentSegment);

        // 根据选择移动到下一个段落
        if (currentSegment.HasChoices && choiceIndex < currentSegment.Choices.Count)
        {
            var choice = currentSegment.Choices[choiceIndex];
            currentSegment = choice.NextSegment;
        }
        else if (currentSegment.NextSegments.Count > 0)
        {
            currentSegment = currentSegment.NextSegments[0];
        }
    }

    public void MovePrevious()
    {
        if (history.Count > 0)
        {
            currentSegment = history.Pop();
        }
    }
}
```

#### NarrativeSegment
**位置**: `Scripts/NarrativeSystem/Core/NarrativeSegment.cs`

单个对话段落的数据结构：

```csharp
[Serializable]
public class NarrativeSegment
{
    [SerializeField] private string segmentID;
    [SerializeField] private string speakerName;
    [SerializeField] private string textContent;
    [SerializeField] private Sprite speakerPortrait;
    [SerializeField] private float displayDuration = 3f;
    [SerializeField] private bool isEndSegment;

    // 链接关系
    [SerializeField] private List<NarrativeSegment> nextSegments;
    [SerializeField] private NarrativeSegment previousSegment;
    [SerializeField] private List<NarrativeChoice> choices;

    // v2.0改进：正确设置双向链接
    public void AddNextSegment(NarrativeSegment segment)
    {
        if (!nextSegments.Contains(segment))
        {
            nextSegments.Add(segment);
            segment.previousSegment = this;  // 自动设置反向链接
        }
    }
}
```

### 3. 管理器层 (Manager Layer)

#### NarrativeManager
**位置**: `Scripts/NarrativeSystem/Core/NarrativeManager.cs`

中央控制器，管理叙事流程：

```csharp
public class NarrativeManager : MonoBehaviour
{
    public static NarrativeManager Instance { get; private set; }

    // v2.0新增公开属性
    public NarrativeSequence ActiveSequence => activeSequence;
    public bool IsNarrativeActive => isNarrativeActive;

    public void StartNarrative(NarrativeSO narrativeData)
    {
        // 创建深拷贝实例，确保不修改原始数据
        activeSequence = narrativeData.CreateSequenceInstance();

        // 显示UI
        ShowNarrativeUI(narrativeData.ConversationType);

        // 初始化第一个段落
        ProcessCurrentSegment();
    }
}
```

### 4. UI层 (UI Layer) - v2.0完全重构

#### FanConversationPanel (动态标签系统版本)
**位置**: `Scripts/NarrativeSystem/UI/FanConversationPanel.cs`

##### 核心问题与解决方案

**旧版本的问题**:
```csharp
// 问题：静态引用导致动画后引用混乱
private ConversationBox topBox;
private ConversationBox centerBox;
private ConversationBox bottomBox;

// 动画后交换引用
void AnimateSlideUp()
{
    // 动画：bottomBox滑到中间位置
    // 问题：系统仍将其视为bottomBox，而非centerBox
    var temp = topBox;
    topBox = centerBox;
    centerBox = bottomBox;
    bottomBox = temp;  // 新建的框
}
// 结果：中间位置的框无法正确显示新内容
```

**v2.0动态标签解决方案**:
```csharp
// 新系统：位置标签独立于对象实例
public enum BoxPosition { Top, Center, Bottom }

private Dictionary<BoxPosition, ConversationBox> boxesByPosition;
private Stack<ConversationBox> boxPool;  // 对象池

// 动画后重新分配标签
private void ReassignPositionsAfterSlide()
{
    var oldTop = boxesByPosition[BoxPosition.Top];
    var oldCenter = boxesByPosition[BoxPosition.Center];
    var oldBottom = boxesByPosition[BoxPosition.Bottom];

    // 重新分配位置标签（标签跟踪位置，而非实例）
    boxesByPosition[BoxPosition.Top] = oldCenter;    // 中间框现在标记为顶部
    boxesByPosition[BoxPosition.Center] = oldBottom;  // 底部框现在标记为中间

    // 回收旧顶部框
    ReturnToPool(oldTop);

    // 创建新底部框
    var newBottom = GetOrCreateBox();
    ConfigureBoxForPosition(newBottom, BoxPosition.Bottom);
    boxesByPosition[BoxPosition.Bottom] = newBottom;
}
```

##### 关键配置参数

```csharp
[Header("Fan Layout Configuration")]
[SerializeField] private float fanAngle = 15f;        // 扇形角度
[SerializeField] private Vector2 fanPivot = new Vector2(0, -300f);  // 旋转轴心
[SerializeField] private float fanRadius = 300f;      // 扇形半径（v2.0修复）

[Header("Box Configuration")]
[SerializeField] private ConversationBox conversationBoxPrefab;  // 单一预制体引用
[SerializeField] private Vector2 boxSize = new Vector2(600f, 150f);  // 对话框大小

[Header("Animation")]
[SerializeField] private float slideUpDuration = 0.5f;
[SerializeField] private float fadeInDuration = 0.3f;
[SerializeField] private float fadeOutDuration = 0.2f;
```

##### 位置计算（v2.0修复fanRadius）

```csharp
private struct BoxPositionConfig
{
    public Vector2 anchoredPosition;
    public Vector3 rotation;
    public Vector3 scale;
    public Vector2 size;
}

private BoxPositionConfig GetPositionConfig(BoxPosition position)
{
    var config = new BoxPositionConfig();

    switch (position)
    {
        case BoxPosition.Top:
            config.rotation = new Vector3(0, 0, fanAngle);
            // v2.0修复：使用fanRadius变量而非硬编码
            config.anchoredPosition = new Vector2(0, fanRadius * 0.5f);
            config.scale = Vector3.one * 0.9f;
            config.size = boxSize * 0.9f;
            break;

        case BoxPosition.Center:
            config.rotation = Vector3.zero;
            config.anchoredPosition = Vector2.zero;
            config.scale = Vector3.one;
            config.size = boxSize;
            break;

        case BoxPosition.Bottom:
            config.rotation = new Vector3(0, 0, -fanAngle);
            config.anchoredPosition = new Vector2(0, -fanRadius * 0.5f);
            config.scale = Vector3.one * 0.9f;
            config.size = boxSize * 0.9f;
            break;
    }

    // 应用旋转轴心
    config.anchoredPosition += fanPivot;

    return config;
}
```

##### 动画系统重构

```csharp
private IEnumerator AnimateSlideUp()
{
    isAnimating = true;

    // 获取当前位置的对话框（通过位置标签）
    var topBox = boxesByPosition[BoxPosition.Top];
    var centerBox = boxesByPosition[BoxPosition.Center];
    var bottomBox = boxesByPosition[BoxPosition.Bottom];

    // 并行动画
    // 1. 顶部淡出
    Tween.Alpha(topBox.GetComponent<CanvasGroup>(), 0f, fadeOutDuration);

    // 2. 中间滑到顶部位置
    var topConfig = GetPositionConfig(BoxPosition.Top);
    Tween.UIAnchoredPosition(centerBox.transform as RectTransform,
        topConfig.anchoredPosition, slideUpDuration, Ease.InOutCubic);
    Tween.LocalRotation(centerBox.transform,
        topConfig.rotation, slideUpDuration, Ease.InOutCubic);
    Tween.Scale(centerBox.transform,
        topConfig.scale, slideUpDuration, Ease.InOutCubic);

    // 3. 底部滑到中间位置
    var centerConfig = GetPositionConfig(BoxPosition.Center);
    Tween.UIAnchoredPosition(bottomBox.transform as RectTransform,
        centerConfig.anchoredPosition, slideUpDuration, Ease.InOutCubic);
    Tween.LocalRotation(bottomBox.transform,
        centerConfig.rotation, slideUpDuration, Ease.InOutCubic);
    Tween.Scale(bottomBox.transform,
        centerConfig.scale, slideUpDuration, Ease.InOutCubic);

    // 等待动画完成
    yield return new WaitForSeconds(slideUpDuration);

    // 关键：重新分配位置标签
    ReassignPositionsAfterSlide();

    // 创建并配置新的底部框
    var nextSegment = GetNextSegment();
    if (nextSegment != null)
    {
        CreateNewBottomBox(nextSegment);
    }

    isAnimating = false;
}
```

##### 对象池优化

```csharp
private ConversationBox GetOrCreateBox()
{
    // 优先从池中获取
    if (boxPool.Count > 0)
    {
        var pooledBox = boxPool.Pop();
        pooledBox.gameObject.SetActive(true);
        pooledBox.Clear();
        return pooledBox;
    }

    // 池为空时创建新实例
    var newBox = Instantiate(conversationBoxPrefab, transform);
    return newBox;
}

private void ReturnToPool(ConversationBox box)
{
    // 清理并返回池中
    box.Clear();
    box.gameObject.SetActive(false);
    boxPool.Push(box);
}

// 初始化时预创建
private void InitializeBoxPool()
{
    boxPool = new Stack<ConversationBox>();

    // 预创建3个框（通常足够）
    for (int i = 0; i < 3; i++)
    {
        var box = Instantiate(conversationBoxPrefab, transform);
        box.gameObject.SetActive(false);
        boxPool.Push(box);
    }
}
```

#### ConversationBox
**位置**: `Scripts/NarrativeSystem/UI/ConversationBox.cs`

单个对话框控制器：

```csharp
public class ConversationBox : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Components")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI contentText;
    [SerializeField] private TextMeshProUGUI speakerText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Outline outlineEffect;

    [Header("Appearance")]
    [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
    [SerializeField] private Color highlightColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color centerColor = new Color(0.25f, 0.25f, 0.25f, 1f);

    public event Action OnBoxClicked;

    public void SetContent(string text, string speaker = null)
    {
        contentText.text = text;

        if (!string.IsNullOrEmpty(speaker))
        {
            speakerText.text = speaker;
            speakerText.gameObject.SetActive(true);
        }
        else
        {
            speakerText.gameObject.SetActive(false);
        }
    }

    public void SetHighlight(bool highlighted)
    {
        isHighlighted = highlighted;

        backgroundImage.color = highlighted ? centerColor : normalColor;
        outlineEffect.enabled = highlighted;
    }

    public void Clear()
    {
        contentText.text = "";
        speakerText.text = "";
        speakerText.gameObject.SetActive(false);
        SetHighlight(false);
    }

    // 点击动画反馈
    private void PlayClickAnimation()
    {
        Tween.Scale(transform, originalScale * 0.95f, 0.1f, Ease.InOutQuad)
            .OnComplete(() => Tween.Scale(transform, originalScale, 0.1f, Ease.InOutQuad));
    }
}
```

## 设置指南

### 1. 创建ConversationBox预制体

1. **创建UI结构**:
   ```
   ConversationBox (GameObject)
   ├── Background (Image)
   │   └── Component: Outline
   ├── ContentContainer
   │   └── ContentText (TextMeshProUGUI)
   └── SpeakerContainer
       └── SpeakerText (TextMeshProUGUI)
   ```

2. **添加组件**:
   ```
   ConversationBox GameObject:
   - RectTransform (自动)
   - CanvasGroup
   - ConversationBox.cs
   - Button (可选)

   Background:
   - Image
   - Outline (用于高亮效果)
   ```

3. **配置ConversationBox脚本**:
   ```
   UI Components:
   - Background Image: [拖入Background]
   - Content Text: [拖入ContentText]
   - Speaker Text: [拖入SpeakerText]
   - Canvas Group: [自动获取]
   - Outline Effect: [拖入Outline组件]

   Appearance:
   - Normal Color: (0.2, 0.2, 0.2, 0.9)
   - Highlight Color: (0.3, 0.3, 0.3, 1)
   - Center Color: (0.25, 0.25, 0.25, 1)
   - Hover Scale: 1.05
   ```

4. **保存预制体**:
   - 路径: `Assets/Prefabs/UI/ConversationBox.prefab`

### 2. 配置FanConversationPanel

1. **场景层级结构**:
   ```
   Canvas (Screen Space - Overlay)
   └── NarrativePanel
       ├── PortraitContainer
       │   └── CharacterPortrait (Image)
       └── ConversationContainer
           └── FanConversationPanel (GameObject)
   ```

2. **FanConversationPanel配置**:
   ```
   Inspector设置:

   [Fan Layout Configuration]
   - Fan Angle: 15
   - Fan Pivot: (0, -300)
   - Fan Radius: 300  ← 重要：控制扇形半径

   [Box Configuration]
   - Conversation Box Prefab: [拖入ConversationBox预制体]
   - Box Size: (600, 150)

   [Animation]
   - Slide Up Duration: 0.5
   - Fade In Duration: 0.3
   - Fade Out Duration: 0.2

   [References]
   - Portrait: [拖入CharacterPortrait]
   - Portrait Container: [拖入PortraitContainer]
   ```

### 3. 创建NarrativeSO资产

1. **创建菜单**:
   ```
   右键 → Create → PopLife → Narrative → Narrative Data
   ```

2. **配置数据**:
   ```
   Basic Info:
   - Narrative ID: NARR01 (自动生成)
   - Narrative Name: "Midori Introduction"
   - Description: "初次见面的对话"

   Character Info:
   - Character Portrait: [角色头像Sprite]
   - Character Name: "Midori"
   - Conversation Type: Regular

   Narrative Data:
   - Narrative Sequence: [使用编辑器创建]

   Trigger Conditions:
   - Is VIP Only: □
   - Minimum Day Required: 1
   - Minimum Fame Required: 0

   Rewards:
   - Type: Money
   - Amount: 100
   ```

3. **使用Narrative Graph Editor**:
   ```
   Window → Narrative Graph Editor
   - Load Asset: [选择NarrativeSO]
   - 创建节点
   - 连接段落
   - 保存
   ```

## 使用示例

### 基础使用

```csharp
public class NPCInteraction : MonoBehaviour
{
    [SerializeField] private NarrativeSO narrativeData;

    void OnPlayerInteract()
    {
        // 检查条件
        int currentDay = DayLoopManager.Instance.CurrentDay;
        int currentFame = ResourceManager.Instance.Fame;
        bool isVIP = PlayerData.IsVIP;

        if (narrativeData.CheckTriggerConditions(currentDay, currentFame, isVIP))
        {
            // 启动叙事
            NarrativeManager.Instance.StartNarrative(narrativeData);
        }
    }
}
```

### 监听叙事事件

```csharp
public class NarrativeEventHandler : MonoBehaviour
{
    void Start()
    {
        var manager = NarrativeManager.Instance;

        manager.OnNarrativeStarted += OnNarrativeStart;
        manager.OnSegmentDisplayed += OnSegmentDisplay;
        manager.OnNarrativeCompleted += OnNarrativeComplete;
    }

    void OnSegmentDisplay(NarrativeSegment segment)
    {
        Debug.Log($"显示对话: {segment.SpeakerName}: {segment.TextContent}");

        // 可以在这里触发音效、动画等
        AudioManager.Instance.PlayDialogueSound();
    }

    void OnNarrativeComplete()
    {
        Debug.Log("对话完成");

        // 处理完成逻辑
        if (NarrativeManager.Instance.ActiveSequence != null)
        {
            var rewards = NarrativeManager.Instance.GetCompletionRewards();
            ProcessRewards(rewards);
        }
    }
}
```

### 导航控制

```csharp
public class DialogueController : MonoBehaviour
{
    void Update()
    {
        if (!NarrativeManager.Instance.IsNarrativeActive)
            return;

        // 鼠标滚轮向下或点击 - 下一句
        if (Input.GetMouseButtonDown(0) || Input.mouseScrollDelta.y < 0)
        {
            NarrativeManager.Instance.NavigateForward();
        }

        // 鼠标滚轮向上 - 上一句
        if (Input.mouseScrollDelta.y > 0)
        {
            NarrativeManager.Instance.NavigateBackward();
        }

        // ESC - 跳过对话
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            NarrativeManager.Instance.SkipToEnd();
        }
    }
}
```

## 调试指南

### 运行时调试

1. **查看当前状态**:
   ```csharp
   // 添加到FanConversationPanel
   void OnGUI()
   {
       if (!debugMode) return;

       GUILayout.BeginArea(new Rect(10, 10, 300, 200));
       GUILayout.Label("=== 对话框位置 ===");

       foreach (var kvp in boxesByPosition)
       {
           var box = kvp.Value;
           var rect = box.transform as RectTransform;
           GUILayout.Label($"{kvp.Key}: {box.name}");
           GUILayout.Label($"  位置: {rect.anchoredPosition}");
           GUILayout.Label($"  旋转: {rect.localEulerAngles.z}°");
       }

       GUILayout.Label($"对象池大小: {boxPool.Count}");
       GUILayout.EndArea();
   }
   ```

2. **Gizmos可视化**:
   ```csharp
   void OnDrawGizmos()
   {
       if (!Application.isPlaying) return;

       // 绘制扇形布局
       Gizmos.color = Color.cyan;

       // 绘制旋转轴心
       var pivotWorld = transform.TransformPoint(fanPivot);
       Gizmos.DrawWireSphere(pivotWorld, 10f);

       // 绘制扇形半径
       Gizmos.DrawWireArc(pivotWorld, Vector3.forward, Vector3.up, fanAngle * 2, fanRadius);
   }
   ```

### 常见问题排查

**问题1: 对话框不显示**
- 确认ConversationBoxPrefab已赋值
- 检查Canvas Sort Order
- 验证FanConversationPanel在正确的Canvas下
- 查看Console是否有空引用错误

**问题2: 动画后内容错乱**
- 确认使用v2.0动态标签系统
- 检查ReassignPositionsAfterSlide()执行
- 验证boxesByPosition字典正确更新
- 使用调试模式查看位置分配

**问题3: previousSegment为null**
- 必须使用NarrativeSO.CreateSequenceInstance()
- 不要直接引用narrativeSequence
- 确认使用AddNextSegment()而非直接添加
- 检查RebuildSegmentLinks()是否执行

**问题4: fanRadius不起作用**
- 确认使用最新代码（fanRadius * 0.5f）
- 检查Inspector中fanRadius值
- 验证GetPositionConfig()被调用
- 旋转轴心fanPivot也会影响最终位置

**问题5: 对象池内存泄漏**
- 确保ReturnToPool()正确调用
- 检查Clear()方法清理引用
- 场景切换时清空池

## 性能优化建议

### 1. 对象池最佳实践

```csharp
// 预热对象池
void Start()
{
    // 根据最大可能同时显示的框数量预创建
    int poolSize = 5;  // 3个显示 + 2个缓冲

    for (int i = 0; i < poolSize; i++)
    {
        var box = CreateNewBox();
        ReturnToPool(box);
    }
}

// 场景卸载时清理
void OnDestroy()
{
    while (boxPool.Count > 0)
    {
        var box = boxPool.Pop();
        Destroy(box.gameObject);
    }
}
```

### 2. 动画优化

```csharp
// 批量动画减少调用
private void BatchAnimate(params Tween[] tweens)
{
    // PrimeTween自动批处理同时启动的动画
    foreach (var tween in tweens)
    {
        // 动画会在同一帧启动
    }
}
```

### 3. 内存管理

```csharp
// 避免频繁字符串拼接
private StringBuilder textBuilder = new StringBuilder();

void UpdateText(string speaker, string content)
{
    textBuilder.Clear();
    textBuilder.Append(speaker);
    textBuilder.Append(": ");
    textBuilder.Append(content);

    contentText.text = textBuilder.ToString();
}
```

## 扩展指南

### 添加新的对话UI样式

```csharp
// 创建新的面板类型
public class BubbleConversationPanel : BaseConversationPanel
{
    [SerializeField] private float bubbleSpacing = 20f;
    [SerializeField] private bool alternatesSides = true;

    protected override void DisplaySegment(NarrativeSegment segment)
    {
        // 实现气泡样式布局
        var bubble = CreateBubble(segment);

        // 左右交替显示
        if (alternatesSides && currentIndex % 2 == 0)
        {
            bubble.AlignLeft();
        }
        else
        {
            bubble.AlignRight();
        }
    }
}
```

### 添加打字机效果

```csharp
public class TypewriterEffect : MonoBehaviour
{
    private TextMeshProUGUI textComponent;
    private Coroutine typewriterCoroutine;

    public void ShowText(string fullText, float charactersPerSecond = 30f)
    {
        if (typewriterCoroutine != null)
            StopCoroutine(typewriterCoroutine);

        typewriterCoroutine = StartCoroutine(TypeText(fullText, charactersPerSecond));
    }

    private IEnumerator TypeText(string fullText, float speed)
    {
        textComponent.text = "";
        float delay = 1f / speed;

        foreach (char c in fullText)
        {
            textComponent.text += c;

            // 音效
            if (c != ' ')
                AudioManager.Instance.PlayTypeSound();

            yield return new WaitForSeconds(delay);
        }
    }
}
```

### 集成教程系统

```csharp
public class TutorialNarrativeIntegration : MonoBehaviour
{
    void OnTutorialMarkerReached(TutorialMarker marker)
    {
        switch (marker)
        {
            case TutorialMarker.FirstShelfPlaced:
                // 触发Midori的赞扬对话
                NarrativeManager.Instance.StartNarrative("NARR_GOOD_JOB");
                break;

            case TutorialMarker.FirstCustomerServed:
                // 触发成就对话
                NarrativeManager.Instance.StartNarrative("NARR_FIRST_SALE");
                break;
        }
    }
}
```

## 版本历史

### v2.0.0 - 动态标签系统重构 (当前版本)
- **完全重写FanConversationPanel**
  - 实现动态位置标签系统
  - 解决动画后内容显示错误
  - 标签跟踪位置而非实例
- **修复fanRadius未使用问题**
  - 从硬编码150f改为fanRadius * 0.5f
  - 正确应用Inspector配置值
- **添加对象池优化**
  - 减少GC压力
  - 提升性能
- **实现深拷贝机制**
  - NarrativeSO.CreateSequenceInstance()
  - 确保会话独立性
- **修复previousSegment链接**
  - RebuildSegmentLinks()正确重建
  - AddNextSegment()自动设置双向链接

### v1.5.0
- 添加NarrativeManager公开属性
- 修复IsNarrativeActive重复定义
- 改进错误处理

### v1.0.0 - 初始版本
- 基础叙事系统
- 扇形对话UI
- 分支对话支持

## 相关文档

- [Narrative Graph Editor使用指南](./NarrativeGraphEditor使用指南.md) - 可视化编辑器
- [Tutorial Sequence配置指南](./Tutorial_Sequence_Configuration_Guide.md) - 教程序列
- [指引系统集成指南](./GuidanceSystem_Guide.md) - 全屏遮罩教程

---

*文档版本: 2.0*
*最后更新: 2025年*
*作者: Pop Life Simulator开发团队*

### 更新记录
- v2.0: 完整记录动态标签系统重构，添加详细问题分析和解决方案
- v1.1: 添加PrimeTween动画库集成说明
- v1.0: 初始文档