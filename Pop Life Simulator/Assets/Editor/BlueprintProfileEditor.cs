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
        private Vector2 availableShelfScrollPosition;
        private Vector2 availableFacilityScrollPosition;

        private string newShelfId = "";
        private string newFacilityId = "";

        private List<ShelfArchetype> allShelves = new List<ShelfArchetype>();
        private List<FacilityArchetype> allFacilities = new List<FacilityArchetype>();

        private bool showAvailableShelves = false;
        private bool showAvailableFacilities = false;

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

            // 分为左右两栏
            EditorGUILayout.BeginHorizontal();

            // 左栏：货架
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width / 2 - 10));
            DrawShelfSection();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // 右栏：设施
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width / 2 - 10));
            DrawFacilitySection();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

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
            if (GUILayout.Button("Sync to Runtime", GUILayout.Width(120)))
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

        private void DrawPathInfo()
        {
            EditorGUILayout.HelpBox(
                $"StreamingAssets Path:\n{SavePathManager.GetReadPath("BlueprintProfile.json")}\n\n" +
                $"Runtime Path (persistentDataPath):\n{Path.Combine(Application.persistentDataPath, "BlueprintProfile.json")}",
                MessageType.None
            );
        }

        private void LoadProfile()
        {
            string path = SavePathManager.GetReadPath("BlueprintProfile.json");
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                profile = JsonUtility.FromJson<BlueprintProfile>(json);
                Debug.Log($"[BlueprintProfileEditor] Loaded from {path}");
            }
            else
            {
                profile = new BlueprintProfile();
                profile.unlockedShelfIds = new List<string>();
                profile.unlockedFacilityIds = new List<string>();
                Debug.LogWarning($"[BlueprintProfileEditor] File not found, created new profile");
            }
        }

        private void SaveProfile()
        {
            string path = SavePathManager.GetWritePath("BlueprintProfile.json");
            SavePathManager.EnsureDirectoryExists(path);

            string json = JsonUtility.ToJson(profile, true);
            File.WriteAllText(path, json);
            AssetDatabase.Refresh();
            Debug.Log($"[BlueprintProfileEditor] Saved to {path}");
        }

        private void SyncToRuntime()
        {
            string runtimePath = Path.Combine(Application.persistentDataPath, "BlueprintProfile.json");
            SavePathManager.EnsureDirectoryExists(runtimePath);

            string json = JsonUtility.ToJson(profile, true);
            File.WriteAllText(runtimePath, json);
            Debug.Log($"[BlueprintProfileEditor] Synced to {runtimePath}");
        }

        private void ScanAllBuildings()
        {
            // 扫描所有BuildingArchetype
            BuildingArchetype[] allBuildings = Resources.LoadAll<BuildingArchetype>("ScriptableObjects/BuildingArchetype");

            allShelves.Clear();
            allFacilities.Clear();

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
            }

            Debug.Log($"[BlueprintProfileEditor] Scanned {allShelves.Count} shelves and {allFacilities.Count} facilities");
        }

        private void UnlockAll()
        {
            if (EditorUtility.DisplayDialog("Unlock All", "确定解锁所有货架和设施蓝图吗？", "确定", "取消"))
            {
                profile.unlockedShelfIds.Clear();
                profile.unlockedFacilityIds.Clear();

                foreach (var shelf in allShelves)
                {
                    if (shelf != null)
                    {
                        profile.UnlockShelf(shelf.archetypeId);
                    }
                }

                foreach (var facility in allFacilities)
                {
                    if (facility != null)
                    {
                        profile.UnlockFacility(facility.archetypeId);
                    }
                }

                Debug.Log($"[BlueprintProfileEditor] Unlocked all blueprints: {profile.unlockedShelfIds.Count} shelves, {profile.unlockedFacilityIds.Count} facilities");
            }
        }

        private void ClearAll()
        {
            if (EditorUtility.DisplayDialog("Clear All", "确定清空所有已解锁蓝图吗？", "确定", "取消"))
            {
                profile.unlockedShelfIds.Clear();
                profile.unlockedFacilityIds.Clear();
                Debug.Log("[BlueprintProfileEditor] Cleared all unlocked blueprints");
            }
        }
    }
}
