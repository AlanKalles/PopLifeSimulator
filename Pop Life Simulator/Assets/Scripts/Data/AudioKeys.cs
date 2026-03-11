namespace PopLife.Data
{
    /// <summary>
    /// 音频键名常量 - 统一管理所有音效和背景音乐的键名
    /// 避免拼写错误，提供代码自动补全
    /// </summary>
    public static class AudioKeys
    {
        #region 建造音效 (Building Sound Effects)

        // 货架建造音效 - 按商品类别分类
        public const string BUILD_ACCESSORIES = "Build_Accessories";
        public const string BUILD_ANAL = "Build_Anal";
        public const string BUILD_DIGITAL_MEDIA = "Build_DigitalMedia";
        public const string BUILD_DILDO = "Build_Dildo";
        public const string BUILD_ENHANCEMENTS = "Build_Enhancements";
        public const string BUILD_FLESHLIGHT = "Build_Fleshlight";
        public const string BUILD_FURNITURE = "Build_Furniture";
        public const string BUILD_INSTRUMENTS = "Build_Instruments";
        public const string BUILD_LINGERIE = "Build_Lingerie";
        public const string BUILD_VIBRATOR = "Build_Vibrator";
        public const string BUILD_WELLNESS = "Build_Wellness";

        // 设施建造音效
        public const string BUILD_FACILITY = "Build_Facility";

        // 通用建造音效（兜底）
        public const string BUILDING_PLACED = "BuildingPlaced";

        #endregion

        #region 建筑操作音效 (Building Operations)

        public const string BUILDING_MOVED = "BuildingMoved";
        public const string BUILDING_DESTROYED = "BuildingDestroyed";
        public const string BUILDING_UPGRADED = "BuildingUpgraded";

        #endregion

        #region UI 音效 (UI Sound Effects)

        public const string UI_CLICK = "UI_Click";
        public const string UI_HOVER = "UI_Hover";
        public const string UI_CONFIRM = "UI_Confirm";
        public const string UI_CANCEL = "UI_Cancel";
        public const string UI_ERROR = "UI_Error";
        public const string UI_STATS_OPEN = "UI_Stats_Open";
        public const string UI_STATS_SWITCH = "UI_Stats_Switch";
        public const string UI_BUILD_PANEL_OPEN = "UI_BuildPanel_Open";
        public const string SETTLEMENT_TICK = "Settlement_Tick";           // 结算数字滚动tick
        public const string SETTLEMENT_GRADE = "Settlement_Grade";         // 结算评级揭晓音效
        public const string UI_GUIDE_PAGE = "UI_Guide_Page";                // 操作指南翻页音效

        #endregion

        #region 顾客音效 (Customer Sound Effects)

        public const string CUSTOMER_ENTER = "Customer_Enter";
        public const string CUSTOMER_PURCHASE = "Customer_Purchase";
        public const string CUSTOMER_CHECKOUT = "Customer_Checkout";
        public const string CUSTOMER_LEAVE = "Customer_Leave";

        #endregion

        #region 游戏阶段音效 (Game Phase Sound Effects)

        public const string STORE_OPEN = "Store_Open";
        public const string STORE_CLOSE = "Store_Close";

        #endregion

        #region 任务系统音效 (Quest Sound Effects)

        public const string QUEST_TRACKER_OPEN = "Quest_Tracker_Open";
        public const string QUEST_TRACKER_CLOSE = "Quest_Tracker_Close";
        public const string QUEST_ENTRY_COMPLETE = "Quest_Entry_Complete";
        public const string QUEST_COMPLETE = "Quest_Complete";
        public const string QUEST_NEW = "Quest_New";
        public const string QUEST_FAILED = "Quest_Failed";

        #endregion

        #region 彩票音效 (Lottery Sound Effects)

        public const string LOTTERY_PURCHASE = "Lottery_Purchase";
        public const string LOTTERY_WIN = "Lottery_Win";

        #endregion

        #region 背景音乐 (Background Music)

        public const string BGM_MENU = "BGM_Menu";
        public const string BGM_SHOP = "BGM_Shop";
        public const string BGM_BUILD_PHASE = "BGM_BuildPhase";
        public const string BGM_NIGHT = "BGM_Night";

        #endregion

        #region 辅助方法 (Helper Methods)

        /// <summary>
        /// 根据商品类别获取对应的建造音效键
        /// </summary>
        public static string GetBuildSoundKey(ProductCategory category)
        {
            return category switch
            {
                ProductCategory.Accessories => BUILD_ACCESSORIES,
                ProductCategory.Anal => BUILD_ANAL,
                ProductCategory.DigitalMedia => BUILD_DIGITAL_MEDIA,
                ProductCategory.Dildo => BUILD_DILDO,
                ProductCategory.Enhancements => BUILD_ENHANCEMENTS,
                ProductCategory.Fleshlight => BUILD_FLESHLIGHT,
                ProductCategory.Furniture => BUILD_FURNITURE,
                ProductCategory.Instruments => BUILD_INSTRUMENTS,
                ProductCategory.Lingerie => BUILD_LINGERIE,
                ProductCategory.Vibrator => BUILD_VIBRATOR,
                ProductCategory.Wellness => BUILD_WELLNESS,
                _ => BUILDING_PLACED // 兜底
            };
        }

        #endregion
    }
}
