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
            public string id;
            public Sprite sprite;
        }

        public AppearanceEntry[] appearances;

        // 缓存字典，首次访问时构建
        private Dictionary<string, Sprite> _cache;

        public Sprite Get(string id)
        {
            if (_cache == null)
            {
                _cache = new Dictionary<string, Sprite>();
                foreach (var entry in appearances)
                {
                    if (!string.IsNullOrEmpty(entry.id) && entry.sprite != null)
                    {
                        _cache[entry.id] = entry.sprite;
                    }
                }
            }
            return _cache.TryGetValue(id, out var sprite) ? sprite : null;
        }

        // 编辑器辅助：清空缓存（当数组被修改时）
        private void OnValidate()
        {
            _cache = null;
        }
    }
}
