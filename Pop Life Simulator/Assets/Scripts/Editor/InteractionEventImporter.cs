using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PopLife.Data;

namespace PopLife.Editor
{
    /// <summary>
    /// CSV 导入工具：批量创建 InteractionEventData SO 和 Dialogue Database 对话
    /// 菜单：Tools > PopLife > Import Interaction Events (CSV)
    /// </summary>
    public class InteractionEventImporter : EditorWindow
    {
        [SerializeField] private TextAsset csvAsset;
        [SerializeField] private DialogueDatabase dialogueDatabase;
        [SerializeField] private string soOutputDir = "Assets/Resources/ScriptableObjects/InteractionEvents";
        [SerializeField] private string conversationPrefix = "Interaction/";

        private const int DefaultFameReward = 20;
        private const float DefaultBubbleTimeout = 10f;
        private const string CustomerActorName = "Customer";
        private const string PlayerActorName = "Player";

        private Vector2 scrollPos;
        private string lastLog = "";

        [MenuItem("PopLife/Import/InteractionEvents (CSV)")]
        private static void Open()
        {
            var window = GetWindow<InteractionEventImporter>("Interaction Importer");
            window.minSize = new Vector2(500, 380);
        }

        private void OnGUI()
        {
            GUILayout.Label("交互事件 CSV 导入工具", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            csvAsset = (TextAsset)EditorGUILayout.ObjectField(
                "CSV 文件", csvAsset, typeof(TextAsset), false);
            dialogueDatabase = (DialogueDatabase)EditorGUILayout.ObjectField(
                "Dialogue Database", dialogueDatabase, typeof(DialogueDatabase), false);

            EditorGUILayout.Space();
            soOutputDir = EditorGUILayout.TextField("SO 输出路径", soOutputDir);
            conversationPrefix = EditorGUILayout.TextField("对话前缀", conversationPrefix);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "CSV 格式：\n" +
                "Category, Question, Answer1, Answer2, Answer3, Correct, " +
                "Correct Response, Incorrect Response, Attached to, Frequency, Reward\n\n" +
                "• Category: Fleshlights/Dildos/Vibrators/Lingerie/Wellness/General\n" +
                "• Correct: Ans1/Ans2/Ans3\n" +
                "• Attached to: 货架SO名(逗号分隔), Any 或留空=不限\n" +
                "• Frequency/Reward: 忽略，统一 20 fame",
                MessageType.Info);

            EditorGUILayout.Space();
            EditorGUI.BeginDisabledGroup(csvAsset == null || dialogueDatabase == null);
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("开始导入", GUILayout.Height(35)))
            {
                Import();
            }
            GUI.backgroundColor = Color.white;
            EditorGUI.EndDisabledGroup();

            if (!string.IsNullOrEmpty(lastLog))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("导入日志：", EditorStyles.boldLabel);
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(150));
                EditorGUILayout.TextArea(lastLog, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
        }

