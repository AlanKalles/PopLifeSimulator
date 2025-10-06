using UnityEngine;
using UnityEditor;
using PopLife.Runtime;

namespace PopLife.Editor
{
    [CustomPropertyDrawer(typeof(FloorEntry))]
    public class FloorEntryDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // 计算各个字段的位置
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            Rect rect = new Rect(position.x, position.y, position.width, lineHeight);

            // 绘制折叠标题
            property.isExpanded = EditorGUI.Foldout(rect, property.isExpanded, label, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                // Floor 字段
                rect.y += lineHeight + spacing;
                var floorProp = property.FindPropertyRelative("floor");
                EditorGUI.PropertyField(rect, floorProp);

                // isActive 字段
                rect.y += lineHeight + spacing;
                var isActiveProp = property.FindPropertyRelative("isActive");
                EditorGUI.PropertyField(rect, isActiveProp);

                // 显示只读的 FloorId（从 FloorGrid 读取）
                rect.y += lineHeight + spacing;
                Object floorObject = floorProp.objectReferenceValue;
                int floorId = -1;
                if (floorObject != null && floorObject is FloorGrid floorGrid)
                {
                    floorId = floorGrid.floorId;
                }

                EditorGUI.BeginDisabledGroup(true);
                EditorGUI.IntField(rect, "Floor ID (只读)", floorId);
                EditorGUI.EndDisabledGroup();

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            // 折叠标题 + floor + isActive + floorId（只读）
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            return lineHeight * 4 + spacing * 3;
        }
    }
}
