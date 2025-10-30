using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using PopLife.Data;
using System.Reflection;

namespace PopLife.Editor
{
    /// <summary>
    /// 建筑占地可视化编辑器
    /// 在Inspector中显示可点击的网格，方便设置footprintPattern
    /// </summary>
    [CustomEditor(typeof(BuildingArchetype), true)]
    public class FootprintPatternEditor : UnityEditor.Editor
    {
        private const int GRID_SIZE = 12; // 网格大小（12x12，足够容纳大型建筑）
        private const float CELL_SIZE = 25f; // 每个格子的像素大小
        private const int ORIGIN_X = 0; // 锚点X坐标（左下角）
        private const int ORIGIN_Y = GRID_SIZE - 1; // 锚点Y坐标（左下角，因为屏幕Y向下）

        private BuildingArchetype archetype;
        private SerializedProperty footprintProperty;
        private bool[,] gridState = new bool[GRID_SIZE, GRID_SIZE];

        private void OnEnable()
        {
            archetype = target as BuildingArchetype;
            footprintProperty = serializedObject.FindProperty("footprintPattern");
            LoadGridFromFootprint();
        }

        public override void OnInspectorGUI()
        {
            // 绘制默认Inspector（除了footprintPattern）
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "footprintPattern");

            // 分隔线
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Footprint Pattern Editor", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "点击格子添加/移除占地。左下角（绿色）为建筑锚点(0,0)。\n" +
                "蓝色格子为已选择区域。坐标向右为+X，向上为+Y。",
                MessageType.Info
            );

            // 绘制网格
            DrawGrid();

            // 统计信息
            int count = GetFootprintCount();
            EditorGUILayout.LabelField($"当前占地格子数: {count}");

