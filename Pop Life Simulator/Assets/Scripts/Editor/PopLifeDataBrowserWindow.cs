using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using PopLife.Data;

namespace PopLife.Editor
{
    /// <summary>
    /// PopLife Data Browser — unified editor window for viewing and editing all game ScriptableObjects.
    /// Open via menu: PopLife > Data Browser
    /// </summary>
    public class PopLifeDataBrowserWindow : EditorWindow
    {
        // ── Tab definitions ──────────────────────────────────────────────────
        private static readonly string[] TabLabels =
        {
            "Shelves",
            "Quests",
            "Codex",
            "Buffers",
            "Season Events",
            "Interaction Events",
            "News Libraries",
            "Op Guides",
            "Dialogue Actions",
        };

        // Matching C# type name strings for AssetDatabase.FindAssets
        private static readonly string[] TypeNames =
        {
            "ShelfArchetype",
            "QuestDefinition",
            "CodexMasterList",
            "BufferData",
            "SeasonEventData",
            "InteractionEventData",
            "NewsLibrary",
            "OperationGuideData",
            "DialogueAction",
        };

        private const int TabShelves = 0;
        private const int TabInteractionEvents = 5;

        // ── State ─────────────────────────────────────────────────────────────
        private int currentTab = 0;
        private int prevTab = -1;

        private List<ScriptableObject> items = new List<ScriptableObject>();
        private int selectedIndex = -1;

        private Vector2 listScroll;
        private Vector2 propertyScroll;

        private string searchFilter = "";

        private UnityEditor.Editor cachedEditor;

        // Filtered view (indices into items)
        private List<int> filteredIndices = new List<int>();

        // ── Layout constants ──────────────────────────────────────────────────
        private const float ListPanelWidth = 240f;
        private const float SplitterWidth = 4f;
        private static readonly Color SplitterColor = new Color(0.15f, 0.15f, 0.15f);
        private static readonly Color SelectedRowColor = new Color(0.22f, 0.48f, 0.72f);
        private static readonly Color HoverRowColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);
        private static readonly Color ConvHeaderColor = new Color(0.18f, 0.35f, 0.22f);
        private static readonly Color CorrectAnswerColor = new Color(0.2f, 0.55f, 0.25f);

        private int hoveredIndex = -1;

        // ── Blueprint panel ───────────────────────────────────────────────────
        private bool blueprintFoldout = true;
        private static readonly string BlueprintJsonPath =
            Path.Combine(Application.dataPath, "StreamingAssets", "BlueprintProfile.json");
        private static readonly Color BlueprintHeaderColor = new Color(0.20f, 0.28f, 0.42f);
        private static readonly Color UnlockedColor = new Color(0.22f, 0.62f, 0.32f);
        private static readonly Color LockedColor = new Color(0.55f, 0.22f, 0.22f);

        // ── Dialogue Database ─────────────────────────────────────────────────
        private ScriptableObject dialogueDatabase;
        private SerializedObject dbSerializedObject;

        // Foldout state for conversation panel
        private bool convFoldout = true;

        // ── Open ──────────────────────────────────────────────────────────────
        [MenuItem("PopLife/Data Browser")]
        public static void Open()
        {
            var window = GetWindow<PopLifeDataBrowserWindow>("PopLife Data Browser");
            window.minSize = new Vector2(720, 480);
        }

        // ── Unity callbacks ───────────────────────────────────────────────────
        private void OnEnable()
        {
            LoadAssets();
        }

        private void OnFocus()
        {
            LoadAssets();
            // Re-sync db serialized object in case the database was saved externally
            if (dbSerializedObject != null)
                dbSerializedObject.Update();
        }

        private void OnGUI()
        {
            HandleKeyboardNavigation();
            DrawTabBar();
            DrawBody();

            if (currentTab != prevTab)
            {
                prevTab = currentTab;
                selectedIndex = -1;
                searchFilter = "";
                DestroyEditor();
                LoadAssets();
                Repaint();
            }
        }

