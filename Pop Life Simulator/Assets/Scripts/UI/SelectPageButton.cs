using UnityEngine;
using UnityEngine.UI;
using PopLife.Data;

namespace PopLife.UI
{
    /// <summary>
    /// SelectPage 筛选按钮 - 使用传统 Button 组件 + Alpha Hit Test 检测非矩形区域
    /// 支持自定义形状的 Sprite（Body/Head/Front/Back 等）
    /// 在 Inspector 中手动配置筛选范围和视觉样式
    /// </summary>
    [RequireComponent(typeof(Button))]
    [RequireComponent(typeof(Image))]
    public class SelectPageButton : MonoBehaviour, IFilterButton
    {
        [Header("Filter Configuration")]
        [SerializeField] private bool isAllButton = false; // 是否为 "All" 按钮（显示所有货架）
        [SerializeField] private SelectPage filterPage; // 筛选范围（如果是 All 按钮则忽略此值）

        [Header("UI References")]
        [SerializeField] private Button button; // 传统 Button 组件
        [SerializeField] private Image buttonImage; // 主按钮图像（使用 Alpha Hit Test）

        private bool isSelected = false;
        private System.Action<IFilterButton> onClickCallback;
        private ColorBlock originalColors; // 缓存原始 ColorBlock

        // IFilterButton interface
        public object FilterValue => isAllButton ? null : (object)filterPage; // All 按钮返回 null，普通按钮返回 SelectPage 枚举值
        public bool IsSelected => isSelected;

        private void Awake()
        {
            // Auto-find references
            if (button == null)
                button = GetComponent<Button>();

            if (buttonImage == null)
                buttonImage = GetComponent<Image>();

            // Configure Alpha Hit Test on the button's target graphic
            if (buttonImage != null)
            {
                buttonImage.alphaHitTestMinimumThreshold = 0.1f;

                // 确保 Button 组件使用这个 Image 作为 targetGraphic
                if (button != null && button.targetGraphic == null)
                {
                    button.targetGraphic = buttonImage;
                }
            }

            // 缓存原始 ColorBlock
            if (button != null)
            {
                originalColors = button.colors;
                button.onClick.AddListener(OnButtonClick);
            }

            UpdateVisual();
        }

        /// <summary>
        /// Initialize button (called by ShelfListPanel)
        /// </summary>
        public void Initialize(System.Action<IFilterButton> onClickCallback)
        {
            this.onClickCallback = onClickCallback;
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

        public void Initialize(object filterValue, string displayText, System.Action<IFilterButton> onClickCallback)
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// Get GameObject (IFilterButton interface)
        /// </summary>
        public GameObject GetGameObject()
        {
            return gameObject;
        }

        /// <summary>
        /// Button click callback (called by Unity Button component)
        /// </summary>
        private void OnButtonClick()
        {
            onClickCallback?.Invoke(this);
        }

        /// <summary>
        /// Update visual appearance based on state
        /// 使用 Button 的 ColorBlock 系统管理颜色
        /// </summary>
        private void UpdateVisual()
        {
            if (button == null || buttonImage == null) return;

            if (isSelected)
            {
                // 选中状态：设置为 selectedColor
                buttonImage.color = originalColors.selectedColor;

                // 禁用 Button 交互（防止重复点击）
                button.interactable = false;
            }
            else
            {
                // 未选中状态：恢复为 normalColor
                buttonImage.color = originalColors.normalColor;

                // 启用 Button 交互
                button.interactable = true;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Apply alpha threshold in editor (需要贴图 Read/Write Enabled)
            if (buttonImage != null && buttonImage.sprite != null && buttonImage.sprite.texture != null)
            {
                try
                {
                    // 只有贴图可读或使用 Crunch 压缩时才能设置
                    if (buttonImage.sprite.texture.isReadable)
                    {
                        buttonImage.alphaHitTestMinimumThreshold = 0.1f;
                    }
                    else
                    {
                        Debug.LogWarning($"[SelectPageButton] '{gameObject.name}' 的贴图不可读，无法设置 alphaHitTestMinimumThreshold。" +
                            $"请在贴图导入设置中勾选 'Read/Write Enabled'。", this);
                    }
                }
                catch (UnityException ex)
                {
                    Debug.LogWarning($"[SelectPageButton] '{gameObject.name}' 无法设置 alphaHitTestMinimumThreshold: {ex.Message}", this);
                }
            }
        }
#endif
    }
}