        // ══════════════════════════════════════
        // 导入主流程
        // ══════════════════════════════════════
        private void Import()
        {
            var log = new StringBuilder();
            int soCreated = 0, soSkipped = 0, convCreated = 0, convSkipped = 0;

            try
            {
                // 确保输出目录存在
                EnsureFolderExists(soOutputDir);

                // 解析 CSV
                var rows = ParseCSV(csvAsset.text);
                if (rows.Count < 2)
                {
                    lastLog = "CSV 为空或只有表头。";
                    return;
                }
                log.AppendLine($"解析到 {rows.Count - 1} 行数据（跳过表头）");

                // 预加载所有 ShelfArchetype SO
                var allShelves = Resources.LoadAll<ShelfArchetype>(
                    "ScriptableObjects/BuildingArchetype");
                log.AppendLine($"加载了 {allShelves.Length} 个 ShelfArchetype");

                // 确保 Customer/Player Actor 存在
                var template = Template.FromDefault();
                int customerActorId = EnsureActor(template, CustomerActorName, false);
                int playerActorId = EnsureActor(template, PlayerActorName, true);
                log.AppendLine($"Actor: {CustomerActorName}={customerActorId}, {PlayerActorName}={playerActorId}");

                // 获取下一个 conversation ID
                int nextConvId = template.GetNextConversationID(dialogueDatabase);

                // 按 Category 分组计数器
                var categoryCounter = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                // 遍历数据行（跳过表头）
                for (int rowIdx = 1; rowIdx < rows.Count; rowIdx++)
                {
                    var row = rows[rowIdx];

                    string category = GetCell(row, 0).Trim();
                    string question = GetCell(row, 1).Trim();
                    if (string.IsNullOrEmpty(question))
                    {
                        log.AppendLine($"  行{rowIdx + 1}: 跳过（Question为空）");
                        continue;
                    }

                    // 生成 EventId
                    if (!categoryCounter.ContainsKey(category))
                        categoryCounter[category] = 0;
                    categoryCounter[category]++;
                    string eventId = $"{category}_{categoryCounter[category]:D3}";

                    // 解析各字段
                    string answer1 = GetCell(row, 2).Trim();
                    string answer2 = GetCell(row, 3).Trim();
                    string answer3 = GetCell(row, 4).Trim();
                    string correctStr = GetCell(row, 5).Trim(); // "Ans1"/"Ans2"/"Ans3"
                    string correctResp = GetCell(row, 6).Trim();
                    string incorrectResp = GetCell(row, 7).Trim();
                    string attachedTo = GetCell(row, 8).Trim();

                    int correctIndex = ParseCorrectIndex(correctStr);
                    if (correctIndex < 1 || correctIndex > 3)
                    {
                        log.AppendLine($"  行{rowIdx + 1}: 跳过（Correct 格式错误: '{correctStr}'）");
                        continue;
                    }

                    // 解析 Category → ProductCategory
                    bool useFilterCategory = true;
                    ProductCategory productCategory = default;
                    switch (category.ToLowerInvariant())
                    {
                        case "fleshlights": productCategory = ProductCategory.Fleshlight; break;
                        case "dildos": productCategory = ProductCategory.Dildo; break;
                        case "vibrators": productCategory = ProductCategory.Vibrator; break;
                        case "lingerie": productCategory = ProductCategory.Lingerie; break;
                        case "wellness": productCategory = ProductCategory.Wellness; break;
                        case "general":
                            useFilterCategory = false;
                            break;
                        default:
                            if (!Enum.TryParse(category, true, out productCategory))
                            {
                                useFilterCategory = false;
                                log.AppendLine($"  行{rowIdx + 1}: 未知类别 '{category}'，不过滤类别");
                            }
                            break;
                    }

                    // 解析 Attached to → ShelfArchetype[]
                    bool useFilterShelf = false;
                    ShelfArchetype[] requiredShelves = null;
                    if (!string.IsNullOrEmpty(attachedTo)
                        && !attachedTo.Equals("Any", StringComparison.OrdinalIgnoreCase))
                    {
                        var shelfNames = attachedTo.Split(',');
                        var found = new List<ShelfArchetype>();
                        foreach (var rawName in shelfNames)
                        {
                            var trimmed = rawName.Trim();
                            if (string.IsNullOrEmpty(trimmed)) continue;

                            var shelf = FindShelfByName(trimmed, allShelves);
                            if (shelf != null)
                            {
                                found.Add(shelf);
                            }
                            else
                            {
                                log.AppendLine($"  行{rowIdx + 1}: ⚠️ 未找到货架 '{trimmed}'");
                            }
                        }
                        if (found.Count > 0)
                        {
                            useFilterShelf = true;
                            requiredShelves = found.ToArray();
                        }
                    }

                    // ── 创建 SO ──
                    string soPath = $"{soOutputDir}/{eventId}.asset";
                    if (AssetDatabase.LoadAssetAtPath<InteractionEventData>(soPath) != null)
                    {
                        soSkipped++;
                        log.AppendLine($"  [跳过SO] {eventId}");
                    }
                    else
                    {
                        CreateSO(soPath, eventId, useFilterCategory, productCategory,
                            useFilterShelf, requiredShelves);
                        soCreated++;
                        log.AppendLine($"  [创建SO] {eventId}");
                    }

                    // ── 创建对话 ──
                    string convTitle = conversationPrefix + eventId;
                    if (dialogueDatabase.conversations.Exists(c => c.Title == convTitle))
                    {
                        convSkipped++;
                        log.AppendLine($"  [跳过对话] {convTitle}");
                        continue;
                    }

                    CreateConversation(template, convTitle, nextConvId,
                        customerActorId, playerActorId,
                        question, answer1, answer2, answer3,
                        correctIndex, correctResp, incorrectResp);
                    convCreated++;
                    nextConvId++;
                    log.AppendLine($"  [创建对话] {convTitle}");
                }

                // 保存
                EditorUtility.SetDirty(dialogueDatabase);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                string summary = $"创建 {soCreated} 个 SO（跳过 {soSkipped}），" +
                                 $"创建 {convCreated} 个对话（跳过 {convSkipped}）";
                log.AppendLine($"\n✓ 完成！{summary}");
                EditorUtility.DisplayDialog("导入完成", summary, "确定");
            }
            catch (Exception e)
            {
                log.AppendLine($"\n✗ 错误: {e.Message}\n{e.StackTrace}");
            }

            lastLog = log.ToString();
            Debug.Log("[InteractionEventImporter]\n" + lastLog);
        }

