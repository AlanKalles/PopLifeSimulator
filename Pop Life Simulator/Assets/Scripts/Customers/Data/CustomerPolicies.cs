using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PopLife.Data;


namespace PopLife.Customers.Data
{
// —— 漏斗阶段枚举 ——
    /// <summary>
    /// 顾客购物漏斗的四个阶段，按优先级从高到低
    /// </summary>
    public enum FunnelPhase
    {
        Favorite,   // 偏好货架（精确名称匹配）
        Category,   // 同品类货架
        Brand,      // 同品牌货架
        PriceBased  // 剩余所有可购买货架
    }

// —— 策略基类（SO）——
    public abstract class TargetSelectorPolicy : ScriptableObject
    {
        public abstract int SelectTargetShelf(in CustomerContext ctx, List<ShelfSnapshot> candidates);
    }
    public abstract class PurchasePolicy : ScriptableObject
    {
        public abstract int DecidePurchaseQty(in CustomerContext ctx, in ShelfSnapshot shelf, int wallet, int price);
    }
    public abstract class QueuePolicy : ScriptableObject
    {
        public abstract bool ShouldSwitchQueue(in CustomerContext ctx, int myPos, int predictedSeconds);
    }
    public abstract class PathPolicy : ScriptableObject
    {
        public abstract bool ShouldRepath(in CustomerContext ctx, float lastRepath, float distLeft);
    }
    public abstract class EmbarrassmentPolicy : ScriptableObject
    {
        public abstract int TickEmbarrassment(in CustomerContext ctx, int eebPerSec);
    }
    public abstract class CheckoutPolicy : ScriptableObject
    {
        public abstract int ChooseCashier(in CustomerContext ctx, List<CashierSnapshot> cashiers);
    }


// —— 运行时上下文快照（供策略使用）——
    public struct CustomerContext
    {
        public string customerId;
        public int loyaltyLevel;
        public int trust;

        // 偏好货架（按优先级排序的 archetypeId）
        public string[] favoriteShelfIds;
        // 偏好货架的去重品类索引（Initialize 时预计算）
        public int[] favoriteCategoryIndices;
        // 偏好货架的去重品牌（Initialize 时预计算）
        public BrandData[] favoriteBrands;

        // 当前漏斗阶段（由行为树设置）
        public FunnelPhase currentPhase;
        // 最近一次货架选择命中的漏斗层级（由策略写入）
        public FunnelPhase currentFunnelTier;
        // 当前钱包余额（用于买得起过滤）
        public int walletAmount;

        public int embarrassmentCap;
        public float moveSpeed;
        public int queueToleranceSec;
        public HashSet<ShelfArchetype> purchasedArchetypes; // 本次访问已购买的货架archetype（SO引用）
    }


    public struct ShelfSnapshot
    {
        public string shelfId;
        public ShelfArchetype archetype; // 货架的archetype SO引用（运行时比较用）
        public int categoryIndex; // 与 ProductCategory 对齐
        public int appeal; // 0..N（来自货架等级与系统）
        public int price;
        public int stock;
        public Vector2Int gridCell;
        public int queueLength;
    }


    public struct CashierSnapshot
    {
        public string cashierId;
        public Vector2Int gridCell;
        public int queueLength;
    }
}