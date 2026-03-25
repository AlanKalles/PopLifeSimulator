using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PopLife.Data;

public class ShelfTableWindow : EditorWindow
{
    private enum SortField
    {
        Name,
        Price,
        Category,
        Brand
    }

    private class ShelfRow
    {
        public ShelfArchetype shelf;
        public SerializedObject serializedObject;

        public ShelfRow(ShelfArchetype shelf)
        {
            this.shelf = shelf;
            serializedObject = new SerializedObject(shelf);
        }

        public void RefreshSerializedObject()
        {
            if (shelf != null)
                serializedObject = new SerializedObject(shelf);
        }
    }

    private List<ShelfRow> rows = new List<ShelfRow>();
    private Vector2 scroll;

    private string rootFolder = "Assets";
    private int previewLevel = 1;

    private SortField sortField = SortField.Name;
    private bool sortAscending = true;

    [MenuItem("PopLife/Shelf Table")]
    public static void Open()
    {
        var window = GetWindow<ShelfTableWindow>("Shelf Table");
        window.minSize = new Vector2(1000, 500);
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

        if (rows == null || rows.Count == 0)
        {
            EditorGUILayout.HelpBox("No ShelfArchetype assets found in the selected folder.", MessageType.Info);
            return;
        }

        DrawHeader();
        DrawRows();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Root Folder", GUILayout.Width(80));
        rootFolder = EditorGUILayout.TextField(rootFolder);

        if (GUILayout.Button("Select Folder", GUILayout.Width(100)))
        {
            string selected = EditorUtility.OpenFolderPanel("Select Shelf Root Folder", Application.dataPath, "");
            if (!string.IsNullOrEmpty(selected))
            {
                if (selected.StartsWith(Application.dataPath))
                {
                    rootFolder = "Assets" + selected.Substring(Application.dataPath.Length);
                    RefreshData();
                }
                else
                {
                    EditorUtility.DisplayDialog("Invalid Folder", "Please select a folder inside this Unity project's Assets folder.", "OK");
                }
            }
        }

        if (GUILayout.Button("Refresh", GUILayout.Width(80)))
        {
            RefreshData();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Preview Level", GUILayout.Width(80));
        previewLevel = EditorGUILayout.IntSlider(previewLevel, 1, 5, GUILayout.Width(250));

        EditorGUILayout.LabelField("Sort By", GUILayout.Width(50));
        var newSortField = (SortField)EditorGUILayout.EnumPopup(sortField, GUILayout.Width(120));
        if (newSortField != sortField)
        {
            sortField = newSortField;
            ApplySorting();
        }

        bool newAscending = GUILayout.Toggle(sortAscending, sortAscending ? "Ascending" : "Descending", "Button", GUILayout.Width(100));
        if (newAscending != sortAscending)
        {
            sortAscending = newAscending;
            ApplySorting();
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"Found: {rows.Count}", GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        GUILayout.Label("Name", EditorStyles.boldLabel, GUILayout.Width(180));
        GUILayout.Label($"Price (Lv {previewLevel})", EditorStyles.boldLabel, GUILayout.Width(100));
        GUILayout.Label("Category", EditorStyles.boldLabel, GUILayout.Width(140));
        GUILayout.Label("Brand", EditorStyles.boldLabel, GUILayout.Width(180));
        GUILayout.Label("Build Cost", EditorStyles.boldLabel, GUILayout.Width(90));
        GUILayout.Label("Description", EditorStyles.boldLabel, GUILayout.MinWidth(180));
        GUILayout.Label("Usage Instruction", EditorStyles.boldLabel, GUILayout.MinWidth(180));
        GUILayout.Label("Ping", EditorStyles.boldLabel, GUILayout.Width(50));

        EditorGUILayout.EndHorizontal();
    }

    private void DrawRows()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        foreach (var row in rows)
        {
            if (row.shelf == null) continue;

            row.serializedObject.Update();

            SerializedProperty categoryProp = row.serializedObject.FindProperty("category");
            SerializedProperty brandProp = row.serializedObject.FindProperty("brand");
            SerializedProperty descriptionProp = row.serializedObject.FindProperty("description");
            SerializedProperty usageProp = row.serializedObject.FindProperty("usageInstruction");
            SerializedProperty buildCostProp = row.serializedObject.FindProperty("buildCost");

            EditorGUILayout.BeginHorizontal("box");

            DrawNameField(row);

            DrawPriceField(row, buildCostProp);

            if (categoryProp != null)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(categoryProp, GUIContent.none, GUILayout.Width(140));
                if (EditorGUI.EndChangeCheck())
                {
                    ApplyRowChanges(row);
                }
            }
            else
            {
                GUILayout.Label("category not found", GUILayout.Width(140));
            }

            if (brandProp != null)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(brandProp, GUIContent.none, GUILayout.Width(180));
                if (EditorGUI.EndChangeCheck())
                {
                    ApplyRowChanges(row);
                }
            }
            else
            {
                GUILayout.Label("brand not found", GUILayout.Width(180));
            }

            if (buildCostProp != null)
            {
                EditorGUI.BeginChangeCheck();
                int newBuildCost = EditorGUILayout.IntField(buildCostProp.intValue, GUILayout.Width(90));
                if (EditorGUI.EndChangeCheck())
                {
                    buildCostProp.intValue = Mathf.Max(0, newBuildCost);
                    ApplyRowChanges(row);
                }
            }
            else
            {
                GUILayout.Label("N/A", GUILayout.Width(90));
            }

            if (descriptionProp != null)
            {
                EditorGUI.BeginChangeCheck();
                string newDesc = EditorGUILayout.TextField(descriptionProp.stringValue, GUILayout.MinWidth(180));
                if (EditorGUI.EndChangeCheck())
                {
                    descriptionProp.stringValue = newDesc;
                    ApplyRowChanges(row);
                }
            }
            else
            {
                GUILayout.Label("description not found", GUILayout.MinWidth(180));
            }

            if (usageProp != null)
            {
                EditorGUI.BeginChangeCheck();
                string newUsage = EditorGUILayout.TextField(usageProp.stringValue, GUILayout.MinWidth(180));
                if (EditorGUI.EndChangeCheck())
                {
                    usageProp.stringValue = newUsage;
                    ApplyRowChanges(row);
                }
            }
            else
            {
                GUILayout.Label("usageInstruction not found", GUILayout.MinWidth(180));
            }

            if (GUILayout.Button("Ping", GUILayout.Width(50)))
            {
                EditorGUIUtility.PingObject(row.shelf);
                Selection.activeObject = row.shelf;
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawNameField(ShelfRow row)
    {
        string currentName = row.shelf.name;

        EditorGUI.BeginChangeCheck();
        string newName = EditorGUILayout.TextField(currentName, GUILayout.Width(180));
        if (EditorGUI.EndChangeCheck())
        {
            if (!string.IsNullOrWhiteSpace(newName) && newName != currentName)
            {
                string path = AssetDatabase.GetAssetPath(row.shelf);
                string error = AssetDatabase.RenameAsset(path, newName);

                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError($"Rename failed: {error}");
                }
                else
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    RefreshData();
                    GUIUtility.ExitGUI();
                }
            }
        }
    }

