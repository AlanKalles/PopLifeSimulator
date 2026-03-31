using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace PopLife.Test
{
    /// <summary>
    /// 测试用常驻网格线框 — 双层 Tilemap（线框 + 状态叠加）
    /// 所有 tile 运行时生成，不需要美术资产
    /// </summary>
    public class TestGridOverlay : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private TestFloorGrid grid;

        [Header("线框设置")]
        [SerializeField] private Color gridLineColor = new(1f, 1f, 1f, 0.15f);
        [SerializeField] private Color gridLineBuildModeColor = new(1f, 1f, 1f, 0.3f);

        [Header("状态叠加颜色")]
        [SerializeField] private Color occupiedColor = new(0.3f, 0.9f, 0.3f, 0.2f);
        [SerializeField] private Color elevatorColor = new(1f, 0.85f, 0.2f, 0.25f);
        [SerializeField] private Color validPreviewColor = new(0.3f, 1f, 0.3f, 0.4f);
        [SerializeField] private Color invalidPreviewColor = new(1f, 0.3f, 0.3f, 0.4f);

        // Tilemap 层
        private Tilemap gridLineTilemap;
        private Tilemap overlayTilemap;

        // 运行时生成的 Tile
        private Tile gridLineTile;
        private Tile occupiedTile;
        private Tile elevatorTile;
        private Tile validTile;
        private Tile invalidTile;

        // 预览状态
        private readonly List<Vector3Int> previewCells = new();

        private bool overlayVisible = true;

        void Start()
        {
            // 自动对齐到 FloorGrid 的 origin
            if (grid != null)
                transform.position = grid.OriginPos;

            CreateTilemaps();
            CreateTiles();

            if (grid != null)
            {
                grid.OnFloorChanged += Refresh;
                Refresh();
            }
        }

        void OnDestroy()
        {
            if (grid != null)
                grid.OnFloorChanged -= Refresh;
        }

        // ========== 初始化 ==========

        private void CreateTilemaps()
        {
            // 确保父物体有 Grid 组件
            var gridComponent = GetComponent<Grid>();
            if (gridComponent == null)
                gridComponent = gameObject.AddComponent<Grid>();
            gridComponent.cellSize = new Vector3(grid ? grid.cellSize : 1f, grid ? grid.cellSize : 1f, 0);

            // 线框层（常驻，低 sorting order）
            var lineGo = new GameObject("GridLines");
            lineGo.transform.SetParent(transform, false);
            gridLineTilemap = lineGo.AddComponent<Tilemap>();
            gridLineTilemap.tileAnchor = Vector3.zero; // pivot 对齐左下角
            var lineRenderer = lineGo.AddComponent<TilemapRenderer>();
            lineRenderer.sortingLayerName = "Default";
            lineRenderer.sortingOrder = -5;

            // 状态叠加层（可切换，高 sorting order）
            var overlayGo = new GameObject("Overlay");
            overlayGo.transform.SetParent(transform, false);
            overlayTilemap = overlayGo.AddComponent<Tilemap>();
            overlayTilemap.tileAnchor = Vector3.zero; // pivot 对齐左下角
            var overlayRenderer = overlayGo.AddComponent<TilemapRenderer>();
            overlayRenderer.sortingLayerName = "Default";
            overlayRenderer.sortingOrder = 5;
        }

        private void CreateTiles()
        {
            int ppu = 16;

            gridLineTile = CreateTileFromSprite(GenerateGridLineSprite(ppu, gridLineColor));
            occupiedTile = CreateTileFromSprite(GenerateSolidSprite(ppu, occupiedColor));
            elevatorTile = CreateTileFromSprite(GenerateSolidSprite(ppu, elevatorColor));
            validTile = CreateTileFromSprite(GenerateSolidSprite(ppu, validPreviewColor));
            invalidTile = CreateTileFromSprite(GenerateSolidSprite(ppu, invalidPreviewColor));
        }

        // ========== 刷新 ==========

        /// <summary> 刷新全部（地板/建筑/电梯变化时调用） </summary>
        public void Refresh()
        {
            if (grid == null) return;

            RefreshGridLines();
            RefreshOverlay();
        }

        private void RefreshGridLines()
        {
            gridLineTilemap.ClearAllTiles();

            foreach (var cell in grid.FloorCells)
            {
                gridLineTilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), gridLineTile);
            }
        }

        private void RefreshOverlay()
        {
            if (!overlayVisible) return;

            // 保留预览格子，只清除非预览的
            var preservePreview = new HashSet<Vector3Int>(previewCells);

            overlayTilemap.ClearAllTiles();

            // 建筑占用
            foreach (var cell in grid.BuildingCells)
            {
                var tilePos = new Vector3Int(cell.x, cell.y, 0);
                if (preservePreview.Contains(tilePos)) continue;

                if (grid.IsElevatorAt(cell))
                    overlayTilemap.SetTile(tilePos, elevatorTile);
                else
                    overlayTilemap.SetTile(tilePos, occupiedTile);
            }

            // 恢复预览
            foreach (var pos in previewCells)
            {
                var currentTile = overlayTilemap.GetTile(pos);
                if (currentTile == null)
                {
                    // 预览 tile 在 Refresh 后可能被清除，需要重设
                    // 但我们不知道当时的 valid 状态，先跳过
                    // ShowPreview 会在下一帧重新设置
                }
            }
        }

        // ========== 状态叠加控制 ==========

        /// <summary> 切换状态叠加层显隐 </summary>
        public void SetOverlayVisible(bool visible)
        {
            overlayVisible = visible;
            overlayTilemap.gameObject.SetActive(visible);
            if (visible) RefreshOverlay();
        }

        /// <summary> 设置建造模式（调整线框透明度） </summary>
        public void SetBuildMode(bool buildMode)
        {
            gridLineTilemap.color = buildMode ? gridLineBuildModeColor : Color.white;

            // 建造模式下线框 tile 使用更高透明度
            if (buildMode)
            {
                gridLineTile.sprite = GenerateGridLineSprite(16, gridLineBuildModeColor);
            }
            else
            {
                gridLineTile.sprite = GenerateGridLineSprite(16, gridLineColor);
            }
            gridLineTilemap.RefreshAllTiles();

            SetOverlayVisible(buildMode);
        }

        // ========== 放置预览 ==========

        /// <summary> 显示放置预览 </summary>
        public void ShowPreview(List<Vector2Int> footprint, Vector2Int origin, bool valid)
        {
            ClearPreview();

            var tile = valid ? validTile : invalidTile;
            foreach (var off in footprint)
            {
                var pos = new Vector3Int(origin.x + off.x, origin.y + off.y, 0);
                overlayTilemap.SetTile(pos, tile);
                previewCells.Add(pos);
            }
        }

        /// <summary> 清除放置预览 </summary>
        public void ClearPreview()
        {
            foreach (var pos in previewCells)
            {
                overlayTilemap.SetTile(pos, null);
            }
            previewCells.Clear();

            // 恢复被覆盖的状态 tile
            RefreshOverlay();
        }

        // ========== Tile 生成工具 ==========

        private static Tile CreateTileFromSprite(Sprite sprite)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.color = Color.white;
            return tile;
        }

        /// <summary> 生成线框 sprite：四边细线，内部透明 </summary>
        private static Sprite GenerateGridLineSprite(int ppu, Color lineColor)
        {
            int size = ppu;
            var tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Point;

            var clear = Color.clear;
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isBorder = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                    pixels[y * size + x] = isBorder ? lineColor : clear;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0f, 0f), ppu);
        }

        /// <summary> 生成纯色半透明 sprite </summary>
        private static Sprite GenerateSolidSprite(int ppu, Color color)
        {
            int size = ppu;
            var tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Point;

            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0f, 0f), ppu);
        }
    }
}
