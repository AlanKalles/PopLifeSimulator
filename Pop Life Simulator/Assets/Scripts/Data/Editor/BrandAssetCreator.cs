#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace PopLife.Data.Editor
{
    /// <summary>
    /// 一次性编辑器工具：创建所有品牌 ScriptableObject 资产
    /// 使用后可删除此脚本
    /// </summary>
    public static class BrandAssetCreator
    {
        private static readonly string[] brandNames = new[]
        {
            "C¥B3R5EX",
            "SoloFans",
            "A R T D S A J I",
            "PopLife",
            "Rough Angel",
            "Good Unicorn",
            "BLUBURRY",
            "Dildo & Chbana",
            "Cucci",
            "Lewis Spitton",
            "LOVXNS"
        };

        [MenuItem("PopLife/Create Brand Assets")]
        public static void CreateAllBrands()
        {
            string folder = "Assets/Resources/ScriptableObjects/Brands";

            if (!AssetDatabase.IsValidFolder(folder))
            {
                // 逐级创建目录
                if (!AssetDatabase.IsValidFolder("Assets/Resources/ScriptableObjects"))
                    AssetDatabase.CreateFolder("Assets/Resources", "ScriptableObjects");
                AssetDatabase.CreateFolder("Assets/Resources/ScriptableObjects", "Brands");
            }

            int created = 0;
            foreach (string brandName in brandNames)
            {
                // 文件名去除非法字符
                string safeName = brandName
                    .Replace("¥", "Y")
                    .Replace("&", "and");

                string path = $"{folder}/{safeName}.asset";

                if (AssetDatabase.LoadAssetAtPath<BrandData>(path) != null)
                {
                    Debug.Log($"[BrandAssetCreator] 已存在，跳过: {brandName}");
                    continue;
                }

                var brand = ScriptableObject.CreateInstance<BrandData>();
                brand.displayName = brandName;

                AssetDatabase.CreateAsset(brand, path);
                created++;
                Debug.Log($"[BrandAssetCreator] 已创建: {brandName} → {path}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[BrandAssetCreator] 完成！共创建 {created} 个品牌资产。");
        }
    }
}
#endif
