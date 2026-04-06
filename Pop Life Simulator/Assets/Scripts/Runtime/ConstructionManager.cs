using UnityEngine;
using PopLife.Data;
using PopLife.Services;
using PopLife.Manager;
using PopLife.UI;

namespace PopLife.Runtime
{
    public class ConstructionManager : MonoBehaviour
    {
        public enum Mode { None, Place, Move, Destroy, PlaceElevator, MoveFloorTile, DestroyFloorTile }

        // 建筑放置、销毁、移动或升级后触发，用于UI刷新和 Store Appeal 重算
        public static event System.Action OnBuildingPlacedOrDestroyed;

        /// <summary>
        /// 外部触发建筑变化通知（如升级时调用）
        /// </summary>
        public static void NotifyBuildingChanged() => OnBuildingPlacedOrDestroyed?.Invoke();

        [Header("状态")]
        public Mode mode = Mode.None;
        public BuildingArchetype selectedArchetype;
        public BuildingInstance selectedInstance;

        [Header("预览")]
        private GameObject preview;
        private SpriteRenderer[] previewRenderers; // 支持多个SpriteRenderer
        private int previewRot; // 0/1/2/3
        private Color validColor = new Color(0.5f, 1f, 0.5f, 0.7f); // 半透明绿色
        private Color invalidColor = new Color(1f, 0.5f, 0.5f, 0.7f); // 半透明红色

        [Header("引用")]
        public BlueprintManager blueprintManager;// 需由你项目提供
        public ResourceManager resourceManager;  // 需由你项目提供
        private Camera mainCamera;               // 缓存主相机引用

        [Header("Destroy/Move模式高亮")]
        [SerializeField] private PopLife.UI.BuildingInteraction.BuildingHighlighter buildingHighlighter; // 建筑高亮器
        [SerializeField] private Color destroyHighlightColor = new Color(1f, 0.2f, 0.2f, 1f); // 红色高亮
        [SerializeField] private Color moveHighlightColor = new Color(0.2f, 0.5f, 1f, 1f); // 蓝色高亮
        private BuildingInstance hoveredBuildingInDestroyMode; // Destroy模式下鼠标悬停的建筑
        private BuildingInstance hoveredBuildingInMoveMode;    // Move模式下鼠标悬停的建筑
        private Vector3 lastMousePositionInDestroyMode;        // 上次鼠标位置（优化性能）
        private Vector3 lastMousePositionInMoveMode;           // Move模式上次鼠标位置（优化性能）
        private bool isMoveDragging = false;                   // Move模式：是否正在拖动建筑

        // Move模式：记录原始位置/旋转用于取消回滚
        private Vector2Int moveOriginalGridPos;
        private int moveOriginalRot;

        // MoveFloorTile / DestroyFloorTile 模式
        private FloorTileInstance hoveredFloorTile;
        private Vector3 lastMousePositionInMoveFloorTileMode;
        private Vector3 lastMousePositionInDestroyFloorTileMode;

        // 当前鼠标所在的 FloorTileInstance（shelf/facility 放置用）
        private FloorTileInstance currentTargetTile;

        // 电梯放置（两次点击）
        private FloorTileInstance elevatorFirstTile;
        private Vector2Int elevatorFirstLocalCell;
        private bool hasElevatorFirstClick;

        void Awake()
        {
            // 缓存主相机
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("ConstructionManager: 找不到主相机！请确保场景中有一个相机的tag设置为'MainCamera'");
            }
        }

        void OnDisable()
        {
            // 无需清理楼层选中状态（FloorManager 已移除）
        }

        void OnDestroy()
        {
            // 无需清理楼层选中状态（FloorManager 已移除）
        }

        void Update()
        {
            if (mode == Mode.Place)
            {
                // 始终更新预览位置
                UpdatePlacePreview();
                HandlePlaceInput();
            }
            else if (mode == Mode.Move)
            {
                if (isMoveDragging)
                {
                    // 拖动状态：显示预览并跟随鼠标
                    UpdateMovePreview();
                }
                else
                {
                    // 选择状态：悬停高亮建筑
                    UpdateMoveHover();
                }

                HandleMoveInput();
            }
            else if (mode == Mode.Destroy)
            {
                UpdateDestroyHover();
                HandleDestroyInput();
            }
            else if (mode == Mode.PlaceElevator)
            {
                HandleElevatorInput();
            }
            else if (mode == Mode.MoveFloorTile)
            {
                if (isMoveDragging)
                {
                    // 拖动状态：显示预览并跟随鼠标
                    UpdateMovePreview();
                    HandleMoveFloorTileInput();
                }
                else
                {
                    // 选择状态：悬停高亮地板
                    UpdateMoveFloorTileHover();
                    HandleMoveFloorTileInput();
                }
            }
            else if (mode == Mode.DestroyFloorTile)
            {
                UpdateDestroyFloorTileHover();
                HandleDestroyFloorTileInput();
            }
        }

        // —— 放置模式 ——
        /// <summary>
        /// Select a building archetype for placement (called by BuildingListPanel)
        /// </summary>
        public void SelectArchetypeForPlacement(BuildingArchetype arch)
        {
            BeginPlace(arch);
        }

        public void BeginPlace(BuildingArchetype arch)
        {
            // 资源校验：蓝图检查（直接通过BlueprintManager判断是否已解锁）
            if (!blueprintManager.HasBlueprint(arch.archetypeId))
            {
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.ShowAlert(AlertType.BlueprintRequired);
                }
                return;
            }

