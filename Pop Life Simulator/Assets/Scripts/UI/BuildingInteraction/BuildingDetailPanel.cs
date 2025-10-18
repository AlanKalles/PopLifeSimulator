using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using PopLife.Runtime;
using PopLife.Data;

namespace PopLife.UI.BuildingInteraction
{
    /// <summary>
    /// Building Detail Panel - Screen space UI panel showing detailed building information
    /// 建筑详情面板 - 屏幕空间UI面板，显示详细建筑信息
    /// </summary>
    public class BuildingDetailPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI categoryText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private TextMeshProUGUI stockText;
        [SerializeField] private TextMeshProUGUI attractivenessText;
        [SerializeField] private TextMeshProUGUI maintenanceText;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private TextMeshProUGUI upgradeButtonText;
        [SerializeField] private Button closeButton;

        [Header("Animation")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.2f;

        private CanvasGroup canvasGroup;
        private BuildingInstance currentBuilding;
        private DayLoopManager dayLoopManager;
        private ResourceManager resourceManager;

        private void Awake()
        {
            // Get or add CanvasGroup
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // Setup button listeners
            if (upgradeButton != null)
            {
                upgradeButton.onClick.AddListener(OnUpgradeClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(OnCloseClicked);
            }

            // Start hidden
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
            canvasGroup.alpha = 0f;
        }

        private void Start()
        {
            // Get managers
            dayLoopManager = DayLoopManager.Instance;
            resourceManager = ResourceManager.Instance;

            if (dayLoopManager == null)
            {
                Debug.LogWarning("BuildingDetailPanel: DayLoopManager not found!");
            }

            if (resourceManager == null)
            {
                Debug.LogWarning("BuildingDetailPanel: ResourceManager not found!");
            }
        }

        /// <summary>
        /// Show panel for a building
        /// 显示建筑详情面板
        /// </summary>
        public void Show(BuildingInstance building)
        {
            if (building == null)
            {
                Debug.LogWarning("BuildingDetailPanel: Null building!");
                return;
            }

            currentBuilding = building;

            // Ensure this GameObject is active (needed for coroutines)
            // 确保此 GameObject 是激活的（协程需要）
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            // Show panel root
            // 显示面板根对象
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            // Update content
            UpdateContent(building);

            // Fade in
            StopAllCoroutines();
            StartCoroutine(FadeIn());
        }

        /// <summary>
        /// Hide panel
        /// 隐藏详情面板
        /// </summary>
        public void Hide()
        {
            StopAllCoroutines();
            StartCoroutine(FadeOut());
        }

        /// <summary>
        /// Update panel content based on building
        /// 根据建筑更新面板内容
        /// </summary>
        private void UpdateContent(BuildingInstance building)
        {
            if (building == null || building.archetype == null)
            {
                return;
            }

            // Icon
            if (iconImage != null && building.archetype.icon != null)
            {
                iconImage.sprite = building.archetype.icon;
                iconImage.enabled = true;
            }
            else if (iconImage != null)
            {
                iconImage.enabled = false;
            }

            // Name
            if (nameText != null)
            {
                nameText.text = building.archetype.displayName;
            }

            // Category (for shelves)
            if (categoryText != null)
            {
                if (building is ShelfInstance shelf)
                {
                    ShelfArchetype sa = shelf.archetype as ShelfArchetype;
                    if (sa != null)
                    {
                        categoryText.text = $"Category: {sa.category}";
                        categoryText.gameObject.SetActive(true);
                    }
                    else
                    {
                        categoryText.gameObject.SetActive(false);
                    }
                }
                else
                {
                    categoryText.gameObject.SetActive(false);
                }
            }

            // Level
            if (levelText != null)
            {
                levelText.text = $"Level: {building.currentLevel}/{building.archetype.MaxLevel}";
            }

            // Price (for shelves)
            if (priceText != null)
            {
                if (building is ShelfInstance shelf)
                {
                    priceText.text = $"Price: ${shelf.currentPrice}";
                    priceText.gameObject.SetActive(true);
                }
                else
                {
                    priceText.gameObject.SetActive(false);
                }
            }

            // Stock (for shelves)
            if (stockText != null)
            {
                if (building is ShelfInstance shelf)
                {
                    stockText.text = $"Stock: {shelf.currentStock}/{shelf.maxStock}";
                    stockText.gameObject.SetActive(true);
                }
                else
                {
                    stockText.gameObject.SetActive(false);
                }
            }

            // Attractiveness (for shelves)
            if (attractivenessText != null)
            {
                if (building is ShelfInstance shelf)
                {
                    float attractiveness = shelf.GetAttractiveness();
                    attractivenessText.text = $"Attractiveness: {attractiveness:F1}";
                    attractivenessText.gameObject.SetActive(true);
                }
                else
                {
                    attractivenessText.gameObject.SetActive(false);
                }
            }

            // Maintenance fee
            if (maintenanceText != null)
            {
                var levelData = building.archetype.GetLevel(building.currentLevel);
                if (levelData != null)
                {
                    int maintenanceFee = levelData.maintenanceFee;
                    maintenanceText.text = $"Maintenance: ${maintenanceFee}/day";
                }
                else
                {
                    maintenanceText.text = "Maintenance: N/A";
                }
            }

            // Update upgrade button
            UpdateUpgradeButton();
        }

        /// <summary>
        /// Update upgrade button state
        /// 更新升级按钮状态
        /// </summary>
        private void UpdateUpgradeButton()
        {
            if (upgradeButton == null || currentBuilding == null)
            {
                return;
            }

            bool isMaxLevel = currentBuilding.currentLevel >= currentBuilding.archetype.MaxLevel;
            bool isBuildPhase = dayLoopManager != null && dayLoopManager.currentPhase == GamePhase.BuildPhase;

            // Check if can upgrade
            bool canUpgrade = isBuildPhase && !isMaxLevel;

            if (isMaxLevel)
            {
                // Max level
                upgradeButton.interactable = false;
                if (upgradeButtonText != null)
                {
                    upgradeButtonText.text = "Max Level";
                }
            }
            else if (!isBuildPhase)
            {
                // Not in build phase
                upgradeButton.interactable = false;
                if (upgradeButtonText != null)
                {
                    upgradeButtonText.text = "Upgrade (Build Phase Only)";
                }
            }
            else
            {
                // Can upgrade
                var nextLevel = currentBuilding.archetype.GetLevel(currentBuilding.currentLevel + 1);
                if (nextLevel != null)
                {
                    bool hasEnoughFame = resourceManager != null &&
                                        resourceManager.CanAfford(0, nextLevel.upgradeFameCost);

                    upgradeButton.interactable = hasEnoughFame;

                    if (upgradeButtonText != null)
                    {
                        upgradeButtonText.text = hasEnoughFame
                            ? $"Upgrade (Fame: {nextLevel.upgradeFameCost})"
                            : $"Upgrade (Need Fame: {nextLevel.upgradeFameCost})";
                    }
                }
                else
                {
                    upgradeButton.interactable = false;
                    if (upgradeButtonText != null)
                    {
                        upgradeButtonText.text = "Cannot Upgrade";
                    }
                }
            }
        }

        /// <summary>
        /// Handle upgrade button click
        /// 处理升级按钮点击
        /// </summary>
        private void OnUpgradeClicked()
        {
            if (currentBuilding == null)
            {
                return;
            }

            // Try to upgrade
            bool success = currentBuilding.TryUpgrade();

            if (success)
            {
                // Refresh panel
                UpdateContent(currentBuilding);

                // Show success feedback (you can add visual effects here)
                Debug.Log($"Building upgraded to level {currentBuilding.currentLevel}!");
            }
            else
            {
                // Show failure feedback
                Debug.Log("Upgrade failed! Not enough Fame or already at max level.");
            }
        }

        /// <summary>
        /// Handle close button click
        /// 处理关闭按钮点击
        /// </summary>
        private void OnCloseClicked()
        {
            Hide();
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

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        /// <summary>
        /// Update panel if visible (for real-time updates during Build Phase)
        /// 如果面板可见则更新（用于建造阶段的实时更新）
        /// </summary>
        private void Update()
        {
            if (panelRoot != null && panelRoot.activeSelf && currentBuilding != null)
            {
                // Update upgrade button state in case phase changed
                UpdateUpgradeButton();
            }

            // Close panel if ESC is pressed
            if (Input.GetKeyDown(KeyCode.Escape) && panelRoot != null && panelRoot.activeSelf)
            {
                Hide();
            }
        }
    }
}
