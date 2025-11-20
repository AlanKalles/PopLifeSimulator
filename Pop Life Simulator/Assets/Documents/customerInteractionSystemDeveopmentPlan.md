Claude’s Plan
顾客交互系统实现计划
一、新增文件
1. CustomerInteractionState.cs
路径: Assets/Scripts/Customers/Runtime/CustomerInteractionState.cs 职责：挂载在 CustomerAgent 上，管理单个顾客的交互状态
// 核心状态
public bool IsInDialogueState { get; private set; }
public bool HasTriggeredToday { get; private set; }
private float dialogueStateTimer;  // 10秒倒计时（Time.deltaTime）
private Collider2D interactionCollider;  // 仅对话状态时激活

// 主要方法
EnterDialogueState()
  - 暂停寻路
  - 播放 customer_interaction 动画
  - 激活 interactionCollider（设为 enabled = true）
  - 启动倒计时

ExitDialogueState()
  - 恢复寻路
  - 停用 interactionCollider
  - 标记 hasTriggeredDialogueToday = true

OnDialogueTriggered()
  - 玩家点击后调用
  - 停止倒计时
  - 等待对话结束后调用 ExitDialogueState()
2. CustomerInteractionManager.cs
路径: Assets/Scripts/Customers/Services/CustomerInteractionManager.cs 职责：全局交互管理器（单例）
// 公共池管理
private HashSet<string> usedNarrativeIds;  // 当日已使用的对话ID

// 核心功能
- Update() 检测点击（Raycast → CustomerInteractionLayer）
- 同时只能与一个顾客对话（isDialogueActive 标记）
- 监听 DayLoopManager.OnBuildPhaseStart 清空公共池

// 公共池方法
string SelectNarrativeId(string[] customerAvailableIds)
  1. 获取可用ID = customerAvailableIds - usedNarrativeIds
  2. 如果可用ID为空 → 从 customerAvailableIds 任意选
  3. 随机选择一个ID
  4. 添加到 usedNarrativeIds
  5. 返回选中的ID

ClearUsedNarrativeIds()
  - BuildPhase 开始时调用
  - 清空 usedNarrativeIds
二、行为树节点设计
新增 Condition：ShouldEnterInteractionCondition.cs
位置：Shopping Circulation 循环内，SelectTargetShelfAction 之前 检查条件：
hasEnteredStore == true
isUpset == false
isClosingTime == false
本日未触发过交互
随机概率通过（如 15%）
新增 Action：EnterDialogueStateAction.cs
调用 CustomerInteractionState.EnterDialogueState()
新增 Action：WaitForDialogueAction.cs
每帧检查 IsInDialogueState
状态变为 false 时返回 Success
三、修改现有文件
1. CustomerRecord.cs
[SerializeField] public string[] availableNarrativeIds;  // 可用对话ID列表
2. CustomerBlackboardAdapter.cs
public bool hasTriggeredDialogueToday;  // 今日是否已触发对话
3. FanConversationPanel.cs
// 第830行、848行：修复时间暂停时UI动画
elapsed += Time.unscaledDeltaTime;
4. NarrativeManager.cs
// 第348行：修复协程
yield return new WaitForSecondsRealtime(0.5f);

// StartNarrative() 中添加：
DayLoopManager.Instance?.PauseTime();

// EndCurrentNarrative() 中添加：
DayLoopManager.Instance?.ResumeTime();
5. CustomerAgent.cs
private CustomerInteractionState interactionState;
// 初始化时获取组件
四、核心流程
【交互触发流程】
1. 行为树检查 ShouldEnterInteractionCondition
2. 随机概率触发 → EnterDialogueStateAction
   - 暂停寻路
   - 播放 customer_interaction 动画
   - 激活 interactionCollider
   - 启动 10秒倒计时（Time.deltaTime）
3. WaitForDialogueAction 等待

【场上状态】
- 允许多个顾客同时处于对话状态
- 各自的 interactionCollider 都激活
- 玩家一次只能点击一个

【玩家点击流程】
1. CustomerInteractionManager.Update() 检测点击
2. Raycast 命中 CustomerInteractionLayer
3. 检查当前是否有对话进行中（如有则忽略点击）
4. 获取 CustomerInteractionState
5. 调用 OnDialogueTriggered()（停止倒计时）
6. 调用 SelectNarrativeId() 从公共池筛选
   - 排除已用ID，随机选择
   - 如果全被占用，任意选择
   - 将选中ID加入公共池
7. 调用 NarrativeManager.StartNarrative()
8. DayLoopManager.PauseTime()

【对话结束流程】
1. NarrativeManager.EndCurrentNarrative()
2. DayLoopManager.ResumeTime()
3. CustomerInteractionManager 通知顾客调用 ExitDialogueState()
   - 停用 interactionCollider
   - 恢复寻路
   - 标记 hasTriggeredDialogueToday = true

【超时流程】
- 10秒倒计时到期
- 自动调用 ExitDialogueState()
- 停用 interactionCollider
- 恢复移动

【每日重置】
- DayLoopManager.OnBuildPhaseStart 事件
- CustomerInteractionManager.ClearUsedNarrativeIds()
- 清空公共池
五、公共池机制详解
// 示例：三个顾客的可用对话ID
CustomerA: ["N001", "N002", "N003"]
CustomerB: ["N001", "N004"]
CustomerC: ["N002", "N003", "N005"]

// 第一个交互：CustomerA
公共池: {} → 可选 ["N001", "N002", "N003"] → 抽中 "N002"
公共池: {"N002"}

// 第二个交互：CustomerB
公共池: {"N002"} → CustomerB可用 ["N001", "N004"] → 排除后 ["N001", "N004"]
抽中 "N001" → 公共池: {"N002", "N001"}

// 第三个交互：CustomerC
公共池: {"N002", "N001"} → CustomerC可用 ["N002", "N003", "N005"] 
排除后 ["N003", "N005"] → 抽中 "N003"
公共池: {"N002", "N001", "N003"}

// 特殊情况：如果顾客D的可用ID是 ["N001", "N002"]
公共池已有 {"N001", "N002"} → 排除后为空
→ 从原始 ["N001", "N002"] 任意选
六、时间暂停解决方案
组件	时间来源	原因
CustomerInteractionState 倒计时	Time.deltaTime	游戏世界内等待
FanConversationPanel 动画	Time.unscaledDeltaTime	暂停时UI继续
NarrativeManager 协程	WaitForSecondsRealtime	暂停时继续处理
七、文件创建/修改顺序
修改 FanConversationPanel.cs - 修复时间暂停问题
修改 NarrativeManager.cs - 添加时间暂停控制
修改 CustomerRecord.cs - 添加 availableNarrativeIds
修改 CustomerBlackboardAdapter.cs - 添加状态字段
创建 CustomerInteractionState.cs - 单个顾客交互状态
创建 CustomerInteractionManager.cs - 全局管理器+公共池
创建 ShouldEnterInteractionCondition.cs - 行为树条件
创建 EnterDialogueStateAction.cs - 进入对话状态
创建 WaitForDialogueAction.cs - 等待对话状态
修改 CustomerAgent.cs - 集成组件
更新行为树资产 - Unity编辑器配置