using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using PopLife.Data;
using PopLife.Utility;

namespace PopLife.Editor
{
    /// <summary>
    /// BlueprintProfile.json 可视化编辑工具
    /// 功能:
    /// - 管理解锁的货架和设施蓝图ID列表
    /// - 读写 StreamingAssets/BlueprintProfile.json
    /// - 一键同步到 persistentDataPath (测试用)
    /// - 自动扫描所有BuildingArchetype并提供快速添加功能
    /// </summary>
    public class BlueprintProfileEditor : EditorWindow
    {
        private BlueprintProfile profile;
        private Vector2 shelfScrollPosition;
        private Vector2 facilityScrollPosition;
        private Vector2 floorTileScrollPosition;
        private Vector2 availableShelfScrollPosition;
        private Vector2 availableFacilityScrollPosition;
        private Vector2 availableFloorTileScrollPosition;

        private string newShelfId = "";
        private string newFacilityId = "";
        private string newFloorTileId = "";

        private List<ShelfArchetype> allShelves = new List<ShelfArchetype>();
        private List<FacilityArchetype> allFacilities = new List<FacilityArchetype>();
        private List<FloorTileArchetype> allFloorTiles = new List<FloorTileArchetype>();

        private bool showAvailableShelves = false;
        private bool showAvailableFacilities = false;
        private bool showAvailableFloorTiles = false;

        private const string ELEVATOR_ID = "Elevator";

