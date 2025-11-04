using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace PopLife.UI
{
    /// <summary>
    /// Reusable toggle button for filter groups
    /// Supports single-selection behavior with visual feedback
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class FilterToggleButton : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button button;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI labelText;

        [Header("Color Settings")]
        [SerializeField] private Color normalColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.5f, 0.7f, 0.9f, 1f); // Blue highlight
        [SerializeField] private Color hoverColor = new Color(0.4f, 0.4f, 0.4f, 1f);

        private bool isSelected = false;
        private System.Action<FilterToggleButton> onToggleCallback;

        /// <summary>
        /// Associated data (can be SelectPage enum, ProductCategory enum, or null for "All")
        /// </summary>
        public object FilterValue { get; private set; }

        private void Awake()
        {
            // Auto-find references
            if (button == null)
                button = GetComponent<Button>();
            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();
            if (labelText == null)
                labelText = GetComponentInChildren<TextMeshProUGUI>();

            // Register button click
            if (button != null)
            {
                button.onClick.AddListener(OnButtonClicked);
            }

            // Set initial color
            UpdateVisual();
        }

        /// <summary>
        /// Initialize toggle button with filter value and callback
        /// </summary>
        /// <param name="filterValue">Enum value (SelectPage/ProductCategory) or null for "All"</param>
        /// <param name="displayText">Text to display on button</param>
        /// <param name="onToggleCallback">Callback when button is clicked</param>
        /// <param name="customSprite">Optional custom sprite for background (e.g., SelectPage icons)</param>
        public void Initialize(object filterValue, string displayText, System.Action<FilterToggleButton> onToggleCallback, Sprite customSprite = null)
        {
            this.FilterValue = filterValue;
            this.onToggleCallback = onToggleCallback;

            if (labelText != null)
            {
                labelText.text = displayText;
            }

            // Set custom sprite if provided
            if (customSprite != null && backgroundImage != null)
            {
                backgroundImage.sprite = customSprite;
            }

            UpdateVisual();
        }

        /// <summary>
        /// Set selected state
        /// </summary>
        public void SetSelected(bool selected)
        {
            isSelected = selected;
            UpdateVisual();
        }

        /// <summary>
        /// Get selected state
        /// </summary>
        public bool IsSelected => isSelected;

        private void OnButtonClicked()
        {
            // Notify parent group
            onToggleCallback?.Invoke(this);
        }

        /// <summary>
        /// Update visual appearance based on state
        /// </summary>
        private void UpdateVisual()
        {
            if (backgroundImage == null) return;

            if (isSelected)
            {
                backgroundImage.color = selectedColor;
            }
            else
            {
                backgroundImage.color = normalColor;
            }
        }

        /// <summary>
        /// Optional: Add hover effect
        /// </summary>
        public void OnPointerEnter()
        {
            if (backgroundImage != null && !isSelected)
            {
                backgroundImage.color = hoverColor;
            }
        }

        /// <summary>
        /// Optional: Remove hover effect
        /// </summary>
        public void OnPointerExit()
        {
            if (backgroundImage != null && !isSelected)
            {
                backgroundImage.color = normalColor;
            }
        }
    }
}
