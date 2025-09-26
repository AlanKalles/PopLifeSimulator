using System.Collections.Generic;
using UnityEngine;
using PopLife.Runtime; // 引用 ShelfInstance


namespace PopLife.Customers.Services
{
    public struct ReservationTicket
    {
        public string shelfInstanceId;
        public int qty;
    }


    public class CommerceService : MonoBehaviour
    {
        public static CommerceService Instance;
        void Awake(){ Instance = this; }
        
        // 原型期：软预留只返回可买量，不真正锁库存
        public ReservationTicket SoftReserve(ShelfInstance shelf, int wantQty)
        {
            int can = Mathf.Clamp(wantQty, 0, shelf.currentStock);
            return new ReservationTicket { shelfInstanceId = shelf.instanceId, qty = can };
        }


        public int CommitAtCashier(ReservationTicket ticket)
        {
// 简化：直接减库存，返回成交量
// 实际应通过 ShelfInstance 的线程安全/串行接口处理
            var shelf = FindShelf(ticket.shelfInstanceId);
            if (!shelf) return 0;
            int deal = Mathf.Clamp(ticket.qty, 0, shelf.currentStock);
            shelf.currentStock -= deal;
            return deal;
        }
        
        private ShelfInstance FindShelf(string id)
        {
// 原型版：全局查找（可替换为索引）
            var all = GameObject.FindObjectsByType<ShelfInstance>(FindObjectsSortMode.None);
    
            foreach (var s in all) 
                if (s.instanceId == id) 
                    return s;
    
            return null;
        }
    }
}