    private void DrawPriceField(ShelfRow row, SerializedProperty buildCostProp)
    {
        int currentPrice = row.shelf.GetPrice(previewLevel);

        EditorGUI.BeginChangeCheck();
        int newPrice = EditorGUILayout.IntField(currentPrice, GUILayout.Width(100));
        if (EditorGUI.EndChangeCheck())
        {
            if (buildCostProp != null)
            {
                Undo.RecordObject(row.shelf, "Edit Shelf Price");

                // 公式:
                // price = Floor(buildCost * 0.1 + level^3 * 0.2)
                // 反推近似 buildCost:
                // buildCost ≈ (price - level^3 * 0.2) / 0.1
                float levelPart = Mathf.Pow(previewLevel, 3) * 0.2f;
                int newBuildCost = Mathf.RoundToInt((newPrice - levelPart) / 0.1f);
                buildCostProp.intValue = Mathf.Max(0, newBuildCost);

                ApplyRowChanges(row);
            }
        }
    }

    private void ApplyRowChanges(ShelfRow row)
    {
        row.serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(row.shelf);
        AssetDatabase.SaveAssets();
        ApplySorting();
    }

    private void RefreshData()
    {
        rows.Clear();

        if (string.IsNullOrWhiteSpace(rootFolder))
            rootFolder = "Assets";

        string[] guids;

        if (AssetDatabase.IsValidFolder(rootFolder))
        {
            guids = AssetDatabase.FindAssets("t:ShelfArchetype", new[] { rootFolder });
        }
        else
        {
            guids = AssetDatabase.FindAssets("t:ShelfArchetype");
        }

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ShelfArchetype shelf = AssetDatabase.LoadAssetAtPath<ShelfArchetype>(path);
            if (shelf != null)
            {
                rows.Add(new ShelfRow(shelf));
            }
        }

        ApplySorting();
        Repaint();
    }

    private void ApplySorting()
    {
        Func<ShelfRow, object> keySelector = sortField switch
        {
            SortField.Name => r => r.shelf != null ? r.shelf.name : "",
            SortField.Price => r => r.shelf != null ? r.shelf.GetPrice(previewLevel) : 0,
            SortField.Category => r => r.shelf != null ? r.shelf.category.ToString() : "",
            SortField.Brand => r => r.shelf != null && r.shelf.brand != null ? r.shelf.brand.name : "",
            _ => r => r.shelf != null ? r.shelf.name : ""
        };

        rows = sortAscending
            ? rows.OrderBy(keySelector).ToList()
            : rows.OrderByDescending(keySelector).ToList();

        Repaint();
    }
}