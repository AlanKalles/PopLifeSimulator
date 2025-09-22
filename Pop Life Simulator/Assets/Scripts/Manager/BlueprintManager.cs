using UnityEngine;

namespace PopLife
{
    public class BlueprintManager : MonoBehaviour
    {
        public static BlueprintManager Instance;
        void Awake(){ Instance = this; }

        public bool HasBlueprint(string id) => true;         // 一律认为有
        public void ConsumeBlueprint(string id) { }          // 不做事
        public void AddBlueprint(string id) { }              // 不做事
    }
}