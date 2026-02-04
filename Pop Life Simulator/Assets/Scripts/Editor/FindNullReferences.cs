using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 查找场景中所有空引用的工具
/// </summary>
public class FindNullReferences : EditorWindow
{
    [MenuItem("Tools/Find Null References")]
    public static void ShowWindow()
    {
        GetWindow<FindNullReferences>("Find Null References");
    }

    private Vector2 scrollPos;

    private void OnGUI()
    {
        if (GUILayout.Button("扫描当前场景", GUILayout.Height(30)))
        {
            ScanScene();
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        EditorGUILayout.EndScrollView();
    }

    private void ScanScene()
    {
        Debug.Log("=== 开始扫描场景中的空引用 ===");

        // 检查所有 GameObject
        var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        int nullCount = 0;

        foreach (var go in allObjects)
        {
            if (go == null) continue;
            if (!go.scene.isLoaded) continue; // 跳过预制体资源

            // 检查是否有损坏的组件
            var components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    Debug.LogError($"[空组件] GameObject: {GetFullPath(go)} 的第 {i} 个组件为 null（脚本丢失）", go);
                    nullCount++;
                }
            }

            // 检查 Image 组件的 sprite 引用
            var images = go.GetComponents<Image>();
            foreach (var img in images)
            {
                if (img == null) continue;

                // 检查 material
                try
                {
                    var mat = img.material;
                }
                catch
                {
                    Debug.LogError($"[Image Material 错误] {GetFullPath(go)}", go);
                    nullCount++;
                }
            }

            // 检查 RectTransform
            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                try
                {
                    var parent = rt.parent;
                }
                catch
                {
                    Debug.LogError($"[RectTransform Parent 错误] {GetFullPath(go)}", go);
                    nullCount++;
                }
            }
        }

        // 检查预制体实例
        var prefabStages = PrefabStageUtility.GetCurrentPrefabStage();

        Debug.Log($"=== 扫描完成，发现 {nullCount} 个问题 ===");

        if (nullCount == 0)
        {
            Debug.Log("未发现空引用问题。错误可能是由运行时动态对象引起的。");
            Debug.Log("建议：在 Play 模式下重现错误，然后暂停游戏检查 Hierarchy。");
        }
    }

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
}