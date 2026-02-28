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
    public class FilterToggleButton : MonoBehaviour, IFilterButton
    {
        [Header("UI References")]
        [SerializeField] private Button button;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI labelText;

        [Header("Sprite Settings (可选，设置后优先使用sprite切换)")]
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite selectedSprite;

        [Header("Color Settings")]
        [SerializeField] private Color normalColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.5f, 0.7f, 0.9f, 1f); // Blue highlight
        [SerializeField] private Color hoverColor = new Color(0.4f, 0.4f, 0.4f, 1f);

        private bool isSelected = false;
        private System.Action<FilterToggleButton> onToggleCallback;

        /// <summary>
        /// Associated data (ProductCategory enum or null for "All")
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
        /// Initialize toggle button with filter value and callback (IFilterButton interface)
        /// </summary>
        public void Initialize(object filterValue, string displayText, System.Action<IFilterButton> onClickCallback)
        {
            this.FilterValue = filterValue;
            this.onToggleCallback = (btn) => onClickCallback?.Invoke(btn);

            if (labelText != null)
            {
                labelText.text = displayText;
            }

            UpdateVisual();
        }

        /// <summary>
        /// Initialize toggle button with filter value and callback (Legacy overload for backward compatibility)
        /// </summary>
        /// <param name="filterValue">ProductCategory enum value or null for "All"</param>
        /// <param name="displayText">Text to display on button</param>
        /// <param name="onToggleCallback">Callback when button is clicked</param>
        /// <param name="customSprite">Optional custom sprite for background</param>
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
        /// 如果设置了 normalSprite/selectedSprite 则切换 sprite，否则仅切换颜色
        /// </summary>
        private void UpdateVisual()
        {
            if (backgroundImage == null) return;

            bool useSpriteSwap = normalSprite != null && selectedSprite != null;

            if (isSelected)
            {
                if (useSpriteSwap)
                    backgroundImage.sprite = selectedSprite;
                backgroundImage.color = selectedColor;
            }
            else
            {
                if (useSpriteSwap)
                    backgroundImage.sprite = normalSprite;
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

        /// <summary>
        /// Get GameObject (IFilterButton interface)
        /// </summary>
        public GameObject GetGameObject()
        {
            return gameObject;
        }
    }
}
