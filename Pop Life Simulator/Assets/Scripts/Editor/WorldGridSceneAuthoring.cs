using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using PopLife.Runtime;
using PopLife.Data;
using PopLife.AlanBot;
using System.Collections.Generic;

namespace PopLife.Editor
{
    /// <summary>
    /// Scene GUI 主处理器 — 在 SceneView 中绘制网格可视化并处理鼠标放置/擦除事件。
    /// 性能要点：
    ///   - 所有绘制限 EventType.Repaint
    ///   - 高频批量绘制用 GL（grid lines, filled cells）
    ///   - 少量特殊高亮用 Handles（Default border, preview footprint）
    ///   - Snapshot 用脏标记缓存（场景不变时不重建）
    /// </summary>
    [InitializeOnLoad]
    public static class WorldGridSceneAuthoring
    {
        // ── 颜色 ──
        private static readonly Color ColorGridLine = new(1f, 1f, 1f, 0.15f);
        private static readonly Color ColorOccupied = new(0.3f, 0.5f, 1f, 0.25f);
        private static readonly Color ColorDefaultBorder = new(0.2f, 0.4f, 1f, 0.9f);
        private static readonly Color ColorValid = new(0.2f, 1f, 0.2f, 0.4f);
        private static readonly Color ColorInvalid = new(1f, 0.2f, 0.2f, 0.4f);
        private static readonly Color ColorPlaceable = new(1f, 0.6f, 0.1f, 0.2f);
        private static readonly Color ColorWalkable = new(0.2f, 0.9f, 0.3f, 0.2f);
        private static readonly Color ColorPortal = new(0.2f, 0.9f, 0.9f, 0.25f);
        private static readonly Color ColorInteriorOccupied = new(0.5f, 0.5f, 0.5f, 0.3f);
        private static readonly Color ColorEraseHover = new(1f, 0.3f, 0.3f, 0.5f);

        // ── World Snapshot 缓存（脏标记） ──
        private static EditorGridSnapshot cachedSnap;
        private static bool worldDirty = true;

        // ── Interior 缓存（脏标记 + tile ID） ──
        private static InteriorGrid cachedInterior;
        private static string cachedInteriorTileId;
        private static bool interiorDirty = true;

        // ── GL 材质（单例） ──
        private static Material glMat;
        private static Material GetGLMat()
        {
            if (glMat == null)
            {
                var shader = Shader.Find("Hidden/Internal-Colored");
                glMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                glMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                glMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                glMat.SetInt("_ZWrite", 0);
                glMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            }
            return glMat;
        }

        // ── 复用顶点 buffer ──
        private static readonly Vector3[] quadVerts = new Vector3[4];

        private static EditorGridSnapshot GetSnapshot(WorldGrid wg)
        {
            // 用户在 Inspector 改 gridSize/cellSize/origin 不会触发 worldDirty，
            // 所以这里也比对当前 WorldGrid 的关键参数，发现不一致直接重建快照
            Vector3 originPos = wg.origin != null ? wg.origin.position : wg.transform.position;
            bool paramsChanged = cachedSnap != null
                && (cachedSnap.GridSize != wg.gridSize
                    || !Mathf.Approximately(cachedSnap.CellSize, wg.cellSize)
                    || cachedSnap.Origin != originPos);

            if (cachedSnap == null || worldDirty || paramsChanged)
            {
                cachedSnap = EditorGridSnapshot.Build(wg);
                worldDirty = false;
            }
            return cachedSnap;
        }

        private static InteriorGrid GetInterior(FloorTileInstance tile)
        {
            string tileId = tile.instanceId ?? tile.name;
            if (cachedInterior == null || interiorDirty || cachedInteriorTileId != tileId)
            {
                cachedInterior = EditorGridSnapshot.BuildEditorInterior(tile);
                cachedInteriorTileId = tileId;
                interiorDirty = false;
            }
            return cachedInterior;
        }

        private static void InvalidateWorld() => worldDirty = true;
        private static void InvalidateInterior() => interiorDirty = true;
        private static void InvalidateAll() { worldDirty = true; interiorDirty = true; }

        // ================================================================
        //  生命周期
        // ================================================================

        static WorldGridSceneAuthoring()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private static void OnUndoRedo() => InvalidateAll();

