using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using PopLife.Data;

namespace PopLife.UI
{
    /// <summary>
    /// 地板 item — 图标 + 尺寸 + 价格
    /// 用于建造面板 Floor Tab 的内容区
    /// </summary>
    public class FloorTileListItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI sizeText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private Button button;
        [SerializeField] private Outline outline;

        [Header("Hover Settings")]
        [SerializeField] private Color normalOutlineColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        [SerializeField] private Color hoverOutlineColor = new Color(1f, 1f, 0f, 1f);
        [SerializeField] private float outlineWidth = 3f;

        private FloorTileArchetype archetype;
        private System.Action<FloorTileArchetype> onSelectCallback;
        private System.Action onElevatorCallback;
        private bool isElevatorButton;

        void Awake()
        {
            ApplyIconImageSettings();

            if (button != null)
                button.onClick.AddListener(OnButtonClicked);

            if (outline == null)
            {
                outline = GetComponent<Outline>();
                if (outline == null)
                    outline = gameObject.AddComponent<Outline>();
            }
            outline.effectColor = normalOutlineColor;
            outline.effectDistance = new Vector2(outlineWidth, outlineWidth);
            outline.enabled = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyIconImageSettings();
        }
#endif

        public void Initialize(FloorTileArchetype archetype, System.Action<FloorTileArchetype> onSelectCallback)
        {
            this.archetype = archetype;
            this.onSelectCallback = onSelectCallback;

            SetIcon(archetype != null ? archetype.icon : null);

            if (sizeText != null)
                sizeText.text = $"{archetype.TileSize.x}x{archetype.TileSize.y}";

            UpdateCostDisplay();
        }

        /// <summary>
        /// 初始化为电梯放置按钮（无 archetype）
        /// </summary>
        public void InitializeAsElevator(ElevatorArchetype elevatorArchetype, System.Action onElevatorCallback)
        {
            this.onElevatorCallback = onElevatorCallback;
            this.isElevatorButton = true;

            if (sizeText != null)
                sizeText.text = "Elevator";

            if (costText != null)
                costText.text = ""; // 电梯放置无固定价格

            SetIcon(elevatorArchetype != null ? elevatorArchetype.icon : null);
        }

        public void UpdateCostDisplay()
        {
            if (isElevatorButton || costText == null || archetype == null) return;

            int cost = archetype.buildCost;
            costText.text = $"${cost}";

            if (ResourceManager.Instance != null)
            {
                bool canAfford = ResourceManager.Instance.money >= cost;
                costText.color = canAfford ? Color.white : Color.red;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (outline != null)
            {
                outline.enabled = true;
                outline.effectColor = hoverOutlineColor;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (outline != null)
                outline.enabled = false;
        }

        private void OnButtonClicked()
        {
            if (isElevatorButton)
                onElevatorCallback?.Invoke();
            else if (archetype != null)
                onSelectCallback?.Invoke(archetype);
        }

        public FloorTileArchetype GetArchetype() => archetype;

        private void SetIcon(Sprite sprite)
        {
            if (iconImage == null) return;

            ApplyIconImageSettings();
            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
        }

        private void ApplyIconImageSettings()
        {
            if (iconImage == null) return;

            iconImage.preserveAspect = true;
        }
    }
}
