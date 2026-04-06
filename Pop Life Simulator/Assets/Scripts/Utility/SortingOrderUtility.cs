using UnityEngine;

namespace PopLife.Utility
{
    /// <summary>
    /// 工具类：计算建筑的 sorting order，实现透视关系
    /// 基于世界坐标计算，不再依赖网格坐标和楼层ID
    /// </summary>
    public static class SortingOrderUtility
    {
        // 基础排序值（确保建筑在背景之上）
        private const int BASE_ORDER = 100;

        // 世界坐标排序比例因子
        private const float X_SCALE = 10f;
        private const float Y_SCALE = 1f;

        /// <summary>
        /// 根据世界坐标计算 sorting order
        /// X 越大越靠右→排序越高，Y 越大越靠上→排序越高
        /// </summary>
        public static int CalculateSortingOrder(Vector3 worldPosition)
        {
            return BASE_ORDER
                + Mathf.RoundToInt(worldPosition.x * X_SCALE)
                + Mathf.RoundToInt(worldPosition.y * Y_SCALE);
        }

        /// <summary>
        /// 更新建筑 GameObject 上所有 SpriteRenderer 的 sorting order
        /// </summary>
        public static void UpdateBuildingSortingOrder(GameObject buildingObject, Vector3 worldPosition)
        {
            if (buildingObject == null) return;

            int baseSortingOrder = CalculateSortingOrder(worldPosition);

            SpriteRenderer[] renderers = buildingObject.GetComponentsInChildren<SpriteRenderer>(true);

            foreach (var renderer in renderers)
            {
                // 保留原始的相对偏移（子sprite的层级关系）
                int originalOffset = renderer.sortingOrder;

                // 原始sortingOrder < 1000 视为相对偏移，>= 1000 视为绝对值需重置
                if (originalOffset >= 1000)
                {
                    originalOffset = 0;
                }

                renderer.sortingOrder = baseSortingOrder + originalOffset;
            }
        }
    }
}
