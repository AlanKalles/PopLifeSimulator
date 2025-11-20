using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using PopLife.NarrativeSystem;

namespace PopLife.Editor.NarrativeSystem
{
    /// <summary>
    /// 叙事对话图形编辑器窗口
    /// Visual node-based editor for narrative dialogues
    /// </summary>
    public class NarrativeGraphEditor : EditorWindow
    {
        // Window properties
        private const float GRID_SIZE = 20f;
        private const float NODE_WIDTH = 200f;
        private const float NODE_HEIGHT = 100f;
        private const float CONNECTION_WIDTH = 2f;

        // Current editing target
        private NarrativeSO currentNarrative;
        private List<NodeData> nodes = new List<NodeData>();
        private List<Connection> connections = new List<Connection>();

        // Editor state
        private Vector2 scrollPos;
        private Vector2 drag;
        private NodeData selectedNode;
        private NodeData connectingFromNode;
        private bool isConnecting;
        private bool isDraggingCanvas;
        private string searchQuery = "";
        private List<NodeData> searchResults = new List<NodeData>();

        // Styles
        private GUIStyle nodeStyle;
        private GUIStyle selectedNodeStyle;
        private GUIStyle rootNodeStyle;
        private GUIStyle choiceNodeStyle;
        private GUIStyle titleStyle;

        /// <summary>
        /// Node data wrapper for visual representation
        /// </summary>
        [Serializable]
        private class NodeData
        {
            public NarrativeSegment segment;
            public Rect rect;
            public bool isRoot;
            public string displayName;
            public int depth;  // For auto-layout

            public NodeData(NarrativeSegment seg, Vector2 position, bool root = false)
            {
                segment = seg;
                rect = new Rect(position.x, position.y, NODE_WIDTH, NODE_HEIGHT);
                isRoot = root;
                UpdateDisplayName();
            }

            public void UpdateDisplayName()
            {
                if (segment == null) return;

                displayName = string.IsNullOrEmpty(segment.SegmentID)
                    ? "Unnamed"
                    : segment.SegmentID;

                // Add preview of text (shorter for better display)
                if (!string.IsNullOrEmpty(segment.TextContent))
                {
                    string preview = segment.TextContent.Replace("\n", " ");
                    if (preview.Length > 20)
                        preview = preview.Substring(0, 20) + "...";
                    displayName += "\n" + preview;
                }
            }
        }

        /// <summary>
        /// Connection between nodes
        /// </summary>
        private class Connection
        {
            public NodeData from;
            public NodeData to;
            public int childIndex;  // Which next segment index

            public Connection(NodeData f, NodeData t, int index)
            {
                from = f;
                to = t;
                childIndex = index;
            }
        }

        [MenuItem("Window/PopLife/Narrative Graph Editor")]
        public static void OpenWindow()
        {
            var window = GetWindow<NarrativeGraphEditor>("Narrative Graph");
            window.minSize = new Vector2(800, 600);
        }

        private bool stylesInitialized = false;

        private void OnEnable()
        {
            // 不在这里初始化样式，因为 GUI 系统可能还没准备好
            // 样式将在 OnGUI 中延迟初始化
            stylesInitialized = false;
        }

