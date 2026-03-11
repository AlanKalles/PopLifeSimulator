using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using TMPro;

/// <summary>
/// 批量替换 TMP 字体工具，支持场景物体和 Prefab 资产
/// </summary>
public class TMPFontReplacer : EditorWindow
{
    enum Scope { Scene, Prefabs, Both }

    [SerializeField] TMP_FontAsset sourceFont;
    [SerializeField] TMP_FontAsset targetFont;
    [SerializeField] Scope scope = Scope.Both;
    [SerializeField] string prefabSearchFolder = "Assets";

    Vector2 scrollPos;
    string resultLog = "";

    [MenuItem("Tools/TMP Font Replacer")]
    public static void ShowWindow()
    {
        GetWindow<TMPFontReplacer>("TMP Font Replacer");
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("TMP 字体批量替换工具", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        sourceFont = (TMP_FontAsset)EditorGUILayout.ObjectField(
            "源字体 (要替换的)", sourceFont, typeof(TMP_FontAsset), false);

        targetFont = (TMP_FontAsset)EditorGUILayout.ObjectField(
            "目标字体 (替换为)", targetFont, typeof(TMP_FontAsset), false);

        scope = (Scope)EditorGUILayout.EnumPopup("替换范围", scope);

        if (scope == Scope.Prefabs || scope == Scope.Both)
        {
            prefabSearchFolder = EditorGUILayout.TextField("Prefab 搜索路径", prefabSearchFolder);
        }

        EditorGUILayout.Space(10);

        using (new EditorGUI.DisabledGroupScope(sourceFont == null))
        {
            if (GUILayout.Button("预览 (仅查找，不替换)", GUILayout.Height(25)))
            {
                Execute(false);
            }
        }

        using (new EditorGUI.DisabledGroupScope(sourceFont == null || targetFont == null))
        {
            if (GUILayout.Button("执行替换", GUILayout.Height(30)))
            {
                Execute(true);
            }
        }

        EditorGUILayout.Space(5);

        if (!string.IsNullOrEmpty(resultLog))
        {
            EditorGUILayout.LabelField("结果", EditorStyles.boldLabel);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            EditorGUILayout.HelpBox(resultLog, MessageType.Info);
            EditorGUILayout.EndScrollView();
        }
    }

    private void Execute(bool doReplace)
    {
        var lines = new List<string>();
        int sceneCount = 0;
        int prefabCount = 0;

        // ── 场景 ──
        if (scope == Scope.Scene || scope == Scope.Both)
        {
            if (doReplace)
            {
                Undo.SetCurrentGroupName("TMP Font Replace (Scene)");
            }

            var allTMP = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var tmp in allTMP)
            {
                if (tmp.font != sourceFont) continue;
                sceneCount++;
                string path = GetFullPath(tmp.gameObject);

                if (doReplace)
                {
                    Undo.RecordObject(tmp, "Replace TMP Font");
                    tmp.font = targetFont;
                    EditorUtility.SetDirty(tmp);
                }
                lines.Add($"  [场景] {path}");
            }
        }

        // ── Prefab 资产 ──
        if (scope == Scope.Prefabs || scope == Scope.Both)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabSearchFolder });

            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);

                if (EditorUtility.DisplayCancelableProgressBar(
                    "扫描 Prefab", assetPath, (float)i / guids.Length))
                {
                    break;
                }

                // 加载 Prefab 资产（不实例化）
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null) continue;

                var texts = prefab.GetComponentsInChildren<TMP_Text>(true);
                bool modified = false;

                foreach (var tmp in texts)
                {
                    if (tmp.font != sourceFont) continue;
                    prefabCount++;
                    string compPath = GetComponentPath(tmp, prefab);
                    lines.Add($"  [Prefab] {assetPath}  →  {compPath}");

                    if (doReplace)
                    {
                        // 直接修改 Prefab 资产中的组件
                        Undo.RecordObject(tmp, "Replace TMP Font in Prefab");
                        tmp.font = targetFont;
                        EditorUtility.SetDirty(tmp);
                        modified = true;
                    }
                }

                if (doReplace && modified)
                {
                    // 保存修改到磁盘
                    PrefabUtility.SavePrefabAsset(prefab);
                }
            }

            EditorUtility.ClearProgressBar();
        }

        // ── 汇总 ──
        int total = sceneCount + prefabCount;
        string action = doReplace ? "替换" : "找到";
        string header = $"{action}完成: 共 {total} 个组件";
        if (scope == Scope.Both)
            header += $" (场景 {sceneCount} + Prefab {prefabCount})";
        else if (scope == Scope.Scene)
            header += $" (场景 {sceneCount})";
        else
            header += $" (Prefab {prefabCount})";

        header += $"\n[{sourceFont.name}] → [{(targetFont != null ? targetFont.name : "?")}]";
        if (doReplace) header += "\n场景修改可 Ctrl+Z 撤销，Prefab 修改已保存到磁盘";
        header += "\n";

        resultLog = header + "\n" + string.Join("\n", lines);
        Debug.Log(resultLog);
    }

    // 获取场景中 GameObject 的完整路径
    private string GetFullPath(GameObject go)
    {
        string path = go.name;
        Transform parent = go.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }

    // 获取 Prefab 内部组件的相对路径
    private string GetComponentPath(TMP_Text tmp, GameObject root)
    {
        var parts = new List<string>();
        Transform t = tmp.transform;
        while (t != null && t.gameObject != root)
        {
            parts.Insert(0, t.name);
            t = t.parent;
        }
        parts.Insert(0, root.name);
        return string.Join("/", parts);
    }
}
