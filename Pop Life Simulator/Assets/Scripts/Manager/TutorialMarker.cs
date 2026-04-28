namespace PopLife.Manager
{
    /// <summary>
    /// Tutorial trigger markers that can be raised from anywhere in the code
    /// 教程触发标记，可在代码任意位置触发
    /// </summary>
    public enum TutorialMarker
    {
        None = -1,                // 无触发（默认）

        // Game start
        GameStarted = 0,          // 游戏启动

        // Build phase
        FirstBuildPhaseEntered,   // 1  首次进入建造模式
        FirstTimeBuild,           // 2  首次进入建造Place模式
        BeforeFirstShelfPlaced,   // 3
        FirstShelfPlaced,         // 4  首次放置货架

        WhatIsMoney,              // 5
        OneMoreShelf,             // 6
        BeforeTwoShelvesPlaced,   // 7
        TwoShelvesPlaced,         // 8  放置了2个货架
        FirstBuildingUpgraded,    // 9  首次升级建筑

        // Open phase
        StoreOpened,              // 10 商店开张
        FirstCustomerEntered,     // 11 首位顾客进店
        FirstCustomerPurchased,   // 12 首位顾客购买
        FirstCustomerCheckedOut,  // 13 首位顾客结账

        // Economy
        FirstFameEarned,          // 14 首次获得声望
        FirstDayCompleted,        // 15 首日结束
        EarnedMoney1000,          // 16 赚到1000金钱

        // Advanced
        UnlockedBlueprint,        // 17 解锁蓝图
        PlacedFacility,           // 18 放置设施

        // Construction modes
        FirstMoveMode,            // 19 首次进入Move模式
        FirstDestroyMode,         // 20 首次进入Destroy(Sell)模式

        // AlanBot panels
        FirstCalendarOpened,      // 21 首次打开日历面板
        FirstCustomerCodexOpened, // 22 首次打开顾客图鉴面板
        FirstItemCodexOpened,     // 23 首次打开物品图鉴面板

        // Quest
        FirstQuestToastDismissed, // 24 首个任务通知Toast完全消失

        // Store UI
        EnableStoreToggle,        // 25 解锁开店按钮（一次性，游戏开局教程用）

        BottomButton,             // 26
        AlreadyKnowShelfSelectionPanel, // 27
        PointOverShelf,           // 28
        Alanbot,                  // 29

        // Day-based
        Day5Auditor1stQuestDDL,   // 30 第5天开始时触发（审计员首个任务DDL）

        // Lottery
        LotteryUnlocked,          // 31 第3天解锁彩票系统

        // Day-based triggers for quest chain activation
        Day2Trigger,              // 32 — raised by DayLoopManager on Day 2 start
        Day3Trigger,              // 33
        Day4Trigger,              // 34
        Day8Trigger,              // 35
        Day10Trigger,             // 36
        Day12Trigger,             // 37
        Day15Trigger,             // 38

        // Chain 1: Buildings placed (current total) — starts Day 10
        a_Building1C,             // 39
        a_Building2C,             // 40
        a_Building3C,             // 41
        a_Building5C,             // 42
        a_Building7C,             // 43
        a_Building10C,            // 44

        // Chain 2: Daily revenue thresholds — starts Day 3
        a_Revenue50C,             // 45
        a_Revenue100C,            // 46
        a_Revenue200C,            // 47
        a_Revenue500C,            // 48
        a_Revenue1000C,           // 49

        // Chain 3: Lifetime income thresholds — starts Day 2
        a_Income500C,             // 50
        a_Income1000C,            // 51
        a_Income3000C,            // 52
        a_Income5000C,            // 53
        a_Income10000C,           // 54

        // Chain 4: Daily customers served — starts Day 4
        a_DailyCustomers5C,       // 55
        a_DailyCustomers10C,      // 56
        a_DailyCustomers20C,      // 57
        a_DailyCustomers50C,      // 58
        a_DailyCustomers100C,     // 59

        // Chain 5: Blueprints unlocked (manual trigger) — starts Day 8
        a_BP10C,                  // 60
        a_BP20C,                  // 61
        a_BP50C,                  // 62

        // Chain 6: Total customers served (cumulative) — starts Day 12
        a_Serve100C,              // 63
        a_Serve500C,              // 64
        a_Serve1000C,             // 65

        // Chain 7: Total items sold (cumulative) — starts Day 15
        a_Sell1000C,              // 66
        a_Sell5000C,              // 67
        a_Sell10000C,             // 68

        // Story quest completion flags
        a_WelcomeC,               // 69
        a_FirstshelfC,            // 70
        a_FiveshelfC,             // 71
        a_TenshelfC,              // 72
        a_ProtectionC,            // 73
        a_WellnessC,              // 74
        a_BoostC,                 // 75
        a_PleasureCondomC,        // 76
        a_InclusivenessC,         // 77
        a_CucciC,                 // 78
        a_AwkwardSexC,            // 79
        a_BrandingStrategyC,      // 80
        a_CyberC,                 // 81
        a_BDSMC,                  // 82
        a_RoughSexC,              // 83
        a_AnalC,                  // 84
        a_FirstChildrensDayC,     // 85

        // Seasonal events
        ChildrensDayFinished,     // 86 — fired when the Children's Day seasonal event ends
    }
}
