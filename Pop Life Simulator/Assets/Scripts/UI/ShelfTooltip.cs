using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
using PopLife.Data;
using System.Collections;
using System.Collections.Generic;

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
        [FormerlySerializedAs("appealText")]
        [SerializeField] private TextMeshProUGUI appealValueText;
        [FormerlySerializedAs("priceText")]
        [SerializeField] private TextMeshProUGUI priceValueText;
        [FormerlySerializedAs("stockFeeText")]
        [SerializeField] private TextMeshProUGUI stockFeeValueText; // 每件补货成本（可选，Prefab 没配置时跳过）
        [FormerlySerializedAs("maintenanceFeeText")]
        [SerializeField] private TextMeshProUGUI maintenanceFeeValueText;
        [SerializeField] private Image brandIconImage;
        [SerializeField] private TextMeshProUGUI brandNameText;
        [SerializeField] private CanvasGroup canvasGroup; // For fade animation

        [Header("Grid Size Display")]
        [Tooltip("Bind 16 cells from bottom-left to top-right. Index = y * 4 + x, so 0=(0,0), 1=(1,0), 4=(0,1).")]
        [SerializeField] private Image[] gridSizeCells = new Image[16];
        [SerializeField] private Color gridEmptyColor = Color.white;
        [SerializeField] private Color gridOccupiedColor = new Color(0.3f, 0.6f, 1f, 1f);

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
        private RectTransform parentRectTransform;
        private Camera cachedCamera;

        private void Awake()
        {
            cachedRectTransform = GetComponent<RectTransform>();
            ApplyIconImageSettings();

            // 缓存 Canvas 引用，用于 ScreenPointToLocalPointInRectangle
            var rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            parentRectTransform = transform.parent as RectTransform;
            if (rootCanvas != null)
            {
                cachedCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null : rootCanvas.worldCamera;
            }

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

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyIconImageSettings();
        }
#endif

        /// <summary>
        /// Show tooltip with shelf information
        /// </summary>
        public void Show(ShelfArchetype shelf, Vector3 position)
        {
            if (shelf == null) return;

            // Update content
            UpdateGridSizeDisplay(shelf);

            if (iconImage != null)
            {
                ApplyIconImageSettings();
                Sprite iconSprite = GetShelfPrefabSprite(shelf) ?? shelf.icon;
                iconImage.sprite = iconSprite;
                iconImage.enabled = iconSprite != null;
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

            // 品牌信息
            if (brandNameText != null)
            {
                brandNameText.text = shelf.brand != null ? shelf.brand.displayName : "";
            }
            if (brandIconImage != null)
            {
                if (shelf.brand != null && shelf.brand.icon != null)
                {
                    brandIconImage.sprite = shelf.brand.icon;
                    brandIconImage.enabled = true;
                }
                else
                {
                    brandIconImage.enabled = false;
                }
            }

            // Get level 1 data for stats display
            var shelfLevel = shelf.GetShelfLevel(1);
            if (shelfLevel != null)
            {
                // Display appeal
                if (appealValueText != null)
                {
                    appealValueText.text = $"{shelfLevel.appeal:F1}";
                }

                // Display price
                if (priceValueText != null)
                {
                    priceValueText.text = $"${shelfLevel.price}";
                }

                // Display per-unit restock cost
                if (stockFeeValueText != null)
                {
                    stockFeeValueText.text = $"${shelfLevel.stockFee}/unit";
                }

                // Display maintenance fee
                if (maintenanceFeeValueText != null)
                {
                    maintenanceFeeValueText.text = $"${shelfLevel.maintenanceFee}/day";
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

        private void UpdateGridSizeDisplay(ShelfArchetype shelf)
        {
            if (gridSizeCells == null || gridSizeCells.Length == 0) return;

            var occupiedCells = new HashSet<Vector2Int>(shelf.GetRotatedFootprint(0));

            for (int i = 0; i < gridSizeCells.Length; i++)
            {
                var cellImage = gridSizeCells[i];
                if (cellImage == null) continue;

                int x = i % 4;
                int y = i / 4;
                cellImage.color = occupiedCells.Contains(new Vector2Int(x, y))
                    ? gridOccupiedColor
                    : gridEmptyColor;
            }
        }

        private static Sprite GetShelfPrefabSprite(ShelfArchetype shelf)
        {
            if (shelf == null || shelf.prefab == null) return null;

            var spriteRenderer = shelf.prefab.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = shelf.prefab.GetComponentInChildren<SpriteRenderer>();
            }

            return spriteRenderer != null ? spriteRenderer.sprite : null;
        }

        private void ApplyIconImageSettings()
        {
            if (iconImage == null) return;

            iconImage.preserveAspect = true;
        }

        /// <summary>
        /// 仅更新 tooltip 位置，不改变内容和动画状态（用于鼠标跟随）
        /// </summary>
        public void UpdatePosition(Vector3 screenPosition)
        {
            if (cachedRectTransform == null || parentRectTransform == null) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRectTransform, (Vector2)screenPosition, cachedCamera, out Vector2 localPoint))
            {
                cachedRectTransform.localPosition = localPoint + (Vector2)positionOffset;
            }
            ClampToScreen();
        }

        /// <summary>
        /// Hide tooltip with fade out animation
        /// </summary>
        public void Hide()
        {
            if (!gameObject.activeInHierarchy)
            {
                // GO已经inactive，无法启动协程，也无需淡出
                fadeCoroutine = null;
                return;
            }
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            fadeCoroutine = StartCoroutine(FadeOut());
        }

        /// <summary>
        /// Clamp tooltip position to stay within screen bounds
        /// </summary>
        private void ClampToScreen()
        {
            if (parentRectTransform == null) return;

            Vector3[] corners = new Vector3[4];
            cachedRectTransform.GetWorldCorners(corners);

            // Overlay 模式 world == screen；Camera 模式需转换
            Vector2 bl = cachedCamera != null
                ? (Vector2)cachedCamera.WorldToScreenPoint(corners[0])
                : (Vector2)corners[0];
            Vector2 tr = cachedCamera != null
                ? (Vector2)cachedCamera.WorldToScreenPoint(corners[2])
                : (Vector2)corners[2];

            float sw = Screen.width;
            float sh = Screen.height;
            Vector2 delta = Vector2.zero;

            if (tr.x > sw) delta.x = sw - tr.x;
            if (bl.x < 0) delta.x = -bl.x;
            if (tr.y > sh) delta.y = sh - tr.y;
            if (bl.y < 0) delta.y = -bl.y;

            if (delta.sqrMagnitude < 0.01f) return;

            // 将当前屏幕位置加上偏移量，再转回本地坐标
            Vector2 currentScreen = cachedCamera != null
                ? (Vector2)cachedCamera.WorldToScreenPoint(cachedRectTransform.position)
                : (Vector2)cachedRectTransform.position;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRectTransform, currentScreen + delta, cachedCamera, out Vector2 newLocal))
            {
                cachedRectTransform.localPosition = newLocal;
            }
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
