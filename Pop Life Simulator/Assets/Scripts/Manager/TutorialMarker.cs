namespace PopLife.Manager
{
    /// <summary>
    /// Tutorial trigger markers that can be raised from anywhere in the code
    /// 教程触发标记，可在代码任意位置触发
    ///
    /// ⚠️ 维护规约（必读）：
    /// 1. 禁止给任何 marker 写显式赋值（`= N`），唯一例外是 `None = -1` 锚点。
    ///    原因：显式与隐式混搭时，新加的隐式成员会"前一项+1"，撞上其他显式值会产生 enum alias
    ///    （C# 不会报错），导致 HashSet/Dictionary/相等比较把两个 marker 合并，bug 极隐蔽。
    /// 2. SO 资产（DialogueActions/、OperationGuides/、Quests/）通过 **int 值** 序列化引用 marker。
    ///    新增 marker 直接追加到末尾即可（自动得到下一个递增 int）。
    /// 3. 不要在中间插入 marker、不要改变现有 marker 的声明顺序——这样会改它们的 int 值，
    ///    破坏所有引用了 .asset 文件中 `activationMarker:` 数字字段。
    ///    如果一定要重排，必须同步搜索 `Resources/ScriptableObjects/**/*.asset` 中
    ///    `activationMarker:` 字段并手动修正所有受影响 SO 的 int 值。
    /// </summary>
    public enum TutorialMarker
    {
        None = -1,                // 锚点：显式 -1。不要删，否则 enum 默认从 0 开始会与 GameStarted 冲突

        // Game start
        GameStarted,              // = 0  游戏启动

        // Build phase
        FirstBuildPhaseEntered,   // = 1  首次进入建造模式
        FirstTimeBuild,           // = 2  首次进入建造Place模式
        BeforeFirstShelfPlaced,   // = 3
        FirstShelfPlaced,         // = 4  首次放置货架

        WhatIsMoney,              // = 5
        OneMoreShelf,             // = 6
        BeforeTwoShelvesPlaced,   // = 7
        TwoShelvesPlaced,         // = 8  放置了2个货架
        FirstBuildingUpgraded,    // = 9  首次升级建筑

        // Open phase
        StoreOpened,              // = 10  商店开张
        FirstCustomerEntered,     // = 11  首位顾客进店
        FirstCustomerPurchased,   // = 12  首位顾客购买
        FirstCustomerCheckedOut,  // = 13  首位顾客结账

        // Economy
        FirstFameEarned,          // = 14  首次获得声望
        FirstDayCompleted,        // = 15  首日结束 (引用: 4_FinishRestock.asset, 5_FirstDayComplete.asset)
        EarnedMoney1000,          // = 16  赚到1000金钱

        // Advanced
        UnlockedBlueprint,        // = 17  解锁蓝图
        PlacedFacility,           // = 18  放置设施

        // Construction modes
        FirstMoveMode,            // = 19  首次进入Move模式 (引用: Guide_MovePhase.asset)
        FirstDestroyMode,         // = 20  首次进入Destroy(Sell)模式 (引用: Guide_SellPhase.asset)

        // AlanBot panels
        FirstCalendarOpened,      // = 21  首次打开日历面板 (引用: Guide_Calendar.asset)
        FirstCustomerCodexOpened, // = 22  首次打开顾客图鉴面板 (引用: Guide_CustomerCodex.asset)
        FirstItemCodexOpened,     // = 23  首次打开物品图鉴面板 (引用: Guide_ItemCodex.asset)

        // Quest
        FirstQuestToastDismissed, // = 24  首个任务通知Toast完全消失 (引用: Guide_Quest.asset)

        // Store UI
        EnableStoreToggle,        // = 25  [DEPRECATED] 解锁开店按钮已改由 StoreToggleAnimator 监听 Midori/4 对话结束直接触发，此 marker 不再被任何系统消费。保留 enum 值仅为兼容历史 .asset 序列化。

        BottomButton,             // = 26
        AlreadyKnowShelfSelectionPanel, // = 27  (引用: 3_TalkAboutSellShelf.asset)
        PointOverShelf,           // = 28

        // AlanBot 解锁
        CallAlanBot,              // = 29  解锁 AlanBot 本体 + AlanBot Button

        // Day-based
        Day5Auditor1stQuestDDL,   // = 30  第5天开始时触发（审计员首个任务DDL） (引用: _Auditor1stMissionDDL.asset)

        // Lottery
        LotteryUnlocked,          // = 31  第3天解锁彩票系统 (引用: _LotteryUnlock.asset)

        // Day-based triggers for quest chain activation (raised by DayLoopManager)
        Day2Trigger,              // = 32  raised by DayLoopManager on Day 2 start
        Day3Trigger,              // = 33
        Day4Trigger,              // = 34
        Day8Trigger,              // = 35
        Day10Trigger,             // = 36
        Day12Trigger,             // = 37
        Day15Trigger,             // = 38

        // Chain 1: Buildings placed (current total) — starts Day 10
        a_Building1C,             // = 39
        a_Building2C,             // = 40
        a_Building3C,             // = 41
        a_Building5C,             // = 42
        a_Building7C,             // = 43
        a_Building10C,            // = 44

        // Chain 2: Daily revenue thresholds — starts Day 3
        a_Revenue50C,             // = 45
        a_Revenue100C,            // = 46
        a_Revenue200C,            // = 47
        a_Revenue500C,            // = 48
        a_Revenue1000C,           // = 49

        // Chain 3: Lifetime income thresholds — starts Day 2
        a_Income500C,             // = 50
        a_Income1000C,            // = 51
        a_Income3000C,            // = 52
        a_Income5000C,            // = 53
        a_Income10000C,           // = 54

        // Chain 4: Daily customers served — starts Day 4
        a_DailyCustomers5C,       // = 55
        a_DailyCustomers10C,      // = 56
        a_DailyCustomers20C,      // = 57
        a_DailyCustomers50C,      // = 58
        a_DailyCustomers100C,     // = 59

        // Chain 5: Blueprints unlocked (manual trigger) — starts Day 8
        a_BP10C,                  // = 60
        a_BP20C,                  // = 61
        a_BP50C,                  // = 62

        // Chain 6: Total customers served (cumulative) — starts Day 12
        a_Serve100C,              // = 63
        a_Serve500C,              // = 64
        a_Serve1000C,             // = 65

        // Chain 7: Total items sold (cumulative) — starts Day 15
        a_Sell1000C,              // = 66
        a_Sell5000C,              // = 67
        a_Sell10000C,             // = 68

        // Story quest completion flags
        a_WelcomeC,               // = 69
        a_FirstshelfC,            // = 70
        a_FiveshelfC,             // = 71
        a_TenshelfC,              // = 72
        a_ProtectionC,            // = 73
        a_WellnessC,              // = 74
        a_BoostC,                 // = 75
        a_PleasureCondomC,        // = 76
        a_InclusivenessC,         // = 77
        a_CucciC,                 // = 78
        a_AwkwardSexC,            // = 79
        a_BrandingStrategyC,      // = 80
        a_CyberC,                 // = 81
        a_BDSMC,                  // = 82
        a_RoughSexC,              // = 83
        a_AnalC,                  // = 84
        a_FirstChildrensDayC,     // = 85

        // Seasonal events
        ChildrensDayFinished,     // = 86  fired when the Children's Day seasonal event ends (TODO: 接 CalendarManager)

        // Restock — moved to end after merging anna's chain markers (was 32/33 originally)
        // ⚠️ 重命名/重排号会破坏 SO 引用，需要同步改 .asset 中的 activationMarker 数字
        CallRestock,              // = 87  解锁 Restock Button
        FirstRestockDecisionMade, // = 88  首次在 RestockPanel 点击 Restock 或 Skip Restock 按钮（任一即触发，仅一次）(引用: 4_FinishRestock.asset)

        // Narrative quest activations from Dialogue System conversations
        Main4Activated,           // = 89
        Main5Activated,           // = 90
        Main6Activated,           // = 91
        Main7Activated,           // = 92
        Main8Activated,           // = 93
        Main9Activated,           // = 94
        BDSM1Activated,           // = 95
        VIPSoloFans1Activated,    // = 96
        VIPSoloFans2Activated,    // = 97
        VIPSoloFans3Activated,    // = 98
        VIPSoloFans4Activated,    // = 99
        VIPShyGirl1Activated,     // = 100
        VIPShyGirl2Activated,     // = 101
        VIPShyGirl3Activated,     // = 102
    }
}
