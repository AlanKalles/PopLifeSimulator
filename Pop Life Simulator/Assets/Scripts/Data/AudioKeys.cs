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
        public const string BUILD_LINGERIE = "Build_Lingerie";
        public const string BUILD_CONDOM = "Build_Condom";
        public const string BUILD_VIBRATOR = "Build_Vibrator";
        public const string BUILD_FLESHLIGHT = "Build_Fleshlight";
        public const string BUILD_LUBRICANT = "Build_Lubricant";
        public const string BUILD_BDSM = "Build_BDSM";

        // 设施建造音效
        public const string BUILD_FACILITY = "Build_Facility";

        // 通用建造音效（兜底）
        public const string BUILDING_PLACED = "BuildingPlaced";

        #endregion

        #region 建筑操作音效 (Building Operations)

        public const string BUILDING_MOVED = "BuildingMoved";
        public const string BUILDING_DESTROYED = "BuildingDestroyed";

        #endregion

        #region UI 音效 (UI Sound Effects)

        public const string UI_CLICK = "UI_Click";
        public const string UI_HOVER = "UI_Hover";
        public const string UI_CONFIRM = "UI_Confirm";
        public const string UI_CANCEL = "UI_Cancel";
        public const string UI_ERROR = "UI_Error";

        #endregion

        #region 顾客音效 (Customer Sound Effects)

        public const string CUSTOMER_ENTER = "Customer_Enter";
        public const string CUSTOMER_PURCHASE = "Customer_Purchase";
        public const string CUSTOMER_CHECKOUT = "Customer_Checkout";
        public const string CUSTOMER_LEAVE = "Customer_Leave";

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
                ProductCategory.Lingerie => BUILD_LINGERIE,
                ProductCategory.Condom => BUILD_CONDOM,
                ProductCategory.Vibrator => BUILD_VIBRATOR,
                ProductCategory.Fleshlight => BUILD_FLESHLIGHT,
                ProductCategory.Lubricant => BUILD_LUBRICANT,
                ProductCategory.BDSM => BUILD_BDSM,
                _ => BUILDING_PLACED // 兜底
            };
        }

        #endregion
    }
}
