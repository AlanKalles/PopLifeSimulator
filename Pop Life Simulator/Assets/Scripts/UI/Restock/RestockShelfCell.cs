using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using PrimeTween;

namespace PopLife.UI.Restock
{
    /// <summary>
    /// 补货面板中的单格货架视图——完全无状态，所有数据通过 <see cref="Render"/> 从 RestockPanel 注入。
    /// 点击/输入等用户操作通过 Action 回调上报给 RestockPanel；Cell 自己不决定选中/数量变化。
    /// </summary>
    public class RestockShelfCell : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Display")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text stockNumberText;    // 显示 "current/max"，例如 "5/12"；current=0 时整行变色

        [Header("Amount Control")]
        [SerializeField] private Button minusButton;
        [SerializeField] private Button plusButton;
        [SerializeField] private Button maxButton;
        [SerializeField] private TMP_InputField amountInput;

        [Header("Selection")]
        [SerializeField] private Button selectCheckboxButton;
        [SerializeField] private Image selectCheckboxImage;
        [SerializeField] private Sprite selectedCheckboxSprite;
        [SerializeField] private Sprite unselectedCheckboxSprite;
        [SerializeField] private Image borderImage;

        [Header("Hover Background")]
        [SerializeField] private Image hoverBackgroundImage;

        [Header("Colors (customizable in Inspector)")]
        [SerializeField] private Color borderSelectedColor = new Color(1f, 0.4f, 0.7f, 1f); // 粉色
        [SerializeField] private Color borderDeselectedColor = Color.white;
        [SerializeField] private Color stockLowColor = Color.red;
        [SerializeField] private Color stockNormalColor = Color.white;
        [SerializeField] private Color hoverColor = new Color(0f, 0f, 0f, 0.35f);

        [Header("Click Animation")]
        [SerializeField] private float pressScale = 0.95f;
        [SerializeField] private float pressDuration = 0.05f;
        [SerializeField] private float releaseDuration = 0.08f;

        // Cell → Panel 事件上报
        public event Action<string> OnCellBodyClicked;
        public event Action<string> OnSelectCheckboxClicked;
        public event Action<string, int> OnAmountDeltaRequested;
        public event Action<string, int> OnAmountSetRequested;