        // ── Tab bar ───────────────────────────────────────────────────────────
        private void DrawTabBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            for (int i = 0; i < TabLabels.Length; i++)
            {
                bool isActive = (i == currentTab);
                GUIStyle style = isActive
                    ? new GUIStyle(EditorStyles.toolbarButton) { fontStyle = FontStyle.Bold }
                    : EditorStyles.toolbarButton;

                if (GUILayout.Toggle(isActive, TabLabels[i], style))
                {
                    if (!isActive) currentTab = i;
                }
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("⟳ Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                LoadAssets();
                if (dbSerializedObject != null) dbSerializedObject.Update();
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
        }

        // ── Body ──────────────────────────────────────────────────────────────
        private void DrawBody()
        {
            EditorGUILayout.BeginHorizontal();
            DrawListPanel();
            DrawSplitter();
            DrawPropertyPanel();
            EditorGUILayout.EndHorizontal();
        }

        // ── Left panel: list ──────────────────────────────────────────────────
        private void DrawListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(ListPanelWidth), GUILayout.ExpandHeight(true));

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Search", GUILayout.Width(45));
            string newFilter = GUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField);
            if (newFilter != searchFilter)
            {
                searchFilter = newFilter;
                RefreshFilter();
                selectedIndex = -1;
                DestroyEditor();
            }
            if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(20)))
            {
                searchFilter = "";
                RefreshFilter();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"{filteredIndices.Count} / {items.Count} assets", EditorStyles.centeredGreyMiniLabel);

            listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.ExpandHeight(true));

            hoveredIndex = -1;
            for (int fi = 0; fi < filteredIndices.Count; fi++)
            {
                int realIndex = filteredIndices[fi];
                ScriptableObject so = items[realIndex];
                if (so == null) continue;

                bool isSelected = (realIndex == selectedIndex);
                Rect rowRect = EditorGUILayout.GetControlRect(false, 22f);

                if (rowRect.Contains(Event.current.mousePosition))
                    hoveredIndex = fi;

                if (isSelected)
                    EditorGUI.DrawRect(rowRect, SelectedRowColor);
                else if (hoveredIndex == fi)
                    EditorGUI.DrawRect(rowRect, HoverRowColor);

                Texture icon = AssetPreview.GetMiniThumbnail(so);
                Rect iconRect = new Rect(rowRect.x + 4, rowRect.y + 3, 16, 16);
                Rect labelRect = new Rect(rowRect.x + 24, rowRect.y + 3, rowRect.width - 56, 16);

                if (icon != null) GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                GUI.Label(labelRect, so.name, isSelected ? EditorStyles.whiteLabel : EditorStyles.label);

                Rect pingRect = new Rect(rowRect.xMax - 28, rowRect.y + 3, 24, 16);
                if (GUI.Button(pingRect, "→", EditorStyles.miniButton))
                    EditorGUIUtility.PingObject(so);

                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    selectedIndex = realIndex;
                    DestroyEditor();
                    GUI.FocusControl(null);
                    Event.current.Use();
                    Repaint();
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            if (GUILayout.Button($"+ Create New {TabLabels[currentTab]}", GUILayout.Height(26)))
                CreateNewAsset();

            EditorGUILayout.Space(2);
            EditorGUILayout.EndVertical();
        }

        // ── Splitter ──────────────────────────────────────────────────────────
        private void DrawSplitter()
        {
            Rect splitterRect = GUILayoutUtility.GetRect(SplitterWidth, SplitterWidth, GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(splitterRect, SplitterColor);
        }

        // ── Right panel: property editor ──────────────────────────────────────
        private Vector2 propertyScrollRight;   // second scroll for 2-col right column

        private void DrawPropertyPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (selectedIndex < 0 || selectedIndex >= items.Count || items[selectedIndex] == null)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("← Select an asset to edit", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            ScriptableObject so = items[selectedIndex];

            // Header bar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField(so.name, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Select in Project", EditorStyles.toolbarButton, GUILayout.Width(120)))
            {
                Selection.activeObject = so;
                EditorGUIUtility.PingObject(so);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            if (cachedEditor == null || cachedEditor.target != so)
            {
                DestroyEditor();
                UnityEditor.Editor.CreateCachedEditor(so, null, ref cachedEditor);
            }

            // ── Shelves: 2-column layout (inspector left | blueprint right) ──
            if (currentTab == TabShelves)
            {
                EditorGUILayout.BeginHorizontal();

                // Left column — standard inspector
                propertyScroll = EditorGUILayout.BeginScrollView(
                    propertyScroll, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
                if (cachedEditor != null)
                    cachedEditor.OnInspectorGUI();
                EditorGUILayout.EndScrollView();

                // Thin divider between columns
                Rect divRect = GUILayoutUtility.GetRect(SplitterWidth, SplitterWidth, GUILayout.ExpandHeight(true));
                EditorGUI.DrawRect(divRect, SplitterColor);

                // Right column — blueprint panel
                propertyScrollRight = EditorGUILayout.BeginScrollView(
                    propertyScrollRight, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
                DrawShelfBlueprintPanel(so);
                EditorGUILayout.EndScrollView();

                EditorGUILayout.EndHorizontal();
            }
            else
            {
                // All other tabs: single scrollable column
                propertyScroll = EditorGUILayout.BeginScrollView(propertyScroll, GUILayout.ExpandHeight(true));

                if (cachedEditor != null)
                    cachedEditor.OnInspectorGUI();

                // Conversation editor — only for Interaction Events tab
                if (currentTab == TabInteractionEvents)
                {
                    EditorGUILayout.Space(10);
                    DrawConversationPanel(so);
                }

                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();
        }

        // ─────────────────────────────────────────────────────────────────────
        // CONVERSATION PANEL
        // ─────────────────────────────────────────────────────────────────────

        private void DrawConversationPanel(ScriptableObject interactionEventSO)
        {
            // Read the conversationTitle field from the SO
            using var soSerialized = new SerializedObject(interactionEventSO);
            string convTitle = soSerialized.FindProperty("conversationTitle").stringValue;

            // Section header
            Rect headerRect = EditorGUILayout.GetControlRect(false, 22f);
            EditorGUI.DrawRect(headerRect, ConvHeaderColor);
            Rect foldoutRect = new Rect(headerRect.x + 4, headerRect.y + 3, headerRect.width - 8, 16f);
            convFoldout = EditorGUI.Foldout(foldoutRect, convFoldout,
                $"  Conversation Content  —  \"{convTitle}\"", true, EditorStyles.foldout);

            if (!convFoldout) return;

            EditorGUILayout.Space(4);

            if (string.IsNullOrEmpty(convTitle))
            {
                EditorGUILayout.HelpBox("No conversation title set on this asset.", MessageType.Warning);
                return;
            }

            // Lazy-load database
            EnsureDialogueDatabaseLoaded();
            if (dialogueDatabase == null)
            {
                EditorGUILayout.HelpBox("Dialogue Database not found. Expected at: Assets/Data/Dialogue Database.asset", MessageType.Error);
                return;
            }

            dbSerializedObject.Update();

            SerializedProperty conversations = dbSerializedObject.FindProperty("conversations");
            if (conversations == null)
            {
                EditorGUILayout.HelpBox("Could not find 'conversations' property in Dialogue Database.", MessageType.Error);
                return;
            }

            int convIndex = FindConversationIndex(conversations, convTitle);
            if (convIndex == -1)
            {
                EditorGUILayout.HelpBox($"Conversation \"{convTitle}\" not found in Dialogue Database.", MessageType.Warning);
                if (GUILayout.Button("Ping Database Asset", GUILayout.Height(24)))
                    EditorGUIUtility.PingObject(dialogueDatabase);
                return;
            }

            SerializedProperty conv = conversations.GetArrayElementAtIndex(convIndex);
            SerializedProperty entries = conv.FindPropertyRelative("dialogueEntries");

            if (entries == null)
            {
                EditorGUILayout.HelpBox("Could not read dialogue entries.", MessageType.Error);
                return;
            }

            EditorGUI.BeginChangeCheck();

            // ── Question ──
            SerializedProperty questionEntry = FindEntryByTitle(entries, "Question");
            EditorGUILayout.LabelField("NPC Question", EditorStyles.boldLabel);
            if (questionEntry != null)
            {
                string dialogueText = GetFieldValue(questionEntry, "Dialogue Text");
                string newText = EditorGUILayout.TextArea(dialogueText, GUILayout.MinHeight(48));
                if (newText != dialogueText)
                {
                    SetFieldValue(questionEntry, "Dialogue Text", newText);
                    SetFieldValue(questionEntry, "Menu Text", newText);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Entry 'Question' not found.", MessageType.Warning);
            }

            EditorGUILayout.Space(8);

            // ── Answers ──
            EditorGUILayout.LabelField("Player Answers", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Mark exactly one answer as Correct. The correct answer's userScript sets Variable[\"InteractionCorrect\"] = true.", MessageType.Info);
            EditorGUILayout.Space(2);

            string[] answerTitles = { "Answer1", "Answer2", "Answer3" };
            int currentCorrect = FindCorrectAnswerIndex(entries, answerTitles);

            for (int i = 0; i < answerTitles.Length; i++)
            {
                SerializedProperty answerEntry = FindEntryByTitle(entries, answerTitles[i]);
                if (answerEntry == null)
                {
                    EditorGUILayout.HelpBox($"Entry '{answerTitles[i]}' not found.", MessageType.Warning);
                    continue;
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                {
                    EditorGUILayout.BeginHorizontal();
                    {
                        bool isCorrect = (i == currentCorrect);

                        // Correct radio toggle
                        GUI.backgroundColor = isCorrect ? CorrectAnswerColor : Color.white;
                        bool toggleResult = GUILayout.Toggle(isCorrect,
                            isCorrect ? "✓ Correct" : "  Mark Correct",
                            EditorStyles.miniButton, GUILayout.Width(100));
                        GUI.backgroundColor = Color.white;

                        if (toggleResult && !isCorrect)
                        {
                            // Move correct flag from old answer to this one
                            if (currentCorrect >= 0)
                                SetAnswerCorrect(FindEntryByTitle(entries, answerTitles[currentCorrect]), false);
                            SetAnswerCorrect(answerEntry, true);
                            currentCorrect = i;
                        }

                        EditorGUILayout.LabelField($"Answer {i + 1}", EditorStyles.boldLabel);
                    }
                    EditorGUILayout.EndHorizontal();

                    string menuText = GetFieldValue(answerEntry, "Menu Text");
                    string newMenuText = EditorGUILayout.TextArea(menuText, GUILayout.MinHeight(36));
                    if (newMenuText != menuText)
                    {
                        SetFieldValue(answerEntry, "Menu Text", newMenuText);
                        SetFieldValue(answerEntry, "Dialogue Text", newMenuText);
                    }
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.Space(8);

            // ── Correct response ──
            EditorGUILayout.LabelField("NPC Correct Response", EditorStyles.boldLabel);
            SerializedProperty correctResponseEntry = FindEntryByTitle(entries, "CorrectResponse");
            if (correctResponseEntry != null)
            {
                string text = GetFieldValue(correctResponseEntry, "Dialogue Text");
                string newText = EditorGUILayout.TextArea(text, GUILayout.MinHeight(40));
                if (newText != text) SetFieldValue(correctResponseEntry, "Dialogue Text", newText);
            }
            else
            {
                EditorGUILayout.HelpBox("Entry 'CorrectResponse' not found.", MessageType.Warning);
            }

            EditorGUILayout.Space(6);

            // ── Incorrect response ──
            EditorGUILayout.LabelField("NPC Incorrect Response", EditorStyles.boldLabel);
            SerializedProperty incorrectResponseEntry = FindEntryByTitle(entries, "IncorrectResponse");
            if (incorrectResponseEntry != null)
            {
                string text = GetFieldValue(incorrectResponseEntry, "Dialogue Text");
                string newText = EditorGUILayout.TextArea(text, GUILayout.MinHeight(40));
                if (newText != text) SetFieldValue(incorrectResponseEntry, "Dialogue Text", newText);
            }
            else
            {
                EditorGUILayout.HelpBox("Entry 'IncorrectResponse' not found.", MessageType.Warning);
            }

            EditorGUILayout.Space(8);

            // ── Save / Ping ──
            EditorGUILayout.BeginHorizontal();
            {
                if (EditorGUI.EndChangeCheck())
                {
                    dbSerializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(dialogueDatabase);
                }

                if (GUILayout.Button("Save Database", GUILayout.Height(26)))
                {
                    dbSerializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(dialogueDatabase);
                    AssetDatabase.SaveAssets();
                }

                if (GUILayout.Button("Ping Database", GUILayout.Height(26)))
                    EditorGUIUtility.PingObject(dialogueDatabase);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
        }

        // ─────────────────────────────────────────────────────────────────────
        // DATABASE HELPERS
        // ─────────────────────────────────────────────────────────────────────

        private void EnsureDialogueDatabaseLoaded()
        {
            if (dialogueDatabase != null && dbSerializedObject != null) return;

            // Try to find the main dialogue database (prefer "Dialogue Database.asset" over auto-backup)
            string[] guids = AssetDatabase.FindAssets("Dialogue Database t:ScriptableObject");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("Auto-Backup") || path.Contains("/Demo/") || path.Contains("/ThirdParty/"))
                    continue;
                if (path.EndsWith("Dialogue Database.asset"))
                {
                    dialogueDatabase = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                    if (dialogueDatabase != null)
                    {
                        dbSerializedObject = new SerializedObject(dialogueDatabase);
                        return;
                    }
                }
            }

            // Fallback: first non-backup result
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("Auto-Backup") || path.Contains("/Demo/")) continue;
                dialogueDatabase = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (dialogueDatabase != null)
                {
                    dbSerializedObject = new SerializedObject(dialogueDatabase);
                    return;
                }
            }
        }

        /// <summary>Returns the array index of the conversation whose Title field matches convTitle, or -1.</summary>
        private static int FindConversationIndex(SerializedProperty conversations, string convTitle)
        {
            int count = conversations.arraySize;
            for (int i = 0; i < count; i++)
            {
                SerializedProperty conv = conversations.GetArrayElementAtIndex(i);
                string title = GetConversationTitle(conv);
                if (title == convTitle) return i;
            }
            return -1;
        }

        private static string GetConversationTitle(SerializedProperty conv)
        {
            SerializedProperty fields = conv.FindPropertyRelative("fields");
            if (fields == null) return null;
            int count = fields.arraySize;
            for (int i = 0; i < count; i++)
            {
                SerializedProperty field = fields.GetArrayElementAtIndex(i);
                if (field.FindPropertyRelative("title").stringValue == "Title")
                    return field.FindPropertyRelative("value").stringValue;
            }
            return null;
        }

        /// <summary>Finds a dialogue entry by its "Title" field value (e.g. "Question", "Answer1").</summary>
        private static SerializedProperty FindEntryByTitle(SerializedProperty entries, string entryTitle)
        {
            int count = entries.arraySize;
            for (int i = 0; i < count; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if (GetFieldValue(entry, "Title") == entryTitle)
                    return entry;
            }
            return null;
        }

        /// <summary>Gets the "value" of a named field inside a dialogue entry's fields array.</summary>
        private static string GetFieldValue(SerializedProperty entry, string fieldTitle)
        {
            SerializedProperty fields = entry.FindPropertyRelative("fields");
            if (fields == null) return string.Empty;
            int count = fields.arraySize;
            for (int i = 0; i < count; i++)
            {
                SerializedProperty f = fields.GetArrayElementAtIndex(i);
                if (f.FindPropertyRelative("title").stringValue == fieldTitle)
                    return f.FindPropertyRelative("value").stringValue;
            }
            return string.Empty;
        }

        /// <summary>Sets the "value" of a named field inside a dialogue entry's fields array.</summary>
        private static void SetFieldValue(SerializedProperty entry, string fieldTitle, string newValue)
        {
            SerializedProperty fields = entry.FindPropertyRelative("fields");
            if (fields == null) return;
            int count = fields.arraySize;
            for (int i = 0; i < count; i++)
            {
                SerializedProperty f = fields.GetArrayElementAtIndex(i);
                if (f.FindPropertyRelative("title").stringValue == fieldTitle)
                {
                    f.FindPropertyRelative("value").stringValue = newValue;
                    return;
                }
            }
        }

        /// <summary>
        /// Returns which answer index (0-based within answerTitles) currently has
        /// Variable["InteractionCorrect"] = true in its userScript. Returns -1 if none.
        /// </summary>
        private static int FindCorrectAnswerIndex(SerializedProperty entries, string[] answerTitles)
        {
            for (int i = 0; i < answerTitles.Length; i++)
            {
                SerializedProperty entry = FindEntryByTitle(entries, answerTitles[i]);
                if (entry == null) continue;
                string script = entry.FindPropertyRelative("userScript").stringValue;
                if (!string.IsNullOrEmpty(script) && script.Contains("InteractionCorrect"))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Adds or removes the InteractionCorrect Lua variable assignment in an answer entry's userScript.
        /// </summary>
        private static void SetAnswerCorrect(SerializedProperty entry, bool correct)
        {
            if (entry == null) return;
            SerializedProperty userScript = entry.FindPropertyRelative("userScript");
            if (correct)
            {
                userScript.stringValue = "Variable[\"InteractionCorrect\"] = true";
            }
            else
            {
                // Remove the line if it's set
                string current = userScript.stringValue ?? string.Empty;
                if (current.Contains("InteractionCorrect"))
                    userScript.stringValue = string.Empty;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // SHELF BLUEPRINT PANEL
        // ─────────────────────────────────────────────────────────────────────

        private void DrawShelfBlueprintPanel(ScriptableObject so)
        {
            ShelfArchetype shelf = so as ShelfArchetype;
            if (shelf == null) return;

            // Section header
            Rect headerRect = EditorGUILayout.GetControlRect(false, 22f);
            EditorGUI.DrawRect(headerRect, BlueprintHeaderColor);
            Rect foldoutRect = new Rect(headerRect.x + 4, headerRect.y + 3, headerRect.width - 8, 16f);
            blueprintFoldout = EditorGUI.Foldout(foldoutRect, blueprintFoldout,
                $"  Blueprint  —  {shelf.name}", true, EditorStyles.foldout);

            if (!blueprintFoldout) return;

            EditorGUILayout.Space(6);

            // ── Unlock status ──
            BlueprintProfile profile = LoadBlueprintProfile();
            bool isUnlocked = profile != null && profile.HasShelfBlueprint(shelf.name);

            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("Unlock Status", GUILayout.Width(110));

                GUI.backgroundColor = isUnlocked ? UnlockedColor : LockedColor;
                string statusLabel = isUnlocked ? "✓  Unlocked" : "✗  Locked";
                if (GUILayout.Button(statusLabel, GUILayout.Height(22), GUILayout.Width(120)))
                {
                    if (profile != null)
                    {
                        if (isUnlocked)
                            profile.RemoveShelf(shelf.name);
                        else
                            profile.UnlockShelf(shelf.name);
                        SaveBlueprintProfile(profile);
                    }
                }
                GUI.backgroundColor = Color.white;

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Open JSON", EditorStyles.miniButton, GUILayout.Width(70)))
                    UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(BlueprintJsonPath, 1);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // ── Computed stats table ──
            EditorGUILayout.LabelField("Formula-Computed Stats  (buildCost = " + shelf.buildCost + ")", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            // Header row
            DrawStatsTableRow("Level", "Upgrade Fame", "Price", "Maint. Fee", "Max Stock", "Appeal", true);
            DrawStatsTableDivider();

            for (int lvl = 1; lvl <= shelf.MaxLevel; lvl++)
            {
                DrawStatsTableRow(
                    lvl.ToString(),
                    shelf.GetUpgradeFameCost(lvl).ToString(),
                    shelf.GetPrice(lvl).ToString(),
                    shelf.GetMaintenanceFee(lvl).ToString(),
                    shelf.GetStock(lvl).ToString(),
                    shelf.GetAppeal(lvl).ToString("0"),
                    false
                );
            }

            EditorGUILayout.Space(4);
        }

        private static readonly float[] StatsColWidths = { 44f, 100f, 60f, 82f, 76f, 60f };

        private static void DrawStatsTableRow(string level, string fame, string price,
                                              string maint, string stock, string appeal, bool isHeader)
        {
            GUIStyle style = isHeader ? EditorStyles.boldLabel : EditorStyles.label;
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(4);
            GUILayout.Label(level,  style, GUILayout.Width(StatsColWidths[0]));
            GUILayout.Label(fame,   style, GUILayout.Width(StatsColWidths[1]));
            GUILayout.Label(price,  style, GUILayout.Width(StatsColWidths[2]));
            GUILayout.Label(maint,  style, GUILayout.Width(StatsColWidths[3]));
            GUILayout.Label(stock,  style, GUILayout.Width(StatsColWidths[4]));
            GUILayout.Label(appeal, style, GUILayout.Width(StatsColWidths[5]));
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawStatsTableDivider()
        {
            Rect r = EditorGUILayout.GetControlRect(false, 1f);
            r.x += 4;
            r.width -= 8;
            EditorGUI.DrawRect(r, new Color(0.4f, 0.4f, 0.4f));
        }

        // ── BlueprintProfile JSON helpers ──────────────────────────────────────

        private static BlueprintProfile LoadBlueprintProfile()
        {
            if (!File.Exists(BlueprintJsonPath))
            {
                Debug.LogWarning($"[DataBrowser] BlueprintProfile.json not found at: {BlueprintJsonPath}");
                return null;
            }
            try
            {
                string json = File.ReadAllText(BlueprintJsonPath);
                return JsonUtility.FromJson<BlueprintProfile>(json);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DataBrowser] Failed to load BlueprintProfile: {ex.Message}");
                return null;
            }
        }

        private static void SaveBlueprintProfile(BlueprintProfile profile)
        {
            try
            {
                string json = JsonUtility.ToJson(profile, true);
                File.WriteAllText(BlueprintJsonPath, json);
                AssetDatabase.Refresh();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DataBrowser] Failed to save BlueprintProfile: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // KEYBOARD NAVIGATION
        // ─────────────────────────────────────────────────────────────────────

        private void HandleKeyboardNavigation()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown) return;
            if (e.keyCode != KeyCode.UpArrow && e.keyCode != KeyCode.DownArrow) return;
            if (filteredIndices.Count == 0) return;

            // Don't steal keys while a text field has focus
            if (GUIUtility.keyboardControl != 0) return;

            int currentPos = filteredIndices.IndexOf(selectedIndex);

            int newPos;
            if (e.keyCode == KeyCode.UpArrow)
                newPos = currentPos <= 0 ? 0 : currentPos - 1;
            else
                newPos = currentPos >= filteredIndices.Count - 1 ? filteredIndices.Count - 1 : currentPos + 1;

            // If nothing was selected yet, start at the top/bottom
            if (currentPos == -1)
                newPos = e.keyCode == KeyCode.UpArrow ? filteredIndices.Count - 1 : 0;

            if (newPos == currentPos) { e.Use(); return; }

            selectedIndex = filteredIndices[newPos];
            DestroyEditor();
            ScrollListToItem(newPos);
            e.Use();
            Repaint();
        }

        private const float RowHeight = 22f;

        /// <summary>Adjusts listScroll so the item at filteredPos is fully visible.</summary>
        private void ScrollListToItem(int filteredPos)
        {
            // Reserve space for the search bar (~22), count label (~18), create button (~34), padding
            const float listHeaderHeight = 44f;
            const float listFooterHeight = 36f;
            float visibleHeight = position.height - listHeaderHeight - listFooterHeight;

            float itemTop = filteredPos * RowHeight;
            float itemBottom = itemTop + RowHeight;

            if (itemTop < listScroll.y)
                listScroll.y = itemTop;
            else if (itemBottom > listScroll.y + visibleHeight)
                listScroll.y = itemBottom - visibleHeight;
        }

        // ─────────────────────────────────────────────────────────────────────
        // ASSET LOADING
        // ─────────────────────────────────────────────────────────────────────

        private void LoadAssets()
        {
            items.Clear();
            DestroyEditor();

            string typeName = TypeNames[currentTab];
            string[] guids = AssetDatabase.FindAssets($"t:{typeName}");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/ThirdParty/") || path.StartsWith("Packages/")) continue;

                ScriptableObject so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so != null) items.Add(so);
            }

            items.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));

            RefreshFilter();

            if (selectedIndex >= items.Count)
                selectedIndex = -1;
        }

        private void RefreshFilter()
        {
            filteredIndices.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null) continue;
                if (string.IsNullOrEmpty(searchFilter) ||
                    items[i].name.IndexOf(searchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    filteredIndices.Add(i);
                }
            }
        }

        // ── Create new asset ──────────────────────────────────────────────────
        private void CreateNewAsset()
        {
            string defaultFolder = "Assets/Resources/ScriptableObjects";
            if (items.Count > 0)
            {
                string existingPath = AssetDatabase.GetAssetPath(items[0]);
                if (!string.IsNullOrEmpty(existingPath))
                    defaultFolder = System.IO.Path.GetDirectoryName(existingPath);
            }

            string typeName = TypeNames[currentTab];
            System.Type soType = FindType(typeName);
            if (soType == null)
            {
                EditorUtility.DisplayDialog("Error", $"Cannot find type: {typeName}", "OK");
                return;
            }

            string path = EditorUtility.SaveFilePanelInProject(
                $"Create New {TabLabels[currentTab]}",
                $"New{typeName}",
                "asset",
                $"Choose location for new {typeName}",
                defaultFolder
            );

            if (string.IsNullOrEmpty(path)) return;

            ScriptableObject newSO = ScriptableObject.CreateInstance(soType);
            AssetDatabase.CreateAsset(newSO, path);
            AssetDatabase.SaveAssets();

            LoadAssets();

            for (int i = 0; i < items.Count; i++)
            {
                if (AssetDatabase.GetAssetPath(items[i]) == path)
                {
                    selectedIndex = i;
                    break;
                }
            }

            Selection.activeObject = newSO;
            Repaint();
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        private static System.Type FindType(string typeName)
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.Name == typeName) return type;
                }
            }
            return null;
        }

        private void DestroyEditor()
        {
            if (cachedEditor != null)
            {
                DestroyImmediate(cachedEditor);
                cachedEditor = null;
            }
        }

        private void OnDestroy()
        {
            DestroyEditor();
            if (dbSerializedObject != null)
            {
                dbSerializedObject.Dispose();
                dbSerializedObject = null;
            }
        }
    }
}
