# Pop Life Simulator — 功能规划路线图 (Feature Roadmap Plan)

> **For agentic workers:** Use superpowers:subagent-driven-development or superpowers:executing-plans to implement task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把当前迭代的所有需求整理为单一可执行清单，标清每项的代码现状、负责人、优先级与依赖，作为冲内测的总调度表。

**核查日期:** 2026-05-31（已逐项核对真实代码，非仅文档）

**图例:** 负责人 🟦Claude · 🟧Alan · 🟩设计 · 美术🎨ㅤ|ㅤ状态 ✅已实现 · 🔶半成品 · ⬜未开始ㅤ|ㅤ优先级 P0必须 · P1重要 · P2可选

---

## 主表 — 全量需求

| # | 功能 | 状态 | 实际缺口（要做的事） | 负责人 | 优先级 | 关键文件 |
|---|---|---|---|---|---|---|
| 1 | 破产循环 | 🔶 | 机制已全做；只差**破产对话**接入 + 贷款 UX/文案 | 🟪 | **P0** | `Manager/BankruptcyManager.cs`、`Manager/LoanManager.cs`、`UI/BankruptcyPanel.cs`、`UI/LoanPanel.cs` |
| 2 | 难度过高 | ⬜ | 集中**调参**：开局现金/维护费/客流/客单价；可加开局保底 | 🟩+🟦 | **P0** | `Data/ShelfArchetypes.cs`、`Customers/Spawner/CustomerSpawner.cs`、`Manager/DayLoopManager.cs` |
| 3 | 每日结算 | 🔶 | 图表已齐（趋势/饼图/Top3）；缺**可行动解读**+决策引导 | 🟩+🟦 | **P0** | `UI/NewDailySettlementPanel.cs`、`Manager/DayLoopManager.cs` |
| 4 | 内测 | ⬜ | 出包/反馈渠道/指标；以 #2#3 为主验证目标 | 🟪 | **P0** | — |
| 5 | 购买新楼层 Quest 线 | ⬜ | **全新**：楼层付费解锁机制 + 配套 quest | 🟪 | P1 | `Runtime/WorldGrid.cs`、`Quest/QuestLogicManager.cs`、`Data/QuestDefinition.cs`、`Data/FloorTileArchetype.cs` |
| 6 | 剧情深化 | 🔶 | 框架就绪；纯**内容产出**（更多/深/复杂分支） | 🟩+🟦 | P1 | `DialogueBridge/*`、`Quest/*` |
| 7 | 成就 UI + 任务差异化 | ⬜ | 无独立成就系统；建议**复用 Quest 分类型**（方案B） | 🟪 | P1 | `Data/QuestDefinition.cs`、`UI/Quest/*` |
| 8 | 货架数值 override | ⬜ | 纯公式无 override；加 `useCustomValues` 开关+可选字段 | 🟧 | P1 | `Data/ShelfArchetypes.cs` |
| 9 | 主动 Policy 界面 | 🔶 | **启动后端已存在**（调用启动 policy）；只差**玩家 UI** | 🟩 | P1 | （对接现有启动接口） |
| 10 | Bundle 系统 | ⬜ | **全新**：货架组合→数值加成；定义组合规则与检测 | 🟧+🟩 | P2 | 参考 `Data/FacilityArchetype.cs` EffectType 模式 |
| 11 | 装饰物系统 | ⬜ | **全新**：buff 加成 + sprite + 放置；可复用 EffectManager | 🟧+🟩+🎨 | P2 | `Manager/EffectManager.cs`、`Runtime/InteriorGrid.cs` |
| 12 | 货架放置位置策略 | ⬜ | 决策里**无距离/位置因素**；加热区加成/死角惩罚 | 🟩+🟦 | P2 | `Customers/Data/Policies/FunnelTargetSelector.cs`、`Runtime/InteriorGrid.cs` |
| 13 | 精简 Quest marker | ⬜ | 114 个巨型 enum 序列化为 int，三系统耦合；解耦 | 🟦 | P2 | `Manager/TutorialMarker.cs`、`DialogueBridge/TutorialMarkerBridge.cs` |

---

## 执行波次 — checkbox 任务清单

### 第一波 · 冲内测（P0）
- [ ] **#1** 走查破产→贷款全流程，列出缺失对话/弹窗，补对话 + 贷款 UX（不重写系统）
- [ ] **#2** 建可调参数清单，集中调难度（成本/客流/客单价/开局保底）
- [ ] **#3** 为结算关键数字加可行动解读，把面板变为下一天决策入口；补客单价/转化率/Δ
- [ ] **#4** 内测出包，用真实数据验证 #2/#3（关注流失点/破产率/首日留存）

### 第二波 · 内容与结构（P1）
- [ ] **#13** 趁剧情未大扩张，先做 marker 解耦（还技术债，全量搜 .asset 引用）
- [ ] **#7** Quest 加 `questType` 枚举，UI 按类型分区 → 实现"成就"与任务差异化
- [ ] **#5** 设计楼层解锁门槛（金钱/声望）→ 实现机制 + quest 线
- [ ] **#6** 框架就绪后持续产出剧情分支
- [ ] **#8** (Alan) 货架 override 字段
- [ ] **#9** (设计) 主动 policy 玩家 UI，对接现有启动接口

### 第三波 · 深度系统（P2）
- [ ] **#10** (Alan) Bundle 组合加成系统
- [ ] **#11** (Alan+美术) 装饰物 buff + sprite
- [ ] **#12** 货架放置位置策略（建议与 #10 协同设计，避免机制打架）

---

## 待你拍板（4 项）
- [ ] **成就系统**：复用 Quest 分类型(建议) vs 新建独立系统？
- [ ] **#13 marker 精简**：是否在剧情大扩张前做？(建议是)
- [ ] **难度方向**：降成本 / 提客流 / 加开局保底 —— 单选或组合？
- [ ] **楼层解锁**：金钱还是声望门槛？与难度曲线如何咬合？

---

> 详细分项说明见 `Pop Life Simulator/Assets/Documents/功能规划路线图.md`
