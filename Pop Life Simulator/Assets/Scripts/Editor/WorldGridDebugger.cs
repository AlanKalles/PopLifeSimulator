using UnityEngine;
using UnityEditor;
using PopLife.Runtime;

namespace PopLife.Editor
{
    /// <summary>
    /// WorldGrid 调试工具 — 网格可视化、坐标检查、FloorTile 层状态
    /// </summary>
    [CustomEditor(typeof(WorldGrid))]
    public class WorldGridDebugger : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            WorldGrid grid = (WorldGrid)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("=== Debug Info ===", EditorStyles.boldLabel);

            // 显示 Origin 信息
            Vector3 originPos = grid.transform.position;
            if (grid.origin != null)
            {
                originPos = grid.origin.position;
                EditorGUILayout.LabelField($"Origin: {grid.origin.name} at {originPos}");
            }
            else
            {
                EditorGUILayout.HelpBox("Warning: No origin set, using WorldGrid position", MessageType.Warning);
                EditorGUILayout.LabelField($"WorldGrid Position: {originPos}");
            }

            EditorGUILayout.LabelField($"BuildingContainer: {(grid.buildingContainer ? grid.buildingContainer.name : "NULL")}");

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("=== Tools ===", EditorStyles.boldLabel);

            if (GUILayout.Button("Initialize Grid"))
            {
                grid.Init();
                EditorUtility.SetDirty(grid);
                Debug.Log($"Grid initialized for {grid.name}");
            }

            if (GUILayout.Button("Debug FloorTile Layer State"))
            {
                DebugFloorTileState(grid);
            }

            if (GUILayout.Button("Rebuild From Scene"))
            {
                grid.RebuildFromScene();
                EditorUtility.SetDirty(grid);
                Debug.Log($"WorldGrid rebuilt from scene: {grid.name}");
            }
        }

        private void OnSceneGUI()
        {
            WorldGrid grid = (WorldGrid)target;

            // 确保 grid 已初始化（编辑器模式下需要手动初始化）
            EnsureGridInitialized(grid);

            // 显示调试信息
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10, 10, 350, 180));
            GUILayout.BeginVertical("box");

            GUILayout.Label($"WorldGrid: {grid.name}", EditorStyles.boldLabel);
            GUILayout.Label($"Grid Size: {grid.gridSize}");
            GUILayout.Label($"Cell Size: {grid.cellSize}");

            Vector3 originPos = grid.origin ? grid.origin.position : grid.transform.position;
            GUILayout.Label($"Origin Position: {originPos}");

            if (grid.buildingContainer != null)
            {
                GUILayout.Label($"Container: {grid.buildingContainer.name}");
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
                Vector2Int gridPos = grid.WorldToGrid(mousePos);

                GUILayout.Label($"Mouse World: ({mousePos.x:F2}, {mousePos.y:F2})");
                GUILayout.Label($"Grid Pos: ({gridPos.x}, {gridPos.y})");

                bool inBounds = grid.InBounds(gridPos);

                var style = new GUIStyle(EditorStyles.label);
                style.normal.textColor = inBounds ? Color.green : Color.red;
                GUILayout.Label($"In Bounds: {inBounds}", style);

                // 显示 FloorTile 占有信息
                if (inBounds)
                {
                    bool hasFloor = grid.HasFloorTileAt(gridPos);
                    string ownerId = grid.GetFloorTileOwnerId(gridPos);
                    GUILayout.Label($"FloorTile: {(hasFloor ? ownerId ?? "yes" : "none")}");
                }
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
            Handles.EndGUI();

            SceneView.RepaintAll();
        }

        private void DebugFloorTileState(WorldGrid grid)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== WorldGrid Debug: {grid.name} ===");
            sb.AppendLine($"Grid Size: {grid.gridSize}");
            sb.AppendLine($"Cell Size: {grid.cellSize}");

            // 统计 FloorTile 信息
            int floorCellCount = 0;
            for (int x = 0; x < grid.gridSize.x; x++)
            {
                for (int y = 0; y < grid.gridSize.y; y++)
                {
                    var pos = new Vector2Int(x, y);
                    if (grid.HasFloorTileAt(pos))
                    {
                        floorCellCount++;
                    }
                }
            }
            sb.AppendLine($"Total FloorTile cells: {floorCellCount}");

            // 列出所有 FloorTile 实例
            int tileCount = 0;
            foreach (var tile in grid.AllFloorTiles())
            {
                tileCount++;
                sb.AppendLine($"  FloorTile: {tile.name} id={tile.instanceId} pos={tile.gridPosition} rot={tile.rotation}");
                if (tile.Interior != null)
                {
                    int buildingCount = tile.GetBuildingsInInterior().Count;
                    sb.AppendLine($"    Interior: {tile.Interior.GridSize} cells, {buildingCount} buildings");
                }
                else
                {
                    sb.AppendLine($"    Interior: NOT INITIALIZED");
                }
            }
            sb.AppendLine($"Total FloorTile instances: {tileCount}");

            // 电梯信息
            var linkMgr = grid.ElevatorLinks;
            if (linkMgr != null)
            {
                var links = linkMgr.ActiveLinks;
                sb.AppendLine($"Elevator Links: {links.Count}");
                foreach (var link in links)
                {
                    string doorAName = link.doorA != null ? link.doorA.instanceId : "null";
                    string doorBName = link.doorB != null ? link.doorB.instanceId : "null";
                    sb.AppendLine($"  Link: {doorAName} <-> {doorBName}");
                }
            }
            else
            {
                sb.AppendLine("Elevator Links: (no ElevatorLinkManager)");
            }

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// 确保 grid 已初始化（避免在编辑器模式下出现 null 引用）
        /// </summary>
        private void EnsureGridInitialized(WorldGrid grid)
        {
            // 使用反射检查私有 floorTileLayer 是否已初始化
            var layerField = typeof(WorldGrid).GetField("floorTileLayer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (layerField != null)
            {
                var layer = layerField.GetValue(grid);
                if (layer == null)
                {
                    grid.Init();
                    Debug.Log($"Auto-initialized grid for {grid.name}");
                }
            }
        }
    }
}
