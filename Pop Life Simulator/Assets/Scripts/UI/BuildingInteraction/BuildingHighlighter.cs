using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using PopLife.Runtime;

namespace PopLife.UI.BuildingInteraction
{
    /// <summary>
    /// Building Highlighter - Draws outline around buildings using LineRenderer
    /// 建筑高亮器 - 使用 LineRenderer 绘制建筑轮廓
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class BuildingHighlighter : MonoBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] private Color highlightColor = new Color(0.3f, 0.9f, 1f, 1f); // Cyan
        [SerializeField] private float lineWidth = 0.1f;
        [SerializeField] private float zOffset = -0.1f; // Slightly above buildings
        [SerializeField] private Vector2 positionOffset = new Vector2(-0.5f, -0.5f); // Offset to wrap around buildings (left, down)

        [Header("Animation")]
        [SerializeField] private float fadeInDuration = 0.1f;
        [SerializeField] private float fadeOutDuration = 0.1f;

        private LineRenderer lineRenderer;
        private FloorManager floorManager;
        private Material lineMaterial;

        // Animation state
        private bool isVisible = false;
        private float fadeProgress = 0f;

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            ConfigureLineRenderer();

            // Get FloorManager
            floorManager = FindFirstObjectByType<FloorManager>();
            if (floorManager == null)
            {
                Debug.LogWarning("BuildingHighlighter: FloorManager not found!");
            }
        }

        /// <summary>
        /// Configure LineRenderer settings
        /// 配置 LineRenderer 设置
        /// </summary>
        private void ConfigureLineRenderer()
        {
            lineRenderer.loop = true;
            lineRenderer.useWorldSpace = true;
            lineRenderer.sortingLayerName = "UI"; // Ensure it renders above buildings
            lineRenderer.sortingOrder = 100;

            // Set width
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;

            // Create material if needed
            if (lineRenderer.material == null || lineRenderer.sharedMaterial.name == "Default-Material")
            {
                lineMaterial = new Material(Shader.Find("Sprites/Default"));
                lineMaterial.color = highlightColor;
                lineRenderer.material = lineMaterial;
            }
            else
            {
                lineMaterial = lineRenderer.material;
                lineMaterial.color = highlightColor;
            }

            // Start hidden
            lineRenderer.enabled = false;
        }

        /// <summary>
        /// Show outline for a building
        /// 显示建筑轮廓
        /// </summary>
        public void Show(BuildingInstance building)
        {
            if (building == null || building.archetype == null)
            {
                Debug.LogWarning("BuildingHighlighter: Invalid building!");
                return;
            }

            // Get floor
            FloorGrid floor = GetFloorForBuilding(building);
            if (floor == null)
            {
                Debug.LogWarning($"BuildingHighlighter: Cannot find floor {building.floorId}");
                return;
            }

            // Calculate outline vertices
            List<Vector3> vertices = CalculateOutlineVertices(building, floor);

            if (vertices.Count < 3)
            {
                Debug.LogWarning("BuildingHighlighter: Not enough vertices for outline!");
                return;
            }

            // Set line renderer positions
            lineRenderer.positionCount = vertices.Count;
            lineRenderer.SetPositions(vertices.ToArray());

            // Show line renderer
            lineRenderer.enabled = true;
            isVisible = true;
            fadeProgress = 1f;

            // Update color with fade
            UpdateLineColor();
        }

        /// <summary>
        /// Hide outline
        /// 隐藏轮廓
        /// </summary>
        public void Hide()
        {
            lineRenderer.enabled = false;
            isVisible = false;
            fadeProgress = 0f;
        }

        /// <summary>
        /// Get floor for building
        /// 获取建筑所在楼层
        /// </summary>
        private FloorGrid GetFloorForBuilding(BuildingInstance building)
        {
            if (floorManager == null) return null;

            // Try to find floor with matching ID
            var floors = floorManager.GetAllActiveFloors();
            foreach (var floor in floors)
            {
                if (floor.floorId == building.floorId)
                {
                    return floor;
                }
            }

            return null;
        }

        /// <summary>
        /// Calculate outline vertices from occupied cells
        /// 从占用格子计算外轮廓顶点
        /// </summary>
        private List<Vector3> CalculateOutlineVertices(BuildingInstance building, FloorGrid floor)
        {
            // 1. Get absolute grid positions
            List<Vector2Int> cells = GetAbsoluteCells(building);

            // 2. Build edge set (4 edges per cell)
            HashSet<Edge> edges = new HashSet<Edge>();
            foreach (var cell in cells)
            {
                AddCellEdges(edges, cell);
            }

            // 3. Remove internal shared edges (keep only boundary)
            RemoveInternalEdges(edges, cells);

            // 4. Sort and connect edges into a loop
            List<Vector2Int> gridLoop = ConnectEdgesIntoLoop(edges);

            // 5. Convert to world coordinates
            List<Vector3> worldVertices = new List<Vector3>();
            foreach (var gridPos in gridLoop)
            {
                Vector3 worldPos = floor.GridToWorld(gridPos);

                // Apply position offset (left/down adjustment to wrap around buildings)
                // 应用位置偏移（左/下调整以包裹建筑物）
                worldPos.x += positionOffset.x;
                worldPos.y += positionOffset.y;

                // Set Z offset to render above buildings
                worldPos.z = zOffset;

                worldVertices.Add(worldPos);
            }

            return worldVertices;
        }

        /// <summary>
        /// Get absolute grid positions (building position + rotated footprint)
        /// 获取绝对网格坐标（建筑位置 + 旋转后占地）
        /// </summary>
        private List<Vector2Int> GetAbsoluteCells(BuildingInstance building)
        {
            List<Vector2Int> relativeCells = building.archetype.GetRotatedFootprint(building.rotation);
            List<Vector2Int> absoluteCells = new List<Vector2Int>();

            foreach (var offset in relativeCells)
            {
                absoluteCells.Add(building.gridPosition + offset);
            }

            return absoluteCells;
        }

        /// <summary>
        /// Add 4 edges for a cell (bottom, right, top, left)
        /// 为格子添加4条边（下、右、上、左）
        /// </summary>
        private void AddCellEdges(HashSet<Edge> edges, Vector2Int cell)
        {
            // Cell corners (clockwise from bottom-left)
            // 格子四角（从左下角顺时针）
            Vector2Int bottomLeft = cell;
            Vector2Int bottomRight = new Vector2Int(cell.x + 1, cell.y);
            Vector2Int topRight = new Vector2Int(cell.x + 1, cell.y + 1);
            Vector2Int topLeft = new Vector2Int(cell.x, cell.y + 1);

            // Add 4 edges
            edges.Add(new Edge(bottomLeft, bottomRight));  // Bottom
            edges.Add(new Edge(bottomRight, topRight));    // Right
            edges.Add(new Edge(topRight, topLeft));        // Top
            edges.Add(new Edge(topLeft, bottomLeft));      // Left
        }

        /// <summary>
        /// Remove internal edges (edges shared by 2 cells)
        /// 移除内部边（被2个格子共享的边）
        /// </summary>
        private void RemoveInternalEdges(HashSet<Edge> edges, List<Vector2Int> cells)
        {
            HashSet<Vector2Int> cellSet = new HashSet<Vector2Int>(cells);
            List<Edge> toRemove = new List<Edge>();

            foreach (var edge in edges)
            {
                // Check if this edge is internal (both sides have cells)
                // 检查这条边是否是内部边（两侧都有格子）
                if (IsEdgeInternal(edge, cellSet))
                {
                    toRemove.Add(edge);
                }
            }

            // Remove internal edges
            foreach (var edge in toRemove)
            {
                edges.Remove(edge);
            }
        }

        /// <summary>
        /// Check if an edge is internal (both sides have cells)
        /// 检查边是否是内部边（两侧都有格子）
        /// </summary>
        private bool IsEdgeInternal(Edge edge, HashSet<Vector2Int> cells)
        {
            // Determine edge direction
            Vector2Int diff = edge.end - edge.start;

            if (diff.x == 1 && diff.y == 0) // Horizontal edge (bottom of cells)
            {
                Vector2Int cellAbove = new Vector2Int(edge.start.x, edge.start.y);
                Vector2Int cellBelow = new Vector2Int(edge.start.x, edge.start.y - 1);
                return cells.Contains(cellAbove) && cells.Contains(cellBelow);
            }
            else if (diff.x == 0 && diff.y == 1) // Vertical edge (left of cells)
            {
                Vector2Int cellRight = new Vector2Int(edge.start.x, edge.start.y);
                Vector2Int cellLeft = new Vector2Int(edge.start.x - 1, edge.start.y);
                return cells.Contains(cellRight) && cells.Contains(cellLeft);
            }

            return false;
        }

        /// <summary>
        /// Connect edges into a loop
        /// 将边连接成环
        /// </summary>
        private List<Vector2Int> ConnectEdgesIntoLoop(HashSet<Edge> edges)
        {
            if (edges.Count == 0)
            {
                return new List<Vector2Int>();
            }

            // Build adjacency list (each vertex connects to 2 neighbors)
            // 构建邻接表（每个顶点连接到2个邻居）
            Dictionary<Vector2Int, List<Vector2Int>> adjacency = new Dictionary<Vector2Int, List<Vector2Int>>();

            foreach (var edge in edges)
            {
                // Add bidirectional connections
                if (!adjacency.ContainsKey(edge.start))
                {
                    adjacency[edge.start] = new List<Vector2Int>();
                }
                if (!adjacency.ContainsKey(edge.end))
                {
                    adjacency[edge.end] = new List<Vector2Int>();
                }

                adjacency[edge.start].Add(edge.end);
                adjacency[edge.end].Add(edge.start);
            }

            // Start from any vertex
            Vector2Int startVertex = adjacency.Keys.First();
            List<Vector2Int> loop = new List<Vector2Int>();
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

            Vector2Int current = startVertex;
            Vector2Int previous = current; // Track previous to avoid backtracking

            loop.Add(current);
            visited.Add(current);

            // Traverse the loop
            while (true)
            {
                var neighbors = adjacency[current];
                Vector2Int next = default;

                // Find unvisited neighbor (or start vertex to close loop)
                foreach (var neighbor in neighbors)
                {
                    if (!visited.Contains(neighbor))
                    {
                        next = neighbor;
                        break;
                    }
                    else if (neighbor == startVertex && loop.Count > 2)
                    {
                        // Found start vertex, loop is closed
                        return loop;
                    }
                }

                if (next == default)
                {
                    // No more unvisited neighbors, loop complete
                    break;
                }

                loop.Add(next);
                visited.Add(next);
                previous = current;
                current = next;
            }

            return loop;
        }

        /// <summary>
        /// Update line color with fade effect
        /// 更新线条颜色（带淡入淡出效果）
        /// </summary>
        private void UpdateLineColor()
        {
            Color color = highlightColor;
            color.a = fadeProgress;

            if (lineMaterial != null)
            {
                lineMaterial.color = color;
            }
        }

        private void Update()
        {
            // Simple fade animation (can be enhanced with DOTween if needed)
            // 简单的淡入淡出动画（如需要可使用DOTween增强）
            if (isVisible && fadeProgress < 1f)
            {
                fadeProgress += Time.deltaTime / fadeInDuration;
                fadeProgress = Mathf.Clamp01(fadeProgress);
                UpdateLineColor();
            }
            else if (!isVisible && fadeProgress > 0f)
            {
                fadeProgress -= Time.deltaTime / fadeOutDuration;
                fadeProgress = Mathf.Clamp01(fadeProgress);
                UpdateLineColor();

                if (fadeProgress <= 0f)
                {
                    lineRenderer.enabled = false;
                }
            }
        }
    }

    /// <summary>
    /// Represents an edge between two grid vertices
    /// 表示两个网格顶点之间的边
    /// </summary>
    public struct Edge : System.IEquatable<Edge>
    {
        public Vector2Int start;
        public Vector2Int end;

        public Edge(Vector2Int a, Vector2Int b)
        {
            // Ensure consistent ordering for hash comparison
            // 确保顺序一致以便哈希比较
            if (a.x < b.x || (a.x == b.x && a.y < b.y))
            {
                start = a;
                end = b;
            }
            else
            {
                start = b;
                end = a;
            }
        }

        public bool Equals(Edge other)
        {
            return start == other.start && end == other.end;
        }

        public override int GetHashCode()
        {
            return System.HashCode.Combine(start, end);
        }

        public override bool Equals(object obj)
        {
            return obj is Edge edge && Equals(edge);
        }
    }
}
