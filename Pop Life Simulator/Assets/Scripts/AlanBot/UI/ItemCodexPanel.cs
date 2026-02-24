using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace PopLife.AlanBot.UI
{
    /// <summary>
    /// Item Codex 独立面板（stub实现）
    /// 可被任何外部系统调用 Show()/Hide()
    /// </summary>
    public class ItemCodexPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("动画")]
        [SerializeField] private float fadeInDuration = 0.2f;
        [SerializeField] private float fadeOutDuration = 0.15f;

        private Coroutine fadeCoroutine;

        private void Awake()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (titleText != null)
                titleText.text = "ITEM CODEX";

            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);
        }

        public void Show()
        {
            if (panelRoot != null)
                panelRoot.SetActive(true);

            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);

            fadeCoroutine = StartCoroutine(FadeCanvasGroup(0f, 1f, fadeInDuration, true, true));
        }

        public void Hide()
        {
            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);

            fadeCoroutine = StartCoroutine(FadeAndHide());
        }

        private IEnumerator FadeAndHide()
        {
            yield return FadeCanvasGroup(1f, 0f, fadeOutDuration, false, false);

            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        private IEnumerator FadeCanvasGroup(float startAlpha, float endAlpha, float duration,
            bool interactable, bool blocksRaycasts)
        {
            if (canvasGroup == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
                yield return null;
            }

            canvasGroup.alpha = endAlpha;
            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = blocksRaycasts;
        }
    }
}
