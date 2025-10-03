using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using PopLife.Customers.Runtime;
using PopLife.Customers.Data;

namespace PopLife.Customers.Editor
{
    public class CustomerRecordsEditor : EditorWindow
    {
        private List<CustomerRecord> records = new List<CustomerRecord>();
        private Vector2 scrollPos;
        private string searchFilter = "";
        private int selectedIndex = -1;
        private bool showDetails = false;

        // 默认保存路径
        private static string SaveFolderPath => Path.Combine(Application.dataPath, "Documents", "Save");
        private static string DefaultFilePath => Path.Combine(SaveFolderPath, "Customers.json");

        private CustomerRecord editingRecord;
        private SerializedObject serializedRecord;

        private string[] availableArchetypes = new string[0];
        private string[] availableTraits = new string[0];

        private enum SortBy { None, ID, Name, Trust, Loyalty, Visits }
        private SortBy sortBy = SortBy.None;
        private bool sortAscending = true;

        private bool foldoutAppearance = true;
        private bool foldoutStats = true;
        private bool foldoutInterests = true;

        [MenuItem("PopLife/Customer Records Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<CustomerRecordsEditor>("Customer Records");
            window.minSize = new Vector2(800, 600);
            window.LoadArchetypesAndTraits();
        }

        private void OnEnable()
        {
            LoadArchetypesAndTraits();
            LoadFromJson();
        }

        private void LoadArchetypesAndTraits()
        {
            var archetypeGuids = AssetDatabase.FindAssets("t:CustomerArchetype");
            availableArchetypes = new string[archetypeGuids.Length + 1];
            availableArchetypes[0] = "None";
            for (int i = 0; i < archetypeGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(archetypeGuids[i]);
                var archetype = AssetDatabase.LoadAssetAtPath<CustomerArchetype>(path);
                if (archetype != null)
                    availableArchetypes[i + 1] = archetype.name;
            }

            var traitGuids = AssetDatabase.FindAssets("t:Trait");
            availableTraits = new string[traitGuids.Length];
            for (int i = 0; i < traitGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(traitGuids[i]);
                var trait = AssetDatabase.LoadAssetAtPath<Trait>(path);
                if (trait != null)
                    availableTraits[i] = trait.name;
            }
        }

        private void OnGUI()
        {
            DrawToolbar();

            EditorGUILayout.BeginHorizontal();

            DrawRecordsList();

            if (showDetails && selectedIndex >= 0 && selectedIndex < records.Count)
            {
                DrawRecordDetails();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                CreateNewRecord();
            }

            if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                DuplicateSelectedRecord();
            }

            if (GUILayout.Button("Delete", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                DeleteSelectedRecord();
            }

            GUILayout.Space(20);

            if (GUILayout.Button("Import JSON", EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                ImportFromJson();
            }

            if (GUILayout.Button("Export JSON", EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                ExportToJson();
            }

            if (GUILayout.Button("Import CSV", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                ImportFromCsv();
            }

            if (GUILayout.Button("Export CSV", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                ExportToCsv();
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(150));

            EditorGUILayout.LabelField("Sort:", GUILayout.Width(35));
            sortBy = (SortBy)EditorGUILayout.EnumPopup(sortBy, EditorStyles.toolbarPopup, GUILayout.Width(80));

            if (GUILayout.Button(sortAscending ? "▲" : "▼", EditorStyles.toolbarButton, GUILayout.Width(25)))
            {
                sortAscending = !sortAscending;
                SortRecords();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawRecordsList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(300));

            EditorGUILayout.LabelField($"Records: {records.Count}", EditorStyles.boldLabel);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Width(300));

            var filteredRecords = GetFilteredRecords();

            for (int i = 0; i < filteredRecords.Count; i++)
            {
                var record = filteredRecords[i];
                var originalIndex = records.IndexOf(record);

                var selected = originalIndex == selectedIndex;
                var style = selected ? EditorStyles.selectionRect : EditorStyles.helpBox;

                EditorGUILayout.BeginHorizontal(style);

                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField($"[{record.customerId}] {record.name}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Trust: {record.trust} | Loyalty: {record.loyaltyLevel} | Visits: {record.visitCount}");
                EditorGUILayout.EndVertical();

                if (GUILayout.Button("Edit", GUILayout.Width(40)))
                {
                    selectedIndex = originalIndex;
                    showDetails = true;
                    editingRecord = records[selectedIndex];
                    GUI.FocusControl(null);
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawRecordDetails()
        {
            EditorGUILayout.BeginVertical();

            EditorGUILayout.LabelField("Customer Details", EditorStyles.boldLabel);

            var detailScroll = EditorGUILayout.BeginScrollView(Vector2.zero);

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            editingRecord.customerId = EditorGUILayout.TextField("Customer ID", editingRecord.customerId);
            editingRecord.name = EditorGUILayout.TextField("Name", editingRecord.name);
            EditorGUILayout.LabelField("Bio");
            editingRecord.bio = EditorGUILayout.TextArea(editingRecord.bio, GUILayout.Height(60));

            EditorGUILayout.Space(10);
            foldoutAppearance = EditorGUILayout.Foldout(foldoutAppearance, "Appearance", true);
            if (foldoutAppearance)
            {
                EditorGUI.indentLevel++;
                editingRecord.appearance.presetId = EditorGUILayout.TextField("Preset ID", editingRecord.appearance.presetId);
                editingRecord.appearance.hairId = EditorGUILayout.TextField("Hair ID", editingRecord.appearance.hairId);
                editingRecord.appearance.eyesId = EditorGUILayout.TextField("Eyes ID", editingRecord.appearance.eyesId);
                editingRecord.appearance.outfitId = EditorGUILayout.TextField("Outfit ID", editingRecord.appearance.outfitId);
                editingRecord.appearance.accessoryId = EditorGUILayout.TextField("Accessory ID", editingRecord.appearance.accessoryId);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Behavior", EditorStyles.boldLabel);

            var archetypeIndex = Array.IndexOf(availableArchetypes, editingRecord.archetypeId);
            if (archetypeIndex < 0) archetypeIndex = 0;
            archetypeIndex = EditorGUILayout.Popup("Archetype", archetypeIndex, availableArchetypes);
            editingRecord.archetypeId = archetypeIndex > 0 ? availableArchetypes[archetypeIndex] : "";

            EditorGUILayout.LabelField("Traits");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (editingRecord.traitIds == null) editingRecord.traitIds = new string[0];

            for (int i = 0; i < editingRecord.traitIds.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                var traitIndex = Array.IndexOf(availableTraits, editingRecord.traitIds[i]);
                if (traitIndex < 0) traitIndex = 0;
                traitIndex = EditorGUILayout.Popup(traitIndex, availableTraits);
                if (traitIndex >= 0 && traitIndex < availableTraits.Length)
                    editingRecord.traitIds[i] = availableTraits[traitIndex];

                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    var list = editingRecord.traitIds.ToList();
                    list.RemoveAt(i);
                    editingRecord.traitIds = list.ToArray();
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add Trait"))
            {
                var list = editingRecord.traitIds.ToList();
                list.Add("");
                editingRecord.traitIds = list.ToArray();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            foldoutInterests = EditorGUILayout.Foldout(foldoutInterests, "Interest Deltas", true);
            if (foldoutInterests)
            {
                EditorGUI.indentLevel++;

                if (editingRecord.interestPersonalDelta == null || editingRecord.interestPersonalDelta.Length != 6)
                    editingRecord.EnsureInterestSize(6);

                string[] categories = { "Lingerie", "Condom", "Vibrator", "Fleshlight", "Lubricant", "BDSM" };
                for (int i = 0; i < 6; i++)
                {
                    editingRecord.interestPersonalDelta[i] = EditorGUILayout.FloatField(
                        categories[i],
                        editingRecord.interestPersonalDelta[i]
                    );
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);
            foldoutStats = EditorGUILayout.Foldout(foldoutStats, "Stats & Progress", true);
            if (foldoutStats)
            {
                EditorGUI.indentLevel++;
                editingRecord.trust = EditorGUILayout.IntField("Trust", editingRecord.trust);
                editingRecord.loyaltyLevel = EditorGUILayout.IntField("Loyalty Level", editingRecord.loyaltyLevel);
                editingRecord.xp = EditorGUILayout.IntField("XP", editingRecord.xp);
                editingRecord.walletCapBase = EditorGUILayout.IntField("Wallet Cap Base", editingRecord.walletCapBase);

                EditorGUILayout.Space(5);
                editingRecord.visitCount = EditorGUILayout.IntField("Visit Count", editingRecord.visitCount);
                editingRecord.lifetimeSpent = EditorGUILayout.IntField("Lifetime Spent", editingRecord.lifetimeSpent);
                editingRecord.lastVisitDay = EditorGUILayout.TextField("Last Visit Day", editingRecord.lastVisitDay);
                editingRecord.lastLeaveReason = EditorGUILayout.TextField("Last Leave Reason", editingRecord.lastLeaveReason);
                EditorGUI.indentLevel--;
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(this);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private List<CustomerRecord> GetFilteredRecords()
        {
            var filtered = records;

            if (!string.IsNullOrEmpty(searchFilter))
            {
                var filter = searchFilter.ToLower();
                filtered = filtered.Where(r =>
                    r.customerId.ToLower().Contains(filter) ||
                    r.name.ToLower().Contains(filter) ||
                    (r.bio != null && r.bio.ToLower().Contains(filter))
                ).ToList();
            }

            if (sortBy != SortBy.None)
            {
                SortRecords();
            }

            return filtered;
        }

        private void SortRecords()
        {
            switch (sortBy)
            {
                case SortBy.ID:
                    records = sortAscending ?
                        records.OrderBy(r => r.customerId).ToList() :
                        records.OrderByDescending(r => r.customerId).ToList();
                    break;
                case SortBy.Name:
                    records = sortAscending ?
                        records.OrderBy(r => r.name).ToList() :
                        records.OrderByDescending(r => r.name).ToList();
                    break;
                case SortBy.Trust:
                    records = sortAscending ?
                        records.OrderBy(r => r.trust).ToList() :
                        records.OrderByDescending(r => r.trust).ToList();
                    break;
                case SortBy.Loyalty:
                    records = sortAscending ?
                        records.OrderBy(r => r.loyaltyLevel).ToList() :
                        records.OrderByDescending(r => r.loyaltyLevel).ToList();
                    break;
                case SortBy.Visits:
                    records = sortAscending ?
                        records.OrderBy(r => r.visitCount).ToList() :
                        records.OrderByDescending(r => r.visitCount).ToList();
                    break;
            }
        }

        private void CreateNewRecord()
        {
            var newRecord = new CustomerRecord
            {
                customerId = GenerateNewId(),
                name = "New Customer",
                bio = "",
                appearance = new AppearanceParts(),
                archetypeId = "",
                traitIds = new string[0],
                interestPersonalDelta = new float[6],
                trust = 0,
                loyaltyLevel = 0,
                xp = 0,
                walletCapBase = 100,
                visitCount = 0,
                lifetimeSpent = 0,
                lastVisitDay = "",
                lastLeaveReason = "",
                schemaVersion = 1
            };

            records.Add(newRecord);
            selectedIndex = records.Count - 1;
            editingRecord = newRecord;
            showDetails = true;
        }

        private string GenerateNewId()
        {
            int maxNum = 0;
            foreach (var record in records)
            {
                if (record.customerId.StartsWith("C"))
                {
                    if (int.TryParse(record.customerId.Substring(1), out int num))
                    {
                        maxNum = Mathf.Max(maxNum, num);
                    }
                }
            }
            return $"C{(maxNum + 1):D3}";
        }

        private void DuplicateSelectedRecord()
        {
            if (selectedIndex >= 0 && selectedIndex < records.Count)
            {
                var original = records[selectedIndex];
                var duplicate = JsonUtility.FromJson<CustomerRecord>(JsonUtility.ToJson(original));
                duplicate.customerId = GenerateNewId();
                duplicate.name = original.name + " (Copy)";

                records.Add(duplicate);
                selectedIndex = records.Count - 1;
                editingRecord = duplicate;
                showDetails = true;
            }
        }

        private void DeleteSelectedRecord()
        {
            if (selectedIndex >= 0 && selectedIndex < records.Count)
            {
                if (EditorUtility.DisplayDialog("Delete Record",
                    $"Are you sure you want to delete {records[selectedIndex].name}?",
                    "Delete", "Cancel"))
                {
                    records.RemoveAt(selectedIndex);
                    selectedIndex = -1;
                    showDetails = false;
                    editingRecord = null;
                }
            }
        }

        private void LoadFromJson()
        {
            // 确保文件夹存在
            if (!Directory.Exists(SaveFolderPath))
            {
                Directory.CreateDirectory(SaveFolderPath);
                Debug.Log($"Created save directory: {SaveFolderPath}");
            }

            if (File.Exists(DefaultFilePath))
            {
                try
                {
                    string json = File.ReadAllText(DefaultFilePath);

                    // 兼容 CustomerRepository 的格式 (使用 items 字段)
                    if (json.Contains("\"items\""))
                    {
                        var repoWrapper = JsonUtility.FromJson<CustomerRecordList>(json);
                        if (repoWrapper != null && repoWrapper.items != null)
                        {
                            records = repoWrapper.items;
                        }
                    }
                    else
                    {
                        var wrapper = JsonUtility.FromJson<CustomerRecordWrapper>(json);
                        if (wrapper != null && wrapper.customers != null)
                        {
                            records = wrapper.customers.ToList();
                        }
                    }

                    Debug.Log($"Loaded {records.Count} records from {DefaultFilePath}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to load Customers.json: {e.Message}");
                }
            }
            else
            {
                Debug.Log($"Customers.json not found at: {DefaultFilePath}");
            }
        }

        private void ImportFromJson()
        {
            string path = EditorUtility.OpenFilePanel("Import Customer Records", SaveFolderPath, "json");
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    string json = File.ReadAllText(path);

                    // 兼容两种格式
                    if (json.Contains("\"items\""))
                    {
                        var repoWrapper = JsonUtility.FromJson<CustomerRecordList>(json);
                        if (repoWrapper != null && repoWrapper.items != null)
                        {
                            records = repoWrapper.items;
                        }
                    }
                    else
                    {
                        var wrapper = JsonUtility.FromJson<CustomerRecordWrapper>(json);
                        if (wrapper != null && wrapper.customers != null)
                        {
                            records = wrapper.customers.ToList();
                        }
                    }

                    selectedIndex = -1;
                    showDetails = false;
                    EditorUtility.DisplayDialog("Import Successful",
                        $"Imported {records.Count} customer records.", "OK");
                }
                catch (Exception e)
                {
                    EditorUtility.DisplayDialog("Import Failed",
                        $"Failed to import JSON: {e.Message}", "OK");
                }
            }
        }

        private void ExportToJson()
        {
            // 确保文件夹存在
            if (!Directory.Exists(SaveFolderPath))
            {
                Directory.CreateDirectory(SaveFolderPath);
                Debug.Log($"Created save directory: {SaveFolderPath}");
            }

            string path = EditorUtility.SaveFilePanel("Export Customer Records",
                SaveFolderPath, "Customers", "json");

            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    // 使用 CustomerRepository 兼容的格式 (items 字段)
                    var list = new CustomerRecordList { items = records };
                    string json = JsonUtility.ToJson(list, true);
                    File.WriteAllText(path, json);

                    EditorUtility.DisplayDialog("Export Successful",
                        $"Exported {records.Count} customer records to:\n{path}", "OK");

                    // 如果导出到默认位置，提示用户
                    if (path == DefaultFilePath)
                    {
                        Debug.Log($"Exported to default location: {DefaultFilePath}. This file will be used by CustomerRepository at runtime.");
                    }
                }
                catch (Exception e)
                {
                    EditorUtility.DisplayDialog("Export Failed",
                        $"Failed to export JSON: {e.Message}", "OK");
                }
            }
        }

        private void ImportFromCsv()
        {
            string path = EditorUtility.OpenFilePanel("Import Customer Records (CSV)", SaveFolderPath, "csv");
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    var lines = File.ReadAllLines(path);
                    if (lines.Length <= 1)
                    {
                        EditorUtility.DisplayDialog("Import Failed", "CSV file is empty or contains only headers.", "OK");
                        return;
                    }

                    records.Clear();

                    for (int i = 1; i < lines.Length; i++)
                    {
                        var fields = ParseCsvLine(lines[i]);
                        if (fields.Length < 10) continue;

                        var record = new CustomerRecord
                        {
                            customerId = fields[0],
                            name = fields[1],
                            bio = fields[2],
                            archetypeId = fields[3],
                            traitIds = string.IsNullOrEmpty(fields[4]) ?
                                new string[0] : fields[4].Split(';'),
                            trust = int.TryParse(fields[5], out int trust) ? trust : 0,
                            loyaltyLevel = int.TryParse(fields[6], out int loyalty) ? loyalty : 0,
                            xp = int.TryParse(fields[7], out int xp) ? xp : 0,
                            visitCount = int.TryParse(fields[8], out int visits) ? visits : 0,
                            lifetimeSpent = int.TryParse(fields[9], out int spent) ? spent : 0,
                            walletCapBase = 100,
                            appearance = new AppearanceParts(),
                            interestPersonalDelta = new float[6],
                            schemaVersion = 1
                        };

                        if (fields.Length > 10) record.lastVisitDay = fields[10];
                        if (fields.Length > 11) record.lastLeaveReason = fields[11];
                        if (fields.Length > 12)
                        {
                            int walletCap;
                            if (int.TryParse(fields[12], out walletCap))
                                record.walletCapBase = walletCap;
                        }

                        if (fields.Length > 17)
                        {
                            // 兼容旧版本的5个类别和新版本的6个类别
                            int maxCategories = Math.Min(6, fields.Length - 13);
                            for (int j = 0; j < maxCategories; j++)
                            {
                                if (float.TryParse(fields[13 + j], out float delta))
                                    record.interestPersonalDelta[j] = delta;
                            }
                        }

                        records.Add(record);
                    }

                    selectedIndex = -1;
                    showDetails = false;
                    EditorUtility.DisplayDialog("Import Successful",
                        $"Imported {records.Count} customer records from CSV.", "OK");
                }
                catch (Exception e)
                {
                    EditorUtility.DisplayDialog("Import Failed",
                        $"Failed to import CSV: {e.Message}", "OK");
                }
            }
        }

        private void ExportToCsv()
        {
            string path = EditorUtility.SaveFilePanel("Export Customer Records (CSV)",
                SaveFolderPath, "Customers", "csv");

            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    var sb = new StringBuilder();

                    sb.AppendLine("CustomerID,Name,Bio,ArchetypeID,TraitIDs,Trust,LoyaltyLevel,XP,VisitCount,LifetimeSpent,LastVisitDay,LastLeaveReason,WalletCapBase,InterestDelta_Lingerie,InterestDelta_Condom,InterestDelta_Vibrator,InterestDelta_Fleshlight,InterestDelta_Lubricant,InterestDelta_BDSM");

                    foreach (var record in records)
                    {
                        record.EnsureInterestSize(6);

                        sb.AppendLine(string.Format(
                            "\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",{5},{6},{7},{8},{9},\"{10}\",\"{11}\",{12},{13},{14},{15},{16},{17},{18}",
                            EscapeCsvField(record.customerId),
                            EscapeCsvField(record.name),
                            EscapeCsvField(record.bio ?? ""),
                            EscapeCsvField(record.archetypeId ?? ""),
                            EscapeCsvField(string.Join(";", record.traitIds ?? new string[0])),
                            record.trust,
                            record.loyaltyLevel,
                            record.xp,
                            record.visitCount,
                            record.lifetimeSpent,
                            EscapeCsvField(record.lastVisitDay ?? ""),
                            EscapeCsvField(record.lastLeaveReason ?? ""),
                            record.walletCapBase,
                            record.interestPersonalDelta[0],
                            record.interestPersonalDelta[1],
                            record.interestPersonalDelta[2],
                            record.interestPersonalDelta[3],
                            record.interestPersonalDelta[4],
                            record.interestPersonalDelta[5]
                        ));
                    }

                    File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

                    EditorUtility.DisplayDialog("Export Successful",
                        $"Exported {records.Count} customer records to CSV:\n{path}", "OK");
                }
                catch (Exception e)
                {
                    EditorUtility.DisplayDialog("Export Failed",
                        $"Failed to export CSV: {e.Message}", "OK");
                }
            }
        }

        private string EscapeCsvField(string field)
        {
            if (field == null) return "";
            return field.Replace("\"", "\"\"");
        }

        private string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var inQuotes = false;
            var currentField = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(currentField.ToString());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }

            fields.Add(currentField.ToString());
            return fields.ToArray();
        }

        [Serializable]
        private class CustomerRecordWrapper
        {
            public CustomerRecord[] customers;
        }

        [Serializable]
        private class CustomerRecordList
        {
            public List<CustomerRecord> items = new List<CustomerRecord>();
        }
    }
}