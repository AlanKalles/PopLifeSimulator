using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace PopLife.Customers.Data
{
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
        public int[] interest; // 对齐 ProductCategory
        public int embarrassmentCap;
        public float moveSpeed;
        public int queueToleranceSec;
        public HashSet<string> purchasedArchetypes; // 本次访问已购买的货架archetype ID
    }


    public struct ShelfSnapshot
    {
        public string shelfId;
        public string archetypeId; // 货架的archetype ID
        public int categoryIndex; // 与 ProductCategory 对齐
        public int attractiveness; // 0..N（来自货架等级与系统）
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