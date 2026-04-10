using UnityEngine;
using Pathfinding;
using NodeCanvas.BehaviourTrees;
using System;
using System.Collections;
using PopLife.Runtime;

namespace PopLife.AlanBot
{
    /// <summary>
    /// AlanBot核心控制器（单例）
    /// 管理A*图形绑定、行为树暂停/恢复、DayLoop事件订阅、ES3持久化
    /// 根节点始终位于 InteriorGrid walkable cell 中心，host 归属用 hostFloorTile + hostLocalPos 表达
    /// </summary>
    public class AlanBotController : MonoBehaviour
    {
        public static AlanBotController Instance;

        [Header("组件引用")]
        [SerializeField] private AlanBotAnimator animator;

        // A*组件（自动获取）
        private AILerp aiLerp;
        private Seeker seeker;

        // NodeCanvas行为树
        private BehaviourTreeOwner behaviourTreeOwner;

        // 当前绑定的A*图形索引
        [HideInInspector] public uint boundGraphIndex;

        // 状态标记
        [HideInInspector] public bool isInteracting;
        [HideInInspector] public bool isBeingMoved;

        // Host FloorTile 追踪
        private FloorTileInstance hostFloorTile;
        private Vector2Int hostLocalPos;
        private Vector3 lastHostInteriorOrigin;
        private string pendingHostTileId;
        private Coroutine pendingRebindCoroutine;

        // ES3持久化
        private const string ES3_KEY_POSITION = "alanBotPosition";
        private const string ES3_KEY_HOST_TILE_ID = "alanBotHostTileId";
        private const string ES3_FILE = "AlanBot.es3";

        // 公开属性
        public AILerp AiLerp => aiLerp;
        public AlanBotAnimator Animator => animator;
        public FloorTileInstance HostFloorTile => hostFloorTile;
        public Vector2Int HostLocalPos => hostLocalPos;

        private void Awake()
        {
            Instance = this;

            aiLerp = GetComponent<AILerp>();
            seeker = GetComponent<Seeker>();
            behaviourTreeOwner = GetComponent<BehaviourTreeOwner>();

            LoadPosition();
        }

        private void OnEnable()
        {
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBuildPhaseStart += OnBuildPhaseStart;
                DayLoopManager.Instance.OnStoreOpen += OnStoreOpen;
            }
        }

        private void OnDisable()
        {
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBuildPhaseStart -= OnBuildPhaseStart;
                DayLoopManager.Instance.OnStoreOpen -= OnStoreOpen;
            }

