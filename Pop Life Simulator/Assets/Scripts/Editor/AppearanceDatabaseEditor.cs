using UnityEngine;
using UnityEditor;
using PopLife.Customers.Data;

namespace PopLife.Editor
{
    [CustomEditor(typeof(AppearanceDatabase))]
    public class AppearanceDatabaseEditor : UnityEditor.Editor
    {
        private SerializedProperty appearancesProperty;

        private void OnEnable()
        {
            appearancesProperty = serializedObject.FindProperty("appearances");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("顾客外貌数据库", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("存储顾客外貌 ID 与 Sprite 的映射关系。每个条目的 ID 应唯一。", MessageType.Info);
            EditorGUILayout.Space();

            // 防御性检查
            if (appearancesProperty == null)
            {
                EditorGUILayout.HelpBox("无法访问 appearances 属性，请尝试重新导入资产。", MessageType.Error);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            // 显示数组大小控制
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(appearancesProperty, new GUIContent("外貌条目"), true);
            EditorGUILayout.EndHorizontal();

            // 统计信息
            if (appearancesProperty.arraySize > 0)
            {
                EditorGUILayout.Space();
                int validCount = 0;
                int emptyIdCount = 0;
                int missingSpriteCount = 0;

                for (int i = 0; i < appearancesProperty.arraySize; i++)
                {
                    var element = appearancesProperty.GetArrayElementAtIndex(i);
                    if (element != null)
                    {
                        var idProp = element.FindPropertyRelative("id");
                        var spriteProp = element.FindPropertyRelative("sprite");

                        if (idProp != null && spriteProp != null)
                        {
                            bool hasId = !string.IsNullOrEmpty(idProp.stringValue);
                            bool hasSprite = spriteProp.objectReferenceValue != null;

                            if (hasId && hasSprite)
                                validCount++;
                            if (!hasId)
                                emptyIdCount++;
                            if (!hasSprite)
                                missingSpriteCount++;
                        }
                    }
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("统计信息", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"总条目数: {appearancesProperty.arraySize}");
                EditorGUILayout.LabelField($"完整条目: {validCount}");
                if (emptyIdCount > 0)
                    EditorGUILayout.LabelField($"缺少ID: {emptyIdCount}", new GUIStyle(EditorStyles.label) { normal = { textColor = Color.yellow } });
                if (missingSpriteCount > 0)
                    EditorGUILayout.LabelField($"缺少Sprite: {missingSpriteCount}", new GUIStyle(EditorStyles.label) { normal = { textColor = Color.yellow } });
                EditorGUILayout.EndVertical();

                // 清理按钮
                EditorGUILayout.Space();
                if (GUILayout.Button("清理空条目和无效数据"))
                {
                    CleanupInvalidEntries();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void CleanupInvalidEntries()
        {
            var db = target as AppearanceDatabase;
            if (db == null || db.appearances == null)
                return;

            int originalCount = db.appearances.Length;
            var validEntries = new System.Collections.Generic.List<AppearanceDatabase.AppearanceEntry>();

            foreach (var entry in db.appearances)
            {
                if (entry != null)
                {
                    // 确保 id 不为 null
                    if (entry.id == null)
                        entry.id = "";

                    // 保留所有非 null 条目（即使 ID 或 Sprite 为空）
                    validEntries.Add(entry);
                }
            }

            int removedCount = originalCount - validEntries.Count;
            if (removedCount > 0)
            {
                Undo.RecordObject(db, "清理 AppearanceDatabase 无效条目");
                db.appearances = validEntries.ToArray();
                EditorUtility.SetDirty(db);
                Debug.Log($"[AppearanceDatabase] 已清理 {removedCount} 个 null 条目");
            }
            else
            {
                Debug.Log("[AppearanceDatabase] 未发现需要清理的条目");
            }
        }
    }
}
