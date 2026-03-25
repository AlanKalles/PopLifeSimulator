using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PopLife.Data;

public class QuestTableWindow : EditorWindow
{
    private enum SortField
    {
        QuestName,
        QuestType,
        Deadline,
        Giver,
        Priority
    }

    private class QuestRow
    {
        public QuestDefinition quest;
        public SerializedObject serializedObject;

        public QuestRow(QuestDefinition quest)
        {
            this.quest = quest;
            serializedObject = new SerializedObject(quest);
        }

        public void RefreshSerializedObject()
        {
            if (quest != null)
                serializedObject = new SerializedObject(quest);
        }
    }

    private List<QuestRow> rows = new List<QuestRow>();
    private Vector2 leftScroll;
    private Vector2 rightScroll;

    private string rootFolder = "Assets";
    private SortField sortField = SortField.QuestName;
    private bool sortAscending = true;

    private QuestRow selectedRow;

    [MenuItem("PopLife/Quest Table")]
    public static void Open()
    {
        var window = GetWindow<QuestTableWindow>("Quest Table");
        window.minSize = new Vector2(1200, 650);
        window.RefreshData();
    }

    private void OnEnable()
    {
        RefreshData();
    }

    private void OnFocus()
    {
        RefreshData();
    }

    private void OnGUI()
    {
        DrawToolbar();
        EditorGUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();

        DrawLeftPanel();
        DrawRightPanel();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Root Folder", GUILayout.Width(80));
        rootFolder = EditorGUILayout.TextField(rootFolder);

        if (GUILayout.Button("Select Folder", GUILayout.Width(100)))
        {
            string selected = EditorUtility.OpenFolderPanel("Select Quest Root Folder", Application.dataPath, "");
            if (!string.IsNullOrEmpty(selected))
            {
                if (selected.StartsWith(Application.dataPath))
                {
                    rootFolder = "Assets" + selected.Substring(Application.dataPath.Length);
                    RefreshData();
                }
                else
                {
                    EditorUtility.DisplayDialog("Invalid Folder", "Please select a folder inside this project's Assets folder.", "OK");
                }
            }
        }

        if (GUILayout.Button("Refresh", GUILayout.Width(80)))
        {
            RefreshData();
        }

        GUILayout.FlexibleSpace();

        EditorGUILayout.LabelField("Sort By", GUILayout.Width(50));
        var newSort = (SortField)EditorGUILayout.EnumPopup(sortField, GUILayout.Width(120));
        if (newSort != sortField)
        {
            sortField = newSort;
            ApplySorting();
        }

        bool newAscending = GUILayout.Toggle(sortAscending, sortAscending ? "Ascending" : "Descending", "Button", GUILayout.Width(100));
        if (newAscending != sortAscending)
        {
            sortAscending = newAscending;
            ApplySorting();
        }

        EditorGUILayout.LabelField($"Count: {rows.Count}", GUILayout.Width(80));

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawLeftPanel()
{
    EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.55f));

    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
    GUILayout.Label("Select", EditorStyles.boldLabel, GUILayout.Width(50));
    GUILayout.Label("Quest Name", EditorStyles.boldLabel, GUILayout.Width(180));
    GUILayout.Label("Type", EditorStyles.boldLabel, GUILayout.Width(80));
    GUILayout.Label("Deadline", EditorStyles.boldLabel, GUILayout.Width(80));
    GUILayout.Label("Giver", EditorStyles.boldLabel, GUILayout.Width(120));
    GUILayout.Label("Activation", EditorStyles.boldLabel, GUILayout.Width(110));
    GUILayout.Label("Completion", EditorStyles.boldLabel, GUILayout.Width(110));
    GUILayout.Label("Priority", EditorStyles.boldLabel, GUILayout.Width(65));
    GUILayout.Label("Ping", EditorStyles.boldLabel, GUILayout.Width(45));
    EditorGUILayout.EndHorizontal();

    leftScroll = EditorGUILayout.BeginScrollView(leftScroll);

