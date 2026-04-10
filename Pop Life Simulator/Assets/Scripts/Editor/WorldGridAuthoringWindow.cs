using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PopLife.Data;
using PopLife.Runtime;

namespace PopLife.Editor
{
    /// <summary>
    /// WorldGrid Authoring 工具窗口。
    /// 上半部分：WorldGrid 模式（PlaceFloorTile / EraseFloorTile）
    /// 下半部分：Interior 模式（PlaceInteriorBuilding / EraseInteriorBuilding）
    /// </summary>
    public class WorldGridAuthoringWindow : EditorWindow
    {
        // 缓存原型列表
        private FloorTileArchetype[] floorTileArchetypes;
        private BuildingArchetype[] interiorArchetypes;
        private string[] floorTileNames;
        private string[] interiorNames;

        // 选中索引
        private int selectedFloorTileIndex;
        private int selectedInteriorIndex;

        // UI 滚动
        private Vector2 scrollPos;

        [MenuItem("PopLife/WorldGrid Authoring")]
        public static void ShowWindow()
        {
            GetWindow<WorldGridAuthoringWindow>("WorldGrid Authoring");
        }

        private void OnEnable()
        {
            RefreshArchetypes();
            // 监听 Selection 变化，刷新 interior 区域可用性
            Selection.selectionChanged += Repaint;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= Repaint;
        }

        private void RefreshArchetypes()
        {
            // 加载 FloorTileArchetype
            floorTileArchetypes = Resources.LoadAll<FloorTileArchetype>("ScriptableObjects/BuildingArchetype/FloorTile");
            floorTileNames = floorTileArchetypes.Select(a => a.displayName ?? a.name).ToArray();

            // 加载 interior 可放置原型（排除 FloorTileArchetype）
            var all = Resources.LoadAll<BuildingArchetype>("ScriptableObjects/BuildingArchetype");
            var interiorList = new List<BuildingArchetype>();
            foreach (var arch in all)
            {
                if (arch is FloorTileArchetype) continue;
                interiorList.Add(arch);
            }
            interiorArchetypes = interiorList.ToArray();
            interiorNames = interiorArchetypes.Select(a =>
            {
                string prefix = a is ShelfArchetype ? "[Shelf] " :
                                a is FacilityArchetype ? "[Facility] " : "";
                return prefix + (a.displayName ?? a.name);
            }).ToArray();

            // 同步选中索引
            SyncSelectedIndices();
        }

        private void SyncSelectedIndices()
        {
            var state = WorldGridAuthoringState.instance;
            if (state.selectedArchetype != null)
            {
                if (state.selectedArchetype is FloorTileArchetype fta)
                {
                    selectedFloorTileIndex = System.Array.IndexOf(floorTileArchetypes, fta);
                    if (selectedFloorTileIndex < 0) selectedFloorTileIndex = 0;
                }
                else
                {
                    selectedInteriorIndex = System.Array.IndexOf(interiorArchetypes, state.selectedArchetype);
                    if (selectedInteriorIndex < 0) selectedInteriorIndex = 0;
                }
            }
        }