        private void InitializeStyles()
        {
            // Base node style
            nodeStyle = new GUIStyle("box");
            nodeStyle.normal.background = CreateColorTexture(new Color(0.2f, 0.2f, 0.2f, 0.9f));
            nodeStyle.border = new RectOffset(12, 12, 12, 12);
            nodeStyle.padding = new RectOffset(10, 10, 10, 10);
            nodeStyle.normal.textColor = Color.white;
            nodeStyle.alignment = TextAnchor.UpperCenter;
            nodeStyle.wordWrap = true;
            nodeStyle.fontSize = 11;

            selectedNodeStyle = new GUIStyle(nodeStyle);
            selectedNodeStyle.normal.background = CreateColorTexture(new Color(0.3f, 0.5f, 0.3f, 0.9f));

            rootNodeStyle = new GUIStyle(nodeStyle);
            rootNodeStyle.normal.background = CreateColorTexture(new Color(0.3f, 0.3f, 0.5f, 0.9f));

            // 选择节点样式 - 橙色背景表示多重选择节点
            // Choice node style - orange background for multiple choice nodes
            choiceNodeStyle = new GUIStyle(nodeStyle);
            choiceNodeStyle.normal.background = CreateColorTexture(new Color(0.6f, 0.4f, 0.2f, 0.9f));

            titleStyle = new GUIStyle(EditorStyles.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.normal.textColor = Color.white;
        }

        private Texture2D CreateColorTexture(Color color)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        private void OnGUI()
        {
            // 延迟初始化样式，确保 GUI 系统已准备好
            if (!stylesInitialized)
            {
                InitializeStyles();
                stylesInitialized = true;
            }

            DrawToolbar();
            DrawGrid();

            if (currentNarrative == null)
            {
                DrawNoNarrativeMessage();
                return;
            }

            // Begin scrollable area
            scrollPos = GUI.BeginScrollView(new Rect(0, 30, position.width, position.height - 30),
                scrollPos, new Rect(-1000, -1000, 3000, 3000));

            // Draw in correct order
            DrawConnections();
            DrawNodes();
            ProcessEvents(Event.current);

            // Draw connection preview while connecting
            if (isConnecting && connectingFromNode != null)
            {
                DrawConnectionPreview(Event.current.mousePosition);
            }

            GUI.EndScrollView();

            // Repaint if needed
            if (GUI.changed) Repaint();
        }

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Narrative selector
            var newNarrative = EditorGUILayout.ObjectField(currentNarrative, typeof(NarrativeSO), false,
                GUILayout.Width(200)) as NarrativeSO;

            if (newNarrative != currentNarrative)
            {
                LoadNarrative(newNarrative);
            }

            GUILayout.Space(10);

            if (currentNarrative != null)
            {
                if (GUILayout.Button("Auto Layout", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    AutoLayoutNodes();
                }

                if (GUILayout.Button("Add Node", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    AddNewNode();
                }

                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    SaveNarrative();
                }

                GUILayout.Space(10);

                GUILayout.Label($"Nodes: {nodes.Count}", EditorStyles.toolbarButton);

                GUILayout.Space(20);

                // Search field
                GUILayout.Label("Search:", EditorStyles.toolbarButton);
                string newSearchQuery = EditorGUILayout.TextField(searchQuery, EditorStyles.toolbarTextField, GUILayout.Width(150));
                if (newSearchQuery != searchQuery)
                {
                    searchQuery = newSearchQuery;
                    PerformSearch();
                }

                if (!string.IsNullOrEmpty(searchQuery) && searchResults.Count > 0)
                {
                    if (GUILayout.Button($"Next ({searchResults.Count})", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    {
                        CycleSearchResults();
                    }
                }
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Help", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                ShowHelp();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawGrid()
        {
            int widthDivs = Mathf.CeilToInt(position.width / GRID_SIZE);
            int heightDivs = Mathf.CeilToInt(position.height / GRID_SIZE);

            Handles.BeginGUI();
            Handles.color = new Color(0.3f, 0.3f, 0.3f, 0.1f);

            // Vertical lines
            for (int i = 0; i < widthDivs; i++)
            {
                Handles.DrawLine(
                    new Vector3(GRID_SIZE * i, 30, 0),
                    new Vector3(GRID_SIZE * i, position.height, 0)
                );
            }

            // Horizontal lines
            for (int j = 0; j < heightDivs; j++)
            {
                Handles.DrawLine(
                    new Vector3(0, GRID_SIZE * j + 30, 0),
                    new Vector3(position.width, GRID_SIZE * j + 30, 0)
                );
            }

            Handles.color = Color.white;
            Handles.EndGUI();
        }

        private void DrawNodes()
        {
            foreach (var node in nodes)
            {
                DrawNode(node);
            }
        }

        private void DrawNode(NodeData node)
        {
            // Ensure styles are initialized
            if (nodeStyle == null)
                InitializeStyles();

            // Choose style - 优先级：选中 > 选择节点 > 根节点 > 普通
            // Priority: selected > choice node > root > normal
            GUIStyle style = nodeStyle;
            if (node == selectedNode)
                style = selectedNodeStyle;
            else if (node.segment != null && node.segment.IsChoiceNode)
                style = choiceNodeStyle;
            else if (node.isRoot)
                style = rootNodeStyle;

            // Build node content - keep it simple to avoid layout issues
            string nodeContent = "";

            // Root indicator at top
            if (node.isRoot)
            {
                nodeContent = "[ROOT]\n";
            }

            // 选择节点标记
            // Choice node indicator
            if (node.segment != null && node.segment.IsChoiceNode)
            {
                nodeContent += "[CHOICE]\n";
            }

            // Main content
            nodeContent += node.displayName ?? "Unnamed";

            // Connection indicator at bottom (simplified)
            if (node.segment != null && node.segment.NextSegments != null && node.segment.NextSegments.Count > 1)
            {
                nodeContent += $"\n[{node.segment.NextSegments.Count} branches]";
            }

            GUI.Box(node.rect, nodeContent, style);

            // Draw connection points
            DrawConnectionPoints(node);
        }

        private void DrawConnectionPoints(NodeData node)
        {
            // Output point (right side)
            Rect outputRect = new Rect(
                node.rect.x + node.rect.width - 10,
                node.rect.y + node.rect.height / 2 - 5,
                10, 10
            );

            if (GUI.Button(outputRect, "", GUIStyle.none))
            {
                StartConnection(node);
            }

            // Draw visual indicator
            EditorGUI.DrawRect(outputRect, Color.yellow);

            // Input point (left side)
            if (!node.isRoot)  // Root doesn't have input
            {
                Rect inputRect = new Rect(
                    node.rect.x,
                    node.rect.y + node.rect.height / 2 - 5,
                    10, 10
                );

                if (GUI.Button(inputRect, "", GUIStyle.none))
                {
                    if (isConnecting && connectingFromNode != null)
                    {
                        CompleteConnection(node);
                    }
                }

                // Draw visual indicator
                EditorGUI.DrawRect(inputRect, Color.cyan);
            }
        }

        private void DrawConnections()
        {
            foreach (var connection in connections)
            {
                DrawConnection(connection);
            }
        }

        private void DrawConnection(Connection connection)
        {
            if (connection.from == null || connection.to == null) return;

            Vector3 startPos = new Vector3(
                connection.from.rect.x + connection.from.rect.width,
                connection.from.rect.y + connection.from.rect.height / 2,
                0
            );

            Vector3 endPos = new Vector3(
                connection.to.rect.x,
                connection.to.rect.y + connection.to.rect.height / 2,
                0
            );

            // Highlight connections for selected node
            Color connectionColor = Color.white;
            float connectionWidth = CONNECTION_WIDTH;

            // 选择节点的分支使用不同颜色
            // Use different colors for choice node branches
            if (connection.from.segment != null && connection.from.segment.IsChoiceNode)
            {
                // 为不同选择分支使用不同颜色：红、绿、蓝
                // Different colors for different choice branches: red, green, blue
                switch (connection.childIndex)
                {
                    case 0:
                        connectionColor = new Color(1f, 0.4f, 0.4f);  // 红色 - Choice 1
                        break;
                    case 1:
                        connectionColor = new Color(0.4f, 1f, 0.4f);  // 绿色 - Choice 2
                        break;
                    case 2:
                        connectionColor = new Color(0.4f, 0.7f, 1f);  // 蓝色 - Choice 3
                        break;
                    default:
                        connectionColor = new Color(1f, 0.8f, 0.4f);  // 橙色 - 其他
                        break;
                }
                connectionWidth = CONNECTION_WIDTH * 1.5f;
            }
            else if (selectedNode != null)
            {
                if (connection.from == selectedNode)
                {
                    connectionColor = new Color(0.2f, 1f, 0.2f);  // Green for outgoing
                    connectionWidth = CONNECTION_WIDTH * 1.5f;
                }
                else if (connection.to == selectedNode)
                {
                    connectionColor = new Color(0.2f, 0.8f, 1f);  // Cyan for incoming
                    connectionWidth = CONNECTION_WIDTH * 1.5f;
                }
                else
                {
                    connectionColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);  // Dim others
                }
            }

            // Draw bezier curve
            Handles.DrawBezier(
                startPos, endPos,
                startPos + Vector3.right * 50,
                endPos + Vector3.left * 50,
                connectionColor,
                null,
                connectionWidth
            );

            // Draw arrow
            Handles.color = connectionColor;
            DrawArrow(endPos, Vector3.left);
            Handles.color = Color.white;
        }

        private void DrawConnectionPreview(Vector2 mousePos)
        {
            Vector3 startPos = new Vector3(
                connectingFromNode.rect.x + connectingFromNode.rect.width,
                connectingFromNode.rect.y + connectingFromNode.rect.height / 2,
                0
            );

            Handles.DrawBezier(
                startPos, mousePos,
                startPos + Vector3.right * 50,
                new Vector3(mousePos.x - 50, mousePos.y, 0),
                new Color(1, 1, 0, 0.5f),
                null,
                CONNECTION_WIDTH
            );
        }

        private void DrawArrow(Vector3 position, Vector3 direction)
        {
            Handles.color = Color.white;
            Vector3 arrowPoint = position + direction * 5;
            Vector3 arrowUp = Quaternion.Euler(0, 0, 30) * direction * 10;
            Vector3 arrowDown = Quaternion.Euler(0, 0, -30) * direction * 10;

            Handles.DrawLine(position, position + arrowUp);
            Handles.DrawLine(position, position + arrowDown);
        }

        private void ProcessEvents(Event e)
        {
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0)  // Left click
                    {
                        NodeData clickedNode = GetNodeAtPosition(e.mousePosition);
                        if (clickedNode != null)
                        {
                            // Check for double click
                            if (e.clickCount == 2)
                            {
                                EditNode(clickedNode);
                            }
                            else
                            {
                                SelectNode(clickedNode);
                            }
                            GUI.changed = true;
                        }
                        else
                        {
                            // Start canvas drag
                            isDraggingCanvas = true;
                            drag = e.mousePosition;
                        }
                    }
                    else if (e.button == 1)  // Right click
                    {
                        ProcessContextMenu(e.mousePosition);
                    }
                    break;

                case EventType.MouseUp:
                    isDraggingCanvas = false;
                    break;

                case EventType.MouseDrag:
                    if (e.button == 0)
                    {
                        if (selectedNode != null && !isDraggingCanvas)
                        {
                            // Drag selected node
                            selectedNode.rect.position += e.delta;
                            e.Use();
                        }
                        else if (isDraggingCanvas)
                        {
                            // Drag canvas
                            scrollPos -= e.delta;
                            e.Use();
                        }
                    }
                    break;

                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.Delete && selectedNode != null)
                    {
                        DeleteSelectedNode();
                        e.Use();
                    }
                    break;
            }
        }

        private void ProcessContextMenu(Vector2 mousePos)
        {
            NodeData node = GetNodeAtPosition(mousePos);
            GenericMenu menu = new GenericMenu();

            if (node != null)
            {
                menu.AddItem(new GUIContent("Edit Node"), false, () => EditNode(node));
                menu.AddItem(new GUIContent("Duplicate"), false, () => DuplicateNode(node));

                if (!node.isRoot)
                {
                    menu.AddItem(new GUIContent("Delete"), false, () => DeleteNode(node));
                }

                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Set as Root"), false, () => SetAsRoot(node));
            }
            else
            {
                menu.AddItem(new GUIContent("Add Node Here"), false, () => AddNodeAt(mousePos));
                menu.AddItem(new GUIContent("Auto Layout"), false, AutoLayoutNodes);
            }

            menu.ShowAsContext();
        }

        private NodeData GetNodeAtPosition(Vector2 pos)
        {
            foreach (var node in nodes)
            {
                if (node.rect.Contains(pos))
                    return node;
            }
            return null;
        }

        private void LoadNarrative(NarrativeSO narrative)
        {
            currentNarrative = narrative;
            nodes.Clear();
            connections.Clear();

            if (narrative == null || narrative.NarrativeSequence == null) return;

            // Build node graph from narrative data
            Dictionary<NarrativeSegment, NodeData> segmentToNode = new Dictionary<NarrativeSegment, NodeData>();

            // Create root node
            var rootSegment = narrative.NarrativeSequence.RootSegment;
            if (rootSegment != null)
            {
                var rootNode = new NodeData(rootSegment, new Vector2(100, 200), true);
                nodes.Add(rootNode);
                segmentToNode[rootSegment] = rootNode;

                // Recursively create child nodes
                CreateChildNodes(rootSegment, rootNode, segmentToNode, 1);
            }

            // Build connections list
            RebuildConnections();

            // Auto layout if many nodes
            if (nodes.Count > 5)
            {
                AutoLayoutNodes();
            }
        }

        private void CreateChildNodes(NarrativeSegment segment, NodeData parentNode,
            Dictionary<NarrativeSegment, NodeData> segmentToNode, int depth)
        {
            if (segment.NextSegments == null) return;

            for (int i = 0; i < segment.NextSegments.Count; i++)
            {
                var nextSegment = segment.NextSegments[i];
                if (nextSegment == null) continue;

                // Check if node already exists (avoid duplicates in case of loops)
                if (!segmentToNode.ContainsKey(nextSegment))
                {
                    Vector2 position = new Vector2(
                        parentNode.rect.x + 300,
                        parentNode.rect.y + (i * 120) - ((segment.NextSegments.Count - 1) * 60)
                    );

                    var childNode = new NodeData(nextSegment, position, false);
                    childNode.depth = depth;
                    nodes.Add(childNode);
                    segmentToNode[nextSegment] = childNode;

                    // Recursive
                    CreateChildNodes(nextSegment, childNode, segmentToNode, depth + 1);
                }
            }
        }

        private void RebuildConnections()
        {
            connections.Clear();

            foreach (var node in nodes)
            {
                if (node.segment?.NextSegments != null)
                {
                    for (int i = 0; i < node.segment.NextSegments.Count; i++)
                    {
                        var nextSegment = node.segment.NextSegments[i];
                        var targetNode = nodes.FirstOrDefault(n => n.segment == nextSegment);

                        if (targetNode != null)
                        {
                            connections.Add(new Connection(node, targetNode, i));
                        }
                    }
                }
            }
        }

        private void AutoLayoutNodes()
        {
            if (nodes.Count == 0) return;

            // Group nodes by depth
            Dictionary<int, List<NodeData>> depthGroups = new Dictionary<int, List<NodeData>>();

            foreach (var node in nodes)
            {
                if (!depthGroups.ContainsKey(node.depth))
                    depthGroups[node.depth] = new List<NodeData>();
                depthGroups[node.depth].Add(node);
            }

            // Layout each depth level
            float xOffset = 100;
            foreach (var depth in depthGroups.Keys.OrderBy(k => k))
            {
                var nodesAtDepth = depthGroups[depth];
                float yOffset = (position.height - 30) / 2 - (nodesAtDepth.Count * 120) / 2;

                for (int i = 0; i < nodesAtDepth.Count; i++)
                {
                    nodesAtDepth[i].rect.position = new Vector2(
                        xOffset + depth * 300,
                        yOffset + i * 120
                    );
                }
            }

            GUI.changed = true;
        }

        private void AddNewNode()
        {
            Vector2 newPosition = new Vector2(scrollPos.x + this.position.width / 2, scrollPos.y + this.position.height / 2);
            AddNodeAt(newPosition);
        }

        private void AddNodeAt(Vector2 position)
        {
            // Generate next available segment ID
            string nextSegmentID = GetNextAvailableSegmentID();
            var newSegment = new NarrativeSegment(nextSegmentID, "Speaker", "New dialogue text here...");
            var newNode = new NodeData(newSegment, position, false);
            nodes.Add(newNode);

            SelectNode(newNode);
            EditNode(newNode);
        }

        private void SelectNode(NodeData node)
        {
            selectedNode = node;
            Selection.activeObject = currentNarrative;  // Keep narrative selected for inspector
        }

        private void EditNode(NodeData node)
        {
            // Open a popup window to edit node properties
            NarrativeNodeEditor.ShowWindow(node, this);
        }

        private void DuplicateNode(NodeData node)
        {
            var duplicatedSegment = new NarrativeSegment(
                node.segment.SegmentID + "_copy",
                node.segment.SpeakerName,
                node.segment.TextContent
            );

            var newNode = new NodeData(duplicatedSegment, node.rect.position + new Vector2(50, 50), false);
            nodes.Add(newNode);
            SelectNode(newNode);
        }

        private void DeleteNode(NodeData node)
        {
            if (node.isRoot)
            {
                EditorUtility.DisplayDialog("Cannot Delete", "Cannot delete the root node!", "OK");
                return;
            }

            // Remove all connections to/from this node
            connections.RemoveAll(c => c.from == node || c.to == node);

            // Remove from parent segments
            foreach (var otherNode in nodes)
            {
                otherNode.segment?.RemoveNextSegment(node.segment);
            }

            nodes.Remove(node);

            if (selectedNode == node)
                selectedNode = null;

            GUI.changed = true;
        }

        private void DeleteSelectedNode()
        {
            if (selectedNode != null)
                DeleteNode(selectedNode);
        }

        private void SetAsRoot(NodeData node)
        {
            // Clear previous root
            foreach (var n in nodes)
                n.isRoot = false;

            node.isRoot = true;

            // Update narrative sequence
            if (currentNarrative?.NarrativeSequence != null)
            {
                currentNarrative.NarrativeSequence.Initialize(node.segment);
            }

            GUI.changed = true;
        }

        private void StartConnection(NodeData fromNode)
        {
            isConnecting = true;
            connectingFromNode = fromNode;
        }

        private void CompleteConnection(NodeData toNode)
        {
            if (connectingFromNode == null || toNode == null) return;

            // Prevent self-connection
            if (connectingFromNode == toNode)
            {
                isConnecting = false;
                connectingFromNode = null;
                return;
            }

            // Add to next segments
            if (!connectingFromNode.segment.NextSegments.Contains(toNode.segment))
            {
                connectingFromNode.segment.AddNextSegment(toNode.segment);
                RebuildConnections();
            }

            isConnecting = false;
            connectingFromNode = null;
            GUI.changed = true;
        }

        private void SaveNarrative()
        {
            if (currentNarrative == null) return;

            // Find root node
            var rootNode = nodes.FirstOrDefault(n => n.isRoot);
            if (rootNode != null && currentNarrative.NarrativeSequence != null)
            {
                currentNarrative.NarrativeSequence.Initialize(rootNode.segment);
            }

            EditorUtility.SetDirty(currentNarrative);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Saved", "Narrative saved successfully!", "OK");
        }

        private void DrawNoNarrativeMessage()
        {
            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUILayout.Label("Select a NarrativeSO asset to edit", EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }

        private void ShowHelp()
        {
            EditorUtility.DisplayDialog("Narrative Graph Editor Help",
                "• Left Click: Select/Drag nodes\n" +
                "• Right Click: Context menu\n" +
                "• Delete Key: Delete selected node\n" +
                "• Yellow dot: Output connection point\n" +
                "• Cyan dot: Input connection point\n" +
                "• Click output then input to connect nodes\n" +
                "• Auto Layout: Automatically arrange nodes\n" +
                "• Root node (blue) is the starting point\n" +
                "• Choice node (orange) has multiple branches\n" +
                "  - Red line: Choice 1\n" +
                "  - Green line: Choice 2\n" +
                "  - Blue line: Choice 3",
                "OK");
        }

        // Allow other editors to refresh this window
        public void RefreshNode(object nodeData)
        {
            // Cast to NodeData internally
            if (nodeData != null && nodeData is NodeData node)
            {
                node.UpdateDisplayName();
                GUI.changed = true;
                Repaint();
            }
        }

        private void PerformSearch()
        {
            searchResults.Clear();

            if (string.IsNullOrEmpty(searchQuery)) return;

            string query = searchQuery.ToLower();

            foreach (var node in nodes)
            {
                bool matches = false;

                // Search in segment ID
                if (node.segment.SegmentID != null && node.segment.SegmentID.ToLower().Contains(query))
                    matches = true;

                // Search in speaker name
                if (node.segment.SpeakerName != null && node.segment.SpeakerName.ToLower().Contains(query))
                    matches = true;

                // Search in text content
                if (node.segment.TextContent != null && node.segment.TextContent.ToLower().Contains(query))
                    matches = true;

                if (matches)
                    searchResults.Add(node);
            }

            // Select first result if any
            if (searchResults.Count > 0)
            {
                SelectNode(searchResults[0]);
                FocusOnNode(searchResults[0]);
            }
        }

        private void CycleSearchResults()
        {
            if (searchResults.Count == 0) return;

            int currentIndex = searchResults.IndexOf(selectedNode);
            int nextIndex = (currentIndex + 1) % searchResults.Count;

            SelectNode(searchResults[nextIndex]);
            FocusOnNode(searchResults[nextIndex]);
        }

        private void FocusOnNode(NodeData node)
        {
            // Center the scroll position on the node
            scrollPos = node.rect.center - position.size / 2;
            GUI.changed = true;
            Repaint();
        }

        /// <summary>
        /// Get next available segment ID
        /// </summary>
        private string GetNextAvailableSegmentID()
        {
            var existingIDs = new HashSet<string>();

            // Collect all existing segment IDs
            foreach (var node in nodes)
            {
                if (node.segment != null && !string.IsNullOrEmpty(node.segment.SegmentID))
                {
                    existingIDs.Add(node.segment.SegmentID);
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
    }
}