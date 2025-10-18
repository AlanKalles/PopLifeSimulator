using UnityEngine;
using PopLife.Runtime;

namespace PopLife.UI.BuildingInteraction
{
    /// <summary>
    /// Building Interaction Manager - Handles mouse interactions with buildings
    /// 建筑交互管理器 - 处理建筑物的鼠标交互
    /// </summary>
    public class BuildingInteractionManager : MonoBehaviour
    {
        public static BuildingInteractionManager Instance { get; private set; }

        [Header("Components")]
        [SerializeField] private BuildingHighlighter highlighter;
        [SerializeField] private GameObject bubblePrefab;
        [SerializeField] private BuildingDetailPanel detailPanel;

        [Header("Settings")]
        [SerializeField] private LayerMask buildingLayerMask = 1 << 8; // Default to layer 8 (interactableShelf)
        [SerializeField] private float doubleClickWindow = 2f;

        [Header("Object Pooling")]
        [SerializeField] private int bubblePoolSize = 5;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // State tracking
        private BuildingInstance currentHoveredBuilding;
        private BuildingInstance lastClickedBuilding;
        private float lastClickTime;
        private Vector3 lastMousePosition;
        private BuildingInfoBubble currentBubble;

        // Object pool
        private System.Collections.Generic.Queue<BuildingInfoBubble> bubblePool;
        private System.Collections.Generic.HashSet<BuildingInfoBubble> activeBubbles;

        // Cached references
        private Camera mainCamera;
        private ConstructionManager constructionManager;

        private void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Initialize object pool
            bubblePool = new System.Collections.Generic.Queue<BuildingInfoBubble>();
            activeBubbles = new System.Collections.Generic.HashSet<BuildingInfoBubble>();

