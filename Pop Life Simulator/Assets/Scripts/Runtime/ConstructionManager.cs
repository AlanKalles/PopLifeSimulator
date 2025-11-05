using UnityEngine;
using PopLife.Data;
using PopLife.Services;

namespace PopLife.Runtime
{
    public class ConstructionManager : MonoBehaviour
    {
        public enum Mode { None, Place, Move, Destroy }

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

        [Header("楼层控制")]
        [SerializeField] private FloorGrid targetFloor; // 当前目标楼层
        [SerializeField] private bool showFloorIndicator = true; // 是否显示当前楼层指示器

        [Header("引用")]
        public FloorManager floorManager;        // 支持多楼层管理
        public BlueprintManager blueprintManager;// 需由你项目提供
        public ResourceManager resourceManager;  // 需由你项目提供
        private Camera mainCamera;               // 缓存主相机引用

        [Header("Destroy模式高亮")]
        [SerializeField] private PopLife.UI.BuildingInteraction.BuildingHighlighter buildingHighlighter; // 建筑高亮器
        [SerializeField] private Color destroyHighlightColor = new Color(1f, 0.2f, 0.2f, 1f); // 红色高亮
        private BuildingInstance hoveredBuildingInDestroyMode; // Destroy模式下鼠标悬停的建筑
        private Vector3 lastMousePositionInDestroyMode;        // 上次鼠标位置（优化性能）

        [Header("楼层自动检测")]
        [SerializeField] private int detectionInterval = 3; // 检测间隔（帧），默认3帧检测一次
        private FloorDetectionService floorDetector;        // 楼层检测服务
        private FloorGrid currentDetectedFloor;             // 当前检测到的楼层
        private FloorGrid lastPreviewFloor;                 // 上一次预览所在楼层

        void Awake()
        {
            // 缓存主相机
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("ConstructionManager: 找不到主相机！请确保场景中有一个相机的tag设置为'MainCamera'");
            }

            // 初始化楼层检测服务
            if (mainCamera != null)
            {
                floorDetector = new FloorDetectionService(mainCamera, detectionInterval);
            }

            // 初始化时设置默认目标楼层
            if (floorManager != null && targetFloor == null)
            {
                targetFloor = floorManager.GetActiveFloor();
                if (targetFloor != null)
                {
                    targetFloor.isSelected = true;
                }
            }
        }

        void OnDisable()
        {
            // 清理选中状态
            if (targetFloor != null)
            {
                targetFloor.isSelected = false;
            }
        }

        void OnDestroy()
        {
            // 清理选中状态
            if (targetFloor != null)
            {
                targetFloor.isSelected = false;
            }
        }

        void Update()
        {
            // 处理楼层切换输入（保留Tab键功能）
            HandleFloorSwitching();

            if (mode == Mode.Place)
            {
                // 自动检测鼠标所在楼层
                if (floorDetector != null)
                {
                    currentDetectedFloor = floorDetector.DetectFloorAtMouse();

                    // 楼层变化时切换预览
                    if (currentDetectedFloor != lastPreviewFloor)
                    {
                        SwitchPreviewFloor(currentDetectedFloor);
                        lastPreviewFloor = currentDetectedFloor;
                    }
                }

                // 始终更新预览位置（即使没有检测到楼层）
                UpdatePlacePreview();

                HandlePlaceInput();
            }
            else if (mode == Mode.Move)
            {
                // 自动检测鼠标所在楼层（Move模式也支持）
                if (floorDetector != null)
                {
                    currentDetectedFloor = floorDetector.DetectFloorAtMouse();

                    // 楼层变化时切换预览
                    if (currentDetectedFloor != lastPreviewFloor)
                    {
                        SwitchMovePreviewFloor(currentDetectedFloor);
                        lastPreviewFloor = currentDetectedFloor;
                    }
                }

                // 始终更新预览位置（即使没有检测到楼层）
                UpdateMovePreview();

                HandleMoveInput();
            }
            else if (mode == Mode.Destroy)
            {
                UpdateDestroyHover();
                HandleDestroyInput();
            }
        }

        // 处理楼层切换
        private void HandleFloorSwitching()
        {
            if (floorManager == null) return;

            // 使用Tab键循环切换激活的楼层
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                SwitchToNextActiveFloor();
            }

