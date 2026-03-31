using System.Collections.Generic;
using UnityEngine;

namespace PopLife.Test
{
    /// <summary>
    /// 测试用货架数据 (独立测试版)
    /// </summary>
    [CreateAssetMenu(menuName = "Test/TestShelfArchetype")]
    public class TestShelfArchetype : ScriptableObject
    {
        [Header("基础信息")]
        public string id;
        public List<Vector2Int> footprint = new() { new Vector2Int(0, 0) };
        public GameObject prefab;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(id))
                id = name;
            if (footprint == null || footprint.Count == 0)
                footprint = new List<Vector2Int> { new Vector2Int(0, 0) };
        }
#endif
    }
}
