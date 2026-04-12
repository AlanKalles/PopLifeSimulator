using System.Collections.Generic;
using UnityEngine;

namespace PopLife.Data
{
    /// <summary>
    /// 地板瓦片原型 — 矩形尺寸，自动生成 footprintPattern
    /// 放置到 WorldGrid，内部由 InteriorGrid 管理
    /// </summary>
    [CreateAssetMenu(menuName = "PopLife/Buildings/FloorTileArchetype")]
    public class FloorTileArchetype : BuildingArchetype, IWorldPlaceable
    {
        [Header("地板尺寸")]
        [SerializeField] private Vector2Int tileSize = new(3, 3);

        [Header("Interior")]
        [Tooltip("interior 左下角相对 tile 锚点的世界空间偏移")]
        [SerializeField] private Vector2 interiorOffset;
        [Tooltip("interior 的格子数")]
        [SerializeField] private Vector2Int interiorGridSize = new(4, 4);
        [Tooltip("interior 格子大小")]
        [SerializeField] private float interiorCellSize = 0.5f;

        [Header("Portals")]
        [Tooltip("从左起哪几列为 portal 通道（0=最左列，1=第二列...）。这些列可行走但不可放置建筑。空 = 左侧无 portal。")]
        [SerializeField] private List<int> leftPortalCells = new();
        [Tooltip("从右起哪几列为 portal 通道（0=最右列，1=倒数第二列...）。这些列可行走但不可放置建筑。空 = 右侧无 portal。")]
        [SerializeField] private List<int> rightPortalCells = new();

        [Header("Customer")]
        [Tooltip("底部几行可行走")]
        [SerializeField] private int walkableRows = 1;

        public Vector2Int TileSize => tileSize;

        // 无等级系统
        public override int MaxLevel => 0;
        public override BuildingLevelData GetLevel(int lvl) => null;

        public Vector2 InteriorOffset => interiorOffset;
        public Vector2Int InteriorGridSize => interiorGridSize;
        public float InteriorCellSize => interiorCellSize;
        public IReadOnlyList<int> LeftPortalCells => leftPortalCells;
        public IReadOnlyList<int> RightPortalCells => rightPortalCells;
        public int WalkableRows => walkableRows;

        /// <summary>
        /// 获取考虑旋转后的有效尺寸
        /// </summary>
        public Vector2Int GetEffectiveSize(int rotation)
        {
            if (canRotate && rotation % 2 == 1)
                return new Vector2Int(tileSize.y, tileSize.x);
            return tileSize;
        }

        /// <summary>
        /// WorldGrid 放置验证
        /// </summary>
        public bool ValidateWorldPlacement(PopLife.Runtime.WorldGrid grid, Vector2Int position, int rotation)
        {
            return grid.CanPlaceFloorTile(GetRotatedFootprint(rotation), position);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            canRotate = false;
            GenerateFootprintFromSize();

            if (interiorGridSize.x < 1) interiorGridSize.x = 1;
            if (interiorGridSize.y < 1) interiorGridSize.y = 1;
            if (interiorCellSize < 0.01f) interiorCellSize = 0.5f;
            if (walkableRows < 0) walkableRows = 0;
            leftPortalCells?.RemoveAll(c => c < 0 || c >= interiorGridSize.x);
            rightPortalCells?.RemoveAll(c => c < 0 || c >= interiorGridSize.x);
        }

        private void GenerateFootprintFromSize()
        {
            if (tileSize.x <= 0 || tileSize.y <= 0) return;

            int expectedCount = tileSize.x * tileSize.y;
            if (footprintPattern != null && footprintPattern.Count == expectedCount)
            {
                bool match = true;
                int i = 0;
                for (int x = 0; x < tileSize.x && match; x++)
                    for (int y = 0; y < tileSize.y && match; y++, i++)
                        if (i >= footprintPattern.Count || footprintPattern[i] != new Vector2Int(x, y))
                            match = false;
                if (match) return;
            }

            footprintPattern = new List<Vector2Int>(expectedCount);
            for (int x = 0; x < tileSize.x; x++)
                for (int y = 0; y < tileSize.y; y++)
                    footprintPattern.Add(new Vector2Int(x, y));
        }
#endif
    }
}
