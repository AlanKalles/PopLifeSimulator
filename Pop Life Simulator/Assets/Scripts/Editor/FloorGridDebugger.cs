using UnityEngine;
using UnityEditor;
using PopLife.Runtime;
using PopLife.Data;

namespace PopLife.Editor
{
    // 临时调试工具 - 用于诊断 FloorGrid 坐标问题
    [CustomEditor(typeof(FloorGrid))]
    public class FloorGridDebugger : UnityEditor.Editor
    {
        private BuildingArchetype selectedArchetype;
        private int previewRotation = 0;
        private bool snapMode = true;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            FloorGrid floor = (FloorGrid)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("═══ Debug Info ═══", EditorStyles.boldLabel);

            // 显示 Origin 信息
            Vector3 originPos = floor.transform.position;
            if (floor.origin != null)
            {
                originPos = floor.origin.position;
                EditorGUILayout.LabelField($"Origin: {floor.origin.name} at {originPos}");
            }
            else
            {
                EditorGUILayout.HelpBox("Warning: No origin set, using FloorGrid position", MessageType.Warning);
                EditorGUILayout.LabelField($"FloorGrid Position: {originPos}");
            }

            EditorGUILayout.LabelField($"BuildingContainer: {(floor.buildingContainer ? floor.buildingContainer.name : "NULL")}");

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("═══ Level Design Tools ═══", EditorStyles.boldLabel);

            selectedArchetype = (BuildingArchetype)EditorGUILayout.ObjectField(
                "Building Archetype", selectedArchetype, typeof(BuildingArchetype), false);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Rotation", GUILayout.Width(100));
            if (GUILayout.Button("↶ Left")) previewRotation = (previewRotation + 3) % 4;
            EditorGUILayout.LabelField($"{previewRotation * 90}°", GUILayout.Width(50));
            if (GUILayout.Button("Right ↷")) previewRotation = (previewRotation + 1) % 4;
            EditorGUILayout.EndHorizontal();

            snapMode = EditorGUILayout.Toggle("Grid Snap Mode", snapMode);

            EditorGUILayout.Space(5);

            GUI.enabled = selectedArchetype != null;
            if (GUILayout.Button("🏗️ Place at Mouse", GUILayout.Height(30)))
            {
                PlaceBuildingAtMouse(floor);
            }
            GUI.enabled = true;

            if (GUILayout.Button("📋 Register All Buildings in Scene"))
            {
                RegisterAllBuildingsInScene(floor);
            }

            if (GUILayout.Button("🔄 Initialize Grid"))
            {
                floor.Init();
                EditorUtility.SetDirty(floor);
                Debug.Log($"Grid initialized for {floor.name}");
            }

            if (GUILayout.Button("🔍 Debug Grid State"))
            {
                DebugGridState(floor);
            }

            if (GUILayout.Button("🧹 Clean Up Zombie Occupations"))
            {
                CleanUpZombieOccupations(floor);
            }
        }

