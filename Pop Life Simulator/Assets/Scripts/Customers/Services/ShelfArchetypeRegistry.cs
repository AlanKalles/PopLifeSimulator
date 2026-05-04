using System.Collections.Generic;
using UnityEngine;
using PopLife.Data;

namespace PopLife.Customers.Services
{
    /// <summary>
    /// ShelfArchetype 注册表：解决 Resources.Load 不支持子目录路径的问题。
    /// Resources.Load 必须传入完整的子路径（如 "Shelf/Wellness/Latex Condoms"），
    /// 而 customer favorite 等业务侧只持有 archetypeId（=文件名），不知道子目录。
    /// 本类用 LoadAll 一次性递归加载所有 ShelfArchetype，按 archetypeId 建索引。
    ///
    /// 自动过滤 isImplemented=false 的 SO（如 BuildingsNotImplemented/ 下的占位符）。
    /// 不依赖 BuildingsNotImplemented 文件夹必然存在——文件夹删除/缺失也安全。
    /// </summary>
    public static class ShelfArchetypeRegistry
    {
        private const string ResourcesPath = "ScriptableObjects/BuildingArchetype/Shelf";

        private static Dictionary<string, ShelfArchetype> _byId;

        /// <summary>按 archetypeId 查找 ShelfArchetype，找不到返回 null。</summary>
        public static ShelfArchetype GetByArchetypeId(string archetypeId)
        {
            if (string.IsNullOrEmpty(archetypeId)) return null;
            EnsureLoaded();
            return _byId.TryGetValue(archetypeId, out var so) ? so : null;
        }

        /// <summary>清缓存——在 Editor reload 或场景重建后强制重新加载用。</summary>
        public static void Invalidate()
        {
            _byId = null;
        }

        private static void EnsureLoaded()
        {
            if (_byId != null) return;

            _byId = new Dictionary<string, ShelfArchetype>();

            // LoadAll 递归加载子目录下所有 ShelfArchetype
            var all = Resources.LoadAll<ShelfArchetype>(ResourcesPath);
            if (all == null || all.Length == 0)
            {
                Debug.LogWarning($"[ShelfArchetypeRegistry] No ShelfArchetype found at '{ResourcesPath}'");
                return;
            }

            int skippedNotImplemented = 0;
            int skippedDuplicates = 0;
            foreach (var so in all)
            {
                if (so == null) continue;

                // 过滤未实装的占位符 SO（约定：BuildingsNotImplemented/ 下设为 false）
                if (!so.isImplemented)
                {
                    skippedNotImplemented++;
                    continue;
                }

                if (string.IsNullOrEmpty(so.archetypeId))
                {
                    Debug.LogWarning($"[ShelfArchetypeRegistry] '{so.name}' has empty archetypeId, skipped");
                    continue;
                }

                if (_byId.ContainsKey(so.archetypeId))
                {
                    Debug.LogWarning($"[ShelfArchetypeRegistry] Duplicate archetypeId '{so.archetypeId}' in {so.name}, ignored");
                    skippedDuplicates++;
                    continue;
                }

                _byId[so.archetypeId] = so;
            }

            Debug.Log($"[ShelfArchetypeRegistry] Loaded {_byId.Count} shelves (skipped {skippedNotImplemented} notImpl, {skippedDuplicates} dup) from '{ResourcesPath}'");
        }
    }
}
