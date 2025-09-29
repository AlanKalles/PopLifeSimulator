using UnityEngine;
using UnityEditor;
using PopLife.Data;

namespace PopLife.Customers.Editor
{
    [CustomPropertyDrawer(typeof(PopLife.Customers.Data.Trait))]
    public class TraitEditor : UnityEditor.Editor
    {
        // 这个类被用作 CustomEditor，而不是 PropertyDrawer
    }

    // 为 Trait 中的 interestAdd 数组创建专门的 PropertyDrawer
    public class TraitInterestPropertyDrawer
    {
        private const int MIN_INTEREST_ADD = -5;
        private const int MAX_INTEREST_ADD = 5;

        public static void DrawInterestArray(SerializedProperty property, GUIContent label, ref bool foldout)
        {
            // 获取所有的 ProductCategory 枚举值
            string[] categoryNames = System.Enum.GetNames(typeof(ProductCategory));
            int categoryCount = categoryNames.Length;

            // 确保数组大小正确
            if (property.arraySize != categoryCount)
            {
                property.arraySize = categoryCount;
                for (int i = 0; i < categoryCount; i++)
                {
                    property.GetArrayElementAtIndex(i).intValue = 0; // 默认为0，表示没有修饰
                }
            }

            // 绘制折叠标题
            foldout = EditorGUILayout.Foldout(foldout, label, true);

            if (foldout)
            {
                EditorGUI.indentLevel++;

                // 绘制每个类别的兴趣修饰值
                for (int i = 0; i < categoryCount && i < property.arraySize; i++)
                {
                    SerializedProperty element = property.GetArrayElementAtIndex(i);

                    // 创建带有类别名称的标签
                    GUIContent elementLabel = new GUIContent(
                        categoryNames[i],
                        $"对 {categoryNames[i]} 类商品的兴趣修饰 (-5 到 +5，0=不修饰)"
                    );

                    // 使用 IntSlider，范围 -5 到 5
                    element.intValue = EditorGUILayout.IntSlider(elementLabel, element.intValue, MIN_INTEREST_ADD, MAX_INTEREST_ADD);
                }

                EditorGUI.indentLevel--;
            }
        }
    }

    // 为整个 Trait ScriptableObject 创建自定义 Inspector
    [CustomEditor(typeof(PopLife.Customers.Data.Trait))]
    public class TraitInspector : UnityEditor.Editor
    {
        private bool interestAddFoldout = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 绘制默认的属性
            SerializedProperty prop = serializedObject.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;

                // 对 interestAdd 数组使用自定义绘制
                if (prop.name == "interestAdd")
                {
                    TraitInterestPropertyDrawer.DrawInterestArray(
                        prop,
                        new GUIContent("Interest Modifiers", "每个商品类别的兴趣修饰值"),
                        ref interestAddFoldout
                    );
                }
                else if (prop.name != "m_Script")
                {
                    EditorGUILayout.PropertyField(prop, true);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}