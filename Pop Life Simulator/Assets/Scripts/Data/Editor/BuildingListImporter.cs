// Assets/Scripts/Data/Editor/BuildingListImporter.cs
// 重构版：支持新CSV格式，数据从第3行开始，列固定，支持覆盖和扫描

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PopLife.Data;
using UnityEditor;
using UnityEngine;

namespace PopLife.Data.Editor
{
    public class BuildingListImporter : EditorWindow
    {
        [Header("输入")]
        public TextAsset textAsset;
        public string externalFilePath;

        [Header("输出")]
        public string outputDir = "Assets/Resources/ScriptableObjects/BuildingArchetype/Shelf";

        [Header("选项")]
        [Tooltip("默认拆除返还率（0-1）")]
        public float defaultDestroyRefundRate = 0.75f;

        [Tooltip("扫描模式：导入完成后扫描Shelf文件夹，显示未被覆盖的文件")]
        public bool scanForUnprocessedFiles = true;

        [MenuItem("PopLife/Import/BuildingList → Shelf SOs")]
        private static void Open()
        {
            var window = GetWindow<BuildingListImporter>("Shelf Importer");
            window.minSize = new Vector2(500, 300);
        }

        void OnGUI()
        {
            GUILayout.Label("输入文件", EditorStyles.boldLabel);

            textAsset = (TextAsset)EditorGUILayout.ObjectField("CSV文件（项目内）", textAsset, typeof(TextAsset), false);

            EditorGUILayout.BeginHorizontal();
            externalFilePath = EditorGUILayout.TextField("外部CSV文件", externalFilePath);
            if (GUILayout.Button("浏览", GUILayout.Width(80)))
            {
                var path = EditorUtility.OpenFilePanel("选择CSV文件", "", "csv,txt");
                if (!string.IsNullOrEmpty(path)) externalFilePath = path;
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("输出设置", EditorStyles.boldLabel);

            outputDir = EditorGUILayout.TextField("输出文件夹", outputDir);
            defaultDestroyRefundRate = EditorGUILayout.Slider("默认返还率", defaultDestroyRefundRate, 0f, 1f);

            GUILayout.Space(10);
            GUILayout.Label("导入选项", EditorStyles.boldLabel);
            scanForUnprocessedFiles = EditorGUILayout.Toggle("扫描未处理文件", scanForUnprocessedFiles);

            GUILayout.Space(15);

            EditorGUILayout.HelpBox(
                "CSV格式说明：\n" +
                "• 数据从第3行开始\n" +
                "• 列1: ID, 列2: 名称, 列3: 类别, 列4: 建造成本\n" +
                "• 列5-9: Level1数据（价格/维护/库存/吸引力/升级Fame）\n" +
                "• 列10-14: Level2, 列15-19: Level3, 列20-24: Level4\n" +
                "• 遇到同名文件会询问是否覆盖",
                MessageType.Info);

            GUILayout.Space(10);

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("开始导入", GUILayout.Height(35)))
            {
                Import();
            }
            GUI.backgroundColor = Color.white;
        }