        public string ShelfInstanceId { get; private set; }
        private int cachedRoom;           // maxStock - currentStock，用于输入钳制
        private RectTransform rectTransform;
        private Sequence pressSequence;
        private bool initialized;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            EnsureWired();
        }

        private void EnsureWired()
        {
            if (initialized) return;
            initialized = true;

            if (minusButton != null)
                minusButton.onClick.AddListener(() => EmitDeltaWithBounce(-1));

            if (plusButton != null)
                plusButton.onClick.AddListener(() => EmitDeltaWithBounce(+1));

            if (maxButton != null)
                maxButton.onClick.AddListener(() =>
                {
                    PlayPressBounce();
                    OnAmountSetRequested?.Invoke(ShelfInstanceId, cachedRoom);
                });

            if (selectCheckboxButton != null)
                selectCheckboxButton.onClick.AddListener(() =>
                {
                    PlayPressBounce();
                    OnSelectCheckboxClicked?.Invoke(ShelfInstanceId);
                });

            if (amountInput != null)
            {
                amountInput.contentType = TMP_InputField.ContentType.IntegerNumber;
                amountInput.onEndEdit.AddListener(OnAmountInputEndEdit);
            }
        }

        /// <summary>Panel 调用以更新所有视觉状态。完全覆盖，不保留 Cell 内部状态。</summary>
        public void Render(in CellViewModel vm)
        {
            EnsureWired();
            ShelfInstanceId = vm.instanceId;
            cachedRoom = Mathf.Max(0, vm.maxStock - vm.currentStock);

            if (iconImage != null)
            {
                iconImage.sprite = vm.icon;
                iconImage.enabled = vm.icon != null;
            }
            if (nameText != null) nameText.text = vm.shelfName;
            if (stockNumberText != null)
            {
                stockNumberText.text = $"{vm.currentStock}/{vm.maxStock}";
                stockNumberText.color = vm.stockIsLow ? stockLowColor : stockNormalColor;
            }

            if (amountInput != null)
                amountInput.SetTextWithoutNotify(vm.restockAmount.ToString());

            if (minusButton != null) minusButton.interactable = vm.canDecrement;
            if (plusButton != null) plusButton.interactable = vm.canIncrement;
            if (maxButton != null) maxButton.interactable = cachedRoom > 0;

            if (selectCheckboxImage != null)
            {
                selectCheckboxImage.sprite = vm.isSelected ? selectedCheckboxSprite : unselectedCheckboxSprite;
            }
            if (borderImage != null)
            {
                borderImage.color = vm.isSelected ? borderSelectedColor : borderDeselectedColor;
            }
            if (hoverBackgroundImage != null && !hoverBackgroundImage.gameObject.activeSelf)
            {
                // keep hover layer reset; we do not touch its alpha here if it was just toggled
            }
        }

        // ================================================================
        //  事件上报
        // ================================================================

        private void EmitDeltaWithBounce(int delta)
        {
            PlayPressBounce();
            OnAmountDeltaRequested?.Invoke(ShelfInstanceId, delta);
        }

        private void OnAmountInputEndEdit(string rawText)
        {
            // 集中校验：paste / 空串 / 非法输入 / 溢出 全部在此归一化
            int parsed = int.TryParse(rawText, out var v) ? v : 0;
            int clamped = Mathf.Clamp(parsed, 0, cachedRoom);
            if (amountInput != null)
                amountInput.SetTextWithoutNotify(clamped.ToString());
            PlayPressBounce();
            OnAmountSetRequested?.Invoke(ShelfInstanceId, clamped);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // 只有当点击既不在子按钮也不在输入框上时，事件系统才会冒泡到 Cell 背景
            PlayPressBounce();
            OnCellBodyClicked?.Invoke(ShelfInstanceId);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (hoverBackgroundImage != null)
            {
                var c = hoverColor;
                hoverBackgroundImage.color = c;
                var col = hoverBackgroundImage.color;
                col.a = hoverColor.a;
                hoverBackgroundImage.color = col;
                hoverBackgroundImage.enabled = true;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (hoverBackgroundImage != null)
            {
                var col = hoverBackgroundImage.color;
                col.a = 0f;
                hoverBackgroundImage.color = col;
            }
        }

        private void PlayPressBounce()
        {
            if (rectTransform == null) return;
            pressSequence.Stop();
            rectTransform.localScale = Vector3.one;
            pressSequence = Sequence.Create()
                .Chain(Tween.Scale(rectTransform, pressScale, pressDuration, Ease.InQuad))
                .Chain(Tween.Scale(rectTransform, 1f, releaseDuration, Ease.OutBack));
        }

        private void OnDisable()
        {
            pressSequence.Stop();
            if (rectTransform != null) rectTransform.localScale = Vector3.one;
        }
    }

    /// <summary>Panel → Cell 的数据注入结构。</summary>
    public readonly struct CellViewModel
    {
        public readonly string instanceId;
        public readonly Sprite icon;
        public readonly string shelfName;
        public readonly int currentStock;
        public readonly int maxStock;
        public readonly int restockAmount;
        public readonly bool isSelected;
        public readonly bool canDecrement;
        public readonly bool canIncrement;
        public readonly bool stockIsLow;

        public CellViewModel(string instanceId, Sprite icon, string shelfName,
            int currentStock, int maxStock, int restockAmount, bool isSelected,
            bool canDecrement, bool canIncrement, bool stockIsLow)
        {
            this.instanceId = instanceId;
            this.icon = icon;
            this.shelfName = shelfName;
            this.currentStock = currentStock;
            this.maxStock = maxStock;
            this.restockAmount = restockAmount;
            this.isSelected = isSelected;
            this.canDecrement = canDecrement;
            this.canIncrement = canIncrement;
            this.stockIsLow = stockIsLow;
        }
    }
}
