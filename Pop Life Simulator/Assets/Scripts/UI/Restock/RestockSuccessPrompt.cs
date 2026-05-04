using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PopLife.UI.Restock
{
    /// <summary>
    /// Success overlay shown after a restock transaction completes.
    /// The first short interval only swallows clicks to prevent double-click dismissal.
    /// </summary>
    public class RestockSuccessPrompt : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button dismissClickTarget;
        [SerializeField] private float dismissLockDuration = 0.2f;
        [SerializeField] private string successMessage = "Shelves restocked successfully.";
        [SerializeField] private string skipMessage = "Skipped restocking for today.";

        private Action onDismiss;
        private float shownAtUnscaledTime;
        private bool isVisible;
        private bool isDismissing;

        private void Awake()
        {
            if (root == null) root = gameObject;

            if (dismissClickTarget != null)
                dismissClickTarget.onClick.AddListener(HandleDismissClick);

            Hide();
        }

        public void Show(Action dismissCallback) => ShowInternal(dismissCallback, successMessage);

        public void ShowSkip(Action dismissCallback) => ShowInternal(dismissCallback, skipMessage);

        private void ShowInternal(Action dismissCallback, string message)
        {
            onDismiss = dismissCallback;
            shownAtUnscaledTime = Time.unscaledTime;
            isVisible = true;
            isDismissing = false;

            if (messageText != null)
                messageText.text = message;

            if (root != null)
                root.SetActive(true);
        }

        public void Hide()
        {
            isVisible = false;
            isDismissing = false;
            onDismiss = null;

            if (root != null)
                root.SetActive(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            HandleDismissClick();
        }

        private void HandleDismissClick()
        {
            if (!isVisible || isDismissing) return;

            if (Time.unscaledTime - shownAtUnscaledTime < dismissLockDuration)
                return;

            isDismissing = true;
            onDismiss?.Invoke();
        }
    }
}
