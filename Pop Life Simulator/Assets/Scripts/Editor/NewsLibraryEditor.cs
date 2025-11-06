using UnityEngine;
using UnityEditor;
using PopLife.UI;

namespace PopLife.Editor
{
    /// <summary>
    /// NewsLibrary 自定义编辑器 - 提供更友好的新闻文本编辑界面
    /// </summary>
    [CustomEditor(typeof(NewsLibrary))]
    public class NewsLibraryEditor : UnityEditor.Editor
    {
        private SerializedProperty newsTextsProperty;
        private SerializedProperty minIntervalProperty;
        private SerializedProperty maxIntervalProperty;

        private Vector2 scrollPosition;
        private string newNewsText = "";
        private bool showAddSection = false;

        private void OnEnable()
        {
            newsTextsProperty = serializedObject.FindProperty("newsTexts");
            minIntervalProperty = serializedObject.FindProperty("minInsertInterval");
            maxIntervalProperty = serializedObject.FindProperty("maxInsertInterval");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            NewsLibrary newsLibrary = (NewsLibrary)target;

            // ===== Header =====
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("News Library Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Manage random news texts that will be displayed in the scrolling message bar.\n" +
                "News will be randomly inserted between customer checkout messages.",
                MessageType.Info
            );
            EditorGUILayout.Space(10);

            // ===== Timing Settings =====
            EditorGUILayout.LabelField("Timing Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.PropertyField(minIntervalProperty, new GUIContent("Min Insert Interval (seconds)"));
                EditorGUILayout.PropertyField(maxIntervalProperty, new GUIContent("Max Insert Interval (seconds)"));

                float minInterval = minIntervalProperty.floatValue;
                float maxInterval = maxIntervalProperty.floatValue;
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField($"Random Range: {minInterval:F1}s ~ {maxInterval:F1}s", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);

            // ===== News Texts =====
            EditorGUILayout.LabelField($"News Texts ({newsTextsProperty.arraySize} items)", EditorStyles.boldLabel);

            // Add News Section
            showAddSection = EditorGUILayout.Foldout(showAddSection, "Add New News", true);
            if (showAddSection)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                {
                    EditorGUILayout.LabelField("Enter new news text:", EditorStyles.miniLabel);
                    newNewsText = EditorGUILayout.TextArea(newNewsText, GUILayout.MinHeight(40));

                    EditorGUILayout.BeginHorizontal();
                    {
                        if (GUILayout.Button("Add News", GUILayout.Height(25)))
                        {
                            if (!string.IsNullOrWhiteSpace(newNewsText))
                            {
                                newsTextsProperty.InsertArrayElementAtIndex(newsTextsProperty.arraySize);
                                SerializedProperty newElement = newsTextsProperty.GetArrayElementAtIndex(newsTextsProperty.arraySize - 1);
                                newElement.stringValue = newNewsText.Trim();
                                newNewsText = "";
                                serializedObject.ApplyModifiedProperties();
                                Debug.Log($"[NewsLibraryEditor] Added news: {newElement.stringValue}");
                            }
                            else
                            {
                                EditorUtility.DisplayDialog("Invalid Input", "News text cannot be empty!", "OK");
                            }
                        }

                        if (GUILayout.Button("Clear Input", GUILayout.Height(25)))
                        {
                            newNewsText = "";
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            // Quick Actions
            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("Add Sample News (English)", GUILayout.Height(25)))
                {
                    Undo.RecordObject(newsLibrary, "Add Sample News");
                    AddSampleNews(newsLibrary);
                    EditorUtility.SetDirty(newsLibrary);
                    serializedObject.Update();
                }

                if (GUILayout.Button("Clear All News", GUILayout.Height(25)))
                {
                    if (EditorUtility.DisplayDialog(
                        "Clear All News",
                        $"Are you sure you want to delete all {newsTextsProperty.arraySize} news items?",
                        "Yes, Clear All",
                        "Cancel"
                    ))
                    {
                        newsTextsProperty.ClearArray();
                        serializedObject.ApplyModifiedProperties();
                        Debug.Log("[NewsLibraryEditor] All news cleared");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(10);

            // News List with Scroll
            if (newsTextsProperty.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No news items. Add some news texts above!", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField("Current News Items:", EditorStyles.boldLabel);
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MaxHeight(400));
                {
                    for (int i = 0; i < newsTextsProperty.arraySize; i++)
                    {
                        SerializedProperty newsItem = newsTextsProperty.GetArrayElementAtIndex(i);

                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        {
                            EditorGUILayout.BeginHorizontal();
                            {
                                EditorGUILayout.LabelField($"News #{i + 1}", EditorStyles.boldLabel, GUILayout.Width(60));

                                // Delete Button
                                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                                if (GUILayout.Button("Delete", GUILayout.Width(60), GUILayout.Height(18)))
                                {
                                    if (EditorUtility.DisplayDialog(
                                        "Delete News",
                                        $"Delete this news item?\n\n\"{newsItem.stringValue}\"",
                                        "Delete",
                                        "Cancel"
                                    ))
                                    {
                                        newsTextsProperty.DeleteArrayElementAtIndex(i);
                                        serializedObject.ApplyModifiedProperties();
                                        Debug.Log($"[NewsLibraryEditor] Deleted news #{i + 1}");
                                        break;
                                    }
                                }
                                GUI.backgroundColor = Color.white;

                                // Move Up Button
                                GUI.enabled = i > 0;
                                if (GUILayout.Button("▲", GUILayout.Width(30), GUILayout.Height(18)))
                                {
                                    newsTextsProperty.MoveArrayElement(i, i - 1);
                                    serializedObject.ApplyModifiedProperties();
                                }
                                GUI.enabled = true;

                                // Move Down Button
                                GUI.enabled = i < newsTextsProperty.arraySize - 1;
                                if (GUILayout.Button("▼", GUILayout.Width(30), GUILayout.Height(18)))
                                {
                                    newsTextsProperty.MoveArrayElement(i, i + 1);
                                    serializedObject.ApplyModifiedProperties();
                                }
                                GUI.enabled = true;
                            }
                            EditorGUILayout.EndHorizontal();

                            // News Text Area
                            EditorGUILayout.LabelField("Text:", EditorStyles.miniLabel);
                            newsItem.stringValue = EditorGUILayout.TextArea(newsItem.stringValue, GUILayout.MinHeight(40));

                            // Character Count
                            int charCount = newsItem.stringValue.Length;
                            EditorGUILayout.LabelField($"Characters: {charCount}", EditorStyles.miniLabel);
                        }
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.Space(5);
                    }
                }
                EditorGUILayout.EndScrollView();
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 添加示例新闻（英文）
        /// </summary>
        private void AddSampleNews(NewsLibrary library)
        {
            string[] sampleNews = new string[]
            {
                "Breaking News: Local adult store reports record sales this month",
                "City Council approves new regulations for retail businesses",
                "Economy Update: Consumer spending reaches all-time high",
                "Weather Alert: Clear skies expected for the weekend shopping rush",
                "New Study: Customer satisfaction in retail sector improves by 15%",
                "Tech News: Innovative POS systems revolutionize retail checkout",
                "Market Trend: Online shopping grows but in-store experience remains crucial",
                "Health Notice: Remember to maintain social distancing while shopping",
                "Special Promotion: Weekend sale brings record number of visitors",
                "Industry Report: Adult retail market shows 20% growth this year"
            };

            foreach (string news in sampleNews)
            {
                newsTextsProperty.InsertArrayElementAtIndex(newsTextsProperty.arraySize);
                SerializedProperty newElement = newsTextsProperty.GetArrayElementAtIndex(newsTextsProperty.arraySize - 1);
                newElement.stringValue = news;
            }

            serializedObject.ApplyModifiedProperties();
            Debug.Log($"[NewsLibraryEditor] Added {sampleNews.Length} sample news items");
        }
    }
}