        private void OnSceneGUI()
        {
            FloorGrid floor = (FloorGrid)target;

            // 确保 grid 已初始化（编辑器模式下需要手动初始化）
            EnsureGridInitialized(floor);

            // 显示调试信息
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10, 10, 350, 200));
            GUILayout.BeginVertical("box");

            GUILayout.Label($"FloorGrid: {floor.name}", EditorStyles.boldLabel);
            GUILayout.Label($"Floor ID: {floor.floorId}");
            GUILayout.Label($"Grid Size: {floor.gridSize}");
            GUILayout.Label($"Cell Size: {floor.cellSize}");

            Vector3 originPos = floor.origin ? floor.origin.position : floor.transform.position;
            GUILayout.Label($"Origin Position: {originPos}");

            if (floor.buildingContainer != null)
            {
                GUILayout.Label($"Container: {floor.buildingContainer.name}");
            }
            else
            {
                GUILayout.Label("Container: NULL", EditorStyles.helpBox);
            }

            // 显示鼠标位置和网格坐标
            Event e = Event.current;
            if (e != null)
            {
                Vector3 mousePos = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
                mousePos.z = 0;
                Vector2Int gridPos = floor.WorldToGrid(mousePos);

                GUILayout.Label($"Mouse World: ({mousePos.x:F2}, {mousePos.y:F2})");
                GUILayout.Label($"Grid Pos: ({gridPos.x}, {gridPos.y})");

                bool inBounds = gridPos.x >= 0 && gridPos.y >= 0 &&
                               gridPos.x < floor.gridSize.x && gridPos.y < floor.gridSize.y;

                var style = new GUIStyle(EditorStyles.label);
                style.normal.textColor = inBounds ? Color.green : Color.red;
                GUILayout.Label($"In Bounds: {inBounds}", style);
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
            Handles.EndGUI();

            // 绘制预览
            if (snapMode && selectedArchetype != null)
            {
                Vector3 mousePos = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
                mousePos.z = 0;

                Vector2Int gridPos = floor.WorldToGrid(mousePos);
                Vector3 snappedWorldPos = floor.GridToWorld(gridPos);

                var footprint = selectedArchetype.GetRotatedFootprint(previewRotation);
                bool canPlace = floor.CanPlaceFootprint(footprint, gridPos);

                foreach (var offset in footprint)
                {
                    Vector3 cellWorld = floor.GridToWorld(gridPos + offset);
                    Handles.DrawSolidRectangleWithOutline(
                        new Vector3[] {
                            cellWorld + new Vector3(0, 0, 0),
                            cellWorld + new Vector3(floor.cellSize, 0, 0),
                            cellWorld + new Vector3(floor.cellSize, floor.cellSize, 0),
                            cellWorld + new Vector3(0, floor.cellSize, 0)
                        },
                        canPlace ? new Color(0, 1, 0, 0.2f) : new Color(1, 0, 0, 0.2f),
                        canPlace ? Color.green : Color.red
                    );
                }

                if (e.type == EventType.MouseDown && e.button == 0 && canPlace)
                {
                    PlaceBuildingAtGrid(floor, gridPos);
                    e.Use();
                }
            }

            SceneView.RepaintAll();
        }

        private void PlaceBuildingAtMouse(FloorGrid floor)
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                Debug.LogWarning("Please operate in Scene view");
                return;
            }

            Vector2 mousePos = Event.current.mousePosition;
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
            Vector3 worldPos = ray.origin;
            worldPos.z = 0;

            Vector2Int gridPos = floor.WorldToGrid(worldPos);
            PlaceBuildingAtGrid(floor, gridPos);
        }

        private void PlaceBuildingAtGrid(FloorGrid floor, Vector2Int gridPos)
        {
            if (selectedArchetype == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a building archetype first", "OK");
                return;
            }

            if (!Application.isPlaying)
            {
                floor.Init();
            }

            var footprint = selectedArchetype.GetRotatedFootprint(previewRotation);

            if (!floor.CanPlaceFootprint(footprint, gridPos))
            {
                EditorUtility.DisplayDialog("Cannot Place", $"Cannot place building at {gridPos}\nCheck debug info in Scene view", "OK");
                return;
            }

            Vector3 worldPos = floor.GridToWorld(gridPos);
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(selectedArchetype.prefab, floor.buildingContainer);
            go.transform.SetPositionAndRotation(worldPos, Quaternion.Euler(0, 0, previewRotation * 90));

            BuildingInstance instance = go.GetComponent<BuildingInstance>();
            if (instance != null)
            {
                instance.rotation = previewRotation;
                instance.Initialize(selectedArchetype, gridPos, floor.floorId);

                if (!floor.RegisterExistingBuilding(instance, gridPos, previewRotation))
                {
                    DestroyImmediate(go);
                    EditorUtility.DisplayDialog("Registration Failed", "Cannot register building to grid", "OK");
                    return;
                }

                Undo.RegisterCreatedObjectUndo(go, "Place Building");
                EditorUtility.SetDirty(floor);
                Debug.Log($"✓ Placed: {selectedArchetype.displayName} at {gridPos} (World: {worldPos})");
            }
            else
            {
                DestroyImmediate(go);
                EditorUtility.DisplayDialog("Error", "Prefab is missing BuildingInstance component", "OK");
            }
        }

        private void RegisterAllBuildingsInScene(FloorGrid floor)
        {
            if (!Application.isPlaying)
            {
                floor.Init();
            }

            BuildingInstance[] allBuildings = floor.buildingContainer.GetComponentsInChildren<BuildingInstance>();
            int successCount = 0;
            int failCount = 0;

            foreach (var building in allBuildings)
            {
                Vector2Int gridPos = floor.WorldToGrid(building.transform.position);
                int rotation = Mathf.RoundToInt(building.transform.eulerAngles.z / 90f) % 4;

                var footprint = building.archetype.GetRotatedFootprint(rotation);

                if (floor.CanPlaceFootprint(footprint, gridPos))
                {
                    building.rotation = rotation;
                    building.Initialize(building.archetype, gridPos, floor.floorId);

                    if (floor.RegisterExistingBuilding(building, gridPos, rotation))
                    {
                        successCount++;
                        Debug.Log($"✓ Registered: {building.archetype.displayName} at {gridPos}");
                    }
                    else
                    {
                        failCount++;
                        Debug.LogWarning($"✗ Registration failed: {building.archetype.displayName} at {gridPos}");
                    }
                }
                else
                {
                    failCount++;
                    Debug.LogWarning($"✗ Position conflict: {building.archetype.displayName} at {gridPos}");
                }
            }

            EditorUtility.SetDirty(floor);
            EditorUtility.DisplayDialog("Batch Registration Complete",
                $"Success: {successCount}\nFailed: {failCount}", "OK");
        }

        private void DebugGridState(FloorGrid floor)
        {
            // 使用反射访问私有字段
            var gridField = typeof(FloorGrid).GetField("grid",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var columnsField = typeof(FloorGrid).GetField("columnsWithGroundBuildings",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (gridField == null || columnsField == null)
            {
                Debug.LogError("无法访问 FloorGrid 私有字段");
                return;
            }

            var grid = gridField.GetValue(floor);
            var columns = (System.Collections.Generic.HashSet<int>)columnsField.GetValue(floor);

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== FloorGrid Debug: {floor.name} ===");
            sb.AppendLine($"Grid initialized: {grid != null}");

            if (columns != null && columns.Count > 0)
            {
                sb.AppendLine($"Occupied ground columns: {string.Join(", ", columns)}");
            }
            else
            {
                sb.AppendLine("No occupied ground columns");
            }

            // 检查是否有被占用的格子
            if (grid != null)
            {
                var gridArray = grid as System.Array;
                int occupiedCount = 0;

                for (int x = 0; x < floor.gridSize.x; x++)
                {
                    for (int y = 0; y < floor.gridSize.y; y++)
                    {
                        var cell = gridArray.GetValue(x, y);
                        var occupiedProp = cell.GetType().GetField("occupied");
                        var buildingIdProp = cell.GetType().GetField("buildingId");

                        if (occupiedProp != null && (bool)occupiedProp.GetValue(cell))
                        {
                            occupiedCount++;
                            string buildingId = (string)buildingIdProp?.GetValue(cell);
                            sb.AppendLine($"  Cell ({x}, {y}) occupied by: {buildingId ?? "unknown"}");
                        }
                    }
                }

                sb.AppendLine($"Total occupied cells: {occupiedCount}");
            }

            Debug.Log(sb.ToString());
        }

        // 确保 grid 已初始化（避免在编辑器模式下出现 null 引用）
        private void EnsureGridInitialized(FloorGrid floor)
        {
            var gridField = typeof(FloorGrid).GetField("grid",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (gridField != null)
            {
                var grid = gridField.GetValue(floor);
                if (grid == null)
                {
                    // Grid 未初始化，调用 Init()
                    floor.Init();
                    Debug.Log($"Auto-initialized grid for {floor.name}");
                }
            }
        }

        // 清理已删除建筑的僵尸占用
        private void CleanUpZombieOccupations(FloorGrid floor)
        {
            var gridField = typeof(FloorGrid).GetField("grid",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var instancesField = typeof(FloorGrid).GetField("instances",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var columnsField = typeof(FloorGrid).GetField("columnsWithGroundBuildings",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (gridField == null || instancesField == null || columnsField == null)
            {
                EditorUtility.DisplayDialog("Error", "无法访问 FloorGrid 私有字段", "OK");
                return;
            }

            var grid = gridField.GetValue(floor) as System.Array;
            var instances = instancesField.GetValue(floor) as System.Collections.Generic.Dictionary<string, BuildingInstance>;
            var columns = columnsField.GetValue(floor) as System.Collections.Generic.HashSet<int>;

            if (grid == null)
            {
                EditorUtility.DisplayDialog("Error", "Grid 未初始化，请先点击 Initialize Grid", "OK");
                return;
            }

            int zombieCount = 0;
            int cleanedCells = 0;
            System.Collections.Generic.List<string> zombieIds = new System.Collections.Generic.List<string>();

            // 1. 扫描 instances 字典，找出僵尸引用（已删除的建筑）
            if (instances != null)
            {
                foreach (var kvp in instances)
                {
                    if (kvp.Value == null)
                    {
                        zombieIds.Add(kvp.Key);
                        zombieCount++;
                    }
                }

                // 从字典中移除僵尸引用
                foreach (var id in zombieIds)
                {
                    instances.Remove(id);
                }
            }

            // 2. 扫描 grid，清理僵尸占用的格子
            for (int x = 0; x < floor.gridSize.x; x++)
            {
                for (int y = 0; y < floor.gridSize.y; y++)
                {
                    var cell = grid.GetValue(x, y);
                    var occupiedField = cell.GetType().GetField("occupied");
                    var buildingIdField = cell.GetType().GetField("buildingId");

                    bool isOccupied = (bool)occupiedField.GetValue(cell);
                    string buildingId = (string)buildingIdField.GetValue(cell);

                    if (isOccupied && !string.IsNullOrEmpty(buildingId))
                    {
                        // 检查该建筑是否还存在
                        if (zombieIds.Contains(buildingId) ||
                            (instances != null && !instances.ContainsKey(buildingId)))
                        {
                            // 清理僵尸占用
                            occupiedField.SetValue(cell, false);
                            buildingIdField.SetValue(cell, null);
                            cleanedCells++;
                        }
                    }
                }
            }

            // 3. 重建 columnsWithGroundBuildings（重新扫描第一层）
            if (columns != null)
            {
                columns.Clear();

                for (int x = 0; x < floor.gridSize.x; x++)
                {
                    var cell = grid.GetValue(x, 0); // 第一层 (y=0)
                    var occupiedField = cell.GetType().GetField("occupied");
                    bool isOccupied = (bool)occupiedField.GetValue(cell);

                    if (isOccupied)
                    {
                        columns.Add(x);
                    }
                }
            }

            EditorUtility.SetDirty(floor);

            string message = $"清理完成！\n" +
                           $"移除僵尸引用: {zombieCount} 个\n" +
                           $"清理占用格子: {cleanedCells} 个\n" +
                           $"重建列占用记录: {columns?.Count ?? 0} 列";

            Debug.Log($"FloorGrid Cleanup: {floor.name}\n{message}");
            EditorUtility.DisplayDialog("清理完成", message, "OK");
        }
    }
}