            if (WorldGrid.Instance != null)
                WorldGrid.Instance.OnStructureChanged -= OnWorldStructureChanged;
        }

        private void Start()
        {
            StartCoroutine(InitializeAfterWorldGridReady());

            // 如果当前是建造阶段，暂停行为树
            if (DayLoopManager.Instance != null &&
                DayLoopManager.Instance.currentPhase == GamePhase.BuildPhase)
            {
                PauseBehavior();
            }
        }

        /// <summary>
        /// 延迟一帧初始化，确保 WorldGrid.Start() 和 NavigationService 完成
        /// </summary>
        private IEnumerator InitializeAfterWorldGridReady()
        {
            yield return null;

            ResolveHostTile();
            BindToNearestGraph();

            if (WorldGrid.Instance != null)
                WorldGrid.Instance.OnStructureChanged += OnWorldStructureChanged;
        }

        // ─── Host Tile 管理 ───

        /// <summary>
        /// 设置 host FloorTile（放置完成时调用）
        /// </summary>
        public void SetHostFloorTile(FloorTileInstance tile, Vector2Int localPos)
        {
            hostFloorTile = tile;
            hostLocalPos = localPos;
            lastHostInteriorOrigin = (tile != null && tile.Interior != null)
                ? tile.Interior.OriginWorld : Vector3.zero;
        }

        /// <summary>
        /// 从持久化数据或当前位置解析 host tile
        /// </summary>
        private void ResolveHostTile()
        {
            var wg = WorldGrid.Instance;
            if (wg == null) return;

            FloorTileInstance tile = null;

            // 1. 优先从存档 ID 解析
            if (!string.IsNullOrEmpty(pendingHostTileId))
                tile = wg.GetFloorTileById(pendingHostTileId);

            // 2. fallback：从当前位置推断
            if (tile == null)
            {
                var gridPos = wg.WorldToGrid(transform.position);
                tile = wg.GetFloorTileAt(gridPos);
            }

            // 3. fallback：找 default tile
            if (tile == null)
            {
                foreach (var t in wg.AllFloorTiles())
                {
                    if (t.IsDefault)
                    {
                        tile = t;
                        break;
                    }
                }
            }

            if (tile == null)
            {
                Debug.LogWarning("[AlanBotController] 无法解析 host tile，AlanBot 保持原位");
                return;
            }

            // 解析本地坐标并对齐到 cell 中心
            if (tile.Interior != null)
            {
                hostFloorTile = tile;
                hostLocalPos = tile.Interior.WorldToLocal(transform.position);

                // 确保本地坐标在合法 walkable 区域内
                if (!tile.Interior.InBounds(hostLocalPos) || !tile.Interior.IsWalkable(hostLocalPos))
                    hostLocalPos = FindWalkableCell(tile);

                SnapToCellCenter(tile.Interior, hostLocalPos);
                lastHostInteriorOrigin = tile.Interior.OriginWorld;
            }
            else
            {
                hostFloorTile = tile;
            }

            pendingHostTileId = null;
        }

        /// <summary>
        /// 响应 WorldGrid 结构变化（tile 拆除/移动/新增 → graph 重建）
        /// </summary>
        private void OnWorldStructureChanged()
        {
            // 1. 检查 host tile 是否被销毁（Unity fake-null）
            if (hostFloorTile == null)
            {
                RelocateToDefaultTile();
            }
            else if (hostFloorTile.Interior != null)
            {
                // 2. 检查 host tile 是否被移动（origin 变化）
                Vector3 currentOrigin = hostFloorTile.Interior.OriginWorld;
                if (currentOrigin != lastHostInteriorOrigin)
                {
                    Vector3 newCenter = hostFloorTile.Interior.LocalToWorld(hostLocalPos);
                    float half = hostFloorTile.Interior.CellSize * 0.5f;
                    transform.position = new Vector3(newCenter.x + half, newCenter.y + half, 0f);
                    lastHostInteriorOrigin = currentOrigin;
                    SavePosition();
                }
            }

            // 3. 延迟一帧重绑 A*（防重复，确保 NavigationService 先完成重建）
            if (pendingRebindCoroutine != null)
                StopCoroutine(pendingRebindCoroutine);
            pendingRebindCoroutine = StartCoroutine(DelayedRebindGraph());
        }

        private IEnumerator DelayedRebindGraph()
        {
            yield return null;
            BindToNearestGraph();
            pendingRebindCoroutine = null;
        }

        /// <summary>
        /// fallback：将 AlanBot 移到 default tile 的可用 walkable cell
        /// </summary>
        private void RelocateToDefaultTile()
        {
            var wg = WorldGrid.Instance;
            if (wg == null) return;

            // 优先找 default tile，再找任意 tile
            FloorTileInstance targetTile = null;
            foreach (var t in wg.AllFloorTiles())
            {
                if (t.IsDefault && t.Interior != null)
                {
                    targetTile = t;
                    break;
                }
            }

            // 没有 default tile，找任意有 interior 的 tile
            if (targetTile == null)
            {
                foreach (var t in wg.AllFloorTiles())
                {
                    if (t.Interior != null)
                    {
                        targetTile = t;
                        break;
                    }
                }
            }

            if (targetTile == null || targetTile.Interior == null)
            {
                Debug.LogWarning("[AlanBotController] 无可用 FloorTile，AlanBot 保持原位");
                hostFloorTile = null;
                return;
            }

            Vector2Int cell = FindWalkableCell(targetTile);
            SnapToCellCenter(targetTile.Interior, cell);
            SetHostFloorTile(targetTile, cell);
            SavePosition();
        }

        /// <summary>
        /// 在 tile 的 Interior 中找一个 walkable 且未被占用的 cell
        /// </summary>
        private Vector2Int FindWalkableCell(FloorTileInstance tile)
        {
            var interior = tile.Interior;
            if (interior == null) return Vector2Int.zero;

            var size = interior.GridSize;
            // 优先找 walkable + 未占用
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    var pos = new Vector2Int(x, y);
                    if (interior.IsWalkable(pos) && !interior.IsOccupied(pos))
                        return pos;
                }
            }

            // 退而求其次：只找 walkable
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    var pos = new Vector2Int(x, y);
                    if (interior.IsWalkable(pos))
                        return pos;
                }
            }

            return Vector2Int.zero;
        }

        /// <summary>
        /// 将根节点移到 InteriorGrid cell 中心
        /// </summary>
        private void SnapToCellCenter(InteriorGrid interior, Vector2Int localPos)
        {
            Vector3 snapped = interior.LocalToWorld(localPos);
            float half = interior.CellSize * 0.5f;
            transform.position = new Vector3(snapped.x + half, snapped.y + half, 0f);
        }

        // ─── A* 绑定 ───

        /// <summary>
        /// 绑定到最近的A*图形，设置seeker的graphMask
        /// </summary>
        public void BindToNearestGraph()
        {
            if (AstarPath.active == null || seeker == null) return;

            var nearestInfo = AstarPath.active.GetNearest(transform.position);
            if (nearestInfo.node != null)
            {
                boundGraphIndex = nearestInfo.node.GraphIndex;
                seeker.graphMask = GraphMask.FromGraphIndex(boundGraphIndex);
            }
        }

        // ─── 行为树控制 ───

        /// <summary>
        /// 暂停行为树和移动
        /// </summary>
        public void PauseBehavior()
        {
            if (behaviourTreeOwner != null)
                behaviourTreeOwner.PauseBehaviour();

            if (aiLerp != null)
                aiLerp.isStopped = true;
        }

        /// <summary>
        /// 恢复行为树
        /// </summary>
        public void ResumeBehavior()
        {
            if (isInteracting || isBeingMoved) return;

            if (behaviourTreeOwner != null && behaviourTreeOwner.graph != null)
                behaviourTreeOwner.graph.Resume();
        }

        /// <summary>
        /// 放置完成后调用（重新绑定A*图形+保存位置）
        /// </summary>
        public void OnPlacementComplete()
        {
            isBeingMoved = false;
            BindToNearestGraph();
            SavePosition();
        }

        private void OnBuildPhaseStart()
        {
            PauseBehavior();
        }

        private void OnStoreOpen()
        {
            ResumeBehavior();
        }

        // ─── ES3 持久化 ───

        private void SavePosition()
        {
            try
            {
                ES3.Save<Vector3>(ES3_KEY_POSITION, transform.position, ES3_FILE);
                string tileId = (hostFloorTile != null) ? hostFloorTile.instanceId : "";
                ES3.Save<string>(ES3_KEY_HOST_TILE_ID, tileId, ES3_FILE);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AlanBotController] 保存位置失败: {e.Message}");
            }
        }

        private void LoadPosition()
        {
            try
            {
                if (ES3.KeyExists(ES3_KEY_POSITION, ES3_FILE))
                {
                    Vector3 savedPos = ES3.Load<Vector3>(ES3_KEY_POSITION, ES3_FILE);
                    transform.position = savedPos;
                }

                if (ES3.KeyExists(ES3_KEY_HOST_TILE_ID, ES3_FILE))
                    pendingHostTileId = ES3.Load<string>(ES3_KEY_HOST_TILE_ID, ES3_FILE);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AlanBotController] 加载位置失败（首次运行属正常情况）: {e.Message}");
            }
        }

        private void OnApplicationQuit()
        {
            SavePosition();
        }

        private void OnDestroy()
        {
            SavePosition();
        }
    }
}
