using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using PrimeTween;

namespace PopLife.NarrativeSystem.UI
{
    /// <summary>
    /// 扇形对话面板，管理三层对话框的显示和交互
    /// Fan conversation panel managing three-tier conversation box display and interaction
    /// </summary>
    public class FanConversationPanel : MonoBehaviour
    {
        [Header("Panel References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Conversation Boxes")]
        [SerializeField] private ConversationBox topBox;     // 上方对话框
        [SerializeField] private ConversationBox centerBox;  // 中间对话框
        [SerializeField] private ConversationBox bottomBox;  // 下方对话框

        [Header("Fan Layout Settings")]
        [SerializeField] private float fanAngle = 25f;       // 扇形角度
        [SerializeField] private float fanRadius = 350f;     // 扇形半径
        [SerializeField] private Vector2 fanPivot;           // 扇形圆心位置

        [Header("Box Sizes")]
        [SerializeField] private Vector2 centerBoxSize = new Vector2(600f, 150f);
        [SerializeField] private Vector2 edgeBoxSize = new Vector2(450f, 100f);
        [SerializeField] private float edgeBoxScale = 0.8f;  // 边缘框缩放

        [Header("Character Portrait")]
        [SerializeField] private Image characterPortrait;
        [SerializeField] private TextMeshProUGUI characterName;

        [Header("Finish Button")]
        [SerializeField] private Button finishButton;
        [SerializeField] private TextMeshProUGUI finishButtonText;

        [Header("Animation")]
        [SerializeField] private float transitionDuration = 0.5f;
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.3f;

        // State
        private NarrativeSequence currentSequence;
        private NarrativeSO currentNarrative;
        private bool isTransitioning;
        private bool isPanelVisible;
        private Coroutine transitionCoroutine;

        // Events
        public event Action<int> OnChoiceSelected;
        public event Action OnFinishClicked;

        private void Awake()
        {
            InitializeComponents();
            SetupEventHandlers();
            HidePanel(true); // Start hidden
        }

        private void InitializeComponents()
        {
            // Ensure canvas group exists
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // Setup finish button
            if (finishButton != null)
            {
                finishButton.gameObject.SetActive(false);
                if (finishButtonText != null)
                    finishButtonText.text = "Finish";
            }

            // Initialize box positions
            PositionBoxes();
        }

        private void SetupEventHandlers()
        {
            // Box click handlers
            if (topBox != null)
                topBox.OnBoxClicked += () => NavigateToBox(BoxPosition.Top);

            if (bottomBox != null)
                bottomBox.OnBoxClicked += () => NavigateToBox(BoxPosition.Bottom);

            // Finish button
            if (finishButton != null)
                finishButton.onClick.AddListener(HandleFinishClicked);
        }

        private void Update()
        {
            if (isPanelVisible && !isTransitioning)
            {
                // Handle scroll wheel input
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (scroll > 0.01f)
                {
                    NavigateToBox(BoxPosition.Top);
                }
                else if (scroll < -0.01f)
                {
                    NavigateToBox(BoxPosition.Bottom);
                }
            }
        }

        /// <summary>
        /// 显示面板
        /// </summary>
        public void ShowPanel(NarrativeSO narrative)
        {
            if (narrative == null) return;

            currentNarrative = narrative;
            isPanelVisible = true;

            // Setup character portrait
            if (characterPortrait != null && narrative.CharacterPortrait != null)
            {
                characterPortrait.sprite = narrative.CharacterPortrait;
                characterPortrait.enabled = true;
            }

            if (characterName != null)
            {
                characterName.text = narrative.CharacterName;
            }

            // Show panel with animation
            StartCoroutine(FadeInPanel());
        }

        /// <summary>
        /// 隐藏面板
        /// </summary>
        public void HidePanel(bool immediate = false)
        {
            isPanelVisible = false;

            if (immediate)
            {
                panelRoot.SetActive(false);
                canvasGroup.alpha = 0;
            }
            else
            {
                StartCoroutine(FadeOutPanel());
            }
        }

        /// <summary>
        /// 显示对话片段
        /// </summary>
        public void DisplaySegments(NarrativeSegment previous, NarrativeSegment current, NarrativeSegment next)
        {
            if (isTransitioning) return;

            // Update top box
            if (topBox != null)
            {
                if (previous != null)
                {
                    topBox.SetContent(previous.TextContent, previous.SpeakerName);
                    topBox.Show();
                }
                else
                {
                    topBox.Hide();
                }
            }

            // Update center box
            if (centerBox != null && current != null)
            {
                centerBox.SetContent(current.TextContent, current.SpeakerName);
                centerBox.Show();
                centerBox.SetHighlight(true); // Center is always highlighted
            }

            // Update bottom box
            if (bottomBox != null)
            {
                if (next != null)
                {
                    bottomBox.SetContent(next.TextContent, next.SpeakerName);
                    bottomBox.Show();
                }
                else
                {
                    bottomBox.Hide();
                }
            }
        }

