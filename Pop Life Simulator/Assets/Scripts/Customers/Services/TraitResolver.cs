using System.Collections.Generic;
using UnityEngine;
using PopLife.Customers.Data;


namespace PopLife.Customers.Services
{
    public struct EffectiveStats
    {
        public float walletCapMul;
        public float patienceMul;
        public float embarrassmentCapMul;
        public float priceSensitivityMul;
        public float moveSpeedMul;
        public float xpMul;  // 经验获取倍率
    }


    public static class TraitResolver
    {
        public static EffectiveStats Compute(IReadOnlyList<Trait> traits)
        {
            var e = new EffectiveStats{ walletCapMul = 1f, patienceMul = 1f, embarrassmentCapMul = 1f, priceSensitivityMul = 1f, moveSpeedMul = 1f, xpMul = 1f };
            if (traits == null) return e;
            for (int i = 0; i < traits.Count; i++)
            {
                var t = traits[i]; if (t == null) continue;
                e.walletCapMul *= t.walletCapMul;
                e.patienceMul *= t.patienceMul;
                e.embarrassmentCapMul *= t.embarrassmentCapMul;
                e.priceSensitivityMul *= t.priceSensitivityMul;
                e.moveSpeedMul *= t.moveSpeedMul;
                e.xpMul *= t.xpMultiplier;
            }
            return e;
        }
    }
}