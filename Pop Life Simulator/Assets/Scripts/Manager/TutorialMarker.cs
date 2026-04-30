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
        EnableStoreToggle,        // = 25  解锁开店按钮（一次性，游戏开局教程用）

        BottomButton,             // = 26
        AlreadyKnowShelfSelectionPanel, // = 27  (引用: 3_TalkAboutSellShelf.asset)
        PointOverShelf,           // = 28

        // AlanBot 解锁
        CallAlanBot,              // = 29  解锁 AlanBot 本体 + AlanBot Button

        // Day-based
        Day5Auditor1stQuestDDL,   // = 30  第5天开始时触发（审计员首个任务DDL） (引用: _Auditor1stMissionDDL.asset)

        // Lottery
        LotteryUnlocked,          // = 31  第3天解锁彩票系统 (引用: _LotteryUnlock.asset)

        // Restock
        CallRestock,              // = 32  解锁 Restock Button
        FirstRestockDecisionMade, // = 33  首次在 RestockPanel 点击 Restock 或 Skip Restock 按钮（任一即触发，仅一次）
    }
}
