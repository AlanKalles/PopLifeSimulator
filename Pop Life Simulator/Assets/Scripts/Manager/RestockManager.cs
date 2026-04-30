using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PopLife.Runtime;
using PopLife.UI.Restock;

namespace PopLife
{
    /// <summary>
    /// 补货会话管理器：追踪玩家在本次建造阶段是否已补过货，
    /// 以及自上次补货以来是否新增了货架（新货架会重置 ready 状态）。
    ///
    /// 事件源：ConstructionManager.OnBuildingPlacedOrDestroyed（同一个事件覆盖 place/destroy/move/upgrade/FloorTile 等所有操作）
    /// 判定方式：比较 shelf instanceId 集合的差集——只有新 id 出现才算"新建货架"，其他变动不影响 ready。
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class RestockManager : MonoBehaviour
    {
        public static RestockManager Instance;

        [Header("场景引用")]
        [Tooltip("补货面板，RequestOpenWithoutRestock 会打开并显示 AlanBot 未补货提示")]
        [SerializeField] private RestockPanel restockPanel;

        // 会话级 flag：本次 BuildPhase 是否已经补过货
        private bool hasRestockedThisSession;

        // 上次补货时场上存在的 shelf id 快照
        private HashSet<string> shelvesKnownAtLastRestock = new HashSet<string>();

        private const string ES3_KEY_RESTOCKED = "RestockMgr_HasRestockedThisSession";
        private const string ES3_KEY_KNOWN_IDS = "RestockMgr_ShelvesKnownAtLastRestock";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            ConstructionManager.OnBuildingPlacedOrDestroyed += OnConstructionChanged;
        }

        private void OnDisable()
        {
            ConstructionManager.OnBuildingPlacedOrDestroyed -= OnConstructionChanged;
        }

        private void Start()
        {
            LoadFromES3();

            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBuildPhaseStart += OnBuildPhaseStart;
            }
        }

        private void OnDestroy()
        {
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBuildPhaseStart -= OnBuildPhaseStart;
            }
        }

        // ================================================================
        //  公共 API
        // ================================================================

        /// <summary>是否可以开店：已补过货 AND 自补货以来没有新 shelf；或者场上压根没 shelf。</summary>
        public bool IsReadyToOpen()
        {
            var currentIds = CollectCurrentShelfIds();

            // 场上没有任何货架 → 允许开空店
            if (currentIds.Count == 0) return true;

            if (!hasRestockedThisSession) return false;

            // 检查是否有新 id（补货后新建的货架）
            foreach (var id in currentIds)
            {
                if (!shelvesKnownAtLastRestock.Contains(id))
                    return false;
            }

            return true;
        }

        /// <summary>玩家成功点击 Restock 后调用：置 flag 为 true 并刷新已知货架快照。</summary>
        public void MarkRestocked()
        {
            hasRestockedThisSession = true;
            shelvesKnownAtLastRestock = CollectCurrentShelfIds();
            SaveToES3();
        }

        /// <summary>点击 OpenStore 时若尚未补货，打开 RestockPanel 并显示 AlanBot 提示。</summary>
        public void RequestOpenWithoutRestock()
        {
            if (restockPanel == null)
            {
                Debug.LogWarning("[RestockManager] restockPanel 未配置，无法显示 Not Ready 提示");
                return;
            }
            restockPanel.Open(showNotReadyPrompt: true);
        }

        // ================================================================
        //  事件响应
        // ================================================================

        private void OnConstructionChanged()
        {
            // 已经是未准备状态 → 不需要重复判定
            if (!hasRestockedThisSession) return;

            var currentIds = CollectCurrentShelfIds();

            // 检查是否有新 shelf id——只有新 id 才算"新建了货架"
            // 被卖掉（id 消失）、升级（id 不变，level 变）、移动（id 不变，floor 变）都不会出现新 id
            foreach (var id in currentIds)
            {
                if (!shelvesKnownAtLastRestock.Contains(id))
                {
                    hasRestockedThisSession = false;
                    // 注意：shelvesKnownAtLastRestock 故意保持不变——它仍然是"上次 ready"的快照
                    SaveToES3();
                    Debug.Log($"[RestockManager] 检测到新货架 {id}，ready 状态重置");
                    return;
                }
            }
        }

        private void OnBuildPhaseStart()
        {
            // 新的一天：重置 flag 并重新快照（否则第二天的老货架会被误判成"新货架"）
            hasRestockedThisSession = false;
            shelvesKnownAtLastRestock = CollectCurrentShelfIds();
            SaveToES3();
        }

        // ================================================================
        //  工具
        // ================================================================

        private static HashSet<string> CollectCurrentShelfIds()
        {
            var set = new HashSet<string>();
            var wg = WorldGrid.Instance;
            if (wg == null) return set;
            foreach (var shelf in wg.AllShelves())
            {
                if (shelf != null && !string.IsNullOrEmpty(shelf.instanceId))
                    set.Add(shelf.instanceId);
            }
            return set;
        }

        // ================================================================
        //  持久化
        // ================================================================

        private void SaveToES3()
        {
            ES3.Save(ES3_KEY_RESTOCKED, hasRestockedThisSession);
            ES3.Save(ES3_KEY_KNOWN_IDS, new List<string>(shelvesKnownAtLastRestock));
        }

        // 必须保留在 Start，不要移到 Awake：GameStateManager.ClearAllSaves 在 Awake 阶段删除 ES3 key，
        // 依赖此函数总是晚于 ClearAllSaves 才执行。如果改成在 Awake 加载，clearSaveOnStart 时会读到陈旧 key。
        private void LoadFromES3()
        {
            if (ES3.KeyExists(ES3_KEY_RESTOCKED))
                hasRestockedThisSession = ES3.Load<bool>(ES3_KEY_RESTOCKED);

            if (ES3.KeyExists(ES3_KEY_KNOWN_IDS))
            {
                var list = ES3.Load<List<string>>(ES3_KEY_KNOWN_IDS);
                shelvesKnownAtLastRestock = list != null ? new HashSet<string>(list) : new HashSet<string>();
            }
        }
    }
}
