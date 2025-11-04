using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using PopLife.Data;

namespace PopLife.UI
{
    /// <summary>
    /// Confirmation dialog panel for building destruction
    /// 建筑销毁确认对话框面板
    ///
    /// UI Structure:
    /// - Full-screen blocking panel (prevents all other interactions)
    /// - Content panel containing:
    ///   - Title text: "Sell <building name>?"
    ///   - Info text: "This will return <refund amount> money"
    ///   - Confirm button
    ///   - Cancel button
    /// </summary>
    public class ConfirmationPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject blockingPanel;      // Full-screen blocking panel
        [SerializeField] private GameObject contentPanel;       // Content panel (dialog box)
        [SerializeField] private TextMeshProUGUI titleText;     // Title: "Sell <building name>?"
        [SerializeField] private TextMeshProUGUI infoText;      // Info: "This will return X money"
        [SerializeField] private Button confirmButton;          // Confirm button
        [SerializeField] private Button cancelButton;           // Cancel button

        [Header("Visual Settings")]
        [SerializeField] private Color blockingColor = new Color(0, 0, 0, 0.5f); // Blocking panel color

        // Event callbacks
        private Action onConfirm;
        private Action onCancel;

        private void Awake()
        {
            // Setup blocking panel color
            if (blockingPanel != null)
            {
                Image blockingImage = blockingPanel.GetComponent<Image>();
                if (blockingImage != null)
                {
                    blockingImage.color = blockingColor;
                }
            }

            // Setup button listeners
            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(OnConfirmClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(OnCancelClicked);
            }

            // Hide panel initially
            HidePanel();
        }

        private void Update()
        {
            // Allow ESC to cancel
            if (blockingPanel != null && blockingPanel.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    OnCancelClicked();
                }
            }
        }

        /// <summary>
        /// Show confirmation dialog for building destruction
        /// 显示建筑销毁确认对话框
        /// </summary>
        /// <param name="buildingName">Building name</param>
        /// <param name="refundAmount">Refund amount</param>
        /// <param name="onConfirmCallback">Callback when confirmed</param>
        /// <param name="onCancelCallback">Callback when cancelled (optional)</param>
        public void Show(string buildingName, int refundAmount, Action onConfirmCallback, Action onCancelCallback = null)
        {
            // Set title text
            if (titleText != null)
            {
                titleText.text = $"Sell {buildingName}?";
            }

            // Set info text
            if (infoText != null)
            {
                infoText.text = $"This will return ${refundAmount} money";
            }

            // Save callbacks
            onConfirm = onConfirmCallback;
            onCancel = onCancelCallback;

            // Show panel
            ShowPanel();

            Debug.Log($"[ConfirmationPanel] Showing: Sell {buildingName}? Refund: ${refundAmount}");
        }

        /// <summary>
        /// Handle confirm button click
        /// </summary>
        private void OnConfirmClicked()
        {
            // Play sound
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(AudioKeys.UI_CLICK);
            }

            // Hide panel
            HidePanel();

            // Trigger callback
            onConfirm?.Invoke();
            onConfirm = null;
            onCancel = null;

            Debug.Log("[ConfirmationPanel] Confirmed");
        }

        /// <summary>
        /// Handle cancel button click
        /// </summary>
        private void OnCancelClicked()
        {
            // Play sound
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(AudioKeys.UI_CLICK);
            }

            // Hide panel
            HidePanel();

            // Trigger callback
            onCancel?.Invoke();
            onConfirm = null;
            onCancel = null;

            Debug.Log("[ConfirmationPanel] Cancelled");
        }

        /// <summary>
        /// Show the panel
        /// </summary>
        private void ShowPanel()
        {
            if (blockingPanel != null)
            {
                blockingPanel.SetActive(true);
            }

            if (contentPanel != null)
            {
                contentPanel.SetActive(true);
            }
        }

        /// <summary>
        /// Hide the panel
        /// </summary>
        private void HidePanel()
        {
            if (blockingPanel != null)
            {
                blockingPanel.SetActive(false);
            }

            if (contentPanel != null)
            {
                contentPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Check if panel is showing
        /// </summary>
        public bool IsShowing()
        {
            return blockingPanel != null && blockingPanel.activeSelf;
        }
    }
}
