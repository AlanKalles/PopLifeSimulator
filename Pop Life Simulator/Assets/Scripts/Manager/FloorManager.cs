using UnityEngine;

namespace PopLife.Runtime
{
    public class FloorManager : MonoBehaviour
    {
        [SerializeField] private FloorGrid activeFloor;
        public FloorGrid GetActiveFloor() => activeFloor;
        public FloorGrid GetFloor(int id) => activeFloor; // 单层原型：总是返回当前
        public void SetActive(FloorGrid floor) => activeFloor = floor;
    }
}