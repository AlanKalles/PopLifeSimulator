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

                if (snapshot.behaviourTree != null)
                {
                    snapshot.treeWasRunning = snapshot.behaviourTree.isRunning;
                    snapshot.treeWasPaused = snapshot.behaviourTree.isPaused;
                    if (snapshot.treeWasRunning && !snapshot.treeWasPaused)
                    {
                        snapshot.behaviourTree.PauseBehaviour();
                    }
                }

                if (snapshot.aiLerp != null)
                {
                    snapshot.aiWasStopped = snapshot.aiLerp.isStopped;
                    snapshot.aiDestination = snapshot.aiLerp.destination;
                    snapshot.aiLerp.isStopped = true;
                }

                if (snapshot.destinationSetter != null)
                {
                    snapshot.destinationTarget = snapshot.destinationSetter.target;
                }

                if (snapshot.seeker != null)
                {
                    snapshot.graphMask = snapshot.seeker.graphMask;
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

                    if (!snapshot.aiWasStopped)
                    {
                        snapshot.aiLerp.SearchPath();
                    }
                }

                if (snapshot.behaviourTree != null
                    && snapshot.treeWasRunning
                    && !snapshot.treeWasPaused
                    && snapshot.behaviourTree.isPaused
                    && snapshot.behaviourTree.graph != null)
                {
                    snapshot.behaviourTree.graph.Resume();
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