        private static void OnSceneGUI(SceneView sceneView)
        {
            var state = WorldGridAuthoringState.instance;

            var wg = Object.FindFirstObjectByType<WorldGrid>();
            if (wg == null) return;

            // 纯显示开关（独立于操作模式，不消耗事件）
            if (Event.current.type == EventType.Repaint)
            {
                Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

                if (state.showWorldGrid)
                {
                    var snap = GetSnapshot(wg);
                    DrawWorldGridGL(sceneView, snap);
                    DrawOccupiedCellsGL(snap, wg);
                }

                if (state.showInteriorGrid)
                {
                    var snapForInterior = GetSnapshot(wg);
                    var mouseForInterior = GetMouseWorldPosition();
                    var gridForInterior = snapForInterior.WorldToGrid(mouseForInterior);
                    var tileForInterior = snapForInterior.GetFloorTileAt(gridForInterior);
                    if (tileForInterior != null)
                    {
                        var interior = GetInterior(tileForInterior);
                        if (interior != null)
                            DrawInteriorGridGL(interior);
                    }
                }
            }

            // 操作模式
            if (state.mode == WorldGridAuthoringState.AuthoringMode.None)
                return;

            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            switch (state.mode)
            {
                case WorldGridAuthoringState.AuthoringMode.PlaceFloorTile:
                    HandlePlaceFloorTile(sceneView, wg, state);
                    break;
                case WorldGridAuthoringState.AuthoringMode.EraseFloorTile:
                    HandleEraseFloorTile(sceneView, wg, state);
                    break;
                case WorldGridAuthoringState.AuthoringMode.PlaceInteriorBuilding:
                    HandlePlaceInterior(sceneView, wg, state);
                    break;
                case WorldGridAuthoringState.AuthoringMode.EraseInteriorBuilding:
                    HandleEraseInterior(sceneView, wg, state);
                    break;
                case WorldGridAuthoringState.AuthoringMode.PlaceAlanBot:
                    HandlePlaceAlanBot(sceneView, wg, state);
                    break;
            }

            HandleCancelInput(state);

            // 操作模式激活时持续请求重绘，确保预览流畅
            // （不依赖 WorldGridDebugger 选中才触发的 RepaintAll）
            sceneView.Repaint();
        }

        // ================================================================
        //  WorldGrid — PlaceFloorTile
        // ================================================================

        private static void HandlePlaceFloorTile(SceneView sv, WorldGrid wg, WorldGridAuthoringState state)
        {
            var snap = GetSnapshot(wg);
            var mouseWorld = GetMouseWorldPosition();
            var gridPos = snap.WorldToGrid(mouseWorld);

            // 只在 Repaint 时绘制
            if (Event.current.type == EventType.Repaint)
            {
                // 如果 visibility toggle 没开，由 handler 补画
                if (!state.showWorldGrid)
                {
                    DrawWorldGridGL(sv, snap);
                    DrawOccupiedCellsGL(snap, wg);
                }

                // 预览 footprint（少量格子，用 Handles）
                if (state.selectedArchetype is FloorTileArchetype fta)
                {
                    var fp = fta.GetRotatedFootprint(state.rotation);
                    bool canPlace = snap.CanPlaceFloorTile(fp, gridPos, skipSupportCheck: state.markAsDefault);
                    Color previewColor = canPlace ? ColorValid : ColorInvalid;
                    foreach (var off in fp)
                        DrawFilledCellHandles(snap.GridToWorld(gridPos + off), snap.CellSize, previewColor);
                }
            }

            // 点击放置
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (state.selectedArchetype is FloorTileArchetype floorArch && floorArch.prefab != null)
                {
                    var fp = floorArch.GetRotatedFootprint(state.rotation);
                    if (snap.CanPlaceFloorTile(fp, gridPos, skipSupportCheck: state.markAsDefault))
                        PlaceFloorTileInScene(wg, snap, floorArch, gridPos, state);
                }
                e.Use();
            }

            DrawInfoLabel(sv, $"Place FloorTile | Grid: ({gridPos.x}, {gridPos.y}) | Rot: {state.rotation * 90}\u00b0");
        }

        // ================================================================
        //  WorldGrid — EraseFloorTile
        // ================================================================

