using System.Collections.Generic;
using NodeCanvas.BehaviourTrees;
using Pathfinding;
using PixelCrushers.DialogueSystem;
using PopLife.Customers.Runtime;
using UnityEngine;

namespace PopLife.DialogueBridge
{
    /// <summary>
    /// Dialogue System 对话期间暂停经营时钟与顾客 AI/A*，不修改 Time.timeScale。
    /// </summary>
    public class DialogueGameplayPauseService : MonoBehaviour
    {
        public static DialogueGameplayPauseService Instance { get; private set; }
        public static bool IsGameplayPaused => Instance != null && Instance.pauseDepth > 0;

        [Header("Audio Ducking")]
        [SerializeField] private bool duckMusicDuringDialogue = true;
        [SerializeField, Range(0f, 1f)] private float dialogueMusicVolumeMultiplier = 0.35f;

        private readonly Dictionary<CustomerAgent, CustomerPauseSnapshot> customerSnapshots = new();
        private int pauseDepth;
        private bool gameplayClockPaused;
        private bool subscribed;
        private bool musicDucked;
        private float cachedMusicVolume = 1f;

        private struct CustomerPauseSnapshot
        {
            public BehaviourTreeOwner behaviourTree;
            public bool treeWasRunning;
            public bool treeWasPaused;
            public AILerp aiLerp;
            public bool aiWasStopped;
            public Vector3 aiDestination;
            public AIDestinationSetter destinationSetter;
            public Transform destinationTarget;
            public Seeker seeker;
            public GraphMask graphMask;
        }

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
            TrySubscribe();
        }

        private void Start()
        {
            TrySubscribe();
        }

        private void Update()
        {
            if (!subscribed)
            {
                TrySubscribe();
            }
        }

        private void OnDisable()
        {
            if (subscribed && DialogueManager.instance != null)
            {
                DialogueManager.instance.conversationStarted -= OnConversationStarted;
                DialogueManager.instance.conversationEnded -= OnConversationEnded;
            }
            subscribed = false;

            ResumeGameplayIfNeeded(force: true);
            if (Instance == this) Instance = null;
        }

        private void TrySubscribe()
        {
            if (subscribed || DialogueManager.instance == null)
            {
                return;
            }

            DialogueManager.instance.conversationStarted += OnConversationStarted;
            DialogueManager.instance.conversationEnded += OnConversationEnded;
            subscribed = true;
        }

        private void OnConversationStarted(Transform actor)
        {
            pauseDepth++;
            if (pauseDepth == 1)
            {
                PauseGameplay();
            }
        }

        private void OnConversationEnded(Transform actor)
        {
            if (pauseDepth <= 0)
            {
                return;
            }

            pauseDepth--;
            if (pauseDepth == 0)
            {
                ResumeGameplayIfNeeded(force: false);
            }
        }

        public void PushExternalPause(CustomerAgent exemptCustomer = null)
        {
            pauseDepth++;
            if (pauseDepth == 1)
            {
                PauseGameplay(exemptCustomer);
            }
        }

        public void PopExternalPause()
        {
            if (pauseDepth <= 0)
            {
                return;
            }

            pauseDepth--;
            if (pauseDepth == 0)
            {
                ResumeGameplayIfNeeded(force: false);
            }
        }