            // 资源校验：金钱和声望检查（应用全局修饰器的建造成本乘数）
            int finalBuildCost = GlobalModifierManager.Instance != null
                ? Mathf.RoundToInt(arch.buildCost * GlobalModifierManager.Instance.GetConstructionCostMultiplier())
                : arch.buildCost;
            if (!resourceManager.CanAfford(finalBuildCost, 0))
            {
                // 判断具体缺少哪种资源
                AlertType alertType = AlertType.NotEnoughMoney;
                if (resourceManager.GetMoney() < finalBuildCost)
                {
                    alertType = AlertType.NotEnoughMoney;
                }
                else if (resourceManager.GetFame() < 0) // 如果未来有声望消耗
                {
                    alertType = AlertType.NotEnoughFame;
                }

                // 显示警告弹窗，关闭时自动取消建造
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.ShowAlert(alertType, onClose: () =>
                    {
                        // 弹窗关闭时取消建造模式（等同于右键取消）
                        Cancel();
                    });
                }
                return;
            }

            selectedArchetype = arch;
            previewRot = 0;
            mode = Mode.Place;
            CreatePreview(arch);

            // 通知首次进入建造Place模式
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.NotifyPlaceModeEntered();
            }
        }

        private void CreatePreview(BuildingArchetype arch)
        {
            if (preview) Destroy(preview);

            // 直接实例化原型的prefab作为预览
            preview = Instantiate(arch.prefab);
            preview.name = "Preview_" + arch.archetypeId;

            // 禁用所有可能的游戏逻辑组件，只保留视觉效果
            DisableGameplayComponents(preview);

            // 获取所有的SpriteRenderer（支持多个子对象）
            previewRenderers = preview.GetComponentsInChildren<SpriteRenderer>(true);

            // 设置初始透明度和 sorting layer
            foreach (var renderer in previewRenderers)
            {
                // 保存原始颜色并设置透明度
                var originalColor = renderer.color;
                renderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.7f);

                // 设置 sorting layer 为 FRAME
                renderer.sortingLayerName = "FRAME";
            }
        }

        private void DisableGameplayComponents(GameObject obj)
        {
            // 禁用所有可能影响游戏的组件，但保留渲染组件
            // 禁用碰撞器
            var colliders = obj.GetComponentsInChildren<Collider2D>();
            foreach (var col in colliders) col.enabled = false;

            // 禁用刚体
            var rigidbodies = obj.GetComponentsInChildren<Rigidbody2D>();
            foreach (var rb in rigidbodies) rb.simulated = false;

            // 禁用自定义脚本（建筑实例相关）
            var instances = obj.GetComponentsInChildren<BuildingInstance>();
            foreach (var inst in instances) inst.enabled = false;

            var shelves = obj.GetComponentsInChildren<ShelfInstance>();
            foreach (var shelf in shelves) shelf.enabled = false;

            var facilities = obj.GetComponentsInChildren<FacilityInstance>();
            foreach (var facility in facilities) facility.enabled = false;

            var floorTiles = obj.GetComponentsInChildren<FloorTileInstance>();
            foreach (var ft in floorTiles) ft.enabled = false;
        }


        private void UpdatePlacePreview()
        {
            if (!preview) return;

            // 检查相机是否存在
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    Debug.LogError("ConstructionManager: 无法获取主相机引用");
                    return;
                }
            }

            var mouse = mainCamera.ScreenToWorldPoint(Input.mousePosition); mouse.z = 0;

            bool canPlace = false;

            if (selectedArchetype is Data.IWorldPlaceable wp)
            {
                // FloorTile: 使用 WorldGrid 坐标，通过 IWorldPlaceable 验证
                var wg = WorldGrid.Instance;
                if (wg != null)
                {
                    var gridPos = wg.WorldToGrid(mouse);
                    preview.transform.SetPositionAndRotation(
                        wg.GridToWorld(gridPos),
                        Quaternion.Euler(0, 0, previewRot * 90));

                    canPlace = wp.ValidateWorldPlacement(wg, gridPos, previewRot);
                }
                else
                {
                    preview.transform.SetPositionAndRotation(mouse, Quaternion.Euler(0, 0, previewRot * 90));
                }
            }
            else if (selectedArchetype is Data.IInteriorPlaceable ip)
            {
                // Shelf/Facility: 检测 FloorTileInstance，通过 IInteriorPlaceable 验证
                currentTargetTile = DetectFloorTileAtWorld(mouse);
                if (currentTargetTile?.Interior != null)
                {
                    var localPos = currentTargetTile.Interior.WorldToLocal(mouse);
                    preview.transform.SetPositionAndRotation(
                        currentTargetTile.Interior.LocalToWorld(localPos),
                        Quaternion.Euler(0, 0, previewRot * 90));

                    canPlace = ip.ValidateInteriorPlacement(currentTargetTile.Interior, localPos, previewRot);
                }
                else
                {
                    // 没有地板时，预览直接跟随鼠标（但标记为不可建造）
                    preview.transform.SetPositionAndRotation(mouse, Quaternion.Euler(0, 0, previewRot * 90));
                    canPlace = false;
                }
            }

            // 更新所有渲染器的颜色
            UpdatePreviewColor(canPlace);

            // 确保预览可见
            ShowPreview();
        }

        private void HandlePlaceInput()
        {
            if (Input.GetKeyDown(KeyCode.R) && selectedArchetype.canRotate)
                previewRot = (previewRot + 1) % 4;

            // 使用 InputGateService 判定纯点击（避免拖拽时误放置建筑）
            if (InputGateService.Instance != null ? InputGateService.Instance.WasClickThisFrame : Input.GetMouseButtonDown(0))
            {
                if (mainCamera == null)
                {
                    Debug.LogError("ConstructionManager: 无法放置建筑 - 主相机未找到");
                    return;
                }

                var mouse = mainCamera.ScreenToWorldPoint(Input.mousePosition); mouse.z = 0;

                // 根据类型走不同放置路径
                BuildingInstance inst = null;

                if (selectedArchetype is Data.IWorldPlaceable && selectedArchetype is FloorTileArchetype fta)
                {
                    // FloorTile: 通过 WorldGrid 放置（IWorldPlaceable 验证已在预览阶段完成）
                    var wg = WorldGrid.Instance;
                    if (wg != null)
                    {
                        var gp = wg.WorldToGrid(mouse);
                        inst = wg.PlaceFloorTileTransactional(fta, gp, previewRot);
                    }
                }
                else if (selectedArchetype is Data.IInteriorPlaceable)
                {
                    // Shelf/Facility: 通过 FloorTileInstance.Interior 放置（IInteriorPlaceable 验证已在预览阶段完成）
                    if (currentTargetTile != null && currentTargetTile.Interior != null)
                    {
                        var localPos = currentTargetTile.Interior.WorldToLocal(mouse);
                        inst = currentTargetTile.PlaceBuildingTransactional(selectedArchetype, localPos, previewRot);
                    }
                    else
                    {
                        // 鼠标不在地板区域 - 不弹窗，预览已经变红
                        return;
                    }
                }

                if (inst)
                {
                    PlayBuildSound(selectedArchetype);

                    if (FloatingTextSpawner.Instance != null)
                    {
                        int displayCost = GlobalModifierManager.Instance != null
                            ? Mathf.RoundToInt(selectedArchetype.buildCost * GlobalModifierManager.Instance.GetConstructionCostMultiplier())
                            : selectedArchetype.buildCost;
                        FloatingTextSpawner.Instance.SpawnCostText(
                            inst.transform.position,
                            displayCost
                        );
                    }

                    if (GameStateManager.Instance != null)
                        GameStateManager.Instance.NotifyShelfPlaced();

                    OnBuildingPlacedOrDestroyed?.Invoke();

                    // 地板可以按 Shift 连续放置，货架不可
                    if (selectedArchetype is ShelfArchetype || !Input.GetKey(KeyCode.LeftShift))
                        Cancel();
                }
                // 注意：放置失败时预览已经是红色，不需要额外弹窗
            }

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) Cancel();
        }

        // —— 移动模式 ——
        /// <summary>
        /// 进入移动模式（无参数，类似Destroy模式）
        /// </summary>
        public void BeginMove()
        {
            mode = Mode.Move;
            isMoveDragging = false;
            selectedInstance = null;
            hoveredBuildingInMoveMode = null;
            lastMousePositionInMoveMode = Vector3.zero;
            Debug.Log("Entered Move mode - Click any building to select and move");

            // 首次进入Move模式时触发教程
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.NotifyMoveModeEntered();
        }

        /// <summary>
        /// 更新Move模式下的鼠标悬停高亮（选择阶段）
        /// </summary>
        private void UpdateMoveHover()
        {
            // 检查鼠标是否移动（性能优化）
            Vector3 currentMousePos = Input.mousePosition;
            if (currentMousePos == lastMousePositionInMoveMode)
            {
                return;
            }
            lastMousePositionInMoveMode = currentMousePos;

            // 检查相机是否存在
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    return;
                }
            }

            // Raycast检测鼠标悬停的建筑（含地板）
            Vector2 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero, Mathf.Infinity, LayerMask.GetMask("InteractableShelf"));

            BuildingInstance newHovered = null;
            if (hit.collider != null)
            {
                newHovered = hit.collider.GetComponent<BuildingInstance>();
            }

            // 如果悬停建筑改变
            if (newHovered != hoveredBuildingInMoveMode)
            {
                // 隐藏之前的高亮
                if (hoveredBuildingInMoveMode != null && buildingHighlighter != null)
                {
                    buildingHighlighter.Hide();
                }

                // 显示新的蓝色高亮
                if (newHovered != null && buildingHighlighter != null)
                {
                    buildingHighlighter.Show(newHovered, moveHighlightColor);
                }

                hoveredBuildingInMoveMode = newHovered;
            }
        }

        /// <summary>
        /// 开始拖动选中的建筑（从悬停状态进入拖动状态）
        /// </summary>
        private void BeginMoveDragging(BuildingInstance bi)
        {
            var wg = WorldGrid.Instance;
            if (wg == null) return;

            // Editor 锁定检查
            if (bi.EditorLockedMove)
            {
                UIManager.Instance?.ShowAlert("This building is locked and cannot be moved.");
                return;
            }

            // 地板：检查限制
            if (bi is FloorTileInstance fti)
            {
                if (fti.IsDefault)
                {
                    if (UIManager.Instance != null)
                        UIManager.Instance.ShowAlert("Cannot move the default floor.");
                    return;
                }
                if (fti.HasBuildingsInInterior())
                {
                    if (UIManager.Instance != null)
                        UIManager.Instance.ShowAlert("Cannot move: remove all buildings on this floor tile first.");
                    return;
                }
                if (wg.HasElevatorOnFloorTile(fti))
                {
                    if (UIManager.Instance != null)
                        UIManager.Instance.ShowAlert("Cannot move: remove elevator first.");
                    return;
                }
                if (wg.WouldBreakSupport(fti))
                {
                    if (UIManager.Instance != null)
                        UIManager.Instance.ShowAlert("Cannot move: other floor tiles depend on this one.");
                    return;
                }

                // 记录原始位置用于取消回滚
                moveOriginalGridPos = fti.gridPosition;
                moveOriginalRot = fti.rotation;

                // 临时反注册地板（不销毁 GO）
                if (!wg.UnregisterFloorTile(fti))
                {
                    if (UIManager.Instance != null)
                        UIManager.Instance.ShowAlert("Cannot move this floor tile.");
                    return;
                }
            }
            else
            {
                // Shelf/Facility: 记录原始位置
                moveOriginalGridPos = bi.gridPosition;
                moveOriginalRot = bi.rotation;
            }

            selectedInstance = bi;
            previewRot = bi.rotation;
            isMoveDragging = true;

            if (buildingHighlighter != null)
                buildingHighlighter.Hide();

            CreatePreview(bi.archetype);
            Debug.Log($"Started dragging: {bi.archetype.displayName}");
        }

        private void UpdateMovePreview()
        {
            if (!preview || !selectedInstance) return;

            // 检查相机是否存在
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    Debug.LogError("ConstructionManager: 无法获取主相机引用");
                    return;
                }
            }

            var mouse = mainCamera.ScreenToWorldPoint(Input.mousePosition); mouse.z = 0;

            bool canPlace = false;

            if (selectedInstance is FloorTileInstance)
            {
                // FloorTile: 使用 WorldGrid 坐标
                var wg = WorldGrid.Instance;
                if (wg != null)
                {
                    var gp = wg.WorldToGrid(mouse);
                    preview.transform.SetPositionAndRotation(
                        wg.GridToWorld(gp),
                        Quaternion.Euler(0, 0, previewRot * 90));

                    var fp = selectedInstance.archetype.GetRotatedFootprint(previewRot);
                    canPlace = wg.CanPlaceFloorTile(fp, gp);
                }
                else
                {
                    preview.transform.SetPositionAndRotation(mouse, Quaternion.Euler(0, 0, previewRot * 90));
                }
            }
            else
            {
                // Shelf/Facility: 使用宿主 tile 的 interior
                var hostTile = FindFloorTileByInstanceId(selectedInstance.hostFloorTileInstanceId);
                if (hostTile?.Interior != null)
                {
                    var localPos = hostTile.Interior.WorldToLocal(mouse);
                    preview.transform.SetPositionAndRotation(
                        hostTile.Interior.LocalToWorld(localPos),
                        Quaternion.Euler(0, 0, previewRot * 90));

                    var fp = selectedInstance.archetype.GetRotatedFootprint(previewRot);
                    canPlace = hostTile.Interior.CanPlaceAllowSelf(fp, localPos, selectedInstance.instanceId);
                }
                else
                {
                    preview.transform.SetPositionAndRotation(mouse, Quaternion.Euler(0, 0, previewRot * 90));
                }
            }

            // 更新所有渲染器的颜色
            UpdatePreviewColor(canPlace);

            // 确保预览可见
            ShowPreview();
        }

        private void HandleMoveInput()
        {
            // 右键或ESC：根据拖动状态决定行为
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                if (isMoveDragging)
                {
                    // 拖动中 → 取消拖动，回到悬停选择状态
                    CancelMoveDrag();
                }
                else
                {
                    // 非拖动 → 退出移动模式
                    Cancel();
                }
                return;
            }

            if (!isMoveDragging)
            {
                // 阶段1：选择建筑（使用 InputGateService 判定纯点击）
                if (InputGateService.Instance != null ? InputGateService.Instance.WasClickThisFrame : Input.GetMouseButtonDown(0))
                {
                    // 检查相机是否存在
                    if (mainCamera == null)
                    {
                        mainCamera = Camera.main;
                        if (mainCamera == null)
                        {
                            Debug.LogError("ConstructionManager: 无法获取主相机引用");
                            return;
                        }
                    }

                    // Raycast检测点击的建筑（含地板）
                    Vector2 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
                    RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero, Mathf.Infinity, LayerMask.GetMask("InteractableShelf"));

                    if (hit.collider != null)
                    {
                        BuildingInstance building = hit.collider.GetComponent<BuildingInstance>();
                        if (building != null)
                        {
                            BeginMoveDragging(building);
                        }
                    }
                }
            }
            else
            {
                // 阶段2：拖动并放置建筑
                // 支持R键旋转（地板总是可旋转，shelf根据archetype决定）
                if (Input.GetKeyDown(KeyCode.R) && selectedInstance.archetype.canRotate)
                    previewRot = (previewRot + 1) % 4;

                // 点击放置（使用 InputGateService 判定纯点击）
                if (InputGateService.Instance != null ? InputGateService.Instance.WasClickThisFrame : Input.GetMouseButtonDown(0))
                {
                    if (mainCamera == null)
                    {
                        Debug.LogError("ConstructionManager: 无法移动建筑 - 主相机未找到");
                        return;
                    }

                    var mouse = mainCamera.ScreenToWorldPoint(Input.mousePosition); mouse.z = 0;

                    // 移动成本
                    int moveCost = selectedInstance.archetype.moveCost;

                    // 1. 资源检查
                    if (!resourceManager.CanAfford(moveCost, 0))
                    {
                        Debug.Log($"Not enough money to move building. Required: ${moveCost}, Current: ${resourceManager.GetMoney()}");
                        return;
                    }

                    // 2. 执行移动
                    bool moveSuccess = false;

                    if (selectedInstance is FloorTileInstance movingFti)
                    {
                        // FloorTile: 重新注册到新位置
                        var wg = WorldGrid.Instance;
                        if (wg != null)
                        {
                            var gp = wg.WorldToGrid(mouse);
                            moveSuccess = wg.ReregisterFloorTile(movingFti, gp, previewRot);
                        }
                    }
                    else
                    {
                        // Shelf/Facility: 在宿主 tile 的 interior 内移动
                        var hostTile = FindFloorTileByInstanceId(selectedInstance.hostFloorTileInstanceId);
                        if (hostTile != null)
                        {
                            var localPos = hostTile.Interior.WorldToLocal(mouse);
                            moveSuccess = hostTile.MoveBuilding(selectedInstance, localPos, previewRot);
                        }
                    }

                    // 3. 处理结果
                    if (moveSuccess)
                    {
                        // 扣除移动费用
                        if (moveCost > 0)
                            resourceManager.SpendMoney(moveCost);

                        AudioManager.Instance.PlaySound(AudioKeys.BUILDING_MOVED);
                        OnBuildingPlacedOrDestroyed?.Invoke();
                        FinishMoveDrag(); // 成功后不回滚
                    }
                    // 注意：移动失败时预览已经是红色，不需要额外弹窗
                }
            }
        }

        /// <summary>
        /// 取消拖动并回滚到原位
        /// </summary>
        private void CancelMoveDrag()
        {
            if (selectedInstance is FloorTileInstance cancelFti)
            {
                // FloorTile: 回滚到原位置（gridPosition 未被修改，因为 ReregisterFloorTile 没被成功调用过）
                var wg = WorldGrid.Instance;
                if (wg != null)
                {
                    wg.ReregisterFloorTile(cancelFti, moveOriginalGridPos, moveOriginalRot, skipSupportCheck: true);
                }
            }
            // Shelf/Facility: 无需回滚（MoveBuilding 是原子操作，没调用过就没有副作用）

            FinishMoveDrag();
        }

        /// <summary>
        /// 完成拖动（成功或取消后的通用清理，不做回滚）
        /// </summary>
        private void FinishMoveDrag()
        {
            isMoveDragging = false;
            selectedInstance = null;
            if (preview) Destroy(preview);
            previewRenderers = null;

            hoveredBuildingInMoveMode = null;
            lastMousePositionInMoveMode = Vector3.zero;

            hoveredFloorTile = null;
            lastMousePositionInMoveFloorTileMode = Vector3.zero;
        }

        // —— 移动地板模式 ——
        /// <summary>
        /// 进入移动地板模式（使用逻辑检测而非 Raycast）
        /// </summary>
        public void BeginMoveFloorTile()
        {
            mode = Mode.MoveFloorTile;
            isMoveDragging = false;
            selectedInstance = null;
            hoveredFloorTile = null;
            lastMousePositionInMoveFloorTileMode = Vector3.zero;
            Debug.Log("Entered MoveFloorTile mode - Click any floor tile to move");
        }

        /// <summary>
        /// 更新MoveFloorTile模式下的鼠标悬停高亮（选择阶段）
        /// </summary>
        private void UpdateMoveFloorTileHover()
        {
            Vector3 currentMousePos = Input.mousePosition;
            if (currentMousePos == lastMousePositionInMoveFloorTileMode) return;
            lastMousePositionInMoveFloorTileMode = currentMousePos;

            if (mainCamera == null) { mainCamera = Camera.main; if (mainCamera == null) return; }

            Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0;
            FloorTileInstance newHovered = DetectFloorTileAtWorld(mouseWorld);

            if (newHovered != hoveredFloorTile)
            {
                if (hoveredFloorTile != null && buildingHighlighter != null)
                    buildingHighlighter.Hide();
                if (newHovered != null && buildingHighlighter != null)
                    buildingHighlighter.Show(newHovered, moveHighlightColor);
                hoveredFloorTile = newHovered;
            }
        }

        /// <summary>
        /// 处理MoveFloorTile模式的输入
        /// </summary>
        private void HandleMoveFloorTileInput()
        {
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                if (isMoveDragging)
                    CancelMoveDrag();
                else
                    Cancel();
                return;
            }

            if (!isMoveDragging)
            {
                // 选择阶段：使用逻辑检测，非 Raycast
                if (InputGateService.Instance != null ? InputGateService.Instance.WasClickThisFrame : Input.GetMouseButtonDown(0))
                {
                    if (hoveredFloorTile != null)
                        BeginMoveDragging(hoveredFloorTile);
                }
            }
            else
            {
                // 拖动阶段：支持R键旋转
                if (Input.GetKeyDown(KeyCode.R) && selectedInstance.archetype.canRotate)
                    previewRot = (previewRot + 1) % 4;

                // 点击放置
                if (InputGateService.Instance != null ? InputGateService.Instance.WasClickThisFrame : Input.GetMouseButtonDown(0))
                {
                    if (mainCamera == null) { mainCamera = Camera.main; if (mainCamera == null) return; }

                    var mouse = mainCamera.ScreenToWorldPoint(Input.mousePosition); mouse.z = 0;
                    var wg = WorldGrid.Instance;
                    if (wg == null) return;

                    var gp = wg.WorldToGrid(mouse);
                    int moveCost = selectedInstance.archetype.moveCost;

                    if (!resourceManager.CanAfford(moveCost, 0))
                    {
                        Debug.Log($"Not enough money to move floor tile. Required: ${moveCost}, Current: ${resourceManager.GetMoney()}");
                        return;
                    }

                    var movingFti = selectedInstance as FloorTileInstance;
                    if (movingFti != null)
                    {
                        bool moveSuccess = wg.ReregisterFloorTile(movingFti, gp, previewRot);
                        if (moveSuccess)
                        {
                            if (moveCost > 0)
                                resourceManager.SpendMoney(moveCost);

                            movingFti.InitializeInterior();
                            AudioManager.Instance.PlaySound(AudioKeys.BUILDING_MOVED);
                            OnBuildingPlacedOrDestroyed?.Invoke();
                            FinishMoveDrag();
                        }
                    }
                }
            }
        }

        // —— 销毁模式 ——
        /// <summary>
        /// 进入销毁模式
        /// </summary>
        public void BeginDestroy()
        {
            mode = Mode.Destroy;
            hoveredBuildingInDestroyMode = null;
            lastMousePositionInDestroyMode = Vector3.zero;
            Debug.Log("Entered Destroy mode - Click any building to destroy");

            // 首次进入Destroy模式时触发教程
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.NotifyDestroyModeEntered();
        }

        /// <summary>
        /// 更新销毁模式下的鼠标悬停高亮
        /// </summary>
        private void UpdateDestroyHover()
        {
            // 检查鼠标是否移动（性能优化）
            Vector3 currentMousePos = Input.mousePosition;
            if (currentMousePos == lastMousePositionInDestroyMode)
            {
                return;
            }
            lastMousePositionInDestroyMode = currentMousePos;

            // 检查相机是否存在
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    return;
                }
            }

            // Raycast检测鼠标悬停的建筑（含地板）
            Vector2 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero, Mathf.Infinity, LayerMask.GetMask("InteractableShelf"));

            BuildingInstance newHovered = null;
            if (hit.collider != null)
            {
                newHovered = hit.collider.GetComponent<BuildingInstance>();
            }

            // 如果悬停建筑改变
            if (newHovered != hoveredBuildingInDestroyMode)
            {
                // 隐藏之前的高亮
                if (hoveredBuildingInDestroyMode != null && buildingHighlighter != null)
                {
                    buildingHighlighter.Hide();
                }

                // 显示新的红色高亮
                if (newHovered != null && buildingHighlighter != null)
                {
                    buildingHighlighter.Show(newHovered, destroyHighlightColor);
                }

                hoveredBuildingInDestroyMode = newHovered;
            }
        }

        /// <summary>
        /// 处理销毁模式的输入
        /// </summary>
        private void HandleDestroyInput()
        {
            // 右键或ESC取消销毁模式
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                Cancel();
                return;
            }

            // 左键点击建筑（使用 InputGateService 判定纯点击）
            if (InputGateService.Instance != null ? InputGateService.Instance.WasClickThisFrame : Input.GetMouseButtonDown(0))
            {
                // 检查相机是否存在
                if (mainCamera == null)
                {
                    mainCamera = Camera.main;
                    if (mainCamera == null)
                    {
                        Debug.LogError("ConstructionManager: 无法获取主相机引用");
                        return;
                    }
                }

                // Raycast检测点击的建筑（含地板）
                Vector2 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
                RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero, Mathf.Infinity, LayerMask.GetMask("InteractableShelf"));

                if (hit.collider != null)
                {
                    BuildingInstance building = hit.collider.GetComponent<BuildingInstance>();
                    if (building != null)
                    {
                        // Editor 锁定检查
                        if (building.EditorLockedDestroy)
                        {
                            UIManager.Instance?.ShowAlert("This building is locked and cannot be destroyed.");
                            return;
                        }

                        // 地板拆除：额外检查（通��� WorldGrid）
                        if (building is FloorTileInstance destroyFti)
                        {
                            var wg = WorldGrid.Instance;
                            if (destroyFti.IsDefault)
                            {
                                if (UIManager.Instance != null)
                                    UIManager.Instance.ShowAlert("Cannot destroy the default floor.");
                                return;
                            }
                            if (destroyFti.HasBuildingsInInterior())
                            {
                                if (UIManager.Instance != null)
                                    UIManager.Instance.ShowAlert("Cannot destroy: remove all buildings on this floor tile first.");
                                return;
                            }
                            if (wg != null && wg.HasElevatorOnFloorTile(destroyFti))
                            {
                                if (UIManager.Instance != null)
                                    UIManager.Instance.ShowAlert("Cannot destroy: remove elevator first.");
                                return;
                            }
                            if (wg != null && wg.WouldBreakSupport(destroyFti))
                            {
                                if (UIManager.Instance != null)
                                    UIManager.Instance.ShowAlert("Cannot destroy: other floor tiles depend on this one.");
                                return;
                            }
                        }
                        ShowDestroyConfirmation(building);
                    }
                }
            }
        }

        /// <summary>
        /// 显示销毁确认弹窗
        /// </summary>
        private void ShowDestroyConfirmation(BuildingInstance building)
        {
            // 计算退款金额
            int refundAmount = Mathf.RoundToInt(building.archetype.buildCost * building.archetype.destroyRefundRate);

            // 显示确认弹窗
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowConfirmation(
                    buildingName: building.archetype.displayName,
                    refundAmount: refundAmount,
                    onConfirm: () => {
                        // 确认销毁
                        ExecuteDestroyBuilding(building);
                    },
                    onCancel: () => {
                        // 取消销毁，继续保持Destroy模式
                        Debug.Log("Destroy cancelled, staying in Destroy mode");
                    }
                );
            }
            else
            {
                // 如果没有UIManager，直接销毁（调试用）
                Debug.LogWarning("UIManager not found, destroying directly without confirmation");
                ExecuteDestroyBuilding(building);
            }
        }

        /// <summary>
        /// 执行销毁建筑并返还部分建造成本
        /// </summary>
        private void ExecuteDestroyBuilding(BuildingInstance bi)
        {
            if (bi is FloorTileInstance fti)
            {
                // 地板拆除（WorldGrid.RemoveFloorTile 会 Destroy GO 并退款）
                var wg = WorldGrid.Instance;
                if (wg != null)
                {
                    wg.RemoveFloorTile(fti, refundMoney: true);
                }
            }
            else
            {
                // 普通建筑拆除：通过宿主 FloorTileInstance 移除
                var hostTile = FindFloorTileByInstanceId(bi.hostFloorTileInstanceId);
                hostTile?.RemoveBuilding(bi, refundMoney: true);
                Destroy(bi.gameObject);
            }

            AudioManager.Instance.PlaySound(AudioKeys.BUILDING_DESTROYED);
            OnBuildingPlacedOrDestroyed?.Invoke();

            Debug.Log($"Destroyed {bi.archetype.displayName}, refunded ${Mathf.RoundToInt(bi.archetype.buildCost * bi.archetype.destroyRefundRate)}");
        }

        /// <summary>
        /// 销毁建筑（公开接口，供外部直接调用，不显示确认弹窗）
        /// </summary>
        public void DestroyBuilding(BuildingInstance bi)
        {
            ExecuteDestroyBuilding(bi);
        }

        // —— 销毁地板模式 ——
        /// <summary>
        /// 进入销毁地板模式（使用逻辑检测而非 Raycast）
        /// </summary>
        public void BeginDestroyFloorTile()
        {
            mode = Mode.DestroyFloorTile;
            hoveredFloorTile = null;
            lastMousePositionInDestroyFloorTileMode = Vector3.zero;
            Debug.Log("Entered DestroyFloorTile mode - Click any floor tile to destroy");
        }

        /// <summary>
        /// 更新DestroyFloorTile模式下的鼠标悬停高亮
        /// </summary>
        private void UpdateDestroyFloorTileHover()
        {
            Vector3 currentMousePos = Input.mousePosition;
            if (currentMousePos == lastMousePositionInDestroyFloorTileMode) return;
            lastMousePositionInDestroyFloorTileMode = currentMousePos;

            if (mainCamera == null) { mainCamera = Camera.main; if (mainCamera == null) return; }

            Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0;
            FloorTileInstance newHovered = DetectFloorTileAtWorld(mouseWorld);

            if (newHovered != hoveredFloorTile)
            {
                if (hoveredFloorTile != null && buildingHighlighter != null)
                    buildingHighlighter.Hide();
                if (newHovered != null && buildingHighlighter != null)
                    buildingHighlighter.Show(newHovered, destroyHighlightColor);
                hoveredFloorTile = newHovered;
            }
        }

        /// <summary>
        /// 处理DestroyFloorTile模式的输入
        /// </summary>
        private void HandleDestroyFloorTileInput()
        {
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                Cancel();
                return;
            }

            if (InputGateService.Instance != null ? InputGateService.Instance.WasClickThisFrame : Input.GetMouseButtonDown(0))
            {
                if (hoveredFloorTile != null)
                {
                    var wg = WorldGrid.Instance;

                    // Editor 锁定检查
                    if (hoveredFloorTile.EditorLockedDestroy)
                    {
                        UIManager.Instance?.ShowAlert("This building is locked and cannot be destroyed.");
                        return;
                    }

                    // 地板拆除限制检查
                    if (hoveredFloorTile.IsDefault)
                    {
                        UIManager.Instance?.ShowAlert("Cannot destroy the default floor.");
                        return;
                    }
                    if (hoveredFloorTile.HasBuildingsInInterior())
                    {
                        UIManager.Instance?.ShowAlert("Cannot destroy: remove all buildings on this floor tile first.");
                        return;
                    }
                    if (wg != null && wg.HasElevatorOnFloorTile(hoveredFloorTile))
                    {
                        UIManager.Instance?.ShowAlert("Cannot destroy: remove elevator first.");
                        return;
                    }
                    if (wg != null && wg.WouldBreakSupport(hoveredFloorTile))
                    {
                        UIManager.Instance?.ShowAlert("Cannot destroy: other floor tiles depend on this one.");
                        return;
                    }

                    // 显示确认弹窗
                    ShowDestroyConfirmation(hoveredFloorTile);
                }
            }
        }

        private void UpdatePreviewColor(bool canPlace)
        {
            if (previewRenderers == null) return;

            foreach (var renderer in previewRenderers)
            {
                if (renderer == null) continue;

                // 根据是否可建造设置颜色
                if (canPlace)
                {
                    // 绿色调，表示可以建造
                    renderer.color = validColor;
                }
                else
                {
                    // 红色调，表示不可建造
                    renderer.color = invalidColor;
                }
            }
        }

        // 隐藏预览（鼠标不在任何楼层上时）
        private void HidePreview()
        {
            if (preview == null || previewRenderers == null) return;

            // 设置透明度为0（而不是销毁对象，避免频繁创建销毁）
            foreach (var renderer in previewRenderers)
            {
                if (renderer != null)
                {
                    Color c = renderer.color;
                    c.a = 0f;
                    renderer.color = c;
                }
            }
        }

        // 显示预览（恢复透明度）
        private void ShowPreview()
        {
            if (preview == null || previewRenderers == null) return;

            foreach (var renderer in previewRenderers)
            {
                if (renderer != null)
                {
                    Color c = renderer.color;
                    c.a = 0.7f;
                    renderer.color = c;
                }
            }
        }

        // ========== 电梯放置 ==========

        /// <summary>
        /// 进入电梯放置模式（由 UI 调用）
        /// </summary>
        public void BeginPlaceElevator()
        {
            Cancel(); // 先退出当前模式
            mode = Mode.PlaceElevator;
            hasElevatorFirstClick = false;
            elevatorFirstTile = null;
            Debug.Log("Entered Elevator placement mode - Click first cell, then second cell.");
        }

        private void HandleElevatorInput()
        {
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                Cancel();
                return;
            }

            if (!(InputGateService.Instance != null ? InputGateService.Instance.WasClickThisFrame : Input.GetMouseButtonDown(0)))
                return;

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null) return;
            }

            // 检测点击位置所在的 FloorTileInstance 和 interior 局部坐标
            var mouse = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mouse.z = 0;

            var tile = DetectFloorTileAtWorld(mouse);
            if (tile?.Interior == null)
            {
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowAlert("No floor tile here. Click on a floor tile.");
                return;
            }

            var localCell = tile.Interior.WorldToLocal(mouse);
            if (!tile.Interior.InBounds(localCell))
            {
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowAlert("Click inside a floor tile's interior.");
                return;
            }

            if (!hasElevatorFirstClick)
            {
                // 第一次点击
                if (tile.Interior.IsOccupied(localCell))
                {
                    if (UIManager.Instance != null)
                        UIManager.Instance.ShowAlert("Cell occupied by a building. Choose an empty cell.");
                    return;
                }

                elevatorFirstTile = tile;
                elevatorFirstLocalCell = localCell;
                hasElevatorFirstClick = true;
                Debug.Log($"Elevator first cell: tile={tile.instanceId}, local={localCell}. Now click the second cell on an adjacent floor.");
            }
            else
            {
                // 第二次点击
                if (tile.instanceId == elevatorFirstTile.instanceId && localCell == elevatorFirstLocalCell)
                {
                    Debug.Log("Same cell. Click a different cell.");
                    return;
                }

                if (tile.Interior.IsOccupied(localCell))
                {
                    if (UIManager.Instance != null)
                        UIManager.Instance.ShowAlert("Cell occupied. Choose an empty cell.");
                    hasElevatorFirstClick = false;
                    return;
                }

                var wg = WorldGrid.Instance;
                if (wg == null)
                {
                    hasElevatorFirstClick = false;
                    return;
                }

                // 尝试两种方向
                bool placed = wg.PlaceElevator(elevatorFirstTile, elevatorFirstLocalCell, tile, localCell)
                           || wg.PlaceElevator(tile, localCell, elevatorFirstTile, elevatorFirstLocalCell);

                if (placed)
                {
                    AudioManager.Instance?.PlaySound(AudioKeys.BUILDING_PLACED);
                    OnBuildingPlacedOrDestroyed?.Invoke();
                    Debug.Log($"Elevator placed: {elevatorFirstTile.instanceId}:{elevatorFirstLocalCell} ↔ {tile.instanceId}:{localCell}");
                }
                else
                {
                    if (UIManager.Instance != null)
                        UIManager.Instance.ShowAlert("Invalid: cells must be on adjacent floors, both with floor tiles, both empty.");
                }

                hasElevatorFirstClick = false;
                elevatorFirstTile = null;
            }
        }

        public void Cancel()
        {
            mode = Mode.None;
            selectedArchetype = null;
            selectedInstance = null;
            currentTargetTile = null;
            if (preview) Destroy(preview);
            previewRenderers = null;

            // 清理Destroy模式的高亮
            if (hoveredBuildingInDestroyMode != null && buildingHighlighter != null)
            {
                buildingHighlighter.Hide();
                hoveredBuildingInDestroyMode = null;
            }

            // 清理Move模式的高亮和状态
            if (hoveredBuildingInMoveMode != null && buildingHighlighter != null)
            {
                buildingHighlighter.Hide();
                hoveredBuildingInMoveMode = null;
            }
            isMoveDragging = false;

            // 清理MoveFloorTile / DestroyFloorTile模式的状态
            if (hoveredFloorTile != null && buildingHighlighter != null)
            {
                buildingHighlighter.Hide();
            }
            hoveredFloorTile = null;
            lastMousePositionInMoveFloorTileMode = Vector3.zero;
            lastMousePositionInDestroyFloorTileMode = Vector3.zero;

            // 清理电梯放置状态
            hasElevatorFirstClick = false;
            elevatorFirstTile = null;

            // 关闭确认面板（如果正在显示）
            if (UIManager.Instance != null && UIManager.Instance.IsConfirmationShowing())
            {
                UIManager.Instance.HideConfirmation();
                Debug.Log("Closed confirmation panel when exiting Destroy mode");
            }
        }

        // ========== 辅助方法 ==========

        /// <summary> 通过世界坐标检测鼠标所在的 FloorTileInstance </summary>
        private FloorTileInstance DetectFloorTileAtWorld(Vector3 worldPos)
        {
            var wg = WorldGrid.Instance;
            if (wg == null) return null;
            var gridPos = wg.WorldToGrid(worldPos);
            return wg.GetFloorTileAt(gridPos);
        }

        /// <summary> 通过 instanceId 查找 FloorTileInstance </summary>
        private FloorTileInstance FindFloorTileByInstanceId(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var wg = WorldGrid.Instance;
            if (wg == null) return null;
            foreach (var tile in wg.AllFloorTiles())
                if (tile.instanceId == id) return tile;
            return null;
        }

        // 根据建筑类型播放对应的建造音效
        private void PlayBuildSound(BuildingArchetype archetype)
        {
            if (archetype is ShelfArchetype shelfArchetype)
            {
                // 货架：根据商品类别播放音效
                string soundKey = AudioKeys.GetBuildSoundKey(shelfArchetype.category);
                AudioManager.Instance.PlaySound(soundKey);
            }
            else if (archetype is FacilityArchetype)
            {
                // 设施：通用建造音效
                AudioManager.Instance.PlaySound(AudioKeys.BUILD_FACILITY);
            }
            else
            {
                // 其他建筑类型：通用音效
                AudioManager.Instance.PlaySound(AudioKeys.BUILDING_PLACED);
            }
        }
    }
}
