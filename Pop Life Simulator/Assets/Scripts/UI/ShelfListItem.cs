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

        private void Awake()
        {
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

        /// <summary>
        /// Initialize shelf list item with shelf data
        /// </summary>
        public void Initialize(ShelfArchetype shelf, System.Action<ShelfArchetype> onSelectCallback, ShelfTooltip tooltip)
        {
            this.shelf = shelf;
            this.onSelectCallback = onSelectCallback;
            this.tooltip = tooltip;

            // Update UI
            if (iconImage != null && shelf.icon != null)
            {
                iconImage.sprite = shelf.icon;
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
            if (button != null && !button.interactable) return;

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
        /// Cleanup when destroyed
        /// </summary>
        private void OnDestroy()
        {
            // Hide tooltip if this item is destroyed while hovered
            if (tooltip != null)
            {
                tooltip.HideImmediate();
            }
        }
    }
}
