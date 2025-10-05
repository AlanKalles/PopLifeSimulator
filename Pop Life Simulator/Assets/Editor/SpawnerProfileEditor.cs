using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using PopLife.Customers.Data;
using PopLife.Customers.Editor;
using PopLife.Utility;

namespace PopLife.Editor
{
    /// <summary>
    /// SpawnerProfile.json 可视化编辑工具
    /// 功能:
    /// - 管理解锁顾客ID列表
    /// - 读写 StreamingAssets/SpawnerProfile.json
    /// - 一键同步到 persistentDataPath (测试用)
    /// </summary>
    public class SpawnerProfileEditor : EditorWindow
    {
        private SpawnerProfile profile;
        private Vector2 scrollPosition;
        private string newCustomerId = "";

        [MenuItem("PopLife/Spawner Profile Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<SpawnerProfileEditor>("Spawner Profile Editor");
            window.minSize = new Vector2(400, 300);
        }

        private void OnEnable()
        {
            LoadProfile();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Spawner Profile Editor", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("管理可生成的顾客ID列表 (存储于 StreamingAssets/SpawnerProfile.json)", MessageType.Info);

            EditorGUILayout.Space(10);

            // 工具栏
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
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Open Customer Records Editor", GUILayout.Width(200)))
            {
                CustomerRecordsEditor.ShowWindow();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // 添加新ID
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Add Customer ID:", GUILayout.Width(120));
            newCustomerId = EditorGUILayout.TextField(newCustomerId);
            if (GUILayout.Button("Add", GUILayout.Width(50)))
            {
                if (!string.IsNullOrEmpty(newCustomerId) && !profile.unlockedCustomerIds.Contains(newCustomerId))
                {
                    profile.unlockedCustomerIds.Add(newCustomerId);
                    newCustomerId = "";
                    GUI.FocusControl(null);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // 解锁列表
            EditorGUILayout.LabelField($"Unlocked Customers ({profile.unlockedCustomerIds.Count})", EditorStyles.boldLabel);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            for (int i = profile.unlockedCustomerIds.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(profile.unlockedCustomerIds[i], GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Remove", GUILayout.Width(70)))
                {
                    profile.unlockedCustomerIds.RemoveAt(i);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);

            // 路径信息
            EditorGUILayout.HelpBox(
                $"StreamingAssets Path:\n{SavePathManager.GetReadPath("SpawnerProfile.json")}\n\n" +
                $"Runtime Path (persistentDataPath):\n{Path.Combine(Application.persistentDataPath, "SpawnerProfile.json")}",
                MessageType.None
            );
        }

        private void LoadProfile()
        {
            string path = SavePathManager.GetReadPath("SpawnerProfile.json");
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                profile = JsonUtility.FromJson<SpawnerProfile>(json);
                Debug.Log($"[SpawnerProfileEditor] Loaded from {path}");
            }
            else
            {
                profile = new SpawnerProfile();
                profile.unlockedCustomerIds = new List<string>();
                Debug.LogWarning($"[SpawnerProfileEditor] File not found, created new profile");
            }
        }

        private void SaveProfile()
        {
            string path = SavePathManager.GetWritePath("SpawnerProfile.json");
            SavePathManager.EnsureDirectoryExists(path);

            string json = JsonUtility.ToJson(profile, true);
            File.WriteAllText(path, json);
            AssetDatabase.Refresh();
            Debug.Log($"[SpawnerProfileEditor] Saved to {path}");
        }

        private void SyncToRuntime()
        {
            string runtimePath = Path.Combine(Application.persistentDataPath, "SpawnerProfile.json");
            SavePathManager.EnsureDirectoryExists(runtimePath);

            string json = JsonUtility.ToJson(profile, true);
            File.WriteAllText(runtimePath, json);
            Debug.Log($"[SpawnerProfileEditor] Synced to {runtimePath}");
        }
    }
}
