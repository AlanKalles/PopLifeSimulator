using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
using PopLife.Data;
using System.Collections;

namespace PopLife.UI
{
    /// <summary>
    /// Shelf tooltip window that appears on hover
    /// Shows detailed shelf information
    /// </summary>
    public class ShelfTooltip : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI categoryText;
        [FormerlySerializedAs("attractivenessText")]
        [SerializeField] private TextMeshProUGUI appealText;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private TextMeshProUGUI maintenanceFeeText;
        [SerializeField] private CanvasGroup canvasGroup; // For fade animation

        [Header("Color Settings")]
        [SerializeField] private Color affordableColor = Color.white;
        [SerializeField] private Color unaffordableColor = Color.red;

        [Header("Position Settings")]
        [SerializeField] private Vector3 positionOffset = new Vector3(10f, 50f, 0f);

        [Header("Animation Settings")]
        [SerializeField] private float fadeInDuration = 0.05f;
        [SerializeField] private float fadeOutDuration = 0.05f;

        private Coroutine fadeCoroutine;
        private RectTransform cachedRectTransform;

        private void Awake()
        {
            cachedRectTransform = GetComponent<RectTransform>();

            // Ensure canvas group exists
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            // Start invisible
            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Show tooltip with shelf information
        /// </summary>
        public void Show(ShelfArchetype shelf, Vector3 position)
        {
            if (shelf == null) return;

            // Update content
            if (iconImage != null && shelf.icon != null)
            {
                iconImage.sprite = shelf.icon;
            }

            if (nameText != null)
            {
                nameText.text = shelf.displayName;
            }

            if (costText != null)
            {
                costText.text = $"${shelf.buildCost}";

                // Color based on affordability
                if (ResourceManager.Instance != null)
                {
                    bool canAfford = ResourceManager.Instance.money >= shelf.buildCost;
                    costText.color = canAfford ? affordableColor : unaffordableColor;
                }
            }

            if (categoryText != null)
            {
                // Only show category name (without "Shelf" prefix) in uppercase
                categoryText.text = shelf.category.ToString().ToUpper();
            }

            // Get level 1 data for stats display
            var shelfLevel = shelf.GetShelfLevel(1);
            if (shelfLevel != null)
            {
                // Display appeal
                if (appealText != null)
                {
                    appealText.text = $"Appeal: {shelfLevel.appeal:F1}";
                }

                // Display price
                if (priceText != null)
                {
                    priceText.text = $"Unit Price: ${shelfLevel.price}";
                }

                // Display maintenance fee
                if (maintenanceFeeText != null)
                {
                    maintenanceFeeText.text = $"Maintenance: ${shelfLevel.maintenanceFee}/day";
                }
            }

            // 更新位置
            UpdatePosition(position);

            // Fade in
            gameObject.SetActive(true);

            // panelRoot.SetActive(true) 同步触发 OnPointerEnter 时，
            // 子物体的 activeInHierarchy 可能尚未生效，无法启动协程
            if (!gameObject.activeInHierarchy)
            {
                canvasGroup.alpha = 1f;
                return;
            }

            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            fadeCoroutine = StartCoroutine(FadeIn());
        }

        /// <summary>
        /// 仅更新 tooltip 位置，不改变内容和动画状态（用于鼠标跟随）
        /// </summary>
        public void UpdatePosition(Vector3 position)
        {
            if (cachedRectTransform == null) return;

            cachedRectTransform.position = position + positionOffset;
            ClampToScreen(cachedRectTransform);
        }

        /// <summary>
        /// Hide tooltip with fade out animation
        /// </summary>
        public void Hide()
        {
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            fadeCoroutine = StartCoroutine(FadeOut());
        }

        /// <summary>
        /// Clamp tooltip position to stay within screen bounds
        /// </summary>
        private void ClampToScreen(RectTransform rectTransform)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);

            // Get screen bounds
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            // Check if tooltip goes off screen
            Vector3 pos = rectTransform.position;

            // Right edge
            if (corners[2].x > screenWidth)
            {
                pos.x -= (corners[2].x - screenWidth);
            }

            // Left edge
            if (corners[0].x < 0)
            {
                pos.x -= corners[0].x;
            }

            // Top edge
            if (corners[1].y > screenHeight)
            {
                pos.y -= (corners[1].y - screenHeight);
            }

            // Bottom edge
            if (corners[0].y < 0)
            {
                pos.y -= corners[0].y;
            }

            rectTransform.position = pos;
        }

        /// <summary>
        /// Fade in animation
        /// </summary>
        private IEnumerator FadeIn()
        {
            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;

            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / fadeInDuration);
                yield return null;
            }

            canvasGroup.alpha = 1f;
            fadeCoroutine = null;
        }

        /// <summary>
        /// Fade out animation
        /// </summary>
        private IEnumerator FadeOut()
        {
            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeOutDuration);
                yield return null;
            }

            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
            fadeCoroutine = null;
        }

        /// <summary>
        /// Force hide immediately without animation
        /// </summary>
        public void HideImmediate()
        {
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }

            canvasGroup.alpha = 0f;

            // 将位置移至屏幕外，防止重开面板时闪现旧位置
            if (cachedRectTransform != null)
            {
                cachedRectTransform.position = new Vector3(-10000f, -10000f, 0f);
            }

            gameObject.SetActive(false);
        }
    }
}
