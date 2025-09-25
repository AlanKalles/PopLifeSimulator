// Assets/Editor/BuildingListImporter.cs
// Fixed column-letter mapping for levels; start row selectable; dup-id replace/skip/cancel.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PopLife.Data;
using UnityEditor;
using UnityEngine;

namespace Data.Editor
{
    public class BuildingListImporter : EditorWindow
    {
        [Header("Input")]
        public TextAsset textAsset;
        public string externalFilePath;
        public string sheetName = "BuildingList";
        [Tooltip("1-based row index to start importing (default 3).")]
        public int startRowIndex = 3;

        [Header("Output")]
        public string outputDir = "Assets/ScriptableObjects/Archetypes/Shelves";

        [MenuItem("PopLife/Import/BuildingList → Shelf SOs")]
        private static void Open() => GetWindow<BuildingListImporter>("Shelf Importer");

        void OnGUI()
        {
            GUILayout.Label("Input", EditorStyles.boldLabel);
            textAsset = (TextAsset)EditorGUILayout.ObjectField("TSV/CSV (Project)", textAsset, typeof(TextAsset), false);

            EditorGUILayout.BeginHorizontal();
            externalFilePath = EditorGUILayout.TextField("TSV/CSV (External)", externalFilePath);
            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                var p = EditorUtility.OpenFilePanel("Select TSV/CSV", "", "tsv,csv,txt");
                if (!string.IsNullOrEmpty(p)) externalFilePath = p;
            }
            EditorGUILayout.EndHorizontal();

            sheetName = EditorGUILayout.TextField("Sheet Name (note)", sheetName);
            startRowIndex = Mathf.Max(1, EditorGUILayout.IntField("Start Row (1-based)", startRowIndex));

            GUILayout.Space(8);
            GUILayout.Label("Output", EditorStyles.boldLabel);
            outputDir = EditorGUILayout.TextField("Output Folder", outputDir);

            GUILayout.Space(10);
            if (GUILayout.Button("Import / Update", GUILayout.Height(32))) Import();
        }

