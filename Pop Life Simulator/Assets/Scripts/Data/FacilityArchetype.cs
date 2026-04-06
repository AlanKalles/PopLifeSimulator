using System;
using System.Collections.Generic;
using UnityEngine;

namespace PopLife.Data
{
// 设施原型
    [CreateAssetMenu(menuName = "PopLife/Buildings/FacilityArchetype")]
    public class FacilityArchetype : BuildingArchetype
    {
        [Header("设施属性")]
        public FacilityType facilityType;

        [Serializable]
        public class FacilityEffect
        {
            public EffectType type;
            public float value = 1f;
            public float radius = 3f;
            public bool affectsSameFloor = true;
        }

        public List<FacilityEffect> effects = new();
    }
}