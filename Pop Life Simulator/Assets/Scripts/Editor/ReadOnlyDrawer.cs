using UnityEngine;
using UnityEditor;

namespace PopLife.Runtime
{
    // PropertyDrawer for the ReadOnly attribute
    [CustomPropertyDrawer(typeof(FloorEntry.ReadOnlyAttribute))]
    public class ReadOnlyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // 保存当前GUI启用状态
            bool previousEnabled = GUI.enabled;

            // 禁用GUI（使字段只读）
            GUI.enabled = false;

            // 绘制属性字段
            EditorGUI.PropertyField(position, property, label, true);

            // 恢复GUI状态
            GUI.enabled = previousEnabled;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }
}