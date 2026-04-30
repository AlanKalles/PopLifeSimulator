using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using PopLife.Data;

namespace PopLife.UI
{
    /// <summary>
    /// Shelf list item - Square button with icon and cost
    /// Shows tooltip on hover with detailed information
    /// </summary>
    public class ShelfListItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private Button button;
        [SerializeField] private Outline outline; // Border highlight on hover

        [Header("Hover Settings")]
        [SerializeField] private Color normalOutlineColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        [SerializeField] private Color hoverOutlineColor = new Color(1f, 1f, 0f, 1f); // Yellow highlight
        [SerializeField] private float outlineWidth = 3f;

        [Header("Disabled Settings")]
        [SerializeField] private Color disabledIconColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        [SerializeField] private Color disabledCostColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

        private ShelfArchetype shelf;
        private System.Action<ShelfArchetype> onSelectCallback;
        private ShelfTooltip tooltip;

        // 标记当前 item 是否处于悬停状态。OnPointerExit 不一定能在所有路径上触发
        // （按钮 mid-hover 变 disabled、GameObject 被 SetActive(false)、被销毁等），
        // 这里作为兜底依据让 OnDisable / OnDestroy 知道是否需要主动收起 tooltip
        private bool isHovered = false;

        private void Awake()
        {
            ApplyIconImageSettings();

            if (button != null)
            {
                button.onClick.AddListener(OnButtonClicked);
            }

            // Setup outline component
            if (outline == null)
            {
                outline = GetComponent<Outline>();
                if (outline == null)
                {
                    outline = gameObject.AddComponent<Outline>();
                }
            }

            outline.effectColor = normalOutlineColor;
            outline.effectDistance = new Vector2(outlineWidth, outlineWidth);
            outline.enabled = false; // Start disabled
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyIconImageSettings();
        }
#endif

        /// <summary>
        /// Initialize shelf list item with shelf data
        /// </summary>
        public void Initialize(ShelfArchetype shelf, System.Action<ShelfArchetype> onSelectCallback, ShelfTooltip tooltip)
        {
            this.shelf = shelf;
            this.onSelectCallback = onSelectCallback;
            this.tooltip = tooltip;

            // Update UI
            if (iconImage != null)
            {
                ApplyIconImageSettings();
                Sprite iconSprite = GetShelfPrefabSprite(shelf) ?? shelf.icon;
                iconImage.sprite = iconSprite;
                iconImage.enabled = iconSprite != null;
            }

            UpdateCostDisplay();
        }

        /// <summary>
        /// Update cost text with affordability check
        /// </summary>
        public void UpdateCostDisplay()
        {
            if (costText == null || shelf == null) return;

            int cost = shelf.buildCost;
            costText.text = $"${cost}";

            // Check affordability
            if (ResourceManager.Instance != null)
            {
                bool canAfford = ResourceManager.Instance.money >= cost;
                costText.color = canAfford ? Color.white : Color.red;
            }
        }

        /// <summary>
        /// Pointer enter event - Show highlight and tooltip
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            // 已放置的货架按钮不显示高亮和tooltip
            if (button != null && !button.interactable) return;

            isHovered = true;

            // Enable outline highlight
            if (outline != null)
            {
                outline.enabled = true;
                outline.effectColor = hoverOutlineColor;
            }

            // Show tooltip at mouse position
            if (tooltip != null && shelf != null)
            {
                tooltip.Show(shelf, eventData.position);
            }
        }

        /// <summary>
        /// Pointer move event - Update tooltip position to follow cursor
        /// </summary>
        public void OnPointerMove(PointerEventData eventData)
        {
            if (button != null && !button.interactable) return;

            if (tooltip != null && shelf != null)
            {
                tooltip.UpdatePosition(eventData.position);
            }
        }

        /// <summary>
        /// Pointer exit event - Hide highlight and tooltip
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            // 注意：这里不再检查 button.interactable。
            // 如果在悬停过程中按钮变成 disabled（例如刚放置完该 shelf 触发 RefreshItemDisplays
            // → SetPlacementDisabled(true)），早退会导致 tooltip 永远拿不到 Hide 信号。
            isHovered = false;

            // Disable outline highlight
            if (outline != null)
            {
                outline.enabled = false;
            }

            // Hide tooltip
            if (tooltip != null)
            {
                tooltip.Hide();
            }
        }

        private void OnButtonClicked()
        {
            if (shelf != null)
            {
                onSelectCallback?.Invoke(shelf);
            }
        }

        /// <summary>
        /// Get the shelf associated with this item
        /// </summary>
        public ShelfArchetype GetShelf()
        {
            return shelf;
        }

        private static Sprite GetShelfPrefabSprite(ShelfArchetype shelf)
        {
            if (shelf == null || shelf.prefab == null) return null;

            var spriteRenderer = shelf.prefab.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = shelf.prefab.GetComponentInChildren<SpriteRenderer>();
            }

            return spriteRenderer != null ? spriteRenderer.sprite : null;
        }

        private void ApplyIconImageSettings()
        {
            if (iconImage == null) return;

            iconImage.preserveAspect = true;
        }

        /// <summary>
        /// 根据是否已放置设置按钮禁用状态，同时调整图标和价格文字颜色
        /// </summary>
        public void SetPlacementDisabled(bool alreadyPlaced)
        {
            if (button != null)
                button.interactable = !alreadyPlaced;

            if (iconImage != null)
                iconImage.color = alreadyPlaced ? disabledIconColor : Color.white;

            // 禁用时覆盖价格颜色；启用时由 UpdateCostDisplay 控制
            if (alreadyPlaced && costText != null)
                costText.color = disabledCostColor;
        }

        /// <summary>
        /// item 被 SetActive(false) 关闭（例如 ApplyFilters 过滤掉，使被悬停的 item
        /// 不会再触发 OnPointerExit）时主动收起 tooltip
        /// </summary>
        private void OnDisable()
        {
            if (!isHovered) return;

            isHovered = false;
            if (outline != null) outline.enabled = false;
            if (tooltip != null) tooltip.HideImmediate();
        }

        /// <summary>
        /// Cleanup when destroyed
        /// </summary>
        private void OnDestroy()
        {
            // 仅当本 item 处于悬停状态时才收起 tooltip，避免 ClearItemList 时
            // 对每个被销毁的 item 都重复调用一次 HideImmediate
            if (isHovered && tooltip != null)
            {
                tooltip.HideImmediate();
            }
        }
    }
}
