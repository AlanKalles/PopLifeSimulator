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

        // 楼层ID直接从 FloorGrid 读取，不再存储在这里
        public int FloorId => floor != null ? floor.floorId : -1;
    }

    public class FloorManager : MonoBehaviour
    {
        [Header("楼层配置")]
        [SerializeField] private List<FloorEntry> floors = new List<FloorEntry>();

        [Header("当前焦点楼层")]
        [SerializeField] private int currentFloorIndex = 0;

        // === 事件系统 ===
        /// <summary>
        /// 当前激活楼层发生变化时触发
        /// </summary>
        public event System.Action<FloorGrid> OnActiveFloorChanged;
        
        [Header("编辑器")]
        [SerializeField] private bool autoAssignFloorIds = false; // 启用后自动分配唯一楼层ID

        [Header("调试")]
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
            var entry = floors.FirstOrDefault(f => f != null && f.floor != null && f.floor.floorId == id);
            return entry?.floor;
        }

        // 根据ID获取激活的楼层
        public FloorGrid GetActiveFloor(int id)
        {
            var entry = floors.FirstOrDefault(f => f != null && f.floor != null && f.floor.floorId == id && f.isActive);
            return entry?.floor;
        }

        // 设置楼层激活状态
        public void SetFloorActive(int floorId, bool active)
        {
            var entry = floors.FirstOrDefault(f => f != null && f.floor != null && f.floor.floorId == floorId);
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
            var entry = floors.FirstOrDefault(f => f != null && f.floor != null && f.floor.floorId == floorId);
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
                if (entry != null && entry.floor != null)
                {
                    entry.isActive = (entry.floor.floorId == floorId);
                }
            }
            ApplyFloorActivation();
            InvalidateCache();
        }

        // 获取楼层信息
        public int GetTotalFloorCount() => floors.Count;
        public int GetActiveFloorCount() => GetAllActiveFloors().Count;

        // 获取所有激活楼层的货架总数
        public int GetTotalShelfCount()
        {
            int count = 0;
            var activeFloors = GetAllActiveFloors();
            foreach (var floor in activeFloors)
            {
                foreach (var _ in floor.AllShelves())
                {
                    count++;
                }
            }
            return count;
        }

        // 获取所有激活楼层的建筑总数
        public int GetTotalBuildingCount()
        {
            int count = 0;
            var activeFloors = GetAllActiveFloors();
            foreach (var floor in activeFloors)
            {
                foreach (var _ in floor.AllBuildings())
                {
                    count++;
                }
            }
            return count;
        }

        // 获取楼层的激活状态
        public bool IsFloorActive(int floorId)
        {
            var entry = floors.FirstOrDefault(f => f != null && f.floor != null && f.floor.floorId == floorId);
            return entry?.isActive ?? false;
        }

        // 获取所有楼层条目（供编辑器或调试使用）
        public List<FloorEntry> GetAllFloorEntries() => new List<FloorEntry>(floors);

        // 私有方法
        private void ValidateAndInitialize()
        {
            // 移除空条目
            floors.RemoveAll(f => f == null || f.floor == null);
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
                        if (!entry.floor.HasAnyRegisteredBuildings()) { entry.floor.RebuildFromScene(); }
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

        // 清理旧序列化残留并校验数据一致性
        private void CleanAndValidate(bool explicitInvoke)
        {
            if (floors == null) floors = new List<FloorEntry>();

            bool changed = false;

            // 1) 移除空条目
            int before = floors.Count;
            floors.RemoveAll(f => f == null);
            if (floors.Count != before) changed = true;

            // 2) 重建列表元素以丢弃旧的序列化字段（如旧的 floorId）并去重 FloorGrid 引用
            var seen = new HashSet<FloorGrid>();
            var rebuilt = new List<FloorEntry>(floors.Count);
            foreach (var e in floors)
            {
                if (e == null)
                    continue;

                if (e.floor == null)
                {
                    Debug.LogWarning("FloorManager: 楼层条目缺少 FloorGrid 引用");
                    rebuilt.Add(new FloorEntry { floor = null, isActive = e.isActive });
                    changed = true;
                    continue;
                }

                if (!seen.Add(e.floor))
                {
                    Debug.LogWarning($"FloorManager: 检测到重复引用楼层 '{e.floor.name}'，已去重");
                    changed = true;
                    continue;
                }

                // 重建元素实例，丢弃可能存在的旧序列化字段（如旧的 floorId）
                rebuilt.Add(new FloorEntry { floor = e.floor, isActive = e.isActive });
            }

            if (rebuilt.Count != floors.Count) changed = true;
            floors = rebuilt;

            // 3) 校验楼层ID唯一性与有效性（>0），必要时按配置自动分配
            EnsureUniqueFloorIds();

            // 4) 索引校正
            currentFloorIndex = Mathf.Max(0, currentFloorIndex);

#if UNITY_EDITOR
            if (changed || explicitInvoke)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                foreach (var e in floors)
                {
                    if (e != null && e.floor != null)
                        UnityEditor.EditorUtility.SetDirty(e.floor);
                }
            }
#endif
        }

        private void EnsureUniqueFloorIds()
        {
            var idToFloors = new Dictionary<int, List<FloorGrid>>();
            var order = new List<FloorGrid>();

            foreach (var e in floors)
            {
                if (e == null || e.floor == null) continue;
                order.Add(e.floor);
                int id = e.floor.floorId;
                if (!idToFloors.TryGetValue(id, out var list))
                {
                    list = new List<FloorGrid>();
                    idToFloors[id] = list;
                }
                list.Add(e.floor);
            }

            bool hasProblem = false;
            foreach (var kv in idToFloors)
            {
                if (kv.Key <= 0)
                {
                    hasProblem = true;
                }
                if (kv.Value.Count > 1)
                {
                    hasProblem = true;
                }
            }

            if (!hasProblem) return;

            if (autoAssignFloorIds)
            {
                // 依照 floors 列表顺序分配 1..N 的唯一正整数ID
                var used = new HashSet<int>();
                int next = 1;
                foreach (var fg in order)
                {
                    while (used.Contains(next)) next++;
                    fg.floorId = next;
                    used.Add(next);
                    next++;
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(fg);
#endif
                }
                Debug.Log("FloorManager: 已自动分配唯一的楼层ID");
            }
            else
            {
                // 仅提示问题，方便用户手动修复
                foreach (var kv in idToFloors)
                {
                    if (kv.Key <= 0)
                    {
                        foreach (var fg in kv.Value)
                        {
                            Debug.LogWarning($"FloorManager: FloorGrid '{fg.name}' 的 floorId = {kv.Key}，请设置为正且唯一的ID，或启用 autoAssignFloorIds 自动分配。");
                        }
                    }
                    if (kv.Value.Count > 1)
                    {
                        var names = string.Join(", ", kv.Value.Select(f => f.name));
                        Debug.LogWarning($"FloorManager: 检测到重复的 floorId = {kv.Key}，涉及楼层: {names}。请调整或启用 autoAssignFloorIds。");
                    }
                }
            }
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
            }

            currentFloorIndex = Mathf.Max(0, currentFloorIndex);
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
                    int floorId = entry.floor != null ? entry.floor.floorId : -1;
                    Debug.Log($"楼层 ID {floorId}: {name} - {status}");
                }
            }
        }
        [ContextMenu("清理楼层序列化残留并校验")]
        private void CleanupAndValidateMenu()
        {
            CleanAndValidate(true);
            Debug.Log("已清理旧的序列化残留并完成校验");
        }
        #endif

        // === 程序化楼层切换接口（用于鼠标自动检测系统） ===

        /// <summary>
        /// 程序化切换到指定楼层（由检测系统调用）
        /// </summary>
        /// <param name="targetFloor">目标楼层，null表示取消激活</param>
        public void SetActiveFloorProgrammatic(FloorGrid targetFloor)
        {
            // 获取当前激活的楼层
            FloorGrid currentActiveFloor = GetActiveFloor();

            // 避免重复切换
            if (targetFloor == currentActiveFloor)
            {
                return;
            }

            // 停用旧楼层的视觉高亮
            if (currentActiveFloor != null)
            {
                SetFloorHighlight(currentActiveFloor, false);
            }

            // 激活新楼层的视觉高亮
            if (targetFloor != null)
            {
                SetFloorHighlight(targetFloor, true);
            }

            // 触发事件
            OnActiveFloorChanged?.Invoke(targetFloor);
        }

        /// <summary>
        /// 设置楼层的视觉高亮状态
        /// </summary>
        /// <param name="floor">目标楼层</param>
        /// <param name="enabled">是否启用高亮</param>
        private void SetFloorHighlight(FloorGrid floor, bool enabled)
        {
            if (floor == null) return;

            // 使用FloorGrid内置的isSelected字段
            floor.isSelected = enabled;

            // FloorGrid的OnDrawGizmos会自动根据isSelected字段切换颜色
            // 参见FloorGrid.cs:409-418
        }
    }
}
