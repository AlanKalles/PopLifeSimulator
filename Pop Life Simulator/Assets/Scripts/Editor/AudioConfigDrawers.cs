using UnityEngine;
using UnityEditor;
using PopLife.Data;

namespace PopLife.Editor
{
    /// <summary>
    /// 音效配置条目的自定义绘制器 - 显示键名而非 Element 0/1/2
    /// 支持多个AudioClip的编辑
    /// </summary>
    [CustomPropertyDrawer(typeof(AudioConfigSO.SoundClipEntry))]
    public class SoundClipEntryDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // 获取属性
            SerializedProperty keyProp = property.FindPropertyRelative("key");
            SerializedProperty clipsProp = property.FindPropertyRelative("clips");

            // 使用键名作为显示标签，如果为空则显示默认标签
            string displayName = string.IsNullOrEmpty(keyProp.stringValue)
                ? "(未命名音效)"
                : keyProp.stringValue;

            // 显示音频数量提示
            int clipCount = clipsProp != null ? clipsProp.arraySize : 0;
            if (clipCount > 1)
            {
                displayName += $" [{clipCount} variants]";
            }

            // 绘制折叠标题
            property.isExpanded = EditorGUI.Foldout(
                new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
                property.isExpanded,
                displayName,
                true
            );

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                // 绘制 Key 字段
                EditorGUI.PropertyField(
                    new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight),
                    keyProp,
                    new GUIContent("Key", "音效键（如 Build_Condom, BuildingMoved）")
                );

                y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                // 绘制 Clips 数组字段（使用默认的数组绘制器）
                float clipsHeight = EditorGUI.GetPropertyHeight(clipsProp, true);
                EditorGUI.PropertyField(
                    new Rect(position.x, y, position.width, clipsHeight),
                    clipsProp,
                    new GUIContent("Audio Clips", "音效文件列表（多个时随机播放）"),
                    true // includeChildren
                );

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            // 折叠标题 + Key + Clips数组
            SerializedProperty clipsProp = property.FindPropertyRelative("clips");
            float clipsHeight = EditorGUI.GetPropertyHeight(clipsProp, true);

            return EditorGUIUtility.singleLineHeight * 2
                   + EditorGUIUtility.standardVerticalSpacing * 2
                   + clipsHeight;
        }
    }

    /// <summary>
    /// 背景音乐配置条目的自定义绘制器 - 显示键名而非 Element 0/1/2
    /// 支持多个AudioClip的编辑
    /// </summary>
    [CustomPropertyDrawer(typeof(AudioConfigSO.MusicClipEntry))]
    public class MusicClipEntryDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // 获取属性
            SerializedProperty keyProp = property.FindPropertyRelative("key");
            SerializedProperty clipsProp = property.FindPropertyRelative("clips");
            SerializedProperty loopProp = property.FindPropertyRelative("loop");
            SerializedProperty volumeProp = property.FindPropertyRelative("volume");

            // 使用键名作为显示标签
            string displayName = string.IsNullOrEmpty(keyProp.stringValue)
                ? "(未命名音乐)"
                : keyProp.stringValue;

            // 显示音频数量提示
            int clipCount = clipsProp != null ? clipsProp.arraySize : 0;
            if (clipCount > 1)
            {
                displayName += $" [{clipCount} variants]";
            }

            // 绘制折叠标题
            property.isExpanded = EditorGUI.Foldout(
                new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
                property.isExpanded,
                displayName,
                true
            );

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                // Key
                EditorGUI.PropertyField(
                    new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight),
                    keyProp,
                    new GUIContent("Key", "音乐键（如 BGM_Shop, BGM_Menu）")
                );
                y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                // Clips 数组
                float clipsHeight = EditorGUI.GetPropertyHeight(clipsProp, true);
                EditorGUI.PropertyField(
                    new Rect(position.x, y, position.width, clipsHeight),
                    clipsProp,
                    new GUIContent("Audio Clips", "音乐文件列表（多个时随机播放）"),
                    true // includeChildren
                );
                y += clipsHeight + EditorGUIUtility.standardVerticalSpacing;

                // Loop
                EditorGUI.PropertyField(
                    new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight),
                    loopProp,
                    new GUIContent("Loop", "是否循环播放")
                );
                y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                // Volume
                EditorGUI.PropertyField(
                    new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight),
                    volumeProp,
                    new GUIContent("Volume", "音量（0-1）")
                );

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            // 折叠标题 + Key + Clips数组 + Loop + Volume
            SerializedProperty clipsProp = property.FindPropertyRelative("clips");
            float clipsHeight = EditorGUI.GetPropertyHeight(clipsProp, true);

            return EditorGUIUtility.singleLineHeight * 4
                   + EditorGUIUtility.standardVerticalSpacing * 4
                   + clipsHeight;
        }
    }
}