        private void PauseGameplay(CustomerAgent exemptCustomer = null)
        {
            customerSnapshots.Clear();
            DayLoopManager.Instance?.PauseGameplayClock();
            gameplayClockPaused = true;
            DuckMusic();

            var customers = FindObjectsByType<CustomerAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var customer in customers)
            {
                if (customer == null) continue;
                if (exemptCustomer != null && customer == exemptCustomer) continue;

                var snapshot = new CustomerPauseSnapshot
                {
                    behaviourTree = customer.GetComponent<BehaviourTreeOwner>(),
                    aiLerp = customer.GetComponent<AILerp>(),
                    destinationSetter = customer.GetComponent<AIDestinationSetter>(),
                    seeker = customer.GetComponent<Seeker>()
                };

                // 关键顺序：必须先拍 AILerp/destinationSetter/seeker 快照，再 PauseBehaviour。
                // PauseBehaviour 会同步触发 ActionTask.OnPause（MoveToEntranceAction / MoveToExitAction /
                // ClubStayPointAction 都会立即把 aiLerp.isStopped 改成 true），如果先 PauseBehaviour 再拍快照，
                // snapshot.aiWasStopped 会被污染成 true，恢复时跳过 SearchPath，导致 customer 永远停滞。
                if (snapshot.aiLerp != null)
                {
                    snapshot.aiWasStopped = snapshot.aiLerp.isStopped;
                    snapshot.aiDestination = snapshot.aiLerp.destination;
                }

                if (snapshot.destinationSetter != null)
                {
                    snapshot.destinationTarget = snapshot.destinationSetter.target;
                }

                if (snapshot.seeker != null)
                {
                    snapshot.graphMask = snapshot.seeker.graphMask;
                }

                if (snapshot.behaviourTree != null)
                {
                    snapshot.treeWasRunning = snapshot.behaviourTree.isRunning;
                    snapshot.treeWasPaused = snapshot.behaviourTree.isPaused;
                    if (snapshot.treeWasRunning && !snapshot.treeWasPaused)
                    {
                        snapshot.behaviourTree.PauseBehaviour();
                    }
                }

                // 兜底：OnPause 已经做过 isStopped=true（对有 OnPause 的 Action），
                // 这里覆盖确保没有 OnPause 的 Action（如 MoveToTargetAction）也被暂停
                if (snapshot.aiLerp != null)
                {
                    snapshot.aiLerp.isStopped = true;
                }

                customerSnapshots[customer] = snapshot;
            }
        }

        private void ResumeGameplayIfNeeded(bool force)
        {
            if (!force && pauseDepth > 0)
            {
                return;
            }

            pauseDepth = 0;

            foreach (var pair in customerSnapshots)
            {
                var customer = pair.Key;
                if (customer == null) continue;

                var snapshot = pair.Value;

                // 1. 先恢复目标 / graphMask / destination / isStopped（按快照真实值）
                if (snapshot.destinationSetter != null)
                {
                    snapshot.destinationSetter.target = snapshot.destinationTarget;
                }

                if (snapshot.seeker != null)
                {
                    snapshot.seeker.graphMask = snapshot.graphMask;
                }

                if (snapshot.aiLerp != null)
                {
                    snapshot.aiLerp.destination = snapshot.aiDestination;
                    snapshot.aiLerp.isStopped = snapshot.aiWasStopped;
                }

                // 2. Resume BehaviourTree（同步触发 OnResume；OnResume 仅修改 isStopped 与内部计时，
                //    不动 destination/graphMask，不污染上面的全局快照恢复）
                if (snapshot.behaviourTree != null
                    && snapshot.treeWasRunning
                    && !snapshot.treeWasPaused
                    && snapshot.behaviourTree.isPaused
                    && snapshot.behaviourTree.graph != null)
                {
                    snapshot.behaviourTree.graph.Resume();
                }

                // 3. 兜底：总是 SearchPath() 刷新路径（不依赖 aiWasStopped）。
                //    AILerp 在 isStopped=true 期间会丢路径，恢复 isStopped=false 后若没有有效路径会原地卡死。
                //    SearchPath 仅刷新路径，不改变行为状态——本来该停的 customer (isStopped=true) 仍然停。
                if (snapshot.aiLerp != null)
                {
                    snapshot.aiLerp.SearchPath();
                }
            }

            customerSnapshots.Clear();
            if (gameplayClockPaused)
            {
                DayLoopManager.Instance?.ResumeGameplayClock();
                gameplayClockPaused = false;
            }

            RestoreMusic();
        }

        private void DuckMusic()
        {
            if (!duckMusicDuringDialogue || musicDucked || PopLife.AudioManager.Instance == null)
            {
                return;
            }

            cachedMusicVolume = PopLife.AudioManager.Instance.GetMusicVolume();
            PopLife.AudioManager.Instance.SetMusicVolume(cachedMusicVolume * dialogueMusicVolumeMultiplier);
            musicDucked = true;
        }

        private void RestoreMusic()
        {
            if (!musicDucked)
            {
                return;
            }

            if (PopLife.AudioManager.Instance != null)
            {
                PopLife.AudioManager.Instance.SetMusicVolume(cachedMusicVolume);
            }

            musicDucked = false;
        }
    }
}
