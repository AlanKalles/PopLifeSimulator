using UnityEngine;
using UnityEditor;
using PopLife.Data;

namespace PopLife.Editor
{
    /// <summary>
    /// FloorTileArchetype 专用编辑器
    /// 显示 footprint 网格预览 + 新 interior 配置字段的默认 Inspector
    /// </summary>
    [CustomEditor(typeof(FloorTileArchetype))]
    public class FloorTileArchetypeEditor : UnityEditor.Editor
    {
        private const float CELL_SIZE = 30f;
        private const int PADDING = 1;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 使用默认 Inspector 绘制所有字段
            // 新的 interior 字段（interiorOffset, interiorGridSize, interiorCellSize,
            // leftPortalCells, rightPortalCells, walkableRows）均为标准序列化类型，
            // Unity 默认绘制即可
            DrawDefaultInspector();

            FloorTileArchetype archetype = (FloorTileArchetype)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Footprint Preview", EditorStyles.boldLabel);

            var ts = archetype.TileSize;
            if (ts.x > 0 && ts.y > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Tile: {ts.x} x {ts.y} ({ts.x * ts.y} cells)\n" +
                    $"Interior Grid: {archetype.InteriorGridSize.x} x {archetype.InteriorGridSize.y} " +
                    $"(cell size: {archetype.InteriorCellSize})\n" +
                    $"Walkable Rows: {archetype.WalkableRows}\n" +
                    $"Left Portals: {archetype.LeftPortalCells.Count}  |  Right Portals: {archetype.RightPortalCells.Count}",
                    MessageType.Info
                );

                DrawFootprintGrid(archetype);
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 绘制 footprint 网格预览（只读）
        /// 蓝色 = 占地格子，绿色 = 锚点 (0,0)
        /// </summary>
        private void DrawFootprintGrid(FloorTileArchetype archetype)
        {
            var ts = archetype.TileSize;

            int gridW = ts.x + PADDING * 2;
            int gridH = ts.y + PADDING * 2;

            Rect gridRect = GUILayoutUtility.GetRect(gridW * CELL_SIZE, gridH * CELL_SIZE);

            for (int screenY = 0; screenY < gridH; screenY++)
            {
                for (int screenX = 0; screenX < gridW; screenX++)
                {
                    int tileX = screenX - PADDING;
                    int tileY = screenY - PADDING;

                    // 屏幕 Y 翻转（底部=0）
                    Rect cellRect = new Rect(
                        gridRect.x + screenX * CELL_SIZE,
                        gridRect.y + (gridH - 1 - screenY) * CELL_SIZE,
                        CELL_SIZE,
                        CELL_SIZE
                    );

                    bool isFootprint = tileX >= 0 && tileY >= 0 && tileX < ts.x && tileY < ts.y;
                    bool isOrigin = tileX == 0 && tileY == 0;

                    // 颜色
                    Color color;
                    if (isOrigin && isFootprint)
                        color = new Color(0.3f, 1f, 0.3f, 1f); // 绿色 = 锚点
                    else if (isFootprint)
                        color = new Color(0.3f, 0.6f, 1f, 1f); // 蓝色 = 占地
                    else
                        color = new Color(0.15f, 0.15f, 0.15f, 1f); // 深灰 = 空

                    EditorGUI.DrawRect(cellRect, color);

                    // 边框
                    Handles.DrawSolidRectangleWithOutline(cellRect, Color.clear,
                        isFootprint ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.1f, 0.1f, 0.1f));

                    // 坐标标签（只标注小范围坐标，避免拥挤）
                    if (isFootprint && tileX <= 4 && tileY <= 4)
                    {
                        GUI.Label(cellRect, $"{tileX},{tileY}",
                            new GUIStyle(EditorStyles.miniLabel)
                            {
                                alignment = TextAnchor.MiddleCenter,
                                fontSize = 9,
                                normal = { textColor = Color.white }
                            });
                    }
                }
            }
        }
    }
}