        private void OnGUI()
        {
            var state = WorldGridAuthoringState.instance;
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            // ── 网格可视化开关 ──
            EditorGUILayout.LabelField("Grid Visibility", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            bool newShowWorld = EditorGUILayout.ToggleLeft("Show World Grid", state.showWorldGrid, GUILayout.Width(150));
            bool newShowInterior = EditorGUILayout.ToggleLeft("Show Interior Grid", state.showInteriorGrid, GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();
            if (newShowWorld != state.showWorldGrid || newShowInterior != state.showInteriorGrid)
            {
                state.showWorldGrid = newShowWorld;
                state.showInteriorGrid = newShowInterior;
                OnStateChanged(state);
            }

            EditorGUILayout.Space(4);

            // ── 刷新按钮 ──
            if (GUILayout.Button("Refresh Archetypes"))
                RefreshArchetypes();

            EditorGUILayout.Space(6);

            // ================================================================
            //  WorldGrid 模式
            // ================================================================
            EditorGUILayout.LabelField("WorldGrid Mode", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            DrawModeButton("Place FloorTile", WorldGridAuthoringState.AuthoringMode.PlaceFloorTile, state);
            DrawModeButton("Erase FloorTile", WorldGridAuthoringState.AuthoringMode.EraseFloorTile, state);
            EditorGUILayout.EndHorizontal();

            // FloorTile 选择（仅 Place 模式）
            bool isFloorMode = state.mode == WorldGridAuthoringState.AuthoringMode.PlaceFloorTile;
            using (new EditorGUI.DisabledGroupScope(!isFloorMode))
            {
                if (floorTileArchetypes != null && floorTileArchetypes.Length > 0)
                {
                    int newIndex = EditorGUILayout.Popup("FloorTile Archetype", selectedFloorTileIndex, floorTileNames);
                    if (newIndex != selectedFloorTileIndex || (isFloorMode && state.selectedArchetype != floorTileArchetypes[newIndex]))
                    {
                        selectedFloorTileIndex = newIndex;
                        if (isFloorMode)
                        {
                            state.selectedArchetype = floorTileArchetypes[selectedFloorTileIndex];
                            OnStateChanged(state);
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No FloorTileArchetype found in Resources/ScriptableObjects/BuildingArchetype/FloorTile", MessageType.Warning);
                }

                // 旋转
                DrawRotationButtons(state, isFloorMode);

                // Default Floor 标记
                bool newDefault = EditorGUILayout.Toggle("Mark as Default Floor", state.markAsDefault);
                if (newDefault != state.markAsDefault)
                {
                    state.markAsDefault = newDefault;
                    OnStateChanged(state);
                }
            }

            EditorGUILayout.Space(12);

            // ================================================================
            //  Interior 模式
            // ================================================================
            EditorGUILayout.LabelField("Interior Mode", EditorStyles.boldLabel);

            // 检测是否选中了 FloorTileInstance
            FloorTileInstance selectedTile = GetSelectedFloorTile();
            bool hasSelectedTile = selectedTile != null;

            if (!hasSelectedTile)
            {
                EditorGUILayout.HelpBox("Select a FloorTileInstance in the Hierarchy to enable Interior mode.", MessageType.Info);
            }

            using (new EditorGUI.DisabledGroupScope(!hasSelectedTile))
            {
                EditorGUILayout.BeginHorizontal();
                DrawModeButton("Place Interior", WorldGridAuthoringState.AuthoringMode.PlaceInteriorBuilding, state);
                DrawModeButton("Erase Interior", WorldGridAuthoringState.AuthoringMode.EraseInteriorBuilding, state);
                EditorGUILayout.EndHorizontal();

                bool isInteriorPlace = state.mode == WorldGridAuthoringState.AuthoringMode.PlaceInteriorBuilding;
                using (new EditorGUI.DisabledGroupScope(!isInteriorPlace))
                {
                    if (interiorArchetypes != null && interiorArchetypes.Length > 0)
                    {
                        int newIndex = EditorGUILayout.Popup("Building Archetype", selectedInteriorIndex, interiorNames);
                        if (newIndex != selectedInteriorIndex || (isInteriorPlace && state.selectedArchetype != interiorArchetypes[newIndex]))
                        {
                            selectedInteriorIndex = newIndex;
                            if (isInteriorPlace)
                            {
                                state.selectedArchetype = interiorArchetypes[selectedInteriorIndex];
                                OnStateChanged(state);
                            }
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("No interior BuildingArchetypes found in Resources.", MessageType.Warning);
                    }

                    // 旋转
                    DrawRotationButtons(state, isInteriorPlace);

                    // 锁定选项
                    bool newLockMove = EditorGUILayout.Toggle("Lock Move", state.lockMove);
                    if (newLockMove != state.lockMove)
                    {
                        state.lockMove = newLockMove;
                        OnStateChanged(state);
                    }

                    bool newLockDestroy = EditorGUILayout.Toggle("Lock Destroy", state.lockDestroy);
                    if (newLockDestroy != state.lockDestroy)
                    {
                        state.lockDestroy = newLockDestroy;
                        OnStateChanged(state);
                    }
                }
            }

            EditorGUILayout.Space(12);

            // ================================================================
            //  Elevator 模式
            // ================================================================
            EditorGUILayout.LabelField("Elevator Mode", EditorStyles.boldLabel);
            DrawModeButton("Place Elevator", WorldGridAuthoringState.AuthoringMode.PlaceElevator, state);

            EditorGUILayout.Space(12);

            // ================================================================
            //  AlanBot 模式
            // ================================================================
            EditorGUILayout.LabelField("AlanBot Mode", EditorStyles.boldLabel);

            var newAlanBotPrefab = (GameObject)EditorGUILayout.ObjectField(
                "AlanBot Prefab", state.alanBotPrefab, typeof(GameObject), false);
            if (newAlanBotPrefab != state.alanBotPrefab)
            {
                state.alanBotPrefab = newAlanBotPrefab;
                OnStateChanged(state);
            }

            using (new EditorGUI.DisabledGroupScope(state.alanBotPrefab == null))
            {
                DrawModeButton("Place AlanBot", WorldGridAuthoringState.AuthoringMode.PlaceAlanBot, state);
            }

            if (state.alanBotPrefab == null)
                EditorGUILayout.HelpBox("Assign AlanBot prefab to enable placement.", MessageType.Info);

            EditorGUILayout.Space(12);

            // ================================================================
            //  状态显示 & 取消
            // ================================================================
            EditorGUILayout.LabelField("Current State", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Mode", state.mode.ToString());
            EditorGUILayout.LabelField("Archetype", state.selectedArchetype != null ? state.selectedArchetype.name : "(none)");
            EditorGUILayout.LabelField("Rotation", $"{state.rotation * 90} deg");

            EditorGUILayout.Space(6);

            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("Cancel (Reset to None)", GUILayout.Height(30)))
            {
                state.mode = WorldGridAuthoringState.AuthoringMode.None;
                OnStateChanged(state);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndScrollView();
        }

        // --- 辅助方法 ---

        private void DrawModeButton(string label, WorldGridAuthoringState.AuthoringMode targetMode, WorldGridAuthoringState state)
        {
            bool isActive = state.mode == targetMode;
            GUI.backgroundColor = isActive ? Color.cyan : Color.white;
            if (GUILayout.Button(label))
            {
                if (isActive)
                {
                    // 再次点击取消
                    state.mode = WorldGridAuthoringState.AuthoringMode.None;
                }
                else
                {
                    state.mode = targetMode;

                    // 自动设置选中的原型
                    if (targetMode == WorldGridAuthoringState.AuthoringMode.PlaceFloorTile)
                    {
                        if (floorTileArchetypes != null && floorTileArchetypes.Length > 0)
                            state.selectedArchetype = floorTileArchetypes[Mathf.Clamp(selectedFloorTileIndex, 0, floorTileArchetypes.Length - 1)];
                    }
                    else if (targetMode == WorldGridAuthoringState.AuthoringMode.PlaceInteriorBuilding)
                    {
                        if (interiorArchetypes != null && interiorArchetypes.Length > 0)
                            state.selectedArchetype = interiorArchetypes[Mathf.Clamp(selectedInteriorIndex, 0, interiorArchetypes.Length - 1)];
                    }
                }
                OnStateChanged(state);
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawRotationButtons(WorldGridAuthoringState state, bool enabled)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Rotation");
            for (int r = 0; r < 4; r++)
            {
                bool isActive = state.rotation == r;
                GUI.backgroundColor = isActive ? Color.yellow : Color.white;
                if (GUILayout.Button($"{r * 90}\u00b0", GUILayout.Width(50)))
                {
                    if (enabled)
                    {
                        state.rotation = r;
                        OnStateChanged(state);
                    }
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private void OnStateChanged(WorldGridAuthoringState state)
        {
            state.Save();
            SceneView.RepaintAll();
            Repaint();
        }

        private FloorTileInstance GetSelectedFloorTile()
        {
            if (Selection.activeGameObject == null) return null;
            return Selection.activeGameObject.GetComponent<FloorTileInstance>();
        }
    }
}
