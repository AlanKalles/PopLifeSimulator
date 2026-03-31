using UnityEngine;

namespace PopLife.Test
{
    /// <summary>
    /// 地板瓦片运行时组件 (独立测试版)
    /// </summary>
    public class FloorTileInstance : MonoBehaviour
    {
        [Header("实例数据")]
        public FloorTileArchetype archetype;
        public string instanceId;
        public Vector2Int gridPosition;
        public int rotation;

        [Header("标记")]
        public bool isDefault; // 默认一楼，不可移动/拆除
    }
}
