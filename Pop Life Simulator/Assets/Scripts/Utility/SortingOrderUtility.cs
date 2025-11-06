using UnityEngine;

namespace PopLife.Utility
{
    /// <summary>
    /// 工具类：计算建筑的 sorting order，实现透视关系
    /// 规则：从左到右，右侧建筑遮挡左侧建筑
    /// </summary>
    public static class SortingOrderUtility
    {
        // 基础排序值（确保建筑在背景之上）
        private const int BASE_ORDER = 100;

        // 每个网格X坐标的排序增量（右侧建筑更高）
        private const int X_INCREMENT = 10;

        // 每个网格Y坐标的排序增量（上层建筑更高）
        private const int Y_INCREMENT = 1;

        /// <summary>
        /// 计算建筑的 sorting order
        /// </summary>
        /// <param name="gridPosition">建筑在网格中的位置</param>
        /// <param name="floorId">楼层ID（可选，用于多楼层支持）</param>
        /// <returns>计算出的 sorting order</returns>
        public static int CalculateSortingOrder(Vector2Int gridPosition, int floorId = 0)
        {
            // 公式：BASE_ORDER + (floorId * 10000) + (gridX * X_INCREMENT) + (gridY * Y_INCREMENT)
            // 这样可以确保：
            // 1. 同一楼层中，X坐标越大（越靠右）的建筑，sorting order 越高
            // 2. 同一X坐标中，Y坐标越大（越靠上）的建筑，sorting order 越高
            // 3. 不同楼层之间有明确的层级区分

            int floorOffset = floorId * 10000;
            int xOffset = gridPosition.x * X_INCREMENT;
            int yOffset = gridPosition.y * Y_INCREMENT;

            return BASE_ORDER + floorOffset + xOffset + yOffset;
        }

        /// <summary>
        /// 更新建筑GameObject上所有SpriteRenderer的sorting order
        /// </summary>
        /// <param name="buildingObject">建筑GameObject</param>
        /// <param name="gridPosition">建筑在网格中的位置</param>
        /// <param name="floorId">楼层ID</param>
        public static void UpdateBuildingSortingOrder(GameObject buildingObject, Vector2Int gridPosition, int floorId = 0)
        {
            if (buildingObject == null) return;

            int baseSortingOrder = CalculateSortingOrder(gridPosition, floorId);

            // 获取建筑上所有的SpriteRenderer（包括子对象）
            SpriteRenderer[] renderers = buildingObject.GetComponentsInChildren<SpriteRenderer>(true);

            foreach (var renderer in renderers)
            {
                // 保留原始的相对偏移（如果有多个层级的sprite）
                // 这样可以确保建筑内部的sprite层级关系不变
                int originalOffset = renderer.sortingOrder;

                // 如果原始sortingOrder小于1000，我们认为它是相对偏移值（比如0, 1, 2等）
                // 如果原始sortingOrder >= 1000，我们认为它是绝对值，需要重置
                if (originalOffset >= 1000)
                {
                    originalOffset = 0;
                }

                renderer.sortingOrder = baseSortingOrder + originalOffset;
            }
        }
    }
}