        void Import()
        {
            try
            {
                string raw = null;
                if (textAsset != null) raw = textAsset.text;
                else if (!string.IsNullOrEmpty(externalFilePath)) raw = File.ReadAllText(externalFilePath, new UTF8Encoding(true));
                if (string.IsNullOrEmpty(raw)) { EditorUtility.DisplayDialog("Error", "No input file.", "OK"); return; }

                var rows = ParseDelimited(raw, out var delim);
                if (rows.Count < 3) { EditorUtility.DisplayDialog("Error", "Need ≥2 header rows + data rows.", "OK"); return; }

                // ----- Basic columns by header (tolerant) -----
                var H1 = rows[0]; var H2 = rows[1];
                int c_id    = FindCol(new[] { "archetype id","archetypeid","id" }, H1, H2);
                int c_name  = FindCol(new[] { "display name","name" }, H1, H2);
                int c_cat   = FindCol(new[] { "category" }, H1, H2);
                int c_build = FindCol(new[] { "build fee","build cost","cost" }, H1, H2);

                // ----- Fixed column-letter mapping for levels -----
                // Helper converts Excel letters to 0-based index.
                int F(string col) => ColToIndex(col);

                // L1: price=F, maintenance=G, stock=H, attractiveness=I, fame=J
                int L1_price = F("F"), L1_maint = F("G"), L1_stock = F("H"), L1_attr = F("I"), L1_fame = F("J");
                // L2: price=L, maintenance=N, stock=P, attractiveness=R, fame=S
                int L2_price = F("L"), L2_maint = F("N"), L2_stock = F("P"), L2_attr = F("R"), L2_fame = F("S");
                // L3: price=U, maintenance=W, stock=Y, attractiveness=AA, fame=AB
                int L3_price = F("U"), L3_maint = F("W"), L3_stock = F("Y"), L3_attr = F("AA"), L3_fame = F("AB");
                // L4: price=AD, maintenance=AF, stock=AH, attractiveness=AJ, fame=AK
                int L4_price = F("AD"), L4_maint = F("AF"), L4_stock = F("AH"), L4_attr = F("AJ"), L4_fame = F("AK");

                // existing assets by archetypeId
                Directory.CreateDirectory(outputDir);
                var existing = LoadExistingById();

                int created=0, updated=0, skipped=0;
                int firstDataRow = Mathf.Clamp(startRowIndex - 1, 2, rows.Count - 1);

                for (int r = firstDataRow; r < rows.Count; r++)
                {
                    var row = rows[r];
                    string archeId = Get(row, c_id).Trim();
                    string name    = Get(row, c_name).Trim();
                    if (string.IsNullOrEmpty(archeId) && string.IsNullOrEmpty(name)) continue;

                    // basics
                    var catStr = Get(row, c_cat);
                    if (!Enum.TryParse<ProductCategory>(catStr, true, out var category)) category = GuessCategory(catStr);
                    int buildCost = ParseInt(Get(row, c_build));

                    // levels
                    var levels = new List<ShelfArchetype.ShelfLevelData>();

                    void AddLevel(int lvl, int ci_price, int ci_maint, int ci_stock, int ci_attr, int ci_fame)
                    {
                        var L = new ShelfArchetype.ShelfLevelData
                        {
                            level           = lvl,
                            price           = ParseInt(Get(row, ci_price)),
                            maintenanceFee  = ParseInt(Get(row, ci_maint)),
                            maxStock        = ParseInt(Get(row, ci_stock)),
                            attractiveness  = ParseFloat(Get(row, ci_attr)),
                            upgradeFameCost = ParseInt(Get(row, ci_fame)),
                        };
                        if (!IsAllZero(L)) levels.Add(L);
                    }

                    AddLevel(1, L1_price, L1_maint, L1_stock, L1_attr, L1_fame);
                    AddLevel(2, L2_price, L2_maint, L2_stock, L2_attr, L2_fame);
                    AddLevel(3, L3_price, L3_maint, L3_stock, L3_attr, L3_fame);
                    AddLevel(4, L4_price, L4_maint, L4_stock, L4_attr, L4_fame);

                    // dup-id handling
                    bool needCreate = true;
                    ShelfArchetype target = null;
                    if (!string.IsNullOrEmpty(archeId) && existing.TryGetValue(archeId, out var found))
                    {
                        int choice = EditorUtility.DisplayDialogComplex(
                            "Duplicate archetypeId",
                            $"Found existing archetypeId = {archeId}\nAsset: {found.name}\nReplace with imported row?",
                            "Replace","Skip","Cancel");
                        if (choice == 2) { Debug.Log("Import canceled."); return; }
                        if (choice == 1) { skipped++; continue; }
                        target = found; needCreate = false;
                    }

                    if (needCreate)
                    {
                        string assetName = string.IsNullOrWhiteSpace(name) ? archeId : name;
                        string path = AssetDatabase.GenerateUniqueAssetPath($"{outputDir}/{Sanitize(assetName)}.asset");
                        target = ScriptableObject.CreateInstance<ShelfArchetype>();
                        AssetDatabase.CreateAsset(target, path);
                        created++;
                    }
                    else updated++;

                    Undo.RecordObject(target, "Update ShelfArchetype");
                    target.archetypeId = archeId;
                    target.displayName = string.IsNullOrWhiteSpace(name) ? archeId : name;
                    target.buildCost   = buildCost;
                    target.category    = category;

                    var soLevels = levels.OrderBy(x => x.level).ToArray();
                    var fi = typeof(ShelfArchetype).GetField("shelfLevels",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    fi?.SetValue(target, soLevels);

                    EditorUtility.SetDirty(target);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Done",
                    $"Start row: {startRowIndex}\nCreated: {created}\nUpdated: {updated}\nSkipped: {skipped}",
                    "OK");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Import Error", e.Message, "OK");
            }
        }

        // ---------- helpers ----------

        static string Get(List<string> row, int idx) => (idx>=0 && idx<row.Count)? row[idx] : "";

        static int ParseInt(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            var t=new string(s.Where(ch=>char.IsDigit(ch)||ch=='-'||ch=='.').ToArray());
            if (string.IsNullOrEmpty(t)) return 0;
            if (float.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f))
                return Mathf.RoundToInt(f);
            return 0;
        }
        static float ParseFloat(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0f;
            var t=new string(s.Where(ch=>char.IsDigit(ch)||ch=='-'||ch=='.').ToArray());
            if (string.IsNullOrEmpty(t)) return 0f;
            if (float.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f))
                return f;
            return 0f;
        }

        static bool IsAllZero(ShelfArchetype.ShelfLevelData x)
            => x.price==0 && x.maintenanceFee==0 && x.maxStock==0 && Mathf.Approximately(x.attractiveness,0f) && x.upgradeFameCost==0;

        static string Sanitize(string name)
        {
            foreach(var ch in Path.GetInvalidFileNameChars()) name=name.Replace(ch,'_');
            return name.Trim();
        }

        static int ColToIndex(string col) // "A"→0, "Z"→25, "AA"→26 ...
        {
            col = (col ?? "").Trim().ToUpperInvariant();
            int n = 0;
            foreach (char c in col)
            {
                if (c < 'A' || c > 'Z') continue;
                n = n * 26 + (c - 'A' + 1);
            }
            return n - 1; // 0-based
        }

        // header search only for basics
        static int FindCol(IEnumerable<string> keys, List<string> H1, List<string> H2)
        {
            string N(string s)=>(s??"").Trim().ToLowerInvariant();
            for(int c=0;c<Math.Max(H1.Count,H2.Count);c++)
            {
                string a=c<H1.Count?N(H1[c]):"";
                string b=c<H2.Count?N(H2[c]):"";
                foreach(var k in keys){ var kk=N(k); if(a==kk || b==kk) return c; }
            }
            return -1;
        }

        static ProductCategory GuessCategory(string s)
        {
            s=(s??"").Trim().ToLowerInvariant();
            if (s.StartsWith("ling")) return ProductCategory.Lingerie;
            if (s.StartsWith("cond")) return ProductCategory.Condom;
            if (s.StartsWith("vib"))  return ProductCategory.Vibrator;
            if (s.StartsWith("flesh"))return ProductCategory.Fleshlight;
            if (s.StartsWith("lub"))  return ProductCategory.Lubricant;
            return ProductCategory.Lingerie;
        }

        static List<List<string>> ParseDelimited(string text, out char delimiter)
        {
            delimiter = text.Contains('\t') ? '\t' : ',';
            var lines = text.Replace("\r\n","\n").Replace("\r","\n").Split('\n');
            var result=new List<List<string>>(lines.Length);
            foreach(var ln in lines){ if(string.IsNullOrEmpty(ln)) continue; result.Add(SplitLine(ln, delimiter)); }
            return result;
        }
        static List<string> SplitLine(string line, char delim)
        {
            var list=new List<string>();
            var sb=new StringBuilder();
            bool inQ=false;
            for(int i=0;i<line.Length;i++)
            {
                char c=line[i];
                if(c=='"')
                {
                    if(inQ && i+1<line.Length && line[i+1]=='"'){ sb.Append('"'); i++; }
                    else inQ=!inQ;
                }
                else if(c==delim && !inQ){ list.Add(sb.ToString()); sb.Length=0; }
                else sb.Append(c);
            }
            list.Add(sb.ToString());
            return list;
        }

        static Dictionary<string, ShelfArchetype> LoadExistingById()
        {
            var dict=new Dictionary<string, ShelfArchetype>(StringComparer.OrdinalIgnoreCase);
            var guids=AssetDatabase.FindAssets("t:PopLife.Data.ShelfArchetype");
            foreach(var g in guids)
            {
                var path=AssetDatabase.GUIDToAssetPath(g);
                var so=AssetDatabase.LoadAssetAtPath<ShelfArchetype>(path);
                if(so!=null && !string.IsNullOrWhiteSpace(so.archetypeId)) dict[so.archetypeId]=so;
            }
            return dict;
        }
    }
}
