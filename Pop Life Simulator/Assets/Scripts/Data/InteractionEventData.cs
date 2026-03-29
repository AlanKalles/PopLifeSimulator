using System;
using UnityEngine;

namespace PopLife.Data
{
    [CreateAssetMenu(menuName = "PopLife/InteractionEvents/InteractionEventData")]
    public class InteractionEventData : ScriptableObject
    {
        [HideInInspector] public string eventId; // OnValidate 自动同步为文件名

        [Header("过滤条件")]
        [Tooltip("是否要求特定商品类别")]
        [SerializeField] private bool useFilterCategory = false;

        [Tooltip("要求的商品类别（启用 useFilterCategory 时有效）")]
        [SerializeField] private ProductCategory requiredCategory;

        [Tooltip("是否要求特定货架")]
        [SerializeField] private bool useFilterShelf = false;

        [Tooltip("要求的货架原型列表（启用 useFilterShelf 时有效，匹配任一即可）")]
        [SerializeField] private ShelfArchetype[] requiredShelves;

        [Header("对话配置")]
        [Tooltip("Pixel Crushers Dialogue System 对话标题")]
        [SerializeField] private string conversationTitle;

        [Header("奖励（回答正确时发放）")]
        [SerializeField] private QuestReward[] rewards;

        [Header("交互配置")]
        [Tooltip("气泡显示超时时间（秒），超时自动消失")]
        [SerializeField] private float bubbleTimeout = 10f;

        // ── 公共只读属性 ──
        public string DisplayName => name;
        public bool UseFilterCategory => useFilterCategory;
        public ProductCategory RequiredCategory => requiredCategory;
        public bool UseFilterShelf => useFilterShelf;
        public ShelfArchetype[] RequiredShelves => requiredShelves;
        public string ConversationTitle => conversationTitle;
        public QuestReward[] Rewards => rewards;
        public float BubbleTimeout => bubbleTimeout;

#if UNITY_EDITOR
        private void OnValidate()
        {
            eventId = name;
        }
#endif
    }
}
