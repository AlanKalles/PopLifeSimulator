using UnityEngine;
using Pathfinding;
using NodeCanvas.BehaviourTrees;
using System;

namespace PopLife.AlanBot
{
    /// <summary>
    /// AlanBot核心控制器（单例）
    /// 管理A*图形绑定、行为树暂停/恢复、DayLoop事件订阅、ES3持久化
    /// </summary>
    public class AlanBotController : MonoBehaviour
    {
        public static AlanBotController Instance;

        [Header("组件引用")]
        [SerializeField] private AlanBotAnimator animator;

        [Header("地面设置")]
        [Tooltip("AlanBot脚底相对于楼层origin.y的偏移量")]
        [SerializeField] private float groundYOffset;

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

        // ES3持久化
        private const string ES3_KEY_POSITION = "alanBotPosition";
        private const string ES3_FILE = "AlanBot.es3";

        // 公开属性
        public float GroundYOffset => groundYOffset;
        public AILerp AiLerp => aiLerp;
        public AlanBotAnimator Animator => animator;

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
        }

        private void Start()
        {
            BindToNearestGraph();

            // 如果当前是建造阶段，暂停行为树
            if (DayLoopManager.Instance != null &&
                DayLoopManager.Instance.currentPhase == GamePhase.BuildPhase)
            {
                PauseBehavior();
            }
        }

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
