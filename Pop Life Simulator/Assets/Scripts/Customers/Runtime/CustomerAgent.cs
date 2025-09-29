using UnityEngine;
using PopLife.Customers.Data;
using PopLife.Customers.Services;


namespace PopLife.Customers.Runtime
{
    [RequireComponent(typeof(CustomerBlackboardAdapter))]
    public class CustomerAgent : MonoBehaviour
    {
        public CustomerBlackboardAdapter bb;
        public string customerID;


        void Awake(){ if (!bb) bb = GetComponent<CustomerBlackboardAdapter>(); }


// 原型期的最小初始化：由 Spawner 调用
        public void Initialize(CustomerRecord record, CustomerArchetype archetype, Trait[] traits, int categories, int daySeed)
        {
// 1) 最终兴趣（含 Trait interest 修正，已在 Record 组合）
            var finalInterest = record.ComposeFinalInterest(archetype, categories, traits);


// 2) Trait 乘子
            var eff = TraitResolver.Compute(traits);


// 3) 采样本次钱袋与尴尬上限（曲线 × Trait 乘子）
            int walletCap = Mathf.RoundToInt(record.walletCapBase * archetype.walletCapCurve.Eval(record.loyaltyLevel) * eff.walletCapMul);
            int embarrassmentCap = Mathf.RoundToInt(archetype.embarrassmentCapCurve.Eval(record.loyaltyLevel) * eff.embarrassmentCapMul);
            int queueTolerance = Mathf.RoundToInt(archetype.queueToleranceSeconds * eff.patienceMul);


// 4) 注入黑板
            bb.InjectFromRecord(record, archetype, finalInterest, embarrassmentCap);
            bb.moneyBag = Random.Range(Mathf.Max(10, walletCap/2), walletCap + 1);
            bb.embarrassment = 0;
            bb.queueToleranceSec = queueTolerance;


            CustomerEventBus.RaiseSpawned(this);
        }
    }
}