using System;
using UnityEngine;
using PopLife.Data;

namespace PopLife.Runtime{
// 设施实例
    public class FacilityInstance : BuildingInstance
    {
        private FacilityArchetype FA => (FacilityArchetype)archetype;

        protected override void OnInitialized() => ApplyEffects();
        private void OnDestroy() => EffectManager.Instance.UnregisterEffect(this);

        public void ApplyEffects()
        {
            foreach (var e in FA.effects)
                EffectManager.Instance.RegisterEffect(this, e);
        }
    }
}