        // ══════════════════════════════════════
        // SO 创建
        // ══════════════════════════════════════
        private void CreateSO(string path, string eventId,
            bool useFilterCategory, ProductCategory productCategory,
            bool useFilterShelf, ShelfArchetype[] requiredShelves)
        {
            var so = ScriptableObject.CreateInstance<InteractionEventData>();
            so.eventId = eventId;
            AssetDatabase.CreateAsset(so, path);

            var serialized = new SerializedObject(so);

            serialized.FindProperty("useFilterCategory").boolValue = useFilterCategory;
            if (useFilterCategory)
                serialized.FindProperty("requiredCategory").enumValueIndex = (int)productCategory;

            serialized.FindProperty("useFilterShelf").boolValue = useFilterShelf;
            var shelvesProp = serialized.FindProperty("requiredShelves");
            if (requiredShelves != null && requiredShelves.Length > 0)
            {
                shelvesProp.arraySize = requiredShelves.Length;
                for (int i = 0; i < requiredShelves.Length; i++)
                    shelvesProp.GetArrayElementAtIndex(i).objectReferenceValue = requiredShelves[i];
            }
            else
            {
                shelvesProp.arraySize = 0;
            }

            serialized.FindProperty("conversationTitle").stringValue =
                conversationPrefix + eventId;

            // 奖励：统一 20 fame
            var rewardsProp = serialized.FindProperty("rewards");
            rewardsProp.arraySize = 1;
            var reward = rewardsProp.GetArrayElementAtIndex(0);
            reward.FindPropertyRelative("type").enumValueIndex = (int)RewardType.Fame;
            reward.FindPropertyRelative("amount").intValue = DefaultFameReward;
            reward.FindPropertyRelative("itemId").stringValue = "";

            serialized.FindProperty("bubbleTimeout").floatValue = DefaultBubbleTimeout;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(so);
        }

