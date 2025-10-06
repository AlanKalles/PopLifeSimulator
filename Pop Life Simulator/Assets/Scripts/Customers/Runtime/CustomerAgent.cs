using UnityEngine;
using PopLife.Customers.Data;
using PopLife.Customers.Services;


namespace PopLife.Customers.Runtime
{
    [RequireComponent(typeof(CustomerBlackboardAdapter))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class CustomerAgent : MonoBehaviour
    {
        public CustomerBlackboardAdapter bb;
        public string customerID;
        public AppearanceDatabase appearanceDB;

        private SpriteRenderer spriteRenderer;

        void Awake()
        {
            if (!bb) bb = GetComponent<CustomerBlackboardAdapter>();
            if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();
        }


// 原型期的最小初始化：由 Spawner 调用
        public void Initialize(CustomerRecord record, CustomerArchetype archetype, Trait[] traits, int categories, int daySeed)
        {
// 0) 设置顾客ID
            customerID = record.customerId;

// 1) 设置外貌
            if (!string.IsNullOrEmpty(record.appearanceId) && appearanceDB != null)
            {
                Sprite sprite = appearanceDB.Get(record.appearanceId);
                if (sprite != null && spriteRenderer != null)
                {
                    spriteRenderer.sprite = sprite;
                }
                else if (sprite == null)
                {
                    Debug.LogWarning($"CustomerAgent: 找不到外貌ID '{record.appearanceId}' 对应的Sprite");
                }
            }

// 2) 最终兴趣（含 Trait interest 修正，已在 Record 组合）
            var finalInterest = record.ComposeFinalInterest(archetype, categories, traits);


// 3) Trait 乘子
            var eff = TraitResolver.Compute(traits);


// 4) 采样本次钱袋与尴尬上限（曲线 × Trait 乘子）
            int walletCap = Mathf.RoundToInt(record.walletCapBase * archetype.walletCapCurve.Eval(record.loyaltyLevel) * eff.walletCapMul);
            int embarrassmentCap = Mathf.RoundToInt(archetype.embarrassmentCapCurve.Eval(record.loyaltyLevel) * eff.embarrassmentCapMul);
            int queueTolerance = Mathf.RoundToInt(archetype.queueToleranceSeconds * eff.patienceMul);
            float finalMoveSpeed = archetype.moveSpeed * eff.moveSpeedMul;


// 5) 注入黑板
            bb.InjectFromRecord(record, archetype, finalInterest, embarrassmentCap, finalMoveSpeed);
            bb.moneyBag = Random.Range(Mathf.Max(10, walletCap/2), walletCap + 1);
            bb.embarrassment = 0;
            bb.queueToleranceSec = queueTolerance;


            CustomerEventBus.RaiseSpawned(this);
        }
    }
}