using System.Collections.Generic;
using UnityEngine;
using PopLife.Customers.Data;


namespace PopLife.Customers.Services
{
    public class TraitRegistry : MonoBehaviour
    {
        public static TraitRegistry Instance;
        void Awake(){ Instance = this; BuildIndex(); }


        [SerializeField] private List<Trait> traits = new();
        private readonly Dictionary<string, Trait> byId = new();


        private void BuildIndex(){ byId.Clear(); foreach (var t in traits) if (t) byId[t.traitId] = t; }
        public Trait Get(string id) => (id != null && byId.TryGetValue(id, out var t)) ? t : null;
        public List<Trait> Resolve(string[] ids){ var list = new List<Trait>(); if (ids==null) return list; foreach (var id in ids){ var t = Get(id); if (t) list.Add(t);} return list; }
    }
}