        // ══════════════════════════════════════
        // 对话创建
        // ══════════════════════════════════════
        private void CreateConversation(Template template, string title, int convId,
            int customerActorId, int playerActorId,
            string question, string answer1, string answer2, string answer3,
            int correctIndex, string correctResp, string incorrectResp)
        {
            var conv = template.CreateConversation(convId, title);
            conv.ActorID = customerActorId;
            conv.ConversantID = playerActorId;

            // Entry 0: START (根节点)
            var e0 = template.CreateDialogueEntry(0, convId, "START");
            e0.ActorID = customerActorId;
            e0.ConversantID = playerActorId;
            e0.Sequence = "None()";
            e0.canvasRect = new Rect(50, 50, 160, 30);
            e0.outgoingLinks.Add(new Link(convId, 0, convId, 1));

            // Entry 1: NPC 提问
            var e1 = template.CreateDialogueEntry(1, convId, "Question");
            e1.ActorID = customerActorId;
            e1.ConversantID = playerActorId;
            e1.DialogueText = question;
            e1.canvasRect = new Rect(270, 50, 160, 30);

            // Entry 5: 正确回复
            var e5 = template.CreateDialogueEntry(5, convId, "CorrectResponse");
            e5.ActorID = customerActorId;
            e5.ConversantID = playerActorId;
            e5.DialogueText = correctResp;
            e5.canvasRect = new Rect(710, 20, 160, 30);

            // Entry 6: 错误回复
            var e6 = template.CreateDialogueEntry(6, convId, "IncorrectResponse");
            e6.ActorID = customerActorId;
            e6.ConversantID = playerActorId;
            e6.DialogueText = incorrectResp;
            e6.canvasRect = new Rect(710, 100, 160, 30);

            // Entry 2-4: 玩家选项
            string[] answers = { answer1, answer2, answer3 };
            for (int i = 0; i < 3; i++)
            {
                int entryId = 2 + i;
                bool isCorrect = (correctIndex == i + 1);
                var entry = template.CreateDialogueEntry(entryId, convId, $"Answer{i + 1}");
                entry.ActorID = playerActorId;
                entry.ConversantID = customerActorId;
                entry.MenuText = answers[i];
                entry.DialogueText = answers[i];
                entry.Sequence = "None()";
                entry.canvasRect = new Rect(490, 10 + i * 50, 160, 30);

                if (isCorrect)
                    entry.userScript = "Variable[\"InteractionCorrect\"] = true";

                // 链接到正确/错误回复
                int targetEntry = isCorrect ? 5 : 6;
                entry.outgoingLinks.Add(new Link(convId, entryId, convId, targetEntry));

                // NPC提问 → 玩家选项
                e1.outgoingLinks.Add(new Link(convId, 1, convId, entryId));

                conv.dialogueEntries.Add(entry);
            }

            conv.dialogueEntries.Insert(0, e0);
            conv.dialogueEntries.Insert(1, e1);
            conv.dialogueEntries.Add(e5);
            conv.dialogueEntries.Add(e6);

            dialogueDatabase.conversations.Add(conv);
        }

        // ══════════════════════════════════════
        // Actor 管理
        // ══════════════════════════════════════
        private int EnsureActor(Template template, string actorName, bool isPlayer)
        {
            var actor = dialogueDatabase.actors.Find(a =>
                string.Equals(a.Name, actorName, StringComparison.OrdinalIgnoreCase));
            if (actor != null) return actor.id;

            int newId = template.GetNextActorID(dialogueDatabase);
            var newActor = template.CreateActor(newId, actorName, isPlayer);
            dialogueDatabase.actors.Add(newActor);
            Debug.Log($"[InteractionEventImporter] 创建 Actor: {actorName} (id={newId})");
            return newId;
        }

        // ══════════════════════════════════════
        // 货架查找
        // ══════════════════════════════════════
        private static ShelfArchetype FindShelfByName(string csvName, ShelfArchetype[] allShelves)
        {
            // 精确匹配
            foreach (var s in allShelves)
            {
                if (string.Equals(s.name, csvName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(s.displayName, csvName, StringComparison.OrdinalIgnoreCase))
                    return s;
            }
            // 子串 fallback：SO名 包含在 CSV名中
            foreach (var s in allShelves)
            {
                if (csvName.IndexOf(s.name, StringComparison.OrdinalIgnoreCase) >= 0
                    || csvName.IndexOf(s.displayName, StringComparison.OrdinalIgnoreCase) >= 0)
                    return s;
            }
            return null;
        }

        // ══════════════════════════════════════
        // CSV 解析（复用 BuildingListImporter 模式）
        // ══════════════════════════════════════
        private static List<List<string>> ParseCSV(string text)
        {
            var rows = new List<List<string>>();
            var lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                rows.Add(SplitCSVLine(line));
            }
            return rows;
        }

        private static List<string> SplitCSVLine(string line)
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

        private static string GetCell(List<string> row, int index)
        {
            return (index >= 0 && index < row.Count) ? row[index] : "";
        }

        private static int ParseCorrectIndex(string s)
        {
            // "Ans1" → 1, "Ans2" → 2, "Ans3" → 3
            if (string.IsNullOrEmpty(s)) return -1;
            s = s.Trim();
            if (s.StartsWith("Ans", StringComparison.OrdinalIgnoreCase) && s.Length > 3)
            {
                if (int.TryParse(s.Substring(3), out int idx))
                    return idx;
            }
            return -1;
        }

        // ══════════════════════════════════════
        // 工具方法
        // ══════════════════════════════════════
        private static void EnsureFolderExists(string path)
        {
            var parts = path.Replace("\\", "/").Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
