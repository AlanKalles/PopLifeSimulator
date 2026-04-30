using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PopLife.DialogueBridge;
using PopLife.Customers.Data;
using PopLife.Customers.Runtime;
using PopLife.Customers.Services;
using PopLife;


namespace PopLife.Customers.Spawner
{
    public class CustomerSpawner : MonoBehaviour
    {
        [Header("基础配置")]
        public GameObject customerPrefab; // 带 CustomerAgent + CustomerBlackboardAdapter

        [Header("生成点配置")]
        [Tooltip("多个生成点，随机选择")]
        public Transform[] spawnPoints;

        [Header("流量控制")]
        [Tooltip("随机生成间隔选项 (秒)")]
        public float[] spawnIntervalOptions = { 2f, 3f, 4f, 5f };

        [Tooltip("场上顾客基础数量上限（appeal 额外加成在此基础上叠加）")]
        public int baseCustomersOnFloor = 5;

        [Tooltip("防止顾客在同一天内离店后再次访问")]
        public bool preventSameDayRevisit = false;

        [Header("Store Appeal 影响")]
        [Tooltip("每增加多少 appeal 多允许1个顾客同时在场")]
        [SerializeField] private float appealPerExtraCustomer = 90f;

        [Tooltip("appeal 达到此值时生成间隔降到最短")]
        [SerializeField] private float appealForMaxSpeed = 2000f;

        [Tooltip("最短间隔倍率（0.55 = 间隔最短缩到原来的55%）")]
        [SerializeField] private float minIntervalMultiplier = 0.55f;

        [Header("节奏控制")]
        [Tooltip("开店后延迟多久开始生成第一个顾客 (秒)")]
        public float initialSpawnDelay = 5f;

        [Tooltip("在间隔基础上的随机抖动范围 (秒)")]
        public Vector2 spawnJitter = new Vector2(-1f, 1f);

        [Header("默认配置 (当找不到顾客记录时使用)")]
        public CustomerArchetype defaultArchetype;

        [Header("手动生成指定顾客")]
        [Tooltip("要生成的顾客ID")]
        public string targetCustomerId;

        [Tooltip("点击以生成指定ID的顾客")]
        public bool spawnTargetCustomer = false;

        [Header("调试信息")]
        [SerializeField] private int loadedCustomerCount = 0;
        [SerializeField] private string lastSpawnedCustomer = "";
        [SerializeField] private int currentCustomerCount = 0;
        [SerializeField] private float nextSpawnTime = 0f;
        [SerializeField] private bool isSpawning = false;
        [SerializeField] private int visitedTodayCount = 0;
        [SerializeField] private int effectiveMaxCustomers = 0;
        [SerializeField] private int spawnedTodayCount = 0;

        private CustomerRepository repository;
        private bool repositoryLoaded = false;
        private List<CustomerRecord> customerPool = new List<CustomerRecord>();
        private TimeBasedSpawnFilter timeFilter;
        private HashSet<string> activeCustomerIds = new HashSet<string>();
        private HashSet<string> visitedTodayCustomerIds = new HashSet<string>(); // 今天已访问过的顾客ID
        private SpawnerProfile spawnerProfile; // 运行时缓存的解锁配置
        private int cachedStoreAppeal = 0;

        void Awake()
        {
            repository = CustomerRepository.Instance;
            if (repository == null)
            {
                repository = FindFirstObjectByType<CustomerRepository>();
            }

            if (repository == null)
            {
                Debug.LogError("CustomerSpawner: 找不到 CustomerRepository！");
                return;
            }

            // 初始化时间过滤器
            timeFilter = new TimeBasedSpawnFilter(repository);
        }

        void OnEnable()
        {
            // 订阅DayLoopManager事件
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnStoreOpen += InitializeDailyPool;
                DayLoopManager.Instance.OnStopSpawning += StopSpawning;

                // 热加入：只有在营业阶段且已开店时才初始化
                if (DayLoopManager.Instance.currentPhase == GamePhase.OpenPhase &&
                    DayLoopManager.Instance.isStoreOpen)
                {
                    InitializeDailyPool();
                }
            }
            else
            {
                Debug.LogWarning("CustomerSpawner: 找不到 DayLoopManager，自动生成功能将不可用");
            }
        }