        [MenuItem("PopLife/Blueprint Profile Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<BlueprintProfileEditor>("Blueprint Profile Editor");
            window.minSize = new Vector2(600, 500);
        }

        private void OnEnable()
        {
            LoadProfile();
            ScanAllBuildings();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Blueprint Profile Editor", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("管理已解锁的建筑蓝图ID列表 (存储于 StreamingAssets/BlueprintProfile.json)", MessageType.Info);

            EditorGUILayout.Space(10);

            // 工具栏
            DrawToolbar();

            EditorGUILayout.Space(10);

            // 上排：货架 | 设施
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical(GUILayout.Width(position.width / 2 - 10));
            DrawShelfSection();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginVertical(GUILayout.Width(position.width / 2 - 10));
            DrawFacilitySection();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // 下排：地板 & 电梯（全宽）
            DrawFloorTileSection();

            EditorGUILayout.Space(10);

            // 路径信息
            DrawPathInfo();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Load", GUILayout.Width(80)))
            {
                LoadProfile();
            }
            if (GUILayout.Button("Save", GUILayout.Width(80)))
            {
                SaveProfile();
            }
            if (GUILayout.Button("Apply Template to Save", GUILayout.Width(160)))
            {
                SyncToRuntime();
            }
            if (GUILayout.Button("Rescan Buildings", GUILayout.Width(120)))
            {
                ScanAllBuildings();
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Unlock All", GUILayout.Width(100)))
            {
                UnlockAll();
            }
            if (GUILayout.Button("Clear All", GUILayout.Width(100)))
            {
                ClearAll();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawShelfSection()
        {
            EditorGUILayout.LabelField("Shelves", EditorStyles.boldLabel);

            // 添加新ID
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Add Shelf ID:", GUILayout.Width(80));
            newShelfId = EditorGUILayout.TextField(newShelfId);
            if (GUILayout.Button("Add", GUILayout.Width(50)))
            {
                if (!string.IsNullOrEmpty(newShelfId) && !profile.unlockedShelfIds.Contains(newShelfId))
                {
                    profile.unlockedShelfIds.Add(newShelfId);
                    newShelfId = "";
                    GUI.FocusControl(null);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 已解锁列表
            EditorGUILayout.LabelField($"Unlocked Shelves ({profile.unlockedShelfIds.Count})", EditorStyles.miniBoldLabel);

            shelfScrollPosition = EditorGUILayout.BeginScrollView(shelfScrollPosition, GUILayout.Height(150));

            for (int i = profile.unlockedShelfIds.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(profile.unlockedShelfIds[i], GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Remove", GUILayout.Width(70)))
                {
                    profile.unlockedShelfIds.RemoveAt(i);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(5);

            // 可选：显示所有可用货架
            showAvailableShelves = EditorGUILayout.Foldout(showAvailableShelves, $"Available Shelves in Project ({allShelves.Count})");
            if (showAvailableShelves)
            {
                availableShelfScrollPosition = EditorGUILayout.BeginScrollView(availableShelfScrollPosition, GUILayout.Height(150));

                foreach (var shelf in allShelves)
                {
                    if (shelf == null) continue;

                    EditorGUILayout.BeginHorizontal();

                    // 高亮已解锁的
                    bool isUnlocked = profile.unlockedShelfIds.Contains(shelf.archetypeId);
                    GUI.color = isUnlocked ? Color.green : Color.white;

                    EditorGUILayout.LabelField($"{shelf.displayName} ({shelf.archetypeId})", GUILayout.ExpandWidth(true));

                    GUI.color = Color.white;

                    if (!isUnlocked)
                    {
                        if (GUILayout.Button("Unlock", GUILayout.Width(70)))
                        {
                            profile.UnlockShelf(shelf.archetypeId);
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawFacilitySection()
        {
            EditorGUILayout.LabelField("Facilities", EditorStyles.boldLabel);

            // 添加新ID
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Add Facility ID:", GUILayout.Width(100));
            newFacilityId = EditorGUILayout.TextField(newFacilityId);
            if (GUILayout.Button("Add", GUILayout.Width(50)))
            {
                if (!string.IsNullOrEmpty(newFacilityId) && !profile.unlockedFacilityIds.Contains(newFacilityId))
                {
                    profile.unlockedFacilityIds.Add(newFacilityId);
                    newFacilityId = "";
                    GUI.FocusControl(null);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 已解锁列表
            EditorGUILayout.LabelField($"Unlocked Facilities ({profile.unlockedFacilityIds.Count})", EditorStyles.miniBoldLabel);

            facilityScrollPosition = EditorGUILayout.BeginScrollView(facilityScrollPosition, GUILayout.Height(150));

            for (int i = profile.unlockedFacilityIds.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(profile.unlockedFacilityIds[i], GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Remove", GUILayout.Width(70)))
                {
                    profile.unlockedFacilityIds.RemoveAt(i);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(5);

            // 可选：显示所有可用设施
            showAvailableFacilities = EditorGUILayout.Foldout(showAvailableFacilities, $"Available Facilities in Project ({allFacilities.Count})");
            if (showAvailableFacilities)
            {
                availableFacilityScrollPosition = EditorGUILayout.BeginScrollView(availableFacilityScrollPosition, GUILayout.Height(150));

                foreach (var facility in allFacilities)
                {
                    if (facility == null) continue;

                    EditorGUILayout.BeginHorizontal();

                    // 高亮已解锁的
                    bool isUnlocked = profile.unlockedFacilityIds.Contains(facility.archetypeId);
                    GUI.color = isUnlocked ? Color.green : Color.white;

                    EditorGUILayout.LabelField($"{facility.displayName} ({facility.archetypeId})", GUILayout.ExpandWidth(true));

                    GUI.color = Color.white;

                    if (!isUnlocked)
                    {
                        if (GUILayout.Button("Unlock", GUILayout.Width(70)))
                        {
                            profile.UnlockFacility(facility.archetypeId);
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawFloorTileSection()
        {
            EditorGUILayout.LabelField("Floor Tiles & Elevator", EditorStyles.boldLabel);

            // 添加新ID
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Add FloorTile ID:", GUILayout.Width(110));
            newFloorTileId = EditorGUILayout.TextField(newFloorTileId);
            if (GUILayout.Button("Add", GUILayout.Width(50)))
            {
                if (!string.IsNullOrEmpty(newFloorTileId) && !profile.unlockedFloorTileIds.Contains(newFloorTileId))
                {
                    profile.unlockedFloorTileIds.Add(newFloorTileId);
                    newFloorTileId = "";
                    GUI.FocusControl(null);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 已解锁列表
            EditorGUILayout.LabelField($"Unlocked Floor Tiles ({profile.unlockedFloorTileIds.Count})", EditorStyles.miniBoldLabel);

            floorTileScrollPosition = EditorGUILayout.BeginScrollView(floorTileScrollPosition, GUILayout.Height(120));

            for (int i = profile.unlockedFloorTileIds.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(profile.unlockedFloorTileIds[i], GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Remove", GUILayout.Width(70)))
                {
                    profile.unlockedFloorTileIds.RemoveAt(i);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(5);

            // 可用列表（FloorTile SO + Elevator 特殊条目）
            showAvailableFloorTiles = EditorGUILayout.Foldout(showAvailableFloorTiles,
                $"Available Floor Tiles & Elevator ({allFloorTiles.Count + 1})");
            if (showAvailableFloorTiles)
            {
                availableFloorTileScrollPosition = EditorGUILayout.BeginScrollView(availableFloorTileScrollPosition, GUILayout.Height(150));

                // Elevator 特殊条目
                {
                    EditorGUILayout.BeginHorizontal();
                    bool elevatorUnlocked = profile.unlockedFloorTileIds.Contains(ELEVATOR_ID);
                    GUI.color = elevatorUnlocked ? Color.green : Color.white;
                    EditorGUILayout.LabelField($"Elevator ({ELEVATOR_ID})", GUILayout.ExpandWidth(true));
                    GUI.color = Color.white;

                    if (!elevatorUnlocked)
                    {
                        if (GUILayout.Button("Unlock", GUILayout.Width(70)))
                        {
                            profile.UnlockFloorTile(ELEVATOR_ID);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }

                // FloorTile SO 条目
                foreach (var floorTile in allFloorTiles)
                {
                    if (floorTile == null) continue;

                    EditorGUILayout.BeginHorizontal();

                    bool isUnlocked = profile.unlockedFloorTileIds.Contains(floorTile.archetypeId);
                    GUI.color = isUnlocked ? Color.green : Color.white;

                    EditorGUILayout.LabelField(
                        $"{floorTile.displayName} ({floorTile.archetypeId}) [{floorTile.TileSize.x}x{floorTile.TileSize.y}]",
                        GUILayout.ExpandWidth(true));

                    GUI.color = Color.white;

                    if (!isUnlocked)
                    {
                        if (GUILayout.Button("Unlock", GUILayout.Width(70)))
                        {
                            profile.UnlockFloorTile(floorTile.archetypeId);
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();
            }
        }

        // 设计师工具直接编辑 StreamingAssets 模板，绕开 SavePathManager（运行时存档另存于 persistentDataPath）
        private static string TemplatePath =>
            Path.Combine(Application.dataPath, "StreamingAssets", "BlueprintProfile.json");

        private void DrawPathInfo()
        {
            EditorGUILayout.HelpBox(
                $"Template Path (StreamingAssets, 只读模板):\n{TemplatePath}\n\n" +
                $"Runtime Save Path (persistentDataPath):\n{Path.Combine(Application.persistentDataPath, "BlueprintProfile.json")}",
                MessageType.None
            );
        }

        private void LoadProfile()
        {
            string path = TemplatePath;
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                profile = JsonUtility.FromJson<BlueprintProfile>(json);
                Debug.Log($"[BlueprintProfileEditor] Loaded template from {path}");
            }
            else
            {
                profile = new BlueprintProfile();
                profile.unlockedShelfIds = new List<string>();
                profile.unlockedFacilityIds = new List<string>();
                profile.unlockedFloorTileIds = new List<string>();
                Debug.LogWarning($"[BlueprintProfileEditor] Template not found, created new profile");
            }

            // 确保旧JSON缺失字段时不为null
            if (profile.unlockedFloorTileIds == null)
                profile.unlockedFloorTileIds = new List<string>();
        }

        private void SaveProfile()
        {
            string path = TemplatePath;
            SavePathManager.EnsureDirectoryExists(path);

            string json = JsonUtility.ToJson(profile, true);
            File.WriteAllText(path, json);
            AssetDatabase.Refresh();
            Debug.Log($"[BlueprintProfileEditor] Saved template to {path}");
        }

        // 把当前编辑中的模板内容覆盖到 persistentDataPath 的运行时存档（无需 ClearAllSaves 即可看到模板更新）
        private void SyncToRuntime()
        {
            string runtimePath = Path.Combine(Application.persistentDataPath, "BlueprintProfile.json");
            SavePathManager.EnsureDirectoryExists(runtimePath);

            string json = JsonUtility.ToJson(profile, true);
            File.WriteAllText(runtimePath, json);
            Debug.Log($"[BlueprintProfileEditor] Applied template to runtime save: {runtimePath}");
        }

        private void ScanAllBuildings()
        {
            // 扫描所有BuildingArchetype
            BuildingArchetype[] allBuildings = Resources.LoadAll<BuildingArchetype>("ScriptableObjects/BuildingArchetype");

            allShelves.Clear();
            allFacilities.Clear();
            allFloorTiles.Clear();

            foreach (var building in allBuildings)
            {
                if (building is ShelfArchetype shelf)
                {
                    allShelves.Add(shelf);
                }
                else if (building is FacilityArchetype facility)
                {
                    allFacilities.Add(facility);
                }
                else if (building is FloorTileArchetype floorTile)
                {
                    allFloorTiles.Add(floorTile);
                }
            }

            Debug.Log($"[BlueprintProfileEditor] Scanned {allShelves.Count} shelves, {allFacilities.Count} facilities, {allFloorTiles.Count} floor tiles");
        }

        private void UnlockAll()
        {
            if (EditorUtility.DisplayDialog("Unlock All", "确定解锁所有蓝图吗？", "确定", "取消"))
            {
                profile.unlockedShelfIds.Clear();
                profile.unlockedFacilityIds.Clear();
                profile.unlockedFloorTileIds.Clear();

                foreach (var shelf in allShelves)
                {
                    if (shelf != null)
                        profile.UnlockShelf(shelf.archetypeId);
                }

                foreach (var facility in allFacilities)
                {
                    if (facility != null)
                        profile.UnlockFacility(facility.archetypeId);
                }

                foreach (var floorTile in allFloorTiles)
                {
                    if (floorTile != null)
                        profile.UnlockFloorTile(floorTile.archetypeId);
                }

                // Elevator 特殊条目
                profile.UnlockFloorTile(ELEVATOR_ID);

                Debug.Log($"[BlueprintProfileEditor] Unlocked all: {profile.unlockedShelfIds.Count} shelves, {profile.unlockedFacilityIds.Count} facilities, {profile.unlockedFloorTileIds.Count} floor tiles");
            }
        }

        private void ClearAll()
        {
            if (EditorUtility.DisplayDialog("Clear All", "确定清空所有已解锁蓝图吗？", "确定", "取消"))
            {
                profile.unlockedShelfIds.Clear();
                profile.unlockedFacilityIds.Clear();
                profile.unlockedFloorTileIds.Clear();
                Debug.Log("[BlueprintProfileEditor] Cleared all unlocked blueprints");
            }
        }
    }
}
