using System;
using System.Collections.Generic;
using UnityEngine;

namespace PopLife.Customers.Data
{
    [CreateAssetMenu(fileName = "AppearanceDatabase", menuName = "PopLife/Customer Appearance Database")]
    public class AppearanceDatabase : ScriptableObject
    {
        [Serializable]
        public class AppearanceEntry
        {
            public string id = "";
            public Sprite sprite;
        }

        public AppearanceEntry[] appearances = Array.Empty<AppearanceEntry>();

        // 缓存字典，首次访问时构建
        private Dictionary<string, Sprite> _cache;

        public Sprite Get(string id)
        {
            if (_cache == null)
            {
                _cache = new Dictionary<string, Sprite>();
                if (appearances != null)
                {
                    foreach (var entry in appearances)
                    {
                        if (entry != null && !string.IsNullOrEmpty(entry.id) && entry.sprite != null)
                        {
                            _cache[entry.id] = entry.sprite;
                        }
                    }
                }
            }
            return _cache.TryGetValue(id, out var sprite) ? sprite : null;
        }

        // 编辑器辅助：清空缓存并清理无效条目
        private void OnValidate()
        {
            _cache = null;

            // 清理数组中的 null 条目
            if (appearances != null && appearances.Length > 0)
            {
                var validEntries = new List<AppearanceEntry>();
                foreach (var entry in appearances)
                {
                    if (entry != null)
                    {
                        // 确保 id 不为 null
                        if (entry.id == null)
                            entry.id = "";
                        validEntries.Add(entry);
                    }
                }

                // 如果有 null 条目被移除，更新数组
                if (validEntries.Count != appearances.Length)
                {
                    appearances = validEntries.ToArray();
                    Debug.LogWarning($"[AppearanceDatabase] 已清理 {appearances.Length - validEntries.Count} 个无效条目");
                }
            }
        }
    }
}
