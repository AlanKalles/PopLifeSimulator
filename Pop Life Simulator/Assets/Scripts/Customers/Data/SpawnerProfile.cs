using System;
using System.Collections.Generic;
using UnityEngine;


namespace PopLife.Customers.Data
{
    [CreateAssetMenu(menuName = "PopLife/Customers/SpawnerProfile")]
    public class SpawnerProfile : ScriptableObject
    {
        [Serializable] public class WeightedArchetype { public CustomerArchetype archetype; [Range(0,1)] public float weight = 0.1f; }
        [Serializable] public class WeightedTrait { public Trait trait; [Range(0,1)] public float weight = 0.1f; }


        public List<WeightedArchetype> archetypes = new();
        public List<WeightedTrait> traits = new();
        public Vector2Int visitsPerDayRange = new(20, 40);
    }
}