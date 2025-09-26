using System;
using System.Collections.Generic;
using UnityEngine;


namespace PopLife.Customers.Services
{
    public class NavigationService : MonoBehaviour
    {
        public static NavigationService Instance;
        void Awake(){ Instance = this; }


// A* 封装（若无A*则直线/不可达）
        public bool TryRequestPath(Vector3 start, Vector3 end, out List<Vector3> path)
        {
#if ASTAR_PATHFINDING_PROJECT
// 这里可接入 Pathfinding.Seeker 等（略）
#endif
            path = new List<Vector3> { start, end };
            return true;
        }
    }
}