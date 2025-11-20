using UnityEngine;
using UnityEditor;
using PopLife.NarrativeSystem;
using PopLife.Manager;
using System.Collections.Generic;

namespace PopLife.Editor.NarrativeSystem
{
    /// <summary>
    /// 节点属性编辑器弹出窗口
    /// Popup window for editing node properties
    /// </summary>
    public class NarrativeNodeEditor : EditorWindow
    {
        private object nodeData;  // The NodeData being edited (using object to avoid accessing private class)
        private NarrativeGraphEditor parentEditor;

        // Temporary edit values
        private string segmentID;
        private string speakerName;
        private string textContent;
        private Sprite speakerPortrait;
        private bool isEndSegment;
        private float displayDuration;
        private bool isChoiceNode;
        private List<RewardData> endRewards = new List<RewardData>();

        // Scroll position for text area
        private Vector2 scrollPos;

        // End Rewards foldout state
        private bool endRewardsFoldout = true;

        public static void ShowWindow(object node, NarrativeGraphEditor parent)
        {
            var window = CreateInstance<NarrativeNodeEditor>();
            window.nodeData = node;
            window.parentEditor = parent;
            window.LoadNodeData();

            window.titleContent = new GUIContent("Edit Node");
            window.minSize = new Vector2(400, 500);
            window.maxSize = new Vector2(600, 800);

            // Center the window
            var mainWindow = parent;
            if (mainWindow != null)
            {
                var center = mainWindow.position.center;
                window.position = new Rect(center.x - 200, center.y - 250, 400, 500);
            }

            window.ShowUtility();
        }

        private void LoadNodeData()
        {
            // Use reflection to access the segment from NodeData
            var segment = GetSegmentFromNodeData();
            if (segment != null)
            {
                segmentID = segment.SegmentID ?? "";
                speakerName = segment.SpeakerName ?? "";
                textContent = segment.TextContent ?? "";
                speakerPortrait = segment.SpeakerPortrait;
                isEndSegment = segment.IsEndSegment;
                displayDuration = segment.DisplayDuration;
                isChoiceNode = segment.IsChoiceNode;

                // Load end rewards
                endRewards.Clear();
                if (segment.EndRewards != null)
                {
                    foreach (var reward in segment.EndRewards)
                    {
                        endRewards.Add(new RewardData(reward.Type, reward.RewardID, reward.Amount));
                    }
                }
            }
        }