        void Import()
        {
            try
            {
                // 读取文件
                string rawText = null;
                if (textAsset != null)
                {
                    rawText = textAsset.text;
                }
                else if (!string.IsNullOrEmpty(externalFilePath) && File.Exists(externalFilePath))
                {
                    rawText = File.ReadAllText(externalFilePath, Encoding.UTF8);
                }

                if (string.IsNullOrEmpty(rawText))
                {
                    EditorUtility.DisplayDialog("错误", "请选择一个CSV文件", "确定");
                    return;
                }

                // 解析CSV
                var rows = ParseCSV(rawText);
                if (rows.Count < 3)
                {
                    EditorUtility.DisplayDialog("错误", "CSV文件至少需要3行（2行表头+数据）", "确定");
                    return;
                }

                // 确保输出目录存在
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                    AssetDatabase.Refresh();
                }

                // 统计
                var processedArchetypeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var processedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int created = 0;
                int overwritten = 0;
                int skipped = 0;
                var importLog = new StringBuilder();

                // 从第3行开始导入（索引2）
                for (int rowIdx = 2; rowIdx < rows.Count; rowIdx++)
                {
                    var row = rows[rowIdx];

                    // 列索引（0-based）
                    string archetypeId = GetCell(row, 0).Trim();
                    string displayName = GetCell(row, 1).Trim();
                    string categoryStr = GetCell(row, 2).Trim();
                    string buildCostStr = GetCell(row, 3).Trim();

                    // 跳过空行
                    if (string.IsNullOrEmpty(archetypeId) && string.IsNullOrEmpty(displayName))
                        continue;

                    // 解析基础数据
                    if (!TryParseCategory(categoryStr, out var category))
                    {
                        Debug.LogWarning($"行{rowIdx + 1}: 无法识别类别 '{categoryStr}'，跳过");
                        skipped++;
                        continue;
                    }

                    int buildCost = ParseInt(buildCostStr);

                    // 解析4个等级的数据（列4-23，即索引4-23）
                    var levels = new List<ShelfArchetype.ShelfLevelData>();

                    // Level 1: 列5-9 (索引4-8)
                    AddLevel(levels, 1, row, 4, 5, 6, 7, 8);
                    // Level 2: 列10-14 (索引9-13)
                    AddLevel(levels, 2, row, 9, 10, 11, 12, 13);
                    // Level 3: 列15-19 (索引14-18)
                    AddLevel(levels, 3, row, 14, 15, 16, 17, 18);
                    // Level 4: 列20-24 (索引19-23)
                    AddLevel(levels, 4, row, 19, 20, 21, 22, 23);

                    if (levels.Count == 0)
                    {
                        Debug.LogWarning($"行{rowIdx + 1}: 没有有效等级数据，跳过");
                        skipped++;
                        continue;
                    }

                    // 确定文件名（使用displayName，如果为空则使用archetypeId）
                    string fileName = string.IsNullOrWhiteSpace(displayName) ? archetypeId : displayName;
                    fileName = SanitizeFileName(fileName);
                    string assetPath = $"{outputDir}/{fileName}.asset";

                    // 检查文件是否已存在
                    ShelfArchetype existingAsset = AssetDatabase.LoadAssetAtPath<ShelfArchetype>(assetPath);

                    if (existingAsset != null)
                    {
                        // 询问是否覆盖
                        int choice = EditorUtility.DisplayDialogComplex(
                            "文件已存在",
                            $"已存在同名文件: {fileName}.asset\n" +
                            $"路径: {assetPath}\n" +
                            $"是否覆盖？",
                            "覆盖", "跳过", "取消导入"
                        );

                        if (choice == 2) // 取消
                        {
                            Debug.Log("导入已取消");
                            return;
                        }

                        if (choice == 1) // 跳过
                        {
                            skipped++;
                            importLog.AppendLine($"跳过: {fileName}");
                            continue;
                        }

                        // 覆盖（choice == 0）
                        Undo.RecordObject(existingAsset, "覆盖 ShelfArchetype");
                        UpdateShelfArchetype(existingAsset, archetypeId, displayName, category, buildCost, levels);
                        EditorUtility.SetDirty(existingAsset);
                        overwritten++;
                        importLog.AppendLine($"覆盖: {fileName} (ID: {archetypeId})");
                        processedArchetypeIds.Add(archetypeId);
                        processedFileNames.Add(fileName);
                    }
                    else
                    {
                        // 创建新文件
                        var newAsset = ScriptableObject.CreateInstance<ShelfArchetype>();
                        UpdateShelfArchetype(newAsset, archetypeId, displayName, category, buildCost, levels);

                        AssetDatabase.CreateAsset(newAsset, assetPath);
                        created++;
                        importLog.AppendLine($"创建: {fileName} (ID: {archetypeId})");
                        processedArchetypeIds.Add(archetypeId);
                        processedFileNames.Add(fileName);
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // 扫描未处理的文件
                var unprocessedFiles = new List<string>();
                if (scanForUnprocessedFiles)
                {
                    unprocessedFiles = ScanUnprocessedFiles(processedFileNames);
                }

                // 显示结果
                ShowImportResults(created, overwritten, skipped, importLog.ToString(), unprocessedFiles);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("导入错误", ex.Message, "确定");
            }
        }

        /// <summary>
        /// 更新ShelfArchetype的数据（仅更新可导入的字段）
        /// </summary>
        void UpdateShelfArchetype(
            ShelfArchetype target,
            string archetypeId,
            string displayName,
            ProductCategory category,
            int buildCost,
            List<ShelfArchetype.ShelfLevelData> levels)
        {
            target.archetypeId = archetypeId;
            target.displayName = string.IsNullOrWhiteSpace(displayName) ? archetypeId : displayName;
            target.category = category;
            target.buildCost = buildCost;
            target.destroyRefundRate = defaultDestroyRefundRate;

            // 使用反射设置私有字段 shelfLevels
            var levelArray = levels.OrderBy(l => l.level).ToArray();
            var field = typeof(ShelfArchetype).GetField("shelfLevels",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                field.SetValue(target, levelArray);
            }
            else
            {
                Debug.LogError("无法找到 shelfLevels 字段");
            }
        }

        /// <summary>
        /// 从行中解析并添加等级数据
        /// </summary>
        void AddLevel(
            List<ShelfArchetype.ShelfLevelData> levels,
            int level,
            List<string> row,
            int priceIdx,
            int maintenanceIdx,
            int stockIdx,
            int attractivenessIdx,
            int fameIdx)
        {
            int price = ParseInt(GetCell(row, priceIdx));
            int maintenance = ParseInt(GetCell(row, maintenanceIdx));
            int stock = ParseInt(GetCell(row, stockIdx));
            float attractiveness = ParseFloat(GetCell(row, attractivenessIdx));
            int fameCost = ParseInt(GetCell(row, fameIdx));

            // 如果所有数据都是0，则不添加这个等级
            if (price == 0 && maintenance == 0 && stock == 0 &&
                Mathf.Approximately(attractiveness, 0f) && fameCost == 0)
            {
                return;
            }

            var levelData = new ShelfArchetype.ShelfLevelData
            {
                level = level,
                price = price,
                maintenanceFee = maintenance,
                maxStock = stock,
                attractiveness = attractiveness,
                upgradeFameCost = fameCost
            };

            levels.Add(levelData);
        }

        /// <summary>
        /// 扫描输出目录，找出未被此次导入处理的文件
        /// </summary>
        List<string> ScanUnprocessedFiles(HashSet<string> processedFileNames)
        {
            var unprocessed = new List<string>();

            if (!Directory.Exists(outputDir))
                return unprocessed;

            var allAssetPaths = Directory.GetFiles(outputDir, "*.asset", SearchOption.TopDirectoryOnly);

            foreach (var path in allAssetPaths)
            {
                string fileName = Path.GetFileNameWithoutExtension(path);

                // 排除.meta文件
                if (fileName.EndsWith(".meta"))
                    continue;

                if (!processedFileNames.Contains(fileName))
                {
                    unprocessed.Add(fileName);
                }
            }

            return unprocessed;
        }

        /// <summary>
        /// 显示导入结果窗口
        /// </summary>
        void ShowImportResults(
            int created,
            int overwritten,
            int skipped,
            string importLog,
            List<string> unprocessedFiles)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 导入完成 ===");
            sb.AppendLine();
            sb.AppendLine($"创建: {created}");
            sb.AppendLine($"覆盖: {overwritten}");
            sb.AppendLine($"跳过: {skipped}");
            sb.AppendLine();
            sb.AppendLine("--- 详细日志 ---");
            sb.AppendLine(importLog);

            if (scanForUnprocessedFiles && unprocessedFiles.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("--- 未被此次导入处理的现有文件 ---");
                sb.AppendLine($"（共 {unprocessedFiles.Count} 个）");
                foreach (var file in unprocessedFiles)
                {
                    sb.AppendLine($"  • {file}");
                }
            }

            Debug.Log(sb.ToString());

            // 弹窗显示摘要
            string summary = $"创建: {created}\n覆盖: {overwritten}\n跳过: {skipped}";
            if (unprocessedFiles.Count > 0)
            {
                summary += $"\n\n未处理的现有文件: {unprocessedFiles.Count} 个\n（详见Console日志）";
            }

            EditorUtility.DisplayDialog("导入完成", summary, "确定");
        }