        void OnDisable()
        {
            // 退订事件，防止内存泄漏
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnStoreOpen -= InitializeDailyPool;
                DayLoopManager.Instance.OnStopSpawning -= StopSpawning;
            }
        }

        void Start()
        {
            LoadCustomerData();
        }

        void Update()
        {
            if (DialogueGameplayPauseService.IsGameplayPaused)
            {
                currentCustomerCount = GetCurrentCustomerCount();
                visitedTodayCount = visitedTodayCustomerIds.Count;
                effectiveMaxCustomers = GetEffectiveMaxCustomers();
                return;
            }

            // 手动生成逻辑
            if (spawnTargetCustomer)
            {
                spawnTargetCustomer = false;
                SpawnCustomerById(targetCustomerId);
            }

            // 自动生成逻辑
            if (isSpawning && Time.time >= nextSpawnTime)
            {
                TrySpawnCustomer();
            }

            // 更新当前场上人数和今日访问统计
            currentCustomerCount = GetCurrentCustomerCount();
            visitedTodayCount = visitedTodayCustomerIds.Count;
            effectiveMaxCustomers = GetEffectiveMaxCustomers();
        }

        void LoadCustomerData()
        {
            if (repository == null) return;

            // 确保Repository已加载数据（处理热加入情况）
            var allCustomers = repository.All().ToList();

            // 如果Repository为空，强制重新加载
            if (allCustomers.Count == 0)
            {
                Debug.Log("CustomerSpawner: Repository为空，尝试重新加载...");
                repository.Load();
                allCustomers = repository.All().ToList();
            }

            repositoryLoaded = true;
            loadedCustomerCount = allCustomers.Count;

            Debug.Log($"CustomerSpawner: Repository 中有 {loadedCustomerCount} 个顾客记录");

            if (loadedCustomerCount > 0)
            {
                Debug.Log("已加载的顾客ID列表:");
                foreach (var customer in allCustomers)
                {
                    Debug.Log($"  - {customer.customerId}: {customer.name}");
                }
            }
        }

        public void SpawnCustomerById(string customerId)
        {
            if (string.IsNullOrEmpty(customerId))
            {
                Debug.LogWarning("CustomerSpawner: 请输入要生成的顾客ID!");
                return;
            }

            if (!repositoryLoaded)
            {
                Debug.LogWarning("CustomerSpawner: 正在加载顾客数据，请稍后...");
                LoadCustomerData();
                return;
            }

            if (customerPrefab == null)
            {
                Debug.LogError("CustomerSpawner: 缺少顾客预制体!");
                return;
            }

            if (spawnPoints == null)
            {
                Debug.LogWarning("CustomerSpawner: 未设置生成点，将在原点生成");
            }

            CustomerRecord record = repository.Get(customerId);

            if (record == null)
            {
                Debug.LogError($"CustomerSpawner: 找不到ID为 '{customerId}' 的顾客记录!");
                Debug.Log($"当前已加载 {loadedCustomerCount} 个顾客，请检查ID是否正确");
                return;
            }

            // 尝试从记录中获取原型
            CustomerArchetype archetype = null;

            if (!string.IsNullOrEmpty(record.archetypeId))
            {
                // 从 Resources 文件夹加载原型
                archetype = Resources.Load<CustomerArchetype>($"ScriptableObjects/CustomerArchetypes/{record.archetypeId}");

                if (archetype == null)
                {
                    Debug.LogWarning($"CustomerSpawner: 找不到原型 '{record.archetypeId}'，将使用默认原型");
                }
            }

            // 如果没找到，使用默认原型
            if (archetype == null)
            {
                archetype = defaultArchetype;
            }

            // 如果还是没有原型，报错并返回
            if (archetype == null)
            {
                Debug.LogError($"CustomerSpawner: 无法为顾客 '{record.name}' (ID: {record.customerId}) 找到任何可用的原型！");
                Debug.LogError("请确保：1) 设置了默认原型，或 2) 顾客记录有有效的 archetypeId");
                return;
            }

            int daySeed = UnityEngine.Random.Range(0, int.MaxValue);

            // 获取生成点位置
            Transform selectedSpawnPoint = GetRandomSpawnPoint();
            Vector3 spawnPosition = selectedSpawnPoint != null ? selectedSpawnPoint.position : Vector3.zero;
            GameObject go = Instantiate(customerPrefab, spawnPosition, Quaternion.identity);

            CustomerAgent agent = go.GetComponent<CustomerAgent>();
            if (agent == null)
            {
                Debug.LogError("CustomerSpawner: 顾客预制体缺少 CustomerAgent 组件!");
                Destroy(go);
                return;
            }

            agent.Initialize(record, archetype, daySeed);

            // 设置生成点（用于离开）
            if (agent.bb != null)
            {
                agent.bb.spawnPoint = selectedSpawnPoint;

                // 同步到 NodeCanvas 黑板
#if NODECANVAS
                if (agent.bb.ncBlackboard != null)
                {
                    agent.bb.ncBlackboard.SetVariableValue("spawnPoint", selectedSpawnPoint);
                }
#endif
            }

            // 记录顾客访问到 DayLoopManager
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.RecordCustomerVisit();
            }

            lastSpawnedCustomer = $"{record.customerId}: {record.name}";
            Debug.Log($"成功生成顾客: {lastSpawnedCustomer}");
        }

        [ContextMenu("重新加载顾客数据")]
        public void ReloadCustomerData()
        {
            LoadCustomerData();
        }

        [ContextMenu("生成目标顾客")]
        public void SpawnTargetCustomerFromMenu()
        {
            SpawnCustomerById(targetCustomerId);
        }

        /// <summary>
        /// 清除运行时缓存的 SpawnerProfile 与已生成顾客池，下次访问时从磁盘重新加载。
        /// GameStateManager 重置存档时调用，避免 Save 时把陈旧解锁列表写回。
        /// </summary>
        public void InvalidateProfileCache()
        {
            spawnerProfile = null;
            customerPool.Clear();
        }

        /// <summary>
        /// 初始化每日顾客池（开店时调用）
        /// </summary>
        private void InitializeDailyPool()
        {
            Debug.Log("[CustomerSpawner] 开店，初始化每日顾客池");

            // 确保Repository已加载数据
            if (!repositoryLoaded)
            {
                LoadCustomerData();
            }

            // 1. 加载SpawnerProfile
            var profile = SpawnerProfile.Load();

            // 2. 根据解锁ID从Repository获取records
            customerPool.Clear();
            foreach (var customerId in profile.unlockedCustomerIds)
            {
                var record = repository.Get(customerId);
                if (record != null)
                {
                    customerPool.Add(record);
                }
                else
                {
                    Debug.LogWarning($"[CustomerSpawner] 解锁列表中的顾客 {customerId} 在Repository中未找到");
                }
            }

            Debug.Log($"[CustomerSpawner] 顾客池初始化完成，共 {customerPool.Count} 个可生成顾客");

            // 3. 清空今日访问记录（新的一天开始）
            visitedTodayCustomerIds.Clear();
            spawnedTodayCount = 0;

            // 4. 缓存当天的 Store Appeal（建造阶段决定当天客流）
            cachedStoreAppeal = ResourceManager.Instance != null
                ? ResourceManager.Instance.GetStoreAppeal() : 0;
            Debug.Log($"[CustomerSpawner] 当日 Store Appeal: {cachedStoreAppeal}");

            // 5. 重置生成计时器（加上初始延迟）
            nextSpawnTime = Time.time + initialSpawnDelay;
            isSpawning = true;
        }

        /// <summary>
        /// 停止生成（关店时调用）
        /// </summary>
        private void StopSpawning()
        {
            Debug.Log("[CustomerSpawner] 关店，停止自动生成，设置所有顾客闭店状态");
            isSpawning = false;

            // 设置所有在场顾客的 isClosingTime = true
            var allCustomers = FindObjectsByType<CustomerAgent>(FindObjectsSortMode.None);
            Debug.Log($"[CustomerSpawner] 当前场上有 {allCustomers.Length} 个顾客");

            foreach (var customer in allCustomers)
            {
                var bb = customer.GetComponent<CustomerBlackboardAdapter>();
                if (bb != null)
                {
                    bb.isClosingTime = true;

                    // 同步到 NodeCanvas 黑板
#if NODECANVAS
                    if (bb.ncBlackboard != null)
                    {
                        bb.ncBlackboard.SetVariableValue("isClosingTime", true);
                    }
#endif

                    Debug.Log($"[CustomerSpawner] 设置顾客 {bb.customerId} 闭店状态 (pendingPayment: ${bb.pendingPayment})");
                }
            }

            activeCustomerIds.Clear();
        }

        /// <summary>
        /// 尝试生成顾客（自动生成主逻辑）
        /// </summary>
        private void TrySpawnCustomer()
        {
            // 1. 检查是否超过场上人数上限（基础值 + appeal 加成）
            int effectiveMax = GetEffectiveMaxCustomers();
            if (currentCustomerCount >= effectiveMax)
            {
                ScheduleNextSpawn();
                return;
            }

            // 2. 更新场上顾客ID缓存
            UpdateActiveCustomerIds();

            // Day 1 教程可强制指定第 3 个自动生成顾客，绕过 SpawnerProfile 解锁池。
            int upcomingSpawnNumber = spawnedTodayCount + 1;
            if (TryGetForcedDay1TutorialSpawn(upcomingSpawnNumber, out var forcedCustomer))
            {
                if (SpawnCustomer(forcedCustomer))
                {
                    spawnedTodayCount++;
                }

                ScheduleNextSpawn();
                return;
            }

            // 3. 检查顾客池是否为空
            if (customerPool.Count == 0)
            {
                Debug.LogWarning("[CustomerSpawner] 顾客池为空，无法生成");
                ScheduleNextSpawn();
                return;
            }

            // 4. 检查是否所有顾客池成员都已在场
            if (activeCustomerIds.Count >= customerPool.Count)
            {
                Debug.Log("[CustomerSpawner] 顾客池中所有顾客都已在场，等待顾客离开");
                ScheduleNextSpawn();
                return;
            }

            // 5. 获取当前游戏时间
            float currentHour = GetCurrentGameHour();

            // 6. 使用时间过滤器筛选符合条件的顾客
            var eligibleCustomers = timeFilter.GetEligibleCustomers(customerPool, currentHour);

            if (eligibleCustomers.Count == 0)
            {
                Debug.Log($"[CustomerSpawner] 当前时间 {currentHour:F2} 没有符合条件的顾客");
                ScheduleNextSpawn();
                return;
            }

            // 7. 过滤掉已在场的顾客
            eligibleCustomers.RemoveAll(wc => activeCustomerIds.Contains(wc.record.customerId));

            // 8. 如果启用了防止同日重复访问，过滤掉今天已访问过的顾客
            if (preventSameDayRevisit)
            {
                eligibleCustomers.RemoveAll(wc => visitedTodayCustomerIds.Contains(wc.record.customerId));
            }

            if (eligibleCustomers.Count == 0)
            {
                string reason = preventSameDayRevisit
                    ? "[CustomerSpawner] 所有符合时间条件的顾客都已在场或今天已访问过"
                    : "[CustomerSpawner] 所有符合时间条件的顾客都已在场";
                Debug.Log(reason);
                ScheduleNextSpawn();
                return;
            }

            // 9. 加权随机选择顾客
            var selectedCustomer = WeightedRandom(eligibleCustomers);

            // 10. 生成顾客
            if (SpawnCustomer(selectedCustomer))
            {
                spawnedTodayCount++;
            }

            // 11. 安排下次生成时间
            ScheduleNextSpawn();
        }

        /// <summary>
        /// 根据 Store Appeal 计算实际顾客上限
        /// </summary>
        private int GetEffectiveMaxCustomers()
        {
            int bonus = appealPerExtraCustomer > 0
                ? Mathf.FloorToInt(cachedStoreAppeal / appealPerExtraCustomer)
                : 0;
            return baseCustomersOnFloor + bonus;
        }

        /// <summary>
        /// 安排下次生成时间
        /// </summary>
        private void ScheduleNextSpawn()
        {
            // 从间隔选项中随机选择基础间隔
            float baseInterval = spawnIntervalOptions[UnityEngine.Random.Range(0, spawnIntervalOptions.Length)];

            // 加上随机抖动
            float jitter = UnityEngine.Random.Range(spawnJitter.x, spawnJitter.y);

            // Store Appeal 影响：appeal 越高，间隔越短
            float appealRatio = appealForMaxSpeed > 0
                ? Mathf.Clamp01(cachedStoreAppeal / appealForMaxSpeed)
                : 0f;
            float intervalMul = Mathf.Lerp(1f, minIntervalMultiplier, appealRatio);

            // 全局修饰器：spawnRate > 1 → 间隔缩短（反比关系）
            float spawnRateMul = GlobalModifierManager.Instance != null
                ? GlobalModifierManager.Instance.GetSpawnRateMultiplier()
                : 1f;
            float finalInterval = Mathf.Max(0.1f, (baseInterval + jitter) * intervalMul / Mathf.Max(0.01f, spawnRateMul));
            nextSpawnTime = Time.time + finalInterval;
        }

        /// <summary>
        /// 加权随机算法
        /// </summary>
        private CustomerRecord WeightedRandom(List<WeightedCustomer> weighted)
        {
            if (weighted.Count == 0)
                return null;

            // 计算总权重
            float totalWeight = 0f;
            foreach (var wc in weighted)
                totalWeight += wc.finalWeight;

            // 随机选择
            float rand = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var wc in weighted)
            {
                cumulative += wc.finalWeight;
                if (rand <= cumulative)
                    return wc.record;
            }

            // fallback
            return weighted[weighted.Count - 1].record;
        }

        private bool TryGetForcedDay1TutorialSpawn(int upcomingSpawnNumber, out CustomerRecord record)
        {
            record = null;

            var tutorialController = Day1InteractionTutorialController.Instance;
            if (tutorialController == null || !tutorialController.ShouldForceDay1ThirdSpawn(upcomingSpawnNumber))
            {
                return false;
            }

            string customerId = tutorialController.ForcedDay1ThirdSpawnCustomerId.Trim();
            if (!repositoryLoaded)
            {
                LoadCustomerData();
            }

            record = repository.Get(customerId);
            if (record == null)
            {
                Debug.LogError($"[CustomerSpawner] Day 1 tutorial forced customer '{customerId}' was not found in CustomerRepository. Falling back to normal spawn.");
                return false;
            }

            Debug.Log($"[CustomerSpawner] Day 1 tutorial forcing auto-spawn #{upcomingSpawnNumber}: {customerId}");
            return true;
        }

        /// <summary>
        /// 生成顾客实例（内部方法）
        /// </summary>
        private bool SpawnCustomer(CustomerRecord record)
        {
            if (record == null)
            {
                Debug.LogError("[CustomerSpawner] 尝试生成空的顾客记录");
                return false;
            }

            if (customerPrefab == null)
            {
                Debug.LogError("[CustomerSpawner] 缺少顾客预制体!");
                return false;
            }

            // 随机选择生成点
            Transform spawnPoint = GetRandomSpawnPoint();
            Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : Vector3.zero;

            // 加载Archetype
            CustomerArchetype archetype = LoadArchetype(record.archetypeId);
            if (archetype == null)
            {
                archetype = defaultArchetype;
            }

            if (archetype == null)
            {
                Debug.LogError($"[CustomerSpawner] 无法为顾客 '{record.name}' 找到原型");
                return false;
            }

            // 实例化
            int daySeed = UnityEngine.Random.Range(0, int.MaxValue);
            GameObject go = Instantiate(customerPrefab, spawnPosition, Quaternion.identity);

            CustomerAgent agent = go.GetComponent<CustomerAgent>();
            if (agent == null)
            {
                Debug.LogError("[CustomerSpawner] 顾客预制体缺少 CustomerAgent 组件!");
                Destroy(go);
                return false;
            }

            agent.Initialize(record, archetype, daySeed);

            // 设置生成点（用于最终撤离）
            if (agent.bb != null)
            {
                agent.bb.spawnPoint = spawnPoint;

                // 设置商店入口锚点
                if (EntranceManager.Instance != null)
                {
                    var entrance = EntranceManager.Instance.GetMainEntrance();
                    if (entrance != null)
                    {
                        agent.bb.entranceOutsideAnchor = entrance.outsideAnchor;
                        agent.bb.entranceInsideAnchor = entrance.insideAnchor;
                        Debug.Log($"[CustomerSpawner] 顾客 {record.customerId} 入口已设置: {entrance.entranceId}");
                    }
                    else
                    {
                        Debug.LogWarning($"[CustomerSpawner] 无法为顾客 {record.customerId} 设置入口：EntranceManager 没有可用入口");
                    }
                }
                else
                {
                    Debug.LogWarning("[CustomerSpawner] EntranceManager 未找到，无法设置入口锚点");
                }

                // 同步到 NodeCanvas 黑板
#if NODECANVAS
                if (agent.bb.ncBlackboard != null)
                {
                    agent.bb.ncBlackboard.SetVariableValue("spawnPoint", spawnPoint);
                    agent.bb.ncBlackboard.SetVariableValue("entranceOutsideAnchor", agent.bb.entranceOutsideAnchor);
                    agent.bb.ncBlackboard.SetVariableValue("entranceInsideAnchor", agent.bb.entranceInsideAnchor);
                    agent.bb.ncBlackboard.SetVariableValue("hasEnteredStore", false);
                }
#endif
            }

            // 记录访问
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.RecordCustomerVisit();
            }

            // 记录今日已访问（用于防止同日重复访问）
            if (preventSameDayRevisit && !string.IsNullOrEmpty(record.customerId))
            {
                visitedTodayCustomerIds.Add(record.customerId);
                Debug.Log($"[CustomerSpawner] 记录顾客 {record.customerId} 今日访问 (防止重复: {preventSameDayRevisit})");
            }

            // ── 【隐患修正：对象池脏数据重置】 ──
            // 即使当前顾客是 Destroy(gameObject) 而非对象池复用，
            // 在分配前重置所有交互状态是防御性编码。
            // 如果未来改为对象池复用，不会出现 interactionUsedToday 残留为 true
            // 导致该 GameObject 永远无法再次触发交互的问题。
            if (agent.bb != null)
            {
                agent.bb.assignedInteraction = null;
                agent.bb.interactionUsedToday = false;
                agent.bb.interactionBubbleActive = false;
                agent.bb.interactionDialogueStarted = false;
            }

            lastSpawnedCustomer = $"{record.customerId}: {record.name}";
            Debug.Log($"[CustomerSpawner] 自动生成顾客: {lastSpawnedCustomer}");
            return true;
        }

        /// <summary>
        /// 获取随机生成点
        /// </summary>
        private Transform GetRandomSpawnPoint()
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogWarning("[CustomerSpawner] 未设置生成点数组，使用默认位置");
                return null;
            }

            return spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
        }

        /// <summary>
        /// 获取当前游戏时间（24小时制）
        /// </summary>
        private float GetCurrentGameHour()
        {
            if (DayLoopManager.Instance != null)
            {
                return DayLoopManager.Instance.currentHour;
            }

            // 默认返回营业时间中段
            return 17f;
        }

        /// <summary>
        /// 获取场上当前顾客数量
        /// </summary>
        private int GetCurrentCustomerCount()
        {
            return FindObjectsByType<CustomerAgent>(FindObjectsSortMode.None).Length;
        }

        /// <summary>
        /// 更新场上顾客ID缓存
        /// </summary>
        private void UpdateActiveCustomerIds()
        {
            activeCustomerIds.Clear();
            var agents = FindObjectsByType<CustomerAgent>(FindObjectsSortMode.None);
            foreach (var agent in agents)
            {
                if (!string.IsNullOrEmpty(agent.customerID))
                {
                    activeCustomerIds.Add(agent.customerID);
                }
            }
        }

        /// <summary>
        /// 加载Archetype
        /// </summary>
        private CustomerArchetype LoadArchetype(string archetypeId)
        {
            if (string.IsNullOrEmpty(archetypeId))
                return null;

            var archetype = Resources.Load<CustomerArchetype>($"ScriptableObjects/CustomerArchetypes/{archetypeId}");

            if (archetype == null)
            {
                var allArchetypes = Resources.LoadAll<CustomerArchetype>("ScriptableObjects");
                foreach (var a in allArchetypes)
                {
                    if (a.archetypeId == archetypeId || a.name == archetypeId)
                    {
                        return a;
                    }
                }
            }

            return archetype;
        }

        /// <summary>
        /// 运行时解锁顾客（不立刻刷新顾客池）
        /// - Build Phase 解锁 → 当晚开店时生效
        /// - Open Phase 解锁 → 次日开店时生效
        /// </summary>
        public void UnlockCustomer(string customerId)
        {
            if (string.IsNullOrEmpty(customerId))
            {
                Debug.LogWarning("[CustomerSpawner] Cannot unlock customer with empty ID");
                return;
            }

            // 懒加载 SpawnerProfile
            if (spawnerProfile == null)
            {
                spawnerProfile = SpawnerProfile.Load();
            }

            // 解锁顾客
            spawnerProfile.UnlockCustomer(customerId);

            // 保存到 persistentDataPath（Build 中可持久化）
            spawnerProfile.Save();

            // 提示生效时机
            if (DayLoopManager.Instance != null && DayLoopManager.Instance.currentPhase == GamePhase.BuildPhase)
            {
                Debug.Log($"[CustomerSpawner] Unlocked customer '{customerId}' during Build Phase. They will visit when store opens today.");
            }
            else
            {
                Debug.Log($"[CustomerSpawner] Unlocked customer '{customerId}' during Open Phase. They will visit from next day.");
            }
        }

        /// <summary>
        /// 批量解锁顾客（不立刻刷新顾客池）
        /// </summary>
        public void UnlockCustomers(IEnumerable<string> customerIds)
        {
            if (customerIds == null)
            {
                Debug.LogWarning("[CustomerSpawner] Cannot unlock null customer list");
                return;
            }

            // 懒加载 SpawnerProfile
            if (spawnerProfile == null)
            {
                spawnerProfile = SpawnerProfile.Load();
            }

            int count = 0;
            foreach (var customerId in customerIds)
            {
                if (!string.IsNullOrEmpty(customerId))
                {
                    spawnerProfile.UnlockCustomer(customerId);
                    count++;
                }
            }

            // 保存到 persistentDataPath
            spawnerProfile.Save();

            Debug.Log($"[CustomerSpawner] Unlocked {count} customers. They will take effect on next store open.");
        }

        /// <summary>
        /// 检查顾客是否已解锁
        /// </summary>
        public bool IsCustomerUnlocked(string customerId)
        {
            if (spawnerProfile == null)
            {
                spawnerProfile = SpawnerProfile.Load();
            }

            return spawnerProfile.IsUnlocked(customerId);
        }

        /// <summary>
        /// 锁定顾客（运行时）
        /// </summary>
        public void LockCustomer(string customerId)
        {
            if (spawnerProfile == null)
            {
                spawnerProfile = SpawnerProfile.Load();
            }

            spawnerProfile.LockCustomer(customerId);
            spawnerProfile.Save();

            Debug.Log($"[CustomerSpawner] Locked customer '{customerId}'");
        }
    }
}
