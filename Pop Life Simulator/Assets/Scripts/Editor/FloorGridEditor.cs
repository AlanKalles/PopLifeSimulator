using UnityEngine;
using UnityEditor;
using PopLife.Data;
using PopLife.Runtime;

namespace PopLife.Editor
{
    [CustomEditor(typeof(FloorGrid))]
    public class FloorGridEditor : UnityEditor.Editor
    {
        private BuildingArchetype selectedArchetype;
        private int previewRotation = 0;
        private bool snapMode = false;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            FloorGrid floor = (FloorGrid)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("═══ 关卡设计工具 ═══", EditorStyles.boldLabel);

            // 建筑原型选择
            selectedArchetype = (BuildingArchetype)EditorGUILayout.ObjectField(
                "建筑原型", selectedArchetype, typeof(BuildingArchetype), false);

            // 旋转控制
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("旋转", GUILayout.Width(100));
            if (GUILayout.Button("↶ 左转")) previewRotation = (previewRotation + 3) % 4;
            EditorGUILayout.LabelField($"{previewRotation * 90}°", GUILayout.Width(50));
            if (GUILayout.Button("右转 ↷")) previewRotation = (previewRotation + 1) % 4;
            EditorGUILayout.EndHorizontal();

            // 吸附模式开关
            snapMode = EditorGUILayout.Toggle("网格吸附模式", snapMode);

            EditorGUILayout.Space(5);

            // 快速建造按钮
            GUI.enabled = selectedArchetype != null;
            if (GUILayout.Button("🏗️ 在鼠标位置建造 (免费)", GUILayout.Height(30)))
            {
                PlaceBuildingAtMouse(floor);
            }
            GUI.enabled = true;

            EditorGUILayout.Space(5);

            // 批量操作
            EditorGUILayout.LabelField("批量操作", EditorStyles.boldLabel);

            if (GUILayout.Button("📋 注册场景中所有建筑到网格"))
            {
                RegisterAllBuildingsInScene(floor);
            }

            if (GUILayout.Button("🗑️ 清空网格数据 (保留GameObject)"))
            {
                if (EditorUtility.DisplayDialog("确认", "这将清空网格占用数据,但不删除场景中的建筑GameObject", "确定", "取消"))
                {
                    ClearGridData(floor);
                }
            }
        }

        private void OnSceneGUI()
        {
            FloorGrid floor = (FloorGrid)target;

            if (!snapMode || selectedArchetype == null) return;

            // 获取鼠标世界坐标
            Event e = Event.current;
            Vector3 mousePos = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
            mousePos.z = 0;

            // 转换为网格坐标
            Vector2Int gridPos = floor.WorldToGrid(mousePos);
            Vector3 snappedWorldPos = floor.GridToWorld(gridPos);

            // 绘制预览
            var footprint = selectedArchetype.GetRotatedFootprint(previewRotation);
            bool canPlace = floor.CanPlaceFootprint(footprint, gridPos);

            Handles.color = canPlace ? new Color(0, 1, 0, 0.3f) : new Color(1, 0, 0, 0.3f);

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

            // 点击建造
            if (e.type == EventType.MouseDown && e.button == 0 && canPlace)
            {
                PlaceBuildingAtGrid(floor, gridPos);
                e.Use();
            }

            // 强制场景重绘
            SceneView.RepaintAll();
        }

        private void PlaceBuildingAtMouse(FloorGrid floor)
        {
            // 从Scene视图获取鼠标位置
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                Debug.LogWarning("请在Scene视图中操作");
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
                EditorUtility.DisplayDialog("错误", "请先选择建筑原型", "确定");
                return;
            }

            // 初始化网格(如果未初始化)
            if (!Application.isPlaying)
            {
                floor.Init();
            }

            var footprint = selectedArchetype.GetRotatedFootprint(previewRotation);

            if (!floor.CanPlaceFootprint(footprint, gridPos))
            {
                EditorUtility.DisplayDialog("无法建造", "该位置无法放置此建筑", "确定");
                return;
            }

            // 创建建筑实例
            Vector3 worldPos = floor.GridToWorld(gridPos);
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(selectedArchetype.prefab, floor.buildingContainer);
            go.transform.SetPositionAndRotation(worldPos, Quaternion.Euler(0, 0, previewRotation * 90));

            BuildingInstance instance = go.GetComponent<BuildingInstance>();
            if (instance != null)
            {
                instance.rotation = previewRotation;
                instance.Initialize(selectedArchetype, gridPos, floor.floorId);

                // 手动调用PlaceBuildingTransactional的内部逻辑(但跳过资源检查)
                // 这里使用反射或者直接调用RegisterExistingBuilding
                if (!floor.RegisterExistingBuilding(instance, gridPos, previewRotation))
                {
                    DestroyImmediate(go);
                    EditorUtility.DisplayDialog("注册失败", "无法将建筑注册到网格", "确定");
                    return;
                }

                Undo.RegisterCreatedObjectUndo(go, "Place Building");
                EditorUtility.SetDirty(floor);
                Debug.Log($"已建造: {selectedArchetype.displayName} 在 {gridPos}");
            }
            else
            {
                DestroyImmediate(go);
                EditorUtility.DisplayDialog("错误", "预制体缺少BuildingInstance组件", "确定");
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
                // 尝试从当前位置推断网格位置
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
                        Debug.Log($"✓ 已注册: {building.archetype.displayName} at {gridPos}");
                    }
                    else
                    {
                        failCount++;
                        Debug.LogWarning($"✗ 注册失败: {building.archetype.displayName} at {gridPos}");
                    }
                }
                else
                {
                    failCount++;
                    Debug.LogWarning($"✗ 位置冲突: {building.archetype.displayName} at {gridPos}");
                }
            }

            EditorUtility.SetDirty(floor);
            EditorUtility.DisplayDialog("批量注册完成",
                $"成功: {successCount}\n失败: {failCount}", "确定");
        }

        private void ClearGridData(FloorGrid floor)
        {
            floor.Init();
            EditorUtility.SetDirty(floor);
            Debug.Log("网格数据已清空");
        }
    }
}
