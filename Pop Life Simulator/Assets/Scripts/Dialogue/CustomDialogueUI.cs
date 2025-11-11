using UnityEngine;
using UnityEngine.UI;
using NodeCanvas.DialogueTrees;
using NodeCanvas.Framework;
using System.Collections;

namespace Poplife.Dialogue
{
    /// <summary>
    /// Custom dialogue UI that supports position configuration via Blackboard
    /// 支持通过Blackboard配置位置的自定义对话UI
    /// </summary>
    public class CustomDialogueUI : MonoBehaviour
    {
        [Header("Filter Settings")]
        [Tooltip("Only show dialogues with this UI type. Leave empty for all.")]
        public string targetUIType = "Default";

        [Header("UI Elements")]
        public RectTransform dialoguePanel;
        public Text actorName;
        public Text dialogueText;
        public Image actorPortrait;
        public Button nextButton;
        public GameObject waitInputIndicator;

        [Header("Animation")]
        public float fadeInDuration = 0.2f;
        public float fadeOutDuration = 0.2f;

        private UnityEngine.CanvasGroup canvasGroup;
        private DialogueTree currentDialogue;
        private IBlackboard currentBlackboard;
        private Vector2 originalPosition;
        private Vector2 originalAnchor;
        private Vector3 originalScale;

        private void Awake()
        {
            canvasGroup = dialoguePanel.GetComponent<UnityEngine.CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = dialoguePanel.gameObject.AddComponent<UnityEngine.CanvasGroup>();
            }

            // Store original transform
            originalPosition = dialoguePanel.anchoredPosition;
            originalAnchor = dialoguePanel.anchorMin;
            originalScale = dialoguePanel.localScale;

            Subscribe();
            Hide();
        }

        private void OnEnable() { Subscribe(); }
        private void OnDisable() { UnSubscribe(); }

        private void Subscribe()
        {
            DialogueTree.OnDialogueStarted += OnDialogueStarted;
            DialogueTree.OnDialogueFinished += OnDialogueFinished;
            DialogueTree.OnSubtitlesRequest += OnSubtitlesRequest;
        }

        private void UnSubscribe()
        {
            DialogueTree.OnDialogueStarted -= OnDialogueStarted;
            DialogueTree.OnDialogueFinished -= OnDialogueFinished;
            DialogueTree.OnSubtitlesRequest -= OnSubtitlesRequest;
        }

        private void Hide()
        {
            dialoguePanel.gameObject.SetActive(false);
            if (waitInputIndicator != null)
            {
                waitInputIndicator.SetActive(false);
            }
        }

        private void OnDialogueStarted(DialogueTree dialogue)
        {
            currentDialogue = dialogue;
            currentBlackboard = dialogue.blackboard;

            // Check if this UI should handle this dialogue
            if (!ShouldHandleDialogue())
            {
                return;
            }

            Debug.Log($"[CustomDialogueUI] Handling dialogue with UI type: {targetUIType}");

            // Apply position configuration from Blackboard
            ApplyPositionConfig();
        }

        private void OnSubtitlesRequest(SubtitlesRequestInfo info)
        {
            // Check if this UI should handle this dialogue
            if (!ShouldHandleDialogue())
            {
                return;
            }

            StopAllCoroutines();
            StartCoroutine(ShowDialogueCoroutine(info));
        }

        private void OnDialogueFinished(DialogueTree dialogue)
        {
            if (dialogue == currentDialogue)
            {
                StartCoroutine(FadeOutAndHide());
                currentDialogue = null;
                currentBlackboard = null;

                // Restore original transform
                ResetTransform();
            }
        }

        private bool ShouldHandleDialogue()
        {
            if (currentBlackboard == null)
                return false;

            // Get UI type from blackboard
            string uiType = currentBlackboard.GetVariableValue<string>("uiType");

            // If no target type specified, handle all
            if (string.IsNullOrEmpty(targetUIType))
                return true;

            // Check if UI type matches
            return uiType == targetUIType;
        }

        private void ApplyPositionConfig()
        {
            if (currentBlackboard == null)
                return;

            // Get configuration from blackboard
            Vector2 position = currentBlackboard.GetVariableValue<Vector2>("panelPosition");
            Vector2 anchor = currentBlackboard.GetVariableValue<Vector2>("panelAnchor");
            float scale = currentBlackboard.GetVariableValue<float>("panelScale");

            // Apply anchor
            if (anchor != Vector2.zero)
            {
                dialoguePanel.anchorMin = anchor;
                dialoguePanel.anchorMax = anchor;
                dialoguePanel.pivot = anchor;
            }

            // Apply position (as offset from anchor)
            dialoguePanel.anchoredPosition = position;

            // Apply scale
            if (scale > 0)
            {
                dialoguePanel.localScale = Vector3.one * scale;
            }

            Debug.Log($"[CustomDialogueUI] Applied position: {position}, anchor: {anchor}, scale: {scale}");
        }

        private void ResetTransform()
        {
            dialoguePanel.anchoredPosition = originalPosition;
            dialoguePanel.anchorMin = originalAnchor;
            dialoguePanel.anchorMax = originalAnchor;
            dialoguePanel.localScale = originalScale;
        }

        private IEnumerator ShowDialogueCoroutine(SubtitlesRequestInfo info)
        {
            // Show panel
            dialoguePanel.gameObject.SetActive(true);

            // Fade in
            yield return StartCoroutine(FadeIn());

            // Set content
            if (actorName != null)
            {
                actorName.text = info.actor.name;
            }

            if (dialogueText != null)
            {
                dialogueText.text = "";
                yield return StartCoroutine(TypeText(info.statement.text));
            }

            // Set portrait
            if (actorPortrait != null && info.actor.portraitSprite != null)
            {
                actorPortrait.gameObject.SetActive(true);
                actorPortrait.sprite = info.actor.portraitSprite;
            }
            else if (actorPortrait != null)
            {
                actorPortrait.gameObject.SetActive(false);
            }

            // Show wait indicator
            if (waitInputIndicator != null)
            {
                waitInputIndicator.SetActive(true);
            }
        }

        private IEnumerator FadeIn()
        {
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }

        private IEnumerator FadeOutAndHide()
        {
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
                yield return null;
            }
            canvasGroup.alpha = 0f;
            Hide();
        }

        private IEnumerator TypeText(string text)
        {
            for (int i = 0; i <= text.Length; i++)
            {
                dialogueText.text = text.Substring(0, i);
                yield return new WaitForSeconds(0.03f); // Typing speed
            }
        }
    }
}
