using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PopLife.Data;

namespace PopLife.UI
{
    /// <summary>
    /// Building list item component
    /// Displays building information in the scrollable list
    /// </summary>
    public class BuildingListItem : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private Button selectButton;
        [SerializeField] private Image backgroundImage; // 用于高亮选中状态

        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private Color selectedColor = new Color(0.3f, 0.5f, 0.7f, 0.9f);
        [SerializeField] private Color affordableTextColor = Color.white;
        [SerializeField] private Color unaffordableTextColor = Color.red;

        private BuildingArchetype archetype;
        private System.Action<BuildingArchetype> onSelectCallback;
        private bool isSelected = false;

        private void Awake()
        {
            if (selectButton != null)
            {
                selectButton.onClick.AddListener(OnButtonClicked);
            }
        }

        /// <summary>
        /// Initialize building list item with archetype data
        /// </summary>
        public void Initialize(BuildingArchetype archetype, System.Action<BuildingArchetype> onSelectCallback)
        {
            this.archetype = archetype;
            this.onSelectCallback = onSelectCallback;

            // Update UI
            if (iconImage != null && archetype.icon != null)
            {
                iconImage.sprite = archetype.icon;
            }

            if (nameText != null)
            {
                nameText.text = archetype.displayName;
            }

            UpdateCostDisplay();
            SetSelected(false); // 初始状态为未选中
        }

        /// <summary>
        /// Update cost text with affordability check
        /// </summary>
        public void UpdateCostDisplay()
        {
            if (costText == null || archetype == null) return;

            int cost = archetype.buildCost;
            costText.text = $"${cost}";

            // Check affordability
            if (ResourceManager.Instance != null)
            {
                bool canAfford = ResourceManager.Instance.money >= cost;
                costText.color = canAfford ? affordableTextColor : unaffordableTextColor;
            }
        }

        /// <summary>
        /// Set selected state (visual feedback)
        /// </summary>
        public void SetSelected(bool selected)
        {
            isSelected = selected;

            if (backgroundImage != null)
            {
                backgroundImage.color = selected ? selectedColor : normalColor;
            }
        }

        private void OnButtonClicked()
        {
            if (archetype != null)
            {
                onSelectCallback?.Invoke(archetype);
            }
        }

        /// <summary>
        /// Get the archetype associated with this item
        /// </summary>
        public BuildingArchetype GetArchetype()
        {
            return archetype;
        }
    }
}
