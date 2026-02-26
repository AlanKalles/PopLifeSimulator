using System;
using UnityEngine;
using PopLife.Data;

namespace PopLife.Quest
{
    /// <summary>
    /// 任务完成条件类型枚举
    /// </summary>
    public enum QuestConditionType
    {
        Manual,              // 手动完成（由 TutorialMarkerBridge/对话 标记）
        SellItems,           // 卖出N件商品（总计或指定类别）
        EarnMoney,           // 赚取N金钱（当日或累计）
        EarnFame,            // 赚取N声望（当日或累计）
        ServeCustomers,      // 服务N位顾客
        PlaceBuildings,      // 放置N个建筑（任意或指定类别，含设施）
        PlaceShelves,        // 放置N个货架（仅货架，可选类别过滤）
        ReachStoreAppeal,    // 店铺Appeal达到N
        SurviveDays,         // 经营N天
        ReachMoneyThreshold, // 金钱达到N
    }

    /// <summary>
    /// 计数范围
    /// </summary>
    public enum CountScope
    {
        Cumulative, // 从任务激活起累计
        Daily,      // 每日重置
        Current,    // 当前快照值（ReachStoreAppeal/ReachMoneyThreshold 使用）
    }

    /// <summary>
    /// 任务条件数据（序列化嵌入 QuestDefinition）
    /// 每个条件对应 Dialogue Database 中的一个 Quest Entry
    /// conditions[i] 对应 QuestLog Entry 编号 i+1
    /// </summary>
    [Serializable]
    public class QuestCondition
    {
        [Tooltip("条件类型。Manual = 不自动追踪，由外部代码标记完成")]
        public QuestConditionType conditionType = QuestConditionType.Manual;

        [Tooltip("计数范围。Cumulative=累计，Daily=每日重置，Current=当前快照值")]
        public CountScope scope = CountScope.Cumulative;

        [Tooltip("目标数值（如：卖出100件、赚到5000金币）")]
        public int targetValue;

        [Tooltip("是否启用商品类别过滤（仅 SellItems/PlaceBuildings/PlaceShelves 使用）")]
        public bool useFilterCategory = false;

        [Tooltip("商品类别过滤（启用 useFilterCategory 时有效）")]
        public ProductCategory filterCategory;
    }
}
