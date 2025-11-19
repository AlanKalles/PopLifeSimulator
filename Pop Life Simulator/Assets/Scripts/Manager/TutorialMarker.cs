namespace PopLife.Manager
{
    /// <summary>
    /// Tutorial trigger markers that can be raised from anywhere in the code
    /// 教程触发标记，可在代码任意位置触发
    /// </summary>
    public enum TutorialMarker
    {
        // Game start
        GameStarted,              // 游戏启动

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

        // Custom markers (可扩展)
        D002toGBUILDFIRSTSHELF,
        
    }
}