    foreach (var row in rows)
    {
        if (row.quest == null) continue;

        row.serializedObject.Update();

        SerializedProperty questNameProp = row.serializedObject.FindProperty("questName");
        SerializedProperty questTypeProp = row.serializedObject.FindProperty("questType");
        SerializedProperty deadlineProp = row.serializedObject.FindProperty("deadlineDays");
        SerializedProperty giverNameProp = row.serializedObject.FindProperty("giverName");
        SerializedProperty activationProp = row.serializedObject.FindProperty("activationMarker");
        SerializedProperty completionProp = row.serializedObject.FindProperty("completionMarker");
        SerializedProperty priorityProp = row.serializedObject.FindProperty("sortPriority");

        bool isSelected = selectedRow == row;
        GUIStyle rowStyle = isSelected ? "SelectionRect" : "box";

        EditorGUILayout.BeginHorizontal(rowStyle);

        if (GUILayout.Button("Sel", GUILayout.Width(50)))
        {
            selectedRow = row;
            GUI.FocusControl(null);
        }

        EditorGUI.BeginChangeCheck();

        if (questNameProp != null)
            questNameProp.stringValue = EditorGUILayout.TextField(questNameProp.stringValue, GUILayout.Width(180));
        else
            GUILayout.Label("-", GUILayout.Width(180));

        if (questTypeProp != null)
            EditorGUILayout.PropertyField(questTypeProp, GUIContent.none, GUILayout.Width(80));
        else
            GUILayout.Label("-", GUILayout.Width(80));

        if (deadlineProp != null)
            deadlineProp.intValue = EditorGUILayout.IntField(deadlineProp.intValue, GUILayout.Width(80));
        else
            GUILayout.Label("-", GUILayout.Width(80));

        if (giverNameProp != null)
            giverNameProp.stringValue = EditorGUILayout.TextField(giverNameProp.stringValue, GUILayout.Width(120));
        else
            GUILayout.Label("-", GUILayout.Width(120));

        if (activationProp != null)
            EditorGUILayout.PropertyField(activationProp, GUIContent.none, GUILayout.Width(110));
        else
            GUILayout.Label("-", GUILayout.Width(110));

        if (completionProp != null)
            EditorGUILayout.PropertyField(completionProp, GUIContent.none, GUILayout.Width(110));
        else
            GUILayout.Label("-", GUILayout.Width(110));

        if (priorityProp != null)
            priorityProp.intValue = EditorGUILayout.IntField(priorityProp.intValue, GUILayout.Width(65));
        else
            GUILayout.Label("-", GUILayout.Width(65));

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(row.quest, "Edit QuestDefinition");
            row.serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(row.quest);
            AssetDatabase.SaveAssets();

            selectedRow = row;
            ApplySorting();
            Repaint();

            GUIUtility.ExitGUI();
        }

        if (GUILayout.Button("Ping", GUILayout.Width(45)))
        {
            Selection.activeObject = row.quest;
            EditorGUIUtility.PingObject(row.quest);
            selectedRow = row;
        }

