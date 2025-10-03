using System.Collections.Generic;
using UnityEngine;
using PopLife.Runtime;

namespace PopLife
{
    /// <summary>
    /// 离店点管理器 - 管理所有出口点
    /// </summary>
    public class ExitPointManager : MonoBehaviour
    {
        public static ExitPointManager Instance { get; private set; }

        [Header("出口点列表")]
        [SerializeField] private List<ExitPoint> exitPoints = new List<ExitPoint>();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            // 自动查找场景中的所有出口点
            RefreshExitPoints();
        }

        /// <summary>
        /// 刷新出口点列表
        /// </summary>
        public void RefreshExitPoints()
        {
            exitPoints.Clear();
            var foundExits = FindObjectsByType<ExitPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            exitPoints.AddRange(foundExits);

            Debug.Log($"[ExitPointManager] Found {exitPoints.Count} exit points");
        }

        /// <summary>
        /// 获取离指定位置最近的出口点
        /// </summary>
        public ExitPoint GetNearestExitPoint(Vector3 position)
        {
            if (exitPoints.Count == 0)
            {
                Debug.LogWarning("[ExitPointManager] No exit points available");
                return null;
            }

            ExitPoint nearest = null;
            float minDistance = float.MaxValue;

            foreach (var exit in exitPoints)
            {
                if (exit == null) continue;

                float distance = Vector3.Distance(position, exit.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = exit;
                }
            }

            return nearest;
        }

        /// <summary>
        /// 获取所有出口点
        /// </summary>
        public List<ExitPoint> GetAllExitPoints()
        {
            return new List<ExitPoint>(exitPoints);
        }

        /// <summary>
        /// 根据ID获取出口点
        /// </summary>
        public ExitPoint GetExitPointById(string exitId)
        {
            if (string.IsNullOrEmpty(exitId))
                return null;

            foreach (var exit in exitPoints)
            {
                if (exit != null && exit.exitId == exitId)
                    return exit;
            }

            return null;
        }
    }
}