            // 使用数字键直接切换到对应楼层（1-9）
            for (int i = 1; i <= 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + i))
                {
                    SwitchToFloorByIndex(i - 1);
                }
            }
        }

        // 切换到下一个激活的楼层
        public void SwitchToNextActiveFloor()
        {
            var activeFloors = floorManager.GetAllActiveFloors();
            if (activeFloors.Count <= 1) return;

            int currentIndex = activeFloors.IndexOf(targetFloor);
            int nextIndex = (currentIndex + 1) % activeFloors.Count;
            SetTargetFloor(activeFloors[nextIndex]);
        }

        // 通过索引切换到楼层
        public void SwitchToFloorByIndex(int index)
        {
            var activeFloors = floorManager.GetAllActiveFloors();
            if (index >= 0 && index < activeFloors.Count)
            {
                SetTargetFloor(activeFloors[index]);
            }
        }

        // 设置目标楼层
        public void SetTargetFloor(FloorGrid floor)
        {
            if (floor != null && floor != targetFloor)
            {
                // 取消之前楼层的选中状态
                if (targetFloor != null)
                {
                    targetFloor.isSelected = false;
                }

                targetFloor = floor;

                // 设置新楼层的选中状态
                targetFloor.isSelected = true;

                // 通知UI更新（如果有楼层指示器）
                if (showFloorIndicator)
                {
                    Debug.Log($"切换到楼层: {floor.floorId}");
                    // TODO: 更新UI显示当前楼层
                }
            }
        }

        // 获取当前操作的目标楼层
        private FloorGrid GetTargetFloor()
        {
            // 如果没有设置目标楼层，使用FloorManager的当前活跃楼层
            if (targetFloor == null && floorManager != null)
            {
                targetFloor = floorManager.GetActiveFloor();
            }
            return targetFloor;
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
            // 资源校验：蓝图检查
            if (arch.requiresBlueprint && !blueprintManager.HasBlueprint(arch.archetypeId))
            {
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.ShowAlert(AlertType.BlueprintRequired);
                }
                return;
            }

            // 资源校验：金钱和声望检查
            if (!resourceManager.CanAfford(arch.buildCost, 0))
            {
                // 判断具体缺少哪种资源
                AlertType alertType = AlertType.NotEnoughMoney;
                if (resourceManager.GetMoney() < arch.buildCost)
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

            // 设置初始透明度和层级
            foreach (var renderer in previewRenderers)
            {
                // 保存原始颜色并设置透明度
                var originalColor = renderer.color;
                renderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.7f);

                // 提高排序层级，确保预览在最上层
                renderer.sortingOrder += 100;
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
        }


        private void UpdatePlacePreview()
        {
            if (!preview) return;

            // 使用自动检测的楼层（如果可用），否则使用目标楼层
            var floor = currentDetectedFloor ?? GetTargetFloor();

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

            // 如果有楼层，使用楼层坐标系；否则直接使用鼠标世界坐标
            bool canPlace = false;
            if (floor != null)
            {
                var gridPos = floor.WorldToGrid(mouse);
                preview.transform.SetPositionAndRotation(floor.GridToWorld(gridPos), Quaternion.Euler(0, 0, previewRot * 90));

                canPlace = floor.CanPlaceFootprint(selectedArchetype.GetRotatedFootprint(previewRot), gridPos)
                          && selectedArchetype.ValidatePlacement(floor, gridPos, previewRot);
            }
            else
            {
                // 没有楼层时，预览直接跟随鼠标（但标记为不可建造）
                preview.transform.SetPositionAndRotation(mouse, Quaternion.Euler(0, 0, previewRot * 90));
                canPlace = false;
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

            if (Input.GetMouseButtonDown(0))
            {
                // 使用自动检测的楼层（如果可用），否则使用目标楼层
                var floor = currentDetectedFloor ?? GetTargetFloor();
                if (floor == null)
                {
                    // 鼠标不在楼层区域 - 不弹窗，预览已经变红
                    return;
                }

                if (mainCamera == null)
                {
                    Debug.LogError("ConstructionManager: 无法放置建筑 - 主相机未找到");
                    return;
                }

                var mouse = mainCamera.ScreenToWorldPoint(Input.mousePosition); mouse.z = 0;
                var gp = floor.WorldToGrid(mouse);

                var inst = floor.PlaceBuildingTransactional(selectedArchetype, gp, previewRot);
                if (inst)
                {
                    // 根据建筑类型播放不同音效
                    PlayBuildSound(selectedArchetype);
                    if (!Input.GetKey(KeyCode.LeftShift)) Cancel();
                }
                // 注意：放置失败时预览已经是红色，不需要额外弹窗
            }

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) Cancel();
        }

        // —— 移动模式 ——
        public void BeginMove(BuildingInstance bi)
        {
            selectedInstance = bi;
            previewRot = bi.rotation;
            mode = Mode.Move;

            // 设置目标楼层为建筑所在楼层
            var floor = floorManager.GetFloor(bi.floorId);
            if (floor != null)
            {
                SetTargetFloor(floor);
            }

            CreatePreview(bi.archetype);
        }

        private void UpdateMovePreview()
        {
            if (!preview || !selectedInstance) return;

            // 使用自动检测的楼层（如果可用），否则使用目标楼层
            var floor = currentDetectedFloor ?? GetTargetFloor();

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

            // 如果有楼层，使用楼层坐标系；否则直接使用鼠标世界坐标
            bool canPlace = false;
            if (floor != null)
            {
                var gp = floor.WorldToGrid(mouse);
                preview.transform.SetPositionAndRotation(floor.GridToWorld(gp), Quaternion.Euler(0, 0, previewRot * 90));

                // 如果是跨楼层移动，不允许自身占用检查
                if (floor.floorId == selectedInstance.floorId)
                {
                    // 同楼层移动，允许自身占用
                    canPlace = floor.CanPlaceFootprintAllowSelf(selectedInstance.archetype.GetRotatedFootprint(previewRot), gp, selectedInstance.instanceId);
                }
                else
                {
                    // 跨楼层移动，不允许自身占用
                    canPlace = floor.CanPlaceFootprint(selectedInstance.archetype.GetRotatedFootprint(previewRot), gp);
                }
            }
            else
            {
                // 没有楼层时，预览直接跟随鼠标（但标记为不可建造）
                preview.transform.SetPositionAndRotation(mouse, Quaternion.Euler(0, 0, previewRot * 90));
                canPlace = false;
            }

            // 更新所有渲染器的颜色
            UpdatePreviewColor(canPlace);

            // 确保预览可见
            ShowPreview();
        }

        private void HandleMoveInput()
        {
            if (Input.GetKeyDown(KeyCode.R) && selectedInstance.archetype.canRotate)
                previewRot = (previewRot + 1) % 4;

            if (Input.GetMouseButtonDown(0))
            {
                // 使用自动检测的楼层（如果可用），否则使用目标楼层
                var targetFloor = currentDetectedFloor ?? GetTargetFloor();
                if (targetFloor == null)
                {
                    // 鼠标不在楼层区域 - 不弹窗，预览已经变红
                    return;
                }

                if (mainCamera == null)
                {
                    Debug.LogError("ConstructionManager: 无法移动建筑 - 主相机未找到");
                    return;
                }

                var mouse = mainCamera.ScreenToWorldPoint(Input.mousePosition); mouse.z = 0;
                var gp = targetFloor.WorldToGrid(mouse);

                // 统一移动逻辑：先检查资源，再执行移动
                bool isCrossFloor = (targetFloor.floorId != selectedInstance.floorId);
                int moveCost = isCrossFloor ? selectedInstance.archetype.moveCost * 2 : selectedInstance.archetype.moveCost;

                // 1. 资源检查（统一在此处处理）
                if (!resourceManager.CanAfford(moveCost, 0))
                {
                    // 资金不足 - 显示警告弹窗并取消移动
                    UIManager.Instance.ShowAlert(AlertType.NotEnoughMoney, onClose: () =>
                    {
                        Cancel(); // 取消移动模式
                    });
                    return;
                }

                // 2. 执行移动
                bool moveSuccess = false;
                if (isCrossFloor)
                {
                    // 跨楼层移动
                    moveSuccess = MoveBuilingAcrossFloors(selectedInstance, targetFloor, gp, previewRot);
                }
                else
                {
                    // 同楼层移动
                    moveSuccess = targetFloor.MoveBuilding(selectedInstance, gp, previewRot);
                }

                // 3. 处理结果
                if (moveSuccess)
                {
                    AudioManager.Instance.PlaySound(AudioKeys.BUILDING_MOVED);
                    Cancel();
                }
                // 注意：移动失败时预览已经是红色，不需要额外弹窗
            }

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) Cancel();
        }

        /// <summary>
        /// 跨楼层移动建筑
        /// 注意：调用此方法前应先检查资源是否足够！
        /// </summary>
        private bool MoveBuilingAcrossFloors(BuildingInstance bi, FloorGrid targetFloor, Vector2Int newPos, int newRot)
        {
            // 1. 检查目标位置是否可用
            var footprint = bi.archetype.GetRotatedFootprint(newRot);
            if (!targetFloor.CanPlaceFootprint(footprint, newPos))
                return false;

            // 2. 从原楼层移除
            var sourceFloor = floorManager.GetFloor(bi.floorId);
            if (sourceFloor == null) return false;

            sourceFloor.RemoveBuilding(bi, refundBlueprint: false, refundMoney: false); // 移动时不返还任何资源

            // 3. 移动GameObject到新楼层
            bi.transform.SetPositionAndRotation(targetFloor.GridToWorld(newPos), Quaternion.Euler(0, 0, newRot * 90));
            bi.transform.SetParent(targetFloor.buildingContainer);

            // 4. 注册到新楼层
            if (!targetFloor.RegisterExistingBuilding(bi, newPos, newRot))
            {
                // 如果注册失败，恢复到原楼层
                sourceFloor.RegisterExistingBuilding(bi, bi.gridPosition, bi.rotation);
                bi.transform.SetParent(sourceFloor.buildingContainer);
                return false;
            }

            // 5. 扣除资源（跨楼层移动成本×2，调用方应先检查 CanAfford）
            resourceManager.SpendMoney(bi.archetype.moveCost * 2);

            return true;
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

            // Raycast检测鼠标悬停的建筑
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

            // 左键点击建筑
            if (Input.GetMouseButtonDown(0))
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

                // Raycast检测点击的建筑
                Vector2 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
                RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero, Mathf.Infinity, LayerMask.GetMask("InteractableShelf"));

                if (hit.collider != null)
                {
                    BuildingInstance building = hit.collider.GetComponent<BuildingInstance>();
                    if (building != null)
                    {
                        // 显示确认弹窗
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
            var floor = floorManager.GetFloor(bi.floorId);

            // 蓝图已改为永久解锁，不需要返还
            // 但返还建造成本（按destroyRefundRate比例，默认80%）
            floor.RemoveBuilding(bi, refundBlueprint: false, refundMoney: true);

            Destroy(bi.gameObject);
            AudioManager.Instance.PlaySound(AudioKeys.BUILDING_DESTROYED);

            Debug.Log($"Destroyed {bi.archetype.displayName}, refunded ${Mathf.RoundToInt(bi.archetype.buildCost * bi.archetype.destroyRefundRate)}");
        }

        /// <summary>
        /// 销毁建筑（公开接口，供外部直接调用，不显示确认弹窗）
        /// </summary>
        public void DestroyBuilding(BuildingInstance bi)
        {
            ExecuteDestroyBuilding(bi);
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

        // 切换预览楼层（Place模式）
        private void SwitchPreviewFloor(FloorGrid newFloor)
        {
            // 如果新楼层为null（鼠标离开所有楼层），不需要清理预览
            // 预览会被HidePreview()方法处理

            if (newFloor == null)
            {
                return;
            }

            // 更新目标楼层（这会自动更新FloorManager的选中状态）
            SetTargetFloor(newFloor);

            // 通知FloorManager切换楼层（触发高亮效果）
            if (floorManager != null)
            {
                floorManager.SetActiveFloorProgrammatic(newFloor);
            }
        }

        // 切换预览楼层（Move模式）
        private void SwitchMovePreviewFloor(FloorGrid newFloor)
        {
            if (newFloor == null)
            {
                return;
            }

            // 更新目标楼层
            SetTargetFloor(newFloor);

            // 通知FloorManager切换楼层
            if (floorManager != null)
            {
                floorManager.SetActiveFloorProgrammatic(newFloor);
            }

            // 检查是否跨楼层移动（用于成本显示）
            if (selectedInstance != null)
            {
                bool isCrossFloor = (newFloor.floorId != selectedInstance.floorId);
                if (isCrossFloor && showFloorIndicator)
                {
                    Debug.Log($"跨楼层移动：{selectedInstance.floorId} → {newFloor.floorId} (成本×2)");
                    // TODO: 更新UI显示移动成本
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

        public void Cancel()
        {
            mode = Mode.None;
            selectedArchetype = null;
            selectedInstance = null;
            if (preview) Destroy(preview);
            previewRenderers = null;

            // 重置检测状态
            currentDetectedFloor = null;
            lastPreviewFloor = null;

            // 重置FloorDetector缓存
            if (floorDetector != null)
            {
                floorDetector.ResetCache();
            }

            // 清理Destroy模式的高亮
            if (hoveredBuildingInDestroyMode != null && buildingHighlighter != null)
            {
                buildingHighlighter.Hide();
                hoveredBuildingInDestroyMode = null;
            }

            // 关闭确认面板（如果正在显示）
            if (UIManager.Instance != null && UIManager.Instance.IsConfirmationShowing())
            {
                UIManager.Instance.HideConfirmation();
                Debug.Log("Closed confirmation panel when exiting Destroy mode");
            }
        }

        // 获取当前目标楼层（供UI显示）
        public FloorGrid GetCurrentTargetFloor() => targetFloor;

        // 获取当前目标楼层ID
        public int GetCurrentTargetFloorId() => targetFloor != null ? targetFloor.floorId : -1;

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
