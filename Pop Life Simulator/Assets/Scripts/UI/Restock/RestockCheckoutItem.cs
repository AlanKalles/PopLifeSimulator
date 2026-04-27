using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PopLife.UI.Restock
{
    /// <summary>
    /// 结算面板中单个 shelf 的 line item 视图——无状态，由 RestockPanel 驱动。
    /// 上下箭头和 Cell 的 +/- 共用同一套事件 API（Panel 侧统一处理）。
    /// </summary>
    public class RestockCheckoutItem : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private TMP_Text productNameText;
        [SerializeField] private TMP_Text amountText;        // 补货数量
        [SerializeField] private TMP_Text formulaText;       // "10 units x $10 = $100"

        [Header("Amount Control")]
        [SerializeField] private Button upButton;
        [SerializeField] private Button downButton;

        public event Action<string, int> OnAmountDeltaRequested;

        public string ShelfInstanceId { get; private set; }
        private bool initialized;

        private void Awake()
        {
            EnsureWired();
        }

        private void EnsureWired()
        {
            if (initialized) return;
            initialized = true;

            if (upButton != null)
                upButton.onClick.AddListener(() => OnAmountDeltaRequested?.Invoke(ShelfInstanceId, +1));
            if (downButton != null)
                downButton.onClick.AddListener(() => OnAmountDeltaRequested?.Invoke(ShelfInstanceId, -1));
        }

        public void Render(in CheckoutItemViewModel vm)
        {
            EnsureWired();
            ShelfInstanceId = vm.instanceId;

            if (productNameText != null) productNameText.text = vm.name;
            if (amountText != null) amountText.text = vm.restockAmount.ToString();
            if (formulaText != null)
            {
                formulaText.text = $"{vm.restockAmount} units x ${vm.stockFee} = ${vm.lineTotal}";
            }
            if (upButton != null) upButton.interactable = vm.canIncrement;
            if (downButton != null) downButton.interactable = vm.canDecrement;
        }
    }

    public readonly struct CheckoutItemViewModel
    {
        public readonly string instanceId;
        public readonly string name;
        public readonly int restockAmount;
        public readonly int stockFee;
        public readonly int lineTotal;
        public readonly bool canDecrement;
        public readonly bool canIncrement;

        public CheckoutItemViewModel(string instanceId, string name, int restockAmount,
            int stockFee, int lineTotal, bool canDecrement, bool canIncrement)
        {
            this.instanceId = instanceId;
            this.name = name;
            this.restockAmount = restockAmount;
            this.stockFee = stockFee;
            this.lineTotal = lineTotal;
            this.canDecrement = canDecrement;
            this.canIncrement = canIncrement;
        }
    }
}
