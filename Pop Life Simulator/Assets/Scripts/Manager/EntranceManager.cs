using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PopLife.Runtime;

namespace PopLife
{
    /// <summary>
    /// 商店入口管理器
    /// 管理所有 StoreEntrancePoint，提供入口查询服务
    /// </summary>
    public class EntranceManager : MonoBehaviour
    {
        public static EntranceManager Instance { get; private set; }

        [Header("入口列表")]
        [Tooltip("场景中所有入口点（可自动发现）")]
        public List<StoreEntrancePoint> entrances = new List<StoreEntrancePoint>();

        [Header("配置")]
        [Tooltip("启动时自动发现场景中的所有入口")]
        public bool autoDiscoverEntrances = true;

        void Awake()
        {
            // 单例模式
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[EntranceManager] 场景中存在多个 EntranceManager，销毁重复实例");
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // 自动发现入口
            if (autoDiscoverEntrances)
            {
                DiscoverEntrances();
            }

            // 验证
            if (entrances.Count == 0)
            {
                Debug.LogWarning("[EntranceManager] 场景中没有找到任何 StoreEntrancePoint！");
            }
            else
            {
                Debug.Log($"[EntranceManager] 已注册 {entrances.Count} 个入口点");
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 自动发现场景中的所有入口点
        /// </summary>
        private void DiscoverEntrances()
        {
            var foundEntrances = FindObjectsByType<StoreEntrancePoint>(FindObjectsSortMode.None);
            foreach (var entrance in foundEntrances)
            {
                if (!entrances.Contains(entrance))
                {
                    entrances.Add(entrance);
                }
            }

            Debug.Log($"[EntranceManager] 自动发现 {foundEntrances.Length} 个入口点");
        }

        /// <summary>
        /// 注册入口（由 StoreEntrancePoint.OnEnable 调用）
        /// </summary>
        public void RegisterEntrance(StoreEntrancePoint entrance)
        {
            if (entrance == null) return;

            if (!entrances.Contains(entrance))
            {
                entrances.Add(entrance);
                Debug.Log($"[EntranceManager] 注册入口: {entrance.entranceId}");
            }
        }

        /// <summary>
        /// 注销入口（由 StoreEntrancePoint.OnDisable 调用）
        /// </summary>
        public void UnregisterEntrance(StoreEntrancePoint entrance)
        {
            if (entrance == null) return;

            if (entrances.Remove(entrance))
            {
                Debug.Log($"[EntranceManager] 注销入口: {entrance.entranceId}");
            }
        }

        /// <summary>
        /// 获取主入口
        /// </summary>
        public StoreEntrancePoint GetMainEntrance()
        {
            // 查找标记为主入口的入口点
            var mainEntrance = entrances.FirstOrDefault(e => e != null && e.isMainEntrance);

            // 如果没有，返回第一个可用入口
            if (mainEntrance == null && entrances.Count > 0)
            {
                mainEntrance = entrances.FirstOrDefault(e => e != null);
                if (mainEntrance != null)
                {
                    Debug.LogWarning($"[EntranceManager] 没有找到主入口，使用第一个可用入口: {mainEntrance.entranceId}");
                }
            }

            if (mainEntrance == null)
            {
                Debug.LogError("[EntranceManager] 无法获取主入口：没有可用的入口点！");
            }

            return mainEntrance;
        }

        /// <summary>
        /// 获取距离指定位置最近的入口
        /// </summary>
        public StoreEntrancePoint GetNearestEntrance(Vector3 position)
        {
            if (entrances.Count == 0)
            {
                Debug.LogError("[EntranceManager] 无法获取最近入口：没有可用的入口点！");
                return null;
            }

            StoreEntrancePoint nearest = null;
            float minDistance = float.MaxValue;

            foreach (var entrance in entrances)
            {
                if (entrance == null || entrance.outsideAnchor == null) continue;

                float distance = Vector3.Distance(position, entrance.outsideAnchor.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = entrance;
                }
            }

            if (nearest == null)
            {
                Debug.LogWarning("[EntranceManager] 找不到有效入口，返回主入口");
                return GetMainEntrance();
            }

            return nearest;
        }

        /// <summary>
        /// 根据 ID 获取入口
        /// </summary>
        public StoreEntrancePoint GetEntranceById(string entranceId)
        {
            return entrances.FirstOrDefault(e => e != null && e.entranceId == entranceId);
        }

        /// <summary>
        /// 获取所有入口点
        /// </summary>
        public List<StoreEntrancePoint> GetAllEntrances()
        {
            return entrances.Where(e => e != null).ToList();
        }

        /// <summary>
        /// 获取外部锚点（用于顾客入店前移动）
        /// </summary>
        public Transform GetOutsideAnchor(string entranceId = null)
        {
            var entrance = string.IsNullOrEmpty(entranceId)
                ? GetMainEntrance()
                : GetEntranceById(entranceId);

            return entrance?.outsideAnchor;
        }

        /// <summary>
        /// 获取内部锚点（用于顾客离店前移动）
        /// </summary>
        public Transform GetInsideAnchor(string entranceId = null)
        {
            var entrance = string.IsNullOrEmpty(entranceId)
                ? GetMainEntrance()
                : GetEntranceById(entranceId);

            return entrance?.insideAnchor;
        }
    }
}
