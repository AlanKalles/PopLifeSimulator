using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PopLife.UI
{
    /// <summary>
    /// Spawns short UI messages above RectTransform anchors.
    /// </summary>
    public class UIFloatingMessageSpawner : MonoBehaviour
    {
        public static UIFloatingMessageSpawner Instance { get; private set; }

        [Header("Prefab")]
        [SerializeField] private TextMeshProUGUI messagePrefab;
        [SerializeField] private RectTransform spawnRoot;

        [Header("Motion")]
        [SerializeField] private Vector2 anchorOffset = new Vector2(0f, 28f);
        [SerializeField] private float riseDistance = 36f;
        [SerializeField] private float duration = 1.1f;

        [Header("Bounds")]
        [SerializeField] private bool clampHorizontally = true;
        [SerializeField] private float horizontalPadding = 8f;

        private Canvas rootCanvas;
        private RectTransform spawnRootRect;
        private Camera canvasCamera;
        private readonly Vector3[] anchorCorners = new Vector3[4];
        private readonly Vector3[] messageCorners = new Vector3[4];

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[UIFloatingMessageSpawner] Multiple instances found. The newest one will be used.");
            }

            Instance = this;

            rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (spawnRoot == null)
            {
                spawnRoot = rootCanvas != null
                    ? rootCanvas.transform as RectTransform
                    : transform as RectTransform;
            }

            spawnRootRect = spawnRoot;
            if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                canvasCamera = rootCanvas.worldCamera;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Show(RectTransform anchor, string message)
        {
            if (messagePrefab == null)
            {
                Debug.LogWarning("[UIFloatingMessageSpawner] Message prefab is not assigned.");
                return;
            }

            if (anchor == null || spawnRootRect == null || string.IsNullOrWhiteSpace(message))
                return;

            TextMeshProUGUI messageInstance = Instantiate(messagePrefab, spawnRootRect);
            messageInstance.text = message;
            messageInstance.raycastTarget = false;
            messageInstance.ForceMeshUpdate();

            RectTransform messageRect = messageInstance.rectTransform;
            ForceLayoutUpdate(messageRect);
            messageRect.anchoredPosition = ClampToSpawnRoot(GetAnchorTopCenter(anchor) + anchorOffset, messageRect);

            CanvasGroup canvasGroup = messageInstance.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = messageInstance.gameObject.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            StartCoroutine(Animate(messageInstance.gameObject, messageRect, canvasGroup));
        }

        private Vector2 GetAnchorTopCenter(RectTransform anchor)
        {
            anchor.GetWorldCorners(anchorCorners);
            Vector3 worldTopCenter = (anchorCorners[1] + anchorCorners[2]) * 0.5f;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCamera, worldTopCenter);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                spawnRootRect,
                screenPoint,
                canvasCamera,
                out Vector2 localPoint);

            return localPoint;
        }

        private Vector2 ClampToSpawnRoot(Vector2 position, RectTransform messageRect)
        {
            if (!clampHorizontally)
                return position;

            messageRect.anchoredPosition = position;
            ForceLayoutUpdate(messageRect);

            GetMessageLocalBounds(messageRect, out float left, out float right);

            Rect rootRect = spawnRootRect.rect;
            float minX = rootRect.xMin + horizontalPadding;
            float maxX = rootRect.xMax - horizontalPadding;
            float messageWidth = right - left;
            float availableWidth = Mathf.Max(0f, maxX - minX);
            float deltaX = 0f;

            if (messageWidth > availableWidth)
            {
                float messageCenter = (left + right) * 0.5f;
                deltaX = rootRect.center.x - messageCenter;
            }
            else if (left < minX)
            {
                deltaX = minX - left;
            }
            else if (right > maxX)
            {
                deltaX = maxX - right;
            }

            return position + new Vector2(deltaX, 0f);
        }

        private void GetMessageLocalBounds(RectTransform messageRect, out float left, out float right)
        {
            messageRect.GetWorldCorners(messageCorners);

            left = float.PositiveInfinity;
            right = float.NegativeInfinity;

            for (int i = 0; i < messageCorners.Length; i++)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    spawnRootRect,
                    RectTransformUtility.WorldToScreenPoint(canvasCamera, messageCorners[i]),
                    canvasCamera,
                    out Vector2 localPoint);

                left = Mathf.Min(left, localPoint.x);
                right = Mathf.Max(right, localPoint.x);
            }
        }

        private void ForceLayoutUpdate(RectTransform messageRect)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(messageRect);
        }

        private IEnumerator Animate(GameObject messageObject, RectTransform messageRect, CanvasGroup canvasGroup)
        {
            Vector2 startPosition = messageRect.anchoredPosition;
            Vector2 endPosition = startPosition + new Vector2(0f, riseDistance);
            float elapsed = 0f;
            float safeDuration = Mathf.Max(0.01f, duration);

            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / safeDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                messageRect.anchoredPosition = Vector2.LerpUnclamped(startPosition, endPosition, eased);
                canvasGroup.alpha = 1f - t;
                yield return null;
            }

            Destroy(messageObject);
        }
    }
}
