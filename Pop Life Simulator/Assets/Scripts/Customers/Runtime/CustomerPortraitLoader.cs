using System.Collections.Generic;
using UnityEngine;
using PopLife.Customers.Data;

namespace PopLife.Customers.Runtime
{
    /// <summary>
    /// 静态工具类：从 Resources/CustomerPortraits/ 加载顾客静态肖像
    /// 文件命名约定: 以顾客ID开头，如 "C001_Lucy.png"、"C002_Mei.png"
    /// 通过 LoadAll 加载整个文件夹，按 ID 前缀匹配
    /// 找不到时回退到 CustomerArchetype.portrait
    /// </summary>
    public static class CustomerPortraitLoader
    {
        private static readonly Dictionary<string, Sprite> cache = new();
        private static Dictionary<string, Sprite> folderIndex;

        /// <summary>
        /// 加载顾客静态肖像
        /// 优先从 Resources/CustomerPortraits/ 按ID前缀匹配，
        /// 找不到时回退到 archetype.portrait
        /// </summary>
        /// <param name="customerId">顾客ID，如 "C001"</param>
        /// <param name="archetype">可选的 CustomerArchetype，用于回退</param>
        /// <returns>肖像 Sprite，找不到时返回 null</returns>
        public static Sprite LoadPortrait(string customerId, CustomerArchetype archetype = null)
        {
            if (string.IsNullOrEmpty(customerId))
                return archetype != null ? archetype.portrait : null;

            if (cache.TryGetValue(customerId, out var cached))
                return cached;

            // 首次调用时构建文件夹索引
            if (folderIndex == null)
                BuildFolderIndex();

            // 按 customerId 查找
            Sprite portrait = null;
            if (folderIndex != null)
                folderIndex.TryGetValue(customerId.ToUpperInvariant(), out portrait);

            // 回退到 archetype.portrait
            if (portrait == null && archetype != null)
                portrait = archetype.portrait;

            if (portrait != null)
                cache[customerId] = portrait;

            return portrait;
        }

        /// <summary>
        /// 加载 Resources/CustomerPortraits/ 下所有 Sprite，
        /// 按文件名前缀（前4个字符，如 "C001"）建立索引
        /// </summary>
        private static void BuildFolderIndex()
        {
            folderIndex = new Dictionary<string, Sprite>();

            Sprite[] allSprites = Resources.LoadAll<Sprite>("CustomerPortraits");
            if (allSprites == null || allSprites.Length == 0) return;

            foreach (var sprite in allSprites)
            {
                // 文件名格式: "C001_Lucy" 等，取前4个字符作为ID
                string name = sprite.name;
                if (name.Length < 4) continue;

                string id = name.Substring(0, 4).ToUpperInvariant();

                // 只保留第一个匹配的（避免重复）
                if (!folderIndex.ContainsKey(id))
                    folderIndex[id] = sprite;
            }
        }

        /// <summary>
        /// 清空缓存（场景卸载时调用释放内存）
        /// </summary>
        public static void ClearCache()
        {
            cache.Clear();
            folderIndex = null;
        }
    }
}