using UnityEngine;
using PopLife.Data;

namespace PopLife.Runtime
{
    public class ConstructionManager : MonoBehaviour
    {
        public enum Mode { None, Place, Move }

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

        void Awake()
        {
            // 缓存主相机
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("ConstructionManager: 找不到主相机！请确保场景中有一个相机的tag设置为'MainCamera'");
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
            // 处理楼层切换输入
            HandleFloorSwitching();

            if (mode == Mode.Place) { UpdatePlacePreview(); HandlePlaceInput(); }
            else if (mode == Mode.Move) { UpdateMovePreview(); HandleMoveInput(); }
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
        public void BeginPlace(BuildingArchetype arch)
        {
            // 资源校验
            if (arch.requiresBlueprint && !blueprintManager.HasBlueprint(arch.archetypeId))
            {
                UIManager.Instance.ShowMessage("需要蓝图");
                return;
            }
            if (!resourceManager.CanAfford(arch.buildCost, 0))
            {
                UIManager.Instance.ShowMessage("资金不足");
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
            var floor = GetTargetFloor();
            if (floor == null) return;

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
            var gridPos = floor.WorldToGrid(mouse);

            preview.transform.SetPositionAndRotation(floor.GridToWorld(gridPos), Quaternion.Euler(0, 0, previewRot * 90));

            bool ok = floor.CanPlaceFootprint(selectedArchetype.GetRotatedFootprint(previewRot), gridPos)
                      && selectedArchetype.ValidatePlacement(floor, gridPos, previewRot);

            // 更新所有渲染器的颜色
            UpdatePreviewColor(ok);
        }

        private void HandlePlaceInput()
        {
            if (Input.GetKeyDown(KeyCode.R) && selectedArchetype.canRotate)
                previewRot = (previewRot + 1) % 4;

            if (Input.GetMouseButtonDown(0))
            {
                var floor = GetTargetFloor();
                if (floor == null)
                {
                    UIManager.Instance.ShowMessage("未选择楼层");
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
                    AudioManager.Instance.PlaySound("BuildingPlaced");
                    if (!Input.GetKey(KeyCode.LeftShift)) Cancel();
                }
                else UIManager.Instance.ShowMessage("放置失败");
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

            // 移动模式下，使用目标楼层（可以是不同楼层）
            var floor = GetTargetFloor();
            if (floor == null) return;

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
            var gp = floor.WorldToGrid(mouse);

            preview.transform.SetPositionAndRotation(floor.GridToWorld(gp), Quaternion.Euler(0, 0, previewRot * 90));

            // 如果是跨楼层移动，不允许自身占用检查
            bool ok;
            if (floor.floorId == selectedInstance.floorId)
            {
                // 同楼层移动，允许自身占用
                ok = floor.CanPlaceFootprintAllowSelf(selectedInstance.archetype.GetRotatedFootprint(previewRot), gp, selectedInstance.instanceId);
            }
            else
            {
                // 跨楼层移动，不允许自身占用
                ok = floor.CanPlaceFootprint(selectedInstance.archetype.GetRotatedFootprint(previewRot), gp);
            }

            // 更新所有渲染器的颜色
            UpdatePreviewColor(ok);
        }

        private void HandleMoveInput()
        {
            if (Input.GetKeyDown(KeyCode.R) && selectedInstance.archetype.canRotate)
                previewRot = (previewRot + 1) % 4;

            if (Input.GetMouseButtonDown(0))
            {
                var targetFloor = GetTargetFloor();
                if (targetFloor == null)
                {
                    UIManager.Instance.ShowMessage("未选择目标楼层");
                    return;
                }

                if (mainCamera == null)
                {
                    Debug.LogError("ConstructionManager: 无法移动建筑 - 主相机未找到");
                    return;
                }

                var mouse = mainCamera.ScreenToWorldPoint(Input.mousePosition); mouse.z = 0;
                var gp = targetFloor.WorldToGrid(mouse);

                // 检查是否是跨楼层移动
                if (targetFloor.floorId == selectedInstance.floorId)
                {
                    // 同楼层移动
                    if (targetFloor.MoveBuilding(selectedInstance, gp, previewRot))
                    {
                        AudioManager.Instance.PlaySound("BuildingMoved");
                        Cancel();
                    }
                    else UIManager.Instance.ShowMessage("移动失败");
                }
                else
                {
                    // 跨楼层移动：需要先从原楼层移除，再添加到新楼层
                    if (MoveBuilingAcrossFloors(selectedInstance, targetFloor, gp, previewRot))
                    {
                        AudioManager.Instance.PlaySound("BuildingMoved");
                        Cancel();
                    }
                    else UIManager.Instance.ShowMessage("跨楼层移动失败");
                }
            }

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) Cancel();
        }

        // 跨楼层移动建筑
        private bool MoveBuilingAcrossFloors(BuildingInstance bi, FloorGrid targetFloor, Vector2Int newPos, int newRot)
        {
            // 检查目标位置是否可用
            var footprint = bi.archetype.GetRotatedFootprint(newRot);
            if (!targetFloor.CanPlaceFootprint(footprint, newPos))
                return false;

            // 检查资源
            if (!resourceManager.CanAfford(bi.archetype.moveCost * 2, 0)) // 跨楼层移动成本加倍
            {
                UIManager.Instance.ShowMessage("跨楼层移动需要更多资金");
                return false;
            }

            // 从原楼层移除
            var sourceFloor = floorManager.GetFloor(bi.floorId);
            if (sourceFloor == null) return false;

            sourceFloor.RemoveBuilding(bi, false); // 不返还蓝图

            // 移动GameObject到新楼层
            bi.transform.SetPositionAndRotation(targetFloor.GridToWorld(newPos), Quaternion.Euler(0, 0, newRot * 90));
            bi.transform.SetParent(targetFloor.buildingContainer);

            // 注册到新楼层
            if (!targetFloor.RegisterExistingBuilding(bi, newPos, newRot))
            {
                // 如果注册失败，恢复到原楼层
                sourceFloor.RegisterExistingBuilding(bi, bi.gridPosition, bi.rotation);
                bi.transform.SetParent(sourceFloor.buildingContainer);
                return false;
            }

            // 扣除资源
            resourceManager.SpendMoney(bi.archetype.moveCost * 2);

            return true;
        }

        public void DestroyBuilding(BuildingInstance bi)
        {
            var floor = floorManager.GetFloor(bi.floorId);
            floor.RemoveBuilding(bi, refundBlueprint: true);
            Destroy(bi.gameObject);
            AudioManager.Instance.PlaySound("BuildingDestroyed");
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

        public void Cancel()
        {
            mode = Mode.None;
            selectedArchetype = null;
            selectedInstance = null;
            if (preview) Destroy(preview);
            previewRenderers = null;
        }

        // 获取当前目标楼层（供UI显示）
        public FloorGrid GetCurrentTargetFloor() => targetFloor;

        // 获取当前目标楼层ID
        public int GetCurrentTargetFloorId() => targetFloor != null ? targetFloor.floorId : -1;
    }
}
