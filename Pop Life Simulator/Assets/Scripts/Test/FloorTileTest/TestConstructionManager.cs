using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace PopLife.Test
{
    /// <summary>
    /// 简化版建造管理器 — 支持地板放置、货架放置、移动、拆除
    /// </summary>
    public class TestConstructionManager : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private TestFloorGrid grid;
        [SerializeField] private Camera mainCamera;

        [Header("可用地板")]
        [SerializeField] private FloorTileArchetype[] floorTileOptions;

        [Header("可用货架")]
        [SerializeField] private TestShelfArchetype[] shelfOptions;

        [Header("UI")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Transform buttonContainer;

        [Header("网格叠加")]
        [SerializeField] private TestGridOverlay gridOverlay;

        // 当前模式
        enum Mode { None, PlaceFloor, PlaceShelf, Move, Destroy, SetTarget, PlaceElevator }
        private Mode currentMode = Mode.None;

        // 当前选择
        private FloorTileArchetype selectedFloor;
        private TestShelfArchetype selectedShelf;
        private int currentRotation;

        // 预览
        private GameObject previewObj;
        private SpriteRenderer[] previewRenderers;
        private bool previewValid;

        // 移动
        private MonoBehaviour movingTarget; // FloorTileInstance 或 TestShelfInstance
        private Vector2Int moveOriginalPos;
        private int moveOriginalRot;

        // 带建筑移动
        private List<TestShelfInstance> movingBuildings = new();
        private List<Vector2Int> movingBuildingOriginalPositions = new();
        private List<Vector2Int> movingBuildingOffsets = new(); // 相对地板的偏移

        // Agent 测试
        private TestAgent spawnedAgent;

        // 电梯放置（两次点击）
        private Vector2Int elevatorFirstClick;
        private bool hasElevatorFirstClick;

        private static readonly Color validColor = new(0.5f, 1f, 0.5f, 0.5f);
        private static readonly Color invalidColor = new(1f, 0.4f, 0.4f, 0.5f);

        void Start()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            GenerateUI();
            SetStatus("Ready. Select an item to place.");
        }

        void Update()
        {
            // 右键取消
            if (Input.GetMouseButtonDown(1))
            {
                CancelCurrentMode();
                return;
            }

            // R键旋转
            if (Input.GetKeyDown(KeyCode.R) && currentMode == Mode.PlaceFloor)
            {
                currentRotation = (currentRotation + 1) % 2; // 0 或 1（矩形只有两种）
                RecreatePreview();
            }

            switch (currentMode)
            {
                case Mode.PlaceFloor:
                    UpdatePlaceFloorPreview();
                    if (Input.GetMouseButtonDown(0) && !IsOverUI())
                        HandlePlaceFloor();
                    break;
                case Mode.PlaceShelf:
                    UpdatePlaceShelfPreview();
                    if (Input.GetMouseButtonDown(0) && !IsOverUI())
                        HandlePlaceShelf();
                    break;
                case Mode.Move:
                    if (movingTarget == null)
                    {
                        UpdateMoveHover();
                        if (Input.GetMouseButtonDown(0) && !IsOverUI())
                            HandleMoveSelect();
                    }
                    else
                    {
                        UpdateMoveDrag();
                        if (Input.GetMouseButtonDown(0) && !IsOverUI())
                            HandleMoveDrop();
                    }
                    break;
                case Mode.SetTarget:
                    if (Input.GetMouseButtonDown(0) && !IsOverUI())
                        HandleSetTarget();
                    break;
                case Mode.PlaceElevator:
                    if (Input.GetMouseButtonDown(0) && !IsOverUI())
                        HandlePlaceElevator();
                    break;
                case Mode.Destroy:
                    UpdateDestroyHover();
                    if (Input.GetMouseButtonDown(0) && !IsOverUI())
                        HandleDestroy();
                    break;
            }

            // Agent 状态持续更新
            if (spawnedAgent != null && currentMode == Mode.SetTarget)
            {
                SetStatus($"Agent: {spawnedAgent.GetStatusText()}. Click to set new target.");
            }
        }

        // ========== UI 生成 ==========

        private void GenerateUI()
        {
            if (buttonContainer == null) return;

            foreach (var floor in floorTileOptions)
            {
                CreateButton($"Floor {floor.tileSize.x}x{floor.tileSize.y}", () => SelectFloorTile(floor));
            }
            foreach (var shelf in shelfOptions)
            {
                CreateButton($"Shelf {shelf.id}", () => SelectShelf(shelf));
            }
            CreateButton("Move", EnterMoveMode);
            CreateButton("Destroy", EnterDestroyMode);
            CreateButton("Elevator", EnterElevatorMode);
            CreateButton("Spawn Agent", SpawnAgent);
            CreateButton("Set Target", EnterSetTargetMode);
            CreateButton("Cancel", CancelCurrentMode);
        }

        private void CreateButton(string label, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(label, typeof(RectTransform));
            go.transform.SetParent(buttonContainer, false);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(action);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var txt = textGo.AddComponent<TextMeshProUGUI>();
            txt.text = label;
            txt.fontSize = 14;
            txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.Center;

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = 35;
            layout.preferredWidth = 120;
        }

        // ========== 模式切换 ==========

        public void SelectFloorTile(FloorTileArchetype arch)
        {
            ClearPreview();
            currentMode = Mode.PlaceFloor;
            selectedFloor = arch;
            currentRotation = 0;
            NotifyBuildMode(true);
            SetStatus($"Place Floor {arch.tileSize.x}x{arch.tileSize.y}. Left click to place, R to rotate, Right click to cancel.");
            CreateFloorPreview();
        }

        public void SelectShelf(TestShelfArchetype arch)
        {
            ClearPreview();
            currentMode = Mode.PlaceShelf;
            selectedShelf = arch;
            NotifyBuildMode(true);
            SetStatus($"Place Shelf. Left click to place, Right click to cancel.");
            CreateShelfPreview();
        }

        public void EnterMoveMode()
        {
            ClearPreview();
            currentMode = Mode.Move;
            movingTarget = null;
            NotifyBuildMode(true);
            SetStatus("Move mode. Click on a floor tile or shelf to move it.");
        }

        public void EnterDestroyMode()
        {
            ClearPreview();
            currentMode = Mode.Destroy;
            NotifyBuildMode(true);
            SetStatus("Destroy mode. Click on a floor tile or shelf to destroy it.");
        }

        public void CancelCurrentMode()
        {
            // 如果正在移动，回滚到原始位置
            if (movingTarget != null)
            {
                if (movingTarget is FloorTileInstance fti)
                {
                    // 先回滚地板
                    grid.ReregisterFloorTile(fti, moveOriginalPos, moveOriginalRot, skipSupportCheck: true);

                    // 再回滚建筑
                    for (int i = 0; i < movingBuildings.Count; i++)
                        grid.ReregisterBuilding(movingBuildings[i], movingBuildingOriginalPositions[i]);
                }
                else if (movingTarget is TestShelfInstance si)
                {
                    grid.ReregisterBuilding(si, moveOriginalPos);
                }

                RestorePreviewColor();
                previewObj = null;
                previewRenderers = null;
                movingTarget = null;
                movingBuildings.Clear();
                movingBuildingOriginalPositions.Clear();
                movingBuildingOffsets.Clear();
            }
            else
            {
                ClearPreview();
            }

            currentMode = Mode.None;
            selectedFloor = null;
            selectedShelf = null;
            NotifyBuildMode(false);
            SetStatus("Cancelled.");
        }

        private void NotifyBuildMode(bool buildMode)
        {
            if (gridOverlay != null)
                gridOverlay.SetBuildMode(buildMode);
        }

        // ========== 地板放置 ==========

        private void CreateFloorPreview()
        {
            if (selectedFloor == null) return;
            if (selectedFloor.prefab != null)
            {
                previewObj = Instantiate(selectedFloor.prefab);
            }
            else
            {
                // 生成占位预览
                var size = selectedFloor.GetEffectiveSize(currentRotation);
                previewObj = new GameObject("FloorPreview");
                var sr = previewObj.AddComponent<SpriteRenderer>();
                sr.sprite = CreatePreviewSprite(size.x, size.y);
                sr.sortingOrder = 100;
            }
            previewRenderers = previewObj.GetComponentsInChildren<SpriteRenderer>();
            SetPreviewColor(validColor);

            // 禁用碰撞
            foreach (var col in previewObj.GetComponentsInChildren<Collider2D>())
                col.enabled = false;
        }

        private void RecreatePreview()
        {
            ClearPreview();
            if (currentMode == Mode.PlaceFloor)
                CreateFloorPreview();
        }

        private void UpdatePlaceFloorPreview()
        {
            if (previewObj == null || selectedFloor == null) return;

            var mouseWorld = GetMouseWorldPos();
            var gridPos = grid.WorldToGrid(mouseWorld);
            previewObj.transform.position = grid.GridToWorld(gridPos);

            var fp = selectedFloor.GetFootprint(currentRotation);
            previewValid = grid.CanPlaceFloorTile(fp, gridPos);
            SetPreviewColor(previewValid ? validColor : invalidColor);
        }

        private void HandlePlaceFloor()
        {
            if (!previewValid || selectedFloor == null) return;

            var mouseWorld = GetMouseWorldPos();
            var gridPos = grid.WorldToGrid(mouseWorld);

            var inst = grid.PlaceFloorTile(selectedFloor, gridPos, currentRotation);
            if (inst != null)
            {
                SetStatus($"Floor placed at {gridPos}.");
            }
        }

        // ========== 货架放置 ==========

        private void CreateShelfPreview()
        {
            if (selectedShelf == null) return;
            if (selectedShelf.prefab != null)
            {
                previewObj = Instantiate(selectedShelf.prefab);
            }
            else
            {
                previewObj = new GameObject("ShelfPreview");
                var sr = previewObj.AddComponent<SpriteRenderer>();
                sr.sprite = CreatePreviewSprite(1, 1);
                sr.sortingOrder = 100;
            }
            previewRenderers = previewObj.GetComponentsInChildren<SpriteRenderer>();
            SetPreviewColor(validColor);

            foreach (var col in previewObj.GetComponentsInChildren<Collider2D>())
                col.enabled = false;
        }

        private void UpdatePlaceShelfPreview()
        {
            if (previewObj == null || selectedShelf == null) return;

            var mouseWorld = GetMouseWorldPos();
            var gridPos = grid.WorldToGrid(mouseWorld);
            previewObj.transform.position = grid.GridToWorld(gridPos);

            previewValid = grid.CanPlaceBuilding(selectedShelf.footprint, gridPos);
            SetPreviewColor(previewValid ? validColor : invalidColor);
        }

        private void HandlePlaceShelf()
        {
            if (!previewValid || selectedShelf == null) return;

            var mouseWorld = GetMouseWorldPos();
            var gridPos = grid.WorldToGrid(mouseWorld);

            var inst = grid.PlaceBuilding(selectedShelf, gridPos, 0);
            if (inst != null)
            {
                SetStatus($"Shelf placed at {gridPos}.");
            }
        }

        // ========== 移动 ==========

        private void UpdateMoveHover()
        {
            // 简化：不做悬停高亮
        }

        private void HandleMoveSelect()
        {
            var mouseWorld = GetMouseWorldPos();
            var hit = Physics2D.Raycast(mouseWorld, Vector2.zero);
            if (hit.collider == null) return;

            // 优先检测货架（单独移动）
            var shelf = hit.collider.GetComponent<TestShelfInstance>();
            if (shelf != null)
            {
                movingTarget = shelf;
                moveOriginalPos = shelf.gridPosition;
                grid.UnregisterBuilding(shelf);

                previewObj = shelf.gameObject;
                previewRenderers = previewObj.GetComponentsInChildren<SpriteRenderer>();
                SetPreviewColor(validColor);
                SetStatus("Moving shelf. Click to place, Right click to cancel.");
                return;
            }

            // 检测地板（带建筑一起移动）
            var floorTile = hit.collider.GetComponent<FloorTileInstance>();
            if (floorTile != null)
            {
                if (floorTile.isDefault)
                {
                    SetStatus("Cannot move the default floor.");
                    return;
                }

                movingTarget = floorTile;
                moveOriginalPos = floorTile.gridPosition;
                moveOriginalRot = floorTile.rotation;

                // 收集上面的建筑 + 记录偏移
                movingBuildings = grid.GetBuildingsOnFloorTile(floorTile);
                movingBuildingOriginalPositions.Clear();
                movingBuildingOffsets.Clear();
                foreach (var b in movingBuildings)
                {
                    movingBuildingOriginalPositions.Add(b.gridPosition);
                    movingBuildingOffsets.Add(b.gridPosition - floorTile.gridPosition);
                    grid.UnregisterBuilding(b);
                }

                // 反注册地板
                grid.UnregisterFloorTile(floorTile);

                // 收集所有要一起移动的 renderer
                var allRenderers = new System.Collections.Generic.List<SpriteRenderer>();
                allRenderers.AddRange(floorTile.GetComponentsInChildren<SpriteRenderer>());
                foreach (var b in movingBuildings)
                    allRenderers.AddRange(b.GetComponentsInChildren<SpriteRenderer>());

                previewObj = floorTile.gameObject;
                previewRenderers = allRenderers.ToArray();
                SetPreviewColor(validColor);

                int shelfCount = movingBuildings.Count;
                SetStatus($"Moving floor tile with {shelfCount} building(s). Click to place, Right click to cancel.");
            }
        }

        private void UpdateMoveDrag()
        {
            if (previewObj == null) return;

            var mouseWorld = GetMouseWorldPos();
            var gridPos = grid.WorldToGrid(mouseWorld);

            // 移动地板 GO
            previewObj.transform.position = grid.GridToWorld(gridPos);

            // 同步移动建筑 GO
            if (movingTarget is FloorTileInstance)
            {
                for (int i = 0; i < movingBuildings.Count; i++)
                {
                    var newShelfPos = gridPos + movingBuildingOffsets[i];
                    movingBuildings[i].transform.position = grid.GridToWorld(newShelfPos);
                }
            }

            // 验证
            if (movingTarget is FloorTileInstance fti)
            {
                previewValid = grid.CanMoveFloorTileWithBuildings(fti, movingBuildings, gridPos, fti.rotation);
            }
            else if (movingTarget is TestShelfInstance si)
            {
                previewValid = grid.CanPlaceBuilding(si.archetype.footprint, gridPos);
            }

            SetPreviewColor(previewValid ? validColor : invalidColor);
        }

        private void HandleMoveDrop()
        {
            if (!previewValid) return;

            var mouseWorld = GetMouseWorldPos();
            var gridPos = grid.WorldToGrid(mouseWorld);

            if (movingTarget is FloorTileInstance fti)
            {
                // 先注册地板
                grid.ReregisterFloorTile(fti, gridPos, fti.rotation);

                // 再注册建筑（按偏移）
                for (int i = 0; i < movingBuildings.Count; i++)
                {
                    var newShelfPos = gridPos + movingBuildingOffsets[i];
                    grid.ReregisterBuilding(movingBuildings[i], newShelfPos);
                }

                SetStatus($"Floor tile + {movingBuildings.Count} building(s) moved to {gridPos}.");
            }
            else if (movingTarget is TestShelfInstance si)
            {
                grid.ReregisterBuilding(si, gridPos);
                SetStatus($"Shelf moved to {gridPos}.");
            }

            FinishMove();
        }

        private void FinishMove()
        {
            RestorePreviewColor();
            previewObj = null;
            previewRenderers = null;
            movingTarget = null;
            movingBuildings.Clear();
            movingBuildingOriginalPositions.Clear();
            movingBuildingOffsets.Clear();
            currentMode = Mode.Move;
            SetStatus("Move mode. Click on a floor tile or shelf to move it.");
        }

        // ========== 电梯放置 ==========

        public void EnterElevatorMode()
        {
            ClearPreview();
            currentMode = Mode.PlaceElevator;
            hasElevatorFirstClick = false;
            NotifyBuildMode(true);
            SetStatus("Elevator mode. Click first cell (bottom floor), then second cell (top floor).");
        }

        private void HandlePlaceElevator()
        {
            var mouseWorld = GetMouseWorldPos();
            var gridPos = grid.WorldToGrid(mouseWorld);

            if (!hasElevatorFirstClick)
            {
                // 第一次点击
                if (!grid.HasFloorTileAt(gridPos))
                {
                    SetStatus("No floor tile here. Click on a floor tile.");
                    return;
                }
                if (grid.HasBuildingAt(gridPos) && !grid.IsElevatorAt(gridPos))
                {
                    SetStatus("Cell occupied by a building. Choose an empty cell or an existing elevator.");
                    return;
                }

                elevatorFirstClick = gridPos;
                hasElevatorFirstClick = true;
                SetStatus($"Bottom cell: {gridPos}. Now click the top cell on an adjacent floor.");
            }
            else
            {
                // 第二次点击
                if (gridPos == elevatorFirstClick)
                {
                    SetStatus("Same cell. Click a different cell on an adjacent floor.");
                    return;
                }

                if (grid.PlaceElevator(elevatorFirstClick, gridPos))
                {
                    SetStatus($"Elevator placed: {elevatorFirstClick} → {gridPos}");
                    hasElevatorFirstClick = false;
                }
                else
                {
                    // 尝试反向
                    if (grid.PlaceElevator(gridPos, elevatorFirstClick))
                    {
                        SetStatus($"Elevator placed: {gridPos} → {elevatorFirstClick}");
                        hasElevatorFirstClick = false;
                    }
                    else
                    {
                        SetStatus("Invalid: cells must be on adjacent floors, both with floor tiles, both empty.");
                        hasElevatorFirstClick = false;
                    }
                }
            }
        }

        // ========== Agent 测试 ==========

        public void SpawnAgent()
        {
            ClearPreview();
            currentMode = Mode.None;

            // 在默认一楼中心生成
            var defaultFloor = grid.GetDefaultFloor();
            if (defaultFloor == null)
            {
                SetStatus("No default floor found.");
                return;
            }

            var size = defaultFloor.archetype.GetEffectiveSize(defaultFloor.rotation);
            var center = grid.GridToWorld(defaultFloor.gridPosition)
                + new Vector3(size.x * grid.cellSize * 0.5f, size.y * grid.cellSize * 0.5f, 0);

            if (spawnedAgent != null)
                Destroy(spawnedAgent.gameObject);

            spawnedAgent = TestAgent.CreateAt(center, grid: grid);
            SetStatus($"Agent spawned at default floor center. Use 'Set Target' to test pathfinding.");
        }

        public void EnterSetTargetMode()
        {
            if (spawnedAgent == null)
            {
                SetStatus("No agent. Click 'Spawn Agent' first.");
                return;
            }

            ClearPreview();
            currentMode = Mode.SetTarget;
            SetStatus("Set Target mode. Click on a floor tile to set agent destination.");
        }

        private void HandleSetTarget()
        {
            if (spawnedAgent == null) return;

            var mouseWorld = GetMouseWorldPos();
            var gridPos = grid.WorldToGrid(mouseWorld);

            // 目标设在格子中心
            var targetPos = grid.GridToWorld(gridPos) + new Vector3(grid.cellSize * 0.5f, grid.cellSize * 0.5f, 0);

            spawnedAgent.SetDestination(targetPos);
            SetStatus($"Target set to {gridPos}. Agent: {spawnedAgent.GetStatusText()}");
        }

        // ========== 拆除 ==========

        private void UpdateDestroyHover()
        {
            // 简化：不做悬停高亮
        }

        private void HandleDestroy()
        {
            var mouseWorld = GetMouseWorldPos();
            var hit = Physics2D.Raycast(mouseWorld, Vector2.zero);
            if (hit.collider == null) return;

            var shelf = hit.collider.GetComponent<TestShelfInstance>();
            if (shelf != null)
            {
                grid.RemoveBuilding(shelf);
                SetStatus("Shelf destroyed.");
                return;
            }

            var floorTile = hit.collider.GetComponent<FloorTileInstance>();
            if (floorTile != null)
            {
                if (floorTile.isDefault)
                {
                    SetStatus("Cannot destroy the default floor.");
                    return;
                }
                if (grid.HasBuildingsOnFloorTile(floorTile))
                {
                    var buildings = grid.GetBuildingsOnFloorTile(floorTile);
                    SetStatus($"Cannot destroy: {buildings.Count} building(s) on this floor tile. Remove them first.");
                    return;
                }
                if (!grid.RemoveFloorTile(floorTile))
                {
                    SetStatus("Cannot destroy: other floor tiles above depend on it.");
                    return;
                }
                SetStatus("Floor tile destroyed.");
            }
        }

        // ========== 工具方法 ==========

        private Vector3 GetMouseWorldPos()
        {
            var mouseScreen = Input.mousePosition;
            mouseScreen.z = -mainCamera.transform.position.z;
            return mainCamera.ScreenToWorldPoint(mouseScreen);
        }

        private bool IsOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void ClearPreview()
        {
            if (previewObj != null && movingTarget == null)
            {
                Destroy(previewObj);
            }
            previewObj = null;
            previewRenderers = null;
        }

        private void SetPreviewColor(Color color)
        {
            if (previewRenderers == null) return;
            foreach (var sr in previewRenderers)
                sr.color = color;
        }

        private void RestorePreviewColor()
        {
            if (previewRenderers == null) return;
            foreach (var sr in previewRenderers)
                sr.color = Color.white;
        }

        private void SetStatus(string msg)
        {
            if (statusText != null)
                statusText.text = msg;
            Debug.Log($"[FloorTileTest] {msg}");
        }

        private Sprite CreatePreviewSprite(int w, int h)
        {
            int ppu = 16;
            var tex = new Texture2D(w * ppu, h * ppu);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color[w * ppu * h * ppu];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w * ppu, h * ppu), Vector2.zero, ppu);
        }

    }
}
