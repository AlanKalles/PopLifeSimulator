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
        private bool snapMode = true; // Default enabled

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            FloorGrid floor = (FloorGrid)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("═══ Level Design Tools ═══", EditorStyles.boldLabel);

            // Building archetype selection
            selectedArchetype = (BuildingArchetype)EditorGUILayout.ObjectField(
                "Building Archetype", selectedArchetype, typeof(BuildingArchetype), false);

            // Rotation control
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Rotation", GUILayout.Width(100));
            if (GUILayout.Button("↶ Left")) previewRotation = (previewRotation + 3) % 4;
            EditorGUILayout.LabelField($"{previewRotation * 90}°", GUILayout.Width(50));
            if (GUILayout.Button("Right ↷")) previewRotation = (previewRotation + 1) % 4;
            EditorGUILayout.EndHorizontal();

            // Snap mode toggle
            snapMode = EditorGUILayout.Toggle("Grid Snap Mode", snapMode);

            EditorGUILayout.Space(5);

            // Quick build button
            GUI.enabled = selectedArchetype != null;
            if (GUILayout.Button("🏗️ Place at Mouse (Free)", GUILayout.Height(30)))
            {
                PlaceBuildingAtMouse(floor);
            }
            GUI.enabled = true;

            EditorGUILayout.Space(5);

            // Batch operations
            EditorGUILayout.LabelField("Batch Operations", EditorStyles.boldLabel);

            if (GUILayout.Button("📋 Register All Buildings in Scene"))
            {
                RegisterAllBuildingsInScene(floor);
            }

            if (GUILayout.Button("🗑️ Clear Grid Data (Keep GameObjects)"))
            {
                if (EditorUtility.DisplayDialog("Confirm", "This will clear grid occupation data without deleting building GameObjects", "OK", "Cancel"))
                {
                    ClearGridData(floor);
                }
            }

            if (GUILayout.Button("🔄 Sync Grid with Scene (Auto-cleanup)"))
            {
                SyncGridWithScene(floor);
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
            // Get mouse position from Scene view
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

            // Initialize grid if not initialized
            if (!Application.isPlaying)
            {
                floor.Init();
            }

            var footprint = selectedArchetype.GetRotatedFootprint(previewRotation);

            if (!floor.CanPlaceFootprint(footprint, gridPos))
            {
                EditorUtility.DisplayDialog("Cannot Place", "Cannot place building at this position", "OK");
                return;
            }

            // Create building instance
            Vector3 worldPos = floor.GridToWorld(gridPos);
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(selectedArchetype.prefab, floor.buildingContainer);
            go.transform.SetPositionAndRotation(worldPos, Quaternion.Euler(0, 0, previewRotation * 90));

            BuildingInstance instance = go.GetComponent<BuildingInstance>();
            if (instance != null)
            {
                instance.rotation = previewRotation;
                instance.Initialize(selectedArchetype, gridPos, floor.floorId);

                // Register to grid (skip resource check)
                if (!floor.RegisterExistingBuilding(instance, gridPos, previewRotation))
                {
                    DestroyImmediate(go);
                    EditorUtility.DisplayDialog("Registration Failed", "Cannot register building to grid", "OK");
                    return;
                }

                Undo.RegisterCreatedObjectUndo(go, "Place Building");
                EditorUtility.SetDirty(floor);
                Debug.Log($"Placed: {selectedArchetype.displayName} at {gridPos}");
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
                // Infer grid position from current world position
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

        private void ClearGridData(FloorGrid floor)
        {
            floor.Init();
            EditorUtility.SetDirty(floor);
            Debug.Log("Grid data cleared");
        }

        private void SyncGridWithScene(FloorGrid floor)
        {
            if (!Application.isPlaying)
            {
                floor.Init();
            }

            // Get all buildings currently in scene
            BuildingInstance[] sceneBuildings = floor.buildingContainer.GetComponentsInChildren<BuildingInstance>();
            System.Collections.Generic.HashSet<string> sceneIds = new System.Collections.Generic.HashSet<string>();

            foreach (var building in sceneBuildings)
            {
                if (!string.IsNullOrEmpty(building.instanceId))
                {
                    sceneIds.Add(building.instanceId);
                }
            }

            // Access private 'instances' field using reflection
            var instancesField = typeof(FloorGrid).GetField("instances",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (instancesField != null)
            {
                var instances = (System.Collections.Generic.Dictionary<string, BuildingInstance>)instancesField.GetValue(floor);
                var toRemove = new System.Collections.Generic.List<string>();

                // Find stale entries (buildings that no longer exist in scene)
                foreach (var kvp in instances)
                {
                    if (kvp.Value == null || !sceneIds.Contains(kvp.Key))
                    {
                        toRemove.Add(kvp.Key);
                    }
                }

                // Remove stale entries
                foreach (var id in toRemove)
                {
                    instances.Remove(id);
                    Debug.Log($"Removed missing building from grid: {id}");
                }

                if (toRemove.Count > 0)
                {
                    // Rebuild grid occupation data
                    floor.Init();

                    int reregisteredCount = 0;
                    foreach (var building in sceneBuildings)
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
                                reregisteredCount++;
                            }
                        }
                    }

                    EditorUtility.SetDirty(floor);
                    EditorUtility.DisplayDialog("Sync Complete",
                        $"Removed {toRemove.Count} missing building(s) from grid\nRe-registered {reregisteredCount} building(s)", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Sync Complete", "Grid is already in sync with scene", "OK");
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Cannot access FloorGrid internal data via reflection", "OK");
            }
        }
    }
}