        // ========== 工具方法 ==========

        /// <summary>
        /// 解析CSV文本
        /// </summary>
        static List<List<string>> ParseCSV(string text)
        {
            var rows = new List<List<string>>();
            var lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                rows.Add(SplitCSVLine(line));
            }

            return rows;
        }

        /// <summary>
        /// 分割CSV行（支持引号包裹）
        /// </summary>
        static List<string> SplitCSVLine(string line)
        {
            var cells = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // 双引号转义
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    cells.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }

            cells.Add(sb.ToString());
            return cells;
        }

        static string GetCell(List<string> row, int index)
        {
            return (index >= 0 && index < row.Count) ? row[index] : "";
        }

        static int ParseInt(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            // 移除非数字字符（保留负号和小数点）
            var cleaned = new string(value.Where(c => char.IsDigit(c) || c == '-' || c == '.').ToArray());

            if (float.TryParse(cleaned,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float result))
            {
                return Mathf.RoundToInt(result);
            }

            return 0;
        }

        static float ParseFloat(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0f;

            var cleaned = new string(value.Where(c => char.IsDigit(c) || c == '-' || c == '.').ToArray());

            if (float.TryParse(cleaned,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float result))
            {
                return result;
            }

            return 0f;
        }

        static bool TryParseCategory(string categoryStr, out ProductCategory category)
        {
            // 尝试精确匹配
            if (Enum.TryParse<ProductCategory>(categoryStr, true, out category))
                return true;

            // 模糊匹配
            categoryStr = categoryStr.ToLowerInvariant().Trim();

            if (categoryStr.Contains("ling"))
            {
                category = ProductCategory.Lingerie;
                return true;
            }
            if (categoryStr.Contains("cond"))
            {
                category = ProductCategory.Condom;
                return true;
            }
            if (categoryStr.Contains("vib"))
            {
                category = ProductCategory.Vibrator;
                return true;
            }
            if (categoryStr.Contains("flesh"))
            {
                category = ProductCategory.Fleshlight;
                return true;
            }
            if (categoryStr.Contains("lub"))
            {
                category = ProductCategory.Lubricant;
                return true;
            }
            if (categoryStr.Contains("bdsm"))
            {
                category = ProductCategory.BDSM;
                return true;
            }

            // 特殊处理：Dildo可能属于BDSM或其他
            if (categoryStr.Contains("dildo"))
            {
                category = ProductCategory.BDSM;
                return true;
            }

            category = default;
            return false;
        }

        static string SanitizeFileName(string fileName)
        {
            // 移除文件名中的非法字符
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var ch in invalidChars)
            {
                fileName = fileName.Replace(ch, '_');
            }
            return fileName.Trim();
        }
    }
}
