using UnityEngine;
using PopLife.Customers.Runtime;

namespace PopLife.Title
{
    /// <summary>
    /// Title 场景顾客装扮 helper
    /// 调用 CustomerPartLoader 加载 sprite sheet 后，按 PartIndex 映射到 6 个 SpriteRenderer
    /// 配合 TitleSceneBootstrap 在运行时随机化外观，或在 prefab/Inspector 预设固定 customerId
    /// </summary>
    public class TitleCustomerAppearance : MonoBehaviour
    {
        [Header("顾客 ID")]
        [Tooltip("Resources/CustomerParts/{customerId}_sheet 的 ID 部分。留空则不加载，使用 prefab 默认 sprites")]
        [SerializeField] private string customerId;

        [Header("Renderer 引用")]
        [SerializeField] private SpriteRenderer headRenderer;
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private SpriteRenderer leftArmRenderer;
        [SerializeField] private SpriteRenderer rightArmRenderer;
        [SerializeField] private SpriteRenderer leftFootRenderer;
        [SerializeField] private SpriteRenderer rightFootRenderer;

        public string CustomerId => customerId;

        private void Awake()
        {
            if (!string.IsNullOrEmpty(customerId))
                ApplyAppearance(customerId);
        }

        /// <summary>
        /// 重新加载并应用指定 customerId 的 sprite 装扮
        /// </summary>
        public void ApplyAppearance(string id)
        {
            customerId = id;
            if (string.IsNullOrEmpty(id)) return;

            Sprite[] sprites = CustomerPartLoader.LoadParts(id);
            if (sprites == null) return;

            Apply(headRenderer, sprites, PartIndex.Head);
            Apply(bodyRenderer, sprites, PartIndex.Body);
            Apply(leftArmRenderer, sprites, PartIndex.LeftArm);
            Apply(rightArmRenderer, sprites, PartIndex.RightArm);
            Apply(leftFootRenderer, sprites, PartIndex.LeftFoot);
            Apply(rightFootRenderer, sprites, PartIndex.RightFoot);
        }

        private static void Apply(SpriteRenderer renderer, Sprite[] sprites, PartIndex part)
        {
            if (renderer == null) return;
            int idx = (int)part;
            if (idx >= 0 && idx < sprites.Length)
                renderer.sprite = sprites[idx];
        }
    }
}
