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
        private const float MIN_INTEREST_ADD = -100f;
        private const float MAX_INTEREST_ADD = 100f;
        private const float MIN_INTEREST_MUL = 0f;
        private const float MAX_INTEREST_MUL = 5f;

        public static void DrawInterestAddArray(SerializedProperty property, GUIContent label, ref bool foldout)
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
                    property.GetArrayElementAtIndex(i).floatValue = 0f; // 默认为0，表示没有修饰
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
                        $"对 {categoryNames[i]} 类商品的兴趣加成（建议范围 -100 到 +100，0=不修饰）"
                    );

                    // 使用 Slider，范围 -100 到 100
                    element.floatValue = EditorGUILayout.Slider(elementLabel, element.floatValue, MIN_INTEREST_ADD, MAX_INTEREST_ADD);
                }

                EditorGUI.indentLevel--;
            }
        }

        public static void DrawInterestMulArray(SerializedProperty property, GUIContent label, ref bool foldout)
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
                    property.GetArrayElementAtIndex(i).floatValue = 1f; // 默认为1，表示不影响
                }
            }

            // 绘制折叠标题
            foldout = EditorGUILayout.Foldout(foldout, label, true);

            if (foldout)
            {
                EditorGUI.indentLevel++;

                // 绘制每个类别的兴趣乘数
                for (int i = 0; i < categoryCount && i < property.arraySize; i++)
                {
                    SerializedProperty element = property.GetArrayElementAtIndex(i);

                    // 创建带有类别名称的标签
                    GUIContent elementLabel = new GUIContent(
                        categoryNames[i],
                        $"对 {categoryNames[i]} 类商品的兴趣倍率（建议范围 0 到 5，1=不影响）"
                    );

                    // 使用 Slider，范围 0 到 5
                    element.floatValue = EditorGUILayout.Slider(elementLabel, element.floatValue, MIN_INTEREST_MUL, MAX_INTEREST_MUL);
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
        private bool interestMulFoldout = true;

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
                    TraitInterestPropertyDrawer.DrawInterestAddArray(
                        prop,
                        new GUIContent("Interest Modifiers (Add)", "每个商品类别的兴趣加成值"),
                        ref interestAddFoldout
                    );
                }
                // 对 interestMul 数组使用自定义绘制
                else if (prop.name == "interestMul")
                {
                    TraitInterestPropertyDrawer.DrawInterestMulArray(
                        prop,
                        new GUIContent("Interest Multipliers", "每个商品类别的兴趣倍率"),
                        ref interestMulFoldout
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