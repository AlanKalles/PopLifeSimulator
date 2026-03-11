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
        FirstBuildPhaseEntered,   // 首次进入建造模式
        FirstTimeBuild,           // 首次进入建造Place模式
        BeforeFirstShelfPlaced,
        FirstShelfPlaced,         // 首次放置货架

        WhatIsMoney,
        OneMoreShelf,
        BeforeTwoShelvesPlaced,
        TwoShelvesPlaced,         // 放置了2个货架
        FirstBuildingUpgraded,    // 首次升级建筑

        // Open phase
        StoreOpened,              // 商店开张
        FirstCustomerEntered,     // 首位顾客进店
        FirstCustomerPurchased,   // 首位顾客购买
        FirstCustomerCheckedOut,  // 首位顾客结账

        // Economy
        FirstFameEarned,          // 首次获得声望
        FirstDayCompleted,        // 首日结束
        EarnedMoney1000,          // 赚到1000金钱

        // Advanced
        UnlockedBlueprint,        // 解锁蓝图
        PlacedFacility,           // 放置设施

        // Construction modes
        FirstMoveMode,            // 首次进入Move模式
        FirstDestroyMode,         // 首次进入Destroy(Sell)模式

        // AlanBot panels
        FirstCalendarOpened,      // 首次打开日历面板
        FirstCustomerCodexOpened, // 首次打开顾客图鉴面板
        FirstItemCodexOpened,     // 首次打开物品图鉴面板

        // Quest
        FirstQuestToastDismissed, // 首个任务通知Toast完全消失

        // Store UI
        EnableStoreToggle,            // 解锁开店按钮（一次性，游戏开局教程用）

        BottomButton,
        AlreadyKnowShelfSelectionPanel,
        PointOverShelf,
        Alanbot,
    }
}