        private static void HandleEraseFloorTile(SceneView sv, WorldGrid wg, WorldGridAuthoringState state)
        {
            var snap = GetSnapshot(wg);
            var mouseWorld = GetMouseWorldPosition();
            var gridPos = snap.WorldToGrid(mouseWorld);

            if (Event.current.type == EventType.Repaint)
            {
                if (!state.showWorldGrid)
                {
                    DrawWorldGridGL(sv, snap);
                    DrawOccupiedCellsGL(snap, wg);
                }

                // 高亮悬停 tile（少量格子，Handles）
                var hovered = snap.GetFloorTileAt(gridPos);
                if (hovered != null)
                {
                    var fp = hovered.archetype.GetRotatedFootprint(hovered.rotation);
                    foreach (var off in fp)
                        DrawFilledCellHandles(snap.GridToWorld(hovered.gridPosition + off), snap.CellSize, ColorEraseHover);
                }
            }

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                var hovered = snap.GetFloorTileAt(gridPos);
                if (hovered != null)
                {
                    var scene = hovered.gameObject.scene;
                    Undo.DestroyObjectImmediate(hovered.gameObject);
                    EditorSceneManager.MarkSceneDirty(scene);
                    InvalidateWorld();
                }
                e.Use();
            }

            DrawInfoLabel(sv, $"Erase FloorTile | Grid: ({gridPos.x}, {gridPos.y})");
        }

        // ================================================================
        //  Interior — PlaceInteriorBuilding
        // ================================================================

        private static void HandlePlaceInterior(SceneView sv, WorldGrid wg, WorldGridAuthoringState state)
        {
            var snap = GetSnapshot(wg);
            var mouseWorld = GetMouseWorldPosition();
            var gridPos = snap.WorldToGrid(mouseWorld);

            // 自动探测鼠标下方的 FloorTile
            var tile = snap.GetFloorTileAt(gridPos);
            if (tile == null) { DrawInfoLabel(sv, "Place Interior | Move mouse over a FloorTile"); return; }

            var interior = GetInterior(tile);
            if (interior == null) { DrawInfoLabel(sv, "Place Interior | FloorTile has no interior"); return; }

            var localPos = interior.WorldToLocal(mouseWorld);

            // Elevator 强制 rotation=0
            bool isElevator = state.selectedArchetype is ElevatorArchetype;
            int rotation = isElevator ? 0 : state.rotation;

            if (Event.current.type == EventType.Repaint)
            {
                if (!state.showInteriorGrid)
                    DrawInteriorGridGL(interior);

                // 预览 footprint（少量格子，Handles）
                if (state.selectedArchetype != null)
                {
                    var fp = state.selectedArchetype.GetRotatedFootprint(rotation);
                    bool canPlace = state.selectedArchetype is IInteriorPlaceable ip
                        ? ip.ValidateInteriorPlacement(interior, localPos, rotation)
                        : interior.CanPlace(fp, localPos);

                    Color previewColor = canPlace ? ColorValid : ColorInvalid;
                    foreach (var off in fp)
                        DrawFilledCellHandles(interior.LocalToWorld(localPos + off), interior.CellSize, previewColor);
                }
            }

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (state.selectedArchetype != null && state.selectedArchetype.prefab != null)
                {
                    var fp = state.selectedArchetype.GetRotatedFootprint(rotation);
                    bool canPlace = state.selectedArchetype is IInteriorPlaceable ip
                        ? ip.ValidateInteriorPlacement(interior, localPos, rotation)
                        : interior.CanPlace(fp, localPos);

                    if (canPlace)
                    {
                        if (isElevator)
                            PlaceElevatorInScene(tile, interior, localPos, (ElevatorArchetype)state.selectedArchetype);
                        else
                            PlaceInteriorBuildingInScene(tile, interior, localPos, state);
                    }
                }
                e.Use();
            }

            string rotLabel = isElevator ? "fixed" : $"{rotation * 90}\u00b0";
            DrawInfoLabel(sv, $"Place Interior | Local: ({localPos.x}, {localPos.y}) | Rot: {rotLabel} | Tile: {tile.name}");
        }

        // ================================================================
        //  Interior — EraseInteriorBuilding
        // ================================================================

        private static void HandleEraseInterior(SceneView sv, WorldGrid wg, WorldGridAuthoringState state)
        {
            var snap = GetSnapshot(wg);
            var mouseWorld = GetMouseWorldPosition();
            var gridPos = snap.WorldToGrid(mouseWorld);

            // 自动探测鼠标下方的 FloorTile
            var tile = snap.GetFloorTileAt(gridPos);
            if (tile == null) { DrawInfoLabel(sv, "Erase Interior | Move mouse over a FloorTile"); return; }

            var interior = GetInterior(tile);
            if (interior == null) { DrawInfoLabel(sv, "Erase Interior | FloorTile has no interior"); return; }

            var localPos = interior.WorldToLocal(mouseWorld);

            if (Event.current.type == EventType.Repaint)
            {
                if (!state.showInteriorGrid)
                    DrawInteriorGridGL(interior);

                // 高亮悬停建筑（少量格子，Handles）
                var hoveredBuilding = FindInteriorBuildingAtLocal(tile, interior, localPos);
                if (hoveredBuilding != null)
                {
                    var fp = hoveredBuilding.archetype.GetRotatedFootprint(hoveredBuilding.rotation);
                    var biLocalPos = interior.WorldToLocal(hoveredBuilding.transform.position);
                    foreach (var off in fp)
                        DrawFilledCellHandles(interior.LocalToWorld(biLocalPos + off), interior.CellSize, ColorEraseHover);
                }
            }

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                var hoveredBuilding = FindInteriorBuildingAtLocal(tile, interior, localPos);
                if (hoveredBuilding != null)
                {
                    Undo.DestroyObjectImmediate(hoveredBuilding.gameObject);
                    EditorSceneManager.MarkSceneDirty(tile.gameObject.scene);
                    InvalidateInterior();
                }
                e.Use();
            }

            DrawInfoLabel(sv, $"Erase Interior | Local: ({localPos.x}, {localPos.y}) | Tile: {tile.name}");
        }

        private static void PlaceElevatorInScene(FloorTileInstance tile, InteriorGrid interior,
            Vector2Int localPos, ElevatorArchetype arch)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(arch.prefab, tile.InteriorContainer);
            go.transform.position = interior.LocalToWorld(localPos);
            go.transform.rotation = Quaternion.identity;

            var door = go.GetComponent<ElevatorDoorInstance>();
            if (door != null)
            {
                door.gridPosition = localPos;
                door.rotation = 0;
                door.hostFloorTileInstanceId = !string.IsNullOrEmpty(tile.instanceId) ? tile.instanceId : tile.name;
                door.instanceId = System.Guid.NewGuid().ToString();
                door.archetype = arch;

                // 设置楼层标签
                door.SetFloorLabel(tile.FloorDisplayName);
            }

            Undo.RegisterCreatedObjectUndo(go, "Place Elevator Door");
            EditorUtility.SetDirty(go);
            EditorSceneManager.MarkSceneDirty(go.scene);
            InvalidateInterior();
        }

        // ================================================================
        //  放置实现
        // ================================================================

        private static void PlaceFloorTileInScene(WorldGrid wg, EditorGridSnapshot snap,
            FloorTileArchetype arch, Vector2Int gridPos, WorldGridAuthoringState state)
        {
            var worldPos = snap.GridToWorld(gridPos);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(arch.prefab, wg.buildingContainer);
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.Euler(0, 0, state.rotation * 90);

            var fti = go.GetComponent<FloorTileInstance>();
            if (fti != null)
            {
                fti.gridPosition = gridPos;
                fti.rotation = state.rotation;
                fti.hostFloorTileInstanceId = "";
                fti.instanceId = System.Guid.NewGuid().ToString();
                fti.archetype = arch;
                fti.SetStoreId(arch.StoreId, rebuildNav: false);
                if (state.markAsDefault) fti.IsDefault = true;

                // 玩家锁定（同时设置 move + destroy 锁，匹配单 toggle 语义）
                if (state.markAsLocked)
                {
                    var so = new SerializedObject(fti);
                    var moveField = so.FindProperty("editorLockedMove");
                    var destroyField = so.FindProperty("editorLockedDestroy");
                    if (moveField != null) moveField.boolValue = true;
                    if (destroyField != null) destroyField.boolValue = true;
                    so.ApplyModifiedProperties();
                }
            }

            Undo.RegisterCreatedObjectUndo(go, "Place FloorTile");
            EditorUtility.SetDirty(go);
            EditorSceneManager.MarkSceneDirty(go.scene);
            InvalidateWorld();
        }

        private static void PlaceInteriorBuildingInScene(FloorTileInstance tile, InteriorGrid interior,
            Vector2Int localPos, WorldGridAuthoringState state)
        {
            var arch = state.selectedArchetype;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(arch.prefab, tile.InteriorContainer);
            go.transform.position = interior.LocalToWorld(localPos);
            go.transform.rotation = Quaternion.Euler(0, 0, state.rotation * 90);

            var bi = go.GetComponent<BuildingInstance>();
            if (bi != null)
            {
                bi.gridPosition = localPos;
                bi.rotation = state.rotation;
                bi.hostFloorTileInstanceId = !string.IsNullOrEmpty(tile.instanceId) ? tile.instanceId : tile.name;
                bi.instanceId = System.Guid.NewGuid().ToString();
                bi.archetype = arch;

                if (state.lockMove || state.lockDestroy)
                {
                    var so = new SerializedObject(bi);
                    var moveField = so.FindProperty("editorLockedMove");
                    var destroyField = so.FindProperty("editorLockedDestroy");
                    if (moveField != null) moveField.boolValue = state.lockMove;
                    if (destroyField != null) destroyField.boolValue = state.lockDestroy;
                    so.ApplyModifiedProperties();
                }
            }

            Undo.RegisterCreatedObjectUndo(go, "Place Interior Building");
            EditorUtility.SetDirty(go);
            EditorSceneManager.MarkSceneDirty(go.scene);
            InvalidateInterior();
        }

        // ================================================================
        //  AlanBot — PlaceAlanBot（自动探测 FloorTile，放置到 walkable cell 中心）
        // ================================================================

        private static void HandlePlaceAlanBot(SceneView sv, WorldGrid wg, WorldGridAuthoringState state)
        {
            var snap = GetSnapshot(wg);
            var mouseWorld = GetMouseWorldPosition();
            var gridPos = snap.WorldToGrid(mouseWorld);

            var tile = snap.GetFloorTileAt(gridPos);
            if (tile == null)
            {
                DrawInfoLabel(sv, "Place AlanBot | Move mouse over a FloorTile");
                return;
            }

            var interior = GetInterior(tile);
            if (interior == null) { DrawInfoLabel(sv, "Place AlanBot | FloorTile has no interior"); return; }

            var localPos = interior.WorldToLocal(mouseWorld);
            bool canPlace = interior.InBounds(localPos)
                         && interior.IsWalkable(localPos)
                         && !interior.IsOccupied(localPos);

            if (Event.current.type == EventType.Repaint)
            {
                if (!state.showInteriorGrid)
                    DrawInteriorGridGL(interior);

                // 单格预览
                Color previewColor = canPlace ? ColorValid : ColorInvalid;
                DrawFilledCellHandles(interior.LocalToWorld(localPos), interior.CellSize, previewColor);
            }

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (canPlace && state.alanBotPrefab != null)
                    PlaceAlanBotInScene(wg, tile, interior, localPos, state);
                e.Use();
            }

            DrawInfoLabel(sv, $"Place AlanBot | Local: ({localPos.x}, {localPos.y}) | Tile: {tile.name}");
        }

        private static void PlaceAlanBotInScene(WorldGrid wg, FloorTileInstance tile,
            InteriorGrid interior, Vector2Int localPos, WorldGridAuthoringState state)
        {
            // 检查场景中是否已有 AlanBot，有则删除（单例）
            var existing = Object.FindFirstObjectByType<AlanBotController>();
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            // cell 中心位置
            Vector3 snapped = interior.LocalToWorld(localPos);
            float half = interior.CellSize * 0.5f;
            Vector3 pos = new Vector3(snapped.x + half, snapped.y + half, 0f);

            // 实例化到 WorldGrid.buildingContainer 下（与 FloorTile 同级）
            var go = (GameObject)PrefabUtility.InstantiatePrefab(state.alanBotPrefab, wg.buildingContainer);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.identity;

            Undo.RegisterCreatedObjectUndo(go, "Place AlanBot");
            EditorUtility.SetDirty(go);
            EditorSceneManager.MarkSceneDirty(go.scene);
        }

        // ================================================================
        //  取消输入
        // ================================================================

        private static void HandleCancelInput(WorldGridAuthoringState state)
        {
            var e = Event.current;
            if ((e.type == EventType.MouseDown && e.button == 1) ||
                (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape))
            {
                state.mode = WorldGridAuthoringState.AuthoringMode.None;
                state.Save();
                SceneView.RepaintAll();
                e.Use();
            }
        }

        // ================================================================
        //  GL 批量绘制 — 高频大面积
        // ================================================================

        /// <summary> 世界网格线（GL.LINES，仅可见范围） </summary>
        private static void DrawWorldGridGL(SceneView sv, EditorGridSnapshot snap)
        {
            var cam = sv.camera;
            Vector3 camPos = cam.transform.position;
            float orthoSize = cam.orthographic ? cam.orthographicSize : 20f;
            float halfW = orthoSize * cam.aspect;
            float halfH = orthoSize;

            Vector2Int minG = snap.WorldToGrid(new Vector3(camPos.x - halfW - snap.CellSize, camPos.y - halfH - snap.CellSize, 0));
            Vector2Int maxG = snap.WorldToGrid(new Vector3(camPos.x + halfW + snap.CellSize, camPos.y + halfH + snap.CellSize, 0));

            int xMin = Mathf.Max(0, minG.x), yMin = Mathf.Max(0, minG.y);
            int xMax = Mathf.Min(snap.GridSize.x, maxG.x + 1), yMax = Mathf.Min(snap.GridSize.y, maxG.y + 1);

            var mat = GetGLMat(); mat.SetPass(0);
            GL.PushMatrix(); GL.MultMatrix(Handles.matrix);
            GL.Begin(GL.LINES); GL.Color(ColorGridLine);

            for (int x = xMin; x <= xMax; x++)
            {
                var b = snap.GridToWorld(new Vector2Int(x, yMin));
                var t = snap.GridToWorld(new Vector2Int(x, yMax));
                GL.Vertex3(b.x, b.y, 0); GL.Vertex3(t.x, t.y, 0);
            }
            for (int y = yMin; y <= yMax; y++)
            {
                var l = snap.GridToWorld(new Vector2Int(xMin, y));
                var r = snap.GridToWorld(new Vector2Int(xMax, y));
                GL.Vertex3(l.x, l.y, 0); GL.Vertex3(r.x, r.y, 0);
            }

            GL.End(); GL.PopMatrix();
        }

        /// <summary> 已有 tile 占地填充（GL.QUADS）+ Default 边框（Handles, 少量） </summary>
        private static void DrawOccupiedCellsGL(EditorGridSnapshot snap, WorldGrid wg)
        {
            if (wg.buildingContainer == null) return;

            float cs = snap.CellSize;
            var mat = GetGLMat(); mat.SetPass(0);
            GL.PushMatrix(); GL.MultMatrix(Handles.matrix);
            GL.Begin(GL.QUADS); GL.Color(ColorOccupied);

            foreach (var fti in wg.buildingContainer.GetComponentsInChildren<FloorTileInstance>())
            {
                if (fti.archetype == null) continue;
                var fp = fti.archetype.GetRotatedFootprint(fti.rotation);
                foreach (var off in fp)
                {
                    var p = fti.gridPosition + off;
                    if (snap.InBounds(p))
                    {
                        var w = snap.GridToWorld(p);
                        GL.Vertex3(w.x, w.y, 0);
                        GL.Vertex3(w.x + cs, w.y, 0);
                        GL.Vertex3(w.x + cs, w.y + cs, 0);
                        GL.Vertex3(w.x, w.y + cs, 0);
                    }
                }
            }
            GL.End(); GL.PopMatrix();

            // Default 边框 — 用 Handles（少量、需要粗线）
            foreach (var fti in wg.buildingContainer.GetComponentsInChildren<FloorTileInstance>())
            {
                if (fti.archetype == null || !fti.IsDefault) continue;
                DrawFootprintBorderHandles(fti, snap, ColorDefaultBorder);
            }
        }

        /// <summary> Interior 网格填充（GL.QUADS）+ 边框线（GL.LINES） </summary>
        private static void DrawInteriorGridGL(InteriorGrid interior)
        {
            var gs = interior.GridSize;
            float cs = interior.CellSize;

            var mat = GetGLMat(); mat.SetPass(0);
            GL.PushMatrix(); GL.MultMatrix(Handles.matrix);

            // 按颜色分 pass 的 QUADS
            GL.Begin(GL.QUADS);
            for (int x = 0; x < gs.x; x++)
            {
                for (int y = 0; y < gs.y; y++)
                {
                    var pos = new Vector2Int(x, y);
                    bool occ = interior.IsOccupied(pos);
                    bool plc = interior.IsPlaceable(pos);
                    bool wlk = interior.IsWalkable(pos);

                    Color c;
                    if (occ) c = ColorInteriorOccupied;
                    else if (plc && wlk) c = ColorWalkable;
                    else if (plc) c = ColorPlaceable;
                    else if (wlk) c = ColorPortal;
                    else continue; // 什么都不是就不画

                    GL.Color(c);
                    var w = interior.LocalToWorld(pos);
                    GL.Vertex3(w.x, w.y, 0);
                    GL.Vertex3(w.x + cs, w.y, 0);
                    GL.Vertex3(w.x + cs, w.y + cs, 0);
                    GL.Vertex3(w.x, w.y + cs, 0);
                }
            }
            GL.End();

            // 边框线
            GL.Begin(GL.LINES); GL.Color(new Color(1f, 1f, 1f, 0.5f));
            for (int x = 0; x <= gs.x; x++)
            {
                var b = interior.LocalToWorld(new Vector2Int(x, 0));
                var t = interior.LocalToWorld(new Vector2Int(x, gs.y));
                GL.Vertex3(b.x, b.y, 0); GL.Vertex3(t.x, t.y, 0);
            }
            for (int y = 0; y <= gs.y; y++)
            {
                var l = interior.LocalToWorld(new Vector2Int(0, y));
                var r = interior.LocalToWorld(new Vector2Int(gs.x, y));
                GL.Vertex3(l.x, l.y, 0); GL.Vertex3(r.x, r.y, 0);
            }
            GL.End();

            GL.PopMatrix();
        }

        // ================================================================
        //  Handles 绘制 — 少量特殊高亮
        // ================================================================

        /// <summary> 单格填充（复用静态 buffer，少量预览用） </summary>
        private static void DrawFilledCellHandles(Vector3 worldPos, float cs, Color color)
        {
            quadVerts[0] = worldPos;
            quadVerts[1] = worldPos + new Vector3(cs, 0, 0);
            quadVerts[2] = worldPos + new Vector3(cs, cs, 0);
            quadVerts[3] = worldPos + new Vector3(0, cs, 0);
            Handles.DrawSolidRectangleWithOutline(quadVerts, color, Color.clear);
        }

        /// <summary> Footprint 外轮廓粗线 </summary>
        private static void DrawFootprintBorderHandles(FloorTileInstance fti, EditorGridSnapshot snap, Color color)
        {
            var fp = fti.archetype.GetRotatedFootprint(fti.rotation);
            var cellSet = new HashSet<Vector2Int>();
            foreach (var off in fp) cellSet.Add(fti.gridPosition + off);

            Handles.color = color;
            float cs = snap.CellSize;
            foreach (var cell in cellSet)
            {
                var w = snap.GridToWorld(cell);
                if (!cellSet.Contains(cell + Vector2Int.up))
                    Handles.DrawLine(w + new Vector3(0, cs, 0), w + new Vector3(cs, cs, 0), 2f);
                if (!cellSet.Contains(cell + Vector2Int.down))
                    Handles.DrawLine(w, w + new Vector3(cs, 0, 0), 2f);
                if (!cellSet.Contains(cell + Vector2Int.left))
                    Handles.DrawLine(w, w + new Vector3(0, cs, 0), 2f);
                if (!cellSet.Contains(cell + Vector2Int.right))
                    Handles.DrawLine(w + new Vector3(cs, 0, 0), w + new Vector3(cs, cs, 0), 2f);
            }
        }

        // ================================================================
        //  信息标签
        // ================================================================

        private static void DrawInfoLabel(SceneView sv, string text)
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10, 10, 500, 30));
            GUILayout.Label(text, EditorStyles.helpBox);
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        // ================================================================
        //  工具方法
        // ================================================================

        private static Vector3 GetMouseWorldPosition()
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            if (Mathf.Abs(ray.direction.z) < 0.0001f) return ray.origin;
            float t = -ray.origin.z / ray.direction.z;
            return ray.origin + ray.direction * t;
        }

        private static BuildingInstance FindInteriorBuildingAtLocal(FloorTileInstance tile, InteriorGrid interior, Vector2Int localPos)
        {
            if (!interior.InBounds(localPos)) return null;
            string id = interior.GetOccupantId(localPos);
            if (string.IsNullOrEmpty(id)) return null;

            foreach (var bi in tile.GetComponentsInChildren<BuildingInstance>(true))
            {
                if (bi == tile || bi is FloorTileInstance) continue;
                if (bi.instanceId == id) return bi;
            }
            return null;
        }
    }
}