            // 快捷操作按钮
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("清空"))
            {
                ClearGrid();
            }
            if (GUILayout.Button("填充矩形"))
            {
                ShowFillRectDialog();
            }
            if (GUILayout.Button("应用并保存"))
            {
                ApplyGridToFootprint();
                EditorUtility.SetDirty(archetype);
                AssetDatabase.SaveAssets();
            }
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 绘制可交互网格
        /// </summary>
        private void DrawGrid()
        {
            Rect gridRect = GUILayoutUtility.GetRect(
                GRID_SIZE * CELL_SIZE,
                GRID_SIZE * CELL_SIZE
            );

            Event e = Event.current;

            for (int y = 0; y < GRID_SIZE; y++)
            {
                for (int x = 0; x < GRID_SIZE; x++)
                {
                    Rect cellRect = new Rect(
                        gridRect.x + x * CELL_SIZE,
                        gridRect.y + y * CELL_SIZE,
                        CELL_SIZE,
                        CELL_SIZE
                    );

                    // 确定颜色
                    Color color;
                    if (x == ORIGIN_X && y == ORIGIN_Y)
                    {
                        // 锚点（左下角）
                        color = new Color(0.3f, 1f, 0.3f); // 绿色
                    }
                    else if (gridState[x, y])
                    {
                        // 已选择
                        color = new Color(0.3f, 0.6f, 1f); // 蓝色
                    }
                    else
                    {
                        // 未选择
                        color = new Color(0.85f, 0.85f, 0.85f); // 浅灰色
                    }

                    // 绘制格子
                    EditorGUI.DrawRect(cellRect, color);

                    // 绘制边框
                    Handles.color = Color.black;
                    Handles.DrawSolidRectangleWithOutline(
                        cellRect,
                        Color.clear,
                        new Color(0.3f, 0.3f, 0.3f)
                    );

                    // 显示坐标（前几行几列）
                    if (x <= 3 && y >= GRID_SIZE - 4)
                    {
                        Vector2Int coord = GridToFootprint(x, y);

                        // 只显示非负坐标
                        if (coord.x >= 0 && coord.y >= 0)
                        {
                            GUI.Label(cellRect, $"{coord.x},{coord.y}",
                                new GUIStyle(EditorStyles.miniLabel)
                                {
                                    alignment = TextAnchor.MiddleCenter,
                                    fontSize = 9,
                                    normal = { textColor = new Color(0.2f, 0.2f, 0.2f) }
                                }
                            );
                        }
                    }

                    // 处理点击
                    if (e.type == EventType.MouseDown && cellRect.Contains(e.mousePosition))
                    {
                        gridState[x, y] = !gridState[x, y];
                        e.Use();
                        Repaint();
                    }
                }
            }
        }

        /// <summary>
        /// 网格坐标转为footprint坐标（左下角为0,0，X向右，Y向上）
        /// </summary>
        private Vector2Int GridToFootprint(int gridX, int gridY)
        {
            return new Vector2Int(
                gridX - ORIGIN_X,           // X轴：左侧为0，向右递增
                ORIGIN_Y - gridY            // Y轴：底部为0，向上递增（屏幕坐标Y向下需翻转）
            );
        }

        /// <summary>
        /// Footprint坐标转为网格坐标
        /// </summary>
        private (int x, int y) FootprintToGrid(Vector2Int footprint)
        {
            return (
                footprint.x + ORIGIN_X,     // 恢复X
                ORIGIN_Y - footprint.y      // 恢复Y（翻转）
            );
        }

        /// <summary>
        /// 从footprintPattern加载数据到网格
        /// </summary>
        private void LoadGridFromFootprint()
        {
            // 清空网格
            for (int x = 0; x < GRID_SIZE; x++)
                for (int y = 0; y < GRID_SIZE; y++)
                    gridState[x, y] = false;

            // 使用反射获取footprintPattern（因为是private）
            var field = typeof(BuildingArchetype).GetField(
                "footprintPattern",
                BindingFlags.NonPublic | BindingFlags.Instance
            );

            if (field != null)
            {
                var footprint = field.GetValue(archetype) as List<Vector2Int>;
                if (footprint != null)
                {
                    foreach (var pos in footprint)
                    {
                        var (x, y) = FootprintToGrid(pos);
                        if (x >= 0 && x < GRID_SIZE && y >= 0 && y < GRID_SIZE)
                        {
                            gridState[x, y] = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 将网格状态保存到footprintPattern
        /// </summary>
        private void ApplyGridToFootprint()
        {
            footprintProperty.ClearArray();

            int index = 0;
            for (int y = 0; y < GRID_SIZE; y++)
            {
                for (int x = 0; x < GRID_SIZE; x++)
                {
                    if (gridState[x, y])
                    {
                        footprintProperty.InsertArrayElementAtIndex(index);
                        SerializedProperty element = footprintProperty.GetArrayElementAtIndex(index);
                        element.vector2IntValue = GridToFootprint(x, y);
                        index++;
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
            Debug.Log($"已保存 {index} 个占地格子到 {archetype.name}");
        }

        /// <summary>
        /// 清空网格
        /// </summary>
        private void ClearGrid()
        {
            if (EditorUtility.DisplayDialog("确认清空", "确定要清空所有占地格子吗？", "确定", "取消"))
            {
                for (int x = 0; x < GRID_SIZE; x++)
                    for (int y = 0; y < GRID_SIZE; y++)
                        gridState[x, y] = false;

                ApplyGridToFootprint();
                Repaint();
            }
        }

        /// <summary>
        /// 显示填充矩形对话框
        /// </summary>
        private void ShowFillRectDialog()
        {
            FillRectWindow.ShowWindow(this);
        }

        /// <summary>
        /// 填充矩形区域
        /// </summary>
        public void FillRect(int width, int height, Vector2Int anchor)
        {
            ClearGrid();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector2Int footprintPos = anchor + new Vector2Int(x, y);
                    var (gridX, gridY) = FootprintToGrid(footprintPos);

                    if (gridX >= 0 && gridX < GRID_SIZE && gridY >= 0 && gridY < GRID_SIZE)
                    {
                        gridState[gridX, gridY] = true;
                    }
                }
            }

            ApplyGridToFootprint();
            Repaint();
        }

        /// <summary>
        /// 获取当前占地格子数
        /// </summary>
        private int GetFootprintCount()
        {
            int count = 0;
            for (int x = 0; x < GRID_SIZE; x++)
                for (int y = 0; y < GRID_SIZE; y++)
                    if (gridState[x, y]) count++;
            return count;
        }
    }

    /// <summary>
    /// 填充矩形窗口
    /// </summary>
    public class FillRectWindow : EditorWindow
    {
        private int width = 2;
        private int height = 2;
        private Vector2Int anchor = Vector2Int.zero;
        private FootprintPatternEditor editor;

        public static void ShowWindow(FootprintPatternEditor editor)
        {
            var window = GetWindow<FillRectWindow>("填充矩形");
            window.editor = editor;
            window.minSize = new Vector2(300, 150);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("快速填充矩形区域", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            width = EditorGUILayout.IntField("宽度（格子数）", width);
            height = EditorGUILayout.IntField("高度（格子数）", height);
            anchor = EditorGUILayout.Vector2IntField("锚点坐标", anchor);

            EditorGUILayout.HelpBox(
                $"将填充从 {anchor} 到 ({anchor.x + width - 1}, {anchor.y + height - 1}) 的矩形区域\n" +
                $"锚点在左下角，坐标向右为+X，向上为+Y",
                MessageType.Info
            );

            EditorGUILayout.Space(10);

            if (GUILayout.Button("填充"))
            {
                if (width > 0 && height > 0)
                {
                    editor.FillRect(width, height, anchor);
                    Close();
                }
                else
                {
                    EditorUtility.DisplayDialog("错误", "宽度和高度必须大于0", "确定");
                }
            }
        }
    }
}