        private NarrativeSegment GetSegmentFromNodeData()
        {
            if (nodeData == null) return null;

            var field = nodeData.GetType().GetField("segment");
            if (field != null)
            {
                return field.GetValue(nodeData) as NarrativeSegment;
            }
            return null;
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Title
            EditorGUILayout.LabelField("Node Properties", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            // Segment ID
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Segment ID:", GUILayout.Width(100));
            segmentID = EditorGUILayout.TextField(segmentID);
            if (GUILayout.Button("Generate", GUILayout.Width(70)))
            {
                // Find next available index for segment ID (SEG01, SEG02, etc.)
                segmentID = GenerateNextSegmentID();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Speaker Name
            EditorGUILayout.LabelField("Speaker Name:");
            speakerName = EditorGUILayout.TextField(speakerName);

            EditorGUILayout.Space();

            // Text Content
            EditorGUILayout.LabelField("Dialogue Text:");
            textContent = EditorGUILayout.TextArea(textContent, GUILayout.Height(200));

            EditorGUILayout.Space();

            // Speaker Portrait
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Speaker Portrait:", GUILayout.Width(100));
            speakerPortrait = (Sprite)EditorGUILayout.ObjectField(speakerPortrait, typeof(Sprite), false);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Properties
            EditorGUILayout.LabelField("Properties", EditorStyles.boldLabel);

            isEndSegment = EditorGUILayout.Toggle("Is End Segment", isEndSegment);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Display Duration:", GUILayout.Width(100));
            displayDuration = EditorGUILayout.FloatField(displayDuration);
            EditorGUILayout.LabelField("seconds (0 = manual)", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // End Segment Rewards - 只有当 IsEndSegment 勾选时才显示
            // Only show when IsEndSegment is checked
            if (isEndSegment)
            {
                DrawEndRewardsSection();
            }

            EditorGUILayout.Space();

            // Help text
            EditorGUILayout.HelpBox(
                "• Segment ID: Unique identifier for this dialogue segment\n" +
                "• Speaker Name: Who is speaking (shown above dialogue)\n" +
                "• Dialogue Text: The actual conversation text\n" +
                "• Speaker Portrait: Optional portrait override for this segment\n" +
                "• Is End Segment: Check if this ends the conversation\n" +
                "• Display Duration: Auto-advance time (0 = player controls)\n" +
                "• End Rewards: Rewards given when this end segment is reached",
                MessageType.Info);

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            // Buttons
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.5f, 0.8f, 0.5f);
            if (GUILayout.Button("Save", GUILayout.Height(30)))
            {
                SaveChanges();
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Cancel", GUILayout.Height(30)))
            {
                Close();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void SaveChanges()
        {
            var segment = GetSegmentFromNodeData();
            if (segment != null)
            {
                // Update segment properties
                segment.SetSegmentData(segmentID, speakerName, textContent);
                segment.SpeakerPortrait = speakerPortrait;
                segment.IsEndSegment = isEndSegment;
                segment.DisplayDuration = displayDuration;

                // Save end rewards
                if (isEndSegment && endRewards.Count > 0)
                {
                    segment.EndRewards = endRewards.ToArray();
                }
                else
                {
                    segment.EndRewards = null;
                }

                // Refresh the parent editor
                if (parentEditor != null)
                {
                    parentEditor.RefreshNode(nodeData);
                }

                Close();
            }
        }

        private void OnDestroy()
        {
            // Ensure parent editor repaints when this window closes
            if (parentEditor != null)
            {
                parentEditor.Repaint();
            }
        }

        /// <summary>
        /// Generate next available segment ID
        /// </summary>
        private string GenerateNextSegmentID()
        {
            // Get all existing segment IDs from parent editor
            var existingIDs = new HashSet<string>();

            if (parentEditor != null)
            {
                // Use reflection to access nodes from parent editor
                var nodesField = parentEditor.GetType().GetField("nodes",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (nodesField != null)
                {
                    var nodesList = nodesField.GetValue(parentEditor) as System.Collections.IList;
                    if (nodesList != null)
                    {
                        foreach (var node in nodesList)
                        {
                            var segmentField = node.GetType().GetField("segment");
                            if (segmentField != null)
                            {
                                var segment = segmentField.GetValue(node) as NarrativeSegment;
                                if (segment != null && !string.IsNullOrEmpty(segment.SegmentID))
                                {
                                    existingIDs.Add(segment.SegmentID);
                                }
                            }
                        }
                    }
                }
            }

            // Find next available index
            int nextIndex = 1;
            while (existingIDs.Contains(NarrativeSegment.GenerateSegmentID(nextIndex)))
            {
                nextIndex++;
            }

            return NarrativeSegment.GenerateSegmentID(nextIndex);
        }

        /// <summary>
        /// Draw the End Rewards section
        /// </summary>
        private void DrawEndRewardsSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("End Segment Rewards", EditorStyles.boldLabel);

            endRewardsFoldout = EditorGUILayout.Foldout(endRewardsFoldout, $"Rewards ({endRewards.Count})", true);

            if (endRewardsFoldout)
            {
                EditorGUI.indentLevel++;

                // Draw each reward
                for (int i = 0; i < endRewards.Count; i++)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Element {i}", EditorStyles.boldLabel);

                    // Remove button
                    GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                    if (GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        endRewards.RemoveAt(i);
                        GUIUtility.ExitGUI();
                        return;
                    }
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();

                    // Get current reward values using reflection
                    var reward = endRewards[i];
                    var typeField = typeof(RewardData).GetField("type", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var rewardIDField = typeof(RewardData).GetField("rewardID", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var amountField = typeof(RewardData).GetField("amount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var markerField = typeof(RewardData).GetField("marker", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    // Type
                    var currentType = (RewardData.RewardType)typeField.GetValue(reward);
                    var newType = (RewardData.RewardType)EditorGUILayout.EnumPopup("Type", currentType);
                    typeField.SetValue(reward, newType);

                    // Reward ID
                    var currentRewardID = (string)rewardIDField.GetValue(reward) ?? "";
                    var newRewardID = EditorGUILayout.TextField("Reward ID", currentRewardID);
                    rewardIDField.SetValue(reward, newRewardID);

                    // Amount
                    var currentAmount = (int)amountField.GetValue(reward);
                    var newAmount = EditorGUILayout.IntField("Amount", currentAmount);
                    amountField.SetValue(reward, newAmount);

                    // Marker (only for Marker type)
                    if (newType == RewardData.RewardType.Marker)
                    {
                        var currentMarker = markerField.GetValue(reward);
                        var newMarker = EditorGUILayout.EnumPopup("Marker", (TutorialMarker)currentMarker);
                        markerField.SetValue(reward, newMarker);
                    }

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }

                EditorGUI.indentLevel--;

                // Add button
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUI.backgroundColor = new Color(0.5f, 0.8f, 0.5f);
                if (GUILayout.Button("+ Add Reward", GUILayout.Width(100)))
                {
                    endRewards.Add(new RewardData(RewardData.RewardType.Money, "", 0));
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}