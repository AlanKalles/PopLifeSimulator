using System.Collections.Generic;
using UnityEngine;

namespace PopLife.Customers.Runtime
{
    /// <summary>
    /// 部件索引枚举，与 sprite sheet 切片顺序对应
    /// </summary>
    public enum PartIndex
    {
        Head = 0,
        LeftFoot = 1,
        RightFoot = 2,
        Body = 3,
        LeftArm = 4,
        RightArm = 5
    }

    /// <summary>
    /// 静态工具类：从 sprite sheet 加载6部件精灵
    /// 资源路径约定: Resources/CustomerParts/{customerId}_sheet
    /// 例如 customerID "C006" → 路径 "CustomerParts/c006_sheet"
    /// </summary>
    public static class CustomerPartLoader
    {
        private static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();

        /// <summary>
        /// 加载指定顾客的6个部件精灵
        /// </summary>
        /// <param name="customerId">顾客ID，如 "C006"</param>
        /// <returns>按索引排序的6个精灵数组，找不到时返回 null</returns>
        public static Sprite[] LoadParts(string customerId)
        {
            if (string.IsNullOrEmpty(customerId))
                return null;

            if (cache.TryGetValue(customerId, out var cached))
                return cached;

            // 将 customerID 转小写构造资源路径
            string path = $"CustomerParts/{customerId.ToLower()}_sheet";
            Sprite[] sprites = Resources.LoadAll<Sprite>(path);

            if (sprites == null || sprites.Length < 6)
            {
                Debug.LogWarning($"[CustomerPartLoader] 找不到部件资源: {path}（需要至少6个切片，实际 {sprites?.Length ?? 0} 个）");
                return null;
            }

            // Resources.LoadAll 不保证顺序，按名称后缀数字排序确保索引一致
            System.Array.Sort(sprites, (a, b) =>
            {
                int idxA = ExtractSuffixIndex(a.name);
                int idxB = ExtractSuffixIndex(b.name);
                return idxA.CompareTo(idxB);
            });

            cache[customerId] = sprites;
            return sprites;
        }

        /// <summary>
        /// 获取指定顾客的单个部件精灵
        /// </summary>
        public static Sprite GetPart(string customerId, PartIndex part)
        {
            Sprite[] parts = LoadParts(customerId);
            if (parts == null) return null;

            int index = (int)part;
            if (index < 0 || index >= parts.Length) return null;

            return parts[index];
        }

        /// <summary>
        /// 清空缓存（场景卸载时调用释放内存）
        /// </summary>
        public static void ClearCache()
        {
            cache.Clear();
        }

        /// <summary>
        /// 从 sprite 名称提取末尾数字索引
        /// 例如 "c006_sheet_3" → 3
        /// </summary>
        private static int ExtractSuffixIndex(string name)
        {
            int lastUnderscore = name.LastIndexOf('_');
            if (lastUnderscore >= 0 && int.TryParse(name.Substring(lastUnderscore + 1), out int idx))
                return idx;
            return 0;
        }
    }
}
