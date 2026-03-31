using UnityEngine;

namespace PopLife.Test
{
    /// <summary>
    /// 测试用货架运行时组件 (独立测试版)
    /// </summary>
    public class TestShelfInstance : MonoBehaviour
    {
        [Header("实例数据")]
        public TestShelfArchetype archetype;
        public string instanceId;
        public Vector2Int gridPosition;
    }
}
