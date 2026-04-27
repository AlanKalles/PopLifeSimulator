using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PopLife.UI
{
    /// <summary>
    /// Keeps a button clickable for locked feedback while gating its own allowed callback.
    /// Existing external listeners should still do their own safety checks when needed.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class PhaseLockedButton : MonoBehaviour
    {
        [Header("Phase")]
        [SerializeField] private GamePhase allowedPhase = GamePhase.BuildPhase;
        [TextArea]
        [SerializeField] private string lockedMessage = "This action is not available right now.";

        [Header("Visuals")]
        [SerializeField] private Color lockedColor = new Color(0.45f, 0.45f, 0.45f, 0.65f);
        [SerializeField] private Graphic[] visualsToTint;
        [SerializeField] private RectTransform messageAnchor;

        [Header("Optional Callback")]
        [SerializeField] private UnityEvent onAllowedClick;

        private Button button;
        private Selectable.Transition originalTransition;
        private Graphic[] cachedVisuals = Array.Empty<Graphic>();
        private Color[] originalColors = Array.Empty<Color>();
        private ButtonHoverWobble hoverWobble;
        private bool originalHoverWobbleEnabled;
        private bool subscribedToPhaseEvents;

        public GamePhase AllowedPhase => allowedPhase;
        public bool IsAllowedNow => DayLoopManager.Instance == null || DayLoopManager.Instance.currentPhase == allowedPhase;

        private void Awake()
        {
            button = GetComponent<Button>();
            originalTransition = button.transition;

            if (messageAnchor == null)
                messageAnchor = transform as RectTransform;

            CacheVisuals();

            hoverWobble = GetComponent<ButtonHoverWobble>();
            if (hoverWobble != null)
                originalHoverWobbleEnabled = hoverWobble.enabled;

            button.onClick.AddListener(HandleClick);
            RefreshState();
        }

        private void OnEnable()
        {
            SubscribeToPhaseEvents();
            RefreshState();
        }

        private void OnDisable()
        {
            UnsubscribeFromPhaseEvents();
            RestoreUnlockedState();
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(HandleClick);

            UnsubscribeFromPhaseEvents();
        }

        public void RefreshState()
        {
            if (IsAllowedNow)
                RestoreUnlockedState();
            else
                ApplyLockedState();
        }

        private void HandleClick()
        {
            if (IsAllowedNow)
            {
                onAllowedClick?.Invoke();
                return;
            }

            UIFloatingMessageSpawner.Instance?.Show(messageAnchor, lockedMessage);
        }

        private void CacheVisuals()
        {
            if (visualsToTint == null || visualsToTint.Length == 0)
                visualsToTint = GetComponentsInChildren<Graphic>(true);

            cachedVisuals = visualsToTint ?? Array.Empty<Graphic>();
            originalColors = new Color[cachedVisuals.Length];

            for (int i = 0; i < cachedVisuals.Length; i++)
            {
                if (cachedVisuals[i] != null)
                    originalColors[i] = cachedVisuals[i].color;
            }
        }

        private void ApplyLockedState()
        {
            if (button != null)
            {
                button.interactable = true;
                button.transition = Selectable.Transition.None;
            }

            for (int i = 0; i < cachedVisuals.Length; i++)
            {
                if (cachedVisuals[i] != null)
                    cachedVisuals[i].color = lockedColor;
            }

            if (hoverWobble != null && hoverWobble.enabled)
                hoverWobble.enabled = false;
        }

        private void RestoreUnlockedState()
        {
            if (button != null)
            {
                button.interactable = true;
                button.transition = originalTransition;
            }

            for (int i = 0; i < cachedVisuals.Length; i++)
            {
                if (cachedVisuals[i] != null)
                    cachedVisuals[i].color = originalColors[i];
            }

            if (hoverWobble != null)
                hoverWobble.enabled = originalHoverWobbleEnabled;
        }

        private void SubscribeToPhaseEvents()
        {
            if (subscribedToPhaseEvents || DayLoopManager.Instance == null)
                return;

            DayLoopManager.Instance.OnBuildPhaseStart += RefreshState;
            DayLoopManager.Instance.OnStoreOpen += RefreshState;
            DayLoopManager.Instance.OnStoreClose += RefreshState;
            subscribedToPhaseEvents = true;
        }

        private void UnsubscribeFromPhaseEvents()
        {
            if (!subscribedToPhaseEvents || DayLoopManager.Instance == null)
                return;

            DayLoopManager.Instance.OnBuildPhaseStart -= RefreshState;
            DayLoopManager.Instance.OnStoreOpen -= RefreshState;
            DayLoopManager.Instance.OnStoreClose -= RefreshState;
            subscribedToPhaseEvents = false;
        }
    }
}
