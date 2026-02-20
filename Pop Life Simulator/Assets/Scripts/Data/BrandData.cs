using UnityEngine;

namespace PopLife.Data
{
    /// <summary>
    /// 品牌数据 ScriptableObject - 每个品牌一个资产文件
    /// </summary>
    [CreateAssetMenu(menuName = "PopLife/Buildings/BrandData")]
    public class BrandData : ScriptableObject
    {
        [Tooltip("品牌显示名称")]
        public string displayName;

        [Tooltip("品牌图标（可选）")]
        public Sprite icon;
    }
}
