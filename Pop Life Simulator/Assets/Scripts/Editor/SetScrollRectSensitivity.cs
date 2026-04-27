using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PopLife.Editor
{
    public static class SetScrollRectSensitivity
    {
        private const float TargetSensitivity = 2f;

        [MenuItem("Tools/UI/Set All ScrollRect Sensitivity = 2 (Loaded Scenes + Scene Prefab Assets)")]
        public static void ApplyToCurrentScene()
        {
            var loadedScenes = GetLoadedScenes();
            if (loadedScenes.Count == 0)
            {
                EditorUtility.DisplayDialog("ScrollRect 批量设置", "当前没有已加载的有效场景。", "OK");
                return;
            }

            var found = FindScrollRectsInLoadedScenes(loadedScenes);

            if (found.Count == 0)
            {
                EditorUtility.DisplayDialog("ScrollRect 批量设置", $"已扫描已加载场景，但没有找到 ScrollRect。\n\n已扫描：{string.Join(", ", loadedScenes.ConvertAll(s => s.name))}", "OK");
                return;
            }

            int changed = 0;
            int prefabAssetsChanged = 0;
            var changedPrefabAssetComponents = new HashSet<ScrollRect>();

            Undo.SetCurrentGroupName("Set ScrollRect Sensitivity");
            int undoGroup = Undo.GetCurrentGroup();

            foreach (var sr in found)
            {
                if (sr == null) continue;

                if (SetSensitivity(sr))
                {
                    changed++;
                    EditorSceneManager.MarkSceneDirty(sr.gameObject.scene);
                }

                var prefabAssetScrollRect = GetPrefabAssetScrollRect(sr);
                if (prefabAssetScrollRect != null && changedPrefabAssetComponents.Add(prefabAssetScrollRect))
                {
                    if (SetSensitivity(prefabAssetScrollRect))
                    {
                        prefabAssetsChanged++;
                    }
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            if (prefabAssetsChanged > 0)
            {
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"[SetScrollRectSensitivity] 扫描 {loadedScenes.Count} 个已加载场景（{string.Join(", ", loadedScenes.ConvertAll(s => s.name))}），找到 {found.Count} 个场景 ScrollRect，修改场景组件 {changed} 个，修改场景中 prefab 本体组件 {prefabAssetsChanged} 个，目标值 = {TargetSensitivity}。记得 Ctrl+S 保存场景。");
        }

        private static List<Scene> GetLoadedScenes()
        {
            var scenes = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isLoaded)
                {
                    scenes.Add(scene);
                }
            }

            return scenes;
        }

        private static List<ScrollRect> FindScrollRectsInLoadedScenes(List<Scene> loadedScenes)
        {
            var loadedSceneHandles = new HashSet<int>();
            foreach (var scene in loadedScenes)
            {
                loadedSceneHandles.Add(scene.handle);
            }

            var found = new List<ScrollRect>();
            foreach (var scrollRect in Resources.FindObjectsOfTypeAll<ScrollRect>())
            {
                if (scrollRect == null) continue;
                if (EditorUtility.IsPersistent(scrollRect)) continue;

                var scene = scrollRect.gameObject.scene;
                if (!scene.IsValid() || !scene.isLoaded) continue;
                if (!loadedSceneHandles.Contains(scene.handle)) continue;

                found.Add(scrollRect);
            }

            return found;
        }

        private static bool SetSensitivity(ScrollRect scrollRect)
        {
            if (Mathf.Approximately(scrollRect.scrollSensitivity, TargetSensitivity))
            {
                return false;
            }

            Undo.RecordObject(scrollRect, "Set ScrollRect Sensitivity");
            scrollRect.scrollSensitivity = TargetSensitivity;
            EditorUtility.SetDirty(scrollRect);
            return true;
        }

        private static ScrollRect GetPrefabAssetScrollRect(ScrollRect sceneScrollRect)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(sceneScrollRect))
            {
                return null;
            }

            var prefabAssetScrollRect = PrefabUtility.GetCorrespondingObjectFromSource(sceneScrollRect);
            if (prefabAssetScrollRect == null || !AssetDatabase.Contains(prefabAssetScrollRect))
            {
                return null;
            }

            return prefabAssetScrollRect;
        }
    }
}
