using UnityEngine;
using UnityEditor;
using PopLife.Customers.Data;
using PopLife.Data;

namespace PopLife.Customers.Editor
{
    [CustomPropertyDrawer(typeof(InterestArray))]
    public class InterestArrayPropertyDrawer : PropertyDrawer
    {
        private bool foldout = true;
        private const int DEFAULT_INTEREST = 2;  // 默认兴趣值
        private const int MIN_INTEREST = 0;      // 最小兴趣值
        private const int MAX_INTEREST = 5;      // 最大兴趣值

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // 获取 values 数组属性
            SerializedProperty valuesProperty = property.FindPropertyRelative("values");

            // 获取所有的 ProductCategory 枚举值
            string[] categoryNames = System.Enum.GetNames(typeof(ProductCategory));
            int categoryCount = categoryNames.Length;

            // 确保数组大小正确
            if (valuesProperty.arraySize != categoryCount)
            {
                valuesProperty.arraySize = categoryCount;
                for (int i = 0; i < categoryCount; i++)
                {
                    if (valuesProperty.GetArrayElementAtIndex(i).intValue == 0)
                    {
                        valuesProperty.GetArrayElementAtIndex(i).intValue = DEFAULT_INTEREST;
                    }
                }
            }

            // 绘制折叠标题
            float lineHeight = EditorGUIUtility.singleLineHeight;
            Rect foldoutRect = new Rect(position.x, position.y, position.width, lineHeight);
            foldout = EditorGUI.Foldout(foldoutRect, foldout, label, true);

            if (foldout)
            {
                EditorGUI.indentLevel++;
                float yPos = position.y + lineHeight + EditorGUIUtility.standardVerticalSpacing;

                // 绘制每个类别的兴趣值
                for (int i = 0; i < categoryCount && i < valuesProperty.arraySize; i++)
                {
                    Rect elementRect = new Rect(
                        position.x,
                        yPos,
                        position.width,
                        lineHeight
                    );

                    SerializedProperty element = valuesProperty.GetArrayElementAtIndex(i);

                    // 创建带有类别名称的标签
                    GUIContent elementLabel = new GUIContent(
                        categoryNames[i],
                        $"对 {categoryNames[i]} 类商品的兴趣等级 (0-5，0=无兴趣，5=极感兴趣)"
                    );

                    // 使用 IntSlider，范围 0-5
                    element.intValue = EditorGUI.IntSlider(elementRect, elementLabel, element.intValue, MIN_INTEREST, MAX_INTEREST);

                    yPos += lineHeight + EditorGUIUtility.standardVerticalSpacing;
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!foldout)
                return EditorGUIUtility.singleLineHeight;

            int categoryCount = System.Enum.GetNames(typeof(ProductCategory)).Length;
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            // 标题行 + 每个类别一行
            return lineHeight + (lineHeight + spacing) * categoryCount;
        }
    }
}