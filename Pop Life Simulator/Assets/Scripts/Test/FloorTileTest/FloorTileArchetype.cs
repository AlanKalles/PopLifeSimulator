using System.Collections.Generic;
using UnityEngine;

namespace PopLife.Test
{
    /// <summary>
    /// 地板瓦片数据 (独立测试版，不继承 BuildingArchetype)
    /// </summary>
    [CreateAssetMenu(menuName = "Test/FloorTileArchetype")]
    public class FloorTileArchetype : ScriptableObject
    {
        [Header("基础信息")]
        public string id;
        public Vector2Int tileSize = new(3, 3);
        public GameObject prefab;
        public int buildCost = 100;
        public bool canRotate = true;

        /// <summary>
        /// 返回矩形 footprint（根据旋转状态）
        /// rotation: 0 = 原始, 1 = 90度（宽高互换）
        /// </summary>
        public List<Vector2Int> GetFootprint(int rotation)
        {
            var size = GetEffectiveSize(rotation);
            var result = new List<Vector2Int>(size.x * size.y);
            for (int x = 0; x < size.x; x++)
                for (int y = 0; y < size.y; y++)
                    result.Add(new Vector2Int(x, y));
            return result;
        }

        /// <summary>
        /// 获取考虑旋转后的有效尺寸
        /// </summary>
        public Vector2Int GetEffectiveSize(int rotation)
        {
            if (canRotate && rotation % 2 == 1)
                return new Vector2Int(tileSize.y, tileSize.x);
            return tileSize;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(id))
                id = name;
        }
#endif
    }
}
