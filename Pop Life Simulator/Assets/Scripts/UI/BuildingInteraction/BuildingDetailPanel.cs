using UnityEngine;
using UnityEngine.Serialization;
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
        [FormerlySerializedAs("attractivenessText")]
        [SerializeField] private TextMeshProUGUI appealText;
        [SerializeField] private TextMeshProUGUI maintenanceText;

        [Header("Next Level Stats")]
        [SerializeField] private TextMeshProUGUI nextLevelPriceText;
        [SerializeField] private TextMeshProUGUI nextLevelStockText;
        [FormerlySerializedAs("nextLevelAttractivenessText")]
        [SerializeField] private TextMeshProUGUI nextLevelAppealText;
        [SerializeField] private TextMeshProUGUI nextLevelMaintenanceText;
        [SerializeField] private TextMeshProUGUI nextLevelUpgradeCostText; // 升级所需Fame
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
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
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

            // Appeal (for shelves)
            if (appealText != null)
            {
                if (building is ShelfInstance shelf)
                {
                    float appeal = shelf.GetAppeal();
                    appealText.text = $"Appeal: {appeal:F1}";
                    appealText.gameObject.SetActive(true);
                }
                else
                {
                    appealText.gameObject.SetActive(false);
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

            // Next level info - 下一级属性对比
            UpdateNextLevelInfo(building);

            // Update upgrade button
            UpdateUpgradeButton();
        }

        /// <summary>
        /// Update next level info display
        /// 更新下一级属性对比显示
        /// </summary>
        private void UpdateNextLevelInfo(BuildingInstance building)
        {
            if (building == null || building.archetype == null)
            {
                return;
            }

            bool isMaxLevel = building.currentLevel >= building.archetype.MaxLevel;

            // 获取下一等级数据
            var currentLevelData = building.archetype.GetLevel(building.currentLevel);
            var nextLevelData = building.archetype.GetLevel(building.currentLevel + 1);

            // Price (货架专属)
            if (nextLevelPriceText != null)
            {
                if (building is ShelfInstance shelf && building.archetype is ShelfArchetype shelfArch)
                {
                    if (isMaxLevel)
                    {
                        nextLevelPriceText.text = "MAX";
                    }
                    else
                    {
                        var currentShelfData = shelfArch.GetShelfLevel(building.currentLevel);
                        var nextShelfData = shelfArch.GetShelfLevel(building.currentLevel + 1);
                        if (currentShelfData != null && nextShelfData != null)
                        {
                            nextLevelPriceText.text = $"${currentShelfData.price} → ${nextShelfData.price}";
                        }
                    }
                    nextLevelPriceText.gameObject.SetActive(true);
                }
                else
                {
                    nextLevelPriceText.gameObject.SetActive(false);
                }
            }

            // Stock (货架专属)
            if (nextLevelStockText != null)
            {
                if (building is ShelfInstance shelf && building.archetype is ShelfArchetype shelfArch)
                {
                    if (isMaxLevel)
                    {
                        nextLevelStockText.text = "MAX";
                    }
                    else
                    {
                        var currentShelfData = shelfArch.GetShelfLevel(building.currentLevel);
                        var nextShelfData = shelfArch.GetShelfLevel(building.currentLevel + 1);
                        if (currentShelfData != null && nextShelfData != null)
                        {
                            nextLevelStockText.text = $"{currentShelfData.maxStock} → {nextShelfData.maxStock}";
                        }
                    }
                    nextLevelStockText.gameObject.SetActive(true);
                }
                else
                {
                    nextLevelStockText.gameObject.SetActive(false);
                }
            }

            // Appeal (货架专属)
            if (nextLevelAppealText != null)
            {
                if (building is ShelfInstance shelf && building.archetype is ShelfArchetype shelfArch)
                {
                    if (isMaxLevel)
                    {
                        nextLevelAppealText.text = "MAX";
                    }
                    else
                    {
                        var currentShelfData = shelfArch.GetShelfLevel(building.currentLevel);
                        var nextShelfData = shelfArch.GetShelfLevel(building.currentLevel + 1);
                        if (currentShelfData != null && nextShelfData != null)
                        {
                            nextLevelAppealText.text = $"{currentShelfData.appeal:F1} → {nextShelfData.appeal:F1}";
                        }
                    }
                    nextLevelAppealText.gameObject.SetActive(true);
                }
                else
                {
                    nextLevelAppealText.gameObject.SetActive(false);
                }
            }

            // Maintenance (所有建筑都有)
            if (nextLevelMaintenanceText != null)
            {
                if (isMaxLevel)
                {
                    nextLevelMaintenanceText.text = "MAX";
                }
                else if (currentLevelData != null && nextLevelData != null)
                {
                    nextLevelMaintenanceText.text = $"${currentLevelData.maintenanceFee} → ${nextLevelData.maintenanceFee}";
                }
                else
                {
                    nextLevelMaintenanceText.gameObject.SetActive(false);
                    return;
                }
                nextLevelMaintenanceText.gameObject.SetActive(true);
            }

            // Upgrade Cost (升级所需Fame)
            if (nextLevelUpgradeCostText != null)
            {
                if (isMaxLevel)
                {
                    nextLevelUpgradeCostText.text = "MAX";
                    nextLevelUpgradeCostText.gameObject.SetActive(true);
                }
                else if (nextLevelData != null)
                {
                    // 检查是否有足够的Fame
                    bool hasEnoughFame = resourceManager != null &&
                                        resourceManager.CanAfford(0, nextLevelData.upgradeFameCost);

                    // 根据是否足够设置颜色
                    string colorTag = hasEnoughFame ? "<color=#00FF00>" : "<color=#FF6B6B>";
                    nextLevelUpgradeCostText.text = $"Upgrade Cost: {colorTag}{nextLevelData.upgradeFameCost} Fame</color>";
                    nextLevelUpgradeCostText.gameObject.SetActive(true);
                }
                else
                {
                    nextLevelUpgradeCostText.gameObject.SetActive(false);
                }
            }
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

            // 检查是否已达到最高等级
            if (currentBuilding.currentLevel >= currentBuilding.archetype.MaxLevel)
            {
                Debug.Log("Already at max level!");
                return;
            }

            // 获取下一等级数据
            var nextLevel = currentBuilding.archetype.GetLevel(currentBuilding.currentLevel + 1);
            if (nextLevel == null)
            {
                Debug.LogWarning("Cannot get next level data!");
                return;
            }

            // 检查声望是否足够
            if (!resourceManager.CanAfford(0, nextLevel.upgradeFameCost))
            {
                // 声望不足 - 显示警告弹窗
                UIManager.Instance.ShowAlert(AlertType.NotEnoughFame);
                return;
            }

            // 尝试升级
            bool success = currentBuilding.TryUpgrade();

            if (success)
            {
                // 升级成功 - 刷新面板
                UpdateContent(currentBuilding);
                Debug.Log($"Building upgraded to level {currentBuilding.currentLevel}!");
            }
            else
            {
                // 升级失败（理论上不应该发生，因为已经检查过资源）
                Debug.LogWarning("Upgrade failed unexpectedly!");
            }
        }

        /// <summary>
        /// Handle close button click
        /// 处理关闭按钮点击
        /// </summary>
        private void OnCloseClicked()
        {
            Debug.Log("Close button clicked!"); // 调试日志
            Hide();
        }

        /// <summary>
        /// Fade in animation
        /// 淡入动画
        /// </summary>
        private IEnumerator FadeIn()
        {
            // Enable interaction BEFORE animation
            // 在动画前启用交互
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

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
            // Disable interaction immediately when closing
            // 关闭时立即禁用交互
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

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
            // Check if building was destroyed (Unity's null check)
            // 检查建筑是否已被销毁（Unity的空值检查）
            if (currentBuilding != null && !currentBuilding)
            {
                Hide();
                currentBuilding = null;
                return;
            }

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