        EditorGUILayout.EndHorizontal();
    }

    EditorGUILayout.EndScrollView();
    EditorGUILayout.EndVertical();
}
    private void DrawRightPanel()
    {
        EditorGUILayout.BeginVertical("box");

        if (selectedRow == null || selectedRow.quest == null)
        {
            EditorGUILayout.HelpBox("Select a QuestDefinition from the left panel.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        selectedRow.serializedObject.Update();

        SerializedProperty questNameProp = selectedRow.serializedObject.FindProperty("questName");
        SerializedProperty questTypeProp = selectedRow.serializedObject.FindProperty("questType");
        SerializedProperty deadlineProp = selectedRow.serializedObject.FindProperty("deadlineDays");
        SerializedProperty giverNameProp = selectedRow.serializedObject.FindProperty("giverName");
        SerializedProperty giverPortraitProp = selectedRow.serializedObject.FindProperty("giverPortrait");
        SerializedProperty rewardsProp = selectedRow.serializedObject.FindProperty("rewards");
        SerializedProperty conditionsProp = selectedRow.serializedObject.FindProperty("conditions");
        SerializedProperty activationProp = selectedRow.serializedObject.FindProperty("activationMarker");
        SerializedProperty completionProp = selectedRow.serializedObject.FindProperty("completionMarker");
        SerializedProperty triggerAfterToastProp = selectedRow.serializedObject.FindProperty("triggerMarkerAfterToast");
        SerializedProperty questIconProp = selectedRow.serializedObject.FindProperty("questIcon");
        SerializedProperty priorityProp = selectedRow.serializedObject.FindProperty("sortPriority");

        rightScroll = EditorGUILayout.BeginScrollView(rightScroll);

        EditorGUILayout.LabelField("Quest Detail", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        EditorGUI.BeginChangeCheck();

        if (questNameProp != null) EditorGUILayout.PropertyField(questNameProp);
        if (questTypeProp != null) EditorGUILayout.PropertyField(questTypeProp);
        if (deadlineProp != null) EditorGUILayout.PropertyField(deadlineProp);
        if (giverNameProp != null) EditorGUILayout.PropertyField(giverNameProp);
        if (giverPortraitProp != null) EditorGUILayout.PropertyField(giverPortraitProp);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Rewards", EditorStyles.boldLabel);
        if (rewardsProp != null)
            EditorGUILayout.PropertyField(rewardsProp, true);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Conditions", EditorStyles.boldLabel);
        if (conditionsProp != null)
            EditorGUILayout.PropertyField(conditionsProp, true);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Markers", EditorStyles.boldLabel);
        if (activationProp != null) EditorGUILayout.PropertyField(activationProp);
        if (completionProp != null) EditorGUILayout.PropertyField(completionProp);
        if (triggerAfterToastProp != null) EditorGUILayout.PropertyField(triggerAfterToastProp);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("UI", EditorStyles.boldLabel);
        if (questIconProp != null) EditorGUILayout.PropertyField(questIconProp);
        if (priorityProp != null) EditorGUILayout.PropertyField(priorityProp);

        if (EditorGUI.EndChangeCheck())
        {
            ApplyRowChanges(selectedRow);
        }

        EditorGUILayout.Space(12);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Ping Asset", GUILayout.Width(100)))
        {
            Selection.activeObject = selectedRow.quest;
            EditorGUIUtility.PingObject(selectedRow.quest);
        }

        if (GUILayout.Button("Reload", GUILayout.Width(100)))
        {
            selectedRow.RefreshSerializedObject();
            Repaint();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void RefreshData()
    {
        rows.Clear();

        if (string.IsNullOrWhiteSpace(rootFolder))
            rootFolder = "Assets";

        string[] guids;

        if (AssetDatabase.IsValidFolder(rootFolder))
            guids = AssetDatabase.FindAssets("t:QuestDefinition", new[] { rootFolder });
        else
            guids = AssetDatabase.FindAssets("t:QuestDefinition");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            QuestDefinition quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
            if (quest != null)
                rows.Add(new QuestRow(quest));
        }

        ApplySorting();

        if (selectedRow != null && (selectedRow.quest == null || !rows.Any(r => r.quest == selectedRow.quest)))
            selectedRow = null;

        if (selectedRow == null && rows.Count > 0)
            selectedRow = rows[0];

        Repaint();
    }

    private void ApplySorting()
    {
        Func<QuestRow, object> keySelector = sortField switch
        {
            SortField.QuestName => r => GetStringProp(r, "questName"),
            SortField.QuestType => r => GetEnumName(r, "questType"),
            SortField.Deadline => r => GetIntProp(r, "deadlineDays"),
            SortField.Giver => r => GetStringProp(r, "giverName"),
            SortField.Priority => r => GetIntProp(r, "sortPriority"),
            _ => r => GetStringProp(r, "questName")
        };

        rows = sortAscending
            ? rows.OrderBy(keySelector).ToList()
            : rows.OrderByDescending(keySelector).ToList();

        Repaint();
    }

    private string GetStringProp(QuestRow row, string propertyName)
    {
        if (row == null || row.quest == null) return "";
        var so = new SerializedObject(row.quest);
        var prop = so.FindProperty(propertyName);
        return prop != null ? prop.stringValue : "";
    }

    private int GetIntProp(QuestRow row, string propertyName)
    {
        if (row == null || row.quest == null) return 0;
        var so = new SerializedObject(row.quest);
        var prop = so.FindProperty(propertyName);
        return prop != null ? prop.intValue : 0;
    }

    private string GetEnumName(QuestRow row, string propertyName)
    {
        if (row == null || row.quest == null) return "";
        var so = new SerializedObject(row.quest);
        var prop = so.FindProperty(propertyName);
        if (prop == null || prop.propertyType != SerializedPropertyType.Enum)
            return "";
        return prop.enumDisplayNames[prop.enumValueIndex];
    }

    private void ApplyRowChanges(QuestRow row)
    {
        Undo.RecordObject(row.quest, "Edit QuestDefinition");
        row.serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(row.quest);
        AssetDatabase.SaveAssets();
        ApplySorting();
        Repaint();
    }
}
