using UnityEngine;
using PopLife.Runtime;
using PopLife.Data;

namespace PopLife
{
    public class EffectManager : MonoBehaviour
    {
        public static EffectManager Instance;
        void Awake(){ Instance = this; }

        public void RegisterEffect(FacilityInstance inst, FacilityArchetype.FacilityEffect eff) { /* 原型期不实现 */ }
        public void UnregisterEffect(FacilityInstance inst) { }
    }
}