            // Cache references
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("BuildingInteractionManager: No main camera found!");
            }
        }

        private void Start()
        {
            // Get ConstructionManager reference
            constructionManager = FindFirstObjectByType<ConstructionManager>();
            if (constructionManager == null)
            {
                Debug.LogWarning("BuildingInteractionManager: ConstructionManager not found!");
            }

            // Validate components
            if (highlighter == null)
            {
                Debug.LogError("BuildingInteractionManager: BuildingHighlighter not assigned!");
            }

            if (detailPanel == null)
            {
                Debug.LogWarning("BuildingInteractionManager: BuildingDetailPanel not assigned!");
            }
        }

        private void Update()
        {
            if (!CanInteract())
            {
                // Hide interactions if construction mode is active
                if (currentHoveredBuilding != null)
                {
                    OnHoveredBuildingChanged(null);
                }
                return;
            }

            UpdateHoverDetection();
            UpdateClickDetection();
            ValidateBuildingReferences();
        }

        /// <summary>
        /// Check if interaction is allowed
        /// 检查是否允许交互
        /// </summary>
        private bool CanInteract()
        {
            // Disable interaction when ConstructionManager is in Place/Move mode
            if (constructionManager != null && constructionManager.mode != ConstructionManager.Mode.None)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Update hover detection (only when mouse moves)
        /// 更新悬停检测（仅当鼠标移动时）
        /// </summary>
        private void UpdateHoverDetection()
        {
            Vector3 currentMousePos = Input.mousePosition;

            // Only check when mouse moves (optimization)
            if (currentMousePos != lastMousePosition)
            {
                BuildingInstance hovered = GetBuildingUnderMouse();

                if (hovered != currentHoveredBuilding)
                {
                    OnHoveredBuildingChanged(hovered);
                }

                lastMousePosition = currentMousePos;
            }
        }

        /// <summary>
        /// Update click detection
        /// 更新点击检测
        /// </summary>
        private void UpdateClickDetection()
        {
            if (Input.GetMouseButtonDown(0))
            {
                BuildingInstance clicked = GetBuildingUnderMouse();

                if (clicked != null)
                {
                    OnBuildingClicked(clicked);
                }
            }
        }

        /// <summary>
        /// Validate building references (handle destroyed buildings)
        /// 验证建筑引用（处理被销毁的建筑）
        /// </summary>
        private void ValidateBuildingReferences()
        {
            // Check if hovered building was destroyed
            if (currentHoveredBuilding != null && currentHoveredBuilding == null)
            {
                if (highlighter != null)
                {
                    highlighter.Hide();
                }
                currentHoveredBuilding = null;
            }

            // Check if last clicked building was destroyed
            if (lastClickedBuilding != null && lastClickedBuilding == null)
            {
                HideBubble();
                HideDetailPanel();
                lastClickedBuilding = null;
            }
        }

        /// <summary>
        /// Get building under mouse cursor using raycast
        /// 使用 Raycast 获取鼠标下的建筑
        /// </summary>
        private BuildingInstance GetBuildingUnderMouse()
        {
            if (mainCamera == null) return null;

            Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0; // Ensure Z is 0 for 2D

            RaycastHit2D hit = Physics2D.Raycast(
                mouseWorld,
                Vector2.zero,
                Mathf.Infinity,
                buildingLayerMask
            );

            if (hit.collider != null)
            {
                BuildingInstance building = hit.collider.GetComponent<BuildingInstance>();
                if (building != null && enableDebugLogs)
                {
                    Debug.Log($"Building detected: {building.archetype.displayName}");
                }
                return building;
            }

            return null;
        }

        /// <summary>
        /// Handle hovered building change
        /// 处理悬停建筑变化
        /// </summary>
        private void OnHoveredBuildingChanged(BuildingInstance newHovered)
        {
            // Hide previous outline
            if (currentHoveredBuilding != null && highlighter != null)
            {
                highlighter.Hide();
            }

            // Show new outline
            if (newHovered != null && highlighter != null)
            {
                highlighter.Show(newHovered);
            }

            currentHoveredBuilding = newHovered;

            if (enableDebugLogs)
            {
                Debug.Log($"Hovered building changed: {(newHovered != null ? newHovered.archetype.displayName : "None")}");
            }
        }

        /// <summary>
        /// Handle building click (single or double click)
        /// 处理建筑点击（单击或双击）
        /// </summary>
        private void OnBuildingClicked(BuildingInstance building)
        {
            float currentTime = Time.time;

            // Check if double-click (same building within time window)
            if (lastClickedBuilding == building &&
                currentTime - lastClickTime < doubleClickWindow)
            {
                // Double-click: Show detail panel
                HideBubble();
                ShowDetailPanel(building);

                if (enableDebugLogs)
                {
                    Debug.Log($"Double-clicked: {building.archetype.displayName}");
                }
            }
            else
            {
                // Single-click: Show bubble
                ShowBubble(building);

                if (enableDebugLogs)
                {
                    Debug.Log($"Single-clicked: {building.archetype.displayName}");
                }
            }

            // Update last click info
            lastClickedBuilding = building;
            lastClickTime = currentTime;
        }

        /// <summary>
        /// Show info bubble for building
        /// 显示建筑信息气泡
        /// </summary>
        private void ShowBubble(BuildingInstance building)
        {
            // Hide existing bubble
            HideBubble();

            if (bubblePrefab == null)
            {
                Debug.LogWarning("BuildingInteractionManager: Bubble prefab not assigned!");
                return;
            }

            // Get bubble from pool
            currentBubble = GetBubbleFromPool();

            if (currentBubble != null)
            {
                currentBubble.Show(building, () => ReturnBubbleToPool(currentBubble));
            }
        }

        /// <summary>
        /// Hide info bubble
        /// 隐藏信息气泡
        /// </summary>
        private void HideBubble()
        {
            if (currentBubble != null)
            {
                currentBubble.Hide();
                currentBubble = null;
            }
        }

        /// <summary>
        /// Get bubble from object pool
        /// 从对象池获取气泡
        /// </summary>
        private BuildingInfoBubble GetBubbleFromPool()
        {
            BuildingInfoBubble bubble;

            // Try to get from pool
            if (bubblePool.Count > 0)
            {
                bubble = bubblePool.Dequeue();
                bubble.gameObject.SetActive(true);

                if (enableDebugLogs)
                {
                    Debug.Log("Bubble retrieved from pool");
                }
            }
            else
            {
                // Create new bubble
                GameObject bubbleObj = Instantiate(bubblePrefab);
                bubble = bubbleObj.GetComponent<BuildingInfoBubble>();

                if (bubble == null)
                {
                    Debug.LogError("BuildingInteractionManager: Bubble prefab doesn't have BuildingInfoBubble component!");
                    Destroy(bubbleObj);
                    return null;
                }

                if (enableDebugLogs)
                {
                    Debug.Log("New bubble created");
                }
            }

            activeBubbles.Add(bubble);
            return bubble;
        }

        /// <summary>
        /// Return bubble to object pool
        /// 将气泡返回对象池
        /// </summary>
        private void ReturnBubbleToPool(BuildingInfoBubble bubble)
        {
            if (bubble == null) return;

            activeBubbles.Remove(bubble);

            // Check pool size limit
            if (bubblePool.Count < bubblePoolSize)
            {
                bubble.gameObject.SetActive(false);
                bubblePool.Enqueue(bubble);

                if (enableDebugLogs)
                {
                    Debug.Log("Bubble returned to pool");
                }
            }
            else
            {
                // Pool is full, destroy the bubble
                Destroy(bubble.gameObject);

                if (enableDebugLogs)
                {
                    Debug.Log("Bubble destroyed (pool full)");
                }
            }
        }

        /// <summary>
        /// Show detail panel for building
        /// 显示建筑详情面板
        /// </summary>
        private void ShowDetailPanel(BuildingInstance building)
        {
            if (detailPanel == null)
            {
                Debug.LogWarning("BuildingInteractionManager: Detail panel not assigned!");
                return;
            }

            detailPanel.Show(building);
        }

        /// <summary>
        /// Hide detail panel
        /// 隐藏详情面板
        /// </summary>
        private void HideDetailPanel()
        {
            if (detailPanel != null)
            {
                detailPanel.Hide();
            }
        }

        private void OnDestroy()
        {
            // Clean up object pool
            ClearObjectPool();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Clear object pool and destroy all pooled bubbles
        /// 清理对象池并销毁所有池化的气泡
        /// </summary>
        private void ClearObjectPool()
        {
            // Destroy active bubbles
            if (activeBubbles != null)
            {
                foreach (var bubble in activeBubbles)
                {
                    if (bubble != null)
                    {
                        Destroy(bubble.gameObject);
                    }
                }
                activeBubbles.Clear();
            }

            // Destroy pooled bubbles
            if (bubblePool != null)
            {
                while (bubblePool.Count > 0)
                {
                    var bubble = bubblePool.Dequeue();
                    if (bubble != null)
                    {
                        Destroy(bubble.gameObject);
                    }
                }
            }
        }

        // Public API for external control
        public void ForceHideAll()
        {
            OnHoveredBuildingChanged(null);
            HideBubble();
            HideDetailPanel();
        }

        /// <summary>
        /// Enable or disable interaction system
        /// 启用或禁用交互系统
        /// </summary>
        public void SetInteractionEnabled(bool enabled)
        {
            this.enabled = enabled;

            if (!enabled)
            {
                ForceHideAll();
            }
        }
    }
}
