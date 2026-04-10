using UnityEngine;
using UnityEngine.EventSystems;
using PopLife.Runtime;
using PopLife.Services;

namespace PopLife.AlanBot
{
    /// <summary>
    /// AlanBot建造阶段拖放
    /// 仅在BuildPhase + ConstructionManager.mode == Move时激活
    /// 使用FloorDetectionService检测鼠标所在地板瓦片
    /// 拖动时验证 InteriorGrid walkable cell，吸附到格子中心
    /// </summary>
    public class AlanBotPlacementHandler : MonoBehaviour
    {
        [Header("层检测")]
        [SerializeField] private LayerMask alanBotMask;

        [Header("虚影设置")]
        [SerializeField] private float ghostAlpha = 0.5f;
        [SerializeField] private Color invalidTint = new Color(1f, 0.5f, 0.5f, 1f);

        private AlanBotController controller;
        private SpriteRenderer spriteRenderer;
        private Camera mainCamera;
        private ConstructionManager constructionManager;
        private FloorDetectionService floorDetector;

        private bool isDragging;
        private Vector3 originalPosition;
        private FloorTileInstance currentTargetTile;
        private bool currentCellWalkable;
        private Vector2Int currentLocalPos;

        private void Awake()
        {
            controller = GetComponent<AlanBotController>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            mainCamera = Camera.main;
        }

        private void Start()
        {
            constructionManager = FindAnyObjectByType<ConstructionManager>();

            // 每帧检测（interval=1），拖放时需要即时响应
            if (mainCamera != null)
                floorDetector = new FloorDetectionService(mainCamera, 1);
        }

        private void Update()
        {
            // 仅在 BuildPhase + Move模式 时激活
            if (!IsPlacementActive())
            {
                if (isDragging) CancelDrag();
                return;
            }

            if (isDragging)
            {
                UpdateDrag();
                HandleDragInput();
            }
            else
            {
                DetectClick();
            }
        }

        private bool IsPlacementActive()
        {
            if (DayLoopManager.Instance == null) return false;
            if (DayLoopManager.Instance.currentPhase != GamePhase.BuildPhase) return false;
            if (constructionManager == null) return false;
            if (constructionManager.mode != ConstructionManager.Mode.Move) return false;
            return true;
        }

        // ─── 点击检测 ───

        private void DetectClick()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0f;

            RaycastHit2D hit = Physics2D.Raycast(mouseWorld, Vector2.zero, Mathf.Infinity, alanBotMask);
            if (hit.collider == null) return;
            if (hit.collider.gameObject != gameObject) return;

            BeginDrag();
        }

        // ─── 拖动逻辑 ───

        private void BeginDrag()
        {
            isDragging = true;
            originalPosition = transform.position;
            controller.isBeingMoved = true;
            controller.PauseBehavior();

            // 进入虚影状态
            SetGhostColor(true);
        }

        private void UpdateDrag()
        {
            Vector3 mouse = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mouse.z = 0f;

            currentTargetTile = floorDetector?.DetectFloorTileAtMouse();
            currentCellWalkable = false;

            if (currentTargetTile != null && currentTargetTile.Interior != null)
            {
                var interior = currentTargetTile.Interior;
                currentLocalPos = interior.WorldToLocal(mouse);

                // 验证：InBounds + IsWalkable + 未被建筑占用
                currentCellWalkable = interior.InBounds(currentLocalPos)
                                   && interior.IsWalkable(currentLocalPos)
                                   && !interior.IsOccupied(currentLocalPos);

                if (currentCellWalkable)
                {
                    // 吸附到格子中心
                    Vector3 snapped = interior.LocalToWorld(currentLocalPos);
                    float half = interior.CellSize * 0.5f;
                    transform.position = new Vector3(snapped.x + half, snapped.y + half, 0f);
                }
                else
                {
                    // 无效位置：自由跟随鼠标，便于拖到其他格子
                    transform.position = mouse;
                }
            }
            else
            {
                transform.position = mouse;
            }

            SetGhostColor(currentCellWalkable);
        }

        private void HandleDragInput()
        {
            // 左键确认放置（仅 walkable 且未占用的格子）
            if (Input.GetMouseButtonDown(0))
            {
                if (currentTargetTile != null && currentCellWalkable)
                    ConfirmPlacement();
                // 无效位置不做处理，继续拖动
            }

            // 右键或ESC取消
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                CancelDrag();
            }
        }

        private void ConfirmPlacement()
        {
            isDragging = false;

            if (currentTargetTile != null && currentTargetTile.Interior != null)
            {
                var interior = currentTargetTile.Interior;
                Vector3 snapped = interior.LocalToWorld(currentLocalPos);
                float half = interior.CellSize * 0.5f;

                // 根节点在 cell 中心
                transform.position = new Vector3(snapped.x + half, snapped.y + half, 0f);
                controller.SetHostFloorTile(currentTargetTile, currentLocalPos);
            }

            RestoreColor();
            floorDetector?.ResetCache();
            controller.OnPlacementComplete();

            // 安全检查：如果放置完成时已进入营业阶段，恢复行为树
            if (DayLoopManager.Instance != null &&
                DayLoopManager.Instance.currentPhase == GamePhase.OpenPhase)
            {
                controller.ResumeBehavior();
            }
        }

        private void CancelDrag()
        {
            isDragging = false;
            transform.position = originalPosition;
            controller.isBeingMoved = false;

            RestoreColor();
            floorDetector?.ResetCache();

            // 安全检查：如果取消拖放时已进入营业阶段，恢复行为树
            if (DayLoopManager.Instance != null &&
                DayLoopManager.Instance.currentPhase == GamePhase.OpenPhase)
            {
                controller.ResumeBehavior();
            }
        }

        // ─── 虚影 & 颜色 ───

        private void SetGhostColor(bool isValid)
        {
            if (spriteRenderer == null) return;
            Color baseColor = isValid ? Color.white : invalidTint;
            baseColor.a = ghostAlpha;
            spriteRenderer.color = baseColor;
        }

        private void RestoreColor()
        {
            if (spriteRenderer != null)
                spriteRenderer.color = Color.white;
        }
    }
}
