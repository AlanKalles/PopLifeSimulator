using UnityEngine;
using PopLife.Customers.Runtime;
using PopLife.Customers.Data;


namespace PopLife.Customers.Data
{
// 可选：Trait 行为钩子（原型期可不实现具体逻辑）
    public abstract class TraitHook : ScriptableObject
    {
        public virtual void OnSpawn(CustomerAgent agent) { }
        public virtual void OnTargetChosen(CustomerAgent agent, string shelfId) { }
        public virtual void OnQueueEnter(CustomerAgent agent, string pointId) { }
        public virtual void OnPurchaseComputed(CustomerAgent agent, string shelfId, ref int qty) { }
        public virtual void OnCheckout(CustomerAgent agent, int spent) { }
        public virtual void OnLeave(CustomerAgent agent, string reason) { }
    }
}