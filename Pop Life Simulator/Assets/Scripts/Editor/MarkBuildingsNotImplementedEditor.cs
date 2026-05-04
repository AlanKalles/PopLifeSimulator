using System.IO;
using UnityEditor;
using UnityEngine;
using PopLife.Data;

namespace PopLife.Editor
{
    /// <summary>
    /// 一次性批处理：扫描 Resources/ScriptableObjects/BuildingArchetype/Shelf/BuildingsNotImplemented/
    /// 下所有 BuildingArchetype 资源，把 isImplemented 设为 false。
    ///
    /// 设计：约定 BuildingsNotImplemented 文件夹下的 SO 是占位符（未实装）。
    /// 该文件夹**不是**必须存在的——若文件夹不存在，工具直接返回 0 修改，不报错。
    ///
    /// 入口：菜单 Tools / PopLife / Mark BuildingsNotImplemented
    /// </summary>
    public static class MarkBuildingsNotImplementedEditor
    {
        private const string TargetFolder = "Assets/Resources/ScriptableObjects/BuildingArchetype/Shelf/BuildingsNotImplemented";

        [MenuItem("Tools/PopLife/Mark BuildingsNotImplemented as isImplemented=false")]
        public static void Run()
        {
            if (!Directory.Exists(TargetFolder))
            {
                Debug.Log($"[MarkBuildingsNotImplemented] Folder '{TargetFolder}' does not exist — nothing to do.");
                return;
            }

            // 在 TargetFolder 下递归找所有 BuildingArchetype 子类的 .asset
            var guids = AssetDatabase.FindAssets("t:BuildingArchetype", new[] { TargetFolder });
            int changed = 0;
            int total = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<BuildingArchetype>(path);
                if (so == null) continue;
                total++;

                if (so.isImplemented)
                {
                    so.isImplemented = false;
                    EditorUtility.SetDirty(so);
                    changed++;
                }
            }

            if (changed > 0)
            {
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"[MarkBuildingsNotImplemented] Scanned {total} assets under '{TargetFolder}', updated {changed} (set isImplemented=false).");
        }
    }
}
