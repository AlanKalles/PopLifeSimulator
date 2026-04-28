using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using PopLife.Data;
using PopLife.Manager;
using PopLife.Quest;
using PopLife.UI;

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
            "Quest Chains",
        };

        // Matching C# type name strings for AssetDatabase.FindAssets
        private static readonly string[] TypeNames =
        {
            "ShelfArchetype",
            "QuestDefinition",
            "ShelfArchetype",
            "BufferData",
            "SeasonEventData",
            "InteractionEventData",
            "NewsLibrary",
            "OperationGuideData",
            "DialogueAction",
            "QuestDefinition",
        };

        private const int TabShelves = 0;
        private const int TabCodex = 2;
        private const int TabInteractionEvents = 5;
        private const int TabNewsLibraries = 6;
        private const int TabQuestChains = 9;

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
        private static readonly Color CodexHeaderColor = new Color(0.28f, 0.20f, 0.38f);
        private static readonly Color CorrectAnswerColor = new Color(0.2f, 0.55f, 0.25f);

        private int hoveredIndex = -1;
        private int copiedIndex = -1;

        // ── News Libraries state ───────────────────────────────────────────────
        private SerializedObject newsLibrarySerializedObject;
        private int selectedNewsItemIndex = -1;
        private List<int> filteredNewsIndices = new List<int>();
        private static readonly Color NewsHeaderColor = new Color(0.22f, 0.30f, 0.22f);
        private static readonly Color NewsDeleteColor = new Color(1f, 0.45f, 0.45f);

        // ── Blueprint panel ───────────────────────────────────────────────────
        private bool blueprintFoldout = true;
        private static readonly string BlueprintJsonPath =
            Path.Combine(Application.dataPath, "StreamingAssets", "BlueprintProfile.json");
        private static readonly Color BlueprintHeaderColor = new Color(0.20f, 0.28f, 0.42f);
        private static readonly Color UnlockedColor = new Color(0.22f, 0.62f, 0.32f);
        private static readonly Color LockedColor = new Color(0.55f, 0.22f, 0.22f);

        // ── Quest Chain view ──────────────────────────────────────────────────
        private class ChainDisplayItem
        {
            public bool isHeader;
            public string label;
            public ScriptableObject so;
            public int depth;
            public int triggerValue; // headers: marker value of this group; quest rows: unused
        }

        private List<ChainDisplayItem> chainDisplayList = new List<ChainDisplayItem>();
        private int selectedChainItemIndex = -1;
        private Vector2 chainListScroll;
        private Vector2 chainDetailScroll;

        private int chainDragHoverIndex  = -1;
        private int chainMouseDownIndex  = -1;
        private SerializedObject chainDetailSO;
        private ScriptableObject chainDetailTarget;

        private static string[] s_markerNames;
        private static int[]    s_markerValues;

        private static readonly Color ChainHeaderColor    = new Color(0.15f, 0.22f, 0.38f);
        private static readonly Color ChainTriggerColor   = new Color(0.22f, 0.18f, 0.35f);
        private static readonly Color ChainConditionColor = new Color(0.18f, 0.28f, 0.18f);
        private static readonly Color ChainRewardColor    = new Color(0.30f, 0.24f, 0.10f);
        private static readonly Color ChainTextColor      = new Color(0.18f, 0.18f, 0.28f);
        private static readonly Color[] RewardTypeColors  =
        {
            new Color(0.95f, 0.78f, 0.20f),  // 0 Money
            new Color(0.35f, 0.78f, 0.45f),  // 1 Fame
            new Color(0.40f, 0.65f, 0.95f),  // 2 Blueprint
            new Color(0.80f, 0.50f, 0.85f),  // 3 Customer
        };

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
            if (dbSerializedObject != null)
                dbSerializedObject.Update();
            if (chainDetailSO != null)
                chainDetailSO.Update();
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
                selectedNewsItemIndex = -1;
                selectedChainItemIndex = -1;
                chainDisplayList.Clear();
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
            if (currentTab == TabNewsLibraries)
            {
                DrawNewsListPanel();
                DrawSplitter();
                DrawNewsPropertyPanel();
            }
            else if (currentTab == TabQuestChains)
            {
                DrawQuestChainListPanel();
                DrawSplitter();
                DrawQuestChainDetailPanel();
            }
            else
            {
                DrawListPanel();
                DrawSplitter();
                DrawPropertyPanel();
            }
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

            // Header bar — editable name field renames the asset on commit
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUI.BeginChangeCheck();
            string editedName = EditorGUILayout.DelayedTextField(so.name, EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
            if (EditorGUI.EndChangeCheck() && !string.IsNullOrWhiteSpace(editedName) && editedName != so.name)
            {
                string assetPath = AssetDatabase.GetAssetPath(so);
                string error = AssetDatabase.RenameAsset(assetPath, editedName);
                if (string.IsNullOrEmpty(error))
                {
                    AssetDatabase.SaveAssets();
                    items.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
                    RefreshFilter();
                }
                else
                {
                    Debug.LogWarning($"[DataBrowser] Rename failed: {error}");
                }
            }
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
            else if (currentTab == TabCodex)
            {
                // Codex tab: focused narrative editor
                propertyScroll = EditorGUILayout.BeginScrollView(propertyScroll, GUILayout.ExpandHeight(true));
                DrawCodexNarrativePanel(so);
                EditorGUILayout.EndScrollView();
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
        // CODEX NARRATIVE PANEL
        // ─────────────────────────────────────────────────────────────────────

        private void DrawCodexNarrativePanel(ScriptableObject so)
        {
            ShelfArchetype shelf = so as ShelfArchetype;
            if (shelf == null) return;

            if (cachedEditor == null || cachedEditor.target != so)
            {
                DestroyEditor();
                UnityEditor.Editor.CreateCachedEditor(so, null, ref cachedEditor);
            }

            SerializedObject serialized = cachedEditor.serializedObject;
            serialized.Update();

            // Section header
            Rect headerRect = EditorGUILayout.GetControlRect(false, 22f);
            EditorGUI.DrawRect(headerRect, CodexHeaderColor);
            SerializedProperty displayNameProp = serialized.FindProperty("displayName");
            string displayLabel = displayNameProp != null && !string.IsNullOrEmpty(displayNameProp.stringValue)
                ? displayNameProp.stringValue
                : shelf.name;
            SerializedProperty categoryProp = serialized.FindProperty("category");
            string categoryLabel = categoryProp != null ? categoryProp.enumDisplayNames[categoryProp.enumValueIndex] : "";
            Rect headerLabelRect = new Rect(headerRect.x + 8, headerRect.y + 3, headerRect.width - 8, 16f);
            EditorGUI.LabelField(headerLabelRect, $"  {displayLabel}   [{categoryLabel}]", EditorStyles.whiteLabel);

            EditorGUILayout.Space(10);

            EditorGUI.BeginChangeCheck();

            // Description
            EditorGUILayout.LabelField("Description", EditorStyles.boldLabel);
            SerializedProperty descProp = serialized.FindProperty("description");
            if (descProp != null)
            {
                descProp.stringValue = EditorGUILayout.TextArea(descProp.stringValue,
                    GUILayout.MinHeight(100), GUILayout.ExpandWidth(true));
            }
            else
            {
                EditorGUILayout.HelpBox("'description' field not found on this asset.", MessageType.Warning);
            }

            EditorGUILayout.Space(10);

            // Usage Instruction
            EditorGUILayout.LabelField("Usage Instruction", EditorStyles.boldLabel);
            SerializedProperty usageProp = serialized.FindProperty("usageInstruction");
            if (usageProp != null)
            {
                usageProp.stringValue = EditorGUILayout.TextArea(usageProp.stringValue,
                    GUILayout.MinHeight(100), GUILayout.ExpandWidth(true));
            }
            else
            {
                EditorGUILayout.HelpBox("'usageInstruction' field not found on this asset.", MessageType.Warning);
            }

            EditorGUILayout.Space(12);

            if (EditorGUI.EndChangeCheck())
            {
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(shelf);
            }

            if (GUILayout.Button("Save", GUILayout.Height(26)))
            {
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(shelf);
                AssetDatabase.SaveAssets();
            }
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

            // Don't steal keys while a text field has focus
            if (GUIUtility.keyboardControl != 0) return;

            // Ctrl+C : copy selected asset
            if (e.control && e.keyCode == KeyCode.C)
            {
                copiedIndex = selectedIndex;
                e.Use();
                return;
            }

            // Ctrl+V : paste (duplicate copied asset)
            if (e.control && e.keyCode == KeyCode.V)
            {
                if (copiedIndex >= 0 && copiedIndex < items.Count && items[copiedIndex] != null)
                    DuplicateAsset(items[copiedIndex]);
                e.Use();
                return;
            }

            // ← → : switch tabs
            if (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.RightArrow)
            {
                int newTab = currentTab + (e.keyCode == KeyCode.RightArrow ? 1 : -1);
                newTab = Mathf.Clamp(newTab, 0, TabLabels.Length - 1);
                if (newTab != currentTab)
                {
                    currentTab = newTab;
                    e.Use();
                    Repaint();
                }
                return;
            }

            // ↑ ↓ : move selection — news items or normal SO list
            if (e.keyCode != KeyCode.UpArrow && e.keyCode != KeyCode.DownArrow) return;

            if (currentTab == TabNewsLibraries)
            {
                if (filteredNewsIndices.Count == 0) { e.Use(); return; }
                int curPos = filteredNewsIndices.IndexOf(selectedNewsItemIndex);
                int nPos = e.keyCode == KeyCode.UpArrow
                    ? (curPos <= 0 ? 0 : curPos - 1)
                    : (curPos >= filteredNewsIndices.Count - 1 ? filteredNewsIndices.Count - 1 : curPos + 1);
                if (curPos == -1) nPos = e.keyCode == KeyCode.UpArrow ? filteredNewsIndices.Count - 1 : 0;
                selectedNewsItemIndex = filteredNewsIndices[nPos];
                e.Use();
                Repaint();
                return;
            }

            if (filteredIndices.Count == 0) return;

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

            if (currentTab == TabNewsLibraries)
            {
                newsLibrarySerializedObject = null;
                EnsureNewsLibraryLoaded();
                RefreshNewsFilter();
            }
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

        private void DuplicateAsset(ScriptableObject source)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string dir = System.IO.Path.GetDirectoryName(sourcePath);
            string ext = System.IO.Path.GetExtension(sourcePath);
            string newPath = AssetDatabase.GenerateUniqueAssetPath(
                System.IO.Path.Combine(dir, source.name + "_Copy" + ext).Replace('\\', '/'));

            if (!AssetDatabase.CopyAsset(sourcePath, newPath))
            {
                Debug.LogWarning($"[DataBrowser] Duplicate failed for: {sourcePath}");
                return;
            }

            AssetDatabase.SaveAssets();
            LoadAssets();

            for (int i = 0; i < items.Count; i++)
            {
                if (AssetDatabase.GetAssetPath(items[i]) == newPath)
                {
                    selectedIndex = i;
                    DestroyEditor();
                    break;
                }
            }

            Repaint();
        }

        // ─────────────────────────────────────────────────────────────────────
        // NEWS LIBRARY PANEL
        // ─────────────────────────────────────────────────────────────────────

        private void EnsureNewsLibraryLoaded()
        {
            if (newsLibrarySerializedObject != null && newsLibrarySerializedObject.targetObject != null) return;

            string[] guids = AssetDatabase.FindAssets("t:NewsLibrary");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/ThirdParty/") || path.StartsWith("Packages/")) continue;
                var lib = AssetDatabase.LoadAssetAtPath<NewsLibrary>(path);
                if (lib != null)
                {
                    newsLibrarySerializedObject = new SerializedObject(lib);
                    return;
                }
            }
        }

        private void RefreshNewsFilter()
        {
            filteredNewsIndices.Clear();
            EnsureNewsLibraryLoaded();
            if (newsLibrarySerializedObject == null) return;

            newsLibrarySerializedObject.Update();
            SerializedProperty prop = newsLibrarySerializedObject.FindProperty("newsTexts");
            if (prop == null) return;

            for (int i = 0; i < prop.arraySize; i++)
            {
                string text = prop.GetArrayElementAtIndex(i).stringValue;
                if (string.IsNullOrEmpty(searchFilter) ||
                    text.IndexOf(searchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    filteredNewsIndices.Add(i);
            }
        }

        private void DrawNewsListPanel()
        {
            EnsureNewsLibraryLoaded();
            newsLibrarySerializedObject?.Update();
            SerializedProperty newsTextsProp = newsLibrarySerializedObject?.FindProperty("newsTexts");
            int totalCount = newsTextsProp?.arraySize ?? 0;

            EditorGUILayout.BeginVertical(GUILayout.Width(ListPanelWidth), GUILayout.ExpandHeight(true));

            // Search toolbar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Search", GUILayout.Width(45));
            string newFilter = GUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField);
            if (newFilter != searchFilter)
            {
                searchFilter = newFilter;
                RefreshNewsFilter();
                selectedNewsItemIndex = -1;
            }
            if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(20)))
            {
                searchFilter = "";
                RefreshNewsFilter();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"{filteredNewsIndices.Count} / {totalCount} items",
                EditorStyles.centeredGreyMiniLabel);

            listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.ExpandHeight(true));

            if (newsTextsProp == null)
            {
                EditorGUILayout.HelpBox("No NewsLibrary asset found in the project.", MessageType.Warning);
            }
            else
            {
                hoveredIndex = -1;
                for (int fi = 0; fi < filteredNewsIndices.Count; fi++)
                {
                    int realIndex = filteredNewsIndices[fi];
                    string text = newsTextsProp.GetArrayElementAtIndex(realIndex).stringValue;
                    bool isSelected = realIndex == selectedNewsItemIndex;

                    Rect rowRect = EditorGUILayout.GetControlRect(false, 22f);
                    if (rowRect.Contains(Event.current.mousePosition)) hoveredIndex = fi;

                    if (isSelected)
                        EditorGUI.DrawRect(rowRect, SelectedRowColor);
                    else if (hoveredIndex == fi)
                        EditorGUI.DrawRect(rowRect, HoverRowColor);

                    Rect indexRect = new Rect(rowRect.x + 4, rowRect.y + 4, 28, 15);
                    Rect labelRect = new Rect(rowRect.x + 36, rowRect.y + 3, rowRect.width - 40, 16);

                    GUI.Label(indexRect, $"#{realIndex + 1}", EditorStyles.miniLabel);
                    string preview = string.IsNullOrEmpty(text) ? "(empty)" :
                        text.Length > 48 ? text.Substring(0, 48) + "…" : text;
                    GUI.Label(labelRect, preview, isSelected ? EditorStyles.whiteLabel : EditorStyles.label);

                    if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                    {
                        selectedNewsItemIndex = realIndex;
                        GUI.FocusControl(null);
                        Event.current.Use();
                        Repaint();
                    }
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("+ Add News Item", GUILayout.Height(26)))
                AddNewsItem(newsTextsProp);
            EditorGUILayout.Space(2);

            EditorGUILayout.EndVertical();
        }

        private void DrawNewsPropertyPanel()
        {
            EnsureNewsLibraryLoaded();
            newsLibrarySerializedObject?.Update();
            SerializedProperty newsTextsProp = newsLibrarySerializedObject?.FindProperty("newsTexts");

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (newsTextsProp == null)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("No NewsLibrary asset found.", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            if (selectedNewsItemIndex < 0 || selectedNewsItemIndex >= newsTextsProp.arraySize)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("← Select a news item to edit", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            // Header bar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            Rect headerRect = GUILayoutUtility.GetRect(0, 22f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(headerRect, NewsHeaderColor);
            EditorGUI.LabelField(new Rect(headerRect.x + 6, headerRect.y + 3, headerRect.width - 70, 16),
                $"  News Item #{selectedNewsItemIndex + 1}", EditorStyles.whiteLabel);

            GUI.backgroundColor = NewsDeleteColor;
            if (GUI.Button(new Rect(headerRect.xMax - 64, headerRect.y + 2, 60, 18), "Delete", EditorStyles.miniButton))
            {
                GUI.backgroundColor = Color.white;
                if (EditorUtility.DisplayDialog("Delete News Item",
                    $"Delete news item #{selectedNewsItemIndex + 1}?", "Delete", "Cancel"))
                {
                    DeleteNewsItem(newsTextsProp, selectedNewsItemIndex);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    return;
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            propertyScroll = EditorGUILayout.BeginScrollView(propertyScroll, GUILayout.ExpandHeight(true));

            SerializedProperty item = newsTextsProp.GetArrayElementAtIndex(selectedNewsItemIndex);

            EditorGUI.BeginChangeCheck();
            item.stringValue = EditorGUILayout.TextArea(item.stringValue,
                GUILayout.MinHeight(120), GUILayout.ExpandWidth(true));
            if (EditorGUI.EndChangeCheck())
            {
                newsLibrarySerializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(newsLibrarySerializedObject.targetObject);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Characters: {item.stringValue.Length}", EditorStyles.miniLabel);
            EditorGUILayout.Space(10);

            // Move up / Move down
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = selectedNewsItemIndex > 0;
            if (GUILayout.Button("▲ Move Up", GUILayout.Height(24)))
            {
                newsTextsProp.MoveArrayElement(selectedNewsItemIndex, selectedNewsItemIndex - 1);
                selectedNewsItemIndex--;
                newsLibrarySerializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(newsLibrarySerializedObject.targetObject);
                RefreshNewsFilter();
            }
            GUI.enabled = selectedNewsItemIndex < newsTextsProp.arraySize - 1;
            if (GUILayout.Button("▼ Move Down", GUILayout.Height(24)))
            {
                newsTextsProp.MoveArrayElement(selectedNewsItemIndex, selectedNewsItemIndex + 1);
                selectedNewsItemIndex++;
                newsLibrarySerializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(newsLibrarySerializedObject.targetObject);
                RefreshNewsFilter();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            if (GUILayout.Button("Save", GUILayout.Height(26)))
            {
                newsLibrarySerializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(newsLibrarySerializedObject.targetObject);
                AssetDatabase.SaveAssets();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void AddNewsItem(SerializedProperty newsTextsProp)
        {
            if (newsTextsProp == null) return;
            newsLibrarySerializedObject.Update();
            newsTextsProp.InsertArrayElementAtIndex(newsTextsProp.arraySize);
            newsTextsProp.GetArrayElementAtIndex(newsTextsProp.arraySize - 1).stringValue = "";
            newsLibrarySerializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(newsLibrarySerializedObject.targetObject);
            selectedNewsItemIndex = newsTextsProp.arraySize - 1;
            RefreshNewsFilter();
            Repaint();
        }

        private void DeleteNewsItem(SerializedProperty newsTextsProp, int index)
        {
            if (newsTextsProp == null || index < 0 || index >= newsTextsProp.arraySize) return;
            newsLibrarySerializedObject.Update();
            newsTextsProp.DeleteArrayElementAtIndex(index);
            newsLibrarySerializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(newsLibrarySerializedObject.targetObject);
            selectedNewsItemIndex = -1;
            RefreshNewsFilter();
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

        // ─────────────────────────────────────────────────────────────────────
        // QUEST CHAIN VIEW
        // ─────────────────────────────────────────────────────────────────────

        private static void EnsureMarkerArrays()
        {
            if (s_markerNames != null) return;
            var rawValues = (int[])System.Enum.GetValues(typeof(TutorialMarker));
            var rawNames  = System.Enum.GetNames(typeof(TutorialMarker));
            s_markerValues = rawValues;
            s_markerNames  = new string[rawNames.Length];
            for (int i = 0; i < rawNames.Length; i++)
                s_markerNames[i] = $"{rawNames[i]}  ({rawValues[i]})";
        }

        private void ReparentQuest(ScriptableObject so, int newActivationMarker)
        {
            if (so == null) return;
            var serial = new SerializedObject(so);
            serial.Update();
            serial.FindProperty("activationMarker").intValue = newActivationMarker;
            serial.ApplyModifiedProperties();
            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();
        }

        private void BuildChainDisplayList()
        {
            chainDisplayList.Clear();
            selectedChainItemIndex = -1;

            var allQuests  = new List<ScriptableObject>();
            var activation = new Dictionary<ScriptableObject, int>();
            var completion = new Dictionary<ScriptableObject, int>();
            var priority   = new Dictionary<ScriptableObject, int>();
            var titleMap   = new Dictionary<ScriptableObject, string>();

            string[] guids = AssetDatabase.FindAssets("t:QuestDefinition");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/ThirdParty/") || path.StartsWith("Packages/")) continue;
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so == null) continue;

                var s = new SerializedObject(so);
                activation[so] = s.FindProperty("activationMarker").intValue;
                completion[so] = s.FindProperty("completionMarker").intValue;
                priority[so]   = s.FindProperty("sortPriority").intValue;
                string t       = s.FindProperty("title").stringValue;
                titleMap[so]   = string.IsNullOrEmpty(t) ? so.name : t;
                allQuests.Add(so);
            }

            // childrenByMarker: completionMarker value → quests activated by it (skip -1 activation)
            var childrenByMarker = new Dictionary<int, List<ScriptableObject>>();
            foreach (var so in allQuests)
            {
                int act = activation[so];
                if (act == -1) continue;
                if (!childrenByMarker.TryGetValue(act, out var lst))
                    childrenByMarker[act] = lst = new List<ScriptableObject>();
                lst.Add(so);
            }

            // allCompletions excludes -1, so quests with activationMarker=-1 are never accidentally hidden
            var allCompletions = new HashSet<int>();
            foreach (var so in allQuests)
            {
                int c = completion[so];
                if (c != -1) allCompletions.Add(c);
            }

            // Roots: non-(-1) activation and not triggered by another quest's completion
            var rootsByTrigger = new Dictionary<int, List<ScriptableObject>>();
            foreach (var so in allQuests)
            {
                int act = activation[so];
                if (act == -1) continue;
                if (!allCompletions.Contains(act))
                {
                    if (!rootsByTrigger.TryGetValue(act, out var lst))
                        rootsByTrigger[act] = lst = new List<ScriptableObject>();
                    lst.Add(so);
                }
            }

            // Sort trigger groups ascending (no -1 here)
            var sortedTriggers = new List<int>(rootsByTrigger.Keys);
            sortedTriggers.Sort();

            foreach (int trigger in sortedTriggers)
            {
                chainDisplayList.Add(new ChainDisplayItem
                {
                    isHeader     = true,
                    label        = $"⚡  {GetMarkerDisplayName(trigger)}  ({trigger})",
                    triggerValue = trigger,
                });
                var roots = new List<ScriptableObject>(rootsByTrigger[trigger]);
                roots.Sort((a, b) => priority[a].CompareTo(priority[b]));
                foreach (var root in roots)
                    DfsAddChainItem(root, 1, activation, completion, priority, titleMap, childrenByMarker);
            }

            // No-activation section at the bottom
            var noAct = new List<ScriptableObject>();
            foreach (var so in allQuests)
                if (activation[so] == -1) noAct.Add(so);
            noAct.Sort((a, b) => priority[a].CompareTo(priority[b]));

            if (noAct.Count > 0)
            {
                chainDisplayList.Add(new ChainDisplayItem
                {
                    isHeader     = true,
                    label        = "⬜  No Activation Marker",
                    triggerValue = -1,
                });
                foreach (var so in noAct)
                    DfsAddChainItem(so, 1, activation, completion, priority, titleMap, childrenByMarker);
            }
        }

        private void DfsAddChainItem(ScriptableObject so, int depth,
            Dictionary<ScriptableObject, int> activation,
            Dictionary<ScriptableObject, int> completion,
            Dictionary<ScriptableObject, int> priority,
            Dictionary<ScriptableObject, string> titleMap,
            Dictionary<int, List<ScriptableObject>> childrenByMarker)
        {
            chainDisplayList.Add(new ChainDisplayItem
            {
                isHeader = false,
                label    = titleMap[so],
                so       = so,
                depth    = depth,
            });

            int comp = completion[so];
            if (comp >= 0 && childrenByMarker.TryGetValue(comp, out var children))
            {
                var sorted = new List<ScriptableObject>(children);
                sorted.Sort((a, b) => priority[a].CompareTo(priority[b]));
                foreach (var child in sorted)
                    DfsAddChainItem(child, depth + 1, activation, completion, priority, titleMap, childrenByMarker);
            }
        }

        private void DrawQuestChainListPanel()
        {
            if (chainDisplayList.Count == 0) BuildChainDisplayList();

            int questCount = 0;
            foreach (var item in chainDisplayList) if (!item.isHeader) questCount++;

            EditorGUILayout.BeginVertical(GUILayout.Width(ListPanelWidth), GUILayout.ExpandHeight(true));

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"{questCount} quests", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("⟳", EditorStyles.toolbarButton, GUILayout.Width(24)))
            {
                BuildChainDisplayList();
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            chainListScroll = EditorGUILayout.BeginScrollView(chainListScroll, GUILayout.ExpandHeight(true));

            Event e = Event.current;

            if (e.type == EventType.DragExited || e.type == EventType.MouseUp)
            {
                chainDragHoverIndex = -1;
                chainMouseDownIndex = -1;
            }

            for (int i = 0; i < chainDisplayList.Count; i++)
            {
                var item = chainDisplayList[i];

                if (item.isHeader)
                {
                    EditorGUILayout.Space(2);
                    Rect r = EditorGUILayout.GetControlRect(false, 20f);

                    bool isDropTarget = (chainDragHoverIndex == i);
                    EditorGUI.DrawRect(r, isDropTarget ? new Color(0.15f, 0.45f, 0.15f) : ChainHeaderColor);
                    EditorGUI.LabelField(new Rect(r.x + 6, r.y + 2, r.width - 8, 16),
                        item.label, EditorStyles.whiteLabel);

                    if (e.type == EventType.DragUpdated && r.Contains(e.mousePosition)
                        && DragAndDrop.objectReferences.Length > 0)
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                        chainDragHoverIndex    = i;
                        e.Use();
                        Repaint();
                    }
                    if (e.type == EventType.DragPerform && r.Contains(e.mousePosition)
                        && DragAndDrop.objectReferences.Length > 0)
                    {
                        var droppedSO = DragAndDrop.objectReferences[0] as ScriptableObject;
                        if (droppedSO != null)
                        {
                            DragAndDrop.AcceptDrag();
                            ReparentQuest(droppedSO, item.triggerValue);
                            chainDragHoverIndex = -1;
                            BuildChainDisplayList();
                            Repaint();
                        }
                        e.Use();
                    }
                }
                else
                {
                    bool isSelected = (i == selectedChainItemIndex);
                    bool isDropTgt  = (chainDragHoverIndex == i);
                    Rect rowRect    = EditorGUILayout.GetControlRect(false, 20f);

                    if (isDropTgt)
                        EditorGUI.DrawRect(rowRect, new Color(0.15f, 0.50f, 0.15f, 0.9f));
                    else if (isSelected)
                        EditorGUI.DrawRect(rowRect, SelectedRowColor);
                    else if (rowRect.Contains(e.mousePosition))
                        EditorGUI.DrawRect(rowRect, HoverRowColor);

                    float indent   = (item.depth - 1) * 14f + 6f;
                    string prefix  = item.depth == 1 ? "◉ " : "└ ";
                    Rect labelRect = new Rect(rowRect.x + indent, rowRect.y + 2, rowRect.width - indent - 4, 16);
                    GUI.Label(labelRect, prefix + item.label,
                        isSelected ? EditorStyles.whiteLabel : EditorStyles.label);

                    // Click to select
                    if (e.type == EventType.MouseDown && rowRect.Contains(e.mousePosition))
                    {
                        selectedChainItemIndex = i;
                        chainMouseDownIndex    = i;
                        GUI.FocusControl(null);
                        e.Use();
                        Repaint();
                    }

                    // Start drag: fires once when mouse moves after clicking this row
                    if (e.type == EventType.MouseDrag && chainMouseDownIndex == i && item.so != null)
                    {
                        DragAndDrop.PrepareStartDrag();
                        DragAndDrop.objectReferences = new Object[] { item.so };
                        DragAndDrop.StartDrag(item.label);
                        chainMouseDownIndex = -1;
                        e.Use();
                    }

                    // Highlight drop target while dragging over this row
                    if (e.type == EventType.DragUpdated && rowRect.Contains(e.mousePosition)
                        && DragAndDrop.objectReferences.Length > 0
                        && DragAndDrop.objectReferences[0] != item.so)
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                        chainDragHoverIndex    = i;
                        e.Use();
                        Repaint();
                    }

                    // Accept drop: reparent dragged quest under this quest
                    if (e.type == EventType.DragPerform && rowRect.Contains(e.mousePosition)
                        && DragAndDrop.objectReferences.Length > 0)
                    {
                        var droppedSO = DragAndDrop.objectReferences[0] as ScriptableObject;
                        if (droppedSO != null && droppedSO != item.so)
                        {
                            DragAndDrop.AcceptDrag();
                            var targetSer = new SerializedObject(item.so);
                            targetSer.Update();
                            int targetComp = targetSer.FindProperty("completionMarker").intValue;
                            ReparentQuest(droppedSO, targetComp);
                            chainDragHoverIndex = -1;
                            BuildChainDisplayList();
                            Repaint();
                        }
                        e.Use();
                    }
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawQuestChainDetailPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            bool validSelection = selectedChainItemIndex >= 0
                && selectedChainItemIndex < chainDisplayList.Count
                && !chainDisplayList[selectedChainItemIndex].isHeader
                && chainDisplayList[selectedChainItemIndex].so != null;

            if (!validSelection)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("← Select a quest node to view details",
                    EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            var so = chainDisplayList[selectedChainItemIndex].so;
            if (chainDetailTarget != so || chainDetailSO == null)
            {
                chainDetailSO    = new SerializedObject(so);
                chainDetailTarget = so;
            }
            chainDetailSO.Update();
            EnsureMarkerArrays();

            // ── Header bar ──
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var titleProp    = chainDetailSO.FindProperty("title");
            string dispTitle = string.IsNullOrEmpty(titleProp.stringValue) ? so.name : titleProp.stringValue;
            GUILayout.Label(dispTitle, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            var qtProp = chainDetailSO.FindProperty("questType");
            var badge  = new GUIStyle(EditorStyles.miniLabel);
            badge.normal.textColor = qtProp.intValue == 0
                ? new Color(0.9f, 0.75f, 0.3f) : new Color(0.6f, 0.8f, 0.6f);
            GUILayout.Label(qtProp.intValue == 0 ? "MAIN" : "SIDE", badge);
            GUILayout.Space(8);
            if (GUILayout.Button("Select", EditorStyles.toolbarButton, GUILayout.Width(55)))
            {
                Selection.activeObject = so;
                EditorGUIUtility.PingObject(so);
            }
            if (GUILayout.Button("Save & Rebuild", EditorStyles.toolbarButton, GUILayout.Width(95)))
            {
                chainDetailSO.ApplyModifiedProperties();
                EditorUtility.SetDirty(so);
                AssetDatabase.SaveAssets();
                BuildChainDisplayList();
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            chainDetailScroll = EditorGUILayout.BeginScrollView(chainDetailScroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.Space(6);

            // ── Quest Info ──
            DrawChainSection("Quest Info", ChainHeaderColor);
            EditorGUILayout.PropertyField(chainDetailSO.FindProperty("questName"),    new GUIContent("Quest Name"));
            EditorGUILayout.PropertyField(chainDetailSO.FindProperty("title"),        new GUIContent("Title"));
            EditorGUILayout.PropertyField(chainDetailSO.FindProperty("giverName"),    new GUIContent("Giver"));
            EditorGUILayout.PropertyField(chainDetailSO.FindProperty("questType"),    new GUIContent("Type"));
            EditorGUILayout.PropertyField(chainDetailSO.FindProperty("deadlineDays"), new GUIContent("Deadline Days"));
            var spProp = chainDetailSO.FindProperty("sortPriority");
            spProp.intValue = EditorGUILayout.IntField("Sort Priority", spProp.intValue);
            EditorGUILayout.Space(6);

            // ── Trigger Chain ──
            DrawChainSection("Trigger Chain", ChainTriggerColor);
            var actProp  = chainDetailSO.FindProperty("activationMarker");
            var compProp = chainDetailSO.FindProperty("completionMarker");
            actProp.intValue  = EditorGUILayout.IntPopup(
                "Activation Marker",  actProp.intValue,  s_markerNames, s_markerValues);
            compProp.intValue = EditorGUILayout.IntPopup(
                "Completion Marker", compProp.intValue, s_markerNames, s_markerValues);
            EditorGUILayout.PropertyField(chainDetailSO.FindProperty("triggerMarkerAfterToast"),
                new GUIContent("After Toast"));
            EditorGUILayout.PropertyField(chainDetailSO.FindProperty("autoCompleteOnActivation"),
                new GUIContent("Auto Complete"));
            EditorGUILayout.Space(6);

            // ── Conditions ──
            DrawChainSection("Conditions", ChainConditionColor);
            EditorGUILayout.PropertyField(chainDetailSO.FindProperty("conditions"),
                new GUIContent("Conditions"), true);
            EditorGUILayout.Space(6);

            // ── Rewards ──
            DrawChainSection("Rewards", ChainRewardColor);
            EditorGUILayout.PropertyField(chainDetailSO.FindProperty("rewards"),
                new GUIContent("Rewards"), true);
            EditorGUILayout.Space(6);

            // ── Text ──
            DrawChainSection("Text", ChainTextColor);
            EditorGUILayout.PropertyField(chainDetailSO.FindProperty("description"),
                new GUIContent("Description"));
            EditorGUILayout.PropertyField(chainDetailSO.FindProperty("successDescription"),
                new GUIContent("Success"));
            EditorGUILayout.PropertyField(chainDetailSO.FindProperty("entryTexts"),
                new GUIContent("Objectives"), true);
            EditorGUILayout.Space(16);

            EditorGUILayout.EndScrollView();

            if (EditorGUI.EndChangeCheck())
            {
                chainDetailSO.ApplyModifiedProperties();
                EditorUtility.SetDirty(so);
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawChainSection(string label, Color color)
        {
            Rect r = EditorGUILayout.GetControlRect(false, 18f);
            EditorGUI.DrawRect(r, color);
            EditorGUI.LabelField(new Rect(r.x + 6, r.y + 1, r.width - 8, 16),
                label, EditorStyles.whiteLabel);
            EditorGUILayout.Space(2);
        }

        private static string GetMarkerDisplayName(int value)
        {
            if (value == -1) return "None";
            try { return ((TutorialMarker)value).ToString(); }
            catch { return $"Marker_{value}"; }
        }

        private void OnDestroy()
        {
            DestroyEditor();
            if (dbSerializedObject != null)
            {
                dbSerializedObject.Dispose();
                dbSerializedObject = null;
            }
            chainDetailSO     = null;
            chainDetailTarget = null;
            newsLibrarySerializedObject = null;
        }
    }
}
