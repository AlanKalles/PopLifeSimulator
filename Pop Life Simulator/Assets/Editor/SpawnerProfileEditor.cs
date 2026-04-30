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
    /// SpawnerProfile.json 可视化编辑工具（设计师维护初始解锁顾客模板）
    /// - 读写 StreamingAssets/SpawnerProfile.json（git 模板）
    /// - "Apply Template to Save" 按钮把当前模板覆盖到 persistentDataPath/SpawnerProfile.json
    ///   方便不重置整个存档就预览模板修改
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
            if (GUILayout.Button("Apply Template to Save", GUILayout.Width(160)))
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
                $"Template Path (StreamingAssets, 只读模板):\n{TemplatePath}\n\n" +
                $"Runtime Save Path (persistentDataPath):\n{Path.Combine(Application.persistentDataPath, "SpawnerProfile.json")}",
                MessageType.None
            );
        }

        // 设计师工具直接编辑 StreamingAssets 模板，绕开 SavePathManager（运行时存档另存于 persistentDataPath）
        private static string TemplatePath =>
            Path.Combine(Application.dataPath, "StreamingAssets", "SpawnerProfile.json");

        private void LoadProfile()
        {
            string path = TemplatePath;
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                profile = JsonUtility.FromJson<SpawnerProfile>(json);
                Debug.Log($"[SpawnerProfileEditor] Loaded template from {path}");
            }
            else
            {
                profile = new SpawnerProfile();
                profile.unlockedCustomerIds = new List<string>();
                Debug.LogWarning($"[SpawnerProfileEditor] Template not found, created new profile");
            }
        }

        private void SaveProfile()
        {
            string path = TemplatePath;
            SavePathManager.EnsureDirectoryExists(path);

            string json = JsonUtility.ToJson(profile, true);
            File.WriteAllText(path, json);
            AssetDatabase.Refresh();
            Debug.Log($"[SpawnerProfileEditor] Saved template to {path}");
        }

        // 把当前编辑中的模板内容覆盖到 persistentDataPath 的运行时存档（无需 ClearAllSaves 即可看到模板更新）
        private void SyncToRuntime()
        {
            string runtimePath = Path.Combine(Application.persistentDataPath, "SpawnerProfile.json");
            SavePathManager.EnsureDirectoryExists(runtimePath);

            string json = JsonUtility.ToJson(profile, true);
            File.WriteAllText(runtimePath, json);
            Debug.Log($"[SpawnerProfileEditor] Applied template to runtime save: {runtimePath}");
        }
    }
}
