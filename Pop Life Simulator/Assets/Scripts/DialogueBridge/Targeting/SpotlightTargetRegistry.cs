using System;
using System.Collections.Generic;
using System.Linq;

namespace PopLife.DialogueBridge.UI
{
    /// <summary>
    /// 全局 SpotlightTarget 注册表。
    ///
    /// 多实例安全：同 ID 允许多个 SpotlightTarget 同时注册（运行时 prefab 列表常见）。
    /// 查找规则：按 priority 降序，priority 相同时取最后注册的（栈顶语义）。
    /// </summary>
    public static class SpotlightTargetRegistry
    {
        // ID -> 实例列表（按注册顺序追加，不去重）
        private static readonly Dictionary<string, List<SpotlightTarget>> map
            = new Dictionary<string, List<SpotlightTarget>>();

        public static void Register(SpotlightTarget target)
        {
            if (target == null || string.IsNullOrEmpty(target.Id)) return;

            if (!map.TryGetValue(target.Id, out var list))
            {
                list = new List<SpotlightTarget>();
                map[target.Id] = list;
            }
            // 防止重复注册同一实例（OnEnable 多次调用等边界）
            if (!list.Contains(target)) list.Add(target);
        }

        public static void Unregister(SpotlightTarget target)
        {
            if (target == null || string.IsNullOrEmpty(target.Id)) return;
            if (!map.TryGetValue(target.Id, out var list)) return;

            list.Remove(target);
            if (list.Count == 0) map.Remove(target.Id);
        }

        /// <summary>
        /// 按 priority 降序查找最佳匹配；priority 相同时取最后注册的（栈顶）。
        /// 找不到返回 null。
        /// </summary>
        public static SpotlightTarget Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (!map.TryGetValue(id, out var list) || list.Count == 0) return null;

            int bestIndex = 0;
            for (int i = 1; i < list.Count; i++)
            {
                var cur = list[i];
                var best = list[bestIndex];
                if (cur == null) continue;
                // priority 大优先；相同时取后注册（更新的实例）
                if (best == null
                    || cur.Priority > best.Priority
                    || (cur.Priority == best.Priority && i > bestIndex))
                {
                    bestIndex = i;
                }
            }
            return list[bestIndex];
        }

        /// <summary>
        /// 返回该 ID 下所有实例的只读快照（拷贝），避免外部改 registry 内部 list。
        /// 业务可据此自行选择实例（例如"第一个未完成的任务"）。
        /// </summary>
        public static IReadOnlyList<SpotlightTarget> FindAll(string id)
        {
            if (string.IsNullOrEmpty(id)) return Array.Empty<SpotlightTarget>();
            return map.TryGetValue(id, out var list)
                ? list.ToArray()
                : Array.Empty<SpotlightTarget>();
        }

        /// <summary>
        /// 仅供测试/调试，清空所有注册项。
        /// </summary>
        public static void ClearAll() => map.Clear();
    }
}
