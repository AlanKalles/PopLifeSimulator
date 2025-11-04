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
    public class ShelfListItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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
            // Enable outline highlight
            if (outline != null)
            {
                outline.enabled = true;
                outline.effectColor = hoverOutlineColor;
            }

            // Show tooltip
            if (tooltip != null && shelf != null)
            {
                // Get button's world position for tooltip positioning
                RectTransform rectTransform = GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    // Use the right edge of the button as tooltip anchor
                    Vector3[] corners = new Vector3[4];
                    rectTransform.GetWorldCorners(corners);
                    Vector3 rightCenter = (corners[1] + corners[2]) / 2f; // Right edge center

                    tooltip.Show(shelf, rightCenter);
                }
            }
        }

        /// <summary>
        /// Pointer exit event - Hide highlight and tooltip
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
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
