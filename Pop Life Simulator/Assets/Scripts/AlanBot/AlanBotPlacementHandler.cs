using UnityEngine;
using UnityEngine.EventSystems;
using PopLife.Runtime;
using PopLife.Services;

namespace PopLife.AlanBot
{
    /// <summary>
    /// AlanBot建造阶段拖放
    /// 仅在BuildPhase + ConstructionManager.mode == Move时激活
    /// AlanBot不占格子，使用FloorDetectionService检测鼠标所在楼层
    /// 拖动时为虚影状态自由跟随鼠标，确认放置后锚定到地面
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
        private FloorManager floorManager;
        private ConstructionManager constructionManager;
        private FloorDetectionService floorDetector; // 与ConstructionManager相同的楼层检测方式

        private bool isDragging;
        private Vector3 originalPosition;
        private FloorGrid currentTargetFloor; // 当前拖动时检测到的楼层

        private void Awake()
        {
            controller = GetComponent<AlanBotController>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            mainCamera = Camera.main;
        }

        private void Start()
        {
            floorManager = FindAnyObjectByType<FloorManager>();
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

            // 使用FloorDetectionService检测鼠标所在楼层（Raycast对FloorDetection层，支持垂直堆叠楼层）
            currentTargetFloor = floorDetector?.DetectFloorAtMouse();

            // 拖动时自由跟随鼠标（X、Y均跟随，不锁定地面）
            transform.position = mouse;

            // 虚影颜色反馈：有效楼层=半透明白，无效=半透明红
            SetGhostColor(currentTargetFloor != null);
        }

        private void HandleDragInput()
        {
            // 左键确认放置
            if (Input.GetMouseButtonDown(0))
            {
                if (currentTargetFloor != null)
                {
                    ConfirmPlacement();
                }
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

            // 确认放置：锚定到目标楼层地面
            if (currentTargetFloor != null)
            {
                float floorY = GetFloorOriginY(currentTargetFloor);
                transform.position = new Vector3(transform.position.x, floorY + controller.GroundYOffset, 0f);
            }

            // 恢复正常显示
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

            // 恢复正常显示
            RestoreColor();

            floorDetector?.ResetCache();

            // 安全检查：如果取消拖放时已进入营业阶段，恢复行为树
            // （防止OnStoreOpen在拖放期间触发时isBeingMoved=true导致Resume被跳过）
            if (DayLoopManager.Instance != null &&
                DayLoopManager.Instance.currentPhase == GamePhase.OpenPhase)
            {
                controller.ResumeBehavior();
            }
        }

        // ─── 虚影 & 颜色 ───

        /// <summary>
        /// 设置虚影颜色（拖动期间使用）
        /// </summary>
        private void SetGhostColor(bool isValid)
        {
            if (spriteRenderer == null) return;
            Color baseColor = isValid ? Color.white : invalidTint;
            baseColor.a = ghostAlpha;
            spriteRenderer.color = baseColor;
        }

        /// <summary>
        /// 恢复正常不透明颜色（放置或取消后）
        /// </summary>
        private void RestoreColor()
        {
            if (spriteRenderer != null)
                spriteRenderer.color = Color.white;
        }

        // ─── 楼层工具 ───

        /// <summary>
        /// 获取楼层原点的Y坐标（地面高度）
        /// </summary>
        private float GetFloorOriginY(FloorGrid floor)
        {
            return floor.origin != null ? floor.origin.position.y : floor.transform.position.y;
        }
    }
}