        /// <summary>
        /// 显示选择选项
        /// </summary>
        public void PresentChoices(List<NarrativeSegment> choices)
        {
            // TODO: Implement choice presentation UI
            // For now, just show the first choice in bottom box
            if (choices != null && choices.Count > 0 && bottomBox != null)
            {
                var firstChoice = choices[0];
                bottomBox.SetContent($"[Choice] {firstChoice.TextContent}", "");
                bottomBox.Show();
            }
        }

        /// <summary>
        /// 显示结束按钮
        /// </summary>
        public void ShowFinishButton()
        {
            if (finishButton != null)
            {
                finishButton.gameObject.SetActive(true);

                // Animate button appearance with PrimeTween
                var rectTransform = finishButton.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.localScale = Vector3.zero;
                    Tween.Scale(rectTransform, Vector3.one, 0.3f, Ease.OutBack);
                }
            }
        }

        /// <summary>
        /// 导航到指定位置的对话框
        /// </summary>
        private void NavigateToBox(BoxPosition position)
        {
            if (isTransitioning) return;

            switch (position)
            {
                case BoxPosition.Top:
                    if (topBox != null && topBox.IsVisible)
                    {
                        NarrativeManager.Instance?.NavigateBackward();
                        AnimateTransition(TransitionDirection.Up);
                    }
                    break;

                case BoxPosition.Bottom:
                    if (bottomBox != null && bottomBox.IsVisible)
                    {
                        NarrativeManager.Instance?.NavigateForward();
                        AnimateTransition(TransitionDirection.Down);
                    }
                    break;
            }
        }

        /// <summary>
        /// 动画过渡
        /// </summary>
        private void AnimateTransition(TransitionDirection direction)
        {
            if (transitionCoroutine != null)
                StopCoroutine(transitionCoroutine);

            transitionCoroutine = StartCoroutine(PerformTransition(direction));
        }

        private IEnumerator PerformTransition(TransitionDirection direction)
        {
            isTransitioning = true;

            float elapsed = 0f;

            // Store initial states
            Vector3 topStartPos = topBox.transform.localPosition;
            Vector3 centerStartPos = centerBox.transform.localPosition;
            Vector3 bottomStartPos = bottomBox.transform.localPosition;

            Quaternion topStartRot = topBox.transform.localRotation;
            Quaternion centerStartRot = centerBox.transform.localRotation;
            Quaternion bottomStartRot = bottomBox.transform.localRotation;

            Vector3 topStartScale = topBox.transform.localScale;
            Vector3 centerStartScale = centerBox.transform.localScale;
            Vector3 bottomStartScale = bottomBox.transform.localScale;

            // Calculate target states based on direction
            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = transitionCurve.Evaluate(elapsed / transitionDuration);

                if (direction == TransitionDirection.Up)
                {
                    // Top → Center, Center → Bottom, Bottom disappears
                    // New Top appears

                    // TODO: Implement smooth transition
                }
                else if (direction == TransitionDirection.Down)
                {
                    // Bottom → Center, Center → Top, Top disappears
                    // New Bottom appears

                    // TODO: Implement smooth transition
                }

                yield return null;
            }

            isTransitioning = false;
        }

        /// <summary>
        /// 定位对话框
        /// </summary>
        private void PositionBoxes()
        {
            if (topBox != null)
            {
                var topTransform = topBox.GetComponent<RectTransform>();
                topTransform.anchoredPosition = new Vector2(fanPivot.x, fanPivot.y + 150f);
                topTransform.localRotation = Quaternion.Euler(0, 0, -fanAngle);
                topTransform.localScale = Vector3.one * edgeBoxScale;
                topTransform.sizeDelta = edgeBoxSize;
            }

            if (centerBox != null)
            {
                var centerTransform = centerBox.GetComponent<RectTransform>();
                centerTransform.anchoredPosition = fanPivot;
                centerTransform.localRotation = Quaternion.identity;
                centerTransform.localScale = Vector3.one;
                centerTransform.sizeDelta = centerBoxSize;
            }

            if (bottomBox != null)
            {
                var bottomTransform = bottomBox.GetComponent<RectTransform>();
                bottomTransform.anchoredPosition = new Vector2(fanPivot.x, fanPivot.y - 150f);
                bottomTransform.localRotation = Quaternion.Euler(0, 0, fanAngle);
                bottomTransform.localScale = Vector3.one * edgeBoxScale;
                bottomTransform.sizeDelta = edgeBoxSize;
            }
        }

        private IEnumerator FadeInPanel()
        {
            panelRoot.SetActive(true);
            canvasGroup.alpha = 0;

            float elapsed = 0;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(0, 1, elapsed / fadeInDuration);
                yield return null;
            }

            canvasGroup.alpha = 1;
        }

        private IEnumerator FadeOutPanel()
        {
            float elapsed = 0;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(1, 0, elapsed / fadeOutDuration);
                yield return null;
            }

            canvasGroup.alpha = 0;
            panelRoot.SetActive(false);
        }

        private void HandleFinishClicked()
        {
            OnFinishClicked?.Invoke();
            NarrativeManager.Instance?.EndCurrentNarrative();
        }

        private enum BoxPosition
        {
            Top,
            Center,
            Bottom
        }

        private enum TransitionDirection
        {
            Up,
            Down
        }
    }
}