using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using PopLife.Runtime;

namespace PopLife.UI.BuildingInteraction
{
    /// <summary>
    /// Building Info Bubble - World space canvas that displays brief building information
    /// 建筑信息气泡 - 世界空间画布，显示建筑简要信息
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasGroup))]
    public class BuildingInfoBubble : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI detailsText;
        [SerializeField] private Image iconImage;

        [Header("Display Settings")]
        [SerializeField] private float displayDuration = 1.5f;
        [SerializeField] private float fadeInDuration = 0.2f;
        [SerializeField] private float fadeOutDuration = 0.5f;
        [SerializeField] private float verticalOffset = 1.5f;

        private Canvas canvas;
        private CanvasGroup canvasGroup;
        private Camera mainCamera;
        private BuildingInstance currentBuilding;
        private Coroutine displayCoroutine;
        private System.Action onComplete;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            canvasGroup = GetComponent<CanvasGroup>();

            // Initialize canvas with camera configuration
            InitializeCanvas();

            // Set initial alpha to 0
            canvasGroup.alpha = 0f;

            // Find UI components if not assigned
            if (nameText == null)
            {
                nameText = transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            }
            if (detailsText == null)
            {
                detailsText = transform.Find("DetailsText")?.GetComponent<TextMeshProUGUI>();
            }
            if (iconImage == null)
            {
                iconImage = transform.Find("IconImage")?.GetComponent<Image>();
            }
        }

        /// <summary>
        /// Initialize or reinitialize canvas with main camera
        /// 初始化或重新初始化Canvas相机配置
        /// </summary>
        private void InitializeCanvas()
        {
            // Get or update camera reference
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    Debug.LogError("BuildingInfoBubble: No main camera found!");
                    return;
                }
            }

            // Configure canvas as world space
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = mainCamera;
            canvas.sortingLayerName = "UI";
            canvas.sortingOrder = 200;
        }

        /// <summary>
        /// Ensure camera reference is valid when reactivated from object pool
        /// 从对象池重新激活时确保相机引用有效
        /// </summary>
        private void OnEnable()
        {
            // Validate camera reference when reactivated from pool
            if (canvas != null && canvas.worldCamera == null && mainCamera != null)
            {
                canvas.worldCamera = mainCamera;
            }
        }

        /// <summary>
        /// Show bubble for a building
        /// 显示建筑气泡
        /// </summary>
        public void Show(BuildingInstance building, System.Action onCompleteCallback = null)
        {
            if (building == null)
            {
                Debug.LogWarning("BuildingInfoBubble: Null building!");
                return;
            }

            currentBuilding = building;
            onComplete = onCompleteCallback;

            // Position above building
            Vector3 worldPos = building.transform.position + Vector3.up * verticalOffset;
            transform.position = worldPos;

            // Update content
            UpdateContent(building);

            // Stop any existing display coroutine
            if (displayCoroutine != null)
            {
                StopCoroutine(displayCoroutine);
            }

            // Start display sequence
            displayCoroutine = StartCoroutine(DisplaySequence());
        }

        /// <summary>
        /// Hide bubble immediately
        /// 立即隐藏气泡
        /// </summary>
        public void Hide()
        {
            if (displayCoroutine != null)
            {
                StopCoroutine(displayCoroutine);
                displayCoroutine = null;
            }

            canvasGroup.alpha = 0f;

            // Call completion callback (for object pooling)
            onComplete?.Invoke();
            onComplete = null;
        }

        /// <summary>
        /// Update bubble content based on building type
        /// 根据建筑类型更新气泡内容
        /// </summary>
        private void UpdateContent(BuildingInstance building)
        {
            if (building == null || building.archetype == null)
            {
                return;
            }

            // Set name and level
            if (nameText != null)
            {
                nameText.text = $"{building.archetype.displayName} Lv.{building.currentLevel}";
            }

            // Set details (different for shelves and facilities)
            if (detailsText != null)
            {
                if (building is ShelfInstance shelf)
                {
                    detailsText.text = $"Stock: {shelf.currentStock}/{shelf.maxStock}";
                }
                else if (building is FacilityInstance facility)
                {
                    detailsText.text = GetFacilityDescription(facility);
                }
                else
                {
                    detailsText.text = "";
                }
            }

            // Set icon (if available)
            if (iconImage != null && building.archetype.icon != null)
            {
                iconImage.sprite = building.archetype.icon;
                iconImage.enabled = true;
            }
            else if (iconImage != null)
            {
                iconImage.enabled = false;
            }
        }

        /// <summary>
        /// Get facility description
        /// 获取设施描述
        /// </summary>
        private string GetFacilityDescription(FacilityInstance facility)
        {
            // You can customize this based on facility type
            // 可以根据设施类型自定义
            return $"Facility Level {facility.currentLevel}";
        }

        /// <summary>
        /// Display sequence: Fade in -> Hold -> Fade out -> Return to pool
        /// 显示序列：淡入 -> 保持 -> 淡出 -> 返回对象池
        /// </summary>
        private IEnumerator DisplaySequence()
        {
            // Fade in
            yield return FadeIn();

            // Hold
            float holdDuration = displayDuration - fadeInDuration;
            yield return new WaitForSeconds(holdDuration);

            // Fade out
            yield return FadeOut();

            // Call completion callback (for object pooling)
            onComplete?.Invoke();
            onComplete = null;
        }

        /// <summary>
        /// Fade in animation
        /// 淡入动画
        /// </summary>
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

        /// <summary>
        /// Fade out animation
        /// 淡出动画
        /// </summary>
        private IEnumerator FadeOut()
        {
            float elapsed = 0f;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
                yield return null;
            }

            canvasGroup.alpha = 0f;
        }

        /// <summary>
        /// Update bubble position to follow building
        /// 更新气泡位置以跟随建筑
        /// </summary>
        private void LateUpdate()
        {
            // Keep bubble above building
            if (currentBuilding != null)
            {
                Vector3 worldPos = currentBuilding.transform.position + Vector3.up * verticalOffset;
                transform.position = worldPos;

                // Face camera (use cached reference)
                if (mainCamera != null)
                {
                    transform.rotation = mainCamera.transform.rotation;
                }
            }
        }
    }
}
