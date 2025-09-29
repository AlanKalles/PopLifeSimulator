using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PopLife.Runtime
{
    [System.Serializable]
    public class FloorEntry
    {
        [Tooltip("楼层引用")]
        public FloorGrid floor;

        [Tooltip("是否激活该楼层")]
        public bool isActive = true;

        [Tooltip("楼层ID（自动分配）")]
        [ReadOnly] public int floorId;

        // 自定义属性特性，使字段在Inspector中只读
        public class ReadOnlyAttribute : PropertyAttribute { }
    }

    public class FloorManager : MonoBehaviour
    {
        [Header("楼层配置")]
        [SerializeField] private List<FloorEntry> floors = new List<FloorEntry>();

        [Header("当前焦点楼层")]
        [SerializeField] private int currentFloorIndex = 0;

        [Header("调试")]
        [SerializeField] private bool autoAssignFloorIds = true;
        [SerializeField] private bool hideInactiveFloors = false;

        // 缓存活跃楼层列表
        private List<FloorGrid> cachedActiveFloors = new List<FloorGrid>();
        private bool cacheValid = false;

        private void Awake()
        {
            ValidateAndInitialize();
        }

        private void Start()
        {
            ApplyFloorActivation();
        }

        // 获取当前主楼层（用于主要交互）
        public FloorGrid GetActiveFloor()
        {
            var activeFloors = GetAllActiveFloors();
            if (activeFloors.Count == 0) return null;

            currentFloorIndex = Mathf.Clamp(currentFloorIndex, 0, activeFloors.Count - 1);
            return activeFloors[currentFloorIndex];
        }

        // 获取所有激活的楼层
        public List<FloorGrid> GetAllActiveFloors()
        {
            if (!cacheValid)
            {
                RefreshActiveFloorsCache();
            }
            return new List<FloorGrid>(cachedActiveFloors);
        }

        // 根据ID获取楼层（无论是否激活）
        public FloorGrid GetFloor(int id)
        {
            var entry = floors.FirstOrDefault(f => f != null && f.floor != null && f.floorId == id);
            return entry?.floor;
        }

        // 根据ID获取激活的楼层
        public FloorGrid GetActiveFloor(int id)
        {
            var entry = floors.FirstOrDefault(f => f != null && f.floor != null && f.floorId == id && f.isActive);
            return entry?.floor;
        }

        // 设置楼层激活状态
        public void SetFloorActive(int floorId, bool active)
        {
            var entry = floors.FirstOrDefault(f => f != null && f.floorId == floorId);
            if (entry != null)
            {
                entry.isActive = active;
                ApplyFloorActivation();
                InvalidateCache();
            }
        }

        // 通过引用设置楼层激活状态
        public void SetFloorActive(FloorGrid floor, bool active)
        {
            var entry = floors.FirstOrDefault(f => f != null && f.floor == floor);
            if (entry != null)
            {
                entry.isActive = active;
                ApplyFloorActivation();
                InvalidateCache();
            }
        }

        // 切换楼层激活状态
        public void ToggleFloorActive(int floorId)
        {
            var entry = floors.FirstOrDefault(f => f != null && f.floorId == floorId);
            if (entry != null)
            {
                entry.isActive = !entry.isActive;
                ApplyFloorActivation();
                InvalidateCache();
            }
        }

        // 设置当前焦点楼层
        public void SetActive(FloorGrid floor)
        {
            var activeFloors = GetAllActiveFloors();
            int index = activeFloors.IndexOf(floor);
            if (index >= 0)
            {
                currentFloorIndex = index;
            }
        }

        // 激活所有楼层
        public void ActivateAllFloors()
        {
            foreach (var entry in floors)
            {
                if (entry != null)
                {
                    entry.isActive = true;
                }
            }
            ApplyFloorActivation();
            InvalidateCache();
        }

        // 仅激活指定楼层
        public void ActivateOnlyFloor(int floorId)
        {
            foreach (var entry in floors)
            {
                if (entry != null)
                {
                    entry.isActive = (entry.floorId == floorId);
                }
            }
            ApplyFloorActivation();
            InvalidateCache();
        }

        // 获取楼层信息
        public int GetTotalFloorCount() => floors.Count;
        public int GetActiveFloorCount() => GetAllActiveFloors().Count;

        // 获取楼层的激活状态
        public bool IsFloorActive(int floorId)
        {
            var entry = floors.FirstOrDefault(f => f != null && f.floorId == floorId);
            return entry?.isActive ?? false;
        }

        // 获取所有楼层条目（供编辑器或调试使用）
        public List<FloorEntry> GetAllFloorEntries() => new List<FloorEntry>(floors);

        // 私有方法
        private void ValidateAndInitialize()
        {
            // 移除空条目
            floors.RemoveAll(f => f == null || f.floor == null);

            // 自动分配楼层ID
            if (autoAssignFloorIds)
            {
                AssignFloorIds();
            }

            // 同步楼层GameObject的ID
            foreach (var entry in floors)
            {
                if (entry.floor != null)
                {
                    entry.floor.floorId = entry.floorId;
                }
            }
        }

        private void AssignFloorIds()
        {
            // 保留已有的ID，为新楼层分配未使用的ID
            HashSet<int> usedIds = new HashSet<int>();

            // 收集已使用的ID
            foreach (var entry in floors)
            {
                if (entry.floorId >= 0)
                {
                    usedIds.Add(entry.floorId);
                }
            }

            // 为没有ID的楼层分配新ID
            int nextId = 0;
            foreach (var entry in floors)
            {
                if (entry.floorId < 0)
                {
                    while (usedIds.Contains(nextId))
                    {
                        nextId++;
                    }
                    entry.floorId = nextId;
                    usedIds.Add(nextId);
                    nextId++;
                }
            }
        }

        private void ApplyFloorActivation()
        {
            if (!Application.isPlaying) return;

            foreach (var entry in floors)
            {
                if (entry != null && entry.floor != null)
                {
                    entry.floor.gameObject.SetActive(entry.isActive && !hideInactiveFloors);

                    // 如果楼层激活且之前未初始化，则初始化
                    if (entry.isActive && entry.floor.enabled)
                    {
                        entry.floor.Init();
                    }
                }
            }
        }

        private void RefreshActiveFloorsCache()
        {
            cachedActiveFloors.Clear();
            foreach (var entry in floors)
            {
                if (entry != null && entry.floor != null && entry.isActive)
                {
                    cachedActiveFloors.Add(entry.floor);
                }
            }
            cacheValid = true;
        }

        private void InvalidateCache()
        {
            cacheValid = false;
        }

        // Unity编辑器
        #if UNITY_EDITOR
        private void OnValidate()
        {
            // 在编辑器中验证数据
            if (floors != null)
            {
                // 移除空条目
                floors.RemoveAll(f => f == null);

                // 确保每个条目都有floor引用
                foreach (var entry in floors)
                {
                    if (entry != null && entry.floor == null)
                    {
                        Debug.LogWarning($"FloorManager: 楼层条目缺少FloorGrid引用");
                    }
                }

                // 自动分配ID
                if (autoAssignFloorIds)
                {
                    AssignFloorIds();
                }
            }

            currentFloorIndex = Mathf.Max(0, currentFloorIndex);
        }

        [ContextMenu("刷新楼层ID")]
        private void RefreshFloorIds()
        {
            AssignFloorIds();

            // 同步到FloorGrid
            foreach (var entry in floors)
            {
                if (entry != null && entry.floor != null)
                {
                    entry.floor.floorId = entry.floorId;
                }
            }

            Debug.Log($"已刷新 {floors.Count} 个楼层的ID");
        }

        [ContextMenu("激活所有楼层")]
        private void ActivateAll()
        {
            ActivateAllFloors();
            Debug.Log("已激活所有楼层");
        }

        [ContextMenu("检查楼层状态")]
        private void CheckFloorStatus()
        {
            Debug.Log($"总楼层数: {GetTotalFloorCount()}");
            Debug.Log($"激活楼层数: {GetActiveFloorCount()}");

            foreach (var entry in floors)
            {
                if (entry != null)
                {
                    string status = entry.isActive ? "激活" : "未激活";
                    string name = entry.floor != null ? entry.floor.name : "空引用";
                    Debug.Log($"楼层 ID {entry.floorId}: {name} - {status}");
                }
            }
        }
        #endif
    }
}