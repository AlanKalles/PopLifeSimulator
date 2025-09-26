using UnityEngine;


namespace PopLife.Customers.Services
{
    public class HeatmapService : MonoBehaviour
    {
        public static HeatmapService Instance;
        void Awake(){ Instance = this; }
        public int GetEEBAt(Vector2Int cell) => 0; // 原型期：无尴尬增量
    }
}